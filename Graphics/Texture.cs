using System.IO;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;
using Gears.Utilities;

namespace Gears.Graphics
{
    public class Texture
    {
        public int Handle { get; }
        public int UUID { get; }
        public string Name { get; set; }
        public string FilePath { get; private set; }
        public Vector2i Size { get; private set; }

        private TextureTarget _target;
        private readonly PixelInternalFormat _internalFormat;
        private readonly PixelFormat _format;
        private readonly PixelType _pixelType = PixelType.UnsignedByte;
        private readonly TextureWrapMode _wrapS;
        private readonly TextureWrapMode _wrapT;
        private readonly TextureMinFilter _minFilter;
        private readonly TextureMagFilter _magFilter;

        private static int _nextUUID = 0;

        public Texture(string name, string? filePath = null, TextureData[]? textureData = null, uint[]? rawImage = null)
        {
            Name = name;
            FilePath = filePath ?? "";
            _textureData = textureData ?? Array.Empty<TextureData>();

            _target = ConvertToTextureTarget(GetTextureDataValueOrDefault(TextureDataType.TextureTarget, TextureDataValue.Texture2D), TextureTarget.Texture2D);
            _internalFormat = ConvertToPixelInternalFormat(GetTextureDataValueOrDefault(TextureDataType.PixelFormat, TextureDataValue.Rgba), PixelInternalFormat.Rgba);
            _format = ConvertToPixelFormat(GetTextureDataValueOrDefault(TextureDataType.PixelFormat, TextureDataValue.Rgba), PixelFormat.Rgba);
            _wrapS = ConvertToTextureWrapMode(GetTextureDataValueOrDefault(TextureDataType.TextureWrap, TextureDataValue.Repeat), TextureWrapMode.Repeat);
            _wrapT = _wrapS;
            _minFilter = ConvertToTextureMinFilter(GetTextureDataValueOrDefault(TextureDataType.Mipmap, TextureDataValue.LinearMipmapLinear), TextureMinFilter.LinearMipmapLinear);
            _magFilter = ConvertToTextureMagFilter(GetTextureDataValueOrDefault(TextureDataType.TextureFilter, TextureDataValue.Linear), TextureMagFilter.Linear);

            ImageResult? image = null;

            if (filePath != null)
            {
                string resolvedPath = filePath;
                if (!Path.IsPathFullyQualified(filePath))
                    resolvedPath = Path.Combine("..", "..", "..", "Fallback Assets", "Textures", "Missing.png");
                else
                    resolvedPath = filePath;

                if (!File.Exists(resolvedPath))
                {
                    Logger.Instance.LogError($"Texture file not found: {resolvedPath}");
                    if (Game.MissingTexture != null && Game.MissingTexture != this)
                    {
                        Handle = Game.MissingTexture.Handle;
                        UUID = _nextUUID++;
                        return;
                    }
                    Handle = -1;
                    UUID = -1;
                    return;
                }

                using FileStream stream = File.OpenRead(resolvedPath);
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                FilePath = resolvedPath;
            }
            else if (rawImage != null)
            {
                Logger.Instance.LogError("Raw uint[] image not supported in this constructor.");
                Handle = -1;
                UUID = -1;
                return;
            }
            else
            {
                Logger.Instance.LogError("No texture source provided.");
                Handle = -1;
                UUID = -1;
                return;
            }

            if (image == null)
            {
                Handle = -1;
                UUID = -1;
                return;
            }

            Handle = GL.GenTexture();
            Use(0);

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexImage2D(_target, 0, _internalFormat, image.Width, image.Height, 0, _format, _pixelType, image.Data);

            GL.TexParameter(_target, TextureParameterName.TextureWrapS, (int)_wrapS);
            GL.TexParameter(_target, TextureParameterName.TextureWrapT, (int)_wrapT);
            GL.TexParameter(_target, TextureParameterName.TextureMinFilter, (int)_minFilter);
            GL.TexParameter(_target, TextureParameterName.TextureMagFilter, (int)_magFilter);

            GL.GenerateMipmap((GenerateMipmapTarget)_target);

            Size = new Vector2i(image.Width, image.Height);
            UUID = _nextUUID++;
        }

        public Texture(string name, Vector2i size,
            PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
            PixelFormat format = PixelFormat.Rgba,
            PixelType pixelType = PixelType.UnsignedByte,
            TextureWrapMode wrap = TextureWrapMode.ClampToEdge,
            TextureMinFilter minFilter = TextureMinFilter.Linear,
            TextureMagFilter magFilter = TextureMagFilter.Linear)
        {
            Name = name;
            FilePath = "";
            _textureData = Array.Empty<TextureData>();

            _target = TextureTarget.Texture2D;
            _internalFormat = internalFormat;
            _format = format;
            _pixelType = pixelType;
            _wrapS = wrap;
            _wrapT = wrap;
            _minFilter = minFilter;
            _magFilter = magFilter;

            Handle = GL.GenTexture();
            Use(0);

            GL.TexImage2D(_target, 0, _internalFormat, size.X, size.Y, 0, _format, _pixelType, IntPtr.Zero);

            GL.TexParameter(_target, TextureParameterName.TextureWrapS, (int)_wrapS);
            GL.TexParameter(_target, TextureParameterName.TextureWrapT, (int)_wrapT);
            GL.TexParameter(_target, TextureParameterName.TextureMinFilter, (int)_minFilter);
            GL.TexParameter(_target, TextureParameterName.TextureMagFilter, (int)_magFilter);

            Size = size;
            UUID = _nextUUID++;
        }

        /// <summary>
        /// Loads a single 2D image file with explicit wrap modes and correct relative-path
        /// resolution (relative to Assets/Textures) — the general file-based constructor above
        /// treats any non-fully-qualified path as invalid, which is fine for AssetLoader's
        /// absolute paths but wrong for a plain filename like a skybox texture.
        ///
        /// This is what an equirectangular (lat-long, typically 2:1) skybox source image wants:
        /// a normal sampler2D with wrapS = Repeat (longitude wraps around) and wrapT = ClampToEdge
        /// (latitude doesn't — the poles are real edges), sampled in-shader via a direction-to-UV
        /// conversion rather than GL_TEXTURE_CUBE_MAP. On any load failure this falls back to
        /// Game.MissingTexture, same as the general file-based constructor does.
        /// </summary>
        public Texture(string name, string filePath, TextureWrapMode wrapS,
            TextureWrapMode wrapT = TextureWrapMode.ClampToEdge,
            TextureMinFilter minFilter = TextureMinFilter.LinearMipmapLinear,
            TextureMagFilter magFilter = TextureMagFilter.Linear)
        {
            Name = name;
            FilePath = "";
            _textureData = Array.Empty<TextureData>();

            _target = TextureTarget.Texture2D;
            _internalFormat = PixelInternalFormat.Rgba;
            _format = PixelFormat.Rgba;
            _pixelType = PixelType.UnsignedByte;
            _wrapS = wrapS;
            _wrapT = wrapT;
            _minFilter = minFilter;
            _magFilter = magFilter;

            string resolvedPath = Path.IsPathFullyQualified(filePath)
                ? filePath
                : Path.Combine("..", "..", "..", "Assets", "Textures", filePath);

            if (!File.Exists(resolvedPath))
            {
                Logger.Instance.LogError($"Texture file not found: {resolvedPath}");
                if (Game.MissingTexture != null && Game.MissingTexture != this)
                {
                    Handle = Game.MissingTexture.Handle;
                    UUID = _nextUUID++;
                    return;
                }
                Handle = -1;
                UUID = -1;
                return;
            }

            using FileStream stream = File.OpenRead(resolvedPath);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            FilePath = resolvedPath;

            Handle = GL.GenTexture();
            Use(0);

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexImage2D(_target, 0, _internalFormat, image.Width, image.Height, 0, _format, _pixelType, image.Data);

            GL.TexParameter(_target, TextureParameterName.TextureWrapS, (int)_wrapS);
            GL.TexParameter(_target, TextureParameterName.TextureWrapT, (int)_wrapT);
            GL.TexParameter(_target, TextureParameterName.TextureMinFilter, (int)_minFilter);
            GL.TexParameter(_target, TextureParameterName.TextureMagFilter, (int)_magFilter);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            Size = new Vector2i(image.Width, image.Height);
            UUID = _nextUUID++;
        }

        /// <summary>
        /// Creates a texture with a fixed, explicit GL target (e.g. TextureCubeMap). Unlike the
        /// Texture2D-only constructor above, this never assumes Texture2D — a texture's target is
        /// fixed at first bind in OpenGL and can't be changed later, which is what made the old
        /// skybox loading path (2D constructor + SetImageFromFilePath(..., TextureCubeMap)) invalid.
        /// For TextureCubeMap, allocates storage for all 6 faces at faceSize; follow up with
        /// LoadCubemapCross to fill them in. Only useful for genuine per-face cube data — an
        /// equirectangular skybox image should use the (name, filePath, wrapS) constructor instead.
        /// </summary>
        public Texture(string name, TextureTarget target, Vector2i faceSize,
            PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
            PixelFormat format = PixelFormat.Rgba,
            PixelType pixelType = PixelType.UnsignedByte,
            TextureWrapMode wrap = TextureWrapMode.ClampToEdge,
            TextureMinFilter minFilter = TextureMinFilter.Linear,
            TextureMagFilter magFilter = TextureMagFilter.Linear)
        {
            Name = name;
            FilePath = "";
            _textureData = Array.Empty<TextureData>();

            _target = target;
            _internalFormat = internalFormat;
            _format = format;
            _pixelType = pixelType;
            _wrapS = wrap;
            _wrapT = wrap;
            _minFilter = minFilter;
            _magFilter = magFilter;

            Handle = GL.GenTexture();
            Size = faceSize;
            UUID = _nextUUID++;

            if (_target == TextureTarget.TextureCubeMap)
            {
                GL.BindTexture(TextureTarget.TextureCubeMap, Handle);

                for (int face = 0; face < 6; face++)
                {
                    GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + face, 0, _internalFormat,
                        faceSize.X, faceSize.Y, 0, _format, _pixelType, IntPtr.Zero);
                }

                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)_wrapS);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)_wrapT);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)_wrapS);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)_minFilter);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)_magFilter);

                GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            }
            else
            {
                Use(0);
                GL.TexImage2D(_target, 0, _internalFormat, faceSize.X, faceSize.Y, 0, _format, _pixelType, IntPtr.Zero);
                GL.TexParameter(_target, TextureParameterName.TextureWrapS, (int)_wrapS);
                GL.TexParameter(_target, TextureParameterName.TextureWrapT, (int)_wrapT);
                GL.TexParameter(_target, TextureParameterName.TextureMinFilter, (int)_minFilter);
                GL.TexParameter(_target, TextureParameterName.TextureMagFilter, (int)_magFilter);
            }
        }

        /// <summary>
        /// Loads a single "horizontal cross" layout image (4 cols x 3 rows) and uploads it as this
        /// texture's 6 cubemap faces. Requires this Texture to have been created via the
        /// (name, TextureTarget, Vector2i, ...) cubemap constructor.
        ///
        /// Layout:
        ///          [ +Y ]
        ///   [-X] [ +Z ] [+X] [-Z]
        ///          [ -Y ]
        /// </summary>
        public void LoadCubemapCross(string filePath)
        {
            if (_target != TextureTarget.TextureCubeMap)
            {
                Logger.Instance.LogError($"LoadCubemapCross called on texture '{Name}', which was not created as a TextureCubeMap.");
                return;
            }

            string resolvedPath = Path.IsPathFullyQualified(filePath)
                ? filePath
                : Path.Combine("..", "..", "..", "Assets", "Textures", filePath);

            if (!File.Exists(resolvedPath))
            {
                Logger.Instance.LogError($"Cubemap cross file not found: {resolvedPath}");
                return;
            }

            using FileStream stream = File.OpenRead(resolvedPath);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            int faceSize = image.Width / 4;
            if (faceSize <= 0 || image.Height != faceSize * 3)
            {
                Logger.Instance.LogError($"Cubemap cross '{resolvedPath}' is {image.Width}x{image.Height}; expected a 4:3 horizontal-cross layout ({faceSize * 4}x{faceSize * 3}).");
                return;
            }

            GL.BindTexture(TextureTarget.TextureCubeMap, Handle);

            UploadCrossFace(TextureTarget.TextureCubeMapPositiveX, image.Data, image.Width, faceSize, col: 2, row: 1);
            UploadCrossFace(TextureTarget.TextureCubeMapNegativeX, image.Data, image.Width, faceSize, col: 0, row: 1);
            UploadCrossFace(TextureTarget.TextureCubeMapPositiveY, image.Data, image.Width, faceSize, col: 1, row: 0);
            UploadCrossFace(TextureTarget.TextureCubeMapNegativeY, image.Data, image.Width, faceSize, col: 1, row: 2);
            UploadCrossFace(TextureTarget.TextureCubeMapPositiveZ, image.Data, image.Width, faceSize, col: 1, row: 1);
            UploadCrossFace(TextureTarget.TextureCubeMapNegativeZ, image.Data, image.Width, faceSize, col: 3, row: 1);

            GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);
            GL.BindTexture(TextureTarget.TextureCubeMap, 0);

            Size = new Vector2i(faceSize, faceSize);
            FilePath = resolvedPath;
        }

        private void UploadCrossFace(TextureTarget faceTarget, byte[] fullImage, int fullWidth, int faceSize, int col, int row)
        {
            byte[] face = new byte[faceSize * faceSize * 4];
            int startX = col * faceSize;
            int startY = row * faceSize;

            for (int y = 0; y < faceSize; y++)
            {
                int srcOffset = ((startY + y) * fullWidth + startX) * 4;
                int dstOffset = y * faceSize * 4;
                Array.Copy(fullImage, srcOffset, face, dstOffset, faceSize * 4);
            }

            GL.TexImage2D(faceTarget, 0, _internalFormat, faceSize, faceSize, 0, _format, _pixelType, face);
        }

        public void SetImageFromFilePath(string filePath, TextureTarget textureTarget)
        {
            if (filePath == null)
            {
                Logger.Instance.LogWarning("SetImageFromFilePath called with a null filePath.");
                filePath = Path.Combine("..", "..", "..", "Fallback Assets", "Textures", "Missing.png");
            }

            if (filePath.Length == 0)
            {
                Logger.Instance.LogWarning("SetImageFromFilePath called with a empty filePath.");
                return;
            }

            // A GL texture's target is fixed at first bind and can't be changed afterward — trying to
            // retarget (e.g. a Texture2D created as one and then loaded as a cubemap) is invalid and
            // silently does nothing useful. Use the cubemap constructor + LoadCubemapCross instead.
            if (textureTarget != _target)
            {
                Logger.Instance.LogError($"SetImageFromFilePath: texture '{Name}' was created as {_target} and can't be reloaded as {textureTarget}. For cubemaps, use the (name, TextureTarget, Vector2i) constructor + LoadCubemapCross.");
                return;
            }

            filePath = Path.Combine("..", "..", "..", "Assets", "Textures", filePath);

            if (!File.Exists(filePath))
            {
                Logger.Instance.LogWarning($"Texture file does not exist: {filePath}");
                return;
            }

            using FileStream stream = File.OpenRead(filePath);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            Use(0);
            GL.TexImage2D(_target, 0, _internalFormat, image.Width, image.Height, 0, _format, _pixelType, image.Data);
            GL.GenerateMipmap((GenerateMipmapTarget)_target);

            Size = new Vector2i(image.Width, image.Height);

            FilePath = filePath;
        }

        public void Resize(Vector2i newSize)
        {
            if (Handle == -1 || newSize.X <= 0 || newSize.Y <= 0) return;

            Use(0);
            GL.TexImage2D(_target, 0, _internalFormat, newSize.X, newSize.Y, 0, _format, _pixelType, IntPtr.Zero);
            Size = newSize;
        }

        /// <summary>Reads back the full RGBA8 pixel buffer of this texture. Assumes a 2D, non-mipmapped-readback use case (e.g. probe baking).</summary>
        public byte[] GetPixels()
        {
            if (Handle == -1) return Array.Empty<byte>();
            byte[] data = new byte[Size.X * Size.Y * 4];
            Use(0);
            GL.GetTexImage(_target, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            return data;
        }

        /// <summary>Uploads a full RGBA8 pixel buffer, replacing the texture's contents at its current Size.</summary>
        public void SetPixels(byte[] rgba)
        {
            if (Handle == -1) return;
            Use(0);
            GL.TexImage2D(_target, 0, _internalFormat, Size.X, Size.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        }

        public void Use(int unit = 0)
        {
            if (Handle == -1) return;
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(_target, Handle);
        }

        public void Delete()
        {
            if (Handle != -1)
                GL.DeleteTexture(Handle);
        }

        private TextureDataValue GetTextureDataValue(TextureDataType type)
        {
            foreach (var data in _textureData)
                if (data.Type == type)
                    return data.Value;
            return TextureDataValue.Default;
        }

        private TextureDataValue GetTextureDataValueOrDefault(TextureDataType type, TextureDataValue defaultValue)
        {
            foreach (var data in _textureData)
                if (data.Type == type)
                    return data.Value;
            return defaultValue;
        }

        private static TextureTarget ConvertToTextureTarget(TextureDataValue value, TextureTarget defaultValue)
        {
            return value switch
            {
                TextureDataValue.Texture2D => TextureTarget.Texture2D,
                TextureDataValue.TextureCubeMap => TextureTarget.TextureCubeMap,
                TextureDataValue.Texture3D => TextureTarget.Texture3D,
                TextureDataValue.Texture2DArray => TextureTarget.Texture2DArray,
                _ => defaultValue
            };
        }

        private static PixelInternalFormat ConvertToPixelInternalFormat(TextureDataValue value, PixelInternalFormat defaultValue)
        {
            return value switch
            {
                TextureDataValue.Rgba => PixelInternalFormat.Rgba,
                TextureDataValue.Rgb => PixelInternalFormat.Rgb,
                TextureDataValue.Red => PixelInternalFormat.R8,
                TextureDataValue.Rg => PixelInternalFormat.Rg8,
                _ => defaultValue
            };
        }

        private static PixelFormat ConvertToPixelFormat(TextureDataValue value, PixelFormat defaultValue)
        {
            return value switch
            {
                TextureDataValue.Rgba => PixelFormat.Rgba,
                TextureDataValue.Rgb => PixelFormat.Rgb,
                TextureDataValue.Red => PixelFormat.Red,
                TextureDataValue.Rg => PixelFormat.Rg,
                _ => defaultValue
            };
        }

        private static TextureWrapMode ConvertToTextureWrapMode(TextureDataValue value, TextureWrapMode defaultValue)
        {
            return value switch
            {
                TextureDataValue.Repeat => TextureWrapMode.Repeat,
                TextureDataValue.MirroredRepeat => TextureWrapMode.MirroredRepeat,
                TextureDataValue.ClampToEdge => TextureWrapMode.ClampToEdge,
                TextureDataValue.ClampToBorder => TextureWrapMode.ClampToBorder,
                _ => defaultValue
            };
        }

        private static TextureMinFilter ConvertToTextureMinFilter(TextureDataValue value, TextureMinFilter defaultValue)
        {
            return value switch
            {
                TextureDataValue.Nearest => TextureMinFilter.Nearest,
                TextureDataValue.Linear => TextureMinFilter.Linear,
                TextureDataValue.NearestMipmapNearest => TextureMinFilter.NearestMipmapNearest,
                TextureDataValue.NearestMipmapLinear => TextureMinFilter.NearestMipmapLinear,
                TextureDataValue.LinearMipmapNearest => TextureMinFilter.LinearMipmapNearest,
                TextureDataValue.LinearMipmapLinear => TextureMinFilter.LinearMipmapLinear,
                _ => defaultValue
            };
        }

        private static TextureMagFilter ConvertToTextureMagFilter(TextureDataValue value, TextureMagFilter defaultValue)
        {
            return value switch
            {
                TextureDataValue.Nearest => TextureMagFilter.Nearest,
                TextureDataValue.Linear => TextureMagFilter.Linear,
                _ => defaultValue
            };
        }

        public readonly TextureData[] _textureData;
    }

    public struct TextureData
    {
        public TextureDataType Type;
        public TextureDataValue Value;
    }

    public enum TextureDataType
    {
        Mipmap,
        TextureWrap,
        TextureFilter,
        PixelFormat,
        TextureTarget,
        ColorComponents
    }

    public enum TextureDataValue
    {
        NearestMipmapNearest,
        NearestMipmapLinear,
        LinearMipmapNearest,
        LinearMipmapLinear,

        Nearest,
        Linear,

        Repeat,
        MirroredRepeat,
        ClampToEdge,
        ClampToBorder,

        Rgba,
        Rgb,
        Rg,
        Bgr,
        Bgra,
        Ycrcb422Sgix,
        Ycrcb444Sgix,
        Red,
        Green,
        Blue,
        Alpha,

        Renderbuffer,
        Texture1D,
        Texture1DArray,
        Texture2D,
        Texture2DArray,
        Texture3D,
        TextureBuffer,
        TextureCubeMap,

        RedGreenBlue,
        RedGreenBlueAlpha,
        GreyAlpha,
        Grey,
        Default
    }
}