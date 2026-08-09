# WORK ORDER 938 — `RULES.md`, the one page the owner can point at

**Status:** DONE (2026-08-09 — `RULES.md` authored at repo root, 102 rules; 5 source conflicts surfaced for owner ruling. NOT felt-verified: no seat has yet booted a session from it.)
**Minted:** 2026-08-09 (CLI seat) — number from the `CLI_LANES_WO_NUMBERS.md` banner (bumped 938 → 939 in the same edit)
**Lane:** Docs / process. **No game code.**
**Provenance:** Owner, 2026-08-09 — *"can we create a list of rules, somewhere i can point and say read the rules"*.

---

## 1. Why

The binding rules were spread across `CLAUDE.md`, `PREFLIGHT_GATE.md`, `SESSION_CANON_LOADER.md`,
`docs/HANDOVER.md`, `docs/TICKET_PIPELINE.md`, `docs/ARCHITECTURE_PRINCIPLES.md`,
`docs/INSTRUMENTATION_STANDARD.md` and `docs/BOARD.md`. "Read the rules" had no single target, so
rules were missed — not through disagreement, but because nobody could name the one place.

## 2. The design constraint that keeps it from rotting

**POINT, DO NOT DUPLICATE.** Each rule is one line + a pointer to its authoritative doc. A copied
rule is a future contradiction: the copy drifts, and then nobody knows which is binding. `RULES.md`
is an **INDEX of binding rules, not their source**, and says so in its own preamble — it loses to
its sources by construction.

## 3. Delivered

- `RULES.md` (repo root) — **102 numbered rules**. Rules 1–5 *are* the "five most violated" block
  (continuously numbered, so the top block is not a duplicate set), each carrying the source doc's
  own evidence of repeat breakage.
- Sections: Preamble · **Precedence when sources disagree** · ★ The five most violated ·
  A. Before you touch anything · B. Architecture law · C. Writing code · D. Debugging — the hard gate ·
  E. Scenes, assets + naming canon · F. Work orders + the board · G. Committing · H. Gates · I. Roles ·
  J. Canon maintenance · ⚠ Conflicts needing an owner ruling.
- Two additions beyond the brief, both justified: a **Precedence** block (owner ruling > newest
  ground-truth anchor > code > marker file > numbering banner > WO files > source doc), because
  several conflicts below are only navigable if a seat knows which source wins; and a separate
  **Architecture law** group, because those rules are shape-of-the-code rather than
  moment-of-writing.

## 4. ⚠ FIVE CONFLICTS FOUND — each needs an OWNER RULING (agent correctly picked no winner)

| # | Conflict |
|---|---|
| **C-1** | **WO numbering blocks.** `CLAUDE.md` §2 says the UI seat holds **860–899**; the banner carries an owner ruling (2026-08-07) that 860–899 is CLOSED and the UI seat moved to **1000–1099**. The banner is named sole authority so the live number self-resolves — but §2's restated range is itself the copied-number pattern that caused the 08-02 collisions. |
| **C-2** | **Three different "shared boards" in binding text.** §2 still says *"UI marks the matching Linear issue as Done"*; §2 + `docs/BOARD.md` say `BOARD.html` derived from `WorkOrders/*.md`; §13 + `docs/TICKET_PIPELINE.md` say the Task list. Whether the ticket board and the WO board are one or two is stated nowhere. |
| **C-3** | **Dead paths.** §0 says the project home is `C:\EoA\`; `PREFLIGHT_GATE.md` B11 points logs at `C:\eoa\logs\debug\`. **Verified: `C:\EoA` does not exist** — the repo is `D:\eoa`. §0's mount-vs-Windows rule currently guards a path that isn't there. |
| **C-4** | **Village.unity.** §3 mandates rebuilding it via `Defenders > Week 3 > Build Village Scene`; §7 says `Village.unity` is **deleted from the tree**. The never-hand-edit rule survives; the named scene + menu path is dead with no replacement builder named. |
| **C-5** | **Strip semantics — the dangerous one.** §12 says set `FlowTrace.Enabled=false` *or strip calls* once stable; `INSTRUMENTATION_STANDARD.md` §1.4 says strip only `Step` and **keep every `Warn`/`Fail`/`Guard`**. A seat reading §12 alone can legitimately delete the permanent no-silent-failure net. |

## 5. Follow-up

- The five conflicts want owner rulings; each is a one-line fix to the losing doc once ruled.
- C-3 and C-4 are stale-canon defects fixable without a ruling (a path that does not exist, a menu
  item that does not exist) — but §15 says frozen dated docs get banners, not rewrites, so route them.
- Nobody has yet booted a session using `RULES.md` as the entry point; that is the real acceptance test.
