// =============================================================================
// StrategicPlacementMigration — WO-673 L3: standdown + ONE-SHOT migration
// writer for the auto-placed functional structures. ALWAYS ON since WO-682
// removed ff.strategicplacement (owner ruling 2026-07-12).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE INJECTOR-SHAPE RULING (docs/WO673_ARCHITECTURE_REVIEW.md §3, BINDING):
// flag-gated standdown + a one-time "default layout writer" migration — NEVER a
// permanently dual-mode injector (one system that places AND one that replays
// the same id is the double-spawn factory; ONE owner per concern).
//
// WHAT THIS FILE IS:
//   1. THE MIGRATION WRITER — on the first marker-unset load of the
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
//      PER STRUCTURE (bakes): standdown applies to a baked id that HAS a migrated
//      BaseLayout record OR a structures-catalog row (the player can build it —
//      WO-682 blank-template new game: marker set + zero records still hides the
//      row-having bakes). A bake with NO catalog row AND no record keeps owning
//      its structure. RUNTIME STATIONS (apothecary / jewelers-bench): WO-703 /
//      BLANK-1 (owner ruling 2026-07-13) SUPERSEDED the "never lost" carve-out —
//      once the marker is set the station injectors stand down unconditionally
//      (fresh start = tree + well + walls/gates, nothing else); a save carrying a
//      station record still replays it via BaseLayoutLoader.
//
// SAME-LOAD ORDERING (structural double-spawn guard): during the very load the
// migration runs in, the records are brand new — the injectors already ran
// (bakes visible) and standdown must NOT flip mid-session. StanddownActive is
// therefore latched OFF for the scene-load the migration executed in (scene
// handle compare — no event-order dependence); the ownership handover happens
// atomically on the NEXT home-hub load: records replay, bakes stand down.
//
// WO-682 (owner 2026-07-12): ff.strategicplacement is REMOVED — this lane is
// ALWAYS ON. New games set the marker in ResetToNewGame (nothing to migrate =
// blank template); pre-existing saves load marker false (SaveMigrator v30) and
// migrate once on their next home-hub load.
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
            new BakedRow { bakedName = "Lumbermill_Wood_Storefront",    itemId = "collector_lumbermill" },
            new BakedRow { bakedName = "Windmill_Food_Storefront",      itemId = "collector_farm" },
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

        // ── LEVER 1 read-only census accessors (owner 2026-07-24, "stores pre-stand on a
        //    fresh hub", WWCD) ─────────────────────────────────────────────────────────
        // CastleVendorNpcInjector anchors a vendor NPC to EVERY baked storefront / runtime
        // station even when standdown deactivated it on a fresh save (nothing replayed ->
        // the old poll waited forever -> zero vendors). It reads THESE tables so the
        // role->anchor map stays single-sourced here (no duplicated list, no reflection).

        /// <summary>Read-only view of the baked storefront census (bakedName, itemId).</summary>
        public static IReadOnlyList<(string bakedName, string itemId)> BakedStorefronts()
        {
            var list = new List<(string, string)>(BakedRows.Length);
            for (int i = 0; i < BakedRows.Length; i++)
                list.Add((BakedRows[i].bakedName, BakedRows[i].itemId));
            return list;
        }

        /// <summary>Read-only view of the runtime crafting-station census
        /// (holderName, itemId, fallbackPos) — so a station's speaker NPC can be seated at
        /// its anchor even when the station injector stood down on a fresh save.</summary>
        public static IReadOnlyList<(string holderName, string itemId, Vector3 fallbackPos)> StationAnchors()
        {
            var list = new List<(string, string, Vector3)>(StationRows.Length);
            for (int i = 0; i < StationRows.Length; i++)
                list.Add((StationRows[i].holderName, StationRows[i].itemId, StationRows[i].fallbackPos));
            return list;
        }

        // Scene-load latch: the handle of the scene load the migration executed in.
        // StanddownActive stays FALSE for that load (bakes already visible; loader
        // must not replay the freshly-written records) and flips true on the next
        // home-hub load — the atomic ownership handover. int.MinValue = "never".
        private static int _migratedSceneHandle = int.MinValue;

        // ── PUBLIC GATES (the whole lane asks these) ─────────────────────────────

        /// <summary>True when the one-shot migration has run (persisted marker — set by
        /// the writer on migrated saves, or by ResetToNewGame on a blank-template new
        /// game, WO-682), and we are NOT still inside the scene-load the migration ran
        /// in. Scene-scoped to the HOME hub — the only scene BaseLayout replays in.</summary>
        public static bool StanddownActive
        {
            get
            {
                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null || !svc.State.StrategicPlacementMigrated) return false;
                var scene = SceneManager.GetActiveScene();
                if (scene.name != DeNelle.Core.SceneRouter.Castle) return false;   // records only replay here
                return scene.handle != _migratedSceneHandle;                       // not the migration load itself
            }
        }

        /// <summary>
        /// Baked-storefront standdown (HubStructureVisualInjector): true ONLY when standdown
        /// is active AND a BaseLayout RECORD will actually replace the bake named
        /// <paramref name="bakedName"/> (migrated save, or a player-built replacement; the
        /// record replays via BaseLayoutLoader). Hide it (Barracks SetActive(false) pattern)
        /// so the record's live Building takes over — no double. False → the bake KEEPS
        /// owning + rendering the structure.
        ///
        /// LEVER 1 RECONCILIATION (owner 2026-07-24, WWCD — supersedes the WO-682 blank-template
        /// carve-out for baked STOREFRONTS): the gate was `HasRecord || HasCatalogRow`, which stood
        /// a baked store DOWN the moment it had a catalog row EVEN WITH NO RECORD. On a fresh/blank
        /// save (marker set by ResetToNewGame, BaseLayout empty) every storefront has a catalog row
        /// but NO record, so all 8 hid with nothing to replay → empty grass under floating vendor
        /// NPCs (the captured on-device screenshot). The owner ruling is the opposite: on a fresh
        /// hub the baked stores PRE-STAND, VISIBLE + STAFFED (CoC). So standdown now keys on
        /// HasRecord ALONE — stand down a bake ONLY when a record genuinely replaces it. Un-built
        /// baked stores stay as the pre-stand staffed store; a player-built replacement (its record)
        /// still hides its baked original exactly as before. (Runtime STATIONS keep the WO-703
        /// unconditional standdown via StanddownActiveForStation — this change is baked-only.)
        /// </summary>
        public static bool StanddownActiveForBaked(string bakedName, out string itemId)
        {
            // WO-834 blank-town gate (second clause): on a migrated save whose player has
            // NEVER built this id (StructureSingleton.MayBakedTwinSurface reads
            // GameState.EverBuiltStructureIds), the bake stands down even with no record —
            // a Build-Your-Own founding must load truly BLANK, at scene load (no furnished
            // flash before the deferred EnforceAll sweep). Default-Town/legacy saves are
            // unaffected: their founding load has the marker false (StanddownActive false)
            // and post-migration their template grant keeps MayBakedTwinSurface true, so
            // for them this remains exactly the Lever-1 HasRecord-only rule.
            itemId = ItemIdForBaked(bakedName);
            return itemId != null && StanddownActive &&
                   (HasRecord(itemId) || !StructureSingleton.MayBakedTwinSurface(itemId));
        }

        /// <summary>
        /// Runtime-station standdown (Apothecary / Jeweler's Bench injectors): true whenever
        /// standdown is active (marker set + not the migration load). WO-703 / BLANK-1
        /// (owner ruling 2026-07-13, supersedes the "never lost" carve-out): a fresh start is
        /// the TREE, the WELL, and the WALLS (gates included) — NOTHING else, so a
        /// marker-set save with NO station record shows NO station (and, downstream, no
        /// vendor NPC — CastleVendorNpcInjector anchors to the live Building). A save that
        /// DOES carry a station record replays it through BaseLayoutLoader as before
        /// (ShouldReplayRecord keys off the same StanddownActive). The old
        /// HasRecord||HasCatalogRow qualifier kept row-less stations spawning on blank
        /// saves — that is exactly the residual the ruling stands down.
        /// </summary>
        public static bool StanddownActiveForStation(string itemId)
        {
            return StanddownActive;
        }

        /// <summary>
        /// Replay filter (BaseLayoutLoader.Rebuild): a MIGRATION-MANAGED id replays only
        /// while standdown is active (marker set + not the migration load) — otherwise
        /// the bake/injector owns that structure and replaying the record would
        /// double-spawn it. Non-managed ids (towers, walls, player-placed defenses)
        /// always replay.
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

        /// <summary>True when <paramref name="itemId"/> has a structures-catalog row —
        /// i.e. the player can build it through the palette (WO-682 standdown rule).</summary>
        private static bool HasCatalogRow(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && CatalogRegistry.Get(itemId) != null;
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
        /// when the marker is already set or we're not in the home hub.
        /// Called by the bootstrap below; public for the regression harness.
        /// </summary>
        public static void RunIfNeeded()
        {
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

            // WO-834 TEMPLATE GRANT: this save was granted the prebuilt town (a WO-748
            // Default-Town founding, or a legacy pre-v30 save migrating its auto-placed
            // town) — mark the WHOLE template as ever-built so the blank-town surface
            // gate (StructureSingleton.MayBakedTwinSurface) stays OPEN for it once the
            // marker below flips true. Deliberately includes rows the loops above
            // SKIPPED (no catalog row / absent in scene): the grant is a right of the
            // TEMPLATE, not of one bake — a skipped station's Lever-1 speaker and a
            // later-added catalog row must keep behaving as today. Plus 'barracks': the
            // WO-724 baked-barracks-at-unlock surface is part of the prebuilt town
            // (a Build-Your-Own player builds theirs from the palette instead).
            int granted = 0;
            for (int i = 0; i < BakedRows.Length; i++)
                if (state.MarkEverBuilt(BakedRows[i].itemId)) granted++;
            for (int i = 0; i < StationRows.Length; i++)
                if (state.MarkEverBuilt(StationRows[i].itemId)) granted++;
            if (state.MarkEverBuilt("barracks")) granted++;
            FlowTrace.Step("Placement",
                $"migration: default-town template grant -> {granted} id(s) marked ever-built " +
                "(WO-834 blank-town gate stays open for this save's baked pieces).");

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
    /// HOME hub loads with the marker unset. Runs a frame late (coroutine) so
    /// GameStateService / the injector-spawned stations exist first; waits briefly for
    /// the save service on a cold boot.
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
