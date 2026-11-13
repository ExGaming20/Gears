using OpenTK.Mathematics;

namespace Gears.Graphics
{
    /// <summary>
    /// World-space view frustum built from a combined View * Projection matrix
    /// (OpenTK row-vector convention: world -> view -> clip).
    /// </summary>
    public struct Frustum
    {
        private Vector4 _left;
        private Vector4 _right;
        private Vector4 _bottom;
        private Vector4 _top;
        private Vector4 _near;
        private Vector4 _far;

        public static Frustum FromViewProjection(Matrix4 vp)
        {
            Frustum f = default;

            // Each plane is a column pair of the row-vector view-projection matrix:
            // clip.x + clip.w, clip.w - clip.x, and so on.
            f._left = new Vector4(
                vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41);
            f._right = new Vector4(
                vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41);
            f._bottom = new Vector4(
                vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42);
            f._top = new Vector4(
                vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42);
            f._near = new Vector4(
                vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43);
            f._far = new Vector4(
                vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43);

            f._left = Normalize(f._left);
            f._right = Normalize(f._right);
            f._bottom = Normalize(f._bottom);
            f._top = Normalize(f._top);
            f._near = Normalize(f._near);
            f._far = Normalize(f._far);

            return f;
        }

        /// <summary>Conservative AABB test using the positive vertex of each plane.</summary>
        public bool Intersects(Box3 aabb)
        {
            return Intersects(_left, aabb)
                && Intersects(_right, aabb)
                && Intersects(_bottom, aabb)
                && Intersects(_top, aabb)
                && Intersects(_near, aabb)
                && Intersects(_far, aabb);
        }

        private static bool Intersects(Vector4 plane, Box3 aabb)
        {
            var p = new Vector3(
                plane.X >= 0 ? aabb.Max.X : aabb.Min.X,
                plane.Y >= 0 ? aabb.Max.Y : aabb.Min.Y,
                plane.Z >= 0 ? aabb.Max.Z : aabb.Min.Z);

            return plane.X * p.X + plane.Y * p.Y + plane.Z * p.Z + plane.W >= 0f;
        }

        private static Vector4 Normalize(Vector4 plane)
        {
            float len = new Vector3(plane.X, plane.Y, plane.Z).Length;
            return len > 0f ? plane / len : plane;
        }
    }
}
