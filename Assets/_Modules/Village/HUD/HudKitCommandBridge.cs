// =============================================================================
// HudKitCommandBridge — Village-side handler registration for the HUD kit's
// Core command sink (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 A4 — P23 HUDKIT).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hud
//
// The kit (DeNelle.HUD) fires DeNelle.Core.HUD.HudCommands; this bridge is the
// Village side that plugs the real gameplay handlers in, RE-REGISTERED ON EVERY
// SCENE LOAD so a handler can never go stale on a destroyed body (the exact
// failure mode that killed the talk button's reflection push — see
// PostureSignals header). Handlers resolve their scene objects LAZILY AT FIRE
// TIME, so registration order vs. hero spawn order cannot break them.
//
// Registered here:
//   attack      -> PlayerAttackController.TriggerBasicAttack() (the owner's
//                  HUD seam, same gates as Space/LMB).
//   cycleSelect -> HeroTargetIndicator.EngageLock on the Enemy whose instance
//                  id matches the TargetRecord.Id (mirrors the retired
//                  BattleHud9Zone.SelectCycleRow WO-512 routing).
//   flee        -> registered by BattleArenaHud (battle-scoped, not here).
//   potion      -> NOT registered (no Village consumable-use seam exists yet;
//                  the kit hides the slot while HudCommands.HasPotion is false).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HUD;

namespace DeNelle.Village.Hud
{
    /// <summary>Registers the Village handlers behind the HUD kit's commands (see header).</summary>
    public static class HudKitCommandBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RegisterAll();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => RegisterAll();

        private static void RegisterAll()
        {
            // Lazy-resolving closures: the live component is found at FIRE time
            // (follows body swaps / respawns; never a cached destroyed instance).
            HudCommands.RegisterAttack(() =>
            {
                var atk = Object.FindAnyObjectByType<PlayerAttackController>();
                if (atk == null) { FlowTrace.Warn("HudKit", "attack fired but no PlayerAttackController in scene"); return; }
                bool swung = atk.TriggerBasicAttack();
                FlowTrace.Step("HudKit", "attack command -> TriggerBasicAttack " + (swung ? "SWUNG" : "gated"));
            });

            HudCommands.RegisterCycleSelect(id =>
            {
                if (string.IsNullOrEmpty(id)) return;
                var indicator = Object.FindAnyObjectByType<HeroTargetIndicator>();
                if (indicator == null) { FlowTrace.Warn("HudKit", "cycleSelect fired but no HeroTargetIndicator"); return; }
                int wanted;
                if (!int.TryParse(id, out wanted)) return;
                var enemies = Object.FindObjectsByType<Enemy>();
                for (int i = 0; i < enemies.Length; i++)
                {
                    var en = enemies[i];
                    if (en == null || en.IsDead || en.GetInstanceID() != wanted) continue;
                    var dmg = en.GetComponent<IDamageable>() ?? en.GetComponentInParent<IDamageable>();
                    if (dmg != null)
                    {
                        indicator.EngageLock(dmg);
                        FlowTrace.Step("HudKit", "cycleSelect -> lock engaged on " + en.name);
                    }
                    return;
                }
                FlowTrace.Warn("HudKit", "cycleSelect: enemy id " + id + " not found (died/despawned)");
            });

            FlowTrace.Step("HudKit", "command bridge registered (attack, cycleSelect) for scene '" +
                           SceneManager.GetActiveScene().name + "'");
        }
    }
}
