using System.Collections;
using System.Collections.Generic;
using DeNelle.Core.Analytics;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Adds current non-baked essentials and gate defenses after the
    /// proven default-town scene/migration pass. All pieces use normal BaseLayout.</summary>
    public sealed class StarterSettlementCompletion : MonoBehaviour
    {
        public const string SelectedKey = "founding.default_town_selected";
        public const string CompletedKey = "founding.starter_settlement_v1";

        private readonly struct Entry
        {
            public readonly string Id; public readonly Vector2Int Cell; public readonly int Yaw;
            public Entry(string id, int x, int z, int yaw = 0)
            { Id = id; Cell = new Vector2Int(x, z); Yaw = yaw; }
        }

        private static readonly Entry[] Template =
        {
            new Entry("workshop",              9, 18),
            new Entry("collector_forge",     11, 18),
            new Entry("lumberyard",           13, 18),
            new Entry("foundry",              15, 18),
            new Entry("silo",                 17, 18),
            new Entry("tower_ground_archer", 14,  5, 0),
            new Entry("tower_ground_archer",  5, 14, 1),
            new Entry("tower_ground_archer", 24, 14, 3),
            new Entry("tower_ground_archer", 14, 25, 2),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Arm()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene loaded, LoadSceneMode mode) => Install(loaded.name);

        private static void Install(string scene)
        {
            if (scene != DeNelle.Core.SceneRouter.Castle && scene != "MainCastle_Hall" &&
                scene != "Main_Castle_Overworld") return;
            if (FindAnyObjectByType<StarterSettlementCompletion>() == null)
                new GameObject("StarterSettlementCompletion").AddComponent<StarterSettlementCompletion>();
        }

        private IEnumerator Start()
        {
            float until = Time.realtimeSinceStartup + 12f;
            GameStateService svc = null;
            while (Time.realtimeSinceStartup < until)
            {
                svc = GameStateService.Instance;
                if (svc != null && svc.State != null && CatalogRegistry.Count > 0 &&
                    svc.State.StrategicPlacementMigrated) break;
                yield return null;
            }

            var state = svc != null ? svc.State : null;
            if (state == null || !Seen(state, SelectedKey) || Seen(state, CompletedKey))
            { Destroy(gameObject); yield break; }

            var grid = PlacementGrid.Instance;
            if (grid == null) grid = new GameObject("PlacementGrid").AddComponent<PlacementGrid>();
            var loader = BaseLayoutLoader.Instance != null ? BaseLayoutLoader.Instance : BaseLayoutLoader.EnsureExists();
            if (state.BaseLayout == null) state.BaseLayout = new List<PlacedStructureData>();

            int added = 0, existing = 0, failed = 0;
            for (int i = 0; i < Template.Length; i++)
            {
                Entry item = Template[i];
                if (Count(state.BaseLayout, item.Id) > OccurrenceBefore(i, item.Id))
                { existing++; continue; }

                CatalogEntry catalog = CatalogRegistry.Get(item.Id);
                if (catalog == null) { failed++; FlowTrace.Fail("Founding", $"starter id missing: {item.Id}"); continue; }
                Vector2Int footprint = grid.FootprintCells(
                    StructureFactory.MeasureClaimFootprintXZ(catalog), item.Yaw * 90f);
                if (!ResolveFreeCell(grid, item.Cell, footprint, out Vector2Int cell))
                { failed++; FlowTrace.Fail("Founding", $"no starter seat for {item.Id} near {item.Cell}"); continue; }

                var record = new PlacedStructureData(item.Id, cell.x, cell.y, item.Yaw, 1);
                state.BaseLayout.Add(record);
                state.MarkEverBuilt(item.Id);
                if (loader == null || loader.Spawn(record, grid) == null)
                {
                    state.BaseLayout.RemoveAt(state.BaseLayout.Count - 1);
                    failed++;
                    FlowTrace.Fail("Founding", $"starter spawn failed: {item.Id} at {cell}");
                    continue;
                }
                added++;
                FlowTrace.Step("Founding", $"starter placed {item.Id} at {cell}");
                yield return null;
            }

            svc.MarkTutorialSeen(CompletedKey);
            svc.Save();
            EventTracker.Track("starter_settlement_ready", new { added, existing, failed, total = Template.Length });
            FlowTrace.Step("Founding", $"starter settlement ready: added={added} existing={existing} failed={failed}");
            Destroy(gameObject);
        }

        private static bool Seen(GameState s, string key) => s.SeenTutorials != null &&
            s.SeenTutorials.TryGetValue(key, out bool value) && value;

        private static int Count(List<PlacedStructureData> layout, string id)
        { int n = 0; for (int i = 0; i < layout.Count; i++) if (layout[i].itemId == id) n++; return n; }

        private static int OccurrenceBefore(int index, string id)
        { int n = 0; for (int i = 0; i < index; i++) if (Template[i].Id == id) n++; return n; }

        public static bool ResolveFreeCell(PlacementGrid grid, Vector2Int preferred,
                                           Vector2Int footprint, out Vector2Int cell)
        {
            if (grid != null && grid.CanPlace(preferred, footprint)) { cell = preferred; return true; }
            for (int radius = 1; radius <= 6; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                var candidate = preferred + new Vector2Int(dx, dz);
                if (grid != null && grid.CanPlace(candidate, footprint)) { cell = candidate; return true; }
            }
            cell = default; return false;
        }
    }
}
