// =============================================================================
// RaidOutpostSystem - feature flag + self-bootstrapping spawner that drops FOUR
// walk-to ENEMY OUTPOSTS (one per cardinal edge) in the open world (the RAID bite
// of outpost->raid->loot).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Mirrors CampSystem's bootstrap exactly (AfterSceneLoad self-spawn, OuterWorld-
// only, idempotent, feature-flagged) but spawns EnemyOutposts the owner
// can WALK TO from the village and fight. ENABLED for testing; flip to DefaultEnabled
// to ship it dark later.
//
// WHERE IT SPAWNS: four reachable edge anchors, one at each CARDINAL direction
// (E/W/N/S, ~70m out), so the player can walk straight out of any of the village's
// gates and reach an outpost. Threat-scaled by ZoneManager at each anchor.
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
using DeNelle.Core.Diagnostics;   // TGVRU — FlowTrace/Guard on the cardinal-outpost realize loop

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Static feature flag + the AfterSceneLoad self-bootstrap that, ONLY when
    /// <see cref="Enabled"/>, spawns four walk-to <see cref="EnemyOutpost"/>s (one
    /// per cardinal edge anchor) in the outer world. Inert (no-op) when disabled.
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

        // Routed through the central FeatureFlags gate. Flip via FeatureFlags / PlayerPrefs
        // "ff.raid", not here, so every raid entry point gates from one switch.
        // ⚠ THE REASON THIS LINE USED TO GIVE IS RETIRED (corrected 2026-08-21, WO-932 audit
        // G-copy-2). It read: "raid is OFF until victory/return is built (2026-06-16 demo audit
        // — a cleared raid currently soft-locks)". Victory/return HAS been built:
        // RaidVictoryController owns claim → loot → companion → GoCastle, and
        // RaidDeployController.SettlePartialLoot pays retreat/timeout/hero-death alike
        // (WO-932 phases 3b/5, WO-1110). FeatureFlags.Raid is ON. Left as-is except the reason,
        // because the flag is still the one switch — but a seat reading the old sentence would
        // conclude the raid loop is unfinished and re-implement a victory controller that
        // already exists.
        // WO-449: ALSO gated on RaidContinuousWalk — the walk-to OuterWorld outpost only exists in
        // the continuous-walk loop. When raidwalk is OFF the legacy RaidSelectionScreen->Deploy->GoRaid
        // teleport path owns the raid, so this open-world outpost must NOT spawn (else both loops live).
        private static bool _enabled = DeNelle.Core.FeatureFlags.Raid && DeNelle.Core.FeatureFlags.RaidContinuousWalk;

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
        // Placement. FOUR outposts, one at each CARDINAL edge anchor (E/W/N/E),
        // mirroring the original east anchor 90/180/270 deg around world origin —
        // the same cardinal-mirror pattern used for the castle's 4 gate exits. Each
        // is far enough OUTSIDE the village wall footprint (~+/-42 X, +/-33 Z) that
        // ZoneManager classifies it into a region, but close enough to walk to from
        // the matching gate. Runtime only - NO scene edit. The owner now has a raid
        // target out of EVERY gate.
        // ---------------------------------------------------------------------
        private readonly struct CardinalOutpost
        {
            public readonly Vector3 Anchor;
            public readonly string IdSuffix;   // unique persistence-key suffix per cardinal
            public CardinalOutpost(Vector3 anchor, string idSuffix)
            {
                Anchor = anchor;
                IdSuffix = idSuffix;
            }
        }

        /// <summary>The 4 cardinal outpost anchors (east kept at the original (70,0,0)).</summary>
        private static readonly CardinalOutpost[] OutpostAnchors =
        {
            new CardinalOutpost(new Vector3( 70f, 0f,   0f), "E"),   // east  (original)
            new CardinalOutpost(new Vector3(-70f, 0f,   0f), "W"),   // west
            new CardinalOutpost(new Vector3(  0f, 0f,  70f), "N"),   // north
            new CardinalOutpost(new Vector3(  0f, 0f, -70f), "S"),   // south
        };

        /// <summary>Number of cardinal outposts this system materialises (regression-checked = 4).</summary>
        public static int CardinalOutpostCount => OutpostAnchors.Length;

        /// <summary>Read-only copy of the cardinal anchor positions (for tests / tooling).</summary>
        public static Vector3[] OutpostAnchorPositions()
        {
            var result = new Vector3[OutpostAnchors.Length];
            for (int i = 0; i < OutpostAnchors.Length; i++) result[i] = OutpostAnchors[i].Anchor;
            return result;
        }


        // Per-anchor realize guard: index aligned to OutpostAnchors. _spawned is the
        // SCHEDULE claim (one delayed runner); _realized[i] is the per-cardinal realize
        // guard so a re-realize never double-builds anchor i AND one anchor never blocks
        // the others.
        private static bool _spawned;
        private static readonly bool[] _realized = new bool[OutpostAnchors.Length];
        private static readonly EnemyOutpost[] _outposts = new EnemyOutpost[OutpostAnchors.Length];

        /// <summary>The live outposts spawned this session (entries null until realized).</summary>
        public static EnemyOutpost[] Outposts => (EnemyOutpost[])_outposts.Clone();

        /// <summary>The first live outpost (east), or null until realized. Back-compat.</summary>
        public static EnemyOutpost Outpost => _outposts.Length > 0 ? _outposts[0] : null;

        // ---------------------------------------------------------------------
        // Self-bootstrap. Runs after EVERY scene load; returns instantly when the
        // feature is off (zero footprint) so the build is unaffected.
        // ---------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // WO-482 LIGHT WORLD: when the overworld-encounter loop is on, the open world is
            // kept light — only the wandering orc "reps" populate it. The 4 cardinal raid
            // outposts (each a boss-led garrison) would bury the reps and give the player a
            // pile of non-encounter enemies to attack that never drop to battle. Suppress them.
            // Flag OFF = today's behavior (the outpost system is gated, not deleted — reversible).
            if (DeNelle.Core.FeatureFlags.OverworldEncounter) return;
            if (!Enabled) return;   // SHIPS DARK: do nothing at all.
            // V1 DESCOPE (2026-06-26): arena CUT -> OverworldEncounter OFF -> this guard no longer
            // early-returns, so the walk-to raid outposts are ACTIVE for the V1 raid loop.
            FlowTrace.Step("V1Descope", "arena off (ff.overworldencounter=false) -> RaidOutpostSystem active; cardinal raid outposts will spawn.");
            SpawnNow();
        }

        // OWNER (2026-06-10): a DELAY before the outpost/garrison materialises (originally
        // ~3 min; now SpawnDelaySeconds = 10s per the 2026-06-11 note below),
        // so the heavy fort+garrison realization NEVER lands on the city-emerge frame
        // (that single-frame spike contributed to the OuterWorld-load freeze). The slot is
        // claimed immediately (so we never double-schedule); the actual realize fires
        // SpawnDelaySeconds later, once you've settled into the world.
        // OWNER (2026-06-11): was 180s (3 min) — so long that the owner roamed the overworld and
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
            if (DeNelle.Core.FeatureFlags.OverworldEncounter) return;   // WO-482 light world
            if (!Enabled) return;
            if (_spawned) return;
            if (!InOuterWorld()) return;   // re-bootstrap fires on the next scene load

            _spawned = true;   // claim now so re-entry can't double-schedule

            var runner = new GameObject("RaidOutpostDelayedSpawner");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<DelayedSpawnRunner>();
            Debug.Log($"[RaidOutpostSystem] {OutpostAnchors.Length} cardinal outposts scheduled to materialise in {SpawnDelaySeconds:0}s (delayed off the world-load frame).");
        }

        // The actual heavy realize — runs SpawnDelaySeconds AFTER we enter the world, so the
        // fort+garrison build never stalls the city-emerge frame. Builds ALL 4 cardinal
        // outposts, each guarded per-anchor so one anchor failing/already-built never blocks
        // the other three.
        private static void RealizeOutpost()
        {
            // T — entry/branch trace so a capture sees whether the delayed realize fired at all,
            // and which anchors actually built vs. threw.
            using var _ = FlowTrace.Enter("Outpost", "RaidOutpostSystem.RealizeOutpost");
            if (!InOuterWorld()) { FlowTrace.Step("Outpost", "RealizeOutpost: left OuterWorld during the delay — skipped."); return; }

            var host = new GameObject("RaidOutpostSystem (outposts)");
            Object.DontDestroyOnLoad(host);

            int built = 0;
            for (int i = 0; i < OutpostAnchors.Length; i++)
            {
                if (_realized[i]) continue;   // per-anchor guard: never double-build anchor i
                _realized[i] = true;

                int idx = i;
                // G — guard EACH cardinal independently so one anchor failing never blocks the
                // other three (the loop's own design intent, now fault-isolated + self-reporting).
                bool ok = Guard.Try("Outpost", $"realize cardinal outpost {OutpostAnchors[idx].IdSuffix}", () =>
                {
                    CardinalOutpost cardinal = OutpostAnchors[idx];
                    Vector3 anchor = cardinal.Anchor;

                    RegionId region = ZoneManager.GetZone(anchor);
                    int threat = ZoneManager.ThreatLevel(anchor);

                    var go = new GameObject($"EnemyOutpost_{region}_{cardinal.IdSuffix}");
                    go.transform.SetParent(host.transform, false);
                    go.transform.position = anchor;

                    var outpost = go.AddComponent<EnemyOutpost>();
                    outpost.Configure(region, threat, cardinal.IdSuffix);
                    _outposts[idx] = outpost;

                    FlowTrace.Step("Outpost",
                        $"Spawned cardinal outpost {cardinal.IdSuffix} at {anchor} ({region}, threat {threat}) — walk out the {cardinal.IdSuffix} gate to raid it.");
                    Debug.Log($"[RaidOutpostSystem] Spawned cardinal outpost {cardinal.IdSuffix} at {anchor} ({region}, threat {threat}) - walk out the {cardinal.IdSuffix} gate to raid it.");
                });
                if (ok) built++;
            }

            // R — realize completed but built NOTHING: self-report rather than silently shipping a
            // world with no raid targets (every anchor threw).
            if (built == 0)
                FlowTrace.Fail("Outpost", $"RealizeOutpost: 0 of {OutpostAnchors.Length} cardinal outposts realized — every anchor build failed.");
            else
                FlowTrace.Step("Outpost", $"RealizeOutpost: realized {built}/{OutpostAnchors.Length} cardinal enemy outpost(s) (one per gate exit).");
            Debug.Log($"[RaidOutpostSystem] {OutpostAnchors.Length} cardinal enemy outposts realized (one per gate exit).");
        }

        // Tiny host that waits SpawnDelaySeconds (10s) then realises the cardinal outposts.
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
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (NameMatches(active.name)) return true;
            // Overworld may load ADDITIVELY (WorldSceneLoader) - check every loaded scene.
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && NameMatches(s.name)) return true;
            }
            return false;
        }

        // WO-608 merge: route through HubScenes.IsOverworld so raid outposts spawn on the
        // merged "Main_Castle_Overworld" too (matches legacy "OuterWorld" AND the merged name).
        private static bool NameMatches(string sceneName) =>
            DeNelle.Core.HubScenes.IsOverworld(sceneName);
    }
}
