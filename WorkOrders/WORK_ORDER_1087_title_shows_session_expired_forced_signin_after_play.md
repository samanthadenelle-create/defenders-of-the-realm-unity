# WO-1087 — Title screen shows Session Expired / forced sign-in after play

**Status:** SUPERSEDED 2026-09-07 (CLI) - the frame was misread; the defect it points at is the atmosphere read of Screenshot_20260907-133243.png (haze, not a session-expired title; the session rail is proven on this device: RESET ACCEPTED 13:34) -> see WO-1602, FIXED in the evening gate (REGRESSION_OK 456/456). Also a NUMBER COLLISION: 1084-1087 are main-line numbers already taken; the UI seat mints from its own banner block.
**Minted:** 2026-09-07 — Grok/UI seat from Seeker phone screenshot (not F8)  
**Priority:** P0 felt — blocked from continuing after a session  
**Evidence:** `logs/device/seeker-shots/Screenshot_20260907-133243.png`  
**Lane:** Auth / title / wallet session  
**Respects:** WO-1583 spirit — auth should not gate ordinary play (purchases/codes)

---

## 1. Problem (from the screenshot)

Title / boot art with **TAP TO CONTINUE**, plus a modal:

- **Session Expired**  
- “Please sign in again to continue.”  
- **SIGN IN** button  

Player just played (prior shots in the same hour: death, Ashwood drop). Returning to title forces a re-auth wall instead of a quiet continue into the save.

## 2. Felt intent

Offline / already-authenticated play should resume without a surprise Session Expired modal unless the session truly cannot continue. Align with owner ruling that authentication is for purchases and codes — not every boot after a fight.

## 3. Acceptance

1. After a normal play → title (or soft reboot), TAP TO CONTINUE reaches the game without a mandatory Session Expired modal when a local/offline session is still valid.  
2. If the session is truly dead, the modal is honest and recoverable.  
3. Device log around `2026-09-07 13:32:43` names why the modal fired (token expiry vs false positive).

## 4. Not in scope

Death UI (1084), black void (1085), Ashwood drop (1086). Do not expand wallet purchase flows.

## 5. Paste for CLI

```text
Implement WORK_ORDER_1087 from Screenshot_20260907-133243.png.
Title Session Expired after play — prove why the modal fired; stop false forced sign-in for ordinary continue.
```
