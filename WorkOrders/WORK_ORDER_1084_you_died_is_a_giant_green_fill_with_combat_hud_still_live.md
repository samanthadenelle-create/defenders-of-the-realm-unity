# WO-1084 — YOU DIED is a giant green fill; combat HUD still live at 0 HP

**Status:** SUPERSEDED 2026-09-07 (CLI) - the frame was misread; the defect it points at is the SKILLS tree frame band (Screenshot_20260907-132616.png is the Skills screen, not a death screen) -> see WO-1601, FIXED in the evening gate (REGRESSION_OK 456/456). Also a NUMBER COLLISION: 1084-1087 are main-line numbers already taken; the UI seat mints from its own banner block.
**Minted:** 2026-09-07 — Grok/UI seat from Seeker phone screenshot (not F8)  
**Priority:** P0 felt — death screen unreadable / wrong  
**Evidence:** `logs/device/seeker-shots/Screenshot_20260907-132616.png`  
**Lane:** HUD / death presentation (Village death path)

---

## 1. Problem (from the screenshot)

On death the player sees:

- A near-full-screen **solid green** fill with **YOU DIED** in huge white letters.
- The **combat HUD is still up**: ability bar (Q/W/E/R), HP **0**, RETREAT, bag, etc.
- That reads as a debug/placeholder death flash, not a finished defeat beat — and it leaves combat chrome on a dead hero.

## 2. Felt intent

Death should be a clear, authored moment: readable copy, intentional art, and **town/defeat chrome** — not live combat controls on a 0 HP body under a green slab.

## 3. Acceptance

1. Death presentation is intentional (not a full-bleed green placeholder).  
2. While the death beat is showing, combat ability bar / primary attack chrome is **hidden or disabled**.  
3. HP 0 state does not leave the player looking “still in a fight.”  
4. Regression or capture proves the old green full-bleed path is gone (or gated).

## 4. Not in scope

Ashwood drop (1086), title Session Expired (1087), black void (1085).

## 5. Paste for CLI

```text
Implement WORK_ORDER_1084 from Screenshot_20260907-132616.png.
Fix YOU DIED presentation; strip live combat HUD while death UI is up.
```
