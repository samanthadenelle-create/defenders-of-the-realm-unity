// =============================================================================
// CastleSpawnPointInjector — runtime, NON-DESTRUCTIVE placement of enemy WAVE
// SPAWN POINTS in the Central Castle Hub (MainCastle_Hall) so the wave loop can
// actually materialize enemies.
// -----------------------------------------------------------------------------
// WHY a runtime injector (not a scene edit / regen):
//   WaveManager finds its spawn markers via FindObjectsByType<WaveSpawnPoint>()
//   at Start (WaveManager.cs ~512). All the WaveSpawnPoints used to live in
//   Village2 — MainCastle_Hall has ZERO of them, so a wave spawns nothing.
//   Re-saving / regenerating MainCastle_Hall.unity to bake markers in carries
//   the project's scene-resave corruption risk (CLAUDE.md §3). So, mirroring
//   CastleVendorNpcInjector exactly, this self-bootstrapping DDOL singleton
//   FINDS the castle on every MainCastle_Hall load and spawns a cluster of
//   WaveSpawnPoint markers OUTSIDE the main gate — WITHOUT touching the .unity
//   file. Idempotent per load (named holder, cleared + respawned).
//
// GEOMETRY (CastleHubBuilder, root at world origin so local == world):
//   - Heart / centre of the castle at (0,0,0).
//   - Single south Main Gate at (0,0,-50); drawbridge at (0,0,-58);
//     WorldGate connect marker at (0,0,-68). Walls span ±44.
//   Enemies must approach FROM OUTSIDE the gate (south, beyond the drawbridge)
//   and march NORTH (+Z) toward the Heart. So the spawn cluster sits at
//   z ~ -62..-66 (just past the drawbridge), fanned across the approach lane.
//
// WaveSpawnPoint requirements (read from WaveSpawnPoint.cs — NOT guessed):
//   Just an empty GameObject + the WaveSpawnPoint component. No tag, no layer,
//   no collider. Identity is set via Configure(spawnId, gateIndex, direction,
//   gatePosition); gatePosition feeds HeadingToGate so spawned enemies face the
//   gate. WaveManager reads .transform.position + .HeadingToGate + .SpawnId.
//
// NavMesh: each marker is snapped onto the baked walkable floor via
//   NavMesh.SamplePosition so the enemies WaveManager instantiates at the
//   marker land on the mesh and can path to the Heart.
//
// Village -> Core only. No reflection, no cross-asmdef ref (WaveSpawnPoint lives
// in this same DeNelle.Village assembly).
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime, non-destructive placement of enemy wave spawn points outside the castle gate.</summary>
    public sealed class CastleSpawnPointInjector : MonoBehaviour
    {
        public static CastleSpawnPointInjector Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";
        private const string HolderName = "[CastleSpawnPoints] (runtime)";

        // The Heart / castle centre — the gate the waves march toward sits between
        // the spawn cluster and here. WaveSpawnPoint.gatePosition feeds HeadingToGate.
        private static readonly Vector3 GatePosition = new Vector3(0f, 0f, -50f);

        // Spawn cluster: just past the drawbridge (z=-58) on the south approach lane,
        // fanned out so enemies don't stack. All march NORTH (+Z) through the gate to
        // the Heart at origin. One central + two flanking + two wide flanking.
        private static readonly Vector3[] SpawnLocalPositions =
        {
            new Vector3(  0f, 0f, -64f),   // dead-centre on the approach road
            new Vector3(-12f, 0f, -62f),   // left flank
            new Vector3( 12f, 0f, -62f),   // right flank
            new Vector3(-22f, 0f, -60f),   // wide left
            new Vector3( 22f, 0f, -60f),   // wide right
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("CastleSpawnPointInjector").AddComponent<CastleSpawnPointInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Inject();
        }

        private void Inject()
        {
            // Idempotent: nuke any prior runtime holder so a re-load doesn't double-spawn.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);

            // If the scene somehow already HAS real WaveSpawnPoints (e.g. baked in
            // later), don't compete — WaveManager would find both. Skip injection.
            var existing = FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
            {
                Debug.Log($"[CastleSpawnPointInjector] {existing.Length} WaveSpawnPoint(s) already present — skipping injection.");
                return;
            }

            var holder = new GameObject(HolderName);

            int placed = 0;
            for (int i = 0; i < SpawnLocalPositions.Length; i++)
            {
                Vector3 pos = SpawnLocalPositions[i];

                // Snap onto the baked walkable floor so enemies spawned here land on the
                // NavMesh and can path to the Heart. Generous radius (8m) because the
                // approach lane geometry may not be perfectly centred under the marker.
                if (NavMesh.SamplePosition(pos, out var hit, 8f, NavMesh.AllAreas))
                    pos = hit.position;

                var go = new GameObject($"WaveSpawnPoint_Castle_{i}");
                go.transform.SetParent(holder.transform, false);
                go.transform.position = pos;

                var sp = go.AddComponent<WaveSpawnPoint>();
                // gateIndex 2 = South, direction "south" — matches the cardinal
                // convention (0 N, 1 E, 2 S, 3 W). The castle has a single south gate.
                sp.Configure($"spawn-castle-{i}", 2, "south", GatePosition);

                placed++;
            }

            Debug.Log($"[CastleSpawnPointInjector] placed {placed} wave spawn points outside the south gate (approach z~-60..-64, marching to Heart at origin).");
        }
    }
}
