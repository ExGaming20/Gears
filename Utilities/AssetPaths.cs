using System;
using System.IO;

namespace Gears.Utilities
{
    // All game assets are copied next to the executable by the build and publish
    // steps, so resolve them from AppContext.BaseDirectory instead of relying on
    // the working directory.
    internal static class AssetPaths
    {
        public static string Root => AppContext.BaseDirectory;

        public static string Assets => Path.Combine(Root, "Assets");

        public static string FallbackAssets => Path.Combine(Root, "Fallback Assets");

        public static string InAssets(params string[] parts) => Combine(Assets, parts);

        public static string InFallbackAssets(params string[] parts) => Combine(FallbackAssets, parts);

        private static string Combine(string root, string[] parts)
        {
            string path = root;
            foreach (string part in parts)
                path = Path.Combine(path, part);

            return path;
        }
    }
}
