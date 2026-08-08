> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: its required outputs are absent - ZERO RESULT files existed anywhere in the 900-926 range before the 2026-08-08 reconcile. Note the irony worth recording: this is the exact WO that would have PREVENTED the 2026-08-08 audit, and its own sec.0 predicted precisely this outcome.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 918 — Board hygiene: close shipped work orders + RESULT files (audit wave)

**Status: NOT STARTED** (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-07 (CLI / Grok — process residual after the five-findings audit + WO-899/1001 wave)  
**Silo:** Process / Notion board / docs (no gameplay code required)  
**Roles:** CLI (sole git committer for RESULT files) + whoever has Notion access for board rows  
**Live board:** Notion “Work Orders” DB — https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f  
**Related:** CLAUDE.md §2 completing WOs; `NOTION_SOURCE_OF_TRUTH.md`

---

## 0. One-line truth

Code and commits are ahead of the **board and RESULT paper trail**. Shipped work still reads READY on disk or open on Notion, so the next session re-implements or re-audits finished work. Close the loop for the 2026-08-07 wave and the known landed clusters — without inventing fake DONE for unfelt items.

---

## 1. What “closed” means here

For each WO in the lists below:

| Layer | Done means |
|-------|------------|
| **Repo RESULT** | `WorkOrders/WORK_ORDER_NNN_….RESULT.md` exists with: status DONE or PARTIAL, commit SHA(s), gates, what was **not** done, PO felt still open Y/N |
| **Spec file status line** | Top of the WO markdown: Status updated (DONE / PARTIAL / SUPERSEDED) — do not rewrite the body history |
| **Notion row** | Stage/status → Done (or equivalent) **only when** RESULT exists; link commit or RESULT path in the row if the DB has a field |
| **Never** | Mark Done on faith; close PO felt-only items without owner |

---

## 2. Priority close list (2026-08-07 audit + dungeon/HUD wave)

### A. Audit five-findings (single commit — one RESULT is enough)

Commit: **`f329c8d5`** — *fix: the five highest-value findings from the work-order audit*

| # | Finding | Code truth | RESULT note |
|---|---------|------------|-------------|
| 1 | RealmStorePurchase ON (Q9) | `FeatureFlags.RealmStorePurchase` default true | Ship residual = **WO-915** (do not claim store “finished”) |
| 2 | Arcane Aether visuals | `BoltVisualElement = Aether`; cast/extra empty | Regression residual = **WO-913** |
| 3 | Quest oracle registered | `QuestCompletabilityRegression` in DataRegression; QUEST_REACH 63/63 | Cite first green log |
| 4 | Lumberyard out of FoundingKit | `BuildModeController.FoundingKit` | collector_lumbermill stays |
| 5 | Site tagline restored | `site/index.html` | Prod residual = **WO-916** |

Write: `WorkOrders/WORK_ORDER_AUDIT_FIVE_FINDINGS_2026-08-07.RESULT.md` (or fold into a short RESULT under a tracking id — do **not** mint a new WO number for the RESULT alone).

### B. WO-899 (partial)

| Item | State | Close as |
|------|--------|----------|
| §1 Analog stick | Landed `a35163e1` | DONE in RESULT |
| §2 Compass strip | Landed; layout proof → **WO-914** | PARTIAL |
| §3 Attack pill | Landed | DONE in RESULT |
| §4 Dodge + empty slot | Not done → **WO-917** | OPEN child |

Write: `WorkOrders/WORK_ORDER_899_hud_polish_joystick_compass_attack.RESULT.md` with PARTIAL + pointers to 914/917.

### C. WO-1001 / Phase 2 dungeons (if RESULT incomplete)

- Ensure `WORK_ORDER_1001_deep_dungeon_program.RESULT.md` lists slices 1b–8 + Phase 2A/B portals/bakes with SHAs (`33354ea9` … `335f6b81` / `195ae8c8` family).  
- Do not close felt dungeon difficulty (owner catalog tuning still open).

### D. World exterior (if no RESULT yet)

| Commit theme | SHA (verify with `git log`) |
|--------------|------------------------------|
| Richer exterior terrain | `bfacf0b3` |
| Grass + roads | `cc24da5a` |

Optional short RESULT or section under a world-terrain WO if one exists; otherwise a dated note in HANDOVER/canon is enough — **do not mint duplicate WOs**.

### E. Already-READY VFX (do **not** close as Done)

| WO | Status |
|----|--------|
| **890** harvest auras + subtlety ruling | Still implement — leave READY |
| **892** building damage state | Still implement — leave READY |
| **1002** hub heart yellow aura | READY — leave open until implemented |

---

## 3. Broader “~19” sweep (optional same session)

If Notion shows a large Open pile after §2:

1. Export or filter Work Orders = Done-in-git but Open-on-board (use RESULT files + recent commits as source of truth).  
2. Close **only** rows that have RESULT + SHA.  
3. Cap this pass at a documented list (aim ~15–25 rows) so it does not become a full historical rewrite.  
4. Anything ambiguous stays Open with a one-line “needs PO” comment.

---

## 4. Acceptance

- [ ] RESULT for audit five-findings with SHA `f329c8d5` and residual pointers (913/915/916).  
- [ ] RESULT for WO-899 PARTIAL with 914/917 children.  
- [ ] WO-1001 RESULT complete or explicitly amended.  
- [ ] Spec status lines updated for anything marked Done/Partial.  
- [ ] Notion board updated for those rows (or RESULT notes “Notion: owner/CLI blocked on auth”).  
- [ ] 890 / 892 / 1002 still open.  
- [ ] No gameplay code required for this WO; if only docs, no compile gate required.

---

## 5. RESULT

`WorkOrders/WORK_ORDER_918_board_hygiene_close_shipped_wos.RESULT.md` — checklist of closed IDs + Notion yes/no.
