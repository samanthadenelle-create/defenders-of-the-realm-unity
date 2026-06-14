// =============================================================================
// CastleNavPlaneScrub — hides the hand-authored NavMesh-link planes in
// MainCastle_Hall (owner 2026-06-14: "added steps on all 4 sides and planes as
// navmesh links"). The planes are functional nav geometry (they bridge the new
// steps), not décor — so the renderer is disabled while the collider + any
// NavMesh role are left exactly as the owner set them. Idempotent + reproducible:
// re-running just re-hides any plane that came back visible. We do NOT rebuild
// the castle (it is now hand-authoritative — CastleHubBuilder would revert the
// owner's cleanup/resize/steps), we only scrub the renderer flag.
//
// Batchmode: DeNelle.Editor.CastleNavPlaneScrub.HidePlanes
// Menu:      Defenders/Castle/Hide NavMesh-Link Planes
// =============================================================================
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DeNelle.Editor
{
    public static class CastleNavPlaneScrub
    {
        private const string CastleScene = "Assets/Scenes/MainCastle_Hall.unity";

        // The owner's hand-added link planes. Unity auto-numbers duplicates, so we
        // match the base "Plane" plus the "(N)" siblings.
        private static readonly string[] PlaneNames =
            { "Plane", "Plane (1)", "Plane (2)", "Plane (3)" };

        [MenuItem("Defenders/Castle/Hide NavMesh-Link Planes")]
        public static void HidePlanes()
        {
            var scene = EditorSceneManager.OpenScene(CastleScene, OpenSceneMode.Single);

            int hidden = 0, missing = 0;
            foreach (var name in PlaneNames)
            {
                var go = GameObject.Find(name);
                if (go == null) { missing++; continue; }

                // Disable every renderer on the plane (a primitive Plane is a single
                // MeshRenderer, but cover children defensively) — keep colliders +
                // NavMesh modifiers so the link surface still bakes/walks.
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.enabled) { r.enabled = false; hidden++; }
                }
            }

            if (hidden > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[CastleNavPlaneScrub] hid {hidden} plane renderer(s) " +
                      $"({missing} name(s) not found) in {CastleScene}.");
        }
    }
}
