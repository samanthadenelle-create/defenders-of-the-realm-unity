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

        [Tooltip("CONFIRM-TO-CROSS (default ON). When true the seam NEVER auto-teleports on proximity " +
                 "or trigger-enter; instead it shows an 'Press F to travel to <destination>' prompt while " +
                 "the hero is in range and only crosses when the interact key (F / on-screen Interact " +
                 "button) is pressed. This stops accidental warps when walking past a gate. Set false " +
                 "only for a seam that should auto-cross (legacy behaviour).")]
        public bool requireConfirm = true;

        private bool _fired;
        private Transform _hero;
        private bool _promptShown;   // confirm-mode: an in-range prompt is currently up

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

            bool inRange = (_hero.position - transform.position).sqrMagnitude <= ProximityRadius * ProximityRadius;

            if (requireConfirm)
            {
                // CONFIRM-TO-CROSS: never auto-teleport. Show a prompt while in range and
                // only cross on an explicit interact press THIS frame (F on desktop, the
                // shared on-screen Interact button on mobile — same input the buildings/
                // vendors use, MobileInteractButton fires its onTap = Cross).
                if (inRange)
                {
                    string dest = FriendlyDestinationName();
                    if (!_promptShown)
                    {
                        Debug.Log($"[SeamTrace] '{name}' IN RANGE ({dist:F1}m) -> showing CONFIRM prompt for '{dest}'.");
                        _promptShown = true;
                    }
                    // Re-request every frame so the shared button stays up while in range
                    // and routes its tap to this gate's crossing.
                    MobileInteractButton.Request(this, $"Press F: Travel to {dest}", () => Cross(_hero));

                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        Debug.Log($"[SeamTrace] '{name}' CONFIRM key (F) pressed in range -> firing Cross().");
                        Cross(_hero);
                    }
                }
                else if (_promptShown)
                {
                    // Left range — drop the prompt.
                    MobileInteractButton.Release(this);
                    _promptShown = false;
                }
                return;
            }

            // Legacy AUTO-CROSS (requireConfirm == false): proximity fires the crossing.
            if (inRange)
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
            // Confirm-to-cross: trigger-enter must NOT auto-cross either — the player has
            // to press F / tap Interact. The Update() proximity loop already shows the
            // prompt + handles the confirmed crossing while in range.
            if (requireConfirm) return;
            Cross(other.transform);
        }

        // Release the shared interact button if this gate is torn down while prompting.
        private void OnDisable()
        {
            if (_promptShown)
            {
                MobileInteractButton.Release(this);
                _promptShown = false;
            }
        }

        // Map the raw target scene name to a friendly, player-facing destination.
        private string FriendlyDestinationName()
        {
            string s = targetSceneName ?? "";
            switch (s)
            {
                case "OuterWorld": return "the Outer World";
                case "Garrison_troll_outpost": return "Troll Outpost";
                case "Garrison_ruined_keep": return "Ruined Keep";
                case "Garrison_frost_keep": return "Frost Keep";
                case "MainCastle_Hall": return "the Castle";
                case "Village2": return "the Village";
            }
            // Generic cleanup: strip a leading "Garrison_" / "Outpost_" prefix and
            // Title-Case the underscore-separated remainder (e.g. "Garrison_dark_cave"
            // -> "Dark Cave"). Falls back to the raw name if empty.
            string body = s;
            if (body.StartsWith("Garrison_")) body = body.Substring("Garrison_".Length);
            else if (body.StartsWith("Outpost_")) body = body.Substring("Outpost_".Length);
            body = body.Replace('_', ' ').Trim();
            if (body.Length == 0) return string.IsNullOrEmpty(s) ? "the destination" : s;

            var parts = body.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) +
                           (parts[i].Length > 1 ? parts[i].Substring(1) : "");
            }
            return string.Join(" ", parts);
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
