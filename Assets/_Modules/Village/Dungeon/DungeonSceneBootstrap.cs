// =============================================================================
// DungeonSceneBootstrap — WO-59: wires dungeon VFX mode on scene load.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Place this MonoBehaviour on a persistent scene root in any dungeon scene.
// It swaps VFXManager to its dungeon prefab overrides (DungeonVFXSettings) when
// the dungeon scene is active, and restores village defaults on disable/unload.
//
// Screen shake on enter uses CameraShakeBridge.Shake() — the project-wide shim
// (no CameraShakeManager / ShakeTier; Heavy tier maps to 0.5 intensity).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Place in any dungeon scene. Activates dungeon VFX overrides via
    /// <see cref="VFXManager.ApplyDungeonMode"/> and restores them on unload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonSceneBootstrap : MonoBehaviour
    {
        [Tooltip("Intensity of the entry screen shake. Maps to CameraShakeBridge.Shake() intensity.")]
        [SerializeField, Range(0f, 1f)] private float _entryShakeIntensity = 0.5f;

        [Tooltip("Duration in seconds of the entry screen shake.")]
        [SerializeField, Range(0f, 2f)] private float _entryShakeDuration = 0.6f;

        private void OnEnable()
        {
            // Swap VFX pools to dungeon variants. Null-safe: no-op if VFXManager
            // is not present or dungeonSettings is unassigned.
            VFXManager.Instance?.ApplyDungeonMode(true);

            // Heavy screen shake on dungeon enter (WO-59 §5).
            CameraShakeBridge.Shake(_entryShakeIntensity, _entryShakeDuration);

            // Post-processing override: the URP Volume adjustment is intentionally
            // left to the scene's Volume profile (set bloomIntensityMultiplier /
            // contrastBoost on DungeonVFXSettings for designer reference). Full
            // runtime Volume manipulation requires a PerformanceManager ref (WO-51)
            // which may not be present in all dungeon scenes — wired as a follow-up.
        }

        private void OnDisable()
        {
            // Restore village VFX prefabs when the scene unloads.
            VFXManager.Instance?.ApplyDungeonMode(false);
        }
    }
}
