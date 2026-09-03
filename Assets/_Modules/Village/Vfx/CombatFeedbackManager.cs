// =============================================================================
// CombatFeedbackManager (DEF-44 / DEF-45) — hit stop, combo counter, kill streak.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Three interlocking combat-feel systems triggered by a single static API:
//
//   HIT STOP  (DEF-45)
//     On CombatFeedbackManager.Hit() a very brief Time.timeScale dip (default
//     0.05 s at 0.05× speed) punches the impact of every successful strike into
//     the player's hand. WaitForSecondsRealtime keeps the coroutine alive during
//     the pause. Successive hits within the dip restart it — no stacking.
//
//   COMBO COUNTER  (DEF-45)
//     Each Hit() increments ComboCount and resets a decay window (default 2.5 s).
//     If no hit lands before the window expires the count resets to zero.
//     OnComboChanged(int) fires on every change (increment AND reset to zero).
//     Callers: HUD can subscribe and show a "3× COMBO!" badge.
//
//   KILL STREAK  (DEF-45)
//     Each Kill() increments KillStreak and resets a longer decay window
//     (default 8 s). OnKillStreakChanged(int) fires on change. A "DOUBLE KILL!"
//     type UI subscribes here. Resets to zero on window expiry, not on wave end
//     (wave end is a natural pause — let the streak survive it).
//
// FLOATING DAMAGE NUMBERS (DEF-44)
//   DamageNumberSpawner already handles this in Enemy.TakeDamage() — this
//   manager does NOT duplicate that call.
//
// SCREEN SHAKE  (DEF-44)
//   CameraShakeBridge.Shake() is already called in Enemy.Die() for the death
//   burst. This manager adds a lighter per-hit shake for the survival path via
//   CombatFeedbackManager.Hit() so the camera responds to every landing blow.
//
// WIRING:
//   Enemy.cs calls:
//     CombatFeedbackManager.Hit(position, amount)   — survival hit branch
//     CombatFeedbackManager.Kill(position)           — killed == true branch
//
// ARCHITECTURE (non-negotiable):
//   * DontDestroyOnLoad singleton — bootstrapped BeforeSceneLoad so it's ready
//     for the first enemy spawn.
//   * Update() uses Time.unscaledDeltaTime so timers survive the hit-stop dip.
//   * No per-frame Find / allocation. Static caches only.
//   * Time.timeScale is always restored in a finally block so a script exception
//     cannot permanently freeze the game.
//
// ⛔ AMENDED 2026-09-02 — THE finally IS NOT A GUARANTEE. READ BEFORE TOUCHING A RESTORE.
// The bullet above is true and INSUFFICIENT, and the gap is not theoretical: a `finally`
// covers a THROWN exception, and the way these dips actually leak is that Unity DROPS the
// coroutine when its host is deactivated or destroyed. That throws nothing, so the finally
// never runs, and whatever scale it had already written stays on the engine global forever.
//
// WaveCelebrationManager was caught doing exactly that on 2026-09-02 (owner F8 seq 4656:
// `WorldHold ACQUIRE 'pause-menu' -> timeScale 0 (captured 0.28)` — the clock was already
// at 28% speed before the menu opened). This class's two dips had the IDENTICAL shape and
// had simply not been hit yet. They are contained the same way, on the same pattern as
// HitStopManager (fixed 2026-09-02) — deliberately not a second mechanism:
//   * ownership of the engine-global clock is CLASS state (s_ourScale), never per-host,
//     and every exit funnels through ONE check (ReleaseOurClock) that restores 1.00 only
//     while the clock still reads OUR value, and otherwise releases WITHOUT stamping and
//     SAYS SO;
//   * an UNSCALED deadline sweep driven by BOTH LateUpdate and Application.onBeforeRender,
//     so a disabled or destroyed host cannot strand the clock;
//   * registration with the EXISTING BattleSessionEnd unwind ladder.
//
// ⛔ THE EFFECT IS NOT THE BUG, THE LEAK IS (owner ruling 2026-09-02). Nothing below
// shortens, weakens, gates or disables the hit stop or the kill slow-mo.
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;         // WO-1353: WorldHold is the ONE writer of Time.timeScale
using DeNelle.Village.Arena;   // BattleArena — gate home-scene combat feel during a staged battle

namespace DeNelle.Village
{
    /// <summary>
    /// Singleton combat-feel hub: hit stop, combo counter, kill streak.
    /// Call the static <see cref="Hit"/> / <see cref="Kill"/> entry points from
    /// <see cref="Enemy"/>; subscribe to <see cref="OnComboChanged"/> /
    /// <see cref="OnKillStreakChanged"/> from HUD components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackManager : MonoBehaviour
    {
        public static CombatFeedbackManager Instance { get; private set; }

        // ── Hit stop ─────────────────────────────────────────────────────────

        /// <summary>Time scale applied during the hit-stop dip (0–1).</summary>
        [Header("Hit Stop")]
        [SerializeField, Range(0.01f, 0.3f)] private float _hitStopTimescale = 0.05f;

        /// <summary>Real-time seconds the hit-stop dip lasts.</summary>
        [SerializeField, Range(0.02f, 0.2f)] private float _hitStopDurationSeconds = 0.05f;

        /// <summary>
        /// DEF-178 (mobile safety): minimum REAL seconds between hit-stops. RegisterHit
        /// fires on EVERY enemy that takes damage; in a 20-enemy AoE wave (mage R, tower
        /// burst, multi-hit melee) that previously re-froze Time.timeScale every frame —
        /// a continuous stutter that reads as lag and is nauseating on mobile. With this
        /// cap a fresh freeze can only start once per window; rapid extra hits inside the
        /// window still register combo + shake + VFX, they just don't re-trigger the
        /// freeze. 0 = uncapped (legacy behaviour).
        /// </summary>
        [SerializeField, Range(0f, 0.5f)] private float _hitStopMinIntervalSeconds = 0.12f;

        // ── Kill slo-mo (the "death blow" beat) ───────────────────────────────
        /// <summary>Time scale during a kill slo-mo dip — deeper + longer than the hit-stop,
        /// so a finishing blow reads as a dramatic slow-time moment.</summary>
        [Header("Kill Slo-Mo (death blow)")]
        [SerializeField, Range(0.05f, 0.6f)] private float _killSloMoTimescale = 0.3f;
        /// <summary>Real-time seconds the kill slo-mo lasts.</summary>
        [SerializeField, Range(0.1f, 1f)] private float _killSloMoDurationSeconds = 0.45f;
        /// <summary>Minimum REAL seconds between kill slo-mos so it stays a SPECIAL beat (one
        /// per window) instead of a constant stutter when a swarm dies together. 0 = every kill.</summary>
        [SerializeField, Range(0f, 12f)] private float _killSloMoMinIntervalSeconds = 6f;
        /// <summary>Heavier camera kick on the slo-mo'd finisher.</summary>
        [SerializeField, Range(0f, 1f)] private float _killSloMoShake = 0.18f;
        [SerializeField, Range(0f, 0.6f)] private float _killSloMoShakeDuration = 0.35f;

        /// <summary>
        /// Lighter camera shake intensity on a surviving hit (landing-blow feel).
        /// Set to 0 to disable per-hit shake — death kills already shake harder via
        /// Enemy.Die().
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float _hitShakeIntensity = 0.06f;

        /// <summary>Duration of the per-hit camera shake.</summary>
        [SerializeField, Range(0f, 0.5f)] private float _hitShakeDuration = 0.12f;

        // ── Combo ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Seconds with no hits before the combo count resets to zero.
        /// </summary>
        [Header("Combo Counter")]
        [SerializeField, Min(0.5f)] private float _comboWindowSeconds = 2.5f;

        // ── Kill streak ───────────────────────────────────────────────────────

        /// <summary>
        /// Seconds with no kills before the kill streak resets to zero.
        /// </summary>
        [Header("Kill Streak")]
        [SerializeField, Min(1f)] private float _killStreakWindowSeconds = 8f;

        // ── Runtime state ─────────────────────────────────────────────────────

        private int _comboCount;
        private float _comboTimer;

        private int _killStreak;
        private float _killStreakTimer;

        private Coroutine _hitStopRoutine;

        /// <summary>DEF-178: unscaled time the last hit-stop was allowed to start (rate cap).</summary>
        private float _lastHitStopTime = -999f;

        private Coroutine _killSloMoRoutine;
        private float _lastKillSloMoTime = -999f;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires every time the combo count changes — including the reset to zero.
        /// Argument: new combo count.
        /// </summary>
        public event System.Action<int> OnComboChanged;

        /// <summary>
        /// Fires every time the kill streak changes — including the reset to zero.
        /// Argument: new kill streak.
        /// </summary>
        public event System.Action<int> OnKillStreakChanged;

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // THE HOST-INDEPENDENT WATCHDOG (2026-09-02). Every restore path this class had was
            // keyed to the HOST: the two coroutines, their finallys, OnDestroy. All of them stop
            // dead the moment the host is deactivated or destroyed, and a dropped coroutine throws
            // nothing, so no try/finally could ever have covered it. Application.onBeforeRender is
            // a plain static per-frame event that keeps firing regardless of any MonoBehaviour's
            // enabled state. LateUpdate drives the same sweep because headless batchmode renders
            // nothing and therefore never raises onBeforeRender — together they cover both a live
            // device and the regression fleet. Idempotent: -= before += so a second play-mode entry
            // cannot double-subscribe.
            Application.onBeforeRender -= HostIndependentWatchdog;
            Application.onBeforeRender += HostIndependentWatchdog;

            if (Instance != null) return;
            var go = new GameObject("[CombatFeedbackManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CombatFeedbackManager>();
        }

        /// <summary>
        /// Reset the CLASS-LEVEL clock record and drop the host-independent subscription on every
        /// play-mode entry. Statics survive a play-mode restart when domain reload is disabled, so
        /// without this a dip in flight when the editor left play mode would come back as a phantom
        /// deadline against a clock nobody had touched.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticClockState()
        {
            Application.onBeforeRender -= HostIndependentWatchdog;
            s_ourScale = -1f;
            s_dipDeadlineUnscaled = 0f;
            s_hold = null;   // WO-1353: the hold handle is class state - never carry one across play mode
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A duplicate host. It must NOT register (the ladder is keyed by NAME) and, above
                // all, its teardown must not UNregister the live instance's unwind — see OnDestroy.
                FlowTrace.Warn("CombatFeedback",
                    "duplicate CombatFeedbackManager destroyed in Awake - the live singleton keeps " +
                    "the battle-end unwind.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // THE BATTLE-END UNWIND. Registered by OWNER NAME on the EXISTING ladder, so a
            // re-created singleton replaces rather than stacks. See EndDipNow.
            DeNelle.Core.Combat.BattleSessionEnd.RegisterUnwind("combat-feedback", EndDipNow);
        }

        private void OnDestroy()
        {
            // ⛔ ONLY THE LIVE SINGLETON MAY DETACH THE LADDER. BattleSessionEnd keys unwinds by
            // NAME, so an unconditional unregister from a DUPLICATE's teardown would silently
            // remove the LIVE instance's unwind while every source lint still reads the
            // RegisterUnwind call and reports it wired.
            if (Instance != this) return;

            DeNelle.Core.Combat.BattleSessionEnd.UnregisterUnwind("combat-feedback");

            // Restore the clock if this host is torn down mid-dip — routed through the ONE
            // ownership check. It used to be a bare `Time.timeScale = 1f`, which is the Nth-owner
            // stamp: a teardown here would wipe out a live wave-celebration / hit-stop / death
            // slow-mo owned by somebody else.
            if (_hitStopRoutine != null || _killSloMoRoutine != null || s_ourScale >= 0f)
                ReleaseOurClock("combat-feedback host DESTROYED mid-dip");

            _hitStopRoutine   = null;
            _killSloMoRoutine = null;
            Instance = null;
        }

        private void OnDisable()
        {
            // A coroutine dies on deactivation and OnDestroy does NOT fire for it, so without this
            // a mid-dip SetActive(false) leaves the global pinned. Cheap half of the same fix as
            // the deadline sweep; both exist because either alone can be out-raced.
            if (Instance != this) return;
            if (_hitStopRoutine != null || _killSloMoRoutine != null || s_ourScale >= 0f)
                ReleaseOurClock("combat-feedback host DISABLED mid-dip; the deactivation has just " +
                                "killed the restore coroutine and OnDestroy will not fire");
            _hitStopRoutine   = null;
            _killSloMoRoutine = null;
        }

        // =====================================================================
        //  CLOCK OWNERSHIP (2026-09-02) — see the file header
        // =====================================================================

        /// <summary>
        /// The scale the ACTIVE dip (hit stop OR kill slow-mo) has applied, or -1 when none is in
        /// flight. <b>STATIC</b>: Time.timeScale is an ENGINE GLOBAL, so the record of who owns it
        /// is CLASS state. A per-instance field dies with the host, which is precisely the object
        /// whose death strands the clock.
        /// </summary>
        private static float s_ourScale = -1f;

        /// <summary>Unscaled deadline the active dip must have finished by.</summary>
        private static float s_dipDeadlineUnscaled;

        /// <summary>How close the observed clock must be to our record to still count as ours.</summary>
        private const float ScaleMatchEpsilon = 0.001f;

        /// <summary>Grace past the deadline before a still-applied scale is called a leak.</summary>
        private const float DeadlineGraceSeconds = 0.25f;

        /// <summary>Reason token this class's holds carry. Read it straight out of a capture.</summary>
        private const string HoldReason = WorldHold.ReasonCosmeticPrefix + "combat-dip";

        /// <summary>Watchdog ceiling for one dip. The longest this class arms is the 0.45 s kill
        /// slow-mo, so three seconds is six times any legitimate beat.</summary>
        private const float HoldMaxSeconds = 3f;

        /// <summary>The live world-clock hold, or null. STATIC because the clock is a global.</summary>
        private static WorldHold.Handle s_hold;

        /// <summary>
        /// Apply a scale and RECORD it as ours in the same breath, so the record of who owns the
        /// global can never drift from the write that created it.
        ///
        /// <para>⛔ WO-1353 — THIS NO LONGER WRITES <c>Time.timeScale</c>. It takes a hold from
        /// WorldHold, the one owner. Scales and durations are untouched: the hit stop is still
        /// <c>_hitStopTimescale</c> for <c>_hitStopDurationSeconds</c> and the kill slow-mo still
        /// <c>_killSloMoTimescale</c> for <c>_killSloMoDurationSeconds</c>.</para>
        /// </summary>
        private static void ApplyOurClock(float scale)
        {
            if (s_hold != null && s_hold.IsHeld) WorldHold.SetScale(s_hold, scale);
            else s_hold = WorldHold.AcquireScale(HoldReason, scale, HoldMaxSeconds);
            s_ourScale = scale;
        }

        /// <summary>
        /// Give up this class's claim on the world clock by disposing its hold.
        ///
        /// <para>⛔ WO-1353 — THE THREE-CASE DANCE IS GONE. This used to restore 1.00 only when the
        /// engine global still read the value THIS class wrote, and otherwise release without
        /// stamping so as not to become an Nth writer. Correct in isolation; collectively it is the
        /// defect, because when this class's dip overlapped HitStopManager's stop (Enemy.cs fires
        /// both on the same kill frame, twelve lines apart) BOTH owners correctly declined and the
        /// residue stayed on the global. With one owner and slowest-wins composition, disposing is
        /// unconditional and cannot stamp on anybody — WorldHold recomputes from the holds that
        /// remain.</para>
        /// </summary>
        private static void ReleaseOurClock(string why)
        {
            var hold = s_hold;
            s_hold = null;

            if (s_ourScale < 0f)
            {
                if (hold != null && hold.IsHeld)
                {
                    hold.Dispose();
                    FlowTrace.Warn("CombatFeedback",
                        $"combat dip released - {why} - with NO dip recorded but a live world hold " +
                        "outstanding. Disposed it; the two records had drifted apart.");
                }
                return;
            }

            float ours = s_ourScale;
            s_ourScale = -1f;
            s_dipDeadlineUnscaled = 0f;
            hold?.Dispose();

            FlowTrace.Step("CombatFeedback",
                $"combat dip ({ours:F2}) released - {why}. World holds now [{WorldHold.Describe()}], " +
                $"timeScale {Time.timeScale:F2}.");
        }

        /// <summary>
        /// End any in-flight dip RIGHT NOW because the battle session that produced it is over.
        /// Registered on the EXISTING BattleSessionEnd ladder (the same one HitStopManager uses)
        /// rather than as a second recovery mechanism — a cosmetic beat must never outlive the
        /// fight, and every other restore this class has is keyed to the HOST's lifetime, not the
        /// BATTLE's. Safe to run unconditionally: it only ever reverts a scale this class applied.
        /// </summary>
        public void EndDipNow(string context)
        {
            if (_hitStopRoutine != null)   { StopCoroutine(_hitStopRoutine);   _hitStopRoutine   = null; }
            if (_killSloMoRoutine != null) { StopCoroutine(_killSloMoRoutine); _killSloMoRoutine = null; }

            if (s_ourScale < 0f)
            {
                // Said out loud rather than returned in silence: when the town is left slow and
                // this line reads "clock 0.28", the next question ("then who owns 0.28?") is
                // answered by elimination instead of by a second capture.
                FlowTrace.Step("CombatFeedback",
                    $"battle end ({context}): no combat dip of ours in flight, nothing to unwind. " +
                    $"Clock reads {Time.timeScale:F2}.");
                return;
            }

            ReleaseOurClock($"ENDED by battle end ({context})");
        }

        /// <summary>
        /// Restore the clock if OUR dip outlived its deadline, and SAY SO when it has been taken
        /// over by someone else. Idempotent and cheap — it returns on the first line whenever no dip
        /// of ours is in flight, so both drivers can call it every frame.
        /// </summary>
        private static void SweepDeadline()
        {
            if (s_ourScale < 0f) return;
            if (Time.unscaledTime <= s_dipDeadlineUnscaled + DeadlineGraceSeconds) return;

            float ours    = s_ourScale;
            float overdue = Time.unscaledTime - s_dipDeadlineUnscaled;

            var host = Instance;
            if (host != null)
            {
                if (host._hitStopRoutine != null)   { host.StopCoroutine(host._hitStopRoutine);   host._hitStopRoutine   = null; }
                if (host._killSloMoRoutine != null) { host.StopCoroutine(host._killSloMoRoutine); host._killSloMoRoutine = null; }
            }

            // ⛔ WO-1353 — unconditional release. See ReleaseOurClock for why the old three-case
            // comparison against the engine global was individually right and collectively the bug.
            ReleaseOurClock($"deadline sweep - the dip is {overdue:F2}s past its deadline and its " +
                            "restore never ran (host deactivated or destroyed mid-dip)");

            FlowTrace.Fail("CombatFeedback",
                $"COMBAT DIP LEAK RECOVERED: our {ours:F2} dip ran {overdue:F2}s past its deadline and " +
                "its restore never completed. The world-clock hold has been released; live holds now " +
                $"[{WorldHold.Describe()}], timeScale {Time.timeScale:F2}. Reaching this line is a real " +
                "defect in this class's lifecycle - the recovery is not the fix.");
        }

        /// <summary>LateUpdate driver for the sweep. Covers headless batchmode, which renders
        /// nothing and therefore never raises onBeforeRender.</summary>
        private void LateUpdate() => SweepDeadline();

        /// <summary>
        /// The SAME sweep, driven by <c>Application.onBeforeRender</c> — a static per-frame event
        /// that keeps firing no matter which MonoBehaviours are enabled. This is the driver that
        /// closes the dropped-coroutine hole a try/finally cannot reach.
        /// </summary>
        private static void HostIndependentWatchdog()
            => Guard.Try("CombatFeedback", "host-independent deadline sweep", SweepDeadline);

        // ── Update — decay timers (unscaled so they work during hit stop) ─────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_comboCount > 0)
            {
                _comboTimer -= dt;
                if (_comboTimer <= 0f)
                {
                    _comboCount = 0;
                    OnComboChanged?.Invoke(0);
                }
            }

            if (_killStreak > 0)
            {
                _killStreakTimer -= dt;
                if (_killStreakTimer <= 0f)
                {
                    _killStreak = 0;
                    OnKillStreakChanged?.Invoke(0);
                }
            }
        }

        // ── Public static API — called from Enemy.cs ──────────────────────────

        /// <summary>
        /// Call on every successful hit that the enemy survives. Triggers hit stop,
        /// increments combo, and fires a per-hit camera shake.
        /// </summary>
        /// <param name="worldPos">World-space impact position (used for future per-hit VFX).</param>
        /// <param name="damage">Raw damage amount landed this hit.</param>
        public static void Hit(Vector3 worldPos, float damage)
        {
            Instance?.RegisterHit(worldPos, damage);
        }

        /// <summary>
        /// Call when an enemy is killed (hp reached zero). Increments the kill streak.
        /// </summary>
        /// <param name="worldPos">World-space death position.</param>
        public static void Kill(Vector3 worldPos)
        {
            Instance?.RegisterKill(worldPos);
        }

        // ── Properties — read by HUD / UI ─────────────────────────────────────

        /// <summary>Current consecutive-hit combo count.</summary>
        public static int ComboCount => Instance != null ? Instance._comboCount : 0;

        /// <summary>Current kill streak count.</summary>
        public static int KillStreak => Instance != null ? Instance._killStreak : 0;

        // ── Instance logic ────────────────────────────────────────────────────

        // BATTLE ISOLATION helper: true when this impact is a HOME-scene bleed-through that must be
        // suppressed — i.e. a battle is staged AND the impact is NOT inside the far-offset arena.
        // A live arena hit (inside the arena) always passes; outside-arena hits only pass when no
        // battle is running. Guarded so a missing arena singleton never throws into combat.
        private static bool SuppressHomeBleed(Vector3 worldPos)
        {
            // Non-creating check: never spawn the arena singleton just to probe (BattleArena.Existing
            // is null until a fight is first staged).
            if (!BattleArena.AnyBattleInProgress) return false;
            return !BattleArena.IsArenaPosition(worldPos);
        }

        private void RegisterHit(Vector3 worldPos, float damage)
        {
            // BATTLE ISOLATION: while an additive arena battle is staged, the home scene must NOT
            // contribute combat feel (hit-stop / shake / rumble) — that bled into the battle as a
            // phantom rumble and double-simmed the feel (choppy). Gate by IMPACT POSITION so the
            // LIVE arena's own hits (staged ~7km away) still fire their feel; only home-scene
            // bleed-through (impacts back near the origin world) is suppressed during a battle.
            if (SuppressHomeBleed(worldPos)) return;

            // 1. Hit stop — rate-capped (DEF-178). A fresh freeze can only start once
            // per _hitStopMinIntervalSeconds so a multi-enemy AoE wave doesn't re-freeze
            // every frame. Extra hits inside the window still drive combo + shake + VFX
            // below; they just don't restart the time-freeze. Cap 0 = legacy uncapped.
            if (_killSloMoRoutine == null && Time.unscaledTime - _lastHitStopTime >= _hitStopMinIntervalSeconds)
            {
                _lastHitStopTime = Time.unscaledTime;
                if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
                _hitStopRoutine = StartCoroutine(HitStopRoutine());
            }

            // 2. Per-hit camera shake (lighter than kill shake).
            if (_hitShakeIntensity > 0f)
                CameraShakeBridge.Shake(_hitShakeIntensity, _hitShakeDuration);

            // 3. Combo counter — refresh window and increment.
            _comboCount++;
            _comboTimer = _comboWindowSeconds;
            OnComboChanged?.Invoke(_comboCount);
        }

        private void RegisterKill(Vector3 worldPos)
        {
            // BATTLE ISOLATION (see RegisterHit): suppress home-scene kill feel (streak slow-mo /
            // shake / rumble) while an arena battle is staged so it can't bleed into the fight; the
            // live arena's own kills (far-offset) still fire.
            if (SuppressHomeBleed(worldPos)) return;

            _killStreak++;
            _killStreakTimer = _killStreakWindowSeconds;
            OnKillStreakChanged?.Invoke(_killStreak);

            // The "death blow" slo-mo beat — rate-capped so it lands as a SPECIAL moment
            // (one per window), not a stutter when a swarm dies at once. The killing hit
            // also fired a brief hit-stop; supersede it so the slow-time wins cleanly.
            if (_killSloMoTimescale < 1f
                && Time.unscaledTime - _lastKillSloMoTime >= _killSloMoMinIntervalSeconds)
            {
                _lastKillSloMoTime = Time.unscaledTime;
                StartSloMo();
            }
        }

        /// <summary>
        /// A perfect parry/deflect beat — ALWAYS gets the slow-time moment (skill-gated, so no
        /// rate-cap) + a heavier camera kick. Used by both the Knight's physical parry and the
        /// caster's magical deflect (PlayerAttackController.OnParrySuccess).
        /// </summary>
        public static void Parry(Vector3 worldPos)
        {
            if (Instance == null) return;
            Instance._lastKillSloMoTime = Time.unscaledTime; // so a kill right after doesn't double-dip
            Instance.StartSloMo();
        }

        // Shared slow-time starter: supersede any hit-stop so the slow-time wins cleanly.
        private void StartSloMo()
        {
            if (_hitStopRoutine != null) { StopCoroutine(_hitStopRoutine); _hitStopRoutine = null; }
            if (_killSloMoRoutine != null) StopCoroutine(_killSloMoRoutine);
            _killSloMoRoutine = StartCoroutine(KillSloMoRoutine());
            if (_killSloMoShake > 0f) CameraShakeBridge.Shake(_killSloMoShake, _killSloMoShakeDuration);
        }

        // THE EFFECT IS UNCHANGED — same scale, same duration. What changed on 2026-09-02 is that
        // the write is RECORDED as ours and the dip carries an unscaled DEADLINE, and the restore
        // goes through the ONE ownership check instead of stamping 1.00 unconditionally.
        //
        // The finally is KEPT (it still covers StopCoroutine and a throw, and it is the cheapest
        // path) but it is no longer trusted as the guarantee: Unity DROPS a coroutine when its host
        // is deactivated or destroyed, which throws nothing, so the finally simply never runs. The
        // deadline sweep is the path that covers that, and it is why this dip arms a deadline
        // BEFORE its first clock write.
        private IEnumerator HitStopRoutine()
        {
            s_dipDeadlineUnscaled = Time.unscaledTime + _hitStopDurationSeconds;
            ApplyOurClock(_hitStopTimescale);
            try
            {
                yield return new WaitForSecondsRealtime(_hitStopDurationSeconds);
            }
            finally
            {
                _hitStopRoutine = null;
                ReleaseOurClock("hit stop duration elapsed (or the routine was stopped)");
            }
        }

        // The deeper/longer slow-time dip for a finishing blow (see RegisterKill). Same containment
        // as HitStopRoutine above — recorded ownership, an unscaled deadline, one restore check.
        private IEnumerator KillSloMoRoutine()
        {
            s_dipDeadlineUnscaled = Time.unscaledTime + _killSloMoDurationSeconds;
            ApplyOurClock(_killSloMoTimescale);
            try
            {
                yield return new WaitForSecondsRealtime(_killSloMoDurationSeconds);
            }
            finally
            {
                _killSloMoRoutine = null;
                ReleaseOurClock("kill slow-mo duration elapsed (or the routine was stopped)");
            }
        }
    }
}
