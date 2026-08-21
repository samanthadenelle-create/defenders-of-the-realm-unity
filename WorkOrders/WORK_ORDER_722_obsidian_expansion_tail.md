<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-14
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-14) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 722 — Obsidian expansion tail (non-critical + WO-714 remainder)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Priority:** P2 (breadth)  
**Phase:** 4 (Expand)  
**Effort:** M  
**Depends on:** 716, 720 critical PASSes; can absorb 714 W6/W10/PetSelect  
**Program:** Grok-03 · **Guidance:** Grok-02  

---

## Goal

Finish **remaining** Obsidian conformance after the demo path is green — without blocking G1–G3.

### Candidate backlog (merge with 716 non-critical FIX + WO-714 open)

| Item | Notes |
|---|---|
| WO-714 W6 verify | Per 714 program |
| WO-714 W10 skin | Per 714 program |
| PetSelect UITK → code-built kit | Explicit conversion; no UIDocument |
| Inventory depth (WO-713 if still open) | After founding path stable |
| Raid screens polish | Already partially W4 |
| Talent tree beauty | FrameTalent only if scheduled |
| Any 716 FIX outside critical table | From pair-walk |

---

## Tasks

1. Prioritize by **owner pair-walk FAIL remaining**.  
2. Same formula as 720 (kit only, zones, no UXML).  
3. Re-capture pairs; mark PASS.  
4. PetSelect: if still UITK, rebuild on `BuildObsidianPanel` + existing onboarding VM.  
5. Update WO-714 RESULT / board when program fully closed.

---

## Acceptance

- [ ] No open P0 UI FIX on owner board for expanded set (or explicit defer).  
- [ ] PetSelect (if in scope) ships code-built.  
- [ ] COMPILE_GATE_OK · pair PASSes attached.  

---

## Not in scope

- New features · economy rebalance · wall builder · crypto wallet skins.

---

## RESULT

`WorkOrders/WORK_ORDER_722_obsidian_expansion_tail.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `PetSelectScreen.uxml + RequireComponent(UIDocument)` — still UXML; conversion remains. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
