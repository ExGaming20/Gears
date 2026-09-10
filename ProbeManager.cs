using OpenTK.Mathematics;
using Gears.Graphics;
using Gears.World.Components;

namespace Gears
{
    /// <summary>
    /// Snapshot of an EProbe's data as consumed by the renderer/shader. Holds direct Texture
    /// references rather than UUIDs — EProbe's Color/Depth textures aren't registered in
    /// Game.TextureByUUID (that registry is only populated for RenderTexture-backed textures),
    /// so a UUID lookup would silently miss them.
    /// </summary>
    public readonly struct ProbeRenderData
    {
        public readonly Vector3 Position;
        public readonly Vector3 Domein;
        public readonly Texture? Color;
        public readonly Texture? Depth;
        public readonly float Near;
        public readonly float Far;

        public ProbeRenderData(EProbe probe)
        {
            var tf = probe.Transform;
            Position = tf?.Position ?? Vector3.Zero;
            Domein = probe.Domein;
            Color = probe.Color;
            Depth = probe.Depth;
            Near = probe.Near;
            Far = probe.Far;
        }
    }

    public static class ProbeManager
    {
        private static readonly List<EProbe> _probes = new();
        public static IReadOnlyList<EProbe> Probes => _probes;

        public static void Register(EProbe probe)
        {
            if (!_probes.Contains(probe))
                _probes.Add(probe);
        }

        public static void Unregister(EProbe probe)
        {
            _probes.Remove(probe);
        }

        /// <summary>
        /// Finds the best EProbe for a world position. A probe "contains" the point if it's
        /// within the probe's Domein box (half-extents, centered on the probe). Among probes that
        /// contain the point, the smallest-volume one wins (a small, specific probe should be
        /// preferred over a large, general one even if the large one's center happens to be
        /// closer) — distance only breaks ties between equal-volume probes. If none contain the
        /// point, falls back to the nearest probe overall so shading never goes without a probe as
        /// long as one exists.
        /// </summary>
        public static EProbe? GetNearestProbe(Vector3 worldPosition)
        {
            EProbe? bestContaining = null;
            float bestContainingVolume = float.MaxValue;
            float bestContainingDistSq = float.MaxValue;

            EProbe? bestOverall = null;
            float bestOverallDistSq = float.MaxValue;

            foreach (var probe in _probes)
            {
                if (probe.Transform == null) continue;

                Vector3 delta = worldPosition - probe.Transform.Position;
                float distSq = delta.LengthSquared;

                if (distSq < bestOverallDistSq)
                {
                    bestOverall = probe;
                    bestOverallDistSq = distSq;
                }

                bool inDomein = MathF.Abs(delta.X) <= probe.Domein.X &&
                                MathF.Abs(delta.Y) <= probe.Domein.Y &&
                                MathF.Abs(delta.Z) <= probe.Domein.Z;

                if (!inDomein) continue;

                float volume = probe.Domein.X * probe.Domein.Y * probe.Domein.Z * 8f;
                bool better = volume < bestContainingVolume ||
                              (volume == bestContainingVolume && distSq < bestContainingDistSq);

                if (better)
                {
                    bestContaining = probe;
                    bestContainingVolume = volume;
                    bestContainingDistSq = distSq;
                }
            }

            return bestContaining ?? bestOverall;
        }

        public static ProbeRenderData? GetNearestProbeData(Vector3 worldPosition)
        {
            var probe = GetNearestProbe(worldPosition);
            return probe != null ? new ProbeRenderData(probe) : null;
        }

        public static void Clear()
        {
            _probes.Clear();
        }
    }
}