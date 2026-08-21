// =============================================================================
// ⚠ SUPERSEDED — DELETION PROPOSED TO THE OWNER, 2026-08-21 (WO-992). NOT YET
//   DELETED, and an agent must not delete it unilaterally. Read the blocker.
// -----------------------------------------------------------------------------
// RESEARCH FINDING (the WO asked "when was it created, what touches it, was it
// superseded"). Answers, all read at source 2026-08-21:
//
//   CREATED: commit 00b1662ee, 2026-05-29, under WO-55. No earlier path.
//
//   NOTHING SEATS IT. GUID f768c3e90b8ed5d45b9352187f637362 appears in ZERO
//   .unity / .prefab / .asset, including a raw-byte scan of the 12 binary
//   scenes. There is no AddComponent<TorchFireController> anywhere.
//
//   SUPERSEDED BY THREE INDEPENDENT LIVE PATHS, each rolling its own flicker:
//     • Village  — NightTorchLightSystem (DEF-214) self-bootstraps via
//       RuntimeInitializeOnLoadMethod (:93), scoped to Village2 (:50), and
//       SPAWNS ITS OWN NightTorch point lights with its own flicker (:71-72).
//     • Dungeons — DungeonDresser seats torch meshes + Lights directly
//       (TorchTokens :63, TorchIntensity :73, SeatProp(torchLight:) :214).
//     • Per-builder torch light code: DungeonComposer.cs:89-92,
//       KayKitChallengeOutpostBuilder.DressWallTorches, EnemyStrongholdBuilder.
//
// ⛔ THE BLOCKER — DELETING THIS FILE BREAKS THE COMPILE. It is NOT
//   reference-free, which is the one thing the WO's "zero callers" premise got
//   wrong. NightTorchLightSystem.cs:191 holds a LIVE type reference:
//       var torches = Object.FindObjectsByType<TorchFireController>();
//   inside AttachToExistingTorches() (:189), reading tc.pointLight at :196.
//   Because nothing ever creates one, that call ALWAYS RETURNS EMPTY — it is a
//   defensive "don't double-light" courtesy for props that never existed.
//
//   SO THE DELETION IS A TWO-FILE CHANGE, not a file removal: retire
//   AttachToExistingTorches (or its TorchFireController arm) in the SAME commit.
//   That touches a live lighting system, so it is the owner's call, not an
//   agent's. Left in place deliberately.
// -----------------------------------------------------------------------------
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
using DeNelle.Core.Diagnostics;

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
            // CompareTag THROWS on a tag that isn't defined in TagManager.
            // "Enemy"/"EnemyProjectile" aren't guaranteed defined, so guard the
            // check rather than spamming UnityException every Update.
            if (HasTag(col, "Enemy") || HasTag(col, "EnemyProjectile"))
                return true;
        }
        return false;
    }

    /// <summary>Undefined-tag-safe CompareTag. Returns false if the tag is not
    /// defined in the project (Unity throws UnityException otherwise).</summary>
    private static bool HasTag(Component c, string tag)
    {
        if (c == null) return false;
        try { return c.CompareTag(tag); }
        catch (UnityEngine.UnityException e)
        {
            FlowTrace.Warn("Environment", $"CompareTag('{tag}') failed: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    // ── Editor helpers ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!reactToCombat) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, combatRadius);
    }
}
