# UI Review - MERGED verdict (CLI, 2026-09-05 07:25) - "does this screen give the player a reason to tap the next one?"

Inputs: `REVIEW_A_independent.md` (generalist) and `REVIEW_B_independent.md` (retention / CoC lens), produced by two
read-only agents that did not see each other. Merge rule: a finding enters this list only if (a) BOTH reviewers found it
independently, or (b) one found it and the CLI re-read the PNG/code and confirms. Verification level per row:
**SEEN** = the CLI opened the PNG this session and read the words; **CODE** = confirmed at source; **AGREED** = both
reviewers, CLI did not open the frame; **INFERRED** = one reviewer, unverified - listed as a check, not a ticket.

Frames: device PNGs are build 355952 (bedtime). Headless `Builds/ui-capture/*_2670x1200.png` timestamps 05:14-07:10
post-date the overnight fixes. Fixed-tonight items are excluded unless a fresh frame still shows them.

## 0. Findings about the review inputs (fix these first, no owner word needed)
- **INDEX rows 15/16 mislabelled** (both reviewers): `15-hud-before-journey.png` is the Cathedral upgrade page; `16-journey-deck.png` is the HUD. -> INDEX correction + re-walk on the device when it is back.
- **BuildingUpgrade card looks dimmed** (both; CLI SEEN at 00:29 and 00:24 captures): the NextUpgradeCard body reads dimmer than the pills/CLOSE. CODE: the only `dim = 0.6f` is the Skills-tab locked ROWS (`BuildingUpgradePanelMvvm.cs:1858-1868`), not the upgrade card; the card `Fill` is a translucent tint. Whether the tint is the fixture's `MissingResources` state or the shipped look is UNPROVEN - a device look on the Cathedral page (build 356411) settles it in one glance. Listed as a check.
- **Capture gaps** (both): raid victory/settle, Armies/Muster, Manage post-action rows, Troops tab after e99d2f290 (WO-1389 items 3 and 6 unproven on any frame), Journey deck on device, stale Bag/Equipment/Palette/Queue frames (08-27..09-01), Daily Chest opener.

## 1. Ranked findings -> tickets (one per failing screen)

| # | Screen | Finding (words on screen) | Verify | Fix shape (one mechanism) | Ticket |
|---|---|---|---|---|---|
| 1 | Raid Selection | Rows carry `Wood walls . 9 defenders` + difficulty; **nothing says what a raid PAYS**. Three identical gold pips per row carry nothing; difficulty bars are colour-first (words present). | SEEN | Right column line 2: `Spoils: ~600 wood, ~250 iron` from the camp's authored loot; pips -> hidden until stars vary; `LOCKED - needs Army N` word when above the army. | WO-1402 |
| 2 | Raid Deploy | `Army: -` / `No troops trained yet. Visit the Barracks.` is a sentence, not a door; `ARMY READY?` is a question on a button; **`BEGIN ASSAULT` is full-size and live at 0 troops**; no spoils line; `Est. ~2:30 \| Troops 0 \| Pow...` and the Echo quote truncate; "Assault to recon" is jargon. | SEEN | fielded==0 -> primary reads `TRAIN TROOPS` and opens Manage on the Troops tab (`PanelRouter.Open(Manage, "Troops")` exists); `ARMY READY?` -> `EDIT ARMY`; SCOUT REPORT line 4 `Spoils if you win: ...`; header stats one per line. | WO-1403 |
| 3 | Journey deck | `Read active quests and realm r...` / `Choose a camp and deploy your...` - subtitles truncate and carry no state; WO-1389's `Army 3 / 10` subtitle NOT on the 07:02 frame (fixture has no army, or not landed - CHECK). | SEEN | Subtitles become state: `Army 3 / 10 . 2 camps open`, `2 active . 1 ready to claim`; drop the verb-phrase. | WO-1404 |
| 4 | Manage rows (Defense / Buildings / Research / Troops card) | Every row prices the tap (`Iron 540`, `Wood 1000, 670 gold`, `1200 gold / 13m 0s`, `6m 0s`) and **never says what it buys**; `grid 5, 16` is a developer coordinate. The upgrade PAGE has the benefit line ("Mage spell power +5%") - the list does not surface it. | SEEN (device 04/05/06/07) | One benefit line per row from the same catalog string the upgrade page renders; `grid x, y` -> display name + compass side. | WO-1405 |
| 5 | Manage launcher + Troops header | Chips `Builders 0/2 . Training 0/2 . Research 0/2` say nothing about IDLE; Troops card `Available` has no referent and the army total is absent; locked Troops card "Build a Barracks to unlock" is a wall, not a door. | SEEN (device 03/07; headless ManageWorkspace) | Chip `Builders idle - 2 free` (tap -> that tab); Troops header `Army 3 / 10 - <next camp> fields 9` from `PublishArmyStatus`; locked card tappable `BUILD A BARRACKS` via the Defense tab's `EnterBuildMode` seam. | WO-1406 |
| 6 | HUD (town) | Nothing tells a non-raid-capable player how to become one; Heart plate line "Prepare the realm for the next wave." is static; `Next wave in 855s` raw seconds; `[*] [*] [*]` ASCII pips; no idle-builders surface; "Heartfire is full" clipped on every DEVICE frame (fits headless - device re-check on 356411). | SEEN (device 02/12/16; headless AdaptiveHudPeaceful) | Heart plate line 2 when `!RaidCapable`: `Raids unlock at a Barracks - Build > Realm` / `Train 3 troops to unlock Raids`; `14m 15s`; Builders chip visible when idle (`Builders idle 2`). | WO-1407 |
| 7 | Welcome-back | Reports resources (and, fresh, the Echo mending lines) and then COLLECT lands on a HUD with **no next door**: nothing says a build/troop finished, the town was attacked, Heartfire is full, or the army is ready. | SEEN (00:26 capture) | Two optional rows `FINISHED WHILE AWAY Footman x1, Arcane Spire L2` (door Manage) / `ATTACKED 1x - north gate breached` (door Defense Report); one data-driven line above COLLECT + a second small door (`START WAVE` / `RAID`) when true. | WO-1408 |
| 8 | Night Market (no wallet) | Six `Price unavailable` + three `UNAVAILABLE` with `CONNECT WALLET` and no sentence linking them; `MONTHLY LEDGER` row clipped under the `CLOSE THE GAP` header (device AND fresh frame); badges `BEST ST...` / `FIRST BUY` overlap the art with a `BS` fragment; pack contents truncate mid-list (`7,000 stone, 2,`); "2.7x the Hearth Spark's wood" compares to a pack not on screen; the "never required to spend" seal is unreadable at ~14 px. With a wallet (device 11) prices render (`1023 SKR ~ $19.99`). | SEEN | ONE banner under the wordmark `Connect a wallet to buy - prices shown in USD` + show the `~ $` anchor per card without a wallet; ACTIONS gets two full rows before CLOSE THE GAP; badges to one word top-left; contents list capped with `+N more`. | WO-1409 |
| 9 | Hero screens | **Four names for two screens**: deck `SKILLS` -> chrome `TALENT TREE` -> cross-buttons `TALENTS` -> Loadout "Unlock SKILL nodes" / title `Hot-Swap Skills`; deck `BAG` -> chrome `INVENTORY`. `WISDOM 0` is a mystery number (no source, no next-point). Loadout empty state is a sentence, not a door; the skill tree ALSO owns sockets 1-3. | AGREED (+ CLI SEEN HeroWorkspace; Skill tree rail proven tonight) | One noun per screen == deck card == chrome == every sentence (the WO-1398 one-source pattern); `WISDOM 0 - next point at Level 2`; `OPEN SKILLS` button on the Loadout empty state; one socket owner (RULING). | WO-1410 |
| 10 | Build collections + ghost + confirm | No card says what is affordable now; `Upgrade Defenses` is a Manage door dressed as a category (smaller title); `Defenses` vs `Protection` overlap; ghost stage has three icon-only buttons (check / rotate / X) against the names-not-icons ruling; confirm shows orientation only - no cost, no time; "First build: select a category." persists while a ghost is armed. | AGREED (both quote the same words) | Collections subtitle `Gathering - 2 you can build now`; `PLACE / ROTATE / CANCEL` labels; confirm line `88 wood, 88 iron, 187 crystals . 45s . Builder free`; banner takes the phase; rename/move per rulings. | WO-1411 |
| 11 | Manage -> store -> CLOSE | Store CLOSE lands on the HUD, ejecting the player from Manage (device 11 -> 12); `BUY BUILDER` on the drawer is unpriced and shows while a slot is free. | SEEN (device) | WO-1400's return door with the Manage opener recorded (same mechanism, different opener); `BUY BUILDER - 511 SKR (~$9.99)`, shown only when all slots are busy (RULING). | WO-1412 |
| 12 | Copy hygiene (several screens) | `RESET HERO & PET` (retired word; destructive, no confirm) + `DEV TOOLS` on a player-facing Help; Echo roster "x1.5 to every node's yield" (canon: additive); dialogue option "Repair structures" (retired assignment - fixture vs `dialogues.json` UNPROVEN); Defense Report empty text light grey on beige; `SKILL I / II / III` identical icons in combat; Pause has RESUME + CLOSE; Settings has a Music slider AND a Music toggle; Daily chest `AD NOT READY` with no time; Rumor Board "Standing Watch Over the Western Fields 1 / 2" identical cards. | AGREED (+ CLI SEEN Settings) | One ticket, one line each; grep `dialogues.json` for "Repair structures" first. | WO-1413 |

Excluded (fixed tonight, fresh frame agrees): WO-1391 preview/shortfall, WO-1392 harvest loss, WO-1393 tap-through +
drawer, WO-1398/1399 label source + Settings->Help, WO-1401 rail geometry, WO-1397 Wardrobe lock (fixture; 07:10 frame
shows it available).

## 2. Rulings for the owner (one word each; the default is what the tickets are written to)
1. **Spoils-shown?** Show expected loot on raid rows and the deploy scout report? Default YES (a range, never exact).
2. **Zero-army-assault?** Allow BEGIN ASSAULT with 0 troops? Default NO - the primary becomes TRAIN TROOPS.
3. **Heartfire-does?** One clause for what a charge buys, for the HUD plate (`Heartfire 3/3 - spend one to raid`?). No default - needs your sentence.
4. **Benefit-line?** Manage rows show one benefit line from the catalog? Default YES.
5. **Gridref?** Show `grid x, y` anywhere on player screens? Default NO.
6. **Return-to-Manage?** Store CLOSE returns to the sending Manage tab? Default YES.
7. **Upsell-when?** `BUY BUILDER` visible always or only when all slots are busy? Default busy-only.
8. **USD-without-wallet?** Show `~ $` anchors before a wallet is connected? Default YES.
9. **Card-size?** Shrink the Night Market HUD card to bar-face height and give the space to a RAID call? Default: NO CHANGE tonight - it contradicts your 23:05 "shining gem" ruling; raised because both reviewers flagged it.
10. **Names?** `BAG / SKILLS / LOADOUT` as the three Hero words (or `TALENTS`)? Default SKILLS (matches the deck).
11. **Sockets-owner?** Skill tree or Loadout assigns sockets 1-3? Default LOADOUT.
12. **Wisdom-source?** What earns Wisdom, in one clause? No default.
13. **Rename?** `Protection` -> `Walls & Gates`, `Defenses` -> `Towers`; keep `Upgrade Defenses` in Build? Default rename YES, keep NO (footer link).
14. **Dev-tools-in-Help?** Keep DEV TOOLS visible in release Help? Default NO.
15. **Pet?** `RESET HERO & PET` -> `RESET HERO & ECHOES`? Default YES.
16. Plus the three carried from tonight's lanes: WO-1395 (two storefronts vs Play skin), WO-1397 (Wardrobe card vs Night Market tab), WO-1388 (pack name / basket / badge), WO-1389 Q1-Q3.

## 3. CLI checks queued (not tickets until proven)
- BuildingUpgrade card tint: device glance on 356411.
- WO-1389 `Army 3 / 10` Journey subtitle + Troops next-unlock line: capture Troops/Journey on a fixture WITH an army, or the device.
- `dialogues.json` "Repair structures" option; Echo roster "x1.5" string source; `Bag` portrait "GROM" vs "Thrain"; `EchoCard` "STONE ... NEEDS: FARM" mapping.
- Five bar faces visible on device (`BUILD / TALK / HERO / JOURNEY / MANAGE`) vs `MaxVisibleFaces` - per CLAUDE.md s7 Talk is conditional and the code + suites are the authority; read the constant before anyone calls it a defect.
- Heart plate "Heartfire is full" clip and gear-dock `NIGHT MARK...` truncation: device re-capture on 356411.
