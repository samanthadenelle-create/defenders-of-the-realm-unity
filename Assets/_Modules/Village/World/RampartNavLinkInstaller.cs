// =============================================================================
// RampartNavLinkInstaller — builder-placed installer that spawns the ground→deck
// NavMeshLink stairs at the N/S rampart access points (Part A verticality fix,
// modern NavMeshLink-component approach). Placed under the GameplaySystems root
// by VillageSceneBuilder, so it lives in the BAKED scene (bake-coupled) and
// SUPERSEDES the prior runtime StairNavLinkInjector (removed) — only ONE creator
// of these links exists, so they are never double-built.
// -----------------------------------------------------------------------------
// On Awake (and the public CreateRampartLinks) it spawns one StairNavLink host
// per access — North + South — each with a Bottom + Top child marker transform,
// then calls StairNavLink.Setup(bottom, top).
//
// MEASURED rampart geometry (mirrors RampartLiftInstaller's constants — the SAME
// two access points the lift serves):
//   • access X (interior side of the wall) = -10   (AccessX)
//   • N/S walk-lane centre Z                = ±31.1 (LaneZ)
//   • deck-top (walkable surface) Y         = 5.4   (DeckTopY)
//   • bottom ground Y                       = RAYCAST at each XZ (highest collider
//     under ~2 m — the interior floor, never the deck above), exactly like
//     RampartLiftInstaller.SpawnLift.
// So:  North Bottom=(-10, groundY, +31.1)  Top=(-10, 5.4, +31.1)
//      South Bottom=(-10, groundY, -31.1)  Top=(-10, 5.4, -31.1)
//
// StairNavLink then SAMPLES both endpoints onto the actual navmesh, so even if a
// raycast/constant is slightly off, the link snaps to valid mesh or declines (and
// warns) rather than floating.
//
// LIFT STAYS: this does NOT touch RampartLiftInstaller. Both run; flip
// StairNavLink.Enabled = false to fall back to the lift only.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Builder-placed installer for the rampart ground→deck NavMeshLinks.</summary>
    [DisallowMultipleComponent]
    public sealed class RampartNavLinkInstaller : MonoBehaviour
    {
        // Rampart geometry — same source values as RampartLiftInstaller
        // (VillageSceneBuilder.Fortify.cs). Kept in sync so the link and the lift
        // serve the identical two access points.
        private const float DeckTopY = 5.4f;   // walkable deck surface (DeckTopY)
        private const float LaneZ    = 31.1f;  // N/S walk-lane centre Z (±)
        private const float AccessX  = -10f;   // interior side of the wall

        private bool _built;

        private void Awake()
        {
            CreateRampartLinks();
        }

        /// <summary>
        /// Spawns (idempotently) the N/S ground→deck StairNavLink stairs. Safe to
        /// call more than once — re-runs clear the prior children first.
        /// </summary>
        public void CreateRampartLinks()
        {
            if (_built) return;
            _built = true;

            BuildAccess("RampartStairLink (North)", new Vector3(AccessX, 0f,  LaneZ));
            BuildAccess("RampartStairLink (South)", new Vector3(AccessX, 0f, -LaneZ));
        }

        private void BuildAccess(string label, Vector3 xzPos)
        {
            // Find the REAL interior floor height at this spot, exactly like
            // RampartLiftInstaller.SpawnLift: a naive downward ray hits the DECK
            // (≈5.4) first, so scan all colliders below and take the HIGHEST one
            // under ~2 m — that's the walkable ground, never the deck.
            float groundY = 0f;
            var hits = Physics.RaycastAll(new Vector3(xzPos.x, 8f, xzPos.z),
                                          Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float bestFloor = float.NegativeInfinity;
            if (hits != null)
            {
                foreach (var h in hits)
                    if (h.point.y < 2f && h.point.y > bestFloor) bestFloor = h.point.y;
            }
            if (bestFloor > float.NegativeInfinity) groundY = bestFloor;

            var bottomPos = new Vector3(xzPos.x, groundY,  xzPos.z);
            var topPos    = new Vector3(xzPos.x, DeckTopY, xzPos.z);

            // Host GameObject parented under this installer for tidy hierarchy.
            var host = new GameObject(label);
            host.transform.SetParent(transform, false);
            host.transform.position = bottomPos;

            // Bottom + Top child marker transforms the StairNavLink reads.
            var bottom = new GameObject("Bottom").transform;
            bottom.SetParent(host.transform, false);
            bottom.position = bottomPos;

            var top = new GameObject("Top").transform;
            top.SetParent(host.transform, false);
            top.position = topPos;

            var link = host.AddComponent<StairNavLink>();
            link.Setup(bottom, top);
        }
    }
}
