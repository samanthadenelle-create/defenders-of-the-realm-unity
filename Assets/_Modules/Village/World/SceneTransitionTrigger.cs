using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Simple gate/portal trigger for transitioning between hub scenes (e.g. CastleHub) and OuterWorld.
    /// Place at a gate or seam. On player enter: ensures target scene is loaded additively,
    /// then moves the player (tagged Player or HeroTarget) to the target world position.
    ///
    /// Used by CastleHubBuilder to wire the south gate connection to OuterWorld.
    /// Extend as needed for unload logic, camera snaps, or state save.
    /// </summary>
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [Tooltip("Scene to load additively (must be in Build Settings).")]
        public string targetSceneName = "OuterWorld";

        [Tooltip("World position the player should appear at in the target scene (after load).")]
        public Vector3 targetPosition = Vector3.zero;

        [Tooltip("If true, load additive (recommended for seamless world).")]
        public bool loadAdditive = true;

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            if (other.tag != "Player" && other.tag != "HeroTarget") return;

            var targetScene = SceneManager.GetSceneByName(targetSceneName);
            if (!targetScene.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
                {
                    Debug.LogWarning($"[SceneTransitionTrigger] '{targetSceneName}' not in Build Settings — cannot transition.");
                    return;
                }

                Debug.Log($"[SceneTransitionTrigger] Loading '{targetSceneName}' {(loadAdditive ? "additive" : "single")}.");
                SceneManager.LoadScene(targetSceneName, loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
            }

            // Reposition after load has a chance to process (simple timing; production would use sceneLoaded + frame).
            StartCoroutine(RepositionPlayerAfterLoad(other.transform));
        }

        private IEnumerator RepositionPlayerAfterLoad(Transform playerTransform)
        {
            // Give the additive scene a moment to activate objects / nav.
            yield return new WaitForSeconds(0.15f);
            yield return null; // extra safety frame

            if (playerTransform != null)
            {
                playerTransform.position = targetPosition;

                // Optional: face a direction or clear velocity if Rigidbody present.
                var rb = playerTransform.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;

                Debug.Log($"[SceneTransitionTrigger] Player repositioned to {targetPosition} in '{targetSceneName}'.");
            }
        }
    }
}
