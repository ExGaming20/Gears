namespace Gears
{
    public enum Layer : int
    {
        Default = 0,
        TransparentFX = 1,
        IgnoreRaycast = 2,
        Water = 3,
        UI = 4,
        Player = 5,
        Enemy = 6,
        Environment = 7,
        Terrain = 8,
        Decal = 9,
        Particles = 10,
        Skybox = 11,
        User12 = 12,
        User13 = 13,
        User14 = 14,
        User15 = 15,
        User16 = 16,
        User17 = 17,
        User18 = 18,
        User19 = 19,
        User20 = 20,
        User21 = 21,
        User22 = 22,
        User23 = 23,
        User24 = 24,
        User25 = 25,
        User26 = 26,
        User27 = 27,
        User28 = 28,
        User29 = 29,
        User30 = 30,
        User31 = 31,
    }

    /// <summary>
    /// Utility for building and testing 32-bit layer bitmasks.
    /// </summary>
    public static class LayerMask
    {
        /// <summary>All 32 layers enabled.</summary>
        public const int Everything = ~0;

        /// <summary>No layers enabled.</summary>
        public const int Nothing = 0;

        /// <summary>Returns a bitmask with every specified layer set.</summary>
        public static int GetMask(params Layer[] layers)
        {
            int mask = 0;
            foreach (var l in layers) mask |= 1 << (int)l;
            return mask;
        }

        /// <summary>Returns true if the mask includes the given layer.</summary>
        public static bool Contains(int mask, Layer layer)
            => (mask & 1 << (int)layer) != 0;

        /// <summary>Converts a single Layer to its one-bit mask.</summary>
        public static int ToMask(Layer layer) => 1 << (int)layer;
    }
}