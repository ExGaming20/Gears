using Gears.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Gears.World.Components
{
    public class EProbe : BaseComponent // Environment Probe
    {
        public ushort Resolution { get; set; } = 256;
        public Texture Color { get; private set; }  // Octahedral color map
        public Texture Depth { get; private set; }  // Octahedral distance map (linear distance, 0..1 over Near..Far, in R channel)

        public float Far { get; set; } = 500f;
        public float Near { get; set; } = 0.1f;

        public Vector3 Domein { get; set; } = Vector3.Zero; // the volume that gameobjects would even consider to use for the "nearest" EProbe

        private static int _nextUUID = 0;
        private int _UUID;

        private static readonly Vector3[] Directions = new Vector3[6]
        {
            new Vector3(1, 0, 0),   // +x
            new Vector3(-1, 0, 0),  // -x
            new Vector3(0, 1, 0),   // +y
            new Vector3(0, -1, 0),  // -y
            new Vector3(0, 0, 1),   // +z
            new Vector3(0, 0, -1)   // -z
        };

        private static readonly Vector3[] UpVectors = new Vector3[6]
        {
            new Vector3(0, -1, 0), // +x
            new Vector3(0, -1, 0), // -x
            new Vector3(0, 0, 1),  // +y
            new Vector3(0, 0, -1), // -y
            new Vector3(0, -1, 0), // +z
            new Vector3(0, -1, 0)  // -z
        };

        private struct FaceTarget
        {
            public int FBOHandle;
            public int ColorHandle;
            public int DepthHandle;
        }

        private FaceTarget[] _faces = new FaceTarget[6];
        private int _builtResolution = -1;

        public override void Awake()
        {
            base.Awake();
            _UUID = _nextUUID++;

            Color = new Texture($"EProbe_Color{_UUID}", new Vector2i(Resolution, Resolution));
            Depth = new Texture($"EProbe_Depth{_UUID}", new Vector2i(Resolution, Resolution));

            BuildFaceTargets(Resolution);
        }

        // -----------------------------------------------------------------------
        // Self-registration into ProbeManager (mirrors Light / LightManager)
        // -----------------------------------------------------------------------

        public override void OnEnable()
        {
            base.OnEnable();
            ProbeManager.Register(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ProbeManager.Unregister(this);
        }

        private void BuildFaceTargets(int resolution)
        {
            if (resolution == _builtResolution) return;
            DeleteFaceTargets();

            for (int i = 0; i < 6; i++)
            {
                int fbo = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

                int colorTex = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, colorTex);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, resolution, resolution, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, colorTex, 0);

                // Depth as a sampleable texture (not a renderbuffer) so we can read distances back for the octahedral distance map.
                int depthTex = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, depthTex);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, resolution, resolution, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, depthTex, 0);

                var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (status != FramebufferErrorCode.FramebufferComplete)
                    Logger.Instance.LogError($"EProbe face {i} framebuffer incomplete: {status}");

                GL.BindTexture(TextureTarget.Texture2D, 0);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

                _faces[i] = new FaceTarget { FBOHandle = fbo, ColorHandle = colorTex, DepthHandle = depthTex };
            }

            _builtResolution = resolution;
        }

        private void DeleteFaceTargets()
        {
            for (int i = 0; i < 6; i++)
            {
                if (_faces[i].FBOHandle == 0) continue;
                GL.DeleteFramebuffer(_faces[i].FBOHandle);
                GL.DeleteTexture(_faces[i].ColorHandle);
                GL.DeleteTexture(_faces[i].DepthHandle);
            }
            _faces = new FaceTarget[6];
            _builtResolution = -1;
        }

        public void UpdateProbe()
        {
            if (Resolution != _builtResolution)
            {
                Color.Resize(Vector2i.One * Resolution);
                Depth.Resize(Vector2i.One * Resolution);
                BuildFaceTargets(Resolution);
            }

            Vector3 pos = GameObject.Transform.Position;

            // Populate a dedicated probe render queue (separate from the per-frame Game.RenderQueue,
            // which is only valid between Scene.Render() and its Clear() at the end of OnRenderFrame,
            // and would otherwise be empty by the time Tick() runs the probe accumulator). Geometry
            // doesn't change between the 6 faces, so this is built once and reused for all of them.
            Game.BeginProbeCapture();

            byte[][] faceColor = new byte[6][];
            float[][] faceDepth = new float[6][];

            for (int i = 0; i < 6; i++)
            {
                Matrix4 view = Matrix4.LookAt(pos, pos + Directions[i], UpVectors[i]);
                Matrix4 proj = Camera.CreatePerspective(MathHelper.DegreesToRadians(90f), 1f, Near, Far);

                Game.RenderProbeFace(_faces[i].FBOHandle, Resolution, Resolution, view, proj);

                faceColor[i] = new byte[Resolution * Resolution * 4];
                GL.BindTexture(TextureTarget.Texture2D, _faces[i].ColorHandle);
                GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, faceColor[i]);

                faceDepth[i] = new float[Resolution * Resolution];
                GL.BindTexture(TextureTarget.Texture2D, _faces[i].DepthHandle);
                GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, faceDepth[i]);
            }

            GL.BindTexture(TextureTarget.Texture2D, 0);

            ProjectToOctahedral(faceColor, faceDepth);
        }

        private void ProjectToOctahedral(byte[][] faceColor, float[][] faceDepth)
        {
            int res = Resolution;
            byte[] colorOut = new byte[res * res * 4];
            byte[] depthOut = new byte[res * res * 4];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res;
                    float v = (y + 0.5f) / res;

                    Vector3 dir = OctDecode(u, v);
                    var (face, fu, fv) = DirectionToFace(dir);

                    int sx = Math.Clamp((int)(fu * res), 0, res - 1);
                    int sy = Math.Clamp((int)(fv * res), 0, res - 1);
                    int srcIdx = (sy * res + sx);
                    int srcIdx4 = srcIdx * 4;
                    int dstIdx4 = (y * res + x) * 4;

                    colorOut[dstIdx4 + 0] = faceColor[face][srcIdx4 + 0];
                    colorOut[dstIdx4 + 1] = faceColor[face][srcIdx4 + 1];
                    colorOut[dstIdx4 + 2] = faceColor[face][srcIdx4 + 2];
                    colorOut[dstIdx4 + 3] = faceColor[face][srcIdx4 + 3];

                    float ndc = faceDepth[face][srcIdx] * 2f - 1f;
                    float linear = (2f * Near * Far) / (Far + Near - ndc * (Far - Near));
                    float normalized = Math.Clamp((linear - Near) / (Far - Near), 0f, 1f);
                    byte d = (byte)(normalized * 255f);

                    depthOut[dstIdx4 + 0] = d;
                    depthOut[dstIdx4 + 1] = d;
                    depthOut[dstIdx4 + 2] = d;
                    depthOut[dstIdx4 + 3] = 255;
                }
            }

            Color.SetPixels(colorOut);
            Depth.SetPixels(depthOut);
        }

        private static Vector3 OctDecode(float u, float v)
        {
            Vector2 f = new Vector2(u * 2f - 1f, v * 2f - 1f);
            float z = 1f - MathF.Abs(f.X) - MathF.Abs(f.Y);

            float signX = f.X >= 0f ? 1f : -1f;
            float signY = f.Y >= 0f ? 1f : -1f;

            if (z < 0f)
            {
                float oldX = f.X;
                f.X = (1f - MathF.Abs(f.Y)) * signX;
                f.Y = (1f - MathF.Abs(oldX)) * signY;
            }

            return Vector3.Normalize(new Vector3(f.X, f.Y, z));
        }

        private static (int face, float u, float v) DirectionToFace(Vector3 dir)
        {
            float absX = MathF.Abs(dir.X), absY = MathF.Abs(dir.Y), absZ = MathF.Abs(dir.Z);
            int face; float u, v, ma;

            if (absX >= absY && absX >= absZ)
            {
                ma = absX;
                if (dir.X > 0) { face = 0; u = -dir.Z; v = -dir.Y; }
                else { face = 1; u = dir.Z; v = -dir.Y; }
            }
            else if (absY >= absX && absY >= absZ)
            {
                ma = absY;
                if (dir.Y > 0) { face = 2; u = dir.X; v = dir.Z; }
                else { face = 3; u = dir.X; v = -dir.Z; }
            }
            else
            {
                ma = absZ;
                if (dir.Z > 0) { face = 4; u = dir.X; v = -dir.Y; }
                else { face = 5; u = -dir.X; v = -dir.Y; }
            }

            u = (u / ma + 1f) * 0.5f;
            v = (v / ma + 1f) * 0.5f;
            return (face, u, v);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            ProbeManager.Unregister(this);
            DeleteFaceTargets();
            Color?.Delete();
            Depth?.Delete();
        }
    }
}