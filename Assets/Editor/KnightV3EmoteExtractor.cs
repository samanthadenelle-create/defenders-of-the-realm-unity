// =============================================================================
// KnightV3EmoteExtractor — expose KnightV3's EMBEDDED animation clips as standalone,
// runtime-loadable .anim assets (owner "try this" 2026-07-03).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
//
// WHY: the owner's new KnightV3.fbx (Character-Creator / AccuRIG humanoid) ships EMBEDDED
// clips — a default WALK and a CUSTOM DANCE — baked as sub-assets of the FBX. A sub-asset
// clip inside an FBX cannot be Resources.Load-ed individually at runtime (Resources.Load on
// the FBX returns the GameObject, not a named clip). To let the DANCE be triggered as a hero
// EMOTE (town idle flourish / victory) — and the WALK reused where it fits — this editor pass
// COPIES each embedded AnimationClip out to a standalone .anim asset under
// Assets/Resources/Heroes/Emotes/, which IS Resources-loadable (WebGL-safe, in-build):
//     Resources.Load<AnimationClip>("Heroes/Emotes/KnightV3_<clip>")
// The runtime hook is DeNelle.Village.HeroEmote (plays one of these on the live hero animator).
//
// The copy is a humanoid clip (KnightV3 imports Humanoid), so it retargets onto the shared
// hero avatar exactly like the other Knight anims. Read-then-write editor-only; never touches
// the FBX. Batchmode entry: DeNelle.Editor.KnightV3EmoteExtractor.ExtractEmotes.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Copies KnightV3.fbx's embedded AnimationClips (walk / custom dance) out to standalone,
    /// Resources-loadable .anim assets under Assets/Resources/Heroes/Emotes/ so they can be
    /// triggered at runtime (HeroEmote). Editor-only; batchmode-callable.
    /// </summary>
    public static class KnightV3EmoteExtractor
    {
        private const string FbxPath   = "Assets/Resources/Heroes/KnightV3.fbx";
        private const string OutDir    = "Assets/Resources/Heroes/Emotes";
        private const string OutPrefix = "KnightV3_";

        [MenuItem("Defenders/Heroes/Extract KnightV3 Emote Clips")]
        public static void ExtractEmotesMenu() => ExtractEmotes();

        /// <summary>
        /// Load every embedded AnimationClip sub-asset of KnightV3.fbx and write a standalone .anim
        /// copy to Resources/Heroes/Emotes/. Logs each clip name + length so the exact WALK / DANCE
        /// clip names are known. Idempotent — re-run overwrites the copies with the current import.
        /// </summary>
        public static void ExtractEmotes()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("========================================================================");
            log.AppendLine("[KnightV3EmoteExtractor] Extracting embedded clips from: " + FbxPath);

            var main = AssetDatabase.LoadMainAssetAtPath(FbxPath);
            if (main == null)
            {
                log.AppendLine("FAIL: KnightV3.fbx not found / not imported. Import it first (Humanoid), then re-run.");
                Debug.LogError(log.ToString());
                return;
            }

            if (!Directory.Exists(OutDir))
            {
                Directory.CreateDirectory(OutDir);
                AssetDatabase.Refresh();
            }

            var clips = new List<AnimationClip>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                log.AppendLine("WARN: NO embedded AnimationClips found on KnightV3.fbx. Confirm importAnimation=1 " +
                               "and that the FBX actually carries baked takes (the owner reported a walk + a dance).");
                Debug.LogWarning(log.ToString());
                return;
            }

            int written = 0;
            foreach (var clip in clips)
            {
                string safe = Sanitize(clip.name);
                string outPath = $"{OutDir}/{OutPrefix}{safe}.anim";
                var copy = Object.Instantiate(clip);
                copy.name = OutPrefix + safe;
                // Replace any existing copy so a re-import/re-run stays current.
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
                if (existing != null) AssetDatabase.DeleteAsset(outPath);
                AssetDatabase.CreateAsset(copy, outPath);
                written++;
                log.AppendLine($"  + '{clip.name}'  len={clip.length:0.00}s  humanoid={clip.isHumanMotion}  " +
                               $"loop={clip.isLooping}  ->  {outPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"[KnightV3EmoteExtractor] DONE: wrote {written} clip(s) to {OutDir}. " +
                           "Runtime: Resources.Load<AnimationClip>(\"Heroes/Emotes/KnightV3_<name>\") " +
                           "or DeNelle.Village.HeroEmote.PlayDance(heroRoot).");
            log.AppendLine("========================================================================");
            Debug.Log(log.ToString());
        }

        // FS-safe clip name (spaces / punctuation → underscore) so the .anim path is stable.
        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "clip";
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
                sb.Append((char.IsLetterOrDigit(c)) ? c : '_');
            return sb.ToString();
        }
    }
}
