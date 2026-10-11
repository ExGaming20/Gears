using Gears;
using OpenTK.Mathematics;

namespace Gears.Graphics
{
    public class SubMesh
    {
        public List<Vector3> Vertices = new();
        public List<Vector2> UVs = new();
        public List<Vector3> Normals = new();

        // A SubMesh's default material (used when no MeshRenderer.Material
        // override is set) needs to retain it too, or an "unused" material
        // that's actually still a mesh's default would get deleted.
        private int _materialUUID;
        public int MaterialUUID
        {
            get => _materialUUID;
            set
            {
                if (_materialUUID == value) return;
                FindLoadedAssets.FindMaterial(null, _materialUUID)?.Release();
                _materialUUID = value;
                FindLoadedAssets.FindMaterial(null, _materialUUID)?.Retain();
            }
        }

        public void FreeVertexData()
        {
            Vertices.Clear(); Vertices.TrimExcess();
            UVs.Clear(); UVs.TrimExcess();
            Normals.Clear(); Normals.TrimExcess();
        }
    }
}
