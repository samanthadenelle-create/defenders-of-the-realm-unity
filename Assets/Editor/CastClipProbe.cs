// Temporary diagnostic (demo-day 2026-07-12): why is the knight.cast .anim
// "NOT LOADABLE"? Prints exactly what the asset database sees at the path.
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CastClipProbe
    {
        public static void Run()
        {
            const string path = "Assets/HeroPackages/Knight/Animations/Extracted/Combat_Spell_MagicalMoves_SpellCast_02.anim";
            Debug.Log($"[CastProbe] guid='{AssetDatabase.AssetPathToGUID(path)}'");
            var direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Debug.Log($"[CastProbe] direct={(direct != null ? $"{direct.name} len={direct.length:F3}s" : "<null>")}");
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            Debug.Log($"[CastProbe] all-assets count={all.Length}");
            foreach (var a in all)
                Debug.Log($"[CastProbe]   {a.GetType().Name} '{a.name}'" +
                    (a is AnimationClip c ? $" len={c.length:F3}s real={MotionClipPicker.IsRealMotionClip(c)}" : ""));
            Debug.Log("[CastProbe] CAST_PROBE_DONE");
        }
    }
}
