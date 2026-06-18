using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;

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

        // CONFIRM-TO-CROSS is now the ONLY behaviour (owner directive 2026-06-18, root-cause
        // fix). The serialized field is RETAINED only so the baked scene components keep
        // deserializing without a missing-field warning — it is NO LONGER read by the runtime.
        // A hero walking into range can NEVER auto-teleport: travel ALWAYS requires the
        // explicit "Travel to <destination>" tap prompt. The old auto-cross code path, the
        // Awake() runtime guard, and the OutpostConnector name check are all gone.
        // NOTE: external editor/injector code still writes this field; it is harmless now
        // (the runtime ignores it) and the field is kept public for that back-compat.
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

        // --- NEAREST-IN-RANGE-SEAM-WINS + reach-the-edge radius (RCA 2026-06-13) ---
        // The castle bakes 4 radial gate lanes but only ONE (west) connects to the hero's
        // navmesh island; N/E/S stall the NavMeshAgent ~35m short of their seam markers, so
        // the 12m trigger never reaches and the Press-F prompt never appears. Confirm-to-cross
        // removed the only reason the radius was kept small (auto-fade), so we widen confirm
        // seams to reach the hero at the mesh edge (he WARPS across — he need not touch the
        // marker). BUT a wide radius means the ~35m spawn can sit inside MULTIPLE gate spheres:
        // two overlapping seams would each show a prompt (flicker) and each poll F (double Cross).
        // Guard: each frame, only the seam NEAREST the hero among all in-range confirm seams
        // shows its prompt + accepts the cross. (Proper fix = repair the bake so all 4 lanes
        // connect and the radius can return to 12 — queued as a follow-up.)
        private const float ConfirmMinRadius = 40f;   // clears the ~36m navmesh-edge stall + margin
        private static readonly System.Collections.Generic.List<SceneTransitionTrigger> s_inRangeConfirm =
            new System.Collections.Generic.List<SceneTransitionTrigger>();
        private static int s_collectFrame = -1;
        private float _curDist = float.MaxValue;

        // Effective radius: confirm-to-cross is unconditional, so the seam always reaches to
        // ConfirmMinRadius — the prompt appears at the navmesh edge where the hero stalls and
        // he WARPS across (he need not touch the marker). There is no longer an auto-cross mode
        // that would want the tighter authored ProximityRadius.
        private float EffRadius => Mathf.Max(ProximityRadius, ConfirmMinRadius);

        // =====================================================================
        // ROOT-CAUSE FIX (owner directive 2026-06-18): auto-cross is GONE.
        // ---------------------------------------------------------------------
        // History: the OutpostConnector_* seams were baked with requireConfirm:0, and
        // prior fixes tried to flip the field back on at runtime (Awake guard +
        // OutpostConnectorConfirmInjector). Those were field-dependent and unreliable —
        // the proximity AUTO-CROSS branch could fire on the boot/additive-load frame
        // BEFORE any guard ran, teleporting the hero with no tap (proven in Player.log).
        //
        // The durable fix is to DELETE the auto-cross code path entirely. There is now
        // no requireConfirm==false branch anywhere in this component, so no value of the
        // serialized field — baked false or otherwise — can ever auto-teleport. Travel
        // is tap-only for EVERY SceneTransitionTrigger. The Awake() guard and the
        // NameMarksOutpostConnector helper were removed as now-dead code.

        private bool _confirmAnnounced;   // FlowTrace proof line emitted once per trigger

        private void Update()
        {
            if (!_announced)
            {
                _announced = true;
                Debug.Log($"[SeamTrace] '{name}' ONLINE  target={targetSceneName}@{targetPosition}  gatePos={transform.position}  radius={ProximityRadius}m");
            }

            if (!_confirmAnnounced)
            {
                _confirmAnnounced = true;
                FlowTrace.Step("Seam",
                    $"'{name}' confirm-to-cross enforced — tap required, no auto-teleport");
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
                Debug.Log($"[SeamTrace] '{name}' heroDist={dist:F1}m  closestEver={_minDist:F1}m  radius={EffRadius}m  {(dist <= EffRadius ? "IN-RANGE" : "out")}");
            }

            bool inRange = (_hero.position - transform.position).sqrMagnitude <= EffRadius * EffRadius;

            // CONFIRM-TO-CROSS (unconditional): NEVER auto-teleport. We only REGISTER in-range
            // interest here; the actual prompt + confirmed cross are resolved in LateUpdate for
            // the single NEAREST in-range seam (so overlapping wide gate spheres never flicker
            // two prompts or double-fire Cross). The shared frame list is reset on the first
            // seam to touch it each frame.
            if (Time.frameCount != s_collectFrame)
            {
                s_collectFrame = Time.frameCount;
                s_inRangeConfirm.Clear();
            }
            if (inRange)
            {
                _curDist = dist;
                if (!s_inRangeConfirm.Contains(this)) s_inRangeConfirm.Add(this);
            }
            else if (_promptShown)
            {
                // Left range — drop the prompt immediately (don't wait for LateUpdate).
                MobileInteractButton.Release(this);
                _promptShown = false;
            }
        }

        // CONFIRM-mode prompt + cross, resolved to the single NEAREST in-range confirm seam.
        // Runs after every Update() has populated s_inRangeConfirm this frame, so the "who is
        // nearest" decision sees all seams and is order-independent. This is the guard that
        // stops the wide confirm radius from flickering two prompts / double-firing Cross when
        // the hero stands inside more than one gate sphere.
        private void LateUpdate()
        {
            if (_fired) return;

            // Not in range this frame → ensure our prompt is down.
            if (!s_inRangeConfirm.Contains(this))
            {
                if (_promptShown) { MobileInteractButton.Release(this); _promptShown = false; }
                return;
            }

            // Find the nearest in-range confirm seam this frame.
            SceneTransitionTrigger nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < s_inRangeConfirm.Count; i++)
            {
                var t = s_inRangeConfirm[i];
                if (t != null && t._curDist < best) { best = t._curDist; nearest = t; }
            }

            // Not the winner → yield the shared prompt to the nearer seam.
            if (nearest != this)
            {
                if (_promptShown) { MobileInteractButton.Release(this); _promptShown = false; }
                return;
            }

            // We are the nearest in-range seam: own the prompt + the confirmed cross.
            string dest = FriendlyDestinationName();
            if (!_promptShown)
            {
                Debug.Log($"[SeamTrace] '{name}' NEAREST in-range ({_curDist:F1}m, radius {EffRadius}m) -> showing CONFIRM prompt for '{dest}'.");
                _promptShown = true;
            }
            // Mobile-first: the confirmed crossing fires through the shared on-screen
            // Interact button (requested above). The desktop F-key trigger was removed.
            MobileInteractButton.Request(this, $"Travel to {dest}", () => Cross(_hero));
        }

        // OnTriggerEnter is intentionally a NO-OP for crossing: confirm-to-cross is now
        // unconditional, so trigger-enter must NOT auto-cross either. The player always has to
        // tap the on-screen Interact button. The Update() proximity loop registers in-range
        // interest and LateUpdate shows the prompt + handles the confirmed crossing. (Method
        // kept empty-of-cross deliberately — there is no path here that can teleport the hero.)

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
