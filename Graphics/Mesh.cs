using System;
using System.Collections.Generic;

namespace Gears.Graphics
{
    public class Mesh
    {
        public string Name { get; set; }
        public int UUID { get; }
        public List<SubMesh> SubMeshes { get; set; } = new();

        private static int _nextUUID = 0;

        public Mesh(string name, List<SubMesh>? subMeshes = null)
        {
            Name = name;
            SubMeshes = subMeshes ?? new List<SubMesh>();
            UUID = _nextUUID++;
        }

        public void Dispose()
        {
            foreach (var subMesh in SubMeshes)
            {
                subMesh.Vertices.Clear();
                subMesh.UVs.Clear();
                subMesh.Normals.Clear();
            }

            SubMeshes.Clear();
        }
    }
}