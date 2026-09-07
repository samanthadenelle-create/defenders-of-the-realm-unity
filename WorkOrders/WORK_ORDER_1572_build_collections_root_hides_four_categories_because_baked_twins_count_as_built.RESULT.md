# WO-1572 RESULT - baked twins no longer count as built on any offer surface

**Status:** IMPLEMENTED - 2026-09-07, uncommitted, awaiting gate. No Unity run, no git.

## Predicate, before -> after

| Surface | Before | After |
|---|---|---|
| Collection root filter | `BuildCollectionBrowser.cs:624` `IsBuilt(entry)` | `:652` `IsPlayerBuilt(entry)` |
| Item card "Built" flag | `BuildCollectionBrowser.cs:407` `IsBuilt(entry)` | `:411` `IsPlayerBuilt(entry)` |
| Category subtitle count | `StructureCardVM.cs:291` `IsBuilt(entry)` | `:296` `IsPlayerBuilt(entry)` |

`AffordableCount` moved because `BuildCollectionBrowser.cs:186-190` states the subtitle must fold the
same authorities as the cards behind the door; leaving it would promise "nothing affordable" over a
door full of buildable rows.

## Arm() guard - VERIFIED, NOT CHANGED
`Arm` -> `SingletonAlreadyBuilt` -> `IsSingletonBuilt` (`BuildModeController.cs:2334-2346`) already
returns `IsPlayerBuilt`, so a standing twin never refused a place; `NotifyPlaced -> EnforceInternal ->
StandDownBakedTwins` (`StructureSingleton.cs:299/317/401`) hides it on commit. Both correct as found.

## Other `StructureSingleton.IsBuilt(` consumers, with a verdict
`RaidCapabilityHudBridge.cs:109,155` and `StarterArmyGrant.cs:137` = **correct as IsBuilt**
(capability: a standing barracks grants raids), out of scope. `DestroyedStructureRegression.cs:299,307`
= **correct as IsBuilt** (enforcement oracle). `BuildInventoryModel` names `StructureSingleton` nowhere.

## Regression (registered: `DataRegression.cs:1062` calls `ManageBuildDoorRegression.Run`)
New `CheckCollectionRootSurvivesBakedTwins`: blank `BaseLayout`, every authored `repo.bakedTwins`
GameObject stood up (names read from the catalog, not hardcoded), both per-frame memos cleared by
reflection (`CheckLiveDoor` places a forge one method earlier and `Time.frameCount` does not advance
headless). Asserts `IsBuilt("arcane-tower")==true` **and** `IsPlayerBuilt("arcane-tower")==false`
(proves the fixture surfaced a twin), then all seven collections shown and `arcane-tower` offered.
RED against the old predicate: `build-realm` and `build-trade` drop (5/7) and are named.
`BuildCollectionPlayerRegression.cs:98-112` re-pointed - two source-text pins *required* the old
shape (`IsBuilt(entry)`, `if (entry == null) continue`) and would have reddened the fix on a wrong
cause; all six literals in that block re-verified against the new browser text. That file's header
claim that only three collections are authored is corrected in `ManageBuildDoorRegression` (seven are;
the root was emptied, not authored short).

## Instrumentation
`FlowTrace.Step("BuildCollections", ...)` per collection: `offered=n/total` plus
`hidden-by-visibility` / `no-catalog-entry` / `player-built` counts and `SHOWN`/`DROPPED`. The
predicate walks every item instead of returning on the first hit, so the count is real.

**Fixed after `Builds/reg-wave4a.log`** (`BUILD_COLLECTION_PLAYER_FAIL: palette string carries a
bracket glyph`): the first draft appended a bracketed `[baked twins do NOT count...]` note to that
trace string. `BuildCollectionPlayerRegression.StringLiterals` (`:199-260`) scans EVERY double-quoted
literal in the browser for `[` (WO-1417 palette copy) and cannot tell a diagnostic string from card
copy. The note moved to a `//` comment above the call (`BuildCollectionBrowser.cs:656-660`), which
the scanner skips; the authored trace string is unchanged otherwise. Re-verified by porting that
exact state machine and running it over the file: 111 literals, zero `[`, zero `NO COST`.

## Gate
Braces balanced, zero NUL bytes, ASCII: 65/65, 70/70, 37/37, 23/23. `COMPILE_GATE_OK` +
`REGRESSION_OK n/n` still owed by the gate lane.
