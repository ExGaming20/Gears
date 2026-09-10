using OpenTK.Mathematics;
using Gears.Graphics;

namespace Gears.World.Components
{
    public class Camera : BaseComponent
    {
        public float FOV { get; set; } = 90f;
        public float NearClip { get; set; } = 0.01f;
        public float FarClip { get; set; } = 1000f;
        private Matrix4 _viewMatrix = Matrix4.Identity;
        private Matrix4 _projectionMatrix = Matrix4.Identity;
        private bool _viewDirty = true;
        private bool _projDirty = true;
        private Vector2i _lastScreenSize;

        public RenderTexture? Target { get; set; }

        public Matrix4 ViewMatrix
        {
            get
            {
                if (_viewDirty) RebuildView();
                return _viewMatrix;
            }
        }

        public Matrix4 ProjectionMatrix
        {
            get
            {
                Vector2i screen = Target != null
                    ? Target.Size
                    : new Vector2i((int)Game.WindowSize.X, (int)Game.WindowSize.Y);

                if (_projDirty || screen != _lastScreenSize)
                    RebuildProjection(screen);
                return _projectionMatrix;
            }
        }

        public override void Start()
        {
            base.Start();
            _viewDirty = true;
            _projDirty = true;
        }

        public override void Update()
        {
            base.Update();
            _viewDirty = true;
        }

        private void RebuildView()
        {
            Vector3 pos = GameObject.Transform.Position;
            Vector3 forward = GameObject.Transform.Forward;
            Vector3 up = GameObject.Transform.Up;
            _viewMatrix = Matrix4.LookAt(pos, pos + forward, up);
            _viewDirty = false;
        }

        private void RebuildProjection(Vector2i screenSize)
        {
            if (screenSize.X == 0 || screenSize.Y == 0) return;
            float fovRad = MathHelper.DegreesToRadians(FOV);
            float aspect = screenSize.X / (float)screenSize.Y;
            _projectionMatrix = CreatePerspective(fovRad, aspect, NearClip, FarClip);
            _lastScreenSize = screenSize;
            _projDirty = false;
        }

        public static Matrix4 CreatePerspective(float fov, float aspect, float zNear, float zFar)
        {
            float f = 1f / MathF.Tan(fov / 2f);
            Matrix4 result = Matrix4.Identity;
            result.M11 = f / aspect;
            result.M22 = f;
            result.M33 = zNear / (zNear - zFar);
            result.M34 = -1f;
            result.M43 = zFar * zNear / (zNear - zFar);
            result.M44 = 0f;
            return result;
        }
    }
}