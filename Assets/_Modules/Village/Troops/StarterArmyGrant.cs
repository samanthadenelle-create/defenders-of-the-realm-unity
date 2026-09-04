// =============================================================================
// StarterArmyGrant - THE FIRST ARMY IS FREE (WO-1374, north-star map section 2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner, verbatim (docs/PROGRAM_RAID_ECONOMY_2026-09-04.md section 2):
//   "A player starts with 200 gold but needs 1,650 to participate in the thing
//    you're trying to teach them. That's basically putting a nightclub behind a
//    velvet rope and handing the player twelve cents."
//   "On Barracks completion, grant 3 free Footmen (a starter raid squad)."
//   "The first raid must happen within MINUTES of unlocking Barracks, not hours."
//   (S) "One raid teaches the entire economy."
//
// -----------------------------------------------------------------------------
// (S) WHY THIS IS SAFE TO BUILD WHILE WO-1374 IS BLOCKED.
// -----------------------------------------------------------------------------
// The work order is blocked on a fork the owner has not called: WO-1372 rules that
// troops cost TIME, the map prices three starters at 1,650 GOLD. This grant is
// CORRECT UNDER BOTH READINGS. Under the gold model it removes the 1,650-gold wall
// that stands between a new player and the loop the game is trying to teach; under
// the time model it is simply a fast start. Nothing here reads, spends or asserts
// a gold price, so neither ruling can make it wrong.
//
// -----------------------------------------------------------------------------
// WHY A POLLING BRIDGE AND NOT A HOOK ON THE BUILD JOB.
// -----------------------------------------------------------------------------
// "The player has a Barracks" is reachable by at least four different routes: the
// timed Builder job completing, the offline-fair sweep resolving it on launch, the
// strategic-placement migration granting a founding barracks, and a baked twin
// resurfacing after a WO-753 destruction. Hooking BuildTimerService.JobCompleted
// would cover the first and miss the rest, and a player whose barracks arrived by
// any other road would be handed the velvet rope after all.
//
// So this asks the SAME question every other raid surface asks -
// StructureSingleton.IsBuilt("barracks") - on the SAME 0.5 s edge-triggered
// cadence RaidCapabilityHudBridge uses, and grants on the rising edge. One
// predicate, one answer, no second definition of "has a Barracks" to drift.
//
// -----------------------------------------------------------------------------
// IDEMPOTENT ACROSS RELAUNCHES, WITHOUT A SAVE SCHEMA BUMP.
// -----------------------------------------------------------------------------
// The grant latches on GameState.MarkEverAcquired(GrantLedgerKey) - the monotonic
// v-whatever string ledger that already exists for "has this save ever held X",
// and whose Mark returns TRUE only on the first add. That is exactly an
// idempotency primitive, it is already persisted, migrated and round-tripped, and
// reusing it means this feature needs NO new field, NO version bump and NO
// migrator. It is the same "no new state" reasoning WO-1357 recorded when it
// discriminated NoBarracks from BarracksLost off EverBuiltStructureIds.
//
// The key is namespaced "grant." so it can never collide with a real item id -
// VillageInventory.HasEverAcquired reads the same list for item discovery, and a
// bare id there would make a phantom item look discovered.
//
// (!) A DESTROYED-AND-REBUILT BARRACKS DOES NOT RE-GRANT, and that is deliberate:
// the ledger is monotonic, so the squad is once per save. A second free squad
// would make demolishing a barracks a troop faucet.
//
// ASCII only. FlowTrace tag "Raid". Never stripped (CLAUDE.md section 12).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Grants the free starter squad the first time this save has a Barracks, and
    /// emits funnel step 1 at the same edge. Self-installing, DontDestroyOnLoad,
    /// idempotent forever.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarterArmyGrant : MonoBehaviour
    {
        /// <summary>The StructureSingleton id of the raid building. Same literal every other
        /// raid surface uses - not a second name for the same thing.</summary>
        public const string BarracksItemId = "barracks";

        /// <summary>The troop the map names. TroopDef id, verified against troops.json.</summary>
        public const string StarterTroopId = "troop-footman";

        /// <summary>
        /// How many. The map says three, and three is what 1,650 gold used to buy
        /// (3 x costGold 550 in troops.json), which is the wall this removes.
        /// Ships as a tunable so the owner can re-size the starter squad by feel without
        /// a rebuild - <c>RemoteTunables.KeyRaidStarterArmySize</c>, default 3.
        /// </summary>
        public const int DefaultStarterCount = 3;

        /// <summary>
        /// The idempotency key in GameState's monotonic acquired-ledger. Namespaced so it
        /// can never be mistaken for a real item id by the inventory's discovery reads.
        /// </summary>
        public const string GrantLedgerKey = "grant.starter-army";

        /// <summary>
        /// The FTUE line, built from the ACTUAL number granted rather than a hardcoded
        /// "3": the squad size is a tunable, and a toast that says three while the player
        /// received five is the kind of small lie that costs trust in every other number
        /// the game prints. Ends with the direction the map wrote ("Journey -> Raids"),
        /// which is also the direction the Game Guide now gives - one destination, said
        /// the same way everywhere. ASCII only (mobile font-atlas law).
        /// </summary>
        public static string GrantToastFor(int count)
        {
            string unit = count == 1 ? "Footman" : "Footmen";
            return "Your first squad is ready - " + count + " " + unit +
                   ", free. Open Journey, then Raids.";
        }

        private const float PollInterval = 0.5f;

        private float _timer;      // 0 on spawn -> the first Update asks immediately
        private bool _resolved;    // once granted (or found already granted) we stop polling

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("StarterArmyGrant");
            DontDestroyOnLoad(go);
            go.AddComponent<StarterArmyGrant>();
        }

        private void Update()
        {
            if (_resolved) { enabled = false; return; }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollInterval;

            var gs = GameStateService.Instance;
            var st = gs != null ? gs.State : null;
            if (st == null) return;      // pre-boot / headless with no save: nothing to grant onto

            if (!StructureSingleton.IsBuilt(BarracksItemId)) return;

            // ── The rising edge. Everything below runs at most once per process. ──
            _resolved = true;
            enabled = false;

            // Funnel step 1 fires on the EDGE, not on the grant, so a returning player who
            // already had the squad still registers as having reached the step. RaidFunnel
            // latches it per install, so this is a no-op after the first time.
            Guard.Try("Funnel", "barracks unlocked",
                () => DeNelle.Core.Analytics.RaidFunnel.BarracksUnlocked("StarterArmyGrant"));

            TryGrant(gs, st);
        }

        /// <summary>
        /// The grant itself. Public + state-taking so an oracle can drive it against a
        /// fixture GameState with no scene - and so the "second call grants nothing"
        /// property is provable rather than asserted in a comment.
        /// </summary>
        /// <returns>How many troops were granted (0 when this save already had its squad).</returns>
        public static int TryGrant(GameStateService service, GameState state)
        {
            if (state == null)
            {
                FlowTrace.Warn("Raid", "starter army: no GameState - nothing to grant onto.");
                return 0;
            }

            // MarkEverAcquired returns TRUE only when the key was NEWLY added, so this
            // single call is BOTH the check and the latch. Splitting it into a Has/Mark
            // pair would open a window in which two callers both read false.
            if (!state.MarkEverAcquired(GrantLedgerKey))
            {
                FlowTrace.Step("Raid",
                    "starter army: already granted on this save ('" + GrantLedgerKey +
                    "' is in the acquired ledger) - granting nothing. A rebuilt Barracks " +
                    "never re-issues the free squad.");
                return 0;
            }

            int want = ResolveCount();
            int granted = 0;
            for (int i = 0; i < want; i++)
            {
                // Through the ONE roster owner, so funnel step 2 ("army trained") fires
                // from the same place a paid train fires it and the two cannot disagree.
                int roster = BarracksProgression.GrantTrainedTroop(state, StarterTroopId, "starter-army");
                if (roster <= 0)
                {
                    FlowTrace.Fail("Raid",
                        "starter army: GrantTrainedTroop('" + StarterTroopId + "') reported an " +
                        "empty roster on grant " + (i + 1) + " of " + want + " - the free squad " +
                        "is SHORT. The ledger key is already latched, so this will not retry.");
                    break;
                }
                granted++;
            }

            FlowTrace.Step("Raid",
                "STARTER ARMY GRANTED: " + granted + "/" + want + " x '" + StarterTroopId +
                "' free on first Barracks (WO-1374, map section 2 - removes the 1,650-gold " +
                "wall in front of the loop the game is trying to teach). Roster is now " +
                (state.Army != null && state.Army.Owned != null ? state.Army.Owned.Count : 0) + ".");

            if (service != null)
                Guard.Try("Raid", "persist starter army", () => service.Save());

            if (granted > 0)
                Guard.Try("Raid", "starter army toast",
                    () => ElarionUiKit.ShowToast(GrantToastFor(granted), ElarionUiKit.ToastTone.Confirm));

            return granted;
        }

        /// <summary>
        /// The squad size, off the tunable rail, clamped to a sane band. 0 disables the
        /// grant outright (and the ledger still latches, so it stays disabled for that
        /// save rather than firing later if the knob moves - a grant that appears
        /// retroactively would be worse than one that never happened).
        /// </summary>
        public static int ResolveCount()
        {
            int raw = DeNelle.Core.Ops.RemoteTunables.Int(
                DeNelle.Core.Ops.RemoteTunables.KeyRaidStarterArmySize);
            int clamped = Mathf.Clamp(raw, 0, 10);
            if (clamped != raw)
                FlowTrace.Warn("Raid",
                    "starter army size knob resolved to " + raw + ", outside 0..10 - CLAMPED to " +
                    clamped + ".");
            return clamped;
        }
    }
}
