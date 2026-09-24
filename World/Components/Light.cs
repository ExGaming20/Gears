using Gears.Graphics;
using OpenTK.Mathematics;

namespace Gears.World.Components
{
    public class Light : BaseComponent
    {
        // -----------------------------------------------------------------------
        // General
        // -----------------------------------------------------------------------

        public LightType lightType = LightType.Point;

        /// <summary>
        /// Realtime: always rendered. Mixed: direct light baked, shadows realtime.
        /// Baked: skipped at runtime (fully pre-computed). 
        /// </summary>
        public Mode mode = Mode.Realtime;

        /// <summary>
        /// LayerMask bitmask of which GameObject layers this light illuminates.
        /// Defaults to <see cref="LayerMask.Everything"/>.
        /// </summary>
        public int renderingLayers = LayerMask.Everything;

        // -----------------------------------------------------------------------
        // Spot only
        // -----------------------------------------------------------------------

        public float innerConeAngle = 35f; // degrees — soft inner edge
        public float outerConeAngle = 45f; // degrees — hard outer edge

        // -----------------------------------------------------------------------
        // Area only
        // -----------------------------------------------------------------------

        public Shape shape = Shape.Rectangle;
        public Vector2 widthHeight = Vector2.One; // rectangle extents in world units
        public float radius = 1f;                 // disc radius in world units

        // -----------------------------------------------------------------------
        // Emission
        // -----------------------------------------------------------------------

        public EmissionType emissionType = EmissionType.Color;
        public Color4 color = Color4.White;
        public float temperature = 4000f; // Kelvin — used when emissionType = Temperature
        public float intensity = 0.75f;
        public float range = 10f;         // attenuation radius (Point / Spot)

        // -----------------------------------------------------------------------
        // Cookie (gobo — a texture multiplied over the light output)
        // -----------------------------------------------------------------------

        /// <summary>UUID of the cookie texture. -1 means no cookie.</summary>
        public int cookieTextureUUID = -1;
        public Vector2 cookieScale = Vector2.One;
        public Vector2 cookieOffset = Vector2.Zero;

        // -----------------------------------------------------------------------
        // Shadow
        // -----------------------------------------------------------------------

        public ShadowType shadowType = ShadowType.Hard;
        /// <summary>Shadow darkness from 0 (invisible) to 1 (fully opaque).</summary>
        public float shadowStrength = 1f;
        public float shadowNearPlane = 0.1f;
        public float shadowBias = 0.005f;  // depth bias to avoid shadow acne
        public float shadowNormalBias = 0.02f;   // normal-offset bias
        public ShadowResolution shadowResolution = ShadowResolution.Medium;

        // -----------------------------------------------------------------------
        // Runtime helpers
        // -----------------------------------------------------------------------
        public Vector3 GetLinearColorRGB()
        {
            Vector4 col = emissionType == EmissionType.Color
                ? new Vector4(color.R, color.G, color.B, color.A)
                : TemperatureToColor(temperature);
            return col.Xyz; // ignore alpha
        }

        public LightRenderData BuildLightRenderData()
        {
            return new LightRenderData(this);
        }

        // -----------------------------------------------------------------------
        // Self-registration into LightManager
        // -----------------------------------------------------------------------

        public override void OnEnable()
        {
            base.OnEnable();
            // Baked lights are pre-computed; they don't participate in realtime rendering
            if (mode == Mode.Baked) return;
            LightManager.Register(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            LightManager.Unregister(this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            LightManager.Unregister(this);
        }

        // -----------------------------------------------------------------------
        // Blackbody approximation  (Kelvin → linear RGB)
        // Tanner Helland algorithm — valid from ~1000 K to ~12 000 K
        // -----------------------------------------------------------------------
        public static Vector4 TemperatureToColor(float kelvin)
        {
            float t = Math.Clamp(kelvin, 1000f, 12000f) / 100f;

            float r, g, b;

            // Red
            if (t <= 66f)
                r = 1f;
            else
            {
                r = 329.698727446f * MathF.Pow(t - 60f, -0.1332047592f);
                r = Math.Clamp(r / 255f, 0f, 1f);
            }

            // Green
            if (t <= 66f)
            {
                g = 99.4708025861f * MathF.Log(t) - 161.1195681661f;
                g = Math.Clamp(g / 255f, 0f, 1f);
            }
            else
            {
                g = 288.1221695283f * MathF.Pow(t - 60f, -0.0755148492f);
                g = Math.Clamp(g / 255f, 0f, 1f);
            }

            // Blue
            if (t >= 66f)
                b = 1f;
            else if (t <= 19f)
                b = 0f;
            else
            {
                b = 138.5177312231f * MathF.Log(t - 10f) - 305.0447927307f;
                b = Math.Clamp(b / 255f, 0f, 1f);
            }

            return new Vector4(r, g, b, 1f);
        }

        // -----------------------------------------------------------------------
        // Enums
        // -----------------------------------------------------------------------

        public enum LightType : int
        {
            Directional = 0,
            Point = 1,
            Spot = 2,
            Area = 3,
        }

        public enum Mode
        {
            Realtime,
            Mixed,
            Baked
        }

        public enum Shape
        {
            Rectangle,
            Disc
        }

        public enum EmissionType
        {
            Color,
            Temperature
        }

        public enum ShadowType : int
        {
            None = 0,
            Hard = 1,
            Soft = 2,
        }

        /// <summary>Shadow map resolution in texels.</summary>
        public enum ShadowResolution : int
        {
            Toaster = 32,
            Potato = 64,
            ExtremelyLow = 128,
            VeryLow = 256,
            Low = 512,
            Medium = 1024,
            High = 2048,
            VeryHigh = 4096,
            Ultra = 8192,
            Epic = 16384
        }
    }

    public static class LightExtensions
    {
        public static Vector3 GetEffectiveLightColor(this Light light)
        {
            return light.GetLinearColorRGB();
        }
    }

    public struct LightShaderData
    {
        public int type; // 0=Directional, 1=Point, 2=Spot, 3=Area
        public Vector3 color;
        public Vector2 size; // for area lights: width and height (rectangle) or radius (disc)
        public float intensity;
        public Vector3 position;
        public float range;
        public Vector3 direction;
        public float innerConeCos;
        public float outerConeCos;
        public int shadowType; // 0=None, 1=Hard, 2=Soft
        public float shadowStrength;
        public float shadowBias;

        public LightShaderData(Light light)
        {
            type = (int)light.lightType;
            color = light.GetLinearColorRGB();
            size = light.lightType == Light.LightType.Area ? light.widthHeight : new Vector2(light.radius, 0f);
            intensity = light.intensity;
            position = light.GameObject.Transform.Position;
            range = light.range;
            direction = light.GameObject.Transform.Forward;
            innerConeCos = MathF.Cos(MathHelper.DegreesToRadians(light.innerConeAngle));
            outerConeCos = MathF.Cos(MathHelper.DegreesToRadians(light.outerConeAngle));
            shadowType = (int)light.shadowType;
            shadowStrength = light.shadowStrength;
            shadowBias = light.shadowBias;
        }

        public LightShaderData(LightRenderData lightData)
        {
            type = (int)lightData.Type;
            color = lightData.Color;
            size = Vector2.Zero; // LightRenderData doesn't contain size information
            intensity = lightData.Intensity;
            position = lightData.Position;
            range = lightData.Range;
            direction = lightData.Direction;
            innerConeCos = lightData.InnerConeCos;
            outerConeCos = lightData.OuterConeCos;
            shadowType = 0; // LightRenderData doesn't contain shadowType
            shadowStrength = lightData.ShadowStrength;
            shadowBias = 0f; // LightRenderData doesn't contain shadowBias
        }
    }
}