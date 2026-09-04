#if UNITY_EDITOR
// =============================================================================
// GameOverScreenLifecycleRegression — WO-1369 (b) + (c).
// -----------------------------------------------------------------------------
// THE CAPTURE (owner's device, 2026-09-04, production candidate 2026.09.04.354315):
//
//   09:38:25.058 [Flow:Death] lethal hit ... scene='dg_folks_granary' ...
//                OnDeath listeners=[GameOverScreen.ShowHeroFell, HeroDeathEndState.OnHeroDeath]
//   09:38:25.071 HeroDeath shown: spoils=0 action=retry     (panel=451px)
//   09:38:25.074 'YOU HAVE FALLEN' destroyed WITHOUT firing its primary action
//   09:38:25.080 HeroDeath shown: spoils=0 action=respawn   (panel=370px)
//
// TWO end-states 9 ms apart for ONE death, and the hub defeat screen firing inside
// a composed DUNGEON it has no business owning. Two defects, one capture:
//
//   (c) GameOverScreen.OnSceneLoaded nulled _heart/_hero WITHOUT unsubscribing. The
//       hero is DontDestroyOnLoad and is carried into dungeons, so the stale delegate
//       rode across every scene boundary in the game. Its two stand-downs cover arena
//       and overworld; NEITHER covers a dungeon, and there was no IsDefeatScene
//       re-check inside the handler.
//   (b) The screen took a PLAYER-OWNED world hold and then delegated its release to a
//       delegate living on an EndStateView it does not own. All three release paths
//       (OnRetry / OnSceneLoaded / OnDestroy) are downstream of "the player pressed the
//       button"; the arbiter destroyed the button, and the world clock sat at 0.00 for
//       2 m 07 s until Android killed the app.
//
// ⚠ SOURCE-CONTRACT SUITE, AND HONEST ABOUT IT. GameOverScreen is a DontDestroyOnLoad
// MonoBehaviour singleton whose flow needs a live scene, a HeroHealth, a HeartController
// and a built EndStateView canvas, so the FLOW cannot be driven from a synchronous
// edit-mode suite. What IS asserted here is every structural precondition the capture
// turned on. The behavioural half of (b) — an orphaned hold really is released — is
// proven live, against the real engine, in WorldHoldLivenessRegression Case 3.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1369: the hub defeat screen unsubscribes, re-checks its scene, and binds its
    /// world hold to the object that can actually die.</summary>
    public static class GameOverScreenLifecycleRegression
    {
        private const string Src = "Assets/_Modules/Village/Heart/GameOverScreen.cs";

        public static bool Run(out string result)
        {
            var failures = new List<string>();
            try
            {
                if (!File.Exists(Src))
                {
                    result = "GameOverScreen.cs is missing or unreadable at " + Src;
                    return false;
                }
                string code = File.ReadAllText(Src);

                // ---- (c) IT UNSUBSCRIBES ----------------------------------------------------
                if (!Has(code, "private void Unhook()"))
                    failures.Add("[unhook] GameOverScreen has no Unhook(). Hook() subscribes to " +
                                 "HeroHealth.OnDeath and HeartController.OnHeartDestroyed; without the " +
                                 "matching -= the delegate rides a DontDestroyOnLoad hero into every " +
                                 "scene in the game (HeroDeathEndState.Unhook is the shape to match).");
                if (!Has(code, "OnDeath -= ShowHeroFell") || !Has(code, "OnHeartDestroyed -= ShowHeartFell"))
                    failures.Add("[unhook] the -= for ShowHeroFell / ShowHeartFell is gone. Nulling the " +
                                 "reference is NOT unsubscribing - it only drops our handle on the object " +
                                 "while leaving our delegate on its event. That is the dg_folks_granary bug.");

                // The order is unfixable if reversed: once the field is null there is nothing left
                // to unsubscribe from.
                var unhook = Regex.Match(code, @"private void Unhook\(\)\s*\{(.*?)\n        \}",
                                         RegexOptions.Singleline);
                if (unhook.Success)
                {
                    string b = unhook.Groups[1].Value;
                    int minus = b.IndexOf("-= ShowHeroFell", StringComparison.Ordinal);
                    int nulled = b.IndexOf("_hero = null", StringComparison.Ordinal);
                    if (minus < 0 || nulled < 0 || minus > nulled)
                        failures.Add("[unhook] Unhook() nulls _hero before (or without) unsubscribing. " +
                                     "Unsubscribe FIRST - a null reference cannot be unsubscribed from.");
                }

                // OnSceneLoaded must go through Unhook, never back to the bare nulls.
                var onScene = Regex.Match(code,
                    @"private void OnSceneLoaded\(Scene scene, LoadSceneMode mode\)\s*\{(.*?)\n        \}",
                    RegexOptions.Singleline);
                if (!onScene.Success)
                    failures.Add("[unhook] OnSceneLoaded(Scene, LoadSceneMode) not found - the suite cannot " +
                                 "verify the scene-change path it exists to pin.");
                else
                {
                    string body = onScene.Groups[1].Value;
                    if (body.IndexOf("Unhook()", StringComparison.Ordinal) < 0)
                        failures.Add("[unhook] OnSceneLoaded does not call Unhook(). This is the exact line " +
                                     "that shipped the P0: it read '_heart = null; _hero = null;' and left " +
                                     "both delegates attached to a DDOL hero.");
                    if (Regex.IsMatch(body, @"_hero\s*=\s*null") &&
                        body.IndexOf("Unhook()", StringComparison.Ordinal) < 0)
                        failures.Add("[unhook] OnSceneLoaded still nulls _hero directly.");
                }

                // ---- (c) THE HANDLER RE-CHECKS THE SCENE ------------------------------------
                if (!Has(code, "GateHandlerToDefeatScene"))
                    failures.Add("[scene-gate] the handlers do not re-check IsDefeatScene. Hook()'s gate was " +
                                 "evaluated in a DIFFERENT SCENE from the one the handler fires in, and the " +
                                 "arena/overworld stand-downs are scene-SPECIFIC exclusions that do not " +
                                 "cover a composed dungeon (dg_folks_granary, F8 2026-09-04).");
                foreach (var handler in new[] { "ShowHeroFell", "ShowHeartFell" })
                {
                    var m = Regex.Match(code, @"private void " + handler + @"\(\)\s*\{(.*?)\n        \}",
                                        RegexOptions.Singleline);
                    if (!m.Success)
                    {
                        failures.Add($"[scene-gate] {handler}() not found in its expected form.");
                        continue;
                    }
                    if (m.Groups[1].Value.IndexOf("GateHandlerToDefeatScene", StringComparison.Ordinal) < 0)
                        failures.Add($"[scene-gate] {handler} does not gate on the ACTIVE scene. It trusts a " +
                                     "gate evaluated when the subscription was made, which is the wrong " +
                                     "moment - the delegate outlives the scene that authorised it.");
                }

                // ---- (b) THE HOLD IS BOUND TO THE OBJECT THAT CAN DIE ------------------------
                if (!Has(code, "AcquirePlayerOwned(HoldReason, () => _deathView != null)"))
                    failures.Add("[hold-owner] the game-over hold does not name the END-STATE VIEW as its " +
                                 "liveness probe. ⛔ This screen is a DontDestroyOnLoad singleton - it is " +
                                 "NEVER destroyed and it was ENABLED for the entire 2m07s freeze, so " +
                                 "neither OnDestroy nor OnDisable is a net here. The only thing that CAN " +
                                 "die is the view, so the view is what the probe must ask about.");

                // ORDER: the view must exist before the hold that probes it is taken.
                var show = Regex.Match(code,
                    @"private void Show\(string title, string body, bool isHeartDestroyed\)\s*\{(.*?)\n        \}",
                    RegexOptions.Singleline);
                if (!show.Success)
                    failures.Add("[hold-owner] Show(string,string,bool) not found.");
                else
                {
                    string b = show.Groups[1].Value;
                    int built = b.IndexOf("_deathView = EndStateView.Show", StringComparison.Ordinal);
                    int held  = b.IndexOf("AcquirePlayerOwned(HoldReason", StringComparison.Ordinal);
                    if (built < 0)
                        failures.Add("[hold-owner] Show() does not capture the view it raised into _deathView, " +
                                     "so nothing can ever ask whether it is still there.");
                    else if (held >= 0 && held < built)
                        failures.Add("[hold-owner] Show() takes the world hold BEFORE building the view its " +
                                     "probe reads. The probe's first answer would be 'my owner is null', and " +
                                     "a Show that fails to build would leave a freeze behind a card that " +
                                     "never drew - the same 2m07s shape from the other direction.");
                    if (b.IndexOf("_shown = false", StringComparison.Ordinal) < 0)
                        failures.Add("[hold-owner] Show() has no _shown reset on its failure path. A Show that " +
                                     "cannot build its view must re-arm, or every later death is swallowed.");
                }

                // ---- (b) _shown IS RE-ARMED WHEN THE VIEW IS ABANDONED -----------------------
                if (!Has(code, "PollDeathViewLiveness"))
                    failures.Add("[shown-reset] there is no local poll for an abandoned death view. " +
                                 "WorldHold's probe releases the CLOCK, but only this screen can clear " +
                                 "_shown - and _shown was still true after the capture, so a SECOND death " +
                                 "would have been silently swallowed by Show()'s first line.");
                else
                {
                    var poll = Regex.Match(code, @"private void PollDeathViewLiveness\(\)\s*\{(.*?)\n        \}",
                                           RegexOptions.Singleline);
                    if (poll.Success && poll.Groups[1].Value.IndexOf("_shown = false", StringComparison.Ordinal) < 0)
                        failures.Add("[shown-reset] PollDeathViewLiveness releases the hold but never re-arms " +
                                     "_shown. Half the defect would still ship.");
                    var upd = Regex.Match(code, @"private void Update\(\)\s*\{(.*?)\n        \}",
                                          RegexOptions.Singleline);
                    if (upd.Success && upd.Groups[1].Value.IndexOf("PollDeathViewLiveness", StringComparison.Ordinal) < 0)
                        failures.Add("[shown-reset] PollDeathViewLiveness exists but Update() never calls it. " +
                                     "Update is the ONE hook that still runs at timeScale 0, which is why the " +
                                     "check lives there and not in a coroutine.");
                }

                // ---- STEP-OUTS, AND THE STANDING PROHIBITIONS --------------------------------
                if (!Has(code, "private void OnDisable()"))
                    failures.Add("[step-out] GameOverScreen has no OnDisable step-out (WO-1360's shape: a " +
                                 "disabled component gets no OnDestroy and cannot process Retry).");
                if (!Has(code, "private void OnDestroy()"))
                    failures.Add("[step-out] GameOverScreen lost its OnDestroy step-out.");
                if (Regex.IsMatch(BlankStrings(code), @"Time\s*\.\s*timeScale\s*=(?!=)"))
                    failures.Add("[step-out] GameOverScreen assigns Time.timeScale directly again. WorldHold " +
                                 "is the ONE writer (WO-1353).");
                if (!Has(code, "DeathTrace.TimeScaleFroze") || !Has(code, "DeathTrace.TimeScaleRestored"))
                    failures.Add("[step-out] the DeathTrace step-in/step-out pair is gone. CLAUDE.md §12: " +
                                 "instrumentation is PERMANENT.");
                // ⛔ The ceiling must NOT come back (WO-1369 'WHAT NOT TO TOUCH').
                if (Regex.IsMatch(code, @"AcquireScale\s*\(\s*HoldReason") ||
                    Regex.IsMatch(code, @"AcquirePlayerOwnedScale\s*\(\s*HoldReason"))
                    failures.Add("[no-ceiling] the game-over hold was converted back to a BOUNDED form. ⛔ A " +
                                 "ceiling would have masked this at 180s - the player still loses three " +
                                 "minutes to a frozen world and the orphaned hold is still a defect. Fix " +
                                 "ownership, not the ceiling (WO-1369; WO-1353's regression is why).");
            }
            catch (Exception ex)
            {
                failures.Add("[game-over-lifecycle] the suite itself threw: " + ex);
            }

            if (failures.Count > 0)
            {
                result = "GameOverScreen lifecycle contract BROKEN:\n  - " + string.Join("\n  - ", failures);
                return false;
            }
            result = "the hub defeat screen unsubscribes on scene change, re-checks the active scene " +
                     "inside both handlers, binds its player-owned hold to the end-state view that can " +
                     "actually die, and re-arms _shown when that view is abandoned.";
            return true;
        }

        private static bool Has(string code, string token)
            => code.IndexOf(token, StringComparison.Ordinal) >= 0;

        /// <summary>Blanks string literals AND comments, so a Time.timeScale mention inside a trace
        /// message or a design note is not read as an assignment. Both are needed here: this file's
        /// own header carries the sentence "The pause (Time.timeScale = 0) + reload-on-retry
        /// supersede the hero's silent auto-respawn", which a strings-only blanker would report as
        /// a second clock writer.</summary>
        private static string BlankStrings(string code)
        {
            string s = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
            s = Regex.Replace(s, @"//[^\n]*", "");
            return Regex.Replace(s, "\"(\\\\.|[^\"\\\\])*\"", "\"\"");
        }
    }
}
#endif
