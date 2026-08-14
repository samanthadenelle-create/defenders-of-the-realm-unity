# WORK ORDER 735 — Troop Visual Placeholders (Models + Portraits)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at TroopDef.cs:124 + TroopUnlock.cs:34-80 + TroopTrainingPanel.cs:103-445 + TroopRosterRegression wired at DataRegression.cs:313.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Priority:** P1 (felt identity; can ship after 733 with capsules but should not lag)  
**Silo:** Art integration / Resources  
**Depends on:** WO-732 (model keys authored)  
**Parallel-safe with:** WO-733, WO-734  
**Program:** `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`  
**Effort:** M  
**Audience:** Claude + CLI (owner may supply final art later)  

---

## Goal

Every roster troop has a **non-capsule** body in train/deploy and a **readable identity** in the training UI (portrait or icon). Day-one may **reuse** existing Heroes/NPC assets; no requirement for bespoke Tripo meshes in this WO.

---

## Current state

- `TroopFactory` skins `Resources/Heroes/{model}` via `VisualFactory.Skin`.  
- Exists: `SC_Footman.prefab`, `SC_Archer.prefab`, Knight/Ranger/Mage/Cleric pipelines.  
- Missing models → tinted capsule + LogWarning (acceptable fallback, not the goal).

---

## Deliverables

### 1. Model key resolution (all 7)

| Troop id | Target `model` | Placeholder strategy |
|----------|----------------|----------------------|
| troop-footman | `SC_Footman` | Keep |
| troop-archer | `SC_Archer` | Keep |
| troop-spearman | `SC_Footman` | Same body OK; optional distinct material tint later |
| troop-shieldguard | `Knight` or package prefab | Tanker silhouette |
| troop-outrider | `Ranger` | Fast/light silhouette |
| troop-battlemage | `Mage` | Caster |
| troop-echo-legionnaire | `Knight` | Elite; optional scale mult if SkinOptions support |

Confirm each key **loads** in playmode (or headless spawn). Fix `modelYaw` in JSON if bodies face wrong (SC=0, Tripo often -90).

### 2. Optional distinct materials (cheap)

If `VisualFactory` / skin options allow color tint without new meshes, differentiate:

- Spearman: cooler steel  
- Shieldguard: darker armor  
- Legionnaire: slight gold/tree accent  

**Not required** if tint API is messy — reuse meshes cleanly first.

### 3. UI identity

Training panel detail (and list if easy):

- Prefer existing icon pipeline (`iconId` from WO-732 if set).  
- Fallbacks: kit medallion icons (`sword`, `bow`, etc.) mapped by role.  
- Do **not** block train on missing icon.

Deploy tray already uses troop def names — ensure `DisplayName` shows.

### 4. Documentation for real art pass

In RESULT, table:

| troop id | placeholder model | owner art TODO |
|----------|-------------------|----------------|
| … | … | “needs spear prop / unique Tripo” |

So a later art WO can replace models without code changes (JSON `model` swap only).

---

## Tasks

1. Spawn each troop via `TroopDeployer.SpawnTroop` in a test scene or DevPanel hook.  
2. Fix any missing Resources path / wrong folder.  
3. Wire panel icons if free.  
4. RESULT with pass/fail per troop visual.

---

## Acceptance

- [ ] All 7 types spawn without capsule fallback **or** RESULT explicitly lists which still capsule (max 0 preferred).  
- [ ] Footman/Archer unchanged quality vs pre-roster.  
- [ ] Facing roughly correct in combat (+Z move).  
- [ ] No scene hand-edits; no UXML.  
- [ ] CompileGate if any C# touched.

---

## Not in scope

- New Tripo generation pipeline.  
- Full animation polish per unit.  
- Archer projectile VFX.  
- Unlock logic (WO-733).

---

## Key files

| Action | Path |
|--------|------|
| READ/EDIT | `troops.json` model / modelYaw (both copies) |
| READ | `TroopFactory.cs`, `VisualFactory` |
| MAY ADD | Icons under `Resources/` if project has troop icon folder |
| MAY EDIT | `TroopTrainingPanel.cs` for icon display only |

---

## Claude implementation notes

- Prefer **JSON model key** changes over forking factory per troop id.  
- If Knight path is `Heroes/Knight` vs prefab name, match whatever `VisualFactory` already uses for hero skin.  
- LogWarning on missing mesh is OK; LogError is not for missing optional art.

---

## RESULT

`WorkOrders/WORK_ORDER_735_troop_visual_placeholders.RESULT.md`
