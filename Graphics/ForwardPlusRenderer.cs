using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Gears.Graphics
{
    /// <summary>
    /// GPU-side mirror of LightRenderData, laid out to match the `Light` struct declared in
    /// LightCulling.shader and BaseFragment.shader (std430: every field is a vec4/mat4 to avoid
    /// the alignment surprises vec3 causes in std430 layouts). If you change this, update both
    /// shaders' Light struct to match byte-for-byte, even for fields the culling shader ignores —
    /// culling still needs the correct array stride to index lights[] correctly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GPULight
    {
        public Vector4 Position;   // xyz = world position, w = range
        public Vector4 Color;      // rgb = color (intensity-premultiplied), w = intensity
        public Vector4 Direction;  // xyz = direction, w = type (0=Directional,1=Point,2=Spot,3=Area)
        public Vector4 Params;     // x = innerConeCos, y = outerConeCos, z = shadowStrength, w = renderingLayers
        public Vector4 Size;       // xy = size (area lights), z = shadow near plane, w = shadow far plane
        public Vector4 ShadowA;    // x = shadowIndex (-1 = none), y = bias, z = normalBias, w = soft (0/1)
        public Matrix4 LightSpaceMatrix; // directional/spot only; identity otherwise (point uses cube distance test, no matrix needed)

        public GPULight(LightRenderData l, ShadowAssignment shadow)
        {
            Position = new Vector4(l.Position, l.Range);
            Color = new Vector4(l.Color, l.Intensity);
            Direction = new Vector4(l.Direction, (float)l.Type);
            Params = new Vector4(l.InnerConeCos, l.OuterConeCos, l.ShadowStrength, (float)l.RenderingLayers);
            Size = new Vector4(l.Size.X, l.Size.Y, shadow.Near, shadow.Far);
            ShadowA = new Vector4(shadow.ShadowIndex, l.ShadowBias, l.ShadowNormalBias, l.SoftShadows ? 1f : 0f);
            LightSpaceMatrix = shadow.LightSpaceMatrix;
        }
    }

    /// <summary>
    /// Tile-based (clustered) light culling for Forward+ rendering:
    ///  1. A depth-only pre-pass over opaque geometry, into a sampleable depth texture.
    ///  2. A compute shader that, per screen tile, reads that tile's min/max depth back from the
    ///     pre-pass, builds the tile's view-space frustum, and writes a light-index list for every
    ///     light that overlaps it.
    ///  3. The main fragment shader looks up its tile's light-index list directly from the SSBO
    ///     instead of receiving a flat, capped light array as uniforms.
    ///
    /// Scope note: culling runs once per frame for a single view/projection (the main camera).
    /// RenderTexture-backed cameras and EProbe cubemap faces read the same result rather than each
    /// getting their own culling pass — fine for the common case, but approximate for those. Calling
    /// Run(...) again with a different view/projection before such a pass would make it exact, at
    /// the cost of another depth pass + compute dispatch per view.
    /// </summary>
    public class ForwardPlusRenderer : IDisposable
    {
        public const int TileSize = 16;
        public const int MaxLightsPerTile = 256;

        private int _depthFBO;
        private Texture _depthTexture;
        private Vector2i _screenSize;
        private bool _disposed;

        private readonly SSBO _lightSSBO = new();
        private readonly SSBO _tileLightSSBO = new();
        private GPULight[] _lightScratch = Array.Empty<GPULight>();

        private readonly Shader _depthPrepassShader;
        private readonly Shader _lightCullingShader;

        public int TileCountX { get; private set; }
        public int TileCountY { get; private set; }
        public Texture DepthTexture => _depthTexture;

        public ForwardPlusRenderer(Vector2i screenSize, Shader depthPrepassShader, Shader lightCullingShader)
        {
            _depthPrepassShader = depthPrepassShader;
            _lightCullingShader = lightCullingShader;
            BuildDepthTarget(screenSize);
        }

        private void BuildDepthTarget(Vector2i screenSize)
        {
            _screenSize = screenSize;
            TileCountX = (screenSize.X + TileSize - 1) / TileSize;
            TileCountY = (screenSize.Y + TileSize - 1) / TileSize;

            _depthTexture = new Texture("ForwardPlusDepth", TextureTarget.Texture2D, screenSize,
                PixelInternalFormat.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float,
                TextureWrapMode.ClampToEdge, TextureMinFilter.Nearest, TextureMagFilter.Nearest);

            _depthFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTexture.Handle, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                Logger.Instance.LogError($"Forward+ depth framebuffer incomplete: {status}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Resize(Vector2i newSize)
        {
            if (newSize == _screenSize || newSize.X <= 0 || newSize.Y <= 0) return;
            _depthTexture.Resize(newSize);
            _screenSize = newSize;
            TileCountX = (newSize.X + TileSize - 1) / TileSize;
            TileCountY = (newSize.Y + TileSize - 1) / TileSize;
        }

        /// <summary>
        /// Runs the depth pre-pass + light-culling compute dispatch for one view/projection. Leaves
        /// LightBuffer bound at binding 0 and TileLightIndices bound at binding 1 for the rest of
        /// the frame's draw calls to read from directly.
        /// </summary>
        public void Run(List<Game.RenderRequest> queue, Func<SubMesh, DrawMesh> getOrCreateDrawMesh,
            IReadOnlyList<LightRenderData> lights, IReadOnlyList<ShadowAssignment> shadowAssignments,
            Matrix4 view, Matrix4 projection, Matrix4 invProjection, float nearPlane, float farPlane)
        {
            RunDepthPrepass(queue, getOrCreateDrawMesh, view, projection);
            UploadLights(lights, shadowAssignments);
            DispatchCulling(view, invProjection, lights.Count, nearPlane, farPlane);
        }

        private void RunDepthPrepass(List<Game.RenderRequest> queue, Func<SubMesh, DrawMesh> getOrCreateDrawMesh, Matrix4 view, Matrix4 projection)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _depthFBO);
            GL.Viewport(0, 0, _screenSize.X, _screenSize.Y);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.ColorMask(false, false, false, false);

            _depthPrepassShader.UseShader();
            _depthPrepassShader.SetUniform("view", view);
            _depthPrepassShader.SetUniform("projection", projection);

            foreach (var req in queue)
            {
                // Transparent geometry shouldn't contribute conservative "solid" depth for culling.
                if (req.Transparent) continue;
                var drawMesh = getOrCreateDrawMesh(req.subMesh);
                drawMesh.DrawDepthOnly(_depthPrepassShader, req.ModelMatrix);
            }

            GL.ColorMask(true, true, true, true);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void UploadLights(IReadOnlyList<LightRenderData> lights, IReadOnlyList<ShadowAssignment> shadowAssignments)
        {
            int count = Math.Max(lights.Count, 1);
            if (_lightScratch.Length < count) _lightScratch = new GPULight[count];

            for (int i = 0; i < lights.Count; i++)
            {
                ShadowAssignment shadow = (shadowAssignments != null && i < shadowAssignments.Count) ? shadowAssignments[i] : ShadowAssignment.None;
                _lightScratch[i] = new GPULight(lights[i], shadow);
            }

            _lightSSBO.SetData(_lightScratch);
        }

        private void DispatchCulling(Matrix4 view, Matrix4 invProjection, int lightCount, float nearPlane, float farPlane)
        {
            _tileLightSSBO.Allocate(TileCountX * TileCountY * (MaxLightsPerTile + 1) * sizeof(int));

            _lightSSBO.BindBase(0);
            _tileLightSSBO.BindBase(1);

            _lightCullingShader.UseShader();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _depthTexture.Handle);
            _lightCullingShader.SetUniform("depthTexture", 0);

            _lightCullingShader.SetUniform("view", view);
            _lightCullingShader.SetUniform("invProjection", invProjection);
            _lightCullingShader.SetUniform("screenSizeF", new Vector2(_screenSize.X, _screenSize.Y));
            _lightCullingShader.SetUniform("tileCountF", new Vector2(TileCountX, TileCountY));
            _lightCullingShader.SetUniform("lightCount", lightCount);
            _lightCullingShader.SetUniform("nearPlane", nearPlane);
            _lightCullingShader.SetUniform("farPlane", farPlane);

            GL.DispatchCompute(TileCountX, TileCountY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _depthTexture?.Delete();
            GL.DeleteFramebuffer(_depthFBO);
            _lightSSBO.Delete();
            _tileLightSSBO.Delete();
            GC.SuppressFinalize(this);
        }

        // TEMP diagnostic: reads back the per-tile light counts and writes a 0/1 grid where 1 = tile
        // has at least one light assigned. Lets us prove whether a black rendered tile was culled
        // (count 0) or merely shaded dark (count >= 1 but dot(normal, toLight) <= 0).
        public void DumpTileCounts(string path)
        {
            int stride = MaxLightsPerTile + 1;
            int total = TileCountX * TileCountY * stride;
            int[] buf = new int[total];
            _tileLightSSBO.Bind();
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, total * sizeof(int), buf);
            _tileLightSSBO.Unbind();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# tiles {TileCountX}x{TileCountY} (row 0 = bottom of frame)");
            for (int y = 0; y < TileCountY; y++)
            {
                for (int x = 0; x < TileCountX; x++)
                {
                    int c = buf[(y * TileCountX + x) * stride];
                    sb.Append(c >= 1 ? '1' : '0');
                }
                sb.Append('\n');
            }
            System.IO.File.WriteAllText(path, sb.ToString());
        }

        // TEMP diagnostic: reads back the depth pre-pass texture and writes a grid where g = tile
        // contains geometry (any pixel with depth < 1.0). Lets us separate black tiles caused by a
        // genuine culling bug (geometry present) from black background tiles (no geometry -> just the
        // cleared backdrop, which a point light cannot illuminate).
        public void DumpTileDepths(string path)
        {
            int w = _screenSize.X, h = _screenSize.Y;
            float[] depth = new float[w * h];
            _depthTexture.Use(0);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, depth);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# tiles {TileCountX}x{TileCountY} (row 0 = bottom of frame; g = has geometry)");
            for (int ty = 0; ty < TileCountY; ty++)
            {
                for (int tx = 0; tx < TileCountX; tx++)
                {
                    bool geo = false;
                    for (int py = ty * TileSize; py < Math.Min((ty + 1) * TileSize, h) && !geo; py++)
                        for (int px = tx * TileSize; px < Math.Min((tx + 1) * TileSize, w) && !geo; px++)
                            if (depth[py * w + px] < 1.0f) { geo = true; break; }
                    sb.Append(geo ? 'g' : '.');
                }
                sb.Append('\n');
            }
            System.IO.File.WriteAllText(path, sb.ToString());
        }
    }
}