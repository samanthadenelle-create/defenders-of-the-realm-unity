// =============================================================================
// StructureSingleton v2 - THE one authority for "there should only ever be ONE"
// (owner ruling 2026-08-01, verbatim: "HOW MANY TIMES DO i NNEED TO SAY THERE
// SHOULD ONLY EVER BE ONE, CAN WE NOT CREATE A CLASS THAT CONFIRMS SINGLETON").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Why this exists: singleton-ness was enforced PIECEMEAL - the build palette
// checked BaseLayout records, the vendor injector evicted its own strays, the
// barracks injector stood down its own baked twin - and every new building
// re-leaked the rule (two farms, two barracks, doubled vendors). This class is
// the single source of truth + the single enforcement sweep.
//
// v2 (architect review + owner ruling): a new catalog row with repo.singleton=true
// (plus repo.bakedTwins when it has a legacy baked twin) is FULLY enforced with
// ZERO code - the v1 SupplementalBaked code map is DELETED; baked twins now come
// ONLY from the catalog (RepoProps.bakedTwins, structures-catalog.json v5).
//
//   - IsSingleton(id)   - does the CATALOG flag this id repo.singleton?
//   - IsBuilt(id)       - does ANY representation exist, cheapest-first: a
//                         persisted BaseLayout record, an ACTIVE baked twin
//                         (GameObject.Find skips stood-down = inactive), a live
//                         PlacedStructure, a live Building.IsAlive carrying the
//                         id? A CatalogEntry overload memoizes per-frame (the
//                         palette polls per-card per-render).
//   - Enforce(id)       - placed/recorded instance exists -> stand down active
//                         baked twins (placed wins). NOTHING remains (post-sell)
//                         -> RESURFACE the baked twins (found INCLUDING inactive,
//                         re-skinned via HubStructureVisualInjector; the barracks
//                         twin routes through EnsureBarracksSurfaced so the
//                         WO-724 unlock gate is respected) — UNLESS the WO-834
//                         blank-town gate is closed (MayBakedTwinSurface: on a
//                         migrated save a twin surfaces only for ids in
//                         GameState.EverBuiltStructureIds), in which case the
//                         twins are actively stood DOWN (blank founding = blank).
//                         MIGRATION LATCH: a StrategicPlacementMigration-managed
//                         id SKIPS the standdown while StanddownActive is false -
//                         the bake owns that structure for this session and the
//                         ownership handover is atomic on the next hub load
//                         (WO-673 contract). Non-managed ids (barracks) enforce
//                         immediately.
//   - EnforceAll        - the generic sweep over EVERY singleton catalog row,
//                         run on each hub load by the bootstrap below.
//   - NotifyPlaced/NotifyRemoved + SingletonResolved/SingletonReleased - the
//                         event seam injectors subscribe to instead of polling
//                         (BarracksNpcInjector reseats its drillmaster on
//                         SingletonResolved("barracks", go)).
//
// Callers: BuildModeController.IsSingletonBuilt (palette "Built" card + the
// arm/place refusal) delegates to IsBuilt; BuildModeController.RemoveLayoutEntry
// calls NotifyRemoved on a sell; the DDOL bootstrap below subscribes
// BuildModeController.StructurePlaced -> NotifyPlaced and runs EnforceAll on
// every castle-hub load (after GameStateService is ready).
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>The one authority for structure singleton-ness (query + enforcement + events).</summary>
    public static class StructureSingleton
    {
        /// <summary>
        /// Raised after a <see cref="NotifyPlaced"/> enforce: (itemId, the canonical
        /// placed GameObject - may be null if the placed root could not be resolved).
        /// Subscribers (e.g. BarracksNpcInjector) react instead of polling the world.
        /// </summary>
        public static event System.Action<string, GameObject> SingletonResolved;

        /// <summary>
        /// Raised after a <see cref="NotifyRemoved"/> enforce when NOTHING of the id
        /// remains (no record, no live instance, no active baked twin).
        /// </summary>
        public static event System.Action<string> SingletonReleased;

        // Per-frame memo for the CatalogEntry IsBuilt overload - the palette polls
        // per-card per-render; the world cannot change mid-frame except through
        // Enforce, which invalidates the touched id below.
        private static readonly Dictionary<string, (int frame, bool built)> s_builtMemo =
            new Dictionary<string, (int frame, bool built)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_builtMemo.Clear();
            SingletonResolved = null;
            SingletonReleased = null;
        }

        /// <summary>True when the catalog flags <paramref name="itemId"/> repo.singleton.</summary>
        public static bool IsSingleton(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var entry = CatalogRegistry.Get(itemId);
            return entry?.repo != null && entry.repo.singleton;
        }

        /// <summary>
        /// THE truth query: does any representation of <paramref name="itemId"/> exist?
        /// Union, CHEAPEST-FIRST: persisted BaseLayout record -> ACTIVE baked twin
        /// (GameObject.Find skips inactive = stood-down bakes) -> live PlacedStructure
        /// -> live Building.IsAlive carrying the id.
        /// </summary>
        public static bool IsBuilt(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            // 1. Persisted BaseLayout records (every placement commit appends here).
            var st = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (st?.BaseLayout != null)
                for (int i = 0; i < st.BaseLayout.Count; i++)
                    if (st.BaseLayout[i].itemId == itemId)
                        return true;

            // 2. Active baked twins (catalog repo.bakedTwins; Find = name lookup, cheap).
            foreach (var bakedName in BakedTwinsOf(itemId))
                if (GameObject.Find(bakedName) != null)
                    return true;

            // 3. Live placed structures (a commit the ledger has not recorded yet / replays).
            foreach (var ps in Object.FindObjectsByType<PlacedStructure>(FindObjectsSortMode.None))
                if (ps != null && string.Equals(ps.itemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return true;

            // 4. Live behaviour components carrying the id (covers editor-tool drops that
            //    never got a PlacedStructure - the two-barracks class of leak).
            foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
                if (b != null && b.IsAlive && string.Equals(b.BuildingId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        /// <see cref="IsBuilt(string)"/> memoized per-frame per-id - the palette polls this
        /// per-card per-render (BuildModeController.IsSingletonBuilt delegates here).
        /// </summary>
        public static bool IsBuilt(CatalogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) return false;
            int frame = Time.frameCount;
            if (s_builtMemo.TryGetValue(entry.id, out var memo) && memo.frame == frame)
                return memo.built;
            bool built = IsBuilt(entry.id);
            s_builtMemo[entry.id] = (frame, built);
            return built;
        }

        // =====================================================================
        //  WO-834 — the blank-town baked-twin surface gate
        // =====================================================================

        /// <summary>
        /// WO-834 — THE pure surfacing rule: may a BAKED TWIN of <paramref name="itemId"/>
        /// stand/surface on a save with the given ever-built set + WO-673 migration marker?
        /// <list type="bullet">
        /// <item><c>strategicPlacementMigrated == false</c> → TRUE. The bake owns the town:
        /// a legacy pre-v30 save awaiting its one-shot migration, and the Default-Town
        /// FOUNDING load (WO-748 arms Default Town by CLEARING the marker so the migration
        /// writer converts the live ring — no separate founding flag exists or is needed).</item>
        /// <item>else → TRUE iff <paramref name="everBuilt"/> contains the id
        /// (OrdinalIgnoreCase). The set is MONOTONIC (selling never removes an id), which
        /// preserves the WO-819 sell→resurface contract; Default-Town/legacy saves keep
        /// surfacing because the migration writer / MigrateToV36 granted their template
        /// ids into the set.</item>
        /// </list>
        /// Pure static (no Unity/service reads) so the rule is unit-testable
        /// (BlankTownGateTests) and oracle-pinned (DataRegression.CheckBlankTownGate).
        /// </summary>
        public static bool MayBakedTwinSurface(string itemId,
            IReadOnlyList<string> everBuilt, bool strategicPlacementMigrated)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (!strategicPlacementMigrated) return true;   // bake owns the town (pre-handover / Default-Town founding load)
            if (everBuilt == null) return false;            // migrated save with nothing ever built = blank town
            for (int i = 0; i < everBuilt.Count; i++)
                if (string.Equals(everBuilt[i], itemId, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>
        /// <see cref="MayBakedTwinSurface(string, IReadOnlyList{string}, bool)"/> against the
        /// LIVE save. No save service (raw scene open / editor tools) → TRUE, preserving
        /// pre-WO-834 behaviour where no gate can be evaluated.
        /// </summary>
        public static bool MayBakedTwinSurface(string itemId)
        {
            var st = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (st == null) return true;
            return MayBakedTwinSurface(itemId, st.EverBuiltStructureIds, st.StrategicPlacementMigrated);
        }

        // Per-id outcome of one Enforce pass — tallied by EnforceAll for the WO-834
        // summary trace line (surfaced=N suppressed=M).
        private enum EnforceOutcome { None, StoodDown, LatchSkipped, Surfaced, Suppressed }

        /// <summary>
        /// THE enforcement verb for one singleton id.
        /// A placed/recorded instance exists -> stand down every ACTIVE baked twin
        /// (placed wins - the vendor-eviction rule generalized), UNLESS the WO-673
        /// migration latch says the bake still owns this structure for the session.
        /// NO representation remains -> resurface the baked twins (post-sell), UNLESS the
        /// WO-834 blank-town gate is closed (id never player-built on a migrated save) —
        /// then the twins are actively STOOD DOWN instead (they arrive ACTIVE from the
        /// scene bake, so skipping the resurface alone would leave the town furnished).
        /// Idempotent, traced, safe to call on non-singleton ids (no-op).
        /// </summary>
        public static void Enforce(string itemId)
        {
            EnforceInternal(itemId);
        }

        private static EnforceOutcome EnforceInternal(string itemId)
        {
            if (!IsSingleton(itemId)) return EnforceOutcome.None;

            EnforceOutcome outcome;
            if (HasPlacedInstance(itemId))
            {
                // MIGRATION LATCH (WO-673 contract): during the very load the migration
                // wrote its records, the bake still owns the structure - StanddownActive
                // stays false and the atomic bake->BaseLayout handover happens on the
                // NEXT hub load. Standing the twin down mid-session would double-own it.
                if (StrategicPlacementMigration.IsManagedId(itemId) &&
                    !StrategicPlacementMigration.StanddownActive)
                {
                    FlowTrace.Step("Singleton",
                        $"'{itemId}': baked-twin standdown SKIPPED - migration-managed id with StanddownActive=false " +
                        "(the bake owns this structure this session; handover is atomic on next hub load, WO-673).");
                    outcome = EnforceOutcome.LatchSkipped;
                }
                else
                {
                    StandDownBakedTwins(itemId,
                        $"a PLACED '{itemId}' owns the singleton (only ever ONE)");
                    outcome = EnforceOutcome.StoodDown;
                }
            }
            else if (MayBakedTwinSurface(itemId))
            {
                ResurfaceBakedTwins(itemId);
                outcome = EnforceOutcome.Surfaced;
            }
            else
            {
                // WO-834 blank-town gate: never player-built on this (migrated) save —
                // the baked twin may NOT stand in for it. Actively deactivate (the bake
                // ships ACTIVE), so a Build-Your-Own founding is truly blank.
                int stood = StandDownBakedTwins(itemId,
                    $"'{itemId}' never player-built on this save (blank-town gate, WO-834)");
                outcome = stood > 0 || BakedTwinsOf(itemId).Count > 0
                    ? EnforceOutcome.Suppressed : EnforceOutcome.None;
            }

            s_builtMemo.Remove(itemId);   // world changed - drop this frame's memo for the id
            return outcome;
        }

        /// <summary>
        /// The generic sweep (owner ruling): <see cref="Enforce"/> EVERY catalog row
        /// flagged singleton. Runs on each hub load via the bootstrap below - no
        /// per-building code ever again.
        /// </summary>
        public static void EnforceAll()
        {
            int rows = 0, surfaced = 0, suppressed = 0;
            foreach (var entry in CatalogRegistry.All())
            {
                if (entry?.repo == null || !entry.repo.singleton) continue;
                rows++;
                var outcome = EnforceInternal(entry.id);
                if (outcome == EnforceOutcome.Surfaced) surfaced++;
                else if (outcome == EnforceOutcome.Suppressed) suppressed++;
            }
            FlowTrace.Step("Singleton",
                $"EnforceAll: swept {rows} singleton catalog row(s) - surfaced={surfaced} suppressed={suppressed} (blank-town gate).");
        }

        /// <summary>
        /// Placement seam: the bootstrap below routes BuildModeController.StructurePlaced
        /// here. Enforces the id, then raises <see cref="SingletonResolved"/> with the
        /// canonical placed GameObject.
        /// </summary>
        public static void NotifyPlaced(string itemId, GameObject placed)
        {
            if (!IsSingleton(itemId)) return;
            Enforce(itemId);
            FlowTrace.Step("Singleton", $"NotifyPlaced('{itemId}') - enforced; raising SingletonResolved.");
            Guard.Try("Singleton", $"SingletonResolved('{itemId}') subscribers",
                () => SingletonResolved?.Invoke(itemId, placed));
        }

        /// <summary>
        /// Removal seam: BuildModeController.RemoveLayoutEntry calls this after a sell
        /// drops the persisted record. Enforces the id (resurfaces baked twins when
        /// nothing remains), then raises <see cref="SingletonReleased"/> if the id is
        /// now fully unbuilt.
        /// </summary>
        public static void NotifyRemoved(string itemId)
        {
            if (!IsSingleton(itemId)) return;
            Enforce(itemId);
            if (!IsBuilt(itemId))
            {
                FlowTrace.Step("Singleton", $"NotifyRemoved('{itemId}') - nothing remains; raising SingletonReleased.");
                Guard.Try("Singleton", $"SingletonReleased('{itemId}') subscribers",
                    () => SingletonReleased?.Invoke(itemId));
            }
        }

        // -- internals -------------------------------------------------------

        /// <summary>
        /// Deactivates every ACTIVE baked twin of <paramref name="itemId"/> (the
        /// storefront-standdown pattern - never a scene edit). <paramref name="reason"/>
        /// names WHY in the trace (placed-wins vs the WO-834 blank-town gate). Returns
        /// how many twins stood down. Idempotent, traced.
        /// </summary>
        private static int StandDownBakedTwins(string itemId, string reason)
        {
            int stood = 0;
            foreach (var bakedName in BakedTwinsOf(itemId))
            {
                var baked = GameObject.Find(bakedName);   // active-only: absent or already stood down = skip
                if (baked == null) continue;
                baked.SetActive(false);
                stood++;
                FlowTrace.Step("Singleton",
                    $"baked twin '{bakedName}' stood down - {reason}.");
            }
            return stood;
        }

        /// <summary>
        /// Post-sell resurface: nothing of <paramref name="itemId"/> remains, so its
        /// baked twins (found INCLUDING inactive) come back - the Lever-1 "baked stores
        /// pre-stand" contract applied live. The barracks twin routes through
        /// HubStructureVisualInjector.EnsureBarracksSurfaced so the WO-724 unlock gate
        /// (ff.barracks + founding-complete) is respected; every other twin re-activates
        /// and re-skins via ResurfaceStorefront (idempotent).
        /// </summary>
        private static int ResurfaceBakedTwins(string itemId)
        {
            int surfaced = 0;
            foreach (var bakedName in BakedTwinsOf(itemId))
            {
                if (bakedName == "CastleBarracks")
                {
                    // Unlock-gated: EnsureBarracksSurfaced no-ops while BarracksUnlock is
                    // locked, else reactivates + re-skins the baked building.
                    Guard.Try("Singleton", "EnsureBarracksSurfaced (singleton resurface)",
                        HubStructureVisualInjector.EnsureBarracksSurfaced);
                    FlowTrace.Step("Singleton",
                        $"resurface '{bakedName}' for '{itemId}' routed through EnsureBarracksSurfaced (unlock gate respected).");
                    surfaced++;
                    continue;
                }

                var twin = FindByNameInclInactive(bakedName);
                if (twin == null) continue;   // not in this scene bake
                if (!twin.gameObject.activeSelf)
                {
                    twin.gameObject.SetActive(true);
                    FlowTrace.Step("Singleton",
                        $"baked twin '{bakedName}' RESURFACED - no representation of '{itemId}' remains (post-sell).");
                }
                Guard.Try("Singleton", $"ResurfaceStorefront('{bakedName}')",
                    () => HubStructureVisualInjector.ResurfaceStorefront(bakedName));
                surfaced++;
            }
            return surfaced;
        }

        /// <summary>A PLACED/recorded/live instance exists: BaseLayout record, live
        /// PlacedStructure, or a live Building carrying the id (baked twins do NOT count
        /// - they are the thing being stood down/resurfaced against this answer).</summary>
        private static bool HasPlacedInstance(string itemId)
        {
            var st = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (st?.BaseLayout != null)
                for (int i = 0; i < st.BaseLayout.Count; i++)
                    if (st.BaseLayout[i].itemId == itemId)
                        return true;
            foreach (var ps in Object.FindObjectsByType<PlacedStructure>(FindObjectsSortMode.None))
                if (ps != null && string.Equals(ps.itemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
                if (b != null && b.IsAlive && string.Equals(b.BuildingId, itemId, System.StringComparison.OrdinalIgnoreCase))
                {
                    // A live Building on a baked twin root must NOT count as "placed" -
                    // exclude components sitting under a baked twin of this same id.
                    if (!IsUnderBakedTwin(b.transform, itemId))
                        return true;
                }
            return false;
        }

        /// <summary>True when <paramref name="t"/> sits on/under a baked-twin root of the id.</summary>
        private static bool IsUnderBakedTwin(Transform t, string itemId)
        {
            var twins = BakedTwinsOf(itemId);
            if (twins.Count == 0) return false;
            for (var cur = t; cur != null; cur = cur.parent)
                for (int i = 0; i < twins.Count; i++)
                    if (cur.name == twins[i]) return true;
            return false;
        }

        /// <summary>
        /// v2: the baked twins of an id come ONLY from the catalog (repo.bakedTwins,
        /// structures-catalog.json v5). Empty list when the row has none / no row.
        /// </summary>
        private static IReadOnlyList<string> BakedTwinsOf(string itemId)
        {
            var entry = CatalogRegistry.Get(itemId);
            var twins = entry?.repo?.bakedTwins;
            return twins != null ? (IReadOnlyList<string>)twins : System.Array.Empty<string>();
        }

        // Name match INCLUDING inactive (a stood-down twin is inactive, so the plain
        // GameObject.Find can't see it) - the CastleVendorNpcInjector.FindByNameInclInactive pattern.
        private static Transform FindByNameInclInactive(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }
    }

    /// <summary>
    /// Self-bootstrapping DDOL runner (mirrors <see cref="StrategicPlacementMigrationBootstrap"/> -
    /// no scene edit, CLAUDE.md par.3): on every castle-hub load, waits for GameStateService
    /// (up to 300 frames, one frame minimum so same-load Awake/Start bootstraps settle) and
    /// runs the singleton sweep. Also the ONE subscriber that routes
    /// BuildModeController.StructurePlaced into StructureSingleton.NotifyPlaced.
    /// </summary>
    internal sealed class StructureSingletonBootstrap : MonoBehaviour
    {
        private static StructureSingletonBootstrap s_instance;
        private static bool s_placedHooked;

        // WO-724 merge convention: castle-hub chrome fires on both hub scene names
        // (BarracksNpcInjector.IsCastleHubScene pattern; SceneRouter.Castle is flag-dependent).
        private static bool IsCastleHubScene(string n) =>
            n == "MainCastle_Hall" || n == "Main_Castle_Overworld";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            s_placedHooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (s_instance != null) return;
            var go = new GameObject("StructureSingletonBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<StructureSingletonBootstrap>();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
            s_instance = this;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Subscribe ONCE per domain load: every committed placement notifies the
            // singleton authority (which enforces + raises SingletonResolved).
            if (!s_placedHooked)
            {
                s_placedHooked = true;
                BuildModeController.StructurePlaced -= OnStructurePlaced;
                BuildModeController.StructurePlaced += OnStructurePlaced;
            }

            TryArm();   // the boot scene may already BE the hub
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            BuildModeController.StructurePlaced -= OnStructurePlaced;
            s_placedHooked = false;
            if (s_instance == this) s_instance = null;
        }

        private static void OnStructurePlaced(string itemId)
        {
            Guard.Try("Singleton", $"NotifyPlaced('{itemId}') from StructurePlaced", () =>
            {
                // Resolve the canonical placed root for the event payload (the commit
                // spawned it just before raising StructurePlaced). Null-tolerant.
                GameObject placed = null;
                foreach (var ps in Object.FindObjectsByType<PlacedStructure>(FindObjectsSortMode.None))
                    if (ps != null && string.Equals(ps.itemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        placed = ps.gameObject;
                        break;
                    }
                StructureSingleton.NotifyPlaced(itemId, placed);
            });
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;   // additive streams never re-sweep
            TryArm();
        }

        private void TryArm()
        {
            if (!IsCastleHubScene(SceneManager.GetActiveScene().name)) return;
            StopAllCoroutines();
            StartCoroutine(EnforceDeferred());
        }

        private IEnumerator EnforceDeferred()
        {
            // One frame so same-load Awake/Start bootstraps (GameStateService, BaseLayout
            // replay, the visual injector) settle; then a bounded wait for the save
            // service on a cold boot - IsBuilt/Enforce read BaseLayout through it.
            yield return null;
            int waited = 0;
            while ((GameStateService.Instance == null || GameStateService.Instance.State == null) && waited < 300)
            {
                waited++;
                yield return null;
            }
            if (GameStateService.Instance == null || GameStateService.Instance.State == null)
            {
                FlowTrace.Warn("Singleton",
                    "bootstrap: GameStateService never appeared (300 frames) - EnforceAll deferred to next hub load.");
                yield break;
            }
            Guard.Try("Singleton", "EnforceAll on hub load", StructureSingleton.EnforceAll);
        }
    }
}
