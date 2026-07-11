// =============================================================================
// StrategicPlacementMigration — WO-673 L3: flag-gated standdown + ONE-SHOT
// migration writer for the auto-placed functional structures.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE INJECTOR-SHAPE RULING (docs/WO673_ARCHITECTURE_REVIEW.md §3, BINDING):
// flag-gated standdown + a one-time "default layout writer" migration — NEVER a
// permanently dual-mode injector (one system that places AND one that replays
// the same id is the double-spawn factory; ONE owner per concern).
//
// WHAT THIS FILE IS:
//   1. THE MIGRATION WRITER — on the first ff.strategicplacement-ON load of the
//      HOME hub (SceneRouter.Castle), each auto-placed functional structure
//      (the baked ring storefronts + the two runtime crafting stations) is
//      converted into a BaseLayout PlacedStructureData record at its CURRENT
//      position/yaw (grid-quantized: cell snap drift ≤ ~1.5m is an ACCEPTED
//      felt-pass item — architect ruling §3(a); yaw preserved as the nearest
//      90° yawStep). Then the persisted migration marker
//      (GameState.StrategicPlacementMigrated, save schema v30) is set and the
//      save written. Migration NEVER runs twice — the marker is the one-shot latch.
//   2. THE STANDDOWN ORACLE — the single place the rest of the lane asks
//      "does the bake/injector own this structure, or does BaseLayout?".
//      HubStructureVisualInjector (baked storefront SetActive(false), the
//      proven Barracks pattern), the two station injectors (skip spawn), and
//      BaseLayoutLoader (replay filter) ALL route through this class, so the
//      marker gates BOTH sides and there is never a frame where two owners
//      spawn the same id:
//        • no marker  → bakes/injectors visible, no records replayed.
//        • marker set → records replay, bakes hidden / injectors dark.
//      PER STRUCTURE: standdown only applies to an id that actually HAS a
//      migrated BaseLayout record — a structure whose catalog row was missing
//      at migration time (skipped + Warned, lanes compose at gate time) keeps
//      its bake/injector alive. Nothing is ever lost.
//
// SAME-LOAD ORDERING (structural double-spawn guard): during the very load the
// migration runs in, the records are brand new — the injectors already ran
// (bakes visible) and standdown must NOT flip mid-session. StanddownActive is
// therefore latched OFF for the scene-load the migration executed in (scene
// handle compare — no event-order dependence); the ownership handover happens
// atomically on the NEXT home-hub load: records replay, bakes stand down.
//
// FLAG OFF = today byte-identical behaviour: every public gate here returns
// "not active" and the writer never runs. Flipping the flag OFF after a
// migration ROLLS BACK cleanly: bakes/injectors resume ownership and
// BaseLayoutLoader withholds the migrated records (no double-spawn).
//
// ff.strategicplacement itself is DEFINED by the L1 data lane in FeatureFlags.cs;
// this file only REFERENCES it — and it is the ONLY file in the L3 lane that
// does, so the flag surface stays one line wide.
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
    /// <summary>
    /// WO-673 L3 — one-shot migration of the auto-placed functional structures into
    /// BaseLayout records, plus the standdown gates the injectors/loader ask.
    /// </summary>
    public static class StrategicPlacementMigration
    {
        // ── THE MIGRATION ID TABLE (census: CastleHubBuilder.cs:288-301 + the
        //    HubStructureVisualInjector Swaps rows). bakedName = the scene object
        //    the bake/injector owns today; itemId = the structures-catalog row the
        //    BaseLayout record replays through (trade convention, catalog-verified:
        //    'workshop' displayName "Forge" = the WEAPONS Blacksmith; 'forge'
        //    displayName "Armorer" = the ARMOR storefront). Jeweler_Gems_Storefront
        //    was removed from the current bake but legacy scenes may still carry
        //    it — the row is tolerated-missing-in-scene like every other row. ─────
        private struct BakedRow
        {
            public string bakedName;
            public string itemId;
        }

        private static readonly BakedRow[] BakedRows =
        {
            new BakedRow { bakedName = "Blacksmith_Weapons_Storefront", itemId = "workshop" },
            new BakedRow { bakedName = "Lumbermill_Wood_Storefront",    itemId = "lumbermill" },
            new BakedRow { bakedName = "Windmill_Food_Storefront",      itemId = "mill" },
            new BakedRow { bakedName = "EchoHollow_Pets_RoamingArea",   itemId = "pet-house" },
            new BakedRow { bakedName = "Forge_Armor_Storefront",        itemId = "forge" },
            new BakedRow { bakedName = "ArcaneTower_MagicUpgrades",     itemId = "arcane-tower" },
            new BakedRow { bakedName = "Marketplace_Monetization",      itemId = "market" },
            new BakedRow { bakedName = "Jeweler_Gems_Storefront",       itemId = "jeweler" },
        };

        // ── Runtime crafting stations (the true auto-placers, review G-C). Their
        //    holders hardcode (11,0,2)/(-11,0,2); if the holder is absent when the
        //    writer runs (injector order), the hardcoded constant is the position of
        //    record. Their catalog rows do NOT exist yet (L1 owns the catalog) — the
        //    writer tolerates the missing row (skip + Warn) so lanes compose at gate
        //    time; until a row lands, the injector keeps owning the station. ───────
        private struct StationRow
        {
            public string  holderName;
            public string  itemId;
            public Vector3 fallbackPos;
        }

        private static readonly StationRow[] StationRows =
        {
            new StationRow { holderName = "ApothecaryStation (runtime)",    itemId = "apothecary",     fallbackPos = new Vector3( 11f, 0f, 2f) },
            new StationRow { holderName = "JewelersBenchStation (runtime)", itemId = "jewelers-bench", fallbackPos = new Vector3(-11f, 0f, 2f) },
        };

        /// <summary>bakedName → catalog itemId lookup (standdown queries).</summary>
        private static string ItemIdForBaked(string bakedName)
        {
            for (int i = 0; i < BakedRows.Length; i++)
                if (BakedRows[i].bakedName == bakedName) return BakedRows[i].itemId;
            return null;
        }

        // Scene-load latch: the handle of the scene load the migration executed in.
        // StanddownActive stays FALSE for that load (bakes already visible; loader
        // must not replay the freshly-written records) and flips true on the next
        // home-hub load — the atomic ownership handover. int.MinValue = "never".
        private static int _migratedSceneHandle = int.MinValue;

        // ── PUBLIC GATES (the whole lane asks these; flag OFF → all false) ───────

        /// <summary>True when the flag is ON, the one-shot migration has run (persisted
        /// marker), and we are NOT still inside the scene-load the migration ran in.
        /// Scene-scoped to the HOME hub — the only scene BaseLayout replays in.</summary>
        public static bool StanddownActive
        {
            get
            {
                if (!DeNelle.Core.FeatureFlags.StrategicPlacement) return false;
                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null || !svc.State.StrategicPlacementMigrated) return false;
                var scene = SceneManager.GetActiveScene();
                if (scene.name != DeNelle.Core.SceneRouter.Castle) return false;   // records only replay here
                return scene.handle != _migratedSceneHandle;                       // not the migration load itself
            }
        }

        /// <summary>
        /// Baked-storefront standdown (HubStructureVisualInjector): true when the bake
        /// named <paramref name="bakedName"/> was MIGRATED (its record is in BaseLayout)
        /// and standdown is active — hide it (Barracks SetActive(false) pattern); the
        /// record replays instead. False → the bake keeps owning the structure.
        /// </summary>
        public static bool StanddownActiveForBaked(string bakedName, out string itemId)
        {
            itemId = ItemIdForBaked(bakedName);
            return itemId != null && StanddownActive && HasRecord(itemId);
        }

        /// <summary>
        /// Runtime-station standdown (Apothecary / Jeweler's Bench injectors): true when
        /// the station's id was migrated into BaseLayout and standdown is active — skip
        /// the spawn; the record replays instead. A station with no catalog row (no
        /// record written) keeps spawning via its injector — nothing vanishes.
        /// </summary>
        public static bool StanddownActiveForStation(string itemId)
        {
            return StanddownActive && HasRecord(itemId);
        }

        /// <summary>
        /// Replay filter (BaseLayoutLoader.Rebuild): a MIGRATION-MANAGED id replays only
        /// while standdown is active (marker set + flag ON + not the migration load) —
        /// otherwise the bake/injector owns that structure and replaying the record would
        /// double-spawn it (e.g. flag flipped OFF after migration = clean rollback).
        /// Non-managed ids (towers, walls, player-placed defenses) always replay.
        /// </summary>
        public static bool ShouldReplayRecord(string itemId)
        {
            if (!IsManagedId(itemId)) return true;
            return StanddownActive;
        }

        /// <summary>True when <paramref name="itemId"/> is one this migration owns.</summary>
        public static bool IsManagedId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            for (int i = 0; i < BakedRows.Length; i++)
                if (BakedRows[i].itemId == itemId) return true;
            for (int i = 0; i < StationRows.Length; i++)
                if (StationRows[i].itemId == itemId) return true;
            return false;
        }

        private static bool HasRecord(string itemId)
        {
            var svc = GameStateService.Instance;
            var layout = svc != null && svc.State != null ? svc.State.BaseLayout : null;
            if (layout == null) return false;
            for (int i = 0; i < layout.Count; i++)
                if (layout[i].itemId == itemId) return true;
            return false;
        }

        // ── THE ONE-SHOT WRITER ──────────────────────────────────────────────────

        /// <summary>
        /// Convert every auto-placed functional structure into a BaseLayout record at its
        /// current position/yaw, set the persisted marker, and save. Idempotent: no-ops
        /// when the flag is OFF, the marker is already set, or we're not in the home hub.
        /// Called by the bootstrap below; public for the regression harness.
        /// </summary>
        public static void RunIfNeeded()
        {
            if (!DeNelle.Core.FeatureFlags.StrategicPlacement) return;

            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null)
            {
                FlowTrace.Warn("Placement", "migration writer: GameStateService not ready — retry on next home-hub load.");
                return;
            }
            var state = svc.State;
            if (state.StrategicPlacementMigrated) return;   // one-shot: NEVER runs twice

            var scene = SceneManager.GetActiveScene();
            if (scene.name != DeNelle.Core.SceneRouter.Castle) return;   // home hub only

            using var _ = FlowTrace.Enter("Placement", "StrategicPlacementMigration.RunIfNeeded (one-shot writer)");

            var grid = PlacementGrid.Instance;
            if (grid == null)
                grid = new GameObject("PlacementGrid").AddComponent<PlacementGrid>();
            if (state.BaseLayout == null)
                state.BaseLayout = new List<PlacedStructureData>();

            int migrated = 0, skippedNoRow = 0, skippedAbsent = 0;

            // Baked ring storefronts — position of record = the live scene object.
            for (int i = 0; i < BakedRows.Length; i++)
            {
                var row = BakedRows[i];
                var t = FindByName(row.bakedName);
                if (t == null)
                {
                    // Not in this scene bake (e.g. jeweler was removed from the ring) — fine.
                    FlowTrace.Step("Placement",
                        $"migration: baked '{row.bakedName}' not present in scene — nothing to migrate for '{row.itemId}'.");
                    skippedAbsent++;
                    continue;
                }
                if (TryWriteRecord(state, grid, row.itemId, t.position, t.eulerAngles.y)) migrated++;
                else skippedNoRow++;
            }

            // Runtime crafting stations — live holder if spawned, else the hardcoded const.
            for (int i = 0; i < StationRows.Length; i++)
            {
                var row = StationRows[i];
                var t = FindByName(row.holderName);
                Vector3 pos = t != null ? t.position : row.fallbackPos;
                float yaw = t != null ? t.eulerAngles.y : 0f;
                if (TryWriteRecord(state, grid, row.itemId, pos, yaw)) migrated++;
                else skippedNoRow++;
            }

            // Set the one-shot marker + latch this scene load (standdown flips on the
            // NEXT home-hub load — the atomic bake→BaseLayout ownership handover), then
            // persist. The marker is set even when some rows skipped: a missing catalog
            // row keeps its bake/injector alive via the per-structure HasRecord gate, so
            // nothing is lost — and the writer stays strictly one-shot.
            state.StrategicPlacementMigrated = true;
            _migratedSceneHandle = scene.handle;
            svc.Save();

            FlowTrace.Step("Placement",
                $"migration COMPLETE: {migrated} structure(s) -> BaseLayout, {skippedNoRow} skipped (no catalog row), " +
                $"{skippedAbsent} absent in scene. Marker persisted (save v{SaveSchema.CurrentVersion}); " +
                "standdown activates on the NEXT home-hub load.");
        }

        /// <summary>
        /// Write one PlacedStructureData for <paramref name="itemId"/> at the given world
        /// pose. Tolerates a missing catalog row (skip + Warn naming the id — lanes
        /// compose at gate time) and an already-present record (idempotency belt).
        /// Grid-quantizes the position (accepted ~1.5m drift, named in the trace) and
        /// snaps yaw to the nearest 90° step.
        /// </summary>
        private static bool TryWriteRecord(GameState state, PlacementGrid grid,
            string itemId, Vector3 worldPos, float yawDeg)
        {
            if (CatalogRegistry.Get(itemId) == null)
            {
                FlowTrace.Warn("Placement",
                    $"migration: no structures-catalog row for '{itemId}' — record NOT written (bake/injector keeps " +
                    "owning it via the per-structure standdown gate; add the row and re-migrate a fresh save).");
                return false;
            }
            if (HasRecord(itemId))
            {
                FlowTrace.Step("Placement",
                    $"migration: BaseLayout already has a record for '{itemId}' — not duplicated (idempotent).");
                return false;
            }

            var cell = grid.WorldToCell(worldPos);
            Vector3 snapped = grid.CellToWorld(cell);
            float driftM = Vector2.Distance(new Vector2(worldPos.x, worldPos.z),
                                            new Vector2(snapped.x, snapped.z));
            int yawSteps = ((Mathf.RoundToInt(yawDeg / 90f)) % 4 + 4) % 4;

            state.BaseLayout.Add(new PlacedStructureData(
                itemId, cell.x, cell.y, yawSteps, level: 1,
                yawOffset: 0f, worldY: 0f, wallMounted: false));

            FlowTrace.Step("Placement",
                $"migrated {itemId} @ {worldPos} -> BaseLayout (cell {cell.x},{cell.y}, yawSteps {yawSteps}, " +
                $"snap drift {driftM:0.##}m — accepted felt-pass item).");
            return true;
        }

        // Name match across the loaded scene(s) — mirrors HubStructureVisualInjector.
        private static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>())
                if (t != null && t.name == name) return t;
            return null;
        }
    }

    /// <summary>
    /// Self-bootstrapping DDOL runner (mirrors <see cref="BaseLayoutLoaderBootstrap"/> —
    /// no scene edit, CLAUDE.md §3) that fires the one-shot migration writer when the
    /// HOME hub loads with ff.strategicplacement ON and the marker unset. Runs a frame
    /// late (coroutine) so GameStateService / the injector-spawned stations exist first;
    /// waits briefly for the save service on a cold boot. Flag OFF → constructs nothing
    /// beyond the listener and never writes.
    /// </summary>
    internal sealed class StrategicPlacementMigrationBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("StrategicPlacementMigrationBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<StrategicPlacementMigrationBootstrap>();
        }

        private void Awake()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryArm();   // the boot scene may already BE the home hub
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;   // additive streams never migrate
            TryArm();
        }

        private void TryArm()
        {
            if (!DeNelle.Core.FeatureFlags.StrategicPlacement) return;
            if (SceneManager.GetActiveScene().name != DeNelle.Core.SceneRouter.Castle) return;
            StopAllCoroutines();
            StartCoroutine(RunDeferred());
        }

        private IEnumerator RunDeferred()
        {
            // One frame so same-load Awake/Start bootstraps (GameStateService, the station
            // injectors) settle; then a short bounded wait for the save service on cold boot.
            yield return null;
            int waited = 0;
            while ((GameStateService.Instance == null || GameStateService.Instance.State == null) && waited < 300)
            {
                waited++;
                yield return null;
            }
            if (GameStateService.Instance == null || GameStateService.Instance.State == null)
            {
                FlowTrace.Warn("Placement",
                    "migration bootstrap: GameStateService never appeared (300 frames) — migration deferred to next hub load.");
                yield break;
            }
            StrategicPlacementMigration.RunIfNeeded();
        }
    }
}
