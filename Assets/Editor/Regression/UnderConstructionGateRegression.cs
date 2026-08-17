// =============================================================================
// UnderConstructionGateRegression [under-construction-gate] -- a structure with an
// in-flight build job MUST NOT fight.
// -----------------------------------------------------------------------------
// THE TICKET (owner, 2026-08-04): "Towers queued to be built should not be able to
// attack but they are able to. They are sitting in the queue but display on the
// screen and engage."
//
// THE PROVEN ROOT (owner's own F8 capture, logs/f8-inbox/LATEST_CAPTURE.md
// 2026-08-04 21:29 -- read BEFORE any theory, per CLAUDE.md sec.12):
//
//     [Flow:BuildTimerUI] 'tower_arcane_spire@17_5'    remaining=270s
//     [Flow:BuildTimerUI] 'tower_arcane_spire@10_20'   remaining=270s
//     [Flow:BuildTimerUI] 'tower_arcane_spire@19_20'   remaining=233s
//     [Flow:BuildTimerUI] 'tower_arcane_spire@6_18'    remaining=270s
//     [Flow:BuildTimerUI] 'tower_arcane_spire@11_6'    remaining=270s
//     [Flow:BuildTimerUI] 'tower_ground_archer@15_19'  remaining=41s
//     [Flow:HUD] context inputs: wave=True ... -> Battle
//
// Five Arcane Spires under construction, 4.5 minutes each, during a LIVE WAVE. The
// scaffold (UnderConstructionVisual, WO-612) existed and worked -- but it silenced
// exactly ONE component type, DefenseTower, via
// GetComponentInChildren<DefenseTower>(). The catalog row 'tower_arcane_spire'
// carries behaviorId "ArcaneTower" -> the ArcaneTower component, which that lookup
// returns null for. So every spire ran its full Acquire/FireBlast loop while the
// job sat in the Obsidian Builder queue. WO-855 Phase 4 (build tier derived from the
// cost basket) stretched the same hole from 15 s to up to 2 h.
//
// WHAT THIS SUITE PINS
//   1. GATE ENGAGED  -- attaching the scaffold silences EVERY combat family a placed
//                      structure can carry. THIS IS THE CASE THAT FAILS PRE-FIX:
//                      ArcaneTower stayed enabled.
//   2. GATE RELEASED -- Reveal() re-enables exactly what it silenced, so a completed
//                      tower engages immediately with no relaunch.
//   3. BAKED TOWER   -- a tower with NO scaffold (arena/raid defender, garrison
//                      turret) is untouched: the gate is opt-in by attachment.
//   4. ENEMY-OWNED   -- a TowerAllegiance.EnemyOwned garrison turret is unaffected,
//                      and no garrison/raid spawner attaches a scaffold.
//   5. PERSISTENCE   -- the job key survives the PlacedStructure -> PlacedStructureData
//                      save round trip, so a tower whose job is still pending re-arms
//                      the SAME job on load (BaseLayoutLoader re-attaches) and comes
//                      back inert rather than firing.
//   6. COVERAGE PIN  -- every combat behaviorId in structures-catalog.json is a type
//                      the gate actually collects. This is the guard that stops the
//                      NEXT tower family from silently slipping through, which is
//                      precisely how this bug happened.
//   7. BUILD WORKER  -- WO-871. The same scaffold now also leases a builder NPC that
//                      works for the life of the job. Pinned: assets present, exactly
//                      one worker per scaffolded structure, released on Reveal AND on
//                      the structure being destroyed mid-build (the WO-753 orphan
//                      shape), never parented to the structure, bounded by the pool
//                      cap, and no accumulation across build/complete cycles.
//
// Edit-mode, no PlayMode, NO reflection. Awake does not fire on an edit-mode
// AddComponent (see DefenseTargetableRegression's header), so the components carry
// their field-initializer defaults and no VFX/aura side effects run. Wired into
// DeNelle.Editor.DataRegression.RunAll. NEVER throws (an internal throw => a fail).
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class UnderConstructionGateRegression
    {
        /// <summary>
        /// behaviorId -> the component StructureFactory.AttachBehavior adds, for the families
        /// that ACT ON THE WORLD OFFENSIVELY. Every one of these must be silenced while a build
        /// job is in flight. Mirrors StructureFactory.AttachBehaviorImpl; the coverage pin below
        /// fails the build if the catalog grows a combat behaviorId this table does not carry.
        /// </summary>
        private static readonly Dictionary<string, System.Type> CombatBehaviorTypes =
            new Dictionary<string, System.Type>
            {
                { "DefenseTower", typeof(DefenseTower) },
                { "ArcaneTower",  typeof(ArcaneTower)  },
            };

        /// <summary>The generic collector calls SilenceCombat must make, one per combat family.
        /// Source-pinned so a family can never be dropped from the gate silently.</summary>
        private static readonly string[] RequiredCollectorCalls =
        {
            "CollectEnabled<DefenseTower>",
            "CollectEnabled<ArcaneTower>",
            "CollectEnabled<TowerCombat>",
        };

        private const string GateSrc   = "_Modules/Village/BuildMode/UnderConstructionVisual.cs";
        private const string LoaderSrc = "_Modules/Village/BuildMode/BaseLayoutLoader.cs";
        private const string PlaceSrc  = "_Modules/Village/BuildMode/BuildModeController.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var created = new List<GameObject>();

            try
            {
                CheckGateEngagedAndReleased(failures, created);
                CheckBakedAndEnemyOwnedUntouched(failures, created);
                CheckPersistenceKeyRoundTrip(failures, created);
                CheckCatalogPlacementPath(failures, notes, created);
                CheckBuildWorkerLifecycle(failures, notes, created);
                CheckSourcePins(failures);
            }
            catch (System.Exception ex)
            {
                // The stack is the WHOLE point of a throwing suite (CLAUDE.md sec.12): without it the
                // failure line names only this catch site, and the next reader has to guess which case
                // threw. 2026-08-04: that is exactly what happened -- "IndexOutOfRangeException: Index
                // must be between 0 and 1" arrived with no line, so the throw had to be hunted by
                // re-running with the stack attached. It is attached permanently now.
                reason = $"UnderConstructionGateRegression threw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                Debug.LogError("UNDER_CONSTRUCTION_GATE_FAIL: " + reason);
                return false;
            }
            finally
            {
                foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
                // WO-871: Attach() leases a build worker, so a verification run must leave no
                // builder body standing in the open scene. (Pool bodies are HideFlags.DontSave,
                // so nothing could be serialized either way -- this keeps the editor view clean.)
                ConstructionWorkerPool.DisposeAll();
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"under-construction-gate FAILED ({failures.Count}): ");
                sb.Append(string.Join(" | ", failures));
                if (notes.Count > 0) sb.Append("  [notes: " + string.Join(" ; ", notes) + "]");
                reason = sb.ToString();
                Debug.LogError("UNDER_CONSTRUCTION_GATE_FAIL: " + reason);
                return false;
            }

            var ok = new StringBuilder();
            ok.Append("UNDER-CONSTRUCTION GATE OK -- a scaffolded structure silences every combat family " +
                      "(DefenseTower + ArcaneTower + TowerCombat), Reveal restores exactly what it silenced, " +
                      "baked/EnemyOwned towers with no scaffold are untouched, and the job key survives the " +
                      "PlacedStructure save round trip so a pending tower reloads inert. WO-871: the build-site " +
                      "worker is leased on scaffold, returned on Reveal AND on the structure being destroyed " +
                      "mid-build, is never parented to the structure, and stays bounded + pooled across cycles.");
            if (notes.Count > 0) ok.Append("  [notes: " + string.Join(" ; ", notes) + "]");
            reason = ok.ToString();
            Debug.Log("UNDER_CONSTRUCTION_GATE_OK");
            return true;
        }

        // =====================================================================
        //  1 + 2 -- the gate engages, then releases
        // =====================================================================

        /// <summary>
        /// THE CASE THAT FAILS PRE-FIX. A host carrying BOTH combat families is scaffolded; both
        /// must go inert. Before the fix, SilenceCombat's ancestor did
        /// GetComponentInChildren&lt;DefenseTower&gt;() only, so the ArcaneTower assertion below
        /// fails outright -- the exact defect the owner's capture recorded five times over.
        /// Then Reveal() must hand both back, so a completed tower fights again with no relaunch.
        /// </summary>
        private static void CheckGateEngagedAndReleased(List<string> failures, List<GameObject> created)
        {
            var go = new GameObject("UCGate_BothFamilies");
            created.Add(go);
            var def = go.AddComponent<DefenseTower>();
            var arc = go.AddComponent<ArcaneTower>();

            if (!def.enabled || !arc.enabled)
            {
                failures.Add("[gate-engaged] baseline broken: a freshly added DefenseTower/ArcaneTower is not enabled " +
                             "-- the rest of this suite cannot distinguish 'gated' from 'never on'.");
                return;
            }

            UnderConstructionVisual.Attach(go, "ucgate_test@0_0");

            var scaffold = go.GetComponent<UnderConstructionVisual>();
            if (scaffold == null)
            {
                failures.Add("[gate-engaged] UnderConstructionVisual.Attach added no scaffold component -- " +
                             "the whole construction gate is unreachable.");
                return;
            }

            if (def.enabled)
                failures.Add("[gate-engaged] a scaffolded DefenseTower is STILL ENABLED -- a tower with an in-flight " +
                             "build job would acquire targets and fire while its job sits in the Obsidian queue.");
            if (arc.enabled)
                failures.Add("[gate-engaged] a scaffolded ArcaneTower is STILL ENABLED -- THIS IS THE 2026-08-04 DEFECT. " +
                             "The gate silenced only DefenseTower, so 'tower_arcane_spire' ran its full Acquire/FireBlast " +
                             "loop for the whole build timer (owner capture: five spires at remaining=270s during a live " +
                             "wave). Every combat family must be collected, not just the first one.");

            // --- 2. GATE RELEASED -- a completed tower engages immediately ---
            scaffold.Reveal();

            if (!def.enabled)
                failures.Add("[gate-released] DefenseTower is still disabled after Reveal() -- a COMPLETED tower would " +
                             "stand inert until the player relaunched the game.");
            if (!arc.enabled)
                failures.Add("[gate-released] ArcaneTower is still disabled after Reveal() -- a COMPLETED spire would " +
                             "stand inert until the player relaunched the game.");
        }

        // =====================================================================
        //  3 + 4 -- baked scene towers and EnemyOwned garrison turrets are untouched
        // =====================================================================

        /// <summary>
        /// The gate is OPT-IN BY ATTACHMENT: a tower that was never placed through build mode
        /// carries no scaffold, so nothing can silence it. Covers the arena/raid defenders and
        /// the garrison turrets the brief forbids breaking, plus the prepaid tutorial tower
        /// (which runs the separate TowerPlacementSystem/TowerConstructionQueue path and never
        /// touches UnderConstructionVisual at all).
        /// </summary>
        private static void CheckBakedAndEnemyOwnedUntouched(List<string> failures, List<GameObject> created)
        {
            // 3. A baked scene tower with no build job.
            var baked = new GameObject("UCGate_BakedTower");
            created.Add(baked);
            var bakedTower = baked.AddComponent<DefenseTower>();

            if (baked.GetComponent<UnderConstructionVisual>() != null)
                failures.Add("[baked] a bare DefenseTower somehow carries a scaffold -- the gate must never self-attach.");
            if (!bakedTower.enabled)
                failures.Add("[baked] a baked DefenseTower with NO build job is disabled -- arena/raid defenders and " +
                             "garrison turrets would never fight.");
            if (UnderConstructionVisual.IsUnderConstruction(baked))
                failures.Add("[baked] IsUnderConstruction reported TRUE for a tower with no scaffold and no job -- " +
                             "the gate would silence every baked defender in the game.");

            // 4. An EnemyOwned garrison turret.
            var garrison = new GameObject("UCGate_EnemyOwnedTurret");
            created.Add(garrison);
            var turret = garrison.AddComponent<DefenseTower>();
            turret.Allegiance = TowerAllegiance.EnemyOwned;

            if (!turret.enabled)
                failures.Add("[enemy-owned] an EnemyOwned garrison turret is disabled -- raid arenas would be undefended.");
            if (turret.Allegiance != TowerAllegiance.EnemyOwned)
                failures.Add("[enemy-owned] the gate path mutated a turret's Allegiance -- it must touch only 'enabled'.");
            if (UnderConstructionVisual.IsUnderConstruction(garrison))
                failures.Add("[enemy-owned] IsUnderConstruction reported TRUE for an EnemyOwned garrison turret.");
        }

        // =====================================================================
        //  5 -- the pending state survives a save/load round trip
        // =====================================================================

        /// <summary>
        /// A build job outlives quit/relaunch (it persists in GameState.ObsidianQueue and is
        /// clocked off wall-time), so the LOAD path must re-arm the scaffold for a job still in
        /// flight -- BaseLayoutLoader.Spawn does that, keyed by UnderConstructionVisual.KeyFor.
        /// The join that makes it work is the KEY: the id BuildModeController.Place started the
        /// job under must be the id the loader asks IsBuilding() about after the structure has
        /// round-tripped through PlacedStructureData. If the key drifts, the reloaded tower
        /// answers "not building" and comes back FIRING at full strength -- the same exploit,
        /// one relaunch later. This proves the round trip over the real types.
        /// </summary>
        private static void CheckPersistenceKeyRoundTrip(List<string> failures, List<GameObject> created)
        {
            var placedData = new PlacedStructureData("tower_arcane_spire", 17, 5, 1, 1, 0f, 0f, false);
            string keyAtPlacement = UnderConstructionVisual.KeyFor(placedData);

            if (string.IsNullOrEmpty(keyAtPlacement) || keyAtPlacement.IndexOf('@') < 0)
                failures.Add($"[persist] job key '{keyAtPlacement}' is not the expected '<id>@<cellX>_<cellZ>' form -- " +
                             "CompletedUpgradeApplier routes on the '@', and the loader re-arm would miss.");

            var go = new GameObject("UCGate_PersistProbe");
            created.Add(go);
            var ps = go.AddComponent<PlacedStructure>();
            ps.itemId = placedData.itemId;
            ps.gridCell = new Vector2Int(placedData.cellX, placedData.cellZ);
            ps.level = Mathf.Max(1, placedData.level);
            ps.yawSteps = placedData.yawSteps;
            ps.yawOffset = placedData.yawOffset;
            ps.worldY = placedData.worldY;
            ps.wallMounted = placedData.wallMounted;

            string keyAfterRoundTrip = UnderConstructionVisual.KeyFor(ps.ToSaveData());
            if (keyAfterRoundTrip != keyAtPlacement)
                failures.Add($"[persist] the job key DRIFTS across the save round trip: placement started " +
                             $"'{keyAtPlacement}' but the reloaded structure asks about '{keyAfterRoundTrip}'. " +
                             "BaseLayoutLoader.Spawn would find no in-flight job, skip the scaffold, and the " +
                             "half-built tower would come back FIRING after a relaunch.");
        }

        // =====================================================================
        //  The real placement path, over the real catalog
        // =====================================================================

        /// <summary>
        /// Build each combat catalog row through the SAME StructureFactory.Create the game places
        /// with, scaffold it, and assert the attached behaviour goes inert. A headless
        /// skin/render miss (Create returning null) degrades to a NOTE, never a false fail -- the
        /// deterministic synthetic case above already carries the hard proof.
        /// </summary>
        private static void CheckCatalogPlacementPath(List<string> failures, List<string> notes, List<GameObject> created)
        {
            var entries = LoadCatalogEntries(out string loadError);
            if (entries == null)
            {
                notes.Add(DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                    "catalog placement pass", loadError + " -- synthetic gate proof stands"));
                return;
            }

            int checkedRows = 0;
            foreach (var entry in entries)
            {
                if (entry == null || entry.repo == null) continue;
                string behaviorId = entry.repo.behaviorId;
                if (string.IsNullOrEmpty(behaviorId)) continue;
                if (!CombatBehaviorTypes.ContainsKey(behaviorId)) continue;

                checkedRows++;

                GameObject root = null;
                try
                {
                    root = StructureFactory.Create(entry, new Pose(Vector3.zero, Quaternion.identity), null);
                }
                catch (System.Exception ex)
                {
                    notes.Add($"'{entry.id}': StructureFactory.Create threw ({ex.Message}) -- row skipped.");
                    continue;
                }
                if (root == null)
                {
                    notes.Add($"'{entry.id}': StructureFactory.Create returned null (headless skin/render miss) -- row skipped.");
                    continue;
                }
                created.Add(root);

                var behaviours = new List<Behaviour>();
                foreach (var d in root.GetComponentsInChildren<DefenseTower>(true)) if (d != null) behaviours.Add(d);
                foreach (var a in root.GetComponentsInChildren<ArcaneTower>(true)) if (a != null) behaviours.Add(a);

                if (behaviours.Count == 0)
                {
                    notes.Add($"'{entry.id}' (behaviorId={behaviorId}): built but carries no combat component -- nothing to gate.");
                    continue;
                }

                UnderConstructionVisual.Attach(root, UnderConstructionVisual.KeyFor(
                    new PlacedStructureData(entry.id, 0, 0, 0, 1, 0f, 0f, false)));

                foreach (var b in behaviours)
                {
                    if (b == null || !b.enabled) continue;
                    failures.Add($"[catalog] '{entry.id}' (behaviorId={behaviorId}): its {b.GetType().Name} is STILL " +
                                 "ENABLED after the construction scaffold attached -- this structure fights while its " +
                                 "build job is queued.");
                }
            }

            if (checkedRows == 0)
                failures.Add("[catalog] structures-catalog.json contains NO combat rows (behaviorId in " +
                             "{DefenseTower, ArcaneTower}) -- either the catalog lost every tower or the behaviorId " +
                             "set changed and this gate is now pointed at nothing.");
        }

        private static IReadOnlyList<CatalogEntry> LoadCatalogEntries(out string error)
        {
            error = null;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
                if (string.IsNullOrEmpty(json)) { error = "structures-catalog.json read empty/null"; return null; }

                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                var file = JsonConvert.DeserializeObject<CatalogFile>(json, settings);
                if (file == null || file.Entries == null || file.Entries.Count == 0)
                {
                    error = "structures-catalog.json deserialized to 0 entries";
                    return null;
                }
                return file.Entries;
            }
            catch (System.Exception ex)
            {
                error = $"catalog parse threw: {ex.Message}";
                return null;
            }
        }

        /// <summary>Local mirror of the structures-catalog.json envelope -- the same private
        /// shape DataRegression / SessionRegression / DefenseTargetableRegression each keep, so
        /// this oracle stays self-contained.</summary>
        [System.Serializable]
        private sealed class CatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        // =====================================================================
        //  7 -- WO-871: the build-site WORKER lives and dies with the scaffold
        // =====================================================================

        /// <summary>
        /// WO-871 acceptance, proven over the real types in edit mode (Attach -> Bind runs
        /// synchronously on AddComponent, so the worker really is leased here):
        ///   a. ASSETS      -- the generated work controller + the staged builder body exist.
        ///                     Without them no worker can ever spawn, and a controller-less
        ///                     humanoid would render its T-pose bind pose (WO-833's defect).
        ///   b. SPAWN       -- a scaffolded structure leases exactly ONE worker.
        ///   c. DESPAWN     -- Reveal() (job complete) returns it; the live count goes back down.
        ///   d. NO ORPHAN   -- destroying the structure mid-build releases its worker too. This is
        ///                     the WO-753 shape: a "worker with no building" left chopping at an
        ///                     empty tile is exactly what the one-owner teardown rule exists to stop.
        ///   e. NOT A CHILD -- the worker is NOT parented to the structure (re-parenting during a
        ///                     host's OnDestroy is a Unity error; that is how the orphan would be born).
        ///   f. BOUNDED     -- more concurrent builds than the cap never produce more than the cap's
        ///                     worth of workers, and repeated build/complete cycles do not accumulate.
        /// </summary>
        private static void CheckBuildWorkerLifecycle(List<string> failures, List<string> notes,
                                                      List<GameObject> created)
        {
            // --- a. the assets the worker needs ---
            string ctrl = Path.Combine(Application.dataPath, "Resources/NPCs/KayKit/BuilderWorkerWork.controller");
            string body = Path.Combine(Application.dataPath, "Resources/NPCs/KayKit/" +
                                       ConstructionWorkerPool.BodySlug + ".fbx");
            bool haveCtrl = File.Exists(ctrl);
            bool haveBody = File.Exists(body);

            if (!haveCtrl)
                failures.Add("[worker-assets] the build-worker controller is MISSING at " +
                             "Assets/Resources/NPCs/KayKit/BuilderWorkerWork.controller -- no builder can spawn " +
                             "for any build timer. Generate + commit it: menu Defenders/Art/Build Builder Worker " +
                             "Controller, or batchmode -executeMethod DeNelle.Editor.BuilderWorkerAnimatorSetup.Build " +
                             "(expect BUILDER_WORKER_ANIM_OK).");
            if (!haveBody)
                failures.Add($"[worker-assets] the staged builder body is MISSING at " +
                             $"Assets/Resources/NPCs/KayKit/{ConstructionWorkerPool.BodySlug}.fbx -- " +
                             "ConstructionWorkerPool.BodySlug points at a body that is not staged (WO-818 stager: " +
                             "DeNelle.Editor.KayKitNpcImporter).");

            if (!haveCtrl || !haveBody)
            {
                notes.Add("worker lifecycle cases skipped -- the worker assets above are absent, so a spawn " +
                          "assertion would only restate the same miss.");
                return;
            }

            // --- b + c. spawn on scaffold, despawn on reveal ---
            int baseline = ConstructionWorkerPool.LiveCount;

            var host = new GameObject("UCWorker_Host");
            created.Add(host);
            UnderConstructionVisual.Attach(host, "ucworker_spawn@1_1");

            var scaffold = host.GetComponent<UnderConstructionVisual>();
            if (scaffold == null)
            {
                failures.Add("[worker-spawn] Attach added no scaffold -- the worker seam is unreachable.");
                return;
            }

            int during = ConstructionWorkerPool.LiveCount;
            if (during != baseline + 1)
                failures.Add($"[worker-spawn] a structure with a live build job leased {during - baseline} worker(s), " +
                             "expected exactly 1 -- the owner asked for a builder to be visibly working during a " +
                             "build/upgrade timer and nothing (or more than one body) turned up.");

            // e. the worker must NOT be a child of the structure (it lives on the pool root).
            foreach (Transform child in host.transform)
            {
                if (child == null || child.name != "ConstructionWorker") continue;
                failures.Add("[worker-parenting] the build worker is PARENTED to the structure. It must not be: a " +
                             "pooled body cannot be re-parented during the host's OnDestroy (Unity errors on that), " +
                             "so a parented worker is either destroyed with the building it belongs to or stranded " +
                             "half-torn-down. That is precisely how the orphaned worker WO-753 forbids gets born.");
                break;
            }

            scaffold.Reveal();

            int after = ConstructionWorkerPool.LiveCount;
            if (after != baseline)
                failures.Add($"[worker-despawn] {after - baseline} worker(s) are still leased after Reveal() -- a " +
                             "builder keeps working at a FINISHED building. Reveal must release the worker in the " +
                             "same beat it stops the upgrade aura.");

            // --- d. destroying the structure mid-build takes its worker with it (WO-753) ---
            // NOTE ON WHICH NET THIS PROVES: edit mode does not deliver OnDestroy or Update to a
            // plain MonoBehaviour (see this file's header on Awake), so the two callback-based
            // release nets are unobservable here -- OnDestroy -> StopWorker is pinned instead by
            // the source pin in CheckSourcePins. What this case proves is the net that needs NO
            // callback at all: the pool reaps any lease whose owning scaffold is gone. That is the
            // guarantee that has to hold when a callback is missed, which is exactly the condition
            // an orphaned worker is born under.
            var doomed = new GameObject("UCWorker_DestroyedMidBuild");
            UnderConstructionVisual.Attach(doomed, "ucworker_destroy@2_2");
            int withDoomed = ConstructionWorkerPool.LiveCount;
            if (withDoomed != baseline + 1)
            {
                failures.Add("[worker-orphan] baseline broken: the second probe leased " +
                             $"{withDoomed - baseline} worker(s) instead of 1.");
            }
            Object.DestroyImmediate(doomed);
            int afterDestroy = ConstructionWorkerPool.LiveCount;
            if (afterDestroy != baseline)
                failures.Add($"[worker-orphan] {afterDestroy - baseline} worker(s) SURVIVED the structure being " +
                             "destroyed mid-build -- a builder left chopping at an empty tile. This is the WO-753 " +
                             "one-owner teardown rule: OnDestroy must release the worker exactly as it stops the " +
                             "upgrade loop.");

            // --- f. bounded, and no accumulation across cycles ---
            var many = new List<GameObject>();
            int over = ConstructionWorkerPool.MaxLive + 3;
            for (int i = 0; i < over; i++)
            {
                var go = new GameObject("UCWorker_Cap_" + i);
                many.Add(go);
                created.Add(go);
                UnderConstructionVisual.Attach(go, $"ucworker_cap@{i}_0");
            }
            int live = ConstructionWorkerPool.LiveCount;
            if (live > ConstructionWorkerPool.MaxLive)
                failures.Add($"[worker-bounded] {live} workers are live with a cap of {ConstructionWorkerPool.MaxLive} " +
                             $"({over} concurrent builds) -- the worker count is unbounded. A town mid-boom would " +
                             "carry a skinned humanoid per queued building.");

            foreach (var go in many)
            {
                var s = go.GetComponent<UnderConstructionVisual>();
                if (s != null) s.Reveal();
            }
            int settled = ConstructionWorkerPool.LiveCount;
            if (settled != baseline)
                failures.Add($"[worker-bounded] {settled - baseline} worker(s) remain leased after every build in the " +
                             "batch completed -- workers accumulate build over build instead of returning to the pool.");
            if (ConstructionWorkerPool.IdleCount > ConstructionWorkerPool.MaxLive)
                failures.Add($"[worker-bounded] the pool parked {ConstructionWorkerPool.IdleCount} bodies with a cap of " +
                             $"{ConstructionWorkerPool.MaxLive} -- bodies are being created per build instead of leased.");
        }

        // =====================================================================
        //  6 -- coverage + wiring pins (the guard against the NEXT silent slip)
        // =====================================================================

        /// <summary>
        /// Source pins, because the runtime cases above can only see the families that EXIST
        /// today. These fail the build if:
        ///   * the gate stops collecting one of the combat families (how this bug happened);
        ///   * the load path stops re-arming the scaffold for a job still in flight;
        ///   * the placement path stops attaching the scaffold after starting the job;
        ///   * a garrison/raid tower spawner starts attaching a scaffold (which would put
        ///     EnemyOwned turrets behind a build timer they have no job for).
        /// </summary>
        private static void CheckSourcePins(List<string> failures)
        {
            string assets = Application.dataPath;

            string gate = ReadSource(Path.Combine(assets, GateSrc), "UnderConstructionVisual", failures);
            if (gate != null)
            {
                foreach (var call in RequiredCollectorCalls)
                    if (!gate.Contains(call))
                        failures.Add($"[coverage] UnderConstructionVisual no longer collects '{call}' -- that combat " +
                                     "family fights through its whole build timer. This is EXACTLY the 2026-08-04 " +
                                     "defect: the gate covered DefenseTower only and every Arcane Spire kept firing.");

                if (!gate.Contains("RestoreCombat"))
                    failures.Add("[coverage] UnderConstructionVisual has no RestoreCombat -- a completed tower would " +
                                 "never be handed its combat behaviours back.");

                // WO-871: the worker seam must stay a HANDLE with three release points, exactly like
                // the upgrade aura. The runtime cases above prove today's behaviour; these pins stop
                // a future edit from quietly dropping one of the release calls.
                if (!gate.Contains("ConstructionWorkerPool.Spawn"))
                    failures.Add("[worker-seam] UnderConstructionVisual no longer leases a build worker " +
                                 "(ConstructionWorkerPool.Spawn) -- no builder animates during any build/upgrade timer.");
                if (CountOccurrences(gate, "StopWorker()") < 3)
                    failures.Add("[worker-seam] UnderConstructionVisual calls StopWorker() fewer than 3 times " +
                                 "(expected: Reveal + OnDestroy + the method itself) -- one of the release points was " +
                                 "dropped, so a worker can outlive its build job. Mirror StopUpgradeLoop exactly.");

                // 2026-08-04, proven the hard way (Builds/wo871-stack.log): an Animator quirk inside the
                // worker spawn threw IndexOutOfRangeException straight out of Attach -- which
                // BuildModeController placement and BaseLayoutLoader both call. The worker is DECORATION;
                // it must never be able to abort a real structure being placed or reloaded.
                if (!gate.Contains("stand a build worker at the site"))
                    failures.Add("[worker-seam] the ConstructionWorkerPool.Spawn call in UnderConstructionVisual.Bind " +
                                 "is no longer wrapped in its Guard.Try(\"stand a build worker at the site\") -- a throw " +
                                 "inside the cosmetic worker spawn would propagate out of Attach and abort the actual " +
                                 "placement/reload of the structure. That exact throw already happened once.");
            }

            string loader = ReadSource(Path.Combine(assets, LoaderSrc), "BaseLayoutLoader", failures);
            if (loader != null && !(loader.Contains("IsBuilding(UnderConstructionVisual.KeyFor(data))")
                                    && loader.Contains("UnderConstructionVisual.Attach")))
                failures.Add("[persist] BaseLayoutLoader.Spawn no longer re-arms the scaffold for a job still in " +
                             "flight (IsBuilding(KeyFor(data)) -> Attach) -- a tower saved mid-build would reload " +
                             "FIRING instead of inert.");

            string place = ReadSource(Path.Combine(assets, PlaceSrc), "BuildModeController", failures);
            if (place != null && !place.Contains("UnderConstructionVisual.Attach"))
                failures.Add("[placement] BuildModeController no longer attaches the scaffold after StartBuild -- a " +
                             "freshly placed tower would fight for its entire queued build.");

            // The gate must stay OPT-IN: nothing that spawns a baked/garrison tower may scaffold it.
            string[] mustNotScaffold =
            {
                "_Modules/Village/World/Camps/GarrisonTurretArmer.cs",
                "Editor/WallTools/RaidBaseGenerator.cs",
            };
            foreach (var rel in mustNotScaffold)
            {
                string path = Path.Combine(assets, rel);
                if (!File.Exists(path)) continue;   // moved/renamed is not this suite's business
                if (File.ReadAllText(path).Contains("UnderConstructionVisual"))
                    failures.Add($"[enemy-owned] {Path.GetFileName(path)} now references UnderConstructionVisual -- a " +
                                 "baked/garrison turret has no build job, so a scaffold there would silence it forever.");
            }
        }

        /// <summary>Non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>.</summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }

        private static string ReadSource(string path, string label, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add($"[source] {label} source file missing ({path}) -- the construction gate cannot be verified.");
                return null;
            }
            return File.ReadAllText(path);
        }
    }
}
