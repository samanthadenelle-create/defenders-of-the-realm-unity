// Hero locomotion clip regression — knight walk/run must NOT resolve to 0_T-Pose takes.
// Self-contained (no MotionCastings/MotionClipPicker) — EditorRegression cannot reference DeNelle.Editor.

using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroLocomotionClipRegression
    {
        private const string RegistryPath =
            "Assets/StreamingAssets/Data/Canonical/motion-castings.json";

        private static readonly string[] KnightLocoKeywords = { "walk", "run", "combatWalk", "combatRun" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            if (!File.Exists(RegistryPath))
            {
                reason = "motion-castings.json missing — skip";
                return true;
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(RegistryPath));
            }
            catch (System.Exception ex)
            {
                reason = "motion-castings.json parse failed: " + ex.Message;
                return false;
            }

            var knight = root["targets"]?["knight"] as JObject;
            if (knight == null)
            {
                reason = "knight locomotion clips OK (no knight target)";
                return true;
            }

            foreach (var kw in KnightLocoKeywords)
            {
                var row = knight[kw] as JObject;
                string clipPath = row?["clip"]?.Value<string>();
                if (string.IsNullOrEmpty(clipPath)) continue;

                var clip = PickBestMotionClip(clipPath);
                if (clip == null)
                {
                    failures.Add($"knight.{kw}: no real motion clip in '{clipPath}'");
                    continue;
                }
                string n = clip.name.ToLowerInvariant();
                if (n.Contains("t-pose") || n.Contains("tpose") || n.Contains("bind"))
                    failures.Add($"knight.{kw}: resolved T-pose/bind '{clip.name}' from '{clipPath}'");
                else if (clip.length < 0.1f)
                    failures.Add($"knight.{kw}: resolved degenerate clip '{clip.name}' len={clip.length:F3}s");
            }

            if (failures.Count > 0)
            {
                reason = string.Join(" | ", failures);
                return false;
            }

            reason = "knight locomotion clips OK (no T-pose takes)";
            return true;
        }

        private static bool IsRealMotionClip(AnimationClip c)
        {
            if (c == null) return false;
            if (c.name.StartsWith("__preview", System.StringComparison.Ordinal)) return false;
            string n = c.name.ToLowerInvariant();
            if (n.Contains("t-pose") || n.Contains("tpose") || n.Contains("bind")) return false;
            if (n.StartsWith("0_", System.StringComparison.Ordinal) && n.Contains("pose")) return false;
            return c.length >= 0.1f;
        }

        private static AnimationClip PickBestMotionClip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (direct != null && IsRealMotionClip(direct))
                return direct;

            AnimationClip best = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (a is not AnimationClip c || !IsRealMotionClip(c)) continue;
                if (best == null || c.length > best.length)
                    best = c;
            }
            return best;
        }
    }
}