using System;
using OpenTK.Mathematics;

namespace Gears.World.Components.AddonComponents
{
    public class LookAtCamera : BaseComponent
    {
        public override void Update()
        {
            base.Update();

            var cameraTransform = SceneManager.ActiveScene?.MainCamera?.Transform;

            if (cameraTransform != null)
            {
                Transform.LookAt(cameraTransform.Position);
            }
        }
    }
}