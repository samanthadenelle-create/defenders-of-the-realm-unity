// =============================================================================
// WaveCelebrationManager — wave-clear dopamine burst (WO-83).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Called by WaveManager.CompleteWave() (via OnWaveCleared event hook or direct
// call). Fires bloom spike, screen flash, slow-mo dip, VFX rain, floating
// "Wave X Cleared!" text, and a camera shake — all mobile-safe.
//
// Bloom: uses UnityEngine.Rendering.Universal.Bloom from a Volume profile.
//        If the Volume or Bloom component is absent the spike is skipped
//        gracefully. Same for all other optional systems (VFXManager, etc.).
//
// Results banner: routes through the ONE shared Obsidian end-state template
//   (DeNelle.Village.UI.EndStateView, compact wave-results variant — UI audit
//   2026-07-02 §3.2 WO-B). The old world-space prefab text + IMGUI OnGUI toast
//   are RETIRED (the IMGUI path was a LEGACY-verdict surface in the audit).
//
// ⛔ Time.timeScale — THE 2026-09-02 LEAK. READ THIS BEFORE TOUCHING ANY RESTORE PATH.
//
// The line that used to sit here read: "always restored in a finally-equivalent path —
// the ease-back coroutine always runs to completion via WaitForSecondsRealtime." That was
// FALSE, and it is the exact false comfort CLAUDE.md §12 warns about: a coroutine does NOT
// run to completion when its host is DEACTIVATED or DESTROYED. Unity drops it silently —
// no exception, so no try/finally could ever have covered it — and whatever scale it had
// already written stays on the engine global forever.
//
// CAPTURED EVIDENCE (owner F8 seq 4656, 2026-09-02):
//     [Flow:Pause] WorldHold ACQUIRE 'pause-menu' -> timeScale 0 (captured 0.28).
//     timeScale=0.28 dt=0.0047 inputSuppressed=False autoWalk=False
// 0.28 is _slowMoScale, below. It is this class's number and nobody else's. The clock was
// already at 28% speed BEFORE the pause menu opened; input was never suppressed, so the
// owner could walk — everything was simply running at a quarter speed. That is her
// long-standing "in town everything slowed", finally captured with a number on it.
//
// ⛔ THE EFFECT IS NOT THE BUG — THE LEAK IS (owner ruling 2026-09-02, after this dip was
// once DELETED outright as a "stability fix" and the deletion was REVERSED). Nothing below
// shortens, weakens, gates or disables the celebration dip. It is CONTAINED:
//   * ownership of the engine-global clock is CLASS state (s_ourScale), never per-host,
//     and every exit funnels through ONE check (ReleaseOurClock) that restores 1.00 only
//     while the clock still reads OUR value, and otherwise releases WITHOUT stamping and
//     SAYS SO — a foreign owner's live slow-mo is never overwritten, and never ignored;
//   * an UNSCALED deadline sweep driven by BOTH LateUpdate and Application.onBeforeRender,
//     so a disabled or destroyed host cannot strand the clock (onBeforeRender is a static
//     event that keeps firing regardless of any MonoBehaviour's enabled state; LateUpdate
//     covers headless batchmode, which renders nothing and so never raises it);
//   * registration with the EXISTING BattleSessionEnd unwind ladder, so a cosmetic dip can
//     never outlive the fight that produced it. Same ladder, same shape and same reasoning
//     as HitStopManager (fixed 2026-09-02) — deliberately NOT a second mechanism.
//
// Bootstrapped at runtime: a [RuntimeInitializeOnLoadMethod] installs the
// singleton after each scene load when a WaveManager is present, so no scene
// edit is required.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

#if UNITY_POST_PROCESSING_STACK_V2
// URP Bloom is accessed below via conditional compilation; no hard PPv2 dep.
#endif

namespace DeNelle.Village
{
    /// <summary>
    /// Plays a full celebration sequence on wave clear: bloom spike, screen flash,
    /// slow-mo, VFX rain, floating text, and camera shake. Installed automatically
    /// when a <see cref="WaveManager"/> exists in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveCelebrationManager : MonoBehaviour
    {
        public static WaveCelebrationManager Instance { get; private set; }

        // ── Screen effects ────────────────────────────────────────────────────

        [Header("Bloom (optional — skipped if Volume/Bloom absent)")]
        [Tooltip("Post-process Volume that owns the Bloom override. " +
                 "Leave null to skip the bloom spike.")]
        [SerializeField] private UnityEngine.Rendering.Volume _postProcessVolume;

        [SerializeField] private float _bloomPeakIntensity = 6f;
        [SerializeField] private float _bloomBaseline      = 1.2f;
        [SerializeField] private float _bloomDuration      = 0.55f;

        [Header("Screen Flash")]
        [SerializeField] private float _flashDuration = 0.3f;
        [SerializeField] private Color _flashColor    = new Color(1f, 0.95f, 0.7f, 0.7f);

        [Header("Slow Motion")]
        [SerializeField] private float _slowMoScale    = 0.28f;
        [SerializeField] private float _slowMoDuration = 0.9f;   // real seconds

        [Header("VFX Rain")]
        [SerializeField] private VFXType _celebrationVFX    = VFXType.WaveClear_Celebration;
        [SerializeField] private int     _celebrationBursts = 3;
        [SerializeField] private float   _burstSpread       = 4f;

        [Header("Celebration Anchor")]
        [Tooltip("World-space anchor for the VFX bursts (centre of village). " +
                 "Falls back to Vector3.zero + up when null.")]
        [SerializeField] private Transform  _textSpawnPoint;

        [Header("Mobile")]
        [SerializeField] private bool _reducedOnMobile = true;

        // ── Bloom runtime handle ──────────────────────────────────────────────
        private UnityEngine.Rendering.Universal.Bloom _bloom;
        private bool _bloomAvailable;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A duplicate host. It must NOT register (the ladder is keyed by NAME) and, above
                // all, its teardown must not UNregister the live instance's unwind — see OnDestroy.
                FlowTrace.Warn("WaveCelebration",
                    "duplicate WaveCelebrationManager destroyed in Awake - the live singleton keeps " +
                    "the battle-end unwind.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // THE BATTLE-END UNWIND. Registered by OWNER NAME on the EXISTING ladder, so a
            // re-created singleton replaces rather than stacks. See EndDipNow.
            DeNelle.Core.Combat.BattleSessionEnd.RegisterUnwind("wave-celebration", EndDipNow);

            // Resolve Bloom from the Volume profile (URP).
            if (_postProcessVolume != null && _postProcessVolume.profile != null)
            {
                if (_postProcessVolume.profile.TryGet(
                        out UnityEngine.Rendering.Universal.Bloom b))
                {
                    _bloom          = b;
                    _bloomAvailable = true;
                }
            }
        }

        private void OnDestroy()
        {
            // ⛔ ONLY THE LIVE SINGLETON MAY DETACH THE LADDER. BattleSessionEnd keys unwinds by
            // NAME, so an unconditional unregister from a DUPLICATE's teardown (Awake destroys
            // duplicates, and their OnDestroy runs at the end of that frame) would silently remove
            // the LIVE instance's unwind while every source lint still reads the RegisterUnwind
            // call and reports it wired. Same hazard, same guard, as HitStopManager.OnDestroy.
            if (Instance != this) return;

            DeNelle.Core.Combat.BattleSessionEnd.UnregisterUnwind("wave-celebration");

            // Restore the clock if this host is torn down mid-dip — routed through the ONE
            // ownership check. It used to be a bare `Time.timeScale = 1f`, which is the Nth-owner
            // stamp: a teardown here would wipe out a live HitStop / kill slow-mo / death slow-mo
            // owned by somebody else. Note this path did NOT run for the captured leak at all —
            // OnDestroy does not fire for a coroutine killed by deactivation, which is why the
            // deadline sweep below exists.
            if (s_ourScale >= 0f)
                ReleaseOurClock("wave-celebration host DESTROYED mid-dip");

            _slowMoRoutine = null;
            Instance = null;
        }

        private void OnDisable()
        {
            // A coroutine dies on deactivation and OnDestroy does NOT fire for it, so without this
            // a mid-dip SetActive(false) leaves the global pinned at 0.28 forever. This is the
            // cheap half of the same fix as the deadline sweep; both exist because either alone
            // can be out-raced.
            if (Instance != this) return;
            if (_slowMoRoutine != null || s_ourScale >= 0f)
                ReleaseOurClock("wave-celebration host DISABLED mid-dip; the deactivation has just " +
                                "killed the ease-back coroutine and OnDestroy will not fire");
            _slowMoRoutine = null;
        }

        // =====================================================================
        //  CLOCK OWNERSHIP (2026-09-02) — see the file header
        // =====================================================================

        /// <summary>
        /// The scale the ACTIVE dip has applied, or -1 when no dip of ours is in flight.
        /// <b>STATIC</b>: Time.timeScale is an ENGINE GLOBAL, so the record of who owns it is CLASS
        /// state. A per-instance field dies with the host, which is precisely the object whose death
        /// strands the clock.
        /// </summary>
        private static float s_ourScale = -1f;

        /// <summary>Unscaled deadline the active dip must have finished by.</summary>
        private static float s_dipDeadlineUnscaled;

        /// <summary>How close the observed clock must be to our record to still count as ours.</summary>
        private const float ScaleMatchEpsilon = 0.001f;

        /// <summary>Grace past the deadline before a still-applied scale is called a leak. Generous
        /// enough that a frame hitch or a one-frame ordering race is never mistaken for one.</summary>
        private const float DeadlineGraceSeconds = 0.25f;

        private Coroutine _slowMoRoutine;

        /// <summary>Apply a scale and RECORD it as ours in the same breath, so the record of who
        /// owns the global can never drift from the write that created it.</summary>
        private static void ApplyOurClock(float scale)
        {
            Time.timeScale = scale;
            s_ourScale = scale;
        }

        /// <summary>
        /// Give up ownership of the world clock, restoring 1.00 ONLY if the clock still reads the
        /// value THIS class wrote. Three cases, and the third is the whole point:
        ///   * clock still reads our value -> restore 1.00;
        ///   * clock already reads 1.00    -> somebody restored it first, nothing to do;
        ///   * clock reads someone ELSE's  -> release WITHOUT stamping, and NAME the value.
        /// Stamping in the third case would make this class an Nth writer of the global, which is
        /// the shape of the defect, not the fix. Doing it in SILENCE is how the 0.28 leak went
        /// unexplained for weeks (CLAUDE.md §12).
        /// </summary>
        private static void ReleaseOurClock(string why)
        {
            if (s_ourScale < 0f) return;

            float ours     = s_ourScale;
            float observed = Time.timeScale;

            s_ourScale = -1f;
            s_dipDeadlineUnscaled = 0f;

            if (Mathf.Abs(observed - ours) < ScaleMatchEpsilon)
            {
                Time.timeScale = 1f;
                FlowTrace.Step("WaveCelebration",
                    $"wave-clear slow-mo ({ours:F2}) released - {why}. timeScale restored to 1.00.");
                return;
            }

            if (Mathf.Abs(observed - 1f) < ScaleMatchEpsilon)
            {
                FlowTrace.Step("WaveCelebration",
                    $"wave-clear slow-mo ({ours:F2}) released - {why}. The clock was already back at " +
                    "1.00; another owner restored it first.");
                return;
            }

            FlowTrace.Warn("WaveCelebration",
                $"wave-clear slow-mo ({ours:F2}) released - {why} - but the clock reads {observed:F2} " +
                $"({observed * 100f:F0}% speed), which is NOT the value we wrote. We deliberately do NOT " +
                "stamp 1.00 over another owner's live slow-mo, so the remaining restore for this clock " +
                $"belongs to whoever wrote {observed:F2}. If the world is still slow after this line, " +
                "that owner is the leak - not the wave celebration. Non-1 writers in the tree: " +
                "HitStopManager, CombatFeedbackManager (hit stop 0.05 / kill slow-mo 0.30), " +
                "HeroHitReaction (death 0.30), ArenaDeathCam, WorldHold.");
        }

        /// <summary>
        /// End any in-flight celebration dip RIGHT NOW because the battle session that produced it
        /// is over. Registered on the EXISTING BattleSessionEnd ladder (the same one HitStopManager
        /// uses) rather than as a second recovery mechanism — a cosmetic beat must never outlive the
        /// fight, and every other restore this class has is keyed to the HOST's lifetime, not the
        /// BATTLE's. Safe to run unconditionally: it only ever reverts a scale this class applied.
        /// </summary>
        public void EndDipNow(string context)
        {
            if (_slowMoRoutine != null) { StopCoroutine(_slowMoRoutine); _slowMoRoutine = null; }

            if (s_ourScale < 0f)
            {
                // Said out loud rather than returned in silence: when the town is left slow and this
                // line reads "clock 0.28", the next question ("then who owns 0.28?") is answered by
                // elimination instead of by a second capture.
                FlowTrace.Step("WaveCelebration",
                    $"battle end ({context}): no celebration dip of ours in flight, nothing to unwind. " +
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

            float ours      = s_ourScale;
            float leaked    = Time.timeScale;
            float overdue   = Time.unscaledTime - s_dipDeadlineUnscaled;
            bool  stillOurs = Mathf.Abs(leaked - ours) < ScaleMatchEpsilon;

            var host = Instance;
            if (host != null && host._slowMoRoutine != null)
            {
                host.StopCoroutine(host._slowMoRoutine);
                host._slowMoRoutine = null;
            }

            s_ourScale = -1f;
            s_dipDeadlineUnscaled = 0f;

            if (stillOurs)
            {
                Time.timeScale = 1f;
                FlowTrace.Fail("WaveCelebration",
                    $"WAVE-CLEAR SLOW-MO LEAK RECOVERED: timeScale was still {leaked:F2} {overdue:F2}s " +
                    "past its deadline - the ease-back never completed (the host was almost certainly " +
                    "deactivated or destroyed, which kills coroutines without firing OnDestroy and " +
                    "without throwing, so no try/finally could have caught it). Restored to 1.00. The " +
                    $"world was running at {leaked * 100f:F0}% speed, which is the owner's 'in town " +
                    "everything slowed' (F8 seq 4656 captured this exact value at 0.28).");
                return;
            }

            if (Mathf.Abs(leaked - 1f) < ScaleMatchEpsilon) return;   // benign: already back to normal

            FlowTrace.Warn("WaveCelebration",
                $"WAVE-CLEAR SLOW-MO SUPERSEDED, NOT RESTORED: our {ours:F2} dip is {overdue:F2}s past " +
                $"its deadline but the clock reads {leaked:F2} ({leaked * 100f:F0}% speed), which is NOT " +
                "our value. We release ownership WITHOUT stamping 1.00, because that scale belongs to " +
                "another owner. READ THIS AS A LEAD, NOT AS OUR LEAK: if the world is still slow after " +
                $"this line, whoever wrote {leaked:F2} failed to restore it. Non-1 writers: " +
                "HitStopManager, CombatFeedbackManager, HeroHitReaction, ArenaDeathCam, WorldHold.");
        }

        /// <summary>LateUpdate driver for the sweep. Covers headless batchmode, which renders
        /// nothing and therefore never raises onBeforeRender.</summary>
        private void LateUpdate() => SweepDeadline();

        /// <summary>
        /// The SAME sweep, driven by <c>Application.onBeforeRender</c> — a static per-frame event
        /// that keeps firing no matter which MonoBehaviours are enabled. This is the driver that
        /// closes the captured hole: a dropped coroutine throws nothing, so a try/finally can never
        /// cover it, and every other restore this class had died with its host.
        /// </summary>
        private static void HostIndependentWatchdog()
            => Guard.Try("WaveCelebration", "host-independent deadline sweep", SweepDeadline);

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
        }

        // ── Called by WaveManager (via OnWaveCleared listener) ───────────────

        /// <summary>Trigger the full wave-clear celebration for <paramref name="waveNumber"/>.</summary>
        public void PlayWaveClear(int waveNumber)
        {
            StartCoroutine(WaveClearRoutine(waveNumber));
        }

        public static float Significance01(int waveNumber)
        {
            if (waveNumber <= 1) return 0f;
            if (waveNumber >= 7 && waveNumber % 7 == 0) return 1f;
            return Mathf.Clamp01((waveNumber - 1f) / 12f);
        }

        // ── Main sequence ─────────────────────────────────────────────────────

        private IEnumerator WaveClearRoutine(int waveNumber)
        {
            bool mobile = false;
#if UNITY_ANDROID || UNITY_IOS
            mobile = _reducedOnMobile;
#endif
            float mobileMult = mobile ? 0.6f : 1f;
            float significance = Significance01(waveNumber);
            float celebrationMult = Mathf.Lerp(0.45f, 1f, significance);

            // 1. Bloom spike (fire-and-forget coroutine).
            if (_bloomAvailable)
                StartCoroutine(BloomSpike(_bloomPeakIntensity * mobileMult * celebrationMult));

            // 2. Screen flash.
            StartCoroutine(ScreenFlash(mobile));

            // 3. Slow-mo dip. Tracked so every teardown path can stop it — an untracked
            //    fire-and-forget coroutine is a clock write nobody can cancel.
            if (_slowMoRoutine != null) StopCoroutine(_slowMoRoutine);
            _slowMoRoutine = StartCoroutine(SlowMoDip(_slowMoDuration * mobileMult * celebrationMult));

            // 4. VFX rain bursts.
            int maxBursts = mobile ? Mathf.Max(1, _celebrationBursts - 1) : _celebrationBursts;
            int bursts = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, maxBursts, significance)), 1, maxBursts);
            Vector3 origin = _textSpawnPoint != null
                ? _textSpawnPoint.position
                : Vector3.zero;

            for (int i = 0; i < bursts; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-_burstSpread, _burstSpread), 0f,
                    Random.Range(-_burstSpread, _burstSpread));
                VFXManager.Play(_celebrationVFX, origin + offset + Vector3.up * 1.5f);
                yield return new WaitForSecondsRealtime(0.12f);
            }

            // 5. "Wave X Cleared!" results banner — the shared end-state template
            //    (compact variant: non-blocking, auto-dismissing, one Continue).
            //
            // ARENA GUARD (owner-reported twice: Seeker 313763, desktop EXE F8 seq=2140).
            // EndStateView.Show DESTROYS whatever end-state is already open (EndStateView.cs:92).
            // The village WaveManager keeps ticking while the hero is away fighting a real-time
            // arena encounter 7km out at ArenaCentre, so a village wave can clear seconds after an
            // ARENA VICTORY panel appears. This banner then replaced that panel and took its
            // Continue action with it - and that action was the ONLY route home. The owner tapped
            // what she thought was Continue, hit this banner's action=dismiss instead, and was left
            // standing in the arena with the HUD locked in Battle.
            //
            // A wave-clear banner is pure garnish; an arena victory summary is load-bearing. When
            // they collide, the garnish yields. The wave is still cleared, rewarded and persisted -
            // only the cosmetic banner is skipped.
            // AnyBattleInProgress (BattleArena.cs:227) is the STATIC, null-safe accessor -
            // BattleInProgress itself is an instance property. Fully qualified because
            // BattleArena lives in DeNelle.Village.Arena and this file is DeNelle.Village.
            if (DeNelle.Village.Arena.BattleArena.AnyBattleInProgress)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("WaveCelebration",
                    $"wave {waveNumber} clear banner SUPPRESSED - an arena battle is in progress and " +
                    "showing it would destroy the arena victory summary (and its home-return action). " +
                    "The wave still cleared and rewarded; only the banner is skipped.");
            }
            else
            {
                DeNelle.Village.UI.EndStateView.Show(
                    DeNelle.Village.UI.EndStateVM.FromWaveClear(waveNumber));
            }

            // 6. Camera shake.
            float shakeIntensity = (mobile ? 0.25f : 0.42f) * celebrationMult;
            CameraShakeBridge.Shake(shakeIntensity, 0.35f);

            // AudioService.Instance?.PlaySfx(SfxId.WaveClear);
        }

        // ── Sub-routines ──────────────────────────────────────────────────────

        private IEnumerator BloomSpike(float peak)
        {
            if (!_bloomAvailable || _bloom == null) yield break;

            float elapsed = 0f;
            float rampUp  = _bloomDuration * 0.4f;

            // Ramp up (unscaled — survives slow-mo).
            while (elapsed < rampUp)
            {
                elapsed += Time.unscaledDeltaTime;
                _bloom.intensity.Override(Mathf.Lerp(_bloomBaseline, peak, elapsed / rampUp));
                yield return null;
            }

            // Decay back to baseline.
            elapsed = 0f;
            float decay = _bloomDuration * 0.6f;
            while (elapsed < decay)
            {
                elapsed += Time.unscaledDeltaTime;
                _bloom.intensity.Override(Mathf.Lerp(peak, _bloomBaseline, elapsed / decay));
                yield return null;
            }

            _bloom.intensity.Override(_bloomBaseline);
        }

        private IEnumerator ScreenFlash(bool mobile)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            Color orig  = cam.backgroundColor;
            float alpha = mobile ? _flashColor.a * 0.5f : _flashColor.a;
            Color flash = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);

            cam.backgroundColor = flash;
            yield return new WaitForSecondsRealtime(_flashDuration * 0.2f);

            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                cam.backgroundColor = Color.Lerp(flash, orig, elapsed / _flashDuration);
                yield return null;
            }

            cam.backgroundColor = orig;
        }

        /// <summary>
        /// The wave-clear slow-motion dip. THE EFFECT IS UNCHANGED — same scale, same duration,
        /// same ease. What changed on 2026-09-02 is that every write is RECORDED as ours and the
        /// dip carries an unscaled DEADLINE, so the sweep above can finish a dip whose host was
        /// deactivated or destroyed mid-flight (the captured 0.28 leak).
        /// </summary>
        private IEnumerator SlowMoDip(float duration)
        {
            const float ease = 0.3f;                       // real seconds of ease-back to 1x

            // Arm the deadline BEFORE the first write, so the sweep is armed even if this
            // coroutine is dropped on its very next line.
            s_dipDeadlineUnscaled = Time.unscaledTime + duration + ease;
            ApplyOurClock(_slowMoScale);

            yield return new WaitForSecondsRealtime(duration);

            // Ease back to 1x over 0.3 real seconds.
            float elapsed = 0f;
            while (elapsed < ease)
            {
                elapsed += Time.unscaledDeltaTime;

                // Ownership re-check on EVERY frame of the ramp. A hit stop or kill slow-mo landing
                // mid-ease would otherwise be fought frame-by-frame by this lerp, and the loser is
                // whichever owner writes last. If the clock is no longer ours we hand it over and
                // stop writing, rather than becoming an Nth writer of the global.
                if (Mathf.Abs(Time.timeScale - s_ourScale) > ScaleMatchEpsilon)
                {
                    _slowMoRoutine = null;
                    ReleaseOurClock("another owner took the clock mid ease-back");
                    yield break;
                }

                ApplyOurClock(Mathf.Lerp(_slowMoScale, 1f, elapsed / ease));
                yield return null;
            }

            _slowMoRoutine = null;

            // The final restore goes through the ONE ownership check. It used to be an
            // unconditional `Time.timeScale = 1f`, which stamps 1.00 over a live foreign slow-mo on
            // the normal path — the exact Nth-owner move the sweep is careful never to make.
            ReleaseOurClock("dip completed its ease-back");
        }

        // ── Bootstrap — auto-install when WaveManager is present ─────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            // The host-independent deadline driver. Idempotent (-= before +=) so a second
            // play-mode entry cannot double-subscribe. Armed here rather than in Awake because it
            // must survive the host it is watching.
            Application.onBeforeRender -= HostIndependentWatchdog;
            Application.onBeforeRender += HostIndependentWatchdog;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryInstall();

        private static void TryInstall()
        {
            if (FindAnyObjectByType<WaveManager>() == null) return;
            if (Instance != null) return;

            var go  = new GameObject("[WaveCelebrationManager]");
            var mgr = go.AddComponent<WaveCelebrationManager>();

            // Wire to WaveManager's OnWaveCleared event.
            var wave = FindAnyObjectByType<WaveManager>();
            if (wave != null)
                wave.OnWaveCleared.AddListener(mgr.PlayWaveClear);

            Debug.Log("[WaveCelebrationManager] Installed and wired to WaveManager.");
        }
    }
}
