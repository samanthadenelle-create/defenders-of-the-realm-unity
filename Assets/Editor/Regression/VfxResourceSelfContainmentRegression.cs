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
using DeNelle.Core.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class VfxResourceSelfContainmentRegression
    {
        private const string FlowSys = "VfxSelfContain";

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
            DeNelle.Core.AssetRoots.StructureContent + "/",              // .gitignore:121
            "Assets/Lana Studio/Casual RPG VFX/Upgrade for URP/", // .gitignore:312 (subfolder only)
        };

        private const string MarkerOk   = "VFX_SELF_CONTAINED_OK";
        private const string MarkerFail = "VFX_SELF_CONTAINED_FAIL";

        // How many offending paths to name per prefab before summarising. Naming
        // them is the point -- a count with no path is not actionable.
        private const int MaxNamedPerPrefab = 6;

        // =====================================================================
        //  KNOWN CATALOG EXPOSURE - a DATED, RATCHETED baseline (2026-08-09)
        // =====================================================================
        // Extending this oracle to ScriptableObjects (audit F1/F2/F38) surfaced a
        // PRE-EXISTING P0 that the prefab-only scan could never see: the two VFX
        // catalogs resolve 679 references over 675 distinct assets into gitignored
        // art roots. On a fresh clone every PlayKey(...) row backed by those entries
        // resolves to NOTHING.
        //
        // WHY THIS IS A BASELINE AND NOT AN IMMEDIATE FAIL. The obvious remedy -
        // "run VfxResourceArtMirror" - is WRONG here and would be an expensive
        // mistake. The 08-06 mirror pulled 73 loose materials/textures (~23.85 MB).
        // These 675 are largely whole PACK PREFABS ("Flash 1 nature arrow.prefab"),
        // so mirroring them would import a large slice of a DELIBERATELY gitignored
        // pack into git, against the standing big-art-out-of-git policy (owner
        // ruling 2026-07-15: two machines, no CI, no fresh clones - the policy holds).
        // The likely-correct fix is to TRIM the catalogs to the rows gameplay can
        // actually reach (the audit measured 26 of 79 enum values with ZERO gameplay
        // callers) and mirror only that reachable subset. That is an owner-scoped
        // decision about content, not a gate action - so the gate RECORDS the debt
        // exactly and refuses to let it GROW, rather than pretending it is absent or
        // blocking every other lane until it is resolved.
        //
        // THE RATCHET, deliberately three-sided:
        //   * an asset NOT in this baseline that reaches gitignored art  -> HARD FAIL
        //   * a baseline entry whose exposure COUNT GROWS                -> HARD FAIL
        //   * a baseline entry that has become CLEAN                     -> FAIL, refresh me
        // The third is not pedantry: a baseline that silently keeps passing after the
        // debt is paid is how a stale allowlist comes to protect code that no longer
        // exists (see UiObsidianConformanceRegression's dead PauseHudBootstrap entry,
        // audit finding G10).
        private static readonly Dictionary<string, int> KnownCatalogExposure =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                // 645 -> 689 (2026-08-14, owner directive: "i want high end much better vfx
                // prefabs that actually look good" for the portals). The catalog was regenerated
                // after tagging 'Portal_Threshold_Aura' -> Mirza Beig Ultimate VFX
                // 'pf_vfx-ult_demo_psys_loop_portalBlue' in VfxManualPicks.json; the regen also
                // picked up 'Posion_Cast', whose Spells Pack prefab now resolves on this machine.
                // Two rows, +44 transitively-referenced pack assets (a pack prefab drags its own
                // materials/textures).
                //
                // DELIBERATE, and it takes the SAME shape as the 645 already recorded here: this
                // file's own ruling above is that mirroring pack prefabs into git is the WRONG
                // remedy (big-art-out-of-git, owner 2026-07-15), and .gitignore says so verbatim
                // for these packs - "only the keys we map in HovlVfxCatalog are referenced by
                // path; re-import on fresh clone". So the debt is RECORDED, not hidden: on a
                // machine without the Mirza pack imported, the portal threshold vortex resolves
                // to nothing and the portals fall back to their procedural glow - which is why
                // both portal call sites emit a FlowTrace line naming that exact cause instead
                // of failing silently.
                { "Assets/Resources/VFX/HovlVfxCatalog.asset", 689 },
                { "Assets/Resources/VFX/VFXCatalog.asset",      34 },
            };

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

        /// <summary>
        /// The TRACKED SCRIPTABLEOBJECTS under the VFX root - the catalogs (HovlVfxCatalog.asset,
        /// VFXCatalog.asset) that map a PlayKey string to a prefab.
        ///
        /// WHY THIS EXISTS (2026-08-09 audit, finding F1/F2/F38). VfxPrefabPaths asks
        /// AssetDatabase for "t:Prefab" and then filters on ".prefab", so a ScriptableObject
        /// was STRUCTURALLY INVISIBLE to this oracle. The gate built to prove "nothing reaches
        /// gitignored art" was scanning only half the tree, and the half it skipped is the half
        /// that holds the references: HovlVfxCatalog.asset alone resolved 100 of its 110
        /// distinct GUIDs into Hovl Studio / UnityTechnologies / Spells Pack.
        ///
        /// The failure mode is the SAME one the prefab half exists to stop, and it is worse:
        /// a catalog that cannot resolve does not render a magenta particle, it renders
        /// NOTHING - every PlayKey(...) effect (tower projectiles, casts, impacts, Heal_Aura,
        /// upgrade fireworks) silently resolves null on a fresh clone.
        ///
        /// This is the audit's headline pattern in one line: the gate asserted the PREFABS
        /// were clean, never that the CATALOGS were - it proved the part that was never broken.
        /// </summary>
        public static List<string> VfxCatalogAssetPaths()
        {
            var paths = new List<string>();
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { VfxRoot.TrimEnd('/') });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsInVfxRoot(p) && p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && !paths.Contains(p))
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

            using var _scope = FlowTrace.Enter(FlowSys, "VfxSelfContainment.RunCore");

            List<string> prefabs;
            using (FlowTrace.Enter(FlowSys, "scan prefabs (t:Prefab)"))
            {
                prefabs = VfxPrefabPaths();
                FlowTrace.Step(FlowSys, "prefabs found=" + prefabs.Count + " under " + VfxRoot);
            }

            // F1/F2/F38: the catalogs were never scanned. Absence here is NOT a pass - a run
            // that finds zero catalogs is a run that proved nothing about them, and it says so.
            List<string> catalogs;
            using (FlowTrace.Enter(FlowSys, "scan catalogs (t:ScriptableObject)"))
            {
                catalogs = VfxCatalogAssetPaths();
                if (catalogs.Count == 0)
                    FlowTrace.Warn(FlowSys, "NO ScriptableObject catalogs found under " + VfxRoot +
                                            " - the catalog half of this oracle asserted nothing this run");
                else
                    FlowTrace.Step(FlowSys, "catalogs found=" + catalogs.Count + " (these were INVISIBLE to this gate before 2026-08-09)");
            }

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

            // Prefabs AND catalogs walk the SAME dependency rule. Two derivations of one rule
            // is how a tool and its gate come to disagree while both report success (the
            // standing lesson in this file's header), so the scan list is unified rather than
            // forked into a second loop with its own copy of the logic.
            var scanned = new List<string>(prefabs);
            scanned.AddRange(catalogs);

            using var _walk = FlowTrace.Enter(FlowSys, "walk deps of " + scanned.Count +
                                                       " asset(s) (" + prefabs.Count + " prefab, " + catalogs.Count + " catalog)");

            for (int i = 0; i < scanned.Count; i++)
            {
                string p = scanned[i];
                var offenders = PackDependenciesOf(p);
                log.Append(Short(p)).Append(" packDeps=").Append(offenders.Count).AppendLine();

                // --- the three-sided ratchet (see KnownCatalogExposure) -------------
                int baselined;
                bool isBaselined = KnownCatalogExposure.TryGetValue(p, out baselined);

                if (offenders.Count == 0)
                {
                    if (isBaselined)
                    {
                        // Side 3: the debt was PAID. Refuse to keep passing on a stale entry.
                        FlowTrace.Warn(FlowSys, "baseline entry is now CLEAN, refresh required: " + Short(p));
                        failures.Add(Short(p) + " is now CLEAN (baseline expected " + baselined +
                                     " exposed asset(s)). The debt was paid - REMOVE this entry from " +
                                     "KnownCatalogExposure so the ratchet keeps its teeth. A baseline that " +
                                     "passes after the fix is how a stale allowlist comes to guard nothing.");
                    }
                    continue;
                }

                if (isBaselined && offenders.Count <= baselined)
                {
                    // Side 0: known, dated, not growing. Recorded loudly, not failed.
                    FlowTrace.Step(FlowSys, "baselined exposure " + Short(p) + " = " + offenders.Count +
                                            "/" + baselined + " (known debt, not growing)");
                    log.Append("  BASELINED ").Append(Short(p)).Append(' ')
                       .Append(offenders.Count).Append('/').Append(baselined).AppendLine();
                    continue;
                }

                if (isBaselined)
                {
                    // Side 2: the debt GREW.
                    FlowTrace.Fail(FlowSys, "baselined exposure GREW: " + Short(p) + " " +
                                            baselined + " -> " + offenders.Count);
                    failures.Add(Short(p) + " exposure GREW from a baselined " + baselined + " to " +
                                 offenders.Count + " asset(s) in gitignored art roots. The ratchet only " +
                                 "ever moves down; something added new pack references to a catalog that " +
                                 "was already the single largest exposure in the project.");
                    continue;
                }

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
                FlowTrace.Fail(FlowSys, "exposed=" + failures.Count + "/" + scanned.Count +
                                        " asset(s) reach gitignored art; refs=" + totalExposed +
                                        " distinct=" + exposedAssets.Count);
                reason = "vfx-self-contained FAIL: " + failures.Count + " of " + scanned.Count +
                         " asset(s) still reach gitignored art (" + totalExposed + " reference(s) over " +
                         exposedAssets.Count + " distinct asset(s)). CopyAsset duplicates the PREFAB ONLY - " +
                         "run DeNelle.Editor.VfxResourceArtMirror.Run to mirror the art into " + SharedRoot +
                         " and remap the references. || " + string.Join(" | ", failures.ToArray());
                Debug.LogError(log.ToString() + MarkerFail + " - " + reason);
                return false;
            }

            // HONESTY: a pass here does NOT mean zero exposure while the baseline is non-empty.
            // Stating "0 dependencies resolve into gitignored roots" with 679 baselined
            // references live would be the exact dishonest-gate pattern this oracle exists to
            // kill - a marker that proves the part that was never broken. The pass line
            // therefore always carries the outstanding debt.
            int baselinedAssets = 0, baselinedRefs = 0;
            foreach (var kv in KnownCatalogExposure)
            {
                if (!scanned.Contains(kv.Key)) continue;
                baselinedAssets++;
                baselinedRefs += kv.Value;
            }

            if (baselinedAssets > 0)
            {
                FlowTrace.Step(FlowSys, "PASS with debt: " + baselinedAssets + " baselined asset(s), " +
                                        baselinedRefs + " reference(s) still in gitignored art");
                reason = "vfx-self-contained OK (WITH RECORDED DEBT) - " + prefabs.Count +
                         " prefab(s) are genuinely self-contained, and " + baselinedAssets +
                         " catalog(s) carry a DATED, RATCHETED exposure of " + baselinedRefs +
                         " reference(s) into gitignored art roots that is pinned and NOT GROWING. " +
                         "This is NOT zero exposure: on a machine without the packs those rows still " +
                         "resolve to nothing. Remedy is to TRIM the catalogs to gameplay-reachable rows " +
                         "and mirror only that subset - not a blanket VfxResourceArtMirror run, which " +
                         "would import a gitignored pack into git. Baseline: KnownCatalogExposure.";
            }
            else
            {
                FlowTrace.Step(FlowSys, "clean: 0 of " + scanned.Count + " asset(s) reach a gitignored art root");
                reason = "vfx-self-contained OK - all " + scanned.Count + " asset(s) under " + VfxRoot +
                         " (" + prefabs.Count + " prefab, " + catalogs.Count + " catalog) are genuinely " +
                         "self-contained: 0 recursive dependencies resolve into any of the " +
                         GitignoredArtRoots.Length + " gitignored art roots, so nothing renders magenta " +
                         "on a fresh clone AND no PlayKey row resolves null";
            }
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
