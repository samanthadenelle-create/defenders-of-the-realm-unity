# UI Review - Reviewer ONE (independent, read-only) - Sprint 2026-09-05
# "does this screen give the player a reason to tap the next one?"

Produced 2026-09-05 ~07:15 by a read-only agent that saw no other review. Saved verbatim by the CLI; the CLI's
merged verdict is in `REVIEW_MERGED.md` (every claim re-read against the PNG/code before it becomes a ticket).

Read-only. Every claim is from a PNG opened this session or a doc line read this session. Device PNGs are build 355952 (bedtime baseline, `docs/qa/UI_REVIEW_2026-09-05/INDEX.md:1`); headless PNGs carry the mtime read from `Builds/ui-capture/`, so freshness is stated per file. Items the handover says were fixed overnight (`docs/HANDOVER_2026-09-05_overnight.md`) are only reported where a FRESH capture still shows them.

## 0. Meta-findings on the review inputs themselves

1. **INDEX rows 15 and 16 are mislabelled.** `15-hud-before-journey.png` is the Cathedral of Magic upgrade page (identical frame to `14-research-door-result.png`), not the HUD; `16-journey-deck.png` is the HUD (Wave 7, `Next wave in 696s`), not the Journey deck. The INDEX verdict for row 15 is written against a PNG that does not show a HUD. The HUD claims are still true of `02-hud-town.png` and `12-hud-after-store-close.png`.
2. **No device capture exists for anything past Manage.** Journey deck, RaidSelection, RaidDeploy, Build, Hero, Talk, Night Market sections were not reached. Subtrees B, C, D below are from headless captures, several predating this week (`Bag` 09-01, `HeroEquipment` 08-31, `RaidsFaceStates` 08-31, `BuildPaletteDock_*` 08-27, `RealmMap` 09-01, `ManageQueue*` 09-01). Flagged inline.
3. **`Builds/ui-capture/BuildingUpgrade_2670x1200.png` (00:29, post-WO-1391) renders the whole card body at roughly 30% brightness** - title, tier row, cost pills, the "Short 1280 Wood, 800 Gold / Harvest more wood, or set an Echo to gather it." sentence and the "Timber Wagon 3500 Wood - $2.99 / Coming soon - ta..." pill are dimmed near-unreadable, while CLOSE / Upgrade / Skills are full brightness. Either the fixture dims deliberately (not proven) or the page dims the whole body when unaffordable. Needs the CLI to confirm against the code.

---

## A. MANAGE subtree

### A1. Screens reviewed
| Screen | File(s) |
|---|---|
| Launcher | `03-manage-launcher.png` (device); `Builds/ui-capture/ManageWorkspace_2670x1200.png` (09-05 07:02, Troops locked state) |
| Defense tab | `04-manage-defense.png` (device); `ManageDefense_2670x1200.png` (09-04 23:07, empty state) |
| Buildings tab | `05-manage-buildings.png` (device); `ManageBuildings_2670x1200.png` (09-04 23:07) |
| Troops tab | `07-manage-troops.png`, `08-troops-after-train.png` (device); `ManageTroops_2670x1200.png` (09-04 23:07) |
| Research tab | `06-manage-research.png`, `13-research-again.png` (device); `ManageResearch_2670x1200.png` (09-04 23:07, unlocked state) |
| Queue drawer | `09-troops-queue-drawer.png`, `10-troops-after-upgrade.png` (device); `ManageQueueDefense_2670x1200.png`, `ManageQueueTroops_2670x1200.png` (09-01, STALE) |
| Upgrade page | `14-research-door-result.png` (device, pre-1391); `BuildingUpgrade_2670x1200.png` (09-05 00:29, post-1391) |
| Armies / Muster | NO CAPTURE anywhere (graph `:206`, gap item 9). Not reviewable. |

### A2. Per-screen verdicts

**Launcher.** Names the next action: yes - "Choose a path" + four cards with a verb-phrase each. Reason to tap: weak - `Builders 0/2 . Training 0/2 . Research 0/2` says every line is idle but nothing says *so go start one*. Locked-Troops state: "Build a Barracks to unlock" with a lock glyph - words carry state; but the card is not a door to Build (A-3).

**Defense tab.** Next action: yes - `UPGRADE` per row with cost, `Ready`. Reason: none - `Arcane Spire - grid 5, 16 - L1 -> L2` never says what L2 does. "grid 5, 16" is developer coordinates. Empty state passes ("No defenses are ready to upgrade. Build your first tower or wall here." + `BUILD DEFENSE`).

**Buildings tab.** `Armorer -> T1 / Wood 1000, 670 gold` says nothing about what T1 unlocks.

**Troops tab.** Best of the set: `TRAIN 1 FOOTMAN` / `Train one: 45s . Ready`, `UPGRADE TO L4 / 6m 0s . Ready`, `TRAINING NOW Nothing training. Tap TRAIN to start.` -> after tap `Footman x1 [bar] 43s left`. Gap: nothing says *why* train (army N/10, raid door). WO-1389 claims a beat + coach-marks but no fresh Troops capture exists after e99d2f290. Unproven on a picture. `UPGRADE TO L4` does not say what L4 changes.

**Research tab.** WO-1390 proven on device. Readability defect still present in the device frame: `UPGRADE CATHE...` / `UPGRADE LUMBE...`. Unlocked state: `Ready - takes 13m 0s` / `1200 gold` / `RESEARCH` - time and cost but not the effect.

**Queue drawer.** Device frames show the WO-1393 defect (fixed per handover; no fresh drawer capture - not re-reported). Standing content issues in ALL frames: tiles `TRAIN / T / Footman / 20s`, `UPGRADE / B / Barracks:2:0 -> L4` - single-letter glyphs and an internal id shown to the player. `Permanent builder +1` with `BUY BUILDER`, unpriced.

**Upgrade page.** Pre-fix defects FIXED per handover; fresh capture shows "Short 1280 Wood, 800 Gold / Harvest more wood, or set an Echo to gather it." and a real portrait. Remaining: the dimming (meta 3); `Timber Wagon ... Coming soon - ta...` truncates; `1280 Wood - need 1280 ...` truncates. Reason to upgrade: YES - "Thrain's base kit awakens", "Mage spell power +5%, arcane tower damage +5%" - the only Manage-tree screen that says what the player GETS.

### A3. Findings ranked by player impact

**A-1 (highest). Every Manage list row prices the tap but never says what it buys.** Defense: "Arcane Spire - grid 5, 16 - L1 -> L2 / Iron 540". Buildings: "Armorer -> T1 / Wood 1000, 670 gold". Troops: "UPGRADE TO L4 / 6m 0s". Research: "Arcane Basics / 1200 gold / Ready - takes 13m 0s". Cost + wait and no benefit; the rational tap is BACK. The Upgrade PAGE has the benefit line but the list does not surface it.
*Fix shape:* one benefit line under the cost on every Manage row, from the same catalog string the upgrade page renders. The row VM reads the existing tier-benefit string; no new data.

**A-2. Manage never mentions the raid.** Nothing across `03`..`10` names an army size, a camp, or a garrison.
*Fix shape:* Troops header band gets a permanent `Army 3 / 10 - <next camp> fields 9` from the WO-1389 `PublishArmyStatus` producer; `TRAINING NOW` empty copy -> "Nothing training. 7 empty slots - tap TRAIN to fill the ranks."

**A-3. Locked Troops card is a wall, not a door.** "TROOPS - Build a Barracks to unlock" is not tappable. The Defense tab already has the pattern (`BUILD DEFENSE` -> `OpenDefenseBuilder`).
*Fix shape:* make the locked card tappable, label `BUILD A BARRACKS`, route through the same `Close + EnterBuildMode(...)` seam, armed on the Barracks entry.

**A-4. Developer tokens on player screens.** "grid 5, 16"; "Barracks:2:0 -> L4"; "Repair Gate:4:1"; single-letter tiles `T / B / R`, `M / A / M`.
*Fix shape:* queue tile shows the troop/structure portrait (the Troops rail already has the Footman portrait); `grid x, y` / `id:x:y` -> display name + compass side.

**A-5. `BUY BUILDER` is unpriced and unexplained on the drawer;** tapping leaves Manage for the store and the store's CLOSE lands on the HUD (device walk proved it: `11-research-upgrade-door.png`).
*Fix shape:* `BUY BUILDER - 511 SKR (~$9.99)` from the pack price; store CLOSE returns to the sending Manage tab (a `returnTo` on `StoreFocusRequest`). [CLI note: WO-1400's return door now covers deck-opened panels; the Manage->store return is the same mechanism with a different opener.]

**A-6. Research door labels truncate** (`UPGRADE CATHE...`).
*Fix shape:* button label `UPGRADE`; building name moves into the reason sentence: "Cathedral of Magic must reach Tier 1 first."

**A-7. Research unlocked rows do not say the effect.** *Fix shape:* one effect line from the perk description.

**A-8. Idle-line chips have no verb** (`Builders 0/2 . Training 0/2 . Research 0/2`).
*Fix shape:* `Builders idle - 0/2`, chip tap lands on that tab (`ActivateLauncherCard` exists).

### A4. Rulings
- **Benefit-line?** Default: YES, one line, from the catalog.
- **Coordinates?** Show `grid x, y`? Default: NO - display name + compass side.
- **Price-on-button?** `BUY BUILDER - 511 SKR`? Default: YES.
- **Return-to-Manage?** Store CLOSE returns to the sending Manage tab? Default: YES.

---

## B. JOURNEY subtree

### B1. Screens reviewed
| Screen | File |
|---|---|
| Journey deck | `JourneyWorkspace_2670x1200.png` (09-05 07:02) |
| Raids card (locked) | no named case (graph gap 5) |
| Raid Selection | `RaidSelection_2670x1200.png` (09-05 07:02) |
| Raid Deploy | `RaidDeploy_2670x1200.png` (09-05 07:02, post WO-1385/1389) |
| Victory / settle | no raid-victory capture; only `EndStateWaveClear_repairAll_2670x1200.png` |
| Quests (Rumor Board) | `RumorBoard_2670x1200.png` |
| Daily quests | `DailyQuestHud_2670x1200.png` |
| Realm Map | `RealmMap_2670x1200.png` (09-01; no release door) |
| Raids face states | `RaidsFaceStates_2670x1200.png` (08-31, STALE) |

### B2. Per-screen verdicts

**Journey deck.** Two cards: "QUESTS - Read active quests and realm r..." and "RAIDS - Choose a camp and deploy your...". Both subtitles truncate. Neither names a state. The WO-1389 `Army 3 / 10` subtitle is NOT visible in this fresh capture - fixture has no army, or not landed; unproven which.

**Raid Selection.** Four camps with names, tier words, defender counts - good. Colour-only state present: left edge bars green/yellow/red with no word; the tier words are tinted. Nothing says what a win PAYS; nothing compares to the player's army. Three identical pips per row - a mystery glyph. No lock state visible.

**Raid Deploy.** Compare line proven: "Garrison: 9 defenders - you field 0". Empty-army copy "No troops trained yet. Visit the Barracks." is a sentence, not a door; `ARMY READY?` is a question-shaped button; `BEGIN ASSAULT` is full-size and live with 0 troops. Truncations: "Est. ~2:30 | Troops 0 | Pow...", Echo quote cut. "Assault to recon" is jargon. No loot / reward line anywhere.

**Wave clear** is the model for settle: consequence named, one priced verb (`REPAIR ALL - 120 WOOD, 40 IRON`). No raid settle capture exists to check against.

**Rumor Board.** Passes. Nit: two cards "Standing Watch Over the Western Fields 1 / 2" with identical body and rewards read as a duplicate.

**Daily quests.** "No daily quests today / Fresh quests arrive with the new day." - dead end, no time-to-next.

### B3. Findings ranked

**B-1 (highest). Nothing in the raid chain says what a raid PAYS.** Selection rows carry walls + defenders only; Deploy has no spoils line. The wave-clear overlay proves the resource-row grammar exists.
*Fix shape:* Selection row: `Spoils: ~600 wood, ~250 iron`; Deploy SCOUT REPORT fourth line `Spoils if you win: ...`. Read the existing raid def reward fields.

**B-2. `BEGIN ASSAULT` is live at 0 troops and the empty-army sentence is not a door.**
*Fix shape:* fielded == 0 -> primary becomes `TRAIN TROOPS` routing to `Open(Manage, "Troops")`; `BEGIN ASSAULT` demoted to `Scout only`; `ARMY READY?` -> `EDIT ARMY`.

**B-3. Journey card subtitles truncate and carry no state.**
*Fix shape:* Raids subtitle = `Army 3 / 10 - The Forsaken Camp: 9 defenders`; Quests = `2 active - 1 ready to claim`.

**B-4. Colour-only difficulty bars; the three pips are a label-less glyph.**
*Fix shape:* drop the pips (tier words already exist), use the space for the spoils line; keep the bar but add `LOCKED - needs Army 6` when the camp is above the army.

**B-5. Daily Quests dead end.** *Fix shape:* `No daily quests today - new set in 6h 12m` + a `RUMOR BOARD` button.

**B-6. Raid Deploy jargon and truncation.** *Fix shape:* `Recon: 2:30 - Troops 0 - Power ?` on one line; Echo quote authored to two lines.

**B-7. Rumor Board duplicate-looking cards.** *Fix shape:* distinct titles or a `Part 1 of 2` chip.

### B4. Rulings
- **Spoils-shown?** Default: YES (a range, never exact).
- **Zero-army-assault?** Default: NO - primary becomes TRAIN TROOPS.
- **Pips?** Default: NO - replace with the spoils line.
- **Daily-countdown?** Default: YES.

---

## C. BUILD subtree

### C1. Screens reviewed
Collections (`BuildCollections_2670x1200.png` 07:02); palette dock (`BuildPaletteDock_open_2670x1200.png` 08-27, STALE); ghost + chips (`BuildGhostChips_blocked_2670x1200.png` 07:02); orient (`BuildPreview_2670x1200.png` 09-01); Build HUD intent bar - NO CAPTURE (graph gap 1).

### C2. Per-screen verdicts

**Collections.** Eight cards with a two-word verb-phrase each; "First build: select a category." Passes *what*. Fails *why now*: no card says what is affordable, unlocked, or recommended; "Defenses" and "Protection" are near-synonyms; the eighth card "Upgrade Defenses" is a door OUT of Build to Manage dressed as a category, in a smaller font.

**Palette dock (stale).** Item cards "Lumber Mill - Harvests Wood... - NEED [160] [80] [120]"; `NEED` wraps `NEE / D` in every card; costs are red numbers - colour-only affordability. Re-capture before ticketing.

**Ghost chips.** "Arcane Spire - 88 wood, 88 iron, 187 crystals" + "Not enough Wood" - words carry the block. Bottom-right: three unlabelled glyph buttons (check / rotate / X) against the owner's names-not-icons ruling.

**Orient modal.** Clear. Preview is a grey untextured model.

### C3. Findings ranked

**C-1 (highest). Build never says what you can afford or should build next.**
*Fix shape:* Collections subtitle count word: `Gathering - 2 you can build now` / `Defenses - nothing affordable yet` from the palette's NEED check; palette cost pills say `SHORT 72 wood` instead of red digits.

**C-2. Unlabelled icon buttons in the ghost stage.** *Fix shape:* `PLACE / ROTATE / CANCEL` labels.

**C-3. "Upgrade Defenses" is a Manage door disguised as a build category.** *Fix shape:* rename `MANAGE DEFENSES ->` or drop it.

**C-4. "Defenses" vs "Protection" indistinguishable.** *Fix shape:* `Towers` and `Walls & Gates`.

**C-5. `NEE / D` wrap and tiny palette text (stale).** Re-capture first.

### C4. Rulings
- **Affordable-count?** Default: YES. **Rename?** Default: YES. **Keep-8th-card?** Default: NO.

---

## D. HERO subtree

### D1. Screens reviewed
Hero deck (`HeroWorkspace_2670x1200.png` 07:02, Wardrobe present); Equipment (`HeroEquipment_2670x1200.png` 08-31, STALE); Bag (`Bag_2670x1200.png` 09-01); Skill tree (`HeroSkillTree_2670x1200.png` 07:02, post WO-1401); Loadout (`HeroLoadout_2670x1200.png` 00:29).

### D2. Per-screen verdicts

**Hero deck.** Five cards with one-line meanings. Fails *why*: no card carries a number (unspent points, empty sockets). "Complete its requirement first" names no requirement. [CLI note: the 07:10 capture after the 1397 fix shows Wardrobe AVAILABLE ("Looks for your hero, Echo, and town") - the lock was the fixture, now registered.]

**Equipment (stale).** Best-structured Hero screen; `COMPARE TO EQUIPPED / DAMAGE +0% SAME` in words. Nit: `MAIN HAND` label overlaps its bar.

**Bag.** Title says "INVENTORY" while the deck card says "BAG". Portrait reads "GROM" under "Thrain the Wise / MAGE LV 1" (fixture or bug, unproven). Two helper sentences saying different things. Selected rail tab is gold fill only.

**Skill tree.** "TALENT TREE", `WISDOM 0`, nodes `0/1` + lock, quick-swap `1 EMPTY / 2 EMPTY / 3 EMPTY`. Two lower nodes cut off at the bottom. `WISDOM 0` is a mystery number. Deck said "SKILLS", chrome says "TALENT TREE".

**Loadout.** "Hot-Swap Skills"; empty state "No unlocked skills yet. Unlock SKILL nodes in the tree." - a sentence, not a door. The skill tree ALSO has slots 1-3 - two screens assign the same three sockets.

### D3. Findings ranked

**D-1 (highest). Four names for two screens.** BAG/INVENTORY; SKILLS/TALENT TREE/"SKILL nodes"; LOADOUT/Hot-Swap Skills.
*Fix shape:* one word per screen: `BAG`, `SKILLS`, `LOADOUT`, deck card == chrome title == every sentence (the WO-1398 one-source pattern).

**D-2. `WISDOM 0` is a mystery number; the tree gives no reason to return.**
*Fix shape:* `Next point at Level 2` (or how it is earned) under the chip; locked nodes say `1 WISDOM`.

**D-3. Loadout empty state is a sentence, not a door, and duplicates the tree's sockets.**
*Fix shape:* `OPEN SKILLS` button on the empty state; one owner of socket assignment (ruling).

**D-4. Wardrobe lock names no requirement.** [CLI: resolved by the 07:10 fixture fix - the card is available; the LockReason gap remains for a genuinely unregistered shop.]

**D-5. Bag helper text contradicts itself; selected tab colour-only.** *Fix shape:* one helper sentence; selected tab gets an underline or `>` marker.

**D-6. Deck cards carry no live numbers.** *Fix shape:* `SKILLS - 1 point to spend`, `LOADOUT - 0 / 3 sockets`, `EQUIPMENT - 1 better item in bag`.

### D4. Rulings
- **Names?** `BAG / SKILLS / LOADOUT`? Default: YES.
- **Sockets-owner?** Default: LOADOUT; the tree only unlocks.
- **Wisdom-from-raids?** Unknown - if NO, say what earns it on the chip.

---

## E. HUD + Talk + Night Market + welcome-back + pause/settings

### E1. Screens reviewed
Welcome-back (`00-title-or-hub.png` device; `WelcomeBack_2670x1200.png` 00:26); harvest result (`01-harvest-result-modal.png` device, pre-1392); HUD town (`02`, `12`, `16` device; `AdaptiveHud*` 05:14); Night Market (`11-research-upgrade-door.png` device with wallet; `NightMarket_2670x1200.png` 07:02 no wallet); Realm deck (`RealmWorkspace` 07:02); dialogue (`DialogueOptions_2opt` 07:02, `DialogueCompact_Aldwin` 09-01); Echoes (`EchoRoster` 09-01, `EchoCard` 07:02); Pause / Settings / Help (07:02 / 06:44 / 07:02); Defense report / Game guide / Monthly ledger (00:29) / Daily chest (09-01).

### E2. Per-screen verdicts (abridged where the finding below carries it)

**Welcome-back** passes strongly; words carry every state; return hook present. Missing: any door after COLLECT.

**HUD town.** Bar `BUILD / TALK / HERO / JOURNEY / MANAGE` (Talk conditional). Heart plate last line "Heartfire is full" clipped by the plate bottom in every DEVICE frame (`02`, `12`, `16`); fits in headless. `[*] [*] [*]` ASCII pips. `Next wave in 855s` raw seconds. Nothing on the HUD says how to become raid-capable.

**HUD combat.** `SKILL I / II / III` identical sword icons.

**Gear dock (05:14 frame, predates 1398).** `..ERBOARD` clipped, `NIGHT MARK...` truncated - re-capture, not a ticket.

**Night Market (no wallet).** Six `Price unavailable` + three `UNAVAILABLE` with `CONNECT WALLET` at the bottom and no sentence linking them. "What it holds" lists resource names without quantities; "the Hearth Spark" is a pack the player has not seen. Badges `BEST ST...` / `FIRST BUY` truncate/overlap the art; a `BS` fragment. The "never required to spend" seal is ~14 px, unreadable. `MONTHLY LEDGER` row cut in half by the `CLOSE THE GAP` header in BOTH the device and the fresh frame. With a wallet (device frame) prices work: `1023 SKR ~ $19.99`.

**Realm deck.** "THE NIGHT MARKET - Browse clearly priced realm offers" (1398 proven); "MONTHLY LEDGER - Review non-expiring monthly pr..." truncates.

**Dialogue.** `DialogueOptions_2opt`: options `Gather resources / Repair structures` - the second is the RETIRED repair assignment chip (CLAUDE.md s7) if it is data and not fixture text. `DialogueCompact_Aldwin`: "I can farm. I can mend. Put me to work, Keeper."

**Echo roster (09-01).** "Each Echo speeds ALL harvest -- now x1.5 to every node's yield." contradicts canon (additive, never a multiplier). "Idle -- tap to assign" in the same green as "+5% (best)". `EchoCard` passes; nit "Favors: Stone" but "STONE ... NEEDS: FARM".

**Pause / Settings / Help.** Pause has RESUME and a redundant CLOSE. Settings: a Music slider AND a Music Off toggle; `Master 0%` + `Mute all audio On` reads as broken audio (fixture). Help: `RESET HERO & PET` (retired word; destructive, no visible confirm), `DEV TOOLS` in a player menu.

**Defense report** empty-state text light grey on beige - unreadable. **Game guide** "Walk with WASD" on a phone. **Daily chest** `AD NOT READY` with no time.

### E3. Findings ranked

**E-1 (highest). The HUD never tells a non-raid-capable player how to become one.**
*Fix shape:* Heart plate second line when `!RaidCapable`: `Raids unlock at a Barracks - Build > Realm` / `Train 3 troops to unlock Raids`, replacing the static "Prepare the realm for the next wave."

**E-2. Welcome-back COLLECT lands on a HUD with no next door.**
*Fix shape:* one data-driven line above COLLECT (`Heartfire is full - a wave is ready` / `Army 3/10 ready - The Forsaken Camp awaits`) and a second small door (`START WAVE` / `RAID`) when true.

**E-3. Night Market for a no-wallet player is nine "unavailable"s with no reason.**
*Fix shape:* ONE banner under the wordmark `Connect a wallet to see prices`; show the USD anchor on cards without a wallet (the device frame proves the figure exists).

**E-4. Night Market right-rail overlaps** (MONTHLY LEDGER under CLOSE THE GAP; badges over art; `BS` fragment) - persistent across builds.
*Fix shape:* ACTIONS gets two full rows before CLOSE THE GAP; badges to a single word (`BEST` / `FIRST`) top-left.

**E-5. Heart plate "Heartfire is full" clipped on device + ASCII pips + raw seconds.** Device-only; re-verify on 356386 before ticketing.
*Fix shape:* plate budgets four lines at device DPI; real pips; `Next wave in 14m 15s`.

**E-6. Retired words in player copy:** `RESET HERO & PET`; roster "x1.5 to every node's yield"; dialogue "Repair structures" (fixture vs data unproven).
*Fix shape:* `RESET HERO & ECHOES`; roster subtitle from `EchoBonusCalculator` (`+7% total`); grep `dialogues.json`.

**E-7. Defense Report empty-state unreadable.** *Fix shape:* same dark card as the left panel.

**E-8. Combat bar `SKILL I / II / III` identical icons.** *Fix shape:* label = equipped skill's short name; `EMPTY` when unassigned (a nudge to Loadout).

**E-9. Pause CLOSE redundant; Settings Music slider + toggle; Help exposes DEV TOOLS and an unconfirmed reset.**

**E-10. Daily chest `AD NOT READY` has no time.** *Fix shape:* `AD READY IN 2m` or hide until ready.

### E4. Rulings
- **Raid-hint-on-HUD?** Default: YES. **Post-collect-door?** Default: YES. **USD-without-wallet?** Default: YES.
- **Dev-tools-in-Help?** Default: NO. **Seconds-or-minutes?** `14m 15s`? Default: YES.

---

## Cross-subtree: the raid loop as one path

Journey card (no army number) -> Raid Selection (no spoils, no lock word) -> Raid Deploy (compare line PROVEN; no spoils; empty-army is a sentence not a door; BEGIN ASSAULT live at 0) -> battle -> settle (NO CAPTURE) -> Manage Troops (TRAIN/UPGRADE proven; no army number) -> Heartfire (HUD plate only) -> Journey. Of the seven arrows the sprint doc requires as visible doors: two proven doors, one sentence, four with no on-screen arrow in any capture. WO-1389's post-raid beat may cover settle->Troops but no picture exists of it.

## Unproven / needs a CLI check before ticketing
1. `BuildingUpgrade_2670x1200.png` whole-card dimming.
2. INDEX rows 15/16 mislabel - fix the INDEX, re-capture the Journey deck on device.
3. WO-1389 `Army 3 / 10` subtitle absent from `JourneyWorkspace_2670x1200.png`.
4. `Bag_2670x1200.png` portrait "GROM" under "Thrain the Wise".
5. `DialogueOptions_2opt` "Repair structures" (fixture text vs `dialogues.json`).
6. `EchoCard` "STONE ... NEEDS: FARM".
7. Device re-capture of the Heart plate clip + gear-dock truncation on 356386.
