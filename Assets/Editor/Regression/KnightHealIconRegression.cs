using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>Pins the phone action-bar contract for the Knight's authoritative heal.</summary>
    public static class KnightHealIconRegression
    {
        public static bool Run(out string report)
        {
            var failures = new List<string>();

            AbilityCatalog.Reload();
            ConceptIconResolver.ClearCache();

            var heal = AbilityCatalog.Find("knight", AbilitySlot.E);
            if (heal == null)
                failures.Add("knight E ability is missing");
            else
            {
                if (heal.Id != "knight.e")
                    failures.Add("knight E no longer binds authoritative id knight.e");
                if (heal.Verb != "Heal")
                    failures.Add("knight.e must author the visible fallback caption 'Heal'");
            }

            Sprite resolved = ConceptIconResolver.Resolve("knight.e");
            if (resolved == null)
                failures.Add("knight.e resolves no sprite through the production concept-icon path");
            else if (resolved.name != "Paladin5")
                failures.Add("knight.e resolved '" + resolved.name + "', expected approved heal art Paladin5");

            const string artPath = "Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin5.png";
            if (!File.Exists(artPath))
                failures.Add("approved heal art is absent: " + artPath);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(artPath) == null)
                failures.Add("approved heal art is not imported as a Sprite: " + artPath);

            // The common action-slot renderer must retain a non-blank fallback even if art drifts.
            string renderer = File.ReadAllText("Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs");
            if (!renderer.Contains("if (s == null) s = ConceptIconResolver.DefaultSprite();"))
                failures.Add("ActionSlotHandle.SetIcon lost its non-blank missing-sprite fallback");

            report = failures.Count == 0
                ? "KNIGHT_HEAL_ICON_OK: knight.e -> Paladin5 with visible Heal caption and non-blank fallback"
                : "KNIGHT_HEAL_ICON_FAIL: " + string.Join(" | ", failures);
            return failures.Count == 0;
        }
    }
}
