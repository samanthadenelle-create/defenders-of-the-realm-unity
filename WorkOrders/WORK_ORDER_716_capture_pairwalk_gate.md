<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-14
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-14) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 716 — Capture + image-pair sign-off gate

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Priority:** P0 — unlocks the rest of Grok-03 program  
**Phase:** 0 (Gate)  
**Effort:** S  
**Depends on:** none (unblocks 717/720; pairs with open WO-714)  
**Program:** `docs/UI/Grok-03-here-to-there-WO-program.md`  
**Guidance:** `docs/UI/Grok-02-Obsidian-UI-guidance.md` §7  

---

## Goal

Stop arguing from memory. Produce a **graphics-enabled** Windows build, run the **panel capture** path, assemble **`UI_REVIEW/INDEX.html`**, and hand the owner a PASS/FIX sheet for every critical surface.

**Without this WO, WO-714 “conformance” cannot honestly close.**

---

## Tasks

1. **Rebuild Windows player** (folder may be empty — night report):  
   `Remove-Item -Recurse -Force Builds\Windows -ErrorAction SilentlyContinue; .\build-windows.ps1`  
   Unity editor **closed**.
2. **Graphics fleet / capture** (windowed, real rendering):  
   `.\run-autopilot-fleet.ps1 -Count 1 -SeedStart 9500 -TimeoutMin 12 -Graphics`  
   Ensure `CaptureExtraPanels` (or equivalent) writes `panel_<Screen>.png`.
3. **Assemble contact sheet:**  
   `.\build-ui-review.ps1` → `UI_REVIEW/INDEX.html`.
4. **Seed the review list** (minimum set for demo path — owner can add):  
   Title · HeroSelect · Founding/Steward dialogue · Build palette (Town + Defenses) · Upgrade panel · Shop/PartyShop · Inventory (if reachable) · Settings/Pause · End-state / wave report · Combat HUD snapshot (vitals).
5. **Doc handoff:** write `UI_REVIEW/PAIRWALK_716.md` with table: Screen | Shot | PASS/FIX | Notes. Owner fills PASS/FIX; CLI does not invent PASS.
6. **FlowTrace / gate:** build success + at least one non-blank panel PNG (file size / non-zero dimensions check in RESULT).

---

## Acceptance

- [ ] `Builds/Windows/DefendersOfTheRealm.exe` exists and boots.  
- [ ] `UI_REVIEW/INDEX.html` opens and shows side-by-side (or listed) captures.  
- [ ] `PAIRWALK_716.md` lists critical screens with empty PASS/FIX for owner.  
- [ ] RESULT cites paths + timestamps.  
- [ ] No claim that screens “look fine” without owner marks.

---

## Not in scope

- Fixing FIX screens (→ **WO-720** + kit fixes).  
- WebGL deploy.  
- Full fleet green unrelated tickets.

---

## RESULT

`WorkOrders/WORK_ORDER_716_capture_pairwalk_gate.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `UI_REVIEW/PAIRWALK_716.md, INDEX.html, build-ui-review.ps1` — pair-walk pipeline shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
