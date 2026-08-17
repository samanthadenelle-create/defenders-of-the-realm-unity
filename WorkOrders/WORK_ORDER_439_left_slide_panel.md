> ⚠ **UNRESOLVED NUMBER COLLISION — WO-439 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_439_quest_board_collection.md` (06-17, first-on-disk), `WORK_ORDER_439_left_slide_panel.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WO-439 — P2 UI: Left slide-out panel — collapsed to left edge, expands on click

**Status:** READY TO IMPLEMENT  
**Priority:** P2  
**Lane:** 4 UI/HUD  
**Minted:** 2026-07-03

---

## What

The ">" chevron button currently sits floating at bottom-left. Per owner direction:
it should be **pinned to the left edge of the screen**, collapsed by default, and
expand on click to reveal a panel with: **Chat**, **Leaderboard**, and one TBD slot
(owner to confirm third tab — candidates: Map, Party, Settings).

"The idea is a minimized experience" — nothing visible until the player requests it.

## Blink Obsidian components to use

- **`CollapseButton.prefab`** + **`ExpandButton.prefab`** — use for the tab toggle
- **`Chat.prefab`** — reference for chat panel styling
- **`QuestTracker.prefab`** — reference for secondary panel styling

## Design spec

**Collapsed state:**
- A single narrow tab (~32×96px) pinned to the left edge (anchorMin.x=0),
  vertically centered or bottom-third. Shows a "‹" icon or the ExpandButton sprite.
- The content panel is off-screen to the left (anchorMax.x = 0, or position X = -panelWidth).

**Expanded state:**
- Tab changes to "›" (CollapseButton sprite).
- Content panel animates (LeanTween or simple RectTransform lerp, ~0.2s)
  sliding in from the left to anchorMax.x ≈ 0.22 (22% screen width).
- Panel contains three tabs: Chat | Leaderboard | [TBD].
- Clicking the tab again collapses back.

## Implementation notes

- Build entirely in code (no scene hand-edits, no UXML).
- Panel is a child of the HUD canvas, parented to the PLAYER area pool or a dedicated
  overlay pool.
- Use `ElarionUiKit.BuildObsidianPanel()` for the panel background.
- Chat tab: wire to existing chat system if present; stub "Coming soon" label if not.
- Leaderboard tab: stub with placeholder rows (rank, name, score) — actual data TBD.
- Animate: `StartCoroutine` lerp on `anchorMax.x` from 0 → 0.22 over 0.2s (ease out).
  No DOTween/LeanTween dependency unless already in project.

## Owner question (block before implementing chat/leaderboard content)
**What is the third tab?** Options: Map, Party Frame, Settings, Emotes.
Owner confirms before CLI fills the content — tab slot can be stubbed in the meantime.

## Files to touch
- `Assets/_Modules/HUD/Kit/HudKitController.cs` — add `BuildLeftSlidePanel()` call,
  remove old floating ">" button
- `Assets/_Modules/Core/UI/ElarionUiKit.cs` — add `BuildSlideTab()` helper if needed

## Do NOT touch
- Any scene files, `VillageHudController.cs`, chat backend

## Acceptance criteria
- [ ] Left edge tab visible when collapsed; content panel off-screen
- [ ] Click tab → panel slides in smoothly (~0.2s), tab icon flips
- [ ] Click again → panel slides out, returns to collapsed state
- [ ] Chat and Leaderboard tabs present (may be stubbed)
- [ ] Third tab slot present (may be stubbed pending owner decision)
- [ ] Headless smoke run passes (no null refs on build)
