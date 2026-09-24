using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;

namespace Gears.Graphics
{
    public class Shader
    {
        public ShaderProgram shader { get; set; }
        public string Name { get; set; }
        public int UUID { get; }

        // O(1) uniform existence checks — replaces the old List<Uniform> AvailableUniforms
        public HashSet<string> UniformNameSet { get; private set; } = new();

        private static int _nextUUID = 0;

        public Shader(ShaderProgram Shader, string name)
        {
            this.shader = Shader;
            this.Name = name;
            _nextUUID = Guid.NewGuid().GetHashCode();
            UUID = _nextUUID;
            RefreshUniforms();
        }

        public void UseShader()
        {
            shader.UseProgram();
        }

        public void DeleteShader()
        {
            shader.DeleteProgram();
        }

        public void SetUniform(string uniformName, object value)
        {
            shader.SetUniform(uniformName, value);
        }

        public void ReloadShader()
        {
            shader.Reload();
            RefreshUniforms();
        }

        public void RefreshUniforms()
        {
            UniformNameSet.Clear();

            if (shader.ProgramID == 0)
            {
                Logger.Instance.LogError("Shader program is not valid. Cannot refresh uniforms.", nameof(RefreshUniforms));
                return;
            }

            GL.GetProgram(shader.ProgramID, GetProgramParameterName.ActiveUniforms, out int uniformCount);

            for (int i = 0; i < uniformCount; i++)
            {
                GL.GetActiveUniform(shader.ProgramID, i, 256, out _, out _, out _, out string name);
                UniformNameSet.Add(name);
            }
        }
    }
}