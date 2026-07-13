// =============================================================================
// CastleTownsfolkInjector — spawns a handful of WANDERING ambient villagers in
// the castle hub, scattered near the building anchors inside the town ring.
// -----------------------------------------------------------------------------
// WHY (owner feature 2026-07-06 "villagers hide during combat... returning
// after"): AmbientNPC carries the flee/hide/return behaviour, but the fleet
// census (run 9413) proved the castle hub had ZERO wander-eligible AmbientNPCs —
// every body there (vendors, barracks, introducer) is Configure(wander:false).
// The only wander=true villager in the game was Village2's Mevina, and Village2
// is the abandoned raid target. So the owner's feature had no subjects in her
// town. This injector adds them.
//
// WHAT: 5 generic townsfolk (non-vendor, NO dialogue role beyond the ambient
// proximity-chatter every AmbientNPC has, NO interactable, NO structure id),
// using the same Resources/NPCs People-pack peasant bodies the other injectors
// use. Each is Configure(arch, wander:TRUE, anchor) with a live NavMeshAgent,
// so they roam their anchor — and automatically inherit AmbientNPC's combat
// shelter behaviour (flee to the nearest Building, hide, return after calm).
//
// WHERE: positions are NOT hardcoded. Each villager anchors near a distinct
// scene Building inside the town ring (~60u of the Heart-at-origin, canon §7 /
// HudContextEvaluator's radial model): building pos, nudged toward the Heart
// plus a small tangent so they don't stack on the vendor NPCs, then
// NavMesh-sampled. A building whose surroundings fail to sample is skipped.
//
// Mirrors CastleVendorNpcInjector's self-bootstrap / scene gate / guard-every-
// spawn pattern. Non-destructive (own runtime holder, idempotent per load),
// never touches a .unity file. Village -> Core only.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime spawner of wandering ambient villagers in the castle hub
    /// (the flee-into-shelter feature's subjects — owner 2026-07-06).</summary>
    public sealed class CastleTownsfolkInjector : MonoBehaviour
    {
        public static CastleTownsfolkInjector Instance { get; private set; }

        // Same castle-hub gate as CastleVendorNpcInjector (WO-608 merged scene included;
        // never Village2 — that town has its own injector).
        private const string TargetScene = "MainCastle_Hall";
        private const string MergedTargetScene = "Main_Castle_Overworld";
        private static bool IsCastleHubScene(string n) => n == TargetScene || n == MergedTargetScene;

        private const string HolderName = "CastleTownsfolk (runtime)";

        /// <summary>How many wandering villagers to scatter (modest; owner-tunable later).</summary>
        private const int VillagerCount = 5;

        /// <summary>Town ring (HudContextEvaluator's radial model) — only anchor near
        /// buildings inside the walls, never distant overworld structures.</summary>
        private const float TownRadius = 60f;

        // WO-703 / BLANK-1 ground band for spawn-position sampling: the merged
        // Main_Castle_Overworld ground is the scripted flat plane at y=0
        // (HubStructureVisualInjector.GroundY / WorldMergeBuilder.LowerCastleToGround).
        // A NavMesh.SamplePosition hit outside this band is elevated mesh (wall-walk /
        // deck) — never a valid townsfolk spawn. Bands mirror NpcGroundSeat's
        // AcceptedFloorBandBelowGround (0.35) / a generous above-ground step allowance.
        private const float GroundMinY = -0.35f;
        private const float GroundMaxY = 0.75f;

        // People-pack peasant bodies — the same Resources source every NPC injector uses.
        private static readonly string[] BodyPool =
        {
            "NPCs/NPC_Peasant_Mevina",
            "NPCs/NPC_Peasant_Tob",
        };

        // Generic everyday voices only — no warden/vendor archetypes (those speak as
        // named tradesfolk and belong to the vendor bodies).
        private static readonly TownsfolkDialogue.Archetype[] ArchPool =
        {
            TownsfolkDialogue.Archetype.Villager,
            TownsfolkDialogue.Archetype.Child,
            TownsfolkDialogue.Archetype.Elder,
            TownsfolkDialogue.Archetype.Farmer,
            TownsfolkDialogue.Archetype.Villager,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("CastleTownsfolkInjector").AddComponent<CastleTownsfolkInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (IsCastleHubScene(SceneManager.GetActiveScene().name)) Arm();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsCastleHubScene(scene.name)) Arm();
        }

        private void Arm()
        {
            StopCoroutine(nameof(InjectWhenBuildingsReady));
            StartCoroutine(nameof(InjectWhenBuildingsReady));
        }

        // Deferred: buildings arrive from BOTH the baked scene and runtime station
        // injectors (nondeterministic order — same reason the vendor injector defers
        // its apothecary/jeweler passes). Wait for at least one in-ring Building
        // (up to ~8s), then spawn once.
        private IEnumerator InjectWhenBuildingsReady()
        {
            const float TimeoutSeconds = 8f;
            float deadline = Time.unscaledTime + TimeoutSeconds;
            while (Time.unscaledTime < deadline)
            {
                if (!IsCastleHubScene(SceneManager.GetActiveScene().name)) yield break;

                var anchors = CollectTownBuildings();
                if (anchors.Count > 0)
                {
                    Inject(anchors);
                    yield break;
                }
                yield return null;
            }
            FlowTrace.Warn("Townsfolk",
                "CastleTownsfolkInjector: no in-ring Building appeared within 8s — no villagers injected.");
        }

        /// <summary>Scene Buildings inside the town ring (horizontal distance to the
        /// Heart-at-origin), i.e. the houses/storefronts inside the castle walls.</summary>
        private static List<Building> CollectTownBuildings()
        {
            var result = new List<Building>();
            Guard.Try("Townsfolk", "collect town buildings", () =>
            {
                foreach (var b in FindObjectsByType<Building>(FindObjectsSortMode.None))
                {
                    if (b == null) continue;
                    Vector3 p = b.transform.position;
                    if (p.x * p.x + p.z * p.z <= TownRadius * TownRadius) result.Add(b);
                }
            });
            return result;
        }

        private void Inject(List<Building> anchors)
        {
            // Idempotent: clear any prior runtime holder so a re-load never double-spawns.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);
            var holder = new GameObject(HolderName);

            Transform hero = ResolveHero();
            Vector3 heart = HeartCenter();

            // WO-703 / BLANK-1 (owner ruling 2026-07-13): every townsfolk NPC gates on ITS
            // home building existing — ONE villager per DISTINCT in-ring Building, never a
            // crowd recycled around a single anchor (the old i % anchors.Count loop put all
            // 5 villagers on one building on a near-blank save — the "crowd of townsfolk
            // near the tree" symptom). No building, no NPC.
            int toPlace = Mathf.Min(VillagerCount, anchors.Count);
            FlowTrace.Step("Townsfolk",
                $"BLANK-1 gate: {anchors.Count} in-ring building(s) -> spawning {toPlace} villager(s) " +
                $"(cap {VillagerCount}; one per distinct home building).");

            int placed = 0;
            for (int i = 0; i < toPlace; i++)
            {
                // One villager per distinct building (BLANK-1) — scatter, never clump.
                Building anchor = anchors[i];
                if (anchor == null) continue;

                if (!TrySamplePosNear(anchor.transform.position, heart, out Vector3 pos))
                {
                    FlowTrace.Warn("Townsfolk",
                        $"CastleTownsfolkInjector: no NavMesh near building '{anchor.name}' — villager slot {i} skipped.");
                    continue;
                }

                if (SpawnVillager(i, pos, hero, holder.transform)) placed++;
            }

            if (placed == 0)
                FlowTrace.Fail("Townsfolk",
                    $"CastleTownsfolkInjector: injected 0 villagers ({anchors.Count} in-ring buildings) — every spawn failed/skipped.");
            else
                FlowTrace.Step("Townsfolk",
                    $"injected {placed} villagers (of {VillagerCount} planned, {anchors.Count} in-ring building anchors).");
        }

        /// <summary>
        /// Picks a walkable point near the building: nudged toward the Heart (the open
        /// town side, mirroring the vendors' center-facing rule) plus a small random
        /// tangent so villagers never stack on the vendor spots, then NavMesh-sampled.
        /// </summary>
        private static bool TrySamplePosNear(Vector3 buildingPos, Vector3 heart, out Vector3 pos)
        {
            pos = default;
            Vector3 flat = new Vector3(buildingPos.x, 0f, buildingPos.z);
            Vector3 toHeart = new Vector3(heart.x - flat.x, 0f, heart.z - flat.z);
            toHeart = toHeart.sqrMagnitude < 0.01f ? Vector3.forward : toHeart.normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, toHeart);

            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector3 candidate = flat
                    + toHeart * Random.Range(5f, 9f)
                    + tangent * Random.Range(-4f, 4f);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    // WO-703 / BLANK-1 ("one NPC ON TOP of the gatehouse wall"): the 6m
                    // sample radius is 3D — a candidate near the wall ring can resolve to
                    // the WALL-WALK navmesh several metres up instead of the courtyard.
                    // Constrain spawns to the GROUND RING: accept only hits inside the
                    // ground band around y=0 (the merged world's scripted flat ground —
                    // HubStructureVisualInjector.GroundY; band mirrors NpcGroundSeat's
                    // accepted-floor bands). An out-of-band hit is rejected and the
                    // attempt retried with a fresh candidate.
                    if (hit.position.y < GroundMinY || hit.position.y > GroundMaxY)
                    {
                        FlowTrace.Step("Townsfolk",
                            $"spawn sample rejected: navmesh hit y={hit.position.y:F2} outside ground band " +
                            $"[{GroundMinY:F2}..{GroundMaxY:F2}] (wall-top/elevated mesh) — attempt {attempt + 1}/4 retried.");
                        continue;
                    }
                    pos = hit.position;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Spawns ONE wandering villager. Guarded end to end — a failed spawn
        /// logs (Warn/Fail -> break-log) and is skipped, never throws.</summary>
        private bool SpawnVillager(int index, Vector3 pos, Transform hero, Transform parent)
        {
            var arch = ArchPool[index % ArchPool.Length];
            string bodyRes = BodyPool[index % BodyPool.Length];

            GameObject go = null;
            var prefab = Resources.Load<GameObject>(bodyRes);
            if (prefab != null)
            {
                Guard.Try("Townsfolk", $"instantiate villager '{bodyRes}'", () =>
                {
                    go = Instantiate(prefab, pos, Quaternion.identity, parent);
                });
                if (go != null && !VerifyRenders(go))
                {
                    FlowTrace.Warn("Townsfolk",
                        $"CastleTownsfolkInjector: villager body '{bodyRes}' has no visible mesh — placeholder used.");
                    Destroy(go);
                    go = null;
                }
            }
            else
            {
                FlowTrace.Warn("Townsfolk",
                    $"CastleTownsfolkInjector: missing Resources/{bodyRes} — placeholder villager used (Models gitignored?).");
            }

            float seatDelta = 0f;   // vertical correction NpcGroundSeat applied (held by the wanderer's baseOffset below)
            if (go == null)
            {
                // Capsule fallback so the slot is never silently empty; AmbientNPC's
                // WO-29 archetype tint keeps it reading as a person.
                Guard.Try("Townsfolk", $"placeholder villager {index}", () =>
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = $"CastleVillager_{index}_{arch}_Placeholder";
                    go.transform.SetParent(parent, false);
                    go.transform.position = pos + Vector3.up * 1f;
                    var col = go.GetComponent<Collider>();
                    if (col != null) col.isTrigger = true;   // never blocks the hero
                });
                if (go == null)
                {
                    FlowTrace.Fail("Townsfolk",
                        $"CastleTownsfolkInjector: placeholder build failed for villager slot {index}.");
                    return false;
                }
            }
            else
            {
                go.name = $"CastleVillager_{index}_{arch}";
                NormalizeToHeroHeight(go);
                seatDelta = NpcGroundSeat.Seat(go, pos.y);
            }

            bool wired = Guard.Try("Townsfolk", $"wire villager {index} ({arch})", () =>
            {
                // Off-screen animator culling (WO-53) on the pack body's Animator.
                var anim = go.GetComponentInChildren<Animator>();
                if (anim != null) anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // WANDERER: a live NavMeshAgent is what makes it roam — and what makes it
                // flee-eligible for the combat-shelter behaviour. The pack prefabs carry
                // one; the capsule fallback does not, so ensure it.
                var agent = go.GetComponent<NavMeshAgent>();
                if (agent == null) agent = go.AddComponent<NavMeshAgent>();
                agent.enabled = true;
                // Feet-on-ground while WALKING: once enabled the agent re-snaps Y to the (inflated)
                // navmesh every frame, undoing the ground-seat. baseOffset carries the seat's vertical
                // correction so the wanderer stays on the true floor through roam/flee/return.
                agent.baseOffset = seatDelta;

                var npc = go.GetComponent<AmbientNPC>();
                if (npc == null) npc = go.AddComponent<AmbientNPC>();
                npc.Configure(arch, /*wander*/ true, pos);   // before Start() runs
                var bubble = go.GetComponentInChildren<TownsfolkBubble>();
                if (bubble != null) npc.SetBubble(bubble);
                if (hero != null) npc.SetHero(hero);
            });
            if (!wired)
            {
                Destroy(go);
                return false;
            }
            return true;
        }

        // The castle centre villagers drift around — the Heart (world-tree); same
        // runtime lookup + fallback the vendor injector uses.
        private static Vector3 HeartCenter()
        {
            var h = FindAnyObjectByType<HeartController>();
            return h != null ? h.transform.position : new Vector3(0f, 0f, 12f);
        }

        // Render-verify (same contract as the sibling injectors): >=1 enabled Renderer
        // with an actual mesh, else the body reads invisible and is dropped.
        private static bool VerifyRenders(GameObject go)
        {
            if (go == null) return false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                bool hasMesh =
                    (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) ||
                    (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null);
                if (hasMesh) return true;
            }
            return false;
        }

        // Same height normalization the sibling injectors apply so the pack bodies sit
        // at ~hero height, with the speech bubble counter-scaled to real world size.
        private static void NormalizeToHeroHeight(GameObject go)
        {
            float npcScale = 1f;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) npcScale = 1.95f / b.size.y;
            }
            if (npcScale > 0.01f && !Mathf.Approximately(npcScale, 1f))
            {
                go.transform.localScale *= npcScale;
                var bubbleRoot = go.transform.Find("BubbleRoot");
                if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one / Mathf.Max(0.01f, npcScale);
            }
        }

        // Tag-first hero lookup (CLAUDE.md §7: hero is tagged "Player"), name fallback.
        private static Transform ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) return tagged.transform;
            foreach (var t in FindObjectsByType<Transform>())
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }
}
