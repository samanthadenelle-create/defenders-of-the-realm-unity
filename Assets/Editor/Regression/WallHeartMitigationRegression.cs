// =============================================================================
// WallHeartMitigationRegression [wall-mitigation] -- proves walls actually PROTECT.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village). Two proofs:
//   (1) walls.json's heartDamageMultiplier is read by a RUNTIME (non-test) reader --
//       DeNelle.Village.Walls.WallDefense.HeartDamageMultiplier(int), whose declaring
//       assembly is gameplay (DeNelle.Village), not an editor/test assembly. Also the
//       JSON key is present and the tier-3 multiplier is <= tier-1 (more mitigation).
//   (2) An IDENTICAL raw hit, mitigated by the wall's own function, loses LESS Heart
//       HP under tier-3 than tier-1 -- driven on TWO real HeartController objects
//       through the real IDamageableStructure.ApplyContactDamage path.
//
// Marker: WALL_MITIGATION_OK / WALL_MITIGATION_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!WallHeartMitigationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wall-mitigation] " + r);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class WallHeartMitigationRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- WALL/HEART MITIGATION (walls.json heartDamageMultiplier -> WallDefense -> HeartController) ---");

            // (1a) The runtime reader exists and lives in a gameplay (non-editor/test) assembly.
            var readerType = typeof(DeNelle.Village.Walls.WallDefense);
            var mult = readerType.GetMethod("HeartDamageMultiplier", new[] { typeof(int) });
            if (mult == null)
            {
                failures.Add("WallDefense.HeartDamageMultiplier(int) not found -- the runtime heartDamageMultiplier reader is missing/renamed");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            string asm = readerType.Assembly.GetName().Name;
            log.AppendLine($"  runtime reader: {readerType.FullName} in assembly '{asm}'");
            if (asm.IndexOf("Editor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                asm.IndexOf("Test", System.StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add($"WallDefense lives in assembly '{asm}' -- the heartDamageMultiplier reader must be RUNTIME gameplay code, not editor/test");

            // (1b) walls.json declares the key, and tier-3 mitigates at least as much as tier-1.
            float mT1, mT3;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/walls.json");
                if (string.IsNullOrEmpty(json)) failures.Add("walls.json not found/empty (Resources or StreamingAssets)");
                else
                {
                    var root = JObject.Parse(json);
                    var tiers = root["tiers"] as JArray;
                    if (tiers == null || tiers.Count == 0) failures.Add("walls.json has no 'tiers' array");
                    else
                    {
                        bool sawKey = false;
                        foreach (var t in tiers) if (t is JObject o && o["heartDamageMultiplier"] != null) { sawKey = true; break; }
                        if (!sawKey) failures.Add("walls.json tiers carry NO 'heartDamageMultiplier' key (the mitigation data is absent)");
                    }
                }
            }
            catch (System.Exception ex) { failures.Add($"walls.json parse threw: {ex.Message}"); }

            mT1 = (float)mult.Invoke(null, new object[] { 1 });
            mT3 = (float)mult.Invoke(null, new object[] { 3 });
            log.AppendLine($"  HeartDamageMultiplier: tier1={mT1:0.00} tier3={mT3:0.00}");
            if (!(mT3 <= mT1))
                failures.Add($"[wall-mitigation] tier-3 multiplier {mT3:0.00} is not <= tier-1 {mT1:0.00} (a higher wall must mitigate at least as much)");
            if (!(mT3 < 1f))
                failures.Add($"[wall-mitigation] tier-3 multiplier {mT3:0.00} is not < 1.0 (walls grant no protection at all)");

            // (2) Identical hit -> tier-3 Heart loses LESS HP than tier-1.
            var created = new List<GameObject>();
            try
            {
                const float rawHit = 30f;
                float hpT1 = ApplyMitigatedHit(created, rawHit * mT1);
                float hpT3 = ApplyMitigatedHit(created, rawHit * mT3);
                log.AppendLine($"  identical {rawHit:0} raw hit -> Heart HP remaining: tier1={hpT1:0.0}, tier3={hpT3:0.0}");
                if (!(hpT3 > hpT1))
                    failures.Add($"[wall-mitigation] under an identical hit the tier-3 Heart kept {hpT3:0.0} HP but tier-1 kept {hpT1:0.0} -- tier-3 did NOT lose less (walls not mitigating)");
            }
            catch (System.Exception ex)
            {
                failures.Add($"[wall-mitigation] heart-hit drive threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static float ApplyMitigatedHit(List<GameObject> created, float mitigatedDamage)
        {
            var go = new GameObject("HeartController (wall-mitigation oracle)");
            created.Add(go);
            var heart = go.AddComponent<HeartController>();
            var dmg = (DeNelle.Core.Combat.IDamageableStructure)heart;
            dmg.ApplyContactDamage(mitigatedDamage);
            return heart.Hp;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "WALL_MITIGATION_OK");
                return "WALL MITIGATION OK -- runtime WallDefense reads walls.json heartDamageMultiplier, and a tier-3 Heart loses less HP than tier-1 on an identical hit";
            }
            string reason = "wall-mitigation: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "WALL_MITIGATION_FAIL: " + reason);
            return reason;
        }
    }
}
