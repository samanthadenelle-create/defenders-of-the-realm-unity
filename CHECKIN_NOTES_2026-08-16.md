# CHECK-IN NOTES — 2026-08-16

**Operator's list for landing a very large uncommitted wave.** Written by the CLI seat at the end of a
~20-lane night. **233 files uncommitted, 61 commits already landed today.**

⛔ **Stage by EXPLICIT PATH, per lane. Never `git add -A`** (CLAUDE.md §11). I broke that rule once
tonight and got away with it, which is exactly why the rule exists — "I checked" is not the same as
being unable to get it wrong.

---

## 1. ⚠ CROSS-LANE DEPENDENCY — READ THIS BEFORE STAGING ANYTHING

Two **untracked** files are shared infrastructure created by the hollow-pass lane:

```
Assets/Editor/Regression/RegressionSourceText.cs   (the shared comment/string stripper)
Assets/Editor/Regression/RegressionOutcome.cs      (the Skip third state)
```

**At least one other lane's suite already depends on them** — `OfflineClaimFanOutRegression.cs` uses
both. Others may too (the shared stripper was the whole point).

> **STAGE THESE TWO FIRST, OR IN THE SAME COMMIT AS ANY SUITE THAT REFERENCES THEM.**
> Committing a dependent suite without them = a broken build at HEAD, and the break will look like it
> came from whichever lane happened to be staged first. Grep the uncommitted set for
> `RegressionSourceText` / `RegressionOutcome` before each commit.

---

## 2. New `.cs` needing `.meta` on first Unity import

`.meta` files are generated on import, not by the lanes. A missing one is a **silent** asset break.

```
BiomeRoadsRegression.cs        CombatCueAuthorityRegression.cs   EconomySweepRegression.cs
ForgeShelfClassKindRegression.cs  MageAbilityIconRegression.cs   OfflineClaimFanOutRegression.cs
RaidRepeatClearRegression.cs   RaidsDiscoverabilityRegression.cs RangedFacingLockRegression.cs
RangerBowFireRegression.cs     RegressionOutcome.cs              RegressionSourceText.cs
```
…plus the non-regression new files (`OfflineClaimCoordinator.cs`, the `Composed*` dungeon set,
`EnemyTypeVfxLibrary.cs`, `WaveSpawnResolver.cs`, `BackendRequestSigner.cs`, and others — take the
full list from `git status`). Also the new `Assets/Resources/Collectors/` folder + its `.asset`.

---

## 3. Gate sequence — and what a red MEANS

1. `DeNelle.Editor.CompileGate.Run` -> **`COMPILE_GATE_OK`**
2. `DeNelle.Editor.DataRegression.RunAll` -> **`REGRESSION_OK <n>/<n> suites`**

**Judge by the MARKER on a FRESH log, never the exit code** (memory `gates-report-success-without-proving-it`).
⚠ A **LICENSE ERROR** appeared three times tonight; those runs did NOT complete and prove nothing. The
runner correctly refuses to emit a marker. If it recurs, it needs an interactive Hub refresh or a
reboot — the one thing a CLI seat cannot do.

**Known-red baseline = 4, all pre-existing:**
| red | status |
|---|---|
| UI-OBSIDIAN violation (`CaravanStatusChip.cs`) | pre-existing |
| `vfx-self-contained` (1 of 69 reaches gitignored art) | pre-existing |
| `vfx-null-slot` (2 findings) | **awaiting an OWNER ruling** — retag or repair |
| `WANDERER BUBBLE` x4 (scene-vs-code drift) | needs the dungeon re-bake, §4 |

> **A 5TH RED IS NEW. DIAGNOSE IT — DO NOT BASELINE IT.** Tonight every "new red" turned out to be
> either a real defect or a test measuring the wrong thing; none deserved a baseline. One was the
> oracle's own estimator (a chord across a bow's diagonal, `atan(0.02/1.0) = 1.15deg`), and widening
> the tolerance would have shipped a suite that measured nothing forever.

---

## 4. Batchmode actions that are NOT code

- **The dungeon re-bake — REQUIRED for the egress trim to be visible.** The exit pads are baked
  GameObjects; the JSON trim (6 ways out -> 2) changes nothing on screen until this runs.
  ```
  Unity -batchmode -quit -projectPath <repo> -buildTarget Win64 \
    -executeMethod DeNelle.Editor.RoomForge.DungeonBaker.BakeLayoutBatch -dungeon <id>
  ```
  once each for `dg_ember_deep`, `dg_bonecrypt`, `dg_sunken_vault`.
  ⚠ **Read `WORK_ORDER_1043` first.** The sanctioned isolated-worktree route was tried tonight and
  **rejected with evidence**: a fresh worktree has none of the gitignored art packs (`polyperfect`,
  `KayKit`, `Blink`) and no `Library`, so it would bake scenes with missing references after an hour
  of cold import — a WORSE artifact than not baking. Recommendation there: shared tree, editor
  closed, **attended**, with a NUL scan on the three scenes immediately after as the acceptance
  criterion.
- **`DeNelle.Editor.CollectorStackPropCatalogBuilder.Build`** — already run once tonight; re-run only
  if the catalog asset is lost.
- Any editor method a lane named in its report (check the enemy-VFX and animator lanes).

---

## 5. ⚠ OPEN OWNER RULINGS — one line each

| # | ruling |
|---|---|
| 1 | **Dungeon re-bake route** — shared tree attended w/ NUL scan (recommended), or copy 2 GB of packs into a worktree? |
| 2 | **Make-good** for upgrades paid for that never landed (lumbermill/farm/forge tier baskets + instant-finish crystals). |
| 3 | **Crafted ring sells for the same as a bought ring** (50% of buy cost) — the dictionary model gives an item no provenance. Tune? |
| 4 | **Back dungeon exit: beacon or quiet pad?** A beacon reads better as a door onward AND removes the last X-ray label, but a regression asserts exactly one beacon per dungeon. |
| 5 | **Repeat-clear loot curve** — shipped default is 0 (a claimed base pays once). Proposed: 1.0 / 0.35 / 0.20 / 0.10 floor. |
| 6 | **Four mage ability icons to tag** — ⚠ `mage.poison` (R ultimate) currently paints a **FIRE STAFF for a poison ability**. Also `mage.drain`, `mage.thunder`, `mage.manaweave`. Candidates listed in the lane report. |
| 7 | **Tunnel name** — "The Rootways" proposed over "The Hollow Roads" (the latter promises Hollowed enemies the design deliberately does not put there). |
| 8 | **WO-437/438/439/440 collision ownership** — first-on-disk and referenced-by-commit disagree. |
| 9 | **`vfx-null-slot`**: retag or repair the 2 ParticlePack prefabs. |
| 10 | **Enemy attack SFX is silent** — the default type-VFX set ships with empty sound lists; picking clips is an owner tag, not a CLI pick. See the audio catalog. |
| 11 | **Base-store drain order** — the 6-level storage ladder contradicts the 2026-08-04 "pallets drain last" ruling from container level 2. Presentation rule, or accept the flip? |
| 12 | **The twelve rulings in `WORK_ORDER_1044_biome_identity.md`** — answerable as "yes to all defaults". |
| 13 | **Wallet-only identity** — ruled ("i want sign in with wallet"), but ⚠ it is now DECOUPLED from the publisher blocker; removing the guest rail has ZERO permission benefit. Do it because it is the model she wants, not under blocker pressure. |

---

## 6. DONE but NOT felt-verified — needs her eyes in the next build

She is the PO and closes tickets; headless cannot judge feel.

- **Ranger**: bow upright held AND sheathed (both were wrong), facing lock on target, the 0.45s planted
  shot with a cancel toast, the bow on slot Q reading "Shoot".
- **Raids**: the greyed `Raids 0/5` face, the `ff.raidtest` bypass, the hero carrying real abilities in,
  death paying what retreat pays, an off-mesh deploy tap now refused.
- **Dungeons**: the lantern at ~200s (was 62.5s), visible keys and locks, a cleared composed run paying
  out, abilities working at all.
- **Economy**: storage to 6 levels, upgrades actually completing, the Echo silo popping the APPLIED
  amount, the dual-family level reset (her lumbermill/farm/forge drop to L1 by her ruling).
- **Talents**: her own class tree instead of the knight's, with stranded Wisdom refunded — ⚠ the refund
  is **silent** (the status label has no reader), so she will see her balance jump with no explanation.

---

## 7. Sequencing recommendation

1. Stage `RegressionSourceText.cs` + `RegressionOutcome.cs` **first**.
2. Gate. Fix any 5th red at the source; do not baseline.
3. Commit lane by lane, by explicit path.
4. Build the exe. Re-read the **manifest merger report** to confirm the three IMPLIED permission lines
   are gone — **that re-read is the acceptance criterion for the publisher reply, not a green build.**
5. Only then send the publisher response (drafted in the permissions lane report).
6. The dungeon bake, attended, when she is at the keyboard.
