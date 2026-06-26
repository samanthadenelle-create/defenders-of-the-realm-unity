> ⚠ **OBSOLETE working scratch — retained only as history; do not action.** Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Pending Commits — run in order, then delete this file

Branch: `feat/tower-core-loop`

---

## Commit 1 — WO-334 spec + lane update

```
git add WORK_ORDER_334_tower_placement_rotate_menu.md CLI_LANES_WO_NUMBERS.md
git commit -m "docs: WO-334 TowerPlacementRotateMenu spec (Preview & Rotate panel)"
```

---

## Commit 2 — WO-335 + WO-336 specs (ATB capsule bug + village wall environment)

```
git add WORK_ORDER_335_atb_capsule_primitive_bug.md
git add WORK_ORDER_336_atb_village_wall_environment.md
git commit -m "docs: WO-335 ATB capsule bug + WO-336 torch-lit village wall arena"
```

---

## Commit 3 — WO-337 + WO-338 specs (Echo Hollow dialogue + rebrand)

```
git add WORK_ORDER_337_pet_house_dialogue_overlap.md
git add WORK_ORDER_338_echo_hollow_rebrand.md
git commit -m "docs: WO-337 Echo Hollow dialogue overlap + WO-338 Echo Hollow rebrand"
```

---

## Commit 4 — Lane doc (all numbering updates)

```
git add CLI_LANES_WO_NUMBERS.md
git commit -m "docs: lane update — WO-334 thru 338 slotted; next free WO = 339"
```

---

## Commit 5 — WO-334 implementation (CLI fills in when done)

```
git add Assets/_Modules/Village/UI/TowerPlacementRotateMenu.cs
git add Assets/_Modules/Village/UI/TowerPlacementRotateMenu.cs.meta
git add Assets/_Modules/Village/UI/TowerPreviewCamera.cs
git add Assets/_Modules/Village/UI/TowerPreviewCamera.cs.meta
git commit -m "feat(WO-334): TowerPlacementRotateMenu — Preview & Rotate UIElements panel"
```

---

## Commit 6 — WO-335 implementation (CLI fills in when done)

```
git add <files edited>
git commit -m "fix(WO-335): remove stray purple capsule primitive from ATBBattle scene"
```

---

## Commit 7 — WO-336 implementation (CLI fills in when done)

```
git add Assets/_Modules/BattleATB/ATBBattleEnvironmentBuilder.cs
git add Assets/_Modules/BattleATB/ATBBattleEnvironmentBuilder.cs.meta
git commit -m "feat(WO-336): ATB village wall environment — torch-lit stone gate arena"
```

---

## Commit 8 — WO-337 implementation (CLI fills in when done)

```
git add <files edited — dialogue view + UIDocument sort orders>
git commit -m "fix(WO-337): Echo Hollow dialogue text no longer overlaps choice options"
```

---

## Commit 9 — WO-338 implementation (CLI fills in when done)

```
git add <en.json, .yarn files, VillageSceneBuilder.cs, DESIGN-DECISIONS.md, any renamed files>
git commit -m "feat(WO-338): Echo Hollow rebrand — rename Pet House + echo terminology throughout"
```

---

## Commit 10 — cleanup

```
git rm PENDING_COMMIT.md
git commit -m "chore: remove PENDING_COMMIT.md"
```
