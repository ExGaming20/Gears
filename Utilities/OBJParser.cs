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
        public static Mesh Parse(string name, string path)
        {
            List<Vector3> positions = new();
            List<Vector2> uvs = new();
            List<Vector3> normals = new();
            List<SubMesh> subMeshes = new();
            SubMesh current = new();
            int pendingMaterialUUID = 0;

            foreach (string rawLine in File.ReadLines(path))
            {
                string line = rawLine.Trim();

                if (line.StartsWith("v "))
                {
                    string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    positions.Add(new Vector3(
                        float.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[2], CultureInfo.InvariantCulture),
                        float.Parse(p[3], CultureInfo.InvariantCulture)));
                }
                else if (line.StartsWith("vt "))
                {
                    string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    uvs.Add(new Vector2(
                        float.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[2], CultureInfo.InvariantCulture)));
                }
                else if (line.StartsWith("vn "))
                {
                    string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    normals.Add(new Vector3(
                        float.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[2], CultureInfo.InvariantCulture),
                        float.Parse(p[3], CultureInfo.InvariantCulture)));
                }
                else if (line.StartsWith("o ") || line.StartsWith("g "))
                {
                    if (current.Vertices.Count > 0)
                    {
                        subMeshes.Add(current);
                        current = new SubMesh();
                        current.MaterialUUID = pendingMaterialUUID;
                    }
                }
                else if (line.StartsWith("usemtl "))
                {
                    string matName = line.Substring("usemtl ".Length).Trim();
                    Material? mat = Game.Materials.Find(m => m.Name == matName);
                    if (mat == null)
                        Logger.Instance.LogWarning($"OBJParser: material '{matName}' not found for mesh '{name}'");
                    pendingMaterialUUID = mat?.UUID ?? Game.MissingMaterial.UUID;
                    if (current.Vertices.Count > 0)
                    {
                        subMeshes.Add(current);
                        current = new SubMesh();
                    }
                    current.MaterialUUID = pendingMaterialUUID;
                }
                else if (line.StartsWith("f "))
                {
                    string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    List<string> faceTokens = new();
                    for (int i = 1; i < tokens.Length; i++)
                        faceTokens.Add(tokens[i]);
                    for (int i = 1; i < faceTokens.Count - 1; i++)
                    {
                        ParseVertex(faceTokens[0], positions, uvs, normals, current);
                        ParseVertex(faceTokens[i], positions, uvs, normals, current);
                        ParseVertex(faceTokens[i + 1], positions, uvs, normals, current);
                    }
                }
            }

            if (current.Vertices.Count > 0)
                subMeshes.Add(current);

            if (subMeshes.Count == 0)
                subMeshes.Add(current);

            return new Mesh(name, subMeshes);
        }

        private static void ParseVertex(string token, List<Vector3> positions, List<Vector2> uvs, List<Vector3> normals, SubMesh target)
        {
            string[] parts = token.Split('/');
            int vIdx = int.Parse(parts[0]) - 1;
            int vtIdx = parts.Length > 1 && parts[1] != "" ? int.Parse(parts[1]) - 1 : -1;
            int vnIdx = parts.Length > 2 && parts[2] != "" ? int.Parse(parts[2]) - 1 : -1;

            target.Vertices.Add(vIdx >= 0 && vIdx < positions.Count ? positions[vIdx] : Vector3.Zero);
            target.UVs.Add(vtIdx >= 0 && vtIdx < uvs.Count ? uvs[vtIdx] : Vector2.Zero);
            target.Normals.Add(vnIdx >= 0 && vnIdx < normals.Count ? normals[vnIdx] : Vector3.Zero);
        }
    }
}