using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Gate/portal SEAM between a hub scene (e.g. MainCastle_Hall) and OuterWorld.
    ///
    /// PROXIMITY-based (not OnTriggerEnter): when the hero (tagged Player/HeroTarget) comes
    /// within <see cref="ProximityRadius"/> of this object, it ensures the target scene is
    /// loaded additively and <c>WarpTo</c>s the hero across to <see cref="targetPosition"/>.
    ///
    /// WHY proximity: the castle and OuterWorld each have their OWN baked NavMesh (two scenes
    /// overlaid at runtime). A NavMeshAgent hero stops at the castle navmesh EDGE and can't
    /// physically reach a trigger box just beyond it — the "invisible barrier" / two-scene
    /// seam. A distance check fires reliably AT that edge and carries the hero across. The
    /// OnTriggerEnter path is kept as a fallback for movers that DO trip physics triggers.
    ///
    /// Used by CastleHubBuilder to wire the south gate connection to OuterWorld.
    /// </summary>
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [Tooltip("Scene to load additively (must be in Build Settings).")]
        public string targetSceneName = "OuterWorld";

        [Tooltip("World position the player should appear at in the target scene (after load).")]
        public Vector3 targetPosition = Vector3.zero;

        [Tooltip("If true, load additive (recommended for seamless world).")]
        public bool loadAdditive = true;

        [Tooltip("Hero within this distance (m) of the gate triggers the crossing. RAISE this if " +
                 "the hero stops short at the navmesh edge and never fires; LOWER it if it fires " +
                 "too early before you reach the gate.")]
        public float ProximityRadius = 6f;

        private bool _fired;
        private Transform _hero;

        // --- SEAMTRACE diagnostics (temporary; strip once the exit bug is closed) ---
        // Owner directive 2026-06-13: instrument the WHOLE exit path so one F8 run shows
        // exactly which step fires and where it dies. The key signal is _minDist: the
        // CLOSEST the hero ever gets to this gate. If _minDist never drops below
        // ProximityRadius, the hero physically can't reach the seam (navmesh edge / gap)
        // and Cross() can never fire — that's the all-4-borders-blocked root cause.
        private float _traceTimer;
        private float _minDist = float.MaxValue;
        private bool _announced;
        private bool _heroEverFound;

        private void Update()
        {
            if (!_announced)
            {
                _announced = true;
                Debug.Log($"[SeamTrace] '{name}' ONLINE  target={targetSceneName}@{targetPosition}  gatePos={transform.position}  radius={ProximityRadius}m");
            }

            if (_fired) return;
            if (_hero == null) _hero = ResolveHero();
            if (_hero == null)
            {
                _traceTimer += Time.deltaTime;
                if (_traceTimer >= 2f)
                {
                    _traceTimer = 0f;
                    Debug.LogWarning($"[SeamTrace] '{name}' still has NO hero (Player/HeroTarget tag not found).");
                }
                return;
            }
            if (!_heroEverFound)
            {
                _heroEverFound = true;
                Debug.Log($"[SeamTrace] '{name}' resolved hero '{_hero.name}' at {_hero.position}.");
            }

            float dist = Vector3.Distance(_hero.position, transform.position);
            if (dist < _minDist) _minDist = dist;

            _traceTimer += Time.deltaTime;
            if (_traceTimer >= 1f)
            {
                _traceTimer = 0f;
                Debug.Log($"[SeamTrace] '{name}' heroDist={dist:F1}m  closestEver={_minDist:F1}m  radius={ProximityRadius}m  {(dist <= ProximityRadius ? "IN-RANGE" : "out")}");
            }

            if ((_hero.position - transform.position).sqrMagnitude <= ProximityRadius * ProximityRadius)
            {
                Debug.Log($"[SeamTrace] '{name}' IN RANGE ({dist:F1}m <= {ProximityRadius}m) -> firing Cross().");
                Cross(_hero);
            }
        }

        // Box-collider entry still works as a fallback (for movers that DO trip OnTriggerEnter).
        private void OnTriggerEnter(Collider other)
        {
            if (_fired || other == null) return;
            if (other.tag != "Player" && other.tag != "HeroTarget") return;
            Cross(other.transform);
        }

        private void Cross(Transform player)
        {
            if (_fired || player == null) return;
            _fired = true;
            Debug.Log($"[SeamTrace] '{name}' Cross() ENTERED for '{player.name}'.");

            var targetScene = SceneManager.GetSceneByName(targetSceneName);
            if (!targetScene.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
                {
                    Debug.LogWarning($"[SeamTrace] '{name}' Cross() ABORT: '{targetSceneName}' not in Build Settings — cannot transition.");
                    _fired = false;   // allow a retry once it's loadable
                    return;
                }

                Debug.Log($"[SeamTrace] '{name}' Cross() loading '{targetSceneName}' {(loadAdditive ? "additive" : "single")} (was not loaded).");
                SceneManager.LoadScene(targetSceneName, loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
            }
            else
            {
                Debug.Log($"[SeamTrace] '{name}' Cross() target '{targetSceneName}' already loaded — repositioning.");
            }

            StartCoroutine(RepositionPlayerAfterLoad(player));
        }

        private Transform ResolveHero()
        {
            var p = SafeFindWithTag("Player") ?? SafeFindWithTag("HeroTarget");
            return p != null ? p.transform : null;
        }

        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        // ---------------------------------------------------------------------
        // WO-410/seam-pop: code-built fade-to-black mask around the warp.
        // The old crossing was a one-frame teleport + camera hard-cut (a harsh
        // visual POP). We now fade to black, snap the hero under the black,
        // let the camera settle, then fade back in. UXML is deliberately NOT
        // used (it doesn't render in player builds) — this is a pure-code
        // ScreenSpaceOverlay Canvas + Image + CanvasGroup, created lazily and
        // cached + DontDestroyOnLoad so it survives the additive load.
        // ---------------------------------------------------------------------
        private static CanvasGroup s_fadeGroup;

        private static CanvasGroup EnsureFadeOverlay()
        {
            if (s_fadeGroup != null) return s_fadeGroup;

            var go = new GameObject("__SceneTransitionFade");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // draw above everything (HUD included)

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var imgGo = new GameObject("Black");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            s_fadeGroup = group;
            return group;
        }

        private static IEnumerator FadeTo(CanvasGroup group, float target, float duration)
        {
            if (group == null) yield break;
            float start = group.alpha;
            if (duration <= 0f) { group.alpha = target; yield break; }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // unscaled: survives any timeScale pause
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = target;
        }

        private IEnumerator RepositionPlayerAfterLoad(Transform playerTransform)
        {
            // (1) Fade to black BEFORE the snap so the teleport + camera cut
            //     happen unseen.
            var fade = EnsureFadeOverlay();
            yield return FadeTo(fade, 1f, 0.25f);

            // Give the additive scene a moment to activate objects / nav (under black).
            yield return new WaitForSeconds(0.15f);
            yield return null; // extra safety frame

            if (playerTransform != null)
            {
                // WO-383: don't hard-set transform.position — that fights HeroLocomotion's
                // off-mesh clamp + NavMeshAgent every frame (the "camera/direction break at the
                // gate" bug). Use the teleport-aware WarpTo: disables the agent, moves it,
                // re-warps onto the destination (additively-loaded) NavMesh, and raises
                // OnTeleported so the follow camera snaps instead of chasing the jump.
                var loco = playerTransform.GetComponent<HeroLocomotion>();
                if (loco != null)
                {
                    loco.WarpTo(targetPosition);
                }
                else
                {
                    // Fallback (no HeroLocomotion): land on the nearest valid NavMesh point.
                    Vector3 dest = targetPosition;
                    if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                        dest = hit.position;
                    playerTransform.position = dest;
                }

                var rb = playerTransform.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;

                Debug.Log($"[SeamTrace] '{name}' repositioned: requested {targetPosition}, hero now at {playerTransform.position} in '{targetSceneName}' (loco={(loco != null)}).");
            }

            // (3) Let the follow camera settle on the warped hero for one frame
            //     under the black, THEN (4) fade back in.
            yield return null;

            // FIX B (FLAGGED, NOT APPLIED): the castle (MainCastle_Hall) stays
            // fully loaded + rendering behind OuterWorld here, which feeds the
            // WO-410 framerate collapse. Deactivating its roots is NOT safe yet:
            //   • RegionMobSpawner.ResolveHeart() (OuterWorld) calls
            //     FindAnyObjectByType<HeartController>() — HeartController is
            //     castle-owned and FindAnyObjectByType ignores INACTIVE objects,
            //     so deactivation would null the heart the mob spawner tethers to.
            //   • The crossing is ONE-WAY: there is no OuterWorld->castle return
            //     seam wired, so we can't guarantee a clean re-load on return.
            // Deactivation needs the return-seam WO + a heart-reference fix first.

            yield return FadeTo(fade, 0f, 0.35f);
        }
    }
}
