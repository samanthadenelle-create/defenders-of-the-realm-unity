# WORK ORDER 1217 - Early economy: flatten the first upgrade step, seed starting Gold, gate crystals to tier 3

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 303/303 suites` (Builds/w3-c, Builds/w3-r). AWAITING OWNER FELT-VERIFY to close.
**Silo:** Economy / balance data
**Origin:** Owner felt-test + rulings, Seeker build `2026.08.26.341419`, 2026-08-26.
Owner verbatim: *"would take hours just to level anything in the start"* · *"so start gold at 200"* ·
*"i think only tier 3 should cost crystals on the arcane tower"*.

**Companion ticket:** WO-1216 opens the kill faucet (~37 of each material per wave). This ticket is
the SINK side. ⚠ They must felt-test **together** - each is tuned against the other, and shipping
one alone reads as either still-a-wall or suddenly-trivial.

---

## Slice A - the first upgrade step is a wall (×3 of build cost)

Measured from `Assets/Resources/Data/Canonical/structures-catalog.json` (catalog v37) this session:

| Archer Tower | Wood | Iron | vs build |
|---|---|---|---|
| build | 360 | 160 | - |
| → L2 | **1080** | **480** | **3.0×** |
| → L3 | 3150 | 1400 | 8.75× |

At WO-1216's ~37 wood/wave, **L2 alone is ~29 waves**. That is the "hours just to level anything."

**⭐ OWNER RULING: flatten the FIRST step to ~1.5× the build cost. Leave L3 as authored.**
Archer Tower L2 becomes **540 wood / 240 iron** (~15 waves). The ladder still climbs steeply into
L3, so late structures remain a commitment - only the first rung stops being a wall.

- Apply as a **curve/rule across the catalog**, not 28 hand-edited rows. ⛔ A hand-edited table is
  the drift shape that produced WO-1137 (a fallback covering 3 of 28 rows, drifted four times).
- ⚠ Some entries author `upgradeCost` explicitly and some do not. **Read every `maxLevel > 1` entry
  before deciding the mechanism** - report how many exist and how many carry an explicit array.
- ⛔ Do NOT touch the second step. L3 stays at its authored value everywhere.

## Slice B - starting Gold is ZERO

`GameStateService.cs:~1025-1035` seeds a new game with `Iron = StartingBudget.StrategicIron` (0) and
`Wood = StartingBudget.StrategicWood` (0). **Gold is never seeded at all, so it starts at 0.**

**⭐ OWNER RULING: start Gold at 200.**

- Seed it the same way Wood/Iron are - through **`StartingBudget`** (`NestedTypes.cs`), which the
  code comment already names as *"the one authoritative pair"*. Make it a triple; do not scatter a
  literal `200` into `ResetToNewGame`.
- ⛔ **Do NOT reintroduce a Wood/Iron founding seed.** The owner ruled those to ZERO on 2026-07-13
  and replaced them with the per-id free-first-build flags, explicitly to prevent
  all-defense-no-town. Gold is a separate currency and a separate ruling.

### ⛔ WHILE YOU ARE IN THIS BLOCK - do not "fix" the line above it

`ResetToNewGame` also contains **`s.Stone = 20`**. That is the **PHANTOM** balance: `GameState.Stone`
is persisted, server-guarded, **displayed nowhere and spent by nothing**. The balance the player
actually sees and spends is `Resources.Food` / `EconomyService.Food`. Retiring it is **WO-1212**,
which carries its own owner ruling (retire it and **DISCARD** the value - never migrate or sum, or
every existing save gains a free +20). **Leave that line exactly as it is.**

## Slice C - crystals gate too early on the Arcane Spire

Current `tower_arcane_spire`:

| | Iron | Crystals |
|---|---|---|
| build | 160 | **200** |
| → L2 | 480 | **400** |
| → L3 | 1400 | 800 |

**⭐ OWNER RULING: only tier 3 costs crystals.** Build and L2 become crystal-free; move their value
into Iron (and/or Wood) so the structure keeps a real price. L3 keeps its 800 crystals.

**Why this matters beyond taste:** there is **still no crystal faucet in town** (PROD-015, open). So
today that L2 Upgrade button **cannot be satisfied by playing at all, at any grind rate** - the owner
hit exactly that on device. Gating crystals to the top tier makes the Spire playable now and turns
crystals into the final-tier ask, which is what PROD-015 is really about.

**⚠ NAME AMBIGUITY - RESOLVE WITH THE OWNER BEFORE EDITING, DO NOT GUESS.**
There are **two** entries and the ruling said "arcane tower":
- **`tower_arcane_spire`** - displayed as **"Arcane Spire"**, and the one on the owner's screen
  (`tmp/screen-102646.png` shows *"Arcane Spire -> L2 · Iron 480 Crystals 400"*). Highest confidence.
- **`arcane-tower`** - a separate `GameplayBuilding`, singleton, build cost **240 iron / 240
  crystals**, no `maxLevel`, so it has no tiers at all and "only tier 3" cannot apply to it.

Since `arcane-tower` has no tier ladder, the ruling can only mean the **Spire**. Apply it there and
**ask the owner whether `arcane-tower`'s flat 240 crystals should also move** - do not change it on
inference.

⚠ Also confirm against the **WO-947 cost-basket ruling** (regular structures = wood+iron; magical =
crystal-based) that moving the Spire's lower tiers to iron does not violate the separation. If it
does, that is an owner ruling, not an implementer's call.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. Catalog **version bumped** and BOTH dual copies written byte-identical
   (`Resources/Data/Canonical` WINS at runtime; `StreamingAssets` is the editable source).
   ⚠ The version check does not assert that a change bumps it - 24 catalogs have changed with no
   bump. Bump it deliberately.
3. ⭐ A regression asserting the first-step ratio holds across **every** `maxLevel > 1` entry, so a
   future added structure cannot silently reintroduce a ×3 first rung.
4. ⭐ A regression asserting a NEW GAME starts with **Gold 200, Wood 0, Iron 0** - the last two pin
   the 2026-07-13 zero-seed ruling against accidental restoration.
5. ⭐ A regression asserting `tower_arcane_spire` build and L2 carry **zero crystals** and L3 carries
   nonzero. Prove it RED against today's catalog first - a test that passes before the change is
   decoration (WO-1138).
6. **A DEVICE SCREENSHOT of the Manage screen** showing the new L2 costs, opened and looked at.
7. Owner felt-verifies and CLOSES. Not the CLI.

## What NOT to touch

- ⛔ `s.Stone = 20` (WO-1212's territory - see Slice B).
- ⛔ The Wood/Iron founding zero-seed (owner ruling 2026-07-13).
- ⛔ Second-step (L3) upgrade costs anywhere.
- ⛔ Repair pricing (`WallRepairController.CostForFraction`). The owner chose the faucet over the
  cost; moving both makes the felt-test unattributable.
- ⛔ `RepoProps.MaxStructureLevel` - the single level ceiling. Never re-hardcode a level cap.
