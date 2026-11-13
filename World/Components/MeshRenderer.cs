using Gears.Graphics;
using OpenTK.Mathematics;

namespace Gears.World.Components
{
    public class MeshRenderer : BaseComponent
    {
        public bool Visible { get; set; } = true;

        /// <summary>Render sort order (opaque pass layer ordering, not the semantic GameObject Layer).</summary>
        public int SortLayer { get; set; } = 0;

        private Material? _material;
        /// <summary>Optional material override. If null, uses the SubMesh's material.</summary>
        public Material? Material
        {
            get => _material;
            set
            {
                if (_material == value) return;
                _material?.Release();
                _material = value;
                _material?.Retain();
            }
        }

        private MeshFilter? _meshFilter;

        public override void Start()
        {
            base.Start();
            _meshFilter = GameObject.GetComponent<MeshFilter>();
        }

        public void RefreshMesh()
        {
            _meshFilter = null;
        }

        public override void OnRenderFrame(double deltaTime, Camera renderingCamera)
        {
            if (!Visible) return;

            _meshFilter ??= GameObject.GetComponent<MeshFilter>();

            if (_meshFilter?.Mesh == null) return;

            if (OutSideOfCameraFrustum(_meshFilter.AABB, renderingCamera)) return;

            foreach (var subMesh in _meshFilter.Mesh.SubMeshes)
            {
                Game.AddRenderRequest(subMesh, GameObject.Transform.WorldMatrix, SortLayer, GameObject.Layer, Material);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _meshFilter = null;
            Material = null;
        }

        private bool OutSideOfCameraFrustum(Box3 localAabb, Camera renderingCamera)
        {
            var camera = renderingCamera;
            if (camera == null) return false;

            var wm = GameObject.Transform.WorldMatrix;
            Vector3 min = new Vector3(float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity);

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? localAabb.Min.X : localAabb.Max.X,
                    (i & 2) == 0 ? localAabb.Min.Y : localAabb.Max.Y,
                    (i & 4) == 0 ? localAabb.Min.Z : localAabb.Max.Z);
                var world = Vector3.TransformPosition(corner, wm);
                min = Vector3.ComponentMin(min, world);
                max = Vector3.ComponentMax(max, world);
            }

            return !FrustumIntersectsAABB(camera.GetFrustum(), new Box3(min, max));
        }

        private static bool FrustumIntersectsAABB(Vector4[] frustumPlanes, Box3 aabb)
        {
            foreach (var plane in frustumPlanes)
            {
                var p = new Vector3(
                    plane.X >= 0 ? aabb.Max.X : aabb.Min.X,
                    plane.Y >= 0 ? aabb.Max.Y : aabb.Min.Y,
                    plane.Z >= 0 ? aabb.Max.Z : aabb.Min.Z
                );

                if (plane.X * p.X + plane.Y * p.Y + plane.Z * p.Z + plane.W < 0)
                    return false;
            }

            return true;
        }
    }
}