using OpenTK.Mathematics;

namespace Gears.Graphics
{
    public class SubMesh
    {
        public List<Vector3> Vertices = new();
        public List<Vector2> UVs = new();
        public List<Vector3> Normals = new();

        public int MaterialUUID;

        public void FreeVertexData()
        {
            Vertices.Clear(); Vertices.TrimExcess();
            UVs.Clear(); UVs.TrimExcess();
            Normals.Clear(); Normals.TrimExcess();
        }
    }
}