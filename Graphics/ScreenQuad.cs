using OpenTK.Graphics.OpenGL4;

namespace Gears.Graphics
{
    public class ScreenQuad : IDisposable
    {
        private readonly int _vao;
        private readonly int _vbo;
        private bool _disposed;

        // NDC fullscreen quad: position(2) + uv(2)
        private static readonly float[] Vertices =
        {
            -1f,  1f,  0f, 1f,
            -1f, -1f,  0f, 0f,
             1f, -1f,  1f, 0f,

            -1f,  1f,  0f, 1f,
             1f, -1f,  1f, 0f,
             1f,  1f,  1f, 1f,
        };

        public ScreenQuad()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, Vertices.Length * sizeof(float),
                Vertices, BufferUsageHint.StaticDraw);

            int stride = 4 * sizeof(float);

            // position (location = 0)
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            // uv (location = 1)
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        public void Draw(Shader shader, int textureUnit = 0)
        {
            shader.UseShader();
            shader.SetUniform("screenTexture", textureUnit);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GC.SuppressFinalize(this);
        }
    }
}