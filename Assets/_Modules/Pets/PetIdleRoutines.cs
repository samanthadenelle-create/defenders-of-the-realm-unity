// =============================================================================
// PetIdleRoutines — WO-366: cute idle routines for the companion pet / Echo.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Pets   Namespace: DeNelle.Pets
//
// WHAT IT DOES:
//   After the pet has been genuinely idle for a stretch — not moving, not
//   hunting/fighting — this occasionally plays a CUTE idle routine (sit, lie
//   down / "play dead", a little shake/wag), then returns to its normal idle.
//   The choice + the wait between routines are picked DETERMINISTICALLY by an
//   index walk + a per-instance seed (System.Random) — no UnityEngine.Random /
//   Math.random — so a scene restart is reproducible and the pack doesn't all
//   emote in lockstep.
//
// HOW IT DRIVES THE PET:
//   It reuses the pet's existing Animator (the one Pet.cs / PetEmoteController
//   already drive). It NEVER drives a parameter the controller doesn't declare:
//   every Set call is guarded by a HasParameter check cached ONCE at init (per
//   the animator-param-guard memory — unguarded SetX spams "Parameter does not
//   exist" once per frame). If NONE of the cute params exist on the resolved
//   controller, the routine self-disables and logs ONCE exactly which
//   params/clips an artist needs to author (see GAP note below) — it does not
//   fake the animation.
//
//   "Idle" is inferred the same cheap way Pet.cs does it: per-frame world
//   displacement. We also ask Pet (via the public IsAlive / HasHostileInRange /
//   Mode surface) so we never play a cute routine mid-combat or while hunting.
//
// MODULE ISOLATION: DeNelle.Pets only. Reads Pet (same assembly). No Village /
//   HUD / camera references. Fully WebGL-safe — every Animator touch is wrapped
//   in try/catch so a malformed controller can never break the pet.
//
// SELF-BOOTSTRAP: PetDeployer.SpawnPet attaches this on every spawned pet (incl.
//   the WO-360 Echo summon). Idempotent: Awake bails if a sibling already added.
//
// ── GAP / AUTHORING FLAG (read me) ───────────────────────────────────────────
//   The CUTE-IDLE clips + Animator params DO NOT EXIST yet on any shipped pet
//   controller. Today's pets resolve to one of:
//     * Resources/Pets/Pet.controller — params: Speed, Attack, Hit, Dead
//       (+ PetEmoteController adds Happy, Alert, Celebrate)
//     * Resources/Pets/<species>.controller — per-species (none shipped yet)
//     * PetClipPlayer PlayableGraph fallback (ice-wolf) — NO controller, NO
//       params at all → this routine cleanly no-ops there.
//   To make the cute routines actually PLAY, an artist must add, to the pet's
//   AnimatorController:
//       Trigger  "Sit"        + a Pet_Sit clip      (sit down, hold, stand)
//       Trigger  "LieDown"    + a Pet_LieDown clip   ("play dead" / belly flop)
//       Trigger  "Shake"      + a Pet_Shake clip     (shake / tail wag)
//   plus transitions from the locomotion idle state into each, and a return
//   transition back to idle (or an exit-time auto-return). The param NAMES below
//   (CuteParamNames) are the contract — keep them in sync with AnimatorSetup.cs.
// =============================================================================

using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>
    /// Plays occasional cute idle routines (sit / lie-down "play dead" / shake)
    /// on a pet once it has been idle for a while, then returns to normal. Every
    /// Animator drive is guarded by a cached HasParameter check; if the cute
    /// params aren't authored on the controller the component self-disables and
    /// logs which clips/params are missing. Attach on the pet root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetIdleRoutines : MonoBehaviour
    {
        // ── Tuning ──────────────────────────────────────────────────────────
        [Header("Idle gating")]
        [Tooltip("Seconds the pet must stay (roughly) still before cute idle routines may start.")]
        [SerializeField, Min(1f)] private float _idleSettleSeconds = 4f;

        [Tooltip("World units/sec under which the pet counts as 'not moving'.")]
        [SerializeField, Min(0.01f)] private float _stillSpeedThreshold = 0.15f;

        [Header("Routine cadence")]
        [Tooltip("Minimum wait between cute routines once settled.")]
        [SerializeField, Min(2f)] private float _routineIntervalMin = 8f;

        [Tooltip("Maximum wait between cute routines once settled.")]
        [SerializeField, Min(4f)] private float _routineIntervalMax = 18f;

        // ── Cute param contract (keep in sync with AnimatorSetup.cs) ─────────
        // Index order == routine order; the deterministic picker walks this list.
        private static readonly string[] CuteParamNames = { "Sit", "LieDown", "Shake" };
        private int[] _cuteHashes;       // StringToHash per name (parallel array)
        private bool[] _cuteHasParam;    // controller declares this param? (cached once)
        private int _availableCount;     // how many cute params actually exist

        // ── Runtime ─────────────────────────────────────────────────────────
        private Animator _animator;
        private Pet _pet;
        private Vector3 _lastPosition;
        private float _idleAccum;        // how long we've been continuously still+safe
        private float _nextRoutineWait;  // seconds to wait before the next routine
        private float _sinceLastRoutine; // seconds elapsed toward _nextRoutineWait
        private int _routineIndex;       // deterministic walk cursor over routines
        private System.Random _rng;      // per-instance, seeded — no UnityEngine.Random
        private bool _active;            // false when no cute params → self-disabled

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Idempotent: if a sibling instance already exists, stand down.
            var existing = GetComponents<PetIdleRoutines>();
            if (existing.Length > 1 && existing[0] != this)
            {
                enabled = false;
                return;
            }

            _pet = GetComponent<Pet>();
            // The Animator sits on the pet mesh child (same lookup Pet.cs uses).
            _animator = GetComponentInChildren<Animator>();
            _lastPosition = transform.position;

            // Deterministic seed: instance id (matches the per-pet RNG Pet.cs /
            // PetHeroLeash seed off) so a scene restart reproduces the cadence.
            _rng = new System.Random(gameObject.GetInstanceID() ^ 0x1d1e);

            CacheCuteParams();
            ResetRoutineTimer();
        }

        /// <summary>
        /// Caches which cute params the resolved controller declares. Logs ONCE
        /// (warning) exactly which params/clips are missing so the authoring gap
        /// is visible rather than a silent no-op. WebGL-safe (try/catch).
        /// </summary>
        private void CacheCuteParams()
        {
            _cuteHashes = new int[CuteParamNames.Length];
            _cuteHasParam = new bool[CuteParamNames.Length];
            for (int i = 0; i < CuteParamNames.Length; i++)
                _cuteHashes[i] = Animator.StringToHash(CuteParamNames[i]);

            _availableCount = 0;
            try
            {
                if (_animator != null && _animator.runtimeAnimatorController != null)
                {
                    var ps = _animator.parameters;
                    for (int p = 0; p < ps.Length; p++)
                    {
                        for (int c = 0; c < _cuteHashes.Length; c++)
                        {
                            if (ps[p].nameHash == _cuteHashes[c] && !_cuteHasParam[c])
                            {
                                _cuteHasParam[c] = true;
                                _availableCount++;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                // Malformed controller / WebGL edge — never let it break the pet.
                Debug.LogWarning("[PetIdleRoutines] Could not read Animator params on '" +
                                 gameObject.name + "' — cute idle routines disabled. " + e.Message);
                _availableCount = 0;
            }

            _active = _availableCount > 0;

            if (!_active)
            {
                // GAP FLAG (logged once per pet instance). Lists the exact contract
                // an artist must author so the routines can actually play.
                Debug.LogWarning(
                    "[PetIdleRoutines] No cute-idle params on '" + gameObject.name +
                    "' controller — cute routines are NO-OP (gameplay unaffected). " +
                    "TO AUTHOR: add Trigger params + clips to the pet AnimatorController: " +
                    "Sit (Pet_Sit), LieDown (Pet_LieDown / play-dead), Shake (Pet_Shake), " +
                    "with idle->routine + routine->idle transitions. See PetIdleRoutines.cs header.");
                enabled = false; // stop Update churn; nothing to drive.
            }
        }

        // ── Tick ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_active || _animator == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Cheap idle inference: per-frame world displacement (Pet moves
            // kinematically; no rigidbody velocity to read).
            float speed = (transform.position - _lastPosition).magnitude / dt;
            _lastPosition = transform.position;

            // Never emote mid-combat / hunting / downed. Pet may be null on an
            // NPC; treat "no Pet" as safe-to-idle.
            bool combatBusy = false;
            if (_pet != null)
            {
                combatBusy = !_pet.IsAlive
                             || _pet.Mode == PetMode.Defend && _pet.HasHostileInRange;
            }

            bool still = speed <= _stillSpeedThreshold && !combatBusy;

            if (!still)
            {
                // Moving / fighting — reset the settle + routine clocks so a
                // routine only fires after a fresh, uninterrupted idle stretch.
                _idleAccum = 0f;
                _sinceLastRoutine = 0f;
                return;
            }

            // Settle first; only then start counting toward the next routine.
            if (_idleAccum < _idleSettleSeconds)
            {
                _idleAccum += dt;
                return;
            }

            _sinceLastRoutine += dt;
            if (_sinceLastRoutine >= _nextRoutineWait)
            {
                PlayNextRoutine();
                _sinceLastRoutine = 0f;
                ResetRoutineTimer();
            }
        }

        // ── Routine selection (deterministic) ─────────────────────────────────

        /// <summary>
        /// Fires the next AVAILABLE cute routine, walking the routine list by
        /// index so the pet cycles through its repertoire rather than repeating
        /// one. Skips params the controller lacks. Guarded + try/catch.
        /// </summary>
        private void PlayNextRoutine()
        {
            // Walk forward to the next available cute param (cycle the cursor).
            for (int step = 0; step < _cuteHasParam.Length; step++)
            {
                _routineIndex = (_routineIndex + 1) % _cuteHasParam.Length;
                if (_cuteHasParam[_routineIndex])
                {
                    SafeSetTrigger(_cuteHashes[_routineIndex]);
                    return;
                }
            }
        }

        private void SafeSetTrigger(int hash)
        {
            try
            {
                if (_animator != null) _animator.SetTrigger(hash);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PetIdleRoutines] SetTrigger failed on '" +
                                 gameObject.name + "': " + e.Message);
                _active = false;
                enabled = false;
            }
        }

        /// <summary>
        /// Picks the next wait deterministically from the per-instance seeded RNG
        /// (NOT UnityEngine.Random / Math.random) so cadence is reproducible.
        /// </summary>
        private void ResetRoutineTimer()
        {
            float min = Mathf.Min(_routineIntervalMin, _routineIntervalMax);
            float max = Mathf.Max(_routineIntervalMin, _routineIntervalMax);
            double t = _rng != null ? _rng.NextDouble() : 0.5;
            _nextRoutineWait = min + (float)t * (max - min);
        }
    }
}
