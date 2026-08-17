<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 500 — Weapon & Armor Balance Pass (Knight-first, Elarion)

**Status:** READY — ✅ APPROVED — OWNER RATIFIED 2026-08-14. Being applied to the 65 `blink_*` rows.

> ## ✅ RATIFIED — owner, 2026-08-14: *"approve WO-500 curve and finish the 65"*
> This stops being a proposal. **The curve below is now the authority** the generated rows are graded
> against. Two decisions were made in one breath:
> - **The curve is approved as designed** (TierBaseValue per rarity, the `statWorth` formula, the
>   multiplicative premiums, the `damageMult` 1.0 → 2.4 ladder).
> - **Option A: finish the 65** `blink_*` rows rather than commissioning art for the 31 designed
>   weapons. The 65 already have models *and* icons — as this WO's §0 diagnosed, the missing half was
>   always a spreadsheet, not an art budget.
>
> **Applying this unhides them.** The `"excludeIdPrefixes": ["blink_"]` on the Forge vendor row is
> WO-860 Part B deliberately hiding flat placeholders; once the curve lands, that exclusion is what
> comes off. Before this change **24 of 96 weapons were obtainable** — 72 could not be acquired by any
> non-debug path.
>
> ⚠ **Do not invent numbers for anything this curve does not cover.** If a class or weapon category
> present in the 65 has no rule here, that is a REAL GAP: name it, leave those rows untouched, and
> bring it back to the owner. Filling it with plausible values is how an unratified curve gets shipped
> under a ratified one's name.
>
> ⚠ Every row touched gets `manual: true`, so a future generator pass cannot flatten it back to
> `damageMult: 1.0`.
>
> _Prior status line, preserved: PROPOSAL / DESIGN — READY FOR OWNER REVIEW (not yet READY TO IMPLEMENT)_
**Type:** Balance + creative design (data-only when applied; no code change)
**Author:** design pass (read-only survey + curve design)
**Silo:** Gear data (`weapons.json` / `armor.json`) — no scene, no `.cs`
**Scope:** V1 = solo Knight (swords + shields), then the rest of the family (Ranger bows/daggers,
Mage staves/wands, Cleric maces) on the SAME curve. Pure tuning of existing schema fields.

---

## 0. Why this WO exists

The hand-authored v1 gear in `weapons.json` / `armor.json` already defines a clean rarity curve
(damageMult 1.0 -> 2.4; defense 0.04 -> 0.28). But the GENERATED rows from the gear generators
(Tripo + Blink: ~40 weapons, ~30 armor) are all **flat placeholders** — every one is
`damageMult: 1.0`, `defense: 0.04`, `rarity: "common"`, `req.level: 1`, `buy*: 20/20/20`. That is
the entire owned weapon/armor library sitting at a single power level: no progression, no tension,
no reason to upgrade. This WO supplies a coherent, schema-matching curve so the catalog FEELS like
an RPG ladder and the economy has pull.

This is a DESIGN proposal. Applying it = re-tuning JSON fields + setting `"manual": true` on each
touched row so the generator never reverts it (see §7 "How to apply").

---

## 1. Survey — what exists today (cited)

### 1.1 Schema (from `Assets/_Modules/Village/Hero/GearCatalog.cs`)
- **WeaponDef fields:** `id, name, icon, job ("knight"|"ranger"|"mage"|"cleric"|"any"), category
  ("sword"|"axe"|"bow"|"staff"|"wand"|"dagger"|"hammer"|"shield"), hand ("1h"|"2h"),
  damageType ("melee"|"ranged"|"magic"), rarity, damageMult (float), reach (float m, melee only),
  req{level,dex,arcane,might}, setId, saga, flavor, makersMark, buyWood/buyFood/buyIron/buyCrystals,
  prefabPath, iconPath, loadVia, capabilities, generated, manual`.
- **ArmorDef fields:** same minus weapon-only, plus `weight ("light"|"heavy"|"any"), defense
  (0..0.9 fractional dmg reduction), hpBonus (float), slot`.
- `reach` only matters for MELEE jobs and only overrides `PlayerAttackController.AttackRange`
  (fixed 3.2) when `> 0`. A longer blade outreaches a shorter one. Ranged/magic leave it 0.
- Class-fit: weapons gate 1:1 by `job` (`"any"` fits all). Armor gates by `weight`
  (`GearCatalog.ArmorFitsClass`): light = Ranger/Mage, heavy = Knight/Cleric, `any`/empty = all.

### 1.2 Pricing is DERIVED, not hand-set (from `GearAppraisal.cs` via survey)
Vendor SHOP cost is now **GOLD/Coins**, computed by `GearAppraisal.Appraise()`
(`GearCatalog.GetBuyCost` -> `GoldPrice(estimatedValue)`, floored at 1). The legacy
`buyWood/buyFood/buyIron/buyCrystals` fields are **retained but no longer drive shop cost**
(building UPGRADES still use resources). The gold formula:

```
TierBaseValue:  common 15 | uncommon/"Fine" 40 | rare/"Master" 120 | epic ~ Master | legendary 400
Weapon statWorth = max(0, damageMult - 1.0) * 50  +  max(0, reach - 3.2) * 8
Armor  statWorth = defense * 300  +  hpBonus * 0.5
Premiums (multiplicative): Elarion-marked makersMark x1.25 ; legendary tier x1.5
estimatedValue = round( (TierBase + statWorth) * premiums ),  min 1
```

**Design consequence:** I balance `rarity`, `damageMult`/`defense`/`hpBonus`, `reach`, and
`makersMark`; the GOLD PRICE then falls out automatically and scales with power. The "Predicted
gold" column in every table below is computed from this exact formula so the owner can see the
curve. No price field is hand-typed.

### 1.3 The reference curve (existing hand-authored items — the items I balanced against)
Weapons (`weapons.json`): `knight_starter` 1.0 / `knight_iron` 1.25 / `knight_oath` 1.6 (rare,
Emberhand) / `knight_dawn` 2.1 (epic, Emberhand) / `aegis_emberbrand` 2.4 (legendary, reach 4.8).
Armor (`armor.json`): `armor_cloth` def .04 hp 10 / `armor_leather` .08/25 / `armor_chain` .14/45
(rare, Oathweld) / `armor_plate` .20/75 (epic, Oathweld) / `aegis_plate` .28/100 (legendary).
**This is the north-star ladder. The new tiers slot ONTO it — same multipliers, same level gates,
same makersMark lore (Emberhand=knight steel, Heartwood=ranger bows, Last-Pressing=mage crystal,
Oathweld=armor/cleric).**

### 1.4 VFX hook for on-hit flair
Element/on-hit flair ties to the Spells Pack VFX already referenced by abilities; flair below names
the intended VFX family (ember/frost/storm/holy) so the eventual on-hit hook (a later WO) has a
target. V1 flair is descriptive + lore — the gold formula does NOT yet read it, so it is "free"
power-budget headroom we grant only to rare+ items as a feel reward, not a stat the curve depends on.

---

## 2. The curve (the point of this WO)

Five rarity bands, each a meaningful step. Multipliers match the existing ladder exactly so nothing
re-balances the hero's damage chain:

| Rarity | dmgMult | reach (1h sword) | armor def | armor hp | req.level | Gold band (weapon) | Makers-mark |
|---|---|---|---|---|---|---|---|
| common | 1.00 | 2.8 | 0.04 | 10 | 1 | ~15 | (none) |
| uncommon | 1.25 | 3.2 | 0.08 | 25 | 3 | ~52 | (none) |
| rare | 1.60 | 3.8 | 0.14 | 45 | 6 | ~150 | Emberhand/Heartwood/etc (Elarion mark -> x1.25) |
| epic | 2.10 | 4.4 | 0.20 | 75 | 10 | ~370 | Elarion mark |
| legendary | 2.40 | 4.8 | 0.28 | 100 | 12 | ~840 | Elarion mark + set |

**Rationale:**
- **Power per tier ~ +25-35%** (1.0 -> 1.25 -> 1.6 -> 2.1 -> 2.4): each upgrade is FELT in a hit but
  no tier trivializes the next, so the player keeps buying up.
- **Price scales SUPERLINEARLY with power** (15 -> 52 -> 150 -> 370 -> 840 gold): the top tier is a
  ~56x sink over the floor. Because rare+ adds the Elarion-mark x1.25 and legendary x1.5, the price
  jump OUTRUNS the power jump — classic mobile-RPG tension: you can clearly see the next blade, you
  just can't afford it yet. Gold income (combat drops) gates aspiration.
- **Level gates** (1/3/6/10/12) keep the curve paced to progression — you can't skip to epic at L2
  even with gold.
- **reach** climbs only on melee (sword/axe/hammer) so a Knight's blade visibly outranges as it
  upgrades (and adds a small gold premium past 3.2m via the formula).
- **Armor** stays on the proven .04/.08/.14/.20/.28 + 10/25/45/75/100 ladder; hpBonus is carried
  (v1 applies defense; hp lands when `HeroHealth.maxHp` surgery is safe — see armor `_note`).

---

## 3. WEAPONS — proposed balance

Predicted gold = `round((TierBase + (dmgMult-1)*50 + max(0,reach-3.2)*8) * premiums)`. Items that
already exist + are correctly tuned are listed as REFERENCE (do not touch); NEW/RETUNE rows are the
work.

### 3.1 KNIGHT — swords (V1 PRIORITY; one-handed, melee, makersMark "Emberhand")

| id | name | tier | dmgMult | reach | req.lv | Predicted gold | Flair / notes | Action |
|---|---|---|---|---|---|---|---|---|
| knight_starter | Squire's Blade | common | 1.00 | 2.8 | 1 | 15 | plain steel | REF (exists) |
| knight_iron | Iron Longsword | uncommon | 1.25 | 3.4 | 3 | 53 | tempered edge | REF (exists) |
| knight_oath | Oathkeeper | rare | 1.60 | 4.0 | 6 | round((120+30+6.4)*1.25)=196 | Emberhand, sworn-blade lore | REF (exists) |
| knight_dawn | Dawnbreaker | epic | 2.10 | 4.6 | 10 | round((120+55+11.2)*1.25)=233* | dawn-ember on-hit (ember VFX) | REF (exists) |
| aegis_emberbrand | Emberbrand, the Rekindled | legendary | 2.40 | 4.8 | 12 | round((400+70+12.8)*1.875)=905 | combo finisher: stored aether shock (storm VFX) | REF (exists) |
| tripo_sword_a | Wardens' Edge | common | 1.00 | 2.8 | 1 | 15 | starter loadout blade | RETUNE (set name/reach) |
| tripo_sword_d | Footman's Cut | uncommon | 1.25 | 3.4 | 3 | 53 | guardsman issue | RETUNE -> uncommon |
| tripo_sword_f | Vigil Longsword | rare | 1.60 | 4.0 | 6 | 196 | Emberhand; faint ember trail | RETUNE -> rare + makersMark Emberhand |
| tripo_sword_g | Dawnward Greatblade | epic | 2.10 | 4.6 | 10 | 233 | Emberhand; crit ignites (ember VFX) | RETUNE -> epic + mark |
| blink_sword1h_xx | (Blink 1h swords) | spread common->rare | 1.0/1.25/1.6 | 2.8/3.2/3.8 | 1/3/6 | 15/52/150 | fill the early ladder so the Blink library is shoppable, not flat | RETUNE a graded subset |

*epic Dawnbreaker reads slightly under rare Oathweld in raw gold because the formula's legendary x1.5
only fires at legendary tier; epic gets only the x1.25 mark. That is acceptable (epic's draw is
dmgMult 2.1 vs 1.6, a 31% power jump) but the OWNER may want an "epic x1.35" premium added to
`GearAppraisal.FinishValue` so price strictly tracks tier — flagged in §6.

### 3.2 KNIGHT — shields (off-hand; category "shield"; defense flair via armor-adjacent buff)
Shields carry no damageMult; they are an off-hand defensive item. Until a shield-block stat exists,
model their value as a small `hpBonus`-style ward described in notes (the gold formula reads
weapon stats, so a shield prices at its tier base + any reach=0 -> just tier base).

| id | name | tier | req.lv | Predicted gold | Ward / flair | Action |
|---|---|---|---|---|---|---|
| knight_shield_starter | Squire's Heater | common | 1 | 15 | plain iron-banded | REF (exists) |
| tripo_shield_a | Oakband Heater | common | 1 | 15 | starter | RETUNE name |
| shield_warden | Warden's Kiteshield | uncommon | 3 | 40 | +small block (lore) | NEW/RETUNE Blink shield |
| shield_oath | Oathbearer Bulwark | rare | 6 | 150 | Oathweld; reflects a sliver of damage as Heart ward (holy VFX) | NEW/RETUNE Blink shield |
| shield_aegis | Aegis Wall | epic | 10 | 370 | Oathweld; brief block-shimmer (frost VFX) | NEW/RETUNE Blink shield |

### 3.3 KNIGHT — axes / hammers (2h melee alt-line, "Emberhand")
Two-handed: no off-hand, higher reach, same dmgMult ladder. Gives the Knight a heavy alternative.

| id | name | tier | dmgMult | reach | req.lv | Predicted gold | Flair | Action |
|---|---|---|---|---|---|---|---|---|
| tripo_axe_a | Reaver's Hatchet | common | 1.00 | 3.0 | 1 | 15 | 1h light axe | RETUNE |
| blink_axe2h_g1 | Ironwood Splitter | uncommon | 1.25 | 3.6 | 3 | 56 | wide arc | RETUNE a Blink 2h axe |
| blink_axe2h_g2 | Emberfall Cleaver | rare | 1.60 | 4.2 | 6 | 204 | Emberhand; cleave embers (ember VFX) | RETUNE |
| tripo_hammer_a | Wardstone Maul | epic | 2.10 | 4.6 | 10 | 233 | Emberhand; ground-slam stun flair (storm VFX) | RETUNE -> epic |

### 3.4 RANGER — bows + daggers (ranged/melee; makersMark "Heartwood")
Bows leave reach 0 (range via AbilityDef). Daggers are 1h melee, short reach, fast feel.

| id | name | tier | dmgMult | req.lv | Predicted gold | Flair | Action |
|---|---|---|---|---|---|---|---|
| ranger_starter | Hunter's Shortbow | common | 1.00 | 1 | 15 | — | REF |
| ranger_yew | Yewwood Longbow | uncommon | 1.25 | 3 | 53 | — | REF |
| ranger_storm | Stormrender Bow | rare | 1.60 | 6 | 196 | Heartwood; living wood (storm VFX) | REF |
| ranger_eclipse | Eclipse Recurve | epic | 2.10 | 10 | 233 | Heartwood mark | REF |
| aegis_heartwood_longbow | Heartwood Longbow | legendary | 2.40 | 12 | 905 | charged shot "remembers" last foe | REF |
| tripo_bow_a | Greenwarden Bow | common | 1.00 | 1 | 15 | starter | RETUNE |
| tripo_bow_b | Forester's Recurve | uncommon | 1.25 | 3 | 53 | — | RETUNE |
| tripo_bow_c | Glade Longbow | rare | 1.60 | 6 | 196 | Heartwood; leaf-trail arrows (nature VFX) | RETUNE -> rare |
| tripo_dagger_a | Bramblefang | uncommon | 1.25 | 3 | 53 | fast; bleed flair (lore) | RETUNE |

### 3.5 MAGE — staves + wands (magic; makersMark "Last-Pressing")

| id | name | tier | dmgMult | req.lv | Predicted gold | Flair | Action |
|---|---|---|---|---|---|---|---|
| mage_starter | Apprentice Wand | common | 1.00 | 1 | 15 | — | REF |
| mage_oak | Oakheart Staff | uncommon | 1.25 | 3 | 53 | — | REF |
| mage_arcane | Arcane Scepter | rare | 1.60 | 6 | 196 | Last-Pressing; drinks Heart-light (arcane VFX) | REF |
| mage_void | Voidcaller Staff | epic | 2.10 | 10 | 233 | hums the old note (void VFX) | REF |
| aegis_aetherstaff | Aetherstaff | legendary | 2.40 | 12 | 905 | spells cost less the closer you fight | REF |
| tripo_staff_a..d / tripo_wand_a | Emberglass / Tideglass / Sparkwood / Heartglass / Acolyte's Wand | common->rare spread | 1.0/1.25/1.6 | 1/3/6 | 15/53/196 | grade the 5 placeholders across the band; rare gets Last-Pressing + frost/arcane VFX | RETUNE |
| blink_staff_g1 | Voidpressed Rod | epic | 2.10 | 10 | 233 | Last-Pressing; void motes (void VFX) | RETUNE one Blink staff |

### 3.6 CLERIC — maces (melee, holy; makersMark "Oathweld")

| id | name | tier | dmgMult | reach | req.lv | Predicted gold | Flair | Action |
|---|---|---|---|---|---|---|---|---|
| cleric_starter | Acolyte's Mace | common | 1.00 | 1.8 | 1 | 15 | — | REF |
| cleric_warden | Wardpriest's Mace | uncommon | 1.25 | 2.2 | 3 | 53 | — | NEW/RETUNE |
| cleric_oath | Oathlight Mace | rare | 1.60 | 2.6 | 6 | 196 | Oathweld; heal-on-hit flair (holy VFX) | NEW/RETUNE |
| aegis_hallowed_censer | The Hallowed Censer | legendary | 2.20 | 1.8 | 12 | round((400+60)*1.875)=863 | heals seed a Heart/structure ward | REF |

---

## 4. ARMOR — proposed balance

Predicted gold = `round((TierBase + defense*300 + hpBonus*0.5) * premiums)`. Armor is class-gated by
`weight` (light = Ranger/Mage, heavy = Knight/Cleric, any = all). The Knight wears HEAVY.

### 4.1 Universal / starter ladder (the proven hand-authored set — REFERENCE)

| id | name | tier | weight | defense | hp | req.lv | Predicted gold | Buff / flair | Action |
|---|---|---|---|---|---|---|---|---|---|
| armor_cloth | Wanderer's Cloth | common | any | 0.04 | 10 | 1 | round(15+12+5)=32 | baseline | REF |
| armor_leather | Tanned Leather | uncommon | light | 0.08 | 25 | 3 | round(40+24+12.5)=77 | — | REF |
| armor_chain | Chainmail Vest | rare | heavy | 0.14 | 45 | 6 | round((120+42+22.5)*1.25)=231 | Oathweld; turned a killing blow | REF |
| armor_plate | Elarion Plate | epic | heavy | 0.20 | 75 | 10 | round((120+60+37.5)*1.25)=272 | Oathweld; never wearies | REF |
| aegis_plate | Aegis of Elarion | legendary | any | 0.28 | 100 | 12 | round((400+84+50)*1.875)=1001 | returns damage as Heart ward (set) | REF |

### 4.2 KNIGHT-line heavy armor (grade the flat Blink/Tripo "outfit" placeholders)
All ~30 Blink armor rows are currently `defense 0.04 / hp 10 / common`. Grade a curated subset onto
the ladder so the Knight has a HEAVY progression to buy; leave the rest common as cosmetic-tier fill.

| id (example) | name | tier | weight | defense | hp | req.lv | Predicted gold | Buff / flair | Action |
|---|---|---|---|---|---|---|---|---|---|
| blink_armor_centurion | Centurion Harness | uncommon | heavy | 0.08 | 25 | 3 | 77 | +stamina (lore) | RETUNE |
| blink_armor_lionguard | Lionguard Plate | rare | heavy | 0.14 | 45 | 6 | 231 | Oathweld; +block flair (holy VFX) | RETUNE -> rare + mark |
| blink_armor_dragonhunter | Dragonscale Aegis | epic | heavy | 0.20 | 75 | 10 | 272 | Oathweld; fire-resist flair (ember VFX) | RETUNE -> epic + mark |
| blink_armor_pantherknight | Pantherknight Warplate | epic | heavy | 0.20 | 75 | 10 | 272 | +ability-haste flair (storm VFX) | RETUNE -> epic |
| blink_armor_minotaur / boar / bear / hydra | (heavy variants) | common->uncommon | heavy | 0.04/0.08 | 10/25 | 1/3 | 32/77 | fill the heavy early ladder | RETUNE a graded few |

### 4.3 RANGER/MAGE-line light armor

| id (example) | name | tier | weight | defense | hp | req.lv | Predicted gold | Buff | Action |
|---|---|---|---|---|---|---|---|---|---|
| blink_armor_beasthunter | Beasthunter Garb | uncommon | light | 0.08 | 25 | 3 | 77 | +move flair | RETUNE |
| blink_armor_demonhunter | Demonhunter Leathers | rare | light | 0.14 | 45 | 6 | 231 | Heartwood; +crit flair | RETUNE -> rare + mark |
| blink_armor_savage | Savage Wraps | uncommon | light | 0.08 | 25 | 3 | 77 | +dodge flair | RETUNE |
| blink_armor_engineer | Arcanist's Coat | rare | light | 0.14 | 45 | 6 | 231 | Last-Pressing; +ability-haste | RETUNE -> rare + mark |

---

## 5. The pricing / damage / buff curve — rationale (summary)

1. **Damage/defense ladder is FIXED to the proven hand-authored values** (1.0/1.25/1.6/2.1/2.4 ;
   .04/.08/.14/.20/.28). Nothing re-balances the hero's existing damage chain — the new rows simply
   join it at the right rung. This is the safe, schema-faithful move.
2. **Price (gold) is emergent, not typed** — it comes from `GearAppraisal`, so it always tracks the
   power I set: ~15 -> ~52 -> ~150 -> ~370 -> ~840 (weapons), ~32 -> ~77 -> ~231 -> ~272 -> ~1001
   (armor). Superlinear, because rare+ stack the Elarion-mark (x1.25) and legendary the set premium
   (x1.5). **The economy gets tension: the next tier is always visible and always a stretch.**
3. **Level gates** (1/3/6/10/12) pace the ladder to progression so gold alone can't skip tiers.
4. **MakersMark is canon-consistent** (Emberhand=knight steel, Heartwood=ranger wood,
   Last-Pressing=mage crystal, Oathweld=armor/cleric) AND is a real price lever (the x1.25), so lore
   and economy reinforce each other — an Elarion-marked item is BOTH richer in story and pricier.
5. **Flair (element/on-hit/VFX)** is granted to rare+ as a feel reward; it's descriptive in v1 (no
   stat dependency) so it costs no balance risk, and it pre-targets the Spells Pack VFX families
   (ember/frost/storm/holy/arcane/void) for a later on-hit-VFX WO.
6. **Building tech-tree (WO-432) interplay:** the Forge/Armorer/Arcane WC3-style research raises the
   hero's BASE damage/armor MULTIPLIER independently of the equipped weapon's `damageMult`. The two
   stack (base x talent x level x timing x WEAPON). So this gear curve and the research curve are
   orthogonal levers — gear = the item you hold, research = the village-wide tier buff. Keep them
   tuned so neither dominates: a fully-researched player on a common blade should still want a rare
   blade (and vice-versa).

---

## 6. Open questions for the owner (decide before implement)

1. **Epic gold dip vs rare:** epic items (x1.25 mark only) can price under a legendary but ALSO read
   close to rare because legendary's x1.5 is the only tier multiplier. Add an `epic -> x1.35`
   premium to `GearAppraisal.FinishValue` so price strictly increases per tier? (Tiny `.cs` change,
   separate WO — NOT done here.)
2. **Rarity label mapping:** appraisal tiers are Common/Fine/Master/Legendary; JSON uses
   common/uncommon/rare/epic/legendary. Confirm `epic` maps to Master-base (120) in
   `GearAppraisal.TierBaseValue` (the survey shows epic ~ Master). If `epic` falls through to a
   lower base, epic prices collapse — verify the mapping before relying on the predicted golds.
3. **Shield/off-hand stat:** shields have no defensive STAT today (no block field). Do we (a) keep
   them lore-only for v1, (b) give shields an `hpBonus`/`defense` via a shield-as-armor model, or
   (c) add a `block` field (schema + combat change, separate WO)? Predicted shield golds above
   assume (a) — tier base only.
4. **How many Blink/Tripo rows to grade:** the library is ~40 weapons / ~30 armor. Recommend grading
   a CURATED ladder subset (the named rows above) and leaving the rest common cosmetic-fill, so the
   shop reads as a clean ladder, not 40 identical commons. Confirm the subset size.
5. **hpBonus activation:** armor `hpBonus` is carried but v1 applies defense only (HeroHealth maxHp
   surgery untested). Leave hp as a priced-but-inert stat (current behavior) or schedule the
   maxHp wiring? (Affects whether the hp column is real value or lore.)

---

## 7. How to apply (when promoted to READY TO IMPLEMENT — data-only, CLI executes)

This is JSON tuning of EXISTING rows in two files. No `.cs`, no scene.

1. **Files:** `Assets/Resources/Data/Canonical/weapons.json` and `.../armor.json`. Keep the
   `Assets/StreamingAssets` copies in sync (the `CanonicalJson` law — Resources first, StreamingAssets
   fallback). Verify whether a 3rd copy exists at `Assets/Data/Canonical/` (ITEM_MODEL §6 flags it as
   a dead drift copy — fold or ignore, do NOT diverge it).
2. **Per RETUNE row, set ONLY these fields** (all already in the schema — zero code change):
   - `rarity` (common|uncommon|rare|epic|legendary)
   - `damageMult` (weapons) / `defense` + `hpBonus` (armor) per the §2 ladder
   - `reach` (melee weapons) per the ladder
   - `req.level` (1/3/6/10/12)
   - `name` (the Elarion fantasy name from the tables)
   - `makersMark` (rare+ only: Emberhand/Heartwood/Last-Pressing/Oathweld) — drives the x1.25 gold
     premium AND the appraisal "Elarion-marked" flag
   - `flavor` / `saga` (optional lore line; rare+)
   - `weight` (armor: light|heavy) — already mostly set; confirm Knight-line = heavy
   - **`"manual": true`** on every touched row — CRITICAL: this LOCKS the row so the gear generator
     (`GearCatalogGenerator`, which respects `manual:true` and never overwrites it) cannot revert the
     balance back to the flat template on its next run.
3. **Do NOT set** the gold price — it is derived. Do NOT touch `prefabPath`/`iconPath`/`loadVia`/
   `category`/`hand`/`damageType`/`generated`/`id`/`capabilities` (identity + look + generator linkage).
   Leave the legacy `buyWood/buyFood/buyIron/buyCrystals` as-is (retained, no longer drives shop cost).
4. **Verify after edit:** JSON parses (the catalog loader is graceful but a parse error disables ALL
   gear); run the data regression (`DeNelle.Editor.DataRegression.RunAll` -> `REGRESSION_OK`) which
   asserts the ITEM_MODEL invariants (every Weapon is Carriable+Equippable, etc.); spot-check a few
   predicted golds against `GearAppraisal.Appraise` in a headless dump to confirm the price curve
   lands where §3/§4 predict.
5. **Regression-author** a small assertion (optional, recommended): "no two adjacent rarity tiers
   have equal damageMult/defense" and "rare+ weapons set a makersMark" so a future generator run
   can't silently flatten the ladder again.

---

## 8. Acceptance criteria (when implemented)

- [ ] No Blink/Tripo weapon or armor row remains at the flat `damageMult 1.0 / defense 0.04 / common`
      default UNLESS intentionally left as common cosmetic-fill; the curated ladder subset is graded
      across all five tiers.
- [ ] Every RETUNE row carries `"manual": true` (generator can't revert it).
- [ ] damageMult/defense/hpBonus/reach/req.level match the §2 ladder exactly (no new multipliers
      introduced into the hero damage chain).
- [ ] rare+ rows carry the canon makersMark; predicted gold rises monotonically across tiers within a
      weapon line (subject to the §6.1 epic-premium decision).
- [ ] JSON parses; `REGRESSION_OK`; Resources + StreamingAssets copies in sync.
- [ ] A headless appraisal spot-check confirms the gold curve (~15/52/150/370/840 weapons;
      ~32/77/231/272/1001 armor) within rounding.

---

## 9. What NOT to touch

- No `.cs` files (the appraisal formula tweak in §6.1 is a SEPARATE WO if the owner wants it).
- No scene files; no `prefabPath`/`iconPath`/`loadVia`/`id`/`category`/`hand`/`generated`.
- Do not add new currencies or change building-upgrade resource costs.
- Do not greenfield new items — this WO TUNES existing rows only (the library already exists).
