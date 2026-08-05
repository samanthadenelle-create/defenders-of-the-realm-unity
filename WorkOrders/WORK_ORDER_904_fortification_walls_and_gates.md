# WORK ORDER 904 — Fortification: upgradeable walls AND gates around what's worth stealing

**Status:** SPEC — **blocked by design sequencing, see §2. Do not implement standalone.**
**Minted:** 2026-08-04 (CLI), owner directive
**Silo:** Village / defensive structures. Data-first; code only where the gate ladder needs a seam.
**Design pillar:** `docs/design/OWNER_RULINGS_2026-08-04.md` §0 (the satisfaction loop) + §0a
**Adjacent:** WO-857 / WO-901 Phase F (bank cap) · WO-853 (structures are targetable) · WO-674 (player wall
system) · WO-114 (wall upgrade tiers, historical)

---

## 1. Goal

> **Owner, 2026-08-04:** *"Eventually when they build their own bases I want them to be able to upgrade
> walls around those higher or stronger for that reason."*

The *reason* is §0 of the rulings doc — **the satisfaction loop**: a player hoards toward a big upgrade,
and a raid can take a portion of it. Walls are how the player chooses to **fortify** instead of spending
or accepting the risk. This WO makes the fortification side of that choice real and, critically,
**closes the hole that would defeat it**.

---

## 2. ⚠ SEQUENCING — this ships AFTER raid-steal, not before

**Fortification is only meaningful once there is something to lose.** Building the ladder first ships a
cost with no reason to pay it — which is the exact shape of the four authored-but-unreachable systems
found on 2026-08-04 (the Crystal Mine that never paid, the Windmill food perks that reached nothing, the
tower upgrade ladder nothing consumed, `CollectorStackView` with zero callers).

**Order: raid-steal (§0 of the rulings doc) → THEN this.** If this is implemented first it must be
explicitly acknowledged as pre-building a cost with no payoff.

---

## 3. State of the scaffolding — verified at source 2026-08-04

| Piece | Status |
|---|---|
| Player builds their own base | **LIVE** — strategic placement always on; the flag was removed |
| Walls **upgrade** | **LIVE** — `wall_wood` + `wall_stone` author `maxLevel: 3` and a 2-rung `repo.upgradeCost` |
| Walls **take damage** | **LIVE** — WO-853 closed the disjoint `IDamageable` / `IDamageableStructure` contract |
| **Gates upgrade** | ⚠ **MISSING — this WO's core defect** |
| Raid steals from the hoard | **NOT BUILT** — §2 |

**Walls were among the ONLY catalog rows authoring an upgrade ladder** before the towers got one on
2026-08-04. The wall half of this feature is largely already there.

---

## 4. ⚠ THE CORE DEFECT — the gate has no ladder

Verified in `Assets/Resources/Data/Canonical/structures-catalog.json`:

| id | displayName | `maxLevel` | `repo.upgradeCost` |
|---|---|---|---|
| `wall_wood` | Wooden Palisade | **3** | **AUTHORED (2 rungs)** |
| `wall_stone` | Stone Wall | **3** | **AUTHORED (2 rungs)** |
| **`gate_stone`** | **Stone Gate** | **none** | **NONE** |

`RepoProps.maxLevel` defaults to **1** (`RepoProps.cs:62`) and `BuildModeController.MaxLevelFor` clamps
`1..3` (`:2453`), so the upgrade verb evaluates `1 >= 1` and toasts **"Max tier reached."** on a
freshly-placed gate — **the identical failure mode WO-856 just fixed on the Crystal Mine.**

**Why this is fatal to the feature and not a nice-to-have:** a defensive perimeter is only as strong as
its weakest authored point. A player upgrades `wall_stone` to level 3 and the gate stays at level 1
forever, so **a raider simply walks the door while the reinforced walls stand untouched.** The
fortification spend buys nothing. **The gate ladder is not an add-on to this WO — without it the WO has
no effect.**

---

## 5. Scope

### 5.1 Author the gate ladder (DATA — both canonical copies, byte-identical)
`gate_stone.repo` gains `"maxLevel": 3` and a two-rung `"upgradeCost"` array, schema matching
`wall_stone` verbatim.

**Pricing rule:** a gate rung should cost **at least** the equivalent wall rung — it is the deliberate
breach point and the shortest path to the hoard. Price it against the post-WO-855 economy (early ~2,304
basket/hr, mid ~16,046, late ~141,480) and state the assumption. **Do not** make it a crystal sink;
crystals are the bottleneck currency and the WO-830 guard applies.

**⚠ Check for other unladdered defensive rows in the same pass.** `gate_stone` was found by inspection,
not by a sweep — sweep every `CatalogType` defensive row for `maxLevel`/`upgradeCost` and report any
other row that cannot be upgraded. Assume there are more.

### 5.2 Make the ladder MEAN something (verify, then fix only if broken)
Authoring `maxLevel` makes the verb *reachable*; it does not make the upgrade *do* anything.
**Verify at source** that a level-2/3 wall or gate actually gets more HP:

- `BuildModeController.ApplyTierStats` (`:2401-2443`) is a **hard-coded three-branch switch** reaching
  only `DefenseTower`, `ArcaneTower` and `WallSegment`. **Confirm `WallSegment` receives an HP scale and
  determine whether a GATE routes through it at all** — `Gate.cs` implements `IDamageableStructure`
  separately.
- If the gate has no receiver, **that is the real work of this WO**, and it is code. Do NOT bolt a
  fourth branch onto that switch without flagging it: a generic `IStructureLevelReceiver` seam is already
  logged as its own WO from WO-856 §9, and this is the second consumer that wants it. **Report; let the
  owner choose** between a fourth branch now and the seam.

### 5.3 Player legibility (small, but the feature is invisible without it)
The player must be able to tell a level-3 wall from a level-1 one **without opening a panel** — height,
material, or crenellation. Owner's words were *"higher or stronger."*

- ⚠ **"Higher" has a camera consequence.** `WORK_ORDER_156_camera_pivot_high_walls.md` and
  `WORK_ORDER_204_camera_rig_wall_occlusion.md` both exist — taller walls have previously fought the
  camera rig. **Read both before raising any wall height.** If height is chosen, the capture must prove
  the camera still frames the town.
- **Never colour alone** — the owner is red/green colourblind. Silhouette or material, plus text where a
  panel shows it.
- `UI_CAPTURE_OK` if anything visual changes: open the PNGs.

---

## 6. Acceptance criteria

- [ ] A freshly-placed `gate_stone` offers an Upgrade verb — **not** the toast "Max tier reached."
- [ ] Gate levels 2 and 3 charge the authored cost and the gate is **measurably tougher** (state the
      HP/stat delta and where it is applied, with a `file:line`).
- [ ] A level-3 wall and a level-1 wall are **visually distinguishable** in a landscape capture.
- [ ] No defensive catalog row is left unladdered without an explicit, recorded reason.
- [ ] Both canonical JSON copies byte-identical.
- [ ] `COMPILE_GATE_OK` (if any `.cs`) + `REGRESSION_OK <n>/<n>` + `UI_CAPTURE_OK` (if visual).

---

## 7. Regression

Extend the existing build-economy / structure oracles (find the right home; do **not** create a new suite
if one fits):

- **`[defensive-ladder-complete]`** — sweep every defensive catalog row (walls, gates, and any other
  `CatalogType` that takes damage): if the row is upgradeable it must author BOTH `maxLevel > 1` and
  `upgradeCost` with `maxLevel - 1` rungs. **Fails today on `gate_stone`** — that is the proving case.
- **`[level-changes-stats]`** — a level-3 wall/gate resolves strictly better defensive stats than level 1.
  This is the case that catches "the ladder is authored but `ApplyTierStats` never reaches it", which is
  the WO-856 defect class repeating on a different building.
- Gate pricing is monotonic and never crystal-denominated.

---

## 8. What NOT to touch

- **Raid-steal / loot** — §0 of the rulings doc, its own WO, its own owner conversation.
- **The bank cap / `EconomyService.Grant` clamp** — WO-857 / WO-901 Phase F.
- **`ApplyTierStats`'s hard-coded switch** — do not generalize it here (WO-856 §9 owns that seam).
  Report if this WO needs it.
- **Wall placement / drag-line building** — WO-674, WO-708.
- **Any `.unity` scene file.** Walls must **STAY on layer `Structure`** — it is the tower line-of-sight
  mask (WO-853 §4).

---

## 9. Open question for the owner

**Should the gate be the *weakest* point by design, or equal to the walls?** CoC makes walls uniform and
has no gate at all; this game has one. If the gate is deliberately the soft spot, that is a legitimate
design (it creates a defended choke point and gives troops a path), but it must then be **cheaper** than
a wall rung, not more expensive — and the player must be able to read that it is the weak point. If it
is meant to be equal, price it at or above the wall. **This WO cannot price the gate until that is
answered.**
