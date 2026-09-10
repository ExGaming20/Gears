using System;
using OpenTK.Graphics.OpenGL4;

namespace Gears.Graphics
{
    internal class VAO
    {
        public int ID { get; private set; }

        public VAO()
        {
            ID = GL.GenVertexArray();
        }

        public void Bind()
        {
            GL.BindVertexArray(ID);
        }

        public void Unbind()
        {
            GL.BindVertexArray(0);
        }

        public void Delete()
        {
            GL.DeleteVertexArray(ID);
        }
    }
}
