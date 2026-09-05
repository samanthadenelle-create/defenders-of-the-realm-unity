// =============================================================================
// ResourceBuildingHarvester — the runtime harvest tick that makes the resource-
// building UPGRADE LADDER actually pay out (T-025). WO-230 follow-up.
// -----------------------------------------------------------------------------
// Before this, ResourceBuildingProgression's per-level YieldPerTick was a phantom
// number: the upgrade panel SHOWED it, but nothing in the world ticked it — the
// only in-world income came from the separate HarvestSite / Outpost world-claim
// systems (their own BaseYield/HarvestInterval). So upgrading a Farm/Lumbermill/
// Forge changed a label but not the player's actual income.
//
// This driver closes that loop ENTIRELY WITHIN the upgrade silo: it owns ONE
// per-building cooldown for Farm / Lumbermill / Forge, reads the live level's
// HarvestInterval (the SPEED upgrade axis) + effective yield (YieldPerTick ×
// YieldSizeMultiplier, the SIZE axis), and credits the harvestable through the
// ResourceCollector pending buffer (CoC spine WO-663): ticks accrue into Pending on
// each collector; the player Collect All at Heart banks into the wallet. Upgrading
// a building now visibly ticks FASTER and fills the bubble MORE.
//
// SCOPE / CROSS-SILO: this references only the silo's own data (Progression +
// ResourceLedger, both DeNelle.Village -> DeNelle.Core legal). It does NOT touch
// HarvestSite, Outpost, EconomyService, WaveManager, or any scene builder. It is
// auto-spawned by BuildingUpgradePanelBootstrap (same lifetime as the panel), so
// no scene-file edit is needed. A single global cadence is intentional: the three
// resource buildings are global upgradeable economy nodes (CoC-style), not placed
// world props — matching how the panel already treats them by id.
//
// -----------------------------------------------------------------------------
// PHANTOM COLLECTOR INCOME - the 2026-08-04 correctness gate.
// -----------------------------------------------------------------------------
// MEASURED DEFECT: Update() iterated ALL THREE OrderedIds unconditionally. The
// only guard was `GetLevel(id) < 1`, and ResourceBuildingState.GetLevel is
// `PlayerPrefs.GetInt(key, 1)` clamped to >= 1 (ResourceBuildingState.cs:63-69) -
// it DEFAULTS TO 1 and never asks whether the building exists, so `level < 1` is
// unreachable. An EMPTY town therefore earned farm + lumbermill + forge income
// from t=0, and because no ResourceCollector was registered it fell through to the
// direct-grant branch below: UNCAPPED and AUTO-BANKED, bypassing the capped
// pending pool, the manual Collect tap and the siege-loot risk that are the whole
// CoC collector design. Post-WO-855 that free baseline is ~720 wood + 936 food +
// 432 iron per hour for a town with nothing in it. PLACING the collector made
// income strictly WORSE.
//
// THE GATE: a per-id tick now requires the building to ACTUALLY EXIST, proven by
// the persisted WO-834 ledger `GameState.EverBuiltStructureIds` (save v36, written
// at the placement commit seam BuildModeController.cs:1892 and by the
// StrategicPlacementMigration template grant, StrategicPlacementMigration.cs:340-343)
// OR by a live registered ResourceCollector. The rule is a PURE static
// (<see cref="MayHarvest"/>) exactly like WO-834's StructureSingleton
// .MayBakedTwinSurface, so it is decidable headless without a scene.
//
// THE DIRECT-GRANT FALLBACK IS REMOVED (not merely existence-gated). Ruling:
//   1. `EverBuiltStructureIds` is MONOTONIC by design (GameState.cs:518-521,
//      WO-843) - selling or losing a collector never removes the id. An
//      existence-gated direct grant would therefore keep PAYING for a SOLD or
//      SIEGE-DESTROYED collector, contradicting WO-753 ("destroyed = build fresh
//      at full cost"). Only a LIVE registered collector proves "standing".
//   2. Its stated purpose ("collector not wired yet") no longer exists: a resource
//      building comes into being ONLY by placement, and placement attaches the
//      ResourceCollector in the same breath (StructureFactory.cs:744-751).
//   3. The harvester bootstraps in EVERY non-enemy gameplay scene
//      (BuildingUpgradePanelMvvmBootstrap.cs:75), so the fallback also auto-banked
//      full town income while the player was off in a dungeon.
//   4. A standing building whose collector component is missing is a WIRING BUG,
//      not a balance case. Paying money to paper over it is the silent failure
//      CLAUDE.md sec.12 forbids - it is now a throttled FlowTrace.Warn instead.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// Drives the per-level auto-harvest tick for the three resource buildings,
    /// consuming the upgrade ladder's HarvestInterval (speed) + effective yield
    /// (size). Self-contained MonoBehaviour; one instance, auto-spawned with the
    /// upgrade panel. A tick pays out ONLY into the live collector's capped pending
    /// pool (<c>ResourceCollector.Accrue</c>) and only for a building the existence
    /// gate (<see cref="MayHarvest"/>) proves was actually built.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBuildingHarvester : MonoBehaviour
    {
        public static ResourceBuildingHarvester Instance { get; private set; }

        // Per-building elapsed time toward its current interval, parallel to
        // ResourceBuildingProgression.OrderedIds.
        private float[] _elapsed;

        // Last observed existence-gate verdict per id, parallel to OrderedIds.
        // -1 = not yet evaluated, 0 = closed (never built), 1 = open. EDGE-logged
        // only (the ResourceCollector._lastLoggedAccrualScale pattern) so a capture
        // shows the gate flipping without a per-frame spam wall.
        private int[] _lastGate;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            int n = ResourceBuildingProgression.OrderedIds.Length;
            _elapsed = new float[n];
            _lastGate = new int[n];
            for (int i = 0; i < n; i++) _lastGate[i] = -1;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        //  WO-1414 B -- NEW GAME: the LIVE half of the harvest tick's ledger view
        // ---------------------------------------------------------------------
        //  THE PERSISTED HALF IS ALREADY RIGHT and was before this change: ResetToNewGame
        //  assigns EverBuiltStructureIds = new List<string>() (GameStateService.cs:1277), and
        //  EverBuiltIds() below re-reads that list from the live state on EVERY tick -- no
        //  snapshot is cached -- so a fresh town's existence gate is CLOSED for every id.
        //
        //  WHAT SURVIVED A NEW GAME IS THIS COMPONENT'S OWN PER-ID STATE. The harvester is
        //  DDOL-lifetime beside the upgrade panel, so across "Start New" it carried:
        //    * _elapsed[i] -- and WO-1208 deliberately makes that an OWED interval when a
        //      tick finds no live collector ("HELD, not lost"). An owed tick banked under the
        //      PREVIOUS town would be paid out the instant the new town places the same id.
        //    * _lastGate[i] -- the last gate verdict, which is what suppresses the edge trace.
        //      Carrying it means the new game's FIRST gate evaluation prints nothing, so the
        //      one line that would prove what the ledger held on a fresh town is missing from
        //      the capture. That is the line WO-1414 B needed and did not have.
        //  Cleared here, with the ledger named out loud, so the next capture answers it.
        //
        //  Static handler on a static event, installed BeforeSceneLoad: the same shape and the
        //  same reason as ResourceCollector.InstallNewGameHook (WO-1371) -- the hook must exist
        //  whether or not a harvester happens to be alive when START NEW is pressed.
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallNewGameHook()
        {
            DeNelle.Core.State.GameStateService.NewGameStarted -= OnNewGameStarted;
            DeNelle.Core.State.GameStateService.NewGameStarted += OnNewGameStarted;
            DeNelle.Core.Diagnostics.FlowTrace.Step("Harvest",
                "harvester New Game hook installed - a reset now clears the tick's own per-id state " +
                "as well as the persisted ever-built ledger (WO-1414 B).");
        }

        private static void OnNewGameStarted()
        {
            var inst = Instance;
            int n = inst != null && inst._elapsed != null ? inst._elapsed.Length : 0;
            for (int i = 0; i < n; i++)
            {
                inst._elapsed[i] = 0f;
                inst._lastGate[i] = -1;   // force a fresh edge trace on the new town's first evaluation
            }
            DeNelle.Core.Diagnostics.FlowTrace.Step("Harvest",
                $"New Game: cleared {n} per-id harvest tick slot(s) (owed intervals + cached gate verdicts). " +
                $"The persisted ever-built ledger now reads [{EverBuiltJoined()}] - a fresh town must show " +
                "<empty> here, and every existence gate must evaluate CLOSED, so no id can print the " +
                "'in the ever-built ledger but NO ResourceCollector' HELD line (WO-1414 B).");
        }

        private void Update()
        {
            // No service yet (Title / HeroSelect) → nothing to credit; skip cheaply.
            var ids = ResourceBuildingProgression.OrderedIds;
            if (_elapsed == null || _elapsed.Length != ids.Length) return;
            if (_lastGate == null || _lastGate.Length != ids.Length) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                var def = ResourceBuildingProgression.Find(id);
                if (def == null) continue;

                // -- EXISTENCE GATE (2026-08-04, phantom collector income) ----------
                // BEFORE the level read, because GetLevel DEFAULTS TO 1 and can never
                // report "this building does not exist" (ResourceBuildingState.cs:63-69).
                // A never-built id accrues NOTHING - not even elapsed time, so a town
                // founded an hour into the session cannot bank a phantom backlog.
                var collector = ResourceCollectorRegistry.Get(id);
                bool gateOpen = MayHarvest(CatalogIdsForBuilding(id), EverBuiltIds(), collector != null);
                if (_lastGate[i] != (gateOpen ? 1 : 0))
                {
                    _lastGate[i] = gateOpen ? 1 : 0;
                    DeNelle.Core.Diagnostics.FlowTrace.Step("Harvest",
                        $"existence gate {(gateOpen ? "OPEN" : "CLOSED")} for '{id}' " +
                        $"(liveCollector={(collector != null ? "yes" : "no")}, everBuilt=[{EverBuiltJoined()}]) - " +
                        (gateOpen ? "this id may tick" : "NEVER BUILT, so it earns nothing (phantom-income gate)"));
                }
                if (!gateOpen)
                {
                    _elapsed[i] = 0f;   // no banked backlog for a building that does not exist
                    continue;
                }

                // SUPERSEDED 2026-07-13 evening (owner "agree"): LEVEL 1 PRODUCES —
                // CoC-style, a placed collector earns from the moment it stands
                // (level-1 interval/yield from the existing tables: LevelDef(1), no
                // step bonus); upgrades multiply. The old owner-2026-06-14 rule
                // ("level > 1 only, manual farming funds the first upgrade") is dead:
                // with the free-first-build flags + ZERO founding seed there is no
                // other baseline income, and level-1-pays-nothing would deadlock the
                // bootstrap. Defensive < 1 only (GetLevel never returns below 1).
                int level = ResourceBuildingState.GetLevel(id);
                if (level < 1) continue;
                if (level == 1)
                    DeNelle.Core.Diagnostics.FlowTrace.Once("Harvest", "level1-accrual-" + id,
                        $"level-1 '{id}' is ACCRUING from placement (owner 2026-07-13: level 1 produces; zero-seed bootstrap income live).");

                float interval = ResourceBuildingState.CurrentHarvestInterval(id);
                _elapsed[i] += dt;
                if (_elapsed[i] < interval) continue;

                // Roll over (carry the remainder so faster tiers stay accurate).
                _elapsed[i] -= interval;

                // ONE authority for "what does one tick of this building pay" - shared with the
                // WO-859 offline/away catch-up (ResourceCollector.AwayAmount). The multiplier
                // stack lives in EffectiveYieldPerTick and is deliberately NOT re-implemented
                // anywhere else, so the away path can never drift from the online path.
                int amount = EffectiveYieldPerTick(id);
                if (amount <= 0) continue;

                // THE ONLY PAYOUT PATH: the live collector's capped pending pool
                // (WO-663 CoC spine). The player banks it with a Collect tap; a siege
                // can steal what is uncollected. The old uncapped auto-banking direct
                // grant that used to live here is REMOVED - see the file header for the
                // ruling (monotonic ledger cannot see a sold/destroyed collector; a
                // missing component is a wiring bug, not an income case).
                if (collector != null)
                {
                    collector.Accrue(amount);
                    continue;
                }

                // ⛔ HOLD THE TICK, DO NOT BURN IT (WO-1208, owner device 2026-08-25).
                // The rollover above already CONSUMED this interval, so before this change a tick
                // with no live collector computed its payout and threw it away - permanently. That
                // is not a rare wiring case: the collector belongs to the town's PLACED structure
                // and unregisters while the player is in a dungeon, so the DDOL harvester kept
                // ticking over an absent collector and quietly destroyed the income. Captured on
                // device, the registry flapping across one session:
                //   19:12:31 existence gate OPEN for 'farm' (liveCollector=yes)
                //   19:22:31 'farm' ... NO ResourceCollector is registered - 13 WITHHELD this tick
                //   19:29:25 existence gate OPEN for 'farm' (liveCollector=yes)
                // Restoring the interval makes the tick OWED instead of LOST: it pays once the
                // collector comes back.
                //
                // CLAMPED TO EXACTLY ONE INTERVAL on purpose. The ever-built ledger is monotonic and
                // cannot see a SOLD or destroyed collector (this file's own header ruling), so an
                // uncapped hold would bank a limitless backlog for a building that no longer stands.
                // One owed tick is the most a returning collector can ever be handed.
                _elapsed[i] = Mathf.Min(_elapsed[i] + interval, interval);

                DeNelle.Core.Diagnostics.FlowTrace.Throttle("Harvest", "no-live-collector-" + id, 10f,
                    $"'{id}' is in the ever-built ledger but NO ResourceCollector is registered - " +
                    $"{amount} {def.Yields} HELD (not lost) this tick; the interval is preserved and " +
                    "pays when a collector registers. A standing building with no collector component " +
                    "is still a wiring bug; income is never granted straight to the wallet.");
            }
        }

        // =====================================================================
        //  THE RATE AUTHORITY (WO-859 - shared by the online tick and the away catch-up)
        // =====================================================================

        /// <summary>
        /// WO-859 - the units ONE harvest tick of <paramref name="buildingId"/> pays, with the full
        /// multiplier stack applied, in the order it has always been applied:
        /// <list type="number">
        /// <item>the level's effective yield (YieldPerTick x YieldSizeMultiplier x the WO-430
        /// production perk) - <see cref="ResourceBuildingState.CurrentEffectiveYield"/>;</item>
        /// <item>the WO-709 echo-count GLOBAL harvest multiplier;</item>
        /// <item>the WO-676 STEWARD <c>harvestRate</c> talent.</item>
        /// </list>
        /// <para>
        /// EXTRACTED FROM <see cref="Update"/> UNCHANGED. Its whole reason to exist is that
        /// <see cref="ResourceCollector"/>'s away/offline catch-up needs the SAME number: an offline
        /// path that re-derived the stack would silently diverge from the online one the first time
        /// either was retuned, and the player would be paid a different rate for being away than for
        /// standing there. One authority per concern.
        /// </para>
        /// Null-safe throughout (no EchoService => x1, no talent tree => +0), so the value is
        /// byte-identical to the pre-extraction baseline. 0 for an unknown id.
        /// </summary>
        public static int EffectiveYieldPerTick(string buildingId)
        {
            int amount = ResourceBuildingState.CurrentEffectiveYield(buildingId);
            if (amount <= 0) return 0;

            // WO-709 (owner curve ruling 2026-07-13, quadratic-total): the echo-count GLOBAL
            // harvest multiplier applies to ALL harvest income - collectors included - through
            // this one read at the accrual choke point. 1 echo = x1 (baseline unchanged); each
            // new echo amps the entire operation.
            double echoMult = EchoHarvestMultiplier();
            if (echoMult > 1.0)
            {
                amount = Mathf.RoundToInt(amount * (float)echoMult);
                DeNelle.Core.Diagnostics.FlowTrace.Once("Harvest", "echo-global-mult",
                    $"WO-709 global harvest multiplier x{echoMult:0.#} applied to collector ticks (echoes amp the whole operation).");
            }

            // WO-676 STEWARD (Provider's Bond): `harvestRate` scales the per-tick yield. StatSum
            // is internally null-safe (no service/tree/nodes => 0), so the yield is byte-identical
            // to baseline at sum 0. NOTE: this term is deliberately ABSENT from the CAPACITY basis
            // (ResourceCollector.ThroughputScale) - capacity is `collectorCap`'s seam, not this
            // one, matching the identical ruling on the Echo silo.
            float rateBonus = DeNelle.Village.Talents.HeroTalentModifiers.StatSum(
                DeNelle.Village.HeroTalentClassReader.Slug(), "harvestRate");
            if (rateBonus > 0f)
            {
                amount = Mathf.Max(amount, Mathf.RoundToInt(amount * (1f + rateBonus)));
                DeNelle.Core.Diagnostics.FlowTrace.Once("Talent", "building-harvestRate",
                    $"harvestRate x{1f + rateBonus:0.###} applied to resource-building tick (WO-676 Provider's Bond).");
            }

            return amount;
        }

        /// <summary>
        /// The WO-709 echo-count global harvest multiplier, or 1.0 when there is no EchoService.
        /// One read point shared by <see cref="EffectiveYieldPerTick"/> (the RATE) and
        /// <see cref="ResourceCollector"/>'s capacity scale (the CAP), so the two can never
        /// disagree about how much an echo is worth - which is precisely the divergence that made
        /// a 6-echo collector fill in minutes while its cap stayed on the one-echo basis.
        /// </summary>
        public static double EchoHarvestMultiplier()
        {
            var echoSvc = EchoService.Instance;
            if (echoSvc == null) return 1.0;
            double m = echoSvc.GlobalHarvestMultiplier;
            return m > 1.0 ? m : 1.0;
        }

        // =====================================================================
        //  THE EXISTENCE RULE (pure - WO-834 MayBakedTwinSurface pattern)
        // =====================================================================

        /// <summary>
        /// PURE existence rule for one resource building's harvest tick. True when the
        /// building may produce at all:
        /// <list type="bullet">
        /// <item>a LIVE registered <see cref="ResourceCollector"/> is standing for it
        /// (the strongest possible proof - it exists right now); or</item>
        /// <item>one of its collector CATALOG ids is in the persisted WO-834
        /// <c>GameState.EverBuiltStructureIds</c> ledger (OrdinalIgnoreCase).</item>
        /// </list>
        /// Everything else - an empty town, a building the player has never placed - is
        /// FALSE and earns nothing. No world/service reads, so the check-in oracle can
        /// pin the truth table headless.
        /// </summary>
        public static bool MayHarvest(IReadOnlyList<string> collectorCatalogIds,
                                      IReadOnlyList<string> everBuiltIds,
                                      bool liveCollectorPresent)
        {
            if (liveCollectorPresent) return true;
            if (collectorCatalogIds == null || everBuiltIds == null) return false;
            for (int i = 0; i < collectorCatalogIds.Count; i++)
            {
                string want = collectorCatalogIds[i];
                if (string.IsNullOrEmpty(want)) continue;
                for (int j = 0; j < everBuiltIds.Count; j++)
                    if (string.Equals(everBuiltIds[j], want, System.StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            return false;
        }

        /// <summary>
        /// The COLLECTOR catalog ids that stand for a bare progression building id
        /// ("farm" -> "collector_farm"). Resolved from the catalog's
        /// <c>repo.collectorBuildingId</c> across the Collector type - the same
        /// resolution StructureFactory / ResourceCollector.CatalogCapacity use, so the
        /// gate agrees with placement.
        /// <para>
        /// The bare id is deliberately NOT a candidate: `lumbermill` / `forge` also
        /// exist as separate GameplayBuilding storefront rows in structures-catalog.json,
        /// so accepting the bare id would let building the Forge STOREFRONT open the
        /// forge COLLECTOR's harvest gate. When the registry is empty (catalog not
        /// loaded yet) we fall back to the "collector_" naming convention rather than
        /// stranding a genuinely-placed collector.
        /// </para>
        /// </summary>
        public static List<string> CatalogIdsForBuilding(string buildingId)
        {
            var result = new List<string>(2);
            if (string.IsNullOrEmpty(buildingId)) return result;

            foreach (var e in DeNelle.Core.Catalog.CatalogRegistry.OfType(
                         DeNelle.Core.Catalog.CatalogType.Collector))
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                string bid = (e.repo != null && !string.IsNullOrEmpty(e.repo.collectorBuildingId))
                    ? e.repo.collectorBuildingId : e.id;
                if (string.Equals(bid, buildingId, System.StringComparison.OrdinalIgnoreCase)
                    && !result.Contains(e.id))
                    result.Add(e.id);
            }

            // WO-1163: a faucet may declare OTHER structures that also open its gate
            // (repo.satisfiedByStructureIds). Owner ruling 2026-08-23: iron is the
            // ARMORER's resource, so owning the Armorer pays iron. Authored in the
            // catalog, never branched on here - which building pays what is DATA.
            foreach (var e in DeNelle.Core.Catalog.CatalogRegistry.OfType(
                         DeNelle.Core.Catalog.CatalogType.Collector))
            {
                if (e == null || e.repo == null || e.repo.satisfiedByStructureIds == null) continue;
                string bid = !string.IsNullOrEmpty(e.repo.collectorBuildingId)
                    ? e.repo.collectorBuildingId : e.id;
                if (!string.Equals(bid, buildingId, System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var extra in e.repo.satisfiedByStructureIds)
                    if (!string.IsNullOrEmpty(extra) && !result.Contains(extra))
                        result.Add(extra);
            }

            if (result.Count == 0) result.Add("collector_" + buildingId);   // catalog not loaded
            return result;
        }

        /// <summary>The persisted WO-834 ever-built ledger (empty when there is no state).</summary>
        private static IReadOnlyList<string> EverBuiltIds()
        {
            var s = DeNelle.Core.State.GameStateService.Instance?.State;
            return s?.EverBuiltStructureIds ?? (IReadOnlyList<string>)System.Array.Empty<string>();
        }

        /// <summary>Ledger contents for the edge-triggered gate trace (never per-frame).</summary>
        private static string EverBuiltJoined()
        {
            var ids = EverBuiltIds();
            return ids.Count == 0 ? "<empty>" : string.Join(",", ids);
        }
    }
}
