# WORK ORDER 907 — Elemental affinity: towers, enemies, and a match bonus that is never a lock

**Status:** SPEC — not started. **Blocks nothing, but see §6: this must be decided BEFORE the VFX pass picks per-tower visuals.**
**Minted:** 2026-08-04 (CLI), owner ruling
**Lane:** Combat / balance + data. Touches the damage path — treat with the care that implies.
**Adjacent:** WO-872 (combat VFX master), WO-870 (tower type x tier VFX), WO-855 (economy balance, just shipped)

---

## 1. The ruling

> **Owner, 2026-08-04:** *"Each tower could land a different affinity."* … *"So they could both apply (based on affinity)."*
> Asked whether enemies carry an element today: ***"They don't yet but should."***

**Affinity applies BOTH ways: it drives the VISUAL and it drives the DAMAGE.** A tower's look and its element are the same fact, so they can never drift apart — which is exactly the bug the Arcane Spire had (it dealt Aether and rendered Fire).

---

## 2. ⚠ THE GOVERNING RULE — match bonus, NEVER a lock

**This is not a new invention. It is the grammar this game already uses**, ruled by the owner for Echoes
(CLAUDE.md §7, WO-830):

> *"Echo harvest affinity is a MATCH BONUS, NEVER a lock… matching that Echo's affinity **doubles** the
> yield. Never gate an Echo to one resource."*

**Tower affinity uses the same grammar.** A matching element hits harder; a mismatched one still works.
**No tower may ever become useless against an enemy type.** A player who built the "wrong" tower must be
inefficient, never blocked — the moment affinity gates damage, tower choice becomes a puzzle with one
answer and the whole build-your-own-town premise narrows.

**Design consequence to preserve:** the bonus must be legible from the art. A player who learns *"the
blue one melts skeletons"* by watching, without reading a table, is being taught by the VFX. That is the
best case, and it is only possible because §1 ties the visual to the element.

---

## 3. State of the ground — verified at source 2026-08-04

| Piece | Status |
|---|---|
| `ElementType` enum | **EXISTS** (`Types.cs`, `Defs.cs`) with a `ToToken` mapper |
| `IDamageable.TakeDamage(dmg, element)` | **EXISTS** — and `IDamageable.cs:61` documents the param as *"used for **resist / bonus math**"* |
| `DefenseTower` passes its `Element` into `TakeDamage` | **YES** |
| Tower elements authored | ⚠ **ONLY `tower_arcane_spire` = Aether.** `tower_ground_archer`, `tower_wall_wizard`, `tower_siege_tower`, `tower_catapult` author **NONE** |
| Enemy elements / weaknesses | ⚠ **NONE** (owner-confirmed) |
| The resist/bonus math itself | ⚠ **UNKNOWN — §4.1 is to find out** |

`CombatantDefSO.element` and `Combat.cs:50` (`ElementType element = input.Element;`) suggest the **ATB /
battle** system may already read element while the **village/tower** path does not. Establish which.

---

## 4. What this WO must determine and build

### 4.1 FIRST — does the resist/bonus math exist? (§12: answer before designing)
`IDamageable.cs:61`'s doc comment **promises** resist/bonus math. **Find out whether anything implements
it.** Three possible worlds, and they are very different jobs:
- **It exists and is live** → this WO is mostly authoring data.
- **It exists but nothing reaches it** (the shape of four defects found on 2026-08-04 — the Crystal Mine,
  the windmill perks, the collector tell, the tower scaffold) → this is a wiring job.
- **It was never written and the doc comment is aspirational** → this is a real feature.

**Report which, with `file:line`, before writing anything.**

### 4.2 Author tower affinities
Four towers need an element. **The owner assigns them — do NOT pick.** Present the tower kits as context
for her decision (the Ballista's slow single heavy bolt, the Sky Ballista's air-only role, the Archer as
the plain baseline, the Catapult's future siege identity per WO-906) and **hold until tagged**, exactly as
the VFX picks are held.

### 4.3 Author enemy affinities
Enemies need an element and/or a weakness. `enemies.json` currently has none. Same rule: **the owner
assigns; you propose the schema, not the values.**

⚠ **A tower affinity with no enemy affinities is half a system** — the towers are themed and nothing
responds. **Both sides land together or neither ships.**

### 4.4 The bonus magnitude
One number, authored in data, not a C# literal. The Echo precedent is **x2 on a match**. Whether towers
use the same multiplier is the owner's call; propose, do not decide. **Any mismatch penalty must be a
reduced bonus, not a penalty below baseline** — §2's never-a-lock rule.

### 4.5 Tie the visual to the element
Per §1 the two must not drift. Whatever a tower's element is, its projectile/impact VFX reads it — and
the VFX pick is tagged in `VfxCasterWindow` per the standing owner-tags rule. **WO-870 is the consumer;
this WO owns the element, not the prefab.**

---

## 5. ⚠ BALANCE BLAST RADIUS — read before touching the damage path

**This changes every matchup in the wave schedule.** WO-855 landed a full economy rebalance hours ago,
and `docs/ECONOMY_REWARD_MEASUREMENT_2026-08-04.md` measured tower cost-per-DPS into an 8-20 band. **A
damage multiplier on top of that re-opens all of it.**

- Re-derive tower basket/DPS **with affinity applied**, against the composition `WaveCompositionBuilder`
  actually generates — not against the roster in the abstract.
- ⚠ **Do not let affinity quietly restore the Sky Ballista outlier.** It was at 1.72 basket/DPS (5x better
  than the ground Archer) and was corrected to 11.17 on three levers today. A matched-element bonus is a
  fourth lever and could undo that.
- The adaptive difficulty system is measuring against a moving floor already; say what this does to it.

---

## 6. Sequencing — this gates part of the VFX program

**Decide the affinities BEFORE WO-870 picks per-tower visuals.** If the VFX are chosen first and the
elements assigned second, they will disagree — which is *precisely* the Arcane Spire defect (Aether
damage, Fire visuals) that WO-872 exists to fix. **Element first, visual second.**

Landing after the current UI/VFX wave is fine; landing the VFX picks before the elements is not.

---

## 7. What NOT to do

- **Do NOT let affinity gate damage to zero or near-zero.** Match bonus, never a lock (§2).
- **Do NOT pick the affinities.** Owner assigns, same as the VFX tags. Hold and report.
- **Do NOT ship tower affinities without enemy affinities** (§4.3).
- **Do NOT hard-code the bonus multiplier** — data, one place.
- **Do NOT touch the `ElementType` enum's existing members** without checking every consumer; the ATB
  battle system reads element too and is a separate lane.
