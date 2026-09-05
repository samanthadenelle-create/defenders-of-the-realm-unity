# WO-1400: deck cards have no "back to deck" - closing any card's screen lands on the HUD, not the deck the player came from

**Status:** READY TO IMPLEMENT - minted 2026-09-05 from the UI screen graph (overnight STRETCH)

## Evidence
- Graph: `docs/qa/UI_SCREEN_GRAPH_2026-09-04.md:62` ("every card: OpenCard :530-536 CLOSES the deck first ... no back to deck"), `:189-191` (closes-to column: "Close :534 before any card opens"), `:251` (dead end 9); the same shape is dead end 10 (`:252`, Manage -> store).
- The code: `Assets/_Modules/HUD/PlayerDeckWorkspace.cs:530-536` `OpenCard`: `Close(); Guard.Try(... spec.Open); FlowTrace.Step("Navigation", "deck card -> " + title);`. `Close()` is the base `ObsidianNavigationWorkspace.Close` (`Assets/_Modules/Core/UI/ObsidianNavigationWorkspace.cs:102-111`): clears the navigation stack and traces "closed workspace ... to world".
- Why it must close first: the arbiter is exclusive - `PanelManager.NotifyOpened` (`Assets/_Modules/Core/UI/PanelManager.cs:188,:220-236`) closes `previous` when a new handle opens; `PanelRouter.cs:148-150` "opening one panel closes any other". So the deck cannot simply stay open beneath.
- Nothing remembers the parent: `PanelManager.NotifyClosed` (`:278-292`) sets `_open = null`, arms the WO-1393 close grace, raises `OpenStateChanged` - no return door. The only return-to-parent notion in the tree is `ObsidianNavigationWorkspace.Done(commit, returnToParent)` (`:87-97`), a per-workspace callback the decks never use.
- Every child closes to nothing: Inventory `InventoryUIBuilder.cs:114` Scrim -> Close; Equipment `EquipmentPanel.cs:175,:954`; Skill tree `HeroSkillTreePanelMvvm.cs:1940`; Loadout `:320`; RumorBoard `:258`; Store `PackStore.cs:1123`; Defense Report `:123`; Ledger `:167,:213`; Guide `:100` (graph `:63-71,:83,:118,:122,:133-135`).
- Precedent for stacking that does NOT apply: RaidDeploy sits over RaidSelection by sorting order alone (`RaidDeployScreen.cs:9-10,:112`) and registers its own arbiter handle (`:164`) - a special case, not a mechanism.

## What the player experiences
Hero -> Bag -> close -> HUD. Hero -> Equipment -> close -> HUD. To browse the four Hero cards the player re-opens the deck four times; the deck reads as a splash screen, not a place. Journey and Realm behave the same. Nothing about the deck says "you can come back here".

## Fix shape (one mechanism)
A RETURN DOOR on the arbiter, set by whoever hands off: `PanelManager.SetReturnDoor(string name, Action reopen)` - one static slot. `PlayerDeckWorkspace.OpenCard` sets it to its own `Open(page)` for the current kind BEFORE `Close()`. `NotifyClosed`, when the open slot becomes null (a close to nothing, not a swap - `NotifyOpened` replacing `previous` never consumes it, so Equipment -> Skills -> close still returns to the Hero deck), invokes and clears the door on the NEXT frame (after the WO-1393 close grace). `CloseAll` (posture flip to combat, `:298`) and `PauseGate.RequestBack` when nothing is open clear it without invoking. MVVM untouched: the door is arbiter state, no panel learns about decks. Manage -> store (dead end 10) can set the same door later - out of scope here.

```
HERO DECK --OpenCard--> SetReturnDoor("Hero deck", reopen) ; Close() ; PanelRouter.Open(Inventory)
   ^                                                                        |
   |                                                                   Scrim / X -> NotifyClosed
   `--------------------- return door fires next frame (open==null) <-------'
Equipment -> Skills : NotifyOpened swaps, door KEPT ; combat CloseAll : door CLEARED
```
Trace: `FlowTrace.Step("Navigation", "return door SET '<name>' by <caller>")`, `"return door FIRED '<name>'"`, `"return door CLEARED reason=<closeall|pause|consumed>"`.

## Acceptance
- [ ] RED first: a `DeckReturnDoorRegression` (EditMode, arbiter only) - open deck, open card, close card => deck's `IsOpen` probe true and trace "return door FIRED 'Hero deck'"; open card, swap to a second panel, close => still returns; `CloseAll` => no reopen. Fails on the current tree. `ModalArbiterRegistrationRegression`, `SessionShapeRegression`, WO-1393 `[close-frame-grace]` still green.
- [ ] Headless: a `HeroWorkspace_return` capture - frame after closing Bag shows the Hero deck, not the HUD.
- [ ] Device: Hero -> Bag -> X lands on the Hero deck; Journey -> Quests -> Back lands on Journey; dock -> Pause with nothing open does not reopen a deck; entering combat with a card open returns to the combat HUD.

## Not in scope
Card sets (WO-1394/1396/1397); the store's own CLOSE label; Manage -> store return (dead end 10 - same door, its own ticket); RaidDeploy's sorting-order stack.

## Owner question
None - CoC returns to the parent screen on close (design tie-breaker memory); default is return-to-deck.
