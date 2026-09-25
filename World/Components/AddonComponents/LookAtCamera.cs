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
                //var directionToCamera = cameraTransform.Position - Transform.Position;
                //Vector3 forwardDir = Transform.Rotation * (-Vector3.UnitY);
                //
                //Quaternion relativeRotation = Quaternion.FromAxisAngle(
                //    Vector3.Cross(forwardDir, directionToCamera),
                //    Vector3.CalculateAngle(forwardDir, directionToCamera)
                //);
                //
                //Quaternion targetRotation = Quaternion.Normalize(Quaternion.Multiply(relativeRotation, Transform.Rotation));
                //
                //Transform.Rotation = targetRotation;


                //directionToCamera += Transform.Position;

                //Transform.LookAt(directionToCamera);
                //Transform.LookAt(cameraTransform.Position);

                //Vector3 d = Vector3.Normalize(cameraTransform.Position - Transform.Position);
                //
                //float yaw = MathF.Atan2(d.X, d.Z);
                //float pitch = -MathF.Atan2(d.Y, MathF.Sqrt(d.X * d.X + d.Z * d.Z));
                //
                //Transform.Rotation = Quaternion.FromEulerAngles(pitch, yaw, 0);

                Transform.LookAt(cameraTransform.Position);
            }
        }
    }
}