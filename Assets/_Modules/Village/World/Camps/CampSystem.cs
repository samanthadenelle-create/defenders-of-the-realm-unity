// =============================================================================
// CampSystem - feature flag + self-bootstrapping spawner for the outer-world
// "clear-camp -> claim -> build-outpost" loop (ISOLATED parallel lane).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// HARD ISOLATION CONTRACT (this whole lane's reason to exist):
//   * Touches NO existing file. Every type here is NEW. It only REFERENCES
//     existing PUBLIC, read-only APIs:
//       - DeNelle.Core.World.ZoneManager  (region/threat classification)
//       - DeNelle.Village.Enemy.Died      (kill counting, subscribe-only)
//       - DeNelle.Core.State.GameStateService (bank harvest, public mutate)
//       - DeNelle.Core.Combat.IDamageableStructure (outpost is damageable)
//   * SHIPS DARK. Enabled defaults to FALSE -> the bootstrap returns immediately,
//     spawns nothing, subscribes to nothing, builds no UI. The module is fully
//     inert in the build until someone flips the flag (see "HOW TO ENABLE").
//
// HOW TO ENABLE (pick one):
//   1. Code: set CampSystem.Enabled = true BEFORE first AfterSceneLoad (e.g. a
//      dev MonoBehaviour Awake, or a debug console). Then enter the outer world.
//   2. Editor/dev: add the scripting define DOTR_CAMPS to default-enable it
//      (DefaultEnabled below ORs the flag on) without editing this file.
//   3. Runtime toggle: call CampSystem.Enable() / CampSystem.Disable().
//
// Camps are code-built primitive markers - no scene edits, no prefab hard-dep.
// On a missing optional art prefab the rule is LogWarning, never error.
// Canon: the village is Elarion (never Avalon).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.World;
using DeNelle.Core.Diagnostics;   // TGVRU — FlowTrace/Guard on the camp spawn fan-out

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Static feature flag + the AfterSceneLoad self-bootstrap that, ONLY when
    /// <see cref="Enabled"/>, spawns a handful of <see cref="ClaimableCamp"/>
    /// entities across the open world by region. Inert (no-op) when disabled.
    /// </summary>
    public static class CampSystem
    {
        // ---------------------------------------------------------------------
        // Feature flag - DEFAULT OFF. The module does nothing until this is true.
        // ---------------------------------------------------------------------

        /// <summary>Compile-time default. OFF unless DOTR_CAMPS is defined so the
        /// feature ships dark in the grant/village build.</summary>
        private const bool DefaultEnabled =
#if DOTR_CAMPS
            true;
#else
            false;
#endif

        // OWNER ENABLED 2026-06-03 for AM playtest (QA-AC verdict: READY, MVP scope; zero
        // risk — sole user, re-flippable). To ship DARK again, set back to `DefaultEnabled`.
        private static bool _enabled = true;

        /// <summary>Master switch. When false the whole camp loop is inert: no
        /// spawns, no UI, no event subscriptions. Set this BEFORE the world scene
        /// loads (or call <see cref="Enable"/> and re-run <see cref="SpawnNow"/>).</summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>Turn the feature on at runtime (then enter/re-enter the world,
        /// or call <see cref="SpawnNow"/> to materialise camps immediately).</summary>
        public static void Enable() => _enabled = true;

        /// <summary>Turn the feature off at runtime (does not despawn already-built camps).</summary>
        public static void Disable() => _enabled = false;

        // ---------------------------------------------------------------------
        // Tunables (code-only; no SO authoring needed for the dark feature).
        // ---------------------------------------------------------------------

        /// <summary>Kills required to clear a camp before it can be claimed.</summary>
        public const int DefaultKillsRequired = 6;

        /// <summary>Camp footprint radius (proximity gate for kills + claim prompt).</summary>
        public const float DefaultCampRadius = 9f;

        // World anchors per outer region (mirrors ZoneManager's cardinal fan-out:
        // East Goldfields +X, West Stoneback -X, South Mirewood -Z, North Ashwood
        // +Z). Placed safely OUTSIDE the village wall footprint (~+/-42 X, +/-33 Z)
        // so ZoneManager.GetZone classifies each into its intended region. Runtime
        // placement only - NO scene file is touched.
        private static readonly Vector3[] CampAnchors =
        {
            new Vector3( 95f, 0f,  10f),  // Goldfields (East,  tier 1)
            new Vector3(-95f, 0f, -10f),  // Stoneback  (West,  tier 2)
            new Vector3( 12f, 0f, -95f),  // Mirewood   (South, tier 3)
            new Vector3(-12f, 0f,  95f),  // Ashwood    (North, tier 4)
        };

        private static bool _spawned;
        private static readonly List<ClaimableCamp> _camps = new List<ClaimableCamp>();

        /// <summary>Live camps spawned this session (read-only view).</summary>
        public static IReadOnlyList<ClaimableCamp> Camps => _camps;

        // ---------------------------------------------------------------------
        // Self-bootstrap. Runs after EVERY scene load; returns instantly when the
        // feature is off (zero footprint) so the build is unaffected.
        // ---------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // WO-482 LIGHT WORLD: when the overworld-encounter loop is on, keep the open
            // world light — only the wandering orc "reps" populate it. Claimable camps with
            // their guard packs would bury the reps and give the player non-encounter enemies
            // to fight that never drop to battle. Suppress. Flag OFF = today's behavior (gated,
            // not deleted — fully reversible).
            if (DeNelle.Core.FeatureFlags.OverworldEncounter) return;
            if (!Enabled) return;             // SHIPS DARK: do nothing at all.
            SpawnNow();
        }

        /// <summary>
        /// Materialise the camps now (idempotent). Honors <see cref="Enabled"/> and
        /// only acts once we are in the outer-world scene. Safe to call from a dev
        /// toggle after flipping <see cref="Enabled"/> at runtime.
        /// </summary>
        public static void SpawnNow()
        {
            // T — entry/branch trace so a capture sees WHY no camps spawned (disabled / already
            // spawned / not in the outer world) vs. a per-anchor build that threw.
            using var _ = FlowTrace.Enter("Camp", "CampSystem.SpawnNow");
            if (DeNelle.Core.FeatureFlags.OverworldEncounter) { FlowTrace.Step("Camp", "SpawnNow: overworld-encounter loop ON (WO-482 light world) — no camps."); return; }
            if (!Enabled) { FlowTrace.Step("Camp", "SpawnNow: feature disabled — no camps."); return; }
            if (_spawned) { FlowTrace.Step("Camp", "SpawnNow: already spawned — no-op."); return; }

            if (!InOuterWorld())
            {
                // Not in the world scene yet - re-bootstrap will fire on the next
                // scene load. (Subscribe-free wait; no per-frame polling.)
                FlowTrace.Step("Camp", "SpawnNow: not in overworld yet — deferring to next scene load.");
                return;
            }

            var host = new GameObject("CampSystem (camps)");
            Object.DontDestroyOnLoad(host);

            // G — guard EACH anchor independently so one bad camp build logs + is skipped, never
            // aborting the whole fan-out (which would leave the world with NO camps on a single fault).
            for (int i = 0; i < CampAnchors.Length; i++)
            {
                int idx = i;
                Guard.Try("Camp", $"spawn camp at anchor {idx}", () =>
                {
                    Vector3 anchor = CampAnchors[idx];
                    RegionId region = ZoneManager.GetZone(anchor);
                    int threat = ZoneManager.ThreatLevel(anchor);

                    var go = new GameObject($"Camp_{region}");
                    go.transform.SetParent(host.transform, false);
                    go.transform.position = anchor;

                    var camp = go.AddComponent<ClaimableCamp>();
                    camp.Configure(region, threat, DefaultKillsRequired, DefaultCampRadius);
                    _camps.Add(camp);
                });
            }

            _spawned = true;
            // R — fan-out completed but produced NOTHING: self-report rather than silently
            // shipping a campless world (every anchor build threw).
            if (_camps.Count == 0)
                FlowTrace.Fail("Camp", "SpawnNow: 0 camps spawned across the outer world — every anchor build failed.");
            else
                FlowTrace.Step("Camp", $"SpawnNow: spawned {_camps.Count} claimable camp(s) across the outer world (feature ENABLED).");
            Debug.Log($"[CampSystem] Spawned {_camps.Count} claimable camps across the outer world (feature ENABLED).");
        }

        private static bool InOuterWorld()
        {
            var sm = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (NameMatches(sm.name)) return true;
            // Overworld may load ADDITIVELY (WorldSceneLoader) - check every loaded scene.
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && NameMatches(s.name)) return true;
            }
            return false;
        }

        // WO-608 merge: route through HubScenes.IsOverworld so camps spawn on the merged
        // "Main_Castle_Overworld" too (matches legacy "OuterWorld" AND the merged name).
        private static bool NameMatches(string sceneName) =>
            DeNelle.Core.HubScenes.IsOverworld(sceneName);
    }
}
