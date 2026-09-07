# WO-1085 — Screen is a black void with only a blue strip / red cursor

**Status:** SUPERSEDED 2026-09-07 (CLI) - the frame was misread; the defect it points at is the JEWELER DISCOVERED card over the Title (Screenshot_20260907-132324.png is a modal on the title menu, not a black void) -> see WO-1600, FIXED in the evening gate (REGRESSION_OK 456/456). Also a NUMBER COLLISION: 1084-1087 are main-line numbers already taken; the UI seat mints from its own banner block.
**Minted:** 2026-09-07 — Grok/UI seat from Seeker phone screenshot (not F8)  
**Priority:** P0 felt — unplayable black screen  
**Evidence:** `logs/device/seeker-shots/Screenshot_20260907-132324.png`  
**Lane:** World / render / scene load (instrument first)

---

## 1. Problem (from the screenshot)

Player-facing frame is almost entirely **black**. Visible:

- A thin **blue horizontal bar** mid/lower screen  
- A small **red cursor / marker** near the bar  
- No terrain, no character mesh, no readable HUD chrome

Timed ~3 minutes before the YOU DIED shot (132616) on the same play session — may be a failed load, camera under the world, or post-combat/world transition blank.

## 2. RCA discipline

Do **not** guess magenta vs camera vs missing Addressables. Instrument / pull device log around `2026-09-07 13:23:24` local and name the dead step (scene, camera, lighting, content 404).

## 3. Acceptance

1. Repro path identified from log + screenshot time.  
2. Player sees a lit, navigable world (or an explicit error UI — never a silent black void).  
3. Capture after-fix PNG proves content is visible.

## 4. Not in scope

Death art (1084), Ashwood drop (1086), Session Expired (1087).

## 5. Paste for CLI

```text
Implement WORK_ORDER_1085 from Screenshot_20260907-132324.png.
Black void with blue strip — instrument first, then fix the named dead step.
```
