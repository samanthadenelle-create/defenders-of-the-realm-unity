// =============================================================================
// StarterArmyGrantRegression [starter-army]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Markers: STARTER_ARMY_OK / _FAIL.
//
// WO-1374 / north-star map section 2 - "the first army is free".
//
//   Owner: "A player starts with 200 gold but needs 1,650 to participate in the
//   thing you're trying to teach them. That's basically putting a nightclub
//   behind a velvet rope and handing the player twelve cents."
//   "On Barracks completion, grant 3 free Footmen (a starter raid squad)."
//
// -----------------------------------------------------------------------------
// THE THREE WAYS THIS FEATURE CAN GO WRONG, EACH WITH A CASE.
// -----------------------------------------------------------------------------
//   (A) IT NEVER FIRES  - the player still faces the velvet rope. Case A drives
//       the real grant against a fixture GameState and counts the roster.
//   (B) IT FIRES TWICE  - a free squad on every Barracks makes demolish-and-
//       rebuild a troop faucet, which is a worse bug than the one being fixed
//       because it is silent and compounding. Case B calls TryGrant again on the
//       SAME state and requires zero.
//   (C) IT CHARGES      - a "free" army that quietly spends is the velvet rope
//       with extra steps. Case C is a source lint for any spend on that path.
//
// PROVEN RED FIRST: every case in (A) fails by construction against the
// pre-WO-1374 tree, where StarterArmyGrant did not exist. Case (B) was red-proved
// by temporarily latching with a Has/Mark PAIR instead of the single
// MarkEverAcquired call - the pair grants twice if two callers read before either
// writes, and B catches the second grant.
//
// -----------------------------------------------------------------------------
// (!) WHY THIS IS TESTABLE AT ALL WITHOUT A SCENE.
// -----------------------------------------------------------------------------
// StarterArmyGrant.TryGrant is deliberately a PUBLIC STATIC taking a GameState,
// with the MonoBehaviour poll as a thin caller. A grant buried inside Update()
// would be provable only in PlayMode, i.e. not provable in the batchmode gate,
// i.e. not actually pinned. The shape is the assertion.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// Pins the WO-1374 free starter squad: it fires, it fires exactly once per
    /// save, it grants the right unit, and it costs nothing.
    /// </summary>
    public static class StarterArmyGrantRegression
    {
        /// <summary>The map's number, as a literal. Never read off the knob being checked.</summary>
        private const int MapStarterCount = 3;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- STARTER ARMY (WO-1374, map section 2) ---");

            // The funnel latches in PlayerPrefs and GrantTrainedTroop emits step 2, so the
            // run is fenced the same way RaidFunnelRegression fences itself: nothing this
            // suite does may survive it, or it becomes un-rerunnable.
            var snapshot = RaidFunnelRegression.SnapshotFunnelPrefs();

            try
            {
                // =============================================================
                //  (A) IT FIRES, AND GRANTS THE RIGHT THING.
                // =============================================================
                // ⚠ GameState is a ScriptableObject, so it is CreateInstance'd, never
                // `new`ed - a `new GameState()` compiles nowhere and, in the shapes where it
                // would, produces an object Unity never initialised.
                var state = ScriptableObject.CreateInstance<GameState>();
                state.Army = new ArmyStorage();
                state.EverAcquiredItemIds = new List<string>();

                int granted = StarterArmyGrant.TryGrant(null, state);
                int roster = state.Army.Owned != null ? state.Army.Owned.Count : 0;
                log.AppendLine("  first grant -> " + granted + " troop(s), roster " + roster);

                if (granted != MapStarterCount)
                    failures.Add("[A1] the first Barracks granted " + granted + " troops, the map says " +
                                 MapStarterCount + " ('On Barracks completion, grant 3 free Footmen')");
                if (roster != MapStarterCount)
                    failures.Add("[A1] the roster holds " + roster + " after the grant, expected " + MapStarterCount);

                if (state.Army.Owned != null)
                {
                    foreach (var t in state.Army.Owned)
                    {
                        if (t == null) { failures.Add("[A2] a null troop landed in the roster"); continue; }
                        if (t.TroopDefId != StarterArmyGrant.StarterTroopId)
                            failures.Add("[A2] the starter squad contains '" + t.TroopDefId + "', expected '" +
                                         StarterArmyGrant.StarterTroopId + "' - the map says Footmen");
                        if (t.Wounded)
                            failures.Add("[A2] a starter troop arrived WOUNDED - the squad must be deployable " +
                                         "immediately, or the first raid is not minutes away");
                    }
                }

                // A3 - the ledger key is what makes it idempotent, and it must be namespaced
                // so it cannot be mistaken for an item id by VillageInventory's discovery reads.
                if (!state.HasEverAcquired(StarterArmyGrant.GrantLedgerKey))
                    failures.Add("[A3] the grant did not latch '" + StarterArmyGrant.GrantLedgerKey +
                                 "' in the acquired ledger - it will re-grant on the next launch");
                if (!StarterArmyGrant.GrantLedgerKey.StartsWith("grant.", System.StringComparison.Ordinal))
                    failures.Add("[A3] the idempotency key '" + StarterArmyGrant.GrantLedgerKey + "' is not " +
                                 "namespaced 'grant.' - it shares a list with real item ids, and a bare id " +
                                 "there makes a phantom item read as discovered");

                // =============================================================
                //  (B) IT NEVER FIRES TWICE. The faucet case.
                // =============================================================
                int again = StarterArmyGrant.TryGrant(null, state);
                int rosterAfter = state.Army.Owned != null ? state.Army.Owned.Count : 0;
                log.AppendLine("  second grant -> " + again + " troop(s), roster " + rosterAfter);
                if (again != 0)
                    failures.Add("[B1] a SECOND call granted " + again + " more troops. The squad is once per " +
                                 "save; re-granting turns demolish-and-rebuild into a troop faucet.");
                if (rosterAfter != MapStarterCount)
                    failures.Add("[B1] the roster grew to " + rosterAfter + " on the second call, expected it to " +
                                 "stay at " + MapStarterCount);

                // B2 - and a save that ALREADY carries the ledger key (a returning player, or
                // one whose barracks was destroyed and rebuilt) gets nothing at all.
                var returning = ScriptableObject.CreateInstance<GameState>();
                returning.Army = new ArmyStorage();
                returning.EverAcquiredItemIds = new List<string> { StarterArmyGrant.GrantLedgerKey };
                int forReturning = StarterArmyGrant.TryGrant(null, returning);
                if (forReturning != 0)
                    failures.Add("[B2] a save that already holds the ledger key was granted " + forReturning +
                                 " troops - a rebuilt Barracks must never re-issue the free squad");

                // B3 - a null state is a no-op, not a crash. Headless and pre-boot both hit this.
                if (StarterArmyGrant.TryGrant(null, null) != 0)
                    failures.Add("[B3] TryGrant(null state) granted troops - it must be a logged no-op");

                // =============================================================
                //  (C) IT IS FREE, AND THE COPY IS HONEST.
                // =============================================================
                string grantCode = RaidLootCurrencyRegression.ReadStripped("StarterArmyGrant.cs");
                if (grantCode == null)
                {
                    failures.Add("[C1] StarterArmyGrant.cs not found under Assets/_Modules - the free-ness lint " +
                                 "cannot run, and a lint that silently skips is worse than none");
                }
                else
                {
                    string[] spends = { "TrySpend", "CanAfford", "costGold", "SpendCrystals", "Coins" };
                    foreach (var s in spends)
                        if (grantCode.Contains(s))
                            failures.Add("[C1] StarterArmyGrant.cs live code contains '" + s + "' - the starter " +
                                         "squad must be FREE. A grant that charges is the velvet rope with " +
                                         "extra steps, and it would also make this feature depend on the " +
                                         "troop-cost fork WO-1374 is blocked on.");
                    // It must go through the ONE roster owner, so funnel step 2 fires from the
                    // same place a paid train fires it.
                    if (!grantCode.Contains("BarracksProgression.GrantTrainedTroop"))
                        failures.Add("[C1] StarterArmyGrant.cs does not grant through " +
                                     "BarracksProgression.GrantTrainedTroop - a second roster-write path means " +
                                     "'army trained' can be reached by one route and missed by the other");
                }

                // C2 - the toast counts what it granted. A toast that says three while the
                // player received five is a small lie that costs trust in every other number
                // the game prints - and the count is a tunable, so this is reachable.
                for (int n = 1; n <= 4; n++)
                {
                    string toast = StarterArmyGrant.GrantToastFor(n);
                    if (string.IsNullOrEmpty(toast)) { failures.Add("[C2] GrantToastFor(" + n + ") is empty"); continue; }
                    if (!toast.Contains(n.ToString()))
                        failures.Add("[C2] the grant toast for " + n + " troops does not name that number: \"" + toast + "\"");
                    if (n == 1 && !toast.Contains("Footman"))
                        failures.Add("[C2] the single-troop toast does not read 'Footman': \"" + toast + "\"");
                    if (n > 1 && !toast.Contains("Footmen"))
                        failures.Add("[C2] the plural toast does not read 'Footmen': \"" + toast + "\"");
                    // The map's FTUE line points at Journey -> Raids, and it must say the SAME
                    // thing the Game Guide now says. One destination, worded one way.
                    if (toast.IndexOf("Journey", System.StringComparison.Ordinal) < 0 ||
                        toast.IndexOf("Raids", System.StringComparison.Ordinal) < 0)
                        failures.Add("[C2] the grant toast does not direct the player to Journey -> Raids: \"" +
                                     toast + "\". The map's whole point is that the first raid happens within " +
                                     "MINUTES, which requires telling them where it is.");
                    foreach (char c in toast)
                        if (c > 126 || c < 32)
                        { failures.Add("[C2] the grant toast is not 7-bit ASCII (mobile font-atlas law): \"" + toast + "\""); break; }
                }

                // C3 - the squad size is on the tunable rail, clamped, and answers the map's
                // number with no override present.
                int resolved = StarterArmyGrant.ResolveCount();
                if (resolved != MapStarterCount)
                    failures.Add("[C3] ResolveCount() answered " + resolved + " with no override, expected the " +
                                 "shipping default " + MapStarterCount);
            }
            finally
            {
                RaidFunnelRegression.RestoreFunnelPrefs(snapshot);
            }

            if (failures.Count == 0)
            {
                reason = "STARTER ARMY OK - the first Barracks grants " + MapStarterCount + " free deployable " +
                         "Footmen through the one roster owner, latches on the monotonic acquired-ledger so a " +
                         "second call and a rebuilt Barracks both grant nothing, no-ops on a null state, spends " +
                         "no resource of any kind, and tells the player where to go (Journey -> Raids) in 7-bit " +
                         "ASCII with a count that matches what was actually granted";
                Debug.Log(log.ToString() + "STARTER_ARMY_OK");
                return true;
            }

            reason = "starter-army: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "STARTER_ARMY_FAIL: " + reason);
            return false;
        }
    }
}
