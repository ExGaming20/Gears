using System;
using OpenTK.Mathematics;

namespace Gears.World.Components
{
    public class Transform : BaseComponent
    {
        private Vector3 _localPosition = Vector3.Zero;
        private Quaternion _localRotation = Quaternion.Identity;
        private Vector3 _localScale = Vector3.One;

        private Matrix4 _localMatrix = Matrix4.Identity;
        private bool _dirty = true;

        public Vector3 LocalPosition
        {
            get => _localPosition;
            set { _localPosition = value; _dirty = true; }
        }

        public Quaternion LocalRotation
        {
            get => _localRotation;
            set { _localRotation = value; _dirty = true; }
        }

        public Vector3 LocalScale
        {
            get => _localScale;
            set { _localScale = value; _dirty = true; }
        }

        public Matrix4 LocalMatrix
        {
            get
            {
                if (_dirty) RebuildMatrix();
                return _localMatrix;
            }
        }

        public Matrix4 WorldMatrix
        {
            get
            {
                var parent = GameObject?.Parent?.Transform;
                return parent != null ? LocalMatrix * parent.WorldMatrix : LocalMatrix;
            }
        }

        public Vector3 Position
        {
            get => WorldMatrix.ExtractTranslation();
            set
            {
                var parent = GameObject?.Parent?.Transform;
                if (parent == null)
                {
                    _localPosition = value;
                }
                else
                {
                    Matrix4 parentWorld = parent.WorldMatrix;
                    parentWorld.Invert();
                    _localPosition = Vector3.TransformPosition(value, parentWorld);
                }
                _dirty = true;
            }
        }

        public Quaternion Rotation
        {
            get => WorldMatrix.ExtractRotation();
            set
            {
                var parent = GameObject?.Parent?.Transform;
                _localRotation = parent == null ? value : Quaternion.Invert(parent.Rotation) * value;
                _dirty = true;
            }
        }

        public Vector3 Scale
        {
            get => WorldMatrix.ExtractScale();
            set
            {
                var parent = GameObject?.Parent?.Transform;
                _localScale = parent == null ? value : value / parent.Scale;
                _dirty = true;
            }
        }

        public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Rotation);
        public Vector3 Right => Vector3.Transform(Vector3.UnitX, Rotation);
        public Vector3 Up => Vector3.Transform(Vector3.UnitY, Rotation);

        public Vector3 EulerAngles
        {
            set
            {
                Rotation = Quaternion.FromEulerAngles(
                    MathHelper.DegreesToRadians(value.X),
                    MathHelper.DegreesToRadians(value.Y),
                    MathHelper.DegreesToRadians(value.Z));
            }
        }

        public Vector3 LocalEulerAngles
        {
            set
            {
                LocalRotation = Quaternion.FromEulerAngles(
                    MathHelper.DegreesToRadians(value.X),
                    MathHelper.DegreesToRadians(value.Y),
                    MathHelper.DegreesToRadians(value.Z));
            }
        }

        public Transform(GameObject gameObject)
        {
            GameObject = gameObject;
        }

        public void Translate(Vector3 delta) => Position += delta;
        public void Translate(float x, float y, float z) => Translate(new Vector3(x, y, z));

        public void TranslateLocal(Vector3 delta) => LocalPosition += delta;
        public void TranslateLocal(float x, float y, float z) => TranslateLocal(new Vector3(x, y, z));

        public void Rotate(Quaternion delta) => Rotation = delta * Rotation;
        public void Rotate(float degreesX, float degreesY, float degreesZ) =>
            Rotate(Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(degreesX), MathHelper.DegreesToRadians(degreesY), MathHelper.DegreesToRadians(degreesZ)));

        public void RotateLocal(Quaternion delta) => LocalRotation = delta * LocalRotation;
        public void RotateLocal(float degreesX, float degreesY, float degreesZ) =>
            RotateLocal(Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(degreesX), MathHelper.DegreesToRadians(degreesY), MathHelper.DegreesToRadians(degreesZ)));

        public void ScaleBy(Vector3 factor) => Scale *= factor;
        public void ScaleBy(float uniform) => ScaleBy(new Vector3(uniform));

        public void ScaleByLocal(Vector3 factor) => LocalScale *= factor;
        public void ScaleByLocal(float uniform) => ScaleByLocal(new Vector3(uniform));

        public void LookAt(Vector3 target, Vector3? up = null)
        {
            Vector3 pos = Position;
            Vector3 dir = target - pos;
            if (dir.LengthSquared < 1e-8f) return;

            Vector3 upVec = up ?? Vector3.UnitY;
            Matrix4 view = Matrix4.LookAt(pos, target, upVec);
            view.Invert();

            Rotation = view.ExtractRotation() * Quaternion.FromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        }

        private void RebuildMatrix()
        {
            var t = Matrix4.CreateTranslation(_localPosition);
            var r = Matrix4.CreateFromQuaternion(_localRotation);
            var s = Matrix4.CreateScale(_localScale);

            _localMatrix = s * r * t;
            _dirty = false;
        }

        public override string ToString()
        {
            return $"Position={Position} LocalPosition={_localPosition} Rotation={Rotation} LocalRotation={_localRotation} Scale={Scale} LocalScale={_localScale}";
        }
    }
}