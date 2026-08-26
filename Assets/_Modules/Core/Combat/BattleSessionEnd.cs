// =============================================================================
// BattleSessionEnd — WO-1233. ONE announcement that a battle session is over,
// and one place for every owner of a global to unwind its own state.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core (Core/Combat).
//
// ⛔ THE CAPTURED DEFECT THIS EXISTS FOR (2026-08-26, Seeker 2026.08.26.342290).
//
// NINE BATTLE_QUIESCENCE_FAIL events in one 735-entry break-log: EIGHT on an
// arena WIN, one on a retreat. The owner reported only the retreat ("doesnt do
// anything"); the dominant case was winning, which is exactly why a fix aimed at
// the retreat button would have closed the rarest instance and shipped the common
// one. The gate named the invariant but not the OWNER, so the first move was
// attribution, not a release call.
//
// THE PROVING LINE — the auto-harvested Player.log window captured alongside an
// identical "arena win / battle-lock still HELD" failure (logs/f8-inbox/archive/
// capture-20260821-102600-seq3545.md, scene Main_Castle_Overworld):
//
//   [Flow:HUD] context inputs: wave=False battleLock=True pursuit=True
//              inVillage=True modal=False buildMode=False
//              scene='Main_Castle_Overworld' -> Battle
//   [Flow:HeroOwner] scene='Main_Castle_Overworld' owner=HeroLocomotion
//              ownerCC=none ... timeScale=1.00 ... pos=(0.00, 0.08, -4.71)
//
// Read them together: the hero is HOME at the town anchor, the arena is torn down
// and the clock is 1.00 — so BattleArena's own probe, the ATB probe and the wave
// probe are all down (wave=False is printed on the same line) — and yet
// battleLock=True. The one thing still true on that line is pursuit=True, and
// PursuitBattleProbe returns PostureSignals.PursuitActive verbatim. The lock was
// NOT held by the battle that ended. It was held by the pursuit pulse window that
// the battle opened and NOTHING EVER CLOSED.
//
// AND THE HOLE IS EXACTLY ONE LINE WIDE: PostureSignals.ClearPursuits() is
// documented "hub return / COMBAT END", and before this file it had exactly ONE
// caller in the whole repository — HudPostureReset, which runs on a SCENE LOAD.
// The arena is staged IN-PLACE (~7 km out, no scene load, by design — see
// BattleQuiescenceGate's header for why the owner ruled against the scene swap),
// so in an arena session's entire lifetime nothing ever reached that clear. Every
// staged enemy that chased the hero left a pulse live for PursuitTtl (1.5 s) past
// its own destruction, and the gate settles at 0.75 s — which is why the RETREAT
// case fails deterministically and the win case fails whenever the warp home lands
// the hero next to a rep that is still chasing.
//
// WHY THIS IS STRUCTURAL AND NOT AN Nth RELEASE CALL. Eight paths failing
// identically is the tell that the release hangs off OUTCOMES. A battle can end as
// a win, a loss, a retreat, a watchdog break-off, a timeout, a hero-gone abandon or
// a stage-destroyed abandon — and every one of them already funnels through
// exactly TWO lifecycle ends (Resolve / ResolveAbandoned). So the session end is
// announced ONCE from there, and each owner of a global subscribes to unwind ITS
// OWN state. Adding a ninth outcome tomorrow inherits the unwind for free; that is
// the WO-1108 "one owner, one lifecycle" rule applied to teardown.
//
// WHY CLEARING PULSES CANNOT SUPPRESS A REAL FIGHT. PostureSignals pursuit is
// PULSE-based and re-reported every aggro tick by every live chaser. Clearing the
// ring drops only pulses whose reporter is gone; anything genuinely still pursuing
// re-reports on its next tick and the lock comes straight back. A stale pulse dies
// permanently, a live chase survives — which is precisely the discrimination the
// gate needed and never had.
//
// ⚠ WHAT THIS IS NOT: it is NOT a second writer of Time.timeScale, and it does NOT
// force BattleLock false. It owns no global itself. It announces, and it clears the
// one signal window the battle itself opened.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// WO-1233. The single end-of-battle-session announcement. Call
    /// <see cref="Release"/> from a battle's ONE lifecycle end (never per outcome);
    /// owners of global state register an unwind with <see cref="RegisterUnwind"/>.
    /// </summary>
    public static class BattleSessionEnd
    {
        private const string Sys = "Quiescence";

        private static readonly List<string> s_owners = new List<string>();
        private static readonly List<Action<string>> s_unwinds = new List<Action<string>>();

        /// <summary>Registered unwind owners, in registration order (diagnostics + regression).</summary>
        public static IReadOnlyList<string> Owners => s_owners;

        /// <summary>
        /// Register a named unwind that runs when a battle session ends. The argument is the
        /// session context ("arena win" / "retreat" / "abandoned: …") purely for the owner's own
        /// log line. Re-registering the same <paramref name="owner"/> REPLACES the previous
        /// delegate, so a re-created singleton can never accumulate duplicates.
        /// </summary>
        public static void RegisterUnwind(string owner, Action<string> unwind)
        {
            if (string.IsNullOrEmpty(owner) || unwind == null) return;
            for (int i = 0; i < s_owners.Count; i++)
            {
                if (s_owners[i] != owner) continue;
                s_unwinds[i] = unwind;
                return;
            }
            s_owners.Add(owner);
            s_unwinds.Add(unwind);
            FlowTrace.Step(Sys, $"battle-session unwind registered: '{owner}' ({s_owners.Count} owner(s) total).");
        }

        /// <summary>Remove an unwind by owner name. Safe for one never registered.</summary>
        public static void UnregisterUnwind(string owner)
        {
            for (int i = 0; i < s_owners.Count; i++)
            {
                if (s_owners[i] != owner) continue;
                s_owners.RemoveAt(i);
                s_unwinds.RemoveAt(i);
                return;
            }
        }

        /// <summary>
        /// A battle session has ended. Announce it ONCE from the battle's lifecycle end, not from
        /// each outcome branch. Clears the pursuit pulse window the battle opened, then runs every
        /// registered owner's unwind, then reports which battle-lock holders (if any) survive — so
        /// the next capture names the holder instead of restating that there is one.
        /// </summary>
        /// <param name="context">Short description for the log: "arena win", "retreat", "abandoned: …".</param>
        public static void Release(string context)
        {
            string before = BattleLock.DescribeHolders();

            // THE MISSING COMBAT-END CLEAR. See the file header: PostureSignals.ClearPursuits is
            // documented for exactly this moment and, before WO-1233, only a SCENE LOAD ever
            // reached it — which an in-place arena battle never performs.
            Guard.Try(Sys, "clear pursuit window at battle end", PostureSignals.ClearPursuits);

            for (int i = 0; i < s_unwinds.Count; i++)
            {
                int idx = i;
                Guard.Try(Sys, $"battle-session unwind '{s_owners[idx]}'", () => s_unwinds[idx](context));
            }

            string after = BattleLock.DescribeHolders();
            FlowTrace.Step(Sys,
                $"BATTLE_SESSION_RELEASED ({context}) - pursuit window cleared, {s_unwinds.Count} owner " +
                $"unwind(s) run. battle-lock holders before=[{before}] after=[{after}]. " +
                "An 'after' that is not 'none' is a LIVE holder, not a leak: pursuit re-reports every " +
                "aggro tick, so a chaser that is still chasing legitimately re-raises the lock here.");
        }

        /// <summary>Test/QA reset — drops every registered unwind. Never called by gameplay.</summary>
        public static void ResetForTests()
        {
            s_owners.Clear();
            s_unwinds.Clear();
        }
    }
}
