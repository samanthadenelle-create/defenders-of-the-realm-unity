// =============================================================================
// HeroHitReaction — screen-edge damage flash + death slow-mo. Combat feel.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   • Damage flash — a red full-screen tint spikes and fades whenever the hero's
//     HP drops, giving the "I got hit" read that HP-number changes alone don't.
//   • Death slow-mo — on death, time briefly ramps to a crawl and recovers, for a
//     dramatic beat. One-shot.
//
// ⛔ Time.timeScale — AMENDED 2026-09-02. The line above used to end "always restores
// Time.timeScale to 1", and "always" was never true. DeathSlowMo is a plain coroutine on
// the HERO — the one object in the scene most likely to be deactivated, warped, respawned
// or destroyed in the seconds after a death, which is exactly the window this dip occupies.
// Unity DROPS a coroutine when its host is deactivated or destroyed: no exception, so not
// even a try/finally could have covered it (and this one did not have one), and the 0.30
// scale it had already written would stay on the engine global forever.
//
// WaveCelebrationManager was caught doing precisely that on 2026-09-02 (owner F8 seq 4656:
// `WorldHold ACQUIRE 'pause-menu' -> timeScale 0 (captured 0.28)` — the clock was already
// at 28% speed before the menu opened; input was never suppressed, the world was simply
// running at a quarter speed). This dip had the identical shape and had not been hit yet.
// It is contained the same way, on the same pattern as HitStopManager (fixed 2026-09-02):
//   * ownership of the engine-global clock is CLASS state (s_ourScale), never per-host —
//     a per-instance field dies with the hero, which is the object whose death strands the
//     clock — and every exit funnels through ONE check (ReleaseOurClock) that restores 1.00
//     only while the clock still reads OUR value, otherwise releases WITHOUT stamping and
//     SAYS SO;
//   * an UNSCALED deadline sweep driven by BOTH LateUpdate and Application.onBeforeRender;
//   * registration with the EXISTING BattleSessionEnd unwind ladder.
//
// ⛔ THE EFFECT IS NOT THE BUG, THE LEAK IS (owner ruling 2026-09-02). Nothing below
// shortens, weakens, gates or disables the death slow-mo.
//
// DESIGN (deliberately self-contained + low-risk):
//   • Driven off HeroHealth's real C# events (OnHealthChanged / OnDied) — NOT the
//     greenfield WO's UnityEvents, which this branch's HeroHealth doesn't use.
//   • Rendered with IMGUI (OnGUI), NOT a URP post-process Vignette: it needs no
//     scene Volume/profile and always renders in player builds (UI-Toolkit HUDs
//     have repeatedly come up empty here — see HeroHealth's own IMGUI bar).
//   • Added to the hero automatically by HeroHealthBootstrap alongside HeroHealth,
//     so it needs no prefab wiring.
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Combat;   // WO-285: ActorAnimator hit-reaction driver
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;       // WO-1353: WorldHold is the ONE writer of Time.timeScale

namespace DeNelle.Village
{
    /// <summary>Red screen flash on hero damage and a slow-motion beat on death.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroHitReaction : MonoBehaviour
    {
        [Header("Damage flash")]
        [Tooltip("Peak opacity of the red full-screen flash on a hit (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _flashPeak = 0.28f;

        [Tooltip("Seconds for the flash to fade from peak to nothing.")]
        [SerializeField, Min(0.05f)] private float _flashFade = 0.32f;

        [Header("Death slow-mo")]
        [Tooltip("Time.timeScale at the moment of death.")]
        [SerializeField, Range(0.05f, 1f)] private float _deathTimeScale = 0.30f;

        [Tooltip("Real seconds over which time recovers from the death slow-mo.")]
        [SerializeField, Min(0.1f)] private float _deathSlowMoSeconds = 1.2f;

        private HeroHealth _health;
        private float _flashAlpha;
        private float _lastHp = -1f;
        private bool  _diedHandled;

        // FIX 1 (owner "walk became messed up"): under heavy aggro HeroHealth broadcasts an HP tick
        // every frame; without a cooldown each one re-latches the full-body Hit trigger and pins the
        // stagger over locomotion. Debounce the body flinch (the red flash still fires on every hit).
        private float _nextHitAnimTime;
        private const float HitAnimCooldown = 0.5f;

        // WO-285: plays the body hit-reaction clip on damage (the screen flash was the
        // only "I got hit" read before). Resolved on the hero root; guarded internally.
        private ActorAnimator _actor;

        private static readonly Color FlashColor = new Color(0.8f, 0.05f, 0.05f);

        private void OnEnable()
        {
            // Start() / OnEnable run after HeroHealth.Awake, so the instance + its
            // events exist. Resolve on the same GameObject, fall back to the singleton.
            _health = GetComponent<HeroHealth>();
            if (_health == null) _health = HeroHealth.Instance;
            if (_health == null)
            {
                FlowTrace.Warn("HitReact", "OnEnable: no HeroHealth on GO or singleton — hit reaction INERT (no flash / no death slow-mo).");
                return;
            }

            _lastHp = _health.Hp;
            if (!TryGetComponent(out _actor)) _actor = gameObject.AddComponent<ActorAnimator>();
            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnDied         += HandleDied;

            // THE BATTLE-END UNWIND, on the EXISTING ladder. Keyed by OWNER NAME, so a re-created
            // hero replaces rather than stacks. Claimed by the FIRST live reaction only, and
            // released again in OnDisable, so a second hero (respawn overlap, an arena double) can
            // never unregister the live one's unwind out from under it.
            if (s_ladderOwner == null || s_ladderOwner == this)
            {
                s_ladderOwner = this;
                DeNelle.Core.Combat.BattleSessionEnd.RegisterUnwind("hero-death-slowmo", EndSlowMoNow);
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnDied         -= HandleDied;
            }

            // A coroutine dies on deactivation and OnDestroy does NOT fire for it, so without this
            // a hero deactivated mid-ramp (death -> respawn -> warp is exactly this window) leaves
            // the global pinned at 0.30. Routed through the ONE ownership check so a teardown can
            // never stamp over another owner's live slow-mo.
            if (_deathSlowMoRoutine != null || (s_clockOwner == this && s_ourScale >= 0f))
                ReleaseOurClock("hero host DISABLED mid death slow-mo; the deactivation has just " +
                                "killed the ramp coroutine and OnDestroy will not fire");
            _deathSlowMoRoutine = null;

            if (s_ladderOwner == this)
            {
                DeNelle.Core.Combat.BattleSessionEnd.UnregisterUnwind("hero-death-slowmo");
                s_ladderOwner = null;
            }
        }

        // =====================================================================
        //  CLOCK OWNERSHIP (2026-09-02) — see the file header
        // =====================================================================

        /// <summary>
        /// The scale the ACTIVE death slow-mo has applied, or -1 when none is in flight.
        /// <b>STATIC</b>: Time.timeScale is an ENGINE GLOBAL, so the record of who owns it is CLASS
        /// state, not per-hero.
        /// </summary>
        private static float s_ourScale = -1f;

        /// <summary>Unscaled deadline the active ramp must have finished by.</summary>
        private static float s_deathDeadlineUnscaled;

        /// <summary>The instance whose ramp currently owns the clock, so the sweep can stop its
        /// coroutine and a foreign teardown cannot release somebody else's dip.</summary>
        private static HeroHitReaction s_clockOwner;

        /// <summary>The instance that currently holds the named battle-end unwind registration.</summary>
        private static HeroHitReaction s_ladderOwner;

        /// <summary>How close the observed clock must be to our record to still count as ours.</summary>
        private const float ScaleMatchEpsilon = 0.001f;

        /// <summary>Grace past the deadline before a still-applied scale is called a leak.</summary>
        private const float DeadlineGraceSeconds = 0.25f;

        private Coroutine _deathSlowMoRoutine;

        /// <summary>Reason token this class's holds carry — the owner's "death" case, named.</summary>
        private const string HoldReason = WorldHold.ReasonCosmeticPrefix + "death-slowmo";

        /// <summary>Watchdog ceiling for one death ramp. The ramp is 1.2 s, the longest deliberate
        /// beat in the project, so four seconds is more than three times it.</summary>
        private const float HoldMaxSeconds = 4f;

        /// <summary>The live world-clock hold, or null. STATIC for the same reason
        /// <see cref="s_ourScale"/> is: the clock is an engine global, so ownership is class state
        /// and the HERO is the object most likely to be destroyed inside this exact window.</summary>
        private static WorldHold.Handle s_hold;

        /// <summary>
        /// Apply a scale and RECORD it as ours in the same breath, so the record of who owns the
        /// global can never drift from the write that created it.
        ///
        /// <para>⛔ WO-1353 — THIS NO LONGER WRITES <c>Time.timeScale</c>. It takes a hold from
        /// WorldHold, the one owner. The death beat's scale (<c>_deathTimeScale</c>) and length
        /// (<c>_deathSlowMoSeconds</c>) are untouched.</para>
        /// </summary>
        private void ApplyOurClock(float scale)
        {
            if (s_hold != null && s_hold.IsHeld) WorldHold.SetScale(s_hold, scale);
            else s_hold = WorldHold.AcquireScale(HoldReason, scale, HoldMaxSeconds);
            s_ourScale   = scale;
            s_clockOwner = this;
        }

        /// <summary>
        /// Give up ownership of the world clock, restoring 1.00 ONLY if the clock still reads the
        /// value THIS class wrote. Three cases, and the third is the whole point:
        ///   * clock still reads our value -> restore 1.00;
        ///   * clock already reads 1.00    -> somebody restored it first, nothing to do;
        ///   * clock reads someone ELSE's  -> release WITHOUT stamping, and NAME the value.
        /// The old ramp ended with an unconditional `Time.timeScale = 1f`, which performs case
        /// three as a stamp: a death beat finishing while a wave-clear dip or hit stop held the
        /// clock wiped that owner out. Stamping makes this class an Nth writer of the global, which
        /// is the shape of the defect; doing it in silence is how the 0.28 leak went unexplained
        /// (CLAUDE.md §12).
        /// </summary>
        private static void ReleaseOurClock(string why)
        {
            var hold = s_hold;
            s_hold = null;

            if (s_ourScale < 0f)
            {
                s_clockOwner = null;
                if (hold != null && hold.IsHeld)
                {
                    hold.Dispose();
                    FlowTrace.Warn("HitReact",
                        $"death slow-mo released - {why} - with NO beat recorded but a live world hold " +
                        "outstanding. Disposed it; the two records had drifted apart.");
                }
                return;
            }

            float ours = s_ourScale;
            s_ourScale = -1f;
            s_deathDeadlineUnscaled = 0f;
            s_clockOwner = null;
            hold?.Dispose();

            // DeathTrace keeps its own step-out record (TimeScaleRestored) — the owner's "death"
            // case is reported by BOTH the clock owner and the death flow on purpose, because a
            // death that leaves the world slow has to be findable from either trace.
            FlowTrace.Step("HitReact",
                $"death slow-mo ({ours:F2}) released - {why}. World holds now " +
                $"[{WorldHold.Describe()}], timeScale {Time.timeScale:F2}.");
        }

        /// <summary>
        /// End an in-flight death beat RIGHT NOW because the battle session that produced it is
        /// over. On the EXISTING BattleSessionEnd ladder rather than as a second recovery mechanism
        /// — every other restore here is keyed to the HERO's lifetime, not the BATTLE's, and the
        /// hero is the object most likely to be torn down in this exact window.
        /// </summary>
        public void EndSlowMoNow(string context)
        {
            if (_deathSlowMoRoutine != null) { StopCoroutine(_deathSlowMoRoutine); _deathSlowMoRoutine = null; }

            if (s_ourScale < 0f)
            {
                // Said out loud rather than returned in silence: when the town is left slow and
                // this line reads "clock 0.28", the next question ("then who owns 0.28?") is
                // answered by elimination instead of by a second capture.
                FlowTrace.Step("HitReact",
                    $"battle end ({context}): no death slow-mo of ours in flight, nothing to unwind. " +
                    $"Clock reads {Time.timeScale:F2}.");
                return;
            }

            ReleaseOurClock($"ENDED by battle end ({context})");
        }

        /// <summary>
        /// Restore the clock if OUR ramp outlived its deadline, and SAY SO when it has been taken
        /// over by someone else. Idempotent and cheap — it returns on the first line whenever no
        /// beat of ours is in flight, so both drivers can call it every frame.
        /// </summary>
        private static void SweepDeadline()
        {
            if (s_ourScale < 0f) return;
            if (Time.unscaledTime <= s_deathDeadlineUnscaled + DeadlineGraceSeconds) return;

            float ours    = s_ourScale;
            float overdue = Time.unscaledTime - s_deathDeadlineUnscaled;

            var owner = s_clockOwner;
            if (owner != null && owner._deathSlowMoRoutine != null)
            {
                owner.StopCoroutine(owner._deathSlowMoRoutine);
                owner._deathSlowMoRoutine = null;
            }

            // ⛔ WO-1353 — unconditional release; see ReleaseOurClock. The death case is the one the
            // owner named explicitly ("every battle death victory"), so it gets the same structural
            // step-out as every other beat rather than a comparison it can lose.
            ReleaseOurClock($"deadline sweep - the death beat is {overdue:F2}s past its deadline and " +
                            "its ramp never completed (the hero was deactivated, respawned or destroyed)");

            FlowTrace.Fail("HitReact",
                $"DEATH SLOW-MO LEAK RECOVERED: our {ours:F2} beat ran {overdue:F2}s past its deadline " +
                "and its ramp never completed. The world-clock hold has been released; live holds now " +
                $"[{WorldHold.Describe()}], timeScale {Time.timeScale:F2}.");
        }

        /// <summary>LateUpdate driver for the sweep. Covers headless batchmode, which renders
        /// nothing and therefore never raises onBeforeRender.</summary>
        private void LateUpdate() => SweepDeadline();

        /// <summary>
        /// The SAME sweep, driven by <c>Application.onBeforeRender</c> — a static per-frame event
        /// that keeps firing no matter which MonoBehaviours are enabled. This is the driver that
        /// closes the dropped-coroutine hole no try/finally can reach, and it is the one that
        /// matters most here: the hero itself is what gets torn down.
        /// </summary>
        private static void HostIndependentWatchdog()
            => Guard.Try("HitReact", "host-independent death slow-mo sweep", SweepDeadline);

        /// <summary>
        /// Reset the CLASS-LEVEL clock record on every play-mode entry. Statics survive a play-mode
        /// restart when domain reload is disabled, so without this a beat in flight when the editor
        /// left play mode would come back as a phantom deadline against a clock nobody had touched.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticClockState()
        {
            Application.onBeforeRender -= HostIndependentWatchdog;
            s_ourScale = -1f;
            s_deathDeadlineUnscaled = 0f;
            s_clockOwner  = null;
            s_ladderOwner = null;
            s_hold = null;   // WO-1353: the hold handle is class state - never carry one across play mode
        }

        /// <summary>Arm the host-independent driver once per play session. Idempotent (-= before +=)
        /// so a second play-mode entry cannot double-subscribe, and armed here rather than in
        /// OnEnable because it must survive the host it is watching.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ArmHostIndependentWatchdog()
        {
            Application.onBeforeRender -= HostIndependentWatchdog;
            Application.onBeforeRender += HostIndependentWatchdog;
        }

        private void HandleHealthChanged(float current, float max)
        {
            // Flash only on a decrease (ignore heals / the initial broadcast).
            if (_lastHp >= 0f && current < _lastHp)
            {
                if (FlowTrace.Enabled) FlowTrace.Step("HitReact", $"damage flash: hp {_lastHp:0.#}->{current:0.#}/{max:0.#} fatal={(current <= 0f)}");
                _flashAlpha = _flashPeak;
                // WO-285: play the body flinch — but NOT on the killing blow (the death
                // anim owns that beat; HandleDied fires for it). No attacker bearing is
                // available here, so use a generic front/gut flinch.
                // FIX 1: debounce so rapid multi-hits don't re-latch the Hit trigger and pin the stagger.
                if (current > 0f && Time.unscaledTime >= _nextHitAnimTime)
                {
                    _actor?.PlayHit(HitDirection.Gut);
                    _nextHitAnimTime = Time.unscaledTime + HitAnimCooldown;
                }
            }
            _lastHp = current;
        }

        private void HandleDied()
        {
            if (_diedHandled) return;
            _diedHandled = true;
            FlowTrace.Step("HitReact", "hero died — strong flash + death slow-mo beat.");
            _flashAlpha = _flashPeak;            // strong flash on the killing blow
            // Tracked so every teardown path can stop it — an untracked fire-and-forget coroutine
            // is a clock write nobody can cancel.
            if (_deathSlowMoRoutine != null) StopCoroutine(_deathSlowMoRoutine);
            _deathSlowMoRoutine = StartCoroutine(DeathSlowMo());
        }

        private void Update()
        {
            if (_flashAlpha > 0f)
                _flashAlpha = Mathf.Max(0f, _flashAlpha - (_flashPeak / _flashFade) * Time.unscaledDeltaTime);
        }

        /// <summary>
        /// The death slow-mo ramp. THE EFFECT IS UNCHANGED — same scale, same duration, same ramp.
        /// What changed on 2026-09-02 is that every write is RECORDED as ours, the beat carries an
        /// unscaled DEADLINE armed before the first write (so the sweep covers a coroutine dropped
        /// on the very next line), and the final restore goes through the ONE ownership check.
        /// </summary>
        private IEnumerator DeathSlowMo()
        {
            s_deathDeadlineUnscaled = Time.unscaledTime + _deathSlowMoSeconds;
            ApplyOurClock(_deathTimeScale);

            float t = 0f;
            while (t < _deathSlowMoSeconds)
            {
                t += Time.unscaledDeltaTime;

                // ⛔ WO-1353 — the per-frame comparison against the engine global is DELETED. A hit
                // stop or wave-clear dip landing mid-ramp no longer forces this beat to abandon the
                // clock at whatever value the lerp had reached (which is how a residue was stranded);
                // WorldHold composes the overlapping holds slowest-wins and this ramp resumes
                // underneath. Bail only if OUR hold was taken away (watchdog force-release).
                if (s_hold == null || !s_hold.IsHeld)
                {
                    _deathSlowMoRoutine = null;
                    ReleaseOurClock("our world-clock hold was force-released mid death ramp");
                    yield break;
                }

                ApplyOurClock(Mathf.Lerp(_deathTimeScale, 1f, t / _deathSlowMoSeconds));
                yield return null;
            }

            _deathSlowMoRoutine = null;
            ReleaseOurClock("death slow-mo ramp complete");
            // Game-over flow hooks off HeroHealth.OnDied elsewhere (WO46 P11).
        }

        private void OnGUI()
        {
            if (_flashAlpha <= 0f) return;
            var prev = GUI.color;
            GUI.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, _flashAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
