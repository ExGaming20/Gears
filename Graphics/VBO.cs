using System;
using OpenTK.Graphics.OpenGL4;

namespace Gears.Graphics
{
    internal class VBO
    {
        public int ID { get; private set; }

        public VBO()
        {
            ID = GL.GenBuffer();
        }

        public void Bind(BufferTarget target)
        {
            GL.BindBuffer(target, ID);
        }

        public void Unbind(BufferTarget target)
        {
            GL.BindBuffer(target, 0);
        }

        public void Delete()
        {
            GL.DeleteBuffer(ID);
        }
    }
}
