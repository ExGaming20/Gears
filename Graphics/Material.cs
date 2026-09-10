using System;
using OpenTK.Mathematics;

namespace Gears.Graphics
{
    public class Material
    {
        public string Name { get; set; }
        public int UUID { get; }

        public string ShaderName { get; set; } = string.Empty;
        public int ShaderUUID { get; set; }

        public _RenderType SeeThroughType { get; set; }

        public Vector4 BaseColor { get; set; } = Vector4.One;
        public Vector4 EmissionColor { get; set; } = Vector4.Zero;

        public _CullMode cullMode { get; set; }
        public float Metallic { get; set; }
        public float Roughness { get; set; }
        public bool ReceiveShadows { get; set; } = true;
        public bool CastShadows { get; set; } = true;
        public bool ZWrite { get; set; } = true;

        public _TextureData[] Textures { get; set; } = Array.Empty<_TextureData>();

        private static int _nextUUID = 0;

        public Material()
        {
            UUID = _nextUUID++;
            Name = $"Material_{UUID}";

            ShaderName = "Standard";
            ShaderUUID = 0;

            SeeThroughType = _RenderType.Opaque;

            BaseColor = Vector4.One;        // White, fully opaque
            EmissionColor = Vector4.Zero;   // No emission

            cullMode = _CullMode.CCW;

            Metallic = 0.0f;                // Non-metallic by default
            Roughness = 0.0f;               // Non-rough by default

            ReceiveShadows = true;
            CastShadows = true;

            ZWrite = true;
        }

        public enum _RenderType
        {
            Opaque,
            Transparent
        }

        public enum _CullMode
        {
            CCW,
            CW,
            Neither
        }

        public struct _TextureData
        {
            public int TextureUUID { get; set; }
            public string TextureName { get; set; }

            public Vector2 TextureScale { get; set; }
            public Vector2 TextureOffset { get; set; }
        }
    }
}
