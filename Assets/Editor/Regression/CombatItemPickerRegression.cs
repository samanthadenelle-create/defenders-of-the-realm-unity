#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Locks the adaptive combat HUD's single, paused, authoritative Item flow.</summary>
    public static class CombatItemPickerRegression
    {
        public static bool Run(out string result)
        {
            try
            {
                string hud = File.ReadAllText("Assets/_Modules/HUD/Kit/HudKitController.cs");
                Require(hud, "Register(\"itemSlot\"");
                Require(hud, "SetCaption(\"ITEM\")");
                Require(hud, "WorldHold.Acquire(WorldHold.ReasonCombatItemPicker)");
                Require(hud, "Gameplay is paused while you choose.");
                Require(hud, "HudCommands.HasPotion && c.HpCooldownRemaining <= 0f");
                Require(hud, "HudCommands.HasManaPotion && c.ManaCooldownRemaining <= 0f");
                Require(hud, "if (_itemUseInFlight) return;");
                Require(hud, "CloseItemPicker();");
                Forbid(hud, "Register(\"hpPotionSlot\"");
                Forbid(hud, "Register(\"manaPotionSlot\"");

                string hold = File.ReadAllText("Assets/_Modules/Core/UI/WorldHold.cs");
                Require(hold, "ReasonCombatItemPicker = \"combat-item-picker\"");

                result = "Combat HUD exposes one Item action; its picker freezes via WorldHold, rechecks live eligibility, and guards repeated use.";
                return true;
            }
            catch (Exception ex)
            {
                result = ex.Message;
                return false;
            }
        }

        private static void Require(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Combat Item contract missing: " + token);
        }

        private static void Forbid(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Retired dual-potion HUD returned: " + token);
        }
    }
}
#endif
