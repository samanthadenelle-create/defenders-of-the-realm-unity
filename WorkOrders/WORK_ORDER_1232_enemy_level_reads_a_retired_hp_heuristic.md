# WORK ORDER 1232 - "Lv 68" on a wave-7 enemy: two call sites still run the RETIRED HP/25 heuristic

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Silo:** HUD / Combat presentation
**Severity:** P1. It also drives the DANGER SKULL, so the threat warning is wrong by the same factor.
**Origin:** Owner felt-test, Seeker build `2026.08.26.342290`, 2026-08-26. Owner verbatim:
***"it died but said it was lvl 68 enemy"***. Captured state: hero **Thrain Lv 5**, **Wave 7**,
Elarion (`tmp/screen-lvl68.png`).

---

## ROOT CAUSE - found at source, no theory required

`Enemy.Level` is the CORRECT value and has been since WO-611 F3. Its own doc comment
(`Enemy.cs:484-489`) states what it replaced:

> the truthful display level for the target frame ("Lv N"). Set in `Configure` from the authored def
> (a STABLE per-archetype band), **replacing the old HUD-side `EnemyLevelStub` HP/25 heuristic that
> read the runtime maxHp and crept upward as wave-scaling inflated it.**

**Two call sites were never migrated and still run the retired heuristic verbatim:**

- `Assets/_Modules/Village/HUD/HudModelHost.cs:223` - `EnemyLevelStub(Enemy e)`
- `Assets/_Modules/Village/Combat/ThreatSkullPlate.cs:70` - `EnemyThreatLevel(Enemy e)`

Both are byte-identical:

```csharp
float maxHp = e.MaxHp > 0.001f ? e.MaxHp : e.Hp;
return Mathf.Max(1, Mathf.RoundToInt(maxHp / 25f));
```

**The arithmetic confirms the report:** 68 x 25 = **1700 HP**, which is what wave-7 scaling inflates
an ordinary enemy to. The label is not reporting a level - it is reporting `maxHp / 25`.

Each site's comment says it "Mirrors" the other so the two "read exactly one value" - and they do
read one value. **It is the wrong one.** This is the duplicated-state drift CLAUDE.md documents
repeatedly (the stale WO number block, the retired dependency table, the hardcoded repo root): the
authority moved to `Enemy.Level`, the copies stayed.

## The second, worse consequence

`ThreatSkullPlate.EnemyThreatLevel` feeds `delta = enemyDifficulty - playerLevel` and the
`LethalDelta` / `RiskyDelta` bands (`ThreatSkullPlate.cs:59-61`). With the heuristic, a routine
wave-7 enemy reads as **63 levels above** a Lv 5 hero, so **every enemy shows a lethal skull** and the
warning carries no information. Fixing the label without fixing the skull leaves half the defect.

`HudModelProducers.cs:481` already carries a comment saying it uses the real level and "not the old
EnemyLevelStub HP/25 heuristic" - so **one consumer was migrated and two were missed.** Confirm there
is no fourth.

## OWNER RULING 2026-08-26 - this may not be a number at all

Owner verbatim, on being shown the root: ***"but lvl 68 versus a lvl 5 seems off"*** ->
***"then remove the level or just but boss or something to tell"***.

**So pointing the two sites at `Enemy.Level` is the FLOOR, not necessarily the answer.** The owner is
open to removing the numeric level entirely in favour of a QUALITATIVE tell - Boss / Elite / ordinary -
which communicates "what am I facing" without inviting the player to do level arithmetic against a
hero level the number was never comparable to.

Implement it in this order:

1. **First, kill the lie.** Both sites must stop deriving a level from `maxHp / 25f`. That is
   non-negotiable and stands regardless of what the presentation becomes.
2. **Then propose the presentation.** Bring back a recommendation with the authored data in hand:
   what per-archetype level bands actually exist, whether a boss/elite flag already exists on the def
   (waves.json authors `boss` and `apexBoss` - check whether the Enemy carries that through), and
   whether a number, a word, or both serves the player best. **The owner decides the final
   presentation** - do not ship a redesign as if it were the fix.
3. If a qualitative tell is the answer, it must be **a WORD the player can read**. The owner is
   red/green colourblind; a skull tinted differently is not a tell. This repo already holds that line
   in `canon-strings.json` (`_raidCooldownNote`: *"EVERY one of these is a WORD the player can read"*).

## Required

1. Both sites read **`Enemy.Level`**. Delete the two heuristic bodies - do not "improve" the divisor;
   there is an authored value and the heuristic must not survive in any form.
2. **Sweep for other consumers** of an HP-derived level. Search for `/ 25f`, `maxHp /`, `EnemyLevelStub`,
   `EnemyThreatLevel` across `Assets/`. Report every hit and its disposition. If a legitimate consumer
   needs a difficulty band that is NOT the level, say so - do not silently repurpose `Level` for it.
3. Re-check the `LethalDelta` / `RiskyDelta` thresholds against REAL levels. They were tuned (if at
   all) against inflated numbers, so they are very likely wrong now. **Report what they are and what
   they now mean; do not retune them without saying so** - threat banding is player-felt and may be an
   owner ruling.
4. Instrument per section 12: a `FlowTrace` line when a target frame resolves a level, naming the
   source, so a future regression of this is one read rather than a felt-test.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression that FAILS on today's tree: configure an enemy with an authored level, inflate its
   `MaxHp` to a wave-scaled value, and assert the displayed level and the threat level BOTH report the
   authored level and are INDEPENDENT of MaxHp. Prove it RED first (WO-1138) and state how.
3. A guard case asserting no `Assets/` source derives a level from `maxHp / 25f`, so this cannot
   silently return a third time.
4. **A DEVICE SCREENSHOT** at 2670x1200 of a wave enemy's target frame showing a sane level next to a
   Lv 5 hero.
5. Owner felt-verifies and CLOSES.

## What NOT to touch

- `Enemy.Level`, `Enemy.Configure`, or the authored per-archetype level bands. That side is correct.
- Wave HP scaling. The inflated HP is intended; reading a LEVEL off it is the defect.
- The danger-skull VISUALS (colour/'art'). The owner is red/green colourblind and the plate's
  non-colour encoding is deliberate - this ticket changes the NUMBER feeding it, nothing else.

---

## ⛔ CORRECTION 2026-08-26 - THIS TICKET'S OWN PREMISE WAS HALF WRONG

The section above says `Enemy.Level` is "the CORRECT value... set from the authored def (a STABLE
per-archetype band)". **That is not what the code does.** `Enemy.cs:623`:

```csharp
_level = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, def.Hp) / 25f));
```

**`Enemy.Level` IS the same HP/25 heuristic** - applied to the def's BASE hp instead of the runtime
scaled hp. **There is no authored level field anywhere; `EnemyDef` has none.** Every level in the game
is HP/25. The doc comment on `Enemy.Level` describing an "authored def / stable per-archetype band"
is itself misleading, and this ticket believed it. Section 12's rule applies to doc comments exactly
as it applies to code: comments lie.

**So the owner's "Lv 68" was NOT printed by the two broken sites.** `necromancer` is authored at
**hp 1700 -> exactly Lv 68**, and `waves.json` names it the **wave-6 boss**. The discriminator is
arithmetic: wave-6 HP scaling (~1.4x) through the RETIRED heuristic would read ~**96**, not 68. Only
the base-HP path yields exactly 68. She was on wave 7 and said *"it DIED but said it was lvl 68"* -
she was looking at a boss whose level is its HP over 25.

**The two fixes remain correct and necessary** - `ThreatSkullPlate` owns the danger skull and was
genuinely reading inflated runtime HP, which is why every enemy read lethal. But **they do not close
her report.** What closes it is the presentation ruling below.

## The sweep result (complete)

| Site | Disposition |
|---|---|
| `ThreatSkullPlate.cs:72` | FIXED - reads `Enemy.Level` |
| `HudModelHost.cs:227` `EnemyLevelStub` | **DELETED** - it had ZERO callers; a pure landmine |
| `HudModelProducers.cs:483` | already migrated; instrumentation added |
| `Enemy.cs:623` | **the real authority, and itself HP/25** - reported, not touched |
| `EnemyFamilyTestSpawner.cs:182` | legitimate - a debug spawner that INVERTS the mapping (`HP = level x 25`) |
| `BattleHud9Zone.cs` | carries no level path today. **No fourth consumer exists.** |

Post-fix sweep: **2203 `.cs` files, 0 hits** for `maxHp/25f`, the `MaxHp > 0.001f ?` probe, or
`EnemyLevelStub`.

## Threat bands - REPORTED, NOT RETUNED (owner ruling)

`RiskyDelta = 3`, `LethalDelta = 7` (`ThreatSkullPlate.cs:47/49`). Against real levels vs a Lv 5 hero
the bands now DISCRIMINATE (before, everything read lethal). Two consequences to rule on:
1. `hollow-brute` (900 hp -> **Lv 36**) is an ordinary wave brute, not a boss, and reads LETHAL forever.
2. Because levels are HP/25, the deltas are **HP ratios wearing a level costume**: `+7` means
   "+175 HP", which is meaningless at high tiers. A LINEAR band over a quadratic-ish HP spread.

## RECOMMENDED PRESENTATION - drop the number, show a WORD

The data already supports it:
- `EnemyDef.Boss` (bool) is authored `true` for exactly two: **`necromancer`** and **`troll-overlord`**;
  `waves.json` names bosses at waves 5/6/12/18 plus `apexBoss` Syndrath (4200 hp) at wave 20.
- `EnemyDef.RoleKind` already maps `role:"elite"` -> `EnemyRole.MiniBoss` (`WaveData.cs:227`), and
  `HudModelHost.RoleName(MiniBoss)` **already returns the literal string `"Boss"`**.
- The target frame already has a spare text slot (`ElarionUiKitObsidian.cs:1771` `extra`, currently
  only "LOCKED"), and the name already takes a `!` / `!!` prefix.

Two gaps: `Enemy.IsBossTier()` / `IsEliteTier()` are **private** (`Enemy.cs:3384/3390`) - one public
accessor needed; and `HudModelHost.ToHudRole` **flattens `MiniBoss` -> `HudRole.Warrior`**
(`:153`), so boss-ness currently dies at the model boundary.

**Proposal: ordinary enemies show nothing, `role:"elite"` shows `ELITE`, `boss:true` shows `BOSS`,
apex shows `APEX`.** Words, readable, colour-independent. `TierFor` keeps driving the existing
`!`/`!!` + RISKY/LETHAL text, which is already word-based.

⚠ **If the owner wants a NUMBER kept, it must first become a real authored `level` field on
`EnemyDef`** - otherwise the arithmetic she objected to ("lvl 68 versus a lvl 5") is unavoidable,
because the number IS the HP.
## LANDED-WORK AUDIT (2026-08-26)

The two retired HP-derived display sites were removed in `b303c4fbf`. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83802` `WO1232_ENEMY_LEVEL OK` proves target and threat read
`Enemy.Level`, remain invariant through x1..x9.7 HP scaling, and finds no remaining `maxHp/25`
derivation across 2208 C# files; `:83814` is `REGRESSION_OK 291/291`. **Post-FIXED APK checklist:** the specified
2670x1200 wave-enemy target-frame screenshot and owner felt-close.

---

## OWNER RULING 2026-08-26 (FINAL) - REMOVE THE NUMBER, AND REMOVE THE THREAT MATH TOO

Owner verbatim: *"HP / 25 is not a level system. Dressing it up as one just produces very confident
nonsense."*

### 1. Numeric enemy levels are REMOVED
Display **authored classification badges only**: **ELITE** and **BOSS**.
- **APEX is RESERVED** for a future explicitly-authored tier. Do NOT ship it now. `apexBoss` exists
  in `waves.json`, but until a tier is authored deliberately, adding a third badge invents the very
  precision this ruling removes.
- **Ordinary enemies get NO badge.** Silence is the default, not a blank label.

### 2. The LETHAL / RISKY calculation is REMOVED, not retuned
Owner verbatim: *"The Lv5 vs Lv36 comparison is downstream of the fake level. Retuning thresholds
just polishes the wrong equation."*

Delete the `RiskyDelta` / `LethalDelta` banding from the player-facing path. **Do NOT retune the
numbers** - the equation itself is the defect. `hollow-brute` reading LETHAL forever is a SYMPTOM.

WARNING: keep `ThreatSkullPlate`'s instrumentation per CLAUDE.md section 12 - instrumentation is
PERMANENT and is never removed as cleanup. Flag the display off; leave the traces in the code.

### 3. What a proper replacement looks like - NOT this ticket, do not build it
The owner named the shape for later: a real **Combat Rating** derived from things that actually
affect the fight - HP, damage, attack cadence, armour, abilities, encounter role - surfaced as
**Low / Even / High / Deadly**. Until that model exists, silence beats fake precision.

STOP: do NOT implement Combat Rating under this ticket. It needs its own spec and its own ruling.

### The principle to carry forward
**Separate IDENTITY from DIFFICULTY.** A Necromancer being a BOSS is an authored fact.
`Lv 68 - LETHAL` is two invented numbers wearing a costume.