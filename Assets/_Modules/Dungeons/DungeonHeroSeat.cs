// =============================================================================
// DungeonHeroSeat — THE ONE PLACEMENT AUTHORITY FOR A HERO ARRIVING IN A DUNGEON.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// WO-1222. The project has TWO dungeon pipelines and, until this file, only ONE of
// them ever asserted where the hero ended up:
//
//   • DATA-DRIVEN (hand-built Dungeon_*.unity)  — DungeonController.PlaceHero(spawnPos)
//     teleports the Keeper to the layout's spawn every Begin().
//   • COMPOSED    (RoomForge dg_*.unity)        — has NO DungeonController, therefore
//     NO PlaceHero, therefore NO placement assertion of any kind. Its hero pose was
//     whatever HeroControlEnsurer's carry re-home happened to leave behind — and
//     nothing downstream ever checked that answer.
//
// WHY AN ASSERTION AND NOT JUST A PLACEMENT: the composed path is not merely
// unseated, it is OVERWRITABLE. The hero root is carried DontDestroyOnLoad across the
// Single load, and OTHER systems write that same transform — BattleArena.WarpHero
// most of all, which is DDOL itself and stages ~7km away at (5000, 0, 5000). On
// 2026-08-26 the owner entered dg_healers_cottage and stood at (5000, 0, 4991):
// that is BattleArena's staged hero stance to the centimetre
// (ArenaCentre + (0, 0, -ArenaHalfDepth + 9), BattleArena.cs:591) — a coordinate no
// dungeon layout can produce and only WarpHero can write. The dungeon itself was
// healthy (7 enemies, 60 fps); the player got a black screen because the camera
// honestly followed a hero standing in an arena staging area with no floor under it.
//
// So the invariant this file owns is deliberately stated as an OUTCOME, not as a
// call order: ⭐ ONE FRAME AFTER A DUNGEON LOADS, THE HERO IS AT THAT DUNGEON'S SEAT.
// Whoever wrote the pose last, whichever pipeline loaded the scene, the answer is
// checked and — when it is wrong — corrected LOUDLY (FlowTrace.Fail → break-log), so
// the next occurrence names its writer instead of costing another felt-test.
//
// ⛔ THIS DOES NOT TOUCH BattleArena.ArenaCentre AND MUST NEVER. The arena's staging
// coordinate is correct and doing its job. The defect is a hero left standing on it
// inside a scene that is not the arena.
//
// ⚠ A LIVE STAGED BATTLE OWNS THE HERO. If BattleArena reports a fight in progress we
// log and STAND DOWN rather than yanking the player out of a real arena encounter
// (dungeons legitimately stage arena fights — EncounterTrigger → BeginEncounter).
// An orphaned fight that survived a scene load is resolved by the arena's own
// abandonment watchdog within its grace, and the caller re-checks after it clears.
//
// Instrumented per CLAUDE.md §12: every step of the arrival is a captured line —
// where the hero was, what seat the scene offered, whether it was on the navmesh,
// and whether this file moved it. Instrumentation here is PERMANENT.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using DeNelle.Village.Arena;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// The single authority that seats — and then PROVES it seated — a hero arriving in a
    /// dungeon scene, serving both the data-driven and the composed pipelines.
    /// </summary>
    public static class DungeonHeroSeat
    {
        private const string Sys = "DungeonSeat";

        /// <summary>Name of the arrival marker every RoomForge bake writes (DungeonBaker.cs:1248).</summary>
        public const string ArrivalMarkerName = "HeroStartPoint_PlayerSpawn";

        /// <summary>
        /// How far from the scene's own seat a hero may be, ON ARRIVAL, before we call the
        /// pose wrong. Entry is checked one frame after load, so a healthy arrival reads ~0m;
        /// this is wide enough for a ground-snap/depenetration settle and a nav sample, and far
        /// tighter than any real stranding seen (130m outside dg_hollow_roads, ~7km at the arena).
        /// </summary>
        public const float EntryDriftMeters = 25f;

        /// <summary>
        /// Result of an arrival check, so the caller can log/branch without re-deriving anything.
        /// </summary>
        public readonly struct SeatVerdict
        {
            /// <summary>The pose the hero held when checked.</summary>
            public readonly Vector3 Observed;
            /// <summary>The seat the scene offered, if it offered one.</summary>
            public readonly Vector3? Seat;
            /// <summary>True when this call moved the hero.</summary>
            public readonly bool Corrected;
            /// <summary>True when a staged battle owned the hero and we stood down.</summary>
            public readonly bool DeferredToBattle;
            /// <summary>Plain-words statement of what was found — always non-null.</summary>
            public readonly string Detail;

            public SeatVerdict(Vector3 observed, Vector3? seat, bool corrected, bool deferredToBattle, string detail)
            {
                Observed = observed;
                Seat = seat;
                Corrected = corrected;
                DeferredToBattle = deferredToBattle;
                Detail = detail ?? "";
            }
        }

        // ---------------------------------------------------------------------
        //  Seat lookup
        // ---------------------------------------------------------------------

        /// <summary>
        /// The arrival seat baked into <paramref name="scene"/>, or null when it bakes none.
        /// Scoped to the scene ON PURPOSE — GameObject.Find is global and would happily return a
        /// marker belonging to a scene that is still unloading, which is the same class of
        /// mid-mutation re-read that produced the 130m defect (WO-1131).
        /// </summary>
        public static Vector3? FindBakedSeat(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;

            GameObject[] roots;
            try { roots = scene.GetRootGameObjects(); }
            catch (System.Exception ex)
            {
                FlowTrace.Warn(Sys, $"FindBakedSeat: GetRootGameObjects failed for '{scene.name}': {ex.Message}");
                return null;
            }

            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null) continue;
                var found = FindByNameRecursive(root.transform, ArrivalMarkerName);
                if (found != null) return found.position + Vector3.up * 0.9f;
            }
            return null;
        }

        private static Transform FindByNameRecursive(Transform t, string name)
        {
            if (t == null) return null;
            if (string.Equals(t.name, name, System.StringComparison.Ordinal)) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindByNameRecursive(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        // ---------------------------------------------------------------------
        //  Placement
        // ---------------------------------------------------------------------

        /// <summary>
        /// Move <paramref name="hero"/> to <paramref name="pos"/> facing <paramref name="facingY"/>,
        /// mover-safe. Prefers HeroLocomotion.WarpTo (it re-warps the NavMeshAgent onto the
        /// destination mesh instead of fighting a hard transform write); falls back to a raw
        /// transform move with the CharacterController suspended across it. Every path is logged.
        /// </summary>
        public static void Seat(Transform hero, Vector3 pos, float facingY, string reason)
        {
            if (hero == null)
            {
                FlowTrace.Warn(Sys, $"Seat SKIPPED ({reason}): no hero transform.");
                return;
            }

            var rot = Quaternion.Euler(0f, facingY, 0f);
            var loco = hero.GetComponentInChildren<HeroLocomotion>(true);
            if (loco != null)
            {
                bool warped = Guard.Try(Sys, $"WarpTo {pos} ({reason})", () => { loco.WarpTo(pos, rot); return true; }, false);
                if (warped)
                {
                    FlowTrace.Step(Sys, $"SEATED via HeroLocomotion.WarpTo -> {pos} yaw={facingY:0} ({reason}).");
                    return;
                }
            }

            var cc = hero.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (ccWasEnabled) cc.enabled = false;
            hero.position = pos;
            hero.rotation = rot;
            if (ccWasEnabled) cc.enabled = true;
            FlowTrace.Step(Sys,
                $"SEATED via transform fallback -> {pos} yaw={facingY:0} ({reason}; cc={(cc == null ? "none" : ccWasEnabled ? "suspended+restored" : "already off")}).");
        }

        // ---------------------------------------------------------------------
        //  The arrival assertion — the half the composed pipeline never had
        // ---------------------------------------------------------------------

        /// <summary>
        /// PROVE the hero arrived where this dungeon wanted it, and correct it when it did not.
        /// Call one frame after the scene loads (the duplicate-hero destroy has resolved by then).
        /// Safe to call on both pipelines and safe to call twice — a healthy arrival is a log line
        /// and nothing else.
        /// </summary>
        /// <param name="hero">The resolved hero root.</param>
        /// <param name="scene">The dungeon scene that just loaded.</param>
        /// <param name="seat">Override seat; when null the scene's baked arrival marker is used.</param>
        /// <param name="facingY">Facing to apply if a correction is needed.</param>
        /// <param name="pipeline">"composed" / "data-driven" — named in every captured line.</param>
        public static SeatVerdict VerifyArrival(GameObject hero, Scene scene, Vector3? seat, float facingY, string pipeline)
        {
            if (hero == null)
            {
                FlowTrace.Fail(Sys, $"VerifyArrival ({pipeline}, scene='{scene.name}'): NO hero to check — " +
                                    "nothing can prove the player arrived anywhere.");
                return new SeatVerdict(Vector3.zero, seat, false, false, "no hero");
            }

            Vector3 pos = hero.transform.position;
            Vector3? resolvedSeat = seat ?? FindBakedSeat(scene);

            var agent = hero.GetComponent<NavMeshAgent>();
            bool onMesh = agent != null && agent.enabled && agent.isOnNavMesh;
            var cc = hero.GetComponent<CharacterController>();
            bool ccMover = cc != null && cc.enabled;
            bool inArena = BattleArena.IsArenaPosition(pos);
            float drift = resolvedSeat.HasValue ? Vector3.Distance(pos, resolvedSeat.Value) : -1f;

            // THE LEDGER LINE. Everything a future triage needs, in one capture, whether or not
            // anything is wrong — an absent line is then itself evidence (the check never ran).
            FlowTrace.Step(Sys,
                $"ARRIVAL {pipeline} scene='{scene.name}' heroPos={pos} " +
                $"seat={(resolvedSeat.HasValue ? resolvedSeat.Value.ToString() : "<none baked>")} " +
                $"drift={(drift >= 0f ? drift.ToString("0.0") + "m" : "n/a")} agentOnMesh={onMesh} ccMover={ccMover} " +
                $"inBattleArena={inArena} battleInProgress={BattleArena.AnyBattleInProgress}.");

            bool wrong = inArena || (drift >= 0f && drift > EntryDriftMeters);
            if (!wrong)
                return new SeatVerdict(pos, resolvedSeat, false, false, "arrival pose accepted");

            // ⚠ A live staged fight OWNS the hero transform. Never yank the player out of a real
            // arena encounter; say so and let the caller re-check once the fight resolves.
            if (BattleArena.AnyBattleInProgress)
            {
                FlowTrace.Warn(Sys,
                    $"ARRIVAL {pipeline} scene='{scene.name}': hero is at {pos} (inBattleArena={inArena}) but a STAGED BATTLE is live — " +
                    "standing down rather than teleporting the player out of a fight. If this scene just loaded, that fight is ORPHANED " +
                    "and BattleArena's own abandonment watchdog resolves it; the seat is re-checked after it clears.");
                return new SeatVerdict(pos, resolvedSeat, false, true, "deferred to a live battle");
            }

            if (!resolvedSeat.HasValue)
            {
                FlowTrace.Fail(Sys,
                    $"ARRIVAL {pipeline} scene='{scene.name}': hero is at {pos}" +
                    (inArena ? " — inside BattleArena's staged arena, ~7km from the dungeon and off any floor it owns" : " — far from any seat") +
                    ", and this scene offers NO seat to correct it to (no '" + ArrivalMarkerName + "' baked). " +
                    "The player is standing in nothing with a working joystick. RE-BAKE the dungeon so it carries an arrival marker.");
                return new SeatVerdict(pos, null, false, false, "wrong pose, no seat available");
            }

            FlowTrace.Fail(Sys,
                $"ARRIVAL {pipeline} scene='{scene.name}' WRONG: hero at {pos}" +
                (inArena
                    ? " — that is INSIDE BattleArena's staged arena (centre 5000,0,5000). Only BattleArena.WarpHero writes coordinates there, " +
                      "so a staged encounter moved the hero and nothing moved it back before this scene took over"
                    : $" — {drift:0.0}m from this dungeon's own seat (limit {EntryDriftMeters:0}m), i.e. outside the composition") +
                $". RE-SEATING to {resolvedSeat.Value}. This net firing is itself a defect report: the arrival should already be correct.");

            Seat(hero.transform, resolvedSeat.Value, facingY, $"{pipeline} arrival correction");

            Vector3 after = hero.transform.position;
            bool onMeshAfter = agent != null && agent.enabled && agent.isOnNavMesh;
            FlowTrace.Step(Sys,
                $"ARRIVAL {pipeline} corrected: {pos} -> {after} (agentOnMesh={onMeshAfter}, " +
                $"residual drift={Vector3.Distance(after, resolvedSeat.Value):0.00}m).");

            return new SeatVerdict(after, resolvedSeat, true, false, "re-seated at the dungeon's own arrival seat");
        }
    }
}
