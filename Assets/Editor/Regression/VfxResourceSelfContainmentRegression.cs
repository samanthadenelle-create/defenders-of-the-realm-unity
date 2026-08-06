// =============================================================================
// VfxResourceSelfContainmentRegression [vfx-self-contained]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
// Markers:  VFX_SELF_CONTAINED_OK / VFX_SELF_CONTAINED_FAIL
//
// THE FALSE CLAIM THIS ORACLE EXISTS TO KILL.
//
// On 2026-08-05 the Particle Pack recipes were duplicated into
// Assets/Resources/VFX/** so that "shipped VFX no longer depend on gitignored
// art", and the Boss_FireBreath commit message states that the tracked copy is
// what ships. THAT CLAIM WAS FALSE. AssetDatabase.CopyAsset duplicates the
// PREFAB FILE ONLY -- never the materials, textures, shaders, meshes or
// animation the prefab points at. Every one of those references stayed pointed
// at Assets/UnityTechnologies/ (.gitignore:399) and Assets/Spells Pack/
// (.gitignore:214). Measured before the fix: 28 prefabs under Resources/VFX
// reached 73 distinct assets inside those two gitignored roots -- Boss_FireBreath
// alone reached 6 (3 materials + 3 textures), Env_Candle reached TinyFlame.mat.
//
// The defect was LATENT only because this machine happens to have the packs
// on disk. On a fresh clone, the laptop, or CI those references resolve to
// nothing, and a particle renderer with a null material draws MAGENTA (or
// untextured white / black / invisible, per platform). The owner's stated
// acceptance criterion for that night's work was visual proof of "no magenta
// leak through, no missing shaders" -- so the shipped state failed the very
// criterion it was signed off against.
//
// THE INVARIANT: for every prefab under Assets/Resources/VFX/**, ZERO of its
// recursive dependencies may resolve into a gitignored art root. Not "few".
// Zero. A single unmirrored texture is a magenta particle on a machine nobody
// on this project is looking at.
//
// This file is also the SINGLE HOME of the gitignored-art-root rule
// (GitignoredArtRoots / IsInGitignoredArtRoot / PackDependenciesOf). The mirror
// builder that FIXES the exposure (DeNelle.Editor.VfxResourceArtMirror) calls
// straight into these members rather than re-deriving the list -- the same
// discipline VfxLoopFlagRegression holds for the IsLoop flag, and for the same
// reason: two derivations of one rule is how a tool and its gate come to
// disagree while both report success.
//
// Deterministic, editor-only asset reads. No scene, no PlayMode, seconds to run.
//
// Registered in DataRegression.RunAll (covenant style):
//   Guard.Try(... VfxResourceSelfContainmentRegression.Run(out var r) ...)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class VfxResourceSelfContainmentRegression
    {
        /// <summary>The curated, git-TRACKED VFX tree. Everything a shipped prefab needs must live here.</summary>
        public const string VfxRoot = "Assets/Resources/VFX/";

        /// <summary>Where the mirror builder parks the art it pulls out of the packs.</summary>
        public const string SharedRoot = VfxRoot + "_Shared/";

        // ---------------------------------------------------------------------
        //  The gitignored art roots.
        // ---------------------------------------------------------------------
        // Every entry below was read out of .gitignore in this repo at the line
        // cited -- not assumed from a folder name. An asset under any of these
        // paths exists on THIS machine and on no fresh clone, so a shipped prefab
        // that references one renders broken art everywhere else.
        //
        // The four the VFX tree actually reaches today are the first four; the
        // rest are the other gitignored ART packs, listed so a future VFX pick
        // out of one of them is caught by this same gate on the day it lands.
        //
        // NOT here on purpose: Assets/Lana Studio/Casual RPG VFX -- only its
        // "Upgrade for URP" SUBFOLDER is ignored (.gitignore:312); the pack's
        // materials/textures themselves are tracked, which is why
        // Flash_generic.prefab measures ZERO exposure while sourcing all seven of
        // its dependencies from Lana Studio. Likewise Assets/Materials/ is tracked.
        public static readonly string[] GitignoredArtRoots =
        {
            "Assets/UnityTechnologies/",                 // .gitignore:399  Particle Pack
            "Assets/Spells Pack/",                       // .gitignore:214
            "Assets/Hovl Studio/",                       // .gitignore:218
            "Assets/Mirza Beig/",                        // .gitignore:212
            "Assets/polyperfect/",                       // .gitignore:128
            "Assets/Quaternius/",                        // .gitignore:288
            "Assets/Supercyan/",                         // .gitignore:137
            "Assets/Blink/",                             // .gitignore:292
            "Assets/Black Dragon/",                      // .gitignore:296
            "Assets/Medieval Village/",                  // .gitignore:298
            "Assets/Leohpaz/",                           // .gitignore:372
            "Assets/Tech hud elements/",                 // .gitignore:318
            "Assets/Action/textures/",                   // .gitignore:302
            "Assets/Art/TripoStructures/",               // .gitignore:119
            "Assets/Resources/Structures/",              // .gitignore:121
            "Assets/Lana Studio/Casual RPG VFX/Upgrade for URP/", // .gitignore:312 (subfolder only)
        };

        private const string MarkerOk   = "VFX_SELF_CONTAINED_OK";
        private const string MarkerFail = "VFX_SELF_CONTAINED_FAIL";

        // How many offending paths to name per prefab before summarising. Naming
        // them is the point -- a count with no path is not actionable.
        private const int MaxNamedPerPrefab = 6;

        // =====================================================================
        //  Shared rule -- the mirror builder calls these; it does not re-derive them
        // =====================================================================

        /// <summary>True when this asset path lives inside a gitignored art root.</summary>
        public static bool IsInGitignoredArtRoot(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            for (int i = 0; i < GitignoredArtRoots.Length; i++)
            {
                if (assetPath.StartsWith(GitignoredArtRoots[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>True when this asset path lives inside the curated, tracked VFX tree.</summary>
        public static bool IsInVfxRoot(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.StartsWith(VfxRoot, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Every recursive dependency of an asset that resolves OUTSIDE the curated VFX
        /// tree and INSIDE a gitignored art root -- i.e. the exact set that goes missing
        /// on a fresh clone. Sorted, deduped, never null. This is THE measurement; both
        /// the mirror builder's verify pass and this oracle read it from here.
        /// </summary>
        public static List<string> PackDependenciesOf(string assetPath)
        {
            var offenders = new List<string>();
            if (string.IsNullOrEmpty(assetPath)) return offenders;

            var deps = AssetDatabase.GetDependencies(assetPath, true);
            for (int i = 0; i < deps.Length; i++)
            {
                string d = deps[i];
                if (string.Equals(d, assetPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsInVfxRoot(d)) continue;
                if (!IsInGitignoredArtRoot(d)) continue;
                if (!offenders.Contains(d)) offenders.Add(d);
            }
            offenders.Sort(StringComparer.Ordinal);
            return offenders;
        }

        /// <summary>Every prefab asset path under the curated VFX tree, sorted for determinism.</summary>
        public static List<string> VfxPrefabPaths()
        {
            var paths = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { VfxRoot.TrimEnd('/') });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsInVfxRoot(p) && p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !paths.Contains(p))
                    paths.Add(p);
            }
            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Standalone batch entry point.</summary>
        public static void RunStandalone()
        {
            string reason;
            bool pass = Run(out reason);
            Debug.Log("[vfx-self-contained] standalone result: " + (pass ? "PASS" : "FAIL") + " - " + reason);
        }

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try
            {
                return RunCore(out reason);
            }
            catch (Exception ex)
            {
                reason = "vfx-self-contained: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogError(MarkerFail + " - " + reason);
                return false;
            }
        }

        // =====================================================================
        //  Body
        // =====================================================================
        private static bool RunCore(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- VFX SELF-CONTAINMENT (every Resources/VFX prefab, recursive deps vs gitignored art roots) ---");

            var prefabs = VfxPrefabPaths();
            if (prefabs.Count == 0)
            {
                // NOT a hollow pass: finding no prefabs is itself the failure. The curated
                // VFX tree is what the whole catalog resolves against; an empty one means
                // every catalogued effect falls back to the procedural placeholder.
                reason = "vfx-self-contained: NO prefabs found under " + VfxRoot +
                         " - the curated VFX tree is empty or missing, so there is nothing shipping " +
                         "and every catalog row resolves to nothing.";
                Debug.LogError(MarkerFail + " - " + reason);
                return false;
            }

            int totalExposed = 0;
            var exposedAssets = new List<string>();

            for (int i = 0; i < prefabs.Count; i++)
            {
                string p = prefabs[i];
                var offenders = PackDependenciesOf(p);
                log.Append(Short(p)).Append(" packDeps=").Append(offenders.Count).AppendLine();

                if (offenders.Count == 0) continue;

                totalExposed += offenders.Count;
                for (int k = 0; k < offenders.Count; k++)
                {
                    if (!exposedAssets.Contains(offenders[k])) exposedAssets.Add(offenders[k]);
                }

                var named = new StringBuilder();
                int show = Math.Min(offenders.Count, MaxNamedPerPrefab);
                for (int k = 0; k < show; k++)
                {
                    if (k > 0) named.Append(", ");
                    named.Append(offenders[k]);
                }
                if (offenders.Count > show) named.Append(" (+").Append(offenders.Count - show).Append(" more)");

                failures.Add(Short(p) + " reaches " + offenders.Count +
                             " asset(s) in a GITIGNORED art root -> missing material/texture/shader on any " +
                             "machine without the pack (magenta / untextured / invisible): " + named);
            }

            if (failures.Count > 0)
            {
                reason = "vfx-self-contained FAIL: " + failures.Count + " of " + prefabs.Count +
                         " prefab(s) still reach gitignored art (" + totalExposed + " reference(s) over " +
                         exposedAssets.Count + " distinct asset(s)). CopyAsset duplicates the PREFAB ONLY - " +
                         "run DeNelle.Editor.VfxResourceArtMirror.Run to mirror the art into " + SharedRoot +
                         " and remap the references. || " + string.Join(" | ", failures.ToArray());
                Debug.LogError(log.ToString() + MarkerFail + " - " + reason);
                return false;
            }

            reason = "vfx-self-contained OK - all " + prefabs.Count + " prefab(s) under " + VfxRoot +
                     " are genuinely self-contained: 0 recursive dependencies resolve into any of the " +
                     GitignoredArtRoots.Length + " gitignored art roots, so nothing renders magenta on a fresh clone";
            Debug.Log(log.ToString() + MarkerOk + " - " + reason);
            return true;
        }

        private static string Short(string assetPath)
        {
            return assetPath.StartsWith(VfxRoot, StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(VfxRoot.Length)
                : assetPath;
        }
    }
}
