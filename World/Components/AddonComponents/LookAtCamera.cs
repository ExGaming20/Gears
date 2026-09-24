using OpenTK.Mathematics;

namespace Gears.World.Components.AddonComponents
{
    public class LookAtCamera : BaseComponent
    {
        public Camera? Camera { get; private set; }

        public override void Start()
        {
            var cameraObject = Scene.ActiveCamera;
            if (cameraObject == null)
            {
                Logger.Instance.LogError("LookAtCamera requires a Camera component on the same GameObject", nameof(Camera));
                return;
            }
            Camera = cameraObject.GetComponent<Camera>();
            if (Camera == null)
            {
                Logger.Instance.LogError("ActiveCamera GameObject does not have a Camera component", nameof(Camera));
            }
        }

        public override void Update()
        {
            base.Update();
            if (Camera == null || Transform == null) return;
            var lookAtMatrix = Matrix4.LookAt(Transform.Position, Camera.Transform.Position, Vector3.UnitY);
            var rotationMatrix3 = new Matrix3(
                lookAtMatrix.M11, lookAtMatrix.M12, lookAtMatrix.M13,
                lookAtMatrix.M21, lookAtMatrix.M22, lookAtMatrix.M23,
                lookAtMatrix.M31, lookAtMatrix.M32, lookAtMatrix.M33
            );
            Transform.Rotation = Quaternion.FromMatrix(rotationMatrix3);
        }
    }
}
