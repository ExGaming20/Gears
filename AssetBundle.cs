using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gears.Graphics;
using Gears.World;
using Gears.Utilities;

namespace Gears
{
    // A loaded asset bundle. Each asset type has its own UUID-keyed
    // registry, populated once by AssetBundleLoader.LoadAssetBundle.
    public class AssetBundle
    {
        public string Name { get; }
        public string RootPath { get; }

        internal readonly Dictionary<string, Texture> Textures = new();
        internal readonly Dictionary<string, Mesh> Meshes = new();
        internal readonly Dictionary<string, Shader> Shaders = new();
        internal readonly Dictionary<string, Material> Materials = new();
        internal readonly Dictionary<string, Scene> Scenes = new();
        internal readonly Dictionary<string, Type> Components = new();
        internal readonly Dictionary<string, string> RawShaderPaths = new();

        public AssetBundle(string name, string rootPath)
        {
            Name = name;
            RootPath = rootPath;
        }

        public Texture GetTexture(string uuid) => Textures.TryGetValue(uuid, out var t) ? t : Game.MissingTexture;
        public Mesh GetMesh(string uuid) => Meshes.TryGetValue(uuid, out var m) ? m : Game.MissingMesh;
        public Shader GetShader(string uuid) => Shaders.TryGetValue(uuid, out var s) ? s : Game.MissingShader;
        public Material GetMaterial(string uuid) => Materials.TryGetValue(uuid, out var mat) ? mat : Game.MissingMaterial;

        // Scenes, components, and raw shader paths have no engine-wide
        // fallback object, so a miss is null, not a placeholder.
        public Scene? GetScene(string uuid) => Scenes.TryGetValue(uuid, out var sc) ? sc : null;
        public Type? GetComponentType(string uuid) => Components.TryGetValue(uuid, out var t) ? t : null;
        public string? GetRawShaderPath(string uuid) => RawShaderPaths.TryGetValue(uuid, out var p) ? p : null;
    }

    public static class AssetBundleLoader
    {
        // Where a bare bundle name (not a path) is looked up.
        public static string BundlesRoot = Path.Combine("..", "..", "..", "Bundles");

        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class BundleMeta
        {
            public string? Name { get; set; }

            [JsonPropertyName("Assets Held")]
            public List<string>? AssetsHeld { get; set; }

            public string? UUID { get; set; }
        }

        private sealed class AssetMeta
        {
            public string? Type { get; set; }
            public string? Location { get; set; }
            public string? UUID { get; set; }
            public string? Name { get; set; }
        }

        public static AssetBundle LoadAssetBundle(string pathOrName)
        {
            string root = ResolveBundleRoot(pathOrName);

            if (!Directory.Exists(root))
            {
                Logger.Instance.LogError($"LoadAssetBundle: bundle folder not found at '{root}'.");
                return new AssetBundle(pathOrName, root);
            }

            string bundleName = ReadBundleName(root) ?? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar));
            var bundle = new AssetBundle(bundleName, root);

            string metadataRoot = Path.Combine(root, "Metadata");
            if (!Directory.Exists(metadataRoot))
            {
                Logger.Instance.LogWarning($"LoadAssetBundle: '{bundleName}' has no Metadata folder, nothing to load.");
                return bundle;
            }

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

                ProcessAssetMeta(bundle, root, meta);
            }

            return bundle;
        }

        private static string ResolveBundleRoot(string pathOrName)
        {
            if (Directory.Exists(pathOrName))
                return Path.GetFullPath(pathOrName);

            return Path.GetFullPath(Path.Combine(BundlesRoot, pathOrName));
        }

        private static string? ReadBundleName(string root)
        {
            string nlabfPath = Path.Combine(root, "Bundle.nlabf");
            if (!File.Exists(nlabfPath)) return null;

            try
            {
                var meta = JsonSerializer.Deserialize<BundleMeta>(File.ReadAllText(nlabfPath), ReadOptions);
                return meta?.Name;
            }
            catch (JsonException e)
            {
                Logger.Instance.LogError($"LoadAssetBundle: 'Bundle.nlabf' at '{root}' failed to parse ({e.Message}).");
                return null;
            }
        }

        private static void ProcessAssetMeta(AssetBundle bundle, string bundleRoot, AssetMeta meta)
        {
            string uuid = meta.UUID!;
            string name = meta.Name!;

            string assetPath = meta.Location != null
                ? Path.Combine(bundleRoot, "Assets", meta.Location.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar))
                : "";

            switch (meta.Type)
            {
                case "Texture":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': texture '{name}' missing at '{assetPath}', using fallback.");
                        bundle.Textures[uuid] = Game.MissingTexture;
                        break;
                    }
                    AssetLoader.LoadTexture(name, assetPath);
                    bundle.Textures[uuid] = FindLoadedAssets.FindTexture(name) ?? Game.MissingTexture;
                    break;

                case "Model":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': model '{name}' missing at '{assetPath}', using fallback.");
                        bundle.Meshes[uuid] = Game.MissingMesh;
                        break;
                    }
                    AssetLoader.LoadMesh(name, assetPath);
                    bundle.Meshes[uuid] = FindLoadedAssets.FindMesh(name) ?? Game.MissingMesh;
                    break;

                case "Shader":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': shader '{name}' missing at '{assetPath}', using fallback.");
                        bundle.Shaders[uuid] = Game.MissingShader;
                        break;
                    }
                    AssetLoader.LoadShader(name, assetPath);
                    bundle.Shaders[uuid] = FindLoadedAssets.FindShader(name) ?? Game.MissingShader;
                    break;

                case "Material":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': material '{name}' missing at '{assetPath}', using fallback.");
                        bundle.Materials[uuid] = Game.MissingMaterial;
                        break;
                    }
                    AssetLoader.LoadMaterial(name, assetPath);
                    bundle.Materials[uuid] = FindLoadedAssets.FindMaterial(name) ?? Game.MissingMaterial;
                    break;

                case "Scene":
                    if (!File.Exists(assetPath))
                    {
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': scene '{name}' missing at '{assetPath}', skipped (no scene fallback).");
                        break;
                    }
                    bundle.Scenes[uuid] = SceneSerializer.Load(File.ReadAllText(assetPath), name);
                    break;

                case "Component":
                    if (SceneSerializer.ComponentTypes.TryGetValue(name, out var componentType))
                        bundle.Components[uuid] = componentType;
                    else
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': component type '{name}' not found in engine, skipped.");
                    break;

                case "RawShader":
                    if (!File.Exists(assetPath))
                        Logger.Instance.LogWarning($"Bundle '{bundle.Name}': raw shader '{name}' missing at '{assetPath}', skipped.");
                    else
                        bundle.RawShaderPaths[uuid] = assetPath;
                    break;

                default:
                    Logger.Instance.LogWarning($"Bundle '{bundle.Name}': unknown asset type '{meta.Type}' for '{name}', skipped.");
                    break;
            }
        }
    }
}