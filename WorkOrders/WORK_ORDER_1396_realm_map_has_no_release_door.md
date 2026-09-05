# WO-1396: the Realm Map (PanelId.RealmMap) has no release door - its only openers are a default-OFF flag and the dev panel

**Status:** IN PROGRESS 2026-09-05 07:50 - landed + gated on the WO's DEFAULT (ship read-only): Journey-deck "Realm Map" card -> PanelId.RealmMap; the travel stub is a worded canon line ("Travel opens with the realm roads.") and the clear reward is named; the dormant Bag MapTab door is deleted so there is ONE door; RealmMapRegression [travel-stub]. Owner question: read-only now (shipped) vs hold for WO-827 travel. Minted 2026-09-05 from the UI screen graph (overnight STRETCH).. Sequenced WITH WO-1376 (P2 "Realm Map navigation"); this is that line's concrete door slice.

## Evidence
- Graph: `docs/qa/UI_SCREEN_GRAPH_2026-09-04.md:199` (RealmMap node) and `:245` (dead end 3).
- Registration: `Assets/_Modules/Village/Hero/RealmMapPanel.cs:203` `PanelRouter.Register(PanelId.RealmMap, (System.Action)Open)`; `RealmMapPanelBootstrap.cs:3` keeps a live opener in every scene.
- Opener 1 (flagged off): `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs:661-667` `OpenRealmMap` - header comment "Reached only when FeatureFlags.MapTab is ON - the dormant entry never calls this"; `Assets/_Modules/Core/FeatureFlags.cs:842` `MapTab => Get("maptab", defaultOn: false)`, with `:836-841` explaining WHY: travel is a disabled stub until WO-827, "a visible tab would promise a journey the game cannot take".
- Opener 2 (dev only): `Assets/_Modules/DevTools/DevPanelController.cs:869-872`; DevTools is compiled out of release (`PanelRouter.cs:98-110` DevPanel doc).
- No other opener: grep `PanelId.RealmMap` under `Assets/_Modules` = the two above + the panel's own register/unregister + a STALE comment `Assets/_Modules/HUD/Kit/HudKitController.cs:854-858` ("now reached from the Bag tab row") - false against the flag default.
- Ruled absent, then re-ruled: `Assets/Editor/Regression/PublicNavigationRetirementRegression.cs:7,:15,:18-21` forbid "Realm Map" on the deck and the Bag rail entry; `docs/PROGRAM_RAID_ECONOMY_2026-09-04.md:268` "REALM MAP  Explore Elarion" and `:305-306` "Realm Map navigation" in P2; WO-1376 `:16-17,:21,:30-31`.
- Travel is still a stub: `RealmMapPanel.cs:30` "Travel is a DISABLED stub until WO-827"; `:807-821` the CTA is `interactable = _vm.TravelEnabled` (false).

## What the player experiences
A finished parchment map of five regions that no player build can open; the word "Journey" on the bar promises destinations the deck does not list (graph dead end 13). If it is opened as-is, the player meets a TRAVEL button that never enables - a door to a no-op, which the sprint doc names as the exact failure.

## Fix shape (one mechanism)
The Journey deck gains a "Realm Map" card routed like its siblings - `Route("Realm Map", "...", "map", PanelId.RealmMap, "realm-map")` in `PlayerDeckWorkspace.cs` case `PlayerDeckKind.Journey` (`:588-624`). No flag, no Bag rail entry (that path stays retired; `FeatureFlags.MapTab` and the Bag "Map" section are deleted so there is ONE door), no new opener. The map's detail pane must give the next reason to tap: while `TravelEnabled` is false the CTA slot reads a WORD from canon-strings ("Travel opens with the realm roads" - the WO-827 promise) instead of a greyed button; each node's detail names its reward. Delete the stale HudKit comment `:854-858`.

```
JOURNEY DECK  [Quests] [Raids] [Dungeons*] [Realm Map] [Season*]    * = WO-1376 / WO-1394
                                              |
                                              v PanelRouter.Open(PanelId.RealmMap)
                              REALM MAP (RealmMapPanel)  detail pane -> node reward + travel state IN WORDS
```
Oracle: re-point `PublicNavigationRetirementRegression.cs:15,:18-21` to "Realm Map present exactly once, on the Journey deck; absent from InventoryUIBuilder / InventoryPaperDoll" - stricter, never deleted.
Trace: `FlowTrace.Step("RealmMap", "opened from Journey deck; travel=<enabled|stub>; nodes discovered=<n>/<m>")` in `RealmMapPanel.Open`.

## Acceptance
- [ ] RED first: the re-pointed `PublicNavigationRetirementRegression` fails on the current tree; `RealmMapRegression` gains a pin that the travel-stub state is a canon-strings WORD, not a disabled button.
- [ ] Headless: `JourneyWorkspace_2670x1200.png` shows the Realm Map card; `RealmMap_2670x1200.png` (`UICaptureLaunch.cs:3934`) regenerated with the worded CTA slot.
- [ ] Device: Journey -> Realm Map opens; the trace line is in logcat; closing returns to the deck once WO-1400 lands (HUD until then).

## Not in scope
Realm travel itself (WO-827); dungeons card and the `/api/dungeon-status` gate (WO-1376); Season card (WO-1394); node balance (`realm-map.json` values are tunables).

## Owner question
Ship the map READ-ONLY now (explore + rewards named, travel worded as coming), or hold the card until WO-827 lands travel? Default proposed: ship read-only - the map is a reason to raid (it shows what a region pays) even before it is a road.
