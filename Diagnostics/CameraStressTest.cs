using System.IO;
using Gears.Graphics;
using Gears.World.Components;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Gears.Diagnostics
{
    /// <summary>
    /// TEMP diagnostic: teleports/rotates the main camera through a set of poses (positions within a
    /// 10m x 10m x 10m zone plus yaw/pitch orientations) and captures a screenshot + per-tile light
    /// counts for each pose. Used to reproduce the "black tiles when the camera moves" symptom.
    ///
    /// Enabled by setting the environment variable GEARS_STRESS=1. Output PPM/counts files are
    /// written under %TEMP% as cam_XX.ppm / cam_XX.counts (XX = pose index).
    /// </summary>
    public static class CameraStressTest
    {
        private struct Pose
        {
            public Vector3 Pos;
            public Quaternion Rot;
            public Pose(Vector3 p, Quaternion r) { Pos = p; Rot = r; }
        }

        // Reproduces the exact camera poses (position + rotation quaternion) reported by the user as
        // producing black tiles (logged via the "=" key: "P:... R:V: (x,y,z), W: w").
        private static readonly Pose[] Poses = new Pose[]
        {
            new Pose(new Vector3(-0.022879902f, -1.3409165f, -2.40286f),
                new Quaternion(0.20571798f, -0.023482436f, 0.0049378253f, 0.9783172f)),
            new Pose(new Vector3(-1.0783207f, 0.0029847932f, -2.9207885f),
                new Quaternion(0.11358321f, -0.3597365f, 0.04417009f, 0.9250609f)),
            new Pose(new Vector3(-2.0437088f, -0.5602597f, -1.7492573f),
                new Quaternion(0.036382966f, -0.19281745f, 0.0071545416f, 0.9805339f)),
            new Pose(new Vector3(-2.0437088f, -0.5602597f, -1.7492573f),
                new Quaternion(-0.37030333f, -0.26577488f, -0.11144865f, 0.8830733f)),
            new Pose(new Vector3(0.969123f, -0.1104334f, -2.1708643f),
                new Quaternion(0.41501987f, 0.38230076f, -0.19795433f, 0.8015103f)),
            new Pose(new Vector3(0.969123f, -0.1104334f, -2.1708643f),
                new Quaternion(0.34082925f, 0.32992116f, -0.12912865f, 0.87081194f)),
            new Pose(new Vector3(0.35651717f, -1.194226f, -1.7710881f),
                new Quaternion(-0.07401577f, -0.3307269f, -0.026028771f, 0.9404595f)),
            new Pose(new Vector3(1.2918687f, 1.23566f, -2.1822708f),
                new Quaternion(0.0020500899f, -0.34201935f, 0.0007461717f, 0.9396905f)),
            new Pose(new Vector3(3.4242532f, 4.089394f, 4.7977753f),
                new Quaternion(0.038423914f, 0.4728692f, -0.020645915f, 0.8800523f)),
            new Pose(new Vector3(-0.24740256f, -1.1947473f, -3.4194543f),
                new Quaternion(0.13475819f, -0.036741056f, 0.005000238f, 0.99018455f)),
            new Pose(new Vector3(-0.29517582f, -1.3054576f, -2.770761f),
                new Quaternion(0.6173393f, -0.12871806f, 0.103307165f, 0.7691889f)),
            new Pose(new Vector3(0.0026026133f, -1.4754422f, -4.0101447f),
                new Quaternion(0.6173072f, -0.0120132025f, 0.009428021f, 0.786574f)),
            new Pose(new Vector3(0.0026026133f, -1.4754422f, -4.0101447f),
                new Quaternion(0.7004273f, -0.026447147f, 0.025989538f, 0.71276003f)),
            new Pose(new Vector3(-2.6399674f, 0.7558498f, -13.069128f),
                new Quaternion(0.000899202f, 0.9987582f, -0.019613093f, 0.045790095f)),
            new Pose(new Vector3(6.169198f, 7.4929557f, -11.247804f),
                new Quaternion(-0.17277256f, 0.7969931f, 0.268605f, 0.5126432f)),
            new Pose(new Vector3(6.1881747f, 8.60003f, 9.757816f),
                new Quaternion(0.028146423f, 0.12182034f, -0.0034559465f, 0.99214697f)),
            new Pose(new Vector3(0.32686517f, -0.34244013f, -3.0414102f),
                new Quaternion(0.6942992f, -0.09772411f, 0.096033216f, 0.7065241f)),
            new Pose(new Vector3(0.32686517f, 0.032239012f, -3.0414102f),
                new Quaternion(0.6942992f, -0.09772411f, 0.096033216f, 0.7065241f)),
        };

        // Configuration read from the environment (kept out of Game.cs on purpose).
        public static readonly bool Enabled =
            string.Equals(Environment.GetEnvironmentVariable("GEARS_STRESS"), "1", StringComparison.OrdinalIgnoreCase);
        private const int Cadence = 12;   // frames per pose (1 rendered + rest wait)
        private const string Prefix = "cam_";

        private static int _poseIdx = -1;
        private static int _frame = 0;

        /// <summary>Returns true when the caller should capture this frame's screenshot (i.e. the frame a pose was applied).</summary>
        public static bool Advance(GameObject? camObj)
        {
            Camera camera = camObj?.GetComponent<Camera>();
            if (camera == null) return false;

            _frame++;
            if ((_frame % Cadence) != 1) return false;

            _poseIdx++;
            if (_poseIdx >= Poses.Length)
            {
                if (_poseIdx == Poses.Length)
                {
                    Logger.Instance.Log($"STRESS: all {Poses.Length} poses done");
                    _poseIdx = Poses.Length - 1;
                }
                return false;
            }

            Pose pose = Poses[_poseIdx];
            Transform t = camObj.Transform!;
            t.Position = pose.Pos;
            t.Rotation = pose.Rot;
            Logger.Instance.Log($"STRESS pose[{_poseIdx}] pos={pose.Pos} rot={pose.Rot}");
            return true;
        }

        /// <summary>Captures the currently bound framebuffer and the Forward+ tile light counts to %TEMP%.</summary>
        public static void Capture(ForwardPlusRenderer forwardPlus, int width, int height)
        {
            if (_poseIdx < 0) return;

            byte[] px = new byte[width * height * 3];
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgb, PixelType.UnsignedByte, px);

            string ppm = Path.Combine(Path.GetTempPath(), $"{Prefix}{_poseIdx:D2}.ppm");
            using (var fs = File.Create(ppm))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write($"P6\n{width} {height}\n255\n");
                sw.Flush();
                fs.Write(px, 0, px.Length);
            }
            forwardPlus.DumpTileCounts(Path.Combine(Path.GetTempPath(), $"{Prefix}{_poseIdx:D2}.counts"));
            forwardPlus.DumpTileDepths(Path.Combine(Path.GetTempPath(), $"{Prefix}{_poseIdx:D2}.depths"));
            Logger.Instance.Log($"STRESS saved {ppm} ({width}x{height})");
        }
    }
}
