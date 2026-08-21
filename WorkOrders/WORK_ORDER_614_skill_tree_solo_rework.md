**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-08
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-08) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 614 — Skill Tree Solo Rework (party-buff purge + hot-swap density)

**Status: RULED — READY TO IMPLEMENT (rulings 1, 2, 2a, 3 received; one open detail: T4 signature placement)**
**⭐ OWNER RULING 3 (2026-07-08, verbatim "data only always"):** conversions and this whole pass are
**100% data-only — no new code hooks, ever, as the standing default.** Any proposed effect that
would need a code hook is redesigned to compose from existing effect primitives or dropped.
**⭐ OWNER RULING 1 (2026-07-08, verbatim "new ones bsed on the premuim animations"):** the
replacement/new actives are **NEW signature skills cut from the premium mocap clips** (the $370
suite / Action-Knight set) — clip-first design wins over plain orphan re-wiring; orphan abilities
may still be granted where they fit, but every headline active fires a premium clip.
**⭐ OWNER RULING 2a (2026-07-08, verbatim "and animation on thunderboth or arcan blast"):** the
TIER 1 ranged signature fires the **Thunderbolt or Arcane Blast animation** — implementation cuts
both candidates from the clip inventory (grep the premium sets for thunderbolt/bolt/cast/blast
clips + the existing Arcane Orb VFX from the tower lane for the projectile), wires the
better-feeling one, and presents both in the felt pass if close. Feel-first rule applies: the
metric is the moment reading well in battle, not clip fidelity on paper.
**⭐ OWNER RULING 2 (2026-07-08, verbatim "the ones on right are signature moves set 1 new one each
tier but tiewr 1 i want a ranged attack"):** the bottom-RIGHT rail (HeroLoadout) = the **SIGNATURE
MOVES set — exactly one new signature unlocks per tree tier, and TIER 1's signature is a RANGED
attack.** Q stays the locked basic; the tier signatures fill W/E/R in tier order (T1-ranged → W,
T2 → E, T3 → R). The hot-swap (bottom-middle) bar remains the player-assigned EXTRAS from the
2-actives-per-tier pool. OPEN detail folded into old question 2: does the TIER 4 signature become
the 4th rail action (capstone promoted to a cast, e.g. Eternal Aegis / Champion's Combo), and if so
where does it sit (long-press Q / a 5th pad / hot-swap-only)?
**Lane:** Combat/AI (data + catalog only — polish-phase rule: no re-architecture)
**Owner directive (2026-07-08, verbatim):** *"have creative look at skill tree some make no sense, they offer buffs for party. since pivot we dont have party. we need more items in tree at each level that can be placed in hot swap"*
**Canon resolved by this directive:** the open pin "V1-solo vs ally/summon phasing" (memory `talent-tree-v2-full-design`) is RULED: **V1 is SOLO KNIGHT — party-buff nodes are dead canon.**

**Audited against the REAL data** (not memory):
- Tree: `Assets/Resources/Data/Canonical/hero-talents.json` (v2 — 68 nodes; only KNIGHT + SHARED wired in V1)
- Equip flow: `Assets/_Modules/Village/Hero/AssignableSkillBar.cs` (4 slots) + `Assets/_Modules/Village/Talents/HeroLoadoutVM.cs` (TryAdd/Assign, battle-locked)
- Ability pool: `Assets/Resources/Data/Canonical/abilities.json` (`knight-skills` + `universal-skills`)
- Ability→clip seam: `Assets/Resources/Data/Canonical/weaponskill-animations.json`
- Clip inventory: `Assets/HeroPackages/Knight/Animations/Extracted/` (61 clips) + `Assets/Action/Knight/` (99 fbx, ~90 unused) per `docs/ANIMATION_DOSSIER_2026-07-03.md`

**Owner canon applied throughout:**
- Actives are ANIMATION MOMENTS; purely-numeric effects stay passives; design skills FROM the best clips (memory `actives-are-animation-moments-convert-passives`).
- Ability→animation is bound AT THE SKILL (data row carries its clip key); hot-swap slots inherit it automatically (owner binding rule 07-03, ANIMATION_DOSSIER addendum).
- Every node must logically earn its place; **no meaning encoded in color alone** (owner is red/green colorblind — see §6 UI note).

---

## 1. AUDIT TABLE — every V1-wired node (Knight 20 + Shared 11 = 31 in scope)

Ranger + Mage trees are STORED-NOT-WIRED (effects no-op until V2, per the file's own `_comment`) — audited lightly in §1.3.

### 1.1 Knight tree (Garran — "Bulwark of Elarion")

Verdicts: **KEEP** (solo-valid as-is) / **CONVERT** (party-buff → named solo equivalent) / **REPLACE** (dead in solo — successor proposed in §3).
Party/ally references quoted **verbatim** from the JSON.

| id | Tier | Name | Current effect (from JSON) | Party ref (verbatim) | Verdict |
|---|---|---|---|---|---|
| knight.t1n1 | T1 | Iron Resolve | "+18% damage reduction." passive | — | **KEEP** |
| knight.t1n2 | T1 | Spear Thrust | Unlocks `knight.ranged-poke` (28 dmg @16m, 1.2s cd) — bar-equippable ACTIVE | — | **KEEP** (the only tree active today) |
| knight.t1n3 | T1 | Guardian Stance | "+25% block chance." | — | **KEEP** |
| knight.t1n4 | T1 | Mending Oath | "Mending Salve heals +30%" — modifies an ability NO NODE GRANTS (see §1.4 orphan finding) | — | **KEEP + FIX** — see §3 option T1-A (make it also grant `knight.mending-salve`) |
| knight.t1n5 | T1 | Battle Call | "Defender's Call taunt affects up to 3 enemies in 6m" — taunt targets ENEMIES, fully solo-valid | — | **KEEP** |
| knight.t2n1 | T2 | Aegis Reinforcement | "+30% shield strength" (note: "no absorb system yet — V-later") | — | **KEEP** (flag: currently a no-op — earns its place only when absorb lands; owner may swap it, see §3 note) |
| knight.t2n2 | T2 | Charge Impact | "Charge/Heroic Leap stuns on impact (1.0s stun)" (stun — V-later) | — | **KEEP** |
| knight.t2n3 | T2 | Honored Warden | "Taunt grants allies in 6m -20% damage for 4s." `"ally": true` | **"Taunt grants allies in 6m -20% damage for 4s"** + `"note": "(allies — V2)"` | **REPLACE** → §3 slot R1 |
| knight.t2n4 | T2 | Emberbrand Strike | "Melee attacks burn for 8 dps over 3s" (wired WO-566) | — | **KEEP** |
| knight.t2n5 | T2 | Shield Wall | "Nearby allies in 6m gain +15% block chance." `"ally": true` | **"Nearby allies in 6m gain +15% block chance"** + `"note": "(allies — V2)"` | **REPLACE** → §3 slot R2 |
| knight.t3n1 | T3 | Suppressing Bastion | "Suppressing Volley now taunts every foe it hits" — modifies an ability NO NODE GRANTS (§1.4) | — | **KEEP + FIX** — its target ability must become grantable (§3 slot R3-A closes this loop) |
| knight.t3n2 | T3 | Oathweld Armor | "25% of damage you take heals allies in 6m." `"ally": true` | **"25% of damage you take heals allies in 6m"** + `"note": "(allies — V2)"` | **CONVERT** → §3 C1 (self-lifebond) |
| knight.t3n3 | T3 | Legendary Vanguard | "+35% defense when stationary" (flat in V1) | — | **KEEP** |
| knight.t3n4 | T3 | Retaliation Surge | "Reflect 30% of melee damage back" (wired WO-566) | — | **KEEP** |
| knight.t3n5 | T3 | Bulwark Command | "Allies near you in 6m gain +20% defense." `"ally": true` | **"Allies near you in 6m gain +20% defense"** + `"note": "(allies — V2)"` | **REPLACE** → §3 slot R3 |
| knight.t4n1 | T4 | Eternal Aegis | "Active: 8s of full invulnerability (90s cd)" — kind `active` but carries **NO abilityId** → NOT bar-equippable; fires as auto-emergency (WO-566 note: "owner-flag for player-active") | — | **KEEP + PROMOTE** → §3 C3 (give it an abilityId = player-cast bar active) |
| knight.t4n2 | T4 | Knight Eternal | "Passive +45% defense; allies in 8m take 25% less damage." Self half applied; ally half dead | **"allies in 8m take 25% less damage"** + `"note": "... (allies — V2)"` | **CONVERT** → §3 C2 (drop ally clause, compensate self) |
| knight.t4n3 | T4 | Last Stand | "Below 20% HP: -60% damage taken + reflect 50% for 5s (120s cd)" (wired WO-566) | — | **KEEP** |
| knight.t4n4 | T4 | Holy Retribution | "Taunted enemies burn for 12 dps over 4s" — targets ENEMIES, solo-valid | — | **KEEP** |
| knight.t4n5 | T4 | Elarion's Champion | "Any ability cast grants allies in 8m +15% damage for 4s." `"ally": true` | **"Any ability cast grants allies in 8m +15% damage for 4s"** + `"note": "(allies — V2)"` | **CONVERT or REPLACE** → §3 C4 (owner picks A/B) |

### 1.2 Shared Universal pool (11 nodes)

All self-only → **11 KEEP**, no party references:
Vitality (+25% HP), Resilience (+20% DR), Wisdom Surge, Battle Instinct (+15% crit), Aether Bond, Legendary Resolve (revive — wired WO-566), Swift Recovery, Elarion's Blessing (+10% all), **Arcane Bolt / Mend / Dash** (shared.n9–n11 — the 3 existing bar-equippable universal actives).

### 1.3 Ranger + Mage trees (V2-stored, unwired — no action this WO)

- **Zero `"ally": true` flags** in either tree (verified by read). No party-buff purge needed there.
- **FLAG for the owner (V2 pin, not this WO):** `ranger.t3n4` **Beast Companion** — "Summon a wolf (120 HP, 15 dmg) for 20s". Under the unifying principle (control ONE thing; allies only where autonomy is a feature) an *autonomous* summon may be legal — but it is the exact boundary the solo ruling touches. Park; rule when Ranger wires.

### 1.4 Bonus audit finding — FOUR ORPHANED equippable abilities (cheapest density win)

`abilities.json → knight-skills` contains **5 fully-built, bar-equippable ability defs**, but only ONE is granted by any tree node:

| Ability id | Def (already built + castable) | Granting node |
|---|---|---|
| knight.ranged-poke | Throwing Spear — 28 dmg @16m, 1.2s cd | knight.t1n2 ✓ |
| knight.mending-salve | heals 42, 16s cd | **NONE** (t1n4 modifies it +30% — a buff to a skill you can never have) |
| knight.snare-arrow | 18 dmg + 2.5s slow @14m, 11s cd | **NONE** |
| knight.suppressing-volley | 36 dmg 6m cleave, 20s cd | **NONE** (t3n1 modifies it — same dead modifier) |
| knight.shield-bash | 22 dmg + 2.5s slow @3.6m, 8s cd | **NONE** |

Two KEEP nodes (t1n4, t3n1) currently upgrade abilities the player can never unlock — they literally "make no sense" today, independent of the party issue. §3 wires all four orphans into the tree at **zero new combat code**.

**AUDIT COUNTS: 25 KEEP / 3 CONVERT (t3n2, t4n2, t4n5*) / 3 REPLACE (t2n3, t2n5, t3n5)** — *t4n5 has a REPLACE option B. 6 nodes carry verbatim ally text; 2 more are dead-modifier orphans.

---

## 2. HOT-SWAP DENSITY PLAN

**The bar:** `AssignableSkillBar` — **4 player slots**, filled from SKILL-kind tree nodes (`unlockAbility` + `abilityId`) via `HeroLoadoutVM.TryAdd/Assign`. HeroLoadout Q/W/E/R (class defaults) is separate and untouched.

**Current density — bar-equippable ACTIVES granted per tier:**

| Tier | Nodes | Actives (bar-equippable) | Passives/stat | Notes |
|---|---|---|---|---|
| T1 | 5 | **1** (Spear Thrust) | 4 | |
| T2 | 5 | **0** | 5 (2 dead ally) | |
| T3 | 5 | **0** | 5 (2 dead ally) | |
| T4 | 5 | **0** | 5 (1 dead ally; Eternal Aegis "active" but not equippable) | |
| Shared | 11 | 3 (Arcane Bolt / Mend / Dash) | 8 | tierless |
| **Total** | 31 | **4** | 27 | 4 actives for a 4-slot bar = ZERO choice pressure |

**Convention anchor:** Diablo 2 / WoW-classic style trees hand the player **a new active every 1–2 tiers per tree** (D2: ~1–2 skills per 6-level tier band; WoW classic: an active roughly every other talent row), so by end-tree a player has ~2× more actives than slots — the bar becomes a BUILD decision. We anchor to that: **2 actives per tier**.

**Proposed mix (after this WO):**

| Tier | Actives | Passives | Change |
|---|---|---|---|
| T1 | **2** (Spear Thrust; Mending Salve via t1n4 fix) | 3 | +1 (option T1-A) |
| T2 | **2** (R1 + R2 replace the two ally auras) | 3 | +2 |
| T3 | **2** (R3 + C1-B if chosen; else 1+lifebond passive) | 3–4 | +1..2 |
| T4 | **2** (Eternal Aegis promoted; C4-B if chosen) | 3–4 | +1..2 |
| Shared | 3 | 8 | 0 |
| **Total** | **10–11 actives** | ~21 | vs 4 today — **11 actives for 4 slots = real hot-swap choice at every tier** |

---

## 3. NEW ACTIVE PROPOSALS (owner reacts/selects — 2–3 options per slot)

Cooldown classes per canon: **brief** (<10s, rotational) / **cooldown** (10–30s, tactical) / **big moment** (30s+, signature clip showcase). Every active names its REAL clip (KnightPackage `Assets/HeroPackages/Knight/Animations/Extracted/*.anim` unless noted). All chosen effects stay inside shapes `HeroAbilities.ResolveEffect` already supports (strike / heal / snare / cleave / aoe / blink / dash / knockback / taunt) unless flagged **[NEW CODE]**.

### T1-A — fix the Mending Oath orphan (knight.t1n4)
- **Option A (recommended):** t1n4 becomes a SKILL node **"Mending Salve"** — grants `knight.mending-salve` (heal 42, **cooldown** class 16s). The +30% heal modifier moves onto a renamed t2 passive or is folded in (heal 55 flat). Clip: `Combat_Spell_Two_Hand_Spell_Casting.anim` (two-hand channel reads as binding wounds).
- **Option B:** leave t1n4 as the modifier; grant mending-salve from slot R1 instead (below).

### R1 — replaces knight.t2n3 "Honored Warden" (dead ally aura), Tier 2
- **Option A — "Shield Slam"** (zero new code): grants the orphaned `knight.shield-bash` — 22 dmg + 2.5s slow up close, **brief** 8s cd. Clip: `Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash.anim`. Logic: T2 defense column stays "the shield does things" — now to ENEMIES instead of allies.
- **Option B — "Warden's Roar"**: self-taunt burst — taunts foes in 6m + 10 dmg, **cooldown** 14s. Effect `taunt` (supported). Clip: `Signature_Taunt.anim` (the taunt take exists and is unused — a pure animation moment).
- **Option C — passive fallback:** "+15% block chance for 4s after using any ability" **[NEW CODE — onEvent self-buff]** — only if the owner wants this slot to stay passive.

### R2 — replaces knight.t2n5 "Shield Wall" (dead ally aura), Tier 2
- **Option A — "Pinning Throw"** (zero new code): grants the orphaned `knight.snare-arrow` (rename to **Pinning Spear** for knight flavor) — 18 dmg + 2.5s slow @14m, **cooldown** 11s. Clip: `Combat_Spell_Fireball.anim` (the one-hand hurl gesture reads perfectly as a thrown spear). Logic: extends the T1 Spear Thrust ranged column.
- **Option B — "Sweeping Cut"**: new cleave — 30 dmg to all foes in a 5m arc, **cooldown** 12s. Effect `cleave`. Clip: `Combat_Weapon_WeaponSkill_Outward_Slash.anim`.

### C1 — converts knight.t3n2 "Oathweld Armor" (ally-heal on damage), Tier 3
- **Option A — CONVERT to solo passive:** "Oathweld Armor — 25% of damage you take is returned to you as healing over 5s" (self-lifebond; same fantasy, no allies) **[NEW CODE — small: reuse the WO-566 onEvent hook that already listens to damage-taken for reflect]**.
- **Option B — REPLACE with active "Second Wind"**: heal 35% max HP, **big moment** 45s cd. Effect `heal`. Clip: `Combat_Spell_Two_Hand_Spell_Casting.anim` (or `Signature_Getting_Up.anim` for a grittier read).

### R3 — replaces knight.t3n5 "Bulwark Command" (dead ally aura), Tier 3
- **Option A (recommended — closes the t3n1 orphan loop):** **"Suppressing Volley"** — grants the orphaned `knight.suppressing-volley` (36 dmg 6m cleave, **cooldown** 20s). Then KEEP-node t3n1 "Suppressing Bastion" (volley taunts every foe hit) finally upgrades a skill you can own — two dead nodes fixed with one grant. Clip: `Combat_Weapon_WeaponSkill_Standing_Melee_Attack_360_High.anim` (the 360-high spin IS a volley-in-melee-form).
- **Option B — "Greatsword Arc"**: heavy single-hit — 55 dmg + knockback in a 4m cone, **cooldown** 18s. Effect `knockback` (supported — knight W uses it). Clip: `Combat_Weapon_WeaponSkill_GreatSword_Swing.anim`.

### C2 — converts knight.t4n2 "Knight Eternal" (half-dead capstone), Tier 4
- **Option A (recommended):** drop the ally clause; compensate the self half: "Passive +50% defense" (45→50 to pay for the lost half). Pure JSON edit.
- **Option B:** "+45% defense; taking a hit has a 20% chance to reset Defender's Call's cooldown" **[NEW CODE — proc→cooldown-reset]**.

### C3 — promotes knight.t4n1 "Eternal Aegis" to a real bar active, Tier 4
- **Option A (recommended — the WO-566 note asked for exactly this):** add `"abilityId": "knight.eternal-aegis"` + a knight-skills catalog entry — player-cast 8s invulnerability, **big moment** 90s cd. The auto-emergency behavior flips off when the node's ability is equipped (owner-flag already anticipated). Clip: `Assets/Action/Knight/sword and shield power up.fbx` (already imported — the Cast_e power-up stance reads as raising the aegis). **[SMALL CODE — invuln already wired WO-566; needs a cast entry-point]**
- **Option B:** keep auto-emergency as-is (no bar slot; density target then met via C4-B).

### C4 — converts knight.t4n5 "Elarion's Champion" (dead ally aura), Tier 4
- **Option A — CONVERT to solo passive:** "Any ability cast grants YOU +15% damage for 4s" — same rhythm-reward fantasy, self-target **[NEW CODE — onEvent self-buff, shares the C1/R1-C hook]**.
- **Option B — REPLACE with active "Champion's Combo"**: signature 3-hit combo — 3 × 40 dmg to the nearest foe, **big moment** 30s cd. Effect `strike` (multi-hit via damage staging) or cleave. Clip: `Combat_Weapon_WeaponSkill_Combo.anim` — the combo take is the single best unused clip in the package; a capstone is where it belongs.
- **Option C — REPLACE with active "Dive Roll"**: 6m evasive roll with 0.5s i-frames, **brief** 9s cd. Effect `blink` (supported — universal.dash pattern). Clip: `Passive_Reaction_Running_Dive_Roll.anim`. (Overlaps shared Dash — only pick if the owner wants a superior knight-flavored dodge.)

---

## 4. IMPLEMENTATION NOTES (data + catalog; NO re-architecture)

**Data files that change:**
1. `Assets/Resources/Data/Canonical/hero-talents.json` — knight tree node edits per selections (verdicts §1.1 + options §3). Shared pool untouched. Ranger/Mage untouched. (Mirror to StreamingAssets copy if one exists — dual-copy rule in weaponskill-animations `_comment`.)
2. `Assets/Resources/Data/Canonical/abilities.json` — `knight-skills` pool: rename/add entries for chosen options (e.g. `knight.eternal-aegis`, `knight.sweeping-cut`); the 4 orphans need NO new defs, only tree nodes pointing at them.
3. `Assets/Resources/Data/Canonical/weaponskill-animations.json` — add one row per new/newly-granted active: ability id → trigger + clip key (owner binding rule: animation bound AT THE SKILL; the bar inherits it).

**Already supported (verified in code — no work):**
- Node grant → bar: `HeroTalentCatalog` SKILL-kind nodes carry `abilityId`; `HeroLoadoutVM.TryAdd/Assign` → `AssignableSkillBarAccess` → `AssignableSkillBar` (4 slots, PlayerPrefs-persisted, battle-locked). Proven live by Spear Thrust + shared.n9–n11.
- Equip resolve: `AbilityCatalog.FindById` flat-indexes ALL class pools, so new knight-skills ids resolve with zero code.
- Effect shapes: strike/heal/snare/cleave/aoe/blink/dash/knockback/taunt all resolve in `HeroAbilities.ResolveEffect` today.
- Behavioral passives: burn / reflect / laststand / invuln / revive wired (WO-566, `HeroTalentModifiers`).

**New code only if these options are picked (flagged inline):**
- Self-buff-on-event (R1-C / C1-A / C4-A): one small onEvent handler reusing the WO-566 damage-taken/cast hooks. Bounded; no new systems.
- Player-cast invuln (C3-A): route the existing WO-566 invuln through a castable ability entry-point.
- Everything else is pure JSON.

**What NOT to touch:** `AssignableSkillBar.SlotCount` (stays 4), HeroLoadout Q/W/E/R defaults, ranger/mage trees, the tree panel UI layout, any `.unity` scene, `HeroAbilities` effect shapes beyond the flagged hooks.

**Verification:** CompileGate + DataRegression (talent/ability JSON cross-refs: every `abilityId` in hero-talents.json must resolve via `AbilityCatalog.FindById`; every new active must have a weaponskill-animations row with `clipExists: true`). Headless: unlock node → TryAdd → cast → FlowTrace `[Flow:SkillBar]` + cast line. PO felt-verifies the animation moments.

---

## 5. Acceptance criteria

- [ ] Zero `"ally": true` effects remain in the WIRED (knight + shared) node set.
- [ ] No tree node modifies an ability that no node grants (orphan rule).
- [ ] Every tier of the knight tree grants ≥2 bar-equippable actives (per the §2 mix, as selected).
- [ ] Every new/newly-granted active has a weaponskill-animations.json row citing an EXISTING clip.
- [ ] Total bar-equippable actives ≥ 10 (vs 4 today) so the 4-slot bar is a real choice.
- [ ] DataRegression green on the JSON cross-refs; owner felt-pass on each new animation moment.

## 6. Tree-UI note (colorblind rule — applies to any panel follow-up)

When the tree panel surfaces the new active/passive distinction: encode it by **shape + text badge** (e.g. diamond frame + "ACTIVE — assignable" tag on skill nodes; circle frame for passives), never by color alone. Luminance contrast between locked/unlocked, not hue.

---

## 7. OWNER DECISIONS NEEDED (the load-bearing three)

1. **Replacement philosophy for the 3 dead ally-aura slots:** wire in the 4 already-built orphan abilities (R1-A / R2-A / R3-A + T1-A — zero new combat code, ships fastest) **vs** new signature actives cut from the best unused WeaponSkill clips (Combo / GreatSword Swing / Outward Slash — more new feel, small data+catalog additions). Mix-and-match per slot is fine — react per option letter.
2. **Capstone actives:** promote Eternal Aegis to a player-cast bar active (C3-A), and Champion's Combo as the T4n5 successor (C4-B)? These two are the "big moment" showcase slots — the strongest spends of the unused clip pool.
3. **Convert style for the self-buff fantasies (C1 / C4-A):** accept the one small onEvent self-buff hook (keeps the Oathweld/Champion fantasies alive solo) **vs** stay 100% data-only this WO (pick the active/flat-stat options everywhere). Rules how pure this stays.

(Parked V2 pin, no action: ranger Beast Companion summon vs the no-party spine — §1.3.)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
