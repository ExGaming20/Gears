using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gears.World;
using Gears.Graphics;
using Gears.Utilities;

namespace Gears
{
    // A loaded asset bundle. Holds the bundle's own identity and the UUIDs
    // of everything it loaded. The loaded objects themselves live in the
    // same Game.Textures/Meshes/Materials/Shaders lists as every other asset.
    public class AssetBundle
    {
        public string Name { get; set; }
        public string UUID { get; }
        public string RootPath { get; set; }

        public List<string> AssetUUIDs { get; } = new();

        public AssetBundle(string name, string uuid, string rootPath)
        {
            Name = name;
            UUID = uuid;
            RootPath = rootPath;
        }
    }

    public static class AssetBundleLoader
    {
        // Where a bare bundle name (not a path) is looked up.
        public static string BundlesRoot = Path.Combine("..", "..", "..", "Assets");

        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class BundleMeta
        {
            public string? Name { get; set; }

            [JsonPropertyName("Asset")]
            public List<string>? Asset { get; set; }

            public string? UUID { get; set; }
        }

        private sealed class AssetMeta
        {
            public string? Type { get; set; }
            public string? Location { get; set; }
            public string? UUID { get; set; }
            public string? Name { get; set; }
        }

        // -----------------------------------------------------------------
        // Load
        // -----------------------------------------------------------------

        public static AssetBundle LoadAssetBundle(string pathOrName)
        {
            string root = ResolveBundleRoot(pathOrName);

            if (!Directory.Exists(root))
            {
                Logger.Instance.LogError($"LoadAssetBundle: bundle folder not found at '{root}'.");
                return new AssetBundle(pathOrName, Guid.NewGuid().ToString(), root);
            }

            var nlabf = ReadBundleMeta(root);
            string bundleName = nlabf?.Name ?? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar));
            string bundleUuid = nlabf?.UUID ?? Guid.NewGuid().ToString();

            var bundle = new AssetBundle(bundleName, bundleUuid, root);

            string metadataRoot = Path.Combine(root, "Metadata");
            if (!Directory.Exists(metadataRoot))
            {
                Logger.Instance.LogWarning($"LoadAssetBundle: '{bundleName}' has no Metadata folder, nothing to load.");
                Game.Bundles.Add(bundle);
                return bundle;
            }

            var metas = new List<AssetMeta>();
            foreach (string metaFile in Directory.GetFiles(metadataRoot, "*.nlam", SearchOption.AllDirectories))
            {
                AssetMeta? meta;
                try
                {
                    meta = JsonSerializer.Deserialize<AssetMeta>(File.ReadAllText(metaFile), ReadOptions);
                }
                catch (JsonException e)
                {
                    Logger.Instance.LogError($"LoadAssetBundle: '{metaFile}' failed to parse ({e.Message}), skipped.");
                    continue;
                }

                if (meta == null || meta.UUID == null || meta.Type == null || meta.Name == null)
                {
                    Logger.Instance.LogWarning($"LoadAssetBundle: '{metaFile}' is missing Type/Name/UUID, skipped.");
                    continue;
                }

                metas.Add(meta);
            }

            var rawShaders = new Dictionary<string, string>();

            // Directory order is by folder name (Materials before Textures), but a Material looks its
            // textures and shader up by name when it loads, and a Model looks its materials up by name
            // when it parses. So load in dependency order. OrderBy is stable.
            foreach (var meta in metas.OrderBy(m => LoadPriority(m.Type)))
            {
                LoadAssetFromMeta(bundle, root, meta, rawShaders);
                bundle.AssetUUIDs.Add(meta.UUID!);
            }

            Game.Bundles.Add(bundle);
            return bundle;
        }

        private static int LoadPriority(string? type) => type switch
        {
            "Texture" => 0,
            "RawShader" => 1,
            "Shader" => 2,
            "Material" => 3,
            "Model" => 4,
            "Scene" => 5,
            "Component" => 6,
            _ => 7
        };

        private static string ResolveBundleRoot(string pathOrName)
        {
            if (Directory.Exists(pathOrName))
                return Path.GetFullPath(pathOrName);

            return Path.GetFullPath(Path.Combine(BundlesRoot, pathOrName)) + "\\";
        }

        private static BundleMeta? ReadBundleMeta(string root)
        {
            string nlabfPath = Path.Combine(root, "Bundle.nlabf");
            if (!File.Exists(nlabfPath)) return null;

            try
            {
                return JsonSerializer.Deserialize<BundleMeta>(File.ReadAllText(nlabfPath), ReadOptions);
            }
            catch (JsonException e)
            {
                Logger.Instance.LogError($"LoadAssetBundle: 'Bundle.nlabf' at '{root}' failed to parse ({e.Message}).");
                return null;
            }
        }

        private static void LoadAssetFromMeta(AssetBundle bundle, string bundleRoot, AssetMeta meta, Dictionary<string, string> rawShaders)
        {
            string name = meta.Name!;

            string assetPath = meta.Location != null
                ? Path.Combine(bundleRoot, "Assets", meta.Location.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar))
                : "";

            // Deterministic across runs for the same nlam UUID: Guid.GetHashCode()
            // hashes the GUID's actual bytes. string.GetHashCode() would NOT work
            // here - .NET randomizes it per process by default.
            int? uuid = Guid.TryParse(meta.UUID, out Guid parsed) ? parsed.GetHashCode() : null;
            if (uuid == null)
                Logger.Instance.LogWarning($"Bundle '{bundle.Name}': '{name}' has a malformed UUID ('{meta.UUID}'), falling back to a random one.");

            switch (meta.Type)
            {
                case "Texture":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': texture '{name}' missing at '{assetPath}', skipped.");
                        break;
                    }
                    AssetLoader.LoadTexture(name, assetPath, uuid);
                    break;

                case "Model":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': model '{name}' missing at '{assetPath}', skipped.");
                        break;
                    }
                    AssetLoader.LoadMesh(name, assetPath, uuid);
                    break;

                case "Shader":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': shader '{name}' missing at '{assetPath}', skipped.");
                        break;
                    }
                    AssetLoader.LoadShader(name, assetPath, uuid, rawShaders);
                    break;

                case "Material":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': material '{name}' missing at '{assetPath}', skipped.");
                        break;
                    }
                    AssetLoader.LoadMaterial(name, assetPath, uuid);
                    break;

                case "Scene":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': scene '{name}' missing at '{assetPath}', skipped.");
                        break;
                    }
                    Scene parsedScene = SceneSerializer.Load(File.ReadAllText(assetPath), name);
                    SceneManager.Register(name, () => parsedScene); // keyed by Name, matching how Game.cs registers/loads scenes
                    break;

                case "Component":
                    if (!SceneSerializer.ComponentTypes.ContainsKey(name))
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': component type '{name}' not found in engine, skipped.");
                    break;

                case "RawShader":
                    if (!File.Exists(assetPath))
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': raw shader '{name}' missing at '{assetPath}', skipped.");
                    else
                        rawShaders[name] = Path.GetFullPath(assetPath); // used by the Shader entries that load after this
                    break;

                default:
                    Logger.Instance.LogWarning($"Bundle '{bundle.Name}': unknown asset type '{meta.Type}' for '{name}', skipped.");
                    break;
            }
        }

        // -----------------------------------------------------------------
        // Unload
        // -----------------------------------------------------------------

        public static void UnloadAssetBundle(AssetBundle bundle)
        {
            if (bundle == null) return;

            string metadataRoot = Path.Combine(bundle.RootPath, "Metadata");
            if (!Directory.Exists(metadataRoot))
            {
                Logger.Instance.LogWarning($"UnloadAssetBundle: '{bundle.Name}' has no Metadata folder to re-scan; removing the bundle entry only.");
                Game.Bundles.Remove(bundle);
                return;
            }

            var metas = new List<AssetMeta>();
            foreach (string metaFile in Directory.GetFiles(metadataRoot, "*.nlam", SearchOption.AllDirectories))
            {
                AssetMeta? meta;
                try { meta = JsonSerializer.Deserialize<AssetMeta>(File.ReadAllText(metaFile), ReadOptions); }
                catch (JsonException) { continue; } // already logged once on load

                if (meta?.Type != null && meta.Name != null)
                    metas.Add(meta);
            }

            var deletedTextureHandles = new HashSet<int>();

            // Material/Model first: unloading a Material releases the retains
            // it held on its Textures/Shader. Doing this before the Texture/
            // Shader pass means RefCount, checked below, only reflects usage
            // genuinely outside this bundle.
            foreach (var meta in metas)
            {
                if (meta.Type is "Material" or "Model")
                    UnloadAssetFromMeta(bundle, meta, deletedTextureHandles);
            }

            foreach (var meta in metas)
            {
                if (meta.Type is not "Material" and not "Model")
                    UnloadAssetFromMeta(bundle, meta, deletedTextureHandles);
            }

            Game.Bundles.Remove(bundle);
        }

        private static void UnloadAssetFromMeta(AssetBundle bundle, AssetMeta meta, HashSet<int> deletedTextureHandles)
        {
            string name = meta.Name!;

            switch (meta.Type)
            {
                case "Texture":
                    {
                        var texture = FindLoadedAssets.FindTexture(name);
                        if (texture == null || texture == Game.MissingTexture) break;

                        if (texture.RefCount > 0)
                        {
                            Logger.Instance.LogWarning($"UnloadAssetBundle '{bundle.Name}': texture '{name}' still in use (RefCount={texture.RefCount}), left loaded.");
                            break;
                        }

                        Game.Textures.Remove(texture);
                        Game.TextureByUUID.Remove(texture.UUID);
                        if (deletedTextureHandles.Add(texture.Handle))
                            texture.Delete();
                        break;
                    }

                case "Shader":
                    {
                        var shader = FindLoadedAssets.FindShader(name);
                        if (shader == null || shader == Game.MissingShader) break;

                        if (shader.RefCount > 0)
                        {
                            Logger.Instance.LogWarning($"UnloadAssetBundle '{bundle.Name}': shader '{name}' still in use (RefCount={shader.RefCount}), left loaded.");
                            break;
                        }

                        Game.Shaders.Remove(shader);
                        Game.ShaderByUUID.Remove(shader.UUID);
                        shader.DeleteShader();
                        break;
                    }

                case "Material":
                    {
                        var material = FindLoadedAssets.FindMaterial(name);
                        if (material == null || material == Game.MissingMaterial) break;

                        if (material.RefCount > 0)
                        {
                            Logger.Instance.LogWarning($"UnloadAssetBundle '{bundle.Name}': material '{name}' still in use (RefCount={material.RefCount}), left loaded.");
                            break;
                        }

                        material.Textures = Array.Empty<Material._TextureData>(); // releases every texture it retained
                        material.ShaderUUID = 0;                                 // releases the shader it retained
                        Game.Materials.Remove(material);
                        Game.MaterialByUUID.Remove(material.UUID);
                        break;
                    }

                case "Model":
                    {
                        var mesh = FindLoadedAssets.FindMesh(name);
                        if (mesh == null || mesh == Game.MissingMesh) break;

                        if (mesh.RefCount > 0)
                        {
                            Logger.Instance.LogWarning($"UnloadAssetBundle '{bundle.Name}': model '{name}' still in use (RefCount={mesh.RefCount}), left loaded.");
                            break;
                        }

                        Game.Instance?.ReleaseMeshFromCache(mesh);
                        Game.Meshes.Remove(mesh);
                        mesh.Dispose();
                        break;
                    }

                case "Scene":
                    if (SceneManager.IsLoaded(name))
                        Logger.Instance.LogWarning($"UnloadAssetBundle '{bundle.Name}': scene '{name}' is still loaded; dropping the registration but leaving it running.");
                    SceneManager.Unregister(name);
                    break;

                case "Component":
                case "RawShader":
                    // Nothing was loaded as an object for these, nothing to free.
                    break;
            }
        }
    }
}