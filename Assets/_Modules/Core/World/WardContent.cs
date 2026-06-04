// =============================================================================
// WardContent — pure-data records for the ward-tether exploration loop (WO-112).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// The ward-stones are the exploration spine of Rung 3: the Keeper cannot leave the
// Heart for long without losing the bond, so the first Keepers planted ward-stones
// along the four marches to carry the Heart's song outward. Relight a march's
// ward-stone and you may walk that far — exploration earned one stone at a time.
//
// THIS FILE is the pure-data half (mirrors WorldContent.cs / WO-159+160):
//   • WardStoneDef   — authoring template for one ward (immutable design data):
//                      which march/region, how far out, the reach it grants, cost,
//                      and the optional node it claims when lit. Code-seeded by
//                      WardTetherService (no ScriptableObject asset / no scene wiring),
//                      exactly as TribeManager seeds TribeDef.
//   • WardStoneState — the per-player persisted record (lit or not). Rides inside
//                      GameState.Wards as a JsonUtility-safe list.
//   • WardReach      — a tiny pure helper computing the live per-march reach from the
//                      set of lit wards. Headless-safe (no UnityEngine, no scene) so
//                      ZoneManager-adjacent callers and the HUD readout can both read
//                      reach without a Village reference.
//
// PERSISTENCE (mirrors the ZoneState / TribeState / SettlementState precedent):
//   records are JsonUtility-safe — public fields, no dictionaries, enums as int —
//   and are APPENDED to GameState (GameState.Wards). They are NOT yet wired into
//   SaveSchema/SaveMigrator: the save owner adds the (de)serialisation + bumps the
//   schema version, exactly as the comment on GameState.Zones documents. Until then
//   lit wards live correctly in-memory across a session.
//
// All reach / cost numbers are DATA here — never hard-coded at a call site — so the
// danger⇄reward dial stays tunable alongside ZoneManager.ThreatLevel / WO-144/155.
// =============================================================================
using System;
using System.Collections.Generic;

namespace DeNelle.Core.World
{
    /// <summary>
    /// Authoring template for one ward-stone (WO-112). Immutable design data: where it
    /// sits on which march, how far out (order), the reach it grants when lit, what it
    /// costs to relight, and whether it gates a resource node. The mutable per-player
    /// record is <see cref="WardStoneState"/>. JsonUtility-safe.
    /// </summary>
    [Serializable]
    public class WardStoneDef
    {
        /// <summary>Stable ward id / save key (e.g. "ward_goldfields_1"). The WardStoneState key.</summary>
        public string Id = "";

        /// <summary>Region (march) this ward belongs to — the <see cref="RegionId"/> enum NAME.</summary>
        public string RegionKey = "";

        /// <summary>1 = closest to the gate, 2/3 = further out on this march. Drives cost + kindle scaling.</summary>
        public int Order = 1;

        /// <summary>World-space placement (the in-field ward position, well outside the walls).</summary>
        public WorldPoint Position = new WorldPoint();

        /// <summary>
        /// How far past the Heart the Keeper may range on this march once this ward is lit,
        /// measured as outward distance past the village wall edge (same metric as
        /// <see cref="ZoneManager.Depth"/>'s overrun). A march's reach is the MAX granted by
        /// its furthest lit ward.
        /// </summary>
        public float ReachRadiusGranted = 40f;

        // ── Relight cost (paid from GameState resources; rises with Order) ──
        /// <summary>Coin cost to relight (deducted from the wallet).</summary>
        public int CoinCost;
        /// <summary>Aether-crystal cost to relight.</summary>
        public int CrystalCost;

        /// <summary>
        /// Node-ward hook (WO-110/111): the MineNode/site id this ward CLAIMS when lit.
        /// Empty ⇒ a pure reach-extension ward (no node). When set, relighting flips the
        /// node's claimed gate so its settlement/harvest can begin — without duplicating
        /// node state.
        /// </summary>
        public string UnlocksNodeId = "";

        /// <summary>Bible-tone line shown on relight (flavour only).</summary>
        public string LitFlavor = "";

        public WardStoneDef() { }

        public WardStoneDef(string id, RegionId region, int order, WorldPoint pos,
                            float reachGranted, int coinCost, int crystalCost,
                            string unlocksNodeId = "", string litFlavor = "")
        {
            Id = id;
            RegionKey = region.ToString();
            Order = order;
            Position = pos;
            ReachRadiusGranted = reachGranted;
            CoinCost = coinCost;
            CrystalCost = crystalCost;
            UnlocksNodeId = unlocksNodeId ?? "";
            LitFlavor = litFlavor ?? "";
        }
    }

    /// <summary>
    /// The persisted, per-player record for a ward-stone (WO-112). Carries the one bit
    /// that matters across sessions — lit or not — plus its id/region/reach so the record
    /// is self-contained (reach can be summed without the def list). Seeded from a
    /// <see cref="WardStoneDef"/>. JsonUtility-safe — rides directly inside
    /// <c>GameState.Wards</c>.
    /// </summary>
    [Serializable]
    public class WardStoneState
    {
        /// <summary>Matches <see cref="WardStoneDef.Id"/>.</summary>
        public string Id = "";
        /// <summary>Region key — the <see cref="RegionId"/> enum NAME (survives enum renumber).</summary>
        public string RegionKey = "";
        /// <summary>Mirror of the def's granted reach (so reach can be computed from state alone).</summary>
        public float ReachRadiusGranted;
        /// <summary>True once the Keeper has relit this ward. The earned-progress bit.</summary>
        public bool Lit;

        public WardStoneState() { }

        public WardStoneState(WardStoneDef def)
        {
            if (def == null) return;
            Id = def.Id;
            RegionKey = def.RegionKey;
            ReachRadiusGranted = def.ReachRadiusGranted;
            Lit = false;
        }
    }

    /// <summary>
    /// Pure, headless reach math for the ward-tether (WO-112). Given the set of lit ward
    /// records, computes the per-march reach (the furthest lit ward's granted radius) and
    /// how far past that edge a position sits — the input to the "forgetting" effect.
    /// No UnityEngine / no scene dependency, so both the Village runtime and the HUD
    /// readout can read reach through one source of truth.
    /// </summary>
    public static class WardReach
    {
        /// <summary>
        /// Base reach (outward distance past the wall edge) the Heart's bare song grants on
        /// EVERY march with no wards lit — "the walls + a little." Keeps the Keeper able to
        /// step just outside the gate before the forgetting begins. Tunable.
        /// </summary>
        public const float BaseReach = 12f;

        /// <summary>
        /// The reach granted on <paramref name="region"/> right now = the MAX granted radius
        /// among LIT wards on that march, floored at <see cref="BaseReach"/>. Marches are
        /// independent (pushing east does not open north).
        /// </summary>
        public static float ReachForRegion(IReadOnlyList<WardStoneState> wards, RegionId region)
        {
            float reach = BaseReach;
            if (wards == null) return reach;
            string key = region.ToString();
            for (int i = 0; i < wards.Count; i++)
            {
                var w = wards[i];
                if (w == null || !w.Lit || w.RegionKey != key) continue;
                if (w.ReachRadiusGranted > reach) reach = w.ReachRadiusGranted;
            }
            return reach;
        }

        /// <summary>
        /// How far (metres) a position is PAST the furthest lit ward on its march — the
        /// forgetting input. 0 (or less) ⇒ inside reach, fully warm. &gt;0 ⇒ out past the
        /// song, the forgetting creeps in with distance. In the safe Village home zone this
        /// is always 0. Uses <see cref="ZoneManager.GetZone"/> + <see cref="ZoneManager.Depth"/>'s
        /// outward-overrun metric so reach and depth share one geometry.
        /// </summary>
        public static float DistancePastReach(IReadOnlyList<WardStoneState> wards,
                                              UnityEngine.Vector3 worldPos)
        {
            var region = ZoneManager.GetZone(worldPos);
            if (region == RegionId.Village) return 0f;

            float overrun = OverrunPastWall(worldPos);   // metres past the wall edge
            float reach = ReachForRegion(wards, region);
            return overrun - reach;
        }

        /// <summary>Outward distance past the village wall footprint along the dominant axis
        /// (mirrors ZoneManager.Depth's overrun, but in raw metres, not normalised).</summary>
        private static float OverrunPastWall(UnityEngine.Vector3 worldPos)
        {
            // Mirror ZoneManager's wall half-extents (kept in sync with that classifier).
            const float villageHalfX = 42f;
            const float villageHalfZ = 33f;
            float overX = UnityEngine.Mathf.Max(0f, UnityEngine.Mathf.Abs(worldPos.x) - villageHalfX);
            float overZ = UnityEngine.Mathf.Max(0f, UnityEngine.Mathf.Abs(worldPos.z) - villageHalfZ);
            return UnityEngine.Mathf.Max(overX, overZ);
        }
    }
}
