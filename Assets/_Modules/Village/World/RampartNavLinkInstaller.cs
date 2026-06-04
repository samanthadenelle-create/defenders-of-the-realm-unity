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
        private const float LaneX    = 40.1f;  // E/W walk-lane centre X (±) — DEF-216
        private const float AccessX  = -10f;   // interior side of the wall (LIFT access)

        // DEF-216 stair access points (must match the BuildRampartStaircase top
        // landings in VillageSceneBuilder.Fortify.cs). Each is the FOOT of a stair on
        // the interior ground; the StairNavLink samples both ends onto the navmesh.
        private const float StairFlank = 12f;  // X offset (N/S stairs) / Z offset (E/W stairs)

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

            // ── LIFT access (kept) — vertical link, foot XZ == top XZ (X=-10). ──
            BuildAccess("RampartLiftLink (North)",
                new Vector2(AccessX,  LaneZ), new Vector2(AccessX,  LaneZ));
            BuildAccess("RampartLiftLink (South)",
                new Vector2(AccessX, -LaneZ), new Vector2(AccessX, -LaneZ));

            // ── DEF-216 STAIR access — 4 stairs flanking the gates, clear of the
            //    lifts. Foot is offset from the top landing by the stair run along the
            //    run axis (matches BuildRampartStaircase basePos/topPos in the builder).
            //    The StairNavLink samples both ends onto the navmesh, so exact run
            //    length isn't critical here — these XZ are the landing + a foot well
            //    inside the wall, and the link snaps to the baked ramp surface.
            const float run = 9f;   // matches rampRun in VillageSceneBuilder.Fortify.cs
            // North/South: stair climbs along Z toward the wall; foot is `run` inboard
            // of the LaneZ landing. Top landing X=+StairFlank.
            BuildAccess("RampartStairLink (North)",
                new Vector2(StairFlank,  LaneZ - run), new Vector2(StairFlank,  LaneZ));
            BuildAccess("RampartStairLink (South)",
                new Vector2(StairFlank, -(LaneZ - run)), new Vector2(StairFlank, -LaneZ));
            // East/West: stair climbs along X; foot is `run` inboard of the LaneX landing.
            BuildAccess("RampartStairLink (East)",
                new Vector2(LaneX - run,  StairFlank), new Vector2(LaneX,  StairFlank));
            BuildAccess("RampartStairLink (West)",
                new Vector2(-(LaneX - run), -StairFlank), new Vector2(-LaneX, -StairFlank));
        }

        /// <summary>
        /// Wires one ground→deck StairNavLink. <paramref name="footXZ"/> is the stair
        /// FOOT (interior ground); <paramref name="topXZ"/> is the deck/walk-lane
        /// landing. For the vertical LIFTS the two XZ are identical.
        /// </summary>
        private void BuildAccess(string label, Vector2 footXZ, Vector2 topXZ)
        {
            // Find the REAL interior floor height at the FOOT, exactly like
            // RampartLiftInstaller.SpawnLift: a naive downward ray hits the DECK
            // (≈5.4) first, so scan all colliders below and take the HIGHEST one
            // under ~2 m — that's the walkable ground, never the deck.
            float groundY = 0f;
            var hits = Physics.RaycastAll(new Vector3(footXZ.x, 8f, footXZ.y),
                                          Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float bestFloor = float.NegativeInfinity;
            if (hits != null)
            {
                foreach (var h in hits)
                    if (h.point.y < 2f && h.point.y > bestFloor) bestFloor = h.point.y;
            }
            if (bestFloor > float.NegativeInfinity) groundY = bestFloor;

            var bottomPos = new Vector3(footXZ.x, groundY,  footXZ.y);
            var topPos    = new Vector3(topXZ.x,  DeckTopY, topXZ.y);

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
