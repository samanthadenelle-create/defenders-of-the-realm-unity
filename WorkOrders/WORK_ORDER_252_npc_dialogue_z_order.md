**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 252 — NPC Dialogue Box Z-Order Fix
**Status: READY TO IMPLEMENT**
**WO:** 252 | **Lane:** HUD (parallel safe)
**Closes:** DEF-149

---
## Problem

NPC dialogue boxes overlap and obscure the HUD (health bar, compass, wave counter, d-pad) during gameplay. The dialogue Canvas is rendering on top of the HUD Canvas.

---
## Fix

**Files:** Wherever NPC dialogue box Canvas is created (likely `TownsfolkBubble.cs` or `NPCDialogueController.cs`)

Unity Canvas rendering order is controlled by `sortingOrder` (screen-space overlay) or `renderQueue` (world-space). The HUD must always render on top of dialogue.

```csharp
// In dialogue box creation — set BELOW HUD:
_dialogueCanvas.sortingOrder = 5;    // dialogue: low order

// HUD Canvas (VillageHudController or MobileHUDController):
_hudCanvas.sortingOrder = 20;        // HUD: always on top
```

If the dialogue box uses a world-space Canvas positioned above the NPC:
- Set `Canvas.renderMode = RenderMode.WorldSpace` — this renders below screen-space overlay HUD by default
- Ensure no screen-space dialogue Canvas is created accidentally

**Audit:** Search for any `Canvas` instantiation in dialogue scripts and confirm `sortingOrder <= 5`.

---
## Acceptance criteria
- [ ] Wave counter, compass, health bar, and d-pad remain fully visible when any NPC dialogue box is active
- [ ] NPC dialogue Canvas `sortingOrder` is ≤5
- [ ] HUD Canvas `sortingOrder` is ≥20
- [ ] Confirmed at 375px mobile width in Play mode on WebGL
- [ ] No UXML / UIDocument
- [ ] Brace balance check passed

## What NOT to touch
- `Village.unity` — do not hand-edit
- NPC dialogue content or trigger logic — layout/z-order only

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
