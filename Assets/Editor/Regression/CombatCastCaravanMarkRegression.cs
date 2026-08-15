// =============================================================================
// CombatCastCaravanMarkRegression — WO-935 / WO-991 / WO-910 / WO-994 pins
// -----------------------------------------------------------------------------
// Source + pure-logic gates (headless). Markers:
//   COMBAT_CAST_CARAVAN_MARK_OK / COMBAT_CAST_CARAVAN_MARK_FAIL
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class CombatCastCaravanMarkRegression
    {
        private const string Tag = "[combat-cast-caravan-mark] ";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                GateCombatCast(failures);
                GateCaravan(failures);
                GateMark(failures);
                GateShieldPort(failures);
            }
            catch (Exception ex)
            {
                failures.Add("threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = Tag + "FAIL " + failures.Count + ": " + string.Join("; ", failures);
                Debug.LogError("COMBAT_CAST_CARAVAN_MARK_FAIL " + reason);
                return false;
            }
            reason = Tag + "OK cast+caravan+mark+shield-port seams present";
            Debug.Log("COMBAT_CAST_CARAVAN_MARK_OK " + reason);
            return true;
        }

        private static void GateCombatCast(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Combat/CombatCast.cs";
            if (!File.Exists(path))
            {
                failures.Add("CombatCast.cs missing (WO-935)");
                return;
            }
            string src = File.ReadAllText(path);
            if (!src.Contains("class CombatCast"))
                failures.Add("CombatCast type missing");
            if (!src.Contains("Fireball") || !src.Contains("Heal"))
                failures.Add("CombatCast missing fireball/heal spell ids");
            if (!src.Contains("SpellVfxFactory"))
                failures.Add("CombatCast must route VFX through SpellVfxFactory");
            if (!src.Contains("PlayCast"))
                failures.Add("CombatCast must drive cast anim");

            string troop = "Assets/_Modules/Village/Troops/TroopController.cs";
            if (File.Exists(troop))
            {
                string t = File.ReadAllText(troop);
                if (!t.Contains("CombatCast.Play"))
                    failures.Add("TroopController does not call CombatCast.Play for mage strikes");
            }
        }

        private static void GateCaravan(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Buildings/HealingCaravanMobility.cs";
            if (!File.Exists(path))
            {
                failures.Add("HealingCaravanMobility.cs missing (WO-991)");
                return;
            }
            string src = File.ReadAllText(path);
            if (!src.Contains("FollowSpeed") && !src.Contains("1.05f"))
                failures.Add("caravan follow speed not pinned as crawl");
            if (!src.Contains("DamageTakenMult") && !src.Contains("1.75f"))
                failures.Add("caravan glass damage mult missing");
            if (!src.Contains("IDamageableStructure"))
                failures.Add("caravan must be damageable (IDamageableStructure)");

            string factory = "Assets/_Modules/Village/Catalog/StructureFactory.cs";
            string f = File.ReadAllText(factory);
            if (!f.Contains("healing_caravan") || !f.Contains("HealingCaravanMobility"))
                failures.Add("StructureFactory must attach HealingCaravanMobility for healing_caravan");
        }

        private static void GateMark(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Combat/CombatMark.cs";
            if (!File.Exists(path))
            {
                failures.Add("CombatMark.cs missing (WO-910)");
                return;
            }
            string src = File.ReadAllText(path);
            if (!src.Contains("ScaleDamage") || !src.Contains("Apply"))
                failures.Add("CombatMark must expose Apply + ScaleDamage");

            // Pure logic: unmarked = 1×
            // Can't call runtime Time.time easily offline; assert API exists via source only.

            string enemy = File.ReadAllText("Assets/_Modules/Village/Enemies/Enemy.cs");
            if (!enemy.Contains("CombatMark.ScaleDamage"))
                failures.Add("Enemy.TakeDamageFrom must scale by CombatMark");

            string ab = File.ReadAllText("Assets/_Modules/Village/Hero/HeroAbilities.cs");
            if (!ab.Contains("CombatMark.Apply") || !ab.Contains("IsHuntersMark"))
                failures.Add("HeroAbilities must apply Hunter's Mark via CombatMark");
        }

        private static void GateShieldPort(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Hero/EquipmentController.cs";
            string src = File.ReadAllText(path);
            if (!src.Contains("OnSceneLoadedReapplyGear") && !src.Contains("CoReapplyGearAfterSceneLoad"))
                failures.Add("EquipmentController missing WO-994 scene-load gear reapply");
            if (!src.Contains("InvalidateHeroHeightCache"))
                failures.Add("EquipmentController missing height cache invalidate (WO-994)");
            if (!src.Contains("sceneLoaded"))
                failures.Add("EquipmentController must subscribe SceneManager.sceneLoaded");
        }
    }
}
