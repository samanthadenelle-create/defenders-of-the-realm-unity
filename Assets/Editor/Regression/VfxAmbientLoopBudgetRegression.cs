// =============================================================================
// VfxAmbientLoopBudgetRegression [vfx-ambient-budget] — WO-1229.
// The oracle that keeps AMBIENT ROOM DRESS from starving the colourblind low-HP tell.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// ## THE CAPTURED DEFECT THIS SUITE EXISTS TO CATCH
//
// Owner's device, dg_starter_loop (tmp/felt2/logcat-auth.txt, 08-25):
//
//   19:29:24.762 [Flow:DungeonVFX] bound 44 CandleAnchor marker(s) to proximity-pooled
//                                  Env_Candle flames in 'dg_starter_loop'.
//   19:30:29.174 [Flow:VFXManager] PlayLoop('Env_Candle')     SKIPPED — active loops 24/24
//        ... once a second, unbroken, to 19:31:26.309 ...
//                [Flow:VFXManager] PlayLoop('Aura_NearDeath') SKIPPED — active loops 24/24
//                [Flow:HeroHpAura] 'NearDeath' aura ('Aura_NearDeath') was REFUSED by
//                                  VFXManager (loop cap or quality gate). This is the
//                                  PRIMARY colourblind low-HP read — if it is being
//                                  dropped, the hero has no non-colour danger signal.
//
// Forty-four independent first-come claimants against a pool of twenty-four. The
// owner is red/green colourblind and the low-health tell is a LOOP, so a refused
// loop is a fight with no danger signal at all.
//
// ## WHAT IT ASSERTS
//
//   (A) DRAIN. Driven through a lease/release cycle with demand FAR above the cap,
//       the ambient hold never exceeds its ring, the pool never reaches its ceiling,
//       and when demand falls to zero the pool returns EXACTLY to its non-ambient
//       occupancy — no residue. A budget that only ever goes up is the leak shape
//       this project keeps paying for.
//   (B) THE RESERVE IS INVIOLABLE. Across the whole cross-product of pool occupancy
//       and ambient demand, ambient dress can never push the live count past
//       cap - AccessibilityReserve. That is what makes "Aura_NearDeath was REFUSED
//       for pool exhaustion" unreachable: whenever the non-ambient world leaves a
//       slot at all, the low-HP tell finds one.
//   (C) THE RING IS SANE — a ceiling, never a target; never negative; and small
//       enough to coexist with the enemy/pet ring inside the village tier.
//   (E) THE TELL IS UNREFUSABLE, FULL STOP (owner ruling 2, 2026-08-26). The pool is
//       filled from NON-ambient sources past the ceiling and Aura_LowHealth /
//       Aura_NearDeath must still grant — while the cap still bites for every other
//       type. The allowlist is pinned at exactly two members, because the bound on how
//       far the pool may exceed its ceiling is derived from that count.
//   (F) THE SCENE TIER RESOLVES, AND BINDS ITSELF (owner ruling 1, 2026-08-26). A
//       dungeon scene resolves the dungeon ceiling, a village scene the village one,
//       additively-loaded dungeons included; and the binding is a runtime hook, not an
//       authoring step. That last assertion is the one that matters: the tier spent its
//       entire life dead because it depended on a component nobody placed.
//   (D) THE CANDLE IS IN THE RING, STRUCTURALLY. DungeonCandleVfx implements
//       IProximityAura (compile-enforced) and its source registers with the AMBIENT
//       half and releases IMMEDIATELY. This is the assertion that stops the next
//       ambient loop class from being greenfielded unbudgeted — which is exactly how
//       the candle got here.
//
// ## POSITIVE CONTROL — PROVE IT CAN GO RED (WO-1138)
//
// CONTROL 1 (the real defect). In DriveCycle below, replace
// `VfxLoopBudget.AmbientEnvBudget(live, held, cap)` with a bare `demand` — that IS
// the pre-WO-1229 policy: every anchor in range lights. Run numerically before this
// file was written, cap=24 / non-ambient=10 / demand=44 (the capture's own numbers):
//     peak ambient hold=44, settled live=54, ticks at/over ceiling=64/64,
//     ticks inside reserve=64/64, first reserve violation at cap=24 non-ambient=0
// i.e. every one of (A)'s four assertions and (B) go red. With the shipped budget the
// same drive gives peak=8, settled live=18, 0 ticks at the ceiling, 0 in the reserve,
// no reserve violation at any cap — and release returns the pool to exactly 10.
//
// CONTROL 2 (the allowlist, ruling 2). Drop the IsAccessibilityLoop short-circuit from
// VfxLoopBudget.WouldRefuseLoop so it is a bare `liveLoops >= cap` — the pre-ruling
// check, and what the owner's device actually did. Run numerically before this file was
// written: Aura_NearDeath REFUSED at 24/24, 48/48 and 32/32, i.e. case (E) goes red at
// the first saturated pool on every tier, which is the captured
// "[Flow:HeroHpAura] 'NearDeath' aura ... was REFUSED by VFXManager" reproduced exactly.
// With the allowlist, neither type is refused at any live count up to cap+16.
//
// CONTROL 3 (the scene tier, ruling 1). Make ResolveDungeonTier return false
// unconditionally — that is not a hypothetical, it is what the shipped build did, since
// the only writer of the dungeon flag was reachable solely through a component present
// in zero scenes. Run numerically: dg_starter_loop resolves 24 instead of 48, and case
// (F) fails on it. It is the same 24 that appears in every captured
// "SKIPPED — active loops 24/24" line from that dungeon.
//
// CONTROL 4 (the reserve itself). Set AccessibilityReserve to 0: case (C) goes red on
// its two explicit reserve assertions. Note that (A)/(B) stay green there, because the
// fixed ring alone still keeps a 24-slot pool off its ceiling — which is the point of
// having BOTH a ring and a reserve, and the reason (C) asserts the reserve directly
// rather than trusting the ring to imply it.
//
// ⚠ THIS SUITE STILL PINS NO CEILING VALUE. VillageLoops / DungeonLoops / BossLoops are
// owner-tunable felt bones; pinning them would turn a tuning pass into a red gate, and
// would quietly bless "raise the cap" as the remedy for saturation — the remedy this
// ticket exists to reject. Case (F) asserts only that a dungeon scene RESOLVES the
// dungeon constant and a village scene the village one, whatever those constants are.
// Re-tune them freely; the suite follows.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class VfxAmbientLoopBudgetRegression
    {
        private const string CandleSrc = "Assets/_Modules/Dungeons/DungeonCandleVfx.cs";

        public static bool Run(out string reason)
        {
            var fails = new List<string>();

            CheckRingSanity(fails);
            CheckDrain(fails);
            CheckReserveIsInviolable(fails);
            CheckCandleIsInTheRing(fails);
            CheckAccessibilityLoopsAreUnrefusable(fails);
            CheckSceneTier(fails);

            if (fails.Count == 0)
            {
                Debug.Log("VFX_AMBIENT_BUDGET_OK");
                reason = "VFX AMBIENT BUDGET OK — ambient ring max " + VfxLoopBudget.AmbientEnvRing +
                         ", accessibility reserve " + VfxLoopBudget.AccessibilityReserve +
                         "; ambient dress drains to zero on demand release and can never occupy the " +
                         "reserve, so Aura_LowHealth / Aura_NearDeath cannot be refused for pool " +
                         "exhaustion; the allowlist makes Aura_LowHealth / Aura_NearDeath " +
                         "unrefusable outright; dungeon scenes resolve " + VfxLoopBudget.DungeonLoops +
                         " loops and village scenes " + VfxLoopBudget.VillageLoops + ", bound by a " +
                         "runtime hook rather than by scene authoring";
                return true;
            }

            reason = "vfx-ambient-budget: " + string.Join("; ", fails);
            Debug.LogError("VFX_AMBIENT_BUDGET_FAIL: " + reason);
            return false;
        }

        // ── (C) the ring is a sane ceiling ───────────────────────────────────
        private static void CheckRingSanity(List<string> fails)
        {
            int ring    = VfxLoopBudget.AmbientEnvRing;
            int reserve = VfxLoopBudget.AccessibilityReserve;
            int village = VfxLoopBudget.VillageLoops;

            if (ring <= 0)
                fails.Add("AmbientEnvRing is " + ring + " — ambient dress would never light at all");
            if (reserve <= 0)
                fails.Add("AccessibilityReserve is " + reserve + " — with no reserve the colourblind " +
                          "low-HP tell can be outbid by decoration, which IS the WO-1229 defect");
            // The tell swaps recipe below quarter health, so the outgoing loop's return can
            // overlap the incoming one. A reserve of one is a race.
            if (reserve < 2)
                fails.Add("AccessibilityReserve is " + reserve + " — the low-HP tell swaps recipe " +
                          "below quarter health, so a single slot can be lost to the overlap");
            if (ring + VfxLoopBudget.NearestAuraRing + reserve > village)
                fails.Add("ambient ring " + ring + " + enemy/pet ring " + VfxLoopBudget.NearestAuraRing +
                          " + reserve " + reserve + " exceeds the village tier " + village +
                          " — the two rings can saturate the pool between them before anything else plays");

            // Never negative, never above the ring, whatever it is handed.
            foreach (int live in new[] { -5, 0, 7, 24, 48, 999 })
            foreach (int held in new[] { -3, 0, 4, 60 })
            {
                int b = VfxLoopBudget.AmbientEnvBudget(live, held, VfxLoopBudget.VillageLoops);
                if (b < 0 || b > ring)
                    fails.Add("AmbientEnvBudget(live=" + live + ", held=" + held + ") = " + b +
                              " — must stay within [0, " + ring + "]");
            }
            if (VfxLoopBudget.AmbientEnvBudget(0, 0, 0) != 0)
                fails.Add("AmbientEnvBudget with a zero cap must grant nothing");
        }

        // ── (A) lease / release drives the hold UP and back to ZERO ──────────
        private static void CheckDrain(List<string> fails)
        {
            const int cap    = 24;    // the village tier the capture was taken at
            const int others = 10;    // POI auras, portal aura, ground fog, Heart aura...
            const int demand = 44;    // the anchors dg_starter_loop actually bound

            int peak = DriveCycle(cap, others, demand, out int liveAtPeak, out int overCeiling, out int intoReserve);

            if (peak <= 0)
                fails.Add("lease cycle granted NOTHING with " + demand + " candidates and " +
                          (cap - others) + " slots free — the ambient ring is dead, not budgeted");
            if (peak > VfxLoopBudget.AmbientEnvRing)
                fails.Add("lease cycle peaked at " + peak + " ambient holds, above the ring " +
                          VfxLoopBudget.AmbientEnvRing);
            if (overCeiling > 0)
                fails.Add("ambient demand pushed the pool to its ceiling on " + overCeiling +
                          " tick(s) (live=" + liveAtPeak + "/" + cap + ") — this is the captured " +
                          "'SKIPPED — active loops 24/24' state reproduced in the model");
            if (intoReserve > 0)
                fails.Add("ambient dress entered the " + VfxLoopBudget.AccessibilityReserve +
                          "-slot accessibility reserve on " + intoReserve + " tick(s) — the low-HP " +
                          "tell would be refused there");

            // RELEASE: demand falls to zero (the hero walks out of every anchor's range).
            // The pool must return EXACTLY to its non-ambient occupancy — a pool that only
            // fills is the leak this ticket was filed as.
            DriveCycle(cap, others, 0, out int liveAfterRelease, out _, out _);
            if (liveAfterRelease != others)
                fails.Add("after demand fell to zero the pool sat at " + liveAfterRelease +
                          " with only " + others + " non-ambient loops live — the ambient hold did " +
                          "not DRAIN");
        }

        /// <summary>
        /// Runs the real production arithmetic through a settle loop, exactly as
        /// VfxAuraProximityCuller.TickAmbient drives it: budget from the live pool, hold
        /// the lesser of budget and demand, recompute. Returns the peak ambient hold and
        /// reports how often the pool touched its ceiling or the reserve.
        /// </summary>
        private static int DriveCycle(int cap, int others, int demand,
                                      out int finalLive, out int overCeiling, out int intoReserve)
        {
            int held = 0, peak = 0;
            overCeiling = 0;
            intoReserve = 0;

            for (int tick = 0; tick < 64; tick++)
            {
                int live   = others + held;
                int budget = VfxLoopBudget.AmbientEnvBudget(live, held, cap);
                held       = Mathf.Min(demand, budget);

                live = others + held;
                if (held > peak) peak = held;
                if (live >= cap) overCeiling++;
                if (live > cap - VfxLoopBudget.AccessibilityReserve) intoReserve++;
            }

            finalLive = others + held;
            return peak;
        }

        // ── (B) the reserve is inviolable, across the whole cross-product ────
        private static void CheckReserveIsInviolable(List<string> fails)
        {
            int reserve = VfxLoopBudget.AccessibilityReserve;

            foreach (int cap in new[] { VfxLoopBudget.VillageLoops, VfxLoopBudget.DungeonLoops, VfxLoopBudget.BossLoops })
            {
                for (int others = 0; others <= cap; others++)
                {
                    // Demand far beyond anything a room could author.
                    DriveCycle(cap, others, 64, out int live, out _, out _);

                    // Ambient must never be the reason the pool has no room left. Once the
                    // non-ambient world has already eaten the reserve, ambient must be at
                    // zero and the situation is not ambient's doing.
                    if (others <= cap - reserve && live > cap - reserve)
                    {
                        fails.Add("cap=" + cap + ", non-ambient=" + others + ": ambient dress settled the " +
                                  "pool at " + live + ", inside the " + reserve + "-slot reserve — " +
                                  "Aura_NearDeath would be REFUSED for pool exhaustion (the captured defect)");
                        break;
                    }
                    if (others > cap - reserve && live != others)
                    {
                        fails.Add("cap=" + cap + ", non-ambient=" + others + ": the pool was already inside " +
                                  "the reserve and ambient still took " + (live - others) + " slot(s)");
                        break;
                    }
                }
            }
        }

        // ── (D) the candle is structurally inside the ring ───────────────────
        private static void CheckCandleIsInTheRing(List<string> fails)
        {
            var candle = typeof(DeNelle.Dungeons.DungeonCandleVfx);

            if (!typeof(IProximityAura).IsAssignableFrom(candle))
                fails.Add("DungeonCandleVfx no longer implements IProximityAura — it is back to being " +
                          "an unbudgeted first-come claimant on the global loop pool (WO-1229)");

            if (!File.Exists(CandleSrc))
            {
                fails.Add("cannot read " + CandleSrc + " to verify the ambient registration");
                return;
            }

            string src = File.ReadAllText(CandleSrc);
            if (!src.Contains("RegisterAmbient"))
                fails.Add("DungeonCandleVfx does not call VfxAuraProximityCuller.RegisterAmbient — " +
                          "implementing the interface without joining the ring budgets nothing");
            if (!src.Contains("UnregisterAmbient"))
                fails.Add("DungeonCandleVfx does not call UnregisterAmbient — a dead registration holds " +
                          "a ranking slot forever and starves a live candle (the leak shape, one level up)");
            if (!src.Contains("_handle.Stop(true)"))
                fails.Add("DungeonCandleVfx no longer releases its flame IMMEDIATELY — the graceful path " +
                          "defers the loop-registry removal (which IS the decrement) by 2.5 s per candle");
        }

        // ── (E) THE ALLOWLIST — the tell is never refused for ANY exhaustion ──
        //
        // Case (B) proves ambient dress cannot starve the tell. That is not the same as
        // the tell being UNREFUSABLE: enemy auras, POI markers, tower projectiles and
        // portal loops can still fill the pool between them, and the line the owner
        // actually lived through was a refusal, not a near-miss. Owner ruling
        // 2026-08-26: the two colourblind low-HP types bypass the cap outright. So this
        // case fills the pool from NON-AMBIENT sources — right past the ceiling — and
        // asserts both types still grant.
        private static void CheckAccessibilityLoopsAreUnrefusable(List<string> fails)
        {
            var list = VfxLoopBudget.AccessibilityLoops;

            // The BOUND on the overrun rests entirely on this list staying at two members
            // owned by one driver (HeroHpStateAura holds exactly one handle). A third
            // member, or a different pair, silently widens how far over the ceiling the
            // pool may go — so the list itself is pinned.
            if (list == null || list.Length != 2)
                fails.Add("VfxLoopBudget.AccessibilityLoops has " + (list == null ? "no" : list.Length.ToString()) +
                          " member(s); it must be exactly the two colourblind low-HP types, because the " +
                          "over-cap bound is derived from that count");
            else
            {
                if (!VfxLoopBudget.IsAccessibilityLoop(VFXType.Aura_LowHealth))
                    fails.Add("Aura_LowHealth is not on the accessibility allowlist");
                if (!VfxLoopBudget.IsAccessibilityLoop(VFXType.Aura_NearDeath))
                    fails.Add("Aura_NearDeath is not on the accessibility allowlist");
            }

            foreach (int cap in new[] { VfxLoopBudget.VillageLoops, VfxLoopBudget.DungeonLoops, VfxLoopBudget.BossLoops })
            {
                // Well past the ceiling — the allowlist must hold even when the pool is
                // already over it (which the allowlist itself can cause).
                for (int live = 0; live <= cap + 16; live++)
                {
                    if (VfxLoopBudget.WouldRefuseLoop(VFXType.Aura_LowHealth, live, cap))
                    { fails.Add("Aura_LowHealth REFUSED at " + live + "/" + cap + " — the colourblind low-HP tell must never be refused for pool exhaustion (WO-1229 ruling 2)"); break; }
                    if (VfxLoopBudget.WouldRefuseLoop(VFXType.Aura_NearDeath, live, cap))
                    { fails.Add("Aura_NearDeath REFUSED at " + live + "/" + cap + " — the colourblind low-HP tell must never be refused for pool exhaustion (WO-1229 ruling 2)"); break; }
                }

                // …and the cap must still BITE for everything else, or the allowlist has
                // been widened into "no cap at all".
                if (!VfxLoopBudget.WouldRefuseLoop(VFXType.Env_Candle, cap, cap))
                    fails.Add("Env_Candle was NOT refused at " + cap + "/" + cap + " — the loop cap has stopped applying to ordinary loops");
                if (VfxLoopBudget.WouldRefuseLoop(VFXType.Env_Candle, cap - 1, cap))
                    fails.Add("Env_Candle was refused at " + (cap - 1) + "/" + cap + " — the cap is biting one slot early");
            }
        }

        // ── (F) THE SCENE TIER — 48 in a dungeon, 24 in town, bound by nobody ──
        //
        // Owner ruling 2026-08-26. Red on the pre-WO-1229 tree in the strongest possible
        // sense: the dungeon tier had never engaged in a shipped build at all. The only
        // writer of the flag was VFXManager.ApplyDungeonMode, whose only caller is
        // DungeonSceneBootstrap — a MonoBehaviour that `grep -rl DungeonSceneBootstrap
        // Assets --include=*.unity --include=*.prefab` finds in ZERO scenes and ZERO
        // prefabs. dg_starter_loop therefore resolved 24, which is exactly the ceiling in
        // the captured "SKIPPED — active loops 24/24" lines.
        private static void CheckSceneTier(List<string> fails)
        {
            // The naming convention, both directions.
            var dungeonNames = new[] { "dg_starter_loop", "dg_folks_granary", "dg_hollow_roads", "DungeonCompose" };
            foreach (var n in dungeonNames)
                if (!VfxLoopBudget.IsDungeonSceneName(n))
                    fails.Add("IsDungeonSceneName(\"" + n + "\") is false — a dungeon scene would run on the village ceiling");

            var villageNames = new[] { "Main_Castle_Overworld", "Village2", "MainCastle_Hall", "", null };
            foreach (var n in villageNames)
                if (VfxLoopBudget.IsDungeonSceneName(n))
                    fails.Add("IsDungeonSceneName(\"" + (n ?? "null") + "\") is true — a town scene would get the dungeon ceiling");

            // A dungeon scene resolves 48; a village scene resolves 24.
            if (!VfxLoopBudget.ResolveDungeonTier(new[] { "dg_starter_loop" }))
                fails.Add("a lone dungeon scene does not resolve the dungeon tier");
            if (VfxLoopBudget.TierCapFor(true, false) != VfxLoopBudget.DungeonLoops)
                fails.Add("dungeon tier resolves " + VfxLoopBudget.TierCapFor(true, false) +
                          " loops, expected " + VfxLoopBudget.DungeonLoops);
            if (VfxLoopBudget.ResolveDungeonTier(new[] { "Main_Castle_Overworld" }))
                fails.Add("a village scene resolves the DUNGEON tier");
            if (VfxLoopBudget.TierCapFor(false, false) != VfxLoopBudget.VillageLoops)
                fails.Add("village tier resolves " + VfxLoopBudget.TierCapFor(false, false) +
                          " loops, expected " + VfxLoopBudget.VillageLoops);

            // ADDITIVE. Dungeons load alongside a persistent hub, so a "the scene that
            // just loaded wins" implementation looks correct in isolation and drops the
            // tier the instant anything else loads on top. This is that case.
            if (!VfxLoopBudget.ResolveDungeonTier(new[] { "Main_Castle_Overworld", "dg_folks_granary" }))
                fails.Add("a dungeon loaded ADDITIVELY beside the hub does not resolve the dungeon tier — " +
                          "the resolver is reading one scene instead of the loaded set");

            // The MAX rule is unchanged by any of this (a boss inside a dungeon is 48).
            if (VfxLoopBudget.TierCapFor(true, true) != Mathf.Max(VfxLoopBudget.DungeonLoops, VfxLoopBudget.BossLoops))
                fails.Add("dungeon+boss no longer resolves the MAX of the two tiers");

            // ── THE INVARIANT THAT STOPS IT GOING DEAD AGAIN ──
            // The old seam died because it needed a human to place a component. This one
            // must be bound by a runtime hook that no scene author can fail to perform.
            // If that hook is ever deleted, this suite goes red BEFORE a device session
            // has to discover it by absence for a second time.
            bool hasRuntimeHook = false;
            foreach (var m in typeof(VfxLoopBudget).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                { hasRuntimeHook = true; break; }
            if (!hasRuntimeHook)
                fails.Add("VfxLoopBudget has no [RuntimeInitializeOnLoadMethod] hook — the dungeon tier is " +
                          "back to depending on someone remembering to declare it, which is exactly how it " +
                          "spent its whole life in zero scenes (WO-1229)");
            if (typeof(VfxLoopBudget).GetMethod("RebindSceneTier", BindingFlags.Static | BindingFlags.Public) == null)
                fails.Add("VfxLoopBudget.RebindSceneTier is gone — nothing re-resolves the tier from the loaded scene set");
        }
    }
}
