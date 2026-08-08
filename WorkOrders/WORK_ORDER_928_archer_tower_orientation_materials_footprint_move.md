# WO-928 — Archer Tower: orientation, materials, footprint parity, and the Move path

> ## ⏸ PARKED 2026-08-08 — owner ruling: "I'm not as focused on the tower... stick that aside"
>
> **Defect A (on its side) is FIXED AND READY TO TEST.** `SkinOptions.PreservePrefabRotation` shipped
> in `bb6dc010`, gated `COMPILE_GATE_OK` + `REGRESSION_OK 130/130`. It reaches both `Create` and
> `ReskinForLevel` via `StructureFactory.OptsFor:393`. **Not felt-verified** — the owner needs to see
> the L3 tower stand up in a fresh exe.
>
> **Defect C (footprint) is expected to fall out of A** — upright, `Fit` measures the right axis and
> the 8.34x scale should drop to ~4.8x. **Unverified. Do not claim it without the numbers.**
>
> **Defects B (material) and D (Move path) are UNSTARTED**, root causes known, parked deliberately.
>
> The dungeon stairs (WO-927) take priority: they block every dungeon, which blocks the store
> screenshots, which blocks submission. Return here after.

**Status: DEFECT A FIXED, READY TO TEST · B/C/D PARKED** (was: READY TO IMPLEMENT; §5 instrumented run is a hard gate on §6)
**Date:** 2026-08-08 · **Priority:** High · **Lane:** Structures / BuildMode / VFX-materials
**Reported by:** the owner, felt-test 2026-08-08, F8 seq 2181-2189 (`Main_Castle_Overworld`)
**Owner ruling 2026-08-08:** these four ship as ONE cluster. Footprint parity requires **explicit
verification — do not assume parity.**

⚠ **This is NOT WO-902.** That WO is correctly SUPERSEDED, but only because its *art ladder* would revert
the owner's 2026-08-06 all-wood ruling. It says nothing about orientation, material application, footprint
or the Move path. Those are live, unticketed defects.

---

## 1. The four defects

| # | Symptom (owner's words) | State |
|---|---|---|
| A | *"Archer Tower 3 still on its side"* | OPEN — prefab bake RULED OUT (§3), cause is runtime |
| B | *"and not colored"* | OPEN — art presence RULED OUT (§3), cause is runtime |
| C | *"sizing of tower three is much larger of a footprint than tower one... otherwise we could have a problem with real estate when we upgrade"* | OPEN — catalog declares ONE footprint for all tiers (§4) |
| D | Select tower, tap Move: ghost shows **tower one**, red/green tracks the cursor, but no click commits and it snaps back | **ROOT CAUSE PROVEN** (§2) |

---

## 2. Defect D — PROVEN from captured data

`BuildModeController.cs:954` emits, on every selection of a placed structure:

```
[Flow:Build] SelectLoop: tap SELECTS '<itemId>' - Move/Upgrade/Sell panel path entered.
```

Measured in the owner's session `Player.log` (**378,232 lines**, nothing muted — `FlowTrace.Only`/`Mute`
have no callers outside `FlowTrace.cs` itself):

| Trace | Hits |
|---|---|
| `[Flow:Build]` — the system is alive and emitting | **377** |
| `SelectLoop: tap SELECTS` | **0** |
| `Two-step RE-DROP` (the commit step, `:1144`) | **0** |
| `d-pad move vector CONSUMED` (`:3496`) | **0** |
| `[Flow:Amesh]`/`[Flow:Ghost]` | **3** |

**The tap never entered the select path.** With `SelectLoop` never entered, `Two-step RE-DROP` can never
run, so the structure returns to its stored position — the snap-back is the *correct* behaviour of a flow
whose commit was never reached. The green/red ghost is therefore **not** a preview of the owner's selection,
which is consistent with it rendering **tower one instead of tower three**.

⚠ **Do not "fix" the snap-back.** The snap-back is a symptom. The defect is upstream, at the selection gate.

---

## 3. Two theories already KILLED — do not re-run them

**A-theory "the -90 X bake is missing from a tier prefab" — REFUTED 2026-08-08.**
The catalog row's own `orientation.note` predicts this failure mode precisely, so it was the obvious
suspect. It is wrong. All three prefabs carry a real baked rotation on the model child:

| Prefab | quaternion (x, y, z, w) | angle about -X |
|---|---|---|
| `Tower_Wooden_Watchtower` | -0.6832738, 0, 0, 0.73016226 | **86.21 deg** |
| `Tower_Wooden_Watchtower_L2` | -0.7071068, 0, 0, 0.7071068 | **90.00 deg** |
| `Tower_Wooden_Watchtower_L3` | -0.7071068, 0, 0, 0.7071068 | **90.00 deg** |

None is at identity. L2 and L3 carry exactly the -90 the owner measured. **The prefabs stand up.**
*(Secondary finding, separate and minor: the BASE tier is 3.79 deg off the owner's measured -90. That is a
lean, not a topple, and does not explain defect A — but it contradicts the note's claim that "all three
need X -90" and should be reconciled.)*

**B-theory "the art is missing / two-machine drift" — REFUTED 2026-08-08.**
Every asset resolves on disk: `Tower_Wooden_Watchtower.fbx` + `.prefab` + `.mat`, the full `_Tex` folders
(basecolor / normal / metallic / roughness / rm), and the L2/L3 ladder with its nine `_part_N` materials.
This is **not** the class that caused the 2026-07-15 magenta ground.

**What both refutations leave:** the defect is in the RUNTIME path — which prefab is actually instantiated
for this instance, and whether its materials survive that path. ⚠ `_bug22` records that the retired
polyperfect ladder **still lives in `Resources/Structures`** and is still consumed by `CastleHubBuilder`,
`VillageSceneBuilder.Walls`, `TowerDataSeeder`, `GarrisonSceneBuilder` and `EnemyStrongholdBuilder`. A tower
seeded by one of those paths is a live candidate for "not the ladder art at all."

---

## 4. Defect C — what the catalog actually declares

`structures-catalog.json`, row `tower_ground_archer`:

```
repo.placement = { mustSitOn: Ground, footprint: 1.75, noOverlap: True, checkAffordable: True }
repo.heightMul = 1.2
repo.upgradeVisualPath = [ Structures/Tower_Wooden_Watchtower_L2, Structures/Tower_Wooden_Watchtower_L3 ]
```

**There is exactly ONE `footprint` value (1.75) for the whole entry. There is no per-tier footprint.**
`upgradeVisualPath` swaps the VISUAL only. So the *claimed* ground footprint is constant across all three
tiers, and the owner is observing the **visual outgrowing its claim** — which is precisely the real-estate
hazard she named: the claim does not move, so the collision/placement grid keeps reserving 1.75 while the
mesh covers more.

**The mechanism to test** (from the row's own note, verified as prose not behaviour):
`VisualFactory.Skin` **fits to height**. Height-normalisation leaves WIDTH a free variable — two models with
the same height and different proportions occupy different ground area. The note documents the pathological
version: a mis-measured axis gives `4.8 / 0.519 = 9.25x` instead of `4.80x`, **1.93x oversized**.

⚠ **The owner has ruled that parity must be VERIFIED, not assumed.** Do not write a fix that presumes the
tiers are proportionally similar.

---

## 5. REQUIRED — the instrumented run (BLOCKING, owner-ordered)

> *"After you create the WO, run the instrumented placement + upgrade + move sequence and report the
> footprint numbers + material application results. Do not guess."*

| # | Measurement |
|---|---|
| M1 | For each tier L1/L2/L3: the **resolved prefab path actually instantiated** (prove it is the ladder, not a polyperfect survivor) |
| M2 | For each tier: **measured world bounds** (X, Y, Z) after fit + orientation, and the applied **scale factor** |
| M3 | For each tier: **ground footprint** (X by Z extent) vs the declared `footprint: 1.75` — the parity number |
| M4 | For each tier: **renderer material count + shader names**, to locate where colour is lost |
| M5 | The instance's **final world rotation** vs its prefab-baked rotation — does anything downstream re-rotate it |
| M6 | A **Move attempt trace**: why `SelectLoop` is not entered — is the tap not hitting the collider, is the structure not registered as selectable, or is the gate upstream |

Add `FlowTrace` where the run is silent. Defect D's whole signature is a MISSING trace, so the instrumentation
must be extended before it can speak.

---

## 6. Acceptance

- [ ] M1-M6 captured and recorded in this WO.
- [ ] Defect D: selecting a placed structure emits `SelectLoop: tap SELECTS`, and a Move commits via
      `Two-step RE-DROP` to the chosen cell.
- [ ] Defect D: the Move ghost shows **the selected structure**, not another one.
- [ ] Defect C: ground footprint is verified across all three tiers against the declared `1.75`; any tier
      that exceeds its claim is corrected, or the claim is re-authored with an owner ruling.
- [ ] Defects A + B: the tower stands upright and renders with its authored materials.
- [ ] A regression pins whichever invariant the fix establishes (orientation, footprint-vs-claim, or both).
- [ ] Owner felt-verifies and closes (CLAUDE.md §13 — the PO closes, not the CLI).

---

## 7. Notes for whoever picks this up

- **`ReskinForLevel` deliberately does NOT apply `entry.orientation`** ("tier models rely on their
  prefab-native orientation"). Do not "fix" that by applying orientation to tiers — the note explains it
  would double-apply on top of the prefab bake.
- **`orientation.manual = true` on this row is LOCKED and INTENTIONALLY ZERO.** Do not copy a -90 into it.
  Two reasons are recorded at source and both are load-bearing.
- The note also carries a self-correction worth heeding: an earlier session inferred from Tripo node data
  that the FBXs "import upright on their own." That inference was **wrong** — all three carry identical node
  data yet measure differently. **Only measurement settles it.**
