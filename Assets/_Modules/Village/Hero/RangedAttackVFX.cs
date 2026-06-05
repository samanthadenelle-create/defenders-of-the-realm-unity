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
        private const bool ShowPlaceholderProjectiles = false;

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
        public void FireArrow(Vector3 targetWorldPos, System.Action onArrive = null)
        {
            Vector3 origin = LaunchOrigin();
            StartCoroutine(PlayCastBurst(origin, FireColor, 0.15f));   // GREEN: fired

            // WO-280: no authored prefab + placeholder visuals suppressed → fire the
            // payload after the same flight time, no debug primitive in the scene.
            if (_arrowPrefab == null && !PlaceholderProjectilesEnabled())
            {
                DeliverWithoutProjectile(origin, targetWorldPos, _arrowSpeed, onArrive);
                return;
            }

            var go = _arrowPrefab != null
                ? Instantiate(_arrowPrefab, origin, Quaternion.identity)
                : BuildPlaceholderArrow(origin);

            var mover = go.GetComponent<ProjectileMover>() ?? go.AddComponent<ProjectileMover>();
            mover.Launch(targetWorldPos, _arrowSpeed, _arrowArc, WithLandBurst(targetWorldPos, onArrive));
        }

        /// <summary>
        /// Fire a spell orb projectile toward <paramref name="targetWorldPos"/>.
        /// Plays a staff-tip charge glow before release.
        /// </summary>
        public void FireSpellOrb(Vector3 targetWorldPos, System.Action onArrive = null)
        {
            Vector3 origin = LaunchOrigin();
            StartCoroutine(PlayCastBurst(origin, FireColor, 0.35f));   // GREEN: fired

            // WO-280: the blue "SpellOrbPlaceholder" SPHERE is the stray debug primitive.
            // With no authored prefab and placeholders suppressed, skip the visual but
            // still land the payload after the same flight time (no debug sphere ships).
            if (_spellOrbPrefab == null && !PlaceholderProjectilesEnabled())
            {
                DeliverWithoutProjectile(origin, targetWorldPos, _orbSpeed, onArrive);
                return;
            }

            var go = _spellOrbPrefab != null
                ? Instantiate(_spellOrbPrefab, origin, Quaternion.identity)
                : BuildPlaceholderOrb(origin);

            var mover = go.GetComponent<ProjectileMover>() ?? go.AddComponent<ProjectileMover>();
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
            var go = new GameObject("CastBurst");
            go.transform.position = pos;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
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
            Destroy(go, duration + 0.5f);
            yield return null;
        }

        // ── Code-built placeholder projectiles ────────────────────────────────

        private static GameObject BuildPlaceholderArrow(Vector3 origin)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ArrowPlaceholder";
            go.transform.position   = origin;
            go.transform.localScale = new Vector3(0.06f, 0.35f, 0.06f);
            Object.Destroy(go.GetComponent<Collider>());

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.55f, 0.35f, 0.1f); // brown wood
                rend.material = mat;
            }

            // Trail renderer for the feather-dust effect.
            var trail = go.AddComponent<TrailRenderer>();
            trail.time     = 0.12f;
            trail.startWidth = 0.03f;
            trail.endWidth   = 0f;
            trail.material   = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            trail.startColor = new Color(0.9f, 0.8f, 0.5f, 0.7f);
            trail.endColor   = new Color(0.9f, 0.8f, 0.5f, 0f);

            return go;
        }

        private static GameObject BuildPlaceholderOrb(Vector3 origin)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpellOrbPlaceholder";
            go.transform.position   = origin;
            go.transform.localScale = Vector3.one * 0.22f;
            Object.Destroy(go.GetComponent<Collider>());

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.35f, 0.5f, 1f);  // blue-purple
                // Enable emission for the glow effect.
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.15f, 0.3f, 1f) * 1.8f);
                rend.material = mat;
            }

            // Wispy trail.
            var trail = go.AddComponent<TrailRenderer>();
            trail.time      = 0.2f;
            trail.startWidth = 0.15f;
            trail.endWidth   = 0f;
            trail.material   = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            trail.startColor = new Color(0.4f, 0.55f, 1f, 0.8f);
            trail.endColor   = new Color(0.4f, 0.55f, 1f, 0f);

            return go;
        }
    }
}
