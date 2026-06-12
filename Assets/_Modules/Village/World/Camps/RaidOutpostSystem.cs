// =============================================================================
// RaidOutpostSystem - feature flag + self-bootstrapping spawner that drops ONE
// walk-to ENEMY OUTPOST in the open world (the RAID bite of outpost->raid->loot).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Mirrors CampSystem's bootstrap exactly (AfterSceneLoad self-spawn, OuterWorld-
// only, idempotent, feature-flagged) but spawns a single EnemyOutpost the owner
// can WALK TO from the village and fight. ENABLED for testing; flip to DefaultEnabled
// to ship it dark later.
//
// WHERE IT SPAWNS: a single reachable Goldfields-edge anchor (+X, tier 1 - the
// nearest, easiest region) so the player can walk straight out of the village's
// east gate and reach the outpost. Threat-scaled by ZoneManager at that anchor.
//
// COMBAT IS FULL REUSE: the outpost's garrison are real Enemy; the hero + party
// auto-fight them via TargetManager (already working in OuterWorld). NO new
// combat/targeting code anywhere in this lane.
//
// HARD ISOLATION: touches NO existing file. References only PUBLIC read-only APIs
// (ZoneManager). Ships toggleable; LogWarning, never error. Canon: Elarion.
// =============================================================================
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Static feature flag + the AfterSceneLoad self-bootstrap that, ONLY when
    /// <see cref="Enabled"/>, spawns ONE walk-to <see cref="EnemyOutpost"/> at a
    /// reachable outer-world anchor. Inert (no-op) when disabled.
    /// </summary>
    public static class RaidOutpostSystem
    {
        // ---------------------------------------------------------------------
        // Feature flag. DefaultEnabled is OFF (ships dark) unless DOTR_RAID is
        // defined; _enabled is forced ON for the current testing pass so the owner
        // can walk to the outpost + fight it. Set _enabled back to DefaultEnabled
        // (or call Disable()) to ship the raid dark.
        // ---------------------------------------------------------------------
        private const bool DefaultEnabled =
#if DOTR_RAID
            true;
#else
            false;
#endif

        // ENABLED for the testing pass (owner walks to it + fights). Re-flippable;
        // zero risk (sole user). To ship DARK again, set back to `DefaultEnabled`.
        private static bool _enabled = true;

        /// <summary>Master switch. When false the whole raid loop is inert: no
        /// outpost, no garrison, no fortification. Set BEFORE the world scene loads
        /// (or call <see cref="Enable"/> + <see cref="SpawnNow"/>).</summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>Turn the feature on at runtime (then enter the world or call
        /// <see cref="SpawnNow"/> to materialise the outpost immediately).</summary>
        public static void Enable() => _enabled = true;

        /// <summary>Turn the feature off (does not despawn an already-built outpost).</summary>
        public static void Disable() => _enabled = false;

        // ---------------------------------------------------------------------
        // Placement. ONE outpost at a reachable Goldfields-edge anchor (+X, the
        // nearest tier-1 region). Far enough OUTSIDE the village wall footprint
        // (~+/-42 X, +/-33 Z) that ZoneManager classifies it as Goldfields, but
        // close enough to walk to from the east gate. Runtime only - NO scene edit.
        // ---------------------------------------------------------------------
        private static readonly Vector3 OutpostAnchor = new Vector3(70f, 0f, 0f);

        /// <summary>Scene the outpost lives in (outer world). Bootstrap only spawns
        /// once we are in this additive world scene so it never appears in the
        /// village. Matched case-insensitively.</summary>
        private const string OuterWorldSceneName = "OuterWorld";

        private static bool _spawned;
        private static EnemyOutpost _outpost;

        /// <summary>The live outpost spawned this session (null until spawned).</summary>
        public static EnemyOutpost Outpost => _outpost;

        // ---------------------------------------------------------------------
        // Self-bootstrap. Runs after EVERY scene load; returns instantly when the
        // feature is off (zero footprint) so the build is unaffected.
        // ---------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Enabled) return;   // SHIPS DARK: do nothing at all.
            SpawnNow();
        }

        // OWNER (2026-06-10): a ~3-MINUTE DELAY before the outpost/garrison materialises,
        // so the heavy fort+garrison realization NEVER lands on the city-emerge frame
        // (that single-frame spike contributed to the OuterWorld-load freeze). The slot is
        // claimed immediately (so we never double-schedule); the actual realize fires
        // SpawnDelaySeconds later, once you've settled into the world.
        // OWNER (2026-06-11): was 180s (3 min) — so long that the owner roamed OuterWorld and
        // NEVER found the outpost; it simply hadn't materialised yet. Cut to a short delay that
        // still lands OFF the city-emerge load-spike frame, but lets you actually reach the
        // outpost (walk east to the anchor ~70m out) and find it built + garrisoned.
        private const float SpawnDelaySeconds = 10f;

        /// <summary>
        /// Claim the spawn + SCHEDULE the outpost to materialise after a delay (idempotent).
        /// Honors <see cref="Enabled"/> and only acts once we are in the outer-world scene.
        /// </summary>
        public static void SpawnNow()
        {
            if (!Enabled) return;
            if (_spawned) return;
            if (!InOuterWorld()) return;   // re-bootstrap fires on the next scene load

            _spawned = true;   // claim now so re-entry can't double-schedule

            var runner = new GameObject("RaidOutpostDelayedSpawner");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<DelayedSpawnRunner>();
            Debug.Log($"[RaidOutpostSystem] Outpost scheduled to materialise in {SpawnDelaySeconds:0}s (delayed off the world-load frame).");
        }

        // The actual heavy realize — runs SpawnDelaySeconds AFTER we enter the world, so the
        // fort+garrison build never stalls the city-emerge frame.
        private static void RealizeOutpost()
        {
            if (!InOuterWorld()) return;   // left the world during the delay

            var host = new GameObject("RaidOutpostSystem (outpost)");
            Object.DontDestroyOnLoad(host);

            RegionId region = ZoneManager.GetZone(OutpostAnchor);
            int threat = ZoneManager.ThreatLevel(OutpostAnchor);

            var go = new GameObject($"EnemyOutpost_{region}");
            go.transform.SetParent(host.transform, false);
            go.transform.position = OutpostAnchor;

            _outpost = go.AddComponent<EnemyOutpost>();
            _outpost.Configure(region, threat);

            Debug.Log($"[RaidOutpostSystem] Spawned ONE enemy outpost at {OutpostAnchor} ({region}, threat {threat}) - walk east from the village to raid it.");
        }

        // Tiny host that waits SpawnDelaySeconds then realises the outpost (owner's 3-min delay).
        private sealed class DelayedSpawnRunner : MonoBehaviour
        {
            private void Start() => StartCoroutine(WaitThenSpawn());
            private System.Collections.IEnumerator WaitThenSpawn()
            {
                yield return new WaitForSeconds(SpawnDelaySeconds);
                RealizeOutpost();
                Destroy(gameObject);
            }
        }

        private static bool InOuterWorld()
        {
            if (string.IsNullOrEmpty(OuterWorldSceneName)) return true;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (NameMatches(active.name)) return true;
            // OuterWorld may load ADDITIVELY (WorldSceneLoader) - check every loaded scene.
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && NameMatches(s.name)) return true;
            }
            return false;
        }

        private static bool NameMatches(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) &&
            sceneName.IndexOf(OuterWorldSceneName, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
