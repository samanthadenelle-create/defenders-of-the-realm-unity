// =============================================================================
// DeathTrace — the hero-death FORENSIC WINDOW (F8-15 extension, owner 2026-07-08:
// "i want debuggers on death to capture why so many screens moving characters
// location").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Diagnostics
//
// WHAT IT IS: a small static window HeroHealth opens at the lethal moment
// (OpenWindow(15s)). While the window is live, the central chokepoints report:
//   • SCREEN OPENED/CLOSED — PanelManager.NotifyOpened/NotifyClosed +
//     EndStateView.Show (the death popups all funnel through those two), each
//     naming WHO opened it (CallerMemberName/FilePath or a stack-derived caller).
//   • HERO MOVED — every warp/teleport chokepoint (HeroLocomotion.WarpTo,
//     BattleArena.WarpHero, HeroHealth.Respawn) plus an unattributed >2m
//     single-frame jump monitor in HeroHealth.LateUpdate as the catch-all.
//   • CAMERA — target/suspend changes (ArenaDeathCam hold, SmartMobileCamera
//     enable/disable/lock/snap) — the owner ruled "stay on hero for the death
//     animation", so a camera leaving the hero in this window is the defect.
//
// Every line is [Flow:DeathTrace]-tagged (FlowTrace system "DeathTrace") so the
// F8 BreakCaptureHarness / f8-watch harvest picks it up. Explicit warps ALSO log
// outside the window (throttled ~1/sec per caller) so a stray mid-play teleport
// is never invisible. Whole class is dark when FlowTrace.Enabled is false.
//
// INSTRUMENTATION ONLY — no behaviour lives here; nothing reads this state to
// make gameplay decisions.
// =============================================================================

using System.IO;
using UnityEngine;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// Hero-death forensic window: opened by HeroHealth at the lethal moment; the
    /// screen/warp/camera chokepoints report through it while it is live.
    /// All output is <c>[Flow:DeathTrace]</c>-tagged and FlowTrace-gated.
    /// </summary>
    public static class DeathTrace
    {
        /// <summary>Default window length after a lethal hit (covers down-beat +
        /// respawn/evac + every popup that stacks on top).</summary>
        public const float DefaultWindowSeconds = 15f;

        /// <summary>Time.time the current window ends. Read-only; -1 = never opened.</summary>
        public static float WindowUntil { get; private set; } = -1f;

        /// <summary>True while the death forensic window is live (and tracing is on).</summary>
        public static bool Active => FlowTrace.Enabled && Time.time < WindowUntil;

        /// <summary>Open (or extend) the forensic window. Called by HeroHealth at the
        /// lethal moment. Extends, never shortens, an already-open window.</summary>
        public static void OpenWindow(float seconds, string context)
        {
            if (!FlowTrace.Enabled) return;
            float until = Time.time + Mathf.Max(1f, seconds);
            WindowUntil = Mathf.Max(WindowUntil, until);
            FlowTrace.Step("DeathTrace",
                $"WINDOW OPEN for {seconds:F0}s (t={Time.time:F1}) — {context}");
        }

        /// <summary>A screen/panel opened during the window. <paramref name="by"/> = the
        /// invoker (class.method) — use <see cref="Describe"/> or <see cref="Caller"/>.</summary>
        public static void ScreenOpened(string panelName, string by)
        {
            if (!Active) return;
            FlowTrace.Step("DeathTrace", $"SCREEN OPENED: {panelName} by {by}");
        }

        /// <summary>A screen/panel closed (or was force-swapped shut) during the window.</summary>
        public static void ScreenClosed(string panelName, string by)
        {
            if (!Active) return;
            FlowTrace.Step("DeathTrace", $"SCREEN CLOSED: {panelName} by {by}");
        }

        /// <summary>
        /// The hero was moved non-locomotively. Logged as a window Step while the window
        /// is live; when <paramref name="always"/> (explicit Warp chokepoints) it also
        /// logs OUTSIDE the window, throttled ~1/sec per caller, so no teleport is invisible.
        /// </summary>
        public static void HeroMoved(Vector3 from, Vector3 to, string by, string reason,
                                     bool always = false)
        {
            if (!FlowTrace.Enabled) return;
            float dist = Vector3.Distance(from, to);
            string msg = $"HERO MOVED: {from} -> {to} ({dist:F1}m) by {by} reason={reason}";
            if (Active) FlowTrace.Step("DeathTrace", msg);
            else if (always) FlowTrace.Throttle("DeathTrace", "warp/" + by, 1f, msg);
        }

        /// <summary>A camera target/suspend/priority change during the window.</summary>
        public static void Camera(string change, string by)
        {
            if (!Active) return;
            FlowTrace.Step("DeathTrace", $"CAMERA: {change} by {by}");
        }

        /// <summary>Free-form window note (e.g. a pending scene route that will move the hero).</summary>
        public static void Note(string message)
        {
            if (!Active) return;
            FlowTrace.Step("DeathTrace", message);
        }

        // ── F8-15 self-reporting DEFECTS (owner 2026-07-08 "why so many screens") ──────
        // Two known death-flow defects that the next run must PROVE, not infer:
        //   • an end-state popup that opens WITHOUT registering with the PanelManager
        //     arbiter (so the single-modal law can't swap it / can't dismiss it) — Warn.
        //   • GameOverScreen pausing Time.timeScale=0 and (potentially) never restoring it —
        //     freeze step-in / restore step-out, plus a stuck-freeze Warn from the LateUpdate poll.

        /// <summary>A screen opened WITHOUT going through PanelManager (the arbiter never
        /// recorded it). This is the known F8-15 defect — end-states bypass the single-modal
        /// arbiter — so it self-reports at Warn level while the death window is live.</summary>
        public static void ScreenBypassedArbiter(string panelName, string by)
        {
            if (!Active) return;
            FlowTrace.Warn("DeathTrace",
                $"SCREEN OPENED *BYPASSING* PanelManager: {panelName} by {by} — no arbiter registration " +
                "(single-modal law can't swap/dismiss it; this is the known death-popup-stacking defect)");
        }

        // Freeze state (unscaled clock so it advances even while Time.timeScale==0).
        private static bool   _freezeActive;
        private static float  _freezeAtUnscaled;
        private static string _freezeBy = "?";
        private static bool   _freezeStuckWarned;

        /// <summary>Time.timeScale was set to 0 (hard pause). Step-in of the freeze; records the
        /// pending freeze so a missing restore self-reports via <see cref="PollFreezeStuck"/>.</summary>
        public static void TimeScaleFroze(string by, string context)
        {
            if (!FlowTrace.Enabled) return;
            _freezeActive      = true;
            _freezeAtUnscaled  = Time.unscaledTime;
            _freezeBy          = string.IsNullOrEmpty(by) ? "?" : by;
            _freezeStuckWarned = false;
            FlowTrace.Step("DeathTrace",
                $"TIMESCALE -> 0 (FROZEN, step-in) by {_freezeBy} — {context}");
        }

        /// <summary>Time.timeScale restored to running. Step-out of the freeze; pairs with the
        /// most recent <see cref="TimeScaleFroze"/> and reports how long the pause held.</summary>
        public static void TimeScaleRestored(string by)
        {
            if (!FlowTrace.Enabled) return;
            if (!_freezeActive)
            {
                FlowTrace.Step("DeathTrace",
                    $"TIMESCALE -> 1 (RESTORED, step-out) by {(string.IsNullOrEmpty(by) ? "?" : by)} (no freeze was pending)");
                return;
            }
            float held = Time.unscaledTime - _freezeAtUnscaled;
            _freezeActive = false;
            FlowTrace.Step("DeathTrace",
                $"TIMESCALE -> 1 (RESTORED, step-out) by {(string.IsNullOrEmpty(by) ? "?" : by)} " +
                $"— pause held {held:F1}s (frozen by {_freezeBy})");
        }

        /// <summary>Seconds a hard pause may hold before the poll flags it as never-restored.</summary>
        private const float FreezeStuckSeconds = 4f;

        /// <summary>Called each frame from the hero's LateUpdate while the window is live (LateUpdate
        /// runs even at timeScale==0). If a freeze has held past <see cref="FreezeStuckSeconds"/> of
        /// UNSCALED time and Time.timeScale is still 0, Warn ONCE — the self-report that the pause
        /// was set and never restored within the death flow.</summary>
        public static void PollFreezeStuck()
        {
            if (!_freezeActive || _freezeStuckWarned) return;
            if (!FlowTrace.Enabled) return;
            if (Time.timeScale > 0.0001f) return;   // still 0 == still frozen
            if (Time.unscaledTime - _freezeAtUnscaled < FreezeStuckSeconds) return;
            _freezeStuckWarned = true;
            FlowTrace.Warn("DeathTrace",
                $"TIMESCALE STILL 0 after {Time.unscaledTime - _freezeAtUnscaled:F1}s — {_freezeBy} froze time " +
                "and it has NOT been restored within the death flow (only Retry / sceneLoaded clears it; " +
                "any scaled-time respawn/down-beat coroutine is stalled behind this pause)");
        }

        /// <summary>Formats CallerMemberName + CallerFilePath into "Class.Method".</summary>
        public static string Describe(string memberName, string filePath)
        {
            string cls = "?";
            try
            {
                if (!string.IsNullOrEmpty(filePath))
                    cls = Path.GetFileNameWithoutExtension(filePath);
            }
            catch { /* diagnostic only — never throw into a caller */ }
            return cls + "." + (string.IsNullOrEmpty(memberName) ? "?" : memberName);
        }

        /// <summary>
        /// Stack-derived "Class.Method" of the nearest caller outside this class — for
        /// chokepoints that cannot take CallerInfo params (e.g. HeroLocomotion.WarpTo,
        /// which BattleArena resolves by an exact-signature reflection GetMethod).
        /// Costful — call only when a log will actually be emitted.
        /// </summary>
        public static string Caller(int skipFrames = 1)
        {
            try
            {
                var st = new System.Diagnostics.StackTrace(skipFrames, false);
                for (int i = 0; i < st.FrameCount; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    var t = m?.DeclaringType;
                    if (t == null || t == typeof(DeathTrace)) continue;
                    // Skip reflection-invoke plumbing (BattleArena.WarpHero calls WarpTo via
                    // MethodInfo.Invoke) so the line names the REAL caller, not the runtime.
                    string ns = t.Namespace ?? string.Empty;
                    if (ns.StartsWith("System.Reflection") || ns.StartsWith("System.Runtime")) continue;
                    return t.Name + "." + m.Name;
                }
            }
            catch { /* diagnostic only */ }
            return "unknown-caller";
        }
    }
}
