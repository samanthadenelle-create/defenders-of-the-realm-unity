// =============================================================================
// SurfaceImpactVfxRegression [surface-impact-vfx] - WO-887's surface half, pinned
// at the three places it can silently come undone.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// BACKGROUND, so the shape of the checks makes sense.
//
// WO-887's surface half was REFUSED on 2026-08-05 with measurements: no enum home
// for the five PP_* keys, demo geometry (mesh + pack material + SPHERE COLLIDER) on
// every prefab ROOT, all five emitting 5/sec ON LOOP at the derivation authority,
// and - the part that made it a design task rather than engineering debt - no
// surface taxonomy. The owner cleared both ends on 2026-08-21: she tagged all five
// surfaces herself in VfxManualPicks.json, and ruled the defaults (wall tier 1 ->
// Wood, tier 2-3 and gates -> Metal, other structures -> Stone, enemies -> Flesh,
// SAND deliberately unused).
//
// ⚠ ONE CLAUSE OF THE ORIGINAL REFUSAL WAS WRONG and is corrected at source, not
//   coded around: it read the shared physics LAYER and concluded the surface signal
//   does not exist. WallSegment.Tier has been public and 1..3 the whole time
//   (WallSegment.cs:144), named by WallTier Wood/Iron/ReinforcedSteel
//   (WallTierData.cs:29). Check (3) below is the one that would have caught that.
//
// -----------------------------------------------------------------------------
// WHAT IT MEASURES, AND HOW EACH CHECK CAN ACTUALLY GO RED.
//
//  (1) CODE AGREES WITH THE OWNER'S DATA. For each of the five surfaces it asks
//      HitSurfaceVfx.KeyFor(...) for a key and then requires that EXACT key to be
//      present as an owner row in Assets/Editor/VfxManualPicks.json, read off disk.
//      This is deliberately NOT "the constant equals the constant": one side is
//      compiled code, the other is a JSON file the owner edits through the VFX
//      Caster. Rename a constant, mistype a key, or have the owner retag a surface
//      to a different key, and the two sides part company and this goes red. It is
//      the only check here that can catch a silent un-wiring of her pick.
//
//  (2) THE SHIPPED MIRRORS ARE ACTUALLY REPAIRED. For every pair in
//      SurfaceImpactMirrorSet - the ONE declaration this suite and the builder that
//      it gates both read - it loads the mirror FROM DISK (never the
//      in-memory object the builder wrote) and requires: zero MeshFilter, zero
//      MeshRenderer, ZERO COLLIDERS anywhere, and main.loop / main.prewarm /
//      main.playOnAwake false on EVERY layer.
//      ⚠ THE SKIP RULE IS ASYMMETRIC, ON PURPOSE. The art packs are gitignored, so
//        on a fresh clone neither source nor mirror exists and the row is SKIPPED
//        AND COUNTED - a suite that goes red on a clean clone is a suite the next
//        person deletes. But when the SOURCE pack prefab IS on disk and the mirror
//        is NOT, that is a real finding on a real machine (the builder was never
//        run, or its output was never committed) and it FAILS. A plain "skip if
//        missing" would make this whole check unfailable on the machine that
//        matters, which is the failure mode the brief calls out by name.
//
//  (3) THE RESOLUTION IS EXERCISED, NOT RESTATED. It builds INACTIVE fixture
//      GameObjects carrying the real components - WallSegment at tier 1, WallSegment
//      at tier 3, Gate, Building, Enemy - and calls the production
//      HitSurfaceVfx.Resolve on each, asserting the owner's five defaults. Inactive
//      is what makes this cheap and side-effect-free: Unity never runs Awake on an
//      inactive GameObject, so no lifecycle boots (Resolve passes includeInactive
//      for exactly this reason, documented at that call). Break the wall tier
//      branch, drop the gate case, reorder walls after the structure fallback so a
//      wall reads as Stone - every one of those goes red here, and none of them is
//      visible to a check that only compares tables.
//      It also measures the SAND ruling instead of asserting a comment: no fixture
//      may resolve to Sand while HitSurfaceVfx.SandIsIntentionallyUnused holds.
//
// WHAT IT DELIBERATELY DOES NOT CHECK: the stored IsLoop flag on the catalog rows.
// That is vfx-loop-flag's job and it already audits both catalogs against the same
// shared derivation. Two oracles asserting one fact is how they come to disagree.
// This suite asserts the PREFAB is one-shot; that suite asserts the ROW agrees.
//
// Editor-only asset reads + throwaway inactive fixtures. No scene, no play mode.
// Registered in DataRegression.RunAll as [surface-impact-vfx].
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class SurfaceImpactVfxRegression
    {
        private static readonly HitSurface[] TaggedSurfaces =
        {
            HitSurface.Flesh, HitSurface.Metal, HitSurface.Stone,
            HitSurface.Wood,  HitSurface.Sand,
        };

        public static bool Run(out string reason)
        {
            var fails = new List<string>();
            var log = new StringBuilder();

            CheckOwnerTags(fails, log);
            CheckMirrors(fails, log);
            CheckResolution(fails, log);

            if (fails.Count > 0)
            {
                reason = "surface-impact-vfx FAIL (" + fails.Count + "): " +
                         string.Join(" | ", fails.ToArray());
                return false;
            }

            reason = "surface-impact-vfx OK - " + log;
            return true;
        }

        // ── (1) code key <-> owner tag ───────────────────────────────────────────

        private static void CheckOwnerTags(List<string> fails, StringBuilder log)
        {
            string picks = Path.Combine(Application.dataPath, "Editor/VfxManualPicks.json");
            if (!File.Exists(picks))
            {
                fails.Add("owner tag file missing: '" + picks + "'. Every surface key is an OWNER " +
                          "row in that file; with no file there is nothing for the code keys to agree " +
                          "with, and the check would pass vacuously.");
                return;
            }

            string json;
            try { json = File.ReadAllText(picks); }
            catch (Exception e) { fails.Add("could not read VfxManualPicks.json: " + e.Message); return; }

            int matched = 0;
            foreach (var surface in TaggedSurfaces)
            {
                string key = HitSurfaceVfx.KeyFor(surface);
                if (string.IsNullOrEmpty(key))
                {
                    fails.Add("HitSurfaceVfx.KeyFor(" + surface + ") returned nothing - a tagged " +
                              "surface with no key can never play.");
                    continue;
                }

                // The owner's rows are written by the VFX Caster as "key": "<name>".
                if (json.IndexOf("\"" + key + "\"", StringComparison.Ordinal) < 0)
                    fails.Add("HitSurfaceVfx maps " + surface + " -> '" + key + "', but no such key " +
                              "appears in Assets/Editor/VfxManualPicks.json. Either the code constant " +
                              "drifted or the owner retagged that surface; the CLI must NOT re-pick " +
                              "art to close the gap - map her tag verbatim or raise it with her.");
                else
                    matched++;
            }

            log.Append("owner tags ").Append(matched).Append('/').Append(TaggedSurfaces.Length)
               .Append(" agree with VfxManualPicks; ");
        }

        // ── (2) the shipped mirrors are repaired ─────────────────────────────────

        private static void CheckMirrors(List<string> fails, StringBuilder log)
        {
            // SurfaceImpactMirrorSet, not the builder: the builder lives in DeNelle.Editor,
            // which this assembly cannot see (one-way reference). The set is the shared
            // declaration both read, so this gate and the tool it gates cannot disagree.
            int verified = 0, skipped = 0;
            var pairs = SurfaceImpactMirrorSet.Pairs;

            foreach (var (src, dst) in pairs)
            {
                var mirror = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
                if (mirror == null)
                {
                    bool sourcePresent = AssetDatabase.LoadAssetAtPath<GameObject>(src) != null;
                    if (sourcePresent)
                        fails.Add("mirror MISSING at '" + dst + "' while its source '" + src + "' IS on " +
                                  "disk. On this machine the pack is imported, so the mirror should have " +
                                  "been built and committed - run Defenders/VFX/Mirror Surface Impact VFX. " +
                                  "(A machine with NEITHER file is a clean clone and is skipped instead.)");
                    else
                        skipped++;
                    continue;
                }

                var faults = new List<string>();

                int mf = mirror.GetComponentsInChildren<MeshFilter>(true).Length;
                int mr = mirror.GetComponentsInChildren<MeshRenderer>(true).Length;
                int co = mirror.GetComponentsInChildren<Collider>(true).Length;
                if (mf > 0) faults.Add(mf + " MeshFilter(s) - demo geometry survived the strip");
                if (mr > 0) faults.Add(mr + " MeshRenderer(s) - demo geometry survived the strip");
                if (co > 0) faults.Add(co + " Collider(s) - a pooled impact burst must carry no physics");

                var systems = mirror.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length == 0)
                    faults.Add("no ParticleSystem at all - the strip removed the effect");

                foreach (var ps in systems)
                {
                    var main = ps.main;
                    if (main.loop)
                        faults.Add("layer '" + ps.name + "' still LOOPS - played fire-and-forget it " +
                                   "would permanently burn one of the 20 global loop slots");
                    if (main.prewarm)     faults.Add("layer '" + ps.name + "' still prewarms");
                    if (main.playOnAwake) faults.Add("layer '" + ps.name + "' still plays on awake - a " +
                                                     "pooled instance would emit at the world origin");
                }

                if (faults.Count > 0)
                    fails.Add("'" + dst + "': " + string.Join("; ", faults.ToArray()));
                else
                    verified++;
            }

            log.Append("mirrors ").Append(verified).Append(" verified");
            if (skipped > 0)
                log.Append(", ").Append(skipped).Append(" skipped (pack + mirror both absent - clean clone)");
            log.Append("; ");
        }

        // ── (3) the resolution, exercised ────────────────────────────────────────

        private static void CheckResolution(List<string> fails, StringBuilder log)
        {
            var fixtures = new List<GameObject>();
            int passed = 0, cases = 0;

            try
            {
                // Owner defaults, 2026-08-21. Each fixture carries the REAL component the
                // production resolver looks for.
                cases++;
                var wood = MakeFixture("fx-wall-t1", fixtures);
                var wallLow = wood.AddComponent<WallSegment>();
                wallLow.SetTier(1);
                if (Expect(wood, HitSurface.Wood, "wall tier 1 (WallTier.Wood)", fails)) passed++;

                cases++;
                var steel = MakeFixture("fx-wall-t3", fixtures);
                var wallHigh = steel.AddComponent<WallSegment>();
                wallHigh.SetTier(3);
                if (Expect(steel, HitSurface.Metal, "wall tier 3 (WallTier.ReinforcedSteel)", fails)) passed++;

                cases++;
                var gate = MakeFixture("fx-gate", fixtures);
                gate.AddComponent<Gate>();
                if (Expect(gate, HitSurface.Metal, "gate", fails)) passed++;

                cases++;
                var building = MakeFixture("fx-building", fixtures);
                building.AddComponent<Building>();
                if (Expect(building, HitSurface.Stone, "generic structure (Building)", fails)) passed++;

                // TWO flesh fixtures, deliberately. TroopController is the CHEAP one - a
                // bare MonoBehaviour with no [RequireComponent] chain - so the Flesh branch
                // is still measured even if the Enemy fixture below ever becomes
                // troublesome to stand up. Enemy is the one that MATTERS (it is the
                // overwhelming majority of hits in play) and pulls in NavMeshAgent +
                // EnemyDamageable through RequireComponent; it stays inactive throughout, so
                // none of those lifecycles run.
                cases++;
                var troop = MakeFixture("fx-troop", fixtures);
                troop.AddComponent<TroopController>();
                if (Expect(troop, HitSurface.Flesh, "troop (living body)", fails)) passed++;

                cases++;
                var enemy = MakeFixture("fx-enemy", fixtures);
                enemy.AddComponent<Enemy>();
                if (Expect(enemy, HitSurface.Flesh, "enemy", fails)) passed++;

                // A CHILD of a wall must resolve as the wall, not as nothing: hits are
                // frequently reported on a child collider, not the structure root.
                cases++;
                var child = new GameObject("fx-wall-child");
                child.transform.SetParent(wood.transform, false);
                if (Expect(child, HitSurface.Wood, "child transform of a tier-1 wall", fails)) passed++;

                // Nothing at all -> None. Proves the resolver refuses to guess.
                cases++;
                var bare = MakeFixture("fx-bare", fixtures);
                if (Expect(bare, HitSurface.None, "bare GameObject (must not guess)", fails)) passed++;

                // The owner's SAND ruling, MEASURED: with Sand deliberately unused, no
                // fixture above may have resolved to it.
                if (HitSurfaceVfx.SandIsIntentionallyUnused)
                {
                    foreach (var go in fixtures)
                    {
                        if (HitSurfaceVfx.Resolve(go) == HitSurface.Sand)
                            fails.Add("fixture '" + go.name + "' resolved to Sand while " +
                                      "HitSurfaceVfx.SandIsIntentionallyUnused is true. The owner ruled " +
                                      "2026-08-21 that no ground-impact case is worth wiring - either " +
                                      "the resolution grew a Sand branch without a ruling, or the flag " +
                                      "should have been flipped in the same edit.");
                    }
                }
            }
            catch (Exception e)
            {
                fails.Add("resolution fixtures threw: " + e.GetType().Name + " " + e.Message);
            }
            finally
            {
                foreach (var go in fixtures)
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            log.Append("resolution ").Append(passed).Append('/').Append(cases)
               .Append(" owner-default cases; sand unused=")
               .Append(HitSurfaceVfx.SandIsIntentionallyUnused).Append("; ");
        }

        /// <summary>
        /// An INACTIVE fixture. Inactive is load-bearing: Unity does not run Awake on an
        /// inactive GameObject, so adding Enemy / Gate / WallSegment boots no lifecycle,
        /// touches no NavMesh and needs no scene.
        /// </summary>
        private static GameObject MakeFixture(string name, List<GameObject> track)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.hideFlags = HideFlags.HideAndDontSave;
            track.Add(go);
            return go;
        }

        private static bool Expect(GameObject go, HitSurface want, string what, List<string> fails)
        {
            HitSurface got = HitSurfaceVfx.Resolve(go);
            if (got == want) return true;
            fails.Add("resolution: " + what + " resolved to " + got + ", owner default is " + want +
                      " (ruling 2026-08-21). Note the ORDER dependency: WallSegment must be tested " +
                      "before the generic IDamageableStructure fallback, or every wall reads as Stone.");
            return false;
        }
    }
}
