using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Gears.Utilities;

namespace Gears.Graphics
{
    public class FrameBuffer : IDisposable
    {
        public int FBOHandle { get; private set; }
        public int ColorHandle { get; private set; }
        public int RBOHandle { get; private set; }

        private Vector2i _size;
        private bool _disposed;

        public FrameBuffer(Vector2i size)
        {
            _size = size;
            Build(size);
        }

        private void Build(Vector2i size)
        {
            // Validate size
            if (size.X <= 0 || size.Y <= 0)
            {
                Logger.Instance.LogError($"Invalid framebuffer size: {size}");
                return;
            }

            // FBO
            FBOHandle = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FBOHandle);

            // Color attachment texture
            ColorHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, ColorHandle);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb,
                size.X, size.Y, 0, PixelFormat.Rgb, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ColorHandle, 0);

            // Depth + stencil renderbuffer
            RBOHandle = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RBOHandle);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.Depth24Stencil8, size.X, size.Y);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, RBOHandle);

            // Ensure texture is unbound before checking status
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                Logger.Instance.LogError($"Framebuffer incomplete: {status}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FBOHandle);
            GL.Viewport(0, 0, _size.X, _size.Y);
        }

        public void Unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Resize(Vector2i newSize)
        {
            if (newSize == _size) return;
            Delete();
            Build(newSize);
            _size = newSize;
        }

        public void BindColorTexture(int unit = 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2D, ColorHandle);
        }

        private void Delete()
        {
            GL.DeleteFramebuffer(FBOHandle);
            GL.DeleteTexture(ColorHandle);
            GL.DeleteRenderbuffer(RBOHandle);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Delete();
            GC.SuppressFinalize(this);
        }
    }
}