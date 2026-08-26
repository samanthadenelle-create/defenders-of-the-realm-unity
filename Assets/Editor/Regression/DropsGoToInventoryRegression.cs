// =============================================================================
// DropsGoToInventoryRegression [drops-to-inventory]  -- WO-1214
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins the four owner rulings behind the P0 "a dropped shield PERMANENTLY DISARMS
// the Mage" (owner felt-test, Seeker build 2026.08.26.341419):
//
//   1. "any drop should just go to inventory"                      -> Case 1
//   2. "if cannot equip (shield for mage) then dont allow equip
//       but they can sell"                                          -> Cases 2 + 3
//   3. enforce class + level AT THE EQUIP SEAM, fail closed, log it -> Case 2
//   4. the armed-hero invariant must FAIL CLOSED                    -> Cases 3 + 4
//
// THE CHAIN THIS PREVENTS (every step was working as written; the DESIGN was the
// defect): a shield drops -> its `job` is "any" so the class gate passes for a
// Mage -> it auto-equips to the off hand -> hand-slot enforcement removes the 2H
// staff (a 2H cannot coexist with an off-hand) -> the 1H fall-back asks the
// catalog for a replacement main hand -> the catalog has none -> the Mage holds a
// shield and NOTHING ELSE, with no in-game way to recover.
//
// ── A CORRECTION TO THE TICKET, READ FROM THE DATA (do not re-derive it wrong) ──
// The work order states "there is ZERO one-handed mage weapon in the game". That
// is true of the WORDS in the rows (all eight mage weapons are staffs) and FALSE
// of what the CODE reads. WeaponDef.IsTwoHanded is driven by the `hand` field and
// an absent `hand` DEFAULTS TO 1h, so mage_oak / mage_arcane / mage_void /
// aegis_aetherstaff (which carry no `hand`) all report IsOneHandedMain == true.
// Only the four tripo_staff_* rows are actually "2h".
//
// The consequence, and why this suite is DATA-DERIVED rather than hardcoded to
// "mage": the disarm is reachable exactly where a class has a 2H it can hold and
// NO 1H at that level. For the Mage that is levels 1-2 (the only mage rows at
// req.level 1 are tripo_staff_a / tripo_staff_d, both 2h). Asserting "the Mage is
// always vulnerable" would be wrong at level 3+, and asserting "the Ranger is
// safe" would be an assumption -- so both are COMPUTED from the catalog here.
//
// Cases:
//   1 [drop-to-inventory]  Neither loot roll equips any more. BattleArena's
//                          TryGrantArenaGear and EnemyOutpost's TryGrantGearDrop
//                          contain ZERO Equip*ById calls and both deposit through
//                          VillageInventory. Source-lint, because the thing that
//                          regresses is a RESTORED CALL, which a lint catches
//                          exactly and a scene-free runtime probe cannot reach
//                          (both methods are private, need a Player-tagged hero,
//                          and roll a random chance).
//   2 [seam-class-gate]    The equip seam REFUSES a class-ineligible weapon, does
//                          not mutate the slot, publishes a player-facing sentence
//                          (LastEquipRefusal, ASCII, non-empty) and LOGS the
//                          refusal via FlowTrace.Warn.
//   3 [armed-hero-closed]  For EVERY playable class: equipping a job:"any" shield
//                          on a fresh loadout never leaves the hero unarmed, and
//                          where the class has NO 1H the off-hand equip is REFUSED
//                          outright (the off hand stays EMPTY and the 2H is kept).
//   4 [ranger-1h]          The Ranger's one-handed status is REPORTED from the
//                          catalog, not assumed, and the shield case is asserted
//                          against whichever answer the data gives.
//   5 [wo860-intact]       Auto-upgrade-on-level-up still exists: GearLoadout still
//                          subscribes OnLevelUp -> Refresh and Refresh still routes
//                          the main hand through ResolveAutoBestMainHand. Ruling 1
//                          is scoped to DROPS and must never disable this.
//
// Markers: DROPS_TO_INVENTORY_OK / DROPS_TO_INVENTORY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.DropsGoToInventoryRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class DropsGoToInventoryRegression
    {
        // The brace characters, written as UNICODE ESCAPES on purpose. This file lints C# source,
        // so it has to name '{' and '}' - and CLAUDE.md section 1's mandatory quality gate counts
        // RAW brace characters per file. A literal brace inside a regex or a char literal would
        // skew that count and make this oracle look like a corrupt file. Escapes keep the file's
        // own brace ledger honest without changing a single matched character.
        private const string OpenBraceStr = "\u007B";
        private const char   OpenBraceCh  = '\u007B';
        private const char   CloseBraceCh = '\u007D';

        private const string ArenaSrc    = "Assets/_Modules/Village/Arena/BattleArena.cs";
        private const string OutpostSrc  = "Assets/_Modules/Village/World/Camps/EnemyOutpost.cs";
        private const string LoadoutSrc  = "Assets/_Modules/Village/Hero/GearLoadout.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DROPS_TO_INVENTORY_OK - " + reason);
            else Debug.LogError("DROPS_TO_INVENTORY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                GearCatalog.Reload();
                Case(failures, "drop-to-inventory", () => Case1_DropsDoNotEquip(failures, notes));
                Case(failures, "seam-class-gate",   () => Case2_SeamRefusesIneligible(failures, notes));
                Case(failures, "armed-hero-closed", () => Case3_ShieldNeverDisarms(failures, notes));
                Case(failures, "ranger-1h",         () => Case4_RangerFromCatalog(failures, notes));
                Case(failures, "wo860-intact",      () => Case5_LevelUpAutoUpgradeIntact(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DROPS TO INVENTORY OK - neither loot roll equips, the equip seam refuses " +
                         "class/level-ineligible gear in words and logs it, no class can be disarmed by a " +
                         "job:\"any\" shield, and level-up auto-upgrade (WO-860) is intact" + noteStr;
                return true;
            }
            reason = "drops-to-inventory FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - RULING 1: a drop goes to the INVENTORY, never to the hands.
        // =====================================================================
        private static void Case1_DropsDoNotEquip(List<string> failures, List<string> notes)
        {
            CheckLootMethodDeposits(failures, notes, ArenaSrc,   "TryGrantArenaGear");
            CheckLootMethodDeposits(failures, notes, OutpostSrc, "TryGrantGearDrop");
        }

        /// <summary>
        /// Reads the named method's body out of <paramref name="path"/> (comments stripped, so
        /// prose can never satisfy the lint) and asserts it EQUIPS NOTHING and DEPOSITS SOMETHING.
        /// </summary>
        private static void CheckLootMethodDeposits(List<string> failures, List<string> notes,
                                                    string path, string method)
        {
            string src = ReadSource(path, failures);
            if (src == null) return;

            string body = ExtractMethodBody(StripComments(src), method);
            if (body == null)
            {
                failures.Add("[drop-to-inventory] could not locate the body of " + method + " in " + path +
                             " - the loot roll was renamed or moved without updating this oracle, so the " +
                             "Ruling 1 guarantee is UNVERIFIED (which is not the same as safe)");
                return;
            }

            var equipCalls = Regex.Matches(body, @"Equip(Weapon|Armor|OffHand)ById\s*\(");
            if (equipCalls.Count > 0)
            {
                failures.Add("[drop-to-inventory] " + method + " in " + path + " still makes " + equipCalls.Count +
                             " Equip*ById call(s). WO-1214 Ruling 1 (\"any drop should just go to inventory\"): a " +
                             "loot roll must DEPOSIT the item, never equip it - equipping is how a job:\"any\" " +
                             "shield displaced a Mage's two-handed staff and left her permanently unarmed");
            }

            bool deposits = Regex.IsMatch(body, @"VillageInventory") ||
                            Regex.IsMatch(body, @"GrantToInventory\s*\(");
            if (!deposits)
            {
                failures.Add("[drop-to-inventory] " + method + " in " + path + " no longer reaches the inventory " +
                             "ledger at all - the drop is granted NOWHERE, which is a silent loss of the " +
                             "player's prize rather than a fix");
            }

            if (equipCalls.Count == 0 && deposits)
                notes.Add(method + " deposits and does not equip");
        }

        // =====================================================================
        //  CASE 2 - RULING 3: the SEAM enforces, fails closed, and says why.
        // =====================================================================
        private static void Case2_SeamRefusesIneligible(List<string> failures, List<string> notes)
        {
            // A weapon that is class-locked to someone OTHER than the knight, so the refusal is
            // unambiguous. Picked from the catalog, never hardcoded.
            WeaponDef foreign = null;
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || w.IsOffHandItem) continue;
                if (string.IsNullOrEmpty(w.job)) continue;
                if (w.job.Equals("any", StringComparison.OrdinalIgnoreCase)) continue;
                if (w.job.Equals("knight", StringComparison.OrdinalIgnoreCase)) continue;
                foreign = w;
                break;
            }
            if (foreign == null)
            {
                failures.Add("[seam-class-gate] weapons.json has no class-locked non-knight main-hand row, so this " +
                             "case cannot prove the class gate at all - it would pass vacuously");
                return;
            }

            var p = RunProbe("knight", equipWeaponId: foreign.id);
            if (p.Failure != null)
            {
                failures.Add("[seam-class-gate] the probe could not run: " + p.Failure);
                return;
            }

            if (p.MainHand != null && string.Equals(p.MainHand.id, foreign.id, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("[seam-class-gate] GearLoadout.EquipWeaponById('" + foreign.id + "', job='" +
                             (foreign.job ?? "<null>") + "') EQUIPPED it on a KNIGHT. The equip seam is not " +
                             "enforcing the class gate, so every non-UI caller (loot grants, story grants, " +
                             "AutoPilot) can still put anything in anyone's hands");
            }

            if (string.IsNullOrEmpty(p.Refusal))
            {
                failures.Add("[seam-class-gate] the seam left LastEquipRefusal EMPTY after refusing '" + foreign.id +
                             "'. WO-1214 Ruling 2: a refusal the player cannot READ is the same silent-failure " +
                             "class this ticket exists to end - never a greyed control, never colour alone");
            }
            else
            {
                if (!IsAscii(p.Refusal))
                    failures.Add("[seam-class-gate] the refusal sentence is not ASCII: \"" + p.Refusal + "\"");
                notes.Add("refusal reads: \"" + Condense(p.Refusal) + "\"");
            }

            // The refusal must also be LOGGED, not only shown (Ruling 3 / CLAUDE.md section 12).
            string loadout = ReadSource(LoadoutSrc, failures);
            if (loadout != null)
            {
                string refuse = ExtractMethodBody(StripComments(loadout), "RefuseEquip");
                if (refuse == null)
                    failures.Add("[seam-class-gate] GearLoadout.RefuseEquip is gone - the single place the seam " +
                                 "records + logs + announces a refusal");
                else if (!refuse.Contains("FlowTrace.Warn"))
                    failures.Add("[seam-class-gate] GearLoadout.RefuseEquip no longer calls FlowTrace.Warn - a " +
                                 "refusal that is shown but not captured leaves the next regression with zero " +
                                 "evidence (instrumentation is PERMANENT, CLAUDE.md section 12)");
            }
        }

        // =====================================================================
        //  CASE 3 - RULING 4: no class can be disarmed by a job:"any" shield.
        // =====================================================================
        private static void Case3_ShieldNeverDisarms(List<string> failures, List<string> notes)
        {
            var classes = PlayableHeroes.AllKnownJobKeys();
            if (classes == null || classes.Count == 0)
            {
                failures.Add("[armed-hero-closed] PlayableHeroes.AllKnownJobKeys() is EMPTY - this case would sweep " +
                             "zero classes and pass vacuously");
                return;
            }

            const int ProbeLevel = 1;   // a bare GearLoadout carries no HeroProgression
            WeaponDef shield = FindAnyJobShield(ProbeLevel);
            if (shield == null)
            {
                failures.Add("[armed-hero-closed] no job:\"any\" off-hand row at level " + ProbeLevel +
                             " in weapons.json - the exact item class the owner's drop belonged to. Without one " +
                             "this case cannot reproduce the defect and must not report a pass");
                return;
            }

            foreach (var job in classes)
            {
                bool hasOneHand = GearCatalog.HasOneHandedMainHand(job, ProbeLevel);
                var p = RunProbe(job, equipOffHandId: shield.id);
                if (p.Failure != null)
                {
                    failures.Add("[armed-hero-closed] '" + job + "': the probe could not run: " + p.Failure);
                    continue;
                }

                // THE INVARIANT, for every class, either way: a shield equip never disarms.
                if (p.MainHand == null)
                {
                    failures.Add("[armed-hero-closed] '" + job + "': after EquipOffHandById('" + shield.id +
                                 "') the MAIN HAND IS EMPTY (off-hand='" + (p.OffHand?.id ?? "<null>") + "'). This " +
                                 "is the reported P0 verbatim - the hero holds a shield and nothing else, with no " +
                                 "in-game way to recover");
                    continue;
                }
                if (p.MainHand.IsOffHandItem)
                {
                    failures.Add("[armed-hero-closed] '" + job + "': the MAIN hand holds off-hand item '" +
                                 p.MainHand.id + "' - a shield can never be the main hand");
                    continue;
                }

                if (!hasOneHand)
                {
                    // Ruling 4: with no 1H to fall back to, the off-hand equip must be REFUSED,
                    // not degraded. The 2H stays; the off hand stays EMPTY.
                    if (p.OffHand != null)
                    {
                        failures.Add("[armed-hero-closed] '" + job + "': the catalog serves this class NO one-handed " +
                                     "main-hand at level " + ProbeLevel + ", yet off-hand '" + p.OffHand.id +
                                     "' was accepted (main='" + p.MainHand.id + "'). Ruling 4 requires the equip to " +
                                     "be REFUSED so the two-hander can never be evicted with nothing to replace it");
                    }
                    if (string.IsNullOrEmpty(p.Refusal))
                    {
                        failures.Add("[armed-hero-closed] '" + job + "': the shield equip produced no player-facing " +
                                     "refusal, so the player is given no reason at all for an action that did nothing");
                    }
                    notes.Add(job + ": no 1H at L" + ProbeLevel + " -> shield REFUSED, main='" + p.MainHand.id + "'");
                }
                else
                {
                    notes.Add(job + ": 1H exists at L" + ProbeLevel + " -> main='" + p.MainHand.id +
                              "' off='" + (p.OffHand?.id ?? "<null>") + "'");
                }
            }
        }

        // =====================================================================
        //  CASE 4 - the RANGER, answered by the catalog rather than assumed.
        // =====================================================================
        private static void Case4_RangerFromCatalog(List<string> failures, List<string> notes)
        {
            const string Job = "ranger";
            const int ProbeLevel = 1;

            var oneHanded = new List<string>();
            var twoHanded = new List<string>();
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || w.IsOffHandItem) continue;
                if (!GearCatalog.CanEquipWeapon(w, Job, ProbeLevel, out _)) continue;
                if (w.IsTwoHanded) twoHanded.Add(w.id); else oneHanded.Add(w.id);
            }

            notes.Add("ranger L" + ProbeLevel + ": " + oneHanded.Count + " one-handed / " + twoHanded.Count +
                      " two-handed main-hand row(s)" +
                      (oneHanded.Count > 0 ? " (e.g. '" + oneHanded[0] + "')" : ""));

            if (oneHanded.Count == 0 && twoHanded.Count == 0)
            {
                failures.Add("[ranger-1h] the catalog serves the RANGER no main-hand weapon at all at level " +
                             ProbeLevel + " - the class cannot be armed, which fails the armed-hero invariant " +
                             "before any shield is involved");
                return;
            }

            WeaponDef shield = FindAnyJobShield(ProbeLevel);
            if (shield == null) return;   // Case 3 already reports the missing shield row

            var p = RunProbe(Job, equipOffHandId: shield.id);
            if (p.Failure != null) { failures.Add("[ranger-1h] the probe could not run: " + p.Failure); return; }

            if (p.MainHand == null)
                failures.Add("[ranger-1h] the RANGER was left with an EMPTY main hand after equipping shield '" +
                             shield.id + "' (off-hand='" + (p.OffHand?.id ?? "<null>") + "'). The ticket warned " +
                             "against assuming the Ranger is safe because its primary is a melee sweep - it is not " +
                             "safe by assumption, it is safe only if the catalog serves it a one-handed row");

            if (oneHanded.Count == 0 && p.OffHand != null)
                failures.Add("[ranger-1h] the RANGER has no one-handed main-hand at level " + ProbeLevel +
                             " yet accepted off-hand '" + p.OffHand.id + "' - Ruling 4 must refuse it");
        }

        // =====================================================================
        //  CASE 5 - WO-860 auto-upgrade-on-level-up is NOT collateral damage.
        // =====================================================================
        private static void Case5_LevelUpAutoUpgradeIntact(List<string> failures, List<string> notes)
        {
            string src = ReadSource(LoadoutSrc, failures);
            if (src == null) return;
            string code = StripComments(src);

            if (!Regex.IsMatch(code, @"OnLevelUp\s*\+="))
                failures.Add("[wo860-intact] GearLoadout no longer subscribes to HeroProgression.OnLevelUp - " +
                             "levelling stops re-resolving gear. WO-1214 Ruling 1 is scoped to DROPS and must " +
                             "never disable auto-equip generally (WO-860)");

            if (!Regex.IsMatch(code, @"private\s+void\s+OnLevelUp\s*\(\s*int[^)]*\)\s*=>\s*Refresh\s*\(\s*\)"))
                failures.Add("[wo860-intact] GearLoadout.OnLevelUp no longer calls Refresh() - a level-up would " +
                             "not re-evaluate the loadout at all");

            string refresh = ExtractMethodBody(code, "Refresh");
            if (refresh == null)
                failures.Add("[wo860-intact] GearLoadout.Refresh is gone - the auto-equip entry point");
            else if (!refresh.Contains("ResolveAutoBestMainHand"))
                failures.Add("[wo860-intact] GearLoadout.Refresh no longer routes the main hand through " +
                             "ResolveAutoBestMainHand - the ownership-gated auto-best pick (WO-860's candidate-set " +
                             "fix) has been removed along with the auto-equip verb it was guarding");
            else
                notes.Add("WO-860 auto-upgrade path intact (OnLevelUp -> Refresh -> ResolveAutoBestMainHand)");
        }

        // =====================================================================
        //  THE HEADLESS PROBE - drives the REAL GearLoadout, no scene.
        //  Mirrors ArmedHeroInvariantRegression.RunProbe: snapshot + clear this
        //  class's equip PlayerPrefs (the gear half of ResetToNewGame), build a
        //  HideAndDontSave throwaway, bind the class, act, read back, restore.
        // =====================================================================
        private sealed class Probe
        {
            public WeaponDef MainHand;
            public WeaponDef OffHand;
            public string Refusal;
            public string Failure;
        }

        private static Probe RunProbe(string job, string equipWeaponId = null, string equipOffHandId = null)
        {
            var p = new Probe();

            var keys = new List<string>();
            string classKey = (job ?? string.Empty).ToLowerInvariant();
            foreach (var prefix in EquipPrefKeys.AllSlotPrefixes) keys.Add(prefix + classKey);

            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var k in keys)
                if (PlayerPrefs.HasKey(k)) snapshot[k] = PlayerPrefs.GetString(k, string.Empty);

            GameObject go = null;
            try
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);

                go = new GameObject("DropProbe_" + classKey);
                go.hideFlags = HideFlags.HideAndDontSave;

                var loadout = go.AddComponent<GearLoadout>();
                if (loadout == null) { p.Failure = "AddComponent<GearLoadout>() returned null"; return p; }

                loadout.BindOwnerClass(job);   // the real starter/auto-best/persisted/enforce chain

                if (!string.IsNullOrEmpty(equipWeaponId)) loadout.EquipWeaponById(equipWeaponId);
                if (!string.IsNullOrEmpty(equipOffHandId)) loadout.EquipOffHandById(equipOffHandId);

                p.MainHand = loadout.EquippedWeapon;
                p.OffHand = loadout.EquippedOffHand;
                p.Refusal = loadout.LastEquipRefusal;
            }
            catch (Exception ex)
            {
                p.Failure = "the probe THREW " + ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
                foreach (var kv in snapshot) PlayerPrefs.SetString(kv.Key, kv.Value);
                PlayerPrefs.Save();
            }
            return p;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>The first job:"any" OFF-HAND row a wearer at <paramref name="level"/> may take -
        /// the exact item class the owner's drop belonged to (19 such rows today).</summary>
        private static WeaponDef FindAnyJobShield(int level)
        {
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || !w.IsOffHandItem) continue;
                if (string.IsNullOrEmpty(w.job) || !w.job.Equals("any", StringComparison.OrdinalIgnoreCase)) continue;
                if (!GearCatalog.MeetsReq(w.req, level)) continue;
                return w;
            }
            return null;
        }

        private static bool IsAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (char c in s) if (c > 126 || (c < 32 && c != '\n' && c != '\r' && c != '\t')) return false;
            return true;
        }

        /// <summary>
        /// The brace-balanced body of the first method whose signature line contains
        /// <paramref name="method"/> followed by '(' , or null. Deliberately simple: the lints
        /// above only ask "does this call still appear inside this method", and a full parser
        /// would be a second thing to keep correct.
        /// </summary>
        private static string ExtractMethodBody(string code, string method)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(method)) return null;
            var m = Regex.Match(code, @"\b" + Regex.Escape(method) + @"\s*\([^;" + OpenBraceStr +
                                      @"]*\)\s*" + Regex.Escape(OpenBraceStr));
            if (!m.Success) return null;

            int i = code.IndexOf(OpenBraceCh, m.Index + m.Length - 1);
            if (i < 0) return null;

            int depth = 0;
            for (int j = i; j < code.Length; j++)
            {
                if (code[j] == OpenBraceCh) depth++;
                else if (code[j] == CloseBraceCh)
                {
                    depth--;
                    if (depth == 0) return code.Substring(i, j - i + 1);
                }
            }
            return null;
        }

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[source] " + path + " not found - the file moved without updating this oracle");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and /* */ comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        private static string Condense(string s)
        {
            string one = Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();
            return one.Length > 160 ? one.Substring(0, 157) + "..." : one;
        }
    }
}
