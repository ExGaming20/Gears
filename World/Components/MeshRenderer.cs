using Gears.Graphics;

namespace Gears.World.Components
{
    public class MeshRenderer : BaseComponent
    {
        public bool Visible { get; set; } = true;

        /// <summary>Render sort order (opaque pass layer ordering, not the semantic GameObject Layer).</summary>
        public int SortLayer { get; set; } = 0;

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

            foreach (var subMesh in _meshFilter.Mesh.SubMeshes)
            {
                // SortLayer controls draw order; GameObject.Layer controls light/camera culling
                Game.AddRenderRequest(subMesh, GameObject.Transform.WorldMatrix, SortLayer, GameObject.Layer);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _meshFilter = null;
        }
    }
}