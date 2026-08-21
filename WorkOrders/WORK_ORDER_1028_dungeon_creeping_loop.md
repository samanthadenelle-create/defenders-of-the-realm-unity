# WORK ORDER 1028 — Wire the creeping loop: the dungeons are built and have no reason to exist

**Status:** CLOSED — owner ruling 2026-08-21.

> Owner ruling 2026-08-17 (*"open ones follow your recommendations"*): creeping pays **Wisdom** as its
> primary currency, with **gear** as the deep-run bonus. Dungeons become the hero-power faucet — which is
> exactly creeping's role in WC3 — and the talent screen finally has a reason to be visited.
>
> **(a) resources REJECTED**: it would collide with the storage-cap progression and force a WO-947 basket
> decision the dungeon has no business making. **(d) Echo shards REJECTED**: Echo pacing is already ruled
> and this must not disturb it.
>
> ### ⛔ SEQUENCING IS PART OF THE RULING — this is BLOCKED, not READY.
> Wisdom is **worthless until the talent trees are alive**. A Ranger with one usable node has nothing to
> spend it on, so shipping this before **WO-910** produces a reward the player cannot use — which reads
> as a broken dungeon, not a deferred one, and would actively confirm the "lackluster" verdict this WO
> exists to fix. **Do not start implementation until WO-910 lands.** The design review's ordering
> (WO-910 above this) is upheld by the same ruling that chose the currency.
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1028 → 1029 in the same edit
**Lane:** Dungeon ↔ town economy. Connective work; the dungeon tech is done.
**Provenance:** `docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md` §3 ⓷.

---

## 1. The gap

In Warcraft 3, **creeping is why you leave your base**: optional PvE, chosen risk, and the reward feeds
directly back into your power. It is the pressure valve between build-up and conflict, and it is what
makes a map feel like a place rather than a menu.

We have built the hard half and skipped the loop:

| built | state |
|---|---|
| 4 content dungeons | **`PathComplete`** — `dg_bonecrypt`, `dg_ember_deep`, `dg_sunken_vault`, `dg_stairwell_probe` (anchor 2026-08-09) |
| floor-to-floor descent | **solved** — the stair-yaw root cause closed a multi-round hunt |
| torch / oil / darkness risk-reward | **~90% built**, parked warm behind the July-31 demo (memory `dungeon-pillar-roadmap`) |
| **a reason to descend** | ❌ **missing** |
| **a reward that feeds the town** | ❌ **missing** |

This is the **largest built-but-parked value in the tree.** The expensive, genuinely hard engineering
already shipped. What is absent is the cheap part: a pull down and a payoff up.

## 2. What "wiring the loop" means

Three connections, in order:

1. **A reason to go down.** The player needs to *want* to descend — a stated prize, visible from town.
   ⚠ Not a quest checkbox. WC3 creeping works because the player chooses the risk and can see what it
   buys. The dungeon portal already places in the hub (`[Flow:DungeonPortal] placed 'dg_ember_deep'`).
2. **A risk the player elects.** The torch/oil/darkness system **is** this and is nearly done —
   descending deeper trades safety for value. Finishing that ~10% is likely the single best use of
   effort in this WO.
3. **A payoff that feeds the town.** The reward must strengthen the *other* pillars, or the dungeon is
   a side game. This is §3, and it is an owner ruling.

## 3. ⛔ OWNER RULING REQUIRED — what does creeping pay out?

The choice determines whether the dungeon becomes core or a detour:

| payout | effect | risk |
|---|---|---|
| **(a) Resources** (wood/iron/crystal) | Feeds the build queue directly — strongest tie to the CoC half | ⚠ Collides with `stockpiles-cap-capacity` and the WO-947 cost-basket ruling (regular = wood+iron, magical = crystal). **Do not invent a basket** |
| **(b) Wisdom** (talent currency) | Feeds the WC3 half — hero power. Cleanest thematic fit: descend into the past, return wiser | ⚠ Interacts with WO-910; a Ranger with 1 usable node has nothing to spend it on |
| **(c) Gear** | Second progression axis, very WC3 (items + shops). Catalogs + Addressables already exist | Loot tables are real design work |
| **(d) Echo shards / unlocks** | Ties to the Echo lane and the "essence of a guarded person" canon (memory `echo-is-essence-of-guarded-person`) | Echo pacing is already ruled; do not disturb it |

**Recommendation: (b) Wisdom as primary, (c) gear as the deep-run bonus — sequenced AFTER WO-910.**
Wisdom makes the dungeon the hero-power faucet, which is exactly creeping's role in WC3, and it gives
the talent screen a reason to be visited. ⚠ But it is **worthless until the trees are alive** — which is
precisely why the design review ranks WO-910 above this.

## 4. Explicitly OUT of scope

- **Do not re-open dungeon generation, stairs, or navmesh.** That work is closed and was expensive.
  ⚠ `dg_stair_rig` and `dg_descent_probe` are **TEST FIXTURES**, and the `StairUp`/`StairDown`/
  `SEALED_VERTICAL` symbols are a **quarantined control group** marked "⚠ DO NOT DELETE"
  (`DungeonMultiLevelRegression.cs:41-63`). Leave all of it alone.
- Do not add dungeon *content* (new layouts) — four exist and are unused
- Do not touch the raid or wave loops
- ⚠ Bake in an **isolated worktree** if any scene work is needed — memory
  `dungeon-scene-shared-tree-corruption` records a `.unity` going NUL-corrupt in the shared tree

## 5. Acceptance criteria

- [ ] From town, the player can see **what a dungeon run offers** before committing
- [ ] A completed run delivers the ruled payout, and it is **visibly usable in town** — the loop closes
- [ ] Descending deeper is a **player-elected** risk with a stated larger reward (torch/oil/darkness)
- [ ] A player who runs a dungeon is **measurably stronger** in the town/wave pillar afterwards
- [ ] Zero regressions in dungeon generation — all four layouts still `PathComplete`, the control-group
      symbols untouched
- [ ] `FlowTrace` on entry / descent / payout so the first economy bug is one read (§12)

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` — including the dungeon suites
2. Headless: full descent + return, assert the payout lands in the save and survives reload
3. Screenshots of the town-side offer and the return payoff
4. Owner felt-verifies: *"did going down there feel worth it, and did I come back stronger?"*

> **OWNER RULING 2026-08-21 (verbal, this session):** Closed by the owner.
