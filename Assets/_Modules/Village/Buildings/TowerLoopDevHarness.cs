// =============================================================================
// TowerLoopDevHarness — Sprint C verify aid (DEV-ONLY; compiled out of release
// builds). Makes the place→build→upgrade loop testable by just hitting Play, with
// zero editor setup: no seeding, no economy friction, no BuildMenu integration.
//   • B = arm placement of a free, skill-gate-free tower (left-click ground to
//     drop it; TowerConstructionQueue raises it over buildTime). NOTE: T is taken
//     by the hero talent tree (HeroTalentPanelBootstrap), so placement is on B.
//   • U = upgrade the most-recently-built Tower one level.
// -----------------------------------------------------------------------------
// This is the DEV trigger that sidesteps the open product decision (gap #3: should
// the new TowerPlacementSystem replace the existing BuildMenu/BuildingCatalog flow,
// or run as a separate placement mode?). The PRODUCTION entry point (a real build-
// menu button → StartPlacing) is the follow-up once that's decided; this harness is
// only here so the loop can be verified now. It self-bootstraps like the project's
// other runtime singletons and never ships (UNITY_EDITOR || DEVELOPMENT_BUILD).
// =============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using DeNelle.Core.Data;
using DeNelle.Core.Catalog;

namespace DeNelle.Village
{
    /// <summary>Dev-only hotkeys to exercise the tower loop without UI wiring.</summary>
    public sealed class TowerLoopDevHarness : MonoBehaviour
    {
        private TowerData _devTower;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<TowerLoopDevHarness>() != null) return;
            var go = new GameObject("TowerLoopDevHarness");
            DontDestroyOnLoad(go);
            go.AddComponent<TowerLoopDevHarness>();
        }

        private void Awake()
        {
            // Prefer the seeded DevTower (real KayKit models via visualPrefab; run
            // Defenders > Seed Tower Data). Fall back to a code-built free tower
            // (procedural cube) if it hasn't been seeded.
            var seeded = Resources.Load<TowerData>("Towers/DevTower");
            _devTower = seeded != null ? seeded : BuildDevTower();
            Debug.Log("[TowerLoopDev] B = toggle Build Mode (catalog palette), U = upgrade the last-built tower, K = +1 hero level, " +
                      "J = spawn 'Catapult' catalog entry via StructureFactory.Create (data-only new type). " +
                      "(T is the talent tree.) Tower visual: " +
                      (seeded != null ? "seeded model." : "procedural placeholder — run Defenders > Seed Tower Data."));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                // FIX (build menu not showing) — B used to arm the OLD single-tower
                // TowerPlacementSystem, which drops a placement marker but has NO
                // palette: the player got a circle and no way to pick what to build
                // (incl. the catalog'd jeweler). Route B through the SAME entry point
                // the production HUD "Build" button uses (BuildButtonBridge →
                // BuildModeController.Toggle()), which self-creates the grid + ghost +
                // the code-built BuildPaletteUI populated from CatalogRegistry. The dev
                // single-tower path is retired (the catalog palette supersedes it).
                BuildModeController.EnsureExists()?.Toggle();
            }
            else if (Input.GetKeyDown(KeyCode.U))
            {
                UpgradeLastTower();
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                // Force the wave loop forward (skip the countdown) — for testing when
                // the loop stalls / the countdown disappears. ForceBeginNextWave is
                // WaveManager's existing public method.
                var wm = FindAnyObjectByType<WaveManager>();
                if (wm != null) { wm.ForceBeginNextWave(); Debug.Log("[TowerLoopDev] N = ForceBeginNextWave()."); }
                else Debug.Log("[TowerLoopDev] N: no WaveManager in scene.");
            }
            else if (Input.GetKeyDown(KeyCode.K))
            {
                LevelUpHero();
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                // J = catalog->factory data-only spawn. Proves a NEW structure type
                // (registered purely as a CatalogEntry, no new code) can be made live
                // via the single StructureFactory.Create path.
                SpawnCatalogStructure("tower_catapult");
            }
        }

        // Spawn a registered CatalogEntry by id a few metres in front of the hero,
        // routed through the ONE creation path (StructureFactory.Create). DEV-ONLY.
        private static void SpawnCatalogStructure(string entryId)
        {
            var entry = CatalogRegistry.Get(entryId);
            if (entry == null)
            {
                Debug.LogWarning($"[TowerLoopDev] J: catalog entry '{entryId}' not registered " +
                                 "(CatalogBootstrap should have registered it).");
                return;
            }

            // Pose: a few metres in front of the hero (fallback: in front of the
            // main camera, then world origin) so the spawn is always harmless.
            Vector3 origin; Quaternion facing;
            // WO-450 cleanup: "HeroTarget" is NOT a declared tag, so the old raw
            // GameObject.FindWithTag("HeroTarget") threw a UnityException. Resolve the
            // hero by component (same fix as the enemy AI), then fall back to the
            // built-in "Player" tag, which is always safe.
            var loco = FindFirstObjectByType<HeroLocomotion>();
            var heroGo = loco != null ? loco.gameObject : null;
            if (heroGo == null) heroGo = GameObject.FindWithTag("Player");
            if (heroGo != null)
            {
                var ht = heroGo.transform;
                origin = ht.position + ht.forward * 4f;
                facing = Quaternion.LookRotation(-ht.forward, Vector3.up);
            }
            else if (Camera.main != null)
            {
                var ct = Camera.main.transform;
                origin = ct.position + ct.forward * 6f;
                facing = Quaternion.LookRotation(-ct.forward, Vector3.up);
            }
            else
            {
                origin = Vector3.zero;
                facing = Quaternion.identity;
            }
            origin.y = 0f; // seat on ground for a ground-placed tower

            // Parent under a dev container so spawns are easy to find/clear.
            var container = GameObject.Find("DevCatalogSpawns");
            if (container == null) container = new GameObject("DevCatalogSpawns");

            var spawned = StructureFactory.Create(entry, new Pose(origin, facing),
                                                  container.transform);
            Debug.Log(spawned != null
                ? $"[TowerLoopDev] J = spawned catalog entry '{entryId}' ('{entry.displayName}') " +
                  $"at {origin} via StructureFactory.Create."
                : $"[TowerLoopDev] J: StructureFactory.Create returned null for '{entryId}'.");
        }

        private static void EnsurePlacement()
        {
            if (TowerPlacementSystem.Instance != null) return;
            new GameObject("TowerPlacementSystem").AddComponent<TowerPlacementSystem>();
        }

        private static void LevelUpHero()
        {
            var hp = HeroProgression.Instance;
            if (hp == null) { Debug.Log("[TowerLoopDev] K: no HeroProgression in scene yet."); return; }
            int gained = hp.AddXp(hp.XpToNext + 1f);
            Debug.Log($"[TowerLoopDev] K = leveled hero up (+{gained}).");
        }

        private static void UpgradeLastTower()
        {
            var towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
            if (towers.Length == 0) { Debug.Log("[TowerLoopDev] No built towers yet — place one (B) and let it finish."); return; }
            var t = towers[towers.Length - 1];
            bool ok = t.Upgrade();
            Debug.Log($"[TowerLoopDev] Upgrade {t.name} → L{t.CurrentLevel} ({(ok ? "ok" : "already max")}).");
        }

        // A free, skill-gate-free TowerData built in code so the loop is testable
        // with no editor setup (no seeded assets, no economy cost). buildTime is
        // shortened to 2s for quick iteration.
        private static TowerData BuildDevTower()
        {
            var d = ScriptableObject.CreateInstance<TowerData>();
            d.towerName = "DevTower";
            d.cost = 0;
            d.buildTime = 2f;
            d.requiredSkill = new SkillRequirement { type = SkillType.None, minLevel = 0 };
            d.upgrades = new TowerUpgrade[3];
            for (int i = 0; i < 3; i++)
            {
                d.upgrades[i] = new TowerUpgrade
                {
                    visualPrefab = null,
                    ability      = SpecialAbility.None,
                    range        = 8f + i * 2f,
                    damage       = 6f + i * 3f,
                    upgradeCost  = 0,
                };
            }
            return d;
        }
    }
}
#endif
