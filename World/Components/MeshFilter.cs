using System;
using Gears.Graphics;
using OpenTK.Mathematics;

namespace Gears.World.Components
{
    public class MeshFilter : BaseComponent
    {
        private Mesh? _mesh;
        public Mesh? Mesh
        {
            get => _mesh;
            set
            {
                if (_mesh == value) return;
                _mesh?.Release();
                _mesh = value;
                _mesh?.Retain();
                RecomputeAABB();
            }
        }

        private Box3 _aabb;
        private bool _hasAABB;
        public Box3 AABB => _aabb;
        public bool HasAABB => _hasAABB;

        public MeshFilter()
        {
        }

        public MeshFilter(Mesh mesh)
        {
            Mesh = mesh;
        }

        private void RecomputeAABB()
        {
            if (_mesh?.SubMeshes is not { Count: > 0 })
            {
                _hasAABB = false;
                return;
            }

            Vector3 min = new Vector3(float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity);
            bool any = false;

            foreach (var sm in _mesh.SubMeshes)
            {
                if (!sm.TryGetBounds(out Box3 bounds)) continue;

                min = Vector3.ComponentMin(min, bounds.Min);
                max = Vector3.ComponentMax(max, bounds.Max);
                any = true;
            }

            if (!any)
            {
                _hasAABB = false;
                return;
            }

            _aabb = new Box3(min, max);
            _hasAABB = true;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Mesh = null;
        }
    }
}
