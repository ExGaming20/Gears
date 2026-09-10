using System.Globalization;
using System.Text.Json;
using System.Collections.Generic;
using Gears.Graphics;
using Gears.Utilities;
using OpenTK.Mathematics;

namespace Gears
{
    public class AssetLoader
    {
        public static void LoadShader(string name, string path)
        {
            if (!Path.IsPathFullyQualified(path))
                path = Path.Combine("..", "..", "..", "Assets", "Shaders", path + ".nlsf");

            if (!File.Exists(path))
            {
                Logger.Instance.LogError($"Shader file not found: {path}");
                Game.Shaders.Add(Game.MissingShader);

                return;
            }

            string json = File.ReadAllText(path);

            bool drawableShader = bool.Parse(JsonParser.FindData(json, "DrawableShader") ?? "true");

            using JsonDocument doc = JsonDocument.Parse(json);

            if (drawableShader)
            {
                string? vertexPath = null;
                string? fragmentPath = null;
                string? geometryPath = null;

                if (doc.RootElement.TryGetProperty("SubShaders", out JsonElement subShaders))
                {
                    foreach (JsonElement sub in subShaders.EnumerateArray())
                    {
                        if (sub.TryGetProperty("Vertex", out JsonElement v)) vertexPath = v.GetString();
                        if (sub.TryGetProperty("Fragment", out JsonElement f)) fragmentPath = f.GetString();
                        if (sub.TryGetProperty("Geometry", out JsonElement g)) geometryPath = g.GetString();
                    }
                }

                Shader shader = new Shader(new ShaderProgram(vertexPath, fragmentPath, geometryPath, null, true), name);
                Game.Shaders.Add(shader);
            }
            else
            {
                string? computePath = null;

                if (doc.RootElement.TryGetProperty("SubShaders", out JsonElement subShaders))
                {
                    foreach (JsonElement sub in subShaders.EnumerateArray())
                    {
                        if (sub.TryGetProperty("Compute", out JsonElement c)) computePath = c.GetString();
                    }
                }

                Shader shader = new Shader(new ShaderProgram(null, null, null, computePath), name);
                shader.ReloadShader();
                Game.Shaders.Add(shader);
            }
        }

        public static void LoadTexture(string name, string path)
        {
            if (!Path.IsPathFullyQualified(path))
                path = Path.GetFullPath(Path.Combine("..", "..", "..", "Assets", "Textures", Path.GetFileName(path)));

            Texture texture = new Texture(name, path, new TextureData[]
            {
                new TextureData { Type = TextureDataType.TextureWrap,     Value = TextureDataValue.Repeat             },
                new TextureData { Type = TextureDataType.Mipmap,          Value = TextureDataValue.Nearest            },
                new TextureData { Type = TextureDataType.TextureFilter,   Value = TextureDataValue.Nearest            },
                new TextureData { Type = TextureDataType.PixelFormat,     Value = TextureDataValue.Rgba               },
                new TextureData { Type = TextureDataType.TextureTarget,   Value = TextureDataValue.Texture2D          },
                new TextureData { Type = TextureDataType.ColorComponents, Value = TextureDataValue.Rgba               }
            });

            Game.Textures.Add(texture);
        }

        public static void LoadMaterial(string name, string path)
        {
            if (!Path.IsPathFullyQualified(path))
                path = Path.Combine("..", "..", "..", "Assets", "Materials", path + ".nlmf");

            if (!File.Exists(path))
            {
                Logger.Instance.LogError($"Material file not found: {path}");
                Game.Materials.Add(Game.MissingMaterial);

                return;
            }

            string json = File.ReadAllText(path);

            string shaderName = JsonParser.FindData(json, "ShaderName") ?? "Standard";
            string seeThroughType = JsonParser.FindData(json, "SeeThroughType") ?? "Opaque";
            string cullMode = JsonParser.FindData(json, "CullMode") ?? "CCW";

            Vector4 baseColor;
            baseColor.X = float.Parse(JsonParser.FindData(json, "BaseColor.X") ?? "1", CultureInfo.InvariantCulture);
            baseColor.Y = float.Parse(JsonParser.FindData(json, "BaseColor.Y") ?? "1", CultureInfo.InvariantCulture);
            baseColor.Z = float.Parse(JsonParser.FindData(json, "BaseColor.Z") ?? "1", CultureInfo.InvariantCulture);
            baseColor.W = float.Parse(JsonParser.FindData(json, "BaseColor.W") ?? "1", CultureInfo.InvariantCulture);

            Vector4 emissionColor;
            emissionColor.X = float.Parse(JsonParser.FindData(json, "EmissionColor.X") ?? "0", CultureInfo.InvariantCulture);
            emissionColor.Y = float.Parse(JsonParser.FindData(json, "EmissionColor.Y") ?? "0", CultureInfo.InvariantCulture);
            emissionColor.Z = float.Parse(JsonParser.FindData(json, "EmissionColor.Z") ?? "0", CultureInfo.InvariantCulture);
            emissionColor.W = float.Parse(JsonParser.FindData(json, "EmissionColor.W") ?? "0", CultureInfo.InvariantCulture);

            float metallic = float.Parse(JsonParser.FindData(json, "Metallic") ?? "0", CultureInfo.InvariantCulture);
            float roughness = float.Parse(JsonParser.FindData(json, "Roughness") ?? "0", CultureInfo.InvariantCulture);

            bool receiveShadows = bool.Parse(JsonParser.FindData(json, "ReceiveShadows") ?? "true");
            bool castShadows = bool.Parse(JsonParser.FindData(json, "CastShadows") ?? "true");
            bool zWrite = bool.Parse(JsonParser.FindData(json, "ZWrite") ?? "true");

            List<Material._TextureData> textures = new();
            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("Textures", out JsonElement texArray))
            {
                foreach (JsonElement tex in texArray.EnumerateArray())
                {
                    string textureName = tex.GetProperty("TextureName").GetString() ?? "";
                    float scaleX = tex.GetProperty("TextureScale").GetProperty("X").GetSingle();
                    float scaleY = tex.GetProperty("TextureScale").GetProperty("Y").GetSingle();
                    float offsetX = tex.GetProperty("TextureOffset").GetProperty("X").GetSingle();
                    float offsetY = tex.GetProperty("TextureOffset").GetProperty("Y").GetSingle();

                    Texture? texture = Game.Textures.Find(t => t.Name == textureName);

                    if (texture != null)
                    {
                        TextureData[] extractedData = ExtractTextureData(tex);
                        texture = new Texture(texture.Name, texture.FilePath, extractedData);

                        int index = Game.Textures.FindIndex(t => t.Name == textureName);
                        if (index >= 0)
                            Game.Textures[index] = texture;
                    }

                    textures.Add(new Material._TextureData
                    {
                        TextureUUID = texture?.UUID ?? 0,
                        TextureName = textureName,
                        TextureScale = new Vector2(scaleX, scaleY),
                        TextureOffset = new Vector2(offsetX, offsetY)
                    });
                }
            }

            Material material = new Material
            {
                Name = name,
                ShaderName = shaderName,
                SeeThroughType = Enum.Parse<Material._RenderType>(seeThroughType),
                cullMode = Enum.Parse<Material._CullMode>(cullMode),
                BaseColor = baseColor,
                EmissionColor = emissionColor,
                Metallic = metallic,
                Roughness = roughness,
                ReceiveShadows = receiveShadows,
                CastShadows = castShadows,
                ZWrite = zWrite,
                Textures = textures.ToArray()
            };

            Shader? matchedShader = Game.Shaders.Find(s => s.Name == shaderName);

            if (matchedShader != null)
            {
                material.ShaderUUID = matchedShader.UUID;
            }
            else
            {
                Logger.Instance.LogError($"LoadMaterial: no shader named '{shaderName}' found for material '{name}'");
            }

            Game.Materials.Add(material);
        }

        public static void LoadMesh(string name, string path)
        {
            if (!Path.IsPathFullyQualified(path))
                path = Path.Combine("..", "..", "..", "Assets", "Models", path + ".obj");

            if (!File.Exists(path))
            {
                Logger.Instance.LogError($"Mesh file not found: {path}");
                Game.Meshes.Add(Game.MissingMesh);

                return;
            }

            Mesh mesh = OBJParser.Parse(name, path);
            Game.Meshes.Add(mesh);
        }

        public static void LoadAllAssets()
        {
            LoadAll(Path.Combine("..", "..", "..", "Assets"));
        }

        private static void LoadAll(string basePath)
        {
            string shadersPath = Path.Combine(basePath, "Shaders");
            if (Directory.Exists(shadersPath))
            {
                foreach (string path in Directory.GetFiles(shadersPath, "*.nlsf"))
                {
                    string fullPath = Path.GetFullPath(path);
                    string name = Path.GetFileNameWithoutExtension(fullPath);
                    LoadShader(name, fullPath);
                }
            }

            string texturesPath = Path.Combine(basePath, "Textures");
            if (Directory.Exists(texturesPath))
            {
                foreach (string path in Directory.GetFiles(texturesPath, "*.png"))
                {
                    string fullPath = Path.GetFullPath(path);
                    string name = Path.GetFileNameWithoutExtension(fullPath);
                    LoadTexture(name, fullPath);
                }
            }

            string materialsPath = Path.Combine(basePath, "Materials");
            if (Directory.Exists(materialsPath))
            {
                foreach (string path in Directory.GetFiles(materialsPath, "*.nlmf"))
                {
                    string fullPath = Path.GetFullPath(path);
                    string name = Path.GetFileNameWithoutExtension(fullPath);
                    LoadMaterial(name, fullPath);
                }
            }

            string modelsPath = Path.Combine(basePath, "Models");
            if (Directory.Exists(modelsPath))
            {
                foreach (string path in Directory.GetFiles(modelsPath, "*.obj"))
                {
                    string fullPath = Path.GetFullPath(path);
                    string name = Path.GetFileNameWithoutExtension(fullPath);
                    LoadMesh(name, fullPath);
                }
            }
        }

        public static void UnloadAllAssets()
        {
            var deletedHandles = new HashSet<int>();
            foreach (var texture in Game.Textures)
            {
                if (deletedHandles.Add(texture.Handle))
                    texture.Delete();
            }
            Game.Textures.Clear();

            foreach (var shader in Game.Shaders)
                shader.DeleteShader();
            Game.Shaders.Clear();

            Game.Materials.Clear();
            Game.Meshes.Clear();
        }

        public static Shader ReturnShaderMissing(string name, string path)
        {
            if (!Path.IsPathFullyQualified(path))
            {
                string baseDir = AppContext.BaseDirectory;
                path = Path.Combine(baseDir, "Fallback Assets", "Shaders", path + ".nlsf");
            }

            string json = File.ReadAllText(path);

            bool drawableShader = bool.Parse(JsonParser.FindData(json, "DrawableShader") ?? "true");

            using JsonDocument doc = JsonDocument.Parse(json);

            if (drawableShader)
            {
                string? vertexPath = null;
                string? fragmentPath = null;
                string? geometryPath = null;

                if (doc.RootElement.TryGetProperty("SubShaders", out JsonElement subShaders))
                {
                    foreach (JsonElement sub in subShaders.EnumerateArray())
                    {
                        if (sub.TryGetProperty("Vertex", out JsonElement v)) vertexPath = v.GetString();
                        if (sub.TryGetProperty("Fragment", out JsonElement f)) fragmentPath = f.GetString();
                        if (sub.TryGetProperty("Geometry", out JsonElement g)) geometryPath = g.GetString();
                    }
                }

                Shader shader = new Shader(new ShaderProgram(vertexPath, fragmentPath, geometryPath, null, true), name);

                Game.Shaders.Add(shader);

                return shader;
            }

            return new Shader(new ShaderProgram(null, null, null, null), name);
        }

        public static Material ReturnMaterialMissing(string name, string path)
        {
            if (!Path.IsPathFullyQualified(path))
            {
                string baseDir = AppContext.BaseDirectory;
                path = Path.Combine(baseDir, path + ".nlmf");
            }

            string json = File.ReadAllText(path);

            string shaderName = JsonParser.FindData(json, "ShaderName") ?? "Standard";
            string seeThroughType = JsonParser.FindData(json, "SeeThroughType") ?? "Opaque";
            string cullMode = JsonParser.FindData(json, "CullMode") ?? "CCW";

            Vector4 baseColor;
            baseColor.X = float.Parse(JsonParser.FindData(json, "BaseColor.X") ?? "1", CultureInfo.InvariantCulture);
            baseColor.Y = float.Parse(JsonParser.FindData(json, "BaseColor.Y") ?? "1", CultureInfo.InvariantCulture);
            baseColor.Z = float.Parse(JsonParser.FindData(json, "BaseColor.Z") ?? "1", CultureInfo.InvariantCulture);
            baseColor.W = float.Parse(JsonParser.FindData(json, "BaseColor.W") ?? "1", CultureInfo.InvariantCulture);

            Vector4 emissionColor;
            emissionColor.X = float.Parse(JsonParser.FindData(json, "EmissionColor.X") ?? "0", CultureInfo.InvariantCulture);
            emissionColor.Y = float.Parse(JsonParser.FindData(json, "EmissionColor.Y") ?? "0", CultureInfo.InvariantCulture);
            emissionColor.Z = float.Parse(JsonParser.FindData(json, "EmissionColor.Z") ?? "0", CultureInfo.InvariantCulture);
            emissionColor.W = float.Parse(JsonParser.FindData(json, "EmissionColor.W") ?? "0", CultureInfo.InvariantCulture);

            float metallic = float.Parse(JsonParser.FindData(json, "Metallic") ?? "0", CultureInfo.InvariantCulture);
            float roughness = float.Parse(JsonParser.FindData(json, "Roughness") ?? "0", CultureInfo.InvariantCulture);

            bool receiveShadows = bool.Parse(JsonParser.FindData(json, "ReceiveShadows") ?? "true");
            bool castShadows = bool.Parse(JsonParser.FindData(json, "CastShadows") ?? "true");
            bool zWrite = bool.Parse(JsonParser.FindData(json, "ZWrite") ?? "true");

            List<Material._TextureData> textures = new();
            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("Textures", out JsonElement texArray))
            {
                foreach (JsonElement tex in texArray.EnumerateArray())
                {
                    string textureName = tex.GetProperty("TextureName").GetString() ?? "";
                    float scaleX = tex.GetProperty("TextureScale").GetProperty("X").GetSingle();
                    float scaleY = tex.GetProperty("TextureScale").GetProperty("Y").GetSingle();
                    float offsetX = tex.GetProperty("TextureOffset").GetProperty("X").GetSingle();
                    float offsetY = tex.GetProperty("TextureOffset").GetProperty("Y").GetSingle();

                    Texture? texture = Game.Textures.Find(t => t.Name == textureName);

                    textures.Add(new Material._TextureData
                    {
                        TextureUUID = texture?.UUID ?? 0,
                        TextureName = textureName,
                        TextureScale = new Vector2(scaleX, scaleY),
                        TextureOffset = new Vector2(offsetX, offsetY)
                    });
                }
            }

            Material material = new Material
            {
                Name = name,
                ShaderName = shaderName,
                SeeThroughType = Enum.Parse<Material._RenderType>(seeThroughType),
                cullMode = Enum.Parse<Material._CullMode>(cullMode),
                BaseColor = baseColor,
                EmissionColor = emissionColor,
                Metallic = metallic,
                Roughness = roughness,
                ReceiveShadows = receiveShadows,
                CastShadows = castShadows,
                ZWrite = zWrite,
                Textures = textures.ToArray()
            };

            Shader? matchedShader = Game.Shaders.Find(s => s.Name == shaderName);

            if (matchedShader == null)
            {
                matchedShader = Game.MissingShader;
                Logger.Instance.LogWarning($"ReturnMaterialMissing: shader '{shaderName}' not found, falling back to MissingShader.");
            }

            if (matchedShader != null)
            {
                material.ShaderUUID = matchedShader.UUID;
            }
            else
            {
                Logger.Instance.LogError($"ReturnMaterialMissing: no shader named '{shaderName}' and no MissingShader fallback for material '{name}'");
            }

            Game.Materials.Add(material);

            return material;
        }

        public static Texture ReturnTextureMissing(string name, string path)
        {
            Texture texture = new Texture(name, path, new TextureData[]
            {
                new TextureData { Type = TextureDataType.TextureWrap,     Value = TextureDataValue.Repeat             },
                new TextureData { Type = TextureDataType.Mipmap,          Value = TextureDataValue.LinearMipmapLinear },
                new TextureData { Type = TextureDataType.TextureFilter,   Value = TextureDataValue.Linear             },
                new TextureData { Type = TextureDataType.PixelFormat,     Value = TextureDataValue.Rgba               },
                new TextureData { Type = TextureDataType.TextureTarget,   Value = TextureDataValue.Texture2D          },
                new TextureData { Type = TextureDataType.ColorComponents, Value = TextureDataValue.Rgba               }
            });

            Game.Textures.Add(texture);

            return texture;
        }

        public static Mesh ReturnMeshMissing(string name, string path)
        {
            if (!Path.IsPathFullyQualified(path))
            {
                string baseDir = AppContext.BaseDirectory;
                path = Path.Combine(baseDir, "Fallback Assets", "Models", path + ".obj");
            }

            Mesh mesh = OBJParser.Parse(name, path);

            return mesh;
        }

        private static TextureData[] ExtractTextureData(JsonElement textureElement)
        {
            var textureDataList = new List<TextureData>();

            if (textureElement.TryGetProperty("TextureWrap", out JsonElement wrapElement))
                textureDataList.Add(new TextureData { Type = TextureDataType.TextureWrap, Value = Enum.Parse<TextureDataValue>(wrapElement.GetString() ?? "Repeat") });

            if (textureElement.TryGetProperty("TextureFilter", out JsonElement filterElement))
                textureDataList.Add(new TextureData { Type = TextureDataType.TextureFilter, Value = Enum.Parse<TextureDataValue>(filterElement.GetString() ?? "Linear") });

            if (textureElement.TryGetProperty("Mipmap", out JsonElement mipmapElement))
                textureDataList.Add(new TextureData { Type = TextureDataType.Mipmap, Value = Enum.Parse<TextureDataValue>(mipmapElement.GetString() ?? "LinearMipmapLinear") });

            if (textureElement.TryGetProperty("PixelFormat", out JsonElement pixelFormatElement))
                textureDataList.Add(new TextureData { Type = TextureDataType.PixelFormat, Value = Enum.Parse<TextureDataValue>(pixelFormatElement.GetString() ?? "Rgba") });

            if (textureElement.TryGetProperty("TextureTarget", out JsonElement targetElement))
                textureDataList.Add(new TextureData { Type = TextureDataType.TextureTarget, Value = Enum.Parse<TextureDataValue>(targetElement.GetString() ?? "Texture2D") });

            if (textureElement.TryGetProperty("ColorComponents", out JsonElement colorCompElement))
                textureDataList.Add(new TextureData { Type = TextureDataType.ColorComponents, Value = Enum.Parse<TextureDataValue>(colorCompElement.GetString() ?? "RedGreenBlueAlpha") });

            return textureDataList.ToArray();

        }
    }
}