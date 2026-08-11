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

---

## PART A — RESULT (CLI, 2026-08-10). Implemented; awaiting batch gate + owner felt-verify.

### The premise held: the authored data was NEVER the problem

Verified at source, `Assets/StreamingAssets/Data/Canonical/abilities.json` `classes.mage.abilities`:

| Slot | Id | effect | damage | range | cooldown |
|---|---|---|---|---|---|
| q | `mage.fireball` | strike | 30 | 14 | 0.6 |
| w | `mage.shell` | shield | 40 | 0 | 16 |
| e | `mage.heal` | heal | 45 | 5 | 14 |
| r | `mage.meteor` | meteor | 260 | 9 | 42 |

All four are authored under the `mage` class. The explicit DPS the owner said was missing is
`mage.fireball` on Q. **No data was changed for Part A.**

### WHERE THE STALE BINDING LIVED: **the persisted state** — not the producer, not the view

`Assets/_Modules/Village/Hero/AssignableSkillBar.cs` (the HOT-SWAP rail the owner named)
persisted under **one GLOBAL PlayerPrefs key**, `"dotr-skillbar-extra-v1"` — a `public const`,
no class in it. Every hero read and overwrote that one string, so switching Grom -> Thrain
re-rendered the Knight's assigned extras on the Mage, verbatim the owner's *"he inherits the
hotswap from previous character."*

The damning detail: `HeroLoadout.cs` (the W/E/R rail) had the **identical** defect and it was
fixed per-class in **WO-861 Phase 0** (`EquipPrefKeys.LoadoutKeyFor`, `EnsureCurrentKey`).
`AssignableSkillBar`'s own header claims it *"deliberately MIRRORS the HeroLoadout persistence +
battle-lock pattern"* — the mirror was never updated when HeroLoadout was fixed. This is the same
one-store-many-heroes shape as **F8 seq-642** (`GearLoadout.CurrentJob`, which corrupted a save
slot) and **WO-967** (the hardcoded `"knight"` HUD literal). Third instance.

The producer was already correct: `AbilityLoadoutProducer` resolves through
`HeroAbilities.ResolvedDef` -> the hero's own `HeroLoadout` + `_heroClass`, and WO-967's
`HudHeroClassResolver` already answered the class question properly. The view renders what it is
given. **Nothing needed changing in either.**

Secondary hole, same root: neither rail ever asked *"does this id even belong to the hero wearing
it?"* A per-class key alone leaves that unenforced for a pre-split save, an aliased class or any
future writer.

### The fix (5 files)

1. **`Assets/_Modules/Village/Hero/AbilityCatalog.cs`** — new `OwningClassOf(id)` +
   `IsUsableByClass(id, heroClass)`. Ownership is answered from the abilities.json **class key**
   an ability is authored under (`"mage-skills"` -> `mage`), NOT from the id prefix — a renamed id
   must not silently change who may equip it. `universal-skills` -> usable by every class; unknown
   id -> false; `cleric` aliases to `mage` (WO-226), or the predicate would drop her whole bar.
2. **`Assets/_Modules/Core/State/GameStateService.cs`** — `EquipPrefKeys.SkillBarKeyFor(class)`
   (`dotr-skillbar-<class>-extra-v1`) + `SkillBarLegacyGlobalKey`. `ClearEquipPrefs` (New Game) now
   clears both; it was the only one of the three stores that reset never touched.
3. **`Assets/_Modules/Village/Hero/AssignableSkillBar.cs`** — per-class key, `ResolveClass()` and
   `EnsureCurrentKey()` copied verbatim from `HeroLoadout`, called on **every** read and write.
   **This is the rebind seam**: a class change makes the resolved key disagree with `_loadedKey`,
   which re-reads THIS hero's bar. Class-validity drop on load (with a `FlowTrace.Warn` naming
   every dropped id and its owner) and a class-validity **reject** on `Assign`. The legacy global
   key is read **once per class through that same filter**, so each hero inherits only what it
   actually owns and the filtered result is written to its own key — no separate migration step
   that could get it wrong.
4. **`Assets/_Modules/Village/Hero/HeroLoadout.cs`** — the same validity drop on `Load` and reject
   on `Equip`. Belt-and-braces behind the per-class key it already had.
5. **`Assets/_Modules/Village/Hero/HeroControlEnsurer.cs`** — ensures + `ReloadFromPrefs()` on the
   hot-swap bar alongside the W/E/R one, so a hero that PERSISTS across a scene load (never re-runs
   Awake) cannot keep the previous hero's in-memory bar.

**Player swaps within class persist normally** — the drop only ever removes ids the wearer's class
does not author, and Q stays the locked class basic.

### Instrumentation — WO-967's line EXTENDED, no competing second trace

`HudModelProducers.cs`. The existing `ability bar bound: class=... source=... ids=[...]` now reads:

```
ability bar bound: bar=qwer    class='mage' (was 'knight') source=HeroAbilities(live) hero='Thrain' ids=[...] sig=...
ability bar bound: bar=hotswap class='mage' (was 'knight') source=HeroAbilities(live) hero='Thrain' ids=[...] sig=...
```

Two additions, both load-bearing: a **`bar=`** qualifier (the hot-swap rail emitted *nothing* —
WO-967 instrumented only the Q/W/E/R rail, which is why the rail the owner actually reported was
invisible in every capture), and the **`(was '<class>')`** transition. A destination-only line
cannot show the defect: a bar that failed to rebind logs old->new class with **unchanged ids**.
Both are change-gated, never per poll (`Poll` runs 5x/s). Plus the two `FlowTrace.Warn` drop lines
in the bars themselves — no silent drops (CLAUDE.md §12).

### Regression

`Assets/Editor/Regression/HeroBarClassRebindRegression.cs` — `[hero-bar-rebind]`, markers
`HERO_BAR_REBIND_OK` / `HERO_BAR_REBIND_FAIL`. Five cases: the mage kit is all-magic with an
explicit damage Q; the pure ownership contract (own class / universal / cross-class / unknown /
cleric alias); **a live class switch on real components rebinds BOTH rails, both directions, with
zero foreign ids surviving** and a mage's write landing only in the mage's key; the owner's exact
legacy-global save shape inherited FILTERED; and a source lint pinning the per-class key and both
traces. PlayerPrefs are snapshotted and restored in a `finally` (absent keys included), so running
it cannot cost a developer their bar.

**Registration line for `DataRegression.cs`** (lane-fenced, left to the committer):

```csharp
DeNelle.Core.Diagnostics.Guard.Try("Regression", "hero-bar-rebind suite", () => { if (!DeNelle.Editor.Regression.HeroBarClassRebindRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hero-bar-rebind] " + r); });
```

Tag: `[hero-bar-rebind]`.

### Not done here (deliberate)

- **Unbound-slot legibility** (acceptance bullet 4) is untouched — it lives in the HUD view, which
  is in another lane's fence tonight, and no capture yet proves an unbound slot renders a
  misleading icon rather than an empty face. Split it out or re-point it at the view.
- No Unity run, no gate, no commit — batch-gated and committed by the orchestrator.

---

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

## PART B — RESULT (CLI, 2026-08-10). Data authored; **every number below is `<<DRAFT — owner tuning pass>>`** and needs her sign-off before this is called done.

### B0. No new gameplay code was needed — verified at source, not assumed

| Spell | `effect` | Shipped resolver | Line | Precedent already in the game |
|---|---|---|---|---|
| `mage.poison` | `dot` | `HeroAbilities.ResolveDot` | `HeroAbilities.cs:859` → `:1580` | `knight.emberbrand-throw` |
| `mage.drain` | `drainshot` | `HeroAbilities.ResolveDrainshot` | `HeroAbilities.cs:870` → `:1305` | `ranger.healing-shot` |
| `mage.thunder` | `strike` | the core Strike branch (enum default) | `HeroAbilities.cs:853-871` falls through | `knight.thunderbolt` |

`ResolveDrainshot` reads, verbatim: *runs the EXISTING Strike resolution and heals the caster for the
damage that shot actually landed* — **"steal health" is real today**, no code written. Every field used
(`dotDamage`, `dotSeconds`, `damage`, `range`, `cooldown`, `manaCost`, `castSeconds`) already exists on
`AbilityCatalog.AbilityDef`. **No field was invented. Zero `.cs` gameplay files touched.**

⚠ **One honest mechanical caveat, flagged not fixed:** `ResolveDot` applies `DamageElement.Flame` +
`StatusEffect.Burn` (it was authored for a burning brand). A poison-flavoured DoT therefore ticks as
*burn* under the hood. It works and it is the sanctioned reuse; if the owner wants poison to read as its
own element/status that is a **separate ticket**, not a silent widening of this one.

### B1. The three authored entries — DRAFT numbers for sign-off

**`mage.poison`** — R ultimate, in `classes.mage` slot `r`:
| field | value | why |
|---|---|---|
| name | **"Poison Cloud"** `<<DRAFT>>` | she said "poison"; matches her `PoisonCloudcast` key |
| damage (initial) | **40** `<<DRAFT>>` | |
| dotDamage | **24** `<<DRAFT>>` | |
| dotSeconds | **10** `<<DRAFT>>` | 40 + 24x10 = **280 total**, ranged against Meteor's 260 burst |
| cooldown | **42** `<<DRAFT>>` | Meteor's cd, kept |
| manaCost | **6** `<<DRAFT>>` | Meteor's, kept |
| range | **14** `<<DRAFT>>` | Fireball's reach |
| castSeconds | **0.5** `<<DRAFT>>` | Meteor's wind-up, kept |

**`mage.drain`** — E default, in `classes.mage` slot `e`:
| field | value | why |
|---|---|---|
| name | **"Drain"** `<<DRAFT>>` | her word, unembellished |
| damage | **28** `<<DRAFT>>` | heals the same, via damage dealt |
| cooldown | **9** `<<DRAFT>>` | shorter than Healing Shot's 12 — it is a **starter**, the trade must be repeatable |
| manaCost | **3** `<<DRAFT>>` | Mend's cost, kept |
| range | **13** `<<DRAFT>>` | just inside Fireball's 14, so you pull then close one step |
| castSeconds | **0.35** `<<DRAFT>>` | |

**`mage.thunder`** — learnable, in `classes.mage-skills`:
| field | value | why |
|---|---|---|
| name | **"Thunder"** `<<DRAFT>>` | her word |
| damage | **65** `<<DRAFT>>` | a finisher, ~2x Fireball |
| cooldown | **6** `<<DRAFT>>` | Thunderbolt's 3s doubled — burst, not poke |
| manaCost | **3** `<<DRAFT>>` · range **15** `<<DRAFT>>` · castSeconds **0.4** `<<DRAFT>>` | |

### B2. Where each one LIVES, and the one structural fact that forced it

**`AbilityCatalog.Find(class, slot)` reads `classes.<class>.abilities[slot]` DIRECTLY — there is no id
indirection** (`AbilityCatalog.cs:242`). A default slot must therefore hold a FULL def. Since the
acceptance criteria forbid duplicate ids (and `FindById` returns the FIRST match, so a duplicate makes
the live def depend on file order and lets two rows drift), each id is authored EXACTLY once:

- `classes.mage`: `q` fireball · `w` shell · **`e` mage.drain** · **`r` mage.poison**
- `classes.mage-skills` (+3 rows, appended): **`mage.thunder`** (new, learnable) · **`mage.heal`** and
  **`mage.meteor`**, MOVED verbatim from the defaults, definitions intact — neither deleted.

⚠ **A tension for the owner to settle, surfaced not resolved.** Her later note — *"they unlock in the
skill tree and hot swap bar"* — reads as the pool being the primary home for these spells, while her
earlier ruling put **drain on E and poison on R as defaults**. Both are honoured as literally as the
schema allows: drain/poison are the ruled defaults, thunder is pool-only. If she meant all three to be
*earned* rather than given, the edit is to restore `mage.heal`/`mage.meteor` to `e`/`r` and move
drain/poison into the pool — a data move, no code. **Not done on a paraphrase of an explicit ruling.**

### B3. Unlock reachability — what a tree node costs, and why none was authored

A pool spell reaches the hot-swap rail ONLY through a `kind: "skill"` node (`HeroLoadoutVM`: unlocked
Skill-kind nodes → `AbilityCatalog.FindById` → the assignable choices). The node shape, copied from
`knight.t1n2`:

```json
{ "id": "mage.tXnY", "name": "Thunder", "tier": "tierN", "slot": S, "cost": C, "kind": "skill",
  "iconPath": "Talents/wizard/wizard_NN", "abilityId": "mage.thunder",
  "description": "Unlocks Thunder - 65 dmg at 15m (6s cd).",
  "effect": { "type": "unlockAbility", "ability": "mage.thunder" }, "prerequisites": ["mage.t..."] }
```

**The mage tree is a FULL 5-slot x 4-tier grid — 20 nodes, every cell occupied.** Adding three skill
nodes means a **tier 5** or re-purposing existing nodes (`mage.t3n4` "Runic Overload" / `mage.t4n4`
"Reality Rift" are `kind:"active"` stubs with no `abilityId` — the obvious re-purpose candidates).
Placement, tier and cost are **her design**, so nothing was authored — and the WO's own *What NOT to
touch* already scopes unlock-hooking as a follow-up. Two mage pool spells (**`mage.frost-nova`,
`mage.arcane-bolt`**) were ALREADY unreachable before this WO; the regression now pins that ledger so it
cannot grow silently.

**Class-filter check (the way this ships broken):** `OwningClassOf` answers from the abilities.json class
key, so all three resolve to `mage` → `IsUsableByClass(id,"mage")` **true**, and false for knight/ranger.
Part A's rail filter will **keep** them. Regression case 3 pins it.

### B4. VFX — what her one tag covers, and what is left wired to nothing

**Her tag, mapped verbatim:** `mage.poison.vfxCast = "Posion_Cast"` — **her spelling, transposed, kept
exactly** (`Assets/Editor/VfxManualPicks.json`, `manual:true` → `Assets/Spells Pack/Particles/Prefabs/
Variations/Spells/Nature/Spell_Nature_2_Green Variant.prefab`).
⚠ **Spelling-mismatch risk, flagged for the orchestrator, deliberately NOT resolved:** the key reads
`Posion_Cast`, not `Poison_Cast`, and the earlier WO text names a third string (`PoisonCloudcast`). Three
spellings, one effect. The mapping is only correct while the *authored key* and the *tagged key* stay
character-identical; "correcting" either one breaks the lookup silently. **Owner's call which spelling is
canon.**

**Is the sequence in the PREFAB or in the code? — ANSWERED AT SOURCE: the prefab.** Her tagged prefab
resolves (through a variant chain) to `Spell_Nature_2`, which contains **6 child particle systems —
Trail, Distortion, Spell_Nature_2, Dust, Rocks(Explosion), Decal — with staggered `startDelay` (0 and
1.7s)**. It **self-sequences**: one `VFXManager.PlayKey` plays the whole 4-5 part show. So for the beat
she tagged, **one key IS the whole effect and nothing is missing.**
As for `cataclysm` specifically: **there is no cataclysm VFX in this repo at all** — `mage.cataclysm` has
zero vfx keys authored, no prefab named cataclysm exists, and **no `.cs` references cataclysm** outside
regression comments. So there is no third pattern and no cataclysm-only code path; the "4 or 5 parts in
order" she remembers is **(a) prefab authoring**, which is exactly the sanctioned special case — a
differently-authored ASSET, never a second spawner/pool. Nothing here goes near the two-stack scar.

**Untagged stages stay wired to NOTHING** (`vfx-map-owner-tags-no-creative-pick`), enforced by the
regression:
| Spell | tagged | HELD EMPTY, awaiting her tag |
|---|---|---|
| `mage.poison` | `vfxCast` = `Posion_Cast` | `vfxProjectile`, `vfxImpact`, `vfxResidual` (the lingering cloud) |
| `mage.drain` | **none** | all four — incl. the ruled **reversed target→caster beam** |
| `mage.thunder` | **none** | all four (knight's `Thunderbolt_*` keys deliberately NOT reused — a look-alike is still a creative pick) |

**One Unity-side step remains for the committer:** `Posion_Cast` is in `VfxManualPicks.json` but **not yet
in `Assets/Resources/VFX/HovlVfxCatalog.asset`** — run `Defenders/VFX/Generate Hovl VFX Catalog` (the
manual overlay merges last and wins on collision). Until then the key **no-ops harmlessly** (throttled
log), it does not throw. **No VFX rows were added by this lane**, so neither the `VFXType` ordinal trap
nor the `Build()` `arraySize` row-drop trap is engaged.

### B5. Two consequences — RECORDED, not resolved

1. **`hero-talents.json` `mage.t3n1` "Cataclysm Prep" (+60% Meteor radius) now buffs a POOL spell.**
   ⚠ *Re-examined rather than carried forward:* under her *"they unlock in the skill tree and hot swap
   bar"* design this is **arguably INTENDED, not a defect** — buffing a spell you then unlock and slot is
   the loop working. The data supports that reading: the tree ALREADY contains `mage.t4n1` unlocking
   `mage.cataclysm` (a pool spell) gated behind `mage.t3n1`, i.e. **"prep the meteor, then earn the
   bigger one"** reads as a designed line, not an accident. **But** Meteor is no longer a *default*, so a
   player can now buy Cataclysm Prep while owning nothing it affects. **Owner's call**; the id was NOT
   deleted, so the talent still resolves either way.
2. **E is now an OFFENSIVE cast.** `HeroAbilities.cs:702` lists the self-cast/heal effects excluded from
   the melee attack trigger, and `drainshot` is deliberately **not** among them — so E now **yaws the
   hero at the target and plays the attack trigger**, where Mend was a static self-cast. That is correct
   for a beam and it is a real change in feel. Sustain now comes from *winning the trade*, not from
   pausing to heal.

### B6. Files touched + regression

- `Assets/StreamingAssets/Data/Canonical/abilities.json` and `Assets/Resources/Data/Canonical/abilities.json`
  — **byte-identical**, SHA256 `837762614FEFFCD16AE1B46862DE3145FE44B49DB6DC100554F76F4A2AC2C6F8`;
  `version` bumped **2 → 3**; no BOM, CRLF preserved, no NUL bytes.
- `Assets/Editor/Regression/HeroBarClassRebindRegression.cs` — Case 1's expected table updated to
  E `mage.drain` / R `mage.poison`, **exactly as the note that file carried required**, in the same
  change as the data edit.
- `Assets/Editor/Regression/MageSpellKitAuthoringRegression.cs` — **NEW**, `[mage-spell-kit]`, markers
  `MAGE_SPELL_KIT_OK` / `MAGE_SPELL_KIT_FAIL`. Seven cases: the ruled default bar (ids + effects +
  all-magic + Q-has-DPS + E-is-offensive); the three new spells' field shape **plus a source lint that
  the `dot` / `drainshot` handlers still exist**; class ownership (mage + cleric alias yes, knight +
  ranger no) so Part A's filter cannot drop them; the displaced ids survive and `mage.t3n1`'s target
  still resolves; no duplicate ids; the dual-copy byte compare + NUL guard; and the unlock-reachability
  ledger.

**Registration line for `DataRegression.cs`** (lane-fenced, left to the committer):

```csharp
DeNelle.Core.Diagnostics.Guard.Try("Regression", "mage-spell-kit suite", () => { if (!DeNelle.Editor.Regression.MageSpellKitAuthoringRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[mage-spell-kit] " + r); });
```

Tag: `[mage-spell-kit]`.

**Not done here (deliberate):** no talent nodes authored (her design); no VFX substitutions; no Unity
run, no gate, no commit.

---

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

### ⚠ CORRECTION 2026-08-10 — TWO CLAIMS BELOW WERE WRONG (owner: *"wrong"*, *"your data is old"*, *"we use hero mapper"*)

**WRONG CLAIM 1: "the pack contains exactly ONE motion / Sylas has no draw-aim-idle."** FALSE.
`Assets/Action/Ranger/` already holds **26 FBX motions**, including **`Ranger_Aim_Idle.fbx`** plus a full
locomotion set (`standing idle 01`, run forward/back/left/right, run forward stop, walk back, turn 90
left/right, …). The archery motion set is largely IN THE TREE ALREADY — the gap analysis below was
written against a stale read and must not drive the work.

**WRONG CLAIM 2: the CC3-vs-Mixamo retarget lecture.** Retargeting is **already solved by the project's
established pipeline** (owner: *"we use hero mapper"*, *"check the docs"*). **The authority is
`docs/ANIMATION_PIPELINE.md`** — read it before touching any import setting:
- *"Every model — heroes, enemies, anything authored later — is **Humanoid**"*, avatar
  `CreateFromThisModel` (`:10`, `:45`).
- A **two-tier set**: a shared base every model retargets, plus a per-type folder — *"retargets onto
  every model with no per-character re-authoring"* (`:11-13`). `Assets/Action/Ranger/` IS that per-type
  folder for Sylas.
- There is already an editor tool for exactly this ingest:
  **`Defenders/Animation/Reimport Action Clips (force Humanoid)`** (`:58`).

⇒ **The correct procedure is mechanical, not analytical:** drop `archery-shotaway.fbx` into the Ranger's
Action folder, run the force-Humanoid reimport tool, and the retarget is handled by the pipeline. My
CC3/`CC_Base_*` warning below is **moot** — force-Humanoid is precisely what neutralises a foreign
skeleton's bone names. Do NOT hand-configure avatars or muscle settings.
`HeroBodySwapper` is the BODY swap (class FBX selection + fallback chain) and it wires
`Resources/Heroes/<slug>.controller` per class — adjacent, not the retargeter.

**What this WO actually needs, restated:** the defect stands — `Ranger.controller` still references
**zero** bow motions (`grep -c "BowShot" = 0`; states are Attack/Cast/Combo/Block…). So the assets exist
and the mapper exists, but the **Ranger animator does not consume them**. The work is wiring
`Assets/Action/Ranger/*` (aim-idle + locomotion) and the owner-supplied `archery-shotaway` into the
Ranger controller through the hero mapper, and verifying Sylas reads as an archer in idle, movement and
attack. Everything below is retained ONLY as provenance for the supplied file; **treat the rig analysis
as superseded by the hero mapper.**

---

**⚠⚠ [SUPERSEDED — see the correction above] rig note on the supplied file.** The companion `.json` declares:
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

**[SUPERSEDED — the "one clip / missing draw-aim-idle" gap analysis was written against a stale read.
`Assets/Action/Ranger/` already has 26 motions incl. `Ranger_Aim_Idle.fbx` + full locomotion. See the
correction block above.]**

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

### PART B — AUTHORING SURVEY (CLI, 2026-08-10). **Not implemented — this is the owner's.**

Recorded so Part B is a **data edit she can approve**, not a code project. Verified at source:
`AbilityCatalog.AbilityDef` (the schema) and `HeroAbilities.ResolveEffect` (the `switch` on the
raw `effect` string, `HeroAbilities.cs:853-871`).

**Headline: all three spells need ZERO new gameplay code.** Every verb the owner named already has
a shipped effect handler with a live in-game precedent. The one exception is Drain's reverse beam,
which is a VFX direction change (below).

| Spell | `effect` string | Handler that already exists | Precedent to copy the shape from |
|---|---|---|---|
| `mage.poison` | **`dot`** | `ResolveDot` (WO-614 hook 1) — initial hit, then a burn tick | `knight.emberbrand-throw` (dot, 12 dmg, 14 m, 10 s cd) |
| `mage.drain` | **`drainshot`** | `ResolveDrainshot` (WO-861 A4) — Strike damage **and heals the caster by the damage DEALT** | `ranger.healing-shot` (drainshot, 34 dmg, 15 m, 12 s cd) |
| `mage.thunder` | **`strike`** | the core Strike branch | `knight.thunderbolt` (strike, 30 dmg, 16 m, 3 s cd) |

**The "steal health" verb is already real** (acceptance bullet: *"Drain heals the caster, not
flavour text"*) — `drainshot` heals by damage dealt today, no new code. `ranger.healing-shot` is
the running proof.

**Fields to author** (copy a neighbouring entry exactly; do NOT invent fields):
`id`, `slot`, `key`, `name`, `description`, `icon`, `color`, `effect`, `cooldown`, `manaCost`,
`damage`, `range`, `castSeconds`, and for `dot` additionally **`dotDamage`** + **`dotSeconds`**
(and `vfxResidual` for the burn loop). Optional VFX keys: `vfxCast` / `vfxProjectile` /
`vfxImpact` / `vfxResidual`. **All numbers are `<<DRAFT — owner tuning pass>>`.**

**Referrer check on the two defaults the ruling displaces** (the WO asked for this before moving
them):
- **`mage.heal`** — **no referrers anywhere** outside `abilities.json`. Safe to move to
  `classes.mage-skills`.
- **`mage.meteor`** — **ONE referrer**: `hero-talents.json` `mage.t3n1` *"Cataclysm Prep"*
  (`modifyAbility`, `"ability": "mage.meteor"`, +60% radius). Moving the def to `mage-skills`
  keeps the **id** alive so that talent still resolves — but the talent then buffs a POOL spell
  the player may not have equipped, which is an owner call, not a CLI one. Do not delete the id.

**Two consequences of the E/R swap worth her sign-off before the edit:**
- E moving `heal` -> `drainshot` changes the **cast presentation**: `HeroAbilities.cs:702` lists
  the self-cast/heal effects that must NOT drive the melee attack trigger, and `drainshot` is
  deliberately excluded (it is offensive — it swings/shoots and yaws the hero at the target).
  That is correct for a beam, and it is a real change from Mend's static self-cast.
- R becoming `dot` means the ultimate's damage lives in `dotDamage x dotSeconds`, not in a single
  `damage` number. Meteor's 260 does not translate directly; it needs its own ruling.

**Still code, not data:** Drain's **reverse beam** (spawn at target, travel to caster, heal lands on
arrival). That is a direction+origin change on the existing projectile/beam path — see the ruling
below. And the VFX keys for all three stay **owner-tagged** (memory
`vfx-map-owner-tags-no-creative-pick`); CLI holds the hooks until she names them.

### The Poison VFX — the owner's "green bubbles" prefab (do NOT substitute)

Owner: *"there is a beautiful vfx prefab for poison used in a dungeon"* · *"it was green bubbles."*
Candidates found in-tree — **CLI must NOT choose; the owner tags the key** (standing rule, memory
`vfx-map-owner-tags-no-creative-pick`):

| Candidate | Path | Note |
|---|---|---|
| **`Fog_poison`** ← strongest match | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_poison.prefab` | Already used in-game and code-cited: `EnemyAuraVFX.cs:240` — *"Aura_Necromancer (Lana Fog_poison, authored saturated green)"*. Green, bubbling/fogging, and it is the one that appears in dungeon necromancer content. |
| `Fire_cartoon_poison` | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Fire/Fire_cartoon_poison.prefab` | The other Lana poison effect — greenish cartoon flame. |

## PART D — THE VFX DESIGN (owner 2026-08-10: *"be creative and make it great"*)

Owner granted creative license here, superseding the usual owner-tags-only rule **for this section**.
Every prefab below is verified in-tree. The design has ONE governing idea:

> **Thrain's magic ESCALATES in ceremony. The more a spell costs you, the longer the world holds its
> breath before it lands.** Poke = no ceremony. Ultimate = the air itself winds up. The player learns
> Thrain's power curve by *watching*, never by reading a tooltip.

| Slot | Spell | Wind-up | Travel | Impact | The read |
|---|---|---|---|---|---|
| **Q** | Fireball | `Casting_Fire` (short) | `Projectile_Fire` | `Explosion_Fire` | **No ceremony.** 0.6s cycle, snap-flick. It must feel like breathing. |
| **W** | Arcane Shell | — | — | `Spell_Arcane` at the caster | **Instant, personal.** Arcane = Thrain's own colour; the shield is *him*, not a spell he throws. |
| **E** | Drain | brief `Casting_Dark` | ⚠ **REVERSED** | `Explosion_Dark` **at the victim**, tether streams BACK, arrival flourish on Thrain | **The theft.** Ceremony is at the far end — it happens *to them*, and comes home to you. |
| **R** | Poison | `Casting_Nature` (full) | `Projectile_Nature_2` | `Spell_Nature_2_Green Variant` cloud | **The commitment.** Longest wind-up on the bar; the cloud lingers after. |

### D1. The `Casting_Fire_3` resolution — give it to the METEOR, not the poke
The swirling-orbit wind-up is too heavy for a 0.6s Fireball (see the timing tension above) — but it is
**perfect** for the spell it was born for: **`mage.meteor` / `mage.cataclysm`** (both now in the learnable
pool). Fireballs orbiting the caster, then the sky answers with a meteor, is the single most satisfying
cause-and-effect in the kit. **Ruling: `Casting_Fire_3` → the meteor-class ultimates.** Fireball keeps
the short sibling. Nothing is wasted, nothing is mistimed, and the player who unlocks the meteor gets a
visibly *bigger* cast than anything they had before — the escalation idea, made literal.

### D2. Drain is the set-piece — build it deliberately
The reversed beam is the kit's signature and the one effect worth extra care:
1. **Cast:** a short dark gather at Thrain's hand — enough to read intent, never enough to slow the trade.
2. **Bite:** `Explosion_Dark` fires **on the victim** — the violence happens at the far end.
3. **Tether:** a dark stream target→caster. **Bind the heal tick to the stream's ARRIVAL**, not the cast.
4. **Arrival:** a warm flourish on Thrain as HP lands — the only warm note in a dark spell. That contrast
   is the whole point: *their* light, entering *you*.
Get this one right and it sells the class fantasy on the first cast.

### D3. Sylas — the arrow tells you what's on it
Hovl's arrows are colour-coded, so make the colour *mean* something instead of decorating:
- **`1 nature` (green) = the default shot.** Also matches the existing `ranger_arrow_poison` / Venomcraft
  rider, so poison ammo reads as "more of the same, worse for you."
- **`11 orange` / `21 red` = fire/ember ammo.** **`4 yellow` = precision/lightning.**
- **`Marker 1 arrows Loop` = the aim reticle** — a Ranger who marks before he looses. Pairs with
  `Ranger_Aim_Idle` so the aim pose and the marker arrive together: Sylas *aims*, the mage *casts*.
- Stage map: `Flash` = release (fires with `archery-shotaway`) · `Projectile` = flight · `Hit` = impact.

### ⚠⚠ D3-CORRECTION — MY OWN COLOUR-CODING BROKE THE COLOURBLIND LAW. FIXED HERE.

The owner is **red/green colourblind** (standing project law: *never convey meaning by colour alone*).
The D3 draft above made **colour the primary carrier of meaning** for arrow ammo — and picked the single
worst possible pairing: **`1 nature` (green) vs `21 red`** are exactly the two a red/green colourblind
player cannot separate. A design the owner cannot personally read is a failed design, no matter how it
looks to anyone else.

**THE BINDING RULE FOR ALL VFX IN THIS WO: every effect must be identifiable with COLOUR REMOVED.**
Colour may reinforce; it may never be the signal. Each effect carries **at least two** of:

| Channel | How it distinguishes |
|---|---|
| **Silhouette / shape** | cloud vs beam vs orbit vs bolt — readable in greyscale |
| **Motion** | Fireball snaps · Drain *streams inward* · Poison *lingers and drifts* · Meteor *falls* |
| **Timing / ceremony** | the escalation idea (D-header) is already colour-free: poke = instant, ultimate = long wind-up |
| **Sound** | distinct cue per element (the audio facade already varies by class/ability) |
| **Text** | the ability name + state text on the bar, per §3's existing colourblind clause |

**Sylas's ammo, re-designed without colour as the signal:**
- Distinguish by **trail shape and impact behaviour**, not hue: default = clean thin trail, small hit;
  poison = *drifting residue that lingers after impact*; fire = *expanding burst*; precision = *tight,
  fast, no spread*. Each is legible in greyscale and at speed.
- Keep the colours as a *secondary* layer — they help players who can see them, and cost nothing.
- Add a **text/icon tell** on the ammo/hotbar so the equipped rider is never a visual guess.
- ⚠ **Do NOT ship "green = poison, red = fire" as the mechanism.** That is the failure this block exists
  to prevent.

**Validation (add to acceptance):** capture each effect and **desaturate it**. If two effects become
ambiguous in greyscale, the design is wrong — not the viewer. This check is cheap and it is the only one
that proves the law was honoured rather than asserted.

### D4. The one rule that keeps it coherent
**Element = identity, never decoration — and identity is carried by SHAPE, MOTION and TIMING first,
colour last.** Fire is Thrain's reflex (snap), Arcane is his self (envelops him), Dark is what he takes
(streams inward), Nature is what he commits to (lingers). If a future effect cannot answer *"what does
this read as in greyscale?"*, it has the wrong effect.

### D5. Creative ownership (owner 2026-08-10: *"im colorblind so id rather someone else handle creative"*)
Visual creative direction is **delegated away from the owner** — she should not be asked to adjudicate
hue, palette or "which looks better" calls, and no work order may block on her making one. Practical
consequences, binding on every seat:
- **Do not send the owner colour choices.** Send her *behaviour* choices (what should this spell FEEL
  like — fast/heavy/lingering?), which are hers and are colour-free.
- The UI seat proposes visual direction (as in Part D); **CLI implements; the greyscale check is the
  objective gate** — so "is it good?" is answered by a test, not by an eye the owner does not want to
  be asked for.
- This **supersedes, for visual-only picks, the usual "owner tags the VFX key" rule** (memory
  `vfx-map-owner-tags-no-creative-pick`) — that rule exists so seats do not silently substitute the
  owner's *intent*, and it still binds wherever the owner HAS named an asset (e.g.
  `Spell_Nature_2_Green Variant`, `Casting_Fire_3`, the Hovl arrows). Where she has not, the seat
  proposes rather than asking her to choose a look.
- ⚠ **Memory note:** this ruling should be recorded so no future session hands her a palette decision.

⚠ Numbers (durations, radii, tuning) remain `<<DRAFT — owner pass>>`; this section rules LOOK and TIMING
INTENT only. Any prefab named here is verified on disk; bind by GUID.

---

### ⚠⚠ SHIP RISK — the Spells Pack needs its URP materials package imported, or EVERYTHING renders MAGENTA

From the vendor doc (`Assets/Spells Pack/Documentation/Documentation.txt`, read 2026-08-10): the pack
ships Standard-pipeline materials by default. For URP you **must** import
`Spells Pack/Packages/URP (2020.3.33+)` — *"When is done all materials look with magenta color"* is the
documented symptom of skipping it. **This project is URP.** Verify BEFORE wiring any spell VFX:
- Confirm the URP package was imported for this pack (not just the prefabs present).
- The build shader-pin (`PinShadersOnBuild` / `EnsureShadersIncluded`) + `MagentaGuard` must cover these
  materials — a spell that looks right in the editor and ships magenta is the WO-1015/E-class defect
  again, and the owner has already hit a magenta asset once (the overworld portal).
- Vendor also recommends **Linear** color space ("everything looks great with this") — check the project's
  current setting before attributing a dull look to the prefab.

**Sylas's arrow VFX (C3) — the owner ALREADY OWNS the packs; nothing to buy.** Owner 2026-08-10: *"i own
it"* (the Zakhanfx Archer Pack, same aesthetic as Spells Pack) and *"thats one of my packs"* re
**`Assets/Hovl Studio/AAA Projectiles Vol 1/`**, which IS imported and holds arrow-family VFX in the
matching three-stage shape, e.g.
`Prefabs/Flash and hits/Flash 1 nature arrow` · `Hit 1 nature arrow` · `Flash 11 orange arrow` ·
`Hit 4 yellow arrow` (flash = muzzle/launch, hit = impact).
**✅ RULED 2026-08-10 — use the HOVL arrows** (owner: *"there are Hovl ones for Arrows"*). Already
imported, no purchase, no import step. The arrow family follows the SAME three-stage shape as the Spells
Pack, so it slots straight into the convention above:

| Stage | Hovl prefab | Maps to |
|---|---|---|
| launch / muzzle | `Flash <n> <colour> arrow` | the release (fires with `archery-shotaway`) |
| travel | `Projectile <n> <colour> arrow` | the arrow in flight |
| impact | `Hit <n> <colour> arrow` | on-hit |

**Colour families present (pick per ammo/element):** `1 nature` (green) · `4 yellow` · `11 orange` ·
`20 pink` · `21 red`. Also available: `2D Projectile …` variants (billboard, cheaper) and
`Marker 1/6 arrows (Loop)` — targeting/indicator markers, useful for a Ranger aim reticle.
**Element fit:** `1 nature` is the obvious base arrow for Sylas and matches the existing
`ranger_arrow_poison` / Venomcraft poison rider (green); `11 orange` / `21 red` suit a fire/ember ammo,
`4 yellow` a lightning/precision shot.
⚠ **Owner still names the exact prefab per shot type** — the table above is the menu, not the pick
(`vfx-map-owner-tags-no-creative-pick`). Bind by GUID.
*(Note: a folder literally named "Archer Pack" is not present under `Assets/`; the Zakhanfx pack is owned
but appears unimported. Not needed — Hovl is the ruled source.)*
*(Other Hovl packs in-tree, for later reference: `AOE Magic spells Vol.1`, `RPG VFX Bundle`,
`Magic circles`, `3D Lasers Pack`, `Map track markers VFX`.)*

⚠ The doc contains **install instructions only** — no per-prefab descriptions. It cannot help pick which
spell looks best; that is exactly what WO-511's catalogue + the owner's eyes are for.

### ⚠ THE SPELL VFX CONVENTION (owner 2026-08-10: *"spells are organized by explosion"* · *"casting_"* · *"projectile_"*)

`Assets/Spells Pack/Particles/Prefabs/Projectiles/` is organised in **THREE STAGES per element** —
this is the contract every new spell's VFX must follow:

```
Projectiles/Casting/      Casting_<Element>[_n].prefab      <- wind-up at the caster
Projectiles/Projectiles/  (the travelling projectile)
Projectiles/Explosion/    Explosion_<Element>[_n].prefab    <- impact
```
**⚠ STAGE 1 IS THE WIND-UP (owner 2026-08-10: *"the whole casting spells are for wind up"*).** The entire
`Casting/` folder is **wind-up only** — it plays at the caster BEFORE the spell leaves, and it must be
driven by the ability's authored **`castSeconds`** field (already in the schema, e.g. frost-nova's
`"castSeconds": 0.4`). Contract:
`Casting_<El>` for `castSeconds` → projectile spawns/travels → `Explosion_<El>` on impact.
Do NOT fire a `Casting_` prefab as the impact or as a one-shot flourish, and do not let the projectile
leave before the wind-up completes — a wind-up that plays after the damage lands reads as lag, not
weight. (Instant-cast spells with `castSeconds: 0` get a minimal or no wind-up rather than a truncated
one.)

**Elements available:** `Arcane · Dark · Fire · Ice · Light · Nature · Storm`
(also mirrored under `Auras/Aura_<Element>`, `Buffs/Buff_<Element>`, `Shields/`, `Spells/Spell_<Element>`).

**✅ OWNER-TAGGED WIND-UP (2026-08-10):** *"casting fire 3 is amazing wind up with swirling fireballs
around caster"* → **`Assets/Spells Pack/Particles/Prefabs/Projectiles/Casting/Casting_Fire_3.prefab`**
(verified on disk; siblings `Casting_Fire`, `_2`, `_4` also exist). Bind by GUID, not filename.

⚠ **TIMING TENSION — flag to the owner, do not silently resolve.** A swirling-orbit wind-up is a
*heavy, showy* cast, but `mage.fireball` (Q) is authored as the **spammable poke**: 0.6s cooldown, no
meaningful `castSeconds`. Playing a full orbit wind-up on a 0.6s-cycle button will either be truncated
every cast (reads broken) or gate the spell's responsiveness (kills the poke). Three honest options:
**(a)** put `Casting_Fire_3` on a HEAVIER fire spell where the wind-up has room to breathe (it would sell
an ultimate beautifully); **(b)** keep it on Fireball and raise Fireball's `castSeconds` to match the
orbit — changes Q from poke to committed cast, a real gameplay change needing an owner ruling;
**(c)** use a shorter sibling (`Casting_Fire`/`_2`) on Q and reserve `_3` for the big one.
**Owner picks; CLI does not choose.**

**✅ OWNER-TAGGED 2026-08-10: `Spell_Arcane` — *"we need to use"***. Verified on disk:
`Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Arcane.prefab` (base, plus variants `_2` … `_11`).
⚠ **WHICH SLOT IS UNSTATED — CLI must not assign it.** Arcane is the Mage's signature element, so the
plausible homes are `mage.shell` (W, Arcane Shell — name matches exactly) or a general Thrain cast
identity. **Bounce to the owner:** which ability, and base `Spell_Arcane` or one of the 10 variants?
Bind by GUID once ruled.

**Element mapping for this WO's kit — OWNER CONFIRM the two marked (?):**
| Spell | Element | Stages to wire |
|---|---|---|
| `mage.fireball` (Q) | **Fire** | `Casting_Fire` → projectile → `Explosion_Fire` |
| `mage.poison` (R) | **Nature** ← the green one; there is no "Poison" element | `Casting_Nature` → projectile → `Explosion_Nature` |
| `mage.thunder` (learnable) | **Storm** (?) | `Casting_Storm` → projectile → `Explosion_Storm` |
| `mage.drain` (E) | **Dark** (?) | ⚠ REVERSED per the owner's ruling: spawn at the TARGET, travel to the CASTER, terminate on the caster. So the stage roles INVERT — the "explosion" plays at the victim on cast, and the arrival flourish plays on Thrain. Wire deliberately; do not reuse the forward-projectile ordering. |

**✅ OWNER TAGGED THE KEY 2026-08-10: `PoisonCloudcast`.** Per the standing rule (memory
`vfx-map-owner-tags-no-creative-pick`) CLI maps this key → the named hook **verbatim** and never
substitutes a look-alike. Reuse the existing facade (`VFXManager.PlayKey`), no bespoke system.

### ✅ RESOLVED 2026-08-10 — the owner named the exact prefab

Owner: *"spell name to use is in spells `Spell_Nature_2_Green_Variant`"*. **Verified on disk:**
```
Assets/Spells Pack/Particles/Prefabs/Variations/Spells/Nature/Spell_Nature_2_Green Variant.prefab
```
⚠ **Note the real filename has a SPACE, not an underscore, before "Variant"** (`Spell_Nature_2_Green
Variant.prefab`) — load it by its true path/GUID; a literal `Spell_Nature_2_Green_Variant` string lookup
will MISS. Prefer the asset GUID over a name string so a future rename cannot silently break the hook.

Its `Nature_2` siblings exist across every stage if the full three-stage treatment is wanted:
`Casting_Nature_2` · `Projectile_Nature_2` · `Explosion_Nature_2` (+ base `Spell_Nature_2`, and a Blue
variant). **This green variant is the owner's pick for `mage.poison` (R)** — register the
`PoisonCloudcast` key against it and do not substitute a sibling.

---

⚠ *(superseded by the block above — kept for provenance)* **UI seat could not resolve `PoisonCloudcast`
to an asset:** Searched
`Assets/**` for `*PoisonCloud*` (no hits) and grepped `Assets/_Modules` + `Assets/StreamingAssets` for
the string `PoisonCloud` (no hits). So the key is **not yet registered in the VFX catalog and no prefab
carries that name today**. Nearest poison/gas prefabs in-tree, listed as candidates ONLY (**do NOT pick
one — the owner's key is the authority**):
`Lana Studio/.../Fog/Fog_poison.prefab` · `Lana Studio/.../Fire/Fire_cartoon_poison.prefab` ·
`UnityTechnologies/ParticlePack/.../Prefabs/PoisonGas.prefab`.
**CLI action:** locate the asset the owner means (it may live in a pack not yet imported, or under a
different filename), then **register `PoisonCloudcast` in the VFX catalog** pointing at it, and bind the
`mage.poison` R-slot cast to that key. If the asset cannot be found, **HOLD the hook and ask the owner**
— do not ship a substitute (the un-tagged-hook rule).

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
