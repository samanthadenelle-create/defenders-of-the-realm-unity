// =============================================================================
// DungeonExitInteractable + DungeonExitSpawner - the composed-dungeon RETURN exit.
// -----------------------------------------------------------------------------
// SHIP-BLOCKER DG-01: a composed playable dungeon (GraphDungeonComposer /
// DungeonBaker.PopulateForPlay) seats a Player-tagged hero + hero-aggro enemy
// spawners but NO way out - the player is trapped ("roach motel", must force-quit).
// The rich Dungeon_HealersCottage scene leaves via DungeonController.ExitToVillage;
// the COMPOSED scenes (DungeonCompose_*) carry no DungeonController, so nothing
// routes home.
//
// FIX (runtime bootstrap - NO re-bake required): DungeonExitSpawner hooks
// SceneManager.sceneLoaded once (RuntimeInitializeOnLoadMethod) and, for every
// scene whose root is "DungeonCompose_*" (the composer's root name, DungeonBaker
// line ~118), injects a DungeonExitInteractable at the entry room. This covers the
// ALREADY-BAKED dg_starter_loop.unity on disk with no re-bake, and every future
// composed dungeon automatically. It is idempotent - if an exit already exists in
// the scene (a future bake-time exit, or a prior inject) it skips.
//
// The exit routes HOME exactly like DungeonController.ExitToVillage:
//   SceneRouter.LoadSceneWithFade(SceneRouter.Castle)  (the merged overworld hub).
//
// Instrumented per CLAUDE.md sec.12 - every step/branch emits [Flow:DungeonExit].
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Auto-installer: injects a <see cref="DungeonExitInteractable"/> into every
    /// composed ("DungeonCompose_*") dungeon scene at load, so a portal-loaded
    /// composed dungeon is never a trap. Self-arming, idempotent, no re-bake.
    /// </summary>
    internal static class DungeonExitSpawner
    {
        private const string Sys = "DungeonExit";
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // A build that boots straight into a composed dungeon has already loaded
            // its scene before this hook - process the active scene once, too.
            TryInject(SceneManager.GetActiveScene());
            FlowTrace.Step(Sys, "installed sceneLoaded hook (composed-dungeon exit auto-inject)");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInject(scene);

        private static void TryInject(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject[] roots;
            try { roots = scene.GetRootGameObjects(); }
            catch (Exception ex) { FlowTrace.Warn(Sys, $"GetRootGameObjects failed for '{scene.name}': {ex.Message}"); return; }

            Transform composeRoot = null;
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go != null && go.name.StartsWith("DungeonCompose_", StringComparison.Ordinal))
                {
                    composeRoot = go.transform;
                    break;
                }
            }
            // Not a composed dungeon (Dungeon_HealersCottage and the hub scenes carry
            // their own exit) - nothing to do.
            if (composeRoot == null) return;

            // Idempotent: never add a second exit (a future bake-time exit, or a prior inject).
            if (UnityEngine.Object.FindAnyObjectByType<DungeonExitInteractable>() != null)
            {
                FlowTrace.Step(Sys, $"exit already present in '{scene.name}' - skip inject");
                return;
            }

            Vector3 pos = ResolveExitPosition(composeRoot);
            var exit = DungeonExitInteractable.Spawn(pos);
            // Push the Player-tagged hero (canon §7) so the prompt/walk-in never depends on a
            // HeroLocomotion mover-type lookup (which excludes a disabled/neutralized component).
            var taggedHero = GameObject.FindGameObjectWithTag("Player");
            if (taggedHero != null) exit.SetHero(taggedHero.transform);
            FlowTrace.Step(Sys, $"injected RETURN exit into composed scene '{scene.name}' at {pos} " +
                $"(routes -> SceneRouter.Castle, hero={(taggedHero != null ? "tagged" : "unresolved")})");
        }

        // The exit seats at the ENTRY room (where the hero spawns) so the player leaves
        // from where they came, nudged a few metres off the exact hero seat so the
        // walk-in trigger does not fire on the spawn frame.
        private static Vector3 ResolveExitPosition(Transform composeRoot)
        {
            Vector3 basePos;
            Transform entry = null;
            foreach (Transform child in composeRoot)
            {
                if (child != null && string.Equals(child.name, "entry", StringComparison.OrdinalIgnoreCase))
                {
                    entry = child;
                    break;
                }
            }
            if (entry != null)
            {
                basePos = entry.position;
            }
            else
            {
                // Fallback: the Player-tagged hero seat, then origin.
                var hero = GameObject.FindGameObjectWithTag("Player");
                basePos = hero != null ? hero.transform.position : composeRoot.position;
                FlowTrace.Warn(Sys, "no 'entry' room found - seating exit at hero/root fallback");
            }
            // Nudge off the hero seat (entry node sits at origin; hero is seated on it).
            return basePos + new Vector3(0f, 0f, -2.6f);
        }
    }

    /// <summary>
    /// The in-world RETURN affordance for a composed dungeon: a glowing home-arch the
    /// hero can walk into (or tap the shared Interact button) to route back to the
    /// castle hub. Mirrors DungeonPortal's mobile-first proximity/button pattern.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonExitInteractable : MonoBehaviour
    {
        private const string Sys = "DungeonExit";
        private const float ActivateRadius = 3.0f;   // shared-button prompt range
        private const float TriggerRadius = 2.0f;    // walk-in trigger radius
        private const float CheckInterval = 0.15f;

        private Transform _hero;
        private bool _heroFound;
        private bool _isInRange;
        private bool _leaving;
        private bool _armed;                          // walk-in only after hero first steps clear
        private float _nextProximityCheck;

        // WO-770.1: a RICH dungeon (DungeonController) supplies a leave action so the exit routes
        // through ExitToVillage (banks the run's crafting scatter + ends the run cleanly) instead
        // of the composed-scene default (direct SceneRouter.Castle). Null => composed-scene behavior.
        private System.Action _onLeave;
        private string _label = "Leave Dungeon";      // prompt text (e.g. "Secret Exit" for the boss back-door)

        /// <summary>Create the exit at <paramref name="position"/> and build its visual.</summary>
        /// <param name="onLeave">Optional rich-scene leave action (ExitToVillage). Null => Castle route.</param>
        /// <param name="label">Interact-button prompt text.</param>
        public static DungeonExitInteractable Spawn(Vector3 position, System.Action onLeave = null, string label = "Leave Dungeon")
        {
            var go = new GameObject("DungeonExit (Return)");
            go.transform.position = position;
            var exit = go.AddComponent<DungeonExitInteractable>();
            exit._onLeave = onLeave;
            exit._label = string.IsNullOrEmpty(label) ? "Leave Dungeon" : label;
            exit.BuildVisual();
            return exit;
        }

        private void BuildVisual()
        {
            // Walk-in trigger volume (the only collider on the root).
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = TriggerRadius;

            // A simple emerald-gold home-arch (distinct from the purple entry portal),
            // built from decorative primitives with their colliders stripped so nothing
            // physically traps the hero. URP/Unlit is pinned into every build by the
            // MAT-02 build preprocessor, so these never render magenta.
            var frame = new Color(0.20f, 0.55f, 0.30f, 1f);   // emerald stone
            var glow = new Color(0.55f, 0.95f, 0.55f, 0.72f); // green-gold sheet

            AddDecor("Pillar_L", new Vector3(-1.1f, 1.3f, 0f), new Vector3(0.35f, 2.6f, 0.35f), frame, false);
            AddDecor("Pillar_R", new Vector3(1.1f, 1.3f, 0f), new Vector3(0.35f, 2.6f, 0.35f), frame, false);
            AddDecor("Lintel", new Vector3(0f, 2.75f, 0f), new Vector3(2.7f, 0.35f, 0.35f), frame, false);
            AddDecor("Sheet", new Vector3(0f, 1.3f, 0f), new Vector3(1.9f, 2.5f, 1f), glow, true);
        }

        private void AddDecor(string label, Vector3 localPos, Vector3 localScale, Color color, bool asQuad)
        {
            var prim = GameObject.CreatePrimitive(asQuad ? PrimitiveType.Quad : PrimitiveType.Cube);
            prim.name = label;
            var col = prim.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col); // purely visual - never trap the hero
            prim.transform.SetParent(transform, false);
            prim.transform.localPosition = localPos;
            prim.transform.localScale = localScale;

            var rend = prim.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                    // Translucent glow sheet (Surface=1 -> transparent on URP/Unlit).
                    if (asQuad && mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    rend.sharedMaterial = mat;
                }
            }
        }

        /// <summary>
        /// Push the hero rig explicitly so the prompt never depends on HeroLocomotion's enabled
        /// state (rich scene: DungeonController._hero; composed scene: the Player-tagged hero).
        /// The dungeon neutralizes the injected HeroLocomotion but KEEPS it enabled — this seam
        /// makes the exit robust either way, and independent of a mover-type lookup entirely.
        /// </summary>
        public void SetHero(Transform hero)
        {
            if (hero == null) return;
            _hero = hero;
            _heroFound = true;
        }

        private void ResolveHero()
        {
            if (_heroFound) return;
            // Prefer the Player-tagged hero (canon §7) — independent of HeroLocomotion being
            // enabled. Fall back to HeroLocomotion only if the tag is somehow unset.
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; _heroFound = true; return; }
            var hero = UnityEngine.Object.FindAnyObjectByType<HeroLocomotion>();
            if (hero != null) { _hero = hero.transform; _heroFound = true; }
        }

        private void Update()
        {
            if (_leaving) return;

            if (!_heroFound) { ResolveHero(); if (!_heroFound) return; }
            // The hero rig can be replaced (body-swap) after we first cached it - re-resolve
            // rather than dereferencing a destroyed Transform (DungeonPortal DEF-40 lesson).
            if (_hero == null) { _heroFound = false; return; }

            // Build/authoring mode: release the shared button and do nothing.
            if (MobileInteractButton.Suppressed)
            {
                MobileInteractButton.Release(this);
                return;
            }

            if (Time.time >= _nextProximityCheck)
            {
                _nextProximityCheck = Time.time + CheckInterval;
                float distSqr = (_hero.position - transform.position).sqrMagnitude;
                _isInRange = distSqr <= ActivateRadius * ActivateRadius;
                // Arm the walk-in trigger only once the hero has been clear of it, so
                // spawning on top of the exit does not yank the player straight home.
                if (!_armed && distSqr > (TriggerRadius + 0.75f) * (TriggerRadius + 0.75f))
                    _armed = true;
            }

            if (_isInRange)
                MobileInteractButton.Request(this, _label, Leave);
            else
                MobileInteractButton.Release(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_leaving || !_armed || other == null) return;
            if (!IsHeroCollider(other)) return;
            FlowTrace.Step(Sys, "hero walked into the RETURN exit");
            Leave();
        }

        // True when `other` belongs to the hero rig — WITHOUT depending on HeroLocomotion's enabled
        // state: the pushed/known hero first, then the Player tag up the parent chain (canon §7),
        // then HeroLocomotion as a last resort (composed scenes where nothing pushed a hero).
        private bool IsHeroCollider(Collider other)
        {
            if (_hero != null && (other.transform == _hero || other.transform.IsChildOf(_hero))) return true;
            for (Transform t = other.transform; t != null; t = t.parent)
                if (t.CompareTag("Player")) return true;
            return other.GetComponentInParent<HeroLocomotion>() != null;
        }

        private void Leave()
        {
            if (_leaving) return;
            _leaving = true;
            MobileInteractButton.Release(this);
            // WO-770.1: a RICH dungeon supplies ExitToVillage (banks the run's crafting scatter to
            // the larder + ends the run), so prefer it. A composed scene has no DungeonRuntimeState /
            // crafting inventory to bank, so it falls through to the direct Castle route below.
            if (_onLeave != null)
            {
                FlowTrace.Step(Sys, "taking RETURN exit -> DungeonController.ExitToVillage (rich scene)");
                _onLeave.Invoke();
                return;
            }
            // Route HOME exactly like DungeonController.ExitToVillage - the merged
            // overworld hub (SceneRouter.Castle). A composed scene has no DungeonRuntimeState
            // / crafting inventory to bank, so this is the whole exit.
            FlowTrace.Step(Sys, $"taking RETURN exit -> SceneRouter.Castle ('{SceneRouter.Castle}')");
            SceneRouter.LoadSceneWithFade(SceneRouter.Castle).Forget();
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }
    }
}
