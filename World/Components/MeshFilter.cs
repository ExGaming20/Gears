using System;
using Gears.Graphics;

namespace Gears.World.Components
{
    public class MeshFilter : BaseComponent
    {
        public Mesh? Mesh { get; set; }

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