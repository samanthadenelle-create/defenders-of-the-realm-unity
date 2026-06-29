# Ticket / Work-Order Status Reconciliation — 2026-06-28

READ-ONLY audit. Cross-checks `WorkOrders/WORK_ORDER_*.md` (+ `.RESULT.md`) against
git reality (commits referencing each WO) and recorded results. No files were edited
other than this audit.

- HEAD at audit time: `ace37cc4` (2026-06-28 20:34 -0500), branch `wip/village2-and-f8-tickets`
- Scope: full WO corpus scanned (584 files); detailed reconciliation focused on the
  active in-flight band **540–584** where drift lives. The 5xx-and-below historical
  band is dormant and its statuses are not load-bearing for current state.

## Headline findings

1. **Systemic stale status (category a/b):** essentially the **entire 550–581 band**
   carries a status line like *"IMPLEMENTED (edit-only; NOT gated/committed —
   orchestrator reconciles)"*. Git log proves every one of them **IS committed on HEAD**.
   The WO files were written by the edit-only agents and never updated post-commit, so
   they all read as in-flight when they are DONE. The reconcile happened; the docs didn't
   catch up.
2. **Four shipped WOs have NO work-order file (category c):** **582, 583, 546, 547** are
   referenced by landed commits but have no `WORK_ORDER_5xx_*.md` in `WorkOrders/`.
   582 and 583 are large (6 and 3 commits) — significant UI work with zero spec/result on disk.
3. **READY-marked but already shipped (category a):** **541, 542, 543, 545** all read
   "READY TO IMPLEMENT" but are committed.
4. **Genuinely open (status accurate):** **544** (no commit), **584** (spec-only docs commit),
   and **560** is *partially* shipped (P0 slice landed, rest still spec).
5. Only **553** and **557** in the active band have `.RESULT.md` files — and both are also
   committed, so those two are the only properly-closed-and-recorded WOs in the band.

## Reconciliation table

| WO# | Claimed status (in WO file) | Actual status | Evidence |
|---|---|---|---|
| 541 | READY TO IMPLEMENT | **DONE** | commits 2bb6a65b, c2cd4325, 19eea196, f0dd0ccc (HUD one-model stages 1/2/3a) |
| 542 | READY TO IMPLEMENT | **DONE** | commit 5fe8ba4d (accessory icons + ItemIconImporter, "fixes WO-542") |
| 543 | READY TO IMPLEMENT | **DONE** | commit 40923673 (rings/amulets + rim-light VFX) |
| 544 | READY TO IMPLEMENT | **OPEN (accurate)** | no commit references WO-544; class-armor catalog wire-in not landed |
| 545 | READY TO IMPLEMENT | **DONE** | commit d018c6c0 (EditMode test triage/fix) |
| 546 | *(no WO file)* | **SHIPPED, unrecorded** | commit 712edec7 (ICatalogSource seam) — no `WORK_ORDER_546*` on disk |
| 547 | *(no WO file)* | **SHIPPED, unrecorded** | commit 16f10308 (ISaveProvider seam) — no `WORK_ORDER_547*` on disk |
| 548–549 | *(no WO file)* | gap (never minted) | no files, no commits |
| 550 | READY (impl in worktree; awaiting gate) | **DONE** | commits ebeccdaf, e4165b34, b6de5c04 (village2 raid polish + re-bake) |
| 551 | READY TO IMPLEMENT | **DONE** | commit 64a9ca74 (geometry-first weapon seating) |
| 552 | READY TO IMPLEMENT (audit) | **DONE** | commit c76861f2 (crafting cards obsidian plate) |
| 553 | READY TO IMPLEMENT | **DONE + recorded** | RESULT.md present + commits fc1543af, 4f5abe40 (jeweler) |
| 554 | READY (impl in worktree) | **DONE** | commit 2a6bac4e (shared obsidian chrome) |
| 555 | IMPLEMENTED (edit-only) | **DONE** | commit 1404098d (offline harvest relocation) |
| 556 | IMPLEMENTED | **DONE** | commit fe4aad5b (victory summary + boss drops) |
| 557 | Phase 1 DONE / 2 PARTIAL / 3 DEFERRED | **DONE (phased) + recorded** | RESULT.md + commits 4a91395a, be39c4db (full Yarn removal) |
| 558 | IMPLEMENTED (pending gate) | **DONE** | commit d8299b6c (quest pack, 41 templates) |
| 559 | IMPLEMENTED (agent worktree) | **DONE** | commit 70ae1f3f (hero-select carousel) |
| 560 | READY TO IMPLEMENT | **PARTIAL** | P0 slice committed 0925fbc6 (telegraph/victory burst); rest still spec |
| 561 | ✅ DONE | **DONE** | commits 4a91395a, be39c4db (intro + lore, with WO-557) |
| 562 | IMPLEMENTED (worktree) | **DONE** | commit 94b74d73 (theme consistency reskin ~25 panels) |
| 563 | IMPLEMENTED (not gated/committed) | **DONE** | commit da0cbb4a (9-zone HUD all battle modes) |
| 564 | IMPLEMENTED (CLI reconciles) | **DONE** | commit d66d58ec (building income + daily payouts) |
| 565 | IMPLEMENTED (not gated/committed) | **DONE** | commit 9797ff3b (hide dead Sort/Filter) |
| 566 | IMPLEMENTED (verify pending) | **DONE** | commit 9db82d68 (Knight talent interpreter) |
| 567 | IMPLEMENTED (edit-only worktree) | **DONE** | commit ea087782 (equip→visual MPB tint) |
| 568 | IMPLEMENTED (edit-only worktree) | **DONE** | commit d6ca3a0d (shared-material cache) |
| 569 | IMPLEMENTED (pending gate) | **DONE** | commit 3aec8f27 (Defenders.mp4 intro) |
| 570 | IMPLEMENTED (worktree) | **DONE** | commit fb5d4e28 (title/tagline canon) |
| 571 | IMPLEMENTED (not gated/committed) | **DONE** | commit 4f51e085 (audio controller wiring) |
| 572 | IMPLEMENTED (not gated/committed) | **DONE** | commit d89d8907 (resource flash throttle) |
| 573 | IMPLEMENTED (awaiting gate) | **DONE** | commit 454d9665 (inventory portrait + slot trace) |
| 574 | IMPLEMENTED (edit-only) | **DONE** | commit d455bd42 (talent quick-swap cast) |
| 575 | IMPLEMENTED (not gated/committed) | **DONE** | commit 13d38a45 (maxed button inert) |
| 576 | IMPLEMENTED (not gated/committed) | **DONE** | commit 661f7940 (building Talk flavor convo) |
| 577 | IMPLEMENTED (pending gate) | **DONE** | commit a0987724 (in-game seating/offset editor) |
| 578 | IMPLEMENTED (worktree, not committed) | **DONE** | commit c5bb1a26 (owned gear ledger ∪ equipped) |
| 579 | IMPLEMENTED (not gated/committed) | **DONE** | commit fbc8fe3a (wave loop auto-run + HUD flip) |
| 580 | IMPLEMENTED (CLI to gate/commit) | **DONE** | commit ea313c29 (white bar + hub floor) |
| 581 | IMPLEMENTED (not gated/committed) | **DONE** | commit a48bb0b5 (hero animator re-cache) |
| 582 | *(no WO file)* | **SHIPPED, unrecorded** | 6 commits a6b56d3c, 6d8cfa1a, 6928221c, 58909f10, e40e3630 (Blink master-frame UI) — no spec/result on disk |
| 583 | *(no WO file)* | **SHIPPED, unrecorded** | commits bcf0955b, 5918eaf3, 5aa44964 (frame-fit screens, victory rating, dressed-hero preview) — no spec/result on disk |
| 584 | READY TO IMPLEMENT (ratified) | **OPEN (accurate, spec-only)** | only docs commit f3dde1a1 (dungeon/outpost/arena consolidation spec); not implemented |

## Recommended canon fixes (not performed — read-only audit)

- Stamp `.RESULT.md` (or flip Status → DONE with commit hash) for **541–543, 545, 550–552,
  554–556, 558, 559, 562–581** — the whole "not gated/committed" band is committed.
- **Back-fill WO files for 582, 583, 546, 547** (shipped with no spec/result). 582/583 are
  substantial UI work and currently invisible to the WO ledger.
- Mark **560** as PARTIAL (P0 landed; remainder open).
- **544** and **584** statuses are accurate (genuinely open).
- Per CLAUDE.md §15, status should be updated in the same breath as the commit; the
  edit-only-agent → orchestrator-commit flow is dropping that step at the WO-file level.
