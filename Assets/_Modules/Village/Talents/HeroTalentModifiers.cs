// =============================================================================
// HeroTalentModifiers — the talent → ability/defense stat bridge (v2 effects).
// -----------------------------------------------------------------------------
// Reads the live unlocked-node set from WisdomCurrencyService + the per-hero tree
// (+ the Shared Universal pool) from HeroTalentCatalog, then aggregates the v2
// per-node `effect` payloads into the multipliers/fractions the combat code reads:
//
//   DamageMultiplier(class)        = 1 + Σ(damageBonus + allStatsPct)      [HeroAbilities]
//   CooldownMultiplier(class)      = 1 - Σ(cdReduction)                    [HeroAbilities]
//   MaxHpMultiplier(class)         = 1 + Σ(maxHpPct + allStatsPct)         [HeroHealth.MaxHp]
//   IncomingDamageReduction(class) = Σ(damageReduction + defense + allStatsPct)  [HeroHealth.TakeDamage]
//   BlockChance(class) / RollBlock = Σ(blockChance)                        [HeroHealth.TakeDamage]
//   HealAmountMultiplier(class)    = 1 + Σ(modifyAbility where stat=="heal")  [HeroAbilities heal]
//
// V1 WIRING SCOPE (solo Knight north star): only KNIGHT pure-stat + heal-modify +
// the unlockAbility skill nodes are wired (unlockAbility reuses the existing v1
// loadout-equip flow via the node's kind="skill"+abilityId — no extra code here).
// Knight ALLY-dependent effects (auras / Oathweld / Knight-Eternal ally portion /
// Champion) are NO-OP in solo V1 — there are no combat allies — and their SELF
// portion (e.g. Knight Eternal's +45% defense) still applies because it is the
// node's `effect.value`.
//
// WO-566 (effect interpreter): the SELF/DEFENSIVE/COMBAT behavioural handlers are now
// LIVE — this class exposes data-driven queries (ReflectFraction, TryGetLastStand,
// TryGetInvuln, TryGetRevive, ForEachOnHitProc) that the runtime consumers honor:
//   • reflect    → HeroHealth.ApplyReflect      (Retaliation Surge)
//   • laststand  → HeroHealth emergency window   (Last Stand: DR + reflect)
//   • invuln     → HeroHealth auto-emergency     (Eternal Aegis; owner-flag: auto vs player-active)
//   • proc       → PlayerAttackController on-hit  (Emberbrand Strike burn DoT)
//   • revive     → HeroHealth cheat-death once    (shared Legendary Resolve)
// STILL DEFERRED under the owner V1-solo-vs-ally phasing question: ally auras / ally
// onEvent (Oathweld, Champion, Shield Wall, Honored Warden, Bulwark Command, Knight
// Eternal ally portion), summon (Beast Companion), enemy stun (Charge Impact),
// shieldStrength absorb (no absorb system), and the taunt-node param-tuning (the
// Defender's Call / Suppressing Volley taunt ABILITIES already function via
// HeroAbilities.ResolveTaunt; only the node's targets/radius/cd overrides are unread).
// Mage/Ranger are STORED-not-wired: their nodes load as data+icons; their effects
// would only apply if that class were ever the active hero (it is not in V1).
//
// Stateless + defensive: with no service / no tree / no unlocked nodes every method
// returns the identity (1f / 0f), so combat behaves exactly as the pre-talent
// baseline until the player learns a node.
// =============================================================================

using System;
using UnityEngine;

namespace DeNelle.Village.Talents
{
    /// <summary>Translates a hero's unlocked talent nodes into ability/defense stat modifiers.</summary>
    public static class HeroTalentModifiers
    {
        // Sanity clamps.
        private const float MaxDamageMultiplier  = 3f;     // +200% damage ceiling
        private const float MinCooldownMultiplier = 0.4f;  // at most -60% cooldown
        private const float MaxHpMult            = 3f;     // +200% HP ceiling
        private const float MaxIncomingReduction = 0.85f;  // never reduce a hit by more than 85%
        private const float MaxBlock             = 0.85f;  // block chance ceiling

        // ── Offensive (existing consumers in HeroAbilities) ───────────────────────

        public static float DamageMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "damageBonus") + StatSum(heroClass, "allStatsPct") + LegacyDamage(heroClass);
            return Mathf.Clamp(1f + sum, 1f, MaxDamageMultiplier);
        }

        // ── WO-910 cluster 1: plain combat stats (attackSpeed / moveSpeed / critChance) ──
        private const float MaxAttackSpeedMult = 2.0f;   // +100% swings ceiling
        private const float MaxMoveSpeedMult   = 1.75f;  // +75% run ceiling
        private const float MaxCritChance      = 0.50f;  // 50% crit chance ceiling

        /// <summary>1 + Σ(attackSpeed). PlayerAttackController shortens swing cooldown.</summary>
        public static float AttackSpeedMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "attackSpeed");
            return Mathf.Clamp(1f + sum, 1f, MaxAttackSpeedMult);
        }

        /// <summary>1 + Σ(moveSpeed). Multiplied on top of injured slow in HeroLocomotion.</summary>
        public static float MoveSpeedMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "moveSpeed");
            return Mathf.Clamp(1f + sum, 1f, MaxMoveSpeedMult);
        }

        /// <summary>Σ(critChance) additive chance [0..MaxCritChance]. Melee basic attack.</summary>
        public static float CritChanceBonus(string heroClass)
        {
            return Mathf.Clamp(StatSum(heroClass, "critChance"), 0f, MaxCritChance);
        }

        public static float CooldownMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "cdReduction") + LegacyCooldown(heroClass);
            return Mathf.Clamp(1f - sum, MinCooldownMultiplier, 1f);
        }

        // ── Defensive (consumed in HeroHealth) ────────────────────────────────────

        /// <summary>1 + Σ(maxHpPct, allStatsPct). Applied to the hero's effective max HP.</summary>
        public static float MaxHpMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "maxHpPct") + StatSum(heroClass, "allStatsPct");
            return Mathf.Clamp(1f + sum, 1f, MaxHpMult);
        }

        /// <summary>
        /// Fractional reduction (0..0.85) applied to incoming damage: damageReduction
        /// (Iron Resolve, Resilience) + defense (Legendary Vanguard, Knight Eternal self
        /// portion) + allStatsPct. NOTE: Legendary Vanguard's "while stationary" gate is
        /// applied flat in V1 (see node note); Last Stand's conditional low-HP DR is a
        /// V-later behavioural handler and is NOT summed here.
        /// </summary>
        public static float IncomingDamageReduction(string heroClass)
        {
            float sum = StatSum(heroClass, "damageReduction")
                      + StatSum(heroClass, "defense")
                      + StatSum(heroClass, "allStatsPct");
            return Mathf.Clamp(sum, 0f, MaxIncomingReduction);
        }

        /// <summary>Σ(blockChance) clamped to 0..0.85 (Guardian Stance; Battle Instinct is crit, not block).</summary>
        public static float BlockChance(string heroClass)
        {
            return Mathf.Clamp(StatSum(heroClass, "blockChance"), 0f, MaxBlock);
        }

        /// <summary>Rolls a block this hit (full negate). Safe with no nodes (chance 0 → false).</summary>
        public static bool RollBlock(string heroClass)
        {
            float c = BlockChance(heroClass);
            return c > 0f && UnityEngine.Random.value < c;
        }

        // ── Heal modify (Mending Oath) ────────────────────────────────────────────

        /// <summary>1 + Σ(modifyAbility nodes flagged stat=="heal"). Mending Oath = +0.30.</summary>
        public static float HealAmountMultiplier(string heroClass)
        {
            return 1f + SumModifyHeal(heroClass);
        }

        // ── Strategic passives (WO-676 — STEWARD / BULWARK branches) ─────────────
        // Each is Σ(effect.value) over the new HeroTalentEffectTypes keys, clamped to a
        // sane band (G2). Consumers (EchoService / ResourceCollector / BuildTimerService /
        // repair-sell paths / towers / walls — lanes A1/A2) call ONE of these at their
        // existing choke point (`?.`-safe, identity when nothing is learned).

        private const float MaxHarvestRateBonus   = 1f;     // +100% harvest ceiling
        private const float MaxCollectorCapBonus  = 2f;     // +200% pending-capacity ceiling
        private const float MaxCostReduction      = 0.75f;  // repair/build-time discounts floor at 25% of base
        private const float MaxSalvageBonus       = 0.5f;   // +50% refund ceiling
        private const float MaxWaveRewardBonus    = 1f;     // +100% wave-reward ceiling
        private const float MaxTowerDamageBonus   = 1f;     // +100% tower damage ceiling
        private const float MaxTowerRangeMeters   = 10f;    // +10m tower range ceiling
        private const float MaxStructureToughness = 0.5f;   // structures never shrug more than 50% (G2 cap)
        private const float MaxTowerAttackSpeed   = 1f;     // +100% fire-rate ceiling
        private const float MaxHealthRegenBonus   = 1f;     // +100% HP-regen ceiling
        private const float MaxManaRegenBonus     = 1f;     // +100% mana-regen ceiling

        /// <summary>+fraction echo/collector harvest rate (Provider's Bond). 0 baseline.</summary>
        public static float HarvestRateBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.HarvestRate), 0f, MaxHarvestRateBonus);

        /// <summary>+fraction collector pending-capacity (Deep Reserves). 0 baseline.</summary>
        public static float CollectorCapBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.CollectorCap), 0f, MaxCollectorCapBonus);

        /// <summary>Fraction OFF repair prices (Master Mason). Consumer: cost * (1 - this). 0 baseline.</summary>
        public static float RepairCostReduction(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.RepairCost), 0f, MaxCostReduction);

        /// <summary>Fraction OFF build/upgrade timer durations (Foreman's Pace). Consumer: secs * (1 - this). 0 baseline.</summary>
        public static float BuildTimeReduction(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.BuildTime), 0f, MaxCostReduction);

        /// <summary>+fraction refunded on structure sell/loss (Salvager). 0 baseline.</summary>
        public static float SalvageBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.Salvage), 0f, MaxSalvageBonus);

        /// <summary>+fraction wave rewards (Bountiful Banners). 0 baseline.</summary>
        public static float WaveRewardBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.WaveReward), 0f, MaxWaveRewardBonus);

        /// <summary>+fraction tower damage (Keen Ballistics). 0 baseline.</summary>
        public static float TowerDamageBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.TowerDamage), 0f, MaxTowerDamageBonus);

        /// <summary>+METERS of tower range (Farsight Emplacements). Additive meters, not a fraction. 0 baseline.</summary>
        public static float TowerRangeBonusMeters(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.TowerRange), 0f, MaxTowerRangeMeters);

        /// <summary>+fraction tower fire rate (Standing Orders). 0 baseline.</summary>
        public static float TowerAttackSpeedBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, HeroTalentEffectTypes.TowerAttackSpeed), 0f, MaxTowerAttackSpeed);

        // ── Regen bonuses (WO-676 G3 wire-or-hide — shared.n7 / shared.n5) ───────
        // Literal-string keys (like "reflect" above): these predate the WO-676
        // HeroTalentEffectTypes constants — they are in the HeroTalentCatalog.cs:34
        // declared vocabulary, not the strategic-branch block.

        /// <summary>+fraction hero HP regen per tick (Swift Recovery, shared.n7 — the
        /// town-footprint / out-of-combat regen paths route through HeroHealth.RegenTick).
        /// 0 baseline.</summary>
        public static float HealthRegenBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, "healthRegen"), 0f, MaxHealthRegenBonus);

        /// <summary>+fraction mana regen (Aether Bond, shared.n5 — consumed by the
        /// HeroAbilities per-second regen tick). 0 baseline.</summary>
        public static float ManaRegenBonus(string heroClass)
            => Mathf.Clamp(StatSum(heroClass, "manaRegen"), 0f, MaxManaRegenBonus);

        /// <summary>
        /// Fraction OFF damage defensive structures take (walls/gates/towers intake read):
        /// Σ(structureToughness, always-on — Hardened Ramparts) + Σ(structureToughnessWave —
        /// Warden of Elarion, added ONLY while <paramref name="waveActive"/>; the consumer
        /// passes WaveManager's live-wave state). Clamped to 0..0.5 (G2 cap) so stacked
        /// toughness never trivializes a raid. 0 baseline.
        /// </summary>
        public static float StructureToughnessReduction(string heroClass, bool waveActive)
        {
            float sum = StatSum(heroClass, HeroTalentEffectTypes.StructureToughness);
            if (waveActive)
                sum += StatSum(heroClass, HeroTalentEffectTypes.StructureToughnessWave);
            return Mathf.Clamp(sum, 0f, MaxStructureToughness);
        }

        // =====================================================================
        //  CATHEDRAL OF MAGIC - building-tier MAGE modifiers (WO-861 Phase 3)
        // ---------------------------------------------------------------------
        //  NOT talents: these come from the CITY (building-tiers.json arcane-tower ->
        //  ModifierService.Active). They live here because this class is already THE
        //  hero-stat bridge every combat consumer reads, and because the MAGE-ONLY
        //  gate must exist in exactly ONE place.
        //
        //  KNIGHT / RANGER ARE PROVABLY UNAFFECTED: every accessor below returns the
        //  identity unless IsMage(heroClass) - a single early-out in ActiveMageMods -
        //  and every consumer reads the modifier ONLY through these accessors (nothing
        //  in the hero path touches GameModifiers.Mage* directly). The Cleric already
        //  resolves to the "mage" slug in HeroAbilities.Awake (it is a caster and casts
        //  the mage kit), so it is deliberately included.
        //
        //  IDENTITY WHEN UNBUILT: a Cathedral at tier 0 contributes nothing, so Active
        //  carries the defaults (mults 1, bonuses 0, unlockSpell "") and every accessor
        //  returns 1f / 0f / "" - byte-identical behaviour to before Phase 3.
        // =====================================================================

        private const string MageSlug = "mage";

        // Sanity bands (same spirit as the talent clamps above): the city can buff the
        // mage hard, but a mis-authored tier can never produce an absurd stat.
        private const float MaxMageSpellPower   = 3f;    // +200% spell damage ceiling
        private const float MaxMageManaRegen    = 3f;    // +200% mana regen ceiling
        private const float MinMageManaCost     = 0.25f; // costs never fall below 25% of authored
        private const float MaxMageManaCost     = 2f;    // and a "penalty" tier can never double past 2x
        private const float MaxMageShell        = 3f;    // Arcane Shell never more than 3x its authored pct
        private const float MaxMageManaMaxBonus = 50f;   // absolute mana added on top of the ~10 pool
        private const float MaxMageHpBonusPct   = 2f;    // +200% max-HP ceiling

        private static bool s_mageModsWarned;

        // PER-FRAME CACHE. ModifierService.Active deliberately has no cache of its own - it
        // RECOMPUTES (and allocates a fresh GameModifiers) on every read so it can never be
        // stale. The mage folds read it several times per frame (mana cap, regen, cost, spell
        // power), so cache the result for the current frame and drop it the instant the service
        // signals a change (tier bought / override set or cleared). Worst case staleness is one
        // frame, and only for a mutation that bypasses ModifierService.Recompute.
        private static DeNelle.Core.State.GameModifiers s_mageModsCache;
        private static int s_mageModsFrame = int.MinValue;
        private static bool s_modifierHookAttached;

        private static void InvalidateMageMods()
        {
            s_mageModsCache = null;
            s_mageModsFrame = int.MinValue;
        }

        /// <summary>True when <paramref name="heroClass"/> is the MAGE (Thrain). The Cleric already
        /// normalises to the "mage" slug upstream, so it is covered. Knight/Ranger are false.</summary>
        public static bool IsMage(string heroClass)
            => !string.IsNullOrEmpty(heroClass)
               && heroClass.Trim().Equals(MageSlug, StringComparison.OrdinalIgnoreCase);

        /// <summary>The active city modifier contract, but ONLY for the mage - null for every other
        /// class (the single gate) and null if the service is unavailable. One try/catch choke point
        /// so a catalog/state fault degrades to identity with a logged warning, never a thrown cast.</summary>
        private static DeNelle.Core.State.GameModifiers ActiveMageMods(string heroClass)
        {
            if (!IsMage(heroClass)) return null;   // <- the ONE gate: Knight/Ranger never get past here
            int frame = Time.frameCount;
            if (s_mageModsCache != null && s_mageModsFrame == frame) return s_mageModsCache;
            try
            {
                if (!s_modifierHookAttached)
                {
                    s_modifierHookAttached = true;
                    DeNelle.Core.State.ModifierService.Changed += InvalidateMageMods;
                }
                s_mageModsCache = DeNelle.Core.State.ModifierService.Active;
                s_mageModsFrame = frame;
                return s_mageModsCache;
            }
            catch (Exception ex)
            {
                InvalidateMageMods();
                if (!s_mageModsWarned)
                {
                    s_mageModsWarned = true;
                    Debug.LogWarning("[HeroTalentModifiers] Cathedral modifiers unavailable, using identity: "
                                     + ex.GetType().Name + ": " + ex.Message);
                }
                return null;
            }
        }

        /// <summary>A multiplier read guarded so a missing/zero value is IDENTITY.</summary>
        private static float Mult(float raw, float min, float max)
            => raw > 0f ? Mathf.Clamp(raw, min, max) : 1f;

        /// <summary>Cathedral spell-power multiplier applied to MAGE outgoing ability damage. 1 baseline.</summary>
        public static float MageSpellPowerMultiplier(string heroClass)
        {
            var m = ActiveMageMods(heroClass);
            return m == null ? 1f : Mult(m.MageSpellPowerMult, 1f / MaxMageSpellPower, MaxMageSpellPower);
        }

        /// <summary>Cathedral mana-regen multiplier (stacks multiplicatively with the Aether Sprite
        /// perk and the Aether Bond talent). 1 baseline.</summary>
        public static float MageManaRegenMultiplier(string heroClass)
        {
            var m = ActiveMageMods(heroClass);
            return m == null ? 1f : Mult(m.MageManaRegenMult, 1f / MaxMageManaRegen, MaxMageManaRegen);
        }

        /// <summary>Cathedral mana-COST multiplier (0.85 = costs 15% less). 1 baseline.
        /// WO-910: also folds talent <c>manaCostReduction</c> (additive fraction off cost).</summary>
        public static float MageManaCostMultiplier(string heroClass)
        {
            var m = ActiveMageMods(heroClass);
            float cathedral = m == null ? 1f : Mult(m.MageManaCostMult, MinMageManaCost, MaxMageManaCost);
            float talentCut = Mathf.Clamp(StatSum(heroClass, "manaCostReduction"), 0f, 0.5f);
            // Upper bound is MaxMageManaCost, NOT 1f: the documented band is [0.25 .. 2.0]
            // (see the consts above — "a 'penalty' tier can never double past 2x"). A 1f cap
            // silently flattened any future cost-PENALTY state to baseline while the comment
            // still promised the penalty tier (review fba0b1079..0e4690036 finding #7).
            return Mathf.Clamp(cathedral * (1f - talentCut), MinMageManaCost, MaxMageManaCost);
        }

        /// <summary>1 + Σ(range). Melee/ability reach scale (WO-910).</summary>
        public static float RangeMultiplier(string heroClass)
        {
            float sum = StatSum(heroClass, "range");
            return Mathf.Clamp(1f + sum, 1f, 1.75f);
        }

        /// <summary>Σ(dodge) chance to fully ignore a hit [0..0.5] (WO-910).</summary>
        public static float DodgeChance(string heroClass)
        {
            return Mathf.Clamp(StatSum(heroClass, "dodge"), 0f, 0.5f);
        }

        /// <summary>Cathedral Arcane Shell strength multiplier, applied to the shield's REDUCTION
        /// PERCENT before it opens the mitigation window. 1 baseline.</summary>
        public static float MageShellStrengthMultiplier(string heroClass)
        {
            var m = ActiveMageMods(heroClass);
            return m == null ? 1f : Mult(m.MageShellStrengthMult, 1f / MaxMageShell, MaxMageShell);
        }

        /// <summary>ADDITIVE max-mana the Cathedral grants the mage (absolute units, not a fraction).
        /// 0 baseline. Negative authored values are floored at 0 so a bad tier can never shrink the pool.</summary>
        public static float MageManaMaxBonus(string heroClass)
        {
            var m = ActiveMageMods(heroClass);
            return m == null ? 0f : Mathf.Clamp(m.MageManaMax, 0f, MaxMageManaMaxBonus);
        }

        /// <summary>ADDITIVE fraction of the mage's BASE max HP the Cathedral grants (0.10 = +10%).
        /// 0 baseline. Consumed by HeroHealth's effective-max composition.</summary>
        public static float MageMaxHpBonusPct(string heroClass)
        {
            var m = ActiveMageMods(heroClass);
            return m == null ? 0f : Mathf.Clamp(m.MageHpBonusPct, 0f, MaxMageHpBonusPct);
        }

        /// <summary>The raw comma-separated abilities.json ids the Cathedral has made learnable for
        /// the mage (union across tiers/perks, built by ModifierService.Apply). "" baseline.</summary>
        public static string MageUnlockedSpellsCsv(string heroClass)
        {
            var m = ActiveMageMods(heroClass);
            return m == null || string.IsNullOrWhiteSpace(m.UnlockSpell) ? string.Empty : m.UnlockSpell;
        }

        /// <summary>True when the Cathedral of Magic has unlocked <paramref name="abilityId"/> for this
        /// hero. Reuses <see cref="AbilityListContains"/> - the SAME comma-separated-ids parser the
        /// talent modifyAbility riders use. False for every non-mage class and for an unbuilt Cathedral.</summary>
        public static bool IsSpellUnlockedByBuilding(string heroClass, string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            return AbilityListContains(MageUnlockedSpellsCsv(heroClass), abilityId);
        }

        /// <summary>Visits every ability id the Cathedral has unlocked for this hero (trimmed, in
        /// authored order). No-op for a non-mage / unbuilt Cathedral, so callers need no class check.</summary>
        public static void ForEachBuildingUnlockedSpell(string heroClass, Action<string> visit)
        {
            if (visit == null) return;
            string csv = MageUnlockedSpellsCsv(heroClass);
            if (string.IsNullOrEmpty(csv)) return;
            foreach (var raw in csv.Split(','))
            {
                string id = raw.Trim();
                if (id.Length > 0) visit(id);
            }
        }

        // ── Generic stat accessor (also exposes not-yet-wired stats: critChance,
        //    attackSpeed, manaRegen, manaCostReduction, healthRegen, moveSpeed,
        //    range, dodge, shieldStrength, wisdomPerLevel) for future consumers. ─────

        /// <summary>Σ(effect.value) over unlocked nodes (hero tree + shared) whose effect.type matches.</summary>
        public static float StatSum(string heroClass, string effectType)
        {
            if (string.IsNullOrEmpty(effectType)) return 0f;
            float acc = 0f;
            ForEachUnlocked(heroClass, n =>
            {
                if (n.Effect != null && string.Equals(n.Effect.Type, effectType, StringComparison.OrdinalIgnoreCase))
                    acc += n.Effect.Value;
            });
            return acc;
        }

        // ── Behavioural effect queries (WO-566 — V1 effect interpreter) ───────────
        // Data-driven readers the runtime consumers (HeroHealth / PlayerAttackController)
        // use to honor the SELF/DEFENSIVE/COMBAT behavioural effect types that StatSum
        // can't express as a flat multiplier: reflect, laststand, invuln, revive, proc.
        // Each returns the IDENTITY (0 / false / empty) until a matching node is learned,
        // so combat is unchanged at baseline. ALLY-dependent types (aura / ally-flagged
        // onEvent / summon) are deliberately NOT read here — deferred under the owner's
        // V1-solo-vs-ally phasing question (see WorkOrders/WORK_ORDER_566_*).

        /// <summary>Σ(reflect) over unlocked "reflect"-type nodes (Retaliation Surge = 0.30).
        /// Fraction of incoming melee damage bounced back to the attacker. 0 baseline.</summary>
        public static float ReflectFraction(string heroClass)
        {
            return Mathf.Clamp01(StatSum(heroClass, "reflect"));
        }

        /// <summary>One on-hit proc (Knight Emberbrand Strike burn; ranger Poison Tip bleed):
        /// a DoT applied to a struck enemy. Carries dps (effect.value), duration, and chance
        /// (1 when unauthored). A readonly struct so the per-swing query allocates nothing.</summary>
        public readonly struct ProcSpec
        {
            public readonly string NodeId;
            public readonly float Dps;
            public readonly float Duration;
            public readonly float Chance;
            public ProcSpec(string nodeId, float dps, float duration, float chance)
            { NodeId = nodeId; Dps = dps; Duration = duration; Chance = Mathf.Clamp01(chance); }
            public bool IsValid => Dps > 0f && Duration > 0f;
        }

        /// <summary>Invoke <paramref name="visit"/> for every unlocked "proc"-type node describing
        /// a SELF on-hit DoT (dps + duration). Skips ally-flagged procs (none today). Data-driven
        /// so a ranger/mage proc reuses it verbatim when those classes go live in V2.</summary>
        public static void ForEachOnHitProc(string heroClass, Action<ProcSpec> visit)
        {
            if (visit == null) return;
            ForEachUnlocked(heroClass, n =>
            {
                var e = n.Effect;
                if (e == null) return;
                if (!string.Equals(e.Type, "proc", StringComparison.OrdinalIgnoreCase)) return;
                if (e.Ally) return;                               // ally proc — deferred (V2)
                float chance = e.Chance > 0f ? e.Chance : 1f;     // unauthored chance = always
                var spec = new ProcSpec(n.Id, e.Value, e.Duration, chance);
                if (spec.IsValid) visit(spec);
            });
        }

        /// <summary>Last Stand capstone: below the HP threshold, gain extra damage reduction +
        /// reflect for a window, on cooldown. Returns false (all zero) when not learned.</summary>
        public static bool TryGetLastStand(string heroClass, out float threshold, out float damageReduction,
                                           out float reflect, out float duration, out float cooldown)
        {
            threshold = damageReduction = reflect = duration = cooldown = 0f;
            var e = FirstUnlockedEffect(heroClass, "laststand");
            if (e == null) return false;
            threshold       = e.Threshold > 0f ? e.Threshold : 0.2f;
            damageReduction = Mathf.Clamp01(e.Value);
            reflect         = Mathf.Clamp01(e.Reflect);
            duration        = e.Duration > 0f ? e.Duration : 5f;
            cooldown        = e.Cooldown  > 0f ? e.Cooldown  : 120f;
            return true;
        }

        /// <summary>Eternal Aegis capstone: a window of full invulnerability on a long cooldown.
        /// Returns false when not learned.</summary>
        public static bool TryGetInvuln(string heroClass, out float duration, out float cooldown)
        {
            duration = cooldown = 0f;
            var e = FirstUnlockedEffect(heroClass, "invuln");
            if (e == null) return false;
            duration = e.Duration > 0f ? e.Duration : 8f;
            cooldown = e.Cooldown  > 0f ? e.Cooldown  : 90f;
            return true;
        }

        /// <summary>Legendary Resolve (shared): revive once per run at a fraction of max HP.
        /// Returns false (0) when not learned.</summary>
        public static bool TryGetRevive(string heroClass, out float hpFraction)
        {
            hpFraction = 0f;
            var e = FirstUnlockedEffect(heroClass, "revive");
            if (e == null) return false;
            hpFraction = Mathf.Clamp(e.Value <= 0f ? 0.4f : e.Value, 0.05f, 1f);
            return true;
        }

        /// <summary>
        /// WO-676: an ability-targeted DoT RIDER — a passive node whose effect is
        /// <c>modifyAbility</c> aimed at one or more ability ids (effect.ability, comma-
        /// separated) and carrying dps (value) + duration (+ optional stack cap in targets).
        /// Mirrors the Emberbrand proc read (ForEachOnHitProc) but keyed by the ABILITY the
        /// hero just cast rather than the melee swing:
        ///   • Holy Retribution — ability "knight.wardens-roar", stat empty  → taunt-burn.
        ///   • Venombrand       — ability "knight.thunderbolt,knight.ranged-poke",
        ///                        stat "poison" → poison rider (5 dps / 6s, stacks to 2).
        /// <paramref name="stat"/>: null/empty matches an UNSET effect.stat only (so the
        /// heal-modify nodes never collide); otherwise exact (case-insensitive) match.
        /// Returns false (identity) until a matching node is learned.
        /// </summary>
        public static bool TryGetAbilityDotRider(string heroClass, string abilityId, string stat,
                                                 out float dps, out float duration, out int maxStacks)
        {
            dps = duration = 0f;
            maxStacks = 1;
            if (string.IsNullOrEmpty(abilityId)) return false;

            HeroTalentEffectDef found = null;
            ForEachUnlocked(heroClass, n =>
            {
                if (found != null) return;
                var e = n.Effect;
                if (e == null) return;
                if (!string.Equals(e.Type, "modifyAbility", StringComparison.OrdinalIgnoreCase)) return;
                // Stat discriminator: empty wanted-stat matches only an empty authored stat.
                bool statMatch = string.IsNullOrEmpty(stat)
                    ? string.IsNullOrEmpty(e.Stat)
                    : string.Equals(e.Stat, stat, StringComparison.OrdinalIgnoreCase);
                if (!statMatch) return;
                if (!AbilityListContains(e.Ability, abilityId)) return;
                found = e;
            });
            if (found == null || found.Value <= 0f) return false;

            dps       = found.Value;
            duration  = found.Duration > 0f ? found.Duration : 4f;
            maxStacks = found.Targets > 0 ? found.Targets : 1;
            return true;
        }

        /// <summary>True when the comma-separated <paramref name="csv"/> ability list contains <paramref name="abilityId"/>.
        /// PUBLIC since WO-861 Phase 3: the Cathedral of Magic's <c>unlockSpell</c> uses the SAME
        /// comma-separated-ids convention, and it reuses this parser rather than inventing a second one.</summary>
        public static bool AbilityListContains(string csv, string abilityId)
        {
            if (string.IsNullOrEmpty(csv)) return false;
            foreach (var part in csv.Split(','))
                if (string.Equals(part.Trim(), abilityId, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>The effect payload of the FIRST unlocked node (hero tree + shared) whose
        /// effect.type matches, or null. Used by the single-instance behavioural capstones.</summary>
        public static HeroTalentEffectDef FirstUnlockedEffect(string heroClass, string effectType)
        {
            if (string.IsNullOrEmpty(effectType)) return null;
            HeroTalentEffectDef found = null;
            ForEachUnlocked(heroClass, n =>
            {
                if (found != null) return;
                if (n.Effect != null && string.Equals(n.Effect.Type, effectType, StringComparison.OrdinalIgnoreCase))
                    found = n.Effect;
            });
            return found;
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        private static float SumModifyHeal(string heroClass)
        {
            float acc = 0f;
            ForEachUnlocked(heroClass, n =>
            {
                if (n.Effect != null
                    && string.Equals(n.Effect.Type, "modifyAbility", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(n.Effect.Stat, "heal", StringComparison.OrdinalIgnoreCase))
                    acc += n.Effect.Value;
            });
            return acc;
        }

        // Legacy v1 fields (top-level damageBonus / cdReduction on a node). v2 data
        // drives effects through the `effect` object, but reading these keeps any
        // remaining v1-shaped node additive (0 when absent → no behaviour change).
        private static float LegacyDamage(string heroClass)
        {
            float acc = 0f;
            ForEachUnlocked(heroClass, n => acc += n.DamageBonus);
            return acc;
        }

        private static float LegacyCooldown(string heroClass)
        {
            float acc = 0f;
            ForEachUnlocked(heroClass, n => acc += n.CdReduction);
            return acc;
        }

        /// <summary>
        /// Invokes <paramref name="visit"/> for every UNLOCKED node belonging to the hero's
        /// tree (id prefix "&lt;class&gt;.") OR the Shared Universal pool (id prefix "shared.").
        /// </summary>
        private static void ForEachUnlocked(string heroClass, Action<HeroTalentNodeDef> visit)
        {
            if (string.IsNullOrEmpty(heroClass) || visit == null) return;

            var service = WisdomCurrencyService.Instance;
            if (service == null) return;
            var unlocked = service.Unlocked;
            if (unlocked == null || unlocked.Count == 0) return;

            string slug = heroClass.Trim().ToLowerInvariant();

            // Hero tree.
            var tree = HeroTalentCatalog.GetTree(slug);
            if (tree != null && tree.Nodes != null)
            {
                foreach (var node in tree.Nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                    if (service.IsUnlocked(node.Id)) visit(node);
                }
            }

            // Shared Universal pool (applies to whichever hero learned them).
            var shared = HeroTalentCatalog.SharedNodes;
            if (shared != null)
            {
                foreach (var node in shared)
                {
                    if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                    if (service.IsUnlocked(node.Id)) visit(node);
                }
            }
        }
    }
}
