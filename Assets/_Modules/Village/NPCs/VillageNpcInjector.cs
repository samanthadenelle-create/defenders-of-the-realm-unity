// =============================================================================
// VillageNpcInjector (DEF-91 Phase 3) - swaps the baked placeholder townsfolk for
// the purchased People-pack prefabs at runtime, WITHOUT touching Village.unity.
// -----------------------------------------------------------------------------
// VillageSceneBuilder.BuildTownsfolk bakes 4 placeholder NPCs (KayKit civilians /
// capsules) into Village.unity at authored positions. Re-saving that scene to swap
// meshes carries a known serialization-corruption risk, and pointing the builder's
// model paths at our prefabs would double the AmbientNPC component (the prefabs
// already carry one). So instead this self-bootstrapping DDOL singleton, on every
// Village load, removes the baked AmbientNPC placeholders and instantiates the 4
// complete prefabs (mesh + URP material + Animator + NavMeshAgent + AmbientNPC +
// TownsfolkBubble) at the SAME positions / archetypes the builder used - so density
// and placement are unchanged, only the models become the real characters.
//
// Prefabs load from Resources/NPCs (relocated there by NpcPackBuild). Idempotent
// per scene load; runs only in the "Village" scene.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime Phase-3 swap of placeholder townsfolk to the People-pack prefabs.</summary>
    public sealed class VillageNpcInjector : MonoBehaviour
    {
        public static VillageNpcInjector Instance { get; private set; }

        private const string TargetScene = "Village2";

        private struct Def
        {
            public string Res;
            public Vector3 Pos;
            public TownsfolkDialogue.Archetype Arch;
            public bool Wander;
        }

        // Positions / archetypes mirror VillageSceneBuilder.BuildTownsfolk so the
        // placement the owner already approved is preserved; only the meshes change.
        // WO-116: the two named tradesfolk models now speak as their wardens
        // (the Blacksmith model talks as the Blacksmith, the Merchant as the
        // Quartermaster) instead of the generic Guard/Trader pools. Mevina (a
        // wanderer) keeps her everyday-villager voice; Tob keeps the elder voice.
        private static readonly Def[] Defs =
        {
            new Def { Res = "NPCs/NPC_Peasant_Mevina", Pos = new Vector3(  4f, 0f,  5f), Arch = TownsfolkDialogue.Archetype.Villager,      Wander = true  },
            new Def { Res = "NPCs/NPC_Peasant_Tob",    Pos = new Vector3(  2f, 0f, -6f), Arch = TownsfolkDialogue.Archetype.Elder,         Wander = false },
            new Def { Res = "NPCs/NPC_Merchant",       Pos = new Vector3(-10f, 0f, -7f), Arch = TownsfolkDialogue.Archetype.Quartermaster, Wander = false },
            // DEF-220: the Blacksmith stands at his Forge anvil (plot 20,-10 / ForgeYard
            // front), stationary, facing the smithy — mirrors VillageSceneBuilder's
            // baked Townsfolk spot[3] so the swapped People-pack smith lands at the forge.
            new Def { Res = "NPCs/NPC_Blacksmith",     Pos = new Vector3( 17.5f, 0f, -8f), Arch = TownsfolkDialogue.Archetype.Blacksmith,    Wander = false },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("VillageNpcInjector").AddComponent<VillageNpcInjector>();
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
            using var _ = FlowTrace.Enter("Village", "VillageNpcInjector.Inject");

            // Remove the baked placeholder townsfolk (capture the parent root + count).
            var existing = FindObjectsByType<AmbientNPC>();
            Transform root = null;
            foreach (var npc in existing)
            {
                if (npc == null) continue;
                if (root == null && npc.transform.parent != null &&
                    npc.transform.parent.name.Contains("Townsfolk"))
                    root = npc.transform.parent;
                Destroy(npc.gameObject);
            }
            if (root == null)
            {
                var found = GameObject.Find("Townsfolk");
                root = found != null ? found.transform
                                     : new GameObject("Townsfolk (People)").transform;
            }

            Transform hero = ResolveHero();

            int placed = 0;
            foreach (var def in Defs)
            {
                // Snap onto the baked NavMesh so wanderers can path (idlers unaffected).
                Vector3 pos = def.Pos;
                if (NavMesh.SamplePosition(def.Pos, out var hit, 6f, NavMesh.AllAreas))
                    pos = hit.position;

                var prefab = Resources.Load<GameObject>(def.Res);
                if (prefab == null)
                {
                    // R (never silently vanish): a missing body prefab USED to `continue` with no
                    // placeholder — the townsfolk simply disappeared. Now we drop a placeholder body
                    // so the slot is always filled, and self-report the load-miss (Warn -> break-log).
                    FlowTrace.Warn("Village",
                        $"VillageNpcInjector: missing Resources/{def.Res} — placeholder townsfolk used (Models gitignored on a fresh clone?).");
                    if (SpawnPlaceholder(def, pos, root)) placed++;
                    continue;
                }

                GameObject go = null;
                Guard.Try("Village", $"instantiate townsfolk '{def.Res}'", () =>
                {
                    go = Instantiate(prefab, pos, Quaternion.identity, root);
                });
                if (go == null)
                {
                    // Instantiate returned/threw null — fall back to a placeholder so the slot is
                    // never left empty, and self-report.
                    FlowTrace.Fail("Village",
                        $"VillageNpcInjector: Instantiate returned null for '{def.Res}' — placeholder townsfolk used.");
                    if (SpawnPlaceholder(def, pos, root)) placed++;
                    continue;
                }
                go.name = prefab.name;

                // V (render-verify): a body that instantiates with no enabled mesh renderer reads as
                // an invisible NPC. Prove it renders; on failure drop it and fall back to a placeholder.
                if (!VerifyNpcRenders(go, def.Res))
                {
                    FlowTrace.Fail("Village",
                        $"VillageNpcInjector: townsfolk '{def.Res}' has no visible mesh — dropping, placeholder used.");
                    Destroy(go);
                    if (SpawnPlaceholder(def, pos, root)) placed++;
                    continue;
                }

                // WO-53 (perf): off-screen animator culling on the spawned townsfolk
                // Animator. CullUpdateTransforms keeps the state machine running while
                // skipping transform/mesh writes when the NPC is off-camera — NOT
                // CullCompletely (which can desync gameplay-driven anim events).
                var npcAnim = go.GetComponentInChildren<Animator>();
                if (npcAnim != null) npcAnim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // NORMALIZE to roughly hero height. Owner 2026-06-02 (DEF-134): the old flat
                // x2 made the People-pack NPCs tower 3-4x over the hero — in the close
                // defend-the-tower camera they merged with the player and their speech bubbles
                // covered the HUD. Scale to a target height by measured bounds instead, so the
                // result is consistent regardless of each prefab's native size.
                float npcScale = 2f;   // fallback if we can't measure (preserves old behaviour)
                var rends = go.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    if (b.size.y > 0.01f) npcScale = 1.95f / b.size.y;   // ~hero height (1.8) + a touch
                }
                go.transform.localScale *= npcScale;

                // T-033 ("NPCs floating"): scaling about a non-feet pivot lifts the model's
                // feet off the ground AND the navmesh Y sits a touch above the visual floor.
                // Raycast DOWN to the real floor collider and seat the (post-rescale)
                // renderer-bounds bottom onto it; falls back to the navmesh Y if none is hit.
                NpcGroundSeat.Seat(go, pos.y);

                // Counter-scale TownsfolkBubble's "BubbleRoot" so the speech bubble keeps its
                // real world size on the resized body (it still rides above the head via its
                // localPosition on the scaled parent).
                var bubbleRoot = go.transform.Find("BubbleRoot");
                if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one / Mathf.Max(0.01f, npcScale);

                var npc = go.GetComponent<AmbientNPC>();
                if (npc != null)
                {
                    npc.Configure(def.Arch, def.Wander, pos);          // before Start() runs
                    var bubble = go.GetComponentInChildren<TownsfolkBubble>();
                    if (bubble != null) npc.SetBubble(bubble);
                    if (hero != null) npc.SetHero(hero);
                }
                placed++;
            }

            // U: placed==0 is a hard anomaly (every townsfolk slot vanished) — Fail-loud to the
            // break-log; a healthy run Steps the count.
            if (placed == 0)
                FlowTrace.Fail("Village",
                    $"VillageNpcInjector: placed 0 townsfolk (removed {existing.Length} placeholders) — all {Defs.Length} slots empty.");
            else
                FlowTrace.Step("Village",
                    $"VillageNpcInjector: placed {placed}/{Defs.Length} People-pack NPCs (removed {existing.Length} placeholders).");
            Debug.Log($"[VillageNpcInjector] placed {placed} People-pack NPCs (removed {existing.Length} placeholders).");
        }

        // Minimal capsule fallback so a missing/broken body never leaves a townsfolk slot EMPTY
        // (the slot used to silently vanish). Carries the same AmbientNPC archetype so the
        // placeholder still chatters/idles like the real body. Idempotent per Inject.
        private bool SpawnPlaceholder(Def def, Vector3 pos, Transform root)
        {
            GameObject go = null;
            Guard.Try("Village", $"placeholder townsfolk '{def.Res}'", () =>
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"Townsfolk_{def.Arch}_Placeholder";
                go.transform.SetParent(root, false);
                go.transform.position = pos + Vector3.up * 1f;

                // Proximity chatter is distance-based; don't let the capsule block the hero.
                var col = go.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                var npc = go.AddComponent<AmbientNPC>();
                npc.Configure(def.Arch, def.Wander, pos);
            });
            if (go == null)
            {
                FlowTrace.Fail("Village", $"VillageNpcInjector: placeholder build failed for '{def.Res}'.");
                return false;
            }
            FlowTrace.Step("Village", $"VillageNpcInjector: placeholder townsfolk placed for '{def.Res}'.");
            return true;
        }

        // V (render-verify): the spawned body must carry >=1 ENABLED Renderer with an actual mesh
        // (SkinnedMeshRenderer.sharedMesh or MeshFilter.sharedMesh). Traces the counts so a capture
        // splits "no mesh" from a real spawn. Returns false => caller drops it + uses a placeholder.
        private static bool VerifyNpcRenders(GameObject go, string res)
        {
            if (go == null) return false;
            int total = 0, enabledWithMesh = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                total++;
                if (!r.enabled) continue;
                bool hasMesh =
                    (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) ||
                    (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null);
                if (hasMesh) enabledWithMesh++;
            }
            bool ok = enabledWithMesh > 0;
            if (!ok)
                FlowTrace.Warn("Village",
                    $"VerifyNpcRenders '{res}': {total} renderer(s), {enabledWithMesh} enabled-with-mesh — reads invisible.");
            return ok;
        }

        // Name-based Keeper lookup (matches AmbientNPC's own fallback: the village
        // hero rig is named "Hero (...)"; the project defines no "Player" tag).
        private static Transform ResolveHero()
        {
            foreach (var t in FindObjectsByType<Transform>())
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }
}
