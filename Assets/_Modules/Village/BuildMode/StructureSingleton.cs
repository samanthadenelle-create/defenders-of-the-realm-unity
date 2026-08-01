// =============================================================================
// StructureSingleton — THE one authority for "there should only ever be ONE"
// (owner ruling 2026-08-01, verbatim: "HOW MANY TIMES DO i NNEED TO SAY THERE
// SHOULD ONLY EVER BE ONE, CAN WE NOT CREATE A CLASS THAT CONFIRMS SINGLETON").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Why this exists: singleton-ness was enforced PIECEMEAL — the build palette
// checked BaseLayout records, the vendor injector evicted its own strays, the
// barracks injector stood down its own baked twin — and every new building
// re-leaked the rule (two farms, two barracks, doubled vendors). This class is
// the single source of truth + the single enforcement sweep:
//
//   • IsSingleton(id)      — does the CATALOG flag this id repo.singleton?
//   • IsBuilt(id)          — does ANY representation exist: a persisted
//                            BaseLayout record, a live PlacedStructure, a live
//                            Building/ResourceCollector carrying the id, or an
//                            ACTIVE baked stand-in (migration table + the
//                            supplemental baked map below)?
//   • StandDownBakedTwins  — when a placed/recorded instance exists, every
//                            ACTIVE baked twin of that id deactivates (the
//                            storefront-standdown pattern — never a scene edit).
//   • EnforceAll           — the generic sweep over EVERY singleton catalog row,
//                            run automatically on each hub-scene load, so no
//                            future building ever needs its own bespoke fix.
//
// Callers: BuildModeController.IsSingletonBuilt (palette "Built" card + the
// arm/place refusal) delegates to IsBuilt; BarracksNpcInjector and any injector
// needing a twin standdown call StandDownBakedTwins; the scene-load bootstrap
// below runs EnforceAll unprompted.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>The one authority for structure singleton-ness (query + enforcement).</summary>
    public static class StructureSingleton
    {
        /// <summary>
        /// Baked stand-ins the WO-673 migration table does not cover (it only maps the
        /// storefront ring). One row per legacy baked object that REPRESENTS a catalog
        /// singleton. Extend HERE — never with a bespoke per-injector standdown again.
        /// </summary>
        private static readonly (string bakedName, string itemId)[] SupplementalBaked =
        {
            ("CastleBarracks", "barracks"),
        };

        /// <summary>True when the catalog flags <paramref name="itemId"/> repo.singleton.</summary>
        public static bool IsSingleton(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var entry = CatalogRegistry.Get(itemId);
            return entry?.repo != null && entry.repo.singleton;
        }

        /// <summary>
        /// THE truth query: does any representation of <paramref name="itemId"/> exist —
        /// persisted record, live placed structure, live behaviour component, or an
        /// ACTIVE baked stand-in? (GameObject.Find skips inactive = stood-down bakes.)
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

            // 2. Live placed structures (a commit the ledger has not recorded yet / replays).
            foreach (var ps in Object.FindObjectsByType<PlacedStructure>(FindObjectsSortMode.None))
                if (ps != null && string.Equals(ps.itemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return true;

            // 3. Live behaviour components carrying the id (covers editor-tool drops that
            //    never got a PlacedStructure — the two-barracks class of leak).
            foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
                if (b != null && b.IsAlive && string.Equals(b.BuildingId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return true;

            // 4. Active baked stand-ins (migration table + the supplemental map).
            foreach (var (bakedName, id) in BakedTwinsOf(itemId))
                if (GameObject.Find(bakedName) != null)
                    return true;

            return false;
        }

        /// <summary>
        /// Deactivates every ACTIVE baked twin of <paramref name="itemId"/> when a
        /// PLACED/recorded instance exists (placed wins — the vendor-eviction rule
        /// generalized). Returns how many twins stood down. Idempotent, traced.
        /// </summary>
        public static int StandDownBakedTwins(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            if (!HasPlacedInstance(itemId)) return 0;   // nothing placed — the bake IS the one

            int stood = 0;
            foreach (var (bakedName, id) in BakedTwinsOf(itemId))
            {
                var baked = GameObject.Find(bakedName);
                if (baked == null) continue;   // absent or already stood down
                baked.SetActive(false);
                stood++;
                FlowTrace.Step("Singleton",
                    $"baked twin '{bakedName}' stood down — a PLACED '{itemId}' owns the singleton (only ever ONE).");
            }
            return stood;
        }

        /// <summary>
        /// The generic sweep (owner ruling): for EVERY catalog row flagged singleton,
        /// stand down baked twins wherever a placed instance exists. Runs on each
        /// hub-scene load via the bootstrap below — no per-building code ever again.
        /// </summary>
        public static void EnforceAll()
        {
            int total = 0;
            foreach (var entry in CatalogRegistry.All())
            {
                if (entry?.repo == null || !entry.repo.singleton) continue;
                total += StandDownBakedTwins(entry.id);
            }
            if (total > 0)
                FlowTrace.Step("Singleton", $"EnforceAll: {total} baked twin(s) stood down this load.");
        }

        // ── internals ───────────────────────────────────────────────────────

        /// <summary>A PLACED/recorded instance exists (records, PlacedStructure, or a live
        /// Building that is NOT itself one of the known baked stand-ins).</summary>
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
            return false;
        }

        private static IEnumerable<(string bakedName, string itemId)> BakedTwinsOf(string itemId)
        {
            foreach (var (bakedName, id) in StrategicPlacementMigration.BakedStorefronts())
                if (string.Equals(id, itemId, System.StringComparison.OrdinalIgnoreCase))
                    yield return (bakedName, id);
            foreach (var (bakedName, id) in SupplementalBaked)
                if (string.Equals(id, itemId, System.StringComparison.OrdinalIgnoreCase))
                    yield return (bakedName, id);
        }
    }

    /// <summary>Runs the singleton sweep on every castle-hub load, unprompted.</summary>
    internal static class StructureSingletonBootstrap
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_hooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainCastle_Hall" && scene.name != "Main_Castle_Overworld") return;
            // Delay one frame is unnecessary: BaseLayout replay may still be seating placed
            // structures, so the 1-frame-late callers (injector polls) re-run the same sweep —
            // EnforceAll is idempotent and cheap (only singleton rows, only name lookups).
            Guard.Try("Singleton", "EnforceAll on hub load", StructureSingleton.EnforceAll);
        }
    }
}
