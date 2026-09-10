using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Gears.Utilities;

namespace Gears.Graphics
{
    /// <summary>
    /// A GPU render target that can be drawn into like a framebuffer and sampled
    /// like a normal texture — equivalent to Unity's RenderTexture.
    ///
    /// Usage:
    ///   var rt = new RenderTexture("SecurityCam", new Vector2i(512, 512));
    ///   var cam = someGameObject.AddComponent&lt;Camera&gt;();
    ///   cam.Target = rt;
    ///   // rt.ColorTexture.UUID can now be referenced in any Material's
    ///   // _TextureData.TextureUUID to display the result on a mesh.
    /// </summary>
    public class RenderTexture : IDisposable
    {
        public string Name { get; }
        public Texture ColorTexture { get; private set; }
        public int FBOHandle { get; private set; }
        public int DepthHandle { get; private set; }
        public bool HasDepth { get; }
        public Vector2i Size { get; private set; }

        private bool _disposed;

        public RenderTexture(string name, Vector2i size, bool depth = true)
        {
            Name = name;
            HasDepth = depth;
            Size = size;

            ColorTexture = new Texture($"{name}_RT", size,
                PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte,
                TextureWrapMode.ClampToEdge, TextureMinFilter.Linear, TextureMagFilter.Linear);

            FBOHandle = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FBOHandle);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ColorTexture.Handle, 0);

            if (HasDepth)
            {
                DepthHandle = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthHandle);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, size.X, size.Y);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, DepthHandle);
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            }

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                Logger.Instance.LogError($"RenderTexture '{name}' framebuffer incomplete: {status}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            Game.Textures.Add(ColorTexture);
            Game.TextureByUUID[ColorTexture.UUID] = ColorTexture;
        }

        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FBOHandle);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        public void Unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, (int)Game.WindowSize.X, (int)Game.WindowSize.Y);
        }

        public void Resize(Vector2i newSize)
        {
            if (newSize == Size || newSize.X <= 0 || newSize.Y <= 0) return;

            ColorTexture.Resize(newSize);

            if (HasDepth)
            {
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthHandle);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, newSize.X, newSize.Y);
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            }

            Size = newSize;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Game.Textures.Remove(ColorTexture);
            Game.TextureByUUID.Remove(ColorTexture.UUID);
            ColorTexture.Delete();

            GL.DeleteFramebuffer(FBOHandle);
            if (HasDepth) GL.DeleteRenderbuffer(DepthHandle);

            GC.SuppressFinalize(this);
        }
    }
}
