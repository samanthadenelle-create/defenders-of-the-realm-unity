# WORK ORDER 127 — Tower "Manage All Towers" UI Shows Stale Level 1 After Upgrade

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30
**Priority:** High — live playtest bug; the upgrade sink looks broken to the player
**Lane:** Combat / UI (code only — no scene files, no bakes)
**Scope:** Small — one screen in `BuildMenu.cs` reads the wrong object's level and its "Upgrade" button is a stub.
**Files to edit:** `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` (only)

---

## 1. Symptom (owner playtest report)

> "If you click Build Tower you get a tower. If you click Upgrade on the last tower it
> upgrades. But then if you go to Manage All Towers it still shows Level 1."

A tower's runtime level upgrades correctly in-world, but the management list displays a
**stale Level 1** for it. UI state-desync between the live tower and the manage/upgrade panel.

---

## 2. Root cause — the panel reads a DIFFERENT object than the one that upgrades

There are **two unrelated "tower" types** in the scene, and the manage UI reads the wrong one.

### What actually upgrades (correct, do NOT touch)
- Placed towers are **`Tower`** components (spawned by `TowerPlacementSystem` →
  `TowerConstructionQueue` → `Tower.Initialize(data)`).
- `Tower.Upgrade()` mutates `_currentLevel` correctly — confirmed:
  `Tower.cs:513-537` (`_currentLevel++`), exposed live via `Tower.CurrentLevel` (`Tower.cs:137`).
- The dedicated **`TowerManagerPanel`** (the panel the fallback "Manage Towers" button opens,
  `BuildMenu.cs:388-392`) already reads the level **live** from the `Tower` and auto-refreshes:
  - row label uses `t.CurrentLevel` — `TowerManagerPanel.cs:144`
  - 0.5 s refresh tick — `TowerManagerPanel.cs:74`
  - re-`Refresh()` right after `Upgrade()` — `TowerManagerPanel.cs:171`
  **This panel is correct. Leave it alone.**

### What displays the stale level (the bug)
The **BuildMenu UXML "Upgrade Tower" screen** enumerates **`Building`** components (a totally
separate type) instead of `Tower`, and prints `Building.Level`:

- enumerates `Building` of type `ArcaneTower` — `BuildMenu.cs:617-627` (`RenderUpgradeTower()`)
- row label prints **`b.Level`** — `BuildMenu.cs:646-648` (`BuildTowerSelectRow`):
  `"... (Lvl " + b.Level + ")"`
- result line prints `b.Level + 1` — `BuildMenu.cs:663` (`BuildUpgradeInfoBlock`)

`Building.Level` is a `[SerializeField] private int _level = 1;` (`Building.cs:76`, getter
`Building.cs:108`) that is **never mutated by any upgrade path** — and crucially, **placed towers
are `Tower` GameObjects, not `Building`s**, so they carry no live level at all. The matching
"Upgrade" button on this same screen is an explicit **stub** that only logs and never calls
`Tower.Upgrade()` — `BuildMenu.cs:668-673`.

**Net:** the manage/upgrade screen is bound to a stale serialized field on the wrong type, so it
is permanently pinned at "Lvl 1" regardless of how many times the live `Tower` upgrades.

> Note on the dedicated `TowerManagerPanel`: if the owner reached "Manage All Towers" via the
> fallback-menu button (`BuildMenu.cs:388`) and *still* saw Lvl 1, that panel is read-correct
> (`t.CurrentLevel`), so the report maps to the BuildMenu UXML "Upgrade Tower" screen. The fix
> below repoints that screen at the live `Tower` list so **both** routes agree. If repro pins it
> specifically to `TowerManagerPanel`, capture a screenshot — its code path reads live and would
> indicate a stale `Tower` reference instead, which this WO's primary fix does not cover.

---

## 3. The fix (precise)

In **`BuildMenu.cs`**, retarget the Upgrade-Tower screen from `Building` to the live `Tower` list
and make the action actually upgrade. Keep changes inside this one file (UI/combat lane).

1. **Enumerate live towers, not Buildings.**
   In `RenderUpgradeTower()` (`BuildMenu.cs:609-638`) replace the
   `FindObjectsByType<Building>(...)` + `b.Type != BuildingType.ArcaneTower` loop with
   `FindObjectsByType<DeNelle.Village.Tower>(FindObjectsSortMode.None)`.
   Change the selection field `_selectedTowerForUpgrade` (currently `Building`,
   `BuildMenu.cs:102`) to type `Tower`.

2. **Display the live level.**
   In the row builder (currently `BuildTowerSelectRow(Building b)`, `BuildMenu.cs:640-649`)
   print `t?.CurrentLevel` (1..`Tower.MaxLevel`) — e.g.
   `"... (Lvl " + (t != null ? t.CurrentLevel : 1) + "/" + Tower.MaxLevel + ")"`.
   Use the null-conditional (`?.`) on the cross-reference per CLAUDE.md §10.

3. **Display the live upgrade result + gate at max.**
   In `BuildUpgradeInfoBlock` (`BuildMenu.cs:652-678`) read `t.CurrentLevel` for the
   "Result: Lvl N" line, and disable/hide the Upgrade button when
   `t.CurrentLevel >= Tower.MaxLevel`.

4. **Make the Upgrade button real (no longer a stub).**
   Replace the stub body at `BuildMenu.cs:668-673` with a guarded
   `_selectedTowerForUpgrade?.Upgrade();` followed by `Render();` so the screen re-reads the
   live level immediately after upgrading (mirrors `TowerManagerPanel.cs:171`).
   *(If economy gating is desired, deduct crystals/stone before `Upgrade()` — but the minimum
   viable fix is correct display + a working upgrade call. Keep the cost-deduct optional/secondary
   so the desync fix is not blocked on the economy stub.)*

> If `BuildingType.ArcaneTower` references become orphaned after retargeting, drop them — the
> Upgrade screen no longer keys off building type. Do not delete `Building.Level`; other building
> systems may still use it. We are only changing what the *tower* upgrade screen reads.

---

## 4. Acceptance criteria

- [ ] Build a tower → open the Upgrade/Manage tower screen → it lists the placed tower at **Lvl 1**.
- [ ] Upgrade the tower (via the in-world upgrade UI, the fallback "Upgrade Last Tower" button,
      or the manage screen's own Upgrade button) → reopen / re-render the manage screen →
      it shows the **correct level** (Lvl 2, Lvl 3), matching the in-world tower's actual
      `Tower.CurrentLevel`.
- [ ] The manage screen's level **always equals** the value shown by `TowerManagerPanel`
      (`t.CurrentLevel`) for the same tower — no two panels disagree.
- [ ] The manage screen's Upgrade button performs a real upgrade (calls `Tower.Upgrade()`),
      and is disabled / hidden once the tower is at `Tower.MaxLevel` (3).
- [ ] Upgrading a tower to max then reopening shows "Lvl 3/3" and no further upgrade is offered.

---

## 5. Respect CLAUDE.md

- **Lane:** code-only, no `.unity` scene files hand-edited, no bakes/batchmode fired by UI.
- **Assembly rule:** `BuildMenu` and `Tower` both live in **`DeNelle.Village`** — this is an
  in-assembly change (Village → Village), no new cross-assembly dependency, no Village ↔ HUD link.
- **Null-conditional:** use `?.` on the selected-tower reference and any cross-object call
  (`_selectedTowerForUpgrade?.Upgrade()`, `t?.CurrentLevel`).
- **Brace gate:** run the §1 brace-balance check on `BuildMenu.cs` before reporting done.
- **No `System.Reflection`** introduced (the existing `InvokeRepairNearestWall` reflection is
  unrelated and untouched).

---

## 6. Do NOT touch

- **`Tower.cs`** — the model is correct; `Upgrade()` and `CurrentLevel` already work. No edits.
- **`TowerManagerPanel.cs`** — already reads `CurrentLevel` live and refreshes; correct as-is.
- **`Building.cs` / `Building.Level`** — leave the serialized field; other building systems may
  read it. The bug is that the *tower* screen read it by mistake, not that the field is wrong.
- **`TowerPersistenceService.cs`** — its "build at level 1 then replay upgrades"
  (`TowerPersistenceService.cs:104-105`) correctly restores `CurrentLevel`; not the cause.
- No scene files, no `VillageSceneBuilder`, no PanelSettings rewiring.
