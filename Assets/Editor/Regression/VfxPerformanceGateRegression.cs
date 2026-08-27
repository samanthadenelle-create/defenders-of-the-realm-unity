// =============================================================================
// VfxPerformanceGateRegression [vfx-perf-gate] - WO-1242.
// The oracle that keeps the Seeker frame-time gate from silencing the one signal
// the owner cannot lose, and from shedding the wrong thing first.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// ## WHAT THIS SUITE EXISTS TO PREVENT
//
// The owner ruled the dungeon VFX tier ON (48 loops) and asked for a perf gate with
// automatic degradation so the raised ceiling cannot cost frame time. A gate that
// can lower quality is, by construction, a gate that can lower the WRONG quality:
//
//   1. It could shed Aura_LowHealth / Aura_NearDeath - the owner is red/green
//      colourblind and that aura is her ONLY non-colour danger signal. WO-1229 made
//      those two loops unrefusable after a captured fight in which the tell lost a
//      pool race to a candle:
//
//        [Flow:HeroHpAura] 'NearDeath' aura ('Aura_NearDeath') was REFUSED by
//                          VFXManager ... the hero has no non-colour danger signal.
//
//      A perf gate able to silence them re-opens exactly that hole.
//   2. It could shed load-bearing combat auras BEFORE ambient room dress, which is
//      backwards: room dress is the most numerous and least load-bearing loop class
//      in the game, and the additive candle quads are the fill-rate cost being
//      measured in the first place.
//   3. It could degrade WITHOUT EVIDENCE - lowering quality off an intuition
//      threshold rather than off a measured relationship, which is the guess-and-ship
//      failure CLAUDE.md section 12 forbids.
//
// ## WHAT IT ASSERTS
//
//   (A) THE LADDER IS SANE. Ambient presence is non-increasing as the level rises;
//       level None returns the AUTHORED rings exactly (a gate that trims while the
//       device is healthy is the silent quality drop this ticket forbids); the top
//       of the ladder is reachable and bounded.
//   (B) AMBIENT FIRST, ARITHMETICALLY. At every level where the enemy/pet ring is
//       below its authored size, the ambient ring is already ZERO. And the ordering
//       is not vacuous: there is at least one level that sheds ambient while leaving
//       the combat ring untouched.
//   (C) THE ACCESSIBILITY ALLOWLIST IS EXEMPT AT EVERY LEVEL, INCLUDING THE TOP.
//       MayShed is false for Aura_LowHealth and Aura_NearDeath at every level in the
//       enum. GOOD PATH ASSERTED TOO: a non-allowlist type IS sheddable at those same
//       levels, so the exemption is a real discrimination and not a function that
//       returns false for everything.
//   (D) THE TELL IS STILL UNREFUSABLE WITH THE POOL SATURATED, on all three tiers,
//       and the cap still bites for everything else. The gate is proved to own no
//       lever that reaches the refusal predicate at all.
//   (E) MEASURE BEFORE YOU DEGRADE, AS A STATE MACHINE. With no measured baseline,
//       Decide cannot escalate from ANY level however bad the frame time is.
//   (F) THE DISCRIMINATOR. When the baseline is ITSELF over budget - the device is
//       slow with the pool nearly idle, so loops are not the cause - Decide returns
//       to None even from the top of the ladder, rather than shedding dress for
//       nothing.
//   (G) ESCALATION IS SUSTAINED AND CLAMPED. One over-budget window does not shed;
//       the sustain count does; and the ladder stops at MaxLevel.
//   (H) HYSTERESIS. A frame time inside the band between the recover factor and the
//       over factor moves nothing in either direction, so the ladder cannot oscillate
//       on the boundary.
//   (I) THE SHED IS ACTUALLY CONSUMED. VfxAuraProximityCuller reads AmbientRingNow
//       and AuraRingNow. Without this the whole gate could be decorative and every
//       arithmetic case above would still pass.
//   (J) THE OWNER-RULED CEILINGS ARE UNTOUCHED. 48 / 24 / the nearest-8 ambient ring
//       / the 2-slot reserve are pinned, and the gate's source contains no reference
//       to the refusal predicate or the manager's loop limit.
//
// ## POSITIVE CONTROL - PROVE IT CAN GO RED (WO-1138)
//
// Every control below was run numerically against this file before it was accepted.
//
// CONTROL 1 (the accessibility hole - the one that matters). In VfxPerformanceGate,
// delete the IsAccessibilityLoop short-circuit from MayShed so it is a bare
// `return level != VfxShedLevel.None`. That is the naive gate anyone would write, and
// it is precisely the WO-1229 hole re-opened. Result: case (C) fails 6 times -
// (Aura_LowHealth, Aura_NearDeath) x (AmbientTrim, AmbientOff, AuraTrim) - naming both
// types and every level at which they became sheddable. With the short-circuit in
// place, all 8 allowlist checks pass AND the good-path check on Env_Candle still
// reports sheddable, so the case is not passing by returning false for everything.
//
// CONTROL 2 (shed the wrong thing first). Change AuraRingAt to trim from
// VfxShedLevel.AmbientTrim instead of AuraTrim - i.e. halve the combat ring at the
// same moment ambient is first trimmed. Result: case (B) fails at AmbientTrim and at
// AmbientOff with "enemy/pet ring is trimmed to 4 while the ambient ring is still 4 /
// 0 is required" - the ordering violation named at the exact level. Shipped code: 0
// failures, and the non-vacuity probe finds AmbientTrim as a level that sheds ambient
// only.
//
// CONTROL 3 (degrade without evidence). Remove the `baselineMs <= 0f` early return
// from Decide, so an unmeasured session escalates on frame time alone. Result: case
// (E) fails for all four starting levels: Decide(None, 40ms, baseline 0, budget 16.7,
// over 9) returns AmbientTrim where it must return None. Shipped code returns the
// current level unchanged at every one of them.
//
// CONTROL 4 (no discriminator). Remove the `baselineMs >= budgetMs * OverFactor`
// branch. Result: case (F) fails from AmbientTrim, AmbientOff and AuraTrim - a device
// that is over budget with 2 loops live escalates to AuraTrim instead of releasing to
// None, i.e. it strips the room and the frame time does not move, which is the silent
// quality drop for nothing.
//
// CONTROL 5 (the gate is decorative). Revert VfxAuraProximityCuller's two budget
// lines to `VfxLoopBudget.NearestAuraRing` and a bare `AmbientEnvBudget(...)`. Result:
// case (I) fails with both "AmbientRingNow" and "AuraRingNow" reported missing from
// the culler - which is the only way to catch a gate whose arithmetic is perfect and
// whose output nothing reads.
//
// ## NO HOLLOW PASSES
//
// Every case here is either pure arithmetic over compiled statics (no dependency to be
// missing) or a source read of a file whose ABSENCE IS A FAILURE, never a skip. There
// is no early return anywhere in this file that leaves a case unasserted.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class VfxPerformanceGateRegression
    {
        private const string CullerSrc = "Assets/_Modules/Village/Vfx/VfxAuraProximityCuller.cs";
        private const string GateSrc   = "Assets/_Modules/Village/Vfx/VfxPerformanceGate.cs";

        /// <summary>Every level in the ladder, low to high.</summary>
        private static readonly VfxShedLevel[] AllLevels =
        {
            VfxShedLevel.None,
            VfxShedLevel.AmbientTrim,
            VfxShedLevel.AmbientOff,
            VfxShedLevel.AuraTrim,
        };

        public static bool Run(out string reason)
        {
            var fails = new List<string>();

            CheckLadderSanity(fails);
            CheckAmbientIsShedFirst(fails);
            CheckAccessibilityIsExemptAtEveryLevel(fails);
            CheckTellStaysUnrefusableWithPoolSaturated(fails);
            CheckMeasureBeforeDegrade(fails);
            CheckDiscriminator(fails);
            CheckEscalationIsSustainedAndClamped(fails);
            CheckHysteresis(fails);
            CheckShedIsConsumed(fails);
            CheckOwnerRuledCeilingsUntouched(fails);

            if (fails.Count == 0)
            {
                Debug.Log("VFX_PERF_GATE_OK");
                reason = "VFX PERF GATE OK - ladder None/" + VfxShedLevel.AmbientTrim + "/" +
                         VfxShedLevel.AmbientOff + "/" + VfxShedLevel.AuraTrim +
                         " sheds ambient dress (ring " + VfxLoopBudget.AmbientEnvRing + " -> " +
                         VfxPerformanceGate.AmbientRingAt(VfxShedLevel.AmbientTrim, VfxLoopBudget.AmbientEnvRing) +
                         " -> 0) before it touches the enemy/pet ring (" + VfxLoopBudget.NearestAuraRing +
                         " -> " + VfxPerformanceGate.AuraRingAt(VfxShedLevel.AuraTrim, VfxLoopBudget.NearestAuraRing) +
                         ", floor 2); Aura_LowHealth / Aura_NearDeath are NEVER shed at any level and " +
                         "stay unrefusable with the pool saturated on all three tiers; degradation " +
                         "cannot arm without a measured low-occupancy baseline and releases entirely " +
                         "when that baseline is itself over budget; the ceilings " +
                         VfxLoopBudget.VillageLoops + "/" + VfxLoopBudget.DungeonLoops +
                         " and the " + VfxLoopBudget.AccessibilityReserve + "-slot reserve are untouched";
                return true;
            }

            reason = "vfx-perf-gate: " + string.Join("; ", fails);
            Debug.LogError("VFX_PERF_GATE_FAIL: " + reason);
            return false;
        }

        // -- (A) the ladder is a sane, non-increasing ceiling -----------------
        private static void CheckLadderSanity(List<string> fails)
        {
            int ambient = VfxLoopBudget.AmbientEnvRing;
            int aura    = VfxLoopBudget.NearestAuraRing;

            if (VfxPerformanceGate.AmbientRingAt(VfxShedLevel.None, ambient) != ambient)
                fails.Add("at shed level None the ambient ring is " +
                          VfxPerformanceGate.AmbientRingAt(VfxShedLevel.None, ambient) +
                          " instead of the authored " + ambient +
                          " - a gate that trims a healthy device is a SILENT quality drop");
            if (VfxPerformanceGate.AuraRingAt(VfxShedLevel.None, aura) != aura)
                fails.Add("at shed level None the enemy/pet ring is " +
                          VfxPerformanceGate.AuraRingAt(VfxShedLevel.None, aura) +
                          " instead of the authored " + aura);

            int prevAmbient = int.MaxValue, prevAura = int.MaxValue;
            for (int i = 0; i < AllLevels.Length; i++)
            {
                var lvl = AllLevels[i];
                int a = VfxPerformanceGate.AmbientRingAt(lvl, ambient);
                int e = VfxPerformanceGate.AuraRingAt(lvl, aura);

                if (a < 0 || a > ambient)
                    fails.Add("AmbientRingAt(" + lvl + ") = " + a + " - must stay within [0, " + ambient + "]");
                if (e < 0 || e > aura)
                    fails.Add("AuraRingAt(" + lvl + ") = " + e + " - must stay within [0, " + aura + "]");
                if (a > prevAmbient)
                    fails.Add("AmbientRingAt(" + lvl + ") = " + a + " is ABOVE the previous level's " +
                              prevAmbient + " - the ladder must be non-increasing");
                if (e > prevAura)
                    fails.Add("AuraRingAt(" + lvl + ") = " + e + " is ABOVE the previous level's " +
                              prevAura + " - the ladder must be non-increasing");
                prevAmbient = a; prevAura = e;
            }

            if (VfxPerformanceGate.AmbientRingAt(VfxPerformanceGate.MaxLevel, ambient) != 0)
                fails.Add("at the top of the ladder (" + VfxPerformanceGate.MaxLevel +
                          ") the ambient ring is still " +
                          VfxPerformanceGate.AmbientRingAt(VfxPerformanceGate.MaxLevel, ambient) +
                          " - room dress must be fully shed before the gate gives up");
            // An enemy aura is a ROLE READ. The bottom of the ladder trims it, never
            // switches it off.
            if (VfxPerformanceGate.AuraRingAt(VfxPerformanceGate.MaxLevel, aura) < 2)
                fails.Add("at the top of the ladder the enemy/pet ring is " +
                          VfxPerformanceGate.AuraRingAt(VfxPerformanceGate.MaxLevel, aura) +
                          " - the nearest bodies must keep their role read (floor 2)");
        }

        // -- (B) ambient dress is shed FIRST, and the ordering is not vacuous --
        private static void CheckAmbientIsShedFirst(List<string> fails)
        {
            int ambient = VfxLoopBudget.AmbientEnvRing;
            int aura    = VfxLoopBudget.NearestAuraRing;

            bool sawAmbientOnlyShed = false;

            for (int i = 0; i < AllLevels.Length; i++)
            {
                var lvl = AllLevels[i];
                int a = VfxPerformanceGate.AmbientRingAt(lvl, ambient);
                int e = VfxPerformanceGate.AuraRingAt(lvl, aura);

                if (e < aura && a != 0)
                    fails.Add("at shed level " + lvl + " the enemy/pet ring is trimmed to " + e +
                              " while the ambient ring is still " + a +
                              " - ambient room dress must be at ZERO before a combat aura is touched");

                if (a < ambient && e == aura) sawAmbientOnlyShed = true;
            }

            // GOOD PATH / non-vacuity: an ordering rule that never fires because nothing
            // is ever shed would pass the loop above trivially.
            if (!sawAmbientOnlyShed)
                fails.Add("no level in the ladder sheds ambient dress while leaving the enemy/pet " +
                          "ring at its authored " + aura +
                          " - the 'ambient first' ordering is vacuous, so it proves nothing");
        }

        // -- (C) the accessibility allowlist is exempt at EVERY level ----------
        private static void CheckAccessibilityIsExemptAtEveryLevel(List<string> fails)
        {
            var allow = VfxLoopBudget.AccessibilityLoops;

            if (allow == null || allow.Length != 2)
                fails.Add("VfxLoopBudget.AccessibilityLoops has " +
                          (allow == null ? "null" : allow.Length.ToString()) +
                          " member(s) - this suite and the WO-1229 overrun bound both assume exactly " +
                          "the two low-HP tell types");
            if (!VfxLoopBudget.IsAccessibilityLoop(VFXType.Aura_LowHealth))
                fails.Add("Aura_LowHealth is not on the accessibility allowlist");
            if (!VfxLoopBudget.IsAccessibilityLoop(VFXType.Aura_NearDeath))
                fails.Add("Aura_NearDeath is not on the accessibility allowlist");

            for (int i = 0; i < AllLevels.Length; i++)
            {
                var lvl = AllLevels[i];
                if (VfxPerformanceGate.MayShed(lvl, VFXType.Aura_LowHealth))
                    fails.Add("MayShed(" + lvl + ", Aura_LowHealth) is TRUE - the perf gate can " +
                              "silence the colourblind low-HP tell, which re-opens the exact hole " +
                              "WO-1229 closed (owner ruling 2026-08-26)");
                if (VfxPerformanceGate.MayShed(lvl, VFXType.Aura_NearDeath))
                    fails.Add("MayShed(" + lvl + ", Aura_NearDeath) is TRUE - the perf gate can " +
                              "silence the colourblind low-HP tell, which re-opens the exact hole " +
                              "WO-1229 closed (owner ruling 2026-08-26)");
            }

            // GOOD PATH. Without this the exemption above would also pass if MayShed
            // simply returned false for everything, and the gate would be inert.
            if (VfxPerformanceGate.MayShed(VfxShedLevel.None, VFXType.Env_Candle))
                fails.Add("MayShed(None, Env_Candle) is TRUE - nothing may be shed while the gate " +
                          "reports a healthy device");
            for (int i = 1; i < AllLevels.Length; i++)
            {
                if (!VfxPerformanceGate.MayShed(AllLevels[i], VFXType.Env_Candle))
                    fails.Add("MayShed(" + AllLevels[i] + ", Env_Candle) is FALSE - ordinary room " +
                              "dress must be sheddable, or the exemption is not discriminating and " +
                              "the gate cannot act at all");
            }
        }

        // -- (D) the tell is unrefusable with the pool saturated ---------------
        private static void CheckTellStaysUnrefusableWithPoolSaturated(List<string> fails)
        {
            int[] tiers = { VfxLoopBudget.VillageLoops, VfxLoopBudget.DungeonLoops, VfxLoopBudget.BossLoops };

            foreach (int cap in tiers)
            {
                // Well past saturation, including the allowlist's own bounded overrun.
                foreach (int live in new[] { cap, cap + 1, cap + 16, cap + 64 })
                {
                    if (VfxLoopBudget.WouldRefuseLoop(VFXType.Aura_LowHealth, live, cap))
                        fails.Add("Aura_LowHealth REFUSED at " + live + "/" + cap +
                                  " - the low-HP tell must start whatever the pool is doing");
                    if (VfxLoopBudget.WouldRefuseLoop(VFXType.Aura_NearDeath, live, cap))
                        fails.Add("Aura_NearDeath REFUSED at " + live + "/" + cap +
                                  " - the low-HP tell must start whatever the pool is doing");

                    // GOOD PATH: the cap must still BITE for everything else, or the
                    // assertion above is satisfied by a gate that refuses nothing.
                    if (!VfxLoopBudget.WouldRefuseLoop(VFXType.Env_Candle, live, cap))
                        fails.Add("Env_Candle was GRANTED at " + live + "/" + cap +
                                  " - the ceiling must still bite for non-allowlist loops");
                }
                // And below the ceiling ordinary loops must be granted - the third leg,
                // without which "the cap bites" could mean "everything is refused".
                if (VfxLoopBudget.WouldRefuseLoop(VFXType.Env_Candle, 0, cap))
                    fails.Add("Env_Candle refused at 0/" + cap + " - an empty pool must grant");
            }
        }

        // -- (E) MEASURE BEFORE YOU DEGRADE ------------------------------------
        private static void CheckMeasureBeforeDegrade(List<string> fails)
        {
            const float budget = 16.7f;     // 60fps
            const float awful  = 40f;       // far over budget, sustained

            for (int i = 0; i < AllLevels.Length; i++)
            {
                var start = AllLevels[i];
                var got = VfxPerformanceGate.Decide(start, awful, 0f, budget,
                                                    VfxPerformanceGate.SustainWindowsToShed * 3, 0,
                                                    out string why);
                if (got != start)
                    fails.Add("Decide(" + start + ", " + awful + "ms, NO BASELINE, budget " + budget +
                              "ms) moved to " + got + " - degradation must be impossible before the " +
                              "low-occupancy baseline is measured (CLAUDE.md section 12). why='" + why + "'");
                if (string.IsNullOrEmpty(why))
                    fails.Add("Decide returned an empty reason from " + start +
                              " with no baseline - a level decision with no traceable reason is the " +
                              "silent quality drop this ticket forbids");
            }
        }

        // -- (F) the discriminator: a slow device with an idle pool ------------
        private static void CheckDiscriminator(List<string> fails)
        {
            const float budget = 16.7f;
            float sickBaseline = budget * VfxPerformanceGate.OverFactor + 1f;

            for (int i = 0; i < AllLevels.Length; i++)
            {
                var start = AllLevels[i];
                var got = VfxPerformanceGate.Decide(start, 40f, sickBaseline, budget,
                                                    VfxPerformanceGate.SustainWindowsToShed * 3, 0,
                                                    out string why);
                if (got != VfxShedLevel.None)
                    fails.Add("Decide(" + start + ") with a baseline of " + sickBaseline.ToString("F1") +
                              "ms - ITSELF over the " + budget + "ms budget at a near-idle pool - " +
                              "returned " + got + " instead of None. Loops are not the cause there, " +
                              "so shedding dress lowers quality and buys nothing. why='" + why + "'");
            }
        }

        // -- (G) escalation is sustained and clamped ---------------------------
        private static void CheckEscalationIsSustainedAndClamped(List<string> fails)
        {
            const float budget   = 16.7f;
            const float healthy  = 12f;      // a good baseline: loops ARE implicated
            const float over     = 25f;

            // One window is not a trend.
            var got = VfxPerformanceGate.Decide(VfxShedLevel.None, over, healthy, budget, 1, 0, out _);
            if (got != VfxShedLevel.None)
                fails.Add("Decide escalated to " + got + " after a SINGLE over-budget window - " +
                          "degradation must be sustained over " + VfxPerformanceGate.SustainWindowsToShed +
                          " windows, or one scheduling stall strips the room");

            // Sustained IS a trend - and it steps exactly one level.
            for (int i = 0; i < AllLevels.Length - 1; i++)
            {
                var start = AllLevels[i];
                var want  = AllLevels[i + 1];
                got = VfxPerformanceGate.Decide(start, over, healthy, budget,
                                                VfxPerformanceGate.SustainWindowsToShed, 0, out string why);
                if (got != want)
                    fails.Add("Decide(" + start + ", sustained " + over + "ms over a " + budget +
                              "ms budget with a healthy " + healthy + "ms baseline) returned " + got +
                              " - expected exactly one step to " + want + ". why='" + why + "'");
            }

            // The ladder stops. It does not run off the end of the enum.
            got = VfxPerformanceGate.Decide(VfxPerformanceGate.MaxLevel, over, healthy, budget,
                                            VfxPerformanceGate.SustainWindowsToShed * 5, 0, out _);
            if (got != VfxPerformanceGate.MaxLevel)
                fails.Add("Decide escalated past MaxLevel to " + got +
                          " - the ladder must clamp, not walk off the end of the enum");

            // And recovery works, or a shed is permanent.
            for (int i = 1; i < AllLevels.Length; i++)
            {
                var start = AllLevels[i];
                var want  = AllLevels[i - 1];
                got = VfxPerformanceGate.Decide(start, healthy, healthy, budget, 0,
                                                VfxPerformanceGate.SustainWindowsToRecover, out string why);
                if (got != want)
                    fails.Add("Decide(" + start + ") did not recover to " + want + " after " +
                              VfxPerformanceGate.SustainWindowsToRecover + " under-budget window(s) - " +
                              "got " + got + ". A shed that never returns is a permanent quality " +
                              "drop off a transient stall. why='" + why + "'");
            }
            got = VfxPerformanceGate.Decide(VfxShedLevel.None, healthy, healthy, budget, 0,
                                            VfxPerformanceGate.SustainWindowsToRecover * 5, out _);
            if (got != VfxShedLevel.None)
                fails.Add("Decide recovered below None to " + got + " - the ladder must clamp at zero");
        }

        // -- (H) hysteresis: the band moves nothing ---------------------------
        private static void CheckHysteresis(List<string> fails)
        {
            const float budget  = 16.7f;
            const float healthy = 12f;

            if (VfxPerformanceGate.RecoverFactor >= VfxPerformanceGate.OverFactor)
                fails.Add("RecoverFactor " + VfxPerformanceGate.RecoverFactor + " is not below " +
                          "OverFactor " + VfxPerformanceGate.OverFactor +
                          " - with no band the ladder oscillates on the boundary");

            // Dead centre of the band, with BOTH counters saturated: nothing moves.
            float mid = budget * (VfxPerformanceGate.RecoverFactor + VfxPerformanceGate.OverFactor) * 0.5f;
            for (int i = 0; i < AllLevels.Length; i++)
            {
                var start = AllLevels[i];
                var got = VfxPerformanceGate.Decide(start, mid, healthy, budget,
                                                    VfxPerformanceGate.SustainWindowsToShed * 3,
                                                    VfxPerformanceGate.SustainWindowsToRecover * 3,
                                                    out string why);
                if (got != start)
                    fails.Add("Decide(" + start + ", " + mid.ToString("F1") + "ms) moved to " + got +
                              " - a frame time inside the hysteresis band around the " + budget +
                              "ms budget must move nothing. why='" + why + "'");
            }

            // A zero budget cannot be divided by, and must not shed.
            if (VfxPerformanceGate.Decide(VfxPerformanceGate.MaxLevel, 40f, healthy, 0f, 99, 0, out _)
                != VfxShedLevel.None)
                fails.Add("Decide with a zero frame budget did not return None - an unresolved " +
                          "budget must leave the authored rings in force");
        }

        // -- (I) the shed is actually consumed by the culler --------------------
        private static void CheckShedIsConsumed(List<string> fails)
        {
            // A MISSING FILE IS A FAILURE, NEVER A SKIP. A guard that returned here on a
            // missing dependency would land this whole case GREEN while proving nothing.
            if (!File.Exists(CullerSrc))
            {
                fails.Add("cannot read " + CullerSrc + " - the file that must consume the shed is " +
                          "missing, so the perf gate cannot be proved to do anything");
                return;
            }
            string culler = File.ReadAllText(CullerSrc);

            if (culler.IndexOf("VfxPerformanceGate.AmbientRingNow", System.StringComparison.Ordinal) < 0)
                fails.Add(CullerSrc + " does not read VfxPerformanceGate.AmbientRingNow - the ambient " +
                          "shed is computed and then ignored, i.e. the gate is decorative");
            if (culler.IndexOf("VfxPerformanceGate.AuraRingNow", System.StringComparison.Ordinal) < 0)
                fails.Add(CullerSrc + " does not read VfxPerformanceGate.AuraRingNow - the combat-ring " +
                          "shed is computed and then ignored");
            // The WO-1229 pool budget must still be in the ambient path: the shed is a
            // SECOND clamp on top of it, never a replacement for it.
            if (culler.IndexOf("VfxLoopBudget.AmbientEnvBudget", System.StringComparison.Ordinal) < 0)
                fails.Add(CullerSrc + " no longer reads VfxLoopBudget.AmbientEnvBudget - the WO-1229 " +
                          "accessibility reserve is enforced there and the perf shed must layer ON TOP " +
                          "of it, not replace it");
        }

        // -- (J) the owner-ruled numbers are untouched -------------------------
        private static void CheckOwnerRuledCeilingsUntouched(List<string> fails)
        {
            if (VfxLoopBudget.DungeonLoops != 48)
                fails.Add("VfxLoopBudget.DungeonLoops is " + VfxLoopBudget.DungeonLoops +
                          " - the 48 dungeon tier is owner-ruled and WO-1242 protects it, it does " +
                          "not re-litigate it");
            if (VfxLoopBudget.VillageLoops != 24)
                fails.Add("VfxLoopBudget.VillageLoops is " + VfxLoopBudget.VillageLoops +
                          " - the 24 village tier is owner-ruled");
            if (VfxLoopBudget.AmbientEnvRing != 8)
                fails.Add("VfxLoopBudget.AmbientEnvRing is " + VfxLoopBudget.AmbientEnvRing +
                          " - the ambient nearest-8 ring is owner-ruled; the perf gate trims the " +
                          "EFFECTIVE ring at runtime and must never change the authored one");
            if (VfxLoopBudget.AccessibilityReserve != 2)
                fails.Add("VfxLoopBudget.AccessibilityReserve is " + VfxLoopBudget.AccessibilityReserve +
                          " - the 2-slot reserve is owner-ruled");

            if (!File.Exists(GateSrc))
            {
                fails.Add("cannot read " + GateSrc + " - the perf gate source is missing, so it " +
                          "cannot be proved to own no lever over the refusal predicate");
                return;
            }
            // Strip comments BEFORE scanning. This lint asserts the gate owns no LEVER over
            // the refusal decision - a comment that merely NAMES WouldRefuseLoop while
            // explaining why the gate must not touch it is documentation, not a lever, and
            // failing on it punishes the very comment that keeps the invariant understood.
            //
            // This is the SAME defect class as the WalletIdentityRegression incident on
            // 2026-08-27, inverted: there a BLOCK COMMENT hid live code from a source lint
            // and the oracle went red against an unchanged invariant; here a comment is
            // mistaken FOR code. Any source lint that greps for a symbol must decide, on
            // purpose, whether comments count - and for a "does this code call X" assertion
            // they never do.
            string gate = StripCommentsForLint(File.ReadAllText(GateSrc));

            // The gate publishes RING SIZES and nothing else. If it ever reaches the
            // refusal predicate or the manager's own limit, the accessibility exemption
            // stops being structural and becomes a policy someone can get wrong.
            if (gate.IndexOf("WouldRefuseLoop", System.StringComparison.Ordinal) >= 0)
                fails.Add(GateSrc + " references WouldRefuseLoop - the perf gate must own NO lever " +
                          "over the loop refusal decision, or the accessibility exemption stops " +
                          "being structural");
            if (gate.IndexOf("_maxActiveLoops", System.StringComparison.Ordinal) >= 0)
                fails.Add(GateSrc + " references _maxActiveLoops - the perf gate must never move " +
                          "the loop ceiling; the ceiling is owner-ruled");
            if (gate.IndexOf("SetQuality", System.StringComparison.Ordinal) >= 0)
                fails.Add(GateSrc + " references SetQuality - the perf gate sheds ring POPULATION, " +
                          "not the global quality tier, which would gate the low-HP tell by MinQuality");
        }

        /// <summary>
        /// Removes block and line comments so a source lint reasons about CODE, never prose.
        /// Deliberately simple: this file's lints ask "does the gate CALL X", and a symbol named
        /// inside a comment is not a call. It is NOT a C# parser - it does not track string
        /// literals - which is safe here because every needle this suite greps for is an
        /// identifier, and an identifier inside a string literal would itself be suspicious.
        /// </summary>
        private static string StripCommentsForLint(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            src = System.Text.RegularExpressions.Regex.Replace(
                src, @"/\*[\s\S]*?\*/", " ");
            src = System.Text.RegularExpressions.Regex.Replace(
                src, @"//[^\n]*", " ");
            return src;
        }

    }
}
