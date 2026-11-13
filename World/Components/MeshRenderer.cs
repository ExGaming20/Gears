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

        public override void OnRenderFrame(double deltaTime)
        {
            if (!Visible) return;

            _meshFilter ??= GameObject.GetComponent<MeshFilter>();

            if (_meshFilter?.Mesh == null) return;

            Box3? worldBounds = _meshFilter.HasAABB ? TransformBoundsToWorld(_meshFilter.AABB) : null;

            foreach (var subMesh in _meshFilter.Mesh.SubMeshes)
            {
                Game.AddRenderRequest(subMesh, GameObject.Transform.WorldMatrix, SortLayer, GameObject.Layer, Material, worldBounds);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _meshFilter = null;
            Material = null;
        }

        /// <summary>
        /// Builds a world-space AABB from the mesh's local bounds. Culling itself happens
        /// per render pass (main camera, shadow lights, render textures, probes) so every
        /// pass tests against its own view/projection.
        /// </summary>
        private Box3 TransformBoundsToWorld(Box3 localAabb)
        {
            Matrix4 wm = GameObject.Transform.WorldMatrix;
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

            return new Box3(min, max);
        }
    }
}