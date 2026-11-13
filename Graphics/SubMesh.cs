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

        private Box3 _bounds;
        private bool _hasBounds;

        /// <summary>
        /// Local-space bounds of this sub-mesh. Computed before
        /// <see cref="FreeVertexData"/> releases the vertex lists, so meshes reused
        /// after their first upload still report valid bounds.
        /// </summary>
        public bool TryGetBounds(out Box3 bounds)
        {
            if (!_hasBounds)
                ComputeBounds();

            bounds = _bounds;
            return _hasBounds;
        }

        private void ComputeBounds()
        {
            if (Vertices.Count == 0)
            {
                _hasBounds = false;
                return;
            }

            Vector3 min = new Vector3(float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity);

            foreach (var v in Vertices)
            {
                min = Vector3.ComponentMin(min, v);
                max = Vector3.ComponentMax(max, v);
            }

            _bounds = new Box3(min, max);
            _hasBounds = true;
        }

        public void FreeVertexData()
        {
            ComputeBounds(); // must run before the vertices are cleared

            Vertices.Clear(); Vertices.TrimExcess();
            UVs.Clear(); UVs.TrimExcess();
            Normals.Clear(); Normals.TrimExcess();
        }
    }
}
