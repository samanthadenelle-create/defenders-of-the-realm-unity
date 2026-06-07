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
