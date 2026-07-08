// =============================================================================
// AegisSetReachabilityRegression — headless oracle proving the WO-295 "Oathweld"
// legendary set bonus is REACHABLE (a full Aegis set can actually be assembled).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core), so
// it reads the REAL GearCatalog + the REAL WeaponDef/ArmorDef.IsAegis predicates +
// GearCatalog.ArmorFitsClass — the same seams GearLoadout.AegisSetActive consults.
//
// THE INVARIANT (data/logic-decidable, no scene / no PlayMode):
//   GearLoadout.AegisSetActive is true only when EquippedWeapon.IsAegis AND
//   EquippedArmor.IsAegis. IsAegis is true only when the item's setId == "aegis".
//   Therefore the Oathweld ward (AegisSetEffect) + the per-class Aegis weapon perk are
//   REACHABLE for a class J iff BOTH of these exist and are co-equippable by J:
//     • an aegis WEAPON whose job matches J (WeaponDef.IsAegis == true), and
//     • an aegis ARMOR that J may wear (ArmorDef.IsAegis && ArmorFitsClass(armor, J)
//       && armor.job matches J), at a shared level.
//   If any aegis weapon carries NO setId (IsAegis false), or no wearable aegis armor
//   co-exists for its class, the full set can NEVER form -> the ward is DEAD CONTENT.
//
// HISTORY / TRUTHFULNESS NOTE (read before assuming a verdict):
//   docs/MASTER_CATALOG/village-hero.md FLAGS record a real past bug — "the 4 aegis
//   weapons have no setId in weapons.json ... AegisSetActive can NEVER be true ... the
//   ward is UNREACHABLE." This oracle was authored to DETECT exactly that. It reads the
//   LIVE catalog through the real load path and reports the TRUTH — if the data has since
//   been corrected (setId added to the four aegis weapons), it PASSES honestly; if the
//   data is (or is reverted to) the broken state, it FAILS and names the unreachable set.
//   It never fabricates a pass and never hard-codes a fail.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!AegisSetReachabilityRegression.Run(out var aegisReason)) failures.Add(aegisReason); else log.AppendLine("[aegis-reach] " + aegisReason);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class AegisSetReachabilityRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- AEGIS SET REACHABILITY (WO-295 Oathweld ward) ---");

            // Read the REAL catalogs through the game's load path (Resources-first, the copy
            // that WINS at runtime), not a re-parse.
            GearCatalog.Reload();
            var weapons = GearCatalog.AllWeapons();
            var armors  = GearCatalog.AllArmors();

            var aegisWeapons = new List<WeaponDef>();
            foreach (var w in weapons) if (w != null && w.IsAegis) aegisWeapons.Add(w);
            var aegisArmors = new List<ArmorDef>();
            foreach (var a in armors) if (a != null && a.IsAegis) aegisArmors.Add(a);

            log.AppendLine($"catalog: {weapons.Count} weapons ({aegisWeapons.Count} aegis), " +
                           $"{armors.Count} armors ({aegisArmors.Count} aegis)");

            // Guard 1: at least ONE aegis armor must exist, else no class can ever complete a set.
            if (aegisArmors.Count == 0)
                failures.Add("no ArmorDef carries setId 'aegis' (ArmorDef.IsAegis false for all) — the Aegis set has no armor half, ward UNREACHABLE for every class");

            // Guard 2: the FLAGGED bug — aegis weapons with no setId. Cross-reference by the
            // canonical aegis weapon ids so a missing setId is named precisely (this is the
            // exact "IsAegis false for the 4 aegis weapons" defect the catalog recorded).
            string[] canonicalAegisWeaponIds =
            {
                "aegis_emberbrand", "aegis_heartwood_longbow", "aegis_aetherstaff", "aegis_hallowed_censer"
            };
            foreach (var id in canonicalAegisWeaponIds)
            {
                var w = GearCatalog.FindWeapon(id);
                if (w == null)
                {
                    failures.Add($"canonical aegis weapon '{id}' is MISSING from the catalog (renamed/removed) — its class can't complete the set");
                    continue;
                }
                if (!w.IsAegis)
                    failures.Add($"aegis weapon '{id}' (job '{w.job}') has NO setId 'aegis' -> WeaponDef.IsAegis is FALSE -> GearLoadout.AegisSetActive can never be true for a {w.job} -> Oathweld ward UNREACHABLE");
            }

            // Reachability per aegis-weapon class: a co-equippable aegis armor must exist.
            foreach (var w in aegisWeapons)
            {
                string job = w.job ?? "any";
                int reqLevel = w.req != null ? w.req.level : 1;

                ArmorDef fit = null;
                foreach (var a in aegisArmors)
                {
                    if (!GearCatalog.ArmorFitsClass(a, job)) continue;   // weight-class gate (light/heavy)
                    if (!JobOk(a.job, job)) continue;                    // job gate ("any" fits all)
                    fit = a;
                    break;
                }

                if (fit == null)
                {
                    failures.Add($"class '{job}': aegis weapon '{w.id}' has NO co-equippable aegis armor " +
                                 $"(weight/job gate excludes every aegis armor) -> full set unreachable for {job}");
                    continue;
                }

                int setLevel = Mathf.Max(reqLevel, fit.req != null ? fit.req.level : 1);
                log.AppendLine($"REACHABLE: {job} -> weapon '{w.id}' + armor '{fit.id}' co-equippable at level {setLevel} " +
                               $"(AegisSetActive can be true -> Oathweld ward + perk live)");
            }

            // INFORMATIONAL (not a pass/fail gate): does the AUTO-EQUIP path (BestWeapon/BestArmor,
            // the same picks GearLoadout.Refresh makes) land on the aegis pair, so the ward
            // activates without a manual equip? A "no" here is a convenience gap, NOT an
            // unreachability bug (the player can still manually equip the set) — logged only.
            foreach (var w in aegisWeapons)
            {
                string job = w.job ?? "any";
                int reqLevel = w.req != null ? w.req.level : 1;
                var bw = GearCatalog.BestWeapon(job, reqLevel);
                var ba = GearCatalog.BestArmor(job, reqLevel);
                bool autoSet = bw != null && bw.IsAegis && ba != null && ba.IsAegis;
                log.AppendLine($"auto-equip@L{reqLevel} {job}: bestWeapon='{bw?.id ?? "<null>"}'(aegis={bw?.IsAegis}) " +
                               $"bestArmor='{ba?.id ?? "<null>"}'(aegis={ba?.IsAegis}) -> auto-activates set={autoSet}");
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "AEGIS_REACH_OK");
                reason = "AEGIS REACHABILITY OK — every aegis weapon has a co-equippable aegis armor; the Oathweld ward is reachable";
                return true;
            }
            reason = "aegis-reachability: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "AEGIS_REACH_FAIL: " + reason);
            return false;
        }

        // Mirror GearCatalog's private JobMatches for the armor's job field ("any"/empty fits all).
        private static bool JobOk(string itemJob, string heroJob)
        {
            if (string.IsNullOrEmpty(itemJob)) return true;
            if (itemJob.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;
            return itemJob.Equals(heroJob ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}
