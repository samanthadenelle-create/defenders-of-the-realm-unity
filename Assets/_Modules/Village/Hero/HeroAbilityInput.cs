// =============================================================================
// HeroAbilityInput — fires HeroAbilities.TryCast from keyboard + gamepad.
// -----------------------------------------------------------------------------
// Keyboard slots use 1 / 2 / 3 / 4 NOT Q / W / E / R because W is reserved by
// HeroLocomotion (forward movement). The HUD labels stay Q W E R as ability-
// slot mnemonics; the in-game Controls menu surfaces the actual hotkeys.
//
// Gamepad face buttons: South=1, East=2, West=3, North=4.
//
// PRIMARY ATTACK (2026-06-02 playtest fix — "no targeting / couldn't hit him,
// heal works"): slot Q is every class's basic strike (Mage Arcane Bolt, Ranger
// Quick Shot, Knight Shield Bash — all 0-mana, short-cd, in abilities.json), but
// it was only on the unintuitive '1' key while the HUD said "Q". So the player
// pressed Q (nothing) and left-click (a 3.2 m melee whiff that never reaches a
// ranged Mage). Heal needs no target, so ONLY heal seemed to work. Fix: bind the
// primary attack (slot Q) to LEFT-CLICK and SPACE too — the universal "attack the
// locked target" input. It resolves against HeroTargetIndicator's auto-lock aim
// point, so clicking fires your main attack at the reticled enemy.
//
// Wiring: VillageSceneBuilder.BuildHero attaches this component alongside
// HeroAbilities. The HUD ability buttons (VillageHudController) can also call
// HeroAbilities.TryCast(slot) directly — both paths converge on the same
// mana / cooldown gate inside TryCast.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroAbilities))]
    public sealed class HeroAbilityInput : MonoBehaviour
    {
        private HeroAbilities _abilities;

        private void Awake()
        {
            _abilities = GetComponent<HeroAbilities>();
        }

        private void Update()
        {
            if (_abilities == null) return;

            // WO-377: no ability casts / primary attacks while a Yarn dialogue is on
            // screen — a click on the dialogue box used to fall through and fire slot Q,
            // breaking the story beat. HeroLocomotion raises this gate on dialogue start
            // and clears it on complete (one suppression source for the whole input surface).
            if (HeroLocomotion.InputSuppressed) return;

            // PRIMARY ATTACK: left-click / Space / gamepad-South fire slot Q (the
            // class basic strike) at the auto-locked target. Universal, forgiving
            // "attack the thing under the reticle" input — the fix for the player
            // pressing 1 and nothing connecting at range.
            if (PrimaryAttackPressed())
            {
                _abilities.TryCast(AbilitySlot.Q);
                return;   // don't also double-fire from the number row this frame
            }

            if (ReadSlot() is AbilitySlot slot)
                _abilities.TryCast(slot);
        }

        /// <summary>True the frame the universal primary-attack input is pressed.</summary>
        private static bool PrimaryAttackPressed()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            // Legacy fallback (left mouse).
            if (UnityEngine.Input.GetMouseButtonDown(0)) return true;
            return false;
        }

        /// <summary>
        /// Returns the first ability slot whose hotkey was pressed this frame
        /// (numeric row + gamepad face buttons + legacy fallback).
        /// </summary>
        private static AbilitySlot? ReadSlot()
        {
            // Mobile-first: the keyboard 1/2/3/4 ability-slot hotkeys are REMOVED. Abilities
            // fire from the on-screen HUD skill buttons (VillageHudController -> HeroAbilities.
            // TryCast(slot)) and from the gamepad face buttons below.
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.buttonSouth.wasPressedThisFrame) return AbilitySlot.Q;
                if (gp.buttonEast.wasPressedThisFrame)  return AbilitySlot.W;
                if (gp.buttonWest.wasPressedThisFrame)  return AbilitySlot.E;
                if (gp.buttonNorth.wasPressedThisFrame) return AbilitySlot.R;
            }

            return null;
        }
    }
}
