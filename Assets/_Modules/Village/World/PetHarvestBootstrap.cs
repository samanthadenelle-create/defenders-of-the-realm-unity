// =============================================================================
// PetHarvestBootstrap — spawns harvestable MineNodes in the VILLAGE so the
// deployed starter pet actually has something to gather.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village (lives here, NOT the Pets asmdef, so it can reference
// MineNode directly — Pets→Village is reflection-only).
//
// DEF-122 gap (found 2026-06-02): the pet auto-harvest loop is fully wired
// (PetDeployer attaches PetHarvester → MineNodeBridge → MineNode banks to
// GameState), but there were ZERO MineNodes in Village.unity — they only exist
// in OuterWorld. So the pet scanned every second and found nothing. This drops a
// small cluster of nodes near the village centre, inside the pet's ~28m harvest-
// detect radius, so the economy loop runs the moment you load the village.
//
// Code-built + runtime (DDOL, same pattern as StoryCompanionInjector /
// RampartLiftInstaller) — no scene edit, no VillageSceneBuilder change. The nodes
// are visible tinted "ore" so the player sees them and can [F]-tap them too; a
// real model can swap in later via the catalog/VisualFactory.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime-spawns starter resource nodes in the village for pet harvesting.</summary>
    public sealed class PetHarvestBootstrap : MonoBehaviour
    {
        public static PetHarvestBootstrap Instance { get; private set; }
        private const string TargetScene = "Village";
        private bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("PetHarvestBootstrap").AddComponent<PetHarvestBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) { _built = false; Build(); }
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            // Idempotent: if the village already has nodes (a future hand-placed pass), skip.
            if (Object.FindFirstObjectByType<MineNode>() != null) return;

            // A small cluster near the village centre — inside the pet's ~28m harvest detect
            // (PetHarvester) so the deployed Warden finds + works them. One node per
            // harvestable (DEF-121: Wood / Food / Iron / Crystals) so pet auto-harvest
            // raises ALL FOUR resource counts in GameState, visibly.
            SpawnNode("Wood",     MineResource.Wood,         new Vector3( 11f, 0f,  11f), new Color(0.45f, 0.30f, 0.16f));
            SpawnNode("Iron",     MineResource.Iron,         new Vector3(-12f, 0f,   9f), new Color(0.55f, 0.57f, 0.62f));
            SpawnNode("Food",     MineResource.Food,         new Vector3(  9f, 0f, -12f), new Color(0.70f, 0.62f, 0.28f));
            SpawnNode("Crystals", MineResource.AetherCrystal, new Vector3(-9f, 0f, -11f), new Color(0.45f, 0.72f, 0.95f));
        }

        private static void SpawnNode(string label, MineResource res, Vector3 pos, Color tint)
        {
            // Snap onto the baked NavMesh so the pet can path to it.
            if (NavMesh.SamplePosition(pos, out var hit, 8f, NavMesh.AllAreas)) pos = hit.position;

            var go = new GameObject($"MineNode-{label}-Village");
            go.transform.position = pos;

            // Visible ore vein (placeholder primitive, tinted by resource). MineNode uses
            // distance checks (not physics), so strip the cube collider to keep paths clear.
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Ore";
            rock.transform.SetParent(go.transform, false);
            rock.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            rock.transform.localScale = new Vector3(1.3f, 1.0f, 1.3f);
            rock.transform.localRotation = Quaternion.Euler(8f, 25f, 6f);
            var col = rock.GetComponent<Collider>(); if (col != null) Destroy(col);
            var mr = rock.GetComponent<Renderer>();
            if (mr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var m = new Material(sh);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); else m.color = tint;
                    mr.sharedMaterial = m;
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var node = go.AddComponent<MineNode>();
            node.Resource         = res;
            node.YieldPerExtract  = 5;
            node.ExtractCooldown  = 6f;
            node.TotalExtracts    = 0;     // infinite — a steady starter economy that never depletes
            node.RespawnSeconds   = 0f;
            node.UseFiniteReserve = false;
            node.InteractRadius   = 2.5f;  // player can also [F]-tap them
        }
    }
}
