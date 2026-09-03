// =============================================================================
// BattleQuiescenceGate — WO-1127. Assert the world is back to baseline after a
// battle, and name exactly which invariant is not.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core (Core/Combat). Core-only by construction: the invariants
// it owns are all Core-visible (Time, BattleLock, PanelManager). Anything that
// needs a Village/Dungeon type arrives as a REGISTERED PROBE (see Register), so
// the asmdef boundary is never crossed and the gate stays usable from any module.
//
// ⛔ WHY THIS EXISTS — the incident, and the architecture it replaced.
//
// 2026-08-20, owner device session. She fought in town, won, and could not move:
//   [Flow:HeroOwner] ... scriptedMove=off inputSuppressed=False timeScale=0.04 dt=0.0013
// Input was never blocked. The WORLD was running at 4% speed and stayed there for
// 182 consecutive samples across three minutes, starting the instant the battle
// resolved. A cosmetic hit-stop had leaked a global and NOTHING NOTICED.
//
// The owner's first instinct was a full scene swap (save -> tear down -> load an
// arena scene -> reward -> tear down -> reload town), on the reasoning that a scene
// load guarantees teardown. It does not guarantee the teardown that matters here:
//   * Time.timeScale is an ENGINE GLOBAL. SceneManager.LoadScene does not reset it.
//     0.04 would have ridden straight through the load — a frozen town AFTER a
//     loading screen.
//   * DontDestroyOnLoad: 350 call sites across 212 files in this repo, HitStopManager
//     among them. ~290 mutable statics in the Vfx+Arena modules alone. All survive.
//   * Measured on the owner's Seeker: LoadScene -> hub loaded = 3.75s, ~5s to a
//     steady frame. Round trip ~7.5s x 13 battles in one session = ~90s of loading.
// She ruled for the contract over the swap. This is the contract.
//
// WHAT IT IS NOT. It is not a repair mechanism and it is not one more owner of
// Time.timeScale. It OBSERVES and REPORTS. A gate that quietly fixes things trains
// everyone to stop reading it and hides the real owner of the bug.
//
// ⚠ THE OWNER LIST WAS STALE AND IS CORRECTED (2026-09-02, CLAUDE.md §15). It read
// "seven: HitStopManager, CombatFeedbackManager, ArenaDeathCam, WaveCelebrationManager,
// PauseController, HeroHitReaction, GameOverScreen" — wrong in BOTH directions, which
// is worse than no list, because attribution is this gate's entire job and a reader
// chasing a leaked clock would have hunted a file that does not write it and never
// opened three that do.
//   * PauseController DOES NOT WRITE Time.timeScale. WO-1149 moved the freeze into
//     DeNelle.Core.UI.WorldHold and converted the pause menu into a CLIENT of it
//     (PauseController.cs:67-79, :260 — it takes WorldHold.Acquire(ReasonPauseMenu)).
//     The only "timeScale" left in that file is prose.
//   * MISSING: WorldHold (Core/UI — the ref-counted freeze owner every pause/purchase
//     hold routes through), BreakCaptureHarness (Core/Diagnostics — the F8 note freeze),
//     BugReportView (HUD).
// Verified by reading every `Time.timeScale =` assignment under Assets/_Modules/. The
// live RUNTIME writers are NINE:
//     WorldHold, BreakCaptureHarness, BugReportView, HitStopManager,
//     CombatFeedbackManager, ArenaDeathCam, WaveCelebrationManager, HeroHitReaction,
//     GameOverScreen
// plus this gate's own last-resort restore at the bottom of Arm (which is a safety net,
// not an owner) and the dev-only DevTools/GateTraversalProof + VfxParade tools.
// KEEP THIS LIST HONEST OR DELETE IT — grep `Time.timeScale =` under Assets/_Modules/.
//
// ⚠ AMENDED 2026-08-30 (WO-1233b) — IT NOW SELF-HEALS, AND THE ORDER IS THE POINT.
// "Reports and does not repair" left the player at 4% speed with a stuck lock while
// the log said so perfectly, which is a diagnosis, not a fix. On a FAIL the gate now
// (1) reports, loudly, first — always; (2) re-drives the ONE authoritative exit,
// BattleSessionEnd.Release, so the recovery runs each owner's OWN unwind by name
// rather than stamping globals from here; (3) only then falls back to writing
// timeScale = 1 itself. It still adds no release call and forces no lock false. The
// discipline the paragraph above protects is intact: nothing is healed silently.
//
// TIMING. Some invariants legitimately settle over a frame or two (posture
// transitions, the reward modal's own open), so a one-shot check on the resolve
// frame would false-positive — and a gate that fails on correct behaviour is a gate
// people learn to ignore. Arm() therefore waits for the reward screen to close and
// then re-checks on the UNSCALED clock, which is the only clock that still advances
// when the very defect it hunts is present.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Core.Combat
{
    /// <summary>One named invariant the world must satisfy once a battle is over.</summary>
    public sealed class QuiescenceProbe
    {
        /// <summary>Short stable name, used in the failure text (e.g. "timeScale", "hero-owner").</summary>
        public string Name;

        /// <summary>
        /// Returns null when the invariant holds, or a HUMAN-READABLE reason when it does not.
        /// The reason must name the observed value — "hero owner is wrong" costs a debugging
        /// session that "hero owner = FOREIGN-CC" does not.
        /// </summary>
        public Func<string> Check;
    }

    /// <summary>
    /// WO-1127. The battle-end teardown contract. <see cref="Arm"/> at battle resolve;
    /// the gate settles, checks every invariant, and reports.
    /// </summary>
    public static class BattleQuiescenceGate
    {
        private const string Sys = "Quiescence";

        public const string MarkerOk   = "BATTLE_QUIESCENCE_OK";
        public const string MarkerFail = "BATTLE_QUIESCENCE_FAIL";

        /// <summary>
        /// Emitted instead of a verdict when a NEW battle session began while this gate was still
        /// settling on the previous one. Not a failure and not a pass — the observer withdraws,
        /// because the state in front of it belongs to a fight that is still happening.
        /// </summary>
        public const string MarkerSuperseded = "BATTLE_QUIESCENCE_SUPERSEDED";

        /// <summary>timeScale tolerance. Wide enough that a legitimate 1.0 never trips it.</summary>
        private const float ScaleEpsilon = 0.01f;

        /// <summary>
        /// How long (unscaled) to let the world settle after the reward screen closes before
        /// judging it. Generous on purpose: a false failure is more expensive than a late one,
        /// because it is the thing that gets a gate switched off.
        /// </summary>
        private const float SettleSeconds = 0.75f;

        /// <summary>
        /// Hard cap (unscaled) on waiting for the reward screen to close. A reward screen that
        /// never closes is itself a defect, and the gate must report rather than wait forever —
        /// silence is the failure mode this whole ticket exists to end.
        /// </summary>
        private const float ModalWaitCapSeconds = 60f;

        private static readonly List<QuiescenceProbe> s_extra = new List<QuiescenceProbe>();

        /// <summary>
        /// Register a module-specific invariant (orphaned arena actors, hero owner, …). Village and
        /// Dungeon types cannot be referenced from Core, so they arrive here instead. Re-registering
        /// the same <see cref="QuiescenceProbe.Name"/> REPLACES the previous one, so a scene reload
        /// that re-installs its probes cannot accumulate duplicates.
        /// </summary>
        public static void Register(QuiescenceProbe probe)
        {
            if (probe == null || string.IsNullOrEmpty(probe.Name) || probe.Check == null) return;
            for (int i = 0; i < s_extra.Count; i++)
            {
                if (s_extra[i].Name == probe.Name) { s_extra[i] = probe; return; }
            }
            s_extra.Add(probe);
            FlowTrace.Step(Sys, $"probe registered: '{probe.Name}' ({s_extra.Count} module probe(s) total)");
        }

        /// <summary>Remove a probe by name. Safe to call for one never registered.</summary>
        public static void Unregister(string name)
        {
            for (int i = 0; i < s_extra.Count; i++)
            {
                if (s_extra[i].Name == name) { s_extra.RemoveAt(i); return; }
            }
        }

        /// <summary>Registered module probes, for the regression suite. Never mutate.</summary>
        public static IReadOnlyList<QuiescenceProbe> ModuleProbes => s_extra;

        // =====================================================================
        //  The check itself
        // =====================================================================

        /// <summary>
        /// Evaluate every invariant NOW and return the failures, most fundamental first.
        /// Pure and synchronous — this is the entry point the regression suite drives, which is
        /// what lets the suite prove the gate FAILS each known-bad state rather than only that it
        /// passes a clean one.
        /// </summary>
        /// <param name="rewardScreenOpen">
        /// True while the reward/victory screen is legitimately up. The modal invariant is SKIPPED
        /// then: an open reward screen is correct behaviour, and failing on it is the fastest way to
        /// teach everyone to ignore this gate.
        /// </param>
        public static List<string> Evaluate(bool rewardScreenOpen)
        {
            var failures = new List<string>();

            // 1. timeScale — THE captured defect, and the cheapest check for the most
            //    player-visible failure there is.
            Guard.Try(Sys, "probe timeScale", () =>
            {
                float ts = Time.timeScale;
                if (Mathf.Abs(ts - 1f) > ScaleEpsilon)
                {
                    failures.Add($"timeScale: the world clock is {ts:F2} ({ts * 100f:F0}% speed), not 1.00. " +
                                 "The player will read this as frozen or unresponsive controls even though " +
                                 "input is fine — this is the exact 2026-08-20 defect (a leaked hit-stop).");
                }
            });

            // 2. battle lock — a stuck lock suppresses combat input and pins the HUD out of its
            //    town context indefinitely.
            Guard.Try(Sys, "probe battle lock", () =>
            {
                if (BattleLock.IsInBattle())
                    // WO-1233: the original sentence is preserved VERBATIM and the HOLDER is
                    // APPENDED. Nine of these fired on the owner's device on 2026-08-26 and not one
                    // said who held the lock — the holder had to be reconstructed from a HUD line in
                    // a neighbouring log. This is a strengthening of the message, never a narrowing:
                    // the finding fires on exactly the same condition it always did.
                    failures.Add("battle-lock: still HELD after the battle ended. Combat input stays " +
                                 "suppressed and the HUD cannot return to its town context. " +
                                 $"HOLDER(S): {BattleLock.DescribeHolders()} (of {BattleLock.ProbeCount} " +
                                 $"registered: {BattleLock.DescribeAll()}).");
            });

            // 3. modal — the Echo-modal FTUE cascade was exactly this, and it is invisible until a
            //    tap goes nowhere. Only meaningful once the reward screen is down.
            if (!rewardScreenOpen)
            {
                Guard.Try(Sys, "probe modal arbiter", () =>
                {
                    if (PanelManager.AnyOpen)
                        // WO-1337: the original sentence is preserved VERBATIM and the HOLDER is
                        // APPENDED — the same strengthening WO-1233 applied to the battle-lock
                        // finding, and for the same reason. F8 seq 4677 reported this invariant on
                        // the owner's device and named no panel, so the capture proved a handle was
                        // stuck and could not say which one: unactionable by construction. This is
                        // an addition, never a narrowing — it fires on exactly the condition it
                        // always did.
                        failures.Add("modal: a panel handle is STILL OPEN after the reward screen closed. " +
                                     "The world interact button stays suppressed underneath and the back " +
                                     "button targets a panel the player cannot see. " +
                                     $"HOLDER: {PanelManager.DescribeOpen()}.");
                });
            }

            // 4..n — module probes (orphaned arena actors, hero owner, combat input gating).
            //        Guarded individually: one bad probe must never hide the others' findings, and
            //        a diagnostic must never take down a battle resolve.
            for (int i = 0; i < s_extra.Count; i++)
            {
                var p = s_extra[i];
                if (p == null) continue;
                Guard.Try(Sys, $"probe '{p.Name}'", () =>
                {
                    string why = p.Check();
                    if (!string.IsNullOrEmpty(why)) failures.Add($"{p.Name}: {why}");
                });
            }

            return failures;
        }

        // =====================================================================
        //  Arming (drive this from battle resolve)
        // =====================================================================

        /// <summary>
        /// Arm the gate at battle resolve. Returns an enumerator the caller runs as a coroutine:
        /// it waits out the reward screen, lets the world settle on the UNSCALED clock, then
        /// evaluates and reports.
        ///
        /// <para>Unscaled throughout ON PURPOSE — a scaled wait would be slowed by the very defect
        /// this gate exists to catch, and at timeScale 0.04 a 0.75 s settle becomes 19 s.</para>
        /// </summary>
        /// <param name="isRewardScreenOpen">
        /// Polled to know when the reward screen closes. Pass null when a battle ends with no
        /// reward screen (a retreat), and the settle begins immediately.
        /// </param>
        /// <param name="context">Short description for the log, e.g. "arena win" / "retreat".</param>
        public static System.Collections.IEnumerator Arm(Func<bool> isRewardScreenOpen, string context)
        {
            float waitStarted = Time.unscaledTime;

            // WO-1233b — WHICH BATTLE AM I TALKING ABOUT? Captured at arm time, i.e. AFTER the
            // resolving battle's own BattleSessionEnd.Release. Any later bump is a battle that
            // started while this gate was settling, and its live state is not ours to judge.
            // See BattleSessionEnd's SESSION EPOCH block for the 2026-08-30 device proof.
            int armedEpoch = BattleSessionEnd.Epoch;

            // Wait out the reward screen — but never forever. A reward screen that never closes is
            // its own defect and must be REPORTED, not waited on in silence.
            bool cappedOut = false;
            while (isRewardScreenOpen != null && SafeIsOpen(isRewardScreenOpen))
            {
                if (BattleSessionEnd.Epoch != armedEpoch)
                {
                    ReportSuperseded(context, armedEpoch, "while waiting out the reward screen");
                    yield break;
                }
                if (Time.unscaledTime - waitStarted > ModalWaitCapSeconds)
                {
                    cappedOut = true;
                    FlowTrace.Warn(Sys,
                        $"reward screen still open {ModalWaitCapSeconds:F0}s after resolve ({context}) - " +
                        "checking anyway rather than waiting in silence. The modal invariant is reported " +
                        "as a finding, because a reward screen that will not close IS the defect.");
                    break;
                }
                yield return null;
            }

            float settleStarted = Time.unscaledTime;
            while (Time.unscaledTime - settleStarted < SettleSeconds) yield return null;

            // The settle window is exactly where the 2026-08-30 capture was lost: the masked home
            // return lands the hero next to a chaser, contact stages the NEXT fight, and every
            // invariant below reads that fight's CORRECT state as this fight's leak.
            if (BattleSessionEnd.Epoch != armedEpoch)
            {
                ReportSuperseded(context, armedEpoch, "during the settle window");
                yield break;
            }

            var failures = Evaluate(rewardScreenOpen: false);

            if (failures.Count == 0)
            {
                FlowTrace.Step(Sys,
                    $"{MarkerOk} ({context}) - timeScale 1.00, battle-lock clear, no modal held, " +
                    $"{s_extra.Count} module probe(s) clean. The world is back to baseline.");
                yield break;
            }

            var sb = new StringBuilder();
            sb.Append(MarkerFail).Append(" (").Append(context).Append(") - ")
              .Append(failures.Count).Append(" invariant(s) NOT restored after the battle:");
            for (int i = 0; i < failures.Count; i++) sb.Append("\n  - ").Append(failures[i]);
            if (cappedOut) sb.Append("\n  (note: the reward-screen wait hit its cap, see the warning above)");

            FlowTrace.Fail(Sys, sb.ToString());

            // =================================================================
            //  SELF-HEAL (WO-1233b). A player must never be left holding a stuck lock or a 4%
            //  world clock because a coroutine died — reporting alone leaves the softlock in
            //  place. Note what this deliberately does NOT do: it does not force BattleLock
            //  false and it does not add an Nth release call. It re-drives the ONE authoritative
            //  exit seam (BattleSessionEnd.Release), which clears the stale pursuit window and
            //  re-runs every registered owner's own unwind (HitStopManager.EndStopNow included),
            //  so the recovery goes through the same door the fight was supposed to leave by.
            //  Safe by construction: pursuit is PULSE-based, so a chaser that is genuinely still
            //  chasing re-raises the lock on its next aggro tick and is reported below.
            // =================================================================
            if (BattleLock.IsInBattle())
            {
                string stuckHolders = BattleLock.DescribeHolders();
                Guard.Try(Sys, "self-heal: re-drive the battle-session exit",
                    () => BattleSessionEnd.Release($"quiescence self-heal: {context}"));

                // One frame, on the unscaled clock's terms: long enough for a live chaser to
                // re-pulse and re-raise the lock legitimately, short enough that a real stuck
                // holder is still named in the same breath as the failure.
                yield return null;

                if (!BattleLock.IsInBattle())
                    FlowTrace.Warn(Sys,
                        $"battle-lock SELF-HEALED after {MarkerFail} ({context}): holders were " +
                        $"[{stuckHolders}] and re-driving BattleSessionEnd.Release cleared them. " +
                        "This is a SAFETY NET, not a fix — the FAIL above names the state that " +
                        "reached it, and something still failed to leave by the front door.");
                else
                    FlowTrace.Fail(Sys,
                        $"battle-lock STILL HELD after the self-heal ({context}): [{BattleLock.DescribeHolders()}] " +
                        $"(was [{stuckHolders}]). A holder that survives a full session release is either a " +
                        "LIVE chase re-pulsing every aggro tick, or an owner whose probe is latched true with " +
                        "no battle behind it. Read the holder name: it is the owner to fix.");
            }

            // =================================================================
            //  MODAL SELF-HEAL (WO-1337). A GHOST handle is closed; a VISIBLE panel is not.
            // -----------------------------------------------------------------
            //  The finding above states its own player consequence: "the world interact button
            //  stays suppressed underneath and the back button targets a panel the player cannot
            //  see". Both halves of that sentence describe an INVISIBLE handle, and PanelManager
            //  can now tell the two cases apart by asking the handle's own IsOpen probe:
            //
            //    * probe says NOT open  -> a proven ghost (the WO-465 invisible-scrim class).
            //      Nothing is on screen, so there is nothing for the player to dismiss and no
            //      way out; this IS the softlock, and closing it cannot take anything away from
            //      anyone. Healed — through the panel's OWN Close action (PanelManager.CloseAll),
            //      never by zeroing the arbiter's record, so the panel unwinds by its own door
            //      exactly as the lock heal above re-drives the owners' own unwinds.
            //
            //    * probe says VISIBLE (or the panel registered no probe) -> a real panel is on
            //      screen and the player can dismiss it, so this is NOT a softlock and force-
            //      closing it would yank a screen out from under her (the pause menu over a
            //      just-ended fight is the obvious case). REPORTED BY NAME and left alone —
            //      which is now enough, because the finding names it.
            //
            //  ⚠ Ordered after the lock heal and before the blunt timeScale write for the same
            //  reason that one is last: attributable recovery gets first refusal.
            // =================================================================
            if (PanelManager.AnyOpen)
            {
                string modalHolder = PanelManager.DescribeOpen();
                bool? selfReportsOpen = PanelManager.OpenPanelSelfReportsOpen;

                if (selfReportsOpen == false)
                {
                    Guard.Try(Sys, "self-heal: close the ghost panel handle", PanelManager.CloseAll);

                    if (!PanelManager.AnyOpen)
                        FlowTrace.Warn(Sys,
                            $"modal SELF-HEALED after {MarkerFail} ({context}): {modalHolder} - it was " +
                            "recorded open while reporting it was not, so the world interact button was " +
                            "suppressed under nothing and back had an invisible target. Closed through the " +
                            "panel's own Close action. This is a SAFETY NET, not a fix - the FAIL above " +
                            "names the panel, and that panel still failed to call NotifyClosed.");
                    else
                        FlowTrace.Fail(Sys,
                            $"modal STILL OPEN after the self-heal ({context}): {PanelManager.DescribeOpen()} " +
                            $"(was {modalHolder}). CloseAll ran and the arbiter still holds a handle, so that " +
                            "panel's own Close action does not clear its registration. Read the panel name: " +
                            "it is the owner to fix.");
                }
                else
                {
                    FlowTrace.Warn(Sys,
                        $"modal left OPEN on purpose after {MarkerFail} ({context}): {modalHolder}. A panel " +
                        "the player can see is hers to dismiss, and force-closing it would take a live screen " +
                        "off her - so this is reported, not healed. If this panel should not have been up at " +
                        "battle end, the fix belongs to whoever opened it.");
                }
            }

            // THE ONE RESTORE. Unsurvivable and unambiguous: there is no legitimate reason for the
            // world clock to sit at anything but 1 once every battle system has finished. Reported
            // FIRST (above) and announced here, so the leaking owner is still named rather than
            // masked — the whole reason the 2026-08-20 defect went three minutes unexplained is
            // that something silently tolerated it.
            //
            // ⚠ Runs AFTER the self-heal on purpose. HitStopManager.EndStopNow unwinds the clock
            // by NAME and refuses to stamp over a scale it does not own; this blunt write is the
            // last resort behind it, so the attributable restore always gets first refusal.
            if (Mathf.Abs(Time.timeScale - 1f) > ScaleEpsilon)
            {
                float leaked = Time.timeScale;
                Time.timeScale = 1f;
                FlowTrace.Warn(Sys,
                    $"timeScale RESTORED to 1.00 by the quiescence gate (was {leaked:F2}). This is a " +
                    "SAFETY NET, not a fix: something above still leaked the world clock and the " +
                    "FAIL line above names when. Fix the owner, do not rely on this.");
            }
        }

        /// <summary>
        /// A newer battle session began while this gate was settling. Withdraw loudly enough to be
        /// findable, quietly enough that it is never mistaken for a defect: the state in front of
        /// the gate is a LIVE fight's, and judging it produced the 2026-08-30 false failure.
        /// </summary>
        private static void ReportSuperseded(string context, int armedEpoch, string when)
        {
            FlowTrace.Step(Sys,
                $"{MarkerSuperseded} ({context}) - a NEW battle session began {when} " +
                $"(epoch {armedEpoch} -> {BattleSessionEnd.Epoch}), so this gate is judging a fight that " +
                $"is still happening. Withdrawing without a verdict. Observed now: timeScale=" +
                $"{Time.timeScale:F2}, battle-lock holders=[{BattleLock.DescribeHolders()}] - all of which " +
                "belong to the LIVE battle, not to the one that ended.");
        }

        private static bool SafeIsOpen(Func<bool> probe)
        {
            bool open = false;
            Guard.Try(Sys, "poll reward-screen state", () => open = probe());
            return open;
        }
    }
}
