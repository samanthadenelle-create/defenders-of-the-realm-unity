// =============================================================================
// AtbEngineRegression — headless data/logic oracle for the isolated BattleATB
// engine (docs/MASTER_CATALOG/battle-atb.md). ZERO oracle coverage existed for
// this area before this file. Mirrors the DataRegression contract exactly
// (namespace DeNelle.Editor, public static bool Run(out string reason);
// true = pass + one-line summary, false = fail + naming detail). NO PlayMode —
// pure "real object in -> assert real response" (INSTRUMENTATION_STANDARD §4).
// The orchestrator owns the one-line RunAll registration.
//
// Invariants asserted (all decidable from data + logic):
//   1. Tuning-table completeness — HERO_ABILITIES has exactly 4 abilities for
//      each of Knight/Ranger/Mage covering the Q/W/E/R slots; HERO_STATS covers
//      all three; the Orc-family + skeleton enemy defs the roster maps to exist.
//   2. MapToEngineDef totality — the REAL BattleController.MapToEngineDef (invoked
//      by reflection: it is a private static) maps every representative breach id
//      (null / empty / exact key / village id / heuristic token / unknown / the
//      OrcFamily) onto a VALID Defs.ENEMY_DEFS key (never a dangling roster entry).
//   3. Engine determinism + termination — Turn.AutoResolveBattle drives a real
//      BattleSetup to BattlePhase.Ended with a decisive Outcome inside the turn
//      guard, and the SAME seed reproduces the SAME Outcome + log length
//      (golden-vector determinism), while a DIFFERENT seed also terminates.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DeNelle.BattleATB;
using DeNelle.BattleATB.Engine;

namespace DeNelle.Editor
{
    public static class AtbEngineRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var summary = new StringBuilder();

            try
            {
                CheckTuningCompleteness(failures, summary);
                CheckMapToEngineDefTotality(failures, summary);
                CheckEngineDeterminismAndTermination(failures, summary);
            }
            catch (Exception ex)
            {
                reason = $"AtbEngineRegression threw: {ex.GetType().Name}: {ex.Message}";
                return false;
            }

            if (failures.Count == 0)
            {
                reason = "ATB engine OK — " + summary.ToString().TrimEnd();
                return true;
            }

            reason = $"ATB engine: {failures.Count} failure(s): " + string.Join(" | ", failures);
            return false;
        }

        // --- 1. tuning-table completeness (4 abilities/class + stats + enemy defs) ---
        private static void CheckTuningCompleteness(List<string> failures, StringBuilder summary)
        {
            var classes = new[] { HeroClass.Knight, HeroClass.Ranger, HeroClass.Mage };
            var requiredSlots = new[] { AbilitySlot.Q, AbilitySlot.W, AbilitySlot.E, AbilitySlot.R };

            foreach (var cls in classes)
            {
                if (!Defs.HERO_ABILITIES.TryGetValue(cls, out var abilities) || abilities == null)
                {
                    failures.Add($"HERO_ABILITIES missing entry for {cls}");
                    continue;
                }
                if (abilities.Length != 4)
                    failures.Add($"HERO_ABILITIES[{cls}] has {abilities.Length} abilities, expected 4");

                var slots = new HashSet<AbilitySlot>(abilities.Select(a => a.Slot));
                foreach (var slot in requiredSlots)
                    if (!slots.Contains(slot))
                        failures.Add($"HERO_ABILITIES[{cls}] missing slot {slot}");

                foreach (var a in abilities)
                    if (string.IsNullOrEmpty(a.Name))
                        failures.Add($"HERO_ABILITIES[{cls}] has an ability with a blank Name");

                if (!Defs.HERO_STATS.TryGetValue(cls, out var stats) || stats.MaxHp <= 0)
                    failures.Add($"HERO_STATS[{cls}] missing or MaxHp <= 0");
            }

            // The roster's staged/fallback enemy defs must exist (BuildEnemyRoster stages
            // AtbPrototypeEncounter.OrcFamily; MapToEngineDef's ultimate fallback is skeleton).
            foreach (var id in new[] { "orc-warrior", "orc-tank", "orc-mage", "skeleton" })
                if (!Defs.ENEMY_DEFS.ContainsKey(id))
                    failures.Add($"ENEMY_DEFS missing required key '{id}'");

            summary.Append($"tuning: {classes.Length} classes x4 abilities + {Defs.ENEMY_DEFS.Count} enemy defs. ");
        }

        // --- 2. MapToEngineDef totality — every id resolves to a valid ENEMY_DEFS key ---
        private static void CheckMapToEngineDefTotality(List<string> failures, StringBuilder summary)
        {
            var method = typeof(BattleController).GetMethod(
                "MapToEngineDef", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                failures.Add("BattleController.MapToEngineDef(string) not found by reflection (signature changed?)");
                return;
            }

            // Representative inputs: null, empty, an exact engine key, the village enemies.json
            // ids (WO-94), the generic heuristic tokens, an unknown id, and the OrcFamily.
            var ids = new[]
            {
                null, "", "skeleton", "goblin", "necromancer",
                "hollow-walker", "hollow-warrior", "hollow-rogue",
                "dark-necromancer", "iron-tank", "goblin-scout",
                "totally-unknown-xyz", "orc-warrior", "orc-tank", "orc-mage",
            };

            int resolved = 0;
            foreach (var id in ids)
            {
                string mapped;
                try { mapped = (string)method.Invoke(null, new object[] { id }); }
                catch (Exception ex)
                {
                    failures.Add($"MapToEngineDef('{id ?? "<null>"}') threw {ex.InnerException?.GetType().Name ?? ex.GetType().Name}");
                    continue;
                }
                if (string.IsNullOrEmpty(mapped) || !Defs.ENEMY_DEFS.ContainsKey(mapped))
                    failures.Add($"MapToEngineDef('{id ?? "<null>"}') => '{mapped ?? "<null>"}' which is NOT a Defs.ENEMY_DEFS key");
                else
                    resolved++;
            }

            summary.Append($"map: {resolved}/{ids.Length} ids -> valid enemy def. ");
        }

        // --- 3. engine determinism + termination ---------------------------------
        private static void CheckEngineDeterminismAndTermination(List<string> failures, StringBuilder summary)
        {
            const int MaxTurns = 5000;

            BattleState first = RunToEnd(MakeSetup(seed: 12345), MaxTurns);
            if (first == null)
            {
                failures.Add("AutoResolveBattle(seed=12345) returned null");
                return;
            }
            if (first.Phase != BattlePhase.Ended)
                failures.Add($"AutoResolveBattle(seed=12345) did NOT terminate (phase={first.Phase} after {MaxTurns} turns) — engine non-termination");
            if (first.Outcome == BattleOutcome.None)
                failures.Add("AutoResolveBattle(seed=12345) ended with Outcome=None (no decisive result)");

            // Determinism: identical seed must reproduce identical outcome + log length.
            BattleState repeat = RunToEnd(MakeSetup(seed: 12345), MaxTurns);
            if (repeat == null)
            {
                failures.Add("AutoResolveBattle(seed=12345) repeat run returned null");
            }
            else
            {
                if (repeat.Outcome != first.Outcome)
                    failures.Add($"Non-deterministic: same seed produced different outcomes ({first.Outcome} vs {repeat.Outcome})");
                int c1 = first.Log?.Count ?? -1;
                int c2 = repeat.Log?.Count ?? -1;
                if (c1 != c2)
                    failures.Add($"Non-deterministic: same seed produced different log lengths ({c1} vs {c2})");
            }

            // A different seed must also terminate (guard against a seed-specific hang).
            BattleState other = RunToEnd(MakeSetup(seed: 999), MaxTurns);
            if (other == null || other.Phase != BattlePhase.Ended)
                failures.Add($"AutoResolveBattle(seed=999) did not terminate (phase={other?.Phase.ToString() ?? "<null>"})");

            summary.Append($"engine: terminated outcome={first.Outcome} log={first.Log?.Count ?? 0} (determinism verified).");
        }

        private static BattleState RunToEnd(BattleSetup setup, int maxTurns)
        {
            BattleState state = BattleStateOps.CreateBattle(setup);
            return Turn.AutoResolveBattle(state, maxTurns);
        }

        /// <summary>A real, minimal Knight-vs-skeleton setup through the authoritative
        /// multi-member party path — the same shape BattleController.BuildSetup produces.</summary>
        private static BattleSetup MakeSetup(int seed)
        {
            return new BattleSetup
            {
                Wave = 1,
                Seed = seed,
                PartyMembers = new List<PartyMemberSpec>
                {
                    new PartyMemberSpec
                    {
                        Id = "hero",
                        Name = "Grom",
                        HeroClass = HeroClass.Knight,
                        Species = null,
                        BondRank = 0,
                        AiMode = PetAiMode.Balanced,
                        ControlMode = ControlMode.Player,
                    },
                },
                HeroClass = HeroClass.Knight,
                HeroName = "Grom",
                Pets = new List<PartyPetSpec>(),
                Enemies = new List<BreachEnemySpec>
                {
                    new BreachEnemySpec { DefId = "skeleton" },
                },
                Inventory = new Dictionary<ItemKind, int> { { ItemKind.Potion, 3 } },
                Reinforcements = false,
            };
        }
    }
}
