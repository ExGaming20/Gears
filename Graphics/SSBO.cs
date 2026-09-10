using OpenTK.Graphics.OpenGL4;

namespace Gears.Graphics
{
    /// <summary>Thin wrapper around a GL_SHADER_STORAGE_BUFFER, in the same style as VBO/VAO.</summary>
    public class SSBO
    {
        public int ID { get; private set; }
        private int _capacityBytes = 0;

        public SSBO()
        {
            ID = GL.GenBuffer();
        }

        public void Bind()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ID);
        }

        public void Unbind()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        /// <summary>Binds this buffer to an indexed binding point (matches a shader's `layout(binding = n)`).</summary>
        public void BindBase(int bindingIndex)
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, bindingIndex, ID);
        }

        /// <summary>
        /// Uploads data. Reallocates storage only when the new data is larger than what's already
        /// allocated (BufferData), otherwise just overwrites in place (BufferSubData) to avoid
        /// reallocating every frame for data whose size doesn't change.
        /// </summary>
        public void SetData<T>(T[] data, BufferUsageHint usage = BufferUsageHint.DynamicDraw) where T : struct
        {
            Bind();
            int stride = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            int bytes = data.Length * stride;

            if (bytes > _capacityBytes)
            {
                GL.BufferData(BufferTarget.ShaderStorageBuffer, bytes, data, usage);
                _capacityBytes = bytes;
            }
            else
            {
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, bytes, data);
            }
        }

        /// <summary>Allocates (grows-only) storage without uploading data — e.g. a compute shader's write-only output buffer.</summary>
        public void Allocate(int bytes, BufferUsageHint usage = BufferUsageHint.DynamicDraw)
        {
            if (bytes <= _capacityBytes) return;
            Bind();
            GL.BufferData(BufferTarget.ShaderStorageBuffer, bytes, IntPtr.Zero, usage);
            _capacityBytes = bytes;
        }

        public void Delete()
        {
            GL.DeleteBuffer(ID);
        }
    }
}