# UI_REVIEW reference mockups — the "compare against" side (UI seat, 2026-07-13)

Owner ask: "show exact mockups so it can compare to something." The 30+ captured screens in
this review collapse into SIX composition families; each family has an exact reference SVG
here (open in any browser, same folder as the captures). Per-screen fine detail comes from
two additional authorities: the OWNED pack's store-gallery layouts
(Assets/Blink/Art/UI/Obsidian_UI · store id 206302, owner-verified) and the approved in-chat
wireframes (WO-713 inventory + gear, WO-676 skill tree, WO-683 jeweler, WO-675 upgrade panel,
WO-709 workforce HUD). WO-714 is the program that enforces all of it.

## Screen → reference mapping (CLI: pair each capture with its family REF + pack gallery shot)

| # capture folder | Family REF | Extra authority |
|---|---|---|
| 01 HeroTalents · 02 HeroSkillTree · 06 Pet Skill Tree | **D node-graph** | pack TALENT TREE gallery · WO-676 |
| 09 Hero Loadout | **D** (socket grammar) | pack SOCKETING gallery |
| 03 Crafting/Workshop · 10 Alchemy · 11 Jeweler | **A master-detail** | pack CRAFTING gallery · WO-683/693 wireframe |
| 08 Rumor Board · 13 Game Guide · 24 Leaderboard | **A** | pack QUESTS gallery |
| 04 Building Upgrade | tier-band variant of **A/B** | WO-675 approved mockup + WO-680 A1-A4 |
| 05 Cosmetic Shop · 07 Party Shop · 27 Pack Store | **B grid+shop** | pack MERCHANT/INVENTORY gallery |
| (Inventory) | **B** | WO-713 APPROVED wireframe (authority) |
| 12 Equipment/paper-doll | **C paperdoll** | pack CHARACTER gallery + WO-713 §D |
| 14 Hero Select | **C** (stage variant) | SEL-1 (skills from abilities.json) |
| 22 Tower Manager | **C** (structure-as-doll) | WO-696 repair-context rules |
| 15 Dialogue view | pack NPC card | FrameCore rebuild (landed) — verify only |
| 16 Build Menu | **B** palette variant | WO-673 taxonomy (Town/Defenses/Walls) + WO-706 portraits |
| 23 End State · wave report | **E row-list** | pack LOOT gallery · REP-1 fix lane |
| 25 Clan Chat · 19 Jukebox | **E** rows | |
| 17 Help · 18 Bug Report · 20 Settings · 21 Pause | **F small modal** | W8: rebuild code-built (UXML retirement) |
| 26 Echo Workforce | WO-709 approved HUD mockup | REF F for the card |
| 28 Raid Selection · 29 Raid Deploy · 30 Troop Training | **A/B** hybrids | pack MERCHANT rows |

## The 10 conformance checks every pair is judged on (from WO-714 Phase 1)
frame-from-table · content-in-zones-only · one Close in the band · kit tabs (no truncation,
phone aspect) · CurrencyChip-only money (compact, never ellipsized) · detail-card grammar
(spaced names, BESTOWS/effect line, disabled-states-name-why) · slot plates + rarity rims +
dim empties · toasts for transients · sprite-first fallback exercised · no dev controls, no
raw ids, nothing color-only.

*UI-seat authored. CLI: wire these into INDEX.html as the left column of each pair when
convenient — the SVGs are browser-native.*
