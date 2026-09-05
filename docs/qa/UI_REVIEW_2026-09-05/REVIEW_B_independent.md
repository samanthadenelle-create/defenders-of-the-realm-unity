# UI Review - Reviewer TWO (independent, read-only; retention / CoC-style town-builder lens) - Sprint 2026-09-05

Produced 2026-09-05 ~07:19 by a read-only agent that saw no other review. Saved verbatim by the CLI (abridged only
where the finding text is identical to Reviewer ONE's; the ranking and CoC comparisons are the reviewer's own).
The CLI's merged verdict is in `REVIEW_MERGED.md`.

Scope: read the sprint doc, the screen graph, the walk INDEX, the overnight handover; opened every PNG in
`docs/qa/UI_REVIEW_2026-09-05/` (00-16) and ~33 `Builds/ui-capture/*_2670x1200.png`. Everything not seen on a PNG is
marked **inferred**. Frames dated 09-01 or 08-27 (Bag, HeroEquipment, BuildPaletteDock, BuildCollections_Page2,
ManageQueue*, RealmMap, DailyChest, RaidsFaceStates) are stale; findings on them are lower confidence.

**Index defect first:** `15-hud-before-journey.png` is NOT the HUD - it is a second copy of the Cathedral upgrade page
(visually identical to `14-research-door-result.png`). The HUD that INDEX row 15 describes is `16-journey-deck.png`
(a HUD frame, not the Journey deck). Rows 15/16 of `INDEX.md` should be re-labelled.

## The through-line first (Phase 4 lens)

The raid loop Journey -> RaidSelection -> RaidDeploy -> battle -> settle -> Troops -> Heartfire -> Journey has
**three visible doors and four missing ones**:

| Arrow | Visible door? | Evidence |
|---|---|---|
| HUD -> Journey | yes (bar face) | `02-hud-town.png` |
| Journey -> Raids | yes, but the card says nothing about state | `JourneyWorkspace_2670x1200.png` "Choose a camp and deploy your..." (truncated) |
| RaidSelection -> a reason to pick one | **no loot preview** on any row | `RaidSelection_2670x1200.png` rows show walls + defenders only |
| RaidDeploy -> Troops when army is empty | **text, not a door**: "No troops trained yet. Visit the Barracks." | `RaidDeploy_2670x1200.png` |
| settle -> Troops | **no capture exists** (`RaidVictoryController.cs` exists; no PNG) | WO-1389 claims the row - unverified |
| Troops -> next unlock named | not on the device frame; "UPGRADE TO L4 / 6m 0s . Ready" carries no benefit | `07-manage-troops.png` |
| Heartfire -> "next charge in" | HUD says only "Heartfire is full" (clipped) | `02-hud-town.png`, `16-journey-deck.png` |

The return loop (welcome-back -> collect) is strong on resources and silent on everything else that brings a CoC
player back: nothing says a build finished, a troop finished, or the town was attacked.

## A. Manage

Session loop: MANAGE -> three chips all `0/2` + "Choose a path"; nothing says which path has something waiting.
Troops is the one tab that closes its own loop (TRAIN -> TRAINING NOW -> OPEN QUEUE). Where it breaks: the launcher
never says *what is idle* (CoC's builder economy runs on "2 builders idle" being visible from the town), the rows never
say *what the upgrade buys*, and the Troops tab never shows "Army 3/10".

- **A1.** Launcher chips `Builders 0/2 . Training 0/2 . Research 0/2` say nothing about idleness; headless shows `0/0`.
  CoC: "Builders 2/2" with the next-free timer, tap jumps to the idle builder. Fix: `Builders idle - 2 free` /
  `Builders 1/2 . next free 3m 59s`.
- **A2.** Defense/Buildings rows carry cost + status, never the benefit; "grid 5, 16" is a developer coordinate on a
  player screen. Fix: `Arcane Spire  L1 -> L2  .  +5% damage  .  2m 30s` (the upgrade page already authors the bullet).
- **A3.** Troops card `Available` under `LEVEL 3` has no referent; the army total is absent. CoC: "Army 30/50"
  persistently. Fix: `3 in your army . Army 3/10`.
- **A4.** `UPGRADE TO L4 / 6m 0s . Ready` names a number, not a payoff; WO-1389's next-unlock line is unverified on any
  frame (`ManageTroops_2670x1200.png` predates it) - capture Troops on 356386+ before closing 1389.
- **A5.** Queue drawer header says `BUILDERS / QUEUE` over a TRAIN card. Fix: header takes the line name.
- **A6.** `Permanent builder +1 / BUY BUILDER` shows while 1 of 2 slots is free. CoC: the buy prompt appears only when
  all builders are busy. Fix: show only when active==max; otherwise `1 slot free - tap TRAIN to fill it`.
- **A7.** Research rows do not say what the research does; door label truncates. Fix: subtitle = perk effect; button `UPGRADE`.
- **A8.** Fresh upgrade page: card washed yellow-brown, CTA line dimmed; `Timber Wagon ... Coming soon - ta...` truncated
  and puts a cash door beside the first upgrade a new player sees; no duration anywhere. CoC: cost, time, stat delta;
  gem shortcut is a secondary line, never a cash pack. Fix: `Takes 2m 30s` under COST; store door as a text link.
- **A9.** Manage -> store -> CLOSE lands on the HUD (`11` -> `12`), ejecting the player from the management loop.
  Fix: PackStore.Close re-opens the sending PanelId when the opener was Manage.
- **A10.** Only two of "Showing 1-4" rows visible above CLOSE on Defense/Buildings.

Rulings: **Gridref?** default no. **Upsell-when?** default busy-only. **Return?** default Manage tab.

## B. Journey

Session loop: two-card deck with the bottom 60% empty -> four camps by difficulty and garrison -> scout report
compares garrison to "you field 0" (WO-1389 proven) but offers `BEGIN ASSAULT` at full prominence with zero troops.
Where it breaks: **there is no loot anywhere in the chain.** CoC's reason to raid is the loot number shown before you
attack; here the only pre-attack information is how hard it is.

- **B1.** No raid row shows what you win. Fix: `Spoils: ~240 wood, ~85 iron` per row (yield estimate or the authored
  loot headline).
- **B2.** Empty-army deploy leads with `BEGIN ASSAULT`; `ARMY READY?` is a question on a button. CoC: attack disabled,
  "Train troops" door. Fix: deployable==0 -> wide button `TRAIN TROOPS` -> Manage/Troops; `ARMY READY?` -> `CHECK ARMY`.
- **B3.** Journey deck cards carry no state and truncate; WO-1389's `Army 3 / 10` subtitle not on the 07:02 frame -
  unverified. Fix: `3 quests . 1 new`, `Army 3/10 . 2 camps open`.
- **B4.** Scout-report header and echo quote truncate (`Est. ~2:30 | Troops 0 | Pow...`).
- **B5.** Difficulty carried by colour first; the three gold diamonds on every row carry nothing. Fix: label or hide.
- **B6.** Settle/victory screen uncaptured - the sprint's main arrow is unproven. Highest Phase-4 cost gap.
- **B7.** Wave-clear end state is good copy with no door. Fix: `UPGRADE NORTH GATE` + `Next wave in 14m`.
- **B8.** Realm Map is a screen with nothing to tap (WO-1396).
- **B9.** Rumor Board titles read as filler ("Standing Watch Over the Western Fields 1 / 2"); `ENDGAME` chip unexplained.
  Fix: first line = the objective; `ENDGAME` -> `LATER` with a lock until its gate.
- **B10.** Heartfire never explains itself; "Heartfire is full" clipped on every device HUD frame. Fix: `Heartfire 3/3 -
  spend one to raid` (or whatever it gates - RULING) and `Next charge in 3h 12m` when not full.

Rulings: **Loot-preview?** default yes. **Heartfire-does?** one clause needed from the owner. **Stars?** hide until they vary.

## C. Build

Session loop: BUILD -> 8 categories -> carousel with NEED costs -> ghost + three icon buttons -> orient modal ->
confirm. Where it breaks: cost is everywhere and *time* is nowhere; the confirm step shows neither; "Upgrade Defenses"
is a Manage door wearing a build card.

- **C1.** Two of eight categories are not build categories (`Upgrade Defenses`, `Trade / Visit shops.`) and two overlap
  (`Defenses` vs `Protection`). CoC: Army / Resources / Defenses / Traps / Decorations, nothing opens a management screen.
  Fix: `Trade - Build shops and stalls.`; `Upgrade Defenses` -> footer link `Already built? Manage defenses >`;
  Protection re-worded or folded (RULING).
- **C2.** Carousel affordability is red numbers; `NEED` wraps `NEE / D` (stale frame). Fix: `Short 80 wood` in words.
- **C3.** Intent bar is three icon-only buttons. Fix: `PLACE  ROTATE  CANCEL` labels.
- **C4.** "First build: select a category." persists while a ghost is armed. Fix: the banner takes the phase.
- **C5.** Confirm shows orientation only - no cost, no build time. CoC: the last tap before spending shows cost + time +
  "Builder 1 free". Fix: one line above CONFIRM.
- **C6.** Ghost blocked-state copy (`Not enough Wood`) is good.

Rulings: **Protection?** default keep, re-word. **Upgrade-card?** default footer link.

## D. Hero

Session loop: HERO -> five cards -> each opens a full panel with no way back (WO-1400 now fixes). Bag shows five
`empty` slots; Skills `WISDOM 0` + locked nodes; Loadout "No unlocked skills yet." with no button. Where it breaks:
the whole subtree is empty-state and none of the empty states say where the thing comes from.

- **D1.** One system, four names: `SKILLS` / `TALENT TREE` / `TALENTS` (two cross-buttons) / `Hot-Swap Skills` /
  "Unlock SKILL nodes". Fix: one noun (RULING; default `Talents` for the tree, `Loadout` for the sockets).
- **D2.** Empty states name the requirement but never the source (`WISDOM 0`, `Needs 1 Wisdom (have 0)`, five `empty`
  slots). Fix: one source clause each; the Loadout line becomes a button `OPEN TALENTS`.
- **D3.** `Needs 1 Wisdom (have 0)` rendered in green (stale popup frame). Fix: body colour.
- **D4.** Wardrobe lock copy "Complete its requirement first" names nothing (CLI: the 07:10 fixture fix renders it
  available; the LockReason gap remains for a genuinely unregistered shop).
- **D5.** Bag header `Thrain the Wise / MAGE LV 1` beside a portrait captioned `GROM` (stale; likely fixture).
- **D6.** Equipment compare (`DAMAGE +0% SAME`) is the model to copy.

Rulings: **Noun?** default Talents. **Wisdom-source?** needs the owner's sentence.

## E. HUD + Talk + Night Market + welcome-back + pause/settings

Session loop: cold launch -> welcome-back with three resource rows -> COLLECT -> HUD. On the HUD the largest, most
saturated element is the NIGHT MARKET card; the wave timer says `Next wave in 855s`; nothing names a thing that is
ready. Where it breaks: the return moment reports resources only, and the HUD has no "something is ready" surface, so a
returning player's first reason to tap is the store.

- **E1.** Welcome-back reports only resources (fresh frame adds the Echo mending lines - better). CoC: loot-stolen /
  builder-finished / troops-ready bundle. Fix: two optional rows `FINISHED WHILE AWAY Footman x1, Arcane Spire L2`
  (door: Manage) and `ATTACKED 1x north gate breached` (door: Defense Report).
- **E2.** The HUD has no "ready" surface; no Builders chip on the device frame when idle. CoC: builder count + next-free
  timer always on the town HUD. Fix: show the chip when idle, `Builders idle 2`.
- **E3.** Store card is the loudest element on the HUD; for a wave-7 player with nothing to buy the most prominent
  invitation is to spend. CoC: shop is a small fixed button; the loud surface is the attack button. Fix: cap the card
  to bar-face height; give the space to a `RAID` call when RaidCapable. Observation for the CLI: five labelled faces
  visible while the graph pins `MaxVisibleFaces = 4` - reconcile at source (Talk is conditional per CLAUDE.md s7).
- **E4.** `Next wave in 855s` -> `14m 15s`.
- **E5.** Heart plate line 2 clipped and generic on every device frame.
- **E6.** Night Market without a wallet: `Price unavailable` x6, `UNAVAILABLE` x3, badges truncated, `MONTHLY LEDGER`
  clipped under `CLOSE THE GAP` (device too), contents truncate mid-list, "2.7x the Hearth Spark's wood" compares to a
  pack not on screen - while the Realm deck promises "clearly priced realm offers". Fix: show `~ $19.99` without a
  wallet (the device frame renders it) and ONE banner `Connect a wallet to buy - prices shown in USD`.
- **E7.** Gear dock rows collide/truncate (`...ERBOARD`, `NIGHT MARK...`) - layout, not the string source.
- **E8.** Help: `RESET HERO & PET` (retired word), destructive with no consequence copy; `DEV TOOLS` on a player frame.
- **E9.** Pause has RESUME and CLOSE.
- **E10.** Settings captured `Master 0%` / `Mute all audio On` - if that is the default, first launch is silent (inferred).
- **E11.** Defense Report empty-state text unreadable (light grey on beige).
- **E12.** Talk compact line offers nothing to tap; Talk verbs have no data (graph dead-end 5). Fix: `>` continue glyph;
  Echo speakers get `Assign work >`.
- **E13.** Echo roster "x1.5 to every node's yield" contradicts canon (additive). `Next Echo in 2 waves` is a good hook -
  keep. Fix: `Echoes 3/6 - harvest +N% together` from EchoBonusCalculator.
- **E14.** Daily Chest exists (`CLAIM 500 GOLD / AD NOT READY`) but has no door in the graph - trace its opener first.

Rulings: **Card-size?** default yes. **Fiat?** default yes. **Return-rows?** default yes. **Pet?** -> ECHO.

## Consolidated ranking (by retention impact)
1. B1 loot preview on raid rows - the reason to raid is absent.
2. B2 empty-army deploy leads with BEGIN ASSAULT; "Visit the Barracks" is text.
3. E1/E2 return and HUD have no "ready" surface; A1 launcher chips say nothing about idle.
4. B6 settle screen uncaptured - the sprint's main arrow is unproven.
5. A2/A4/A7 rows show cost, never benefit.
6. E3 store card is the loudest HUD element; Raids face absent on the device bar.
7. E6 wallet-less store is nine "unavailable"s.
8. D1 four names for one system; D2 empty states without sources.
9. A9 store CLOSE ejects from Manage.
10. C3/C5 icon-only intent bar; confirm without cost/time.

## Not re-reported (fixed tonight; the fresh frame agrees)
WO-1391 preview/false shortfall; WO-1392 harvest loss; WO-1393 tap-through and drawer; WO-1398/1399 label source and
Settings->Help; WO-1401 rail geometry.

## Capture gaps that blocked this review
Raid victory/settle, Armies/Muster, Manage post-action rows, Troops tab on 356386 (for WO-1389 items 3 and 6), Journey
deck on device, a fresh Bag/Equipment/Palette/Queue set (08-27..09-01), the Daily Chest opener.
