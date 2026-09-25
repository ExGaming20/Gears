using OpenTK.Mathematics;
using Gears.World.Components;

namespace Gears
{
    public readonly struct LightRenderData
    {
        /// <summary>Matches <see cref="Light.LightType"/>: 0=Directional 1=Point 2=Spot 3=Area.</summary>
        public readonly int Type;

        public readonly Vector3 Position;
        public readonly Vector3 Direction;

        /// <summary>Linear RGB color already multiplied by intensity.</summary>
        public readonly Vector3 Color;

        public readonly Vector2 Size; // for area lights: width and height (rectangle) or radius (disc)

        public readonly float Intensity;
        public readonly float Range;
        public readonly float InnerConeCos;
        public readonly float OuterConeCos;
        public readonly float ShadowStrength;

        /// <summary>Layer bitmask: which layers this light affects.</summary>
        public readonly int RenderingLayers;

        public readonly bool CastsShadows;
        public readonly bool SoftShadows;
        public readonly float ShadowBias;
        public readonly float ShadowNormalBias;
        public readonly float ShadowNearPlane;
        public readonly int ShadowResolution;

        public LightRenderData(Light light)
        {
            var tf = light.Transform;

            this.Type = (int)light.lightType;
            Position = tf?.Position ?? Vector3.Zero;
            Direction = tf != null ? Vector3.Normalize(tf.Forward) : -Vector3.UnitY;
            Intensity = light.intensity;
            Color = light.GetEffectiveLightColor() * Intensity;
            Range = light.range;
            InnerConeCos = MathF.Cos(MathHelper.DegreesToRadians(light.innerConeAngle * 0.5f));
            OuterConeCos = MathF.Cos(MathHelper.DegreesToRadians(light.outerConeAngle * 0.5f));
            ShadowStrength = light.shadowStrength;
            Size = light.lightType == Light.LightType.Area ? light.widthHeight : (Vector2.One * light.radius);
            RenderingLayers = light.renderingLayers;

            CastsShadows = light.shadowType != Light.ShadowType.None;
            SoftShadows = light.shadowType == Light.ShadowType.Soft;
            ShadowBias = light.shadowBias;
            ShadowNormalBias = light.shadowNormalBias;
            ShadowNearPlane = light.shadowNearPlane;
            ShadowResolution = (int)light.shadowResolution;
        }
    }

    /// <summary>
    /// Extension methods for Light to compute effective color.
    /// </summary>
    public static class LightExtensions
    {
        /// <summary>
        /// Returns the effective linear RGB color, taking EmissionType into account.
        /// Assumes Light has properties: Color (Vector3), Temperature (float), EmissionType (enum with Color/Temperature).
        /// </summary>
        public static Vector3 GetEffectiveLightColor(this Light light)
        {
            switch (light.emissionType)
            {
                case Light.EmissionType.Color:
                    return new Vector3(light.color.R, light.color.G, light.color.B);
                case Light.EmissionType.Temperature:
                    return ColorTemperatureToRgb(light.temperature);
                default:
                    return Vector3.One;
            }
        }

        private static Vector3 ColorTemperatureToRgb(float kelvin)
        {
            float temp = kelvin / 100.0f;
            float r, g, b;
            if (temp <= 66.0f)
            {
                r = 255.0f;
                g = 99.4708025861f * MathF.Log(temp) - 161.1195681661f;
                if (temp <= 19.0f)
                    b = 0.0f;
                else
                    b = 138.5177312231f * MathF.Log(temp - 10.0f) - 305.0447927307f;
            }
            else
            {
                r = 329.698727446f * MathF.Pow(temp - 60.0f, -0.1332047592f);
                g = 288.1221695283f * MathF.Pow(temp - 60.0f, -0.0755148492f);
                b = 255.0f;
            }
            return new Vector3(
                MathHelper.Clamp(r / 255.0f, 0.0f, 1.0f),
                MathHelper.Clamp(g / 255.0f, 0.0f, 1.0f),
                MathHelper.Clamp(b / 255.0f, 0.0f, 1.0f)
            );
        }
    }

    public static class LightManager
    {
        private static readonly List<Light> _staticLights = new();
        private static readonly List<Light> _dynamicLights = new();

        public static IReadOnlyList<Light> StaticLights => _staticLights;
        public static IReadOnlyList<Light> DynamicLights => _dynamicLights;

        private static readonly List<LightRenderData> _staticData = new();
        private static readonly List<LightRenderData> _dynamicData = new();
        private static readonly List<LightRenderData> _allData = new();
        private static bool _allDirty = true;

        public static IReadOnlyList<LightRenderData> AllData
        {
            get
            {
                if (_allDirty)
                {
                    _allData.Clear();
                    _allData.AddRange(_staticData);
                    _allData.AddRange(_dynamicData);
                    _allDirty = false;
                }
                return _allData;
            }
        }

        public static void Register(Light light)
        {
            if (light.mode == Light.Mode.Baked) return;

            bool isStatic = light.GameObject?.IsStatic ?? false;

            if (isStatic)
            {
                if (!_staticLights.Contains(light))
                {
                    _staticLights.Add(light);
                    RebuildStaticData();
                }
            }
            else
            {
                if (!_dynamicLights.Contains(light))
                    _dynamicLights.Add(light);
            }
        }

        public static void Unregister(Light light)
        {
            if (_staticLights.Remove(light))
                RebuildStaticData();
            else
                _dynamicLights.Remove(light);
        }

        public static void RebuildStaticData()
        {
            _staticData.Clear();
            foreach (var light in _staticLights)
                _staticData.Add(new LightRenderData(light));

            _allDirty = true;
        }

        public static void UpdateDynamicData()
        {
            _dynamicData.Clear();
            foreach (var light in _dynamicLights)
            {
                if (light.mode == Light.Mode.Baked) continue;
                _dynamicData.Add(new LightRenderData(light));
            }

            _allDirty = true;
        }

        public static List<LightRenderData> GetLightsForLayer(Layer objectLayer, int maxLights = 8)
        {
            var result = new List<LightRenderData>(maxLights);
            int layerMask = 1 << (int)objectLayer;

            foreach (var d in _staticData)
            {
                if (result.Count >= maxLights) break;
                if ((d.RenderingLayers & layerMask) != 0) result.Add(d);
            }
            foreach (var d in _dynamicData)
            {
                if (result.Count >= maxLights) break;
                if ((d.RenderingLayers & layerMask) != 0) result.Add(d);
            }

            return result;
        }

        public static void Clear()
        {
            _staticLights.Clear();
            _dynamicLights.Clear();
            _staticData.Clear();
            _dynamicData.Clear();
            _allData.Clear();
            _allDirty = true;
        }
    }
}