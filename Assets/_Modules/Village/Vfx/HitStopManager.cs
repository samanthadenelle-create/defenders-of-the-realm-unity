// =============================================================================
// HitStopManager — tiered hit stop, screen shake, and camera kick. DEF-VFX-02.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   • Hit Stop  — briefly slows Time.timeScale to 0 or near-0 on heavy impacts,
//                 then snaps back. Creates the "weight" feeling on big hits.
//   • Screen Shake — delegates to ThirdPersonCameraFollow.Shake(). Tiered so
//                    small hits barely shake and boss slams rock the screen.
//   • Camera Kick — a brief angular kick (roll + pitch) then spring-back.
//
// USAGE (call from anywhere — static helpers require no Instance null-check):
//
//   HitStopManager.DoImpact(HitTier.Light);       // arrow hit, glancing blow
//   HitStopManager.DoImpact(HitTier.Heavy);       // knight slam, brute punch
//   HitStopManager.DoImpact(HitTier.Boss);        // boss special, wave boss hit
//   HitStopManager.DoImpact(HitTier.Lethal);      // killing blow on a big enemy
//
//   // Or call each system individually:
//   HitStopManager.Instance.HitStop(0.05f, 0.08f);         // duration, timeScale
//   HitStopManager.Instance.Shake(0.12f, 0.3f);            // intensity, duration
//   HitStopManager.Instance.CameraKick(2f, 1.5f, 0.18f);   // roll, pitch, duration
//
// PERFORMANCE: Time.timeScale manipulation is a game-wide operation. It is safe
// to call on mobile; the freeze lasts < 100ms. Never overlaps — a new hit stop
// while one is active extends or replaces it (keeps whichever is longer).
//
// ⛔ WHO OWNS THE CLOCK (2026-09-02 — read before touching any restore path).
// Time.timeScale is an ENGINE GLOBAL, so the record of whether this class currently
// owns it is CLASS state (s_frozenScaleApplied / s_hitStopEndTime), not per-host
// state, and EVERY exit funnels through the single ownership check ReleaseOurClock:
//   * clock still reads our value  -> restore 1.00
//   * clock already reads 1.00     -> nothing to do
//   * clock reads someone ELSE's   -> release WITHOUT stamping, and SAY SO
// The third case is the whole fix. It always refused to stamp (correct — stamping
// would make this an Nth owner of the global) and it always did so in SILENCE
// (catastrophic — this sweep is the project's only every-frame UNSCALED observer of
// the world clock, so it is the one witness present when a FOREIGN owner leaks, and
// it was built to look away). The owner's "in town everything slowed to .1" is that
// case: 0.1 is a value this class never writes.
//
// THE EFFECT IS NOT THE BUG (owner ruling 2026-09-02). Deleting the slow-motion is
// the wrong fix; the leak is. Nothing here weakens or gates the stop itself.
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;      // WO-1353: WorldHold is the ONE writer of Time.timeScale

namespace DeNelle.Village
{
    // ── HitTier ───────────────────────────────────────────────────────────────

    /// <summary>Preset tiers for impact feedback. Pass to HitStopManager.DoImpact().</summary>
    public enum HitTier
    {
        /// <summary>Arrow hit, glancing blow, enemy poke — very subtle shake only.</summary>
        Light,
        /// <summary>Knight melee slam, tower bolt, standard enemy hit — brief stop + shake.</summary>
        Medium,
        /// <summary>Hero W/R abilities, large melee — longer stop, visible shake.</summary>
        Heavy,
        /// <summary>Boss special attack, wave-boss impact — strong stop + full camera kick.</summary>
        Boss,
        /// <summary>Killing blow on a mid-boss or brute — maximum impact, freeze + shake + kick.</summary>
        Lethal,
    }

    // ── HitStopManager ────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton manager for hit stop (timeScale pause), screen shake, and
    /// camera kick. Integrates with ThirdPersonCameraFollow.Shake().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitStopManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static HitStopManager Instance { get; private set; }

        // DEF-178: self-bootstrap. Previously NOTHING added this manager to any
        // scene (no builder, no editor setup, no bootstrap), so Instance was always
        // null and EVERY HitStopManager.DoImpact(...) call site — TowerCombat shots,
        // HeroHealth death/hit beats, LevelUpVFXController, EnvironmentVFX — was a
        // silent no-op. The tiered impact stack was wired but DEAD. Mirroring
        // CombatFeedbackManager / VfxPool / ProjectilePool, bring it up on load so
        // those existing calls actually fire. AfterSceneLoad so Camera.main exists.
        /// <summary>
        /// Reset the CLASS-LEVEL clock ownership record and drop the host-independent watchdog
        /// subscription on every play-mode entry. Statics survive a play-mode restart when domain
        /// reload is disabled, so without this a stop that was in flight when the editor left play
        /// mode would come back as a phantom deadline against a clock nobody had touched.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticClockState()
        {
            Application.onBeforeRender -= HostIndependentWatchdog;
            s_frozenScaleApplied = -1f;
            s_hitStopEndTime     = 0f;
            // WO-1353: the world-clock hold is class state too. A stale handle carried across a
            // play-mode restart would make ApplyOurClock re-point a hold WorldHold no longer knows,
            // so the next stop would silently never reach the clock.
            s_hold = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // THE HOST-INDEPENDENT WATCHDOG (2026-09-02). Every restore path this class had before
            // today was keyed to the HOST: the coroutine, LateUpdate, OnDisable, OnDestroy. All four
            // stop dead the moment the host is deactivated or destroyed, and a stall throws nothing,
            // so there was no try/finally that could have covered it. Application.onBeforeRender is
            // a plain static per-frame event that keeps firing regardless of any MonoBehaviour's
            // enabled state, so the deadline sweep now has a driver that cannot be switched off with
            // the object that armed the stop. LateUpdate still drives the same sweep, because a
            // headless batchmode run renders nothing and therefore never raises onBeforeRender —
            // the two together cover both a live device and the regression fleet.
            // Idempotent: -= before += so a second play-mode entry cannot double-subscribe.
            Application.onBeforeRender -= HostIndependentWatchdog;
            Application.onBeforeRender += HostIndependentWatchdog;

            if (Instance != null) return;
            new GameObject("[HitStopManager]").AddComponent<HitStopManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A duplicate host. It must NOT register (the ladder is keyed by NAME) and, above
                // all, its teardown must not UNregister the live instance's unwind — see OnDestroy.
                FlowTrace.Warn("HitStop",
                    "duplicate HitStopManager destroyed in Awake - the live singleton keeps the " +
                    "battle-end unwind. If this fires repeatedly, something is spawning a second host.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // WO-1233 — THE BATTLE-END UNWIND. See EndStopNow for the captured defect.
            // Registered by OWNER NAME, so a re-created singleton replaces rather than stacks.
            DeNelle.Core.Combat.BattleSessionEnd.RegisterUnwind("hit-stop", EndStopNow);
        }

        private void OnDestroy()
        {
            // ⛔ ONLY THE LIVE SINGLETON MAY DETACH THE LADDER (2026-09-02).
            // BattleSessionEnd keys unwinds by NAME, so an unconditional
            // UnregisterUnwind("hit-stop") from a DUPLICATE's teardown (Awake destroys duplicates,
            // and their OnDestroy runs at the end of that frame) silently removes the LIVE
            // instance's unwind. After that, BattleSessionEnd.Release runs ZERO clock unwinds and
            // the sanctioned recovery ladder is gone — while every source lint in
            // BattleQuiescenceRegression still reads the RegisterUnwind call and reports it wired.
            // The `Instance == this` guard on the old Instance-null line existed for exactly this
            // hazard; the unregister was the one duplicate-teardown line never given it.
            if (Instance != this) return;

            DeNelle.Core.Combat.BattleSessionEnd.UnregisterUnwind("hit-stop");

            // Guarantee timeScale is restored if this object is torn down mid-stop
            // (scene unload / domain reload) so a freeze can never leak past us. Routed through
            // the ONE ownership check so a teardown can never stamp over another owner's slow-mo.
            if (_hitStopRoutine != null || s_frozenScaleApplied >= 0f)
                ReleaseOurClock("hit-stop host DESTROYED mid-stop");

            _hitStopRoutine = null;
            Instance = null;
        }

        // =====================================================================
        //  BATTLE-END UNWIND (WO-1233) — a cosmetic stop must never outlive the fight
        // =====================================================================

        /// <summary>
        /// End any in-flight hit stop RIGHT NOW because the battle session that produced it is over.
        ///
        /// <para>CAPTURED DEFECT (2026-08-26, Seeker 2026.08.26.342290). Two of nine
        /// BATTLE_QUIESCENCE_FAIL events read <c>timeScale: the world clock is 0.04 (4% speed)</c> —
        /// this class's own <see cref="HitTier.Medium"/> value, and the exact 2026-08-20 signature
        /// the gate's own text names. The 2026-08-20 fix added THREE unwind paths (the routine, the
        /// LateUpdate deadline, OnDisable/OnDestroy) and it came back anyway, which says the missing
        /// path was never one of those: all three are keyed to THIS OBJECT's lifetime, and none of
        /// them is keyed to the BATTLE's. The killing blow fires a stop on the very frame the battle
        /// resolves — the frame after which the victory screen, the masked warp home and a stack of
        /// teardown work all run — so the whole leak window sits between the stop and its deadline,
        /// on the one path that had no unwind at all.</para>
        ///
        /// <para>This closes the window instead of racing it: the moment the session ends, the stop
        /// ends. It only ever reverts a scale THIS class applied (<see cref="s_frozenScaleApplied"/>),
        /// so an ArenaDeathCam or WaveCelebration slow-mo is never stamped over — the same
        /// discipline as the deadline watchdog, and the reason this is safe to run unconditionally
        /// on every battle end.</para>
        ///
        /// <para>⚠ Deliberately a SEPARATE fix from the battle-lock release, with a separate owner.
        /// They arrive at the same moment and share the announcement, but the clock is this class's
        /// state and nothing else's — merging them would have produced one change with two owners,
        /// which is the shape of the defect, not the fix.</para>
        /// </summary>
        /// <param name="context">The session context ("arena win" / "retreat" / "abandoned: …").</param>
        public void EndStopNow(string context)
        {
            if (_hitStopRoutine != null) { StopCoroutine(_hitStopRoutine); _hitStopRoutine = null; }

            if (s_frozenScaleApplied < 0f)
            {
                // No stop of ours in flight — nothing to unwind. SAID OUT LOUD rather than returned
                // in silence: when the town is left slow and this line reads "clock 0.10", the very
                // next question ("then who owns 0.10?") is answered by elimination instead of by a
                // second capture.
                FlowTrace.Step("HitStop",
                    $"battle end ({context}): no hit stop of ours in flight, nothing to unwind. " +
                    $"Clock reads {Time.timeScale:F2}.");
                return;
            }

            ReleaseOurClock($"ENDED by battle end ({context}) - the unwind the 2026-08-20 fix lacked, " +
                            "because its three paths were keyed to this object's lifetime, not the battle's");
        }

        // =====================================================================
        //  THE ONE OWNERSHIP CHECK (2026-09-02)
        // =====================================================================

        /// <summary>
        /// Give up ownership of the world clock, restoring 1.00 ONLY if the clock still reads the
        /// value THIS class wrote.
        ///
        /// <para>CAPTURED DEFECT (owner, 2026-09-02): <i>"in town everything slowed to .1"</i>, on
        /// top of the 2026-08-26 <c>BATTLE_QUIESCENCE_FAIL (arena win)</c> naming
        /// <c>timeScale: the world clock is 0.04</c>. 0.04 is ours; <b>0.1 is not</b>. Read
        /// <c>Enemy.cs</c> around line 3093: on a hero kill <c>CombatFeedbackManager.Kill()</c>
        /// starts a 0.45 s kill slow-mo, and TWELVE LINES LATER
        /// <c>HitStopManager.Instance?.HitStop(0.05f, 0.04f)</c> stamps 0.04 over it on the same
        /// frame. Two owners, one global, one frame.</para>
        ///
        /// <para>THE LEAK IS THE SILENCE, NOT THE STAMP. Every exit this class had checked
        /// <c>|Time.timeScale - ours| &lt; 0.001</c> and, when it did not match, took the
        /// "superseded — say nothing" branch: it forgot it had ever touched the clock, logged
        /// NOTHING, and disarmed its own watchdog for the rest of the session. That branch is
        /// correct not to stamp (that is how this class would become an Nth owner) and catastrophic
        /// to run mute — this class is the only thing in the project sweeping the world clock every
        /// frame on the UNSCALED tick, so it is the one witness that SEES a foreign leak, and it was
        /// built to look away. Hence CLAUDE.md sec. 12: it now names the value it walked away from,
        /// so the next capture names the owner instead of starting from zero.</para>
        /// </summary>
        /// <param name="why">Why we are letting go — quoted verbatim into the trace line.</param>
        private static void ReleaseOurClock(string why)
        {
            var hold = s_hold;
            s_hold = null;

            if (s_frozenScaleApplied < 0f)
            {
                // A stray hold with no recorded stop should be impossible; dispose it anyway rather
                // than leave the world slow on a state we cannot explain.
                if (hold != null && hold.IsHeld)
                {
                    hold.Dispose();
                    FlowTrace.Warn("HitStop",
                        $"hit stop released - {why} - with NO stop recorded but a live world hold " +
                        "outstanding. Disposed it; the two records had drifted apart.");
                }
                return;
            }

            float ours = s_frozenScaleApplied;
            s_frozenScaleApplied = -1f;
            s_hitStopEndTime     = 0f;
            hold?.Dispose();

            FlowTrace.Step("HitStop",
                $"hit stop ({ours:F2}) released - {why}. World holds now [{WorldHold.Describe()}], " +
                $"timeScale {Time.timeScale:F2}.");
        }

        /// <summary>
        /// Apply a stop and RECORD it as ours in the same breath, so the record of who owns the
        /// global can never drift from the write that created it.
        ///
        /// <para>⛔ WO-1353 — THIS NO LONGER WRITES <c>Time.timeScale</c>. It takes a HOLD from
        /// WorldHold, the one owner, at the requested scale. The three-case "is the clock still
        /// mine" dance this class used to perform on release is GONE because it cannot arise: with
        /// one owner and slowest-wins composition there is no foreign value to decline to stamp
        /// over. That dance was individually correct and collectively the defect — see the WO-1353
        /// block in WorldHold.cs. The stop's SCALE and DURATION are untouched.</para>
        /// </summary>
        private static void ApplyOurClock(float scale)
        {
            if (s_hold != null && s_hold.IsHeld) WorldHold.SetScale(s_hold, scale);
            else s_hold = WorldHold.AcquireScale(HoldReason, scale, HoldMaxSeconds);
            s_frozenScaleApplied = scale;
        }

        /// <summary>Reason token this class's holds carry. Read it straight out of a capture.</summary>
        private const string HoldReason = WorldHold.ReasonCosmeticPrefix + "hit-stop";

        /// <summary>Watchdog ceiling for one stop. A hit stop is milliseconds (the longest this
        /// class arms is well under 0.2 s), so two seconds is four times any legitimate stop and
        /// still far below the point a player would call the world "slow".</summary>
        private const float HoldMaxSeconds = 2f;

        /// <summary>The live world-clock hold, or null. STATIC for the same reason
        /// <see cref="s_frozenScaleApplied"/> is: the clock is an engine global.</summary>
        private static WorldHold.Handle s_hold;

        /// <summary>How close the observed clock must be to our recorded value to still count as ours.</summary>
        private const float ScaleMatchEpsilon = 0.001f;

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Hit Stop Settings")]
        [Tooltip("Minimum TimeScale during a stop (0 = full freeze, 0.05 = near-stop).")]
        [SerializeField, Range(0f, 0.3f)] private float _hitStopMinScale = 0.02f;

        [Header("Camera Shake")]
        [Tooltip("A cached ref to the in-scene ThirdPersonCameraFollow. Auto-resolved if null.")]
        [SerializeField] private ThirdPersonCameraFollow _camera;

        [Header("Quality Gate")]
        [Tooltip("Minimum VFXQuality required for hit stop to fire (Heavy impacts on Low = skip stop, keep shake).")]
        [SerializeField] private VFXQuality _hitStopMinQuality = VFXQuality.Medium;

        // ── Runtime state ─────────────────────────────────────────────────────

        private Coroutine _hitStopRoutine;

        /// <summary>
        /// Unscaled deadline the ACTIVE stop must end by. <b>STATIC</b> — see
        /// <see cref="s_frozenScaleApplied"/>.
        /// </summary>
        private static float s_hitStopEndTime;

        /// <summary>
        /// The scale the ACTIVE stop applied, or -1 when no stop is in flight. Read by the deadline
        /// sweep so it only ever un-does OUR freeze and never stamps over a legitimate slow-mo owned
        /// by someone else.
        ///
        /// <para><b>STATIC SINCE 2026-09-02, AND THAT IS THE POINT.</b> <c>Time.timeScale</c> is an
        /// ENGINE GLOBAL, so "who owns it" is a CLASS-level fact; storing it per-instance was a
        /// mismatch with real consequences. If the host that armed a stop was destroyed or replaced
        /// (a scene teardown, the Awake duplicate guard, a re-bootstrapped singleton), the fresh
        /// instance came up with -1 and could not recover — or even SEE — a freeze the previous host
        /// had left pinned on the global. Class state means any host, present or future, plus the
        /// host-independent <see cref="HostIndependentWatchdog"/>, can finish a stop that outlived
        /// the object that started it.</para>
        /// </summary>
        private static float s_frozenScaleApplied = -1f;

        // ── Static convenience ────────────────────────────────────────────────

        /// <summary>
        /// Fire all three feedback systems (hit stop + shake + kick) based on a
        /// preset HitTier. The most common call site.
        /// </summary>
        public static void DoImpact(HitTier tier)
            => Instance?.TriggerTier(tier);

        /// <summary>Screen shake only — useful for environmental rumbles.</summary>
        public static void DoShake(float intensity, float duration)
            => Instance?.Shake(intensity, duration);

        // ── Public instance methods ───────────────────────────────────────────

        /// <summary>
        /// Trigger a preset tier — hit stop + shake + camera kick all at once.
        /// Individual tuning is inside the switch below.
        /// </summary>
        public void TriggerTier(HitTier tier)
        {
            switch (tier)
            {
                case HitTier.Light:
                    // Subtle — shake only, no stop.
                    Shake(0.04f, 0.15f);
                    break;

                case HitTier.Medium:
                    HitStop(0.06f, 0.04f);
                    Shake(0.08f, 0.25f);
                    break;

                case HitTier.Heavy:
                    HitStop(0.09f, 0.02f);
                    Shake(0.14f, 0.35f);
                    CameraKick(1.5f, 1.0f, 0.15f);
                    break;

                case HitTier.Boss:
                    HitStop(0.12f, 0.0f);
                    Shake(0.22f, 0.50f);
                    CameraKick(3.0f, 2.0f, 0.22f);
                    break;

                case HitTier.Lethal:
                    HitStop(0.18f, 0.0f);
                    Shake(0.30f, 0.60f);
                    CameraKick(4.0f, 3.0f, 0.28f);
                    break;
            }
        }

        /// <summary>
        /// Freeze (or near-freeze) Time.timeScale for <paramref name="duration"/> seconds,
        /// then snap back to 1. Safe to call while another stop is active — takes the
        /// longer/stronger of the two.
        /// </summary>
        /// <param name="duration">Seconds to hold the frozen timeScale.</param>
        /// <param name="frozenScale">TimeScale during the stop (0 = full freeze).</param>
        public void HitStop(float duration, float frozenScale = 0f)
        {
            // Quality gate — skip on Low quality.
            if (VFXManager.Instance != null &&
                (int)VFXManager.Instance.CurrentQuality < (int)_hitStopMinQuality)
                return;

            // Settle any EXPIRED stop of ours before arming a new one. Without this a stop whose
            // restore died silently stays recorded as in-flight, and the new stop overwrites the
            // record — burying the leak instead of reporting it.
            SweepDeadline();

            float endTime = Time.unscaledTime + duration;

            // If an existing stop would end later, leave it running.
            if (_hitStopRoutine != null && endTime <= s_hitStopEndTime) return;

            float scale = Mathf.Max(_hitStopMinScale, frozenScale);

            // INSTRUMENTATION ONLY (CLAUDE.md sec. 12) — the two-owner collision, recorded at the
            // instant it happens. Enemy.cs fires CombatFeedbackManager.Kill() (a 0.45 s kill
            // slow-mo) and then this stop TWELVE LINES LATER on the same frame, so we routinely
            // take a clock another owner is mid-way through owning. That is allowed and the effect
            // is deliberate; what is not allowed is it happening unrecorded, because when one of
            // the two restores later dies the log has to be able to say which owner wrote last.
            // Throttled: on a busy kill frame this would otherwise be per-kill spam.
            float before = Time.timeScale;
            if (s_frozenScaleApplied < 0f && Mathf.Abs(before - 1f) > ScaleMatchEpsilon)
                FlowTrace.Throttle("HitStop", "stamp-over-foreign-scale", 5f,
                    $"hit stop {scale:F2} applied over a clock ALREADY at {before:F2} that this class " +
                    "does not own (CombatFeedbackManager's kill slow-mo is the usual one). We now own " +
                    "the clock and restore 1.00 when our shorter stop ends; the other owner's restore " +
                    "becomes a no-op. Logged because this collision is what makes a later leak " +
                    "un-attributable.");

            s_hitStopEndTime = endTime;
            if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = isActiveAndEnabled ? StartCoroutine(HitStopRoutine(scale, duration)) : null;

            if (_hitStopRoutine == null)
            {
                // No coroutine host available (StartCoroutine is illegal on an inactive object and
                // would throw). THE EFFECT STILL FIRES — owner ruling 2026-09-02: deleting the
                // slow-motion is the wrong fix, the leak is the bug — because the restore no longer
                // depends on this host at all. The deadline is class state and
                // HostIndependentWatchdog sweeps it every frame regardless of who is enabled.
                ApplyOurClock(scale);
                FlowTrace.Warn("HitStop",
                    $"hit stop {scale:F2} armed with NO coroutine host (the manager object is " +
                    "inactive). The stop still fires; its restore is owned by the deadline watchdog, " +
                    $"due at unscaled {s_hitStopEndTime:F2}.");
            }
        }

        // =====================================================================
        //  DEADLINE WATCHDOG (owner report 2026-08-20: town frozen after a fight)
        // =====================================================================

        /// <summary>
        /// Restore <c>Time.timeScale</c> if our stop outlived its deadline.
        ///
        /// <para>CAPTURED DEFECT. After an arena fight in the hub the owner was left unable to move.
        /// The trace named it exactly: <c>[Flow:HeroOwner] ... inputSuppressed=False timeScale=0.04
        /// dt=0.0013</c> — input was never blocked, the world was running at 4% speed, and it stayed
        /// there for 182 consecutive samples over three minutes. 0.04 is this class's own value
        /// (<c>HitTier.Medium</c>, and <c>Enemy.cs</c>'s death stop), applied at the instant the
        /// battle resolved and the victory modal opened.</para>
        ///
        /// <para>WHY A COROUTINE CANNOT BE THE ONLY RESTORE PATH. <see cref="HitStopRoutine"/>
        /// writes a GLOBAL and hands the only restore to a coroutine — and a coroutine stops
        /// silently whenever its GameObject is deactivated, taking the restore with it and leaving
        /// the global stuck. <c>OnDestroy</c> does not fire for a deactivation, so the existing
        /// guard there cannot cover it. A frozen world is not a survivable outcome for a mechanism
        /// this cosmetic, so the restore is now owned by a deadline that runs every frame on the
        /// UNSCALED clock — it cannot be starved by the very slow-down it exists to undo.</para>
        ///
        /// <para>It only reverts a scale THIS class applied (see <see cref="s_frozenScaleApplied"/>),
        /// so an ArenaDeathCam or WaveCelebration slow-mo passes through untouched.</para>
        ///
        /// <para>And it FAILS LOUDLY rather than healing quietly: the exact leak this fixes went
        /// unexplained precisely because nothing announced it. If this fires, the next capture names
        /// the cause instead of starting from zero (CLAUDE.md sec. 12).</para>
        /// </summary>
        private void LateUpdate() => SweepDeadline();

        /// <summary>
        /// The SAME sweep, driven by <c>Application.onBeforeRender</c> — a static per-frame event
        /// that keeps firing no matter which MonoBehaviours are enabled. This is the path that
        /// closes the 2026-09-02 hole: a stall throws nothing, so a try/finally can never cover it,
        /// and every other restore this class had died with its host.
        /// </summary>
        private static void HostIndependentWatchdog()
            => Guard.Try("HitStop", "host-independent deadline sweep", SweepDeadline);

        /// <summary>
        /// Restore <c>Time.timeScale</c> if OUR stop outlived its deadline, and — new on
        /// 2026-09-02 — SAY SO when the clock has been taken over by someone else instead of
        /// walking away in silence. Idempotent and cheap: it returns on the first line whenever no
        /// stop of ours is in flight, so both drivers can call it every frame.
        /// </summary>
        private static void SweepDeadline()
        {
            if (s_frozenScaleApplied < 0f) return;
            if (Time.unscaledTime <= s_hitStopEndTime + DeadlineGraceSeconds) return;

            float ours    = s_frozenScaleApplied;
            float overdue = Time.unscaledTime - s_hitStopEndTime;

            var host = Instance;
            if (host != null && host._hitStopRoutine != null)
            {
                host.StopCoroutine(host._hitStopRoutine);
                host._hitStopRoutine = null;
            }

            // ⛔ WO-1353 — THE THREE-CASE DANCE IS GONE, AND THAT IS THE FIX.
            // This sweep used to compare the engine global against our record and take one of three
            // branches: restore 1.00 if it still read our value, stay quiet if somebody had already
            // restored it, or release WITHOUT stamping when it read a FOREIGN value. Every branch was
            // individually right and the set of them was the defect: when two dips overlapped, both
            // owners correctly declined to restore and the residue stayed on the global with nobody
            // left holding it. That is the 0.28 the owner measured in open town on 2026-09-03.
            // There is now ONE owner and ONE hold, so releasing is unconditional and cannot stamp
            // over anybody: WorldHold recomputes the effective scale from whatever holds remain.
            ReleaseOurClock($"deadline sweep - the stop is {overdue:F2}s past its deadline and its " +
                            "restore never ran (the host was almost certainly deactivated or destroyed, " +
                            "which kills a coroutine without firing OnDestroy and without throwing)");

            FlowTrace.Fail("HitStop",
                $"HIT-STOP LEAK RECOVERED: our {ours:F2} stop ran {overdue:F2}s past its deadline and " +
                "its restore never completed. The world-clock hold has been released; live holds now " +
                $"[{WorldHold.Describe()}], timeScale {Time.timeScale:F2}. A stop that reaches this " +
                "line is a real defect in this class's own lifecycle - the recovery is not the fix.");
        }

        /// <summary>How far past the deadline to wait before calling it a leak. Generous enough that
        /// a frame hitch or a one-frame ordering race is never mistaken for one.</summary>
        private const float DeadlineGraceSeconds = 0.25f;

        private void OnDisable()
        {
            // A coroutine dies on deactivation and OnDestroy does NOT fire for it, so without this
            // a mid-stop SetActive(false) leaves the global pinned. This is the cheap half of the
            // same fix as the watchdog above; both exist because either alone can be out-raced.
            // Routed through the ONE ownership check, which also announces the superseded case that
            // this method used to drop silently.
            if (Instance != this) return;
            if (_hitStopRoutine != null || s_frozenScaleApplied >= 0f)
                ReleaseOurClock("hit-stop host DISABLED mid-stop; the deactivation has just killed " +
                                "the restore coroutine and OnDestroy will not fire");
            _hitStopRoutine = null;
        }

        private IEnumerator HitStopRoutine(float frozenScale, float duration)
        {
            ApplyOurClock(frozenScale);
            yield return new WaitForSecondsRealtime(duration);
            _hitStopRoutine = null;

            // DEF-178: restore to the game's normal 1f, NOT a captured `prev` — capturing `prev`
            // risked pinning a frozen value when CombatFeedbackManager's own stop overlapped this
            // one on the same frame.
            //
            // AMENDED 2026-09-02: that restore was UNCONDITIONAL, so a stop ending while another
            // owner held the clock stamped 1.00 over a live slow-mo — the exact "Nth owner" move
            // the deadline sweep is careful never to make, performed on the normal path. It now
            // goes through the one ownership check, which restores 1.00 when the clock is still
            // ours (the overwhelmingly common case, so the felt behaviour is unchanged) and
            // otherwise leaves the current owner alone and says so.
            ReleaseOurClock("stop duration elapsed");
        }

        /// <summary>
        /// Shake the camera. Delegates to ThirdPersonCameraFollow.Shake().
        /// If no camera component is found, the call is silently ignored.
        /// <para>
        /// HONOURS THE PLAYER PREFERENCE (2026-08-16). This path holds a TYPED, cached camera
        /// reference rather than going through CameraShakeBridge's reflection resolve, so it is
        /// deliberately left as a direct call — but it gates on CameraShakeBridge.Enabled, the
        /// same "camerashake" preference the bridge reads, so the accessibility toggle covers
        /// this seam too. Before this guard, every HitStopManager tier and EnvironmentVFX's
        /// environmental shake ignored the setting entirely.
        /// </para>
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (!CameraShakeBridge.Enabled) return;
            EnsureCamera();
            _camera?.Shake(intensity, duration);
        }

        /// <summary>
        /// Apply a brief roll + pitch kick to the camera, then spring back smoothly.
        /// This adds an angular jolt that feels more dramatic than positional shake alone.
        /// Gated on the same "camerashake" preference (2026-08-16): it is angular rather than
        /// positional, but to the player it is the same involuntary camera motion the comfort
        /// toggle exists to suppress, so opting out of shake must opt out of this too.
        /// </summary>
        public void CameraKick(float rollDeg, float pitchDeg, float duration)
        {
            if (!CameraShakeBridge.Enabled) return;
            EnsureCamera();
            if (_camera == null) return;
            StartCoroutine(CameraKickRoutine(rollDeg, pitchDeg, duration));
        }

        private IEnumerator CameraKickRoutine(float roll, float pitch, float duration)
        {
            if (_camera == null) yield break;
            var camTransform = _camera.transform;
            Quaternion original = camTransform.localRotation;

            // Apply kick (local Euler offset).
            camTransform.localRotation = original *
                Quaternion.Euler(-pitch, 0f, roll);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                // Smooth spring back using SmoothStep.
                camTransform.localRotation = Quaternion.Slerp(
                    camTransform.localRotation, original, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            camTransform.localRotation = original;
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private void EnsureCamera()
        {
            if (_camera != null) return;
            _camera = FindAnyObjectByType<ThirdPersonCameraFollow>();
        }
    }
}
