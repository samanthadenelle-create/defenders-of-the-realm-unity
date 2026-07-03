#if UNITY_EDITOR || DEVELOPMENT_BUILD
// =============================================================================
// EmpowermentDebugTrigger — DEV-ONLY showcase hotkey.
// -----------------------------------------------------------------------------
// Press F8 to force-empower every Tower in the scene so the elemental empowerment
// VFX (ManaSurge / GlacialCore / EternalEmber / TrueAim auras) can be seen before
// the TowerData assets have empowerment authored. The element is chosen from each
// tower's name. Self-bootstraps; compiled only into Editor / Development builds.
// =============================================================================
using UnityEngine;
using DeNelle.Core.Data;

namespace DeNelle.Village
{
    /// <summary>DEV-ONLY: F8 force-empowers all towers to showcase the empowerment VFX.</summary>
    public sealed class EmpowermentDebugTrigger : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<EmpowermentDebugTrigger>() != null) return;
            var go = new GameObject("EmpowermentDebugTrigger");
            DontDestroyOnLoad(go);
            go.AddComponent<EmpowermentDebugTrigger>();
        }

        private void Update()
        {
            // This is a DEV action (force-empower towers) that happens to share the F8
            // key — it is NOT the F8 capture (BreakCaptureHarness owns that and is
            // untouched). Gate the DEV action behind the global DevHotkeys kill-switch
            // (default OFF) so it's dead in the editor too; a dev opts in with
            // PlayerPrefs ff.devhotkeys=1. F8 still triggers BreakCaptureHarness capture.
            if (!DeNelle.Core.FeatureFlags.DevHotkeys) return;
            if (!Input.GetKeyDown(KeyCode.F8)) return;

            var towers = FindObjectsByType<Tower>();
            if (towers == null || towers.Length == 0)
            {
                Debug.Log("[EmpowermentDebug] F8: no towers in scene to empower.");
                return;
            }

            foreach (var t in towers)
                if (t != null) t.DebugForceEmpower(AbilityForTower(t));

            Debug.Log($"[EmpowermentDebug] F8: force-empowered {towers.Length} tower(s).");
        }

        private static EmpowermentAbility AbilityForTower(Tower t)
        {
            string n = t.gameObject.name.ToLowerInvariant();
            if (n.Contains("frost")  || n.Contains("ice"))    return EmpowermentAbility.GlacialCore;
            if (n.Contains("mage")   || n.Contains("arcane"))  return EmpowermentAbility.ManaSurge;
            if (n.Contains("archer") || n.Contains("arrow"))   return EmpowermentAbility.TrueAim;
            return EmpowermentAbility.EternalEmber;
        }
    }
}
#endif
