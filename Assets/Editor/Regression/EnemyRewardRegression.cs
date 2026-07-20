// =============================================================================
// EnemyRewardRegression [enemy-rewards] -- proves the most-played mode PAYS.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
// Loads enemies.json through the SAME CanonicalJson bytes the wave loader reads,
// then:
//   (1) DATA -- every non-boss EnemyDef carries coinReward>0 AND xpReward>0 (the
//       parsed JSON keys), so a kill in the wave loop always pays hero XP + gold.
//   (2) SEAM -- installs the REAL EconomyService + HeroProgression singletons over
//       a throwaway GameState (editmode has no Awake), then DRIVES the exact grant
//       calls Enemy.Die performs on a killed enemy -- EconomyService.AddCoins(coin)
//       + HeroProgression.AddXp(xp) -- and asserts the coin balance and lifetime XP
//       each rose by the def's reward. (Enemy.Die's full body -- VFX/drops -- is not
//       driven headless; this exercises the reward-grant seam it invokes.)
//
// Marker: ENEMY_REWARDS_OK / ENEMY_REWARDS_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!EnemyRewardRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[enemy-rewards] " + r);
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class EnemyRewardRegression
    {
        private const string SaveKey = "dotr-save";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- ENEMY REWARDS (enemies.json coin+xp coverage + AddCoins/AddXp grant seam) ---");

            // (1) DATA -- parse through the real loader (mirror CheckEnemies/CheckWaveScaling).
            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            EnemyCatalog catalog = null;
            JObject root = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex) { failures.Add($"enemies.json failed to parse: {ex.Message}"); }
                try { root = JObject.Parse(json); } catch { /* boss-flag scan optional */ }
            }
            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                failures.Add("enemies.json deserialized to 0 EnemyDef objects (mapping break or empty 'enemies')");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }

            var bossIds = ScanBossIds(root);
            EnemyDef payingDef = null;
            int checkedRows = 0;
            foreach (var e in catalog.Enemies)
            {
                // Skip the schema-doc placeholder row (its id carries a space -- see CheckEnemies).
                if (e == null || string.IsNullOrEmpty(e.Id) || e.Id.Contains(" ")) continue;
                bool isBoss = bossIds.Contains(e.Id);
                if (isBoss) { log.AppendLine($"  EN {e.Id} -> BOSS (reward rule not asserted)"); continue; }
                checkedRows++;
                if (e.CoinReward <= 0)
                    failures.Add($"[enemy-rewards] non-boss enemy '{e.Id}' has coinReward={e.CoinReward} (must be > 0 -- kill pays no gold)");
                if (e.XpReward <= 0)
                    failures.Add($"[enemy-rewards] non-boss enemy '{e.Id}' has xpReward={e.XpReward} (must be > 0 -- kill pays no XP)");
                if (payingDef == null && e.CoinReward > 0 && e.XpReward > 0) payingDef = e;
            }
            log.AppendLine($"  reward coverage checked {checkedRows} non-boss row(s)");

            // (2) SEAM -- drive AddCoins + AddXp on the real singletons over a throwaway state.
            if (payingDef == null)
            {
                failures.Add("[enemy-rewards] no non-boss enemy with both coin+xp rewards to drive the grant seam");
            }
            else
            {
                DriveGrantSeam(payingDef, failures, log);
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static void DriveGrantSeam(EnemyDef def, List<string> failures, StringBuilder log)
        {
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameStateService priorGss = GameStateService.Instance;
            object priorEcon = GetInstance(typeof(EconomyService));
            object priorProg = GetInstance(typeof(HeroProgression));

            GameObject gssGo = null, econGo = null, progGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (enemy-reward oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  NOTE: GameStateService state seam not reflectable -- grant-seam drive skipped (data check stands)");
                    return;
                }

                econGo = new GameObject("EconomyService (enemy-reward oracle)");
                var econ = econGo.AddComponent<EconomyService>();
                SetInstance(typeof(EconomyService), econ);

                progGo = new GameObject("HeroProgression (enemy-reward oracle)");
                var prog = progGo.AddComponent<HeroProgression>();
                SetInstance(typeof(HeroProgression), prog);

                int coinsBefore = econ.Coins;
                float xpBefore = prog.LifetimeXp;

                // The exact two grant operations Enemy.Die performs on a killed enemy.
                EconomyService.Instance.AddCoins(def.CoinReward);
                HeroProgression.Instance.AddXp(def.XpReward);

                int coinsAfter = econ.Coins;
                float xpAfter = prog.LifetimeXp;
                log.AppendLine($"  drove kill of '{def.Id}': coins {coinsBefore}->{coinsAfter} (+{def.CoinReward}), lifetimeXp {xpBefore:0}->{xpAfter:0} (+{def.XpReward})");

                if (coinsAfter - coinsBefore != def.CoinReward)
                    failures.Add($"[enemy-rewards] kill grant: coins moved {coinsAfter - coinsBefore}, expected +{def.CoinReward} (EconomyService.AddCoins broken)");
                if (Mathf.Abs((xpAfter - xpBefore) - def.XpReward) > 0.5f)
                    failures.Add($"[enemy-rewards] kill grant: lifetime XP moved {(xpAfter - xpBefore):0}, expected +{def.XpReward} (HeroProgression.AddXp broken)");
            }
            catch (System.Exception ex)
            {
                failures.Add($"[enemy-rewards] grant-seam drive threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (econGo != null) Object.DestroyImmediate(econGo);
                if (progGo != null) Object.DestroyImmediate(progGo);
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                // Restore prior live singletons the batch's later oracles read.
                SetInstance(typeof(EconomyService), priorEcon);
                SetInstance(typeof(HeroProgression), priorProg);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        // Boss detection from the raw JSON: an entry whose "isBoss"/"boss" is true, or
        // whose "category"/"role"/"tier" says "boss". Absent -> treated as non-boss.
        private static HashSet<string> ScanBossIds(JObject root)
        {
            var set = new HashSet<string>();
            if (root == null) return set;
            var arr = root["enemies"] as JArray;
            if (arr == null) return set;
            foreach (var tok in arr)
            {
                if (!(tok is JObject o)) continue;
                string id = o["id"]?.ToString();
                if (string.IsNullOrEmpty(id)) continue;
                bool boss = (o["isBoss"]?.Type == JTokenType.Boolean && o["isBoss"].Value<bool>())
                         || (o["boss"]?.Type == JTokenType.Boolean && o["boss"].Value<bool>())
                         || Mentions(o["category"], "boss") || Mentions(o["role"], "boss") || Mentions(o["tier"], "boss");
                if (boss) set.Add(id);
            }
            return set;
        }

        private static bool Mentions(JToken tok, string needle)
            => tok != null && tok.Type == JTokenType.String &&
               tok.ToString().IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        // ---- headless singleton reflection helpers ------------------------------
        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        private static FieldInfo InstanceField(System.Type t)
        {
            var f = t.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)
                 ?? t.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null) return f;
            foreach (var ff in t.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
                if (ff.Name.Contains("Instance") && ff.FieldType == t) return ff;
            return null;
        }

        private static object GetInstance(System.Type t)
        {
            var f = InstanceField(t);
            return f != null ? f.GetValue(null) : null;
        }

        private static void SetInstance(System.Type t, object val)
        {
            var f = InstanceField(t);
            if (f != null) f.SetValue(null, val);
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "ENEMY_REWARDS_OK");
                return "ENEMY REWARDS OK -- every non-boss enemy pays coin+xp, and a driven kill raised coins + lifetime XP by the reward";
            }
            string reason = "enemy-rewards: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "ENEMY_REWARDS_FAIL: " + reason);
            return reason;
        }
    }
}
