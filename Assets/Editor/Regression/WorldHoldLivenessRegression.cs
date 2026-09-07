#if UNITY_EDITOR
// =============================================================================
// WorldHoldLivenessRegression — WO-1369. THE ORACLE FOR "A PLAYER-OWNED HOLD
// CANNOT OUTLIVE ITS OWNER."
// -----------------------------------------------------------------------------
// ⛔ WHY THIS FILE EXISTS AT ALL, in the owner's words: *"shouldnt these items all
// have regression cases? so that stuff cant break working features"*.
//
// THE DEFECT (owner's device, 2026-09-04, on the 2026.09.04.354315 PRODUCTION
// CANDIDATE): `everything completely froze. I had to kill app to exit`.
//
//   09:38:25.063  WorldHold ACQUIRE 'game-over' @ 0.00 -> effective timeScale 0.00
//   09:38:25.063  'game-over' is PLAYER-OWNED ... NO watchdog ceiling applies
//   09:38:25.074  'YOU HAVE FALLEN' destroyed WITHOUT firing its primary action
//   09:40:33.252  ActivityManager: Killing com.denellestudios.echoesofelarion
//
// `grep -c "WorldHold RELEASE 'game-over'"` over the whole 1.23M-line buffer
// returns 0. Acquired, never released, timeScale 0.00 for 2 m 07 s until the OS
// killed the app.
//
// ⛔ AND `REGRESSION_OK 358/358` WAS GREEN ON THAT BUILD. That is the finding this
// file answers. WO-1360 removed the ceiling for a good reason (a 180 s ceiling
// force-released a legitimate 507 s pause and the world ran under a PAUSED screen),
// and the suite that pinned the removal asserted only the FIRST half — "a
// player-owned hold survives arbitrarily long". Nothing asserted the second half:
// that it must NOT survive its owner. Both halves are here, and neither may be
// removed to make the other pass.
//
// ⭐ THE QUESTION IS "DOES ITS OWNER STILL EXIST", NEVER "IS THIS TOO OLD". A
// legitimate eight-minute pause with a live PauseController must come through every
// case in this file untouched — Case 2 fails if it does not, so a "fix" that quietly
// reintroduces a ceiling cannot pass by satisfying Case 1.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1369: a PlayerOwned WorldHold must declare a liveness probe, and the watchdog
    /// must force-release the hold the moment that probe answers false — never because of age.</summary>
    public static class WorldHoldLivenessRegression
    {
        // The shipping call sites that take a PLAYER-OWNED hold. The audit in WO-1369 §5 found
        // 2 of 7 GUILTY and 2 PARTIAL, so every one of them is listed and checked; a new
        // player-owned hold added to the tree without a row here fails Case 5.
        private static readonly (string src, string reason, string[] mustHave)[] Owners =
        {
            ("Assets/_Modules/Settings/PauseController.cs",              "pause-menu",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/_Modules/Village/Heart/GameOverScreen.cs",          "game-over",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/_Modules/Village/UI/EndState/EndStateView.cs",      "wave-results",
                new[] { "OnDestroy" }),
            ("Assets/_Modules/HUD/Kit/HudKitController.cs",              "combat-item-picker",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/_Modules/HUD/BugReportView.cs",                     "bug-report-form",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs",  "f8-note-capture",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/VfxParade/Runtime/VfxParadeRuntime.cs",             "vfx-parade-curation",
                new[] { "OnEnable", "OnDisable", "OnDestroy" }),

            // WO-1471 — the four player-paced modals WO-1360 §3 rows 14-17 deliberately LEFT on the
            // bounded default because they dodged the ceiling with a per-frame WorldHold.Renew().
            // That workaround was not enough: on the owner's device 'harvest-overflow-result' held
            // the clock from 12:51:25.157 to 12:53:06.089 (101 s) inside the 152-line
            // "WORLD CLOCK FROZEN timeScale=0.00 scene=Main_Castle_Overworld" run. Converted, and
            // their Renew-every-frame Updates deleted — the enum replaces that pattern.
            ("Assets/_Modules/Core/UI/HarvestOverflowModal.cs",          "harvest-overflow-result",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/_Modules/Core/UI/FocusedModalHost.cs",              "focused-card-modal",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/_Modules/Core/UI/ObsidianNavigationWorkspace.cs",   "obsidian-navigation-workspace",
                new[] { "OnDisable", "OnDestroy" }),
            ("Assets/_Modules/Village/Crafting/JewelerDiscoveryFtue.cs", "jeweler-discovery",
                new[] { "OnDisable", "OnDestroy" }),
        };

        // WO-1471 — CASE 6's table. A PLAYER-PACED modal must never take the BOUNDED handle: the
        // player decides when it ends, so age is not evidence of a leak. Each row is the reason
        // token exactly as the shipping call site spells it.
        private static readonly (string src, string token)[] PlayerPacedModals =
        {
            ("Assets/_Modules/Core/UI/HarvestOverflowModal.cs",          "\"harvest-overflow-result\""),
            ("Assets/_Modules/Core/UI/FocusedModalHost.cs",              "HoldReason"),
            ("Assets/_Modules/Core/UI/ObsidianNavigationWorkspace.cs",   "HoldReason"),
            ("Assets/_Modules/Village/Crafting/JewelerDiscoveryFtue.cs", "\"jeweler-discovery\""),
        };

        public static bool Run(out string result)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            try
            {
                ApiMakesTheProbeImpossibleToForget(failures, log);
                ALiveOwnerIsNeverForceReleased(failures, log);
                ADeadOwnerIsForceReleasedImmediately(failures, log);
                ABoundedBeatIsUnaffected(failures, log);
                EveryShippingCallSiteDeclaresARealProbe(failures, log);
                APlayerPacedModalNeverTakesTheBoundedHandle(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("[world-hold-liveness] the suite itself threw: " + ex);
            }
            finally
            {
                // Never leave the editor's clock where a case left it.
                try { WorldHold.ResetForTests(); } catch { }
            }

            if (failures.Count > 0)
            {
                result = "WorldHold liveness contract BROKEN:\n  - " + string.Join("\n  - ", failures);
                return false;
            }
            result = "a PlayerOwned WorldHold cannot be built without a liveness probe, is " +
                     "force-released the moment its owner stops existing, and is NEVER dropped " +
                     "for age while its owner is alive.\n" + log.ToString().TrimEnd();
            return true;
        }

        // ---------------------------------------------------------------------
        //  CASE 1 — the API. Forgetting is IMPOSSIBLE, not merely remembered.
        //  (The OverTimeEffects/WO-1330 shape the WO named: no default, no null,
        //   no overload that omits it.)
        // ---------------------------------------------------------------------
        private static void ApiMakesTheProbeImpossibleToForget(List<string> failures, StringBuilder log)
        {
            var t = typeof(WorldHold);
            foreach (string name in new[] { "AcquirePlayerOwned", "AcquirePlayerOwnedScale" })
            {
                var overloads = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
                bool sawProbeForm = false;
                foreach (var m in overloads)
                {
                    if (m.Name != name) continue;
                    var ps = m.GetParameters();
                    bool hasProbe = false, probeOptional = false;
                    foreach (var p in ps)
                        if (p.ParameterType == typeof(Func<bool>)) { hasProbe = true; probeOptional = p.IsOptional; }
                    if (!hasProbe)
                    {
                        failures.Add($"[api] {name} has an overload with NO Func<bool> liveness probe " +
                                     $"({ps.Length} parameter(s)). A probe-less overload is the hole with " +
                                     "a signature around it: the NEXT call site, written by a seat who " +
                                     "does not know the argument is load-bearing, will find it and the " +
                                     "world will freeze again (WO-1369). Delete the overload.");
                        continue;
                    }
                    if (probeOptional)
                        failures.Add($"[api] {name}'s liveness probe is OPTIONAL. An argument with a " +
                                     "default is an argument that gets forgotten - it must be REQUIRED, " +
                                     "exactly as OverTimeEngine's isAlive is (WO-1330).");
                    sawProbeForm = true;
                }
                if (!sawProbeForm)
                    failures.Add($"[api] {name} does not exist, or takes no Func<bool> probe at all.");
            }

            // And it must THROW, not silently accept null through a reflective/dynamic path.
            try
            {
                WorldHold.ResetForTests();
                WorldHold.AcquirePlayerOwned("wo1369-null-probe", null);
                failures.Add("[api] AcquirePlayerOwned accepted a NULL liveness probe. It must throw " +
                             "ArgumentNullException - a null probe answers 'alive' forever, which is " +
                             "precisely the state that froze the owner's device for 2m07s.");
            }
            catch (ArgumentNullException)
            {
                log.AppendLine("  [api] AcquirePlayerOwned(reason, null) throws ArgumentNullException");
            }
            catch (Exception ex)
            {
                failures.Add("[api] AcquirePlayerOwned(null probe) threw " + ex.GetType().Name +
                             " instead of ArgumentNullException.");
            }
            finally { WorldHold.ResetForTests(); }
        }

        // ---------------------------------------------------------------------
        //  CASE 2 — ⛔ THE HALF THAT MUST NOT BREAK. A LIVE owner is untouchable
        //  at ANY age. This is the WO-1353/WO-1360 regression the ticket forbids
        //  re-creating, and it is FIRST on purpose: a fix that reintroduces a
        //  ceiling fails here before it can pass Case 3.
        // ---------------------------------------------------------------------
        private static void ALiveOwnerIsNeverForceReleased(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();
            bool ownerAlive = true;
            var hold = WorldHold.AcquirePlayerOwned(WorldHold.ReasonPauseMenu, () => ownerAlive);
            if (!Mathf.Approximately(Time.timeScale, 0f))
                failures.Add("[live-owner] the pause hold never froze the clock, so this case is not " +
                             "testing what it claims to.");

            // The observed 507 s overrun from F8 seq 4679, then an hour, then most of a day.
            foreach (float t in new[] { 507.3f, 3600f, 60000f })
            {
                WorldHold.WatchdogTick(Time.unscaledTime + t);
                if (WorldHold.Count != 1 || !Mathf.Approximately(Time.timeScale, 0f))
                {
                    failures.Add("[live-owner] a player-owned hold with a LIVE owner was force-released " +
                                 $"after {t:0}s (count {WorldHold.Count}, clock {Time.timeScale:0.00}). " +
                                 "⛔ This is the WO-1353 regression: a human can pause for hours and " +
                                 "backgrounding the app is the normal way to do it. Unfreezing the world " +
                                 "under an open PAUSED screen is strictly worse than the leak a ceiling " +
                                 "guards. The probe asks 'does its owner exist', NEVER 'is this old'.");
                    break;
                }
            }
            hold.Dispose();
            if (WorldHold.Count != 0 || !Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[live-owner] disposing the hold did not return the world to 1.00 " +
                             $"(count {WorldHold.Count}, clock {Time.timeScale:0.00}).");
            else
                log.AppendLine("  [live-owner] a live owner survives 507s / 1h / ~17h of ticks and " +
                               "releases only when its owner says so");
            WorldHold.ResetForTests();
        }

        // ---------------------------------------------------------------------
        //  CASE 3 — ⭐ THE CAPTURE, REPRODUCED. This is the case that goes RED
        //  against the pre-fix engine: acquire a player-owned hold, kill its
        //  owner, tick — and before WO-1369 the hold survived forever.
        // ---------------------------------------------------------------------
        private static void ADeadOwnerIsForceReleasedImmediately(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();

            // The 'game-over' shape verbatim: the holder delegates its release to a view it does
            // not own, and the modal arbiter destroys that view 18 ms later.
            object view = new object();
            WorldHold.AcquirePlayerOwned("game-over", () => view != null);
            if (!Mathf.Approximately(Time.timeScale, 0f))
                failures.Add("[dead-owner] the game-over hold never froze the clock.");

            // Still alive: the hold must NOT be dropped, however many ticks pass.
            WorldHold.WatchdogTick(Time.unscaledTime + 1f);
            if (WorldHold.Count != 1)
                failures.Add("[dead-owner] the hold was dropped while its owner was still alive - the " +
                             "probe is being ignored or inverted.");

            // The arbiter destroys the view. Nothing else in the game will ever release this hold.
            view = null;

            WorldHold.WatchdogTick(Time.unscaledTime + 1.1f);
            if (WorldHold.Count != 0)
                failures.Add("[dead-owner] ⛔ THE WO-1369 P0, UNFIXED: a PLAYER-OWNED hold whose owner " +
                             "NO LONGER EXISTS survived the watchdog (count " + WorldHold.Count +
                             "). On the owner's device this state held timeScale at 0.00 for 2m07s " +
                             "until Android killed the app, and the only exit was force-quitting. The " +
                             "watchdog must poll the liveness probe every tick and force-release the " +
                             "instant it answers false.");
            else if (!Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[dead-owner] the orphaned hold was dropped but the clock was left at " +
                             Time.timeScale.ToString("0.00") + " instead of 1.00. Zero live holds " +
                             "ALWAYS means the restorable baseline.");
            else
                log.AppendLine("  [dead-owner] an orphaned player-owned hold is force-released on the " +
                               "first tick after its owner dies, and the clock returns to 1.00");

            // A hold whose probe THROWS must read as ALIVE - never unfreeze the world on an
            // exception - and must not take the watchdog down with it.
            WorldHold.ResetForTests();
            WorldHold.AcquirePlayerOwned("wo1369-throwing-probe",
                () => throw new InvalidOperationException("probe fault"));
            try
            {
                WorldHold.WatchdogTick(Time.unscaledTime + 1f);
                if (WorldHold.Count != 1)
                    failures.Add("[dead-owner] a THROWING liveness probe was treated as 'owner dead' and " +
                                 "the world was unfrozen. An exception is not evidence the owner is gone; " +
                                 "unfreezing under a live modal is the worse failure. Report it, keep the hold.");
                else
                    log.AppendLine("  [dead-owner] a throwing probe reads as ALIVE and is reported, not obeyed");
            }
            catch (Exception ex)
            {
                failures.Add("[dead-owner] a throwing liveness probe took the WATCHDOG down with it (" +
                             ex.GetType().Name + "). One bad probe must never stop the net that " +
                             "protects every other hold.");
            }
            WorldHold.ResetForTests();
        }

        // ---------------------------------------------------------------------
        //  CASE 4 — the exemption is CATEGORICAL, not a global off switch.
        // ---------------------------------------------------------------------
        private static void ABoundedBeatIsUnaffected(List<string> failures, StringBuilder log)
        {
            WorldHold.ResetForTests();
            var beat = WorldHold.AcquireScale("wo1369-bounded-beat", 0.28f, 0.5f);
            if (beat.IsPlayerOwned)
                failures.Add("[bounded] AcquireScale produced a PLAYER-OWNED hold. Unbounded must be " +
                             "asked for by name, or every future leak goes undetected.");
            WorldHold.WatchdogTick(Time.unscaledTime + 2f);
            if (WorldHold.Count != 0 || !Mathf.Approximately(Time.timeScale, 1f))
                failures.Add("[bounded] a bounded beat 2s past its 0.5s ceiling was NOT force-released " +
                             $"(count {WorldHold.Count}, clock {Time.timeScale:0.00}). Adding the probe " +
                             "must not weaken the ceiling that still guards cosmetic dips - a coroutine " +
                             "killed by a deactivated host fires no OnDestroy and throws nothing.");
            else
                log.AppendLine("  [bounded] the ceiling still expires a bounded beat; the probe is an " +
                               "addition, not a replacement");
            WorldHold.ResetForTests();
        }

        // ---------------------------------------------------------------------
        //  CASE 5 — ⛔ THE SEVEN-HOLD AUDIT, MADE PERMANENT. Every shipping
        //  player-owned hold declares a REAL probe and steps out on every exit
        //  path its host can take. Two of seven failed in two days; assume the
        //  rest are guilty until each is proven, and keep proving it.
        // ---------------------------------------------------------------------
        private static void EveryShippingCallSiteDeclaresARealProbe(List<string> failures, StringBuilder log)
        {
            int before = failures.Count;
            var seen = new HashSet<string>();
            foreach (var owner in Owners)
            {
                if (!File.Exists(owner.src))
                {
                    failures.Add($"[call-site/{owner.reason}] {owner.src} is missing. If this hold moved, " +
                                 "move its row too - an unlisted player-owned hold is an unaudited one.");
                    continue;
                }
                string code = File.ReadAllText(owner.src);
                seen.Add(owner.src);

                int at = code.IndexOf("AcquirePlayerOwned", StringComparison.Ordinal);
                if (at < 0)
                {
                    failures.Add($"[call-site/{owner.reason}] {owner.src} no longer takes a PLAYER-OWNED " +
                                 "hold. If that is deliberate, remove its row here in the SAME edit.");
                    continue;
                }

                // The probe must be a lambda in the SAME call, and it must not be a constant.
                foreach (Match m in Regex.Matches(code, @"AcquirePlayerOwned(?:Scale)?\s*\(([^;]{0,400}?)\)\s*;",
                                                  RegexOptions.Singleline))
                {
                    string args = m.Groups[1].Value;
                    if (args.IndexOf("=>", StringComparison.Ordinal) < 0)
                        failures.Add($"[call-site/{owner.reason}] {owner.src} calls AcquirePlayerOwned with " +
                                     "no lambda liveness probe. Pass an existence test on the object that " +
                                     "can actually die, e.g. '() => view != null'.");
                    if (Regex.IsMatch(args, @"\(\s*\)\s*=>\s*true\s*\)?\s*$"))
                        failures.Add($"[call-site/{owner.reason}] {owner.src} passes '() => true' as its " +
                                     "liveness probe. ⛔ That is not a probe, it is the hole with a lambda " +
                                     "around it - it re-creates the exact WO-1369 P0 while satisfying the " +
                                     "compiler. Name the object that can die.");
                }

                foreach (string hook in owner.mustHave)
                    if (code.IndexOf(hook, StringComparison.Ordinal) < 0)
                        failures.Add($"[call-site/{owner.reason}] {owner.src} has no {hook} step-out. With " +
                                     "no ceiling, the host's own lifecycle is a net the probe complements " +
                                     "rather than replaces - and a merely-DISABLED component never receives " +
                                     "OnDestroy, so it can neither commit nor resume nor release.");
            }

            // A NEW player-owned hold anywhere under Assets/ that is not on the audit list above.
            foreach (string file in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
            {
                string norm = file.Replace('\\', '/');
                if (norm.Contains("/Editor/") || norm.Contains("/Tests/")) continue;   // harnesses may fixture
                if (seen.Contains(norm)) continue;
                string code = File.ReadAllText(file);
                if (code.IndexOf("AcquirePlayerOwned", StringComparison.Ordinal) < 0) continue;
                if (norm.EndsWith("Assets/_Modules/Core/UI/WorldHold.cs", StringComparison.Ordinal)) continue;
                failures.Add($"[call-site/new] {norm} takes a PLAYER-OWNED hold and is NOT on this suite's " +
                             "audit list. Every unbounded hold in the game is audited by name - add its row " +
                             "(source, reason, required step-outs) in the same edit that adds the hold.");
            }

            if (failures.Count == before)
                log.AppendLine("  [call-site] all " + Owners.Length + " shipping player-owned holds declare " +
                               "a real liveness probe and step out on every exit path their host can take");
        }

        // ---------------------------------------------------------------------
        //  CASE 6 — WO-1471. ⛔ A PLAYER-PACED MODAL MUST NOT TAKE THE BOUNDED
        //  HANDLE. WO-1360 built the categorical distinction and then left four
        //  player-paced modals on the bounded default because a per-frame
        //  WorldHold.Renew() kept their deadline out of reach. That workaround
        //  IS the pattern the enum replaced, and it did not hold: on the owner's
        //  device 'harvest-overflow-result' pinned the clock for 101 s
        //    12:51:25.157 ACQUIRE -> 12:53:06.089 RELEASE
        //  inside a 36-minute run of
        //    "WORLD CLOCK FROZEN timeScale=0.00  scene=Main_Castle_Overworld"  (x152).
        //  The kind is asserted BOTH ways: the engine must actually mark the
        //  harvest hold player-owned, and the source must not quietly revert.
        // ---------------------------------------------------------------------
        private static void APlayerPacedModalNeverTakesTheBoundedHandle(List<string> failures, StringBuilder log)
        {
            // (a) THE ENGINE. The harvest-overflow handle, taken the way the modal now takes it,
            // reads as PLAYER-OWNED and is not force-released while its owner exists.
            WorldHold.ResetForTests();
            object modal = new object();
            var hold = WorldHold.AcquirePlayerOwned("harvest-overflow-result", () => modal != null);
            if (!hold.IsPlayerOwned)
                failures.Add("[player-paced/harvest] the 'harvest-overflow-result' handle is NOT player-owned. " +
                             "The player dismisses this modal at their own pace, so a ceiling on it is a " +
                             "category error (WO-1360) - and the bounded default is what froze the owner's " +
                             "overworld clock for 101s under an open HARVEST RESULT card (WO-1471).");
            // Past the old 180s ceiling: a live owner must be untouched.
            WorldHold.WatchdogTick(Time.unscaledTime + 300f);
            if (WorldHold.Count != 1 || !Mathf.Approximately(Time.timeScale, 0f))
                failures.Add("[player-paced/harvest] the harvest-overflow hold was force-released after 300s " +
                             $"with its modal still alive (count {WorldHold.Count}, clock {Time.timeScale:0.00}). " +
                             "A player reading a harvest result is not a leak.");
            modal = null;   // and it still must not outlive its owner (Case 3's contract, here too).
            WorldHold.WatchdogTick(Time.unscaledTime + 301f);
            if (WorldHold.Count != 0)
                failures.Add("[player-paced/harvest] the harvest-overflow hold survived the destruction of its " +
                             "modal. Unbounded does not mean unkillable - the probe must end it.");
            // Only if a failure above left it outstanding — a double Release would log a phantom
            // RELEASE line into a trace the next reader would take at face value.
            if (WorldHold.Count > 0) hold.Dispose();
            WorldHold.ResetForTests();

            // (b) THE SOURCE. No player-paced modal may drift back to WorldHold.Acquire(...), and
            // none of them may reintroduce the per-frame Renew() that stood in for the enum.
            foreach (var modalRow in PlayerPacedModals)
            {
                if (!File.Exists(modalRow.src))
                {
                    failures.Add($"[player-paced] {modalRow.src} is missing. If this modal moved, move its row " +
                                 "in the SAME edit - an unlisted player-paced hold is an unaudited one.");
                    continue;
                }
                string code = File.ReadAllText(modalRow.src);

                if (code.IndexOf("WorldHold.Acquire(" + modalRow.token, StringComparison.Ordinal) >= 0)
                    failures.Add($"[player-paced] {modalRow.src} takes the BOUNDED handle " +
                                 $"WorldHold.Acquire({modalRow.token}). This modal is dismissed by the PLAYER, " +
                                 "so the 180s ceiling can force-release it while the card is still on screen - " +
                                 "the world then runs under a screen that says it is stopped (WO-1016 shape, " +
                                 "measured again as WO-1471). Use AcquirePlayerOwned with a liveness probe.");

                if (code.IndexOf("AcquirePlayerOwned", StringComparison.Ordinal) < 0)
                    failures.Add($"[player-paced] {modalRow.src} no longer takes a player-owned hold at all.");

                if (code.IndexOf("WorldHold.Renew(", StringComparison.Ordinal) >= 0)
                    failures.Add($"[player-paced] {modalRow.src} still calls WorldHold.Renew() every frame. " +
                                 "That was the WORKAROUND for the ceiling this hold no longer has (WO-1360 §3 " +
                                 "note on rows 14-17); leaving it in preserves the shape the enum replaced and " +
                                 "hides the next author's mistake. Delete the renew loop, not the probe.");
            }

            log.AppendLine("  [player-paced] the harvest-overflow handle is player-owned in the engine, and all " +
                           PlayerPacedModals.Length + " player-paced modals ask for it by name with no renew loop");
        }
    }
}
#endif
