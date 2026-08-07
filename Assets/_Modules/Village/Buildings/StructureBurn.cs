// =============================================================================
// StructureBurn - the ONE owner of a burning structure's damage-over-time + fire
// VFX (WO-761).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER SPEC (2026-07-24): when FIRE brings a structure to <=50% HP it CATCHES
// FIRE and keeps taking damage over time UNTIL it is REPAIRED (extinguish) or
// DESTROYED. The lingering burn only kicks in once a structure is critically
// damaged (<=50% HP) - not every fire hit burns forever. Burn does NOT
// self-expire; only repair or death stops it.
//
// DESIGN (one owner, minimal, reuse):
//   * IGNITE is driven from the fire-damage site via the static TryIgniteFromFire
//     seam: the damage is applied first, then the caller offers the target here.
//     Only a structure that is alive AND at/below the ignite fraction ignites, so
//     a fire hit that leaves a structure above 50% never ignites, and NON-FIRE
//     callers never touch this class at all (fail-safe: ignite is opt-in per hit).
//   * TICK: while burning, ApplyContactDamage(maxHp * fractionPerSecond * dt) is
//     dealt on an interval through the structure's OWN IDamageableStructure seam -
//     the same path enemy siege uses (death / break handled by the structure).
//     Percent-of-max keeps the burn fair across the 100..240 HP structure range.
//   * EXTINGUISH = REPAIR, self-detected: burn only ever LOWERS the HP fraction,
//     so any UPWARD move of the fraction back above the ignite line is a repair.
//     This catches EVERY repair path (single repair, Repair-All, rebuild, future
//     paths) with no hook into WallRepairController - one owner, zero coupling.
//   * DESTROY: if a tick kills the structure, the fire VFX is Stop()'d and the
//     Destructible lifecycle (WO-753) is nudged so no VFX outlives the shell.
//   * VISUAL: a LOOPING fire VFX (owner-tagged Hovl key "BurningStructure_Aura" -
//     shape + upward motion, colourblind-safe: not hue alone) parented to the
//     structure through the SINGLE VFXManager pool, held by ONE handle, stopped on
//     extinguish / death / disable / destroy - no second stack, no orphaned loop. A
//     one-shot "BurningStructure_Impact" flare fires the moment it ignites (captured +
//     timed-stopped since the catalog authors that key as a loop) - no leaked slot.
//   * STACK = REFRESH: re-igniting a burning structure resets the tick cadence but
//     never stacks a second burn or a second VFX handle.
//
// Instrumented [Flow:StructureBurn] per CLAUDE.md section 12. Null-safe throughout.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// A lingering fire on one critically-damaged structure (WO-761): ticks
    /// percent-of-max damage over time and shows a looping fire VFX until the
    /// structure is repaired (extinguish) or destroyed. Composed on demand by
    /// <see cref="TryIgniteFromFire"/>; there is at most one per structure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StructureBurn : MonoBehaviour
    {
        // -- Tunables (DATA/serialized - balance lives here) -------------------

        [Header("Ignite / extinguish")]
        [Tooltip("A structure ignites (and stays burning) only at/below this HP fraction. " +
                 "Owner spec: 0.5 = catch fire at 50% damage. A repair back above this line extinguishes it.")]
        [SerializeField, Range(0.05f, 0.95f)] private float _igniteHpFraction = 0.5f;

        [Header("Damage over time")]
        [Tooltip("Burn damage per second as a fraction of the structure's MAX HP. " +
                 "Default 0.02 = 2%/sec: a structure burns from 50% to 0 in ~25s, a felt " +
                 "'rush to save it' window that is fair across the 100..240 HP structure range.")]
        [SerializeField, Range(0.002f, 0.2f)] private float _burnFractionPerSecond = 0.02f;

        [Tooltip("Seconds between burn ticks. Smaller = smoother HP drain + VFX cadence; " +
                 "the per-tick damage scales with this so the DPS stays constant.")]
        [SerializeField, Min(0.1f)] private float _tickInterval = 0.5f;

        [Header("Fire VFX (single VFXManager Hovl pool, handle-managed loop)")]
        [Tooltip("Owner-tagged looping fire effect (Hovl catalog key) shown on a burning structure. " +
                 "Reads by shape + upward motion - colourblind-safe, not hue alone.")]
        [SerializeField] private string _fireVfxKey = "BurningStructure_Aura";

        [Tooltip("Owner-tagged one-shot fire flare fired the MOMENT the structure ignites. " +
                 "Authored as a loop in the catalog, so it is captured + timed-stopped (no leak).")]
        [SerializeField] private string _igniteImpactKey = "BurningStructure_Impact";

        [Tooltip("Seconds the ignite flare plays before it is stopped and returned to the pool.")]
        [SerializeField, Min(0.2f)] private float _igniteImpactSeconds = 1.4f;

        [Tooltip("Vertical offset (world units) for the fire loop so the flame sits on the structure body.")]
        [SerializeField] private float _fireVfxYOffset = 0.6f;

        // Margin above the ignite line that counts as 'repaired' (guards float noise).
        private const float ExtinguishMargin = 0.02f;

        // -- Runtime state (set via TryIgniteFromFire / Configure) -------------

        private IDamageableStructure _structure;   // IsAlive + ApplyContactDamage seam
        private Func<float> _hpFraction;           // 0..1 live health fraction of the structure
        private float _maxHp = 100f;               // for percent-of-max tick sizing
        private bool _burning;
        private float _tickTimer;
        private float _lastFraction = 1f;
        private VFXHandle _fireHandle;
        private Transform _vfxAnchor;

        /// <summary>True while this structure is actively burning.</summary>
        public bool IsBurning => _burning;

        // =====================================================================
        //  Static ignite seam - the ONE line a fire-damage site calls
        // =====================================================================

        /// <summary>
        /// FIRE-source ignite: offered AFTER the fire damage is applied. Resolves the
        /// structure under <paramref name="targetTf"/> and, if it is alive and now
        /// at/below the ignite fraction (<=50% HP by default), ensures a
        /// <see cref="StructureBurn"/> on it and ignites (or REFRESHES) the burn.
        /// A structure still above the threshold, or an unrecognised / dead target, is
        /// a safe no-op - so a fire hit only ignites a critically-damaged structure and
        /// non-fire callers (which never call this) can never ignite anything.
        /// Returns true when a burn was started or refreshed.
        /// </summary>
        public static bool TryIgniteFromFire(Transform targetTf)
        {
            if (targetTf == null) return false;

            bool started = false;
            Guard.Try("StructureBurn", "ignite from fire", () =>
            {
                if (!TryResolve(targetTf, out var root, out var str, out var frac, out var maxHp))
                    return;
                if (root == null || str == null || !str.IsAlive) return;

                float f = frac();
                if (f > DefaultIgniteFraction()) return;   // not critically damaged yet - no ignite

                var burn = root.GetComponent<StructureBurn>();
                if (burn == null) burn = root.AddComponent<StructureBurn>();
                burn.Configure(str, frac, maxHp);
                burn.Ignite();
                started = true;
            });
            return started;
        }

        // The static gate uses the component default (0.5). Kept in one place so the
        // gate and the per-instance extinguish threshold stay consistent.
        private static float DefaultIgniteFraction() => 0.5f;

        // =====================================================================
        //  Configure + ignite (also the test seam)
        // =====================================================================

        /// <summary>
        /// Wire this burn to a structure. Idempotent - safe to call again on a
        /// refresh (a re-ignite of an already-burning structure). The production path
        /// is <see cref="TryIgniteFromFire"/>; this is public so the Editor regression
        /// (a separate assembly) can drive it directly with a stub structure - it does
        /// NOT change runtime behaviour, it only wires the burn's target.
        /// </summary>
        public void Configure(IDamageableStructure structure, Func<float> hpFraction, float maxHp)
        {
            _structure = structure;
            _hpFraction = hpFraction;
            _maxHp = Mathf.Max(1f, maxHp);
        }

        /// <summary>
        /// Light the fire (or REFRESH it if already burning - never stacks a second
        /// burn or a second VFX handle). Resets the tick cadence and starts the
        /// looping fire VFX on first ignite.
        /// </summary>
        public void Ignite()
        {
            _tickTimer = _tickInterval;
            _lastFraction = _hpFraction != null ? _hpFraction() : 1f;

            if (_burning)
            {
                FlowTrace.Throttle("StructureBurn", $"refresh:{GetInstanceID()}", 1f,
                    $"[Flow:StructureBurn] '{name}' re-ignited while burning - refreshed (no stack).");
                return;
            }

            _burning = true;
            StartFireVfx();
            FireIgniteImpact();   // one-shot fire flare on the FIRST ignite only (refresh returns above)
            FlowTrace.Step("StructureBurn",
                $"[Flow:StructureBurn] '{name}' IGNITED at {_lastFraction:P0} HP - burning " +
                $"{_burnFractionPerSecond:P0}/s of {_maxHp:0} max until repaired or destroyed.");
        }

        // =====================================================================
        //  Tick (Update in play; TickForTest in edit-mode regression)
        // =====================================================================

        private void Update()
        {
            // TOWN SUSPENSION (owner ruling 2026-08-07): a burning TOWN structure must not
            // keep losing HP while the player is active but elsewhere - a building lost to a
            // fire the player could not see or reach is exactly the damage the ruling exists
            // to stop. The burn is HELD, not cancelled: _burning stays true and the fire VFX
            // keeps showing, so the structure is still visibly in trouble when they return.
            //
            // SuspendedFor(this) carries the active-scene exemption, so a burning structure
            // inside the dungeon the player is standing in keeps burning normally. This is a
            // deliberate per-system hold and NOT Time.timeScale for exactly that reason.
            //
            // Deliberately NOT gated on the extinguish check either: holding the tick holds
            // the whole burn, and a repair performed on return is detected on the next
            // un-suspended tick by the same upward-fraction test as always.
            if (TownSuspension.SuspendedFor(this)) return;

            if (_burning) TickBurn(Time.deltaTime);
        }

        /// <summary>
        /// Edit-mode test seam: advance one burn step of <paramref name="dt"/> seconds
        /// synchronously (no coroutine / real time). Public so the Editor regression (a
        /// separate assembly) can drive ignite->tick->repair->death deterministically.
        /// Runs the SAME tick body <see cref="Update"/> calls - no runtime behaviour change.
        /// </summary>
        public void TickForTest(float dt) => TickBurn(dt);

        private void TickBurn(float dt)
        {
            // Structure gone / destroyed by any path -> stop cleanly (death branch).
            if (_structure == null || !_structure.IsAlive)
            {
                OnStructureDown("target-down");
                return;
            }

            float f = _hpFraction != null ? _hpFraction() : 1f;

            // EXTINGUISH = repair. Burn only lowers HP, so an upward move of the
            // fraction back above the ignite line (or a jump above the last seen
            // value) means the player repaired the structure.
            if (f > _igniteHpFraction + ExtinguishMargin || f > _lastFraction + ExtinguishMargin)
            {
                Extinguish("repaired");
                return;
            }

            _tickTimer -= dt;
            if (_tickTimer > 0f) { _lastFraction = f; return; }
            _tickTimer = _tickInterval;

            float dmg = Mathf.Max(0.01f, _maxHp * _burnFractionPerSecond * _tickInterval);
            Guard.Try("StructureBurn", "apply burn tick", () => _structure.ApplyContactDamage(dmg));

            FlowTrace.Throttle("StructureBurn", $"tick:{GetInstanceID()}", 1f,
                $"[Flow:StructureBurn] '{name}' burns for {dmg:0.#} ({_burnFractionPerSecond:P0}/s of {_maxHp:0}).");

            // Did that tick bring it down? Handle the death branch immediately.
            if (_structure == null || !_structure.IsAlive)
            {
                OnStructureDown("burned-down");
                return;
            }

            _lastFraction = _hpFraction != null ? _hpFraction() : f;
        }

        // =====================================================================
        //  End states - extinguish (repair) and death (destroy)
        // =====================================================================

        /// <summary>Put the fire out (repair). Clears the burn + stops the fire VFX; the
        /// component stays composed (dormant) so a later re-ignite reuses it.</summary>
        private void Extinguish(string reason)
        {
            if (!_burning) return;
            _burning = false;
            StopFireVfx();
            FlowTrace.Step("StructureBurn",
                $"[Flow:StructureBurn] '{name}' EXTINGUISHED ({reason}) - burn cleared, fire VFX stopped.");
        }

        /// <summary>The structure died/broke (burn tick or any other path). Stop the fire VFX
        /// and nudge the Destructible lifecycle so no VFX outlives the shell (WO-753).</summary>
        private void OnStructureDown(string reason)
        {
            _burning = false;
            StopFireVfx();
            // Belt-and-suspenders VFX teardown: our own handle is already stopped above;
            // this tears down any OTHER effects the structure owns on the same death beat.
            Destructible.For(gameObject)?.NotifyBroken("structure-burn:" + reason);
            FlowTrace.Step("StructureBurn",
                $"[Flow:StructureBurn] '{name}' burn ended - structure DOWN ({reason}); fire VFX + Destructible torn down.");
        }

        // =====================================================================
        //  Fire VFX - single VFXManager pool, ONE handle
        // =====================================================================

        private void StartFireVfx()
        {
            if (_fireHandle != null && _fireHandle.IsAlive) return;   // already showing (refresh)

            EnsureVfxAnchor();
            var mgr = VFXManager.Instance;
            if (mgr == null || _vfxAnchor == null) return;            // null-safe (e.g. edit-mode)

            // Owner-tagged looping fire (BurningStructure_Aura) via the ONE VFXManager Hovl pool,
            // parented to the anchor so the flame tracks the structure body. Held by one handle,
            // stopped on extinguish / death / disable / destroy - no second stack, no orphan.
            _fireHandle = VFXManager.PlayKey(
                _fireVfxKey, _vfxAnchor.position, Quaternion.identity, _vfxAnchor);
            // Register with the Destructible owner so ANY removal path also returns the
            // loop to its pool (no orphaned loop - the loop-cap starvation class).
            if (_fireHandle != null)
                Destructible.For(gameObject)?.RegisterHandle(_fireHandle);
        }

        /// <summary>
        /// Fire the owner-tagged one-shot fire flare (BurningStructure_Impact) at the moment of
        /// ignite so the structure visibly CATCHES fire. The catalog authors this key as a LOOP
        /// (it shares the burn-aura prefab), so PlayKey returns a handle instead of auto-returning;
        /// we capture it, parent it to the anchor (so a host destroyed mid-flare is reclaimed by the
        /// VFXManager loop sweep), register it with the Destructible owner, and timed-Stop it after a
        /// short flare - a burst-then-fade with NO leaked loop slot. Null-safe throughout.
        /// </summary>
        private void FireIgniteImpact()
        {
            EnsureVfxAnchor();
            var mgr = VFXManager.Instance;
            if (mgr == null || _vfxAnchor == null) return;   // null-safe (edit-mode / no catalog)

            var impact = VFXManager.PlayKey(
                _igniteImpactKey, _vfxAnchor.position, Quaternion.identity, _vfxAnchor, default, 1.3f);
            if (impact == null) return;   // missing key / cap hit -> safe no-op

            Destructible.For(gameObject)?.RegisterHandle(impact);   // torn down if the shell dies first
            StartCoroutine(StopHandleAfter(impact, Mathf.Max(0.2f, _igniteImpactSeconds)));
        }

        private System.Collections.IEnumerator StopHandleAfter(VFXHandle handle, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (handle != null) handle.Stop();
        }

        private void StopFireVfx()
        {
            if (_fireHandle != null)
            {
                _fireHandle.Stop();   // safe on a dead handle; returns the loop to its pool
                _fireHandle = null;
            }
        }

        private void EnsureVfxAnchor()
        {
            if (_vfxAnchor != null) return;
            var go = new GameObject("[StructureBurnFire]");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _fireVfxYOffset, 0f);
            _vfxAnchor = go.transform;
        }

        // Never leak the loop if the component/structure is disabled or destroyed.
        private void OnDisable() => StopFireVfx();
        private void OnDestroy() => StopFireVfx();

        // =====================================================================
        //  Structure resolver - the one place that maps a hit transform to the
        //  concrete burnable structure + its HP fraction + max HP.
        // =====================================================================

        /// <summary>
        /// Resolve the burnable structure under <paramref name="tf"/>. Matches the
        /// concrete Village structure types (towers / buildings / gates / walls) and
        /// exposes each as a uniform (IsAlive+ApplyContactDamage, hpFraction, maxHp)
        /// triple. The Heart of Elarion is intentionally NOT burnable (it is the
        /// finale target, not a repairable perimeter structure). Returns false for
        /// anything unrecognised.
        /// </summary>
        private static bool TryResolve(Transform tf, out GameObject root,
            out IDamageableStructure str, out Func<float> frac, out float maxHp)
        {
            root = null; str = null; frac = null; maxHp = 100f;
            if (tf == null) return false;

            var tower = tf.GetComponentInParent<Tower>();
            if (tower != null)
            {
                root = tower.gameObject; str = tower; maxHp = tower.MaxHp;
                frac = () => tower.HpFraction; return true;
            }

            var dt = tf.GetComponentInParent<DefenseTower>();
            if (dt != null)
            {
                root = dt.gameObject; str = dt; maxHp = dt.MaxHp;
                frac = () => dt.HpFraction; return true;
            }

            var at = tf.GetComponentInParent<ArcaneTower>();
            if (at != null)
            {
                root = at.gameObject; str = at; maxHp = at.MaxHp;
                frac = () => at.HpFraction; return true;
            }

            var b = tf.GetComponentInParent<Building>();
            if (b != null)
            {
                root = b.gameObject; str = b; maxHp = b.MaxHp;
                frac = () => b.HpFraction; return true;
            }

            var g = tf.GetComponentInParent<Gate>();
            if (g != null)
            {
                root = g.gameObject; str = g; maxHp = g.MaxHp;
                frac = () => g.HpFraction; return true;
            }

            var w = tf.GetComponentInParent<WallSegment>();
            if (w != null)
            {
                var seg = w;   // 0..100 damage scale -> fraction = 1 - damage/100
                root = w.gameObject; str = w; maxHp = 100f;
                frac = () => 1f - Mathf.Clamp01(seg.Damage / 100f); return true;
            }

            return false;
        }
    }
}
