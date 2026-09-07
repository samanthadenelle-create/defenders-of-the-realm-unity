// =============================================================================
// TroopTargetPreferenceRegression [troop-target-preference] — WO-1438.
// -----------------------------------------------------------------------------
// WHAT BROKE, MEASURED (logs/debug/troop-ai-blind-2026-09-06.log, 14:36:26, build
// 2026.09.06.358161 — the owner's own raid):
//
//   [Flow:TroopAI] id=troop-archer role=ranged RETARGET#1 reason=foe-null dropped='<none>'
//     -> won='Wall_Outer_SS_11(WallSegment)' kind=struct dist=4.2m
//     | runnerUpOtherKind='Wall_Outer_SS_11(WallSegment)' dist=4.2m
//     | sweep colliders=18 accepted[unit=1,struct=17] rejected=0 radius=23.9m preferStruct=False
//
// `accepted[unit=1,...]` is the proving field: a LIVE defender was inside the sweep and a wall
// panel 4.2 m away won anyway. The same archer then walked SS_11 -> Watchtower_Archer_0 ->
// SS_12 -> SS_7 -> SS_13 -> SS_6 -> SS_14, outward along the ring in both directions — the
// owner's "they keep attacking adjoining walls", in data. Cause: with _preferStructures false
// (every role but siege) a hostile STRUCTURE competed in the same nearest-wins bucket as a live
// body, so nothing about the pick could ever change when a segment fell.
//
// WHAT THIS SUITE PINS — the Breach-phase pick rule, RaidAssaultAi.PreferUnit (WO-1595
// retired TroopController.PrefersUnitOverStructure). NearestHostile calls PreferUnit via
// PickBucket, so the suite cannot pass while the selector does something else.
//
// RED PROOF, stated honestly (CLAUDE.md §11B). Case 1 is RED against the build the owner played
// and the reasoning is structural, not executed: at HEAD~ the function did not exist and
// NearestHostile's loop put every accepted candidate through `else if (sqr < bestUnitSqr)`
// whenever _preferStructures was false, so with a unit at 20 m and a wall at 4.2 m it returned
// THE WALL. Case 1 asserts the unit is taken once a route to it exists. I could not execute the
// RED (the Unity lock is the CLI lead's), so it is reasoned from the HEAD~ source and from the
// capture above, and is labelled as such rather than claimed as a run.
//
// Cases 2 and 3 are the anti-regressions that keep the fix honest: siege (WO-933) must be
// untouched, and an unreachable unit must NOT be preferred — steering is
// _agent.Move(displacement) with NoObstacleAvoidance, so preferring a defender seen THROUGH an
// intact wall would push the troop into a navmesh edge and freeze it, which is strictly worse
// than chewing the wall. Case 4 pins WO-1439's rule on the body contract, the overload this
// ticket added so the troop selector could stop hand-copying `Faction != Hostile`.
//
// NOT PINNED, deliberately: "prefers the breach". No capture has yet measured whether the
// navmesh under a felled wall becomes walkable (every raid collapse logs `0 carving
// obstacle(s) dropped`, and the BREACH probe's routeStatus was confounded by target kind —
// 133/133 struct probes returned CalculatePath-FAILED because a wall centre is not on the
// navmesh). Asserting a breach preference here would be a guess in test clothing. The repaired
// probe's holeNavmesh= field answers it on the next captured raid.
//
// Marker: TROOP_TARGET_PREF_OK / TROOP_TARGET_PREF_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "troop-target-preference suite", () => { if (!DeNelle.Editor.TroopTargetPreferenceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[troop-target-preference] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core.Combat;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TroopTargetPreferenceRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== TroopTargetPreferenceRegression (WO-1438) ===\n");
            try
            {
                Case1_ReachableUnitBeatsNearerStructure(failures, log);
                Case2_SiegeStillPrefersStructures(failures, log);
                Case3_UnreachableUnitDoesNotPinTheTroop(failures, log);
                Case4_FactionRuleReachesTheBodyContract(failures, log);
                Case5_DestroyedTowerIsNotAProbeTarget(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "TROOP_TARGET_PREF_OK a reachable live defender beats a nearer hostile " +
                         "structure for non-siege troops, siege keeps masonry priority, an " +
                         "unreachable defender never wins, and CombatFactionRules answers on " +
                         "IDamageable as well as IDamageableStructure";
                Debug.Log(reason + "\n" + log);
                return true;
            }

            reason = "TROOP_TARGET_PREF_FAIL " + failures.Count + " failure(s): " +
                     string.Join(" | ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // ── Case 1 — THE TICKET. RED against the build the owner played. ─────────────────────
        private static void Case1_ReachableUnitBeatsNearerStructure(List<string> failures, StringBuilder log)
        {
            // The captured archer: a live guard in the sweep, seventeen wall panels, the nearest
            // of them 4.2 m away, preferStruct=False. Once a complete route to the guard exists,
            // the guard must win.
            // WO-1595: live pick rule is RaidAssaultAi.PreferUnit (Breach mirrors WO-1438).
            bool prefer = RaidAssaultAi.PreferUnit(
                RaidAssaultPhase.Breach, preferStructures: false, hasUnit: true, hasStruct: true,
                unitInAttackRange: false, routeToUnitOpen: true);
            if (!prefer)
                failures.Add("case1: a routable live defender did NOT beat a nearer wall panel " +
                             "for a non-siege troop - this is the 09-06 wall-walk, unfixed");
            else
                log.AppendLine("[case1] routable defender beats the nearer structure (non-siege) OK");

            // Reach alone is sufficient too — a defender already inside attack range needs no
            // route query, and preferring it can have no movement consequence at all.
            bool inReach = RaidAssaultAi.PreferUnit(
                RaidAssaultPhase.Breach, preferStructures: false, hasUnit: true, hasStruct: true,
                unitInAttackRange: true, routeToUnitOpen: false);
            if (!inReach)
                failures.Add("case1b: a defender ALREADY inside attack range lost to a structure");
            else
                log.AppendLine("[case1b] in-reach defender beats the structure OK");
        }

        // ── Case 2 — WO-933 siege is not collateral damage. ──────────────────────────────────
        private static void Case2_SiegeStillPrefersStructures(List<string> failures, StringBuilder log)
        {
            bool prefer = RaidAssaultAi.PreferUnit(
                RaidAssaultPhase.Breach, preferStructures: true, hasUnit: true, hasStruct: true,
                unitInAttackRange: true, routeToUnitOpen: true);
            if (prefer)
                failures.Add("case2: a SIEGE troop (troop-catapult) preferred a unit over a " +
                             "structure - WO-933's whole point is that it does not");
            else
                log.AppendLine("[case2] siege keeps masonry priority OK");

            // …and siege must still fall back to a unit rather than freeze when no structure is
            // in the sweep ("never freezes idle", NearestHostile's own contract).
            bool fallback = RaidAssaultAi.PreferUnit(
                RaidAssaultPhase.Breach, preferStructures: true, hasUnit: true, hasStruct: false,
                unitInAttackRange: true, routeToUnitOpen: true);
            if (fallback)
                log.AppendLine("[case2b] NOTE siege prefers the unit when no structure exists");
            // Either answer is acceptable for 2b - with hasStruct false the caller takes the unit
            // regardless - so it is logged, not asserted. Recorded so the next reader does not
            // mistake the silence for an untested branch.
        }

        // ── Case 3 — the anti-pinning guard. ─────────────────────────────────────────────────
        private static void Case3_UnreachableUnitDoesNotPinTheTroop(List<string> failures, StringBuilder log)
        {
            bool prefer = RaidAssaultAi.PreferUnit(
                RaidAssaultPhase.Breach, preferStructures: false, hasUnit: true, hasStruct: true,
                unitInAttackRange: false, routeToUnitOpen: false);
            if (prefer)
                failures.Add("case3: a defender that is neither in reach nor routable was " +
                             "preferred - the troop will push into an intact wall with " +
                             "NoObstacleAvoidance and freeze (moved~0, inRange=False)");
            else
                log.AppendLine("[case3] unreachable defender is NOT preferred OK");

            bool noUnit = RaidAssaultAi.PreferUnit(
                RaidAssaultPhase.Breach, preferStructures: false, hasUnit: false, hasStruct: true,
                unitInAttackRange: false, routeToUnitOpen: true);
            if (noUnit)
                failures.Add("case3b: preferred a unit when the sweep accepted none");
            else
                log.AppendLine("[case3b] no unit in sweep -> structure stands OK");
        }

        // ── Case 4 — WO-1439's one predicate, on the body contract. ──────────────────────────
        private static void Case4_FactionRuleReachesTheBodyContract(List<string> failures, StringBuilder log)
        {
            int before = failures.Count;
            IDamageable hostileLive   = new FakeBody(CombatFaction.Hostile,  alive: true);
            IDamageable hostileDead   = new FakeBody(CombatFaction.Hostile,  alive: false);
            IDamageable friendlyLive  = new FakeBody(CombatFaction.Friendly, alive: true);

            if (!CombatFactionRules.MayAttack(CombatFaction.Friendly, hostileLive))
                failures.Add("case4: a Friendly troop may not attack a live Hostile body");
            if (CombatFactionRules.MayAttack(CombatFaction.Friendly, friendlyLive))
                failures.Add("case4: a Friendly troop was allowed to attack a Friendly body - " +
                             "this is the WO-1439 defect (garrison razing its own spire) on the " +
                             "attacker side");
            if (CombatFactionRules.MayAttack(CombatFaction.Friendly, hostileDead))
                failures.Add("case4: a DEAD hostile body was still attackable");
            if (CombatFactionRules.MayAttack(CombatFaction.Friendly, (IDamageable)null))
                failures.Add("case4: a null body was attackable");

            // IsFriendlyFire is deliberately liveness-BLIND: it answers a classification
            // question, and TroopController.IsHostileStructure depends on that so it can still
            // say "that was a structure" about a wall that has just collapsed.
            if (!CombatFactionRules.IsFriendlyFire(CombatFaction.Friendly, friendlyLive))
                failures.Add("case4b: IsFriendlyFire(Friendly, friendly body) was false");
            if (CombatFactionRules.IsFriendlyFire(CombatFaction.Friendly, hostileDead))
                failures.Add("case4b: IsFriendlyFire called a DEAD HOSTILE body friendly - the " +
                             "classifier must stay liveness-blind on faction, not on death");

            if (failures.Count == before)
                log.AppendLine("[case4] CombatFactionRules answers on IDamageable, null/dead/own-side all refused OK");
        }

        // -- Case 5 -- WO-1569: the felled tower the probe kept reading after Unity destroyed it.
        //
        // MEASURED, device capture build 2026.09.07.358872, scene RaidBase_raider_camp_small,
        // F8 seq 4688 (troop-footman) and 4689 (troop-archer), same instant:
        //   [Flow:TroopAI] breach-probe id=troop-footman FAILED: NullReferenceException
        //     at UnityEngine.Component.get_transform ()
        //     at DeNelle.Village.DefenseTower.get_WorldPosition ()
        //     at DeNelle.Village.TroopController+<>c__DisplayClass119_0.<TraceBreachProbe>b__0 ()
        //     at DeNelle.Core.Diagnostics.Guard.Try (...)  at TroopController.Update ()
        //
        // RED PROOF, stated honestly (CLAUDE.md 11B) and reasoned, not executed - the Unity lock
        // belongs to the CLI lead, matching this suite's own convention for case 1. At HEAD~,
        // TroopController had no IsLiveTarget and DefenseTower.WorldPosition was the bare
        // `transform.position`, so assertion (b) had no symbol to call and assertion (c) threw
        // the NullReferenceException in the capture above. Both are structural, not probabilistic.
        //
        // The three assertions are one argument:
        //   (a) the interface null check STILL passes on a destroyed component - that is the trap,
        //       and pinning it stops a future seat "simplifying" IsLiveTarget back to `!= null`;
        //   (b) IsLiveTarget answers correctly, so the troop's foeValid goes false and it
        //       RE-SELECTS instead of aiming at a corpse;
        //   (c) WorldPosition survives the read anyway, so no caller outside the guard can be
        //       taken down by a tower that died a frame earlier.
        private static void Case5_DestroyedTowerIsNotAProbeTarget(List<string> failures, StringBuilder log)
        {
            int before = failures.Count;
            GameObject go = null;
            try
            {
                go = new GameObject("wo1569-felled-tower");
                go.transform.position = new Vector3(7f, 0f, -3f);
                var tower = go.AddComponent<DefenseTower>();
                IDamageable target = tower;

                Vector3 alivePos = target.WorldPosition;
                if (!TroopController.IsLiveTarget(target))
                    failures.Add("case5: IsLiveTarget said a LIVE tower was not a live target - " +
                                 "the guard is over-eager and would blind every breach probe");
                if (alivePos != go.transform.position)
                    failures.Add("case5: a live tower's WorldPosition did not equal its transform " +
                                 "position - the destroy-safe cache changed the live answer");

                UnityEngine.Object.DestroyImmediate(go);
                go = null;

                // (a) THE TRAP. An interface-typed reference to a destroyed component is NOT
                // managed-null, which is why `dmg != null` was never a sufficient check.
                if (ReferenceEquals(target, null))
                    failures.Add("case5a: the destroyed tower went managed-null, so this case no " +
                                 "longer reproduces the WO-1569 trap and proves nothing");

                // (b) THE FIX, on the live predicate the selector itself calls.
                if (TroopController.IsLiveTarget(target))
                    failures.Add("case5b: IsLiveTarget accepted a DESTROYED tower - the troop " +
                                 "would keep it as its cached foe instead of re-selecting, and the " +
                                 "breach probe would read WorldPosition off a corpse (F8 seq 4688)");

                // (c) THE SAFETY NET, on the exact property in the captured stack.
                try
                {
                    Vector3 deadPos = target.WorldPosition;
                    if (deadPos != alivePos)
                        failures.Add("case5c: a destroyed tower's WorldPosition returned " +
                                     deadPos + " instead of its last live position " + alivePos);
                }
                catch (Exception ex)
                {
                    failures.Add("case5c: reading WorldPosition on a DESTROYED tower threw " +
                                 ex.GetType().Name + " - this IS the captured defect " +
                                 "(F8 seq 4688/4689, build 2026.09.07.358872)");
                }
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            if (failures.Count == before)
                log.AppendLine("[case5] destroyed tower: interface null check still passes, " +
                               "IsLiveTarget refuses it, WorldPosition returns the last live " +
                               "position without throwing OK");
        }

        /// <summary>
        /// A minimal <see cref="IDamageable"/> so the faction rule can be asserted without a
        /// scene, a prefab or a Village component. Deliberately inert: every verb is a no-op,
        /// because this suite tests arbitration, never damage.
        /// </summary>
        private sealed class FakeBody : IDamageable
        {
            private readonly CombatFaction _faction;
            private readonly bool _alive;
            public FakeBody(CombatFaction faction, bool alive) { _faction = faction; _alive = alive; }
            public CombatFaction Faction => _faction;
            public Vector3 WorldPosition => Vector3.zero;
            public float Hp => _alive ? 1f : 0f;
            public bool IsAlive => _alive;
            public void TakeDamage(float amount, DamageElement element) { }
            public void ApplyStatus(StatusEffect effect, float seconds) { }
        }
    }
}
