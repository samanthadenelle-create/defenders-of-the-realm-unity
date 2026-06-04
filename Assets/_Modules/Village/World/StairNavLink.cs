// =============================================================================
// StairNavLink — a code-built ground→deck NavMesh bridge built on the MODERN
// NavMeshLink COMPONENT (UnityEngine.AI, package com.unity.ai.navigation 2.0.4).
// This is Part A of the rampart verticality fix and is the owner's preferred,
// component-based approach (supersedes the prior NavMesh.AddLink core-API draft).
// -----------------------------------------------------------------------------
// Given a Bottom (interior ground) and a Top (rampart deck) transform, this host
// MonoBehaviour creates ONE child NavMeshLink:
//   • host positioned AT Bottom
//   • startPoint = Vector3.zero        (local — at the host = Bottom)
//   • endPoint   = Top - Bottom        (local — the deck, relative to Bottom)
//   • width 2.8, bidirectional, costOverride 1, autoUpdate true
//   • area 0 / agentTypeID 0 — the default Humanoid agent the hero AND enemies
//     share (shared agent type 0 covers both per the design).
//
// PRODUCTIONIZED beyond the owner's reference:
//   • IDEMPOTENT — destroys any prior child link before re-creating.
//   • NULL-GUARDED — Setup tolerates null Bottom/Top and declines.
//   • RUNTIME-SAFE — no [ExecuteInEditMode], no per-recompile object spam.
//   • Enabled FLAG — static guard; when false, CreateLink no-ops (fall back to
//     the rampart LIFT, which stays in place untouched).
//   • SamplePosition VALIDATION — both endpoints are sampled onto the real
//     navmesh (radius ~4 m). If either end has no nearby mesh, we warn the
//     off-mesh endpoint and DECLINE to build (no link to nowhere); the SAMPLED
//     positions are used for the link so it always anchors on valid mesh.
//
// The rampart LIFT (RampartLiftInstaller / LiftPlatform) is the fallback and is
// NOT touched here. Both can coexist; flip Enabled off to rely on the lift only.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace DeNelle.Village
{
    /// <summary>
    /// Builds ONE child <see cref="NavMeshLink"/> bridging a Bottom (ground) world
    /// transform to a Top (deck) world transform, both snapped onto the navmesh.
    /// Bidirectional, default agent type, width ~2.8. Idempotent + runtime-safe.
    /// The rampart LIFT stays as a fallback — this does not touch it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StairNavLink : MonoBehaviour
    {
        /// <summary>
        /// Master flag for Part A. Default TRUE: the deck IS baked walkable (deck +
        /// walk-lane are NavigationStatic in the village bake) and the bottom is the
        /// interior ground, so a sampled link connects ground↔deck with NO rebake.
        /// If the link ever misbehaves, set this false BEFORE Setup runs and the
        /// rampart LIFT (kept in place) still carries the hero up.
        /// </summary>
        public static bool Enabled = true;

        // Link tuning (owner reference: width ~2.8, bidirectional, cost 1, autoUpdate).
        private const float LinkWidth    = 2.8f;
        // Stair traversal cost. Default 1.0 = neutral ("as mobile as the player" — owner's
        // call). Kept as a static so it's tunable at runtime — bump toward ~1.3 later only
        // if you ever decide stairs should read as a tactical chokepoint.
        public static float CostModifier = 1.0f;
        // How far to search for valid navmesh around each requested endpoint.
        private const float SampleRadius = 4f;

        private Transform _bottom;
        private Transform _top;
        private NavMeshLink _link;

        /// <summary>
        /// Configure the endpoints and build the link. Call once right after
        /// AddComponent. Null Bottom/Top are tolerated (the link is declined).
        /// </summary>
        public void Setup(Transform bottom, Transform top)
        {
            _bottom = bottom;
            _top = top;
            CreateLink();
        }

        /// <summary>
        /// (Re)builds the child NavMeshLink from the current Bottom/Top transforms.
        /// Idempotent: any prior link is destroyed first. No-ops when the static
        /// <see cref="Enabled"/> flag is false or an endpoint is missing / off-mesh.
        /// </summary>
        public void CreateLink()
        {
            // Flag-guard — Part A globally disabled → rely on the lift.
            if (!Enabled) return;

            // Idempotency — clear any link we previously built before re-creating.
            DestroyLink();

            if (_bottom == null || _top == null)
            {
                Debug.LogWarning("[StairNavLink] Setup called with a null Bottom/Top " +
                                 "transform — link not built. Lift fallback still active.");
                return;
            }

            Vector3 bottomWorld = _bottom.position;
            Vector3 topWorld = _top.position;

            // Snap both ends onto the actual navmesh so the link anchors on valid
            // mesh. If an end has no mesh nearby, decline (no link to nowhere).
            if (!NavMesh.SamplePosition(bottomWorld, out var bHit, SampleRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[StairNavLink] No navmesh near BOTTOM {bottomWorld} " +
                                 $"(radius {SampleRadius}) — link not built. The interior " +
                                 "ground should be baked walkable; check the village navmesh " +
                                 "bake. Lift fallback still active.");
                return;
            }
            if (!NavMesh.SamplePosition(topWorld, out var tHit, SampleRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[StairNavLink] No navmesh near TOP {topWorld} " +
                                 $"(radius {SampleRadius}) — link not built. The rampart DECK " +
                                 "must be baked walkable (deck/walk-lane NavigationStatic) for " +
                                 "the link to connect. Falling back to the lift.");
                return;
            }

            Vector3 sampledBottom = bHit.position;
            Vector3 sampledTop = tHit.position;

            // Host the link AT the sampled bottom; local endpoints stay small + exact.
            transform.position = sampledBottom;

            _link = gameObject.AddComponent<NavMeshLink>();
            _link.startPoint   = Vector3.zero;                 // local — at the host (= Bottom)
            _link.endPoint     = sampledTop - sampledBottom;   // local — the deck, relative to Bottom
            _link.width        = LinkWidth;
            _link.costModifier = CostModifier;                  // neutral 1.0 (tunable static)
            _link.bidirectional = true;                         // climb up AND walk down
            _link.autoUpdate   = true;                          // autoUpdate true (owner reference)
            _link.area         = 0;                             // default walkable area — matches the bake
            _link.agentTypeID  = 0;                             // default Humanoid agent (shared hero+enemy)

            // Force the off-mesh connection to (re)build against the current mesh.
            _link.UpdateLink();

            Debug.Log($"[StairNavLink] Built ground→deck NavMeshLink {sampledBottom} → " +
                      $"{sampledTop} (width {LinkWidth}, bidirectional). Hero + enemies can " +
                      "now traverse it; lift kept as fallback.");
        }

        private void DestroyLink()
        {
            if (_link != null)
            {
                if (Application.isPlaying) Destroy(_link);
                else DestroyImmediate(_link);
                _link = null;
            }
        }

        private void OnDestroy()
        {
            DestroyLink();
        }
    }
}
