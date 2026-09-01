using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Validated, explicit application seam for reviewed animation contact frames.</summary>
    public static class CombatAnimationHitFrameManifest
    {
        private const string ManifestPath = "Assets/Editor/CombatAnimationHitFrames.json";

        [Serializable] private sealed class Manifest { public int schemaVersion; public Entry[] entries; }
        [Serializable] private sealed class Entry
        {
            public string assetPath;
            public string clipName;
            public float hitFrameNormalized = -1f;
            public string reviewedBy;
            public string reviewNote;
        }

        [MenuItem("Defenders/Animation/Validate Reviewed HitFrame Manifest")]
        public static void ValidateReviewedHitFrames() => Process(apply: false);

        [MenuItem("Defenders/Animation/Apply Reviewed HitFrame Manifest")]
        public static void ApplyReviewedHitFrames() => Process(apply: true);

        private static void Process(bool apply)
        {
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException("HitFrame manifest missing", ManifestPath);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.schemaVersion != 1)
                throw new InvalidOperationException("HitFrame manifest schemaVersion must be 1.");

            Entry[] entries = manifest.entries ?? Array.Empty<Entry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int changed = 0;
            foreach (Entry entry in entries)
            {
                ValidateEntry(entry, seen);
                if (AssetImporter.GetAtPath(entry.assetPath) is not ModelImporter importer)
                    throw new InvalidOperationException($"No ModelImporter at '{entry.assetPath}'.");
                if (importer.animationType != ModelImporterAnimationType.Human)
                    throw new InvalidOperationException($"'{entry.assetPath}' is not Humanoid; refusing event authoring.");

                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                int matches = 0;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (!string.Equals(clips[i].name, entry.clipName, StringComparison.Ordinal)) continue;
                    matches++;
                    if (!apply) continue;

                    float length = ResolveClipLength(entry.assetPath, entry.clipName);
                    var events = new List<AnimationEvent>(clips[i].events ?? Array.Empty<AnimationEvent>());
                    events.RemoveAll(e => e != null && e.functionName == "HitFrame");
                    events.Add(new AnimationEvent
                    {
                        functionName = "HitFrame",
                        time = Mathf.Clamp01(entry.hitFrameNormalized) * length
                    });
                    clips[i].events = events.ToArray();
                    changed++;
                }
                if (matches != 1)
                    throw new InvalidOperationException($"'{entry.assetPath}::{entry.clipName}' matched {matches} clips; expected exactly one.");
                if (apply)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }

            if (apply) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            Debug.Log($"HITFRAME_MANIFEST_OK mode={(apply ? "apply" : "dry-run")} entries={entries.Length} changed={changed}");
        }

        private static void ValidateEntry(Entry entry, HashSet<string> seen)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.assetPath) || string.IsNullOrWhiteSpace(entry.clipName))
                throw new InvalidOperationException("Every HitFrame entry requires assetPath and clipName.");
            if (!entry.assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException($"HitFrame path must be project-relative: '{entry.assetPath}'.");
            if (entry.hitFrameNormalized < 0.05f || entry.hitFrameNormalized > 0.95f)
                throw new InvalidOperationException($"'{entry.clipName}' normalized contact must be reviewed within [0.05, 0.95].");
            if (string.IsNullOrWhiteSpace(entry.reviewedBy) || string.IsNullOrWhiteSpace(entry.reviewNote))
                throw new InvalidOperationException($"'{entry.clipName}' requires reviewedBy and reviewNote; guessed timings are forbidden.");
            if (!seen.Add(entry.assetPath + "::" + entry.clipName))
                throw new InvalidOperationException($"Duplicate HitFrame entry '{entry.assetPath}::{entry.clipName}'.");
        }

        private static float ResolveClipLength(string path, string clipName)
        {
            float length = -1f;
            int matches = 0;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not AnimationClip clip || clip.name != clipName) continue;
                length = clip.length;
                matches++;
            }
            if (matches != 1 || length <= 0f)
                throw new InvalidOperationException($"Cannot resolve one positive-length AnimationClip '{path}::{clipName}'.");
            return length;
        }
    }
}
