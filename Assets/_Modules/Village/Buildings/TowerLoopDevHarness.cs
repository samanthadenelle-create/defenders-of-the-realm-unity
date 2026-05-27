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
            _devTower = BuildDevTower();
            Debug.Log("[TowerLoopDev] B = place a free tower (left-click ground), U = upgrade the last-built tower. (T is the talent tree.)");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                EnsurePlacement();
                if (TowerPlacementSystem.Instance != null)
                    TowerPlacementSystem.Instance.StartPlacing(_devTower);
            }
            else if (Input.GetKeyDown(KeyCode.U))
            {
                UpgradeLastTower();
            }
        }

        private static void EnsurePlacement()
        {
            if (TowerPlacementSystem.Instance != null) return;
            new GameObject("TowerPlacementSystem").AddComponent<TowerPlacementSystem>();
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
