using System;
using System.Collections.Generic;
using System.IO;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1289: walls stay visible and the camera always seats on their near side.</summary>
    public static class CameraWallOcclusionRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            float normal = SmartMobileCamera.AllowedCameraDistance(5f, 3f, 0.2f);
            if (Math.Abs(normal - 2.8f) > 0.001f)
                failures.Add("normal wall hit did not seat camera at hit minus skin");

            float tight = SmartMobileCamera.AllowedCameraDistance(5f, 0.1f, 0.2f);
            if (Math.Abs(tight - 0.25f) > 0.001f)
                failures.Add("tight wall hit did not use the near-side emergency floor");

            float clearCap = SmartMobileCamera.AllowedCameraDistance(5f, 9f, 0.2f);
            if (Math.Abs(clearCap - 5f) > 0.001f)
                failures.Add("collision distance exceeded the authored camera boom");

            string path = Path.Combine("Assets", "_Modules", "Village", "Hero", "SmartMobileCamera.cs");
            string source = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            int methodAt = source.IndexOf("private Vector3 ApplyCollision", StringComparison.Ordinal);
            int nextAt = source.IndexOf("private void FadeOccluder", methodAt, StringComparison.Ordinal);
            string method = methodAt >= 0 && nextAt > methodAt
                ? source.Substring(methodAt, nextAt - methodAt) : string.Empty;

            if (method.Contains("FadeOccluder(col)"))
                failures.Add("ApplyCollision still hides whole wall renderers");
            if (!method.Contains("nearestOccluderDist < float.MaxValue"))
                failures.Add("camera does not collision-resolve every obstruction");

            int smoothAt = source.IndexOf("Vector3 smoothed = Vector3.SmoothDamp", StringComparison.Ordinal);
            int collisionAt = source.IndexOf("transform.position = ApplyCollision(smoothed, dt)", StringComparison.Ordinal);
            if (smoothAt < 0 || collisionAt < smoothAt)
                failures.Add("collision is not applied to the final smoothed camera position");
            if (!source.Contains("if (_cam.nearClipPlane > 0.08f) _cam.nearClipPlane = 0.08f;"))
                failures.Add("tight-seat near-plane cap is missing");

            reason = failures.Count == 0
                ? "CAMERA_WALL_OCCLUSION_OK near-side collision; walls remain rendered"
                : "CAMERA_WALL_OCCLUSION_FAIL: " + string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
