// =============================================================================
// BarracksNpcInjector — runtime, NON-DESTRUCTIVE placement of the drillmaster NPC
// at the castle Barracks (WO-453 troop-training flow). Mirrors
// CastleVendorNpcInjector's self-bootstrap, but keys off the single 'CastleBarracks'
// building GameObject (placed by CastleBarracksPlacer) instead of the storefront
// "NPC_<Role>_Interactable" markers — the barracks prefab has no such marker child.
// -----------------------------------------------------------------------------
// WHY a runtime injector (not a scene edit / regen):
//   The barracks is a polyperfect prefab dropped into MainCastle_Hall by the
//   CastleBarracksPlacer editor tool. Re-saving / regenerating that scene to add a
//   real NPC body carries the project's known scene-resave corruption risk
//   (CLAUDE.md §3). So this self-bootstrapping DDOL singleton, on every
//   MainCastle_Hall load, FINDS the 'CastleBarracks' root and spawns a static
//   drillmaster NPC in FRONT of it — WITHOUT ever touching the .unity file.
//   Idempotent per load (a re-load nukes the prior runtime holder).
//
// STATIC, not townsfolk: reuses the same Resources/NPCs People-pack body source +
//   AmbientNPC.Configure(arch, wander:FALSE, ...) the vendor injector uses, so the
//   drillmaster stands his ground (idle sway only, no roam/follow).
//
// INTERACTION: the SAME slim CastleNpcInteractable the vendors use, configured with
//   structureId "barracks". Its Talk opens DialogueService.PlayStructure("barracks",
//   "Barracks") → the Barracks_MainMenu Yarn node (PlayStructure routes "barracks"
//   to that node, mirroring the pet-house branch). No reflection, no cross-asmdef ref.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime, non-destructive placement of the drillmaster NPC at the castle Barracks.</summary>
    public sealed class BarracksNpcInjector : MonoBehaviour
    {
        public static BarracksNpcInjector Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";

        // The building root CastleBarracksPlacer drops into the scene.
        private const string BarracksRootName = "CastleBarracks";

        // Resources/NPCs People-pack body — the smith reads as a fitting drillmaster
        // (the vendor injector uses the same body source). Merchant is the safe fallback.
        private const string BodyDrillmaster = "NPCs/NPC_Blacksmith";
        private const string BodyFallback    = "NPCs/NPC_Merchant";

        private const string StructureId = "barracks";
        private const string Label        = "Barracks";

        // How far IN FRONT of the building origin the drillmaster stands (toward the
        // plaza / approaching hero). The barracks faces castle centre, so place the NPC
        // along the building's forward and let the navmesh snap settle the exact spot.
        private const float FrontOffset = 4.5f;

        private const string HolderName = "BarracksNPC (runtime)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("BarracksNpcInjector").AddComponent<BarracksNpcInjector>();
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

            var barracks = GameObject.Find(BarracksRootName);
            if (barracks == null)
            {
                Debug.Log("[BarracksNpcInjector] no 'CastleBarracks' in scene — drillmaster not placed " +
                          "(barracks prefab may be missing; polyperfect pack not imported).");
                return;
            }

            var holder = new GameObject(HolderName);
            Transform hero = ResolveHero();

            if (SpawnDrillmaster(barracks.transform, hero, holder.transform))
                Debug.Log("[BarracksNpcInjector] placed the drillmaster NPC at the Barracks.");
            else
                Debug.LogWarning("[BarracksNpcInjector] failed to place the drillmaster NPC.");
        }

        private bool SpawnDrillmaster(Transform barracks, Transform hero, Transform parent)
        {
            // Stand in front of the building (its forward points toward castle centre /
            // the plaza where the hero approaches). Snap onto the navmesh if it's near.
            Vector3 pos = barracks.position + barracks.forward * FrontOffset;
            if (NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas))
                pos = hit.position;

            // Face back toward the building front / approaching hero.
            Quaternion rot = Quaternion.LookRotation(-barracks.forward, Vector3.up);

            var prefab = Resources.Load<GameObject>(BodyDrillmaster)
                         ?? Resources.Load<GameObject>(BodyFallback);
            if (prefab == null)
            {
                Debug.LogWarning($"[BarracksNpcInjector] no body prefab (missing Resources/{BodyDrillmaster}) — placeholder used.");
                return SpawnPlaceholder(pos, rot, hero, parent);
            }

            var go = Instantiate(prefab, pos, rot, parent);
            go.name = "BarracksDrillmaster";

            NormalizeToHeroHeight(go);
            NpcGroundSeat.Seat(go, pos.y);

            // STATIC: wander=FALSE → AmbientNPC disables its NavMeshAgent and stands its
            // ground (idle sway only). Blacksmith archetype reads as a gruff drillmaster.
            var npc = go.GetComponent<AmbientNPC>();
            if (npc != null) npc.Configure(TownsfolkDialogue.Archetype.Blacksmith, /*wander*/ false, pos);

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            AttachInteraction(go, hero);
            return true;
        }

        // Minimal capsule fallback if the People-pack body is absent (Models gitignored
        // on a fresh clone). Getting the INTERACTION working is the priority.
        private bool SpawnPlaceholder(Vector3 pos, Quaternion rot, Transform hero, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "BarracksDrillmaster_Placeholder";
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 1f;
            go.transform.rotation = rot;

            // Proximity-based interaction → don't let the capsule collider block the hero.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            AttachInteraction(go, hero);
            return true;
        }

        private void AttachInteraction(GameObject body, Transform hero)
        {
            var interact = body.AddComponent<CastleNpcInteractable>();
            interact.Configure(StructureId, Label, hero);
            BuildingInteractable.MarkNpcCovered(StructureId);   // the building defers — NPC owns the talk

            // Always-visible type sign above the drillmaster (same as the vendor NPCs).
            float localHeadClear = SignHeightAboveHead(body);
            InteractableSign.ForStructureId(body, StructureId, localHeadClear);
        }

        private static float SignHeightAboveHead(GameObject body)
        {
            const float WorldClearAboveOrigin = 2.6f;
            float scaleY = body.transform.lossyScale.y;
            return scaleY > 0.01f ? WorldClearAboveOrigin / scaleY : WorldClearAboveOrigin;
        }

        // Reuse the vendor injector's height normalization so the People-pack body sits
        // at ~hero height instead of towering (packs import at varying native scales).
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

        private static Transform ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) return tagged.transform;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }
}
