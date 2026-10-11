using System;
using Gears.Graphics;

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
            }
        }

        public MeshFilter()
        {
        }

        public MeshFilter(Mesh mesh)
        {
            Mesh = mesh;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Mesh = null;
        }
    }
}
