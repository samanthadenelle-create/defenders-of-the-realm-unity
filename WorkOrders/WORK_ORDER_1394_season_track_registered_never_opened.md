# WO-1394: the Season Track (PanelId.BattlePass) is registered and nothing opens it - give it its ruled Journey door

**Status:** READY TO IMPLEMENT - minted 2026-09-05 from the UI screen graph (overnight STRETCH). Sequenced WITH WO-1376 (P2 "Season Pass navigation"): this is that line's concrete door slice, and it may not land before WO-1375's raid-XP feed is proven (a track that never moves is a second dead end).

## Evidence
- Graph: `docs/qa/UI_SCREEN_GRAPH_2026-09-04.md:211` (node row: "NONE - no PanelRouter.Open(PanelId.BattlePass) in Assets/_Modules") and `:243` (dead end 1); capture gap `:274`.
- Registration: `Assets/_Modules/Wallet/BattleMonthlyPanelsBootstrap.cs:64` `PanelRouter.Register(PanelId.BattlePass, OpenSeasonTrack)`; its own header `:15-16` records the earlier defect "NOTHING REGISTERED IT ... the screens shipped unopenable" - the registration was fixed, the DOOR never was.
- Openers: repo grep of `PanelId.BattlePass` under `Assets/_Modules` finds ONLY the registration (`:15,:56,:64,:96`); every other hit is a suite or the capture fixture list (`Assets/Editor/UICaptureLaunch.cs:6745`). The store's only PanelId-parameterised door, `PackStore.BuildFreeDoor` (`Assets/_Modules/Wallet/PackStore.cs:1887`), has exactly one occurrence in the file - its definition.
- The absence is RULED, not accidental: `Assets/Editor/Regression/PublicNavigationRetirementRegression.cs:7` "Owner ruling: Realm Map and Season Pass have no public navigation entry points"; `:15-17` forbid `PanelId.BattlePass` / "Season Track" in PlayerDeckWorkspace.cs and PackStore.cs. `NightMarketUiRegression.cs:283-284` pins the FREE band clear of it.
- The ruling MOVED on 2026-09-04: `docs/PROGRAM_RAID_ECONOMY_2026-09-04.md:345` "The Season Pass has NO navigation entry BY RULING ... section 8's Journey card re-points that oracle; it does not delete it"; WO-1376 `:16-17,:21` (Journey = Quests / Raids / Dungeons / Realm Map / Season) and `:30-31`; WO-1375 `:24-26` "re-point that oracle, never delete it".
- XP feed: `BattleMonthlyPanelsBootstrap.cs:79-93` wires `ArenaOutcomeRelay` -> `BattlePassService.OnArenaResult` and (since 2026-09-04) `OnRaidResult`, pinned by `RaidSeasonXpRegression [wired]`.

## What the player experiences
Thirty authored tiers, a track that raids now feed, and no way to ever see it. The raid loop's monthly cadence (PROGRAM section 217) has no screen, so a raid's season progression is invisible - the player cannot be given "a reason to tap the next one" by a screen that does not exist to them.

## Fix shape (one mechanism)
The Journey deck grows a fifth card, "Season", routed exactly like its siblings - `Route("Season", "...", "season", PanelId.BattlePass, "season")` in `PlayerDeckWorkspace.cs` case `PlayerDeckKind.Journey` (`:588-624`). No new opener, no new PanelId, no store door (the FREE band stays two tabs per `NightMarketUiRegression`). The card is `Available` only while `PanelRouter.IsRegistered(PanelId.BattlePass)`; its Purpose line comes from canon-strings.json, not a literal.

```
JOURNEY DECK  [Quests] [Raids] [Dungeons*] [Realm Map*] [Season]      * = WO-1376 siblings
                                                          |
                                                          v  PanelRouter.Open(PanelId.BattlePass)
                                            SEASON TRACK (SeasonTrackPanel)  <- Scrim/close -> deck (WO-1400)
```
Oracle: re-point `PublicNavigationRetirementRegression.cs:15-17` from "absent" to "present exactly once, on the Journey deck, and nowhere in PackStore" - stricter, never deleted (WO-1159 precedent).
Trace: `FlowTrace.Step("Navigation", "deck card -> Season")` is already emitted by `OpenCard` (`PlayerDeckWorkspace.cs:535`); add `FlowTrace.Step("BattlePass", "Season Track opened from Journey deck tier=<n>")` in `OpenSeasonTrack`.

## Acceptance
- [ ] RED first: `PublicNavigationRetirementRegression` re-pointed to require the Season card on the Journey deck fails on the current tree, then passes.
- [ ] Headless: `JourneyWorkspace_2670x1200.png` regenerated with five cards; a new `SeasonTrack` capture case in `UICaptureLaunch.cs` (capture gap `:274` closed); `BattleMonthlyRegression [one-screen-owner]` still green.
- [ ] Device: Journey -> Season opens the track; the tier read on screen equals `BattlePassService` state after one raid (logcat `[Flow:BattlePass]` line).

## Not in scope
Dungeons / Realm Map cards (WO-1376, WO-1396); the raid XP amounts (tunables, PROGRAM section 6); selling tiers (ruling Q4 "NEVER SELL TIERS"); the deck back-to-deck return (WO-1400).

## Owner question
None - the Journey Season card is ruled in PROGRAM_RAID_ECONOMY section 8. Proposed card copy defaults: title "Season", purpose "Raid to climb this month's track".
