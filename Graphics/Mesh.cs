using System;
using System.Collections.Generic;

namespace Gears.Graphics
{
    public class Mesh
    {
        public string Name { get; set; }
        public int UUID { get; }
        public List<SubMesh> SubMeshes { get; set; } = new();

        // Incremented/decremented by MeshFilter.Mesh's setter. A real count
        // instead of a scan, so unload can check "is anything using this"
        // in O(1) instead of walking every loaded scene's components.
        public int RefCount { get; private set; }
        public void Retain() => RefCount++;
        public void Release() => RefCount = Math.Max(0, RefCount - 1);

        private static int _nextUUID = 0;

        public Mesh(string name, List<SubMesh>? subMeshes = null, int? explicitUUID = null)
        {
            Name = name;
            SubMeshes = subMeshes ?? new List<SubMesh>();
            _nextUUID = explicitUUID ?? Guid.NewGuid().GetHashCode();
            UUID = _nextUUID;
        }

        public void Dispose()
        {
            foreach (var subMesh in SubMeshes)
            {
                subMesh.Vertices.Clear();
                subMesh.UVs.Clear();
                subMesh.Normals.Clear();
                subMesh.MaterialUUID = 0; // releases the default material this submesh retained
            }

            SubMeshes.Clear();
        }
    }
}
