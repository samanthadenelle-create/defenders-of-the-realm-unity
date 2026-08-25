# WORK ORDER 1040 — Dungeon completion: overlapping text, and a payout that feeds nothing

**Status:** BLOCKED — §3 pending an owner ruling; the ticket is NOT wholly closed. §2 CLOSED 2026-08-23 — OWNER FELT-VERIFIED on the go-live Seeker build (owner: *"1040 worked"*), alongside *"dungeons work"* and *"locked doors in dungeons worked"*; F8 `NO_CAPTURE` across the run. ⚠ **§3 REMAINS OPEN — this ticket is not wholly closed.** §2 was the overlapping-text half; **§3 (the payout that feeds nothing) is still SPEC PENDING AN OWNER RULING** and no felt-test can close a ruling that has not been made. Do not let the §2 close read as the whole ticket. Prior status: FIXED — AWAITING OWNER FELT-TEST TO CLOSE / §2 DONE (2026-08-17, commit-verified) · §3 SPEC pending owner ruling

> **§2 — the three-block text collision — IS FIXED.** Shipped inside **eff761fcc**
> (`feat(dungeon): the rough stone economy`), not under this WO's own number, which is why the board
> never caught it. The proof is in the code itself:
> `Assets/_Modules/Dungeons/DungeonTreasurePanel.cs:242` — *"THIS REPLACES EnsureBand, AND THE
> DIFFERENCE IS THE WHOLE BUG FIX"*. `EnsureBand` pinned a growing payout band against
> fixed fractional anchors, so at five reward lines it expanded up into the heading.
>
> **§3 — the graded-runs payout (stats → rating → reward tier) — is still a SPEC** and still needs the
> owner ruling in §3b. Nothing has delivered it. WO-1112 shipped *a* payout for composed dungeons,
> which is adjacent but is NOT this: it wires a reward into composed runs; §3 is about the reward
> being EARNED by run quality.
>
> ⚠ This WO is the clearest instance of the pattern worth watching: **work shipped under a different
> WO's number.** Nothing references 1040, so no automated sweep can ever reconcile it — only a human
> reading the code. The owner spotted it (2026-08-17: *"1040 the exit text and the treasure payout,
> wasn't that completed?"*) — she was right about half of it.
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1040 → 1041 in the same edit
**Lane:** Dungeon reward presentation + reward economy. §3 overlaps **WO-1028** — read §3 before acting.
**Provenance:** owner 2026-08-16, verbatim: **"Finishing a dungeon feels lackluster"**, with a
`TREASURE FOUND` screenshot showing three text blocks rendering on top of each other.

---

## 2. DEFECT — three text blocks collide. Cause found, and it is a KNOWN class

The screenshot shows *"The treasure holds:"*, the five-line payout list, and *"First clear -- a new
recipe is remembered."* all painting over one another.

`DungeonTreasurePanel.cs:128-141`:

```csharp
var payout = ElarionUiKit.Label(content, sb.ToString(), 0.40f, 0.66f, ...);
EnsureBand(payout, LinePx * Mathf.Max(1, lines.Count));   // ← GROWS with line count
...
var unlock = ElarionUiKit.Label(content,
    "First clear -- a new recipe is remembered.", 0.30f, 0.38f, ...);   // ← FIXED fraction
```

**The payout band GROWS with the number of reward lines; its neighbours sit at FIXED fractional
anchors.** At five lines the payout expands past its `0.40–0.66` slot — upward into the heading, and
downward into the `0.30–0.38` first-clear line. Exactly the triple overlap on screen.

⚠ **This is the documented WO-865 failure class**, the same one that produced WO-841 / WO-852 and
WO-1030's clipped dialogue options: **a variable-height element among fixed fractional neighbours.**
The in-code comment even states the intent — *"keeps every line in ONE fixed band whose height is
computed from the line count"* — but computing a band's height does nothing if nothing else moves.

**Fix:** the neighbours must be **driven by the measured content** (stack from a measured layout, or
pin fixed-pixel bands that are re-pinned after measuring), not by fractions authored against an
assumed line count. ⚠ Test at **1 line and at 8+** — a 2-item payout hides this completely, which is
presumably how it shipped.

## 3. THE REAL POINT — the payout only feeds the dungeon itself

The reward for clearing a dungeon, from the screenshot:

```
Dry Reed x2 · Oil-Soaked Cloth x2 · Ember Resin x2 · Moonbloom Herb x2 · Spring Water x1
```

**Every one of those is a dungeon consumable** — torch / oil / darkness inputs (memory
`dungeon-pillar-roadmap`). So clearing a dungeon makes the player **better at dungeons** and does
**nothing** for the town, the hero, or any other pillar.

That is a **closed loop**, and closed loops feel lackluster no matter how they are presented. No amount
of fanfare fixes a reward that does not change anything the player cares about outside the room they
just left.

⚠ **This is exactly WO-1028's §3 gap, now with hard evidence.** That ticket says the missing piece is
*"a payoff that feeds the town"* and lists the candidate currencies for an owner ruling
(resources / Wisdom / gear / Echo shards). **This screenshot is the proof that the gap is real and
player-visible.**

**Therefore: do NOT design a new reward economy here.** Cross-reference WO-1028, take the owner's
ruling there, and let this ticket present whatever that ruling pays out. Two tickets inventing reward
currencies independently is how a game ends up with two economies.

### What THIS ticket owns: the moment, once the payout is worth having

In WC3 terms a creep camp clear is a **beat** — XP, a level, an item, a visible power step. Ours is a
list and a `Take` button. Presentation notes, all cheap, none of which substitute for §3's ruling:

- **Lead with what changed for the player**, not with an inventory manifest. "A new recipe is
  remembered" is the most interesting thing on this screen and it is currently *underneath* the item
  list, in the collision
- **Give the first-clear beat its own weight** — it is a genuine progression event, not a footnote
- **Sequence the reveal** rather than painting the final state instantly. ⚠ Marquee moments are
  sanctioned for special cases (memory `sequenced-vfx-special-cases-for-special-events`) — but that
  licenses richer **presentation**, never a second spawner or pool
- ⚠ **Do not add a second CTA.** The single `Take` is correct — the in-code comment ties it to *"the
  same bottom-row budget as the Echo beat"*, i.e. a deliberate shared convention. Keep it

## 3b. ★ OWNER RULING 2026-08-16 — GRADED RUNS. Stats → rating → reward tier.

> *"you would think completing a dungeon would give a stone or a weapon or a ring (Ring is strong
> option since we have none)"* · *"can we capture details, time enemies killed, potions used, deaths,
> and time spent?"* · *"and using those rate the run, and base that as basis for reward tier"*

**This answers WO-1028 §3: the payout is GEAR, and the tier is EARNED.** It also fixes "lackluster" at
the root — the run's *quality* starts mattering, not just its completion. This is the WC3 creep-camp
beat and the roguelike after-action report in one.

### ✅ CORRECTION — rings ALREADY EXIST. This is far cheaper than assumed

> *"Ring is strong option since we have none"*

**We have five.** Measured at source, 2026-08-16:

| fact | evidence |
|---|---|
| `accessories.json` | **10 entries — 5 rings + 5 amulets** (`ring_iron` Iron Band, `ring_steadfast`, `ring_embercoil`, `ring_heartward` Heartward Seal, `ring_firstlight` Ring of First Light) |
| typed model | `AccessoryDef.cs` — *"rings + amulets, **WO-543**"* |
| live equip slots | `EquipVM.cs:268` — `SlotRing` / `SlotAmulet` → `EquipAccessoryById` |
| VFX already slot-aware | `ArmorVfxMap.Resolve(armor, **ring**, amulet)` |

**So the ring slot, catalog, equip path and VFX hooks are all shipped.** A ring drop is a **drop-table
+ data** job, not a new equipment system. ⚠ **Do not build a ring system — populate a drop table.**
(If more ring *content* is wanted, that is authoring rows in `accessories.json`, still not a system.)

### The stats — essentially NOT captured today

Grep across `Assets/_Modules/Dungeons` + `SaveSchema` found only incidental hits (`Bryn.cs` "deaths",
`RandomEncounterTable` "elapsed"). **There is no run-stat record.** So this ticket adds one:

| stat | note |
|---|---|
| enemies killed | ⚠ decide whether *encountered-but-skipped* counts — it decides whether stealth/rush is a valid style |
| potions used | a resource-efficiency signal |
| deaths | ⚠ if checkpoints exist (`Checkpoint.cs` is in the tree), define whether a checkpoint reload is a "death" |
| time spent | ⚠ see the warning below |

**Instrument it as a run record** (`FlowTrace` on open/close), persisted so the summary survives a
reload, and shaped as **data** so the rating rubric can change without touching capture.

### ⛔ OWNER RULING NEEDED — the rubric. Three traps to decide first

**(1) ⚠ Rewarding SPEED punishes EXPLORATION.** If time dominates the grade, players stop looking at
the dungeon you spent months building, and the torch/oil/darkness risk system becomes something to
rush past rather than engage. **Recommendation: time as a *bonus* band, never a primary weight** — or
graded against a generous par rather than raw minutes.

**(2) ⚠ Potions-used and deaths double-punish a hard fight.** A player who survives a brutal room by
drinking three potions is playing *well*. Counting both the potions and the near-death against them
grades resourcefulness as failure. **Pick one efficiency axis, or weight potions far below deaths.**

**(3) ⛔ A completed run must ALWAYS pay something.** Grade the **tier**, never the existence of a
reward. Both reference games always pay: CoC gives loot even on a 1-star, WC3 gives XP even for a
sloppy creep. A zero-reward completion reads as punishment for finishing — the opposite of what this
ticket is for.

### Presentation

- The grade is the **headline** of the panel — it is what changed, and §3's note (lead with what
  changed, not an inventory manifest) applies doubly now
- Show **why** the grade landed — the four stats *are* the explanation, and a grade without its reason
  is arbitrary. This is also what teaches the player to improve
- ⚠ **Tier must not be hue-only.** S/A/B-style ranks default to gold/silver/bronze; the owner is
  red/green colourblind, so carry it with **letter/shape/position** and verify in greyscale
- The first-clear beat and the grade are **different things** — do not merge them

## 4. Do NOT

- Do not invent a reward currency here (§3 — that is WO-1028's ruling)
- Do not re-open dungeon generation / stairs / navmesh (WO-1028 §4: closed, expensive, and
  `dg_stair_rig` / `dg_descent_probe` are quarantined test fixtures)
- Do not fix the overlap by shrinking the font — that hides the class and it will return with a longer
  payout
- Do not restyle to a bespoke chrome; this panel is kit-built and should stay so

## 5. Acceptance criteria

- [ ] **No overlap at 1, 5 and 8+ payout lines** — verified by capture at each
- [ ] The first-clear line is fully legible and reads as the headline beat
- [ ] Layout is driven by measured content, not fractional anchors assuming a line count
- [ ] Reward composition follows **WO-1028's ruling** — not a currency invented here
- [ ] One CTA (`Take`) preserved
- [ ] Legible in greyscale; ASCII-only labels (tofu on device otherwise)
- [ ] Verified at **2670x1200**, the Seeker's real surface

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. `UI_CAPTURE_OK` — **open the PNGs** at the three payout sizes; the overlap is invisible to markers
3. Owner felt-verifies: *"did finishing that feel worth the run?"* — ⚠ if the answer is still no after
   the layout fix, the remaining gap is §3 and belongs to WO-1028, **not** to more presentation
