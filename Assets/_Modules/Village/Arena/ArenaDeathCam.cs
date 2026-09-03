// =============================================================================
// ArenaDeathCam — the climactic DEATH-CAMERA HOLD for the BattleArena (WO-493 #4,
// the ONE genuinely net-new feel piece per WO-497).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// PROBLEM (WO-497 net-new #1): BattleArena.Resolve() fires the INSTANT the last enemy
// dies / the hero goes down -> the teardown + warp-home cut away BEFORE the death
// animation plays. The kill / defeat beat is lost.
//
// FIX: on the BATTLE-WINNING kill AND on hero death, a brief cinematic camera LINGER:
//   - take over the main Camera (suspend SmartMobileCamera so it does not fight),
//   - frame the dying actor + slow PUSH-IN over ~3-4s,
//   - optional light slow-mo on the kill (unscaled-time driven so it is unaffected),
//   - then release, so BattleArena.Resolve continues the existing return-to-spot flow.
//
// RESERVED for the climactic death (the LAST enemy, or the hero) -- NOT every kill
// (owner: "reserve it for the climactic death"). BattleArena calls Hold(...) and yields
// on IsHolding before teardown, so the return-to-engagement-spot flow is unchanged --
// it just waits for the linger.
//
// Reuse-not-greenfield: drives the EXISTING Camera.main + SmartMobileCamera.enabled
// toggle (no new camera rig); skip-safe (LogWarning, never throws into the fight);
// ASCII-only logs; instrumented per CLAUDE.md S12 (FlowTrace "BattleArena").
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Runtime camera-hold for the climactic death. Created/owned by <see cref="BattleArena"/>.
    /// One hold at a time; <see cref="IsHolding"/> gates the arena teardown.
    /// </summary>
    public sealed class ArenaDeathCam : MonoBehaviour
    {
        // Default linger window (owner: "death cycle only a few seconds", ~3-5s).
        private const float DefaultHoldSeconds = 3.6f;
        // Push-in: how much closer the camera eases toward the body over the hold.
        private const float PushInFraction = 0.45f;   // ends ~45% nearer than it started
        // Light slow-mo on the kill; restored on release. Hero death holds full-speed (the defeat beat).
        private const float KillSlowMoScale = 0.55f;
        private const float SlowMoEaseSeconds = 0.35f;

        /// <summary>True while the cinematic hold is running (BattleArena yields on this before teardown).</summary>
        public bool IsHolding { get; private set; }

        private Behaviour _suspendedCam;   // SmartMobileCamera disabled during the hold (restored after)
        private float _savedTimeScale = 1f;
        private bool _slowMoApplied;

        /// <summary>
        /// Run the climactic death-hold on <paramref name="target"/>. <paramref name="slowMo"/>
        /// adds the light kill slow-mo (use for the battle-winning kill; false for hero death).
        /// Skip-safe: a null target / missing camera ends the hold immediately (no linger,
        /// the fight resolves normally). Idempotent: a second call while holding is ignored.
        /// </summary>
        public void Hold(Transform target, bool slowMo, float seconds = DefaultHoldSeconds)
        {
            if (IsHolding) { FlowTrace.Warn("BattleArena", "ArenaDeathCam.Hold: already holding - ignored."); return; }
            StartCoroutine(HoldRoutine(target, slowMo, Mathf.Clamp(seconds, 0.5f, 6f)));
        }

        private IEnumerator HoldRoutine(Transform target, bool slowMo, float seconds)
        {
            IsHolding = true;
            Camera cam = Camera.main;

            if (cam == null || target == null)
            {
                FlowTrace.Warn("BattleArena", "ArenaDeathCam: no camera/target - skipping linger.");
                IsHolding = false;
                yield break;
            }

            // Suspend the follow camera so it does not fight our framing. Restored in the finally-style tail.
            Guard.Try("BattleArena", "suspend follow camera", () =>
            {
                var smc = cam.GetComponent("SmartMobileCamera") as Behaviour;
                if (smc != null && smc.enabled) { smc.enabled = false; _suspendedCam = smc; }
            });
            // F8-15 death forensic window: the death-cam TAKES the camera here — if this fires on a
            // HERO death, the push-in target below is what the owner sees instead of the follow cam
            // (owner ruling: camera must stay on the hero for the death animation).
            DeathTrace.Camera(
                $"follow cam {(_suspendedCam != null ? "SUSPENDED" : "not present")} -> death-cam push-in on '{target.name}' for {seconds:F1}s",
                "ArenaDeathCam.HoldRoutine");

            // Frame: capture the start seat, aim a touch above the body, ease toward it.
            Vector3 startPos = cam.transform.position;
            Quaternion startRot = cam.transform.rotation;
            Vector3 focus = target.position + Vector3.up * 1.2f;
            // F8 2026-07-11 "camera looks straight up at the last kill": the corpse settle
            // could LIFT the body (footGap bug, now capped enemy-side) and this routine
            // re-read the risen transform per frame — the victory shot chased it into the
            // sky. Freeze the focus HEIGHT at the death moment; the corpse may drift in
            // X/Z during settle but the framing never pitches skyward.
            float focusY = focus.y;
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena",
                $"death-cam hold on '{target.name}' at y={target.position.y:0.00} (focus height frozen)");
            Vector3 endPos = Vector3.Lerp(startPos, focus, PushInFraction);

            if (slowMo) ApplySlowMo();

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                // Unscaled so the slow-mo (timeScale) does not stretch the real-world linger length.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float ease = t * t * (3f - 2f * t); // smoothstep push-in

                // Body may be destroyed (DeathHold expiry) mid-linger -> hold the last good focus.
                // Height stays frozen at the death moment (see above); only X/Z track the body.
                if (target != null)
                    focus = new Vector3(target.position.x, focusY, target.position.z);

                if (cam != null)
                {
                    cam.transform.position = Vector3.Lerp(startPos, endPos, ease);
                    Quaternion want = Quaternion.LookRotation((focus - cam.transform.position).normalized, Vector3.up);
                    cam.transform.rotation = Quaternion.Slerp(startRot, want, ease);
                }
                yield return null;
            }

            RestoreSlowMo();

            // Hand the camera back to the follow rig (BattleArena.Resolve warps the hero home next).
            Guard.Try("BattleArena", "restore follow camera", () =>
            {
                if (_suspendedCam != null) _suspendedCam.enabled = true;
            });
            // F8-15: camera handed back (BattleArena warps the hero home next — the next
            // HERO MOVED line after this is that return warp).
            DeathTrace.Camera("death-cam released -> follow cam restored", "ArenaDeathCam.HoldRoutine");
            _suspendedCam = null;

            FlowTrace.Step("BattleArena", "ArenaDeathCam: linger complete - camera released.");
            IsHolding = false;
        }

        // =====================================================================
        //  WORLD CLOCK — WO-1353. This class WROTE Time.timeScale in four places (an
        //  ease-in lerp, its terminal snap, a restore and an OnDestroy safety) and was
        //  the ONLY writer that saved and restored a CAPTURED value rather than 1.00.
        //  ⚠ THAT CAPTURE IS THE SHAPE OF "A VALUE NOBODY AUTHORED": with N writers, a
        //  lerp mid-flight reads whatever another owner had just written, saves it as the
        //  scale to come back to, and restores a number that appears in no source file.
        //  It now owns a single WorldHold hold and MOVES it (SetScale) as the ease runs,
        //  so the ramp is one hold that changes rather than a stream of global writes,
        //  and there is no captured baseline to launder. Scale (KillSlowMoScale) and ease
        //  length (SlowMoEaseSeconds) are UNCHANGED.
        // =====================================================================

        private DeNelle.Core.UI.WorldHold.Handle _hold;

        /// <summary>Reason token this class's hold carries.</summary>
        private const string HoldReason = DeNelle.Core.UI.WorldHold.ReasonCosmeticPrefix + "arena-death-cam";

        private void ApplySlowMo()
        {
            if (_slowMoApplied) return;
            _savedTimeScale = Time.timeScale;
            _slowMoApplied = true;

            // The hold opens at the CURRENT effective scale so the ease-in starts from where the
            // world already is (no hard snap) and then walks to KillSlowMoScale. The ceiling is the
            // linger length plus generous margin: the cam holds for `seconds`, not milliseconds.
            _hold = DeNelle.Core.UI.WorldHold.AcquireScale(HoldReason, Mathf.Max(0.0001f, _savedTimeScale),
                                                           DeathCamHoldMaxSeconds);
            StartCoroutine(EaseTimeScale(_savedTimeScale, KillSlowMoScale, SlowMoEaseSeconds));
            FlowTrace.Step("BattleArena",
                $"ArenaDeathCam: kill slow-mo engaged - world hold '{HoldReason}' taken, easing " +
                $"{_savedTimeScale:F2} -> {KillSlowMoScale:F2} over {SlowMoEaseSeconds:F2}s unscaled.");
        }

        /// <summary>Unscaled ceiling on the death-cam hold. The linger is a couple of seconds; this
        /// is the watchdog's last resort if the cam host dies mid-hold.</summary>
        private const float DeathCamHoldMaxSeconds = 15f;

        private void RestoreSlowMo()
        {
            if (!_slowMoApplied) return;
            _slowMoApplied = false;
            ReleaseHold("death-cam linger complete");
        }

        /// <summary>
        /// The ONE step-out. Every exit of this class routes here: the normal linger end, a
        /// teardown mid-hold, and a disable. Idempotent.
        /// </summary>
        private void ReleaseHold(string why)
        {
            var hold = _hold;
            _hold = null;
            if (hold == null) return;
            hold.Dispose();
            FlowTrace.Step("BattleArena",
                $"ArenaDeathCam: world hold released - {why}. Live holds now " +
                $"[{DeNelle.Core.UI.WorldHold.Describe()}], timeScale {Time.timeScale:F2}.");
        }

        private IEnumerator EaseTimeScale(float from, float to, float dur)
        {
            float e = 0f;
            while (e < dur && _slowMoApplied && _hold != null && _hold.IsHeld)
            {
                e += Time.unscaledDeltaTime;
                DeNelle.Core.UI.WorldHold.SetScale(_hold, Mathf.Lerp(from, to, Mathf.Clamp01(e / dur)));
                yield return null;
            }
            if (_slowMoApplied && _hold != null && _hold.IsHeld)
                DeNelle.Core.UI.WorldHold.SetScale(_hold, to);
        }

        private void OnDisable()
        {
            // A coroutine dies on deactivation and OnDestroy does NOT fire for it, so a mid-ease
            // SetActive(false) would otherwise leave this hold outstanding until the watchdog
            // ceiling. Same guard, same reasoning, as HitStopManager.OnDisable.
            if (_slowMoApplied || _hold != null)
            {
                _slowMoApplied = false;
                ReleaseHold("death-cam host DISABLED mid-hold; the deactivation has just killed the " +
                            "ease coroutine and OnDestroy will not fire");
            }
        }

        private void OnDestroy()
        {
            // Safety: never leave the camera suspended or the world slow if torn down mid-hold.
            if (_suspendedCam != null) { _suspendedCam.enabled = true; _suspendedCam = null; }
            if (_slowMoApplied || _hold != null)
            {
                _slowMoApplied = false;
                ReleaseHold("death-cam host DESTROYED mid-hold");
            }
        }
    }
}
