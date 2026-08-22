// =============================================================================
// StructureVitalsWatch — WHEN each structure was hit and WHEN it fell (WO-1026 follow-up).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE PROBLEM IT SOLVES, in the owner's words: "A plain list of 'Tower A destroyed,
// Wall B destroyed' is weak. Players need to form the thought 'I know what to move.'"
// A duration is what converts the list into a diagnosis:
//        "this wall held 40s"   vs   "this wall fell in 4s"
// The first is a wall doing its job. The second is a wall in the wrong place. Same
// row, same damage fraction, opposite instruction to the player.
//
// ⛔ IT IS A TIMER, NOT A SECOND DAMAGE AGGREGATOR — the distinction is the whole
//    reason this file is safe to add. WaveDamageReport.Collect() remains the ONE
//    authority on WHAT was damaged, what it costs to repair, and which rows are worth
//    showing. This class answers only WHEN and WHERE, and its output is MERGED INTO
//    the rows Collect() produced. It never emits a row of its own, never prices
//    anything, and never decides what the player sees. If it were allowed to build
//    rows there would be two accounts of the same attack and they would drift — the
//    exact failure CLAUDE.md §5/§16 keep recording.
//
// COST: the watch list is built ONCE at session open and then POLLED, because the
// structures that matter cannot appear mid-assault (the player cannot build during a
// wave) and a destroyed one persists as a scannable shell — WaveDamageReport's own
// header says so, which is what makes a cached list correct rather than merely cheap.
// Polling runs at 4 Hz, the same throttle HudMinimapWidget uses for its provider
// scans, for the same reason: these are FindObjectsByType sweeps and they are the
// expensive part, so we do the sweep once and poll cheap accessors after.
//
// NAME COLLISIONS ARE REAL AND ARE HANDLED EXPLICITLY. The loss rows are keyed only by
// display NAME (that is WaveDamageReport's existing shape), and a base legitimately has
// twenty things called "Wall". Merging by name takes the EARLIEST first-hit and the
// EARLIEST fall, i.e. "when did the first thing called Wall get hit, and when did the
// first one break" — which is the question the player is actually asking. Documented
// here rather than left as an accident.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Defense;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Buildings.Progression;   // ResourceCollectorRegistry, ResourceBuildingProgression
using DeNelle.Village.World;                   // HarvestSite
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Samples per-structure damage timing across one assault.</summary>
    public sealed class StructureVitalsWatch
    {
        /// <summary>Damage fraction above which a structure counts as "being hit".
        /// Matches WaveDamageReport's own pristine threshold so the two agree on what
        /// "untouched" means.</summary>
        private const float HitThreshold = 0.0001f;

        /// <summary>Seconds between polls. 4 Hz — the HudMinimapWidget provider-poll cadence.
        /// A wall that falls between two samples is recorded up to 0.25s late, which is far
        /// below the resolution of the sentence the player reads ("held 40s" / "fell in 4s").</summary>
        private const float PollInterval = 0.25f;

        /// <summary>One watched structure and its timeline.</summary>
        private sealed class Watched
        {
            public string Name;
            public string Id;                 // scene-instance key (matches BreachRecord.BreachedId)
            public string Type;               // "Wall" / "Gate" / "Building" / "Collector" / "Tower" / "HarvestSite"
            public Vector3 Position;          // cached: structures do not move mid-assault
            public float DistanceFromCore;
            public bool IsWall;               // feeds the median-wall-distance front line
            public Func<float> Damage;        // 0..1, 1 = destroyed
            public Func<bool> Alive;          // false once the component is gone
            public bool WasAlreadyDamaged;    // already damaged when the assault opened
            public float FirstHitAtSeconds = -1f;
            public float FellAtSeconds = -1f;
        }

        /// <summary>The merged timeline for one display name — what the report consumes.</summary>
        public struct Timing
        {
            /// <summary>Seconds into the assault of the first damage. -1 = never observed.</summary>
            public float FirstHitAtSeconds;
            /// <summary>Seconds into the assault when it broke. -1 = it survived.</summary>
            public float FellAtSeconds;
            /// <summary>How long it lasted once they started on it. -1 = unknown.</summary>
            public float HoldTimeSeconds;
            /// <summary>It was already damaged before this assault — the hold time describes
            /// an earlier fight and must NOT be rendered as this one's.</summary>
            public bool WasAlreadyDamaged;
            /// <summary>World position (the one that fell first, else the first seen).</summary>
            public Vector3 Position;
            /// <summary>Planar distance from the Heart.</summary>
            public float DistanceFromCore;
            /// <summary>Scene-instance key of the anchor structure. NOT a catalog key --
            /// nothing may persist against it (see StructureOutcome.StructureId).</summary>
            public string StructureId;
            /// <summary>Family: "Wall" / "Gate" / "Building" / "Collector" / "Tower" / "HarvestSite".</summary>
            public string StructureType;
            /// <summary>False when no watched structure carried this name.</summary>
            public bool Found;
        }

        private readonly List<Watched> _watched = new List<Watched>();
        private float _nextPollAt;

        /// <summary>Heart position the distances are measured from.</summary>
        public Vector3 CorePosition { get; private set; }

        /// <summary>Median planar distance of the player's WALLS from the Heart — the front-line
        /// radius. 0 when the base has no walls, which correctly collapses the Front band and
        /// lets the report say "you have no front line" instead of inventing one.</summary>
        public float FrontRadius { get; private set; }

        /// <summary>How many structures are being watched (diagnostic / oracle readout).</summary>
        public int WatchedCount => _watched.Count;

        // =====================================================================
        //  Build — ONE sweep, at session open
        // =====================================================================

        /// <summary>
        /// Builds the watch list from the live town. Called once, before the first spawn, so
        /// <see cref="Watched.WasAlreadyDamaged"/> genuinely means "damaged before this assault".
        /// Never throws.
        /// </summary>
        public static StructureVitalsWatch Build(Vector3 corePosition)
        {
            var w = new StructureVitalsWatch { CorePosition = corePosition };

            Guard.Try("Siege", "build structure vitals watch", () =>
            {
                // Wall / Gate / Building through the SAME uniform RepairTarget view
                // WaveDamageReport uses, so names match its rows exactly (the merge is by name).
                w.AddRepairables<WallSegment>(isWall: true, type: "Wall");
                w.AddRepairables<Gate>(isWall: true, type: "Gate");       // a gate is part of the wall ring
                w.AddRepairables<Building>(isWall: false, type: "Building");

                foreach (var c in ResourceCollectorRegistry.All)
                {
                    if (c == null) continue;
                    var def = ResourceBuildingProgression.Find(c.BuildingId);
                    string name = def != null && !string.IsNullOrEmpty(def.DisplayName)
                        ? def.DisplayName : c.BuildingId;
                    var col = c;
                    w.Add(name, col.transform, false, "Collector",
                        () => col == null ? 1f : (col.IsBroken ? 1f : 1f - col.HpFraction),
                        () => col != null);
                }

                foreach (var t in UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None))
                {
                    if (t == null) continue;
                    var tt = t;
                    string name = tt.Data != null && !string.IsNullOrEmpty(tt.Data.towerName)
                        ? tt.Data.towerName : tt.name;
                    w.Add(name, tt.transform, false, "Tower",
                        () => tt == null ? 1f : (tt.IsBroken ? 1f : 1f - tt.HpFraction),
                        () => tt != null);
                }
                foreach (var t in UnityEngine.Object.FindObjectsByType<DefenseTower>(FindObjectsSortMode.None))
                {
                    // Garrison turrets are ENEMY assets — never a player row (Collect's rule).
                    if (t == null || t.Allegiance != TowerAllegiance.PlayerOwned) continue;
                    var tt = t;
                    w.Add(tt.name, tt.transform, false, "Tower",
                        () => tt == null ? 1f : (tt.IsBroken ? 1f : 1f - tt.HpFraction),
                        () => tt != null);
                }
                foreach (var t in UnityEngine.Object.FindObjectsByType<ArcaneTower>(FindObjectsSortMode.None))
                {
                    if (t == null) continue;
                    var tt = t;
                    w.Add(tt.name, tt.transform, false, "Tower",
                        () => tt == null ? 1f : (tt.IsBroken ? 1f : 1f - tt.HpFraction),
                        () => tt != null);
                }
                foreach (var h in UnityEngine.Object.FindObjectsByType<HarvestSite>(FindObjectsSortMode.None))
                {
                    if (h == null || !h.IsClaimed) continue;
                    var hh = h;
                    w.Add($"{hh.ResourceType} Harvest Site", hh.transform, false, "HarvestSite",
                        () => hh == null ? 1f : (hh.IsBroken ? 1f : 1f - hh.HpFraction),
                        () => hh != null);
                }

                w.ComputeFrontRadius();
            });

            FlowTrace.Step("Siege",
                $"vitals watch built: {w.WatchedCount} structures, frontRadius={w.FrontRadius:F1}m " +
                $"(median wall distance; 0 = no walls).");
            return w;
        }

        private void AddRepairables<T>(bool isWall, string type) where T : Component
        {
            foreach (var s in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (s == null) continue;
                var target = RepairTarget.TryWrap(s);
                if (target == null) continue;
                var t = target;
                Add(t.DisplayName, s.transform, isWall, type,
                    () => t.IsValid ? t.DamageFraction : 1f,
                    () => t.IsValid);
            }
        }

        private void Add(string name, Transform tr, bool isWall, string type,
                         Func<float> damage, Func<bool> alive)
        {
            if (tr == null || damage == null) return;
            Vector3 pos = tr.position;
            float dist = Vector3.ProjectOnPlane(pos - CorePosition, Vector3.up).magnitude;

            float startDamage = 0f;
            try { startDamage = damage(); } catch { /* Guard'd by the caller; treat as pristine */ }

            _watched.Add(new Watched
            {
                Name = string.IsNullOrEmpty(name) ? "Structure" : name,
                Id = tr.gameObject.name,
                Type = type,
                Position = pos,
                DistanceFromCore = dist,
                IsWall = isWall,
                Damage = damage,
                Alive = alive ?? (() => true),
                // Already damaged when the assault opened -> its hold time belongs to an
                // EARLIER fight. Recorded so the report says so instead of reporting a
                // collapse that never happened.
                WasAlreadyDamaged = startDamage > HitThreshold,
            });
        }

        /// <summary>Median wall distance = the front-line radius. Median, not mean, so one
        /// stray outpost wall does not drag the whole line outward.</summary>
        private void ComputeFrontRadius()
        {
            var wallDistances = new List<float>();
            for (int i = 0; i < _watched.Count; i++)
                if (_watched[i].IsWall) wallDistances.Add(_watched[i].DistanceFromCore);
            if (wallDistances.Count == 0) { FrontRadius = 0f; return; }
            wallDistances.Sort();
            FrontRadius = wallDistances[wallDistances.Count / 2];
        }

        // =====================================================================
        //  Poll — cheap, 4 Hz, driven from the session's observe tick
        // =====================================================================

        /// <summary>
        /// Samples every watched structure if the poll interval has elapsed. Never throws.
        /// </summary>
        public void Poll(float elapsedSeconds)
        {
            if (elapsedSeconds < _nextPollAt) return;
            _nextPollAt = elapsedSeconds + PollInterval;

            Guard.Try("Siege", "poll structure vitals", () =>
            {
                for (int i = 0; i < _watched.Count; i++)
                {
                    var w = _watched[i];

                    // A structure whose component is gone counts as fallen at this sample.
                    bool gone = !w.Alive();
                    float dmg = gone ? 1f : w.Damage();

                    if (w.FirstHitAtSeconds < 0f && dmg > HitThreshold && !w.WasAlreadyDamaged)
                        w.FirstHitAtSeconds = elapsedSeconds;

                    if (w.FellAtSeconds < 0f && dmg >= 1f - HitThreshold)
                    {
                        w.FellAtSeconds = elapsedSeconds;
                        FlowTrace.Step("Siege",
                            $"FELL {w.Name} at t={elapsedSeconds:F1}s " +
                            $"(held {(w.FirstHitAtSeconds >= 0f ? (elapsedSeconds - w.FirstHitAtSeconds) : -1f):F1}s, " +
                            $"{w.DistanceFromCore:F0}m from the Heart).");
                    }
                }
            });
        }

        // =====================================================================
        //  Resolve — merged timing for one display name
        // =====================================================================

        /// <summary>
        /// The merged timeline for every watched structure with this display name.
        /// See the file header on name collisions: EARLIEST hit, EARLIEST fall.
        /// <paramref name="assaultSeconds"/> is the assault's total length, used to give a
        /// surviving structure a hold time ("held the whole fight").
        /// </summary>
        public Timing Resolve(string name, float assaultSeconds)
        {
            var t = new Timing
            {
                FirstHitAtSeconds = -1f,
                FellAtSeconds = -1f,
                HoldTimeSeconds = -1f,
                Found = false,
            };
            if (string.IsNullOrEmpty(name)) return t;

            Watched first = null, fellFirst = null;
            bool anyPreExisting = false;

            for (int i = 0; i < _watched.Count; i++)
            {
                var w = _watched[i];
                if (!string.Equals(w.Name, name, StringComparison.Ordinal)) continue;

                t.Found = true;
                if (first == null) first = w;
                if (w.WasAlreadyDamaged) anyPreExisting = true;

                if (w.FirstHitAtSeconds >= 0f
                    && (t.FirstHitAtSeconds < 0f || w.FirstHitAtSeconds < t.FirstHitAtSeconds))
                    t.FirstHitAtSeconds = w.FirstHitAtSeconds;

                if (w.FellAtSeconds >= 0f
                    && (t.FellAtSeconds < 0f || w.FellAtSeconds < t.FellAtSeconds))
                {
                    t.FellAtSeconds = w.FellAtSeconds;
                    fellFirst = w;
                }
            }

            if (!t.Found) return t;

            var anchor = fellFirst ?? first;
            t.Position = anchor.Position;
            t.DistanceFromCore = anchor.DistanceFromCore;
            t.StructureId = anchor.Id;
            t.StructureType = anchor.Type;
            t.WasAlreadyDamaged = anyPreExisting;

            // Hold time: from first contact to its fall, or to the end of the assault if it
            // survived. -1 stays -1 — an UNKNOWN hold time is a real state and must never be
            // rendered as "fell in 0s", which would send the player to move the wrong thing.
            if (t.FirstHitAtSeconds >= 0f)
            {
                float end = t.FellAtSeconds >= 0f ? t.FellAtSeconds : Mathf.Max(0f, assaultSeconds);
                t.HoldTimeSeconds = Mathf.Max(0f, end - t.FirstHitAtSeconds);
            }

            return t;
        }
    }
}
