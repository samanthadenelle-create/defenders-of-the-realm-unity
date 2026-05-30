// =============================================================================
// TorchFireController — WO-55: dynamic fire VFX + warm point light on torches.
// -----------------------------------------------------------------------------
// Attach to any torch, brazier, or lantern GameObject that has a child
// ParticleSystem (fire) and optionally a child Light.
//
// Performance note: Physics.OverlapSphere in Update is acceptable for <= 8
// torches. If the village grows beyond that, subscribe to WaveManager events
// (OnCombatStarted / OnCombatEnded) and remove per-Update IsCombatNearby().
// =============================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// Drives dynamic fire VFX + warm point light on a torch, brazier, or lantern.
/// Attach to any GameObject that has a child ParticleSystem (fire) and
/// optionally a child Light.
/// </summary>
public class TorchFireController : MonoBehaviour
{
    [Header("Particle Systems")]
    [Tooltip("Main fire particle system (from Lana Studio).")]
    public ParticleSystem fireParticles;
    [Tooltip("Rising embers / sparks particle system.")]
    public ParticleSystem emberParticles;

    [Header("Light")]
    public Light pointLight;
    [Range(0.5f, 4f)] public float baseLightIntensity  = 1.4f;
    [Range(0f, 0.5f)] public float flickerAmplitude    = 0.28f;
    public float flickerSpeed = 4.5f;

    [Header("Combat Reaction")]
    [Tooltip("If true, fire intensifies when enemies are within combatRadius.")]
    public bool reactToCombat = true;
    public float combatRadius = 12f;
    [Range(1f, 3f)] public float combatIntensityMultiplier = 1.6f;
    public float combatFadeSpeed = 2f;

    // ── State ─────────────────────────────────────────────────────────────────
    private float _currentIntensityMultiplier = 1f;
    private float _targetIntensityMultiplier  = 1f;
    private float _flickerOffset;

    private void Awake()
    {
        // Auto-find child components if not assigned.
        if (fireParticles == null) fireParticles = GetComponentInChildren<ParticleSystem>();
        if (pointLight    == null) pointLight    = GetComponentInChildren<Light>();

        _flickerOffset = Random.Range(0f, 100f); // stagger flicker phase per torch
    }

    private void OnEnable()
    {
        if (fireParticles  != null && !fireParticles.isPlaying)  fireParticles.Play();
        if (emberParticles != null && !emberParticles.isPlaying) emberParticles.Play();
    }

    private void Update()
    {
        UpdateCombatReaction();
        UpdateFlicker();
    }

    // ── Flicker ───────────────────────────────────────────────────────────────

    private void UpdateFlicker()
    {
        if (pointLight == null) return;

        float noise = Mathf.PerlinNoise(
            Time.time * flickerSpeed + _flickerOffset, 0f);
        float flicker = Mathf.Lerp(
            baseLightIntensity - flickerAmplitude,
            baseLightIntensity + flickerAmplitude,
            noise);

        pointLight.intensity = flicker * _currentIntensityMultiplier;
    }

    // ── Combat Reaction ───────────────────────────────────────────────────────

    private void UpdateCombatReaction()
    {
        if (!reactToCombat) return;

        bool combatNearby = IsCombatNearby();
        _targetIntensityMultiplier = combatNearby
            ? combatIntensityMultiplier
            : 1f;

        _currentIntensityMultiplier = Mathf.MoveTowards(
            _currentIntensityMultiplier,
            _targetIntensityMultiplier,
            combatFadeSpeed * Time.deltaTime);

        // Scale ember emission with combat intensity.
        if (emberParticles != null)
        {
            var em = emberParticles.emission;
            em.rateOverTime = 8f * _currentIntensityMultiplier;
        }
    }

    private bool IsCombatNearby()
    {
        // Consider "combat nearby" if any enemy is alive within combatRadius.
        var cols = Physics.OverlapSphere(transform.position, combatRadius);
        foreach (var col in cols)
        {
            if (col.CompareTag("Enemy") || col.CompareTag("EnemyProjectile"))
                return true;
        }
        return false;
    }

    // ── Editor helpers ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!reactToCombat) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, combatRadius);
    }
}
