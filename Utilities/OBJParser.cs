using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gears.Graphics;
using OpenTK.Mathematics;

namespace Gears.Utilities
{
    public class OBJParser
    {
        private static readonly char[] Separators = { ' ', '\t' };

        public static Mesh Parse(string name, string path)
        {
            List<Vector3> positions = new();
            List<Vector2> uvs = new();
            List<Vector3> normals = new();
            List<SubMesh> subMeshes = new();

            int missingUUID = Game.MissingMaterial.UUID;
            int pendingMaterialUUID = missingUUID;

            SubMesh current = NewSubMesh(pendingMaterialUUID);

            foreach (string rawLine in File.ReadLines(path))
            {
                string line = rawLine.Trim();

                // Skip blank lines and comments ('# Blender 5.2.2 LTS', '# www.blender.org', ...).
                if (line.Length == 0 || line[0] == '#')
                    continue;

                // Split on any whitespace so tab-separated files also work.
                string[] tokens = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;

                switch (tokens[0])
                {
                    case "v":
                        positions.Add(new Vector3(
                            Float(tokens, 1),
                            Float(tokens, 2),
                            Float(tokens, 3)));
                        break;

                    case "vt":
                        // OBJ allows "vt u", "vt u v" and "vt u v w".
                        uvs.Add(new Vector2(
                            Float(tokens, 1),
                            Float(tokens, 2)));
                        break;

                    case "vn":
                        normals.Add(new Vector3(
                            Float(tokens, 1),
                            Float(tokens, 2),
                            Float(tokens, 3)));
                        break;

                    case "o":
                    case "g":
                        current = Flush(subMeshes, current, pendingMaterialUUID);
                        break;

                    case "usemtl":
                        {
                            // Everything after the keyword is the material name (it may contain spaces).
                            string matName = line.Substring(tokens[0].Length).Trim();

                            Material? mat = Game.Materials.Find(m => m.Name == matName);
                            if (mat == null)
                                Logger.Instance.LogWarning($"OBJParser: material '{matName}' not found for mesh '{name}'");

                            pendingMaterialUUID = mat?.UUID ?? missingUUID;
                            current = Flush(subMeshes, current, pendingMaterialUUID);
                            break;
                        }

                    case "f":
                        ParseFace(tokens, positions, uvs, normals, current);
                        break;

                        // "mtllib", "s", "l", "p", "vp" are intentionally ignored.
                }
            }

            current = Flush(subMeshes, current, pendingMaterialUUID);

            // A mesh with no faces at all still needs one (empty) sub-mesh.
            if (subMeshes.Count == 0)
                subMeshes.Add(current);

            // If the file has no "vn" data (or only partial data), shading would be black.
            // Fall back to flat per-triangle normals.
            if (!HasNormals(subMeshes))
                GenerateFlatNormals(subMeshes);

            return new Mesh(name, subMeshes);
        }

        private static SubMesh NewSubMesh(int materialUUID)
        {
            SubMesh sm = new();
            sm.MaterialUUID = materialUUID;
            return sm;
        }

        /// <summary>Adds <paramref name="current"/> if it holds geometry and returns a fresh one.</summary>
        private static SubMesh Flush(List<SubMesh> subMeshes, SubMesh current, int materialUUID)
        {
            if (current.Vertices.Count > 0)
                subMeshes.Add(current);
            return NewSubMesh(materialUUID);
        }

        private static void ParseFace(string[] tokens, List<Vector3> positions, List<Vector2> uvs,
                                      List<Vector3> normals, SubMesh target)
        {
            int count = tokens.Length - 1;   // number of face vertices
            if (count < 3)
                return;                      // degenerate face, skip

            // Fan triangulation: works for triangles, quads (Blender's default export)
            // and n-gons. Winding order is preserved.
            for (int i = 1; i < count - 1; i++)
            {
                AddVertex(tokens[1], positions, uvs, normals, target);
                AddVertex(tokens[1 + i], positions, uvs, normals, target);
                AddVertex(tokens[1 + i + 1], positions, uvs, normals, target);
            }
        }

        private static void AddVertex(string token, List<Vector3> positions, List<Vector2> uvs,
                                      List<Vector3> normals, SubMesh target)
        {
            // Supported forms: "v", "v/vt", "v//vn", "v/vt/vn".
            string[] parts = token.Split('/');

            int v = parts.Length > 0 ? Resolve(parts[0], positions.Count) : -1;
            int t = parts.Length > 1 ? Resolve(parts[1], uvs.Count) : -1;
            int n = parts.Length > 2 ? Resolve(parts[2], normals.Count) : -1;

            target.Vertices.Add(v >= 0 ? positions[v] : Vector3.Zero);
            target.UVs.Add(t >= 0 ? uvs[t] : Vector2.Zero);
            target.Normals.Add(n >= 0 ? normals[n] : Vector3.Zero);
        }

        /// <summary>
        /// Resolves an OBJ index. Positive indices are 1-based, negative indices are
        /// relative to the end of the list ("-1" is the last element). Returns -1 when
        /// the token is missing, malformed or out of range.
        /// </summary>
        private static int Resolve(string? token, int count)
        {
            if (string.IsNullOrEmpty(token))
                return -1;

            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw))
                return -1;

            int index = raw > 0 ? raw - 1 : count + raw;
            return index >= 0 && index < count ? index : -1;
        }

        private static float Float(string[] tokens, int index)
        {
            if (index >= tokens.Length)
                return 0f;

            return float.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : 0f;
        }

        private static bool HasNormals(List<SubMesh> subMeshes)
        {
            foreach (SubMesh sm in subMeshes)
                foreach (Vector3 n in sm.Normals)
                    if (n != Vector3.Zero)
                        return true;

            return false;
        }

        private static void GenerateFlatNormals(List<SubMesh> subMeshes)
        {
            foreach (SubMesh sm in subMeshes)
            {
                for (int i = 0; i + 2 < sm.Vertices.Count; i += 3)
                {
                    Vector3 a = sm.Vertices[i];
                    Vector3 b = sm.Vertices[i + 1];
                    Vector3 c = sm.Vertices[i + 2];

                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.LengthSquared > 1e-12f)
                        n = Vector3.Normalize(n);

                    sm.Normals[i] = n;
                    sm.Normals[i + 1] = n;
                    sm.Normals[i + 2] = n;
                }
            }
        }
    }
}