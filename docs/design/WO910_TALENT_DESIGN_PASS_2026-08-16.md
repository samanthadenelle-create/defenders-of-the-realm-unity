# WO-910 Talent Design Pass -- Mage + Ranger (morning review, 2026-08-16)

**THIS IS A PROPOSAL. Nothing here is decided -- the owner rules on every row.**
Answer the five questions at the bottom and the whole remainder becomes implementable
child WOs. Read time target: five minutes.

Provenance: read-only design-research agent, sourced entirely from the working tree
(branch `wip/village2-and-f8-tickets`) on 2026-08-15. Every claim cites file:line.
ASCII only by rule.

---

## 1. Current truth (re-derived -- the WO body's counts are STALE; do not use them)

| Fact | Number | Source |
|---|---|---|
| Dead nodes TODAY (tracked debt) | **24 -- MAGE 13 + RANGER 11.** Mage is now the LARGER debt. | `Assets/Editor/Regression/TalentStrategyRegression.cs:239-275` (KnownDeadNodeBaseline -- shrink-only, so it IS the truth); counted at `:299` |
| Capstone rows | **Mage tier-4 is dead IN FULL (all five: t4n1-t4n5). Ranger tier-4 has 4 of 5 dead** (only t4n2 Windstrider Legend lives). | baseline ids `TalentStrategyRegression.cs:249-274` |
| Confirmed by latest gate run | 24/24 hit, gate green | `Builds/data-regression-mana.log:18873` -- "shipped nodes checked: 81; hidden/gated-off: 2; tracked debt (WO-910): 24/24"; `:18876` TALENT_STRATEGY_OK |
| Nodes with `"hidden": true` | **2**, both SHARED pool (shared.n3 Wisdom Surge, shared.n4 Battle Instinct). **Zero ranger/mage nodes are hidden.** | `Assets/Resources/Data/Canonical/hero-talents.json:1408`, `:1427` |
| Wired by the path-B work since the WO was written | **7 nodes**: ranger.t1n1 attackSpeed, t1n2 Hunter's Mark, t2n1 moveSpeed, t2n3 critChance, t3n3 dodge, t4n2 moveSpeed; mage.t3n3 manaCostReduction | prune comments `TalentStrategyRegression.cs:241-242, :260`; consumers registered `:142-147` |
| Mage tree state | 7 wired / 13 dead / 0 hidden (of 20) | baseline ids `TalentStrategyRegression.cs:255-274` vs `hero-talents.json:1004-1362` |
| Ranger tree state | 9 wired / 11 dead / 0 hidden (of 20) | baseline ids `TalentStrategyRegression.cs:249-265` vs `hero-talents.json:634-996` |
| WO header status | "READY -- PARTIAL path B -- stats + Hunter's Mark LIVE" | `WorkOrders/WORK_ORDER_910_ranger_mage_talent_consumers.md:3` |
| Owner ruling in force | "B -- all the way, full design" -- no hiding, no minimum spine | `WORK_ORDER_910_...md:11-13` |

**Do not mis-reconcile the log:** the `[G3] no dead nodes (implemented-consumer registry)`
line at `Builds/data-regression-mana.log:18872` is the HEADER of a different sub-check
(the implemented-consumer registry gate), NOT a claim that zero nodes are dead. The
authoritative debt count is the next line (`:18873`, "tracked debt (WO-910): 24/24").
The WO body's older per-class counts predate the path-B pruning and must not be quoted.

Note two mage ids (t4n2, t4n3) were wrongly pruned in cluster 2 and RESTORED -- their
descriptions still promise halves nothing implements (`TalentStrategyRegression.cs:243-248`).

### The headline finding of this pass

**All seven "(NEW ability - stub)" abilities ALREADY EXIST in abilities.json, and every
effect shape they use is implemented.** ranger.tumble-step / multishot / precision-strike /
storm-of-arrows are authored at `Assets/Resources/Data/Canonical/abilities.json:649,664,679,694`;
mage.void-rift / cataclysm at `:533,564`. Their effect shapes -- blink, cleave, strike, aoe
(with freeze), meteor -- all resolve in code (`Assets/_Modules/Village/Hero/AbilityCatalog.cs:164-169`,
`Assets/_Modules/Village/Hero/HeroAbilities.cs:954-965`; frost-nova at `abilities.json:481-498`
is the shipped precedent for aoe+freeze). For these nodes the remaining debt is:
headless-verify the cast, clear the stale stub note (both json copies), prune the baseline id
in the same commit. VFX fields are deliberately empty pending owner tags
(`abilities.json:479` and `:632` comments -- CLI never creative-picks a VFX key).

### Live consumers (data-only wiring is possible against these)

Registered with citations at `TalentStrategyRegression.cs:120-168`: damageBonus, cdReduction,
maxHpPct, damageReduction/defense, blockChance, allStatsPct, reflect, laststand, invuln,
revive, **proc (on-hit DoT -- `HeroTalentModifiers.ForEachOnHitProc`, HeroTalentModifiers.cs:468)**,
unlockAbility (equip flow), healthRegen, manaRegen, attackSpeed, moveSpeed, critChance,
range, dodge, manaCostReduction, modifyAbility:heal, **modifyAbility:poison (the Venombrand
rider -- `HeroTalentModifiers.TryGetAbilityDotRider`, HeroTalentModifiers.cs:535)**.

Class identities respected below: mage = pool/regen/cost economy (Mana 24, Cathedral scaling --
`HeroTalentModifiers.cs:229-260`); ranger = mark/mobility/weaving (Focus on-hit economy).
Kit taxonomy = the ten mini-kits already sketched at
`docs/DESIGN_SUGGESTIONS_OPEN_TICKETS_2026-08-15.md:66-81`; no new taxonomy invented.

---

## 2. Mage -- 13 dead nodes, tier-4 dead in full (json lines from `Assets/Resources/Data/Canonical/hero-talents.json`)

| Node id | Name | Tier | State | Proposed effect | Wiring cost | WC3/CoC rationale |
|---|---|---|---|---|---|---|
| mage.t1n5 (:1066) | Rune Binding | 1 | dead (modifyAbility, note "chain (V2)") | Arcane Bolt chains to 1 extra foe at 40% (set stat:"chain") | [NEEDS CONSUMER] the SAME chain resolve as ranger.t4n5 -- build once, serve both | Chain Lightning starter; makes the Q feel mage-y from tier 1 |
| mage.t2n1 (:1082) | Aether Surge | 2 | dead (onEvent) | Economy kit: +3 mana on kill (payload authored :1092) | [NEEDS CONSUMER] small onKill hook where the mana pool lives (`HeroAbilities.cs`) | Dark Ritual / siphon; feeds the Mana-24 economy identity directly (kit pitch :77) |
| mage.t2n3 (:1119) | Arcane Shield | 2 | dead (shieldStrength) | Shell kit: fold `StatSum("shieldStrength")` into `MageShellStrengthMultiplier` (`HeroTalentModifiers.cs:365`) + the 2s duration bonus at the shell consumer | [NEEDS CONSUMER] but TINY -- the Cathedral shell multiplier already exists; one talent fold + one duration read | Mana Shield up-rank; city-buff and talent stack on the same knob (kit pitch :78) |
| mage.t2n4 (:1139) | Flame Mastery | 2 | dead (modifyAbility, empty stat) | Radius rider: +35% AoE radius (set stat:"radius"). **DATA BUG: targets `mage.fireball`, which does not exist in abilities.json (:481-627) -- re-point to a live spell (owner picks: mage.meteor? mage.thunder?)** | [NEEDS CONSUMER] radius-rider read at the aoe/meteor resolve (`HeroAbilities.cs`) -- shared with Cataclysm Prep | Bigger booms = the WC3 AoE-mastery talent archetype |
| mage.t3n1 (:1178) | Cataclysm Prep | 3 | dead (modifyAbility, empty stat) | Same radius rider: mage.meteor radius +60% (target exists, `abilities.json:611`) | [NEEDS CONSUMER] shared radius rider (one consumer, two nodes) | The "prep" node that makes the capstone visibly larger -- CoC-style upgrade telegraphing |
| mage.t3n2 (:1197) | Spell Echo | 3 | dead (proc, note "duplicate (V2)") | Echo kit: 25% chance a cast fires twice (kit pitch :79) | [NEEDS CONSUMER] one recast hook in the `HeroAbilities` cast pipeline -- the SAME consumer serves t4n5 | Grand Magus double-cast; the mage's slot-machine moment |
| mage.t3n4 (:1232) | Runic Overload | 3 | dead (onEvent active) | Active: +60% spell damage for 6s, 45s cd (payload authored :1241-1244) -- a timed fold into DamageMultiplier | [NEEDS CONSUMER] timed-buff active in `HeroAbilities.cs`. Data-only FALLBACK if deferred: retype to flat damageBonus +0.20 | Avatar-style cooldown steroid; press-button-feel-power |
| mage.t3n5 (:1252) | Void Rift | 3 | dead (stub note; ability exists :533, effect=aoe+freeze -- frost-nova precedent :481) | Ship as authored: 40 dmg + 1.5s hold in 5m, 18s cd | **Data-only** (clear note + verify + prune) | The Rift kit opener (kit pitch :76); control + damage = mage tier 3 |
| mage.t4n1 (:1271) | Cataclysm | 4 | dead (stub note; ability exists :564, effect=meteor -- implemented) | Ship the capstone as authored: 600 dmg over 9m, 50s cd | **Data-only** (clear note + verify + prune; Hovl VFX keys owner-tagged later per kit pitch :76) | The ultimate the whole tree climbs toward; gives mage a reachable capstone NOW |
| mage.t4n2 (:1290) | Aetherweaver Ascension | 4 | dead (damageBonus half wired; "+25% effect" half not -- restore note `TalentStrategyRegression.cs:243-248`) | Option A (data-only): reword description to "+25% spell damage", drop note. Option B: define "effect" = +25% to rider values/durations | A = **data-only**; B = [NEEDS CONSUMER] rider-magnitude fold in `HeroTalentModifiers.cs` | Simple honest capstone stat beats a vague promise; CoC never ships undefined text |
| mage.t4n3 (:1308) | Eternal Arcana | 4 | dead (damageBonus half wired; "+40% mana regen" half not) | The manaRegen consumer EXISTS (`HeroTalentModifiers.cs:211`). Option A (data-only): reword to spell power only. Option B: small split-payload fold so one node grants both | A = **data-only**; B = [NEEDS CONSUMER] tiny -- secondary-stat read in `HeroTalentModifiers.cs` | B is the better capstone (power + economy = the whole mage identity in one node) |
| mage.t4n4 (:1326) | Reality Rift | 4 | dead (onEvent active) | Rift kit: 30 dps ground zone, 6m/6s, 40s cd | [NEEDS CONSUMER] persistent-zone helper (`HeroAbilities.cs` + a DoT-zone component). Data-only FALLBACK: retype to unlockAbility with an aoe def (single 120-dmg burst) | Flame Strike / Death and Decay; zone control is the missing mage verb |
| mage.t4n5 (:1346) | Elarion's Legacy | 4 | dead (proc, note "duplicate (V2)") | Echo kit: 20% chance any spell auto-recasts -- same consumer as Spell Echo | [NEEDS CONSUMER] shared with t3n2 (build once) | Echo capstone; two nodes, one consumer = best value-per-line in the mage list |

---

## 3. Ranger -- 11 dead nodes (4 of 5 tier-4 dead)

| Node id | Name | Tier | State | Proposed effect | Wiring cost | WC3/CoC rationale |
|---|---|---|---|---|---|---|
| ranger.t1n3 (:667) | Tumble Step | 1 | dead (stub note; ability exists `abilities.json:649`, effect=blink) | Ship as authored: 6m roll, 8s cd (Mobility kit). The promised 0.4s dodge window: reword away, OR add a brief post-cast DodgeChance window | **Data-only** to ship the roll (clear note + verify + prune). i-frame window = [NEEDS CONSUMER] small rider in `HeroAbilities.ResolveBlink` | Wind Walk-lite escape; cheap tier-1 utility like a CoC early unlock |
| ranger.t1n5 (:699) | Arrow Storm Prep | 1 | dead (stub note; ability exists :664, effect=cleave) | Ship Multishot as authored: 3 arrows, 18 dmg each in 8m spread | **Data-only** (clear note + verify + prune; VFX tag owner-held) | The classic ranger multishot (Sylvanas/Drow trope); tier-1 taste of the Storm capstone |
| ranger.t2n4 (:772) | Deep Freeze | 2 | dead (modifyAbility:slow -- no slow-rider consumer) | Venom kit: Rimeshot ammo rider deepens to -50% move / 4s (payload already authored :784-785) | [NEEDS CONSUMER] slow-rider read + apply at the ranged basic-attack ammo path -- `PlayerAttackController.cs` (+ enemy slow; freeze precedent exists via aoe) | Frost Arrows orb (Drow Ranger); kiting = the ranger fantasy |
| ranger.t2n5 (:793) | Shadow Veil | 2 | dead (stealth -- no system) | Ghost kit: Tumble Step grants 2s stealth (opacity + enemy de-aggro radius shrink, per kit pitch :74) | [NEEDS CONSUMER] one small stealth handler -- suggest new `Assets/_Modules/Village/Hero/HeroStealth.cs` (serves t4n3 too) | Wind Walk; pairs mobility with the Ghost identity |
| ranger.t3n2 (:831) | Emberhead | 3 | dead (modifyAbility:burn -- key unregistered) | Venom kit: Emberhead ammo burn rider 9 dps / 6s (payload authored :842-844). `TryGetAbilityDotRider` already reads any stat generically | [NEEDS CONSUMER] one call keyed stat "burn" at the ammo path -- same shape as the registered poison rider (`TalentStrategyRegression.cs:163`) | Searing Arrows; mirror of the shipped Knight Venombrand, zero new concepts |
| ranger.t3n4 (:870) | Beast Companion | 3 | dead (summon -- no system) | Beast kit: summon the ICE WOLF (FTUE guide body reuse, kit pitch :75) -- 120 HP / 15 dmg / 20s / 60s cd | [NEEDS CONSUMER] own child WO -- new summon handler (suggest `Assets/_Modules/Village/Hero/HeroSummon.cs`), reuses wolf rig/leash | Literally the WC3 Beastmaster; biggest feature, LAST in order |
| ranger.t3n5 (:889) | Precision Strike | 3 | dead (stub note; ability exists :679, effect=strike) | Ship as authored: 90 dmg aimed shot @ 18m, 12s cd | **Data-only** (clear note + verify + prune) | Aimed Shot single-target nuke; the payoff for the DPS column |
| ranger.t4n1 (:908) | Storm of Arrows | 4 | dead (stub note; ability exists :694, effect=aoe) | Ship the capstone as authored: 120 dmg over 8m rain, 60s cd | **Data-only** (clear note + verify + prune; VFX tag owner-held, troop-archer VFX later per kit pitch :72) | Starfall-class ultimate; gives ranger a reachable capstone NOW |
| ranger.t4n3 (:944) | Phantom Hunter | 4 | dead (stealth proc) | Ghost kit capstone: first shot from stealth +50% damage | [NEEDS CONSUMER] same `HeroStealth.cs` handler as Shadow Veil (one flag read at attack resolve) | Ambush/backstab payoff; makes Ghost a build, not a gimmick |
| ranger.t4n4 (:962) | Nature's Fury | 4 | dead (onEvent -- unregistered) | **Retype `onEvent` -> `proc`** keeping value 14 / duration 5: `ForEachOnHitProc` already applies on-hit DoTs (`HeroTalentModifiers.cs:468`, registry :137) | **Data-only** (retype in both json copies + prune) | Poison Sting always-on orb; a permanent on-hit DoT is pure WC3 orb design |
| ranger.t4n5 (:981) | Elarion's Arrow | 4 | dead (modifyAbility with empty stat, note "pierce/chain (V2)") | Chain rider: arrows chain to 1 extra foe at 50% (set stat:"chain") -- SHARED consumer with mage Rune Binding | [NEEDS CONSUMER] chain resolve in projectile/strike path (`HeroAbilities.cs`); one consumer serves both classes | Moon Glaives / Chain Lightning; signature capstone feel |

Reachability note: hiding is NOT proposed anywhere, per the owner's B ruling. (For the
record: hiding ranger.t2n5 would orphan t3n5, whose only prerequisite it is --
`hero-talents.json:903-905`.)

---

## 4. Rule in one pass -- five questions

Answering these five unlocks implementing all 24 nodes as child WOs under the WO-910 umbrella.

1. **Ship the near-data-only batch now?** Six ability nodes whose abilities already exist and
   run (mage.t3n5, t4n1; ranger.t1n3, t1n5, t3n5, t4n1) + the ranger.t4n4 retype
   (onEvent -> proc): clear stub notes, headless-verify each cast, prune baseline ids,
   VFX held for your tags. Revives 7 nodes and puts a live capstone in BOTH dead tier-4
   rows this week. **Y/N**
2. **Approve the rider cluster (Venom + radius) as one child WO?** Emberhead burn rider,
   Deep Freeze slow rider, Flame Mastery + Cataclysm Prep radius riders (4 nodes, ~3 small
   consumers, all mirroring the shipped Venombrand shape). **And: Flame Mastery targets the
   nonexistent `mage.fireball` -- re-point it to which live spell?** (mage.meteor / mage.thunder / other)
3. **Approve the Ghost kit as one child WO?** Shadow Veil + Phantom Hunter behind a single
   new `HeroStealth.cs` consumer (stealth opacity + de-aggro + first-shot bonus). **Y/N**
4. **Mage capstone text rulings:** Aetherweaver Ascension -- reword to damage-only (A) or
   define the "+25% effect" half (B)? Eternal Arcana -- reword (A) or wire the +40% mana
   regen half via the existing ManaRegenBonus consumer (B, recommended)? **A/B each**
5. **Approve the feature order for the remaining coded kits?** Proposed (extends the kit
   doc's own order, `DESIGN_SUGGESTIONS_OPEN_TICKETS_2026-08-15.md:81`):
   Mage Echo (1 consumer, 2 nodes) -> chain rider (1 consumer, 2 nodes, both classes) ->
   Aether Surge onKill -> Arcane Shield fold -> Runic Overload -> Reality Rift zone ->
   Beast Companion last (biggest, own WO, wolf-art reuse). **Y/N/reorder**

---

*Implementation law reminders for whoever executes the ruling: both hero-talents.json copies
byte-identical (G1); every wire adds its TalentConsumerRegistry citation and prunes its
baseline id in the SAME commit (`TalentStrategyRegression.cs:97-115, :231-237`);
`HiddenTrees` stays empty forever; VFX keys are owner-tagged, never CLI-picked.*
