// =============================================================================
// RaidDeployLog — a SIMPLE record of what the player deployed, when, and where
// during a raid (WO-771.6 V1 "re-watch", LOCKED teleport/deploy loop).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// This is the V1 "replay" surface: NOT a byte-exact deterministic input log (that
// is V2 / WO-771.7 with the fixed-point sim). It is a plain list of deploy events
// so a re-watch UI (or a post-raid summary) can narrate the assault order. Each
// entry stamps the troop def id, the elapsed seconds at drop, and the ground XZ.
// RaidScoring owns the live log; callers read RaidScoring.DeployLog.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>One deploy event: which troop, at what raid-clock second, where.</summary>
    public struct RaidDeployEntry
    {
        /// <summary>The TroopDefId dropped (e.g. troop-footman).</summary>
        public string TroopId;
        /// <summary>Raid-clock seconds elapsed at the moment of the drop.</summary>
        public float AtSeconds;
        /// <summary>Ground X of the drop (world space).</summary>
        public float X;
        /// <summary>Ground Z of the drop (world space).</summary>
        public float Z;

        public RaidDeployEntry(string troopId, float atSeconds, Vector3 worldPos)
        {
            TroopId = troopId;
            AtSeconds = atSeconds;
            X = worldPos.x;
            Z = worldPos.z;
        }
    }

    /// <summary>
    /// A simple ordered record of a raid's deploy events (for re-watch / summary).
    /// V1 only — not the V2 deterministic input log. Append via <see cref="Record"/>.
    /// </summary>
    public sealed class RaidDeployLog
    {
        /// <summary>The deploy events in the order they happened.</summary>
        public readonly List<RaidDeployEntry> Entries = new List<RaidDeployEntry>();

        /// <summary>Total deploy events recorded.</summary>
        public int Count => Entries.Count;

        /// <summary>Append one deploy event (troop dropped at <paramref name="atSeconds"/>).</summary>
        public void Record(string troopId, float atSeconds, Vector3 worldPos)
        {
            Entries.Add(new RaidDeployEntry(troopId, atSeconds, worldPos));
        }

        /// <summary>Drop all recorded events (a fresh raid / re-arm).</summary>
        public void Clear() => Entries.Clear();
    }
}
