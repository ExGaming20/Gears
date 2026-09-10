using System;
using OpenTK.Mathematics;

namespace Gears.World.Components
{
    public class Transform : BaseComponent
    {
        // -----------------------------------------------------------------------
        // Raw TRS fields
        // -----------------------------------------------------------------------

        private Vector3 _position = Vector3.Zero;
        private Vector3 _localPosition = Vector3.Zero;
        private Quaternion _rotation = Quaternion.Identity;
        private Quaternion _localRotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;
        private Vector3 _localScale = Vector3.One;

        private Matrix4 _localMatrix = Matrix4.Identity;
        private bool _dirty = true;

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        public Vector3 Position
        {
            get => _position;
            set
            {
                _position = value;
                UpdateLocalPositionFromWorld();
                _dirty = true;
            }
        }

        public Vector3 LocalPosition
        {
            get => _localPosition;
            set
            {
                _localPosition = value;
                UpdateWorldPositionFromLocal();
                _dirty = true;
            }
        }

        public Quaternion Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                UpdateLocalRotationFromWorld();
                _dirty = true;
            }
        }

        public Quaternion LocalRotation
        {
            get => _localRotation;
            set
            {
                _localRotation = value;
                UpdateWorldRotationFromLocal();
                _dirty = true;
            }
        }

        public Vector3 Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                UpdateLocalScaleFromWorld();
                _dirty = true;
            }
        }

        public Vector3 LocalScale
        {
            get => _localScale;
            set
            {
                _localScale = value;
                UpdateWorldScaleFromLocal();
                _dirty = true;
            }
        }

        public Matrix4 LocalMatrix
        {
            get
            {
                if (_dirty)
                {
                    RebuildMatrix();
                }

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

        public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, _rotation);
        public Vector3 Right => Vector3.Transform(Vector3.UnitX, _rotation);
        public Vector3 Up => Vector3.Transform(Vector3.UnitY, _rotation);

        public Vector3 EulerAngles
        {
            set
            {
                _rotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(value.X), MathHelper.DegreesToRadians(value.Y), MathHelper.DegreesToRadians(value.Z));
                UpdateLocalRotationFromWorld();
                _dirty = true;
            }
        }

        public Vector3 LocalEulerAngles
        {
            set
            {
                _localRotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(value.X), MathHelper.DegreesToRadians(value.Y), MathHelper.DegreesToRadians(value.Z));
                UpdateWorldRotationFromLocal();
                _dirty = true;
            }
        }

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public Transform(GameObject gameObject)
        {
            GameObject = gameObject;
        }

        // -----------------------------------------------------------------------
        // Mutation helpers
        // -----------------------------------------------------------------------

        public void Translate(Vector3 delta)
        {
            _position += delta;
            UpdateLocalPositionFromWorld();
            _dirty = true;
        }

        public void Translate(float x, float y, float z)
        {
            Translate(new Vector3(x, y, z));
        }

        public void TranslateLocal(Vector3 delta)
        {
            _localPosition += delta;
            UpdateWorldPositionFromLocal();
            _dirty = true;
        }

        public void TranslateLocal(float x, float y, float z)
        {
            TranslateLocal(new Vector3(x, y, z));
        }

        public void Rotate(Quaternion delta)
        {
            _rotation = delta * _rotation;
            UpdateLocalRotationFromWorld();
            _dirty = true;
        }

        public void Rotate(float degreesX, float degreesY, float degreesZ)
        {
            Rotate(Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(degreesX), MathHelper.DegreesToRadians(degreesY), MathHelper.DegreesToRadians(degreesZ)));
        }

        public void RotateLocal(Quaternion delta)
        {
            _localRotation = delta * _localRotation;
            UpdateWorldRotationFromLocal();
            _dirty = true;
        }

        public void RotateLocal(float degreesX, float degreesY, float degreesZ)
        {
            RotateLocal(Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(degreesX), MathHelper.DegreesToRadians(degreesY), MathHelper.DegreesToRadians(degreesZ)));
        }

        public void ScaleBy(Vector3 factor)
        {
            _scale *= factor;
            UpdateLocalScaleFromWorld();
            _dirty = true;
        }

        public void ScaleBy(float uniform)
        {
            ScaleBy(new Vector3(uniform));
        }

        public void ScaleByLocal(Vector3 factor)
        {
            _localScale *= factor;
            UpdateWorldScaleFromLocal();
            _dirty = true;
        }

        public void ScaleByLocal(float uniform)
        {
            ScaleByLocal(new Vector3(uniform));
        }

        public void LookAt(Vector3 target, Vector3? up = null)
        {
            var dir = Vector3.Normalize(target - _position);

            if (dir.LengthSquared < 1e-6f)
            {
                return;
            }

            var upVec = up ?? Vector3.UnitY;
            var matrix = Matrix4.LookAt(_position, target, upVec);

            matrix.Invert();
            _rotation = matrix.ExtractRotation();
            UpdateLocalRotationFromWorld();
            _dirty = true;
        }

        // -----------------------------------------------------------------------
        // Private
        // -----------------------------------------------------------------------

        private void RebuildMatrix()
        {
            var t = Matrix4.CreateTranslation(_localPosition);
            var r = Matrix4.CreateFromQuaternion(_localRotation);
            var s = Matrix4.CreateScale(_localScale);

            _localMatrix = s * r * t;

            _dirty = false;
        }

        private void UpdateLocalPositionFromWorld()
        {
            var parent = GameObject?.Parent?.Transform;

            if (parent == null)
            {
                _localPosition = _position;
            }
            else
            {
                var parentWorldMatrix = parent.WorldMatrix;
                parentWorldMatrix.Invert();
                _localPosition = Vector3.TransformPosition(_position, parentWorldMatrix);
            }
        }

        private void UpdateWorldPositionFromLocal()
        {
            var parent = GameObject?.Parent?.Transform;

            if (parent == null)
            {
                _position = _localPosition;
            }
            else
            {
                var parentWorldMatrix = parent.WorldMatrix;
                _position = Vector3.TransformPosition(_localPosition, parentWorldMatrix);
            }
        }

        private void UpdateLocalRotationFromWorld()
        {
            var parent = GameObject?.Parent?.Transform;

            if (parent == null)
            {
                _localRotation = _rotation;
            }
            else
            {
                _localRotation = Quaternion.Invert(parent._rotation) * _rotation;
            }
        }

        private void UpdateWorldRotationFromLocal()
        {
            var parent = GameObject?.Parent?.Transform;

            if (parent == null)
            {
                _rotation = _localRotation;
            }
            else
            {
                _rotation = parent._rotation * _localRotation;
            }
        }

        private void UpdateLocalScaleFromWorld()
        {
            var parent = GameObject?.Parent?.Transform;

            if (parent == null)
            {
                _localScale = _scale;
            }
            else
            {
                _localScale = _scale / parent._scale;
            }
        }

        private void UpdateWorldScaleFromLocal()
        {
            var parent = GameObject?.Parent?.Transform;

            if (parent == null)
            {
                _scale = _localScale;
            }
            else
            {
                _scale = _localScale * parent._scale;
            }
        }

        // -----------------------------------------------------------------------
        // Utility
        // -----------------------------------------------------------------------

        public override string ToString()
        {
            return $"Position={_position} LocalPosition={_localPosition} Rotation={_rotation} LocalRotation={_localRotation} Scale={_scale} LocalScale={_localScale}";
        }
    }
}