using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Gears.World.Components;

namespace Gears.Graphics
{
    /// <summary>Where one light's shadow data landed for this frame — -1 index means "no shadow this frame".</summary>
    public struct ShadowAssignment
    {
        public int ShadowIndex;
        public Matrix4 LightSpaceMatrix; // directional/spot only; identity for point
        public float Near;
        public float Far;

        public static readonly ShadowAssignment None = new ShadowAssignment
        {
            ShadowIndex = -1,
            LightSpaceMatrix = Matrix4.Identity
        };
    }

    /// <summary>
    /// Renders shadow maps for every shadow-casting light, once per frame, into two shared GPU
    /// arrays: a 2D array (directional + spot, one layer per light) and a cube array (point,
    /// six layers per light). Both arrays are fixed-slot-count and fixed-resolution — resolution
    /// grows to fit the largest requested Light.shadowResolution among this frame's casters, and
    /// only reallocates when that changes. Lights past the slot count simply don't get a shadow
    /// this frame (ShadowIndex stays -1) rather than the array growing without bound.
    ///
    /// Cost note: every shadow-casting light re-renders the full opaque queue, unculled, every
    /// frame. Fine for a handful of lights; a lot of shadow casters at once will get expensive
    /// fast (up to 8 + 4*6 = 32 extra full scene passes/frame at the current caps).
    /// </summary>
    public class ShadowMapRenderer : IDisposable
    {
        public const int MaxShadowMaps2D = 8;
        public const int MaxShadowMapsCube = 4;

        private const int MinResolution = 512;
        private const float DirectionalShadowRadius = 40f;
        private const float DirectionalShadowFar = 200f;

        private int _tex2DArray = -1;
        private int _fbo2D = -1;
        private int _res2D = -1;

        private int _cubeArrayTex = -1;
        private int _fboCube = -1;
        private int _cubeDepthRbo = -1;
        private int _resCube = -1;

        private readonly Shader _shadowDepthShader; // position-only, no fragment — directional/spot
        private readonly Shader _shadowCubeShader;  // writes linear distance-to-light — point

        private ShadowAssignment[] _assignmentScratch = Array.Empty<ShadowAssignment>();

        public int ShadowMap2DHandle => _tex2DArray;
        public int ShadowMapCubeHandle => _cubeArrayTex;

        // Standard OpenGL cube map face order (+X,-X,+Y,-Y,+Z,-Z) and matching up vectors —
        // must agree with how the fragment shader's cubemap direction lookup expects faces laid
        // out, same convention EProbe.cs uses for its own cube captures.
        private static readonly Vector3[] Directions =
        {
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, -1, 0),
            new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        };

        private static readonly Vector3[] UpVectors =
        {
            new Vector3(0, -1, 0), new Vector3(0, -1, 0),
            new Vector3(0, 0, 1),  new Vector3(0, 0, -1),
            new Vector3(0, -1, 0), new Vector3(0, -1, 0)
        };

        public ShadowMapRenderer(Shader shadowDepthShader, Shader shadowCubeShader)
        {
            _shadowDepthShader = shadowDepthShader;
            _shadowCubeShader = shadowCubeShader;
        }

        // Algorithm: shadow map slot assignment — picks up to MaxShadowMaps2D directional/spot
        // casters and MaxShadowMapsCube point casters, then renders each into its own array
        // slot from the light's point of view (standard shadow mapping).
        public ShadowAssignment[] Run(IReadOnlyList<LightRenderData> lights, List<Game.RenderRequest> queue,
            Func<SubMesh, DrawMesh> getOrCreateDrawMesh, Vector3 directionalAnchor)
        {
            if (_assignmentScratch.Length < lights.Count)
                _assignmentScratch = new ShadowAssignment[lights.Count];

            for (int i = 0; i < lights.Count; i++)
                _assignmentScratch[i] = ShadowAssignment.None;

            int maxRes2D = MinResolution;
            int maxResCube = MinResolution;
            int want2D = 0;
            int wantCube = 0;

            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (!l.CastsShadows) continue;

                if (l.Type == 1) // Point
                {
                    if (wantCube >= MaxShadowMapsCube) continue;
                    maxResCube = Math.Max(maxResCube, l.ShadowResolution);
                    wantCube++;
                }
                else
                {
                    if (want2D >= MaxShadowMaps2D) continue;
                    maxRes2D = Math.Max(maxRes2D, l.ShadowResolution);
                    want2D++;
                }
            }

            if (want2D > 0) EnsureCapacity2D(maxRes2D);
            if (wantCube > 0) EnsureCapacityCube(maxResCube);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            int index2D = 0;
            int indexCube = 0;

            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (!l.CastsShadows) continue;

                if (l.Type == 1) // Point
                {
                    if (indexCube >= MaxShadowMapsCube) continue;

                    float near = Math.Max(l.ShadowNearPlane, 0.01f);
                    float far = Math.Max(l.Range, near + 1f);

                    RenderPointShadow(l, indexCube, near, far, queue, getOrCreateDrawMesh);

                    _assignmentScratch[i] = new ShadowAssignment
                    {
                        ShadowIndex = indexCube,
                        LightSpaceMatrix = Matrix4.Identity,
                        Near = near,
                        Far = far
                    };
                    indexCube++;
                }
                else
                {
                    if (index2D >= MaxShadowMaps2D) continue;

                    Matrix4 view, proj;
                    float near, far;

                    if (l.Type == 0) BuildDirectionalViewProj(l, directionalAnchor, out view, out proj, out near, out far);
                    else BuildSpotViewProj(l, out view, out proj, out near, out far);

                    RenderShadowMap2D(view, proj, index2D, queue, getOrCreateDrawMesh);

                    _assignmentScratch[i] = new ShadowAssignment
                    {
                        ShadowIndex = index2D,
                        LightSpaceMatrix = view * proj, // view applied first, then proj — matches this codebase's model*view*projection convention
                        Near = near,
                        Far = far
                    };
                    index2D++;
                }
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            return _assignmentScratch;
        }

        // Algorithm: camera-centered fixed-radius orthographic directional shadow frustum. Not a
        // real fitted/cascaded frustum — just a box of fixed size following the camera. Good
        // enough for one directional light over a small-to-medium scene.
        private void BuildDirectionalViewProj(LightRenderData l, Vector3 anchor, out Matrix4 view, out Matrix4 proj, out float near, out float far)
        {
            Vector3 dir = l.Direction.LengthSquared > 1e-6f ? Vector3.Normalize(l.Direction) : -Vector3.UnitY;
            Vector3 eye = anchor - dir * (DirectionalShadowFar * 0.5f);
            Vector3 up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;

            view = Matrix4.LookAt(eye, eye + dir, up);
            near = 0.1f;
            far = DirectionalShadowFar;
            proj = Matrix4.CreateOrthographicOffCenter(-DirectionalShadowRadius, DirectionalShadowRadius,
                -DirectionalShadowRadius, DirectionalShadowRadius, near, far);
        }

        private void BuildSpotViewProj(LightRenderData l, out Matrix4 view, out Matrix4 proj, out float near, out float far)
        {
            Vector3 dir = l.Direction.LengthSquared > 1e-6f ? Vector3.Normalize(l.Direction) : -Vector3.UnitY;
            Vector3 up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;

            view = Matrix4.LookAt(l.Position, l.Position + dir, up);
            near = Math.Max(l.ShadowNearPlane, 0.01f);
            far = Math.Max(l.Range, near + 1f);

            float fov = MathF.Acos(Math.Clamp(l.OuterConeCos, -1f, 1f)) * 2f;
            fov = Math.Clamp(fov, MathHelper.DegreesToRadians(1f), MathHelper.DegreesToRadians(179f));
            proj = Camera.CreatePerspective(fov, 1f, near, far);
        }

        private void RenderShadowMap2D(Matrix4 view, Matrix4 proj, int layer, List<Game.RenderRequest> queue, Func<SubMesh, DrawMesh> getOrCreateDrawMesh)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo2D);
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _tex2DArray, 0, layer);
            GL.Viewport(0, 0, _res2D, _res2D);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            _shadowDepthShader.UseShader();
            _shadowDepthShader.SetUniform("view", view);
            _shadowDepthShader.SetUniform("projection", proj);

            foreach (var req in queue)
            {
                if (req.Transparent) continue;
                var mesh = getOrCreateDrawMesh(req.subMesh);
                mesh.DrawDepthOnly(_shadowDepthShader, req.ModelMatrix);
            }
        }

        private void RenderPointShadow(LightRenderData l, int arrayIndex, float near, float far, List<Game.RenderRequest> queue, Func<SubMesh, DrawMesh> getOrCreateDrawMesh)
        {
            Matrix4 proj = Camera.CreatePerspective(MathHelper.DegreesToRadians(90f), 1f, near, far);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboCube);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _cubeDepthRbo);
            GL.Viewport(0, 0, _resCube, _resCube);

            _shadowCubeShader.UseShader();
            _shadowCubeShader.SetUniform("lightPos", l.Position);
            _shadowCubeShader.SetUniform("projection", proj);

            for (int face = 0; face < 6; face++)
            {
                int layer = arrayIndex * 6 + face;
                GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _cubeArrayTex, 0, layer);
                GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

                Matrix4 view = Matrix4.LookAt(l.Position, l.Position + Directions[face], UpVectors[face]);
                _shadowCubeShader.SetUniform("view", view);

                foreach (var req in queue)
                {
                    if (req.Transparent) continue;
                    var mesh = getOrCreateDrawMesh(req.subMesh);
                    mesh.DrawDepthOnly(_shadowCubeShader, req.ModelMatrix);
                }
            }
        }

        private void EnsureCapacity2D(int resolution)
        {
            if (resolution == _res2D && _tex2DArray != -1) return;
            DeleteArray2D();

            _res2D = resolution;
            _tex2DArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, _tex2DArray);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.DepthComponent32f,
                resolution, resolution, MaxShadowMaps2D, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBorderColor, new float[] { 1f, 1f, 1f, 1f });
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

            if (_fbo2D == -1) _fbo2D = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo2D);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void EnsureCapacityCube(int resolution)
        {
            if (resolution == _resCube && _cubeArrayTex != -1) return;
            DeleteArrayCube();

            _resCube = resolution;
            _cubeArrayTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMapArray, _cubeArrayTex);
            GL.TexImage3D(TextureTarget.TextureCubeMapArray, 0, PixelInternalFormat.R32f,
                resolution, resolution, MaxShadowMapsCube * 6, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.TextureCubeMapArray, 0);

            if (_cubeDepthRbo == -1) _cubeDepthRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _cubeDepthRbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, resolution, resolution);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            if (_fboCube == -1) _fboCube = GL.GenFramebuffer();
        }

        private void DeleteArray2D()
        {
            if (_tex2DArray != -1) { GL.DeleteTexture(_tex2DArray); _tex2DArray = -1; }
        }

        private void DeleteArrayCube()
        {
            if (_cubeArrayTex != -1) { GL.DeleteTexture(_cubeArrayTex); _cubeArrayTex = -1; }
        }

        public void Dispose()
        {
            DeleteArray2D();
            DeleteArrayCube();
            if (_fbo2D != -1) GL.DeleteFramebuffer(_fbo2D);
            if (_fboCube != -1) GL.DeleteFramebuffer(_fboCube);
            if (_cubeDepthRbo != -1) GL.DeleteRenderbuffer(_cubeDepthRbo);
            GC.SuppressFinalize(this);
        }
    }
}