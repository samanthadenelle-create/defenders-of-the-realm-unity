// =============================================================================
// HeroHpStateAura - the hero's HP-state world aura. THE PRIMARY low-HP tell.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## WHY THIS EXISTS (WO-888 - this is an ACCESSIBILITY FIX, not dressing)
//
// Until now the ONLY "you are about to die" signal in the game was a RED screen-edge
// vignette (HeroInjuredVignette). The owner is red/green colourblind: she cannot
// reliably see the one signal that tells her she is in danger. That is a real bug.
//
// This component carries the read on channels that survive GREYSCALE:
//
//   * PULSE RATE      - the aura's emission breathes, and the breath gets FASTER the
//                       closer the hero is to death (about 0.85 Hz at the wounded
//                       cutoff, about 3.2 Hz at empty). Rhythm has no hue.
//   * GUTTERING SHAPE - the trough of that breath gets DEEPER as HP falls, so near
//                       death the effect very nearly goes OUT between beats and snaps
//                       back, like a candle about to gutter. Simulation speed rises
//                       with it so the recovery is a snap, not a drift.
//   * RECIPE SWAP     - below the near-death cutoff the recipe itself changes from
//                       guttering smoke wisps to a fast-guttering flame (a different
//                       SHAPE, not a different colour).
//   * MOTION DIRECTION- healing RISES (calm upward steam column); the wounded states
//                       gutter and settle. Direction is the vocabulary: damage stabs
//                       in, restoration rises.
//
// The greyscale test is the acceptance criterion: with all colour removed, "how fast
// is it beating and how nearly does it go out" still answers "how close am I to dead".
//
// DELIBERATELY NOT GATED BY FeatureFlags.HeroInjuredStance. That flag governs the
// injured LOCOMOTION stance and the (now secondary) red vignette. WO-888's acceptance
// criterion is literally "low-HP is legible with the red vignette DISABLED", so the
// primary survival read must not sit behind the switch that turns the vignette off.
//
// ## THE OTHER HALF: A LOOP MUST HAVE AN OWNER THAT STOPS IT
//
// These are PERSISTENT Family-A loops. A loop played fire-and-forget permanently
// consumes one of the 20 global slots (VFXManager._maxActiveLoops) and every later
// aura in the session is silently dropped. So:
//
//   * There is exactly ONE handle field. Not a list, not a dictionary - ONE. The HP
//     states are therefore MUTUALLY EXCLUSIVE by CONSTRUCTION: there is no second
//     field in which a second HP loop could be held, so "low health and near death
//     both running" is not a bug that can occur, it is a state that cannot be
//     represented. Apply() stops the held loop before it starts the next one.
//   * EVERY exit path stops it: state change (Apply), healed above the cutoff
//     (Drive -> Slot.None), death (Drive with alive=false), the driver going silent
//     (the watchdog in Update), OnDisable, OnDestroy, and scene unload.
//   * The watchdog matters: this component is DRIVEN by HeroHealth. If HeroHealth is
//     disabled, destroyed or simply stops calling Drive, nothing else would ever stop
//     the loop. So a held loop with no Drive call for HoldWithoutDriveSeconds is
//     stopped and reported - a loop with no live owner is a leak by definition.
//
// Worst case this component holds ONE loop. Ever.
//
// Pattern source: DragonBoss.FireBreath (commit 7f3971a3) - timer-driven hold that
// stops on completion, re-entry, death and disable - and ArcaneAura's handle discipline.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The single HP-driven world aura slot on the hero: wounded / near-death / healing,
    /// exactly one at a time, driven by <see cref="HeroHealth"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroHpStateAura : MonoBehaviour
    {
        /// <summary>Which single HP read is live. There is no combination - see the header.</summary>
        public enum Slot
        {
            /// <summary>Nothing held (healthy, or dead - the death beat owns that moment).</summary>
            None,
            /// <summary>Regen is running: a calm RISING column. The only non-danger read.</summary>
            Healing,
            /// <summary>Below the wounded cutoff: guttering wisps, breath accelerating with severity.</summary>
            LowHealth,
            /// <summary>Below the near-death cutoff: fast candle-gutter. Never runs with LowHealth.</summary>
            NearDeath,
        }

        // =====================================================================
        //  TUNABLE BONES - every number below is felt-tunable by the owner.
        //  They encode the READ, so change them for feel, never for hue.
        // =====================================================================

        // -- Pulse rate: the primary severity channel. Hz at the wounded cutoff -> Hz at empty.
        // The calm end deliberately sits near a resting heartbeat and the panic end near a
        // sprinting one; the ACCELERATION between them is what the player feels.
        private const float PulseHzCalm  = 0.85f;
        private const float PulseHzPanic = 3.2f;

        // -- Guttering depth: how far the breath's TROUGH falls below the authored density.
        // At the cutoff the effect only thins (0.55); near death it very nearly goes OUT
        // (0.10) between beats. That near-extinction IS the "about to gutter" read.
        private const float TroughCalm  = 0.55f;
        private const float TroughPanic = 0.10f;

        // -- Crest: how far the breath's peak rises above the authored density. A deeper
        // trough with a higher crest is a bigger swing, i.e. a louder rhythm, at the same hue.
        private const float CrestCalm  = 1.15f;
        private const float CrestPanic = 2.40f;

        // -- Simulation speed at maximum severity. Makes the recovery a SNAP rather than a
        // drift, which is the difference between "smouldering" and "guttering".
        private const float SimSpeedPanic = 1.7f;

        // -- Body-seating scale multipliers, applied to each recipe's AUTHORED scale.
        // MEASURED off the committed prefabs (2026-08-05):
        //   Aura_LowHealth          SmokeEffect  root scale 0.70, startSize 5, lifetime 5 s
        //   Aura_NearDeath          TinyFlames   root scale 0.55, startSize 0.5, lifetime 1.4 s
        //   Aura_HealingInProgress  RisingSteam  root scale 1.25 (!), startSize 0.5, lifetime 10 s
        // The two heal recipes did NOT receive the builder's intended 0.8 / 0.5 scale: the
        // builder only applies Row.Scale to a copy whose scale is still 1, and the pack's
        // RisingSteam ships at 1.25, so both were reported "PRESERVED (already tuned)" and
        // stayed room-sized. These multipliers seat them on a BODY instead, which also serves
        // the landscape-phone rule (2670x1200 - a tall column spends the scarce axis and crops).
        private const float ScaleMulLowHealth = 0.60f;   // -> ~0.42 effective; a body haze, not a room of smoke
        private const float ScaleMulNearDeath = 1.00f;   // 0.55 authored is already body-sized
        private const float ScaleMulHealing   = 0.50f;   // -> ~0.63 effective (the intended "low" column)

        // -- Regen hold. RegenTick is called EVERY FRAME by SafeZoneRecovery while the hero
        // stands in the town footprint, so the healing read cannot be started per call. Each
        // call instead stamps a short keep-alive; the loop stops on its own once the stamp
        // lapses. That makes "regen stopped" a guaranteed stop with no extra call site.
        private const float RegenHoldSeconds = 0.6f;

        // -- Watchdog. If the driver (HeroHealth) stops calling Drive for this long while a
        // loop is held, the loop has no live owner and is stopped. See the header.
        private const float HoldWithoutDriveSeconds = 1.0f;

        // -- Retry throttle for a refused start (loop cap hit / quality gate / no manager).
        // A refusal must NOT latch: the state stays None so it retries and self-heals the
        // moment a slot frees, but not at 60 attempts a second.
        private const float StartRetrySeconds = 0.5f;

        // =====================================================================
        //  State - ONE slot, ONE handle. The mutual exclusion is this shape.
        // =====================================================================

        private Slot      _slot;      // what is currently HELD (None whenever _handle is null)
        private VFXHandle _handle;    // THE one held loop. There is deliberately no second field.
        private float     _phase;     // breath accumulator (radians)
        private float     _regenUntil;
        private float     _lastDriveTime;
        private float     _nextStartAttempt;
        private int       _lastPhaseFrame = -1;   // see DriveSeverity: advance the breath ONCE per frame

        /// <summary>The HP read currently held. Exposed for headless verification / tests.</summary>
        public Slot Current => _slot;

        /// <summary>True while a real pooled loop is held (false when a start was refused).</summary>
        public bool IsHolding => _handle != null && _handle.IsAlive;

        // =====================================================================
        //  Lifecycle - every one of these is an EXIT PATH and stops the loop.
        // =====================================================================

        private void OnEnable()
        {
            // A scene unload can tear down the VFXManager (and its pool) while this hero
            // survives, stranding the held instance. Stop on the way out, always.
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _lastDriveTime = Time.time;   // do not let the watchdog fire before the first Drive
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            StopHeld(immediate: true, "OnDisable");
        }

        private void OnDestroy() => StopHeld(immediate: true, "OnDestroy");

        private void OnSceneUnloaded(Scene _) => StopHeld(immediate: true, "sceneUnloaded");

        private void Update()
        {
            // WATCHDOG. Drive() is called by HeroHealth every frame. If it stops (HeroHealth
            // disabled / destroyed / an exception upstream) nothing else would ever release
            // this loop, and one of the 20 global slots would be gone for the session.
            if (_handle == null) return;
            if (Time.time - _lastDriveTime < HoldWithoutDriveSeconds) return;

            FlowTrace.Fail("HeroHpAura",
                $"held '{_slot}' loop STOPPED by watchdog: no Drive() call for " +
                $"{HoldWithoutDriveSeconds:0.0}s - the HP driver went silent (HeroHealth disabled/destroyed?). " +
                "A loop with no live owner is a leaked loop slot.");
            StopHeld(immediate: true, "watchdog");
        }

        // =====================================================================
        //  Public API - the seams HeroHealth calls
        // =====================================================================

        /// <summary>
        /// Stamp "regen is running right now". Called from <see cref="HeroHealth.RegenTick"/>
        /// (every frame while in the town footprint) and from the discrete heal paths, so the
        /// rising healing read appears while restoration is happening and stops on its own
        /// <see cref="RegenHoldSeconds"/> after it ends. Cheap: one float write.
        /// </summary>
        public void NotifyRegen() => _regenUntil = Time.time + RegenHoldSeconds;

        /// <summary>
        /// The single per-frame drive, called from HeroHealth.UpdateInjuredState
        /// (which runs off the one HP source of truth, alive or dead). Picks the ONE read that
        /// applies, swaps the held loop if it changed, and drives the severity pulse.
        /// </summary>
        /// <param name="alive">False when HP has reached zero - the death beat owns that moment.</param>
        /// <param name="fraction">HP fraction, 0..1.</param>
        public void Drive(bool alive, float fraction)
        {
            _lastDriveTime = Time.time;

            Slot want = Resolve(alive, fraction);
            Apply(want);

            // Severity is only meaningful for the two DANGER reads; the healing column is
            // deliberately steady (a calm rise is the opposite signal to an urgent gutter).
            if (_slot == Slot.LowHealth || _slot == Slot.NearDeath) DriveSeverity(fraction);
        }

        /// <summary>
        /// External teardown - stop whatever is held, now. Called from the hero's death path
        /// so the aura is gone on the same frame the death burst plays, without waiting for
        /// the next Drive. Safe to call when nothing is held.
        /// </summary>
        public void StopAll() => StopHeld(immediate: true, "StopAll");

        // =====================================================================
        //  Internals
        // =====================================================================

        /// <summary>
        /// PRIORITY IS THE WHOLE RULING: a DANGER read always outranks a comfort read.
        /// Near-death beats wounded beats healing. Regenerating at 8% HP must never replace
        /// the "you are about to die" pulse with a calm rising column - the heal still reads
        /// through its own contact burst and the HP number, but the survival signal owns the
        /// world aura. This ordering is also what keeps ONE slot sufficient.
        /// </summary>
        private Slot Resolve(bool alive, float fraction)
        {
            if (!alive) return Slot.None;
            if (fraction < HeroHealth.NearDeathFraction) return Slot.NearDeath;
            if (fraction < HeroHealth.InjuredFraction)   return Slot.LowHealth;
            if (Time.time < _regenUntil && fraction < 1f) return Slot.Healing;
            return Slot.None;
        }

        /// <summary>
        /// Swap the ONE held loop to <paramref name="want"/>. Stops before it starts, always -
        /// which is why two HP auras can never be live at once.
        /// </summary>
        private void Apply(Slot want)
        {
            // A handle whose host died under us (pool torn down / manager destroyed) reads as
            // not-alive: drop it so the state machine can re-acquire rather than sit on a corpse.
            if (_handle != null && !_handle.IsAlive) { _handle = null; _slot = Slot.None; }

            if (want == _slot && (_slot == Slot.None || _handle != null)) return;

            StopHeld(immediate: false, "state change -> " + want);   // graceful: let the tail die out
            _slot = Slot.None;

            if (want == Slot.None) return;

            // Do not hammer the manager when a start is being refused (cap hit / quality gate).
            if (Time.time < _nextStartAttempt) return;

            var mgr = VFXManager.Instance;
            if (mgr == null)
            {
                _nextStartAttempt = Time.time + StartRetrySeconds;
                return;
            }

            // Seated at the hero ROOT (feet), parented so it tracks. Body-hugging on purpose:
            // the phone is landscape, so an aura that grows upward is the one that crops.
            VFXType type = TypeFor(want);
            _handle = mgr.PlayAura(type, transform);

            if (_handle == null)
            {
                // REFUSED, not latched: _slot stays None so the next Drive retries. VFXManager
                // already throttle-reports the reason (loop cap / MinQuality); this line says
                // WHICH survival read was the casualty, which the manager's message cannot.
                _nextStartAttempt = Time.time + StartRetrySeconds;
                FlowTrace.Throttle("HeroHpAura", "start-refused", 2f,
                    $"'{want}' aura ('{type}') was REFUSED by VFXManager (loop cap or quality gate). " +
                    "This is the PRIMARY colourblind low-HP read - if it is being dropped, the hero " +
                    "has no non-colour danger signal. Retrying.");
                return;
            }

            _slot  = want;
            _phase = 0f;   // every entry starts on the beat, so the rhythm reads immediately

            // Seat the recipe onto a body (see the ScaleMul* measurements above) and clear any
            // stale modulation from this pooled instance's previous owner.
            var mod = _handle.Modulator;
            if (mod != null)
            {
                mod.SetScaleMul(ScaleMulFor(want));
                mod.SetSimulationSpeed(1f);
                mod.SetEmissionScale(1f);
            }

            FlowTrace.Step("HeroHpAura",
                $"HELD '{want}' -> '{type}' (one slot, mutually exclusive; scaleMul={ScaleMulFor(want):0.00}).");
        }

        /// <summary>
        /// The colour-free severity read. Everything here is rhythm and shape:
        /// faster breath + deeper trough + faster simulation the closer to death.
        /// </summary>
        private void DriveSeverity(float fraction)
        {
            var mod = _handle != null ? _handle.Modulator : null;
            if (mod == null) return;

            // 0 at the wounded cutoff, 1 at empty. Same expression the (now secondary) vignette
            // uses, so the two cues escalate together for players who CAN see the red -
            // redundancy is good accessibility; colour-ONLY was the bug.
            float sev = Mathf.InverseLerp(HeroHealth.InjuredFraction, 0f, fraction);

            // ONE phase advance per FRAME, not per call. UpdateInjuredState is called from Update
            // AND from Heal / RegenTick / RestoreToFull / Respawn, so a frame in which the hero is
            // also healed would otherwise advance the breath twice and make the pulse rate - the
            // primary severity channel - depend on how many times HP happened to change. The read
            // has to mean HP, and nothing else.
            float hz = Mathf.Lerp(PulseHzCalm, PulseHzPanic, sev);
            if (_lastPhaseFrame != Time.frameCount)
            {
                _lastPhaseFrame = Time.frameCount;
                _phase += Time.deltaTime * hz * Mathf.PI * 2f;
                if (_phase > Mathf.PI * 2f) _phase -= Mathf.PI * 2f;   // keep the accumulator small
            }

            float breath01 = 0.5f + 0.5f * Mathf.Sin(_phase);
            float trough   = Mathf.Lerp(TroughCalm, TroughPanic, sev);
            float crest    = Mathf.Lerp(CrestCalm,  CrestPanic,  sev);

            mod.SetEmissionScale(Mathf.Lerp(trough, crest, breath01));
            mod.SetSimulationSpeed(Mathf.Lerp(1f, SimSpeedPanic, sev));
        }

        /// <summary>Stop and release THE held loop. Idempotent; safe with nothing held.</summary>
        private void StopHeld(bool immediate, string reason)
        {
            if (_handle == null) { _slot = Slot.None; return; }

            var slot = _slot;
            _handle.Stop(immediate);   // Stop restores the instance's modulation before pooling
            _handle = null;
            _slot   = Slot.None;

            FlowTrace.Step("HeroHpAura",
                $"released '{slot}' loop (reason={reason}, immediate={immediate}) - loop slot returned.");
        }

        /// <summary>
        /// Slot -> the landed VFXType. Reference values only; the enum append is Grok's
        /// single-owner edit (WO-884 section 0.2) and these four landed on 2026-08-05.
        /// Aura_LowHealth and Aura_NearDeath are catalogued at MinQuality 0 on purpose - a
        /// survival read that vanishes on a low-end device reintroduces the very bug it exists
        /// to fix (VFXCatalogGenerator.Map, verified at source).
        /// </summary>
        private static VFXType TypeFor(Slot slot)
        {
            switch (slot)
            {
                case Slot.NearDeath: return VFXType.Aura_NearDeath;
                case Slot.LowHealth: return VFXType.Aura_LowHealth;
                case Slot.Healing:   return VFXType.Aura_HealingInProgress;
                default:             return VFXType.None;
            }
        }

        private static float ScaleMulFor(Slot slot)
        {
            switch (slot)
            {
                case Slot.NearDeath: return ScaleMulNearDeath;
                case Slot.LowHealth: return ScaleMulLowHealth;
                case Slot.Healing:   return ScaleMulHealing;
                default:             return 1f;
            }
        }

        /// <summary>Attach the aura driver to <paramref name="host"/> once (idempotent).</summary>
        public static HeroHpStateAura Ensure(GameObject host)
        {
            if (host == null) return null;
            var a = host.GetComponent<HeroHpStateAura>();
            if (a == null) a = host.AddComponent<HeroHpStateAura>();
            return a;
        }
    }
}
