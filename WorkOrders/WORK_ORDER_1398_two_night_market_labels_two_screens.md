# WO-1398: two HUD rows both read "Night Market" and open two different screens - the store's name must come from ONE string source

**Status:** IN PROGRESS 2026-09-05 06:15 - landed + gated (COMPILE_GATE_OK, REGRESSION_OK 379/379 incl. the new store-name-single-source suite, UI_CAPTURE_OK; trace `store face label='The Night Market' source=canon-strings`); awaiting the next APK. Owner question kept at the WO default: the gear-dock row for the Realm deck reads "Realm". Minted 2026-09-05 from the UI screen graph (overnight STRETCH).

## Evidence
- Graph: `docs/qa/UI_SCREEN_GRAPH_2026-09-04.md:116` (HUD card -> RealmStore), `:130` (gear-dock "Night Market" -> RealmDeck, "NOT RealmStore"), `:249` (dead end 7).
- Row 1, the HUD card: `Assets/_Modules/HUD/Kit/HudKitController.cs:1207` `BuildObsidianButton(root.transform, "Night Market", ...)` - literal; `OpenNightMarket` `:1403-1415` opens `PanelId.RealmStore`. WO-1384 sized this card to 320x156 as "the shining gem" (its status line) and `:1005` requires the WORD "NIGHT MARKET" on one line.
- Row 2, the gear dock: `HudKitController.cs:3955` `AddDockTab(_slideDock.panel, dockRow++, "Night Market", OpenRealmStore)` - literal; but `OpenRealmStore` `:4048-4059` opens `PanelId.RealmDeck` (the card launcher: Realm Store / Defense Report / Monthly Ledger / Game Guide, `PlayerDeckWorkspace.cs:626-631`). The method name lies about its target; the comment block `:3961-3968` still describes it as "a SECOND CALLER of the door ... PanelId.RealmStore".
- The store's own title: `Assets/_Modules/Wallet/PackStore.cs:837` `BuildObsidianModal("PackStoreUI", StoreStrings.Get(StoreStrings.KeyWordmark), ...)`; `StoreStrings.cs:122` `KeyWordmark = "storeWordmark"`; `Assets/Resources/Data/Canonical/canon-strings.json:185` `"storeWordmark": "The Night Market"`. That is the ONE canon source - and neither HUD row reads it.
- A third and fourth name for the same door: the Realm deck card `PlayerDeckWorkspace.cs:626` `Route("Realm Store", ...)` and `GooglePlayStorefront.cs:44` title `"REALM STORE"` (WO-1395), both literals.
- HUD already reads canon strings through `Assets/_Modules/Core/UI/HudStrings.cs:49,:103` (`HudStrings.Get(key)`, used at `HudKitController.cs:1834`), so the reader exists in an assembly the HUD may reference.

## What the player experiences
Tap "Night Market" on the HUD card: a store. Tap "Night Market" in the gear dock: a four-card launcher whose first card is called "Realm Store", which opens ... the Night Market. One name for two screens and two names for one screen; a first-time player cannot learn what the word means.

## Fix shape (one mechanism)
ONE string source for the store's name: every face that opens `PanelId.RealmStore` renders `canon-strings.json` `storeWordmark` (through `HudStrings.Get` in HUD, `StoreStrings.Get` in Wallet) - the HUD card, the Realm deck card (rename its route key copy accordingly), the Play skin title (WO-1395). The dock row that opens the Realm DECK is relabelled to what it opens - "Realm" (the workspace's own name, `PlayerDeckWorkspace` kind Realm) - and the method is renamed `OpenRealmDeck`, comment block `:3961-3968` rewritten. No literal "Night Market" / "Realm Store" remains in any `.cs` under `Assets/_Modules`.

```
HUD card   [The Night Market]  -> RealmStore     (label = storeWordmark)
gear dock  [Realm]             -> RealmDeck  --> [The Night Market] [Defense Report] [Monthly Ledger] [Game Guide]
                                                     (label = storeWordmark)
```
Trace: `FlowTrace.Step("Store", "store face label='<storeWordmark>' source=canon-strings site=<hud-card|realm-deck|play-skin>")` once per built face; a missing key falls to `FlowTrace.Fail("Store", "storeWordmark unresolved at <site>")`, never to a literal.

## Acceptance
- [ ] RED first: a `StoreNameSingleSourceRegression` - source scan: the literals `"Night Market"` and `"Realm Store"` occur in NO `.cs` under `Assets/_Modules` (canon-strings.json and comments excepted); every `PanelRouter.Open(PanelId.RealmStore)` face site reads `storeWordmark`. Fails on the current tree (`HudKitController.cs:1207,:3955`, `PlayerDeckWorkspace.cs:626`).
- [ ] Headless: `AdaptiveHudGearOpen_2670x1200.png` shows the dock row "Realm"; `AdaptiveHudPeaceful` shows the card reading the canon wordmark on ONE line (WO-1384 `:1005` rule; `HudLabelFitRegression` green); `RealmWorkspace` shows the first card renamed.
- [ ] Device: both routes screencapped; the card and the store title read the same words.

## Not in scope
The Realm deck's existence or its card set; the store's contents; the double registrar (WO-1395); back-to-deck (WO-1400).

## Owner question
Dock row label for the Realm deck: "Realm" (default proposed) - or fold the four Realm cards elsewhere and retire the dock row? One word.
