using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Gears.World.Components.AddonComponents
{
    public class BasicCameraFlyScript : BaseComponent
    {
        public float Speed { get; set; } = 7.5f;
        public float ShiftSpeed { get; set; } = 15f;
        public float Sensitivity { get; set; } = 0.25f;
        public Camera? Camera { get; private set; }
        private float _yaw = -90f;
        private float _pitch = 0f;

        public override void Start()
        {
            Camera = GameObject?.GetComponent<Camera>();

            if (Camera == null)
            {
                Logger.Instance.LogError("BasicCameraFlyScript requires a Camera component on the same GameObject", nameof(Camera));
            }
        }

        public override void Update()
        {
            base.Update();

            if (Camera == null || Transform == null) return;

            if (Input.GetMouseButtonPressed(Input.MouseButtons.Left))
            {
                if (Input.IsMouseLocked)
                    Input.SetCursorState(Input.MouseStates.Normal);
                else
                    Input.SetCursorState(Input.MouseStates.Grabbed);
            }

            if (Input.IsMouseLocked)
            {
                HandleMouseLook();
            }

            HandleMovement();
        }

        private void HandleMouseLook()
        {
            Vector2 mouseDelta = Input.GetMouseDelta();

            _yaw -= mouseDelta.X * Sensitivity;
            _pitch -= mouseDelta.Y * Sensitivity;

            _pitch = MathHelper.Clamp(_pitch, -89f, 89f);

            Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(_yaw));
            Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(_pitch));
            Transform!.Rotation = yawRotation * pitchRotation;
        }

        private void HandleMovement()
        {
            Vector3 movement = Vector3.Zero;

            movement += Transform!.Forward * Input.GetAxis("Vertical");
            movement += Transform!.Right * Input.GetAxis("Horizontal");

            if (Input.GetKey(Input.KeyCode.Space))
            {
                movement += Vector3.UnitY;
            }

            if (Input.GetKey(Input.KeyCode.LeftControl))
            {
                movement -= Vector3.UnitY;
            }

            if (movement.LengthSquared > 0)
            {
                movement = Vector3.Normalize(movement);
                Transform!.Translate(movement * (Input.GetKeyDown(Input.KeyCode.LeftShift) ? ShiftSpeed : Speed) * (float)Game.DeltaTime);
            }
        }
    }
}