# PROD-018 — After confirm placement, Build category cards must return

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — implementation and regression present; awaiting clean-build + Seeker verification (2026-08-30)
**Minted:** 2026-08-29 (CLI seat) — banner bumped PROD-018 → PROD-021 in the same edit (with 019, 020)  
**Priority:** HIGH — leaves the player in Build with a blank shop after every place  
**Provenance:** owner, 2026-08-29: *"when the user is in the build menu and selects the gathering card it loads the row of cards, when the user selects the building it all hides so they can place, after clicking the confirm placement button, the category row should return"* / *"the screen should return the category cards"* (including when that category is now fully placed)

---

## 1. What the player sees

1. Build → **Gathering** (category card) → building card row  
2. Tap a building → UI hides for ghost placement (correct)  
3. Tap confirm (HUD OK) → building places  
4. **Bug:** category cards (and remaining building cards) **stay gone**. Build mode is still open, shop is blank.  
5. Especially bad when the **last** Gathering singleton was just placed — the player needs the category grid back to pick another collection (Gathering should then be omitted).

---

## 2. Root cause (verified against code — not inference of feel)

WO-1273 moved browse to **`BuildCollectionBrowser`** (category-first).  
BM-1 / WO-746 still restores browse via **`BuildPaletteUI.Expand()`**, which only revives the **legacy carousel canvas**. That canvas is **forced inactive** in `BuildPaletteUI.Show()` while collections own browse.

| Step | Code | Effect |
|---|---|---|
| Select building | `BuildCollectionBrowser.Place` → `Close()` | Browser gone (correct for ghost) |
| Confirm OK | `BuildModeController` → `CancelArmed(afterPlacement:true)` → `_palette.Expand()` | Legacy Expand; **no** `_collectionBrowser.Show()` |
| Finite refresh | `OnFiniteCapacityChanged` only while browser open + on category view | Closed browser never re-filters exhausted Gathering |

Cite: `BuildModeController.cs` ~2155–2167, ~2277–2300; `BuildPaletteUI.cs` Show ~322–335, Expand ~853+; `BuildCollectionBrowser.cs` Place ~222–228, `CollectionHasVisibleItems` ~257–271.

---

## 3. Fix (concrete)

**Preferred:** In `BuildPaletteUI.Expand()` (or replace Expand call from `CancelArmed` with an explicit `RestoreBrowse`):

- Keep legacy `_canvas` **inactive**  
- Call `_collectionBrowser.Show(entry => OnEntrySelected…)` so **`RenderCategories()`** runs  
- FlowTrace: `expand: restored BuildCollectionBrowser categories`

**Optional polish:** before `Place`/`Close`, stash last `collectionId`. On restore, if that collection still has visible items, reopen it; else categories. Owner asked for **category cards** — categories-first is the minimum acceptance.

Also restore on **cancel** while placing (same `CancelArmed` path).

**Do not** change place costs, singleton rules, or catalog ids.

---

## 4. Files

- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` — Expand / restore browse  
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — BM-1 comments; only if API rename  
- Optional: `BuildCollectionBrowser.cs` — remember last collection  
- `Assets/Editor/Regression/BuildCollectionPlayerRegression.cs` — pin restore contract  

---

## 5. Acceptance

1. Gathering → pick building → confirm → **category cards return** (not blank Build).  
2. If Gathering still has unbuilt eligible cards, player can open it again without Done / re-enter Build.  
3. After placing the **last** Gathering singleton, restored grid **omits Gathering**.  
4. Cancel while placing restores the same browse surface.  
5. Pause / FocusedModalHost re-acquired as `Show()` already does.  
6. `COMPILE_GATE_OK`; regression updated.  

## 6. Not in scope

Defense→Manage shortcut, first-use guide copy, charge/BaseLayout, singleton enforcement logic (only re-render).
