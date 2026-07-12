// Shared FBX sub-asset picker — rejects ActorCore/iClone T-pose takes, prefers longest motion.

using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class MotionClipPicker
    {
        public static bool IsRealMotionClip(AnimationClip c)
        {
            if (c == null) return false;
            if (c.name.StartsWith("__preview", StringComparison.Ordinal)) return false;
            string n = c.name.ToLowerInvariant();
            if (n.Contains("t-pose") || n.Contains("tpose") || n.Contains("bind")) return false;
            if (n.StartsWith("0_", StringComparison.Ordinal) && n.Contains("pose")) return false;
            return c.length >= 0.1f;
        }

        public static AnimationClip PickBestFromFbx(string path)
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