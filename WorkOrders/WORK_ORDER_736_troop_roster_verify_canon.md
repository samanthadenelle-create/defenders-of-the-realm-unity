# WORK ORDER 736 — Troop Roster Verify, Regression, Canon Close

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at TroopDef.cs:124 + TroopUnlock.cs:34-80 + TroopTrainingPanel.cs:103-445 + TroopRosterRegression wired at DataRegression.cs:313.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Priority:** P0 (program close for 732–735)  
**Silo:** QA / Regression / Canon  
**Depends on:** WO-732, WO-733; **734 + 735 recommended**  
**Program:** `WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`  
**Feeds:** WO-724 felt-pass, WO-726 deploy with multi-type army  
**Effort:** M  
**Audience:** Claude + CLI  

---

## Goal

Prove the roster unlock ladder is **data-correct, dual-copied, train-gated, and documented** so CoC barracks work does not ship as “two troops forever.”

---

## Deliverables

### 1. DataRegression oracle

Add/extend regression (pattern: `Assets/Editor/Regression/*`) that asserts:

| Check | Rule |
|-------|------|
| Count | `TroopCatalog.All.Count == 7` (or ≥7 with exactly the 7 required ids) |
| Ids | All seven stable ids present |
| Defaults | footman + archer `UnlockBarracksTier == 1` |
| Ladder | spearman 2, shieldguard 3, outrider 4, battlemage 5, legionnaire 6 |
| Costs | food ≥ 0; wood/iron ≥ 0; slots ≥ 1 |
| Dual-copy | StreamingAssets vs Resources troops.json same content **or** Resources load returns same 7 ids |

Emit clear `TROOP_ROSTER_OK` / fail reason in log (match project regression style).

### 2. EditMode or headless train gate smoke (preferred)

If fleet/EditMode harness exists:

1. Mock/set barracks tier = 1 → `IsTrainable` true only for unlock≤1.  
2. Tier = 3 → shieldguard true, outrider false.  
3. `TroopDialogueCommands.Train` locked id returns 0 and does not reduce resources (if economy mockable).

If headless is hard: document **manual PO script** in RESULT and still ship DataRegression.

### 3. Manual PO script (copy into RESULT)

1. Enable barracks (`ff.barracks=1` if needed).  
2. Open Train UI — see 7 rows; only Footman/Archer trainable.  
3. Upgrade Barracks to T2 — Spearman unlocks; effect text mentions Spearman.  
4. Train 1 Spearman — army has it; save/reload preserves.  
5. Attempt train Legionnaire at low tier — fail, no spend.  
6. (Optional) Deploy mixed army in raid if WO-726 available.

### 4. Canon updates (same change-set as green verify)

Short updates only (STALE banners if full rewrite not needed):

- Program index status → **VERIFIED** date when closed.  
- `PIPELINE_STATE.md` or ground-truth: one bullet — *Barracks roster = 7 types; unlock by Barracks tier 1–6.*  
- Do **not** claim CoC raid live unless 726 is done.

### 5. RESULT package

- List of commits/files.  
- Regression marker lines.  
- PO felt sign-off checkbox for owner.  
- Open art TODOs from WO-735.

---

## Tasks

1. Implement regression.  
2. Run CompileGate + DataRegression.  
3. Run train gate smoke (auto or manual).  
4. Canon one-liners.  
5. Close program RESULT; mark 732–735 RESULT cross-links.

---

## Acceptance

- [ ] DataRegression green with troop roster assertions.  
- [ ] Dual-copy verified.  
- [ ] Unlock gate proven at least at tier 1 and one higher tier.  
- [ ] Canon mentions 7-type ladder.  
- [ ] All of 732–736 have RESULT files or explicit defer notes.  
- [ ] No production flag default flips beyond what owner already approved.

---

## Not in scope

- New troop types beyond the 7.  
- Full CoC raid soft-lock proof (WO-726).  
- Final production art.

---

## Key files

| Action | Path |
|--------|------|
| ADD/EDIT | `Assets/Editor/Regression/*Troop*` or catalog regression |
| READ | All 732–735 outputs |
| MAY EDIT | `PIPELINE_STATE.md`, `CANON_GROUND_TRUTH_*.md`, program index |
| READ | `TroopCatalog`, unlock helper, both JSON paths |

---

## Claude implementation notes

- Prefer **headless** proof (owner directive: instrument/regression over manual-only).  
- If dual-copy check is flaky on line endings, compare parsed troop id sets + unlock tiers, not raw bytes.  
- Sole committer rules still apply for git; this WO can be verified without push.

---

## RESULT

`WorkOrders/WORK_ORDER_736_troop_roster_verify_canon.RESULT.md`
