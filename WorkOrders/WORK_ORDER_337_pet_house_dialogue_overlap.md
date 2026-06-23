# WORK ORDER 337 — Echo Hollow: Dialogue Text Overlaps Choice Options

**Status:** READY TO IMPLEMENT
**Lane:** 4 (UI/HUD) — parallel-safe
**Priority:** HIGH — dialogue is unreadable; core Echo acquisition flow broken
**Screenshot evidence:** docs/screenshots/pet_house_dialogue_overlap.png (2026-06-07)

---

## What's Broken

When the player interacts with the Echo Hollow (currently labelled "Pet House"),
the Yarn Spinner dialogue fires correctly — but when choice options appear, they
render **on top of** the dialogue body text instead of replacing or clearing it.

Screenshot shows:
- White text (dialogue body): "Welcome to the Pet House. Choose a guardian…"
- Green text (choice options): "Stand at shadow with you / they'll fight at your side / Defend / Quick" — rendered directly over the white text
- Purple box ("Interact: Pet House") also visible behind both
- Result: completely unreadable — three UI layers stacked with no clearing

---

## Root Cause Candidates (investigate in order)

### 1. DialogueView not hiding body text on choice display (most likely)
The custom `DialogueView` (or `LineView`) MonoBehaviour shows the NPC line and
then, when Yarn calls `RunOptions`, it opens the choice UI **without hiding the
line text**. Check the sequence:

```csharp
// In your DialogueView / YarnDialogueUI:
// RunLine → shows NPC text in a panel
// RunOptions → should HIDE the NPC text panel, THEN show choice buttons
// If the hide step is missing, both layers are visible simultaneously
```

Fix: call `linePanel.style.display = DisplayStyle.None` (UIElements) or
`linePanel.SetActive(false)` (uGUI) before populating choice buttons.

### 2. Two separate GameObjects / UIDocuments overlapping
The line display and the choice display may be on separate GameObjects with
separate UIDocuments, both active at the same time. Check the hierarchy in
the scene for any `DialogueRunner`, `LineView`, `OptionView`, `DialogueUI`
GameObjects — confirm only one is enabled at a time.

### 3. Interact tooltip ("Interact: Pet House") not dismissed before dialogue
The proximity interaction tooltip is still visible when dialogue starts.
The `NPCInteractor` or `ProximityInteractor` should hide the tooltip label
when dialogue begins. Look for the `IDialogueStartHandler` or Yarn
`onDialogueStart` event — hook tooltip hide there.

### 4. WorldSpace + ScreenSpace panel sort order conflict
If the choice panel and line panel are on different UIDocuments with the same
`sortingOrder` in PanelSettings, they may interleave. Confirm each panel has
a unique sort order: line = 10, choices = 20, tooltip = 5.

---

## Fix Steps

```
1. Open the Echo Hollow NPC interaction in the editor (or search codebase for
   the MonoBehaviour that calls DialogueRunner.StartDialogue)
2. Find RunLine / RunOptions implementations
3. Add: hide/clear the previous panel before showing the next
4. Confirm the interact tooltip is dismissed the moment dialogue starts
5. Check PanelSettings sort orders for all UIDocuments on the dialogue stack
6. Play-test: interact with Echo Hollow, confirm dialogue text → then options
   appear cleanly without overlap
7. Also test dismiss ("Not now, come back later") path works cleanly
```

---

## Acceptance Criteria

- [ ] Dialogue body text is NOT visible when choice options are shown
- [ ] Choice options are readable with no overlapping white text behind them
- [ ] "Interact: Pet House" tooltip disappears the moment dialogue opens
- [ ] Dismiss / "come back later" path works (no lingering panels)
- [ ] Fix works for all NPCs, not just Echo Hollow (same DialogueView is shared)
- [ ] No regression to Yarn Spinner dialogue logic or quest advancement

## What NOT to Touch

- Village.unity scene file (hand-edits forbidden)
- WaveManager, TowerSwapService, monetization code
- Yarn .yarn script content (the text itself is correct — only rendering is broken)
