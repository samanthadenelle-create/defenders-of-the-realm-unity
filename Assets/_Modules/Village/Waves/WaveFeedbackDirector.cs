// =============================================================================
// WaveFeedbackDirector — wave-lifecycle juice (WO-38 + WO-40).
// -----------------------------------------------------------------------------
//   WO-38 (wave complete): on OnWaveCleared -> victory music sting, a "WAVE n
//          REPELLED" HUD banner, an amber Heart pulse, and a nudge to repair the
//          worst-damaged wall; village music resumes after a short dwell.
//   WO-40 (wave imminent): when the countdown drops to the alert threshold ->
//          a red edge vignette, a tense danger sting, and a haptic pulse
//          (gamepad rumble on desktop / Handheld.Vibrate on mobile; WebGL relies
//          on the visual + audio). One-shot per countdown; reset on wave start.
//
// WO-41 refactor: reflection removed throughout. HUD calls now go through
// CoreServices.Hud (IVillageHud) and audio through AbilityAudioBridge which
// itself uses CoreServices.Audio.
//
// WO-979: this director binds NO HUD reference of its own, deliberately. Every
// HUD call it makes is `CoreServices.Hud?.<x>()` at the point of use, so it
// always talks to whatever HUD is registered NOW rather than to a stale
// reference captured at scene-load. The old `FindHud()` seam (a stub whose whole
// body was `return null;`) and the `object hud` parameter on Bind() are REMOVED
// for that reason — see Bind() and TrySpawn() below.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class WaveFeedbackDirector : MonoBehaviour
    {
        [Tooltip("Seconds-remaining at which the wave-imminent alert fires. 0 disables it.")]
        [SerializeField] private float _imminentThreshold = 3f;

        // WO-763 (owner 2026-07-25): NO per-wave Wisdom. Wisdom is a LEVEL-UP reward
        // only (+ level-gated tier milestones) so new skills/magic feel EARNED over
        // real time, not sprayed out by combat. Kept as a named 0-const so the
        // HeroProgression Wisdom-economy oracle can assert the leak stays closed.
        public const int WisdomPerWave = 0;

        private WaveManager _wave;
        private WallRepairController _repair;
        private bool _imminentFired;
        private float _compassTimer;         // WO-39 poll throttle
        private Transform _heartT;

        /// <summary>
        /// Wires the wave manager before the GameObject is activated.
        /// <para>
        /// WO-979: the second parameter (<c>object hud</c>) is GONE, and so is the
        /// <c>FindHud()</c> stub that fed it. It was never dereferenced — this method's
        /// entire body was, and still is, <c>_wave = wave;</c> — while the caller logged
        /// <c>hudBound=…</c> beside it, reading a DIFFERENT object (the global
        /// <c>CoreServices.Hud</c>) than the one it named. That trace answered "did the
        /// wave HUD bind?" with a confident True about somebody else's registration.
        /// This director deliberately holds NO HUD reference: it calls
        /// <c>CoreServices.Hud?.…</c> at each point of use, so it always reaches the HUD
        /// registered at that moment and degrades quietly (never NREs) when none is.
        /// Do not re-add a hud parameter here — add the call at the use site instead.
        /// </para>
        /// </summary>
        public void Bind(WaveManager wave)
        {
            _wave = wave;
        }

        // ── WO-39: poll live enemies -> light the compass arms ────────────────
        private void Update()
        {
            if (_wave == null) return;
            _compassTimer -= Time.unscaledDeltaTime;
            if (_compassTimer > 0f) return;
            _compassTimer = 0.25f;

            if (_heartT == null)
            {
                var heart = UnityEngine.Object.FindAnyObjectByType<HeartController>();
                if (heart != null) _heartT = heart.transform;
            }
            Vector3 c = _heartT != null ? _heartT.position : Vector3.zero;

            bool n = false, e = false, s = false, w = false;
            var live = _wave.LiveEnemies;
            if (live != null)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    var en = live[i];
                    if (en == null) continue;
                    Vector3 d = en.transform.position - c; d.y = 0f;
                    if (Mathf.Abs(d.x) >= Mathf.Abs(d.z)) { if (d.x >= 0f) e = true; else w = true; }
                    else { if (d.z >= 0f) n = true; else s = true; }
                }
            }
            CoreServices.Hud?.SetAttackDirections(n, e, s, w);
        }

        private void OnEnable()
        {
            if (_wave == null) return;
            _wave.OnWaveCleared.AddListener(OnWaveCleared);
            _wave.OnWaveStarted.AddListener(OnWaveStarted);
            _wave.OnCountdownTick.AddListener(OnCountdownTick);
        }

        private void OnDisable()
        {
            if (_wave == null) return;
            _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            _wave.OnWaveStarted.RemoveListener(OnWaveStarted);
            _wave.OnCountdownTick.RemoveListener(OnCountdownTick);
        }

        // ── WO-38: wave complete ─────────────────────────────────────────────
        private void OnWaveCleared(int waveId)
        {
            AbilityAudioBridge.PlayMusic("Victory");
            // ⛔ WO-1309 — THE WAVE-CLEAR TOAST LEG IS CUT HERE, AND ON PURPOSE.
            //
            // TWO defects lived on this one line, both proven at source:
            //
            // 1. THE NUMBER WAS A LIE. The old call passed CurrentCrystals() — the player's
            //    CRYSTAL WALLET BALANCE — into the `enemiesDefeated` parameter, which
            //    HudKitController.ShowWaveClearToast formats verbatim as "N foes defeated".
            //    The owner's felt-test screenshot read "400 foes defeated"; that 400 was her
            //    crystal balance. The stale WO-38 comment that used to sit here admitted the
            //    value was only ever filling a "+N diamond" line on a banner that has since
            //    been deleted (VillageHudController's banner became a no-op in WO-563), so the
            //    number was re-purposed into a sentence it was never true for.
            //
            // 2. IT WAS THE SECOND ANNOUNCEMENT OF THE SAME EVENT. WaveManager.OnWaveCleared
            //    has TWO uncoordinated listeners: WaveCelebrationManager (-> EndStateView.Show
            //    (EndStateVM.FromWaveClear), the RICH surface: spoils rows, damage rows, Repair
            //    CTA) and this director (-> the toast). The modal is the announcement and it
            //    survives; this leg is the duplicate and it goes. Cutting here, at the ORIGIN,
            //    rather than at the VillageHudController relay, is deliberate — the relay is
            //    just a forwarder and cutting it would leave a live call chain behind it.
            //
            // ⛔ Do NOT "restore" this as a toast in the arena-suppressed case: the arena
            //    victory summary IS the announcement there, and that collision is exactly what
            //    WaveCelebrationManager's suppression branch exists to prevent.
            //
            // The music sting + heart pulse below stay — they are FEEL, not a second sentence.
            PulseHeart();

            // ── Per-wave soft-currency income (tunables in one place) ────────────
            // Wisdom (WO-763, owner 2026-07-25): NO per-wave Wisdom. Wisdom is a
            // LEVEL-UP reward only (+ level-gated tier milestones + discrete battle
            // wins) so new skills/magic feel EARNED, not sprayed out by every wave.
            // The old flat +2/wave was the "every kill gives wisdom" leak — removed.
            // (See WisdomPerWave = 0 above; the grant is guarded so a future re-tune
            // to a non-zero value would re-enable it, but the oracle asserts it's 0.)
            // player starts at 25, with the only other earn paths being level-5+ tier
            // milestones or IAP. A modest per-wave trickle lets a player reach the
            // first cosmetic over ~a dozen waves of normal play — earned, not grindy.
            if (WisdomPerWave > 0)
                try { DeNelle.Village.Talents.WisdomCurrencyService.Instance?.Grant(WisdomPerWave); } catch { }

            // F8-45: Main_Castle_Overworld ships with NO editor-wired WallRepair object
            // (WallRepairSceneSetup only ever targeted the abandoned Village.unity), so
            // the find used to return null and a silent catch no-op'd the whole WO-38
            // repair nudge. TrySpawn now self-installs the controller per scene; this
            // is the belt-and-braces fallback (e.g. the HUD registered after scene load).
            if (_repair == null) _repair = UnityEngine.Object.FindAnyObjectByType<WallRepairController>();
            if (_repair == null)
            {
                EnsureWallRepairInstalled("wave-cleared fallback");
                _repair = UnityEngine.Object.FindAnyObjectByType<WallRepairController>();
            }
            FlowTrace.Step("WaveClear",
                $"repair scan: controller={_repair != null} scene='{SceneManager.GetActiveScene().name}'");
            if (_repair != null)
                Guard.Try("WaveClear", "SurfaceWorstRepair", () => _repair.SurfaceWorstRepair());

            CancelInvoke(nameof(ReturnToVillageMusic));
            Invoke(nameof(ReturnToVillageMusic), 5.5f);
        }

        private void ReturnToVillageMusic() => AbilityAudioBridge.PlayMusic("Village");

        // WO-1309: the old `CurrentCrystals()` helper lived here and fed the crystal WALLET
        // BALANCE into the wave-clear toast's `enemiesDefeated` slot ("400 foes defeated" was
        // the owner's crystal count). Its only caller was that one line in OnWaveCleared, which
        // is now cut, so the helper is deleted rather than left warm for someone to re-wire the
        // same lie. The wave's REAL payout numbers already have an owner —
        // WaveManager's s_lastPayout record, read by EndStateVM.FromWaveClear for the modal.
        // If a per-wave kill count is ever wanted on screen, source it there, not from a wallet.

        private void PulseHeart()
        {
            var heart = UnityEngine.Object.FindAnyObjectByType<HeartController>();
            Vector3 p = heart != null ? heart.transform.position : Vector3.zero;
            try
            {
                AbilityVfxKit.SpawnAbilityVfx(AbilityEffect.Heal,
                    new Color(1f, 0.84f, 0.40f), p + Vector3.up * 0.5f, 3.5f, p);
            }
            catch { /* cosmetic */ }
        }

        // ── WO-40: wave imminent ─────────────────────────────────────────────
        private void OnWaveStarted(int waveId)
        {
            // DEF-183: a distinct wave-START battle horn ("here they come"), played
            // through the existing audio surface (CoreServices.Audio, guarded). This
            // is separate from the countdown-phase danger sting fired by
            // FireImminentAlert below — the horn lands the moment a wave actually
            // begins, the sting telegraphs it during the countdown.
            GameSfx.PlayWaveStart();

            // FAIL #6 cause (a): the owner starts waves with the HUD "START WAVE"
            // button -> WaveManager.ForceBeginNextWave snaps the countdown straight
            // to 0 and only ever ticks OnCountdownTick(0f), so the 0 < x <= threshold
            // window is NEVER hit and the alert silently never fired. If the alert
            // hasn't fired by the time the wave starts (force-start or a zero/short
            // countdown), fire it now so the red vignette + compass flash still play.
            if (!_imminentFired)
                FireImminentAlert("wave-start (countdown skipped)");

            // Now reset for the NEXT wave's countdown and clear the active alert
            // shortly after, so the flash still reads on the wave kicking off.
            _imminentFired = false;
            CancelInvoke(nameof(ClearImminent));
            Invoke(nameof(ClearImminent), 2.2f);
        }

        private void OnCountdownTick(float secondsRemaining)
        {
            if (_imminentThreshold <= 0f || _imminentFired) return;
            if (secondsRemaining > 0f && secondsRemaining <= _imminentThreshold)
                FireImminentAlert($"countdown {secondsRemaining:0.0}s <= {_imminentThreshold:0.0}s");
        }

        // Single entry point for the wave-imminent alert (WO-40): red edge vignette,
        // amber compass flash, danger sting + haptic.
        private void FireImminentAlert(string reason)
        {
            _imminentFired = true;

            // WO-979: this line also said "hudBound", the same misleading name TrySpawn
            // used. Here the value at least MATCHES what the alert consumes — SetImminent
            // and SetCompassImminent both go through CoreServices.Hud — so it is renamed
            // to what it actually reads and given the failure branch it never had. The
            // audio sting + haptic below fire either way; only the visuals depend on it.
            if (CoreServices.Hud == null)
                FlowTrace.Warn("WaveFeedback",
                    $"wave-imminent alert fired ({reason}) with CoreServices.Hud NULL — " +
                    "sting + haptic will play but the red vignette and compass flash will NOT render.");
            else
                FlowTrace.Step("WaveFeedback",
                    $"wave-imminent alert fired ({reason}); hudRegistered=True " +
                    $"({CoreServices.Hud.GetType().Name}).");

            SetImminent(true);
            SetCompassImminent(true);   // WO-40: flash ALL compass arms amber during the alert
            AbilityAudioBridge.PlayDangerSting();
            TriggerHaptic();
            CancelInvoke(nameof(ClearImminent));
            Invoke(nameof(ClearImminent), 2.2f);
        }

        private void ClearImminent()
        {
            SetImminent(false);
            SetCompassImminent(false);
        }

        private void SetImminent(bool on)
        {
            CoreServices.Hud?.SetWaveImminent(on);
        }

        private void SetCompassImminent(bool on)
        {
            // SetCompassImminent is an internal-only method on VillageHudController
            // (not part of IVillageHud); SetWaveImminent drives the imminent state.
            CoreServices.Hud?.SetWaveImminent(on);
        }

        // WO-40: a DOUBLE-PULSE -- two short rumbles (~0.12s on, ~0.10s gap, ~0.12s
        // on) so the alert reads as a deliberate "bump-bump", not a single buzz.
        // Sequenced with Invoke so it works without coroutines on the gamepad;
        // Handheld.Vibrate is double-fired on mobile.
        private void TriggerHaptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            try { Handheld.Vibrate(); } catch { }
            // Second mobile buzz lines up with the gamepad's second pulse.
            CancelInvoke(nameof(VibrateHandheldAgain));
            Invoke(nameof(VibrateHandheldAgain), 0.22f);
#endif
            // Gamepad rumble on desktop (best-effort; WebGL pads usually don't rumble).
            try
            {
                var pad = UnityEngine.InputSystem.Gamepad.current;
                if (pad != null)
                {
                    // First pulse now; stop it, then fire a second pulse after a gap.
                    pad.SetMotorSpeeds(0.45f, 0.75f);
                    CancelInvoke(nameof(StopHaptic));
                    CancelInvoke(nameof(SecondHapticPulse));
                    CancelInvoke(nameof(StopHapticFinal));
                    Invoke(nameof(StopHaptic), 0.12f);          // end first pulse
                    Invoke(nameof(SecondHapticPulse), 0.22f);   // start second pulse
                    Invoke(nameof(StopHapticFinal), 0.34f);     // end second pulse
                }
            }
            catch { }
        }

#if UNITY_ANDROID || UNITY_IOS
        private void VibrateHandheldAgain()
        {
            try { Handheld.Vibrate(); } catch { }
        }
#endif

        private void SecondHapticPulse()
        {
            try { UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(0.45f, 0.75f); } catch { }
        }

        private void StopHaptic()
        {
            try { UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(0f, 0f); } catch { }
        }

        private void StopHapticFinal()
        {
            try { UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(0f, 0f); } catch { }
        }

        // ── Runtime install (no scene edit) ──────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySpawn();

        private static void TrySpawn()
        {
            var wave = UnityEngine.Object.FindAnyObjectByType<WaveManager>();
            if (wave == null) return;   // not a wave scene (Title/HeroSelect/etc.)

            // F8-45: the WO-38 post-wave repair nudge needs a WallRepairController,
            // but the editor wiring (WallRepairSceneSetup) only ever installed it
            // into the abandoned Village.unity — so live wave scenes (Main_Castle_
            // Overworld) had none and the damage report never showed. Self-install
            // it here, per scene, the same way this director installs itself.
            EnsureWallRepairInstalled("scene load");

            if (UnityEngine.Object.FindAnyObjectByType<WaveFeedbackDirector>() != null) return;

            // Inactive-then-activate so Bind() runs before OnEnable subscribes.
            var go = new GameObject("WaveFeedbackDirector");
            go.SetActive(false);
            var dir = go.AddComponent<WaveFeedbackDirector>();
            dir.Bind(wave);
            go.SetActive(true);

            // ── WO-979: report what THIS director actually holds ─────────────────
            // The line that used to sit here read:
            //     "Installed (wave feedback active). hudBound={CoreServices.Hud != null}."
            // fired unconditionally, right beside a FindHud() stub that returned null
            // every time. It NAMED one thing (this director's HUD bind) and REPORTED
            // another (a global some other system registers), so it printed True on
            // every wave-scene load forever and steered any reader away from the
            // broken seam. The seam is now deleted (see Bind), and each claim below is
            // falsifiable against the exact reference it names.
            bool waveBound = dir._wave != null;
            bool hudRegistered = CoreServices.Hud != null;

            if (!waveBound)
            {
                // Unreachable via the guard at the top of TrySpawn, which is precisely
                // why it is asserted: if that guard ever changes, this fails loudly
                // instead of a "wave feedback active" line over a dead director.
                FlowTrace.Fail("WaveFeedback",
                    "install ABORTED: Bind left _wave null — NO wave feedback will fire " +
                    "(no clear banner, no imminent alert, no compass) for this scene.");
                return;
            }

            if (!hudRegistered)
            {
                // Not fatal: audio + haptics still fire, and the HUD may register a
                // frame later (calls are all `?.`-guarded at the use site). Named as a
                // Warn because the VISUAL half of wave feedback is silently absent
                // until it does.
                FlowTrace.Warn("WaveFeedback",
                    $"installed, waveBound=True, but CoreServices.Hud is NOT registered at install " +
                    $"(scene='{SceneManager.GetActiveScene().name}') — wave-clear banner, imminent " +
                    "vignette and compass arms will NOT render until a VillageHudController registers.");
                return;
            }

            FlowTrace.Step("WaveFeedback",
                $"installed: waveBound=True (wave='{wave.name}'), CoreServices.Hud registered " +
                $"({CoreServices.Hud.GetType().Name}), scene='{SceneManager.GetActiveScene().name}'.");
        }

        /// <summary>
        /// F8-45: runtime install of the WO-38 wall-repair surface. Mirrors the
        /// WallRepairSceneSetup editor wiring (one "WallRepair" GameObject carrying
        /// WallRepairController + WallRepairHudBridge, bridge configured with the
        /// controller + the VillageHudController instance) — but built at runtime so
        /// EVERY wave scene gets it, not just the abandoned Village.unity. The HUD
        /// instance comes from CoreServices.Hud (the WO-41 Core seam; the concrete
        /// controller is a MonoBehaviour, so the Object cast is the same reference
        /// the editor wiring serialized) — no Village->HUD asmdef reference is added.
        /// No per-structure registration is needed: SurfaceWorstRepair calls
        /// CollectAllDamaged, which scans the scene itself (DEF-226 explicit flow).
        /// If the HUD has not registered yet, install is deferred and RE-ARMED on
        /// <see cref="CoreServices.HudRegistered"/> (WO-1024).
        ///
        /// <para>⛔ THE OLD COMMENT HERE WAS WRONG AND IT COST A WHOLE SESSION'S REPAIR SURFACE.
        /// It read "OnWaveCleared retries, by which time the HUD is live" — but that retry only
        /// fires on a WAVE-CLEARED event. In the HUB, where no wave may ever run, the retry never
        /// came, so the only path that installs an ENABLED WallRepairController never ran and
        /// tap-to-repair did not exist at all. (Both other paths — HubRepairAffordance.EnsureRepair
        /// and EchoRepairService — deliberately create a DISABLED, logic-only controller.) The
        /// deferral now re-arms on the event it is actually waiting for, so it is correct in a
        /// scene that never fights.</para>
        /// </summary>
        private static bool s_awaitingHudForRepair;

        private static void EnsureWallRepairInstalled(string context)
        {
            if (UnityEngine.Object.FindAnyObjectByType<WallRepairController>() != null) return;

            var hud = CoreServices.Hud as UnityEngine.Object;
            if (hud == null)
            {
                if (!s_awaitingHudForRepair)
                {
                    s_awaitingHudForRepair = true;
                    CoreServices.HudRegistered += OnHudRegisteredForRepair;
                }
                FlowTrace.Warn("WaveClear",
                    $"wall-repair self-install deferred ({context}): CoreServices.Hud not registered yet - " +
                    "RE-ARMED on CoreServices.HudRegistered (WO-1024), so this no longer depends on a wave clearing.");
                return;
            }

            // Inactive-then-activate so Configure lands before the bridge's Start binds.
            var go = new GameObject("WallRepair");
            go.SetActive(false);
            var controller = go.AddComponent<WallRepairController>();
            var bridge = go.AddComponent<WallRepairHudBridge>();
            bridge.Configure(controller, hud);
            go.SetActive(true);

            FlowTrace.Step("WaveClear",
                $"self-installed WallRepairController (scene='{SceneManager.GetActiveScene().name}', {context})");
        }

        /// <summary>
        /// WO-1024 re-arm. Unsubscribes FIRST - CoreServices.HudRegistered is a static event that
        /// survives scene loads, so a handler left attached would accumulate across every load.
        /// </summary>
        private static void OnHudRegisteredForRepair(DeNelle.Core.HUD.IVillageHud hud)
        {
            CoreServices.HudRegistered -= OnHudRegisteredForRepair;
            s_awaitingHudForRepair = false;
            EnsureWallRepairInstalled("hud-registered re-arm");
        }
    }
}
