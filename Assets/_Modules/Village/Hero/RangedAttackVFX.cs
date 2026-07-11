// =============================================================================
// RangedAttackVFX — DEF-23: Code-built projectile launcher for Ranger + Mage.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Spawns projectiles for ranged hero attacks and plays cast-origin particle bursts.
// Fully code-built — no prefabs required as a placeholder until art assets ship.
// Attaches to the Hero root alongside HeroLocomotion.
//
// When _arrowPrefab / _spellOrbPrefab are null, a code-built placeholder
// (elongated capsule for arrow, glowing sphere for orb) is created instead so
// VFX is visible from day one.
//
// INTEGRATION:
//   HeroAbilities (or the hero's combat script) calls:
//     rangedVFX.FireArrow(targetPos)    // Ranger
//     rangedVFX.FireSpellOrb(targetPos) // Mage
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Combat;   // DamageElement (projectile art element typing)
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Ranged attack VFX launcher. Spawns projectiles that travel via
    /// <see cref="ProjectileMover"/> and provides code-built cast-origin particles.
    /// Attach to the Hero root alongside <see cref="HeroLocomotion"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RangedAttackVFX : MonoBehaviour
    {
        [Header("Ranger — Arrow")]
        [Tooltip("Arrow prefab (optional — code-built placeholder when null).")]
        [SerializeField] private GameObject _arrowPrefab;

        [Tooltip("Travel speed for the arrow (world units/sec).")]
        [SerializeField, Min(1f)] private float _arrowSpeed = 18f;

        [Tooltip("Parabolic arc height. 0.4 gives a natural arrow arc.")]
        [SerializeField, Range(0f, 2f)] private float _arrowArc = 0.4f;

        [Header("Mage — Spell Orb")]
        [Tooltip("Spell orb prefab (optional — code-built placeholder when null).")]
        [SerializeField] private GameObject _spellOrbPrefab;

        [Tooltip("Travel speed for the spell orb (world units/sec).")]
        [SerializeField, Min(1f)] private float _orbSpeed = 24f;

        [Header("Launch point")]
        [Tooltip("Override transform for the projectile origin (e.g. hand bone). " +
                 "If null, fires from transform.position + 1m up.")]
        [SerializeField] private Transform _launchPoint;

        // Owner 2026-06-02 ("green on fire, red on land to confirm where it's going"):
        // a GREEN burst at the launch origin (the shot leaving the hero) and a RED burst
        // at the landing point (where the projectile arrives) — instant visual proof of
        // aim direction, and clean cast-vs-impact feedback.
        private static readonly Color FireColor = new Color(0.30f, 1f, 0.40f, 1f); // green = fired
        private static readonly Color LandColor = new Color(1f, 0.25f, 0.20f, 1f); // red   = landed

        // WO-280 / DEF-274: the code-built placeholder projectiles are raw URP primitives
        // — most visibly the BLUE emissive "SpellOrbPlaceholder" SPHERE the Mage fires.
        // It reads as a stray debug sphere in the playable build, so the placeholder
        // VISUAL is suppressed by default. The projectile's gameplay payload (damage +
        // on-arrive status, and the green-fire / red-land cast bursts) is UNTOUCHED — when
        // the placeholder visual is suppressed and no authored prefab is assigned, the
        // arrival callback fires after the same flight time via a timed coroutine, so the
        // shot still "connects" exactly as before. Assigning a real _arrowPrefab /
        // _spellOrbPrefab always wins regardless of this flag. Mirrors the established
        // PetHarvestBootstrap placeholder-gate pattern (const default + command-line opt-in).
        private static readonly bool ShowPlaceholderProjectiles = false; // readonly (not const) so the gated branch doesn't emit CS0162

        /// <summary>Whether the code-built placeholder projectile visuals should spawn.
        /// Off by default (WO-280); opt back in via the const above or the
        /// <c>-showPlaceholderProjectiles</c> command-line flag (dev iteration).</summary>
        private static bool PlaceholderProjectilesEnabled()
        {
            if (ShowPlaceholderProjectiles) return true;
            var args = System.Environment.GetCommandLineArgs();
            if (args != null)
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "-showPlaceholderProjectiles") return true;
            return false;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Fire an arrow projectile toward <paramref name="targetWorldPos"/>.
        /// Plays a brief bow-draw particle burst at the launch point.
        /// </summary>
        /// <param name="hovlProjectileKey">WO-VFX-RANGED: optional Hovl catalog LOOP key for the
        /// travelling body. When set, a Hovl projectile FX follows the (no-FX) mover and the old
        /// Spells-Pack storm-bolt body + on-arrival SpawnImpact are SUPPRESSED (no double VFX).</param>
        /// <param name="hovlImpactKey">Optional Hovl impact key. Its presence (like a travel key)
        /// suppresses the old ProjectileVFXCatalog.SpawnImpact — the CALLER fires the Hovl impact.</param>
        /// <param name="tint">Optional HDR recolour for the Hovl travel FX (colourblind: reads by motion).</param>
        public void FireArrow(Vector3 targetWorldPos, System.Action onArrive = null,
                              string hovlProjectileKey = null, string hovlImpactKey = null, Color? tint = null)
        {
            Vector3 origin = LaunchOrigin();
            FlowTrace.Step("Ranged", $"FireArrow -> target={targetWorldPos} origin={origin} prefab={(_arrowPrefab == null ? "<pooled-vfx>" : _arrowPrefab.name)} hovl={hovlProjectileKey ?? "<none>"}");
            StartCoroutine(PlayCastBurst(origin, FireColor, 0.15f));   // GREEN: fired

            bool useHovl = !string.IsNullOrEmpty(hovlProjectileKey);
            bool suppressOldImpact = useHovl || !string.IsNullOrEmpty(hovlImpactKey);

            // VFX: fire a real particle-FX-bodied projectile (Storm bolt for the
            // physical arrow). This WINS over the WO-280 placeholder suppression — that
            // flag only hid the raw debug primitive; the particle FX IS the intended visual.
            // POOLED: the body is LEASED from MoverProjectilePool (GC-free) instead of a
            // per-shot Instantiate; it returns itself to the pool on Arrive.
            if (_arrowPrefab == null)
            {
                // WO-VFX-RANGED: with a Hovl travel key, lease a NO-FX body so the Hovl
                // projectile FX (below) is the ONLY travelling visual (no storm-bolt double).
                var smover = LeaseMover(useHovl ? ProjectileBodyKind.NoFxArrow : ProjectileBodyKind.RangerArrowVfx, origin);
                System.Action arrive = suppressOldImpact
                    ? WithLandBurst(targetWorldPos, onArrive)
                    : WithImpactVfx(targetWorldPos, DamageElement.None, WithLandBurst(targetWorldPos, onArrive));
                if (useHovl)
                {
                    var h = PlayHovlTravel(hovlProjectileKey, origin, targetWorldPos, tint, smover);
                    var inner = arrive;
                    arrive = () => { h?.Stop(); inner?.Invoke(); };
                }
                smover.Launch(targetWorldPos, _arrowSpeed, _arrowArc, arrive);
                return;
            }

            // Authored prefab path (rare — only when an _arrowPrefab is assigned): kept as a
            // per-shot Instantiate. The body is unbound, so ProjectileMover self-destructs on
            // arrival via its legacy path. (No pool key for an arbitrary authored prefab.)
            var go = Instantiate(_arrowPrefab, origin, Quaternion.identity);
            if (!go.TryGetComponent(out ProjectileMover mover)) mover = go.AddComponent<ProjectileMover>();
            mover.Launch(targetWorldPos, _arrowSpeed, _arrowArc, WithLandBurst(targetWorldPos, onArrive));
        }

        /// <summary>
        /// Fire a spell orb projectile toward <paramref name="targetWorldPos"/>.
        /// Plays a staff-tip charge glow before release.
        /// </summary>
        /// <param name="hovlProjectileKey">WO-VFX-RANGED: optional Hovl LOOP key for the travelling orb.
        /// When set, the Hovl orb follows the (no-FX) mover and the old arcane-orb body + SpawnImpact are
        /// SUPPRESSED (no double VFX).</param>
        /// <param name="hovlImpactKey">Optional Hovl impact key — its presence suppresses the old
        /// SpawnImpact; the CALLER fires the Hovl impact on arrival.</param>
        /// <param name="tint">Optional HDR recolour for the Hovl travel FX.</param>
        public void FireSpellOrb(Vector3 targetWorldPos, System.Action onArrive = null,
                                 string hovlProjectileKey = null, string hovlImpactKey = null, Color? tint = null)
        {
            Vector3 origin = LaunchOrigin();
            FlowTrace.Step("Ranged", $"FireSpellOrb -> target={targetWorldPos} origin={origin} prefab={(_spellOrbPrefab == null ? "<pooled-vfx>" : _spellOrbPrefab.name)} hovl={hovlProjectileKey ?? "<none>"}");
            StartCoroutine(PlayCastBurst(origin, FireColor, 0.35f));   // GREEN: fired

            bool useHovl = !string.IsNullOrEmpty(hovlProjectileKey);
            bool suppressOldImpact = useHovl || !string.IsNullOrEmpty(hovlImpactKey);

            // VFX: fire a real particle-FX-bodied arcane orb (wins over the WO-280
            // primitive suppression — the FX is the intended visual, not a debug sphere).
            // POOLED: leased from MoverProjectilePool (GC-free), returns itself on Arrive.
            if (_spellOrbPrefab == null)
            {
                // WO-VFX-RANGED: with a Hovl travel key, lease a NO-FX body so the Hovl orb
                // (below) is the ONLY travelling visual (no arcane-orb double).
                var smover = LeaseMover(useHovl ? ProjectileBodyKind.NoFxOrb : ProjectileBodyKind.MageOrbVfx, origin);
                System.Action arrive = suppressOldImpact
                    ? WithLandBurst(targetWorldPos, onArrive)
                    : WithImpactVfx(targetWorldPos, DamageElement.Aether, WithLandBurst(targetWorldPos, onArrive));
                if (useHovl)
                {
                    var h = PlayHovlTravel(hovlProjectileKey, origin, targetWorldPos, tint, smover);
                    var inner = arrive;
                    arrive = () => { h?.Stop(); inner?.Invoke(); };
                }
                smover.Launch(targetWorldPos, _orbSpeed, 0f, arrive);
                return;
            }

            // Authored prefab path (rare): per-shot Instantiate, unbound, self-destructs.
            var go = Instantiate(_spellOrbPrefab, origin, Quaternion.identity);
            if (!go.TryGetComponent(out ProjectileMover mover)) mover = go.AddComponent<ProjectileMover>();
            mover.Launch(targetWorldPos, _orbSpeed, 0f, WithLandBurst(targetWorldPos, onArrive));
        }

        /// <summary>WO-280: deliver a projectile's on-arrival payload + red land-burst after
        /// the time the projectile would have taken to travel, WITHOUT spawning any visual
        /// (used when placeholder primitives are suppressed and no authored prefab exists).
        /// Keeps the "damage lands when the shot connects" timing the projectile gave.</summary>
        private void DeliverWithoutProjectile(Vector3 origin, Vector3 targetWorldPos, float speed, System.Action onArrive)
        {
            float dist  = Vector3.Distance(origin, targetWorldPos);
            float delay = dist / Mathf.Max(0.1f, speed);
            StartCoroutine(DeliverAfter(delay, WithLandBurst(targetWorldPos, onArrive)));
        }

        private IEnumerator DeliverAfter(float delay, System.Action payload)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (this != null) payload?.Invoke();
        }

        /// <summary>Wraps the arrival callback so a RED burst pops where the projectile
        /// lands (the confirm-on-land marker), then runs the original onArrive.</summary>
        private System.Action WithLandBurst(Vector3 landPos, System.Action inner)
        {
            return () =>
            {
                if (this != null && isActiveAndEnabled)
                    StartCoroutine(PlayCastBurst(landPos, LandColor, 0.18f));   // RED: landed
                inner?.Invoke();
            };
        }

        /// <summary>Wraps the arrival callback so the element-matched particle IMPACT
        /// burst pops where the projectile lands (no-op when the prefab is missing).</summary>
        private System.Action WithImpactVfx(Vector3 landPos, DamageElement element, System.Action inner)
        {
            return () =>
            {
                ProjectileVFXCatalog.SpawnImpact(landPos, element);
                inner?.Invoke();
            };
        }

        /// <summary>WO-VFX-RANGED: spawn a Hovl LOOP projectile FX that FOLLOWS the travelling
        /// (no-FX) mover from <paramref name="origin"/> aimed at <paramref name="target"/>. Returns the
        /// loop handle so the caller Stops it on arrival. Null-safe (returns null if PlayKey no-ops).</summary>
        private static VFXHandle PlayHovlTravel(string key, Vector3 origin, Vector3 target, Color? tint, ProjectileMover mover)
        {
            if (string.IsNullOrEmpty(key) || mover == null) return null;
            Vector3 dir = target - origin;
            Quaternion look = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
            return VFXManager.PlayKey(key, origin, look, null, tint, 0f, 0f, mover.transform);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Vector3 LaunchOrigin() =>
            _launchPoint != null
                ? _launchPoint.position
                : transform.position + Vector3.up * 1.1f;

        /// <summary>
        /// Brief particle burst at the cast origin — simulates bow-draw dust or
        /// staff-tip charge glow using URP-compatible default particles.
        /// </summary>
        private IEnumerator PlayCastBurst(Vector3 pos, Color col, float duration)
        {
            // POOLED (owner directive 2026-07-02): rent the host + particle unit from
            // AbilityVfxPool instead of a per-shot new GameObject + Destroy — the unit
            // arrives reset, with the shared URP soft-dot material already applied
            // (no per-cast material instantiation, no GC churn). Pre-boot fallback
            // below keeps the first shots alive before the pool bootstraps.
            var pool = AbilityVfxPool.Instance;
            GameObject host;
            ParticleSystem ps;
            if (pool != null)
            {
                host = pool.RentHost("CastBurst", pos);
                ps = pool.RentUnit(host, "CastBurstPS", pos);
            }
            else
            {
                FlowTrace.Warn("Ranged", "PlayCastBurst: AbilityVfxPool not booted — using per-shot GameObject fallback (pre-boot).");
                host = new GameObject("CastBurst");
                host.transform.position = pos;
                ps = host.AddComponent<ParticleSystem>();
                // ParticleSystem.playOnAwake defaults to true — stop BEFORE configuring
                // (Unity forbids setting main.duration on a playing system).
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                // URP soft-dot material so the burst reads as a soft glow, not opaque squares.
                AbilityVfxKit.ApplyParticleMaterial(
                    host.GetComponent<ParticleSystemRenderer>(), AbilityVfxKit.SoftDotTexture);
            }

            var main = ps.main;
            main.playOnAwake     = false;
            main.duration        = duration;
            main.loop            = false;
            main.startLifetime   = duration;
            main.startSpeed      = 1.5f;
            main.startSize       = 0.12f;
            main.startColor      = col;
            main.maxParticles    = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var burst = new ParticleSystem.Burst(0f, 10);
            emission.SetBursts(new[] { burst });

            ps.Play();
            if (pool != null) pool.ReturnHostAfter(host, duration + 0.5f);
            else Destroy(host, duration + 0.5f);
            yield return null;
        }

        // ── Pooled projectile body lease ─────────────────────────────────────

        /// <summary>Lease a ProjectileMover-bodied projectile of <paramref name="kind"/> from
        /// MoverProjectilePool at <paramref name="origin"/> (GC-free reuse). Falls back to a
        /// fresh one-off body if the pool isn't booted yet (pre-AfterSceneLoad) so a shot is
        /// never dropped. The caller arms it via ProjectileMover.Launch — behavior identical
        /// to the old per-shot Instantiate; only the body's lifecycle changed.</summary>
        private static ProjectileMover LeaseMover(ProjectileBodyKind kind, Vector3 origin)
        {
            if (MoverProjectilePool.Instance != null)
                return MoverProjectilePool.Instance.Acquire(kind, origin, Quaternion.identity);

            FlowTrace.Warn("Ranged", $"LeaseMover: MoverProjectilePool not bootstrapped yet — building one-off {kind} body (pre-AfterSceneLoad fallback).");
            // Fallback (pool not yet bootstrapped): build a one-off VFX body the old way.
            // Unbound → ProjectileMover self-destructs on arrival via its legacy path.
            DamageElement element = kind == ProjectileBodyKind.MageOrbVfx ? DamageElement.Aether : DamageElement.None;
            var go = new GameObject(kind.ToString());
            go.transform.position = origin;
            // WO-VFX-RANGED: no-FX bodies intentionally carry NO built-in visual — a Hovl
            // projectile FX supplies the travelling look, so skip the Spells-Pack storm-bolt/orb.
            if (kind != ProjectileBodyKind.NoFxArrow && kind != ProjectileBodyKind.NoFxOrb)
                ProjectileVFXCatalog.SpawnFlying(go.transform, element);
            if (!go.TryGetComponent(out ProjectileMover mover)) mover = go.AddComponent<ProjectileMover>();
            return mover;
        }

        // ── Code-built placeholder projectiles ────────────────────────────────
        // (Suppressed by default per WO-280.) The placeholder body visuals now live in
        // MoverProjectilePool's ProjectileBodyVisual so they pool alongside the VFX bodies;
        // they are built there, not here, when re-enabled.
    }
}
