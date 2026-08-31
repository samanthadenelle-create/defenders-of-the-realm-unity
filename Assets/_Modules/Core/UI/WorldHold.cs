// =============================================================================
// WorldHold — WO-1149. THE ONE OWNER of a "stop the world" freeze.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE DEFECT IT CLOSES (owner, on device 2026-08-22): *"we need to stop game during
// transactions got killed while making purchase test."* Opening the store switched the
// HUD to Modal posture and NOTHING ELSE — wave timers kept ticking, the ATB kept
// running, enemies kept moving and attacking. A real purchase is not instant (wallet
// signs -> chain confirms -> server verifies -> entitlement recorded -> grant -> save
// verifies), so for many seconds the player could neither defend themselves nor cancel
// without abandoning a transaction that may already have been signed. "Paid but not
// granted" is already a ruled recoverable state (WO-1121 §1.1); we do not manufacture
// new ways into it.
//
// ⛔ WHY THIS IS A NEW FILE AND NOT A SECOND PAUSE OWNER — read this before "simplifying".
// The freeze mechanism already existed, in PauseController (DeNelle.Settings). It could
// not be reached from the code that charges the player: DeNelle.Wallet references
// DeNelle.Core ONLY (read the .asmdef — CLAUDE.md §5), and PauseController is a scene
// MonoBehaviour that is not guaranteed to exist in every scene a purchase can be opened
// from. So this file does NOT add an owner — it MOVES the single owner down into Core,
// where every assembly can reach it and no scene object is required:
//
//     * WorldHold is now the ONLY code in the project that writes Time.timeScale for a
//       freeze. PauseController's pause menu was converted to a CLIENT of it (it takes
//       the "pause-menu" hold instead of zeroing the clock itself).
//     * The WO-1016 capture guard moved here VERBATIM and now guards every caller
//       rather than one of them.
//
// REFERENCE-COUNTED, BECAUSE THE HOLDS OVERLAP. The player can open the pause menu
// during a purchase, or a purchase can begin from an already-paused state. The clock is
// frozen while ANY hold is outstanding and is restored exactly once, when the LAST one
// releases — so a pause-menu Resume mid-transaction does NOT unfreeze the world out
// from under the payment.
//
// ⛔ RESTORE THE CAPTURED SCALE, NEVER A HARDCODED 1.0. The pre-freeze value is captured
// on the 0 -> 1 hold transition and restored on the 1 -> 0 one. A purchase opened during
// a slow-motion beat or a dev time-skip must resume into THAT scale, not into full speed.
// A captured value of <= 0 is never meaningful to restore (it means another owner had
// already frozen the clock — the WO-1016 permanent-invisible-freeze signature), so it
// degrades to 1.
//
// ⛔ A HOLD THAT FAILS TO RELEASE IS WORSE THAN NO HOLD — a frozen game after a completed
// purchase is a support ticket AND a refund. Two structures make that unreachable:
//   1. Acquire() returns an IDisposable, so callers use `using` and the C# compiler
//      covers EVERY return, every early guard clause, every catch and every throw.
//      The next person who adds a branch to the purchase path gets the release free.
//   2. A watchdog (StuckHoldSeconds, UNSCALED time so it runs while frozen) force-
//      releases and logs FlowTrace.Fail. It exists for the one exit a `using` cannot
//      cover: the app being backgrounded mid-flight and an await that never resumes.
//
// Every transition traces with the reason name, so a stuck clock is diagnosable in one
// read of the log rather than by bisecting the purchase path (CLAUDE.md §12).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Reference-counted, cross-assembly "stop the world" freeze. The single owner of
    /// <see cref="Time.timeScale"/> for pausing purposes. Acquire a hold with
    /// <see cref="Acquire"/> inside a <c>using</c>; the world stays frozen until the last
    /// outstanding hold is disposed, then the CAPTURED pre-freeze scale is restored.
    /// </summary>
    public static class WorldHold
    {
        /// <summary>Reason token for the pause menu's own hold (PauseController).</summary>
        public const string ReasonPauseMenu = "pause-menu";

        /// <summary>Reason token for a money-path transaction (PackStore.Purchase).</summary>
        public const string ReasonPurchase = "purchase";

        /// <summary>
        /// Unscaled seconds after which an outstanding hold is force-released with a loud
        /// FlowTrace.Fail. Deliberately generous: a real chain settlement legitimately takes
        /// many seconds and some of it is outside our control, so this is a LAST RESORT for
        /// an await that never resumed (backgrounded mid-flight), not a timeout policy.
        /// </summary>
        public const float StuckHoldSeconds = 180f;

        /// <summary>Disposable hold token. Idempotent: double-dispose is a no-op, never a
        /// double-release that could unfreeze the world while another hold is outstanding.</summary>
        public sealed class Handle : IDisposable
        {
            internal readonly string Reason;
            internal float AcquiredUnscaled;
            private bool _released;

            internal Handle(string reason, float acquiredUnscaled)
            {
                Reason = string.IsNullOrEmpty(reason) ? "unnamed" : reason;
                AcquiredUnscaled = acquiredUnscaled;
            }

            /// <summary>True while this particular hold is still outstanding.</summary>
            public bool IsHeld => !_released;

            public void Dispose()
            {
                if (_released) return;
                _released = true;
                Release(this);
            }
        }

        private static readonly List<Handle> s_holds = new List<Handle>();

        // The scale observed at the moment the FIRST hold engaged — the value a full
        // release restores. Never assumed to be 1 (see the header).
        private static float s_scaleBeforeHold = 1f;
        private static bool s_frozen;
        private static bool s_applicationBackgrounded;
        private static float s_backgroundedAtUnscaled;

        /// <summary>True while at least one hold is outstanding (i.e. the world is frozen).</summary>
        public static bool IsHeld => s_holds.Count > 0;

        /// <summary>How many holds are outstanding. 0 means the world is running.</summary>
        public static int Count => s_holds.Count;

        /// <summary>True while THIS owner has the clock zeroed. Distinct from <see cref="IsHeld"/>
        /// only in the instant between the last release and the scale restore.</summary>
        public static bool IsClockFrozen => s_frozen;

        /// <summary>The scale a full release will restore. Exposed for oracles and logs.</summary>
        public static float CapturedScale => s_scaleBeforeHold > 0f ? s_scaleBeforeHold : 1f;

        /// <summary>Human-readable list of the outstanding hold reasons, for a one-read diagnosis.</summary>
        public static string Describe()
        {
            if (s_holds.Count == 0) return "none (world running)";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s_holds.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(s_holds[i].Reason);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Freezes the world (if it is not already held) and returns the token that unfreezes it.
        /// ⛔ ALWAYS call inside a <c>using</c> — that is what makes the release unskippable on
        /// every branch of the caller, including the ones nobody has written yet.
        /// </summary>
        public static Handle Acquire(string reason)
        {
            var handle = new Handle(reason, Time.unscaledTime);
            s_holds.Add(handle);

            if (s_holds.Count == 1)
            {
                float observed = Time.timeScale;
                s_scaleBeforeHold = observed > 0f ? observed : 1f;
                Time.timeScale = 0f;
                s_frozen = true;
                FlowTrace.Step("Pause",
                    $"WorldHold ACQUIRE '{handle.Reason}' -> timeScale 0 (captured {observed:F2}" +
                    (observed > 0f ? "" : " <= 0, ALREADY FROZEN by another owner - restoring to 1 instead") +
                    $"). Full release will restore {s_scaleBeforeHold:F2}.");
                EnsureWatchdog();
            }
            else
            {
                FlowTrace.Step("Pause",
                    $"WorldHold ACQUIRE '{handle.Reason}' -> already frozen, {s_holds.Count} holds outstanding " +
                    $"[{Describe()}]. The world stays frozen until the LAST one releases.");
            }

            return handle;
        }

        /// <summary>
        /// Renews a legitimate long-lived hold's watchdog age without changing ownership or the
        /// captured clock. Focused browsing surfaces call this while visible; a leaked/disabled
        /// owner stops renewing and is still caught by the ordinary watchdog deadline.
        /// </summary>
        public static void Renew(Handle handle)
        {
            if (handle == null || !handle.IsHeld || !s_holds.Contains(handle)) return;
            handle.AcquiredUnscaled = Time.unscaledTime;
        }

        /// <summary>
        /// Emergency release of EVERY outstanding hold, restoring the captured scale. For
        /// teardown paths that must never leave the engine frozen (quit to title, scene unload,
        /// the stuck-hold watchdog). Loud by design: a caller reaching this means something on
        /// the normal path failed to dispose.
        /// </summary>
        public static void ForceReleaseAll(string why)
        {
            if (s_holds.Count == 0)
            {
                // Nothing outstanding: do NOT stamp the clock. A stale capture written over a
                // legitimate running scale (a dev time-skip, a slow-motion beat) would be this
                // owner overreaching. Only a non-positive clock — which nobody can play through —
                // is corrected here.
                if (Time.timeScale <= 0f)
                {
                    Time.timeScale = 1f;
                    s_frozen = false;
                    FlowTrace.Warn("Pause",
                        $"WorldHold FORCE-RELEASE ({why}): no holds were outstanding but the clock read 0 — " +
                        "somebody else left the world frozen. Restored to 1.");
                }
                return;
            }

            FlowTrace.Warn("Pause",
                $"WorldHold FORCE-RELEASE ({why}): dropping {s_holds.Count} outstanding hold(s) [{Describe()}] " +
                $"and restoring timeScale {CapturedScale:F2}. The world must never be left frozen.");
            s_holds.Clear();
            RestoreScale();
        }

        /// <summary>Test/QA reset. Clears holds WITHOUT the warning, and restores the clock.</summary>
        public static void ResetForTests()
        {
            s_holds.Clear();
            s_scaleBeforeHold = 1f;
            s_frozen = false;
            s_applicationBackgrounded = false;
            s_backgroundedAtUnscaled = 0f;
            Time.timeScale = 1f;
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        private static void Release(Handle handle)
        {
            s_holds.Remove(handle);

            if (s_holds.Count > 0)
            {
                FlowTrace.Step("Pause",
                    $"WorldHold RELEASE '{handle.Reason}' -> STILL FROZEN, {s_holds.Count} hold(s) remain " +
                    $"[{Describe()}]. Restoring now would unfreeze the world under them.");
                return;
            }

            RestoreScale();
            FlowTrace.Step("Pause",
                $"WorldHold RELEASE '{handle.Reason}' -> last hold gone, timeScale {Time.timeScale:F2} " +
                $"(captured {s_scaleBeforeHold:F2}).");
        }

        private static void RestoreScale()
        {
            // Belt-and-braces with the capture guard: a restore is the LAST place a frozen world
            // can be re-armed, so it can never write a non-positive scale.
            Time.timeScale = s_scaleBeforeHold > 0f ? s_scaleBeforeHold : 1f;
            s_frozen = false;
        }

        private static WorldHoldWatchdog s_watchdog;

        private static void EnsureWatchdog()
        {
            if (s_watchdog != null) return;
            // Play-mode only: an editor oracle drives Acquire/Dispose synchronously and has no
            // Update loop to tick, so installing a hidden GameObject there would be litter.
            if (!Application.isPlaying) return;
            Guard.Try("Pause", "WorldHold watchdog install", () =>
            {
                var go = new GameObject("~WorldHoldWatchdog") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);
                s_watchdog = go.AddComponent<WorldHoldWatchdog>();
            });
        }

        /// <summary>
        /// Re-asserts the freeze if another owner stamped a scale while a hold is outstanding.
        /// Several combat/VFX effects write the engine-global timeScale, and an UNSCALED cleanup
        /// can finish after the freeze and stamp 1 — leaving a Paused screen (or a live purchase)
        /// over running gameplay. This lived in PauseController.LateUpdate before WO-1149; it moved
        /// here with the ownership, so it now protects the TRANSACTION hold too, in every scene,
        /// with or without a pause menu in it. The captured scale is never overwritten, so Resume
        /// still restores the exact pre-freeze value rather than a thief's.
        /// </summary>
        internal static void ReassertTick()
        {
            if (s_holds.Count == 0) return;
            if (Mathf.Approximately(Time.timeScale, 0f)) return;

            float stolen = Time.timeScale;
            Time.timeScale = 0f;
            FlowTrace.Warn("Pause",
                $"WORLD HOLD CLOCK REASSERTED: another owner wrote timeScale {stolen:F2} while " +
                $"{s_holds.Count} hold(s) [{Describe()}] were outstanding. Restored 0; the full release " +
                $"still restores the captured {CapturedScale:F2}.");
        }

        internal static void WatchdogTick()
        {
            if (s_holds.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = s_holds.Count - 1; i >= 0; i--)
            {
                if (now - s_holds[i].AcquiredUnscaled < StuckHoldSeconds) continue;
                FlowTrace.Fail("Pause",
                    $"⛔ STUCK WORLD HOLD: '{s_holds[i].Reason}' has been outstanding for " +
                    $"{now - s_holds[i].AcquiredUnscaled:F0}s (limit {StuckHoldSeconds:F0}s). Its owner never " +
                    "disposed it - the most likely cause is the app being backgrounded mid-flight and an " +
                    "await that never resumed. Force-releasing so the player is not left in a frozen game.");
                s_holds.RemoveAt(i);
            }
            if (s_holds.Count == 0) RestoreScale();
        }

        /// <summary>
        /// Excludes OS-suspended time from watchdog age. Android resumes Unity after an arbitrary
        /// wall-clock gap; counting that gap made a legitimate open pause menu look 300-500 seconds
        /// abandoned on the first foreground frame (WO-1260). Real foreground leaks still reach the
        /// unchanged 180-second watchdog ceiling.
        /// </summary>
        internal static void NotifyApplicationPause(bool paused)
        {
            NotifyApplicationPause(paused, Time.unscaledTime);
        }

        // Explicit clock overload is the deterministic regression seam; production always uses the
        // Time.unscaledTime wrapper above.
        internal static void NotifyApplicationPause(bool paused, float nowUnscaled)
        {
            if (paused)
            {
                if (!s_applicationBackgrounded)
                {
                    s_applicationBackgrounded = true;
                    s_backgroundedAtUnscaled = nowUnscaled;
                }
                return;
            }
            if (!s_applicationBackgrounded) return;

            float suspendedSeconds = Mathf.Max(0f, nowUnscaled - s_backgroundedAtUnscaled);
            s_applicationBackgrounded = false;
            s_backgroundedAtUnscaled = 0f;
            if (suspendedSeconds <= 0f || s_holds.Count == 0) return;
            for (int i = 0; i < s_holds.Count; i++)
                s_holds[i].AcquiredUnscaled += suspendedSeconds;
            FlowTrace.Step("Pause",
                $"WorldHold watchdog excluded {suspendedSeconds:F0}s of OS-suspended time from " +
                $"{s_holds.Count} hold(s) [{Describe()}]. Foreground leak detection remains armed.");
        }

        // Static state survives nothing but a domain reload; re-arm a clean slate on play so a
        // stale hold from a previous session can never start the next one frozen.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_holds.Clear();
            s_scaleBeforeHold = 1f;
            s_frozen = false;
            s_watchdog = null;
            s_applicationBackgrounded = false;
            s_backgroundedAtUnscaled = 0f;
        }
    }
}
