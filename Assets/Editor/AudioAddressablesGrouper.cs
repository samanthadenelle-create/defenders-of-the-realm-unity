// =============================================================================
// AudioAddressablesGrouper — moves the audio clips OUT of the Resources folders
// and files them into LOCAL Addressable bundles so the 111.4 MB of force-included
// audio stops shipping in every build. Sibling of HeroAddressablesGrouper
// (WO-545) / VfxAddressablesGrouper; same topology + guarantees, audio content.
// -----------------------------------------------------------------------------
// Unity FORCE-INCLUDES everything under ANY folder named Resources. This project
// has EIGHT such roots and audio lives in THREE of them (measured 2026-08-17 —
// do not re-derive):
//     Assets/Audio/Resources                  20 clips  ~66 MB (the music beds)
//     Assets/Resources                         4 clips   ~5 MB (Audio/Music, Sfx)
//     Assets/_Modules/Audio/Resources         30 clips  ~19 MB (Sfx)
//   = 54 clips / 111.4 MB force-included, of which the twenty music beds are ~93 MB.
//
// This tool is the GROUPING + MIGRATION half of the seam (AudioAssetLoader is the
// CODE half). Two things must both be true for the bytes to leave the build:
//   1. the assets are marked Addressable (so they ride in a bundle), AND
//   2. the assets no longer live under any Resources/ folder (else they
//      double-ship — once in the Resources block AND once in the bundle).
//
// TOPOLOGY produced:
//   • per-track group "Audio_Music_<leaf>" — one bundle per music bed, so a build
//                                            can pull only the tracks it plays.
//                                            (Mirrors HeroAddressablesGrouper's
//                                            one-bundle-per-hero shape.)
//   • shared group    "Audio_Sfx"           — every short/mid clip in ONE bundle.
//                                            SFX are small, numerous and mostly
//                                            wanted together (the combat prewarm in
//                                            AudioService.PrewarmCombatSfx pulls
//                                            ~20 of them at once), so one shared
//                                            bundle is right; 30 tiny bundles would
//                                            be pure catalog overhead.
//   All groups are LOCAL (default schema = the same Local.BuildPath/LoadPath the
//   shipping "Gear"/"Hero_*" bundles use → they land in StreamingAssets/aa/<target>/).
//
// ADDRESSES are COMPUTED, never tabled: address = the asset's path relative to the
// Resources root it sits under (or to AudioContentRoot post-migration), minus the
// extension. That is EXACTLY the key AudioAssetLoader / Resources.Load queries:
//     Assets/Audio/Resources/title.mp3                    -> "title"
//     Assets/Audio/Resources/Music/echo_theme.mp3         -> "Music/echo_theme"
//     Assets/Audio/Resources/Music/Raid/brass-rampart.mp3 -> "Music/Raid/brass-rampart"
//     Assets/_Modules/Audio/Resources/Sfx/SwordSwing.wav  -> "Sfx/SwordSwing"
//     Assets/Resources/Audio/Music/GameOver.mp3           -> "Audio/Music/GameOver"
// A hardcoded key table would rot the first time a clip is renamed; this cannot.
//
// MIGRATION target = Assets/AudioContent/ (NOT under Resources), preserving the
// Resources-RELATIVE sub-path so the address is IDENTICAL before and after the
// move. Moved via AssetDatabase.MoveAsset (GUID- and .meta-preserving → the import
// settings written by AudioImportOptimizer travel with the asset, so the bundle
// stays small).
//
// ⚠ KNOWN DUPLICATE ADDRESS (measured, not hypothetical): two different files
// resolve to the SAME extension-less key "Sfx/Heal" —
//     Assets/_Modules/Audio/Resources/Sfx/Heal.wav   (2.0 s)
//     Assets/Resources/Sfx/Heal.mp3                  (5.2 s)
// Marking both at one address is an Addressables BUILD ERROR, so the second is
// detected, warned and SKIPPED (first-seen wins). Note this ambiguity already
// exists today: Resources.Load<AudioClip>("Sfx/Heal") picks one of the two by
// Unity's own root ordering, so the grouper is surfacing a latent defect, not
// creating one. The fix is to rename or delete one file — a content decision for
// the owner, deliberately NOT made here.
//
// ⚠ SYNC/ASYNC GATE (inherited from WO-545): once assets leave Resources the only
// load path is Addressables, and AudioAssetLoader uses WaitForCompletion, which
// WebGL does NOT support for a bundle that still has to be downloaded. Run the
// MIGRATION only alongside the build check that confirms the audio bundles are
// warmed async before the sync load (or after the loader goes async).
// GroupAudio() is mark-only and safe to run any time.
//
// ⚠ KEEP-BEHIND — deliberately NOT moved (each justified at file:line):
//   • Assets/Audio/Resources/Audio/GameAudioMixer.mixer — an AudioMixer, not an
//     AudioClip, so this tool never enumerates it. It is Resources-loaded from
//     THREE places that have no seam: Assets/_Modules/Audio/AudioBootstrap.cs:88,
//     Assets/_Modules/Settings/AudioMixerBridge.cs:163 and
//     Assets/_Modules/Village/Audio/VillageAudioResources.cs:55. It is 1.8 KB —
//     moving it buys nothing and would risk a silent unmixed build.
//   • Anything a Resources-resident ScriptableObject or prefab references. Verified
//     ZERO today: no .unity/.prefab/.asset/.controller/.mixer file in the tree
//     contains the GUID of any Resources audio clip, and neither
//     Resources/Audio/SfxClipLibrary.asset (AudioService.cs:1149) nor
//     Resources/DeNelleAudioService.prefab (AudioBootstrap.cs:46) EXISTS on disk.
//     If either is ever authored under Resources with clips wired into it, those
//     clips are dragged back into the build regardless of this migration — re-run
//     the GUID check before trusting the saving.
//
// Run (menu): Defenders > Build > Group Audio Addressable      (mark only, no move)
//             Defenders > Build > Migrate Audio Out Of Resources (move only)
//             Defenders > Build > Group + Migrate Audio          (one-shot)
//   headless: -executeMethod DeNelle.Editor.AudioAddressablesGrouper.GroupAndMigrateAudio
// EDITOR-ONLY. Mutates the Addressables settings asset + moves assets; does NOT run
// gameplay, does NOT commit, does NOT touch any data JSON.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Groups + migrates the Resources audio clips into LOCAL Addressable
    /// bundles. Mark-only, migrate-only, and one-shot entry points; idempotent +
    /// guarded for the no-Addressables-settings case.</summary>
    public static class AudioAddressablesGrouper
    {
        // Pre-migration Resources roots that actually contain audio (the shipped
        // locations today). Order matters: first-seen wins on a duplicate address.
        internal static readonly string[] ResourcesRoots =
        {
            "Assets/Audio/Resources",
            "Assets/_Modules/Audio/Resources",
            "Assets/Resources",
        };

        // Post-migration destination (NOT under any Resources/ folder). The
        // Resources-relative sub-path is preserved underneath it so addresses are
        // unchanged by the move.
        internal const string AudioContentRoot = "Assets/AudioContent";

        // Group names — one bundle per music bed, one shared bundle for all SFX.
        internal const string MusicGroupPrefix = "Audio_Music_";
        internal const string SharedSfxGroup   = "Audio_Sfx";

        // ── Entry points ────────────────────────────────────────────────────────

        [MenuItem("Defenders/Build/Group + Migrate Audio")]
        public static void GroupAndMigrateAudio()
        {
            // Order: migrate FIRST (so grouping reads from the final, non-Resources
            // location), then group at that location.
            MigrateAudioOutOfResources();
            GroupAudio();
        }

        [MenuItem("Defenders/Build/Group Audio Addressable")]
        public static void GroupAudio()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[AudioAddressablesGrouper] Addressable settings not found " +
                    "(AddressableAssetSettingsDefaultObject.Settings == null) — Addressables not " +
                    "initialised. Nothing grouped. AudioAssetLoader keeps using the Resources fallback.");
                return;
            }

            int music = 0, sfx = 0, skipped = 0, dupes = 0, roots = 0;
            var seenAddr = new HashSet<string>(StringComparer.Ordinal);

            AddressableAssetGroup sfxGroup = settings.FindGroup(SharedSfxGroup) ?? CreateBundledGroup(settings, SharedSfxGroup);
            if (sfxGroup == null)
            {
                Debug.LogWarning($"[AudioAddressablesGrouper] could not create '{SharedSfxGroup}' — nothing grouped.");
                return;
            }

            foreach (string root in ActiveRoots())
            {
                roots++;
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;

                    string address = AddressFor(path, root);
                    if (string.IsNullOrEmpty(address)) continue;

                    if (!seenAddr.Add(address))
                    {
                        // Two files share an extension-less key (measured: "Sfx/Heal" from
                        // Heal.wav and Heal.mp3). Marking both at one address is an
                        // Addressables BUILD ERROR — keep the first, warn on the rest.
                        dupes++;
                        Debug.LogWarning($"[AudioAddressablesGrouper] duplicate audio address '{address}' " +
                                         $"({path}) — skipped (first-seen wins). Resources.Load(\"{address}\") is " +
                                         "ALREADY ambiguous between these two files today; rename or delete one.");
                        continue;
                    }

                    bool isMusic = IsMusic(path);
                    AddressableAssetGroup group = sfxGroup;
                    if (isMusic)
                    {
                        string groupName = MusicGroupPrefix + Path.GetFileNameWithoutExtension(path);
                        group = settings.FindGroup(groupName) ?? CreateBundledGroup(settings, groupName);
                        if (group == null)
                        {
                            Debug.LogWarning($"[AudioAddressablesGrouper] could not create group '{groupName}' — " +
                                             $"skipping '{address}'.");
                            skipped++;
                            continue;
                        }
                    }

                    if (MarkEntry(settings, group, guid, address))
                    {
                        if (isMusic) music++; else sfx++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AudioAddressablesGrouper] Grouped from {roots} root(s): {music} music clip(s) into " +
                      $"'{MusicGroupPrefix}*' bundles + {sfx} sfx clip(s) into '{SharedSfxGroup}' " +
                      $"({skipped} already addressed/skipped, {dupes} duplicate-address skipped).");
        }

        [MenuItem("Defenders/Build/Migrate Audio Out Of Resources")]
        public static void MigrateAudioOutOfResources()
        {
            int moved = 0, already = 0, failed = 0, dupes = 0;
            var seenDst = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool anyRoot = false;
            foreach (string root in ResourcesRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                anyRoot = true;

                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
                {
                    string src = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(src)) continue;

                    string rel = RelativeTo(src, root);
                    if (string.IsNullOrEmpty(rel)) continue;

                    string dst = AudioContentRoot + "/" + rel;
                    if (!seenDst.Add(dst))
                    {
                        // Same relative path from two different Resources roots — moving both
                        // would clobber. Keep the first, warn on the rest (same first-seen rule
                        // the grouper uses, so the surviving file is the one that got the address).
                        dupes++;
                        Debug.LogWarning($"[AudioAddressablesGrouper] destination collision '{dst}' for '{src}' — " +
                                         "NOT moved (first-seen wins; it stays in Resources and keeps shipping). " +
                                         "Rename one of the two files to complete the saving.");
                        continue;
                    }

                    EnsureFolder(Path.GetDirectoryName(dst)?.Replace('\\', '/'));
                    moved += TryMove(src, dst, ref already, ref failed);
                }
            }

            if (!anyRoot)
            {
                Debug.LogWarning("[AudioAddressablesGrouper] none of the Resources roots exist — nothing to migrate " +
                                 "(already migrated?).");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AudioAddressablesGrouper] Migration: moved {moved} clip(s) to '{AudioContentRoot}' " +
                      $"({already} already there, {failed} failed, {dupes} destination collisions kept behind). " +
                      "GameAudioMixer.mixer is DELIBERATELY kept in Resources (see the KEEP-BEHIND header block).");
            if (failed > 0)
                Debug.LogWarning("[AudioAddressablesGrouper] one or more moves FAILED — see MoveAsset warnings above; " +
                                 "the build-size win is incomplete until every audio clip leaves Resources.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Returns the migrated root (Assets/AudioContent) when it already holds
        /// audio clips, else null — meaning the pre-migration Resources roots are still
        /// live. Lets grouping run correctly before OR after a move.</summary>
        internal static string ResolveActiveRoot()
        {
            if (AssetDatabase.IsValidFolder(AudioContentRoot))
            {
                string[] found = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioContentRoot });
                if (found != null && found.Length > 0) return AudioContentRoot;
            }
            return null;
        }

        /// <summary>The roots the grouper should enumerate: the migrated root once it holds
        /// clips, otherwise every Resources root that exists.</summary>
        internal static IEnumerable<string> ActiveRoots()
        {
            string migrated = ResolveActiveRoot();
            if (migrated != null)
            {
                yield return migrated;
                yield break;
            }
            foreach (string root in ResourcesRoots)
                if (AssetDatabase.IsValidFolder(root)) yield return root;
        }

        /// <summary>The Addressable address for an asset = its path relative to
        /// <paramref name="root"/>, minus the extension. NEVER a hardcoded table — this is
        /// byte-identical to the key Resources.Load / AudioAssetLoader is called with.</summary>
        internal static string AddressFor(string assetPath, string root)
        {
            string rel = RelativeTo(assetPath, root);
            if (string.IsNullOrEmpty(rel)) return null;
            int dot = rel.LastIndexOf('.');
            return dot > 0 ? rel.Substring(0, dot) : rel;
        }

        /// <summary>Path relative to <paramref name="root"/> (forward slashes, no leading
        /// slash), or null when the asset is not under it.</summary>
        internal static string RelativeTo(string assetPath, string root)
        {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(root)) return null;
            string p = assetPath.Replace('\\', '/');
            string r = root.Replace('\\', '/').TrimEnd('/') + "/";
            if (!p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return null;
            return p.Substring(r.Length);
        }

        /// <summary>
        /// True when a clip belongs in its own music bundle. Decided from the REAL clip
        /// length + the folder rule, via the single shared classifier
        /// <see cref="AudioImportOptimizer.Classify"/> — so the grouper and the import
        /// optimizer can never disagree about what "music" means. Falls back to the folder
        /// hint when the clip cannot be loaded (shared bundle = the safe side).
        /// </summary>
        internal static bool IsMusic(string assetPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null) return false;
            return AudioImportOptimizer.Classify(clip.length, AudioImportOptimizer.IsPlayOneShotFolder(assetPath))
                   == AudioImportOptimizer.ClipClass.Music;
        }

        /// <summary>Move an asset if it exists at <paramref name="src"/>. Increments
        /// <paramref name="already"/> when the source is gone (assumed already migrated) and
        /// <paramref name="failed"/> on a MoveAsset error. Returns 1 on a successful move, else 0.</summary>
        private static int TryMove(string src, string dst, ref int already, ref int failed)
        {
            if (!AssetDatabase.IsValidFolder(src) && AssetDatabase.AssetPathToGUID(src) == string.Empty)
            {
                already++;
                return 0;
            }
            string err = AssetDatabase.MoveAsset(src, dst);
            if (string.IsNullOrEmpty(err)) return 1;
            failed++;
            Debug.LogWarning($"[AudioAddressablesGrouper] MoveAsset '{src}' -> '{dst}' FAILED: {err}");
            return 0;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Move the asset into the group and set its address. True when a change was
        /// made; false when already at this exact address (idempotent). Mirrors
        /// HeroAddressablesGrouper.MarkEntry.</summary>
        private static bool MarkEntry(AddressableAssetSettings settings, AddressableAssetGroup group,
                                      string guid, string address)
        {
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, postEvent: false);
            if (entry == null) return false;

            if (string.Equals(entry.address, address, StringComparison.Ordinal))
                return false; // already addressed — no churn

            entry.SetAddress(address, postEvent: false);
            return true;
        }

        /// <summary>Create a LOCAL bundled group with the standard bundled/content-update schemas
        /// (mirrors the Default Local Group + the shipping 'Gear'/'Hero_*' groups —
        /// Local.BuildPath/LoadPath, so the bundle lands in StreamingAssets/aa/&lt;target&gt;/).</summary>
        private static AddressableAssetGroup CreateBundledGroup(AddressableAssetSettings settings, string groupName)
        {
            return settings.CreateGroup(
                groupName,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }
    }
}
