using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeNelle.Core.State;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class CathedralMageHpRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            bool hadOverride = ModifierService.HasOverride;
            GameModifiers saved = hadOverride ? ModifierService.Active.Clone() : null;
            HeroHealth priorHealth = HeroHealth.Instance;
            var go = new GameObject("cathedral-mage-hp-probe");
            try
            {
                var abilities = go.AddComponent<HeroAbilities>();
                var health = go.AddComponent<HeroHealth>();
                Set(abilities, "_heroClass", "mage");
                Set(health, "_abilities", abilities);
                Set(health, "_maxHp", 100f);

                ModifierService.SetOverride(new GameModifiers { MageHpBonusPct = 0.10f });
                if (!Mathf.Approximately(health.MaxHp, 110f))
                    failures.Add("mage base 100 with Cathedral 10% did not resolve MaxHp 110");

                Set(health, "_hp", 50f);
                Set(health, "_appliedEffectiveHpBonus", 0);
                Invoke(health, "SyncGearHp");
                if (!Mathf.Approximately(health.Hp, 60f) || !Mathf.Approximately(health.MaxHp, 110f))
                    failures.Add("adding Cathedral HP did not top current/max HP by exactly base*10%");

                ModifierService.ClearOverride();
                Invoke(health, "SyncGearHp");
                if (!Mathf.Approximately(health.Hp, 60f) || !Mathf.Approximately(health.MaxHp, 100f))
                    failures.Add("removing Cathedral HP changed below-cap current HP or failed to restore max");

                Set(health, "_hp", 110f);
                Set(health, "_appliedEffectiveHpBonus", 10);
                Invoke(health, "SyncGearHp");
                if (!Mathf.Approximately(health.Hp, 100f))
                    failures.Add("removing Cathedral HP did not clamp an over-cap current HP");

                Set(abilities, "_heroClass", "knight");
                ModifierService.SetOverride(new GameModifiers { MageHpBonusPct = 0.10f });
                if (!Mathf.Approximately(health.MaxHp, 100f))
                    failures.Add("Cathedral mage HP bonus leaked to a non-mage class");

                string source = File.ReadAllText("Assets/_Modules/Village/Hero/HeroHealth.cs");
                if (!source.Contains("private int CathedralMageHpBonus => Mathf.RoundToInt(_maxHp *") ||
                    !source.Contains("HeroTalentModifiers.MageMaxHpBonusPct(HeroClassOrDefault)") ||
                    !source.Contains("GearHpBonus + TalentHpBonus + CathedralMageHpBonus"))
                    failures.Add("Cathedral HP no longer composes additively from base HP beside gear/talent");
            }
            catch (Exception ex)
            {
                failures.Add("oracle threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (hadOverride) ModifierService.SetOverride(saved);
                else ModifierService.ClearOverride();
                UnityEngine.Object.DestroyImmediate(go);
                FieldInfo instance = typeof(HeroHealth).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (instance == null) failures.Add("HeroHealth.Instance backing field is no longer restorable");
                else instance.SetValue(null, priorHealth);
            }

            if (failures.Count > 0)
            {
                reason = "CATHEDRAL_MAGE_HP_FAIL: " + string.Join(" | ", failures);
                return false;
            }
            reason = "CATHEDRAL_MAGE_HP_OK - base-additive mage HP, identity, top-up and clamp pinned";
            return true;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().Name, field);
            info.SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, null);
        }
    }
}
