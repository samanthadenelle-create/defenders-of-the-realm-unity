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
//   6 [eligible-equips]  THE GOOD PATH. For EVERY playable class, the first weapon
//                          and the first armor the catalog itself says the class may
//                          hold at the probe level DO equip through the real seam,
//                          with LastEquipRefusal left null. Cases 1-5 are all refusal
//                          oracles, and a suite made only of those certifies a gate
//                          that refuses EVERYTHING just as happily as a correct one -
//                          this repo has already shipped that exact failure once.
//   7 [armor-weight-gate]  Every class x mismatched-authored-weight armor pair is
//                          REFUSED, in ASCII words that name selling. Reports the open
//                          owner item `blink_armor_dragonic` (job:"any" + weight:heavy,
//                          so every job-only filter offers it to a light-armour Mage)
//                          explicitly. Its AUTHORED weight is an owner ruling and is
//                          never touched here - only the GATE's answer is asserted.
//   8 [held-and-sellable]  Ruling 2's second half. (a) PartyShopVM.BuildSellGear stays
//                          eligibility-BLIND, so a refused drop is still worth coins -
//                          a sell list filtered by what the hero can wear would turn
//                          every refusal into permanent dead weight. (b) the one
//                          remaining shipping non-UI path that equips onto the PLAYER
//                          hero (CompanionGearSetup.Apply, a scripted story grant -
//                          ResolveLoadout resolves the Player-tagged hero despite the
//                          file name) BANKS before it equips, so a fail-closed refusal
//                          can never lose the gift.
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
        private const string PartyShopSrc  = "Assets/_Modules/Village/Hero/PartyShopVM.cs";
        private const string CompanionSrc  = "Assets/_Modules/Village/NPCs/CompanionGearSetup.cs";
        private const string PanelSrc      = "Assets/_Modules/Village/Hero/EquipmentPanel.cs";

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
                Case(failures, "eligible-equips",   () => Case6_EligibleGearStillEquips(failures, notes));
                Case(failures, "armor-weight-gate", () => Case7_ArmorWeightGate(failures, notes));
                Case(failures, "held-and-sellable", () => Case8_IneligibleStaysSellable(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DROPS TO INVENTORY OK - neither loot roll equips, the equip seam refuses " +
                         "class/level/weight-ineligible gear in words and logs it, no class can be disarmed by a " +
                         "job:\"any\" shield, ELIGIBLE gear still equips normally for every class, a refused item " +
                         "stays sellable, and level-up auto-upgrade (WO-860) is intact" + noteStr;
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
        //  CASE 6 - THE GOOD PATH. A class-ELIGIBLE item still equips normally.
        // ---------------------------------------------------------------------
        //  Cases 1-5 are all refusal/absence oracles. A suite that only asserts
        //  what must NOT happen certifies a gate that refuses EVERYTHING just as
        //  happily as one that refuses correctly - and this repo has already
        //  shipped exactly that failure (a pin guard that aborted every good run
        //  while exiting 0). So: for EVERY playable class, take the first weapon
        //  and the first armor row the catalog itself says the class may hold at
        //  the probe level, push them through the REAL seam, and assert they LAND
        //  with LastEquipRefusal left null.
        // =====================================================================
        private static void Case6_EligibleGearStillEquips(List<string> failures, List<string> notes)
        {
            var classes = PlayableHeroes.AllKnownJobKeys();
            if (classes == null || classes.Count == 0)
            {
                failures.Add("[eligible-equips] PlayableHeroes.AllKnownJobKeys() is EMPTY - this case would sweep " +
                             "zero classes and pass vacuously");
                return;
            }

            const int ProbeLevel = 1;   // a bare GearLoadout carries no HeroProgression
            int weaponsProven = 0, armorProven = 0;

            foreach (var job in classes)
            {
                // -- the main hand --------------------------------------------------------
                WeaponDef legal = null;
                foreach (var w in GearCatalog.AllWeapons())
                {
                    if (w == null || w.IsOffHandItem) continue;
                    if (!GearCatalog.CanEquipWeapon(w, job, ProbeLevel, out _)) continue;
                    legal = w;
                    break;
                }
                if (legal == null)
                {
                    failures.Add("[eligible-equips] '" + job + "': the catalog serves this class NO main-hand weapon " +
                                 "at level " + ProbeLevel + ", so the good path cannot be proven for it at all - the " +
                                 "class is unarmable before any gate is involved");
                }
                else
                {
                    var p = RunProbe(job, equipWeaponId: legal.id);
                    if (p.Failure != null)
                    {
                        failures.Add("[eligible-equips] '" + job + "': the weapon probe could not run: " + p.Failure);
                    }
                    else if (p.MainHand == null ||
                             !string.Equals(p.MainHand.id, legal.id, StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add("[eligible-equips] '" + job + "': EquipWeaponById('" + legal.id + "') did NOT take - " +
                                     "the main hand reads '" + (p.MainHand?.id ?? "<null>") + "'" +
                                     (string.IsNullOrEmpty(p.Refusal) ? " with NO stated refusal" :
                                      " and the seam refused it: \"" + Condense(p.Refusal) + "\"") +
                                     ". GearCatalog.CanEquipWeapon says this class MAY hold it at level " + ProbeLevel +
                                     ", so the WO-1214 gate is over-refusing: it is now rejecting legal gear, which is a " +
                                     "worse defect than the one it was added to fix");
                    }
                    else if (!string.IsNullOrEmpty(p.Refusal))
                    {
                        failures.Add("[eligible-equips] '" + job + "': the weapon equipped but LastEquipRefusal was left " +
                                     "set to \"" + Condense(p.Refusal) + "\" - a stale refusal on an ACCEPTED equip " +
                                     "makes the UI show a reason for something that succeeded");
                    }
                    else weaponsProven++;
                }

                // -- the armor ------------------------------------------------------------
                ArmorDef legalArmor = null;
                foreach (var a in GearCatalog.AllArmors())
                {
                    if (a == null) continue;
                    if (!GearCatalog.CanEquipArmor(a, job, ProbeLevel, out _)) continue;
                    legalArmor = a;
                    break;
                }
                if (legalArmor == null)
                {
                    failures.Add("[eligible-equips] '" + job + "': the catalog serves this class NO wearable armor row " +
                                 "at level " + ProbeLevel + " - GearLoadout.Refresh's BestArmor would resolve to null " +
                                 "and the class would run at ArmorDefense 0");
                    continue;
                }

                var pa = RunProbe(job, equipArmorId: legalArmor.id);
                if (pa.Failure != null)
                {
                    failures.Add("[eligible-equips] '" + job + "': the armor probe could not run: " + pa.Failure);
                }
                else if (pa.Armor == null ||
                         !string.Equals(pa.Armor.id, legalArmor.id, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("[eligible-equips] '" + job + "': EquipArmorById('" + legalArmor.id + "') did NOT take - " +
                                 "the chest slot reads '" + (pa.Armor?.id ?? "<null>") + "'" +
                                 (string.IsNullOrEmpty(pa.Refusal) ? " with NO stated refusal" :
                                  " and the seam refused it: \"" + Condense(pa.Refusal) + "\"") +
                                 ". CanEquipArmor says the weight and level both fit, so the gate is over-refusing");
                }
                else armorProven++;
            }

            notes.Add("good path: " + weaponsProven + "/" + classes.Count + " classes equipped a legal weapon, " +
                      armorProven + "/" + classes.Count + " a legal armor, none with a refusal");
        }

        // =====================================================================
        //  CASE 7 - the WEIGHT gate, which is what refuses heavy plate to a Mage.
        // ---------------------------------------------------------------------
        //  Open owner item: `blink_armor_dragonic` is authored job:"any" +
        //  weight:"heavy" and is therefore OFFERED to a light-armour Mage by every
        //  job-only filter. Its authored weight is an OWNER RULING and is NOT
        //  touched here - what is asserted is that the ELIGIBILITY GATE refuses it
        //  for a light class, in words, and points at selling.
        // =====================================================================
        private static void Case7_ArmorWeightGate(List<string> failures, List<string> notes)
        {
            var classes = PlayableHeroes.AllKnownJobKeys();
            if (classes == null || classes.Count == 0)
            {
                failures.Add("[armor-weight-gate] PlayableHeroes.AllKnownJobKeys() is EMPTY - vacuous sweep");
                return;
            }

            int mismatches = 0, refused = 0;
            foreach (var job in classes)
            {
                string classWeight = GearCatalog.ClassWeight(job);
                foreach (var a in GearCatalog.AllArmors())
                {
                    if (a == null) continue;
                    string w = (a.weight ?? string.Empty).Trim().ToLowerInvariant();
                    if (w.Length == 0 || w == "any") continue;          // fits everyone by authoring
                    if (w == (classWeight ?? string.Empty)) continue;    // matches - Case 6 covers it
                    if (!GearCatalog.MeetsReq(a.req, 1)) continue;       // level gate would mask the weight gate

                    mismatches++;
                    if (GearCatalog.CanEquipArmorNow(a, job, 1, out string words, out _))
                    {
                        failures.Add("[armor-weight-gate] '" + job + "' (wears " + classWeight + ") was ALLOWED to equip '" +
                                     (a.id ?? "<null>") + "', authored weight '" + w + "'. The weight gate is not being " +
                                     "asked at the seam, so any non-UI caller can dress a caster in plate");
                        continue;
                    }
                    refused++;

                    if (string.IsNullOrEmpty(words))
                    {
                        failures.Add("[armor-weight-gate] '" + job + "' x '" + (a.id ?? "<null>") + "': refused with an " +
                                     "EMPTY player sentence. WO-1214 Ruling 2 - the owner is red/green colourblind, so a " +
                                     "refusal that is not WORDS is not a refusal she can read");
                    }
                    else if (!IsAscii(words))
                    {
                        failures.Add("[armor-weight-gate] '" + job + "' x '" + (a.id ?? "<null>") + "': the refusal " +
                                     "sentence is not ASCII: \"" + Condense(words) + "\"");
                    }
                    else if (words.IndexOf("sell", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        failures.Add("[armor-weight-gate] '" + job + "' x '" + (a.id ?? "<null>") + "': the refusal says " +
                                     "\"" + Condense(words) + "\" but never tells the player the item is still theirs to " +
                                     "SELL. Ruling 2 is HELD AND SELLABLE - a refusal that reads as a confiscation is " +
                                     "the same dead-weight problem in different words");
                    }
                }
            }

            if (mismatches == 0)
            {
                failures.Add("[armor-weight-gate] armor.json has NO row whose authored weight mismatches some class at " +
                             "level 1, so this case swept nothing and would pass vacuously");
                return;
            }
            notes.Add("weight gate: " + refused + "/" + mismatches + " class x mismatched-weight pairs refused in words");

            // The named open item, reported explicitly so the answer lands in the log rather
            // than being re-derived by hand next time.
            var dragonic = GearCatalog.FindArmor("blink_armor_dragonic");
            if (dragonic == null)
            {
                notes.Add("blink_armor_dragonic: NOT in armor.json (row removed or renamed)");
            }
            else
            {
                bool mageMay = GearCatalog.CanEquipArmorNow(dragonic, "mage", 1, out string mageWords, out _);
                if (mageMay)
                {
                    failures.Add("[armor-weight-gate] blink_armor_dragonic (job='" + (dragonic.job ?? "<null>") +
                                 "', weight='" + (dragonic.weight ?? "<null>") + "') is EQUIPPABLE by a Mage. It is " +
                                 "authored heavy and the Mage wears " + GearCatalog.ClassWeight("mage") + " - the weight " +
                                 "gate is being bypassed for job:\"any\" rows");
                }
                else
                {
                    notes.Add("blink_armor_dragonic (job=any, weight=heavy) REFUSED for mage: \"" +
                              Condense(mageWords) + "\"");
                }
            }
        }

        // =====================================================================
        //  CASE 8 - RULING 2's second half: HELD and SELLABLE, never destroyed.
        // ---------------------------------------------------------------------
        //  A refusal is only humane if the item retains value. Two things have to
        //  hold, and neither is provable from the equip seam alone:
        //    (a) the SELL list must not apply the eligibility filter - if the
        //        vendor only lists what the hero can wear, an ineligible drop is
        //        unsellable dead weight and Ruling 2 is a promise the game breaks;
        //    (b) a grant must BANK before it equips, so a refusal costs the player
        //        nothing. CompanionGearSetup is the one remaining shipping path
        //        that equips onto the player hero from a scripted beat.
        // =====================================================================
        private static void Case8_IneligibleStaysSellable(List<string> failures, List<string> notes)
        {
            // (a) the sell list is eligibility-blind.
            string shop = ReadSource(PartyShopSrc, failures);
            if (shop != null)
            {
                string sell = ExtractMethodBody(StripComments(shop), "BuildSellGear");
                if (sell == null)
                {
                    failures.Add("[held-and-sellable] PartyShopVM.BuildSellGear is gone - the only builder that lists " +
                                 "owned gear for sale. Without it there is no proven route to turn a refused drop back " +
                                 "into value, and Ruling 2 (\"they can sell\") is unverified");
                }
                else
                {
                    var gate = Regex.Match(sell, @"CanEquip\w*|ArmorFitsClass|WeaponFitsClass|JobMatches|MeetsReq");
                    if (gate.Success)
                    {
                        failures.Add("[held-and-sellable] PartyShopVM.BuildSellGear now consults '" + gate.Value +
                                     "'. The SELL list must stay eligibility-BLIND: the whole point of Ruling 2 is that " +
                                     "the shield a Mage cannot use is still worth coins to her. Filtering the sell list " +
                                     "by what the hero can equip turns every refused drop into permanent dead weight");
                    }
                    else notes.Add("sell list is eligibility-blind (no class/weight/level filter in BuildSellGear)");
                }
            }

            // (b) a scripted grant banks BEFORE it equips.
            string comp = ReadSource(CompanionSrc, failures);
            if (comp == null) return;
            string apply = ExtractMethodBody(StripComments(comp), "Apply");
            if (apply == null)
            {
                failures.Add("[held-and-sellable] CompanionGearSetup.Apply is gone - this oracle can no longer verify " +
                             "the one shipping non-UI path that equips onto the PLAYER hero (ResolveLoadout resolves " +
                             "the Player-tagged hero, not an NPC, despite the file name)");
                return;
            }

            var equipAt = Regex.Match(apply, @"Equip(Weapon|Armor|OffHand)ById\s*\(");
            var bankAt = Regex.Match(apply, @"\.Add\s*\(\s*grant\.");
            if (!equipAt.Success)
            {
                notes.Add("CompanionGearSetup.Apply no longer equips at all - deposit-only, the strongest form of Ruling 1");
                return;
            }
            if (!bankAt.Success)
            {
                failures.Add("[held-and-sellable] CompanionGearSetup.Apply equips the grant onto the player hero but " +
                             "never banks it into VillageInventory. The equip seam now FAILS CLOSED, so a grant the hero " +
                             "cannot yet hold (several starter grants are req.level 3) is simply LOST - the refusal " +
                             "turns a gift into nothing, which is the opposite of Ruling 2");
                return;
            }
            if (bankAt.Index > equipAt.Index)
            {
                failures.Add("[held-and-sellable] CompanionGearSetup.Apply banks the grant AFTER it equips it. The order " +
                             "is load-bearing: bank first so a seam refusal still leaves the player owning the item");
                return;
            }
            notes.Add("CompanionGearSetup.Apply banks the grant before equipping (a refusal cannot lose the gift)");

            // (c) the panel must not CLAIM a success the seam refused.
            CheckPanelDoesNotLie(failures, notes);
        }

        /// <summary>
        /// EquipmentPanel.DoEquip used to fire an unconditional "Equipped &lt;item&gt;." toast right
        /// after calling EquipVM.Equip, with no idea whether the equip had been REFUSED. A Mage who
        /// tapped a dropped shield was therefore TOLD it was equipped while nothing had changed -
        /// a confident lie, which is strictly worse than a silent failure because it sends the
        /// player away believing the slot changed. The panel must read the refusal and leave before
        /// the confirmation.
        /// </summary>
        private static void CheckPanelDoesNotLie(List<string> failures, List<string> notes)
        {
            string panel = ReadSource(PanelSrc, failures);
            if (panel == null) return;

            string body = ExtractMethodBody(StripComments(panel), "DoEquip");
            if (body == null)
            {
                failures.Add("[held-and-sellable] EquipmentPanel.DoEquip is gone - the handler that turns a tap into an " +
                             "equip and tells the player what happened. The WO-1214 Ruling 2 guarantee at the UI is now " +
                             "UNVERIFIED, which is not the same as safe");
                return;
            }

            int refusalAt = body.IndexOf("LastRefusal", StringComparison.Ordinal);
            var confirmAt = Regex.Match(body, @"ShowToast\s*\(\s*""Equipped");

            if (refusalAt < 0)
            {
                failures.Add("[held-and-sellable] EquipmentPanel.DoEquip never reads EquipVM.LastRefusal, so it cannot " +
                             "tell an accepted equip from a refused one. Every tap reports success - including the Mage " +
                             "tapping the shield that started WO-1214");
                return;
            }
            if (confirmAt.Success && confirmAt.Index < refusalAt)
            {
                failures.Add("[held-and-sellable] EquipmentPanel.DoEquip fires its \"Equipped ...\" confirmation BEFORE " +
                             "it checks LastRefusal. The order is the whole guarantee: a refused equip must never reach " +
                             "the confirmation line");
                return;
            }
            if (!Regex.IsMatch(body, @"ShowToast\s*\(\s*refusal"))
            {
                failures.Add("[held-and-sellable] EquipmentPanel.DoEquip detects the refusal but never SHOWS it. Ruling 2 " +
                             "requires WORDS the player can read - a tap that quietly does nothing is the same " +
                             "silent-failure class this ticket exists to end");
                return;
            }
            notes.Add("EquipmentPanel.DoEquip shows the refusal sentence and skips the success toast");
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
            public ArmorDef  Armor;
            public string Refusal;
            public string Failure;
        }

        private static Probe RunProbe(string job, string equipWeaponId = null, string equipOffHandId = null,
                                      string equipArmorId = null)
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
                if (!string.IsNullOrEmpty(equipArmorId)) loadout.EquipArmorById(equipArmorId);

                p.MainHand = loadout.EquippedWeapon;
                p.OffHand = loadout.EquippedOffHand;
                p.Armor = loadout.EquippedArmor;
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
