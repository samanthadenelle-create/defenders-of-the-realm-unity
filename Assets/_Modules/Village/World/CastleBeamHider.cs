// =============================================================================
// CastleBeamHider — runtime, NON-DESTRUCTIVE guard that hides the white archway
// "beam" lines in the Central Castle Hub (MainCastle_Hall).
// -----------------------------------------------------------------------------
// WHY this exists:
//   Owner 2026-06-30: four objects with "beam" in their name render as white lines
//   inside the castle gate archways; she wants them not drawn. They are NOT loose
//   scene objects (a name grep of MainCastle_Hall.unity finds zero) — they are
//   CHILDREN of a placed prefab, so the name lives in the prefab asset, not the scene.
//   Chasing the exact prefab + hand-resaving MainCastle_Hall.unity carries the project's
//   scene-resave corruption risk (CLAUDE.md §3). So this is a SOURCE-AGNOSTIC runtime
//   cleaner, mirroring CastleSpawnMarkerHider exactly.
//
//   Self-bootstrapping DDOL singleton: on every MainCastle_Hall load (this frame +
//   end-of-frame, to catch anything spawned by AfterSceneLoad injectors) it disables
//   the RENDERER on every object IN THE CASTLE SCENE whose name contains "beam".
//   It NEVER destroys the GameObject or moves a transform — only the visible line is
//   hidden. LineRenderers are skipped so a DefenseTower aim-beam is never caught; only
//   static structural beam meshes are hidden. Scene-scoped so OuterWorld (which streams
//   in additively) is never touched. Idempotent per load; logs the names + count.
//
// Village -> Core only. No reflection, no cross-asmdef ref.
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Hides the white archway "beam" lines in the castle hub at runtime.</summary>
    public sealed class CastleBeamHider : MonoBehaviour
    {
        public static CastleBeamHider Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";
        private const string NameToken   = "beam";   // case-insensitive substring match

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("CastleBeamHider").AddComponent<CastleBeamHider>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) ScheduleHide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) ScheduleHide();
        }

        // Hide on THIS frame (catches baked beams) AND again after end-of-frame (catches
        // anything a runtime injector parents in during the same load).
        private void ScheduleHide()
        {
            Hide();
            if (isActiveAndEnabled) StartCoroutine(HideNextFrame());
        }

        private IEnumerator HideNextFrame()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            Hide();
        }

        // Disable the renderer on every CASTLE-SCENE object whose name contains "beam".
        // Non-destructive (transform/collider kept). Skips LineRenderers (tower aim-beams)
        // and any renderer that lives in another loaded scene (e.g. additive OuterWorld).
        private void Hide()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
            int hidden = 0;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (r is LineRenderer) continue;                          // never the tower aim-beam
                if (r.gameObject.scene.name != TargetScene) continue;     // castle only (not additive OuterWorld)
                if (r.gameObject.name.IndexOf(NameToken, StringComparison.OrdinalIgnoreCase) < 0) continue;

                r.enabled = false;
                hidden++;
                Debug.Log($"[CastleBeamHider] hid beam renderer '{r.gameObject.name}' (transform/collider kept).");
            }
            if (hidden > 0)
                Debug.Log($"[CastleBeamHider] hid {hidden} archway beam renderer(s) in {TargetScene}.");
        }
    }
}
