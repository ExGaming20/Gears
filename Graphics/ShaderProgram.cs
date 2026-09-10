using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using Gears.Utilities;

namespace Gears.Graphics
{
    public class ShaderProgram
    {
        private int _programID;                                      // Made mutable for reloading
        private readonly Dictionary<string, int> _uniformLocationCache = new();
        private readonly HashSet<string> _warnedUniforms = new();

        // Stored original constructor arguments for reloading
        private readonly string? _originalVertexPath;
        private readonly string? _originalFragmentPath;
        private readonly string? _originalGeometryPath;
        private readonly string? _originalComputePath;
        private readonly bool? _missingFallback;

        // Public property to keep backward compatibility (if any external code accesses ProgramID)
        public int ProgramID => _programID;

        public ShaderProgram(string? vertexPath = null, string? fragmentPath = null,
                             string? geometryPath = null, string? computePath = null,
                             bool? missing = false)
        {
            // Store original paths for later reloading
            _originalVertexPath = vertexPath;
            _originalFragmentPath = fragmentPath;
            _originalGeometryPath = geometryPath;
            _originalComputePath = computePath;
            _missingFallback = missing;

            // Load and compile the shader program
            var (vertexSource, fragmentSource, geometrySource, computeSource) =
                LoadShaderSources(vertexPath, fragmentPath, geometryPath, computePath, missing);

            _programID = CreateAndLinkProgram(vertexSource, fragmentSource, geometrySource, computeSource);
        }

        // ------------------------------------------------------------------
        // Public Reload method
        // ------------------------------------------------------------------
        public void Reload()
        {
            // Reload sources using the stored original paths
            var (vertexSource, fragmentSource, geometrySource, computeSource) =
                LoadShaderSources(_originalVertexPath, _originalFragmentPath,
                                  _originalGeometryPath, _originalComputePath,
                                  _missingFallback);

            int newProgramID = CreateAndLinkProgram(vertexSource, fragmentSource, geometrySource, computeSource);

            if (newProgramID != 0)
            {
                // Success: replace old program with new one
                GL.DeleteProgram(_programID);
                _programID = newProgramID;

                // Clear caches because uniform locations have changed
                _uniformLocationCache.Clear();
                _warnedUniforms.Clear();
            }
            else
            {
                Logger.Instance.LogError("Reload failed – old shader program kept.");
            }
        }

        // ------------------------------------------------------------------
        // Core program creation (shared by constructor and Reload)
        // ------------------------------------------------------------------
        private int CreateAndLinkProgram(string? vertexSource, string? fragmentSource,
                                         string? geometrySource, string? computeSource)
        {
            int vertexShaderID = 0, fragmentShaderID = 0, geometryShaderID = 0, computeShaderID = 0;

            if (vertexSource != null)
                vertexShaderID = CompileShader(ShaderType.VertexShader, vertexSource);
            if (fragmentSource != null)
                fragmentShaderID = CompileShader(ShaderType.FragmentShader, fragmentSource);
            if (geometrySource != null)
                geometryShaderID = CompileShader(ShaderType.GeometryShader, geometrySource);
            if (computeSource != null)
                computeShaderID = CompileShader(ShaderType.ComputeShader, computeSource);

            // A requested stage that failed to compile returns 0. Build the program only if every
            // requested stage compiled; otherwise delete the survivors and bail so we never link or
            // hand back an invalid program.
            if ((vertexSource != null && vertexShaderID == 0) ||
                (fragmentSource != null && fragmentShaderID == 0) ||
                (geometrySource != null && geometryShaderID == 0) ||
                (computeSource != null && computeShaderID == 0))
            {
                if (vertexShaderID != 0) GL.DeleteShader(vertexShaderID);
                if (fragmentShaderID != 0) GL.DeleteShader(fragmentShaderID);
                if (geometryShaderID != 0) GL.DeleteShader(geometryShaderID);
                if (computeShaderID != 0) GL.DeleteShader(computeShaderID);
                return 0;
            }

            int program = GL.CreateProgram();

            if (vertexShaderID != 0) GL.AttachShader(program, vertexShaderID);
            if (fragmentShaderID != 0) GL.AttachShader(program, fragmentShaderID);
            if (geometryShaderID != 0) GL.AttachShader(program, geometryShaderID);
            if (computeShaderID != 0) GL.AttachShader(program, computeShaderID);

            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkStatus);

            if (linkStatus == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                Logger.Instance.LogError($"Program linking failed: {log}");
                GL.DeleteProgram(program);
                program = 0;
            }

            // Cleanup shader objects
            if (vertexShaderID != 0) { GL.DetachShader(program, vertexShaderID); GL.DeleteShader(vertexShaderID); }
            if (fragmentShaderID != 0) { GL.DetachShader(program, fragmentShaderID); GL.DeleteShader(fragmentShaderID); }
            if (geometryShaderID != 0) { GL.DetachShader(program, geometryShaderID); GL.DeleteShader(geometryShaderID); }
            if (computeShaderID != 0) { GL.DetachShader(program, computeShaderID); GL.DeleteShader(computeShaderID); }

            return program;
        }

        // ------------------------------------------------------------------
        // Source loading with path resolution and section extraction
        // ------------------------------------------------------------------
        private (string? vertex, string? fragment, string? geometry, string? compute)
            LoadShaderSources(string? vertexPath, string? fragmentPath,
                              string? geometryPath, string? computePath,
                              bool? missingFallback)
        {
            // Resolve physical file paths
            string? resolvedVert = ResolveShaderPath(vertexPath, missingFallback);
            string? resolvedFrag = ResolveShaderPath(fragmentPath, missingFallback);
            string? resolvedGeom = ResolveShaderPath(geometryPath, missingFallback);
            string? resolvedComp = ResolveShaderPath(computePath, missingFallback);

            string? vertexSource = null, fragmentSource = null, geometrySource = null, computeSource = null;

            if (resolvedVert != null && File.Exists(resolvedVert))
            {
                string full = File.ReadAllText(resolvedVert);
                vertexSource = ExtractShaderSection(full, ShaderType.VertexShader);
            }
            if (resolvedFrag != null && File.Exists(resolvedFrag))
            {
                string full = File.ReadAllText(resolvedFrag);
                fragmentSource = ExtractShaderSection(full, ShaderType.FragmentShader);
            }
            if (resolvedGeom != null && File.Exists(resolvedGeom))
            {
                string full = File.ReadAllText(resolvedGeom);
                geometrySource = ExtractShaderSection(full, ShaderType.GeometryShader);
            }
            if (resolvedComp != null && File.Exists(resolvedComp))
            {
                string full = File.ReadAllText(resolvedComp);
                computeSource = ExtractShaderSection(full, ShaderType.ComputeShader);
            }

            return (vertexSource, fragmentSource, geometrySource, computeSource);
        }

        private string? ResolveShaderPath(string? originalPath, bool? missingFallback)
        {
            if (string.IsNullOrEmpty(originalPath))
                return null;

            string path = originalPath;

            // If the path doesn't exist as given, try the standard asset folder
            if (!File.Exists(path))
                path = Path.Combine("..", "..", "..", "Assets", "Raw Shaders", originalPath + ".shader");

            // If still missing and fallback is allowed, try the fallback folder
            if (!File.Exists(path) && missingFallback.HasValue && missingFallback.Value)
                path = Path.Combine("..", "..", "..", "Fallback Assets", "Raw Shaders", Path.GetFileName(originalPath) + ".shader");

            return File.Exists(path) ? path : null;
        }

        // ------------------------------------------------------------------
        // Original shader compilation and section extraction (unchanged except
        // the CompileShader method's logging is kept identical)
        // ------------------------------------------------------------------
        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);

            if (status == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                string[] lines = source.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                    lines[i] = $"{i + 1,3}: {lines[i]}";
                string numberedSource = string.Join("\n", lines);
                Logger.Instance.LogError($"{type} compilation failed:\n{log}\n\nShader Source:\n{numberedSource}");
                GL.DeleteShader(shader);
                return 0;
            }
            return shader;
        }

        private static string ExtractShaderSection(string fullSource, ShaderType shaderType)
        {
            string[] lines = fullSource.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            List<string> result = new();
            bool inTargetSection = false;
            bool foundTargetShader = false;
            string? versionLine = null;

            string shaderMarker = shaderType switch
            {
                ShaderType.VertexShader => "vertex",
                ShaderType.FragmentShader => "fragment",
                ShaderType.GeometryShader => "geometry",
                ShaderType.ComputeShader => "compute",
                _ => ""
            };

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();

                // Found the target shader section
                if (trimmed.StartsWith("#shader") && trimmed.Contains(shaderMarker))
                {
                    inTargetSection = true;
                    foundTargetShader = true;
                    continue;
                }

                // End of target section
                if (inTargetSection && (trimmed.StartsWith("#shader") || trimmed.StartsWith("#end")))
                    break;

                // Extract version line from within a marker section, OR from the whole file if
                // there are no #shader markers (version must be first line, same either way).
                if ((inTargetSection || !foundTargetShader) && trimmed.StartsWith("#version"))
                {
                    versionLine = lines[i];
                    continue;
                }

                // Add content lines
                if ((inTargetSection || (!foundTargetShader && !trimmed.StartsWith("#shader"))) && !trimmed.StartsWith("#version"))
                    result.Add(lines[i]);
            }

            List<string> finalLines = new();
            if (versionLine != null)
                finalLines.Add(versionLine);
            finalLines.AddRange(result);
            return string.Join("\n", finalLines).Trim();
        }

        // ------------------------------------------------------------------
        // Existing public methods (unchanged except using _programID field)
        // ------------------------------------------------------------------
        public void UseProgram() => GL.UseProgram(_programID);

        public void DeleteProgram() => GL.DeleteProgram(_programID);

        public void SetUniform(string uniformName, object value)
        {
            if (!_uniformLocationCache.TryGetValue(uniformName, out int location))
            {
                location = GL.GetUniformLocation(_programID, uniformName);
                _uniformLocationCache[uniformName] = location;
            }

            if (location == -1)
            {
                if (_warnedUniforms.Add(uniformName))
                    Logger.Instance.LogWarning($"Uniform '{uniformName}' not found in shader program.");
                return;
            }

            UseProgram();

            switch (value)
            {
                case bool b: GL.Uniform1(location, b ? 1 : 0); break;
                case int i: GL.Uniform1(location, i); break;
                case float f: GL.Uniform1(location, f); break;
                case Vector2 v2: GL.Uniform2(location, v2); break;
                case Vector3 v3: GL.Uniform3(location, v3); break;
                case Vector4 v4: GL.Uniform4(location, v4); break;
                case Matrix4 m4: GL.UniformMatrix4(location, false, ref m4); break;
                default:
                    Logger.Instance.LogError($"Unsupported uniform type: {value.GetType()}"); break;
            }
        }
    }
}