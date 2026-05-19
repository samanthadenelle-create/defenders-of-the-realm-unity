// =============================================================================
// WaveSpawnPoint — an enemy-wave materialization marker (Week-3 skeleton).
// -----------------------------------------------------------------------------
// docs/avalon-village-layout-spec.md §8.3: one invisible marker sits beyond
// each cardinal gate, ~5 hexes out along the approach lane. The Hollow Ones
// materialize here and walk the approach road toward the gate.
//
// Week-3 depth: an empty-GameObject marker with identity + the cardinal gate it
// feeds. The wave manager that actually spawns enemies at these points lands in
// Week 4 (port spec Part 5). The marker draws a gizmo so the spawn ring is
// visible in the Scene view while authoring.
//
// Instantiated + Configure()'d by the Editor-side VillageSceneBuilder. The
// Editor asmdef does not reference DeNelle.Village, so Configure() is invoked
// by reflection (same pattern as Building / Gate / WallSegment).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// An enemy-wave spawn marker placed beyond one cardinal gate, at the end
    /// of that gate's approach lane. Holds its stable id and the gate index it
    /// feeds; the Week-4 wave manager reads these to materialize the Hollow
    /// Ones. Instantiated by <see cref="VillageController"/> / the scene builder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveSpawnPoint : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Stable id -- spawn-0 (N) .. spawn-3 (W). Parallels the gate ids.")]
        [SerializeField] private string _spawnId;

        [Tooltip("Index of the cardinal gate this spawn feeds -- 0 N, 1 E, 2 S, 3 W.")]
        [SerializeField] private int _gateIndex;

        [Tooltip("Cardinal direction label -- north / east / south / west.")]
        [SerializeField] private string _direction = "north";

        [Tooltip("World position of the gate this spawn marches toward (for the path heading).")]
        [SerializeField] private Vector3 _gatePosition;

        /// <summary>Stable id -- <c>spawn-0</c> .. <c>spawn-3</c>.</summary>
        public string SpawnId => _spawnId;

        /// <summary>Index of the cardinal gate this spawn feeds (0 N .. 3 W).</summary>
        public int GateIndex => _gateIndex;

        /// <summary>Cardinal direction label of the fed gate.</summary>
        public string Direction => _direction;

        /// <summary>World position of the gate the wave marches toward.</summary>
        public Vector3 GatePosition => _gatePosition;

        /// <summary>Unit heading from this spawn point toward its gate.</summary>
        public Vector3 HeadingToGate
        {
            get
            {
                Vector3 d = _gatePosition - transform.position;
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
            }
        }

        /// <summary>
        /// Wires this spawn marker's identity. Called right after instantiation
        /// by <see cref="VillageController"/> / the Editor scene builder.
        /// </summary>
        /// <param name="spawnId">Stable id -- e.g. <c>spawn-0</c>.</param>
        /// <param name="gateIndex">Cardinal gate index this spawn feeds (0 N .. 3 W).</param>
        /// <param name="direction">Cardinal direction label.</param>
        /// <param name="gatePosition">World position of the gate the wave heads toward.</param>
        public void Configure(string spawnId, int gateIndex, string direction, Vector3 gatePosition)
        {
            _spawnId = spawnId;
            _gateIndex = gateIndex;
            _direction = direction;
            _gatePosition = gatePosition;
        }

#if UNITY_EDITOR
        // A visible-in-Scene-view marker; spawn points are invisible in-game.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.86f, 0.27f, 0.27f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 1.5f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f,
                transform.position + HeadingToGate * 3f + Vector3.up * 0.2f);
        }
#endif
    }
}
