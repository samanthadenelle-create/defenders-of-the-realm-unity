// =============================================================================
// TowerPerkRegression — WO-432 (owner 2026-06-28). Headless gate for the DESIGNED,
// data-driven tower-upgrade tech (tower-perks.json + DeNelle.Village.TowerPerkTable).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core), so
// it loads tower-perks.json through the SAME CanonicalJson loader the game uses and
// drives the REAL interpreter — a schema break or a bad number is a hard FAIL line,
// not a silent "upgrade does nothing" at runtime (the no-op this WO closed).
//
// Proves: (a) tower-perks.json is present + parses to >= 3 tiers with sane fields;
//         (b) the apply math is MONOTONIC — per tier damage RISES (tier2 > tier1 >
//             base), range is non-decreasing then rising, and the fire cooldown
//             SHRINKS (faster fire). i.e. a tower genuinely gains dmg/range/fire-rate
//             on upgrade.
//
// Wire into the suite from DataRegression.RunAll (one line — see the WO report):
//   if (!TowerPerkRegression.Run(out var towerPerkReason)) failures.Add(towerPerkReason);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class TowerPerkRegression
    {
        // Mirror of the JSON shape for the direct parse-presence check (independent of
        // the interpreter's built-in fallback, so a MISSING file is caught, not masked).
        private sealed class PerkRow
        {
            [JsonProperty("tier")]             public int Tier;
            [JsonProperty("name")]             public string Name = "";
            [JsonProperty("damageMult")]       public float DamageMult = 1f;
            [JsonProperty("damageAdd")]        public float DamageAdd;
            [JsonProperty("rangeAdd")]         public float RangeAdd;
            [JsonProperty("fireRateMult")]     public float FireRateMult = 1f;
            [JsonProperty("signatureAbility")] public string SignatureAbility = "";
        }

        private sealed class PerkFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("tiers")]   public List<PerkRow> Tiers = new List<PerkRow>();
        }

        /// <summary>
        /// Runs the tower-perk regression. Returns true on pass; on failure returns false and
        /// sets <paramref name="reason"/> to a single aggregated failure line for the suite.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TOWER PERKS (tower-perks.json -> TowerPerkTable) ---");

            // (a) DIRECT presence/parse — catches a missing file the interpreter would otherwise
            //     hide behind its built-in fallback. Uses the same WebGL-safe loader the game uses.
            string json = DeNelle.Core.CanonicalJson.Read(TowerPerkTable.RelativePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                failures.Add($"tower-perks.json not found/empty at '{TowerPerkTable.RelativePath}' (CanonicalJson.Read returned null)");
            }
            else
            {
                PerkFile file = null;
                try { file = JsonConvert.DeserializeObject<PerkFile>(json); }
                catch (System.Exception ex) { failures.Add($"tower-perks.json failed to parse: {ex.Message}"); }

                if (file == null || file.Tiers == null || file.Tiers.Count < 3)
                    failures.Add($"tower-perks.json deserialized to {(file?.Tiers?.Count ?? 0)} tier(s) — expected >= 3 (Lvl 1/2/3)");
                else
                {
                    log.AppendLine($"tower-perks.json -> {file.Tiers.Count} tier rows (v{file.Version})");
                    foreach (var r in file.Tiers)
                    {
                        if (r == null) { failures.Add("tower-perks.json has a null tier row"); continue; }
                        if (r.Tier < 1) failures.Add($"tower-perks.json tier row has invalid tier {r.Tier}");
                        if (r.DamageMult <= 0f) failures.Add($"tower-perks.json tier {r.Tier} damageMult <= 0 ({r.DamageMult})");
                        if (r.FireRateMult <= 0f) failures.Add($"tower-perks.json tier {r.Tier} fireRateMult <= 0 ({r.FireRateMult})");
                        log.AppendLine($"  T{r.Tier} '{r.Name}' dmg x{r.DamageMult:0.00}+{r.DamageAdd} range +{r.RangeAdd} fireRate x{r.FireRateMult:0.00} sig='{r.SignatureAbility}'");
                    }
                }
            }

            // (b) APPLY MATH through the REAL interpreter — monotonic gains per tier.
            TowerPerkTable.Reload();

            const float baseDamage = 20f;
            const float baseRange  = 14f;
            const float baseCd     = 1.1f;

            float dBase = baseDamage;
            float d1 = TowerPerkTable.EffectiveDamage(baseDamage, 1);
            float d2 = TowerPerkTable.EffectiveDamage(baseDamage, 2);
            float d3 = TowerPerkTable.EffectiveDamage(baseDamage, 3);

            // The headline invariant the owner asked for: upgrading GIVES more damage.
            if (!(d1 > dBase)) failures.Add($"tower perk: tier-1 damage {d1:0.0} is not > base {dBase:0.0} (upgrade grants nothing)");
            if (!(d2 > d1))    failures.Add($"tower perk: tier-2 damage {d2:0.0} is not > tier-1 {d1:0.0} (not monotonic)");
            if (!(d3 > d2))    failures.Add($"tower perk: tier-3 damage {d3:0.0} is not > tier-2 {d2:0.0} (not monotonic)");

            float r1 = TowerPerkTable.EffectiveRange(baseRange, 1);
            float r2 = TowerPerkTable.EffectiveRange(baseRange, 2);
            float r3 = TowerPerkTable.EffectiveRange(baseRange, 3);
            if (!(r1 >= baseRange)) failures.Add($"tower perk: tier-1 range {r1:0.0} dropped below base {baseRange:0.0}");
            if (!(r2 > r1))         failures.Add($"tower perk: tier-2 range {r2:0.0} is not > tier-1 {r1:0.0}");
            if (!(r3 > r2))         failures.Add($"tower perk: tier-3 range {r3:0.0} is not > tier-2 {r2:0.0}");

            float c1 = TowerPerkTable.EffectiveCooldown(baseCd, 1);
            float c2 = TowerPerkTable.EffectiveCooldown(baseCd, 2);
            float c3 = TowerPerkTable.EffectiveCooldown(baseCd, 3);
            if (!(c2 < c1)) failures.Add($"tower perk: tier-2 cooldown {c2:0.00} is not < tier-1 {c1:0.00} (fire rate not faster)");
            if (!(c3 < c2)) failures.Add($"tower perk: tier-3 cooldown {c3:0.00} is not < tier-2 {c2:0.00} (fire rate not faster)");

            log.AppendLine($"  apply math: dmg base={dBase:0.0} -> L1={d1:0.0} -> L2={d2:0.0} -> L3={d3:0.0}");
            log.AppendLine($"  apply math: range {r1:0.0}/{r2:0.0}/{r3:0.0} | cooldown {c1:0.00}/{c2:0.00}/{c3:0.00}");

            if (failures.Count == 0)
            {
                reason = null;
                Debug.Log(log.ToString() + "TOWER_PERKS_OK");
                return true;
            }

            reason = "tower-perks: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "TOWER_PERKS_FAIL: " + reason);
            return false;
        }
    }
}
