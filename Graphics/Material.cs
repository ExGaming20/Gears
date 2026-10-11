using System;
using OpenTK.Mathematics;
using Gears;

namespace Gears.Graphics
{
    public class Material
    {
        public string Name { get; set; }
        public int UUID { get; }

        // Incremented/decremented by MeshRenderer.Material's setter.
        public int RefCount { get; private set; }
        public void Retain() => RefCount++;
        public void Release() => RefCount = Math.Max(0, RefCount - 1);

        public string ShaderName { get; set; } = string.Empty;

        private int _shaderUUID;
        public int ShaderUUID
        {
            get => _shaderUUID;
            set
            {
                if (_shaderUUID == value) return;
                FindLoadedAssets.FindShader(null, _shaderUUID)?.Release();
                _shaderUUID = value;
                FindLoadedAssets.FindShader(null, _shaderUUID)?.Retain();
            }
        }

        public _RenderType SeeThroughType { get; set; }

        public Vector4 BaseColor { get; set; } = Vector4.One;
        public Vector4 EmissionColor { get; set; } = Vector4.Zero;

        public _CullMode cullMode { get; set; }
        public float Metallic { get; set; }
        public float Roughness { get; set; }
        public bool ReceiveShadows { get; set; } = true;
        public bool CastShadows { get; set; } = true;
        public bool ZWrite { get; set; } = true;

        private _TextureData[] _textures = Array.Empty<_TextureData>();
        public _TextureData[] Textures
        {
            get => _textures;
            set
            {
                foreach (var t in _textures)
                    FindLoadedAssets.FindTexture(null, t.TextureUUID)?.Release();

                _textures = value ?? Array.Empty<_TextureData>();

                foreach (var t in _textures)
                    FindLoadedAssets.FindTexture(null, t.TextureUUID)?.Retain();
            }
        }

        private static int _nextUUID = Guid.NewGuid().GetHashCode();

        public Material(int? explicitUUID = null)
        {
            UUID = explicitUUID ?? _nextUUID++;
            Name = $"Material_{UUID}";

            ShaderName = "Standard";
            ShaderUUID = 0;

            SeeThroughType = _RenderType.Opaque;

            BaseColor = Vector4.One;
            EmissionColor = Vector4.Zero;

            cullMode = _CullMode.CCW;

            Metallic = 0.0f;
            Roughness = 0.0f;

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
            public string SemanticName { get; set; }

            public Vector2 TextureScale { get; set; }
            public Vector2 TextureOffset { get; set; }
        }
    }
}
