// =============================================================================
// HeroAbilityInput — fires HeroAbilities.TryCast from keyboard + gamepad.
// -----------------------------------------------------------------------------
// Keyboard slots use 1 / 2 / 3 / 4 NOT Q / W / E / R because W is reserved by
// HeroLocomotion (forward movement). The HUD labels stay Q W E R as ability-
// slot mnemonics; the in-game Controls menu surfaces the actual hotkeys.
//
// Gamepad face buttons: South=1, East=2, West=3, North=4.
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
            if (ReadSlot() is AbilitySlot slot)
                _abilities.TryCast(slot);
        }

        /// <summary>
        /// Returns the first ability slot whose hotkey was pressed this frame
        /// (numeric row + gamepad face buttons + legacy fallback).
        /// </summary>
        private static AbilitySlot? ReadSlot()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) return AbilitySlot.Q;
                if (kb.digit2Key.wasPressedThisFrame) return AbilitySlot.W;
                if (kb.digit3Key.wasPressedThisFrame) return AbilitySlot.E;
                if (kb.digit4Key.wasPressedThisFrame) return AbilitySlot.R;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.buttonSouth.wasPressedThisFrame) return AbilitySlot.Q;
                if (gp.buttonEast.wasPressedThisFrame)  return AbilitySlot.W;
                if (gp.buttonWest.wasPressedThisFrame)  return AbilitySlot.E;
                if (gp.buttonNorth.wasPressedThisFrame) return AbilitySlot.R;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) return AbilitySlot.Q;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) return AbilitySlot.W;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) return AbilitySlot.E;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) return AbilitySlot.R;

            return null;
        }
    }
}
