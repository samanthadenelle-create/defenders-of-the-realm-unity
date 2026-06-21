// =============================================================================
// CastleSpawnMarkerHider — runtime, NON-DESTRUCTIVE guard that hides every stray
// placeholder CAPSULE ("pill") in the Central Castle Hub (MainCastle_Hall).
// -----------------------------------------------------------------------------
// WHY this exists:
//   The hero-spawn area in MainCastle_Hall keeps showing a raw white/tinted CAPSULE
//   pill, and the fix kept regressing. Investigation found the pill is NOT one fixed
//   object — several independent sources can draw a capsule near the hero spawn:
//
//     1. The promoted spawn marker "HeroStartPoint_PlayerSpawn" — CastleHubBuilder
//        strips its mesh at bake time, but a build made from a scene state where the
//        MeshRenderer was left enabled shows a pill.
//     2. Runtime placeholder bodies: CastleVendorNpcInjector.SpawnPlaceholder,
//        StoryCompanionInjector.BuildPlaceholder, HeroControlEnsurer.SpawnEmergencyHero,
//        and Village2Playable's hero-body fallback ALL do
//        GameObject.CreatePrimitive(PrimitiveType.Capsule) when the People/Hero art
//        (gitignored on fresh clones / lean builds) is missing. Any of these renders a
//        capsule pill right next to the hero.
//
//   Re-saving MainCastle_Hall.unity by hand to fix it carries the project's
//   scene-resave corruption risk (CLAUDE.md §3), and chasing each spawner one-by-one is
//   why it kept regressing. So this is a SOURCE-AGNOSTIC runtime cleaner.
//
//   Mirroring CastleVendorNpcInjector / StoryCompanionInjector exactly, this
//   self-bootstrapping DDOL singleton runs on every MainCastle_Hall load (deferred one
//   frame so runtime injectors have spawned their bodies first) and:
//     • disables the renderer on the named spawn marker(s), AND
//     • disables the renderer on ANY object whose mesh is the built-in CAPSULE mesh —
//       i.e. every stray pill, whatever spawned it.
//   It NEVER destroys the GameObject or moves a transform — spawn logic and
//   proximity-based NPC interaction keep working; only the visible pill is hidden.
//   Capsule-mesh primitives are ONLY ever placeholders/markers in this project (never
//   final art), so hiding them is always correct. Idempotent per load.
//
// Village -> Core only. No reflection, no cross-asmdef ref.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Hides stray placeholder capsule "pills" in the castle hub at runtime.</summary>
    public sealed class CastleSpawnMarkerHider : MonoBehaviour
    {
        public static CastleSpawnMarkerHider Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";

        // Names CastleHubBuilder uses for the promoted spawn marker(s). The first is
        // the canonical castle spawn pill; the second is the personal-quarters anchor.
        private static readonly string[] MarkerNames =
        {
            "HeroStartPoint_PlayerSpawn",
            "HeroStartPoint_InsidePersonalQuarters",
        };

        // Cached reference to Unity's built-in Capsule mesh, used to identify stray pills
        // regardless of which spawner created them. Captured once from a throwaway primitive.
        private static Mesh _capsuleMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("CastleSpawnMarkerHider").AddComponent<CastleSpawnMarkerHider>();
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

        // The vendor / companion / emergency-hero injectors also bootstrap on scene load
        // and spawn their placeholder capsules during the same frame. Hide on THIS frame
        // (catches baked pills) AND again after end-of-frame (catches the runtime ones).
        private void ScheduleHide()
        {
            Hide();
            if (isActiveAndEnabled) StartCoroutine(HideNextFrame());
        }

        private IEnumerator HideNextFrame()
        {
            yield return null;            // let AfterSceneLoad injectors spawn their bodies
            yield return new WaitForEndOfFrame();
            Hide();
        }

        // Disable the renderer on (a) each named spawn marker and (b) every object drawing
        // the built-in capsule mesh. We do NOT destroy GameObjects or move transforms — the
        // spawn point and proximity-based NPC interaction keep working; only the visible
        // pill is hidden.
        private void Hide()
        {
            // (a) Named spawn markers (the baked HeroStartPoint pill).
            foreach (var markerName in MarkerNames)
            {
                var marker = GameObject.Find(markerName);
                if (marker == null) continue;
                var r = marker.GetComponent<Renderer>();
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    Debug.Log($"[CastleSpawnMarkerHider] hid placeholder capsule on '{markerName}' (marker transform kept).");
                }
            }

            // (b) Source-agnostic: any renderer drawing the built-in CAPSULE mesh is a stray
            //     placeholder pill (vendor / companion / emergency-hero / spawn fallback).
            var capsule = GetCapsuleMesh();
            if (capsule == null) return;

            var filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            int hidden = 0;
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh != capsule) continue;
                var r = mf.GetComponent<Renderer>();
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    hidden++;
                }
            }
            if (hidden > 0)
                Debug.Log($"[CastleSpawnMarkerHider] hid {hidden} stray placeholder capsule pill(s) (transforms/colliders kept).");
        }

        // Capture Unity's built-in Capsule mesh once, from a throwaway primitive. Comparing
        // sharedMesh against this is robust to however the pill was created (no fileID/GUID
        // assumptions). The temp object is destroyed immediately and never renders a frame.
        private static Mesh GetCapsuleMesh()
        {
            if (_capsuleMesh != null) return _capsuleMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            // Ticket #6 (RCA 2026-06-21): the OLD code left this throwaway as a bare root-level "Capsule"
            // with the default (Standard) material and used DEFERRED Destroy — so a MagentaGuard sweep in
            // the SAME frame (the OuterWorld additive-load sweep) caught it, its stripped shader resolved to
            // Hidden/InternalErrorShader (magenta), and it Fail-logged a phantom "stray magenta Capsule" every
            // run. Rename it (never mistaken for art), disable its renderer (can never render/strip-magenta),
            // hide+dontsave, and DestroyImmediate so it cannot survive into any sweep.
            temp.name = "__CapsuleMeshProbe";
            temp.hideFlags = HideFlags.HideAndDontSave;
            var rr = temp.GetComponent<Renderer>();
            if (rr != null) rr.enabled = false;
            var mf = temp.GetComponent<MeshFilter>();
            _capsuleMesh = mf != null ? mf.sharedMesh : null;
            DestroyImmediate(temp);
            return _capsuleMesh;
        }
    }
}
