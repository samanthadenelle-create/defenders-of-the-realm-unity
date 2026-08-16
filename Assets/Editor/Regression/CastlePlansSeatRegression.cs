// =============================================================================
// CastlePlansSeatRegression [castle-plans-seat] -- WO-1105 guardrails for WHERE the
// Castle Defense Plans drop is seated.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Sister suite to CastlePlansUnlockRegression
// (which pins WHETHER/WHEN the drop spawns and what it grants). This one pins the
// two placement defects behind owner F8 seq 2505 -- "Im on wave five and still
// cannot build arcane towers", where the drop HAD spawned correctly at wave 3 and
// the player never found it because it was standing OUTSIDE the wall:
//
//   1. INSIDE THE PERIMETER -- the resolved seat's flat distance from the Heart is
//      STRICTLY LESS than the gate ring, i.e. inside the wall line. This is the
//      assertion the old code could not have passed: it pulled a fixed 8 m off a
//      marker that sits 12 m outside a ~40.8 m gate ring, landing at ~44.8 -- about
//      4 m OUTSIDE the wall -- while its own comment claimed "well inside".
//      "Pulled some amount" is deliberately NOT what is asserted here; the ring is
//      READ from the candidates' authored gate positions, never restated.
//
//   2. DETERMINISTIC SEAT -- the four cardinal markers are EQUIDISTANT from the
//      centre, so the retired "nearest by sqrMagnitude" compare resolved on
//      FindObjectsByType iteration order. Feeding the SAME candidate set in every
//      rotation + the reverse must yield the SAME seat and the SAME chosen marker.
//
//   3. UNAUTHORED-MARKER FALLBACK -- a marker whose GatePosition was never
//      Configure()'d still seats inside, by walking the authored gate-to-spawn
//      offset back in. (A bake that forgets Configure must not put the prop in the
//      field again.)
//
// The oracle is PURE -- CastleDefensePlansService.TryResolveSpawnSeat takes a
// candidate list, so no scene, no Physics, no play mode, and no dependence on what
// FindObjectsByType returns. Geometry comes from the real Main_Castle_Overworld
// WaveSpawnPoint rows, and the inset/offset come from the service's own public
// constants -- no number in this file is a restatement of one in the code.
//
// Marker: CASTLE_PLANS_SEAT_OK / CASTLE_PLANS_SEAT_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "castle-plans-seat suite", () => { if (!CastlePlansSeatRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[castle-plans-seat] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Village;
using Seat = DeNelle.Village.CastleDefensePlansService.SeatCandidate;

namespace DeNelle.Editor
{
    public static class CastlePlansSeatRegression
    {
        // The four cardinal WaveSpawnPoint rows as Main_Castle_Overworld actually bakes
        // them (CastleHubBuilder.PlaceCastleSpawnPoints -- marker 12 m outside its gate).
        // Marker position and authored gate position, verbatim from the scene.
        private static List<Seat> HubCandidates() => new List<Seat>
        {
            new Seat("WaveSpawnPoint-S", new Vector3(-4.37f,   0f, -52.6f),  new Vector3(-4.37f,   0f, -40.6f)),
            new Seat("WaveSpawnPoint-W", new Vector3(-52.6f,   0f,   4.37f), new Vector3(-40.6f,   0f,   4.37f)),
            new Seat("WaveSpawnPoint-N", new Vector3(  4.37f,  0f,  52.6f),  new Vector3(  4.37f,  0f,  40.6f)),
            new Seat("WaveSpawnPoint-E", new Vector3( 52.6f,   0f,  -4.37f), new Vector3( 40.6f,   0f,  -4.37f)),
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CASTLE PLANS SEAT (WO-1105 inside-the-wall / deterministic gate) ---");

            try
            {
                var hub = HubCandidates();

                // The gate ring is READ from the candidates, not restated: every cardinal
                // gate sits on it, so its flat radius is the wall line the seat must be inside.
                float ring = float.MaxValue;
                for (int i = 0; i < hub.Count; i++)
                    ring = Mathf.Min(ring, Flat(hub[i].GatePosition));
                log.AppendLine($"  gate ring (read from authored GatePosition rows): {ring:0.00} m");

                // ---- 1: the seat lands INSIDE the wall line ------------------------
                if (!CastleDefensePlansService.TryResolveSpawnSeat(hub, out var seat, out var source))
                {
                    failures.Add("[castle-plans-seat] TryResolveSpawnSeat returned FALSE on the real hub markers -- the drop would fall back to the Heart approach instead of a gate");
                }
                else
                {
                    float d = Flat(seat);
                    log.AppendLine($"  resolved seat {seat} -> {d:0.00} m from the Heart (source={source})");
                    if (d >= ring)
                        failures.Add($"[castle-plans-seat] seat is {d:0.00} m out, at or BEYOND the {ring:0.00} m gate ring -- the drop stands OUTSIDE the wall, exactly the WO-1105 defect (owner F8 seq 2505: spawned at wave 3, never findable)");
                    else
                        log.AppendLine($"  inside the wall line OK ({d:0.00} m < {ring:0.00} m)");

                    // ...and inside by the service's OWN inset, read from the constant.
                    float expected = ring - CastleDefensePlansService.GateInsetMetres;
                    if (Mathf.Abs(d - expected) > 0.5f)
                        failures.Add($"[castle-plans-seat] seat sits {d:0.00} m out but the gate ring ({ring:0.00}) minus GateInsetMetres ({CastleDefensePlansService.GateInsetMetres}) is {expected:0.00} -- the seat is no longer derived from the gate");

                    // Not degenerate: it must still be OUT at the gate, never dumped at the Heart.
                    if (d < 1f)
                        failures.Add($"[castle-plans-seat] seat collapsed to the town centre ({d:0.00} m) -- the drop belongs at the gate mouth, not on the Heart");
                }

                // ---- 2: the seat is DETERMINISTIC across candidate ORDER ------------
                // Every rotation of the list, plus the reverse: the old strict-less-than
                // distance compare would pick whichever equidistant marker came first.
                var orders = new List<List<Seat>>();
                for (int r = 0; r < hub.Count; r++)
                {
                    var rot = new List<Seat>(hub.Count);
                    for (int i = 0; i < hub.Count; i++) rot.Add(hub[(i + r) % hub.Count]);
                    orders.Add(rot);
                }
                var rev = new List<Seat>(hub);
                rev.Reverse();
                orders.Add(rev);

                Vector3? firstSeat = null;
                string firstSource = null;
                bool drifted = false;
                for (int i = 0; i < orders.Count; i++)
                {
                    if (!CastleDefensePlansService.TryResolveSpawnSeat(orders[i], out var s, out var src))
                    { failures.Add($"[castle-plans-seat] permutation {i} failed to resolve a seat at all"); drifted = true; continue; }

                    if (firstSeat == null) { firstSeat = s; firstSource = src; continue; }
                    if ((s - firstSeat.Value).sqrMagnitude > 0.0001f || !string.Equals(src, firstSource, StringComparison.Ordinal))
                    {
                        failures.Add($"[castle-plans-seat] permutation {i} resolved {s} ({src}) but permutation 0 resolved {firstSeat.Value} ({firstSource}) -- the seat depends on ITERATION ORDER, so the same save drops at a different gate run to run");
                        drifted = true;
                    }
                }
                if (!drifted && firstSource != null)
                    log.AppendLine($"  deterministic across {orders.Count} candidate orderings OK (always {firstSource})");

                // ---- 3: an unauthored marker still seats inside ---------------------
                // GatePosition left at zero (Configure never ran) -- the anchor must be
                // recovered from the authored gate-to-spawn offset, not trusted as (0,0,0).
                var unauthored = new List<Seat>();
                for (int i = 0; i < hub.Count; i++)
                    unauthored.Add(new Seat(hub[i].Name, hub[i].Position, Vector3.zero));

                if (!CastleDefensePlansService.TryResolveSpawnSeat(unauthored, out var fbSeat, out var fbSource))
                {
                    failures.Add("[castle-plans-seat] markers with an unconfigured GatePosition resolved NO seat -- a bake that forgets Configure() loses the drop entirely");
                }
                else
                {
                    float fd = Flat(fbSeat);
                    log.AppendLine($"  unauthored-GatePosition fallback -> {fd:0.00} m (source={fbSource})");
                    if (fd >= ring)
                        failures.Add($"[castle-plans-seat] unauthored-marker fallback seated {fd:0.00} m out, at or beyond the {ring:0.00} m ring -- still outside the wall");
                    if (fd < 1f)
                        failures.Add($"[castle-plans-seat] unauthored-marker fallback collapsed to the town centre ({fd:0.00} m)");
                }

                // ---- guard: no candidates -> the caller's fallback owns it ----------
                if (CastleDefensePlansService.TryResolveSpawnSeat(new List<Seat>(), out _, out _))
                    failures.Add("[castle-plans-seat] an EMPTY candidate list reported a resolved seat -- the near-Heart fallback would never run");
                if (CastleDefensePlansService.TryResolveSpawnSeat(null, out _, out _))
                    failures.Add("[castle-plans-seat] a NULL candidate list reported a resolved seat");
            }
            catch (Exception ex)
            {
                failures.Add($"castle-plans-seat oracle threw: {ex.GetType().Name}: {ex.Message}");
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        /// <summary>Flat (XZ) distance from the Heart at the origin -- the y axis is the
        /// ground snap and says nothing about being inside the wall.</summary>
        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "CASTLE_PLANS_SEAT_OK");
                return "CASTLE PLANS SEAT OK -- drop seats inside the gate ring, derived from the marker's authored gate position, and the chosen gate is order-independent";
            }
            string reason = "castle-plans-seat: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CASTLE_PLANS_SEAT_FAIL: " + reason);
            return reason;
        }
    }
}
