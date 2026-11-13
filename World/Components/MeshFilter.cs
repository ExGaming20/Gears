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
        public Box3 AABB => _aabb;

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
                _aabb = new Box3(Vector3.Zero, Vector3.Zero);
                return;
            }

            Vector3 min = new Vector3(float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity);

            foreach (var sm in _mesh.SubMeshes)
            { 
                foreach (var v in sm.Vertices)
                {
                    min = Vector3.ComponentMin(min, v);
                    max = Vector3.ComponentMax(max, v);
                }
            }

            _aabb = new Box3(min, max);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Mesh = null;
        }
    }
}
