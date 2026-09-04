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
// ⛔ ...BUT A CAPTURED NON-1 SCALE HAS A SHELF LIFE (2026-09-02 — THE AMPLIFIER FIX).
// CAPTURED DEFECT, owner F8 seq 4656:
//     [Flow:Pause] WorldHold ACQUIRE 'pause-menu' -> timeScale 0 (captured 0.28).
//     timeScale=0.28 dt=0.0047 inputSuppressed=False
// The world clock was ALREADY 0.28 before the menu opened — WaveCelebrationManager's
// wave-clear dip (_slowMoScale = 0.28f) had leaked. The guard above only ever asked "is
// the observed scale <= 0"; it had NOTHING to say about a leaked POSITIVE one. So this
// class faithfully captured a dead value and faithfully restored it, and a transient
// cosmetic dip became PERMANENT the instant the player opened a menu. That is the
// difference between a bug the player shrugs off and the owner's long-standing "in town
// everything slowed" — this file was the amplifier, not the source.
//
// The discriminator is TIME, and it is exact. Every non-1 writer in the tree is a BOUNDED
// cosmetic transient — HitStopManager 0.02-0.05 (<0.1 s), CombatFeedbackManager hit stop
// 0.05 / kill slow-mo 0.30 (0.45 s), WaveCelebrationManager 0.28 (0.9 s + 0.3 s ease),
// HeroHitReaction 0.30 (1.2 s ramp), ArenaDeathCam (saves + restores its own). Verified by
// reading every `Time.timeScale =` assignment under Assets/_Modules/ on 2026-09-02: there
// is NO persistent slow-motion mode and NO dev time-skip in this project, so NOTHING
// legitimately holds a non-1 scale for longer than ~1.2 unscaled seconds. Every one of
// those dips also runs on UNSCALED time, so it finishes DURING our freeze and its captured
// value is stale before the player has finished reading the menu.
//
// Hence: a captured non-1 baseline is restored only while it is still PLAUSIBLY LIVE
// (SuspectBaselineGraceSeconds). Held longer than that, we restore 1.0 and say — loudly,
// with the number and the candidate owners — that we refused to launder it. The legitimate
// case the WO-1149 acceptance protects (a menu opened on top of a real slow-motion beat and
// closed again promptly) round-trips exactly as before; only the case that CANNOT be
// legitimate is corrected, and it is NAMED rather than swallowed (CLAUDE.md §12).
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
//
// =============================================================================
// WO-1353 (2026-09-03) — THE FREEZE OWNER BECAME THE WORLD CLOCK OWNER.
// -----------------------------------------------------------------------------
// CAPTURED DEFECT (owner felt-test, Main_Castle_Overworld, no battle, no modal):
//     [Flow:HeroOwner] ... animSpeed=0.00 timeScale=0.28 dt=0.0046
//     inputSuppressed=False autoWalk=False
// 28% speed in open town. Every timer, animation, cooldown and the wave clock all wrong
// together, and nothing on screen said so. The hero read velSelf=0.00 because the clock
// was nearly stopped, which is why it also presented as "frozen in place".
//
// ⛔ THE 0.28 IS WaveCelebrationManager._slowMoScale, AND IT IS THE ONLY 0.28 IN THE TREE.
// But naming that writer is NOT the fix, because the leak was never one class's bug. As of
// 2026-09-02 there were FIVE separate per-class ownership mechanisms (HitStopManager,
// CombatFeedbackManager, WaveCelebrationManager, HeroHitReaction, ArenaDeathCam), each with
// its own s_ourScale + deadline sweep, and each ending in the same branch: "the clock does
// not read MY value, so release WITHOUT stamping and say so". That branch is individually
// CORRECT — stamping would make each class an Nth writer — and collectively it is the
// defect: when two dips overlap, BOTH correctly decline to restore and the residue is left
// on the engine global with every owner having honourably walked away. Five right decisions
// producing one wrong clock. The documented collision is in Enemy.cs, where a hero kill
// starts CombatFeedbackManager's 0.30 kill slow-mo and TWELVE LINES LATER HitStopManager
// stamps 0.04 over it on the same frame.
//
// THE OWNER'S RULING, verbatim: *"I want a guard on all time changes"* / *"every battle
// death victory"* / *"anything that steps into time slow needs to step to time return"*.
//
// So this class stops being the freeze owner and becomes THE world clock owner. Nothing
// else in shipping code writes Time.timeScale; a cosmetic dip takes a HOLD at its scale
// (AcquireScale) and releases it, exactly as a modal takes a hold at 0. There is no longer
// any such thing as a foreign value to decline to stamp over, because there is no second
// writer to produce one.
//
//   CONFLICT RULE = SLOWEST WINS (minimum over live holds). Argued in full on AcquireScale:
//   a freeze is a REQUIREMENT and a dip is a PREFERENCE, and minimum is MONOTONE, so
//   releasing any hold can only move the world toward 1.00. That is what makes the whole
//   ticket's invariant — ZERO LIVE HOLDS IMPLIES 1.00 — provable instead of hoped for.
//
//   THREE FAILSAFES, because a paired contract still breaks:
//     1. every hold carries a MAXIMUM duration on the UNSCALED clock and self-releases with
//        a FlowTrace.Fail naming its overrun (CosmeticHoldSeconds 5 s for a beat whose
//        longest legitimate form is 1.2 s; StuckHoldSeconds 180 s for a chain settlement);
//     2. a SCENE LOAD releases every hold and resets the clock — Time.timeScale is an
//        ENGINE GLOBAL and SceneManager.LoadScene does NOT reset it, and the hosts that
//        took those holds are gone;
//     3. a DRIFT WATCHDOG on the unscaled clock restores the baseline and FAILS loudly when
//        the scale is wrong with zero live holds. That is precisely the state measured on
//        2026-09-03, and it is the one this ticket exists to make impossible to ship silently.
//
// ⭐ GAME FEEL IS UNCHANGED. Not one scale, duration, curve or trigger moved: 0.28 is still
// 0.28 for 0.9 s + 0.3 s of ease, the hit stop is still 0.02-0.05, the death ramp is still
// 0.30 over 1.2 s. This changed OWNERSHIP and GUARANTEES, never tuning (WO-1353 constraint).
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

        /// <summary>Reason token for the combat HUD's paused consumable picker.</summary>
        public const string ReasonCombatItemPicker = "combat-item-picker";

        /// <summary>
        /// WHAT KIND OF HOLD THIS IS. The distinction is CATEGORICAL, not a bigger number
        /// (WO-1360, owner F8 seq 4679).
        ///
        /// <para><b>A ceiling can only judge a hold whose length the CODE owns.</b> A hit stop is
        /// milliseconds, a celebration under a second, a death cam fifteen  -  for those, "still
        /// outstanding well past the longest legitimate run" really does mean the owner died
        /// without disposing (a coroutine killed by a deactivated host fires no OnDestroy and
        /// throws nothing, so nothing else can catch it) and force-releasing is right.</para>
        ///
        /// <para><b>A hold whose length the PLAYER owns has no such number.</b> A pause menu, a
        /// death screen, a bug-report form, a modal the player is reading  -  a human can leave any
        /// of them open for hours, and backgrounding the app is the normal way to do it. Judging
        /// those by elapsed time is a category error, and the consequence is worse than the leak
        /// the ceiling guards: on 2026-09-03 the 180 s ceiling force-released 'pause-menu' after
        /// 507 s and the world ran underneath a PAUSED screen  -  the exact WO-1016 shape the
        /// slowest-wins rule exists to prevent (screenshot:
        /// logs/f8-inbox/device/SM02G4061955851/break_01_error.png).</para>
        ///
        /// <para>STOP: RAISING THE NUMBER IS NOT THE FIX. It reproduces the same bug at a longer
        /// timeout. <see cref="BoundedBeat"/> stays the DEFAULT for every existing and future
        /// caller; an unbounded hold must be ASKED for by name
        /// (<see cref="AcquirePlayerOwned"/>), so an author cannot get it wrong by accident.</para>
        /// </summary>
        public enum HoldKind
        {
            /// <summary>The code owns the duration. The watchdog ceiling applies. THE DEFAULT.</summary>
            BoundedBeat = 0,

            /// <summary>The PLAYER owns the duration. No ceiling; never force-released by age.
            /// Still reported once, loudly, so a capture can read it.</summary>
            PlayerOwned = 1,
        }

        /// <summary>
        /// Unscaled seconds after which an open PLAYER-OWNED hold names itself in the trace  -  ONCE,
        /// as a Warn, and it is NOT force-released. This is observability, not a deadline: a capture
        /// taken during a long pause must still say what is holding the world, or CLAUDE.md sec.12 has
        /// no data to read. Deliberately the old ceiling value so a trace read against tonight's
        /// logs lines up.
        /// </summary>
        public const float PlayerOwnedReportSeconds = 180f;

        /// <summary>
        /// Unscaled seconds after which an outstanding hold is force-released with a loud
        /// FlowTrace.Fail. Deliberately generous: a real chain settlement legitimately takes
        /// many seconds and some of it is outside our control, so this is a LAST RESORT for
        /// an await that never resumed (backgrounded mid-flight), not a timeout policy.
        /// </summary>
        public const float StuckHoldSeconds = 180f;

        /// <summary>
        /// Unscaled seconds a captured NON-1 baseline stays restorable. Past this the capture is
        /// treated as a leaked transient and the release restores 1.0 with a loud warning.
        /// <para>Sized from the tree, not from taste: the longest deliberate non-1 beat in the
        /// project is HeroHitReaction's 1.2 s death ramp, and every dip runs on unscaled time so it
        /// completes while we are frozen. 2 s clears the longest legitimate beat with margin and is
        /// far below any human menu dwell, which is what makes it a clean discriminator rather than
        /// a tuning knob.</para>
        /// </summary>
        public const float SuspectBaselineGraceSeconds = 2f;

        /// <summary>
        /// Reason token for a cosmetic slow-motion beat (hit stop, kill slow-mo, wave-clear dip,
        /// death ramp, arena death cam). Callers pass their own descriptive name; this is the
        /// prefix convention so a capture reads the CLASS of hold before the instance.
        /// </summary>
        public const string ReasonCosmeticPrefix = "fx:";

        /// <summary>
        /// Default maximum UNSCALED seconds a COSMETIC (non-freeze) hold may live. The longest
        /// deliberate beat in the tree is HeroHitReaction's 1.2 s death ramp, so 5 s clears every
        /// legitimate one by 4x while still catching a stranded dip within a breath rather than
        /// within the 180 s a transaction is allowed. Callers may pass their own.
        /// </summary>
        public const float CosmeticHoldSeconds = 5f;

        /// <summary>
        /// Unscaled seconds the clock may sit away from 1.00 with ZERO live holds before the
        /// watchdog restores it and reports. Non-zero only so a foreign one-frame write (vendor
        /// demo code, an editor tool) is not reported as a leak on the frame it happens.
        /// </summary>
        public const float DriftGraceSeconds = 0.5f;

        /// <summary>Disposable hold token. Idempotent: double-dispose is a no-op, never a
        /// double-release that could unfreeze the world while another hold is outstanding.</summary>
        public sealed class Handle : IDisposable
        {
            internal readonly string Reason;
            internal float AcquiredUnscaled;

            /// <summary>The scale THIS hold requests. 0 for a freeze; a cosmetic dip requests its
            /// own value. The world runs at the MINIMUM across every live hold.</summary>
            internal float Scale;

            /// <summary>Unscaled seconds this hold may live before the watchdog force-releases it
            /// with a FlowTrace.Fail. Meaningless for a <see cref="HoldKind.PlayerOwned"/> hold,
            /// which has no ceiling at all.</summary>
            internal float MaxSeconds;

            /// <summary>Bounded beat (ceiling applies) vs player-owned open state (no ceiling).
            /// See <see cref="HoldKind"/>  -  the distinction is categorical, not a duration.</summary>
            internal HoldKind Kind;

            /// <summary>Set once a player-owned hold has named itself in the trace, so a long
            /// pause reports one line rather than one per watchdog tick.</summary>
            internal bool OpenReported;

            private bool _released;

            internal Handle(string reason, float acquiredUnscaled)
            {
                Reason = string.IsNullOrEmpty(reason) ? "unnamed" : reason;
                AcquiredUnscaled = acquiredUnscaled;
            }

            /// <summary>The scale this hold is asking the world to run at.</summary>
            public float RequestedScale => Scale;

            /// <summary>True when the PLAYER owns how long this hold lasts, so no elapsed-time
            /// ceiling may judge it stuck. Exposed for oracles and captures.</summary>
            public bool IsPlayerOwned => Kind == HoldKind.PlayerOwned;

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

        // Unscaled time the baseline above was captured. Only meaningful when the capture is
        // non-1; it is what turns "is this scale still plausibly live?" into an answerable
        // question instead of a guess. See SuspectBaselineGraceSeconds.
        private static float s_capturedAtUnscaled;

        // Unscaled time the zero-hold drift watchdog first SAW a wrong clock. 0 means "no drift in
        // flight". A grace window exists only so a foreign one-frame write is not reported as a
        // leak on the frame it happens; a real leak is reported within half a second.
        private static float s_lastDriftSeenUnscaled;

        // The last hold to release, kept purely so a drift report can name what ran just before it.
        // That one field is the difference between "the clock is 0.28" and "the clock is 0.28 and
        // the wave-clear dip released 40 ms ago" - which is the whole cost of the 2026-09-03 P0.
        private static string s_lastReleasedReason = "none";
        private static float s_lastReleasedAtUnscaled;

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
            return AcquireScale(reason, 0f, StuckHoldSeconds);
        }

        /// <summary>
        /// Freezes the world for an OPEN STATE THE PLAYER OWNS  -  a pause menu, a death screen, a
        /// form the player is typing into. <b>No watchdog ceiling: this hold is never
        /// force-released because time passed.</b>
        ///
        /// <para>STOP: THIS IS THE ONE YOU MUST ASK FOR BY NAME, and that is deliberate. Every other
        /// entry point stays bounded, so a new caller who does not think about the distinction gets
        /// the safe, leak-detecting default. Reach for this ONLY when a human, not the code,
        /// decides when the hold ends (WO-1360).</para>
        ///
        /// <para>Removing the ceiling removes ONE net, not all of them. What still catches this
        /// hold: <see cref="ReleaseAllForSceneLoad"/> (a scene change drops every hold  -  quit to
        /// title cannot land frozen), <see cref="ForceReleaseAll"/> on teardown paths, the
        /// zero-holds drift watchdog once it IS released, and  -  the one that actually matters  - 
        /// the owning UI's own OnDisable/OnDestroy step-out. A player-owned hold whose UI can die
        /// without disposing is a real hole; the owner closes it, not a timer.</para>
        /// </summary>
        public static Handle AcquirePlayerOwned(string reason)
        {
            return AcquireKind(reason, 0f, 0f, HoldKind.PlayerOwned);
        }

        /// <summary>
        /// <see cref="AcquirePlayerOwned"/> at a scale other than 0  -  a player-owned state that
        /// slows the world rather than stopping it. Same rules: no ceiling, must be asked for.
        /// </summary>
        public static Handle AcquirePlayerOwnedScale(string reason, float scale)
        {
            return AcquireKind(reason, scale, 0f, HoldKind.PlayerOwned);
        }

        /// <summary>
        /// Acquires a hold that asks the world to run at <paramref name="scale"/> — the general
        /// form behind <see cref="Acquire"/>, which is simply this at scale 0.
        ///
        /// <para><b>THE CONFLICT RULE IS SLOWEST-WINS (minimum across live holds), and it is not a
        /// taste call.</b> A freeze is a hard REQUIREMENT — a purchase or a modal must stop the
        /// world — while a dip is a cosmetic PREFERENCE, so last-writer-wins would let a hit stop
        /// starting mid-purchase un-freeze live gameplay under a Paused screen (the WO-1016 shape
        /// the reassert tick exists to patch). Minimum is also MONOTONE: releasing any hold can only
        /// move the world toward 1.00, never further from it, which is what makes "zero live holds
        /// implies 1.00" provable rather than hoped for.</para>
        ///
        /// <para>⛔ ALWAYS inside a <c>using</c>, or paired with an explicit Dispose on every exit
        /// of the owner's lifecycle (OnDisable AND OnDestroy — a coroutine killed by deactivation
        /// fires neither a finally nor OnDestroy; that is the 2026-09-02 leak).</para>
        /// </summary>
        /// <param name="reason">Named in every trace line. This is what a future capture reads.</param>
        /// <param name="scale">Requested world scale. Clamped to >= 0.</param>
        /// <param name="maxUnscaledSeconds">Watchdog ceiling on the UNSCALED clock.</param>
        public static Handle AcquireScale(string reason, float scale, float maxUnscaledSeconds)
        {
            return AcquireKind(reason, scale, maxUnscaledSeconds, HoldKind.BoundedBeat);
        }

        /// <summary>
        /// The one construction path behind every Acquire form. <paramref name="kind"/> is what
        /// decides whether elapsed time may judge this hold stuck  -  see <see cref="HoldKind"/>.
        /// Private on purpose: the public surface makes the unbounded case a named request.
        /// </summary>
        private static Handle AcquireKind(string reason, float scale, float maxUnscaledSeconds, HoldKind kind)
        {
            float want = Mathf.Max(0f, scale);
            var handle = new Handle(reason, Time.unscaledTime)
            {
                Scale = want,
                Kind = kind,
                MaxSeconds = kind == HoldKind.PlayerOwned
                    ? 0f
                    : (maxUnscaledSeconds > 0f ? maxUnscaledSeconds : CosmeticHoldSeconds),
            };
            s_holds.Add(handle);

            if (s_holds.Count == 1)
            {
                float observed = Time.timeScale;
                s_scaleBeforeHold = observed > 0f ? observed : 1f;
                s_capturedAtUnscaled = Time.unscaledTime;
                ApplyEffective();
                FlowTrace.Step("Pause",
                    $"WorldHold ACQUIRE '{handle.Reason}' -> timeScale {EffectiveScale:F2} (captured {observed:F2}" +
                    (observed > 0f ? "" : " <= 0, ALREADY FROZEN by another owner - restoring to 1 instead") +
                    $"). Full release will restore {s_scaleBeforeHold:F2}.");

                // THE LEAD LINE (2026-09-02). Capturing a non-1 scale is legal but it is never
                // ROUTINE: it means a cosmetic dip was live at the exact instant a menu opened. If
                // the world is slow after this hold, this line is where the investigation starts,
                // so it names the number and the owners that can write it rather than leaving the
                // next capture to start from zero.
                if (observed > 0f && !Mathf.Approximately(observed, 1f))
                {
                    FlowTrace.Warn("Pause",
                        $"WorldHold captured a NON-1 baseline of {observed:F2} ({observed * 100f:F0}% speed) " +
                        $"for '{handle.Reason}'. Somebody's slow-motion beat was live when this hold opened. " +
                        $"It will be restored ONLY if this hold releases within {SuspectBaselineGraceSeconds:F0}s; " +
                        "held longer, that scale cannot still be a live beat (the longest in the tree is 1.2s) " +
                        "and we restore 1.00 instead of making a transient dip permanent. Owners that write a " +
                        "non-1 scale: HitStopManager, CombatFeedbackManager (hit stop / kill slow-mo), " +
                        "WaveCelebrationManager, HeroHitReaction, ArenaDeathCam.");
                }

                EnsureWatchdog();
            }
            else
            {
                ApplyEffective();
                FlowTrace.Step("Pause",
                    $"WorldHold ACQUIRE '{handle.Reason}' @ {want:F2} -> effective timeScale " +
                    $"{EffectiveScale:F2} (slowest wins), {s_holds.Count} holds outstanding " +
                    $"[{Describe()}]. The world runs at 1.00 again only when the LAST one releases.");
                EnsureWatchdog();
            }

            if (kind == HoldKind.PlayerOwned)
                FlowTrace.Step("Pause",
                    $"WorldHold '{handle.Reason}' is PLAYER-OWNED: the player decides when it ends, " +
                    "so NO watchdog ceiling applies and it will never be force-released for being " +
                    $"old. It names itself once after {PlayerOwnedReportSeconds:F0}s so a capture can " +
                    "still read it. Its release is owned by the UI that took it (WO-1360).");

            return handle;
        }

        /// <summary>
        /// Re-points a LIVE hold at a new scale — the seam a RAMP needs (an ease-back, a lerp into
        /// slow-mo). The hold keeps its identity and its watchdog deadline, so a ramp is one hold
        /// that moves, never a stream of acquire/release pairs that could interleave with someone
        /// else's and leave a residue. A dead or foreign handle is ignored.
        /// </summary>
        public static void SetScale(Handle handle, float scale)
        {
            if (handle == null || !handle.IsHeld || !s_holds.Contains(handle)) return;
            float want = Mathf.Max(0f, scale);
            if (Mathf.Approximately(handle.Scale, want)) return;
            handle.Scale = want;
            ApplyEffective();
        }

        /// <summary>
        /// The scale the world SHOULD be running at right now: the minimum across every live hold,
        /// or the restorable baseline when there are none. This is the only value written to
        /// <see cref="Time.timeScale"/> anywhere in shipping code.
        /// </summary>
        public static float EffectiveScale
        {
            get
            {
                if (s_holds.Count == 0) return RestorableBaseline();
                float min = float.MaxValue;
                for (int i = 0; i < s_holds.Count; i++)
                    if (s_holds[i].Scale < min) min = s_holds[i].Scale;
                return min >= float.MaxValue ? 1f : min;
            }
        }

        /// <summary>THE ONE WRITE. Every path that changes the world clock funnels here, which is
        /// what makes "who slowed the clock" answerable from one grep of one file.</summary>
        private static void ApplyEffective()
        {
            float want = EffectiveScale;
            Time.timeScale = want;
            s_frozen = want <= 0f;
            s_lastDriftSeenUnscaled = 0f;
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
            s_capturedAtUnscaled = 0f;
            s_frozen = false;
            s_applicationBackgrounded = false;
            s_backgroundedAtUnscaled = 0f;
            s_lastDriftSeenUnscaled = 0f;
            s_lastReleasedReason = "none";
            s_lastReleasedAtUnscaled = 0f;
            Time.timeScale = 1f;
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        private static void Release(Handle handle)
        {
            s_holds.Remove(handle);

            float heldFor = Mathf.Max(0f, Time.unscaledTime - handle.AcquiredUnscaled);
            s_lastReleasedReason = handle.Reason;
            s_lastReleasedAtUnscaled = Time.unscaledTime;

            if (s_holds.Count > 0)
            {
                ApplyEffective();
                FlowTrace.Step("Pause",
                    $"WorldHold RELEASE '{handle.Reason}' @ {handle.Scale:F2} after {heldFor:F2}s unscaled " +
                    $"-> STILL HELD, {s_holds.Count} hold(s) remain [{Describe()}], effective timeScale " +
                    $"{Time.timeScale:F2}. Restoring 1.00 now would unfreeze the world under them.");
                return;
            }

            RestoreScale();
            FlowTrace.Step("Pause",
                $"WorldHold RELEASE '{handle.Reason}' @ {handle.Scale:F2} after {heldFor:F2}s unscaled " +
                $"-> LAST hold gone, timeScale {Time.timeScale:F2} (captured {s_scaleBeforeHold:F2}). " +
                "Zero live holds means the world runs at 1.00.");
        }

        /// <summary>
        /// The scale a FULL release lands on: normally 1.00, and the captured pre-hold baseline
        /// only while that capture can still plausibly be a LIVE beat owned by something outside
        /// this class (vendor demo code, an editor tool, an unconverted writer). Pure — it logs
        /// nothing and mutates nothing, so <see cref="EffectiveScale"/> can ask it every frame.
        /// </summary>
        private static float RestorableBaseline()
        {
            float restore = s_scaleBeforeHold > 0f ? s_scaleBeforeHold : 1f;
            if (Mathf.Approximately(restore, 1f)) return 1f;
            float heldFor = Mathf.Max(0f, Time.unscaledTime - s_capturedAtUnscaled);
            return heldFor > SuspectBaselineGraceSeconds ? 1f : restore;
        }

        private static void RestoreScale()
        {
            // Belt-and-braces with the capture guard: a restore is the LAST place a frozen world
            // can be re-armed, so it can never write a non-positive scale.
            float restore = s_scaleBeforeHold > 0f ? s_scaleBeforeHold : 1f;

            // ⛔ THE AMPLIFIER GUARD (owner F8 seq 4656). The capture guard above rejects a
            // non-POSITIVE scale; nothing rejected a leaked POSITIVE one, so a dead 0.28 from a
            // wave-clear dip was captured, held across the whole pause menu, and then written back
            // as if it were the world's true resting speed. Restoring a slow-motion beat that ended
            // seconds ago is not fidelity, it is laundering — and it is what turned a transient bug
            // into the owner's permanent "in town everything slowed".
            if (!Mathf.Approximately(restore, 1f))
            {
                float heldFor = Mathf.Max(0f, Time.unscaledTime - s_capturedAtUnscaled);
                if (heldFor > SuspectBaselineGraceSeconds)
                {
                    FlowTrace.Warn("Pause",
                        $"WorldHold REFUSED TO RESTORE a stale baseline: it captured {restore:F2} " +
                        $"({restore * 100f:F0}% speed) {heldFor:F1}s ago, which is past the " +
                        $"{SuspectBaselineGraceSeconds:F0}s shelf life for a live slow-motion beat (the " +
                        "longest deliberate dip in the tree is 1.2s, and every one of them runs on " +
                        "UNSCALED time so it finished while we were frozen). Restoring 1.00 instead. " +
                        "READ THIS AS A LEAD: the clock was ALREADY slow before this hold opened, so " +
                        $"whoever wrote {restore:F2} leaked it - candidates are HitStopManager, " +
                        "CombatFeedbackManager (hit stop 0.05 / kill slow-mo 0.30), WaveCelebrationManager " +
                        "(wave-clear dip 0.28), HeroHitReaction (death 0.30), ArenaDeathCam.");
                    restore = 1f;
                    s_scaleBeforeHold = 1f;
                }
            }

            ApplyEffective();
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
            float want = EffectiveScale;
            if (Mathf.Approximately(Time.timeScale, want)) return;

            float stolen = Time.timeScale;
            ApplyEffective();
            FlowTrace.Warn("Pause",
                $"WORLD HOLD CLOCK REASSERTED: another owner wrote timeScale {stolen:F2} while " +
                $"{s_holds.Count} hold(s) [{Describe()}] were outstanding. Restored {want:F2} (slowest " +
                "of the live holds). Anything that writes Time.timeScale outside this class is a " +
                "SECOND OWNER and the lint in BattleQuiescenceRegression will name it.");
        }

        internal static void WatchdogTick() => WatchdogTick(Time.unscaledTime);

        /// <summary>
        /// Explicit-clock overload — the deterministic ORACLE SEAM, matching the one
        /// <see cref="NotifyApplicationPause(bool, float)"/> already provides. Public because the
        /// regression suite lives in the DeNelle.Editor assembly and this class's InternalsVisibleTo
        /// names only DeNelle.Core.Tests; a failsafe nothing can drive is a failsafe nobody has
        /// proven. Production always goes through the no-argument form above.
        /// </summary>
        public static void WatchdogTick(float nowUnscaled)
        {
            float now = nowUnscaled;

            if (s_holds.Count == 0)
            {
                // ⛔ THE INVARIANT, ENFORCED: zero live holds means the world runs at the restorable
                // baseline (normally 1.00). Anything else is somebody's leak - which is EXACTLY the
                // state measured on 2026-09-03 (timeScale 0.28 in open town, no battle, no modal,
                // input not suppressed). Corrected, and NAMED - never silently.
                float want = RestorableBaseline();
                if (Mathf.Approximately(Time.timeScale, want)) { s_lastDriftSeenUnscaled = 0f; return; }

                if (s_lastDriftSeenUnscaled <= 0f) { s_lastDriftSeenUnscaled = now; return; }
                if (now - s_lastDriftSeenUnscaled < DriftGraceSeconds) return;

                float drifted = Time.timeScale;
                float driftedFor = now - s_lastDriftSeenUnscaled;
                s_lastDriftSeenUnscaled = 0f;
                Time.timeScale = want;
                FlowTrace.Fail("Pause",
                    $"⛔ WORLD CLOCK DRIFT: timeScale read {drifted:F2} ({drifted * 100f:F0}% speed) for " +
                    $"{driftedFor:F2}s with ZERO live holds. Restored {want:F2}. Zero holds ALWAYS means " +
                    "1.00, so this is a second writer of Time.timeScale, not a hold that failed to " +
                    $"release. Last hold to release was '{s_lastReleasedReason}' at unscaled " +
                    $"{s_lastReleasedAtUnscaled:F2} ({now - s_lastReleasedAtUnscaled:F2}s ago). If that " +
                    "reason is 'none' nothing of ours ever held the clock and the writer is outside " +
                    "WorldHold entirely - vendor demo code or an unconverted owner.");
                return;
            }

            for (int i = s_holds.Count - 1; i >= 0; i--)
            {
                float age = now - s_holds[i].AcquiredUnscaled;

                // STOP: A PLAYER-OWNED HOLD IS NEVER STUCK BECAUSE IT IS OLD (WO-1360). A human can
                // leave a pause menu, a death screen or a bug-report form open for hours, and
                // backgrounding the app is the normal way to do it. Force-releasing one unfreezes
                // live gameplay UNDERNEATH a modal that still says PAUSED - which is strictly worse
                // than the leak a ceiling guards, and is what shipped tonight (owner F8 seq 4679:
                // 'pause-menu' killed at 507.3s past a 180.0s ceiling with the menu still on
                // screen). It still names itself ONCE so sec.12 has data to read.
                if (s_holds[i].Kind == HoldKind.PlayerOwned)
                {
                    if (!s_holds[i].OpenReported && age >= PlayerOwnedReportSeconds)
                    {
                        s_holds[i].OpenReported = true;
                        FlowTrace.Warn("Pause",
                            $"OPEN PLAYER-OWNED HOLD: '{s_holds[i].Reason}' (scale " +
                            $"{s_holds[i].Scale:F2}) has been outstanding for {age:F1}s. This is " +
                            "NOT a leak and it will NOT be force-released - the player owns how " +
                            "long it lasts, and unfreezing the world under an open modal is the " +
                            "worse failure. Logged once so a capture taken during a long pause can " +
                            $"still say what holds the clock. Outstanding: [{Describe()}].");
                    }
                    continue;
                }

                float limit = s_holds[i].MaxSeconds > 0f ? s_holds[i].MaxSeconds : StuckHoldSeconds;
                if (age < limit) continue;
                FlowTrace.Fail("Pause",
                    $"⛔ STUCK WORLD HOLD: '{s_holds[i].Reason}' (scale {s_holds[i].Scale:F2}) has been " +
                    $"outstanding for {age:F1}s, past its {limit:F1}s ceiling. It OVERRAN by " +
                    $"{age - limit:F1}s. Its owner never disposed it - for a cosmetic beat that means the " +
                    "host was deactivated or destroyed mid-dip (which kills a coroutine without firing " +
                    "OnDestroy and without throwing, so no try/finally could have caught it); for a " +
                    "transaction it means the app was backgrounded and an await never resumed. " +
                    "Force-releasing so the world is not left slow.");
                s_holds.RemoveAt(i);
            }

            if (s_holds.Count == 0) RestoreScale();
            else ApplyEffective();
        }

        /// <summary>
        /// Corrects the clock IF and only if it has drifted with zero live holds, and says so.
        /// The seam an OBSERVER (BattleQuiescenceGate) uses to hand a leak back to the owner
        /// instead of writing <see cref="Time.timeScale"/> itself and becoming a second writer.
        /// Returns true when it actually corrected something.
        /// </summary>
        public static bool RestoreIfDrifted(string why)
        {
            if (s_holds.Count > 0)
            {
                float want = EffectiveScale;
                if (Mathf.Approximately(Time.timeScale, want)) return false;
                float stolen = Time.timeScale;
                ApplyEffective();
                FlowTrace.Warn("Pause",
                    $"WorldHold corrected timeScale {stolen:F2} -> {want:F2} at the request of '{why}' " +
                    $"while {s_holds.Count} hold(s) [{Describe()}] were live.");
                return true;
            }

            float baseline = RestorableBaseline();
            if (Mathf.Approximately(Time.timeScale, baseline)) return false;

            float drifted = Time.timeScale;
            s_lastDriftSeenUnscaled = 0f;
            Time.timeScale = baseline;
            FlowTrace.Fail("Pause",
                $"⛔ WORLD CLOCK DRIFT corrected at the request of '{why}': timeScale was {drifted:F2} " +
                $"({drifted * 100f:F0}% speed) with ZERO live holds; restored {baseline:F2}. The last " +
                $"hold to release was '{s_lastReleasedReason}'. Zero holds always means 1.00, so a " +
                "non-1 reading here is a second writer of Time.timeScale - find it, do not rely on this.");
            return true;
        }

        /// <summary>
        /// ⚠ <see cref="Time.timeScale"/> IS AN ENGINE GLOBAL AND A SCENE LOAD DOES NOT RESET IT.
        /// A dip that was live when a scene changed has no host left to release it, so the load
        /// itself is the release. Wired once per play session below.
        /// </summary>
        private static void OnSceneLoadedReleaseAll(
            UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single) return;
            ReleaseAllForSceneLoad(scene.name);
        }

        /// <summary>
        /// The scene-load release, by NAME rather than by <c>Scene</c> struct — so the oracle can
        /// drive it without loading a scene. Production reaches it through the sceneLoaded hook.
        /// </summary>
        public static void ReleaseAllForSceneLoad(string sceneName)
        {
            if (s_holds.Count > 0)
            {
                FlowTrace.Warn("Pause",
                    $"WorldHold scene-load release: scene '{sceneName}' loaded with {s_holds.Count} " +
                    $"hold(s) still outstanding [{Describe()}]. Time.timeScale is an ENGINE GLOBAL and " +
                    "SceneManager.LoadScene does NOT reset it, so the hosts that took these holds are " +
                    "gone and nothing else would ever release them. Dropping all of them.");
                s_holds.Clear();
            }

            // The baseline cannot survive a scene change either: whatever cosmetic beat it captured
            // belonged to the scene that just went away.
            s_scaleBeforeHold = 1f;
            s_capturedAtUnscaled = 0f;
            s_lastDriftSeenUnscaled = 0f;

            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                float carried = Time.timeScale;
                Time.timeScale = 1f;
                FlowTrace.Warn("Pause",
                    $"WorldHold scene-load release: timeScale carried {carried:F2} " +
                    $"({carried * 100f:F0}% speed) across the load into '{sceneName}'. Restored 1.00. " +
                    "A new scene always starts at full speed.");
            }
            else
            {
                FlowTrace.Step("Pause",
                    $"WorldHold scene-load release: '{sceneName}' starts with 0 holds at timeScale 1.00.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WireSceneLoadRelease()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedReleaseAll;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedReleaseAll;

            // ⛔ ARM THE DRIFT WATCHDOG AT BOOT, NOT ON THE FIRST HOLD. The state this ticket was
            // minted from had ZERO live holds - a leaked 0.28 with nothing outstanding - so a
            // watchdog installed lazily by Acquire() would not have been running at the moment it
            // was needed. The one net that catches an UNCONVERTED writer must not depend on a
            // converted one having run first.
            EnsureWatchdog();
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
            s_capturedAtUnscaled = 0f;
            s_frozen = false;
            s_watchdog = null;
            s_applicationBackgrounded = false;
            s_backgroundedAtUnscaled = 0f;
            s_lastDriftSeenUnscaled = 0f;
            s_lastReleasedReason = "none";
            s_lastReleasedAtUnscaled = 0f;
        }
    }
}
