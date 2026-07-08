// =============================================================================
// HeroLinkCrossing — an EXPLICIT, id-PAIRED crossing point (owner 2026-06-21:
// "explicitly create a pair that link, not contingent on distance — kind of a spawn").
//
// Two markers that share the same `crossingId` form a PAIR: when the input-driven
// hero reaches one (within enterRadius while moving), it is placed at the OTHER —
// a portal/spawn, NOT a walk-across-the-gap navlink. Distance-independent: the two
// ends can be 2m or 200m apart (or, later, in different scenes). No NavMeshLink
// required, no "closest link" guessing — placement is deliberate and mappable by id.
//
// Use: drop two empty GameObjects with this component + the SAME crossingId (e.g.
// "village2_gate") at the entry and the destination. bidirectional (default) lets
// the hero cross either way; set it false for a one-way spawn. The id is the handle
// the RegionGate / region-map layer uses to say which regions a crossing connects.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeroLinkCrossing : MonoBehaviour
    {
        [Tooltip("Pair key. TWO markers with the SAME id form one crossing (entry <-> destination).")]
        public string crossingId = "";

        [Tooltip("How close (m, horizontal) the hero must be to trigger the crossing.")]
        public float enterRadius = 2f;

        [Tooltip("If true the hero can cross from either end; false = this end is entry-only (one-way spawn).")]
        public bool bidirectional = true;

        // Live registry so HeroLocomotion can find the partner by id without a scene scan each frame.
        private static readonly List<HeroLinkCrossing> All = new List<HeroLinkCrossing>();
        public static IReadOnlyList<HeroLinkCrossing> Registry => All;

        private void OnEnable()  { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        /// <summary>The partner marker sharing this crossingId (the destination), or null if unpaired.</summary>
        public HeroLinkCrossing Partner()
        {
            if (string.IsNullOrEmpty(crossingId))
            {
                // Once-per-object: an UNPAIRED marker (blank id) can never cross — surface it
                // without spamming the per-frame lookup HeroLocomotion drives.
                FlowTrace.Once("Crossing", "blank-" + GetInstanceID(),
                    $"Partner: marker '{name}' has a blank crossingId — can never pair (crossing dead).");
                return null;
            }
            for (int i = 0; i < All.Count; i++)
            {
                var o = All[i];
                if (o != null && o != this && o.crossingId == crossingId)
                {
                    FlowTrace.Once("Crossing", "paired-" + crossingId, $"Partner: crossingId '{crossingId}' resolved a pair.");
                    return o;
                }
            }
            FlowTrace.Once("Crossing", "unpaired-" + crossingId,
                $"Partner: crossingId '{crossingId}' has NO partner in the registry (count={All.Count}) — one-ended crossing.");
            return null;
        }
    }
}
