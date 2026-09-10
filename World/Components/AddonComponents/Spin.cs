using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Gears.World.Components.AddonComponents
{
    public class Spin : BaseComponent
    {
        public bool CCW = false;
        public int Speed = 60;

        public override void Update()
        {
            base.Update();

            if (Transform != null)
            {
                var deltaAngle = Game.DeltaTime * Speed * (CCW ? -1 : 1);
                var currentLocalRotation = Transform.LocalRotation;
                var deltaQuat = Quaternion.FromEulerAngles(0f, MathHelper.DegreesToRadians(deltaAngle), 0f);
                Transform.LocalRotation = Quaternion.Multiply(currentLocalRotation, deltaQuat);
            }
        }
    }
}