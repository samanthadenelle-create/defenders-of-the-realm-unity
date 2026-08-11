# WORK ORDER 1019 — Thrain (Mage) action bar: stale hero-switch binding + the single-target spell kit

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-10 (UI seat) — provenance stack bumped 1019 → 1020 in the same edit
**Lane:** HUD action bar (binding) + abilities data (kit). Two parts: **A = defect**, **B = design**.
**Provenance:** owner felt-test 2026-08-10, verbatim: *"can you review the default values for thrain in
actionbar? He should have all magic spells and he inherits the hotswap from previous character and has
nothing explicit for dps"* · *"as Mage you try to lure out and kill one at a time"* · *"Should use a
po[iso]n spell"* · *"drain (steal health)"* · *"fireball"* · *"thunder"*.
**Identity confirmed at source:** `en.json` → `hero.mage.name = Thrain`. (Also `hero.knight = Grom`,
`hero.ranger = Sylas`, `hero.cleric = Elara`.) The dungeon capture agrees: `avatar=MageAvatar
controller=Mage`; the equipment screen shows Thrain with an Emberglass Staff.

---

## PART A — THE DEFECT: the bar does not rebind on hero switch

**⚠ The authored data is NOT the problem — verify this before touching `abilities.json`.**
`Assets/StreamingAssets/Data/Canonical/abilities.json` → `classes.mage.abilities` already defines a
complete, all-magic default bar:

| Slot | Id | Name | Note |
|---|---|---|---|
| Q | `mage.fireball` | Fireball | 30 dmg @14m, 0.6s cd — **this IS the explicit DPS** |
| W | `mage.shell` | Arcane Shell | −40% damage taken, 4s |
| E | `mage.heal` | Mend | self-heal 45 HP |
| R | `mage.meteor` | Meteor Strike | ultimate, 260 dmg, 9m blast |

So "he inherits the hotswap from the previous character and has nothing explicit for dps" describes a
**runtime binding failure**: on hero switch the action bar keeps the PREVIOUS hero's slot bindings
instead of resetting to the newly-selected hero's class defaults. Fireball exists and is authored to Q —
the player just is not being given it.

**Fix approach (§12 — instrument first, do not guess):**
1. Trace the bind path: hero-select → class resolve → `classes.<class>.abilities` load → slot bind →
   HUD render. Log the class it resolved, the ids it bound per slot, and whether the bind was a RESET or
   a carry-over. Read that trace before editing.
2. **Prime hypothesis to TEST:** slot bindings are persisted (hotswap/loadout state in save) and are
   restored on load WITHOUT validating that the bound ability belongs to the current hero's class — so a
   previous hero's bar survives the switch.
3. **The rule to implement:** a bound ability that is not valid for the active hero's class is DROPPED
   and replaced by that class's default for the slot. A hero must never present another class's kit.
   Player-chosen swaps WITHIN their class persist normally.
4. Sweep all four heroes (Grom/Sylas/Thrain/Elara), including switching back and forth.

**Also check while in there:** earlier captures show bar slots rendering identical generic
crossed-hammer icons — verify whether unbound/failed slots fall back to a placeholder icon, and make an
unbound slot say so rather than showing a plausible-looking wrong icon (no silent failures, §12).

## PART B — THE KIT: the Mage's single-target pull-and-kill fantasy (owner ruling)

Owner's stated playstyle: ***"as Mage you try to lure out and kill one at a time."*** The kit must
support **pull → soften → finish → sustain**, not crowd control. Owner named four spells:

| Spell | Exists today? | Role in the fantasy |
|---|---|---|
| **Fireball** | ✅ `mage.fireball` (Q default) | the pull + the primary nuke |
| **Poison** | ❌ **does not exist** | damage-over-time — start the kill before it reaches you |
| **Drain (steal health)** | ❌ **does not exist** | sustain — the solo-pull loop's survivability |
| **Thunder** | ❌ **does not exist** | burst finisher |

The existing learnable pool (`classes.mage-skills.abilities`) holds: `frost-nova`, `arcane-bolt`,
`manaweave`, `void-rift`, `blink`, `cataclysm` — no poison, no drain, no thunder.

**Author the three missing spells** into the mage pool, same contract as the existing entries
(`id`, `slot`, `key`, `name`, `description`, `icon`, `castSeconds`, cooldown, damage/heal fields —
match the shape of a neighbouring entry exactly; do NOT invent new fields):
- `mage.poison` — single-target DoT.
- `mage.drain` — single-target damage that heals the caster for a portion (the "steal health" verb).
- `mage.thunder` — single-target burst.

⚠ **Numbers are an OWNER RULING, not CLI's to invent.** Draft values, mark them
`<<DRAFT — owner tuning pass>>`, and bounce for sign-off. Do not silently set damage/cooldown/mana.

### ⚠ OWNER RULING 2026-08-10 — THE DEFAULT BAR CHANGES: **E = Drain, replacing Mend**

Verbatim: *"change mend to drain."* The Mage's default bar becomes:

| Slot | Was | **NOW** |
|---|---|---|
| Q | Fireball | **Fireball** (unchanged — the pull + primary nuke) |
| W | Arcane Shell | **Arcane Shell** (unchanged) |
| **E** | Mend (flat 45 HP self-heal) | **`mage.drain`** — damage a single target, heal the caster for a portion |
| **R** | Meteor Strike (260 dmg, 9m blast) | **`mage.poison`** — owner ruling 2026-08-10: *"make meteor strike into poison"* |

**Why this is the right call (and worth stating so nobody "restores" Mend later):** the Mage's sustain
now comes FROM fighting rather than from pausing to heal. That is exactly the *"lure out and kill one at
a time"* loop — pull with Fireball, trade with Drain, stay alive by winning the trade. A flat self-heal
rewards disengaging; Drain rewards committing to the single-target kill. It also makes the class's
survivability scale with its offence instead of sitting on a fixed number.

**Implementation notes:**
- `mage.drain` is authored in the POOL (above) **and** referenced as the mage class default for slot `e`
  in `classes.mage.abilities` — one id, two references; do not duplicate the definition.
- **`mage.heal` (Mend) is NOT deleted** — it moves to the learnable pool (`classes.mage-skills`) so it
  stays available via progression and any existing unlock/talent reference to `mage.heal` keeps
  resolving. Removing the id outright would break those references; **check for referrers before
  moving** and report any found.
- ⚠ **Existing saves:** a mage who already has `mage.heal` bound to E must not break. Per Part A's rule,
  bindings valid for the class persist — Mend remains class-valid, so an existing player keeps it until
  they swap. NEW mages get Drain. Confirm this is the owner's intent if it comes up; do not force-migrate
  live bars without a ruling.
- Drain's numbers remain a `<<DRAFT — owner tuning pass>>`, but note it is now a DEFAULT (available from
  minute one, no unlock), so its values must be balanced as a starter ability, not as a late-pool spell.

**Only THUNDER stays learnable** (pool only) — it enters through the Cathedral of Magic / talent unlocks
like the rest of the pool.

**`mage.poison` is the R ULTIMATE — balance it as one.** R is the ultimate slot (Meteor was 260 dmg /
42s cd), so poison here is a **heavy, ultimate-scale damage-over-time**, not a light tick: the fight-ending
commitment in the pull-and-kill loop. Numbers stay `<<DRAFT — owner tuning pass>>`. **Meteor Strike is
NOT deleted** — same treatment as Mend: `mage.meteor` moves to the learnable pool (which already holds
`cataclysm` as an R-slot ultimate), so existing unlock/talent references keep resolving; check referrers
before moving.

**Resulting default bar:** Q Fireball · W Arcane Shell · E Drain · R Poison — all magic, single-target
first, sustain from fighting, an ultimate that finishes over time.

## PART C — TALENTS (owner ruling 2026-08-10)

### C1. Mage: a talent that gives Fireball SPLASH damage
Owner: *"could we add one of the skills in his tree be add sp[l]ash damage to fireball?"* — **yes, and
the tree already has the exact pattern to copy:** `mage.t2n4 "Flame Mastery"` is a `passive` whose
effect is `modifyAbility` on `mage.fireball` (value 0.35). The splash node is a sibling of that — same
`kind`/`effect` shape, new `stat` (e.g. an AoE-radius rider), so **no new effect machinery is needed**.

- Author it as a **tier-3 mage node** (tier 2 already holds Flame Mastery; splash is the escalation),
  `kind: "passive"`, `effect.type: "modifyAbility"`, `effect.ability: "mage.fireball"`, prerequisite
  `mage.t2n4` — so it reads as *Flame Mastery → splash* and the tree tells a story.
- **Design note worth stating out loud:** splash pulls AGAINST the ruled Mage fantasy
  (*"lure out and kill one at a time"*). That is a FEATURE if it is a **choice, not a default** — the
  single-target purist keeps a tight, safe pull; the splash-taker trades safety for clearing power and
  must handle the extra aggro they just woke. Keep it a paid node deep enough that the player has
  already learned the pull loop. Do NOT make Fireball splash by default.
- Radius / damage-falloff / whether splash can pull additional enemies are `<<DRAFT — owner tuning>>`.
  ⚠ Flag explicitly: if splash generates aggro on neighbours, say so in the node description — a talent
  that silently breaks the player's pulling strategy is a trap, not a choice.

### C2. Sylas (Ranger) — "need some for Sylas too"
Owner: *"need some for Sylas too."* **Scope check done at source** — node counts today:
`knight 21 · mage 20 · ranger 20 · cleric 0`. So the Ranger tree is NOT thin in count; existing nodes
include Quick Draw, Hunter's Mark, Tumble Step, Venomcraft, Deep Freeze, Shadow Veil, Bloodbound Draw,
Emberhead, Beast Companion, Precision Strike.

⚠ **AMBIGUOUS — bounce to the owner before authoring (§11: never work an unclear ticket blind).** Two
readings: (a) add NEW ranger talents (but the tree is already at parity with mage/knight — what gap?),
or (b) give Sylas an ability-modifying node equivalent to the Mage's splash-Fireball, i.e. a signature
upgrade to his primary. **CLI: ask which, and for the fantasy Sylas should express** (the Mage's is
"lure and kill one at a time" — the Ranger's has not been ruled).
**Separately and NOT ambiguous: `cleric` has ZERO talent nodes** — Elara has no tree at all. That is a
real gap regardless of the Sylas question; flag it to the owner as its own decision (author a cleric
tree / is Elara playable yet?).

### C3. Sylas must ANIMATE WITH HIS BOW — the purchased mocap is unused (VERIFIED DEFECT)
Owner: *"we need to make sure that ranger can use bow mo cap"* · *"there is a motion for bow and arrow I
purchased just for him."*

**Confirmed at source — this is a real, unambiguous defect (unlike C2):**
```
Assets/Resources/Heroes/Ranger.controller  ->  grep -c "BowShot"  =  0
states present: Attack, Base, Block, Cast, CastUpper, CastVariant, Combo, Dead, Death, Hit, ...
```
**Sylas's animator contains ZERO bow animations.** He is firing with the generic melee/cast states — the
archer plays a swordsman. Bow motion assets DO exist in-tree and are simply not wired:
**✅ OWNER SUPPLIED THE ASSET 2026-08-10 — no guessing needed:**
```
C:\Users\Elden\Downloads\Actorcore-Unity-0811-261931\Motion\archery-shotaway.fbx
C:\Users\Elden\Downloads\Actorcore-Unity-0811-261931\Motion\archery-shotaway.json
```
This is an **ActorCore / Reallusion** motion. **Import it into the repo first** (it is currently only in
Downloads — outside the project); suggested home `Assets/Action/Archery/` or alongside the existing
`Assets/Action/` pack. ⚠ The pack as delivered contains **exactly ONE motion** (shot-away) — no separate
draw / aim-hold / idle-with-bow clips. See the gap note below.

**⚠⚠ THE LOAD-BEARING TECHNICAL FACT — read before importing.** The companion `.json` declares:
```
"Generation": "RL_CC3_Plus"      bones: CC_Base_Hip, CC_Base_Spine01, CC_Base_L_Upperarm, CC_Base_R_Hand, ...
```
That is the **Reallusion CC3+ skeleton**, whose bone names are **NOT** the Mixamo names the game's heroes
use (the live capture shows `clips=[mixamo.com(...)]`, `avatar=MageAvatar`). Therefore:
- **Import the FBX as `Animation Type: Humanoid`** so Unity maps `CC_Base_*` → the humanoid muscle rig,
  which is what makes it retarget onto Sylas. **Imported as Generic it will silently not play** — and a
  clip that does not play is exactly the failure this WO exists to fix. Verify the avatar's bone mapping
  in the importer (CC3+ usually maps cleanly, but CONFIRM — do not assume).
- The `.json` is Reallusion **metadata** (physics collision capsules + facial expression bone poses). It
  is **NOT animation data and must not be imported as such** — it is useful only as evidence of the rig
  generation. Keep it beside the FBX for provenance; wire nothing from it.
- After retarget, check for the classic CC3→Mixamo artifacts: wrong root/hip height, arm twist offsets,
  and hand orientation on the bow grip. Fix via avatar/muscle settings, not by editing the FBX.

**GAP — one clip is not a bow kit (flag to the owner):** `archery-shotaway` covers the RELEASE. A
convincing archer also needs **draw**, **aim-hold** (the loop while targeting), and **idle/run carrying
the bow**. Options: (a) ship shot-away now on the attack state and leave idle/locomotion generic —
partial improvement, still reads odd at rest; (b) owner supplies the matching ActorCore archery motions
(same pack family, so same rig — cleanest); (c) fill gaps from the existing generic
`Assets/Blink/Art/Animations/Combat/BowShot.fbx`, accepting a style mismatch. **Recommend (b)** — one
consistent motion source. **Owner: which?**

**Work:** wire the confirmed bow motions into `Ranger.controller` — draw / aim-hold / release / idle-with-bow,
and the locomotion variants if the pack has them — so Sylas reads as an archer in idle, movement and
attack. Verify retarget onto the shared rig (canon: every KayKit humanoid shares Rig_Medium/Rig_Large, so
one retarget drives the cast — confirm Sylas's avatar matches before assuming).
**⚠ Coordinate with WO-1016:** that WO is fixing hero locomotion/velocity feeding the animator. Do NOT
diagnose "the bow animation does not play" independently until WO-1016 lands — a dead speed parameter
would mask correct bow wiring. Sequence: WO-1016 first, then verify C3.

**Acceptance (C3):** `Ranger.controller` references the owner-confirmed bow motions; Sylas visibly draws
and looses a bow on attack (capture-proven, not eyeballed); no generic sword swing remains on his attack
path; his idle/run read as bow-carrying.

### The Poison VFX — the owner's "green bubbles" prefab (do NOT substitute)

Owner: *"there is a beautiful vfx prefab for poison used in a dungeon"* · *"it was green bubbles."*
Candidates found in-tree — **CLI must NOT choose; the owner tags the key** (standing rule, memory
`vfx-map-owner-tags-no-creative-pick`):

| Candidate | Path | Note |
|---|---|---|
| **`Fog_poison`** ← strongest match | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_poison.prefab` | Already used in-game and code-cited: `EnemyAuraVFX.cs:240` — *"Aura_Necromancer (Lana Fog_poison, authored saturated green)"*. Green, bubbling/fogging, and it is the one that appears in dungeon necromancer content. |
| `Fire_cartoon_poison` | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Fire/Fire_cartoon_poison.prefab` | The other Lana poison effect — greenish cartoon flame. |

**Owner: confirm which one** (or point at the dungeon it appeared in and CLI will identify it from the
scene). Once tagged, CLI maps the key → named hook **verbatim** and never swaps it for a look-alike.
Reuse the existing VFX facade (`VFXManager.PlayKey`), no bespoke system.

### Drain's VFX — the beam runs BACKWARD (owner ruling 2026-08-10)

Verbatim: *"for its cast spell do it in re[v]erse, start at target then draw back to caster."*

Every other projectile/beam in the game travels **caster → target**. Drain must travel **target →
caster**: the effect spawns ON the victim and streams back INTO Thrain. That is the whole read — the
player SEES life being pulled out of the enemy and into themselves, so the mechanic explains itself with
no text. It also makes Drain instantly distinguishable from Fireball at a glance in a fight.

- Reuse the existing VFX facade / projectile-beam path; this is a **direction + origin** change
  (spawn at target, travel to caster, terminate on the caster with an absorb/heal flourish) — do NOT
  author a bespoke one-off system.
- The heal lands as the stream ARRIVES at the caster, so the visual and the HP tick agree. A heal that
  fires before the beam reaches you reads as a bug.
- Owner tags the VFX key per the standing rule (memory `vfx-map-owner-tags-no-creative-pick`): **CLI maps
  the owner's tagged key to the named hook verbatim and never substitutes a creative pick.** If no key is
  tagged for Drain yet, HOLD the hook and ask — do not choose one.

## Acceptance criteria

**Part A**
- [ ] Switching heroes rebinds the bar to the new hero's class defaults; no ability from another class
      ever appears on the bar (all four heroes, both directions).
- [ ] Thrain's fresh bar reads Q Fireball / W Arcane Shell / E Mend / R Meteor Strike — capture-proven.
- [ ] Player swaps within-class still persist across scene loads and restarts.
- [ ] An unbound slot is visibly unbound (never a misleading generic icon).
- [ ] `[Flow:*]` lines show class resolved + per-slot binds + reset-vs-carryover on every hero switch.

**Part B**
- [ ] `mage.poison`, `mage.drain`, `mage.thunder` exist in the mage pool, schema-identical to their
      neighbours, with values marked DRAFT pending the owner's tuning pass.
- [ ] Drain heals the caster (the "steal health" verb is real, not flavour text).
- [ ] Data regression covers the three new ids (load + slot validity + no duplicate ids).
- [ ] Owner ruling captured on default-bar promotion before any default slot changes.

**Both:** `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (Thrain's bar after a
hero switch), then an owner felt-test in a dungeon: target verdict "I have my spells."

## What NOT to touch

- The other classes' authored defaults (knight/ranger/cleric) beyond proving the rebind works.
- Ability VFX/animation wiring, the talent tree structure, the Cathedral of Magic unlock table (Part B
  authors POOL entries; hooking them to unlock nodes is a follow-up once numbers are ruled).
- Combat/BattleLock behaviour (the "MELEE swing SUPPRESSED — no active battle" capture line is expected
  outside a staged battle).
