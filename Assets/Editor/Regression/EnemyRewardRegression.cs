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

            // ---- WO-1103 -- base + bounded variance + kill-count scaling + field toast ----
            CheckVarianceData(catalog, root, failures, log);           // (3) data: rewardVariance seeded
            CheckRollAuthority(failures, log);                          // (4) EnemyDef.RollReward band + determinism
            if (payingDef != null)
                DriveRolledKillsSeam(payingDef, 4, failures, log);      // (5) N kills sum ~ N x base within band
            CheckFieldToastSourceLint(failures, log);                   // (6) Enemy.Die out-of-arena toast call site

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // =====================================================================
        //  WO-1103 (3) DATA -- every enemy row carries a rewardVariance token in
        //  [0, 0.9], and at least one row is > 0 (variance is LIVE, not vestigial).
        //  Presence is asserted on the RAW JSON (JObject) so reverting the json
        //  seed goes RED even though the typed default (0) would parse fine.
        // =====================================================================
        private static void CheckVarianceData(EnemyCatalog catalog, JObject root,
                                              List<string> failures, StringBuilder log)
        {
            if (root == null) { failures.Add("[enemy-rewards] WO-1103 variance: raw JSON unavailable (JObject parse failed)"); return; }
            var arr = root["enemies"] as JArray;
            if (arr == null) { failures.Add("[enemy-rewards] WO-1103 variance: no 'enemies' array in raw JSON"); return; }

            int present = 0, live = 0, rows = 0;
            foreach (var tok in arr)
            {
                if (!(tok is JObject o)) continue;
                string id = o["id"]?.ToString();
                if (string.IsNullOrEmpty(id) || id.Contains(" ")) continue;   // schema-doc placeholder
                rows++;
                var v = o["rewardVariance"];
                if (v == null || (v.Type != JTokenType.Float && v.Type != JTokenType.Integer))
                {
                    failures.Add($"[enemy-rewards] WO-1103: enemy '{id}' has NO rewardVariance token (seed reverted -- kill rewards go deterministic)");
                    continue;
                }
                present++;
                float f = v.Value<float>();
                if (f < 0f || f > 0.9f)
                    failures.Add($"[enemy-rewards] WO-1103: enemy '{id}' rewardVariance={f} outside [0, 0.9] (RollReward clamps -- data must not rely on the clamp)");
                if (f > 0f) live++;
            }
            if (rows > 0 && live == 0)
                failures.Add("[enemy-rewards] WO-1103: every rewardVariance is 0 -- variance is authored but dead (owner seeded 0.15/0.10)");
            log.AppendLine($"  WO-1103 variance data: {present}/{rows} rows carry rewardVariance ({live} live > 0)");
        }

        // =====================================================================
        //  WO-1103 (4) the ONE roll authority: EnemyDef.RollReward.
        //   - v=0   -> exact base (deterministic legacy path)
        //   - base<=0 -> 0 (variance never mints a reward from nothing)
        //   - v>0   -> every roll inside [base*(1-v), base*(1+v)] (rounded, floor 1)
        //              AND at least 2 distinct values over 300 rolls (variance LIVE --
        //              a revert to "return base" goes RED here).
        // =====================================================================
        private static void CheckRollAuthority(List<string> failures, StringBuilder log)
        {
            if (EnemyDef.RollReward(24, 0f) != 24)
                failures.Add("[enemy-rewards] WO-1103 roll: RollReward(24, v=0) != 24 (deterministic path broken)");
            if (EnemyDef.RollReward(0, 0.15f) != 0 || EnemyDef.RollReward(-3, 0.15f) != 0)
                failures.Add("[enemy-rewards] WO-1103 roll: RollReward(<=0 base) minted a reward (must stay 0)");

            const int baseVal = 24; const float v = 0.15f; const int rolls = 300;
            int lo = Mathf.Max(1, Mathf.FloorToInt(baseVal * (1f - v)));  // 20
            int hi = Mathf.CeilToInt(baseVal * (1f + v));                 // 28
            var seen = new HashSet<int>();
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < rolls; i++)
            {
                int r = EnemyDef.RollReward(baseVal, v);
                seen.Add(r);
                if (r < min) min = r;
                if (r > max) max = r;
                if (r < lo || r > hi)
                    { failures.Add($"[enemy-rewards] WO-1103 roll: RollReward({baseVal}, {v}) produced {r} outside band [{lo},{hi}] (variance not bounded)"); break; }
            }
            if (seen.Count < 2)
                failures.Add($"[enemy-rewards] WO-1103 roll: 300 rolls of RollReward({baseVal}, {v}) produced ONE value ({min}) -- variance is dead (revert detected)");
            log.AppendLine($"  WO-1103 roll authority: {rolls} rolls of base {baseVal} v {v} -> [{min},{max}] within [{lo},{hi}], {seen.Count} distinct");
        }

        // =====================================================================
        //  WO-1103 (5) N-KILLS SEAM -- drive the exact rolled grants Enemy.Die
        //  performs, N times, on the REAL singletons; assert the cumulative coin +
        //  XP deltas land inside the N x base variance band and above the 1-kill
        //  ceiling (so N kills provably pay more than 1). Uses the def's own
        //  rewardVariance (data-driven, matches the live grant path). Red on
        //  revert: unrolled/uncounted grants fall outside the asserted band.
        // =====================================================================
        private static void DriveRolledKillsSeam(EnemyDef def, int kills,
                                                 List<string> failures, StringBuilder log)
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
                gssGo = new GameObject("GSS (WO-1103 kills oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  NOTE: GSS state seam not reflectable -- WO-1103 kills seam skipped");
                    return;
                }

                econGo = new GameObject("EconomyService (WO-1103 kills oracle)");
                var econ = econGo.AddComponent<EconomyService>();
                SetInstance(typeof(EconomyService), econ);

                progGo = new GameObject("HeroProgression (WO-1103 kills oracle)");
                var prog = progGo.AddComponent<HeroProgression>();
                SetInstance(typeof(HeroProgression), prog);

                float v = Mathf.Clamp(def.RewardVariance, 0f, 0.9f);
                int coinBase = def.CoinReward > 0 ? def.CoinReward : Mathf.Max(4, Mathf.RoundToInt(def.XpReward * 0.4f));

                int coinsBefore = econ.Coins;
                float xpBefore = prog.LifetimeXp;

                // The exact rolled grant pair Enemy.Die performs, driven N times.
                for (int i = 0; i < kills; i++)
                {
                    EconomyService.Instance.AddCoins(EnemyDef.RollReward(coinBase, v));
                    HeroProgression.Instance.AddXp(EnemyDef.RollReward(def.XpReward, v));
                }

                int coinDelta = econ.Coins - coinsBefore;
                float xpDelta = prog.LifetimeXp - xpBefore;

                int coinLo = kills * Mathf.Max(1, Mathf.FloorToInt(coinBase * (1f - v)));
                int coinHi = kills * Mathf.CeilToInt(coinBase * (1f + v));
                int xpLo   = kills * Mathf.Max(1, Mathf.FloorToInt(def.XpReward * (1f - v)));
                int xpHi   = kills * Mathf.CeilToInt(def.XpReward * (1f + v));
                int oneKillXpCeil = Mathf.CeilToInt(def.XpReward * (1f + v));

                log.AppendLine($"  WO-1103 kills seam: {kills} rolled kills of '{def.Id}' (v={v:0.00}) -> coins +{coinDelta} in [{coinLo},{coinHi}], xp +{xpDelta:0} in [{xpLo},{xpHi}]");

                if (coinDelta < coinLo || coinDelta > coinHi)
                    failures.Add($"[enemy-rewards] WO-1103 kills: {kills} kills paid {coinDelta} coins, outside band [{coinLo},{coinHi}]");
                if (xpDelta < xpLo - 0.5f || xpDelta > xpHi + 0.5f)
                    failures.Add($"[enemy-rewards] WO-1103 kills: {kills} kills paid {xpDelta:0} XP, outside band [{xpLo},{xpHi}]");
                if (xpDelta <= oneKillXpCeil + 0.5f)
                    failures.Add($"[enemy-rewards] WO-1103 kills: {kills} kills paid {xpDelta:0} XP, NOT above the 1-kill ceiling {oneKillXpCeil} (kill count does not scale the payout)");
            }
            catch (System.Exception ex)
            {
                failures.Add($"[enemy-rewards] WO-1103 kills seam threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (econGo != null) Object.DestroyImmediate(econGo);
                if (progGo != null) Object.DestroyImmediate(progGo);
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                SetInstance(typeof(EconomyService), priorEcon);
                SetInstance(typeof(HeroProgression), priorProg);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        // =====================================================================
        //  WO-1103 (6) FIELD-KILL TOAST -- source-lint (the WO-sanctioned probe):
        //  Enemy.Die's out-of-arena branch must call the ONE existing label system
        //  (DamageNumberSpawner.SpawnLabel via ShowFieldKillReward) and carry the
        //  owner's 'Pack bounty' wording for a leader-carried pack payout. Red on
        //  revert: deleting the call site or the wording fails by name.
        // =====================================================================
        private static void CheckFieldToastSourceLint(List<string> failures, StringBuilder log)
        {
            const string enemyPath = "Assets/_Modules/Village/Enemies/Enemy.cs";
            string src;
            try { src = System.IO.File.ReadAllText(enemyPath); }
            catch (System.Exception ex)
            {
                failures.Add($"[enemy-rewards] WO-1103 toast lint: could not read {enemyPath}: {ex.Message}");
                return;
            }
            if (!src.Contains("ShowFieldKillReward("))
                failures.Add("[enemy-rewards] WO-1103 toast: Enemy.cs has no ShowFieldKillReward call (field kills notify nothing -- B2 regressed)");
            if (!src.Contains("DamageNumberSpawner.SpawnLabel("))
                failures.Add("[enemy-rewards] WO-1103 toast: Enemy.cs does not route the field-kill label through DamageNumberSpawner.SpawnLabel (the ONE label system)");
            if (!src.Contains("Pack bounty"))
                failures.Add("[enemy-rewards] WO-1103 toast: Enemy.cs lost the 'Pack bounty' wording (leader-carry pack payout reads as a single-kill overpay)");
            if (!src.Contains("ReportArenaKillGrant("))
                failures.Add("[enemy-rewards] WO-1103 toast: Enemy.cs no longer banks arena kills via ReportArenaKillGrant (victory SUMMARY under-reports again)");
            log.AppendLine("  WO-1103 toast lint: SpawnLabel + 'Pack bounty' + ReportArenaKillGrant call sites present in Enemy.cs");
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
