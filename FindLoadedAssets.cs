using System;
using Gears.Graphics;
using Gears.Utilities;

namespace Gears
{
    public class FindLoadedAssets
    {
        public static Texture? FindTexture(string? name, int? UUID = null)
        {
            if (string.IsNullOrEmpty(name) && !UUID.HasValue)
            {
                Logger.Instance.LogWarning("FindTexture called without name or UUID. This will always return null.");
            }

            foreach (Texture texture in Game.Textures)
            {
                if (texture.Name == name || (UUID.HasValue && texture.UUID == UUID.Value))
                {
                    return texture;
                }
            }

            return null;
        }

        public static Shader? FindShader(string? name, int? UUID = null)
        {
            if (string.IsNullOrEmpty(name) && !UUID.HasValue)
            {
                Logger.Instance.LogWarning("FindShader called without name or UUID. This will always return null.");
            }

            foreach (Shader shader in Game.Shaders)
            {
                if (shader.Name == name || (UUID.HasValue && shader.UUID == UUID.Value))
                {
                    return shader;
                }
            }

            return null;
        }

        public static Material? FindMaterial(string? name, int? UUID = null)
        {
            if (string.IsNullOrEmpty(name) && !UUID.HasValue)
            {
                Logger.Instance.LogWarning("FindMaterial called without name or UUID. This will always return null.");
            }

            foreach (Material material in Game.Materials)
            {
                if (material.Name == name || (UUID.HasValue && material.UUID == UUID.Value))
                {
                    return material;
                }
            }

            return null;
        }

        public static Mesh? FindMesh(string? name, int? UUID = null)
        {
            if (string.IsNullOrEmpty(name) && !UUID.HasValue)
            {
                Logger.Instance.LogWarning("FindMesh called without name or UUID. This will always return null.");
            }

            foreach (Mesh mesh in Game.Meshes)
            {
                if (mesh.Name == name || (UUID.HasValue && mesh.UUID == UUID.Value))
                {
                    return mesh;
                }
            }

            return null;
        }
    }
}