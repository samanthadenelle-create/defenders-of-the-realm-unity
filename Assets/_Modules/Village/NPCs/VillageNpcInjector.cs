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
            // Remove the baked placeholder townsfolk (capture the parent root + count).
            var existing = FindObjectsByType<AmbientNPC>(FindObjectsSortMode.None);
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
                var prefab = Resources.Load<GameObject>(def.Res);
                if (prefab == null)
                {
                    Debug.LogWarning($"[VillageNpcInjector] missing Resources/{def.Res} - skipped.");
                    continue;
                }

                // Snap onto the baked NavMesh so wanderers can path (idlers unaffected).
                Vector3 pos = def.Pos;
                if (NavMesh.SamplePosition(def.Pos, out var hit, 6f, NavMesh.AllAreas))
                    pos = hit.position;

                var go = Instantiate(prefab, pos, Quaternion.identity, root);
                go.name = prefab.name;

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
            Debug.Log($"[VillageNpcInjector] placed {placed} People-pack NPCs (removed {existing.Length} placeholders).");
        }

        // Name-based Keeper lookup (matches AmbientNPC's own fallback: the village
        // hero rig is named "Hero (...)"; the project defines no "Player" tag).
        private static Transform ResolveHero()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }
}
