# WO-1086 — Ashwood Hollow roads drop lands at the castle (Elarion)

**Status:** SUPERSEDED 2026-09-07 (CLI) - the frame was misread; the defect it points at is the biome road drop (F8 seq 4703; WO-1604 landed the fail-closed drop + boundary owner) -> see WO-1604, FIXED in the evening gate (REGRESSION_OK 456/456). Also a NUMBER COLLISION: 1084-1087 are main-line numbers already taken; the UI seat mints from its own banner block.
**Minted:** 2026-09-07 — Grok/UI seat from Seeker phone screenshot (not F8)  
**Priority:** P0 felt — prompt lied about destination  
**Evidence:** `logs/device/seeker-shots/Screenshot_20260907-132930.png`  
**Lane:** World / BiomeRoads / HollowRoadsDropInjector

---

## 1. Problem (from the screenshot)

- Center toast/prompt: **“Ashwood Hollow roads”** (player was told Ashwood).  
- Visible world: **castle / town silhouette**, open grass, not Ashwood hollow biome.  
- Bottom bar shows Build / Talk / Hero / Journey / Manage (hub/overworld chrome).

Player-felt: the journey drop promised Ashwood and delivered the castle approach.

## 2. Supporting capture (same minute — do not treat as the ticket source)

Device F8 seq **4703** (`HollowRoadsDropInjector.VerifyArrival`):  
`[Flow:BiomeRoads] drop promised Ashwood but the hero landed at (0.00, 0.08, 50.00), which ZoneManager classifies as Elarion.`

Use that line as **proof of cause**, not as the reason we opened the WO — the screenshot is the owner ask.

## 3. Acceptance

1. Choosing / taking an Ashwood Hollow roads drop places the hero in a position ZoneManager classifies as **Ashwood** (or the prompt is corrected to the true destination — prefer correct land).  
2. VerifyArrival does not Fail when the prompt and zone agree.  
3. Screenshot or FlowTrace after-fix names Ashwood arrival.

## 4. Not in scope

Death UI (1084), black void (1085), Session Expired (1087).

## 5. Paste for CLI

```text
Implement WORK_ORDER_1086 from Screenshot_20260907-132930.png.
Ashwood Hollow roads prompt must land in Ashwood — Fix HollowRoadsDropInjector / zone point.
```
