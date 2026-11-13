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
        private bool _frustumDirty = true;
        private Vector2i _lastScreenSize;
        private Vector4[] _frustumPlanes = new Vector4[6];

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
            _frustumDirty = true;
        }

        public override void Update()
        {
            base.Update();
            _viewDirty = true;
            _frustumDirty = true;
        }

        private void RebuildView()
        {
            Vector3 pos = GameObject.Transform.Position;
            Vector3 forward = GameObject.Transform.Forward;
            Vector3 up = GameObject.Transform.Up;
            _viewMatrix = Matrix4.LookAt(pos, pos + forward, up);
            _viewDirty = false;
            _frustumDirty = true;
        }

        private void RebuildProjection(Vector2i screenSize)
        {
            if (screenSize.X == 0 || screenSize.Y == 0) return;
            float fovRad = MathHelper.DegreesToRadians(FOV);
            float aspect = screenSize.X / (float)screenSize.Y;
            _projectionMatrix = CreatePerspective(fovRad, aspect, NearClip, FarClip);
            _lastScreenSize = screenSize;
            _projDirty = false;
            _frustumDirty = true;
        }

        public Vector4[] GetFrustum()
        {
            if (!_frustumDirty)
                return _frustumPlanes;

            Matrix4 vp = ProjectionMatrix * ViewMatrix;

            // Left: Row3 + Row0
            _frustumPlanes[0] = new Vector4(
                vp.M14 + vp.M11,
                vp.M24 + vp.M21,
                vp.M34 + vp.M31,
                vp.M44 + vp.M41);

            // Right: Row3 - Row0
            _frustumPlanes[1] = new Vector4(
                vp.M14 - vp.M11,
                vp.M24 - vp.M21,
                vp.M34 - vp.M31,
                vp.M44 - vp.M41);

            // Bottom: Row3 + Row1
            _frustumPlanes[2] = new Vector4(
                vp.M14 + vp.M12,
                vp.M24 + vp.M22,
                vp.M34 + vp.M32,
                vp.M44 + vp.M42);

            // Top: Row3 - Row1
            _frustumPlanes[3] = new Vector4(
                vp.M14 - vp.M12,
                vp.M24 - vp.M22,
                vp.M34 - vp.M32,
                vp.M44 - vp.M42);

            // Near: Row3 + Row2
            _frustumPlanes[4] = new Vector4(
                vp.M14 + vp.M13,
                vp.M24 + vp.M23,
                vp.M34 + vp.M33,
                vp.M44 + vp.M43);

            // Far: Row3 - Row2
            _frustumPlanes[5] = new Vector4(
                vp.M14 - vp.M13,
                vp.M24 - vp.M23,
                vp.M34 - vp.M33,
                vp.M44 - vp.M43);

            for (int i = 0; i < 6; i++)
            {
                Vector3 n = new Vector3(_frustumPlanes[i].X,
                                        _frustumPlanes[i].Y,
                                        _frustumPlanes[i].Z);
                float len = n.Length;
                if (len > 0f)
                {
                    _frustumPlanes[i] = new Vector4(
                        _frustumPlanes[i].X / len,
                        _frustumPlanes[i].Y / len,
                        _frustumPlanes[i].Z / len,
                        _frustumPlanes[i].W / len);
                }
            }

            _frustumDirty = false;
            return _frustumPlanes;
        }

        public static Matrix4 CreatePerspective(float fov, float aspect, float zNear, float zFar)
        {
            float f = 1f / MathF.Tan(fov / 2f);
            Matrix4 result = Matrix4.Identity;
            result.M11 = f / aspect;
            result.M22 = f;
            result.M33 = (zFar + zNear) / (zNear - zFar);
            result.M34 = -1f;
            result.M43 = (2f * zFar * zNear) / (zNear - zFar);
            result.M44 = 0f;
            return result;
        }
    }
}