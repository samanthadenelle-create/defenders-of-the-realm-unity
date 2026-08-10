// =============================================================================
// EnemyAuraVFX - the ONE persistent species aura an enemy may hold.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## WHAT IT IS FOR (WO-889)
//
// An enemy's ROLE should be readable before it reaches you. Registry 6d gives each
// dangerous archetype a persistent aura: the caster crackles, the necromancer
// carries a miasma, the reaper trails smoke. Until now no enemy played an aura at
// all - Aura_EnemyCaster / Aura_Necromancer / Aura_SmokeReaper existed in the enum
// and the catalog with ZERO call sites anywhere in the game.
//
// ## GREYSCALE IS THE ACCEPTANCE CHANNEL (owner is red/green colourblind)
//
// The three live auras are separated by MOTION and SHAPE, never by hue:
//   * CASTER    - high-frequency, short-lived point sparks clinging tight to the
//                 silhouette. Stochastic flicker; the only aura that twitches.
//   * NECROMANCER - a slow, wide, GROUND-HUGGING roil that spreads past the body.
//                 Low frequency, large area, no discrete points.
//   * REAPER    - trailing wisps that STREAK behind the body as it moves.
//                 Directional; reads as motion blur rather than as a field.
// Twitching-and-tight vs roiling-and-wide vs streaking-and-trailing is legible with
// all colour removed, which a red/green/purple triple is not.
//
// ## ASSIGNMENT IS A DATA READ, NOT A CREATIVE PICK
//
// Every mapping below is anchored in enemies.json (verified at source, 16 rows):
//   role "caster"      -> Aura_EnemyCaster  (hollow-acolyte, hollow-mage, orc-shaman)
//   id contains "necromancer" -> Aura_Necromancer (necromancer, orc-necromancer)
//   id contains "reaper"      -> Aura_SmokeReaper (hollow-reaper)
// The last two match the enum's OWN NAME against a real roster id; the first matches
// a real, populated role value. Nothing here invents a species relationship.
//
// DELIBERATELY UNASSIGNED: Aura_Dust. Its recipe is built and catalogued, but
// "which enemies kick up foot dust" is a creative call (the honest candidates are
// the four brutes), and this codebase already has a standing precedent for exactly
// that restraint - Enemy.SpeciesDeathVfx leaves Death_Wolf and Death_Tiefling wired
// to prefabs but unassigned rather than guess orc-or-troll. Same discipline here.
// One line in RoleAura turns it on the day the owner rules.
//
// ## THE LOOP DISCIPLINE (the half that actually matters)
//
// This is a PERSISTENT Family-A loop on a POOLED, frequently-destroyed body - the
// single most leak-prone shape in the project. So it copies HeroHpStateAura
// (commit 1534dffb) rather than inventing its own lifecycle:
//
//   * ONE handle field. Not a list - an enemy is one archetype, so "two auras at
//     once" is a state that cannot be REPRESENTED, not a bug to be avoided.
//   * EVERY exit path stops it: the culler revoking the slot, the enemy dying,
//     OnDisable (which is also the POOL DESPAWN path - EnemyPool deactivates the
//     body), OnDestroy, scene unload, and the liveness watchdog below.
//   * THE WATCHDOG matters here for a reason ArcaneAura already learned the hard
//     way: an enemy can reach a DEAD state while its GameObject stays ACTIVE (the
//     death fall / ragdoll window), so no Unity lifecycle callback fires and
//     nothing else would ever release the loop. A held loop whose owner is not
//     alive is a leak by definition, so it is stopped and reported.
//
// Worst case this component holds ONE loop, and only while it is inside the
// nearest-N ring (VfxAuraProximityCuller). See the WO-889 budget arithmetic.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Holds the single persistent species aura for one enemy, granted and revoked by
    /// <see cref="VfxAuraProximityCuller"/> and stopped on every lifecycle exit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyAuraVFX : MonoBehaviour, IProximityAura
    {
        // Retry throttle for a refused start (loop cap hit / quality gate / no manager).
        // A refusal must NOT latch: nothing is held, so the next tick retries and the
        // aura self-heals the moment a slot frees - but not at 60 attempts a second.
        private const float StartRetrySeconds = 0.75f;

        // How often the dead-owner watchdog runs. Cheap, and only while a loop is held.
        private const float LivenessCheckSeconds = 0.5f;

        private Enemy     _enemy;
        private VFXType   _type = VFXType.None;
        private VFXHandle _handle;          // THE one held loop; deliberately no second field
        private bool      _allowed = true;  // the culler's grant (permissive until told otherwise)
        private float     _nextStartAttempt;
        private float     _livenessTimer;
        private bool      _registered;

        /// <summary>The aura this enemy would hold. None when its archetype has no aura.</summary>
        public VFXType AuraType => _type;

        /// <summary>True while a real pooled loop is held. Exposed for headless verification.</summary>
        public bool IsHolding => _handle != null && _handle.IsAlive;

        // =====================================================================
        //  IProximityAura - the nearest-N seam
        // =====================================================================

        Transform IProximityAura.AuraTransform => this == null ? null : transform;

        /// <summary>
        /// Whether this enemy's OWN logic wants an aura. Kept strictly separate from the
        /// culler's grant: "I want one" and "I am allowed one" are different questions, and
        /// conflating them is how a distance cull gets mistaken for a state change.
        /// </summary>
        bool IProximityAura.WantsAura =>
            isActiveAndEnabled && _type != VFXType.None && _enemy != null && _enemy.IsAlive;

        void IProximityAura.SetAuraAllowed(bool allowed)
        {
            if (_allowed == allowed) return;
            _allowed = allowed;

            // Graceful stop, not immediate: a revoked aura is a BUDGET decision, not a
            // death, so its tail is allowed to die out naturally. An enemy popping out of
            // existence at the ring boundary would look like a bug.
            if (!allowed) StopHeld(immediate: false, "nearest-N ring revoked the slot");
        }

        // =====================================================================
        //  Lifecycle - every one of these is an EXIT PATH
        // =====================================================================

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            // A scene unload can tear down the VFXManager (and its pool) while this body
            // survives, stranding the held instance. Stop on the way out, always.
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // Resolve on every enable, not once in Awake: this body is POOLED, so the same
            // GameObject is reconfigured as a different archetype between spawns.
            _type = _enemy != null ? _enemy.SpeciesAuraVfx() : VFXType.None;

            _allowed = true;     // start permissive; the culler revokes if the ring is full
            _livenessTimer = LivenessCheckSeconds;

            if (_type != VFXType.None && !_registered)
            {
                VfxAuraProximityCuller.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Unregister();
            // Immediate: OnDisable is also the POOL DESPAWN path. A graceful stop would
            // leave a pooled VFX instance parented to a body that is about to be handed
            // out as a different enemy somewhere else in the world.
            StopHeld(immediate: true, "OnDisable / pool despawn");
        }

        private void OnDestroy()
        {
            Unregister();
            StopHeld(immediate: true, "OnDestroy");
        }

        private void OnSceneUnloaded(Scene _) => StopHeld(immediate: true, "sceneUnloaded");

        private void Update()
        {
            if (_type == VFXType.None) return;

            // Drop a handle whose host died under us (pool torn down / manager destroyed)
            // so the state machine can re-acquire rather than sit on a corpse.
            if (_handle != null && !_handle.IsAlive) _handle = null;

            if (_handle != null) { TickLiveness(); return; }

            // Not holding: start when allowed, wanted, and not inside a retry backoff.
            if (!_allowed) return;
            if (_enemy == null || !_enemy.IsAlive) return;
            if (Time.time < _nextStartAttempt) return;
            StartHeld();
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        /// <summary>
        /// WATCHDOG. An enemy can reach a DEAD state while its GameObject stays ACTIVE (the
        /// death-fall window), so neither OnDisable nor OnDestroy fires and nothing else
        /// would ever release this loop. ArcaneAura carries the same guard for the tower
        /// that breaks to an inoperable-but-active shell. A held loop with no live owner is
        /// a leaked loop slot, full stop.
        /// </summary>
        private void TickLiveness()
        {
            _livenessTimer -= Time.deltaTime;
            if (_livenessTimer > 0f) return;
            _livenessTimer = LivenessCheckSeconds;

            if (_enemy != null && _enemy.IsAlive) return;

            FlowTrace.Step("EnemyAura",
                $"'{name}' held '{_type}' loop STOPPED by the liveness watchdog: the enemy is no longer " +
                "alive but its GameObject is still active (the death-fall window fires no OnDisable/" +
                "OnDestroy). A loop with no live owner is a leaked loop slot.");
            StopHeld(immediate: false, "owner no longer alive");
        }

        private void StartHeld()
        {
            var mgr = VFXManager.Instance;
            if (mgr == null) { _nextStartAttempt = Time.time + StartRetrySeconds; return; }

            _handle = mgr.PlayAura(_type, transform);

            if (_handle == null)
            {
                // REFUSED, not latched: nothing is held, so the next Update retries.
                // VFXManager already throttle-reports the reason (loop cap / MinQuality);
                // this line says WHICH archetype lost its read, which the manager cannot.
                _nextStartAttempt = Time.time + StartRetrySeconds;
                FlowTrace.Throttle("EnemyAura", "start-refused", 2f,
                    $"'{name}': aura '{_type}' was REFUSED by VFXManager (loop cap or quality gate) " +
                    $"even though it is inside the nearest-N ring. Scene tier={VfxLoopBudget.TierName}, " +
                    $"cap={VfxLoopBudget.CurrentCap}. Retrying - the enemy's role read is missing until then.");
                return;
            }

            FlowTrace.Throttle("EnemyAura", "started", 2f,
                $"'{name}': holding '{_type}' (one slot per enemy, mutually exclusive by construction).");

            // WO-956: FACTION DRIVES PRESENTATION - an ENEMY aura must never present on
            // the green axis (owner is red/green colourblind; green is the SAFE hue - a
            // green-wrapped hostile reads as friendly). The known offender is
            // Aura_Necromancer (Lana Fog_poison, authored saturated green), but the check
            // reads the INSTANCE's authored colours rather than naming types, so any
            // future green re-pick self-detects. The override rides VfxLoopModulator, so
            // Restore() (both pool-return ends) hands the authored art back untouched.
            // Motion/shape stays authored - that is the greyscale acceptance channel.
            var mod = _handle.Modulator;
            if (mod != null && mod.BaselineReadsGreen())
            {
                mod.SetTintOverride(HostilePalette.PlaceholderEffectTint);
                FlowTrace.Step("EnemyAura",
                    $"'{name}': '{_type}' authored art is GREEN-dominant on an ENEMY - applied the " +
                    "WO-956 hostile-palette PLACEHOLDER tint (sickly violet; final hue = owner look " +
                    "pass). Shape/motion read unchanged; authored colours restore on pool return.");
            }
        }

        /// <summary>Stop and release THE held loop. Idempotent; safe with nothing held.</summary>
        private void StopHeld(bool immediate, string reason)
        {
            if (_handle == null) return;

            var type = _type;
            _handle.Stop(immediate);   // Stop restores the instance's modulation before pooling
            _handle = null;

            FlowTrace.Throttle("EnemyAura", "released", 2f,
                $"'{name}': released '{type}' loop (reason={reason}, immediate={immediate}) - loop slot returned.");
        }

        private void Unregister()
        {
            if (!_registered) return;
            VfxAuraProximityCuller.Unregister(this);
            _registered = false;
        }

        /// <summary>
        /// Re-read the archetype from the <see cref="Enemy"/> and re-sync the held loop.
        /// <para>
        /// THIS IS THE POOLED-RESPAWN CORRECTNESS STEP, and it is not optional. On a reused
        /// body the order is: EnemyPool re-activates the GameObject (OnEnable fires, reading
        /// the PREVIOUS occupant's stat block) and only THEN does Configure install the new
        /// one. Without this call a recycled body would wear the aura of whatever enemy
        /// last used that pool slot - a mage's crackle on a walker - which is the same
        /// class of silent pooled-instance contamination VfxLoopModulator exists to prevent,
        /// one level up from the particle system.
        /// </para>
        /// </summary>
        private void RefreshFromDef()
        {
            VFXType next = _enemy != null ? _enemy.SpeciesAuraVfx() : VFXType.None;
            if (next == _type) return;

            // Drop the outgoing archetype's loop before adopting the new one, so the two
            // can never both be live - the same stop-before-start rule HeroHpStateAura uses
            // to make "two auras at once" unrepresentable.
            StopHeld(immediate: true, $"archetype changed '{_type}' -> '{next}' (pooled respawn)");
            _type = next;
            _nextStartAttempt = 0f;   // let the new archetype start on the next Update

            if (_type == VFXType.None) { Unregister(); return; }
            if (!_registered)
            {
                VfxAuraProximityCuller.Register(this);
                _registered = true;
            }
        }

        /// <summary>
        /// Attach the aura driver to <paramref name="host"/> once (idempotent) AND re-sync
        /// it to the host's current stat block. Called from <see cref="Enemy.Configure"/> so
        /// every spawn path - wave, roamer, tribe, arena - picks it up from the one place
        /// that already sets the stat block, on first spawn and on every pooled reuse.
        /// </summary>
        public static EnemyAuraVFX Ensure(GameObject host)
        {
            if (host == null) return null;
            var a = host.GetComponent<EnemyAuraVFX>();
            if (a == null) a = host.AddComponent<EnemyAuraVFX>();
            a.RefreshFromDef();
            return a;
        }
    }
}
