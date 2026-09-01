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
//   attack      -> Mage/Ranger authored Q primary; Knight basic weapon attack.
//   cycleSelect -> HeroTargetIndicator.EngageLock on the Enemy whose instance
//                  id matches the TargetRecord.Id (mirrors the retired
//                  BattleHud9Zone.SelectCycleRow WO-512 routing).
//   flee        -> registered by BattleArenaHud (battle-scoped, not here).
//   potion      -> ConsumableUseService.TryUse(minor-heal-potion, inFight).
//   manaPotion  -> ConsumableUseService.TryUse(cons_mana_draught, inFight).
//   assignable  -> AssignableSkillBar slot -> HeroAbilities.TryCastExtra.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HUD;
using DeNelle.Village.Items;

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

        private static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            RegisterAll();
            if (HubScenes.IsHub(s.name))
                HudPostureReset.OnHubLoaded(s.name);
        }

        private static void RegisterAll()
        {
            // Lazy-resolving closures: the live component is found at FIRE time
            // (follows body swaps / respawns; never a cached destroyed instance).
            HudCommands.RegisterAttack(() =>
            {
                var health = Object.FindAnyObjectByType<HeroHealth>();
                if (health != null && health.IsBlocking)
                {
                    FlowTrace.Step("HudKit", "attack gated while Block is held");
                    return;
                }
                var abilities = Object.FindAnyObjectByType<HeroAbilities>();
                string heroClass = abilities != null ? abilities.HeroClass : null;
                if (abilities != null && (string.Equals(heroClass, "mage", System.StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(heroClass, "ranger", System.StringComparison.OrdinalIgnoreCase)))
                {
                    bool cast = abilities.TryCast(AbilitySlot.Q);
                    FlowTrace.Step("HudKit", "primary command -> class Q for " + heroClass + " " +
                                               (cast ? "FIRED" : "gated"));
                    return;
                }

                var atk = Object.FindAnyObjectByType<PlayerAttackController>();
                if (atk == null) { FlowTrace.Warn("HudKit", "attack fired but no PlayerAttackController in scene"); return; }
                bool swung = atk.TriggerBasicAttack();
                FlowTrace.Step("HudKit", "primary command -> knight basic swing " + (swung ? "SWUNG" : "gated"));
            });

            HudCommands.RegisterBlock(held =>
            {
                var health = Object.FindAnyObjectByType<HeroHealth>();
                if (health == null)
                {
                    FlowTrace.Warn("HudKit", "block fired but no HeroHealth in scene");
                    return;
                }
                health.SetBlocking(held);
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
                    var dmg = en.GetComponent<IDamageable>();
                    if (dmg == null) dmg = en.GetComponentInParent<IDamageable>();
                    if (dmg != null)
                    {
                        indicator.EngageLock(dmg);
                        FlowTrace.Step("HudKit", "cycleSelect -> lock engaged on " + en.name);
                    }
                    return;
                }
                FlowTrace.Warn("HudKit", "cycleSelect: enemy id " + id + " not found (died/despawned)");
            });

            HudCommands.RegisterPotion(() =>
            {
                bool ok = ConsumableUseService.TryUse(HudCommands.HpPotionId, inFight: true);
                FlowTrace.Step("HudKit", "potion command -> TryUse(" + HudCommands.HpPotionId + ") " + (ok ? "OK" : "no-op"));
            });

            HudCommands.RegisterManaPotion(() =>
            {
                bool ok = ConsumableUseService.TryUse(HudCommands.ManaPotionId, inFight: true);
                FlowTrace.Step("HudKit", "manaPotion command -> TryUse(" + HudCommands.ManaPotionId + ") " + (ok ? "OK" : "no-op"));
            });

            HudCommands.RegisterAssignableCast(slot =>
            {
                var bar = AssignableSkillBarAccess.Current;
                var abilities = Object.FindAnyObjectByType<HeroAbilities>();
                if (bar == null || abilities == null)
                {
                    FlowTrace.Warn("HudKit", "assignableCast slot=" + slot + " but no bar/abilities");
                    return;
                }
                string id = bar.AbilityIdForSlot(slot);
                if (string.IsNullOrEmpty(id)) return;
                bool fired = abilities.TryCastExtra(id);
                FlowTrace.Step("HudKit", "assignableCast slot=" + slot + " id=" + id + " -> " + (fired ? "FIRED" : "gated"));
            });

            FlowTrace.Step("HudKit", "command bridge registered (attack, block, cycleSelect, potions, assignable) for scene '" +
                           SceneManager.GetActiveScene().name + "'");
        }
    }
}
