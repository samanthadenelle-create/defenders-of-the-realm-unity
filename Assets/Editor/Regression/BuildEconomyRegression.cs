// =============================================================================
// BuildEconomyRegression — headless oracle for the BUILD MODE + BUILD ECONOMY
// data spine (structures-catalog.json → CatalogRegistry → StructureFactory /
// BuildModeController cost boundary → PlacementGrid math → BaseLayout replay →
// BuildTimerConfig (WO-172/612) → damage-states.json (WO-672 D) → the
// CatalogBootstrap.RegisterFallback ⇄ catalog parity gate on the JSON-failure path).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Style/contract mirrors the
// other Run(out reason) oracles wired into DataRegression.RunAll:
//   public static bool Run(out string reason)
//   markers: BUILDECON_OK (Debug.Log) / BUILDECON_FAIL (Debug.LogError → lands
//   in break-log.jsonl per docs/INSTRUMENTATION_STANDARD.md §4/§5).
//
// Data + logic ONLY — no scene loads, no play mode. Real objects in, real
// response out: costs resolve through the REAL BuildModeController.CostFor /
// UpgradeCostFor / RefundCostFor (the actual charge/refund boundary), placement
// math through a REAL PlacementGrid instance, the replay probe through the REAL
// StructureFactory.Create (the ONE creation path).
//
// DELIBERATELY NOT DUPLICATED HERE (covered elsewhere — do not re-add):
//   • base visualPrefabPath Resources-load per entry → DataRegression.CheckStructures
//   • Wood/Iron dual-wallet + crystal SSOT              → VillageEconomyRegression (B2
//     is a fail-by-design oracle; duplicating it would double the known failure)
//   • 45° footprint claim inflation, migration round-trip, save v30, one-per-id,
//     yaw round-trip, repair chain                       → StrategicPlacementRegression
//   • live upgrade charge + ApplyTierStats on components → BuildingUpgradeRegression /
//     play-mode (needs live DefenseTower/WallSegment behaviours ticking)
//   • BaseLayoutLoader.Spawn full replay (footprint blocker + NavMeshObstacle +
//     UnderConstruction re-arm) — Spawn strips colliders via Object.Destroy (play-
//     mode-only) and gates on the live scene name/GameStateService, so the loader
//     half needs play mode; the DATA half + the real factory Create are proven here.
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;
using DeNelle.Village;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Editor
{
    public static class BuildEconomyRegression
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";
        private const string DamageRelPath  = "Data/Canonical/damage-states.json";

        [System.Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== BuildEconomyRegression: build-mode/economy data spine ===");

            var created = new List<GameObject>();
            try
            {
                // ── 1. CATALOG PARSE — the production parse (CatalogBootstrap.LoadFromJson
                //       settings verbatim: StringEnumConverter + ignore null/miss) ─────────
                var entries = ParseCatalog(failures, log);
                if (entries == null)
                {
                    // Parse-level break — nothing downstream is decidable.
                    return Verdict(failures, log, out reason);
                }

                // ── 2. DUAL-COPY BYTE-EQUAL (the catalog's own stated rule) ─────────────
                CheckDualCopy(CatalogRelPath, failures, log);
                CheckDualCopy(DamageRelPath, failures, log);

                // Hydrate the registry so the REAL cost/refund/factory paths resolve ids
                // exactly as the game does (CatalogRegistry.Get). Mirrors CatalogBootstrap's
                // register loop over the SAME parsed entries.
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.id) && CatalogRegistry.Get(e.id) == null)
                        CatalogRegistry.Register(e);

                // ── 2b. REQUIRED IDS (WO-812): the Barracks must exist as a placeable row —
                // the army/raid ladder (train -> deploy) depends on it; the hub redesign
                // silently dropped it once, never again. Type Resource => the Town palette.
                bool hasBarracks = false;
                foreach (var e in entries)
                    if (e != null && string.Equals(e.id, "barracks", System.StringComparison.OrdinalIgnoreCase))
                    { hasBarracks = true; break; }
                if (!hasBarracks)
                    failures.Add("required id 'barracks' MISSING from structures-catalog (WO-812 — the train/raid ladder has no world entry)");
                else
                    log.AppendLine("  required id 'barracks' present (WO-812) OK");

                // ── 3. COST SANITY through the REAL resolver ────────────────────────────
                CheckCosts(entries, failures, log);

                // ── 4. UPGRADE LADDER — tier-monotonic costs + tier visuals resolve ─────
                CheckUpgradeLadder(entries, failures, log);

                // ── 5. TOWER BEHAVIOUR CONTRACT — stats + projectileStyle vocabulary ────
                CheckTowerContract(entries, failures, log);

                // ── 6. PLACEMENT MATH — real PlacementGrid invariants ───────────────────
                CheckPlacementMath(entries, created, failures, log);

                // ── 7. SELL REFUND = 50% (invested-cost-aware) via the REAL RefundCostFor ─
                CheckSellRefund(created, failures, log);

                // ── 8. BASELAYOUT REPLAY — data-half + the REAL StructureFactory.Create ──
                CheckBaseLayoutReplay(created, failures, log);

                // ── 9. BUILD-TIMER CONFIG (WO-172 / WO-612 rewarded-ad path) ─────────────
                CheckBuildTimerConfig(failures, log);

                // ── 10. DAMAGE STATES (WO-672 D) — real DamageStatesCatalog loader ───────
                CheckDamageStates(failures, log);

                // -- 11. WO-855 -- tower spam softcap + the build-tier fix ----------------
                CheckTowerSoftcap(entries, created, failures, log);
                CheckBuildTierDerivation(entries, failures, log);

                // -- 12. FALLBACK/CATALOG PARITY -- the JSON-load-FAILURE path must ship
                //        the SAME content as the catalog it exists to mirror.
                CheckFallbackParity(entries, failures, log);
            }
            catch (System.Exception ex)
            {
                failures.Add($"BuildEconomyRegression threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                foreach (var go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }

            return Verdict(failures, log, out reason);
        }

        // =====================================================================
        //  1. CATALOG PARSE — ids unique + display fields present
        // =====================================================================
        private static List<CatalogEntry> ParseCatalog(List<string> failures, StringBuilder log)
        {
            string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add($"{CatalogRelPath} unreadable (CanonicalJson.Read returned empty)");
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
            catch (System.Exception ex)
            {
                failures.Add($"structures-catalog.json failed to parse: {ex.Message}");
                return null;
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("structures-catalog.json deserialized to 0 CatalogEntry objects (mapping break or empty 'entries')");
                return null;
            }

            log.AppendLine($"structures-catalog.json v{file.Version} -> {file.Entries.Count} CatalogEntry object(s)");

            var seen = new HashSet<string>();
            foreach (var e in file.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id))
                { failures.Add("catalog entry with null/empty id"); continue; }
                if (!seen.Add(e.id))
                    failures.Add($"duplicate catalog id '{e.id}' — CatalogRegistry.Register would silently REPLACE the first row");
                if (string.IsNullOrEmpty(e.displayName))
                    failures.Add($"catalog entry '{e.id}' has no displayName (blank palette row)");
                if (e.repo == null)
                    failures.Add($"catalog entry '{e.id}' has null repo (behaviour half missing — deserializer default was lost)");
            }
            return file.Entries;
        }

        // =====================================================================
        //  2. DUAL-COPY — Resources copy must stay byte-identical to StreamingAssets
        //     (the WebGL-safe CanonicalJson contract stated in both files' notes).
        // =====================================================================
        private static void CheckDualCopy(string relPath, List<string> failures, StringBuilder log)
        {
            string res = Application.dataPath + "/Resources/" + relPath;
            string sa  = Application.dataPath + "/StreamingAssets/" + relPath;
            bool hasRes = System.IO.File.Exists(res);
            bool hasSa  = System.IO.File.Exists(sa);
            if (!hasRes || !hasSa)
            {
                failures.Add($"dual-copy '{relPath}': missing {(hasRes ? "" : "Resources copy ")}{(hasSa ? "" : "StreamingAssets copy")}".Trim());
                return;
            }
            byte[] a = System.IO.File.ReadAllBytes(res);
            byte[] b = System.IO.File.ReadAllBytes(sa);
            bool equal = a.Length == b.Length;
            if (equal)
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) { equal = false; break; }
            if (!equal)
                failures.Add($"dual-copy '{relPath}': Resources and StreamingAssets copies DIVERGED " +
                             $"({a.Length} vs {b.Length} bytes) — editor and WebGL would load different data");
            else
                log.AppendLine($"  dual-copy '{relPath}' byte-identical ({a.Length} bytes) OK");
        }

        // =====================================================================
        //  3. COSTS — every affordable-gated entry resolves a NON-ZERO effective
        //     cost through the REAL BuildModeController.CostFor (multi-cost wins,
        //     crystals-only buildCost fallback), and no slot is negative.
        // =====================================================================
        private static void CheckCosts(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            int checkedCount = 0;
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || e.repo == null) continue;
                CoreCost c = BuildModeController.CostFor(e);
                checkedCount++;

                if (c.wood < 0 || c.food < 0 || c.iron < 0 || c.crystals < 0)
                    failures.Add($"'{e.id}' resolves a NEGATIVE cost slot ({c.wood}/{c.food}/{c.iron}/{c.crystals})");

                bool affordGated = e.repo.placement == null || e.repo.placement.checkAffordable;
                if (affordGated && c.IsZero)
                    failures.Add($"'{e.id}' is affordability-gated but CostFor resolves ZERO " +
                                 "(no repo.cost AND buildCost 0 — placement would be free)");

                log.AppendLine($"  COST {e.id} -> w{c.wood} f{c.food} i{c.iron} c{c.crystals}" +
                               (affordGated ? "" : " (checkAffordable=false)"));
            }
            if (checkedCount == 0)
                failures.Add("cost check covered 0 entries (catalog empty after filtering?)");
        }

        // =====================================================================
        //  4. UPGRADE LADDER — for maxLevel > 1 rows: every step resolves a
        //     non-zero cost via the REAL UpgradeCostFor, step totals never
        //     DECREASE (the CoC sink escalates), the authored tier-visual ladder
        //     fits maxLevel and every tier prefab Resources-loads, maxLevel stays
        //     within the StructureTierVisual 1..3 ceiling, and the tower tier
        //     stat multiplier table is strictly increasing (tier-monotonic).
        // =====================================================================
        private static void CheckUpgradeLadder(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            foreach (var e in entries)
            {
                if (e == null || e.repo == null) continue;
                int maxLevel = Mathf.Clamp(e.repo.maxLevel, 1, 3);   // mirrors BuildModeController.MaxLevelFor

                if (e.repo.maxLevel > 3)
                    failures.Add($"'{e.id}' authors maxLevel {e.repo.maxLevel} — above the StructureTierVisual ceiling (3); levels 4+ are unreachable dead data");

                var ladder = e.repo.upgradeVisualPath;
                if (ladder != null && ladder.Length > 0)
                {
                    if (maxLevel < 2)
                        failures.Add($"'{e.id}' authors upgradeVisualPath but maxLevel {maxLevel} — the tier models are unreachable");
                    if (ladder.Length > maxLevel - 1)
                        failures.Add($"'{e.id}' authors {ladder.Length} tier visual(s) but only {maxLevel - 1} upgrade step(s) exist");
                    for (int i = 0; i < ladder.Length; i++)
                    {
                        if (string.IsNullOrEmpty(ladder[i])) continue;   // "keep previous model" is legal
                        // The SAME resolve StructureFactory.VisualPathForLevel feeds to
                        // VisualFactory.Skin → Resources.Load. A null here = the upgraded
                        // tower silently keeps its old look (F8 2026-07-06 class of bug).
                        if (Resources.Load<GameObject>(ladder[i]) == null)
                            failures.Add($"'{e.id}' upgradeVisualPath[{i}] '{ladder[i]}' (the L{i + 2} model) loads NULL from Resources");
                        else
                            log.AppendLine($"  TIER {e.id} L{i + 2} -> '{ladder[i]}' prefab OK");
                    }
                    // VisualPathForLevel through the REAL resolver: L1 = base, L2+ = ladder.
                    string l1 = StructureFactory.VisualPathForLevel(e, 1);
                    if (l1 != e.visualPrefabPath)
                        failures.Add($"'{e.id}' VisualPathForLevel(1) returned '{l1}' — must be the base visualPrefabPath");
                }

                if (maxLevel <= 1) continue;

                int prevTotal = -1;
                for (int from = 1; from < maxLevel; from++)
                {
                    CoreCost step = BuildModeController.UpgradeCostFor(e, from);
                    int total = step.wood + step.food + step.iron + step.crystals;
                    if (total <= 0)
                        failures.Add($"'{e.id}' upgrade step L{from}->L{from + 1} resolves ZERO cost (free upgrade — sink broken)");
                    if (step.wood < 0 || step.food < 0 || step.iron < 0 || step.crystals < 0)
                        failures.Add($"'{e.id}' upgrade step L{from}->L{from + 1} has a negative slot");
                    if (prevTotal >= 0 && total < prevTotal)
                        failures.Add($"'{e.id}' upgrade cost NOT tier-monotonic: L{from}->L{from + 1} total {total} < previous step {prevTotal}");
                    log.AppendLine($"  UP {e.id} L{from}->L{from + 1}: w{step.wood} f{step.food} i{step.iron} c{step.crystals} (total {total})");
                    prevTotal = total;
                }
            }

            // Tower tier stat multiplier (private table, read-only reflection): L1..L3
            // must be strictly increasing or an upgrade would not step the tower's power.
            var mulField = typeof(BuildModeController).GetField("s_towerTierMul",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mulField == null)
                failures.Add("BuildModeController.s_towerTierMul not found (renamed?) — tier stat monotonicity unverifiable");
            else if (mulField.GetValue(null) is float[] mul && mul.Length >= 4)
            {
                if (!(mul[1] < mul[2] && mul[2] < mul[3]))
                    failures.Add($"tower tier stat multipliers not strictly increasing: L1 x{mul[1]}, L2 x{mul[2]}, L3 x{mul[3]}");
                else
                    log.AppendLine($"  tower tier stat multipliers L1 x{mul[1]} < L2 x{mul[2]} < L3 x{mul[3]} OK");
            }
        }

        // =====================================================================
        //  5. TOWER CONTRACT — StructureFactory copies range/damage/fireRate
        //     VERBATIM onto DefenseTower/ArcaneTower; a zero stat is a tower
        //     that never fires. projectileStyle is a loose string — assert the
        //     known vocabulary (pellet|bolt|spell|empty) so a typo ('blot')
        //     can't silently fall back to the pellet.
        // =====================================================================
        private static void CheckTowerContract(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            var knownStyles = new HashSet<string> { "pellet", "bolt", "spell" };
            foreach (var e in entries)
            {
                if (e == null || e.repo == null) continue;
                string b = e.repo.behaviorId;
                if (b != "DefenseTower" && b != "ArcaneTower") continue;

                if (e.repo.range <= 0f)    failures.Add($"tower '{e.id}' authors range {e.repo.range} — would never acquire a target");
                if (e.repo.damage <= 0f)   failures.Add($"tower '{e.id}' authors damage {e.repo.damage} — would deal nothing");
                if (e.repo.fireRate <= 0f) failures.Add($"tower '{e.id}' authors fireRate {e.repo.fireRate} — would never fire");

                string style = e.repo.projectileStyle;
                if (!string.IsNullOrEmpty(style) && !knownStyles.Contains(style))
                    failures.Add($"tower '{e.id}' projectileStyle '{style}' is not in the known vocabulary (pellet|bolt|spell) — silent pellet fallback");

                log.AppendLine($"  TW {e.id} range={e.repo.range} dmg={e.repo.damage} rof={e.repo.fireRate} " +
                               $"style='{style ?? "<null>"}' airOnly={e.repo.airOnly}");
            }
        }

        // =====================================================================
        //  6. PLACEMENT MATH — real PlacementGrid: cell↔world round-trip, snap,
        //     footprint occupancy, edge-allow bounds, metric→cell conversion,
        //     and gate-clearance / footprint rule constants from the catalog.
        //     (45° claim inflation is StrategicPlacementRegression gate 7.)
        // =====================================================================
        private static void CheckPlacementMath(List<CatalogEntry> entries, List<GameObject> created,
                                               List<string> failures, StringBuilder log)
        {
            var gridGo = new GameObject("BuildEconRegressionGrid");
            created.Add(gridGo);
            var grid = gridGo.AddComponent<PlacementGrid>();
            // Editmode AddComponent does not run Awake — apply the SAME origin-centering
            // Awake performs so cell↔world matches the runtime grid exactly.
            grid.origin = new Vector3(-grid.gridWidth * grid.cellSize * 0.5f, 0f,
                                      -grid.gridHeight * grid.cellSize * 0.5f);

            // Round-trip: WorldToCell(CellToWorld(cell)) == cell across the grid,
            // including the corner cells (edge-allow: margin 0 keeps them buildable).
            var samples = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(grid.gridWidth - 1, grid.gridHeight - 1),
                new Vector2Int(grid.gridWidth / 2, grid.gridHeight / 2), new Vector2Int(3, 25),
            };
            foreach (var cell in samples)
            {
                var back = grid.WorldToCell(grid.CellToWorld(cell));
                if (back != cell)
                    failures.Add($"grid round-trip broke: cell ({cell.x},{cell.y}) -> world -> ({back.x},{back.y})");
            }

            // SnapToGrid preserves the surface Y and lands on the cell centre.
            var probe = new Vector3(4.2f, 7.31f, -8.9f);
            var snapped = grid.SnapToGrid(probe);
            if (!Mathf.Approximately(snapped.y, probe.y))
                failures.Add($"SnapToGrid did not preserve Y ({probe.y} -> {snapped.y}) — placed structures would lose their surface height");
            var expectCentre = grid.CellToWorld(grid.WorldToCell(probe));
            if (Mathf.Abs(snapped.x - expectCentre.x) > 0.001f || Mathf.Abs(snapped.z - expectCentre.z) > 0.001f)
                failures.Add("SnapToGrid XZ is not the cell centre");

            // Occupancy: a 2x2 claim blocks every overlapping placement; Free restores.
            var at = new Vector2Int(10, 10);
            var fp = new Vector2Int(2, 2);
            if (!grid.CanPlace(at, fp)) failures.Add("empty grid refused a legal 2x2 placement");
            grid.Occupy(at, fp, "oracle");
            if (grid.CanPlace(at, Vector2Int.one))
                failures.Add("Occupy did not block the anchor cell");
            if (grid.CanPlace(new Vector2Int(11, 11), Vector2Int.one))
                failures.Add("Occupy did not block the far corner of a 2x2 footprint");
            if (grid.CanPlace(new Vector2Int(9, 9), fp))
                failures.Add("a 2x2 at (9,9) overlapping one occupied cell was allowed — footprint overlap check broken");
            if (!grid.CanPlace(new Vector2Int(12, 10), Vector2Int.one))
                failures.Add("a free cell adjacent to the footprint was wrongly blocked");
            grid.Free(at, fp);
            if (!grid.CanPlace(at, fp)) failures.Add("Free did not release the occupied cells (sell/move would leak occupancy)");

            // Bounds: out-of-grid always refused; boundary cell allowed (edgeMargin 0 —
            // the owner's edge-allow rule so perimeter walls reach the map edge).
            if (grid.CanPlace(new Vector2Int(-1, 5), Vector2Int.one)) failures.Add("out-of-bounds cell (-1,5) was placeable");
            if (grid.CanPlace(new Vector2Int(grid.gridWidth - 1, 5), fp)) failures.Add("a 2x2 hanging off the +X edge was placeable");
            if (grid.edgeMargin == 0 && !grid.CanPlace(new Vector2Int(0, 5), Vector2Int.one))
                failures.Add("edge-allow broken: boundary cell (0,5) refused with edgeMargin 0");

            // Metric→cell: FootprintCells = Ceil(m / cellSize), floor 1.
            foreach (var m in new[] { 0.5f, 2.5f, 3f, 3.4f, 6f, 7.1f })
            {
                int expect = Mathf.Max(1, Mathf.CeilToInt(m / grid.cellSize));
                var got = grid.FootprintCells(m);
                if (got.x != expect || got.y != expect)
                    failures.Add($"FootprintCells({m}m) = {got.x}x{got.y}, expected {expect}x{expect}");
            }

            // Catalog placement-rule constants: every rule footprint positive, gate
            // clearance non-negative, and every Gate-type row carries a clearance rule
            // (the spawn→Heart lane must stay open).
            foreach (var e in entries)
            {
                var p = e != null && e.repo != null ? e.repo.placement : null;
                if (p == null) continue;
                if (p.footprint <= 0f)
                    failures.Add($"'{e.id}' placement.footprint {p.footprint} — non-positive footprint collapses the grid claim");
                if (p.minDistanceFromGate < 0f)
                    failures.Add($"'{e.id}' placement.minDistanceFromGate {p.minDistanceFromGate} is negative");
                if (e.type == CatalogType.Gate && p.minDistanceFromGate <= 0f)
                    failures.Add($"gate '{e.id}' has no minDistanceFromGate rule — gates could stack and wall off the lane");
            }
            log.AppendLine($"  placement math on {grid.gridWidth}x{grid.gridHeight} grid (cell {grid.cellSize}m) OK-checked");
        }

        // =====================================================================
        //  7. SELL REFUND — drive the REAL (private) BuildModeController
        //     .RefundCostFor on synthetic PlacedStructure components and assert
        //     the 50%-of-invested-cost rule: L1 = build/2 per slot (floor);
        //     L3 = (build + L1→L2 + L2→L3)/2 per slot. The WO-676 salvage bonus
        //     is read through the SAME public StatSum the production code calls,
        //     so a stray talent save cannot false-fail the oracle.
        // =====================================================================
        private static void CheckSellRefund(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            var entry = CatalogRegistry.Get("tower_ground_archer");
            if (entry == null)
            {
                failures.Add("refund check: 'tower_ground_archer' not in registry (hydration failed)");
                return;
            }

            var refundMethod = typeof(BuildModeController).GetMethod("RefundCostFor",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (refundMethod == null)
            {
                failures.Add("BuildModeController.RefundCostFor not found (renamed?) — the 50% sell rule is unverifiable");
                return;
            }

            // HeroTalentClassReader is INTERNAL to DeNelle.Village — resolve the SAME
            // slug production RefundCostFor uses via reflection (headless: no
            // GameStateService => "knight" => StatSum 0, the baseline identity).
            string slug = "knight";
            var readerType = typeof(EconomyService).Assembly.GetType("DeNelle.Village.HeroTalentClassReader");
            var slugMethod = readerType != null
                ? readerType.GetMethod("Slug", BindingFlags.Public | BindingFlags.Static) : null;
            if (slugMethod != null) slug = (string)slugMethod.Invoke(null, null) ?? "knight";
            float salvage = DeNelle.Village.Talents.HeroTalentModifiers.StatSum(slug, "salvage");
            if (salvage > 0f)
                log.AppendLine($"  note: salvage bonus {salvage:0.###} active in this environment — folded into expected refunds");

            // Local mirror of ApplySalvage (identity at salvage 0 — the headless norm).
            int Half(int slotTotal) => salvage > 0f
                ? Mathf.RoundToInt((slotTotal / 2) * (1f + salvage))
                : slotTotal / 2;

            foreach (int level in new[] { 1, 3 })
            {
                var go = new GameObject($"RefundOracle_L{level}");
                created.Add(go);
                var ps = go.AddComponent<PlacedStructure>();
                ps.itemId = entry.id;
                ps.level = level;
                ps.gridCell = new Vector2Int(5, 5);

                // Expected invested cost through the SAME public resolvers the game uses.
                CoreCost total = BuildModeController.CostFor(entry);
                for (int from = 1; from < level; from++)
                {
                    var step = BuildModeController.UpgradeCostFor(entry, from);
                    total.wood += step.wood; total.food += step.food;
                    total.iron += step.iron; total.crystals += step.crystals;
                }

                var refund = (CoreCost)refundMethod.Invoke(null, new object[] { ps });
                int ew = Half(total.wood), ef = Half(total.food), ei = Half(total.iron), ec = Half(total.crystals);
                if (refund.wood != ew || refund.food != ef || refund.iron != ei || refund.crystals != ec)
                    failures.Add($"sell refund L{level} '{entry.id}' = w{refund.wood} f{refund.food} i{refund.iron} c{refund.crystals}, " +
                                 $"expected 50% of invested (w{ew} f{ef} i{ei} c{ec}) — the half-back rule broke");
                else
                    log.AppendLine($"  REFUND {entry.id} L{level}: w{refund.wood} f{refund.food} i{refund.iron} c{refund.crystals} " +
                                   $"== 50% of invested (w{total.wood} f{total.food} i{total.iron} c{total.crystals}) OK");
            }
        }

        // =====================================================================
        //  8. BASELAYOUT REPLAY — (a) data-half: a synthetic PlacedStructureData
        //     list resolves ids, fits the grid, and its yaw/level round-trips
        //     inside the persisted contract; (b) factory-half: ONE record builds
        //     through the REAL StructureFactory.Create (the same call
        //     BaseLayoutLoader.Spawn makes) and comes back rendering. Loader-side
        //     collider/NavMesh wiring is play-mode (see header) — data half here.
        // =====================================================================
        private static void CheckBaseLayoutReplay(List<GameObject> created, List<string> failures, StringBuilder log)
        {
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (grid == null)
            {
                failures.Add("replay check: no PlacementGrid available (check 6 should have created one)");
                return;
            }

            var layout = new List<PlacedStructureData>
            {
                new PlacedStructureData("tower_ground_archer", 3, 5, 2, 1),
                new PlacedStructureData("wall_wood", 10, 10, 0, 3),
                new PlacedStructureData("gate_stone", 20, 8, 1, 1, yawOffset: 45f),
            };

            foreach (var rec in layout)
            {
                var entry = CatalogRegistry.Get(rec.itemId);
                if (entry == null)
                { failures.Add($"replay record '{rec.itemId}' does not resolve in CatalogRegistry — the building would be lost on load"); continue; }

                // Cell claim fits the grid (the same InBounds gate placement validity uses).
                float metres = entry.repo != null && entry.repo.placement != null ? entry.repo.placement.footprint : 3f;
                float yawDeg = rec.yawSteps * 90f + rec.yawOffset;
                var fp = grid.FootprintCells(metres, yawDeg);
                if (!grid.InBounds(new Vector2Int(rec.cellX, rec.cellZ), fp))
                    failures.Add($"replay record '{rec.itemId}' at ({rec.cellX},{rec.cellZ}) footprint {fp.x}x{fp.y} falls outside the grid");

                // Persisted yaw contract: yawSteps 0..3 + a sub-90 offset reproduces the facing.
                if (rec.yawSteps < 0 || rec.yawSteps > 3)
                    failures.Add($"replay record '{rec.itemId}' yawSteps {rec.yawSteps} outside the 0..3 contract");
                if (rec.yawOffset < 0f || rec.yawOffset >= 90f)
                    failures.Add($"replay record '{rec.itemId}' yawOffset {rec.yawOffset} outside [0,90) — double-counts a quarter step");

                // Level within the entry's catalog ceiling.
                int maxLevel = entry.repo != null ? Mathf.Clamp(entry.repo.maxLevel, 1, 3) : 1;
                if (rec.level < 1 || rec.level > maxLevel)
                    failures.Add($"replay record '{rec.itemId}' level {rec.level} outside 1..{maxLevel}");
            }
            log.AppendLine($"  replay data-half: {layout.Count} synthetic record(s) validated against registry + grid");

            // Factory-half — the REAL create path (registry entry -> VisualFactory.Skin ->
            // render-verify -> AttachBehavior). Create returning null IS the data break
            // (missing/render-broken prefab); it Fail-logs the exact path itself.
            var archer = CatalogRegistry.Get("tower_ground_archer");
            var parent = new GameObject("BuildEconReplayRoot");
            created.Add(parent);
            GameObject builtRoot = null;
            try
            {
                builtRoot = StructureFactory.Create(archer,
                    new Pose(grid.CellToWorld(new Vector2Int(3, 5)), Quaternion.Euler(0f, 180f, 0f)),
                    parent.transform);
            }
            catch (System.Exception ex)
            {
                failures.Add($"StructureFactory.Create threw for 'tower_ground_archer': {ex.GetType().Name}: {ex.Message}");
            }
            if (builtRoot != null) created.Add(builtRoot);

            if (builtRoot == null)
            {
                failures.Add("StructureFactory.Create returned NULL for 'tower_ground_archer' — the replayed base would lose the tower (prefab missing/render-broken)");
            }
            else
            {
                if (builtRoot.GetComponentInChildren<Renderer>(true) == null)
                    failures.Add("replayed 'tower_ground_archer' has no Renderer — invisible structure");
                if (builtRoot.GetComponent<DefenseTower>() == null)
                    failures.Add("replayed 'tower_ground_archer' did not receive its DefenseTower behaviour (AttachBehavior broke)");
                else
                {
                    var dt = builtRoot.GetComponent<DefenseTower>();
                    if (!Mathf.Approximately(dt.Range, archer.repo.range) || !Mathf.Approximately(dt.Damage, archer.repo.damage))
                        failures.Add($"replayed tower stats diverge from catalog: range {dt.Range} vs {archer.repo.range}, damage {dt.Damage} vs {archer.repo.damage}");
                    else
                        log.AppendLine($"  replay factory-half: Create OK, DefenseTower carries catalog stats (range {dt.Range}, dmg {dt.Damage})");
                }
            }
        }

        // =====================================================================
        //  9. BUILD-TIMER CONFIG — the SAME resolve BuildTimerService performs
        //     (authored Resources asset, else code default). Curve + ad-skip +
        //     instant-finish + slot knobs must be sane and tier-monotonic.
        // =====================================================================
        private static void CheckBuildTimerConfig(List<string> failures, StringBuilder log)
        {
            var cfg = Resources.Load<BuildTimerConfig>(BuildTimerConfig.ResourcesPath);
            bool authored = cfg != null;
            if (cfg == null) cfg = BuildTimerConfig.CreateDefault();
            log.AppendLine($"  build-timer config: {(authored ? "authored asset" : "code default")} " +
                           $"(base {cfg.baseBuildSeconds}s, growth x{cfg.tierGrowth}, adSkip {cfg.adSkipSeconds}s, slots {cfg.freeBuildSlots})");

            if (cfg.baseBuildSeconds <= 0f) failures.Add("BuildTimerConfig.baseBuildSeconds <= 0 — every build finishes instantly (sink dead)");
            if (cfg.tierGrowth < 1f)        failures.Add($"BuildTimerConfig.tierGrowth {cfg.tierGrowth} < 1 — higher tiers get SHORTER (inverted curve)");
            if (cfg.maxDurationSeconds <= 0f) failures.Add("BuildTimerConfig.maxDurationSeconds <= 0 — the clamp zeroes every duration");
            if (cfg.freeBuildSlots < 1)     failures.Add("BuildTimerConfig.freeBuildSlots < 1 — nothing could ever build");
            if (cfg.adSkipSeconds <= 0f)    failures.Add("BuildTimerConfig.adSkipSeconds <= 0 — the WO-612 rewarded-ad skip does nothing");

            float prev = -1f;
            for (int tier = 0; tier <= 5; tier++)
            {
                float s = cfg.DurationSecondsForTier(tier, BuildJobKind.Build);
                if (s < 0f) failures.Add($"DurationSecondsForTier({tier}) negative ({s})");
                if (s > cfg.maxDurationSeconds + 0.01f)
                    failures.Add($"DurationSecondsForTier({tier}) = {s}s exceeds the {cfg.maxDurationSeconds}s ceiling — clamp broken");
                if (prev >= 0f && s < prev - 0.01f)
                    failures.Add($"build duration NOT tier-monotonic: tier {tier} = {s}s < tier {tier - 1} = {prev}s");
                prev = s;
            }
            float b1 = cfg.DurationSecondsForTier(1, BuildJobKind.Build);
            float u1 = cfg.DurationSecondsForTier(1, BuildJobKind.Upgrade);
            if (cfg.upgradeMultiplier >= 1f && u1 < b1 - 0.01f)
                failures.Add($"upgrade duration ({u1}s) shorter than build ({b1}s) despite multiplier x{cfg.upgradeMultiplier}");

            // Instant-finish: enabled → floor at the minimum price; disabled → always 0.
            if (cfg.instantFinishCrystalsPerMinute > 0)
            {
                int nearDone = cfg.InstantFinishPrice(1.0);
                if (nearDone < cfg.instantFinishMinCrystals)
                    failures.Add($"InstantFinishPrice(1s) = {nearDone} below the {cfg.instantFinishMinCrystals} minimum — near-done jobs finish for free");
                int hourJob = cfg.InstantFinishPrice(3600.0);
                if (hourJob < nearDone)
                    failures.Add($"InstantFinishPrice not monotonic: 1h job ({hourJob}) cheaper than a 1s job ({nearDone})");
            }
            else if (cfg.InstantFinishPrice(3600.0) != 0)
            {
                failures.Add("instant-finish disabled (perMinute 0) but InstantFinishPrice(1h) != 0");
            }
        }

        // =====================================================================
        //  10. DAMAGE STATES — the REAL DamageStatesCatalog loader: thresholds
        //     parse + stay ordered (0 < fire <= smolder < 1), every perType key
        //     resolves through the same Resolve path the visuals read, and the
        //     authored optOuts (gate/heart bespoke tells) hold.
        // =====================================================================
        private static void CheckDamageStates(List<string> failures, StringBuilder log)
        {
            DamageStatesCatalog.Invalidate();   // force a fresh read through CanonicalJson

            float smolder = DamageStatesCatalog.Smolder("wall");   // any non-override key = defaults
            float fire    = DamageStatesCatalog.Fire("wall");
            float bar     = DamageStatesCatalog.BarOffset("wall");
            int   loops   = DamageStatesCatalog.MaxBurnLoops;
            log.AppendLine($"  damage-states defaults: smolder {smolder}, fire {fire}, barOffset {bar}, maxBurnLoops {loops}");

            if (!(fire > 0f && fire < 1f))      failures.Add($"damage-states fire threshold {fire} outside (0,1)");
            if (!(smolder > 0f && smolder < 1f)) failures.Add($"damage-states smolder threshold {smolder} outside (0,1)");
            if (fire > smolder)
                failures.Add($"damage-states thresholds INVERTED: fire {fire} > smolder {smolder} — the full-burn tell would show before the smolder");
            if (bar <= 0f)   failures.Add($"damage-states barOffset {bar} <= 0 — the health bar spawns inside the mesh");
            if (loops < 1)   failures.Add($"damage-states maxBurnLoops {loops} < 1");

            // Every authored perType key must map through the loader (parse-level check
            // over the raw JSON — the loader swallows unknown keys silently), and each
            // override that authors both thresholds must keep them ordered.
            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/damage-states.json");
            if (string.IsNullOrEmpty(json))
            { failures.Add("damage-states.json unreadable"); return; }
            try
            {
                var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (raw == null || !raw.ContainsKey("defaults"))
                    failures.Add("damage-states.json has no 'defaults' block (loader would run on code defaults silently)");
                if (raw != null && raw.TryGetValue("perType", out var pt) && pt is Newtonsoft.Json.Linq.JObject perType)
                {
                    foreach (var kv in perType)
                    {
                        string key = kv.Key;
                        if (key != key.ToLowerInvariant())
                            failures.Add($"damage-states perType key '{key}' is not lowercase — the stated lowercase-kind contract");
                        float s = DamageStatesCatalog.Smolder(key);
                        float f = DamageStatesCatalog.Fire(key);
                        if (f > s)
                            failures.Add($"damage-states perType '{key}' resolves inverted thresholds (fire {f} > smolder {s})");
                        log.AppendLine($"  damage-states perType '{key}': optOut={DamageStatesCatalog.OptOut(key)} smolder={s} fire={f}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                failures.Add($"damage-states.json raw parse failed: {ex.Message}");
            }
        }

        // =====================================================================
        //  11a. WO-855 PHASE 1 -- TOWER SPAM SOFTCAP
        //  ---------------------------------------------------------------------
        //  Phase 0 measured that NOTHING limited tower count: no cap, no singleton
        //  flag, flat cost -- the 1st and the 50th archer cost the same. This gate
        //  proves the one multiplier is (a) correctly shaped, (b) wired into the
        //  NEW-PLACEMENT path only, and (c) unable to touch the owner-locked
        //  freebies that ARE a fresh save's entire starting budget.
        // =====================================================================
        private static void CheckTowerSoftcap(List<CatalogEntry> entries, List<GameObject> created,
                                              List<string> failures, StringBuilder log)
        {
            var archer = CatalogRegistry.Get("tower_ground_archer");
            if (archer == null)
            {
                failures.Add("[softcap] 'tower_ground_archer' not in registry -- the softcap gate cannot run");
                return;
            }

            // -- A. PURE CURVE -- monotonic, flat below the start ordinal, capped --
            int start = BuildModeController.TowerSoftcapStartAtOrdinal;
            float per  = BuildModeController.TowerSoftcapMultPerExtra;
            float cap  = BuildModeController.TowerSoftcapMaxMult;
            if (start < 2)  failures.Add($"[softcap] startAtOrdinal {start} < 2 -- the very FIRST tower would be surcharged");
            if (per <= 0f)  failures.Add($"[softcap] multPerExtra {per} <= 0 -- tower spam is not self-limiting at all");
            if (cap <= 1f)  failures.Add($"[softcap] maxMult {cap} <= 1 -- the ceiling cancels the whole curve");

            for (int ordinal = 1; ordinal < start; ordinal++)
            {
                float m = BuildModeController.TowerSoftcapMultiplier(ordinal);
                if (!Mathf.Approximately(m, 1f))
                    failures.Add($"[softcap] tower #{ordinal} multiplier {m} != 1.0 -- the first {start - 1} towers must be un-surcharged");
            }

            float expectFirstSurcharge = Mathf.Min(cap, 1f + per);
            float gotFirstSurcharge = BuildModeController.TowerSoftcapMultiplier(start);
            if (!Mathf.Approximately(gotFirstSurcharge, expectFirstSurcharge))
                failures.Add($"[softcap] tower #{start} multiplier {gotFirstSurcharge} != expected {expectFirstSurcharge} (1 + multPerExtra)");

            float prevMult = 0f;
            bool everRose = false;
            for (int ordinal = 1; ordinal <= 60; ordinal++)
            {
                float m = BuildModeController.TowerSoftcapMultiplier(ordinal);
                if (m < prevMult - 0.0001f)
                    failures.Add($"[softcap] multiplier NOT monotonic: #{ordinal} = {m} < #{ordinal - 1} = {prevMult}");
                if (m > cap + 0.0001f)
                    failures.Add($"[softcap] multiplier {m} at #{ordinal} exceeds the {cap} ceiling -- the clamp broke");
                if (m > prevMult + 0.0001f && ordinal > 1) everRose = true;
                prevMult = m;
            }
            if (!everRose)
                failures.Add("[softcap] multiplier never rose across 60 placements -- tower spam is still free");
            if (!Mathf.Approximately(BuildModeController.TowerSoftcapMultiplier(60), cap))
                failures.Add($"[softcap] multiplier at #60 = {BuildModeController.TowerSoftcapMultiplier(60)} -- expected the {cap} ceiling");
            log.AppendLine($"  [softcap] curve: #1..#{start - 1} x1.00, #{start} x{gotFirstSurcharge:0.##}, " +
                           $"#8 x{BuildModeController.TowerSoftcapMultiplier(8):0.##}, ceiling x{cap:0.##} OK");

            // -- B. PURE APPLICATION -- a tower row escalates, slot by slot --------
            CoreCost archerBase = BuildModeController.CostFor(archer);
            CoreCost at0 = BuildModeController.ApplyTowerSoftcap(archer, archerBase, 0);
            CoreCost at4 = BuildModeController.ApplyTowerSoftcap(archer, archerBase, start - 1);   // placing #start
            CoreCost at7 = BuildModeController.ApplyTowerSoftcap(archer, archerBase, start + 2);   // three later
            if (Total(at0) != Total(archerBase))
                failures.Add($"[softcap] cost with 0 live towers ({Total(at0)}) != the authored cost ({Total(archerBase)}) -- the softcap leaked onto tower #1");
            if (Total(at4) <= Total(archerBase))
                failures.Add($"[softcap] tower #{start} total {Total(at4)} did NOT rise above the authored {Total(archerBase)} -- the multiplier is not applied");
            if (Total(at7) <= Total(at4))
                failures.Add($"[softcap] tower #{start + 3} total {Total(at7)} did not exceed tower #{start} total {Total(at4)} -- the escalation is flat");
            log.AppendLine($"  [softcap] '{archer.id}' basket-total: #1={Total(at0)} -> #{start}={Total(at4)} -> #{start + 3}={Total(at7)} OK");

            // -- C. NON-TOWER IMMUNITY -- a building row is never surcharged -------
            CatalogEntry nonTower = null;
            foreach (var e in entries)
            {
                if (e == null || e.repo == null || BuildModeController.IsTowerEntry(e)) continue;
                if (BuildModeController.CostFor(e).IsZero) continue;
                nonTower = e; break;
            }
            if (nonTower == null)
            {
                failures.Add("[softcap] no non-tower priced row found -- the non-tower immunity gate could not run");
            }
            else
            {
                CoreCost nBase = BuildModeController.CostFor(nonTower);
                CoreCost nAt50 = BuildModeController.ApplyTowerSoftcap(nonTower, nBase, 50);
                if (Total(nAt50) != Total(nBase))
                    failures.Add($"[softcap] non-tower '{nonTower.id}' was surcharged at 50 live towers " +
                                 $"({Total(nBase)} -> {Total(nAt50)}) -- the softcap must be tower-class only");
                else
                    log.AppendLine($"  [softcap] non-tower '{nonTower.id}' immune at 50 live towers OK");
            }

            // -- D. LIVE WIRING -- real PlacedStructure towers in the world raise the
            //      PLACE cost, and leave UPGRADE + REFUND untouched (WO-855 sec.5:
            //      "does not apply to upgrades of existing towers, only new place").
            //      Baseline first: check 7 leaves its own tower PlacedStructures alive
            //      until the shared finally, so we must measure, not assume, zero.
            BuildModeController.InvalidateTowerCount();
            int baseline = BuildModeController.LiveTowerCount();
            int want = start + 3;                       // enough to be well past the surcharge start
            for (int i = baseline; i < want; i++)
            {
                var go = new GameObject($"SoftcapOracleTower_{i}");
                created.Add(go);
                var ps = go.AddComponent<PlacedStructure>();
                ps.itemId = archer.id;
                ps.level = 1;
                ps.gridCell = new Vector2Int(40 + i, 40);
            }
            BuildModeController.InvalidateTowerCount();
            int live = BuildModeController.LiveTowerCount();
            if (live < want)
            {
                failures.Add($"[softcap] LiveTowerCount() returned {live} after seeding {want} tower PlacedStructure(s) " +
                             "-- the live census does not see catalog-placed towers (the softcap would never arm)");
            }
            else
            {
                CoreCost livePlace = BuildModeController.SoftcappedCostFor(archer);
                if (Total(livePlace) <= Total(archerBase))
                    failures.Add($"[softcap] SoftcappedCostFor with {live} live towers = {Total(livePlace)}, " +
                                 $"not above the authored {Total(archerBase)} -- the softcap is NOT wired into the place path");
                else
                    log.AppendLine($"  [softcap] live wiring: {live} towers standing -> place cost {Total(archerBase)} -> {Total(livePlace)} OK");

                // UPGRADE must be identical to the pure fallback off the RAW build cost.
                CoreCost upStep = BuildModeController.UpgradeCostFor(archer, 1);
                CoreCost expectUp = ExpectedUpgradeStep(archer, 1, archerBase);
                if (Total(upStep) != Total(expectUp))
                    failures.Add($"[softcap] UpgradeCostFor(L1->L2) = {Total(upStep)} with {live} live towers, " +
                                 $"expected {Total(expectUp)} off the RAW cost -- the softcap leaked into UPGRADES");
                else
                    log.AppendLine($"  [softcap] UpgradeCostFor unchanged at {live} live towers ({Total(upStep)}) OK");

                // REFUND must be identical too (RefundCostFor sums CostFor + upgrade steps).
                var refundMethod = typeof(BuildModeController).GetMethod("RefundCostFor",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (refundMethod == null)
                {
                    failures.Add("[softcap] RefundCostFor not found -- the refund-immunity gate could not run");
                }
                else
                {
                    var rgo = new GameObject("SoftcapRefundProbe");
                    created.Add(rgo);
                    var rps = rgo.AddComponent<PlacedStructure>();
                    rps.itemId = archer.id;
                    rps.level = 1;
                    rps.gridCell = new Vector2Int(2, 2);
                    // NOTE: this probe is itself a tower PlacedStructure, so it nudges the live
                    // count by one -- which is precisely the point: the refund must not move.
                    BuildModeController.InvalidateTowerCount();
                    var refund = (CoreCost)refundMethod.Invoke(null, new object[] { rps });
                    if (refund.wood != archerBase.wood / 2 || refund.iron != archerBase.iron / 2 ||
                        refund.food != archerBase.food / 2 || refund.crystals != archerBase.crystals / 2)
                    {
                        // Only a genuine leak fails here; the WO-676 salvage talent is 0 headless.
                        float salvage = DeNelle.Village.Talents.HeroTalentModifiers.StatSum("knight", "salvage");
                        if (salvage <= 0f)
                            failures.Add($"[softcap] RefundCostFor L1 = w{refund.wood}/f{refund.food}/i{refund.iron}/c{refund.crystals} " +
                                         $"with towers standing, expected 50% of the RAW cost " +
                                         $"(w{archerBase.wood / 2}/f{archerBase.food / 2}/i{archerBase.iron / 2}/c{archerBase.crystals / 2}) " +
                                         "-- the softcap leaked into REFUNDS");
                    }
                    else
                    {
                        log.AppendLine("  [softcap] RefundCostFor unchanged with towers standing OK");
                    }
                }
            }

            // -- E. FREEBIES SURVIVE THE SOFTCAP -- the owner-locked free placements ARE
            //      the starting budget on a v32 zero-resource save (starting wood/iron
            //      are 0), so a softcap that charged placement #1 or #2 would soft-lock
            //      a fresh run. Driven on a THROWAWAY GameState with the softcap fully
            //      armed (towers still standing from D).
            CheckFreebiesUnderSoftcap(archer, failures, log);

            BuildModeController.InvalidateTowerCount();   // leave no stale census for later suites
        }

        /// <summary>The pure UpgradeCostFor expectation off a RAW build cost (authored table wins, else base x fromLevel).</summary>
        private static CoreCost ExpectedUpgradeStep(CatalogEntry entry, int fromLevel, CoreCost rawBase)
        {
            var repo = entry != null ? entry.repo : null;
            int idx = Mathf.Max(0, fromLevel - 1);
            if (repo != null && repo.upgradeCost != null && idx < repo.upgradeCost.Length && !repo.upgradeCost[idx].IsZero)
                return repo.upgradeCost[idx];
            int scale = Mathf.Max(1, fromLevel);
            return new CoreCost
            {
                wood     = rawBase.wood     * scale,
                food     = rawBase.food     * scale,
                iron     = rawBase.iron     * scale,
                crystals = rawBase.crystals * scale,
            };
        }

        // =====================================================================
        //  11b. FREEBIES UNDER THE ARMED SOFTCAP + the zero-resource founding walk.
        //  Installs a throwaway GameState through the SAME private seams the
        //  FoundingReachabilityRegression oracle uses, then drives the REAL
        //  BuildModeController.FreeBuildAvailable / EffectiveCostFor.
        // =====================================================================
        private static void CheckFreebiesUnderSoftcap(CatalogEntry archer, List<string> failures, StringBuilder log)
        {
            var stateField = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            var instField  = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (stateField == null || instField == null)
            {
                failures.Add("[softcap] GameStateService _state/_instance seams not reflectable -- the freebie gate could not run");
                return;
            }

            var priorInstance = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                // A v32 fresh save exactly as GameStateService.ResetToNewGame leaves it:
                // ZERO wood / ZERO iron / empty freebie ledger. The freebies literally ARE
                // the starting budget, which is why they are load-bearing here.
                throwaway = ScriptableObject.CreateInstance<GameState>();
                throwaway.Resources = ResourceBalance.Zero;
                throwaway.Wood = StartingBudget.StrategicWood;
                throwaway.Iron = StartingBudget.StrategicIron;
                throwaway.FreeBuildsUsed = new List<string>();
                gssGo = new GameObject("GSS (softcap freebie oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                stateField.SetValue(gss, throwaway);
                instField.SetValue(null, gss);

                if (StartingBudget.StrategicWood != 0 || StartingBudget.StrategicIron != 0)
                    log.AppendLine($"  [softcap] note: StartingBudget is no longer zero " +
                                   $"(wood {StartingBudget.StrategicWood}, iron {StartingBudget.StrategicIron}) -- " +
                                   "the freebies are no longer the only route through founding");
                else
                    log.AppendLine("  [softcap] fresh save: 0 wood / 0 iron -- the placement freebies ARE the starting budget");

                BuildModeController.InvalidateTowerCount();
                int liveNow = BuildModeController.LiveTowerCount();
                if (liveNow < BuildModeController.TowerSoftcapStartAtOrdinal)
                    log.AppendLine($"  [softcap] note: only {liveNow} live tower(s) while checking freebies -- surcharge may not be armed");

                // The two WOODEN archer freebies must both be EXACTLY zero with the
                // softcap armed. This is the soft-lock guard: charging either one on a
                // 0-wood / 0-iron save leaves the player with no way to found.
                for (int placement = 1; placement <= 2; placement++)
                {
                    if (!BuildModeController.FreeBuildAvailable(archer))
                    {
                        failures.Add($"[softcap] archer placement #{placement} is NOT free -- the owner-locked " +
                                     "2 wooden-tower freebie broke (a fresh 0-resource save cannot found)");
                        break;
                    }
                    CoreCost eff = BuildModeController.EffectiveCostFor(archer);
                    if (!eff.IsZero)
                    {
                        failures.Add($"[softcap] archer placement #{placement} costs " +
                                     $"w{eff.wood}/f{eff.food}/i{eff.iron}/c{eff.crystals} with the softcap armed -- " +
                                     "the freebie MUST short-circuit before the multiplier");
                        break;
                    }
                    throwaway.FreeBuildsUsed.Add(archer.id);   // burn it, exactly as Place() does
                }
                log.AppendLine("  [softcap] both wooden archer freebies still cost ZERO with the softcap armed OK");

                // The THIRD archer is charged -- and, with the softcap armed, charged MORE
                // than the authored cost. This is the whole point of Phase 1.
                if (BuildModeController.FreeBuildAvailable(archer))
                {
                    failures.Add("[softcap] archer placement #3 is still FREE -- the 2-cap wooden freebie is not being burned");
                }
                else
                {
                    CoreCost third = BuildModeController.EffectiveCostFor(archer);
                    CoreCost raw = BuildModeController.CostFor(archer);
                    if (third.IsZero)
                        failures.Add("[softcap] archer placement #3 resolves ZERO cost -- placements past the freebie must be charged");
                    else if (liveNow >= BuildModeController.TowerSoftcapStartAtOrdinal && Total(third) <= Total(raw))
                        failures.Add($"[softcap] archer placement #3 costs {Total(third)} with {liveNow} towers standing -- " +
                                     $"not above the authored {Total(raw)}; EffectiveCostFor is not routed through the softcap");
                    else
                        log.AppendLine($"  [softcap] archer placement #3 charged {Total(third)} (authored {Total(raw)}, {liveNow} towers standing) OK");
                }

                // FOUNDING WALK on a 0-wood / 0-iron save: every id the founding tutorial
                // forces must resolve a ZERO effective cost at its first placement, in
                // sequence, with the softcap armed. Total must be exactly 0.
                throwaway.FreeBuildsUsed.Clear();
                var foundingWalk = new[] { "pet-house", "collector_lumbermill", "tower_ground_archer" };
                int walkTotal = 0;
                foreach (var id in foundingWalk)
                {
                    var e = CatalogRegistry.Get(id);
                    if (e == null)
                    {
                        failures.Add($"[softcap] founding-walk id '{id}' is not in the catalog -- the founding sequence cannot complete");
                        continue;
                    }
                    CoreCost c = BuildModeController.EffectiveCostFor(e);
                    walkTotal += Total(c);
                    if (!c.IsZero)
                        failures.Add($"[softcap] founding step '{id}' costs w{c.wood}/f{c.food}/i{c.iron}/c{c.crystals} " +
                                     "on a fresh 0-wood/0-iron save -- the founding sequence SOFT-LOCKS");
                    throwaway.FreeBuildsUsed.Add(id);
                }
                if (walkTotal == 0)
                    log.AppendLine($"  [softcap] founding walk ({string.Join(" -> ", foundingWalk)}) totals 0 on a fresh save OK");
            }
            catch (System.Exception ex)
            {
                failures.Add($"[softcap] freebie gate threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                instField.SetValue(null, priorInstance);
            }
        }

        // =====================================================================
        //  11c. WO-855 PHASE 4 -- BUILD TIER IS NO LONGER A CONSTANT.
        //  ---------------------------------------------------------------------
        //  Phase 0 measured BuildModeController passing a hard-coded literal 0 as the
        //  tier to BuildTimerService.StartBuild, so EVERY structure in the game built
        //  in exactly baseBuildSeconds and BuildTimerConfig.tierGrowth was unreachable
        //  dead tuning. The FIRST gate below FAILS against the pre-fix tree by design.
        // =====================================================================
        private static void CheckBuildTierDerivation(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            // -- A. THE CALL SITE -- this gate is RED on the pre-WO-855 tree -------
            string bmcPath = System.IO.Path.Combine(Application.dataPath,
                "_Modules", "Village", "BuildMode", "BuildModeController.cs");
            if (!System.IO.File.Exists(bmcPath))
            {
                failures.Add("[build-tier] BuildModeController.cs not found -- the hard-coded-tier gate cannot run");
            }
            else
            {
                string src;
                try { src = System.IO.File.ReadAllText(bmcPath); }
                catch (System.Exception ex) { src = null; failures.Add($"[build-tier] BuildModeController.cs unreadable ({ex.Message})"); }
                if (src != null)
                {
                    if (src.IndexOf("StartBuild(jobKey, 0)", System.StringComparison.Ordinal) >= 0)
                        failures.Add("[build-tier] BuildModeController still calls StartBuild(jobKey, 0) -- the HARD-CODED tier is back, " +
                                     "so every structure in the game builds in exactly baseBuildSeconds and tierGrowth is dead tuning");
                    if (src.IndexOf("TierForCost(", System.StringComparison.Ordinal) < 0)
                        failures.Add("[build-tier] BuildModeController does not call BuildTimerConfig.TierForCost -- the placement path " +
                                     "is not deriving a real build tier");
                    else
                        log.AppendLine("  [build-tier] placement path derives its tier via TierForCost (no hard-coded 0) OK");
                }
            }

            // Same resolve BuildTimerService performs (authored asset, else code default).
            // Explicit null test, NOT ??, so Unity's fake-null can never slip a dead object through.
            var cfg = Resources.Load<BuildTimerConfig>(BuildTimerConfig.ResourcesPath);
            if (cfg == null) cfg = BuildTimerConfig.CreateDefault();

            // -- B. THE BANDS -- ascending, positive, and actually reachable -------
            var bands = cfg.tierCostThresholds;
            if (bands == null || bands.Length == 0)
            {
                failures.Add("[build-tier] tierCostThresholds is empty -- every structure collapses back to tier 0 (a flat timer)");
                return;
            }
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i] <= 0f)
                    failures.Add($"[build-tier] tierCostThresholds[{i}] = {bands[i]} <= 0 -- a non-positive band makes the tier meaningless");
                if (i > 0 && bands[i] <= bands[i - 1])
                    failures.Add($"[build-tier] tierCostThresholds not ascending: [{i}] {bands[i]} <= [{i - 1}] {bands[i - 1]}");
            }

            // -- C. TWO REAL CATALOG ROWS PRODUCE TWO DIFFERENT DURATIONS --------
            //      (the whole point of the fix -- pre-WO-855 every row produced 15s).
            var tierHistogram = new Dictionary<int, int>();
            var byTier = new Dictionary<int, string>();
            foreach (var e in entries)
            {
                if (e == null || e.repo == null) continue;
                CoreCost c = BuildModeController.CostFor(e);
                if (c.IsZero) continue;
                int t = cfg.TierForCost(c);
                tierHistogram.TryGetValue(t, out int n);
                tierHistogram[t] = n + 1;
                if (!byTier.ContainsKey(t)) byTier[t] = e.id;
            }
            if (tierHistogram.Count < 2)
            {
                failures.Add($"[build-tier] every priced catalog row resolves the SAME tier ({tierHistogram.Count} distinct) -- " +
                             "build duration is still a constant; the cost bands do not split the catalog");
            }
            else
            {
                var tiersSorted = new List<int>(byTier.Keys);
                tiersSorted.Sort();
                int lo = tiersSorted[0], hi = tiersSorted[tiersSorted.Count - 1];
                float dLo = cfg.DurationSecondsForTier(lo, BuildJobKind.Build);
                float dHi = cfg.DurationSecondsForTier(hi, BuildJobKind.Build);
                if (dHi <= dLo)
                    failures.Add($"[build-tier] '{byTier[hi]}' (tier {hi}, {dHi}s) does not build LONGER than '{byTier[lo]}' (tier {lo}, {dLo}s)");
                else
                    log.AppendLine($"  [build-tier] '{byTier[lo]}' tier {lo} = {dLo:0}s vs '{byTier[hi]}' tier {hi} = {dHi:0}s -- " +
                                   "two rows, two durations OK");
                var histo = new List<string>();
                foreach (var t in tiersSorted)
                    histo.Add($"t{t}x{tierHistogram[t]}({cfg.DurationSecondsForTier(t, BuildJobKind.Build):0}s)");
                log.AppendLine("  [build-tier] catalog tier histogram: " + string.Join(" ", histo));
            }

            // -- D. MOBILE SHAPE -- snappy early, long endgame (WO-855 sec.4.6) ----
            float tier0 = cfg.DurationSecondsForTier(0, BuildJobKind.Build);
            if (tier0 > 180f)
                failures.Add($"[build-tier] tier-0 build is {tier0}s -- the first 10 minutes of a new save would be gated " +
                             "(owner: early builds stay snappy)");
            int top = cfg.MaxReachableTier;
            float topSec = cfg.DurationSecondsForTier(top, BuildJobKind.Build);
            if (topSec < 30f * 60f)
                failures.Add($"[build-tier] the top reachable tier ({top}) builds in {topSec}s -- the endgame has no wall-clock drag " +
                             "(owner: endgame long, hours)");
            log.AppendLine($"  [build-tier] shape: tier0 {tier0:0}s .. top reachable tier {top} {topSec / 3600f:0.##}h " +
                           $"(base {cfg.baseBuildSeconds}s, growth x{cfg.tierGrowth}, upgradeMult x{cfg.upgradeMultiplier}, slots {cfg.freeBuildSlots})");

            // -- E. THE CLAMP HOLDS AT (AND PAST) THE HIGHEST REACHABLE TIER -----
            //      A builder-queue job that becomes reachable at high tiers must still
            //      respect maxDurationSeconds -- including the upgrade multiplier and a
            //      few tiers of headroom past the top band.
            for (int t = top; t <= top + 3; t++)
            {
                float b = cfg.DurationSecondsForTier(t, BuildJobKind.Build);
                float u = cfg.DurationSecondsForTier(t, BuildJobKind.Upgrade);
                if (b > cfg.maxDurationSeconds + 0.01f)
                    failures.Add($"[build-tier] BUILD duration at tier {t} = {b}s exceeds maxDurationSeconds {cfg.maxDurationSeconds}s");
                if (u > cfg.maxDurationSeconds + 0.01f)
                    failures.Add($"[build-tier] UPGRADE duration at tier {t} = {u}s exceeds maxDurationSeconds {cfg.maxDurationSeconds}s " +
                                 "(the upgradeMultiplier escapes the clamp)");
            }
            if (cfg.freeBuildSlots != 2)
                failures.Add($"[build-tier] freeBuildSlots is {cfg.freeBuildSlots} -- WO-855 sec.4.6 keeps it at 2 (scarcity)");
            log.AppendLine($"  [build-tier] maxDurationSeconds {cfg.maxDurationSeconds / 3600f:0.#}h clamp holds through tier {top + 3} OK");
        }

        // =====================================================================
        //  12. FALLBACK / CATALOG PARITY
        //  ---------------------------------------------------------------------
        //  CatalogBootstrap.RegisterFallback is the JSON-load-FAILURE path: when
        //  structures-catalog.json cannot be read, its hardcoded rows ARE the game's
        //  content. Nothing asserted that those rows matched the catalog they mirror,
        //  so they drifted silently and REPEATEDLY:
        //    - placement.footprint 2.5 vs the catalog's 1.75   (fixed 0ac59581)
        //    - visualPrefabPath "PatriciaLight/tower2" -- art from the Defend-the-Tower
        //      module REMOVED 2026-06-09 -- on two of the three rows
        //    - displayName "Wizard Tower" vs "Ballista", damage 20 vs 30, mustSitOn
        //      WallWalk vs Ground, and every cost/upgrade/visual-tier field simply absent
        //  A player who hits the failure path would get a different-looking, differently
        //  priced, differently placeable town and nobody would know.
        //
        //  METHOD: this is a REAL-OBJECT comparison, not a source-text lint. It invokes
        //  the private RegisterFallback through reflection against a cleared registry,
        //  snapshots the CatalogEntry objects it actually constructs, restores the
        //  registry, then walks EVERY public field of CatalogEntry / RepoProps /
        //  PlacementRules / OrientationFix (and their arrays + nested structs) by
        //  reflection against the parsed catalog row. Reflection over the field graph --
        //  rather than a fixed field list -- means a field added to RepoProps tomorrow is
        //  covered the day it lands, with no edit here.
        //
        //  BLIND SPOTS (deliberate, stated):
        //    - OrientationFix.note is EXCLUDED: it is human annotation, not behaviour.
        //    - JSON keys with no C# field (the catalog's "_bug22" comment key and its
        //      stray top-level "canHitAir") are invisible to both sides -- the production
        //      parse drops them via MissingMemberHandling.Ignore, so parity here matches
        //      what the game actually loads, which is the property that matters.
        //    - A fallback row whose id is absent from the catalog is FAILED, not deleted.
        // =====================================================================
        private static readonly HashSet<string> FallbackParityIgnoredFields =
            new HashSet<string> { "note" };

        private static void CheckFallbackParity(List<CatalogEntry> entries, List<string> failures, StringBuilder log)
        {
            var method = typeof(CatalogBootstrap).GetMethod(
                "RegisterFallback", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                failures.Add("[fallback-parity] CatalogBootstrap.RegisterFallback is not reflectable (renamed/removed) -- " +
                             "the JSON-failure path is UNGUARDED and free to drift from the catalog again");
                return;
            }

            // Snapshot + restore the live registry: earlier gates hydrated it and later
            // suites read it, so this gate must leave it byte-for-byte as it found it.
            var snapshot = new List<CatalogEntry>(CatalogRegistry.All());
            List<CatalogEntry> fallbackRows = null;
            try
            {
                CatalogRegistry.Clear();
                method.Invoke(null, null);
                fallbackRows = new List<CatalogEntry>(CatalogRegistry.All());
            }
            catch (System.Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                failures.Add($"[fallback-parity] RegisterFallback threw: {inner.GetType().Name}: {inner.Message}");
            }
            finally
            {
                CatalogRegistry.Clear();
                foreach (var e in snapshot) CatalogRegistry.Register(e);
            }

            if (fallbackRows == null) return;
            if (fallbackRows.Count == 0)
            {
                failures.Add("[fallback-parity] RegisterFallback registered ZERO entries -- " +
                             "a JSON load failure would leave the build palette EMPTY");
                return;
            }

            var byId = new Dictionary<string, CatalogEntry>();
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.id)) byId[e.id] = e;

            int before = failures.Count;
            foreach (var fb in fallbackRows)
            {
                if (fb == null || string.IsNullOrEmpty(fb.id))
                {
                    failures.Add("[fallback-parity] RegisterFallback registered a null / id-less entry");
                    continue;
                }
                if (!byId.TryGetValue(fb.id, out var cat))
                {
                    failures.Add($"[fallback-parity] fallback row '{fb.id}' has NO counterpart in structures-catalog.json -- " +
                                 "the failure path would offer a structure the loaded game does not have");
                    continue;
                }
                CompareFallbackValue(fb.id, "", fb, cat, failures, 0);
            }

            int drift = failures.Count - before;
            if (drift == 0)
                log.AppendLine($"  [fallback-parity] all {fallbackRows.Count} RegisterFallback row(s) field-equal to their " +
                               "structures-catalog.json counterparts OK");
            else
                log.AppendLine($"  [fallback-parity] {drift} field divergence(s) across {fallbackRows.Count} fallback row(s)");
        }

        /// <summary>
        /// Reflective deep field-compare of a fallback value against its catalog value.
        /// Reports id + dotted field path + BOTH values on every divergence.
        /// </summary>
        private static void CompareFallbackValue(string id, string path, object fb, object cat,
                                                 List<string> failures, int depth)
        {
            if (depth > 6) return;   // the CatalogEntry graph is 4 deep; the cap is a cycle guard

            if (fb == null && cat == null) return;
            if (fb == null || cat == null)
            {
                failures.Add($"[fallback-parity] '{id}' {path}: fallback {FmtParity(fb)} vs catalog {FmtParity(cat)}");
                return;
            }

            var t = fb.GetType();
            if (t != cat.GetType())
            {
                failures.Add($"[fallback-parity] '{id}' {path}: type mismatch ({t.Name} vs {cat.GetType().Name})");
                return;
            }

            if (t == typeof(float) || t == typeof(double))
            {
                double a = System.Convert.ToDouble(fb), b = System.Convert.ToDouble(cat);
                if (System.Math.Abs(a - b) > 0.0001)
                    failures.Add($"[fallback-parity] '{id}' {path}: fallback {a} vs catalog {b}");
                return;
            }

            if (t.IsPrimitive || t.IsEnum || t == typeof(string))
            {
                if (!fb.Equals(cat))
                    failures.Add($"[fallback-parity] '{id}' {path}: fallback {FmtParity(fb)} vs catalog {FmtParity(cat)}");
                return;
            }

            if (t.IsArray)
            {
                var a = (System.Array)fb;
                var b = (System.Array)cat;
                if (a.Length != b.Length)
                {
                    failures.Add($"[fallback-parity] '{id}' {path}: fallback has {a.Length} element(s), catalog has {b.Length}");
                    return;
                }
                for (int i = 0; i < a.Length; i++)
                    CompareFallbackValue(id, $"{path}[{i}]", a.GetValue(i), b.GetValue(i), failures, depth + 1);
                return;
            }

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (FallbackParityIgnoredFields.Contains(f.Name)) continue;
                string child = path.Length == 0 ? f.Name : path + "." + f.Name;
                CompareFallbackValue(id, child, f.GetValue(fb), f.GetValue(cat), failures, depth + 1);
            }
        }

        private static string FmtParity(object v) =>
            v == null ? "<null>" : (v is string s ? $"\"{s}\"" : v.ToString());

        /// <summary>Flat basket total of a cost (all four slots) -- the comparison scalar for the softcap gates.</summary>
        private static int Total(CoreCost c) => c.wood + c.food + c.iron + c.crystals;

        // =====================================================================
        //  Verdict + markers
        // =====================================================================
        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "BUILD ECONOMY OK — catalog parse/ids + dual-copy + cost sanity + tier-monotonic upgrades " +
                         "+ tower contract + placement math + 50% sell refund + BaseLayout replay (data + real factory) " +
                         "+ build-timer curve + damage-states thresholds + fallback/catalog parity all hold";
                Debug.Log("BUILDECON_OK\n" + log);
                return true;
            }
            reason = $"BUILD ECONOMY: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError($"BUILDECON_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
