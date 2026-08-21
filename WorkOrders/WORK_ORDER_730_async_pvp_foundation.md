<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-16
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-16) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 730 — Async PvP Foundation (Snapshot I/O, No Live Netcode)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Priority:** P2 (vision path; client-ready, not ship-ranked)  
**Silo:** Backend / State  
**Depends on:** WO-727 (snapshot format locked via AI recipes)  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** M  
**Parallel-safe with:** WO-729  

---

## Goal

Prove player bases can be **exported/imported** as the same recipe AI camps use. **No live PvP netcode.** Enables future async “raid a snapshot of another player.”

---

## Deliverables

1. **Snapshot DTO:** `BaseLayout` + `ArenaDefense` + schema version + threat/metadata.
2. **Export:** from local `GameState` → JSON (file, persistentDataPath, or debug dump).
3. **Import/Realize:** load snapshot as raid target (same path as AI camp, WO-726/727).
4. **Stub matchmaking:** “Next opponent” = seeded AI **or** last imported snapshot.
5. **Server re-verify note** in RESULT: async server can re-run Realize later (per ARENA_SOLUTION).
6. **Policy note:** open-market builds may disable crypto wagers; snapshots are gameplay-only.

---

## Tasks

1. Define versioned DTO; additive-friendly fields.
2. Export/import APIs + DevPanel or hidden debug entry for smoke.
3. Wire import → raid target on Path A attack loop.
4. Soft-fail on schema mismatch (warn + reject; no crash).
5. Optional: Worker endpoint stub behind env flag (not required for acceptance).

---

## Acceptance

- [ ] Export base → alter session → import as enemy → raid with army (726 path).
- [ ] Schema mismatch fails soft.
- [ ] No dependency on live multiplayer session.
- [ ] CompileGate green.

---

## Not in scope

- Ranked ladder, ELO, anti-cheat.
- On-chain escrow / SKR stakes as requirement.
- Live synchronized PvP.
- Push notifications for “you were raided.”

---

## Key files

- GameState `BaseLayout` / `ArenaDefense`
- `ArenaMode` + Realize pipeline
- New: snapshot DTO + import/export helper (Core or Village)
- Optional: API worker stub

---

## RESULT

`WorkOrders/WORK_ORDER_730_async_pvp_foundation.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no BaseSnapshot/ExportBase` — async PvP snapshot unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
