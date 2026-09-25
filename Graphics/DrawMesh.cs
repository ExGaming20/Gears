using System.Collections.Generic;
using Gears.Utilities;
using Gears.World;
using Gears.World.Components;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using static Gears.Graphics.Material;

namespace Gears.Graphics
{
    public class DrawMesh : IDisposable
    {
        private readonly VAO vao;
        private readonly VBO vbo;
        private readonly int vertexCount;
        private bool disposed;

        private static int _maxTextureUnits = -1;

        public DrawMesh(SubMesh subMesh)
        {
            if (subMesh == null) throw new ArgumentNullException(nameof(subMesh));

            int vc = subMesh.Vertices.Count;

            if (vc != subMesh.UVs.Count || vc != subMesh.Normals.Count)
                throw new ArgumentException("SubMesh Vertices, UVs and Normals must all have the same count.");

            vertexCount = vc;

            const int stride = 8;
            float[] interleaved = new float[vc * stride];

            for (int i = 0, dst = 0; i < vc; i++, dst += stride)
            {
                Vector3 v = subMesh.Vertices[i];
                Vector2 uv = subMesh.UVs[i];
                Vector3 n = subMesh.Normals[i];

                interleaved[dst + 0] = v.X; interleaved[dst + 1] = v.Y; interleaved[dst + 2] = v.Z;
                interleaved[dst + 3] = uv.X; interleaved[dst + 4] = uv.Y;
                interleaved[dst + 5] = n.X; interleaved[dst + 6] = n.Y; interleaved[dst + 7] = n.Z;
            }

            vao = new VAO();
            vao.Bind();

            vbo = new VBO();
            vbo.Bind(BufferTarget.ArrayBuffer);

            int byteStride = stride * sizeof(float);

            GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, byteStride, 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, byteStride, (IntPtr)(3 * sizeof(float)));
            GL.EnableVertexAttribArray(1);

            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, byteStride, (IntPtr)(5 * sizeof(float)));
            GL.EnableVertexAttribArray(2);

            vbo.Unbind(BufferTarget.ArrayBuffer);
            vao.Unbind();
        }

        public void Draw(Shader shader, Matrix4 model, Matrix4 view, Matrix4 projection, Material material, float timeSinceStart, Layer objectLayer, int shadowMap2DHandle = -1, int shadowMapCubeHandle = -1)
        {
            if (shader == null)
            {
                Logger.Instance.LogError(nameof(shader));
                return;
            }
            if (material == null)
            {
                Logger.Instance.LogError(nameof(material));
                return;
            }

            shader.UseShader();
            vao.Bind();

            SetSafeUniform(shader, "model", model);
            SetSafeUniform(shader, "view", view);
            SetSafeUniform(shader, "projection", projection);

            // Camera world position (translation column of the inverse view) for view-dependent
            // shading such as the skybox ambient reflection in BaseFragment.
            Vector3 cameraPos = Matrix4.Invert(view).Row3.Xyz;
            SetSafeUniform(shader, "cameraPos", cameraPos);

            if (_maxTextureUnits < 0)
                _maxTextureUnits = GL.GetInteger(GetPName.MaxTextureImageUnits);

            int textureUnit = 0;

            // Shadow map arrays are raw GL objects (not Texture-wrapped — see ShadowMapRenderer),
            // so they're bound directly here instead of through Texture.Use().
            if (shadowMap2DHandle != -1 && textureUnit < _maxTextureUnits && DoesUniformExist(shader, "shadowMap2D"))
            {
                GL.ActiveTexture(TextureUnit.Texture0 + textureUnit);
                GL.BindTexture(TextureTarget.Texture2DArray, shadowMap2DHandle);
                shader.SetUniform("shadowMap2D", textureUnit);
                textureUnit++;
            }

            if (shadowMapCubeHandle != -1 && textureUnit < _maxTextureUnits && DoesUniformExist(shader, "shadowMapCube"))
            {
                GL.ActiveTexture(TextureUnit.Texture0 + textureUnit);
                GL.BindTexture(TextureTarget.TextureCubeMapArray, shadowMapCubeHandle);
                shader.SetUniform("shadowMapCube", textureUnit);
                textureUnit++;
            }

            SetSafeUniform(shader, "baseColor", material.BaseColor);
            SetSafeUniform(shader, "emissionColor", material.EmissionColor);
            SetSafeUniform(shader, "metallic", material.Metallic);
            SetSafeUniform(shader, "roughness", material.Roughness);
            SetSafeUniform(shader, "zWrite", material.ZWrite ? 1 : 0);
            SetSafeUniform(shader, "timeSinceStart", timeSinceStart);
            SetSafeUniform(shader, "objectLayer", (int)objectLayer);

            // Samplers (including cubemaps) are bound to a texture unit; the uniform itself just
            // takes that unit's index as an int. Passing the Texture object straight into
            // SetUniform (as this used to do for skyBox) doesn't work — ShaderProgram.SetUniform
            // only understands bool/int/float/Vector2/3/4/Matrix4 and silently no-ops otherwise.
            Texture? skyBox = Scene.SkyBox;
            if (skyBox != null && textureUnit < _maxTextureUnits && DoesUniformExist(shader, "skyBox"))
            {
                skyBox.Use(textureUnit);
                shader.SetUniform("skyBox", textureUnit);
                textureUnit++;
            }

            foreach (Material._TextureData texData in material.Textures)
            {
                if (textureUnit >= _maxTextureUnits)
                {
                    Logger.Instance.LogWarning($"Too many textures (limit {_maxTextureUnits}), skipping the rest.");
                    break;
                }

                if (!Game.TextureByUUID.TryGetValue(texData.TextureUUID, out Texture? texture)) continue;

                texture.Use(textureUnit);

                string uniformBase = string.IsNullOrEmpty(texData.SemanticName) ? texData.TextureName : texData.SemanticName;

                SetSafeUniform(shader, $"material_{uniformBase}", textureUnit);
                SetSafeUniform(shader, $"material_{uniformBase}Scale", texData.TextureScale);
                SetSafeUniform(shader, $"material_{uniformBase}Offset", texData.TextureOffset);

                textureUnit++;
            }

            SetSafeUniform(shader, "textureCount", textureUnit);

            // Nearest environment probe for this object, by world position (translation column of
            // the model matrix). Bound the same way as any other sampler: texture unit + int uniform.
            Vector3 worldPos = model.ExtractTranslation();
            ProbeRenderData? probeData = ProbeManager.GetNearestProbeData(worldPos);

            if (probeData.HasValue)
            {
                var probe = probeData.Value;

                if (probe.Color != null && textureUnit < _maxTextureUnits && DoesUniformExist(shader, "probeColor"))
                {
                    probe.Color.Use(textureUnit);
                    shader.SetUniform("probeColor", textureUnit);
                    textureUnit++;
                }

                if (probe.Depth != null && textureUnit < _maxTextureUnits && DoesUniformExist(shader, "probeDepth"))
                {
                    probe.Depth.Use(textureUnit);
                    shader.SetUniform("probeDepth", textureUnit);
                    textureUnit++;
                }

                SetSafeUniform(shader, "probePosition", probe.Position);
                SetSafeUniform(shader, "probeNear", probe.Near);
                SetSafeUniform(shader, "probeFar", probe.Far);
                SetSafeUniform(shader, "hasProbe", 1);
            }
            else
            {
                SetSafeUniform(shader, "hasProbe", 0);
            }

            // Lighting is now Forward+: the fragment shader reads its tile's culled light list
            // straight out of the LightBuffer/TileLightIndices SSBOs (bound once per frame by
            // ForwardPlusRenderer), so there's nothing left to set here per object or per light.

            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            vao.Unbind();
        }

        /// <summary>Minimal position-only draw for the Forward+ depth pre-pass — no material, textures, or lighting, just depth.</summary>
        public void DrawDepthOnly(Shader shader, Matrix4 model)
        {
            vao.Bind();
            shader.SetUniform("model", model);
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            vao.Unbind();
        }

        private static bool DoesUniformExist(Shader shader, string name)
        {
            return shader.UniformNameSet.Contains(name);
        }

        private void SetSafeUniform(Shader shader, string name, object value)
        {
            if (DoesUniformExist(shader, name))
                shader.SetUniform(name, value);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            vbo?.Delete();
            vao?.Delete();
            GC.SuppressFinalize(this);
        }
    }
}