// =============================================================================
// WaveSpawnResolver - deterministic WaveSpawnPoint selection (2026-08-16).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE DEFECT THIS CLOSES (verified at source):
//   WaveManager released a wave's authored BOSS with a hardcoded
//   SpawnPoint = "spawn-0" and a comment claiming it arrived "at the north spawn".
//   The only live producer of WaveSpawnPoints is CastleSpawnPointInjector, which
//   emits ids shaped "spawn-castle-{south|west|north|east}-{0..4}"
//   (CastleSpawnPointInjector.cs:156). "spawn-0" therefore NEVER matched, so
//   WaveManager.FindSpawnPoint fell through to the FIRST element of a list built
//   from FindObjectsByType - which is UNORDERED - and warned via Debug.LogWarning,
//   invisible to the F8 harness. The boss entered from a random side of the castle
//   every session and nothing said so.
//
// WHAT THIS DOES:
//   * Orders candidates DETERMINISTICALLY (ordinal by SpawnId) so a fallback is at
//     least reproducible instead of dependent on scene enumeration order.
//   * Resolves a boss to the PREFERRED cardinal direction (the north side the
//     original comment promised), reading WaveSpawnPoint.Direction - the value
//     CastleSpawnPointInjector actually stamps - rather than guessing an id string.
//   * Always reports WHY, so the caller can FlowTrace at the right severity: an
//     exact preferred-direction hit is a Step, a deterministic fallback is a Warn,
//     and no spawn point at all is a Fail.
//
// PURE + SCENE-FREE: takes the candidate list, touches no singletons, so a
// regression can hand it hand-built markers and assert the real behaviour.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Village
{
    /// <summary>
    /// Deterministic <see cref="WaveSpawnPoint"/> selection for wave and boss
    /// releases. Never silently picks an arbitrary gate - every result carries a
    /// reason string and an exactness flag the caller instruments on.
    /// </summary>
    public static class WaveSpawnResolver
    {
        /// <summary>
        /// The side a wave's authored boss should march in from. Matches the
        /// direction label CastleSpawnPointInjector stamps ("north"/"east"/
        /// "south"/"west"), NOT an id string, so a renamed id cannot break it.
        /// </summary>
        public const string PreferredBossDirection = "north";

        /// <summary>
        /// Picks the spawn point an authored boss releases from.
        /// </summary>
        /// <param name="points">Candidate markers (nulls tolerated).</param>
        /// <param name="reason">Always set - why this result was chosen.</param>
        /// <param name="matchedPreferred">
        /// TRUE only when a marker in <see cref="PreferredBossDirection"/> was found.
        /// FALSE with a non-null result means a deterministic fallback: the boss will
        /// enter from a side nobody authored, which the caller must surface.
        /// </param>
        /// <returns>The chosen marker, or null when there are none at all.</returns>
        public static WaveSpawnPoint ResolveBossSpawn(
            IReadOnlyList<WaveSpawnPoint> points, out string reason, out bool matchedPreferred)
        {
            matchedPreferred = false;

            List<WaveSpawnPoint> ordered = OrderedCandidates(points);
            if (ordered.Count == 0)
            {
                reason = "no WaveSpawnPoint markers exist in the scene";
                return null;
            }

            foreach (WaveSpawnPoint p in ordered)
            {
                if (!string.Equals(p.Direction, PreferredBossDirection,
                                   System.StringComparison.OrdinalIgnoreCase)) continue;
                matchedPreferred = true;
                reason = "resolved to the '" + PreferredBossDirection + "' spawn '" + p.SpawnId + "'";
                return p;
            }

            reason = "no '" + PreferredBossDirection + "' spawn marker exists (" + ordered.Count +
                     " marker(s) present); falling back to the deterministic first, '" +
                     ordered[0].SpawnId + "' on the '" + ordered[0].Direction + "' side";
            return ordered[0];
        }

        /// <summary>
        /// The deterministic first candidate (ordinal by <see cref="WaveSpawnPoint.SpawnId"/>).
        /// Replaces "whatever FindObjectsByType returned first", so a fallback is at least
        /// the same gate every session. Null when there are no valid markers.
        /// </summary>
        public static WaveSpawnPoint FirstDeterministic(IReadOnlyList<WaveSpawnPoint> points)
        {
            List<WaveSpawnPoint> ordered = OrderedCandidates(points);
            return ordered.Count > 0 ? ordered[0] : null;
        }

        private static List<WaveSpawnPoint> OrderedCandidates(IReadOnlyList<WaveSpawnPoint> points)
        {
            var ordered = new List<WaveSpawnPoint>();
            if (points == null) return ordered;
            for (int i = 0; i < points.Count; i++)
                if (points[i] != null) ordered.Add(points[i]);
            ordered.Sort((a, b) => string.CompareOrdinal(a.SpawnId ?? string.Empty,
                                                         b.SpawnId ?? string.Empty));
            return ordered;
        }
    }
}
