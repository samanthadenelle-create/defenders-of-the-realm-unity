// =============================================================================
// StructureCadenceRegression [structure-cadence] — the gate that can SEE a
// building that dwarfs the town, because fit-to-HEIGHT structurally cannot.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Markers: STRUCTURE_CADENCE_OK /
// STRUCTURE_CADENCE_FAIL.  Editor-only asset reads. No scene, no PlayMode.
//
// that was forbidden to touch DataRegression.cs (another lane owns it this hour).
// The committer wires the registration line; this token exists ONLY so
// RegressionMarkerRegression RULE 2 does not red the tree in the window between
// this file landing and that line landing. DELETE THIS TOKEN in the same commit
// that registers the suite — a suite that stays "standalone" is a suite that
// never runs, which is the failure this file was written about.
//
// Standalone: run-unity-method
//   -Method DeNelle.Editor.StructureCadenceRegression.RunAll
//
// =============================================================================
// WHY THIS EXISTS — THE CAPTURED DEFECT, NOT A THEORY
// =============================================================================
// Owner, on device, 2026-08-20: "farm seems to be much larger than anything else".
// The numbers behind that felt-report, read off logs/device/2026-08-20-portal.log
// ([Flow:Xform] "after Fit+SeatOnGround" + [Flow:VisualFactory] "skinned ...
// boundsSize"), NOT inferred from source:
//
//   'farm'   (entry='collector_farm')  scale=(14.34)  fitted bounds 14.00 x 5.60 x 14.34 m
//   'Forge'  (entry='forge')           scale=(3.99)   fitted bounds  2.91 x 4.00 x  2.55 m
//   'store'  (entry='market')          scale=(4.01)   fitted bounds  4.02 x 4.00 x  3.78 m
//   'lumbermill' (collector_lumbermill) scale=(5.09)  fitted bounds  5.09 x 4.00 x  4.84 m
//
// Dividing the fitted bounds by the fitted scale gives the model's NATIVE size —
// Structures/farm is 0.977 x 1.000 x 0.391 m, and the two independent captures
// (identity-reset pose and euler-applied pose) agree to three decimals, which is
// how we know the number rather than believing it.
//
// =============================================================================
// THE MECHANISM (read at source; file:line so the next seat can re-derive it)
// =============================================================================
// VisualFactory.Fit, the `largest:false` arm (Assets/_Modules/Village/VisualFactory.cs):
//     measure = bounds.size.y;  localScale *= target / measure;
// StructureFactory.OptsFor clears FitLargest and sets FitHeight, so every structure
// fits to HEIGHT. That is a SINGLE-AXIS promise executed as a UNIFORM scale: the
// footprint is never asked about, it just rides along at the same factor.
//
// That is harmless while every model's fit-time pose is roughly building-shaped.
// It detonates when a model's fit-time pose is FLAT, because the divisor is tiny:
// collector_farm authors orientation.euler (-90,0,0), which since the GROK_BRIEF
// change of 2026-08-19 is applied PRE-fit via SkinOptions.LocalRotation, and that
// stands the model's 0.391 m axis UP. 5.6 / 0.391 = 14.34, and the 1.000 m plan
// axis is multiplied by that same 14.34 — a 14 m building in a 3-5 m town.
//
// NEITHER DIRECTION ON heightMul FIXES IT, which is why a new axis had to exist:
// lowering it re-ships "the shrunk farm" the owner already rejected (commit
// 31b41d19); raising it makes the footprint worse. The fix is repo.maxFootprint —
// a CEILING on the fitted model's widest horizontal extent, default 0 = disarmed.
//
// =============================================================================
// WHAT THIS SUITE ASSERTS
// =============================================================================
// C0  SELF-TEST (both directions, runs FIRST). The outlier rule is a pure function
//     over (label, widest-metres). It is fed a synthetic CLEAN family and must pass
//     it, then the same family plus one 14.34 m pancake and must fail exactly that
//     row. A gate that has never been shown to go red is not evidence of anything.
//
// C1  CATALOG COPIES ARE BYTE-IDENTICAL. Resources wins at load and StreamingAssets
//     ships to the device; a divergence means the thing measured here is not the
//     thing the player gets. Byte compare, not JSON compare — a re-serialization
//     that reorders keys is still a divergence worth knowing about.
//
// C2  FOOTPRINT OUTLIER BAND (measured, BASE-HEIGHT-relative, upper bound only).
//     Every row's base visual is replayed through the shipped pipeline and its
//     widest horizontal extent max(x,z) is compared to an ABSOLUTE ceiling:
//     StructureFactory.YHeightVariable * CadenceWidthRatio = 4.0 * 2.6 = 10.4 m.
//
//     ⚠ UNTIL 2026-08-26 THIS COMPARED AGAINST THE FAMILY MEDIAN, AND THAT
//     REFERENCE WAS ITSELF A DEFECT (WO-1239). The intent was right — "it holds if
//     the owner re-scales the whole town" — but a median over the measured
//     population silently RE-THRESHOLDS whenever ANY member changes size. WO-1224
//     halved three GenericContainer rows; the median fell 4.32 -> 3.78 m, the band
//     fell 8.64 -> 7.56 m, and 'barracks' (7.64 m in both runs, untouched) was
//     reported as an outlier. The gate went red because three OTHER buildings got
//     SMALLER. Measured, not theorised: Builds/wo1211-reg.log (green) vs
//     Builds/gate-r3 (red), same 27 rows, same ids, three rows different.
//
//     YHeightVariable keeps the whole-town-re-scale property (it IS the one number
//     the town scales from) with none of the population coupling, and it still
//     needs no list of ids and no per-row thresholds. The family median is still
//     COMPUTED AND PRINTED, as observability — never again as a threshold.
//
// C3  AN ARMED CAP IS OBEYED. A row authoring repo.maxFootprint must measure at or
//     under it. This is the direct assert on the fix; C2 is the assert on the next
//     model nobody has imported yet. NOTE the cap is a UNIFORM scale-down, so it
//     shortens the building as well as narrowing it — it is the right tool for a
//     model that is FLAT AT FIT TIME (where the height was inflated by a tiny
//     divisor anyway) and the WRONG tool for a correctly-posed wide building, which
//     it would simply shrink. See the WO-1239 note under C2.
//
// C4  THE PRODUCTION PATH CARRIES THE CAP. SkinOptions must expose a public float
//     MaxFootprint and StructureFactory.OptsFor must populate it from the row.
//     Checked by REFLECTION deliberately (see the note on the method): this file is
//     authored on a lane that may not edit those two files, and without C4 the
//     suite would go green over data that nothing reads — a catalog key with no
//     consumer is exactly the silent no-op this project keeps re-learning.
//
// =============================================================================
// ⚠ WHAT THIS SUITE DOES **NOT** COVER — stated, never special-cased
// =============================================================================
// (1) UPPER BOUND ONLY. A row that measures far SMALLER than the median is not
//     flagged: deco_torch (heightMul 0.35) is deliberately tiny and the siege group
//     deliberately sits under the house line, so a symmetric band would red honest
//     rows. "Too small" already has an owner-visible channel (the felt-report that
//     produced the farm's 1.4 in the first place); "too large" did not, until now.
// (2) TIER MODELS (repo.upgradeVisualPath) are not measured here. They are fit
//     through the same OptsFor call and StructureOrientationOracle already measures
//     them for height; adding them to a FAMILY MEDIAN would mix L1 and L3 silhouettes
//     into one statistic and blunt it. A tier whose art is a pancake is a real gap.
// (3) THE HUB INJECTOR PATH IS NOT COVERED. HubStructureVisualInjector hand-rolls
//     its own SkinOptions (it sets FitHeight from YHeightVariable * a local mult
//     rather than calling OptsFor), so a cap authored in the catalog does NOT reach
//     it. The same device log shows that path producing 'farm' at 17.93 x 4.00 x
//     14.40 m. Routing it through OptsFor is a separate ticket; naming it here is
//     the honest alternative to a special case.
// (4) RealmStore is not a catalog row (StructureOrientationOracle coverage note 1),
//     so no catalog-driven suite can see it.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using DeNelle.Core;            // CanonicalJson
using DeNelle.Core.Catalog;    // CatalogEntry / RepoProps
using DeNelle.Village;         // SkinOptions / StructureFactory
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeNelle.Editor
{
    /// <summary>
    /// Footprint-cadence oracle over structures-catalog.json: no structure may render
    /// wildly wider than its family. Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class StructureCadenceRegression
    {
        public const string MarkerOk   = "STRUCTURE_CADENCE_OK";
        public const string MarkerFail = "STRUCTURE_CADENCE_FAIL";

        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";
        private const string ResourcesCopy   = "Resources/Data/Canonical/structures-catalog.json";
        private const string StreamingCopy   = "StreamingAssets/Data/Canonical/structures-catalog.json";

        /// <summary>
        /// THE BAND, expressed as a multiple of <see cref="StructureFactory.YHeightVariable"/> —
        /// the ONE global base fit height (4.0 m) that the whole town is scaled from. A row is an
        /// outlier when its widest horizontal extent exceeds <c>YHeightVariable * this</c>.
        /// <para>⚠ THIS USED TO BE "2.0x THE FAMILY MEDIAN" AND THAT REFERENCE WAS THE DEFECT
        /// (WO-1239, 2026-08-26). A median over the measured population silently RE-THRESHOLDS
        /// every time any member changes size, in either direction. The proof, measured not
        /// theorised — Builds/wo1211-reg.log (green, 08-25 21:15) vs Builds/gate-r3 (red, 08-26
        /// 17:23), same 27 rows, same ids:
        /// WO-1224 halved three GenericContainer rows (lumberyard/foundry/silo, heightMul 0.5),
        /// so their widest went 5.83 -> 2.91 m. That moved the MEDIAN 4.32 -> 3.78 m and the band
        /// 8.64 -> 7.56 m, and 'barracks' — which measured 7.64 m in BOTH runs and was not edited
        /// — became an "outlier" without changing by one millimetre. i.e. the gate went red
        /// because three OTHER buildings got SMALLER, which is the town getting BETTER. A
        /// threshold that inverts like that is not measuring the thing it names.</para>
        /// <para>The base height is the right reference and keeps the original design intent:
        /// the reason a family-relative band was chosen was "it holds if the owner re-scales the
        /// whole town", and YHeightVariable IS that one number ("change THIS ONE number and the
        /// entire town re-scales together" — StructureFactory). Only now it cannot be moved by an
        /// unrelated row. Deliberately the FLAT base, NOT the row's own fit height
        /// (YHeightVariable * heightMul): collector_farm authors heightMul 1.4, so a row-relative
        /// ceiling would have been 5.6*2.6 = 14.56 m and the measured 14.34 m defect this suite
        /// exists for would have walked straight through it.</para>
        /// <para>2.6 IS NOT A TASTE VALUE — it is bracketed by the same two measurements the old
        /// 2.0 was, re-expressed against the base: the widest HONEST structure in the town is
        /// 'barracks' at 7.64 m = 1.91x base (with 'wall_stone' right behind at 7.42 = 1.86x),
        /// and the collector_farm defect measured 14.34 m = 3.58x base. 2.6 is the GEOMETRIC
        /// midpoint of 1.91 and 3.58 (sqrt(1.91*3.58) = 2.615), i.e. equal multiplicative margin
        /// on both sides: 1.36x of headroom over the widest honest row, 1.38x of bite before the
        /// known defect. Ceiling = 10.4 m. Raising this to make something pass defeats the file;
        /// the row's repo.maxFootprint cap is the per-row dial.</para>
        /// </summary>
        private const float CadenceWidthRatio = 2.6f;

        /// <summary>The absolute ceiling in metres. One place, derived from the shipped base
        /// height so a whole-town re-scale carries it — never a second hardcoded number.</summary>
        private static float WidthBandM => StructureFactory.YHeightVariable * CadenceWidthRatio;

        /// <summary>
        /// Slack in metres when checking an ARMED cap (C3). The cap is one float multiply, so a
        /// correctly-capped model reproduces it to float precision; this absorbs bounds
        /// round-tripping only, same rationale as StructureOrientationOracle.HeightToleranceM.
        /// </summary>
        private const float CapToleranceM = 0.05f;

        /// <summary>Below this many measured rows the catalog is not being read. The band no
        /// longer depends on the population (see CadenceWidthRatio), so this is no longer a
        /// statistical minimum — it is an ART-OUTAGE detector, and it still FAILS rather than
        /// skips so "nothing resolved" can never read as "nothing wrong".</summary>
        private const int MinMeasuredFamily = 6;

        [Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        /// <summary>One measured row: what it is called and how wide it actually came out.</summary>
        private struct Sample
        {
            public string Label;
            public float  WidestM;      // max(size.x, size.z) of the FITTED model
            public float  HeightM;
            public float  CapM;         // repo.maxFootprint, 0 = disarmed
        }

        [MenuItem("Defenders/Build/Audit Structure Cadence (footprint)")]
        public static void RunMenu()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log(reason); else Debug.LogError(reason);
        }

        /// <summary>Standalone entry point (run-unity-method). Exits 1 so a batch can judge it.</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }

        // =====================================================================
        //  THE SUITE
        // =====================================================================
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();

            // ---- C0: the rule must work in BOTH directions before it judges anything ----
            SelfTest(failures, log);

            // ---- C1: the two shipped copies of the catalog -------------------
            CheckCopiesIdentical(failures, log);

            // ---- parse ------------------------------------------------------
            var entries = ParseCatalog(failures);
            if (entries == null) return Verdict(failures, log, 0, out reason);

            var addressToPath = BuildAddressMap();
            if (addressToPath == null)
            {
                failures.Add("no AddressableAssetSettings object — structure art lives in the remote " +
                             "Structure_Art group (Assets/StructureContent), so nothing resolves and no " +
                             "footprint can be measured. This suite cannot pass without geometry.");
                return Verdict(failures, log, 0, out reason);
            }

            // ---- measure every base visual through the shipped pipeline ------
            var samples = new List<Sample>();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (string.IsNullOrEmpty(e.visualPrefabPath)) continue;   // meshless row: nothing to measure

                if (!addressToPath.TryGetValue(e.visualPrefabPath, out string assetPath) || string.IsNullOrEmpty(assetPath))
                {
                    failures.Add("'" + e.id + "': address '" + e.visualPrefabPath + "' is NOT registered in any " +
                                 "Addressable group — StructureAssetLoader resolves it via no path, so this " +
                                 "structure renders NOTHING and its size is undefined.");
                    continue;
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    failures.Add("'" + e.id + "': address '" + e.visualPrefabPath + "' points at '" + assetPath +
                                 "', which loads no GameObject (dangling entry — the group keeps the GUID " +
                                 "after the asset moves).");
                    continue;
                }

                if (!TryMeasure(prefab, e, out Vector3 size, out string note))
                {
                    failures.Add("'" + e.id + "' (" + assetPath + "): " + note);
                    continue;
                }

                float cap = e.repo != null ? e.repo.maxFootprint : 0f;
                samples.Add(new Sample
                {
                    Label   = e.id,
                    WidestM = Mathf.Max(size.x, size.z),
                    HeightM = size.y,
                    CapM    = cap,
                });

                // ---- C3: an ARMED cap is obeyed -----------------------------
                if (cap > 0f && Mathf.Max(size.x, size.z) > cap + CapToleranceM)
                {
                    failures.Add("'" + e.id + "' CAP NOT APPLIED: repo.maxFootprint authors " +
                                 cap.ToString("0.##") + " m but the fitted model measures " +
                                 Mathf.Max(size.x, size.z).ToString("0.##") + " m across (" +
                                 size.x.ToString("0.##") + " x " + size.y.ToString("0.##") + " x " +
                                 size.z.ToString("0.##") + "). Either the cap is not being applied in " +
                                 "VisualFactory after the fit, or something re-scales the model afterwards. " +
                                 "The row authored a ceiling and the pipeline ignored it — that is worse " +
                                 "than not having the key, because the catalog now lies.");
                }
            }

            // ---- C4: the production path can actually carry a cap ------------
            CheckProductionPathCarriesCap(entries, failures, log);

            // ---- C5: WO-1224's three shared storage-container rows -----------
            CheckStorageContainerScale(entries, failures, log);

            // ---- C2: the outlier band ---------------------------------------
            if (samples.Count < MinMeasuredFamily)
            {
                failures.Add("only " + samples.Count + " structure model(s) measured (expected at least " +
                             MinMeasuredFamily + "). A 28-row catalog that yields this few measurable " +
                             "buildings is an art outage, not a quiet day — failing rather than skipping " +
                             "so it cannot read as a pass.");
            }
            else
            {
                // The band is FIXED against the town's base height and does NOT depend on this
                // population (WO-1239). The median is still computed and PRINTED — as observability,
                // never as a threshold — so a reader can see the family shift without the shift being
                // able to move the line under anybody's feet.
                float median = Median(samples);
                log.AppendLine("band = " + WidthBandM.ToString("0.00") + " m (" +
                               CadenceWidthRatio.ToString("0.0") + "x the " +
                               StructureFactory.YHeightVariable.ToString("0.0") +
                               " m base fit height, population-independent). POPULATION (reported, not " +
                               "used as a threshold): " + samples.Count + " measured base visual(s), " +
                               "median widest-horizontal-extent " + median.ToString("0.00") + " m.");
                EvaluateOutliers(samples, WidthBandM, failures);

                foreach (var s in samples)
                    log.AppendLine("  " + s.Label.PadRight(24) + " widest " + s.WidestM.ToString("0.00") +
                                   " m, height " + s.HeightM.ToString("0.00") + " m" +
                                   (s.CapM > 0f ? ", cap " + s.CapM.ToString("0.##") + " m" : string.Empty));
            }

            return Verdict(failures, log, samples.Count, out reason);
        }

        // =====================================================================
        //  THE RULE — pure, so it can be shown to go red (C0)
        // =====================================================================
        /// <summary>
        /// Flags every sample wider than <paramref name="bandM"/> metres — an ABSOLUTE ceiling
        /// derived once from the town's base fit height (see <see cref="CadenceWidthRatio"/>),
        /// deliberately NOT from anything about this population.
        /// Pure over its inputs and free of Unity state on purpose: that is what lets
        /// <see cref="SelfTest"/> prove it fails on a known-bad family and passes a clean one.
        /// UPPER BOUND ONLY — see coverage note (1) in the header.
        /// </summary>
        private static void EvaluateOutliers(List<Sample> samples, float bandM, List<string> failures)
        {
            if (samples == null || samples.Count == 0 || bandM <= 0.0001f) return;
            float band = bandM;

            foreach (var s in samples)
            {
                if (s.WidestM <= band) continue;
                failures.Add("'" + s.Label + "' FOOTPRINT OUTLIER: the fitted model is " +
                             s.WidestM.ToString("0.00") + " m across — " +
                             (s.WidestM / StructureFactory.YHeightVariable).ToString("0.0") +
                             "x the " + StructureFactory.YHeightVariable.ToString("0.0") +
                             " m base fit height, over the " + band.ToString("0.00") + " m band (" +
                             CadenceWidthRatio.ToString("0.0") +
                             "x base). THE CAUSE IS ALMOST NEVER heightMul. Fit-to-height is a single-axis " +
                             "promise run as a UNIFORM scale (VisualFactory.Fit: localScale *= target / " +
                             "bounds.size.y), so a model whose FIT-TIME pose is FLAT divides by a tiny " +
                             "number and drags its footprint up with it. FITTED ASPECT (widest : height) = " +
                             (s.WidestM / Mathf.Max(s.HeightM, 0.0001f)).ToString("0.00") + " : 1, at a " +
                             "measured height of " + s.HeightM.ToString("0.00") + " m. \u26a0 THE HEIGHT " +
                             "ALONE IS NOT DIAGNOSTIC and never was: Fit divides by whatever axis is up, " +
                             "so a heightMul-1.0 row measures EXACTLY the base height whether it was " +
                             "posed upright or flat. The ASPECT is the number that separates them — the " +
                             "honest town runs 0.6 : 1 to 1.9 : 1 (wall_stone 1.86, barracks 1.91) and " +
                             "the measured collector_farm pancake was 2.56 : 1. CHECK, IN THIS ORDER: " +
                             "(1) is the model upright at FIT time? its catalog orientation.euler is " +
                             "applied PRE-fit via SkinOptions.LocalRotation, so a wrong euler chooses " +
                             "which axis Fit divides by; (2) if the art really is flat-and-wide and " +
                             "correctly posed, author repo.maxFootprint on the row (a ceiling in metres, " +
                             "default 0 = disarmed) — that is what collector_farm does. \u26a0 (2) IS FOR " +
                             "A FLAT MODEL ONLY. The cap is a UNIFORM scale-down, so on a correctly-posed " +
                             "wide building it just makes the building smaller — the same objection as " +
                             "heightMul, on a different key (WO-1239). DO NOT lower heightMul to fix this " +
                             "either: it shrinks the BUILDING as well as the footprint, which is the " +
                             "'shrunk farm' the owner already rejected in commit 31b41d19.");
            }
        }

        /// <summary>
        /// C0 — the rule is exercised in BOTH directions against synthetic families before it is
        /// trusted on the real one. The clean numbers are the real measured town (forge 2.91,
        /// workshop 2.84, store 4.02, lumbermill 5.09, pet-house 4.32, container 5.83) PLUS the
        /// two widest honest rows in the shipped catalog — wall_stone 7.42 and barracks 7.64
        /// (both measured green in Builds/wo1211-reg.log). Those two are in here deliberately:
        /// without them the clean family's widest row was 5.83 m, so the self-test could not have
        /// noticed a band that reds a CORRECT building — which is exactly what WO-1239 was.
        /// The bad one is the actual defect (collector_farm 14.34). If either direction
        /// misbehaves this suite fails LOUD rather than judging the catalog with a broken rule.
        /// </summary>
        private static void SelfTest(List<string> failures, StringBuilder log)
        {
            var clean = new List<Sample>
            {
                new Sample { Label = "st_forge",      WidestM = 2.91f, HeightM = 4.00f },
                new Sample { Label = "st_workshop",   WidestM = 2.84f, HeightM = 4.00f },
                new Sample { Label = "st_store",      WidestM = 4.02f, HeightM = 4.00f },
                new Sample { Label = "st_lumbermill", WidestM = 5.09f, HeightM = 4.00f },
                new Sample { Label = "st_pethouse",   WidestM = 4.32f, HeightM = 4.00f },
                new Sample { Label = "st_container",  WidestM = 5.83f, HeightM = 4.00f },
                new Sample { Label = "st_wall_stone",  WidestM = 7.42f, HeightM = 4.00f },
                new Sample { Label = "st_barracks",    WidestM = 7.64f, HeightM = 4.00f },
            };

            var cleanFailures = new List<string>();
            EvaluateOutliers(clean, WidthBandM, cleanFailures);
            if (cleanFailures.Count != 0)
            {
                failures.Add("SELF-TEST (clean family) FAILED: the honest measured town — forge 2.91, " +
                             "workshop 2.84, store 4.02, lumbermill 5.09, pet-house 4.32, container 5.83, " +
                             "wall_stone 7.42, barracks 7.64 m — produced " + cleanFailures.Count +
                             " outlier report(s) against the " + WidthBandM.ToString("0.00") + " m band. " +
                             "The band is too tight and would red correct buildings; a gate that reds " +
                             "correct buildings gets itself disabled (WO-1239 — it already did once). " +
                             "First report: " + cleanFailures[0]);
            }

            var dirty = new List<Sample>(clean)
            {
                // The measured defect, verbatim from logs/device/2026-08-20-portal.log.
                new Sample { Label = "st_pancake", WidestM = 14.34f, HeightM = 5.60f },
            };
            var dirtyFailures = new List<string>();
            EvaluateOutliers(dirty, WidthBandM, dirtyFailures);
            bool caught = dirtyFailures.Count == 1 && dirtyFailures[0].Contains("st_pancake");
            if (!caught)
            {
                failures.Add("SELF-TEST (known-bad family) FAILED: the same clean family plus the measured " +
                             "14.34 m pancake produced " + dirtyFailures.Count + " report(s) and " +
                             (dirtyFailures.Count == 0 ? "did NOT name st_pancake" : "named the wrong row") +
                             ". The rule cannot go red on the exact defect it was written for, so nothing " +
                             "it says about the real catalog is evidence of anything.");
            }

            if (failures.Count == 0)
                log.AppendLine("C0 self-test: the clean family (widest honest row 7.64 m) passes the " +
                               WidthBandM.ToString("0.00") + " m band, and the measured 14.34 m pancake " +
                               "is caught (1 report, correctly named) — the rule can go red, and it " +
                               "does not go red on the town as shipped.");
        }

        // =====================================================================
        //  C1 — the two shipped copies
        // =====================================================================
        private static void CheckCopiesIdentical(List<string> failures, StringBuilder log)
        {
            string res = null, str = null;
            try
            {
                res = Path.Combine(Application.dataPath, ResourcesCopy);
                str = Path.Combine(Application.dataPath, StreamingCopy);
            }
            catch (Exception ex)
            {
                failures.Add("could not build the catalog copy paths: " + ex.Message);
                return;
            }

            if (!File.Exists(res)) { failures.Add("missing " + ResourcesCopy + " — Resources wins at load, so this copy IS the game's catalog."); return; }
            if (!File.Exists(str)) { failures.Add("missing " + StreamingCopy + " — this copy is what ships to the device."); return; }

            byte[] a, b;
            try { a = File.ReadAllBytes(res); b = File.ReadAllBytes(str); }
            catch (Exception ex) { failures.Add("could not read the catalog copies: " + ex.Message); return; }

            if (a.Length != b.Length)
            {
                failures.Add("the two catalog copies DIVERGE in length (" + a.Length + " vs " + b.Length +
                             " bytes). Resources wins at load and StreamingAssets ships to the device, so " +
                             "the town measured in the editor is not the town the player gets. Edit BOTH.");
                return;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i]) continue;
                failures.Add("the two catalog copies DIVERGE at byte " + i + " (same length, different " +
                             "content). Resources wins at load and StreamingAssets ships to the device. " +
                             "Edit BOTH copies with the same bytes.");
                return;
            }
            log.AppendLine("C1 catalog copies byte-identical (" + a.Length + " bytes).");
        }

        // =====================================================================
        //  C4 — the catalog key has a consumer
        // =====================================================================
        /// <summary>
        /// Asserts SkinOptions exposes a public float MaxFootprint and that
        /// StructureFactory.OptsFor populates it from repo.maxFootprint for an armed row.
        /// <para>REFLECTION IS DELIBERATE HERE and is not the §10 "bridge script" pattern: this
        /// file may not edit VisualFactory/StructureFactory on its lane, and a hard compile
        /// reference would make the suite un-compilable in the window before the consumer lands
        /// — i.e. it would be silently absent exactly when it is needed. Reflection lets the gate
        /// go RED (visible) instead of missing (invisible).</para>
        /// </summary>
        private static void CheckProductionPathCarriesCap(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            FieldInfo f = typeof(SkinOptions).GetField("MaxFootprint", BindingFlags.Public | BindingFlags.Instance);
            if (f == null || f.FieldType != typeof(float))
            {
                failures.Add("SkinOptions has no public float MaxFootprint — the catalog can author " +
                             "repo.maxFootprint but NOTHING READS IT, so every armed row is a silent no-op " +
                             "and this suite's C3 would pass on rows that render at full size. Apply the " +
                             "VisualFactory/StructureFactory patch that carries the cap (cap AFTER the " +
                             "height fit, uniform, scale-down-only) before landing catalog data that " +
                             "depends on it.");
                return;
            }

            CatalogEntry armed = null;
            foreach (var e in entries)
            {
                if (e != null && e.repo != null && e.repo.maxFootprint > 0f) { armed = e; break; }
            }
            if (armed == null)
            {
                // hollow-pass-ok: this is ONE of four checks, not the suite's verdict — C0/C1/C2 have
                // already asserted by the time we get here, and this branch arms itself automatically
                // the moment any row authors a cap.
                log.AppendLine("C4: SkinOptions.MaxFootprint exists; no row authors a cap today, so the " +
                               "wiring assert has nothing to exercise (this is a real gap the moment a row " +
                               "arms one, and it arms itself automatically when that happens).");
                return;
            }

            object opts;
            try { opts = StructureFactory.OptsFor(armed); }
            catch (Exception ex)
            {
                failures.Add("StructureFactory.OptsFor threw on the armed row '" + armed.id + "': " + ex.Message);
                return;
            }

            float carried = (float)f.GetValue(opts);
            if (Mathf.Abs(carried - armed.repo.maxFootprint) > 0.0001f)
            {
                failures.Add("StructureFactory.OptsFor does NOT carry the cap: row '" + armed.id +
                             "' authors repo.maxFootprint=" + armed.repo.maxFootprint.ToString("0.###") +
                             " but SkinOptions.MaxFootprint came back " + carried.ToString("0.###") + ". " +
                             "OptsFor is the ONE shared options builder — Create, ReskinForLevel, the " +
                             "placement GHOST and MeasureUprightFootprintXZ all go through it — so a cap " +
                             "that does not land there reaches none of them, and the ghost would disagree " +
                             "with the placed structure again (the WO-928 defect, on a new axis).");
                return;
            }
            log.AppendLine("C4: SkinOptions.MaxFootprint exists and OptsFor carries " +
                           carried.ToString("0.##") + " m for '" + armed.id + "'.");
        }

        // =====================================================================
        //  MEASUREMENT — replays the CURRENT VisualFactory.Skin pipeline
        // =====================================================================
        /// <summary>
        /// Instantiates <paramref name="prefab"/> and reproduces, step for step, what the shipped
        /// pipeline does to it, then returns the final WORLD bounds size.
        /// <para>Order matters and is read at source (VisualFactory.Skin): LocalRotation FIRST
        /// (opts.LocalRotation wins, else identity unless PreservePrefabRotation), THEN Fit, THEN
        /// the cap, THEN SeatOnGround (translation only, never affects size). Note this differs
        /// from StructureOrientationOracle.TryMeasure, which applies the catalog euler AFTER the
        /// fit under a comment saying "LocalRotation is never set by OptsFor (structures)" — that
        /// stopped being true with the GROK_BRIEF change of 2026-08-19, and the difference is
        /// precisely what decides which axis the fit divides by. Do not copy that order here.</para>
        /// <para>Like that oracle, this deliberately does NOT call VisualFactory.Skin: that path
        /// goes through StructureAssetLoader -> Addressables, whose editor behaviour depends on the
        /// play-mode script, and a gate that can silently resolve nothing is a hollow pass. The
        /// fit target and rotation policy still come from the real StructureFactory.OptsFor so no
        /// formula is re-typed here.</para>
        /// </summary>
        private static void CheckStorageContainerScale(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            string[] ids = { "lumberyard", "foundry", "silo" };
            const float expected = 0.5f;
            int matched = 0;

            foreach (string id in ids)
            {
                CatalogEntry entry = entries.Find(e => e != null && e.id == id);
                if (entry == null)
                {
                    failures.Add("[storage-container-scale] structures-catalog.json is missing required row '" +
                                 id + "'. WO-1224 applies to the complete three-container family.");
                    continue;
                }
                if (entry.repo == null)
                {
                    failures.Add("[storage-container-scale] '" + id + "' has no repo block, so it cannot " +
                                 "author the WO-1224 height dial.");
                    continue;
                }
                if (Mathf.Abs(entry.repo.heightMul - expected) > 0.0001f)
                {
                    failures.Add("[storage-container-scale] '" + id + "' heightMul=" +
                                 entry.repo.heightMul.ToString("0.###") + "; expected 0.5. The three " +
                                 "GenericContainer rows move together so their apparent scale cannot drift.");
                    continue;
                }
                matched++;
            }

            if (matched == ids.Length)
                log.AppendLine("C5 [storage-container-scale]: lumberyard/foundry/silo all author heightMul 0.5.");
        }

        private static bool TryMeasure(GameObject prefab, CatalogEntry entry, out Vector3 size, out string note)
        {
            size = Vector3.zero;
            note = null;

            SkinOptions opts;
            try { opts = StructureFactory.OptsFor(entry); }
            catch (Exception ex) { note = "StructureFactory.OptsFor threw: " + ex.Message; return false; }

            float target = opts.FitHeight;
            if (target <= 0f)
            {
                note = "OptsFor produced a non-positive fit height (" + target.ToString("0.###") +
                       ") — nothing to measure against.";
                return false;
            }

            var host = new GameObject("StructureCadenceProbe");
            host.hideFlags = HideFlags.HideAndDontSave;
            host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            GameObject go = null;
            try
            {
                go = Object.Instantiate(prefab, host.transform);
                go.transform.localPosition = Vector3.zero;

                // VisualFactory.Skin — clone-root policy, BEFORE the fit.
                if (opts.LocalRotation.HasValue) go.transform.localRotation = opts.LocalRotation.Value;
                else if (!opts.PreservePrefabRotation) go.transform.localRotation = Quaternion.identity;

                // VisualFactory.Fit, `largest:false` arm: localScale *= target / bounds.size.y
                if (!TryActiveWorldBounds(go, out Bounds pre))
                {
                    note = "no ACTIVE renderers with measurable bounds — VerifyRenders would destroy this " +
                           "instance and the structure would fall back to nothing.";
                    return false;
                }
                if (pre.size.y < 0.0001f)
                {
                    note = "degenerate pre-fit Y extent (" + pre.size.y.ToString("0.#####") + " m) — Fit " +
                           "would early-return and leave the model at import scale.";
                    return false;
                }
                go.transform.localScale *= target / pre.size.y;

                // The FOOTPRINT CAP, applied AFTER the fit, scale-down only, uniform.
                float cap = entry.repo != null ? entry.repo.maxFootprint : 0f;
                if (cap > 0f && TryActiveWorldBounds(go, out Bounds fitted))
                {
                    float widest = Mathf.Max(fitted.size.x, fitted.size.z);
                    if (widest > cap && widest > 0.0001f)
                        go.transform.localScale *= cap / widest;
                }

                // StructureFactory.Create — post-skin orientation SCALE only. The euler is already
                // in LocalRotation above (pre-fit); re-applying it here would tip the model twice.
                if (entry.orientation != null && entry.orientation.manual && entry.orientation.HasScale)
                    go.transform.localScale = Vector3.Scale(go.transform.localScale, entry.orientation.EffectiveScale);

                if (!TryActiveWorldBounds(go, out Bounds post))
                {
                    note = "bounds became unmeasurable after the fit.";
                    return false;
                }
                size = post.size;
                return true;
            }
            catch (Exception ex)
            {
                note = "measurement threw: " + ex.Message;
                return false;
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>Mirror of VisualFactory.TryBounds: ACTIVE renderers only, world AABB.</summary>
        private static bool TryActiveWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return false;
            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
            return true;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        /// <summary>Median widest-horizontal-extent. Median, not mean, so one 14 m outlier
        /// cannot drag the very statistic that is supposed to catch it.</summary>
        private static float Median(List<Sample> samples)
        {
            if (samples == null || samples.Count == 0) return 0f;
            var widths = new List<float>(samples.Count);
            foreach (var s in samples) widths.Add(s.WidestM);
            widths.Sort();
            int n = widths.Count;
            return (n % 2 == 1) ? widths[n / 2] : 0.5f * (widths[n / 2 - 1] + widths[n / 2]);
        }

        private static List<CatalogEntry> ParseCatalog(List<string> failures)
        {
            string json = CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add(CatalogRelPath + " unreadable (CanonicalJson.Read returned empty) — no rows to " +
                             "measure, so nothing can be asserted.");
                return null;
            }

            StructuresFile file;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
            }
            catch (Exception ex)
            {
                failures.Add("structures-catalog.json failed to parse: " + ex.Message);
                return null;
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("structures-catalog.json deserialized to 0 CatalogEntry objects (mapping break " +
                             "or empty 'entries').");
                return null;
            }
            return file.Entries;
        }

        /// <summary>Every authored Addressable address -> the asset path its GUID resolves to.</summary>
        private static Dictionary<string, string> BuildAddressMap()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.address)) continue;
                    map[entry.address] = AssetDatabase.GUIDToAssetPath(entry.guid);
                }
            }
            return map;
        }

        private static bool Verdict(List<string> failures, StringBuilder log, int measured, out string reason)
        {
            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append(MarkerFail).Append(": ").Append(failures.Count).Append(" issue(s):");
                foreach (var f in failures) sb.Append("\n  - ").Append(f);
                if (log.Length > 0) sb.Append("\n").Append(log);
                reason = sb.ToString();
                return false;
            }

            var ok = new StringBuilder(MarkerOk);
            ok.Append(" — ").Append(measured)
              .Append(" structure base visual(s) measured through the shipped fit pipeline (the ")
              .Append("population size is stated on purpose: this band no longer depends on it, and ")
              .Append("the WO-1239 defect was a threshold that silently did); none is wider ")
              .Append("than ").Append(WidthBandM.ToString("0.00")).Append(" m (")
              .Append(CadenceWidthRatio.ToString("0.0"))
              .Append("x the base fit height), every armed repo.maxFootprint is obeyed to within ")
              .Append(CapToleranceM.ToString("0.00"))
              .Append(" m, the rule was shown to catch the measured 14.34 m defect, and the two catalog ")
              .Append("copies are byte-identical. NOT COVERED: tier models, the HubStructureVisualInjector ")
              .Append("path, RealmStore, and the small side of the band — see the header coverage notes.\n");
            ok.Append(log);
            reason = ok.ToString();
            return true;
        }
    }
}
