// =============================================================================
// DungeonBakerChecks — the PURE, shared Room Forge mate / seal / verify / overlap
// logic AND the compose loop.
// -----------------------------------------------------------------------------
// Single source of truth for the door-touch-door contract. Lives in the runtime
// DeNelle.Dungeons assembly (NOT the editor DungeonBaker) so BOTH the editor baker
// (DeNelle.Editor.RoomForge.DungeonBaker) AND the headless regression oracle
// (DeNelle.Editor.Regression.RoomForgeRegression, in DeNelle.EditorRegression) can
// call the exact same code with no assembly cycle. UnityEngine-only (NO UnityEditor)
// so it is safe in a runtime assembly and never duplicated in the test.
//
// WO-745 §2 contract this pins:
//   fix 1 — a mate failure means the bake must ABORT (caller reads ComposeOutcome.Aborted).
//   fix 2 — after all connections mate, EVERY connection is re-verified (drift) and
//           room footprints are AABB-overlap checked; either is a failure.
// WO-745 §3 — Compose emits the [Flow:DungeonBake] band (per-connection reason enum,
//   seal events) so the baker and the oracle harvest the same trace.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Dungeons.RoomForge
{
    /// <summary>Why a single connection failed to mate (WO-745 §3 FlowTrace enum).</summary>
    public enum MateFailReason
    {
        None = 0,
        MissingInstance,   // fromInstance / toInstance not placed in the layout
        MissingSocket,     // named socket not found on the room prefab
        TypeMismatch,      // socket types are not compatible (door<->stair, etc.)
        Distance,          // still farther apart than maxMateDistance after the nudge
        Alignment,         // sockets do not oppose (align < threshold)
        Drift,             // an earlier mate drifted apart when a later nudge moved a room
        Overlap,           // two room footprints overlap beyond tolerance
    }

    /// <summary>Outcome of a single mate attempt (dist/align/nudge are for the trace line).</summary>
    public struct MateResult
    {
        public bool ok;
        public MateFailReason reason;
        public float dist;
        public float align;
        public float nudge;

        public static MateResult Fail(MateFailReason r, float dist = 0f, float align = 0f, float nudge = 0f)
            => new MateResult { ok = false, reason = r, dist = dist, align = align, nudge = nudge };
        public static MateResult Ok(float dist, float align, float nudge)
            => new MateResult { ok = true, reason = MateFailReason.None, dist = dist, align = align, nudge = nudge };
    }

    /// <summary>Per-connection result recorded by <see cref="DungeonBakerChecks.Compose"/>.</summary>
    public sealed class ConnectionOutcome
    {
        public string connId;
        public bool ok;
        public MateFailReason reason;
        public float dist, align, nudge;
    }

    /// <summary>Whole-layout compose result (the numbers the summary + oracle assert on).</summary>
    public sealed class ComposeOutcome
    {
        public int mateOk;
        public int mateFail;      // missing/type/distance/alignment failures
        public int driftFail;     // fix-2 re-verify failures
        public int overlapFail;   // fix-2 AABB overlap failures
        public int sealedN;       // unmated sockets sealed (wall or secret)
        public readonly List<ConnectionOutcome> connections = new List<ConnectionOutcome>();
        public readonly List<string> driftedConns = new List<string>();
        public readonly List<string> overlaps = new List<string>();

        /// <summary>Total connection-level failures reported in matesFail= of the summary.</summary>
        public int ConnectionFail => mateFail + driftFail;
        /// <summary>WO-745 fix 1: any failure => the bake must abort (no scene, no Build Settings).</summary>
        public bool Aborted => (mateFail + driftFail + overlapFail) > 0;
    }

    /// <summary>Pure door-touch-door checks + compose loop shared by the baker and the oracle.</summary>
    public static class DungeonBakerChecks
    {
        public const string Sys = "DungeonBake";
        /// <summary>Default gap tolerance (units) when a layout does not set maxMateDistance.</summary>
        public const float DefaultMaxMateDistance = 1.25f;
        /// <summary>Opposing-normal dot must be at least this to count as a facing mate.</summary>
        public const float AlignThreshold = 0.25f;
        /// <summary>AABB penetration below this (units) is a shared wall/touch, not an overlap.</summary>
        public const float OverlapTolerance = 0.05f;

        /// <summary>Socket-type compatibility matrix (Door&lt;-&gt;Arch, StairUp&lt;-&gt;StairDown).</summary>
        public static bool TypesCompatible(RoomSocketType a, RoomSocketType b)
        {
            if (a == b) return true;
            if (a == RoomSocketType.Door && b == RoomSocketType.Arch) return true;
            if (a == RoomSocketType.Arch && b == RoomSocketType.Door) return true;
            if (a == RoomSocketType.StairUp && b == RoomSocketType.StairDown) return true;
            if (a == RoomSocketType.StairDown && b == RoomSocketType.StairUp) return true;
            return false;
        }

        /// <summary>
        /// Attempt to mate socket <paramref name="bSock"/> (on room <paramref name="bGo"/>) to
        /// <paramref name="aSock"/>. If the sockets are farther apart than <paramref name="maxD"/>,
        /// planar-nudge the WHOLE "to" room so the sockets touch, then re-measure. A mate needs
        /// BOTH dist &lt;= maxD AND opposing outward normals (align &gt;= AlignThreshold). Callers
        /// must have already verified the instances, sockets, and type compatibility.
        /// </summary>
        public static MateResult TryMate(RoomSocket aSock, RoomSocket bSock, GameObject bGo, float maxD)
        {
            if (aSock == null || bSock == null || bGo == null)
                return MateResult.Fail(MateFailReason.MissingSocket);
            if (maxD <= 0f) maxD = DefaultMaxMateDistance;

            float dist = Vector3.Distance(aSock.WorldPosition, bSock.WorldPosition);
            float nudge = 0f;
            // Prefer sliding the "to" room so sockets touch if slightly off grid (planar only).
            if (dist > maxD)
            {
                Vector3 delta = aSock.WorldPosition - bSock.WorldPosition;
                Vector3 planar = new Vector3(delta.x, 0f, delta.z);
                nudge = planar.magnitude;
                bGo.transform.position += planar;
                dist = Vector3.Distance(aSock.WorldPosition, bSock.WorldPosition);
            }

            float align = Vector3.Dot(aSock.Outward.normalized, -bSock.Outward.normalized);
            if (dist > maxD) return MateResult.Fail(MateFailReason.Distance, dist, align, nudge);
            if (align < AlignThreshold) return MateResult.Fail(MateFailReason.Alignment, dist, align, nudge);
            return MateResult.Ok(dist, align, nudge);
        }

        /// <summary>
        /// Fix-2 re-verify: a previously-mated connection must STILL touch and oppose at final
        /// positions (a later connection's nudge can drag a room an earlier one already mated).
        /// </summary>
        public static bool StillMated(RoomSocket aSock, RoomSocket bSock, float maxD)
        {
            if (aSock == null || bSock == null) return false;
            if (maxD <= 0f) maxD = DefaultMaxMateDistance;
            float dist = Vector3.Distance(aSock.WorldPosition, bSock.WorldPosition);
            if (dist > maxD) return false;
            float align = Vector3.Dot(aSock.Outward.normalized, -bSock.Outward.normalized);
            return align >= AlignThreshold;
        }

        /// <summary>
        /// Fix-2 overlap: true when two room footprints penetrate on BOTH X and Z beyond
        /// <paramref name="tolerance"/>. Footprint is the meta's world size (yaw 90/270 swaps
        /// width/depth) centred on the room's world position; a shared wall (touch) is not an
        /// overlap.
        /// </summary>
        public static bool RoomsOverlap(RoomPrefabMeta a, Vector3 aPos, float aYaw,
                                        RoomPrefabMeta b, Vector3 bPos, float bYaw, float tolerance)
        {
            Vector2 ha = HalfExtents(a, aYaw);
            Vector2 hb = HalfExtents(b, bYaw);
            float penX = (ha.x + hb.x) - Mathf.Abs(aPos.x - bPos.x);
            float penZ = (ha.y + hb.y) - Mathf.Abs(aPos.z - bPos.z);
            return penX > tolerance && penZ > tolerance;
        }

        // World half-extents on XZ, accounting for a 90/270 yaw that swaps width/depth.
        private static Vector2 HalfExtents(RoomPrefabMeta meta, float yawDeg)
        {
            Vector2 fp = meta != null ? meta.FootprintWorld : new Vector2(6f, 6f);
            float half = 0.5f;
            int q = Mathf.RoundToInt(Mathf.Repeat(yawDeg, 360f) / 90f) % 4;
            if (q == 1 || q == 3) return new Vector2(fp.y * half, fp.x * half);
            return new Vector2(fp.x * half, fp.y * half);
        }

        /// <summary>Unmated secret socket seals invisibly; a normal one gets a wall box.</summary>
        public static bool SealsAsSecret(RoomSocket s) => s != null && s.isSecret;

        /// <summary>First socket on <paramref name="room"/> whose id matches (deep search).</summary>
        public static RoomSocket FindSocket(GameObject room, string socketId)
        {
            if (room == null || string.IsNullOrEmpty(socketId)) return null;
            foreach (var s in room.GetComponentsInChildren<RoomSocket>(true))
                if (s != null && s.id == socketId) return s;
            return null;
        }

        /// <summary>
        /// Seal one unmated socket. Secret => invisible marker (matedTo="SEALED_SECRET", NO
        /// geometry). Normal => a Seal_&lt;id&gt; wall cube scaled to halfWidth*2 and
        /// matedTo="SEALED_WALL". Returns true if wall geometry was spawned.
        /// </summary>
        public static bool SealSocket(RoomSocket s)
        {
            if (s == null) return false;
            if (s.isSecret)
            {
                s.matedTo = "SEALED_SECRET";
                return false;
            }
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Seal_{s.id}";
            wall.transform.SetParent(s.transform, false);
            wall.transform.localPosition = Vector3.forward * 0.15f;
            wall.transform.localRotation = Quaternion.identity;
            wall.transform.localScale = new Vector3(s.halfWidth * 2f, 2.5f, 0.35f);
            s.matedTo = "SEALED_WALL";
            return true;
        }

        /// <summary>Stable fail-reason key for the [Flow:DungeonBake] trace line (§3 enum).</summary>
        public static string ReasonKey(MateFailReason r)
        {
            switch (r)
            {
                case MateFailReason.MissingInstance: return "missing-instance";
                case MateFailReason.MissingSocket:   return "missing-socket";
                case MateFailReason.TypeMismatch:    return "type-mismatch";
                case MateFailReason.Distance:        return "distance";
                case MateFailReason.Alignment:       return "alignment";
                case MateFailReason.Drift:           return "drift";
                case MateFailReason.Overlap:         return "overlap";
                default:                             return "none";
            }
        }

        /// <summary>
        /// THE compose loop (shared by the editor baker + the headless oracle): mate every
        /// connection, re-verify for drift (fix 2a), AABB-overlap check every room pair (fix 2b),
        /// then seal the unmated. Positions/rotations must already be applied to the instances.
        /// Emits the [Flow:DungeonBake] band per step. Returns the tallies; the caller decides to
        /// abort (fix 1) on <see cref="ComposeOutcome.Aborted"/>.
        /// </summary>
        public static ComposeOutcome Compose(Dictionary<string, GameObject> instances, DungeonComposeLayout layout)
        {
            var outcome = new ComposeOutcome();
            if (instances == null || layout == null) return outcome;

            var rules = layout.rules ?? new ComposeRules();
            float maxD = rules.maxMateDistance > 0f ? rules.maxMateDistance : DefaultMaxMateDistance;
            var matedPairs = new List<ConnMate>();

            if (layout.connections != null)
            {
                foreach (var c in layout.connections)
                {
                    if (c == null) continue;
                    string connId = $"{c.fromInstance}.{c.fromSocket}::{c.toInstance}.{c.toSocket}";
                    var co = new ConnectionOutcome { connId = connId };

                    if (!instances.TryGetValue(c.fromInstance, out var aGo) ||
                        !instances.TryGetValue(c.toInstance, out var bGo))
                    {
                        co.ok = false; co.reason = MateFailReason.MissingInstance;
                        outcome.mateFail++; outcome.connections.Add(co);
                        FlowTrace.Fail(Sys, $"mate FAIL conn={connId} reason=missing-instance");
                        continue;
                    }

                    var aSock = FindSocket(aGo, c.fromSocket);
                    var bSock = FindSocket(bGo, c.toSocket);
                    if (aSock == null || bSock == null)
                    {
                        co.ok = false; co.reason = MateFailReason.MissingSocket;
                        outcome.mateFail++; outcome.connections.Add(co);
                        FlowTrace.Fail(Sys, $"mate FAIL conn={connId} reason=missing-socket " +
                                            $"(from={(aSock == null ? "MISSING" : "ok")} to={(bSock == null ? "MISSING" : "ok")})");
                        continue;
                    }

                    if (!TypesCompatible(aSock.type, bSock.type))
                    {
                        co.ok = false; co.reason = MateFailReason.TypeMismatch;
                        outcome.mateFail++; outcome.connections.Add(co);
                        FlowTrace.Fail(Sys, $"mate FAIL conn={connId} reason=type-mismatch ({aSock.type} vs {bSock.type})");
                        continue;
                    }

                    var r = TryMate(aSock, bSock, bGo, maxD);
                    co.dist = r.dist; co.align = r.align; co.nudge = r.nudge;
                    if (!r.ok)
                    {
                        co.ok = false; co.reason = r.reason;
                        outcome.mateFail++; outcome.connections.Add(co);
                        FlowTrace.Fail(Sys, $"mate FAIL conn={connId} reason={ReasonKey(r.reason)} " +
                                            $"dist={r.dist:F2} align={r.align:F2} nudge={r.nudge:F2}");
                        continue;
                    }

                    aSock.matedTo = connId;
                    bSock.matedTo = connId;
                    matedPairs.Add(new ConnMate { connId = connId, a = aSock, b = bSock });
                    co.ok = true;
                    outcome.mateOk++; outcome.connections.Add(co);
                    FlowTrace.Step(Sys, $"mate OK conn={connId} dist={r.dist:F2} align={r.align:F2} nudge={r.nudge:F2}");
                }
            }

            // fix 2a — re-verify every mated pair still touches + opposes at final pose.
            foreach (var pair in matedPairs)
            {
                if (!StillMated(pair.a, pair.b, maxD))
                {
                    outcome.driftFail++; outcome.driftedConns.Add(pair.connId);
                    FlowTrace.Fail(Sys, $"mate FAIL conn={pair.connId} reason=drift (mated earlier, drifted after a later nudge)");
                }
            }

            // fix 2b — no two room footprints may penetrate beyond tolerance.
            var ids = new List<string>(instances.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var gi = instances[ids[i]]; var gj = instances[ids[j]];
                    if (gi == null || gj == null) continue;
                    var mi = gi.GetComponent<RoomPrefabMeta>();
                    var mj = gj.GetComponent<RoomPrefabMeta>();
                    if (RoomsOverlap(mi, gi.transform.position, gi.transform.eulerAngles.y,
                                     mj, gj.transform.position, gj.transform.eulerAngles.y, OverlapTolerance))
                    {
                        outcome.overlapFail++; outcome.overlaps.Add($"{ids[i]}&{ids[j]}");
                        FlowTrace.Fail(Sys, $"overlap FAIL rooms='{ids[i]}' & '{ids[j]}' reason=overlap " +
                                            $"(footprints penetrate beyond {OverlapTolerance:F2}u)");
                    }
                }
            }

            // Seal the unmated (count is reported in the summary either way).
            if (rules.sealUnmated)
            {
                foreach (var kv in instances)
                {
                    if (kv.Value == null) continue;
                    foreach (var s in kv.Value.GetComponentsInChildren<RoomSocket>(true))
                    {
                        if (s == null || s.IsMated) continue;
                        bool wall = SealSocket(s);
                        outcome.sealedN++;
                        FlowTrace.Step(Sys, $"seal {(wall ? "WALL" : "SECRET")} socket='{s.id}' room='{kv.Key}'");
                    }
                }
            }

            return outcome;
        }

        private struct ConnMate { public string connId; public RoomSocket a; public RoomSocket b; }
    }
}
