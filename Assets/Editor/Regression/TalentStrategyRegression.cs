// =============================================================================
// TalentStrategyRegression — WO-676 §C gates G1–G3 (+ the G4 fleet-probe spec)
// for the strategic skill-tree redesign.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Headless, no scene load. Follows the
// StrategicPlacementRegression precedent (self-contained Run(out reason) oracle,
// real objects in / real response out, reflection only on private seams with
// fail-LOUD "re-point this oracle" messages — never a vacuous pass).
//
// The gates (WO_676 §C):
//   G1 DATA        — hero-talents.json parses through the REAL loader path
//      (CanonicalJson bytes → raw JObject + HeroTalentCatalog.Reload); the two
//      canonical copies (Resources / StreamingAssets) are byte-equal; every
//      node's effect.type is a member of the effect-type VOCABULARY (the
//      HeroTalentCatalog.cs:34 list + the WO-676 strategic types); every
//      unlockAbility node carries a matching non-empty abilityId.
//   G2 STATSUM MATH — drive the REAL HeroTalentModifiers.StatSum over an
//      injected fixture catalog + a fixture WisdomCurrencyService: stacking
//      sums across the hero tree AND the shared pool, case-insensitive type
//      match, zero-default for unauthored types, identity with no service; the
//      EXISTING clamps hold (IncomingDamageReduction ≤ 0.85, BlockChance ≤
//      0.85, MaxHp ≤ 3x, Damage ≤ 3x, Cooldown ≥ 0.4x); the NEW WO-676 clamped
//      accessors hold (structureToughness total cap 0.5; a sane harvestRate
//      cap) — probed by name so the A3 lane's accessor is exercised the moment
//      it lands, and the gate fails loudly until it does.
//   G3 NO DEAD NODES — the headline. Every SHIPPED (non-hidden) node's effect
//      key must appear in TalentConsumerRegistry (below) — the static registry
//      of effect types that have an IMPLEMENTED consumer, each entry citing
//      the consuming code. A shipped node whose key is unregistered FAILS the
//      gate BY NAME unless the node (or its whole tree) carries "hidden": true
//      in hero-talents.json. A shipped node whose effect.note carries a dead-
//      stub marker ((V2) / (V-later) / stub) also fails. This mechanizes the
//      owner's wire-or-hide law forever: a dead node can never ship silently.
//   G4 — the RunAll wiring line lives in DataRegression.cs ([talent-strategy]);
//      the headless fleet-probe half is SPECIFIED at the bottom of this file
//      for the next AutoPilot lane (not implemented here — it needs a play-mode
//      bot, which is the fleet's silo, not this EditMode oracle's).
//
// EXPECTED FIRST-RUN STATE (2026-07-11, before the A1/A2/A3 lanes compose):
// G3 is EXPECTED TO FAIL on today's legacy stubs — that is the gate WORKING.
// Wire-or-hide each named node, or mark it "hidden": true, to go green:
//   knight.t4n4 Holy Retribution  (modifyAbility, no stat — "(taunted-foe dot — V-later)")
//   shared.n3   Wisdom Surge      (wisdomPerLevel — "(progression hook — V-later)")
//   shared.n4   Battle Instinct   (critChance — no consumer reads it)
//   shared.n5   Aether Bond       (manaRegen — no consumer reads it)
//   shared.n7   Swift Recovery    (healthRegen — no consumer reads it)
// G2's new-clamp probes fail until the A3 lane lands the clamped accessors.
//
// ── G3 STATE 2026-08-05 — READ THIS BEFORE "FIXING" THE RED ───────────────────
// The Ranger and Mage classes were unlocked, but the HiddenTrees set below was not
// updated in that commit (its own update rule said to). G3 therefore skipped BOTH
// FULL TREES for as long as players could reach them: it audited 41 nodes and
// reported green while 40 shipped nodes were never checked at all.
// HiddenTrees is now EMPTY, so G3 audits all 81 shipped nodes — and it goes RED on
// 31 PRE-EXISTING dead nodes (17 ranger + 14 mage). None of them are new breakage;
// they are the stubs that were always there, finally visible. Split:
//   * UNREGISTERED effect keys — no consumer exists anywhere for attackSpeed,
//     moveSpeed, critChance, stealth, summon, dodge, onEvent, shieldStrength,
//     manaCostReduction, modifyAbility:slow, modifyAbility:burn.
//   * REGISTERED key + a "(V2)" / "(NEW ability - stub)" note — the note and the
//     wiring disagree; the node advertises an effect its consumer does not deliver.
// The knight tree (32 nodes) and shared (9) remain fully green.
// THE ONLY TWO LEGAL MOVES REMAIN: wire a consumer (and register it with a
// citation), or mark that NODE "hidden": true in hero-talents.json. Which of these
// 31 nodes ship is a DESIGN call (they are player-reachable talents), so this pass
// deliberately leaves the gate honest rather than making that call for the owner.
// Re-adding a tree slug to HiddenTrees to get green is the bug, not the fix.
//
// RESOLUTION (orchestrator ruling, 2026-08-05): the 31 are recorded ONCE in
// KnownDeadNodeBaseline below as dated tracked debt under WO-910, so the gate keeps
// auditing them and names them as debt instead of blocking every other lane. The set
// may only SHRINK — a non-baselined dead node still fails, and a stale baseline id
// fails too. HIDING WAS CONSIDERED AND REJECTED for these 31: HeroTalentNodeDef.Hidden
// had no runtime reader (so hiding would only have silenced the gate), and hiding all
// 31 strands ranger t4 + mage t3/t4 entirely and orphans 3 more nodes — ranger would
// drop to ONE reachable talent of 20, mage to five. Hidden is NOW genuinely wired
// (HeroSkillTreeVM.Rebuild), but WHETHER to hide these is the owner's call: see
// WorkOrders/WORK_ORDER_910_ranger_mage_talent_consumers.md.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!TalentStrategyRegression.Run(out var talentStratReason)) failures.Add(talentStratReason); else log.AppendLine("[talent-strategy] " + talentStratReason);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village.Talents;

namespace DeNelle.Editor
{
    // =========================================================================
    //  TalentConsumerRegistry — the IMPLEMENTED-CONSUMER registry (WO-676 G3)
    // -------------------------------------------------------------------------
    //  THE UPDATE RULE (non-negotiable — this is what keeps the registry honest):
    //   1. A key may be added ONLY in the same commit as the consumer read it
    //      cites. The value MUST name the consuming file + member (a claim one
    //      grep away from verification — a lying citation is instantly exposed).
    //   2. If a consumer is removed or relocated, remove/re-point its key in
    //      the SAME commit. A stale citation = a lie = fix it, don't ship it.
    //   3. NEVER add a key just to silence the gate. If a node's effect has no
    //      consumer, either wire one (and register it per rule 1) or mark the
    //      node "hidden": true in hero-talents.json. Those are the ONLY two
    //      legal moves — that is the owner's wire-or-hide law, mechanized.
    //   4. Key shape: effect.type lower-cased; modifyAbility is keyed at
    //      "modifyability:<stat lower-cased>" because the interpreter routes
    //      it per-stat (a bare modifyAbility with no wired stat is dead).
    //
    //  A static table (not self-registration) on purpose: registration code
    //  that "runs somewhere at startup" can silently not-run in a headless
    //  EditMode pass and vacuously green the gate. A const table + a cited
    //  consumer per row cannot drift silently — the citation is greppable and
    //  the gate output prints it next to every shipped node it admits.
    // =========================================================================
    public static class TalentConsumerRegistry
    {
        /// <summary>Effect key → the implemented consumer that reads it (file/member citation).</summary>
        public static readonly IReadOnlyDictionary<string, string> Implemented =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Live today (verified from code 2026-07-11, WO-566 wave included) ──
            { "damagereduction",     "HeroHealth.TakeDamage via HeroTalentModifiers.IncomingDamageReduction" },
            { "defense",             "HeroHealth.TakeDamage via HeroTalentModifiers.IncomingDamageReduction" },
            { "allstatspct",         "HeroTalentModifiers Damage/MaxHp/IncomingDamageReduction fold-ins" },
            { "maxhppct",            "HeroHealth.MaxHp via HeroTalentModifiers.MaxHpMultiplier" },
            { "blockchance",         "HeroHealth.TakeDamage via HeroTalentModifiers.RollBlock" },
            { "damagebonus",         "HeroAbilities damage calc via HeroTalentModifiers.DamageMultiplier" },
            { "cdreduction",         "HeroAbilities cooldown calc via HeroTalentModifiers.CooldownMultiplier" },
            { "unlockability",       "Loadout equip flow (kind=skill + abilityId → HeroLoadout/quick-swap)" },
            { "modifyability:heal",  "HeroAbilities heal path via HeroTalentModifiers.HealAmountMultiplier" },
            { "reflect",             "HeroHealth.ApplyReflect via HeroTalentModifiers.ReflectFraction (WO-566)" },
            { "laststand",           "HeroHealth emergency window via HeroTalentModifiers.TryGetLastStand (WO-566)" },
            { "invuln",              "HeroHealth auto-emergency via HeroTalentModifiers.TryGetInvuln (WO-566)" },
            { "revive",              "HeroHealth cheat-death via HeroTalentModifiers.TryGetRevive (WO-566)" },
            { "proc",                "PlayerAttackController on-hit via HeroTalentModifiers.ForEachOnHitProc (WO-566)" },
            { "healthregen",         "HeroHealth.RegenTick via HeroTalentModifiers.HealthRegenBonus (WO-676 G3 wire — town/Oathmend regen paths)" },
            { "manaregen",           "HeroAbilities.Update mana tick via HeroTalentModifiers.ManaRegenBonus (WO-676 G3 wire)" },

            // ── WO-676 strategic types (consumers land in the concurrent A1/A2
            //    lanes this batch; the composed tree is gated as one — if a lane
            //    slips, the citation is the work order for wiring it) ──────────
            { "harvestrate",         "WO-676 STEWARD: EchoService tick + ResourceBuildingHarvester accrual (A2 lane)" },
            { "collectorcap",        "WO-676 STEWARD: ResourceCollector capacity (A2 lane)" },
            { "repaircost",          "WO-676 STEWARD: WO-672 repair pricing choke point (A2 lane)" },
            { "buildtime",           "WO-676 STEWARD: BuildTimerService duration calc (A2 lane)" },
            { "salvage",             "WO-676 STEWARD: BuildModeController sell refund + WO-672 destroyed-loss calc (A2 lane)" },
            { "wavereward",          "WO-676 STEWARD: wave reward grant path (A2 lane)" },
            { "towerdamage",         "WO-676 BULWARK: DefenseTower/ArcaneTower damage calc shared base read (A1 lane)" },
            { "towerrange",          "WO-676 BULWARK: tower range seam (A1 lane)" },
            { "structuretoughness",  "WallSegment.ApplyContactDamage + Gate.ApplyContactDamage via WallSegment.StructureToughnessReduction" },
            { "structuretoughnesswave", "WallSegment.StructureToughnessReduction wave-active slice (WaveManager.Phase==Active) consumed by WallSegment+Gate intake" },
            { "towerattackspeed",    "TowerCombat.Update fire tick via Tower.TalentAttackSpeedMult (HeroTalentModifiers.TowerAttackSpeedBonus, TTL-cached) — WO-676 BULWARK" },
            { "modifyability:poison","WO-676 WAR Venombrand: Thunderbolt/Throwing Spear poison rider (Emberbrand-burn shape) (A1 lane)" },
            // Bare modifyAbility (empty stat) — Holy Retribution's taunt-burn rider. The empty
            // stat IS the discriminator: TryGetAbilityDotRider(stat: null) matches only nodes
            // with an UNSET effect.stat (see HeroTalentModifiers), so this key is not a wildcard.
            { "modifyability:",      "HeroAbilities.ResolveTaunt via HeroTalentModifiers.TryGetAbilityDotRider (Holy Retribution taunt-burn, WO-676 WAR)" },
        };
    }

    public static class TalentStrategyRegression
    {
        private const string RelativePath = "Data/Canonical/hero-talents.json";

        // The effect-type VOCABULARY: the HeroTalentCatalog.cs:34 declared list,
        // plus "revive" (used by shared.n6 Legendary Resolve; the catalog comment
        // is one type behind — canon-fix candidate), plus the WO-676 strategic
        // types. UPDATE RULE: extending the vocabulary in HeroTalentCatalog.cs:34
        // requires extending THIS array in the same commit (G1 enforces membership,
        // and the gate self-checks that every registry key's base type is here).
        private static readonly HashSet<string> Vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // HeroTalentCatalog.cs:34 list
            "damageReduction", "blockChance", "defense", "maxHpPct", "damageBonus",
            "cdReduction", "unlockAbility", "modifyAbility", "aura", "onEvent",
            "proc", "taunt", "reflect", "laststand", "invuln", "summon", "stealth",
            "stun", "mark", "pull", "allStatsPct", "critChance", "attackSpeed",
            "manaRegen", "manaCostReduction", "healthRegen", "shieldStrength",
            "wisdomPerLevel", "moveSpeed", "range", "dodge",
            // in-data but missing from the comment (shared.n6)
            "revive",
            // WO-676 strategic types
            "harvestRate", "collectorCap", "repairCost", "buildTime", "salvage",
            "waveReward", "towerDamage", "towerRange", "structureToughness",
            "towerAttackSpeed", "structureToughnessWave",
        };

        // Trees hidden from the SHIPPED scope: a tree listed here is AUDITED (logged)
        // but cannot fail G3. UPDATE RULE: when a class unlocks, remove its slug here in
        // the same commit — its stubs then become shipped and must be wired or hidden.
        //
        // 2026-08-05: EMPTIED. This set held { "ranger", "mage" } from the ff.knightonly
        // era and was NOT updated when those two classes were actually unlocked, so G3 —
        // the "no dead talent nodes" gate — silently skipped both entire trees while
        // players could reach them. A coverage loss with no failing test to announce it is
        // the worst kind, so the set stays EMPTY: hiding is now a per-NODE decision in
        // hero-talents.json ("hidden": true), which is visible in the data the designer
        // edits instead of buried in an editor-only C# constant. Do not re-add a tree slug
        // here to make the suite green — that is exactly the failure this line caused.
        private static readonly HashSet<string> HiddenTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // =====================================================================
        //  KnownDeadNodeBaseline — TRACKED DEBT, dated 2026-08-05, WO-910
        // ---------------------------------------------------------------------
        //  31 nodes (17 ranger + 14 mage) are PLAYER-REACHABLE talents whose effect
        //  has NO implemented consumer, pending an OWNER DESIGN PASS (WO-910). They
        //  are listed here so G3 keeps AUDITING them and reports them as tracked debt
        //  instead of either lying about them or blocking every other lane's gate.
        //  Split: 16 unregistered-effect-key + 15 registered-key-with-a-stub-note.
        //  Knight (32 nodes) and shared (9) are fully green — this is isolated to the
        //  two classes unlocked on 2026-08-05.
        //
        //  WHY A BASELINE AND NOT "hidden": true ON EACH NODE (the rejected fix —
        //  this reasoning must survive, or someone will "fix" this by hiding them):
        //   1. On 2026-08-05 HeroTalentNodeDef.Hidden had ZERO runtime readers, so
        //      hiding would have greened the gate while leaving all 31 nodes fully
        //      clickable in the player's tree — suppression, just spelled in JSON.
        //      (Hidden is now genuinely wired in HeroSkillTreeVM.Rebuild; the point
        //      stands that hiding is the OWNER's call to make, not the gate's.)
        //   2. Hiding all 31 STRANDS THREE WHOLE TIERS (ranger t4, mage t3 + t4) and
        //      ORPHANS 3 survivors whose only prerequisite would be hidden. Ranger
        //      would collapse to ONE reachable talent of 20; mage to five. Shipping
        //      an unreachable tree is worse than the bug being fixed.
        //
        //  THE RATCHET (debt may only SHRINK — enforced below, not by good intentions):
        //   * A dead node NOT in this set FAILS the gate. New debt cannot be added.
        //   * A baseline id that NO LONGER fails (wired, hidden, renamed or deleted)
        //     FAILS the gate too, naming the line to delete. A baseline entry can
        //     therefore never outlive the debt it tracks and quietly rot into a lie.
        //  There is no way to make this gate green by EDITING THIS SET — only by
        //  wiring a consumer (or the owner ruling "hide it") and then pruning the id.
        // =====================================================================
        private static readonly HashSet<string> KnownDeadNodeBaseline = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // -- unregistered effect key: no consumer exists anywhere (16) --
            "ranger.t1n1",  // Quick Draw            attackSpeed
            "ranger.t2n1",  // Windstrider Boots     moveSpeed
            "ranger.t2n3",  // Eagle Vision          critChance
            "ranger.t2n4",  // Deep Freeze           modifyAbility:slow
            "ranger.t2n5",  // Shadow Veil           stealth
            "ranger.t3n2",  // Emberhead             modifyAbility:burn
            "ranger.t3n3",  // Leafcloak             dodge
            "ranger.t3n4",  // Beast Companion       summon
            "ranger.t4n2",  // Windstrider Legend    moveSpeed
            "ranger.t4n3",  // Phantom Hunter        stealth
            "ranger.t4n4",  // Nature's Fury         onEvent
            "mage.t2n1",    // Aether Surge          onEvent
            "mage.t2n3",    // Arcane Shield         shieldStrength
            "mage.t3n3",    // Aether Form           manaCostReduction
            "mage.t3n4",    // Runic Overload        onEvent
            "mage.t4n4",    // Reality Rift          onEvent
            // -- registered key, but the note declares a stub the consumer does not deliver (15) --
            "ranger.t1n2",  // Hunter's Mark         unlockAbility "(NEW ability - stub)"
            "ranger.t1n3",  // Tumble Step           unlockAbility "(NEW ability - stub)"
            "ranger.t1n5",  // Arrow Storm Prep      unlockAbility "(NEW ability - stub)"
            "ranger.t3n5",  // Precision Strike      unlockAbility "(NEW ability - stub)"
            "ranger.t4n1",  // Storm of Arrows       unlockAbility "(NEW ability - stub)"
            "ranger.t4n5",  // Elarion's Arrow       modifyAbility: "pierce/chain (V2)"
            "mage.t1n5",    // Rune Binding          modifyAbility: "chain (V2)"
            "mage.t2n4",    // Flame Mastery         modifyAbility: "(V2)"
            "mage.t3n1",    // Cataclysm Prep        modifyAbility: "(V2)"
            "mage.t3n2",    // Spell Echo            proc "duplicate (V2)"
            "mage.t3n5",    // Void Rift             unlockAbility "(NEW ability - stub)"
            "mage.t4n1",    // Cataclysm             unlockAbility "(NEW ability - stub)"
            "mage.t4n2",    // Aetherweaver Ascension damageBonus "(V2)"
            "mage.t4n3",    // Eternal Arcana        damageBonus "+40% mana regen (V2)"
            "mage.t4n5",    // Elarion's Legacy      proc "duplicate (V2)"
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TALENT STRATEGY (WO-676 §C gates G1-G3) ---");

            try
            {
                var root = GateOne_DataAndVocabulary(failures, log);
                GateTwo_StatSumMathAndClamps(failures, log);
                if (root != null) GateThree_NoDeadNodes(root, failures, log);
            }
            catch (Exception ex)
            {
                failures.Add($"talent-strategy oracle threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "TALENT_STRATEGY_OK");
                reason = "TALENT STRATEGY OK — hero-talents.json parse/dual-copy/vocabulary + StatSum " +
                         "stacking/clamps + every shipped node's effect has an implemented consumer, except " +
                         KnownDeadNodeBaseline.Count + " node(s) tracked as dated debt under WO-910 " +
                         "(ranger/mage; awaiting the owner design pass) (WO-676 G1-G3)";
                return true;
            }
            reason = "talent-strategy: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "TALENT_STRATEGY_FAIL: " + reason);
            return false;
        }

        // =====================================================================
        //  G1 — parse + dual-copy byte-equality + effect-type vocabulary
        // =====================================================================
        private static JObject GateOne_DataAndVocabulary(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[G1] data / dual-copy / vocabulary");

            // 1a. Parse through the REAL platform loader path (CanonicalJson bytes).
            string json = DeNelle.Core.CanonicalJson.Read(RelativePath);
            if (string.IsNullOrEmpty(json))
            { failures.Add("hero-talents.json unreadable via CanonicalJson (Resources + StreamingAssets both missing)"); return null; }

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex)
            { failures.Add($"hero-talents.json failed to parse: {ex.Message}"); return null; }

            // 1b. The REAL typed loader maps it to trees (the same code path the panel uses).
            HeroTalentCatalog.Reload();
            var knight = HeroTalentCatalog.GetTree("knight");
            if (knight == null || knight.Nodes == null || knight.Nodes.Count == 0)
                failures.Add("HeroTalentCatalog.GetTree('knight') is empty after Reload — the JSON->object mapping broke");
            else log.AppendLine($"  knight tree -> {knight.Nodes.Count} node(s); shared pool -> {HeroTalentCatalog.SharedNodes.Count}");
            if (HeroTalentCatalog.SharedNodes.Count == 0)
                failures.Add("HeroTalentCatalog.SharedNodes is empty — the shared pool lost its mapping");

            // 1c. Dual copies byte-equal (the CanonicalJson dual-copy rule).
            string res = Application.dataPath + "/Resources/" + RelativePath;
            string sa = Application.dataPath + "/StreamingAssets/" + RelativePath;
            try
            {
                var a = System.IO.File.ReadAllBytes(res);
                var b = System.IO.File.ReadAllBytes(sa);
                if (!System.Collections.StructuralComparisons.StructuralEqualityComparer.Equals(a, b))
                    failures.Add("hero-talents.json: Resources and StreamingAssets copies are NOT byte-equal (CanonicalJson dual-copy rule)");
                else log.AppendLine("  dual copies byte-equal ok");
            }
            catch (Exception ex)
            { failures.Add($"hero-talents.json dual-copy check could not read both copies ({ex.Message})"); }

            // 1d. Vocabulary membership for EVERY node (hidden trees included — a
            //     typo'd type is a data bug regardless of ship state) + the
            //     unlockAbility abilityId belt.
            int checkedNodes = 0;
            foreach (var (treeSlug, node) in AllNodes(root))
            {
                checkedNodes++;
                string id = Str(node, "id") ?? $"<{treeSlug} unnamed node>";
                var effect = node["effect"] as JObject;
                string type = effect != null ? Str(effect, "type") : null;
                if (string.IsNullOrEmpty(type))
                { failures.Add($"hero-talents.json: node '{id}' has no effect.type (every node must declare its effect)"); continue; }
                if (!Vocabulary.Contains(type))
                    failures.Add($"hero-talents.json: node '{id}' effect.type '{type}' is not in the HeroTalentCatalog vocabulary (typo, or extend HeroTalentCatalog.cs:34 + this gate's Vocabulary in the same commit)");

                if (string.Equals(type, "unlockAbility", StringComparison.OrdinalIgnoreCase))
                {
                    string abilityId = Str(node, "abilityId");
                    string effAbility = Str(effect, "ability");
                    if (string.IsNullOrEmpty(abilityId) || !string.Equals(abilityId, effAbility, StringComparison.OrdinalIgnoreCase))
                        failures.Add($"hero-talents.json: unlockAbility node '{id}' abilityId ('{abilityId ?? "<null>"}') must be non-empty and match effect.ability ('{effAbility ?? "<null>"}') — the equip flow routes off abilityId");
                }
            }
            log.AppendLine($"  vocabulary checked over {checkedNodes} node(s) ok");
            if (checkedNodes == 0) failures.Add("hero-talents.json enumerated 0 nodes (trees/shared blocks missing)");

            // 1e. Self-check: every registry key's base type is in the vocabulary
            //     (a registered key for a type the data can never author is drift).
            foreach (var kv in TalentConsumerRegistry.Implemented)
            {
                string baseType = kv.Key.Split(':')[0];
                if (!Vocabulary.Contains(baseType))
                    failures.Add($"TalentConsumerRegistry key '{kv.Key}' has base type '{baseType}' outside the vocabulary — registry/vocabulary drift");
            }
            return root;
        }

        // =====================================================================
        //  G2 — StatSum math: stacking, zero-default, identity, clamps
        // =====================================================================
        private static void GateTwo_StatSumMathAndClamps(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[G2] StatSum math + clamps");

            // Inject a fixture catalog + a fixture WisdomCurrencyService so the
            // REAL StatSum path (ForEachUnlocked over hero tree + shared pool,
            // gated by service.IsUnlocked) is exercised — not a re-derivation.
            var dataField = typeof(HeroTalentCatalog).GetField("_data", BindingFlags.NonPublic | BindingFlags.Static);
            if (dataField == null)
            { failures.Add("HeroTalentCatalog._data not found by reflection — the catalog seam moved; re-point this oracle"); return; }

            var prevInstance = WisdomInstance();
            GameObject svcGo = null;
            try
            {
                // Fixture tree "oracle": stacking pairs per type + clamp-overdrive values.
                var tree = new HeroTalentTreeDef { HeroSlug = "oracle", DisplayName = "Oracle" };
                tree.Nodes.Add(Fx("oracle.st1", "structureToughness", 0.30f));
                tree.Nodes.Add(Fx("oracle.st2", "structureToughness", 0.30f));
                tree.Nodes.Add(Fx("oracle.hr1", "harvestRate", 1.50f));
                tree.Nodes.Add(Fx("oracle.hr2", "harvestRate", 1.50f));
                tree.Nodes.Add(Fx("oracle.dr1", "damageReduction", 0.50f));
                tree.Nodes.Add(Fx("oracle.dr2", "damageReduction", 0.50f));
                tree.Nodes.Add(Fx("oracle.bc1", "blockChance", 0.70f));
                tree.Nodes.Add(Fx("oracle.bc2", "blockChance", 0.50f));
                tree.Nodes.Add(Fx("oracle.hp1", "maxHpPct", 5.00f));
                tree.Nodes.Add(Fx("oracle.db1", "damageBonus", 9.00f));
                tree.Nodes.Add(Fx("oracle.cd1", "cdReduction", 0.90f));
                tree.Nodes.Add(Fx("oracle.td1", "towerDamage", 0.15f));
                var data = new HeroTalentData();
                data.Trees["oracle"] = tree;
                // Shared pool must stack INTO the hero sum (the v2 contract).
                data.Shared.Add(Fx("shared.oracle-st", "structureToughness", 0.90f));
                dataField.SetValue(null, data);

                // Fixture service: inactive GO (Awake never runs), unlocked = all fixture ids.
                svcGo = new GameObject("Oracle_WisdomService");
                svcGo.SetActive(false);
                var svc = svcGo.AddComponent<WisdomCurrencyService>();
                var unlocked = new HashSet<string>();
                foreach (var n in tree.Nodes) unlocked.Add(n.Id);
                foreach (var n in data.Shared) unlocked.Add(n.Id);
                SetPrivate(svc, "_unlocked", unlocked);
                SetWisdomInstance(svc);

                // 2a. Stacking sums (hero tree pairs + the shared-pool contribution).
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "structureToughness"), 1.50f,
                    "StatSum(structureToughness) must stack 0.3+0.3 (tree) + 0.9 (shared) = 1.5");
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "harvestRate"), 3.00f,
                    "StatSum(harvestRate) must stack 1.5+1.5 = 3.0");
                // Case-insensitive type match (the interpreter contract).
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "HARVESTRATE"), 3.00f,
                    "StatSum type match must be case-insensitive");
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "towerDamage"), 0.15f,
                    "StatSum(towerDamage) single node = 0.15");

                // 2b. Zero-default: unauthored types sum to exactly 0 (a consumer
                //     reading an unlearned capability must see the identity).
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "towerRange"), 0f,
                    "StatSum(towerRange) with no node must be 0 (zero-default)");
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "salvage"), 0f,
                    "StatSum(salvage) with no node must be 0 (zero-default)");
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "notARealType"), 0f,
                    "StatSum of an unknown type must be 0, never throw");

                // 2c. EXISTING clamps hold under overdrive (the proven-baseline half).
                AssertApprox(failures, HeroTalentModifiers.IncomingDamageReduction("oracle"), 0.85f,
                    "IncomingDamageReduction must clamp 1.0 raw to the 0.85 ceiling");
                AssertApprox(failures, HeroTalentModifiers.BlockChance("oracle"), 0.85f,
                    "BlockChance must clamp 1.2 raw to the 0.85 ceiling");
                AssertApprox(failures, HeroTalentModifiers.MaxHpMultiplier("oracle"), 3f,
                    "MaxHpMultiplier must clamp +500% raw to the 3x ceiling");
                AssertApprox(failures, HeroTalentModifiers.DamageMultiplier("oracle"), 3f,
                    "DamageMultiplier must clamp +900% raw to the 3x ceiling");
                AssertApprox(failures, HeroTalentModifiers.CooldownMultiplier("oracle"), 0.4f,
                    "CooldownMultiplier must clamp -90% raw to the 0.4x floor");
                log.AppendLine("  stacking + zero-default + existing clamps ok");

                // 2d. NEW WO-676 clamped accessors (the A3 lane's seam). Probed by
                //     name (public static float <name contains type>(string)) so the
                //     accessor is exercised the moment it lands; a missing accessor
                //     FAILS — a raw uncapped read at the consumer is the bug class
                //     this clamp exists to prevent (structureToughness > 0.5 would
                //     make defenses near-unkillable).
                // A3 landed the capped read as StructureToughnessReduction(string heroClass,
                // bool waveActive) — two params, so the generic (string) probe can't find it.
                // Probe the real signature: waveActive=true folds BOTH sums (1.5 raw here)
                // and must still clamp to the 0.5 total cap.
                var stAccessor = typeof(HeroTalentModifiers).GetMethods(
                        BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name.IndexOf("toughness", StringComparison.OrdinalIgnoreCase) >= 0
                                         && m.ReturnType == typeof(float)
                                         && m.GetParameters().Length == 2
                                         && m.GetParameters()[0].ParameterType == typeof(string)
                                         && m.GetParameters()[1].ParameterType == typeof(bool));
                if (stAccessor == null)
                    failures.Add("no clamped structureToughness accessor found on HeroTalentModifiers (public static float X(string heroClass, bool waveActive) with 'toughness' in the name) — the capped read (total cap 0.5) must exist; re-point this probe if the signature changed");
                else
                {
                    float v = (float)stAccessor.Invoke(null, new object[] { "oracle", true });
                    if (Mathf.Abs(v - 0.5f) > 0.001f)
                        failures.Add($"{stAccessor.Name}('oracle', waveActive:true) = {v:0.###} with a 1.5 raw sum — the WO-676 structureToughness total cap is 0.5");
                    else log.AppendLine($"  {stAccessor.Name} clamps 1.5 raw -> 0.5 ok (wave slice folded)");
                }
                var hrAccessor = FindClampedAccessor("harvestrate", "harvest");
                if (hrAccessor == null)
                    failures.Add("no clamped harvestRate accessor found on HeroTalentModifiers (public static float X(string) with 'harvest' in the name) — the A3 lane must expose a sanely-capped read; re-point this probe if it is named otherwise");
                else
                {
                    float v = (float)hrAccessor.Invoke(null, new object[] { "oracle" });
                    if (v >= 3.0f - 0.001f)
                        failures.Add($"{hrAccessor.Name}('oracle') = {v:0.###} equals the 3.0 raw sum — no clamp is applied (harvestRate needs a sane cap)");
                    else if (v > 2.0f)
                        failures.Add($"{hrAccessor.Name}('oracle') = {v:0.###} — a harvestRate cap above +200% is not sane (WO-676 G2)");
                    else log.AppendLine($"  {hrAccessor.Name} clamps 3.0 raw -> {v:0.###} ok");
                }

                // 2e. Identity with NO service: every read must return the identity
                //     (combat/economy unchanged at baseline).
                SetWisdomInstance(null);
                AssertApprox(failures, HeroTalentModifiers.StatSum("oracle", "structureToughness"), 0f,
                    "StatSum with no WisdomCurrencyService must be 0 (identity)");
                AssertApprox(failures, HeroTalentModifiers.DamageMultiplier("oracle"), 1f,
                    "DamageMultiplier with no service must be 1 (identity)");
                log.AppendLine("  no-service identity ok");
            }
            finally
            {
                dataField.SetValue(null, null);   // drop the fixture; next read reloads the real JSON
                SetWisdomInstance(prevInstance);
                if (svcGo != null) UnityEngine.Object.DestroyImmediate(svcGo);
            }
        }

        // =====================================================================
        //  G3 — NO DEAD NODES: shipped effect ⇒ implemented consumer, or hidden
        // =====================================================================
        private static void GateThree_NoDeadNodes(JObject root, List<string> failures, StringBuilder log)
        {
            log.AppendLine("[G3] no dead nodes (implemented-consumer registry)");

            int shipped = 0, hidden = 0, auditStubs = 0;
            var auditSample = new List<string>();
            // Every baseline id we actually SAW fail this run. Anything in the baseline
            // that is missing from this set at the end is stale debt -> the ratchet fires.
            var baselineHits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var debtSample = new List<string>();
            foreach (var (treeSlug, node) in AllNodes(root))
            {
                string id = Str(node, "id") ?? $"<{treeSlug} unnamed>";
                string name = Str(node, "name") ?? "?";
                var effect = node["effect"] as JObject;
                string type = effect != null ? Str(effect, "type") : null;
                string stat = effect != null ? Str(effect, "stat") : null;
                string note = effect != null ? Str(effect, "note") : null;
                string key = EffectKey(type, stat);

                bool nodeHidden = Bool(node, "hidden") || HiddenTrees.Contains(treeSlug);
                if (nodeHidden)
                {
                    hidden++;
                    // AUDIT (log-only): hidden-tree stubs stay on the books so the
                    // re-audit when a class unlocks starts from data, not memory.
                    if (!TalentConsumerRegistry.Implemented.ContainsKey(key) || IsStubNote(note))
                    {
                        auditStubs++;
                        if (auditSample.Count < 12) auditSample.Add($"{id} ({type}{(string.IsNullOrEmpty(note) ? "" : " " + note)})");
                    }
                    continue;
                }

                shipped++;

                // The node's verdict is computed FIRST, then routed: a dead node that is
                // TRACKED DEBT (WO-910 baseline) logs instead of failing; a dead node that
                // is NOT baselined always fails. New debt can never be added silently.
                string deadReason = null;
                if (!TalentConsumerRegistry.Implemented.TryGetValue(key, out var consumer))
                {
                    deadReason = $"effect key '{key}' has NO implemented consumer in TalentConsumerRegistry" +
                                 (string.IsNullOrEmpty(note) ? "" : $" - note says '{note}'");
                }
                // Belt: a registered type whose NOTE still declares a dead stub is a
                // half-truth — the note and the wiring must agree.
                else if (IsStubNote(note))
                {
                    deadReason = $"effect note '{note}' declares a V2/V-later stub while shipped non-hidden";
                }
                if (deadReason == null) continue;

                if (KnownDeadNodeBaseline.Contains(id))
                {
                    baselineHits.Add(id);
                    if (debtSample.Count < 12) debtSample.Add(id);
                    continue;   // tracked debt (WO-910) — audited + reported, not failing
                }
                failures.Add($"DEAD NODE '{id}' ({name}): {deadReason} - wire a consumer (and register it " +
                             "with a citation) or mark the node \"hidden\": true (owner's wire-or-hide law). " +
                             "Do NOT add it to KnownDeadNodeBaseline — that set is frozen debt (WO-910) and may only shrink");
            }

            log.AppendLine($"  shipped nodes checked: {shipped}; hidden/gated-off: {hidden}; " +
                           $"tracked debt (WO-910): {baselineHits.Count}/{KnownDeadNodeBaseline.Count}");
            if (debtSample.Count > 0)
                log.AppendLine($"  DEBT (WO-910, non-failing, awaiting the owner design pass): {string.Join(", ", debtSample)}" +
                               (baselineHits.Count > debtSample.Count ? $", +{baselineHits.Count - debtSample.Count} more" : ""));

            // THE RATCHET: a baseline id that no longer fails (wired / hidden / renamed /
            // deleted) is stale debt. Failing here is what forces the set to SHRINK — the
            // one-line fix is to delete that id, and the baseline can never rot into a lie.
            foreach (var id in KnownDeadNodeBaseline)
                if (!baselineHits.Contains(id))
                    failures.Add($"STALE BASELINE '{id}': listed in KnownDeadNodeBaseline (WO-910 tracked debt) but it no " +
                                 "longer reports as a dead node - it was wired, hidden, renamed or deleted. DELETE that id " +
                                 "from KnownDeadNodeBaseline in the same commit (the debt set may only shrink)");

            if (auditStubs > 0)
                log.AppendLine($"  AUDIT (hidden trees, log-only): {auditStubs} stub effect(s) on the books — sample: {string.Join(", ", auditSample)}");
            if (shipped == 0)
                failures.Add("G3 enumerated 0 shipped nodes — the shipped-scope filter is broken (HiddenTrees ate everything?)");
        }

        // =====================================================================
        //  G4 (second half) — HEADLESS FLEET-PROBE SPEC (for the next AutoPilot lane)
        // ---------------------------------------------------------------------
        //  NOT implemented here: it needs a play-mode bot in the AutoPilot fleet
        //  silo (Assets/Editor/AutoPilot). Spec, precise enough to implement blind:
        //
        //  Probe: "TalentStrategyProbe" (fleet convention: seeded bot, FIXED oracle,
        //  emits a single authoritative marker for the log scan).
        //   1. Boot the headless play session (castle hub); wait until
        //      WisdomCurrencyService.Instance and the economy services are live.
        //   2. Locate the STEWARD harvest node BY EFFECT TYPE, not by id: scan
        //      HeroTalentCatalog.GetTree("knight").Nodes + SharedNodes for the first
        //      node with effect.type == "harvestRate" (Provider's Bond per WO-676 §A).
        //      FAIL "TALENT_PROBE_FAIL: no harvestRate node shipped" if absent.
        //   3. Baseline: measure one collector's accrual over a fixed window (>= 30s
        //      of ResourceBuildingHarvester ticks / EchoService tick output) → r0.
        //   4. Unlock through the REAL path (never a direct set):
        //      WisdomCurrencyService.Instance.Grant(node.Cost + prereq costs);
        //      unlock each prerequisite then the node via Unlock(id) — asserting each
        //      returns true.
        //   5. Re-measure the same window → r1. ASSERT
        //      r1 ≈ r0 * (1 + HeroTalentModifiers.StatSum("knight", "harvestRate"))
        //      within 5% — one end-to-end proof that data → Σ → consumer → tick.
        //   6. FlowTrace [Flow:TalentStrategy] Enter/Step/Fail at every stage;
        //      emit TALENT_PROBE_OK / TALENT_PROBE_FAIL: <why> for the fleet scan.
        //   Optional (one proof per branch, WO-676 G4): repeat the shape for BULWARK
        //   via towerDamage — spawn a tower, compare damage-per-shot before/after
        //   unlocking the first towerDamage node; STEWARD is the mandatory probe.
        // =====================================================================

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>Enumerates (treeSlug, node) over every tree's nodes + the shared pool ("shared").</summary>
        private static IEnumerable<(string, JObject)> AllNodes(JObject root)
        {
            if (root["trees"] is JObject trees)
                foreach (var prop in trees.Properties())
                    if (prop.Value is JObject tree && tree["nodes"] is JArray nodes)
                        foreach (var t in nodes)
                            if (t is JObject n) yield return (prop.Name, n);
            if (root["shared"] is JArray shared)
                foreach (var t in shared)
                    if (t is JObject n) yield return ("shared", n);
        }

        /// <summary>The G3 registry key for an effect: type lower-cased; modifyAbility
        /// keys per-stat ("modifyability:heal") because the interpreter routes per-stat —
        /// a bare modifyAbility with no wired stat resolves to "modifyability:" (unregistered = dead).</summary>
        private static string EffectKey(string type, string stat)
        {
            string t = (type ?? "").Trim().ToLowerInvariant();
            if (t == "modifyability")
                return "modifyability:" + (stat ?? "").Trim().ToLowerInvariant();
            return t;
        }

        private static bool IsStubNote(string note)
        {
            if (string.IsNullOrEmpty(note)) return false;
            return note.IndexOf("V2", StringComparison.OrdinalIgnoreCase) >= 0
                || note.IndexOf("V-later", StringComparison.OrdinalIgnoreCase) >= 0
                || note.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Str(JObject o, string key)
        {
            var t = o[key];
            return t != null && t.Type == JTokenType.String ? (string)t : null;
        }

        private static bool Bool(JObject o, string key)
        {
            var t = o[key];
            return t != null && t.Type == JTokenType.Boolean && (bool)t;
        }

        private static HeroTalentNodeDef Fx(string id, string type, float value)
        {
            return new HeroTalentNodeDef
            {
                Id = id, Name = id, Tier = "tier1", Cost = 1,
                Effect = new HeroTalentEffectDef { Type = type, Value = value },
            };
        }

        private static void AssertApprox(List<string> failures, float actual, float expected, string what)
        {
            if (Mathf.Abs(actual - expected) > 0.001f)
                failures.Add($"{what} — got {actual:0.####}, expected {expected:0.####}");
        }

        /// <summary>Finds a public static float Method(string) on HeroTalentModifiers whose
        /// name contains any of the given fragments (case-insensitive) — the A3 lane's
        /// clamped accessor seam. Returns null when absent (the caller fails loudly).</summary>
        private static MethodInfo FindClampedAccessor(params string[] nameFragments)
        {
            foreach (var m in typeof(HeroTalentModifiers).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.ReturnType != typeof(float)) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(string)) continue;
                string lower = m.Name.ToLowerInvariant();
                foreach (var frag in nameFragments)
                    if (lower.Contains(frag)) return m;
            }
            return null;
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(obj, value);
        }

        private static WisdomCurrencyService WisdomInstance()
        {
            var p = typeof(WisdomCurrencyService).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return p != null ? p.GetValue(null) as WisdomCurrencyService : null;
        }

        private static void SetWisdomInstance(WisdomCurrencyService svc)
        {
            var p = typeof(WisdomCurrencyService).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var setter = p != null ? p.GetSetMethod(true) : null;
            if (setter != null) { setter.Invoke(null, new object[] { svc }); return; }
            var backing = typeof(WisdomCurrencyService).GetField("<Instance>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (backing != null) backing.SetValue(null, svc);
        }
    }
}
