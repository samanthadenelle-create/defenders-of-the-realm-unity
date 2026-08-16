# WORK ORDER 937 — Board status-line hygiene + parser scope

**Status:** DONE — A+B were already in-tree (Doc bucket + Unlabeled 0); C (gate wiring) + duplicate-number reporting landed 2026-08-16; see RESULT
**Minted:** 2026-08-09 (CLI seat) — number from the `CLI_LANES_WO_NUMBERS.md` banner (bumped 937 → 938 in the same edit)
**Lane:** Board / docs hygiene. **No game code.**
**Parent:** WO-1011 (board workflow). This is the Unlabeled half; WO-1011 Phase 2 owns the ~516 stale *Ready* claims.

---

## 1. The finding

`python tools/board_build.py --check` reports **91 Unlabeled**. That single number hides **two
different problems**, and fixing them the same way would be wrong:

> ### ⚠ CORRECTED 2026-08-09 — THE ORIGINAL FRAMING BELOW WAS WRONG FOR HALF THE FILES
> This WO first said all 36 non-`WORK_ORDER_<n>` rows were "not work orders at all." **They are not.**
> The 36 split evenly:
> - **18 companion docs** (`README.md`, `AUDIT_*`, `BRIEF_*`, `HANDOFF_*`, `NOTES_*`, `QA_CHECKLIST_*`,
>   `DESIGN_*`, `RESEARCH_BRIEF_*`, `REVIEW_*`, `WO541_MODEL_API.md`, …) → genuinely not work orders.
> - **18 LEGACY UNNUMBERED WORK ORDERS** (`WORK_ORDER_ad_generator.md`,
>   `WORK_ORDER_second_grom_companion.md`, `WORK_ORDER_outpost_base_footprint.md`, …) → **these ARE
>   work orders and DO owe a status.**
>
> **Scope on the document KIND (filename prefix `WORK_ORDER_`), never on "has a number."** Taking the
> obvious reading of the original spec would have laundered **5 genuine defects** into the non-defect
> bucket: `WORK_ORDER_COMBAT_VFX_BATCH_2026-07-10`, `PROGRAM_732_736_barracks_troop_roster`,
> `outpost_base_footprint`, `second_grom_companion`, `store_packs_content`. A scope fix that hides
> defects is worse than the miscount it replaces.

| Count | What it is | Right fix |
|---|---|---|
| **18** | **Companion docs** — not work orders; filename has no `WORK_ORDER_` prefix | **Parser SCOPE.** Do NOT add `**Status:**` lines to a README or an audit brief |
| **73** | **Real work orders** (numbered *and* legacy-unnumbered) with a missing or empty `**Status:**` line | **Status hygiene.** These are the genuine defects |

Until the scope is fixed, the Unlabeled count is not a defect count and cannot be gated on honestly.

---

## 2. Deliverables

**A. Parser scope (do FIRST — it makes the number mean something).**
Decide and implement ONE, in `tools/board_build.py`:
- exclude non-`WORK_ORDER_<n>` files from the board entirely, **or**
- keep them but bucket them as a distinct non-defect category (e.g. `Doc`) that `--check` ignores.

Prefer whichever keeps the *documents discoverable* — several are live references (`WO541_MODEL_API.md`,
`DESIGN_CONNECTOR_IS_THE_ONLY_CONTRACT.md`). Silently dropping them from the only index anyone reads
would trade a cosmetic problem for a discoverability one. Update `docs/BOARD.md` §3 in the SAME commit.

**B. The 71 status lines.** Give each real WO a `**Status:**` line carrying one canonical keyword
(§3 of `docs/BOARD.md`). Evidence-based per §12 — never guess a status:
- a matching `.RESULT.md`, HEAD commits referencing the number, or the feature verifiably in the tree
  → `DONE (reconciled 2026-08-09 from the tree, NOT felt-verified)`
- superseded / system removed → `CLOSED — SUPERSEDED by WO-<n>`
- genuinely pending → `READY TO IMPLEMENT`
- frozen/dated WOs: **status line only, body frozen** (§15)

Known ids (first 40 of 71): 21, 22, 23, 24, 25, 97, 136 (×2), 138, 139, 159, 190, 199, 200, 307, 333,
385, 430, 444, 480, 482 (×2), 483, 494, 496, 497, 503, 516, 573, 574, 591, 684, 690, 691, 692, 708,
710, 740, 758, 760 — regenerate for the full list.

⚠ **Duplicate numbers exist** (136 and 482 each appear twice, different files). Do not silently
renumber — flag them; a collision is its own finding.

**C. Gate it.** Once Unlabeled is 0, wire `python tools/board_build.py --check` into the check-in gate
so the vocabulary is enforced and cannot regress.

---

## 3. Acceptance criteria

- [ ] Non-WO files no longer counted as Unlabeled, and still discoverable; `docs/BOARD.md` §3 updated in the same commit.
- [ ] All 71 real WOs carry a canonical keyword, each flip evidence-cited in the status line.
- [ ] Duplicate WO numbers (136, 482, and any others found) are reported, not silently renumbered.
- [ ] `python tools/board_build.py --check` exits 0 (`BOARD_CHECK_OK 0 unlabeled`).
- [ ] Board regenerated; no game files touched (prove with `git status`).

## 4. What NOT to touch

- `BOARD.html` by hand. Game code, scenes, catalogs.
- WO **bodies** of frozen/dated ledgers — status line + banner only (§15).
- The ~516 stale *Ready* claims — that is WO-1011 Phase 2, deliberately a separate wave.
