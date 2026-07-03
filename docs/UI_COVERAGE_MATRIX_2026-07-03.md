# UI 100% Coverage Matrix — 2026-07-03

> Owner directive (2026-07-03 morning): **"i want all panels signs dialogue storefront death state
> crafting build menu help screen everything styled — not mostly or a lot — 100% is a definitive
> pass value."**
>
> This matrix IS the definition of 100%. One row per player-facing surface (enumerated from the
> code registries by a read-only agent, cross-checked against
> `docs/UI_BLINK_CONFORMANCE_AUDIT_2026-07-02.md`). A surface PASSES when all four cells are green:
>
> | Cell | Pass bar |
> |---|---|
> | **STYLED** | Built from the Obsidian factory (`BuildObsidianPanel` / kit widgets), Blink frames, EnsureFont TMP — no legacy Text, no IMGUI, no UIDocument/UXML at runtime |
> | **CLOSE** | The ONE shared Obsidian Close (no per-panel X — canon `obsidian-panel-chrome`) or a designed auto-dismiss |
> | **TESTED** | Machine-asserted by the fleet (popup oracle / posture lines / phase assert) |
> | **EYES** | Screenshot captured windowed AND reviewed against the Blink source |
>
> DEAD/retired rows are marked ✂ — they pass by DELETION, not styling. Dev-gated rows pass by
> release-strip proof. **The matrix ends at all-green or it isn't done.**
>
> Status source: audit 2026-07-02 headline = 3 CONFORMANT · 26 PARTIAL · 34 LEGACY · 3 MISSING.
> Since then: EndStateView shipped (WO-B), HUD kit shipped (posture HUD = the new baseline),
> popup close oracle green (TESTED column for the 13 router panels).

Legend: ✅ pass · 🟡 partial · ❌ fail · ✂ delete-candidate (pass = deleted) · 🔒 dev-gated · — n/a

## A. PanelRouter panels (13)

| # | Surface | STYLED | CLOSE | TESTED | EYES |
|---|---|---|---|---|---|
| 1 | Hero Talents (HeroSkillTreePanelMvvm→HeroTalents) | 🟡 | ✅ | ✅ | 🟡 shot exists, unreviewed |
| 2 | Crafting / Workshop (VillageCraftingPanel) | ❌ UIDocument legacy | ❌ per-panel X (named CloseButton 07-03) | ✅ | 🟡 |
| 3 | Building Upgrade (BuildingUpgradePanelMvvm) | 🟡 WO-A finish | ✅ | ✅ | 🟡 |
| 4 | Cosmetic Shop (CosmeticShopPanel) | ❌ UIDocument legacy | 🟡 idempotent close fixed 07-03 | ✅ | 🟡 |
| 5 | Pet Skill Tree (PetSkillTreePanel) | ❌ UIDocument legacy | ❌ "Close (P)" | ✅ | 🟡 |
| 6 | Party Shop (PartyShopPanelMvvm) | 🟡 empty medallion, yellow X seen | ❌ X | ✅ | ✅ reviewed 07-03 (gaps listed) |
| 7 | Rumor Board (RumorBoardPanel) | 🟡 | ✅ | ✅ | 🟡 |
| 8 | Hero Skill Tree (MVVM) | ✅ audit-conformant | ✅ | ✅ | 🟡 |
| 9 | Hero Loadout | 🟡 | ✅ | ✅ | 🟡 |
| 10 | Consumable Crafting (Alchemy) | 🟡 | ✅ | ✅ | 🟡 |
| 11 | Jeweler Crafting | 🟡 | ✅ | ✅ | 🟡 |
| 12 | Equipment / Character paper-doll | 🟡 WO-582 reference | ✅ | ✅ | 🟡 |
| 13 | Game Guide | 🟡 | ✅ | ✅ | 🟡 |

## B. Front-end screens (8)

| # | Surface | STYLED | CLOSE | TESTED | EYES |
|---|---|---|---|---|---|
| 14 | Studio bumper | — video | auto | ❌ | ❌ |
| 15 | Title screen (TitleController, UIDocument) | ❌ WO-C | — | ❌ | ❌ |
| 16 | Story intro (UIDocument) | ❌ WO-C | skippable | ❌ | ❌ |
| 17 | Intro sequence player | 🟡 | auto/skip | ❌ | ❌ |
| 18 | Hero Select (UIDocument; Blink-carousel redesign WO-559) | ❌ WO-C | confirm | ❌ | ❌ |
| 19 | Pet Select | ✂ flag-retired | — | — | — |
| 20 | Onboarding flow (6-beat coach marks) | ❌ | ✅ Skip | ❌ | ❌ |
| 21 | Village load overlay | ❌ legacy Text | auto | ❌ | ❌ |

## C. HUD postures (7 — the kit, shipped 07-03)

| # | Posture | STYLED | TESTED | EYES |
|---|---|---|---|---|
| 22 | calm(town) | 🟡 move cluster still flat arrows | ✅ posture line in player | ✅ reviewed (hud_calm_town_live.png) |
| 23 | build (kit stands down) | 🟡 depends on build-UI rows 35–41 | ✅ | ❌ |
| 24 | calm(explore) | 🟡 | ✅ | ❌ |
| 25 | hostile(prebattle) | 🟡 | ✅ transitions captured | ❌ |
| 26 | hostile(activebattle) | 🟡 | ❌ no fleet phase drives it visually | ❌ |
| 27 | hostile(postbattle) (EndState owns) | see #29 | ❌ | ❌ |
| 28 | modal (kit stands down) | ✅ by design | ✅ | ✅ (panel shots) |

## D. Non-router overlays (21 live)

| # | Surface | STYLED | CLOSE | TESTED | EYES |
|---|---|---|---|---|---|
| 29 | EndStateView (victory/defeat/death/results — WO-B SHIPPED) | 🟡 verify | ✅ single footer | ❌ | ❌ |
| 30 | GameOverScreen (hub death) | ❌ manual hit-test, no EventSystem | ❌ | ❌ | ❌ |
| 31 | GameOverUI | ✂ orphan | — | — | — |
| 32 | BattleArenaHud victory/defeat + flee strip | 🟡 split TMP/legacy | 🟡 | ❌ | ❌ |
| 33 | Arena Panel | 🟡 | ✅ | ❌ | ❌ |
| 34 | Dialogue view (MVVM reference impl) | ✅ | ✅ | ✅ card oracle | 🟡 |
| 35 | BuildMenu | ❌ UIDocument | ❌ per-panel | ❌ | ❌ |
| 36 | BuildPaletteUI | ❌ | ❌ "Done" | ❌ | ❌ |
| 37 | BuildSelectionUI | ❌ | ❌ Cancel | ❌ | ❌ |
| 38 | BuildStructureInfoPanel | ✂ disabled | — | — | — |
| 39 | BuildPreviewModal | ✂ never instantiated | — | — | — |
| 40 | TowerManagerPanel | ❌ | ❌ | ❌ | ❌ |
| 41 | LevelUpSkillPopup | 🟡 | ✅ | ❌ | ❌ |
| 42 | UiSpotlight (tutorial) | 🟡 | auto | ❌ | ❌ |
| 43 | ObjectiveBannerUi (tutorial) | 🟡 | auto | ❌ | ❌ |
| 44 | HelpMenu | ❌ legacy Text | ❌ per-panel | ❌ | ❌ |
| 45 | BugReportView | 🟡 | ✅ | ❌ | ❌ |
| 46 | MusicSelectionPanel (Jukebox) | ❌ | ❌ per-panel | ❌ | ❌ |
| 47 | Settings + Pause (UXML — empty-in-build risk!) | ❌ | ❌ "Back" | ❌ | ❌ |
| 48 | GearGrantToast | ✅ kit ToastCard | auto | ❌ | ❌ |
| 49 | BuildFeedbackToast | ✅ kit ToastCard | auto | ❌ | ❌ |

## E. Social / store / raid / misc (17 live)

| # | Surface | STYLED | CLOSE | TESTED | EYES |
|---|---|---|---|---|---|
| 50 | LeaderboardPanel | ❌ X | ❌ | ❌ | ❌ |
| 51 | ClanChatPanel | ❌ | ❌ **NO CLOSE AT ALL — defect** | ❌ | ❌ |
| 53 | PlayerProgressPanel | ✂? verify orphan | — | — | — |
| 54 | DailyQuestHud (widget) | ❌ UITK | — | ❌ | ❌ |
| 55 | QuestTrackerHud (widget) | 🟡 fragment overlaps Gear Shop frame (seen 07-03) | — | ❌ | 🟡 |
| 56 | CompassHud (widget) | 🟡 | — | ❌ | ❌ |
| 57 | SocialAccessCluster (cluster) | ❌ (file deleted in kit demolition — verify replacement) | — | ❌ | ❌ |
| 58 | EchoWorkforceHud | ✅ BuildObsidianModal | ✅ | ❌ | ❌ |
| 59 | PackStore (IAP) | ❌ UIDocument | 🟡 | ❌ | ❌ |
| 60 | Pi Sign-In | ❌ legacy | — | ❌ | ❌ |
| 61 | RaidSelectionScreen | ✅ BuildObsidianPanel | ✅ | ❌ | ❌ |
| 62 | RaidDeployScreen | 🟡 redundant 2nd header | ✅ | ❌ | ❌ |
| 63 | RaidVictoryController | 🟡 | Continue | ❌ | ❌ |
| 64 | OutpostVictoryController | 🟡 toast | auto | ❌ | ❌ |
| 65 | TroopTrainingPanel | 🟡 raw TMP | ✅ | ❌ | ❌ |
| 66 | ShopPanel (merchant) | 🟡 | ✅ | ✅ vendor oracle | 🟡 |
| 67 | Inventory (bag) | 🟡 | ✅ | ❌ | ❌ |
| 68 | WaveCelebrationManager | ❌ IMGUI — retire into EndStateView | auto | ❌ | ❌ |

## F. Dev-gated (pass = release-strip proof) — 69 AdminOverlay 🔒 · 70 OwnerDevToolsOverlay 🔒 (ships Pi-gated — decide) · 71 DebuggingController 🔒 · 72 DevConsole 🔒

## G. Dead/retired (pass = deleted) — 19 PetSelect ✂ · 31 GameOverUI ✂ · 38 BuildStructureInfoPanel ✂ · 39 BuildPreviewModal ✂ · 73 HeroTalentPanel ✂ · 74 BuildingUpgradePanel twin ✂ · 75 ATB BattleHUD ✂flag · 76 PortraitLockOverlay ✂

---

## The path to all-green (rides the audit's WO-A…F redo lanes)

1. **WO-F (largest)**: UIDocument popup family → kit panels: Workshop #2, CosmeticShop #4, PetSkillTree #5, BuildMenu #35, HelpMenu #44, Jukebox #46, Settings #47, Leaderboard #50, ClanChat #51 (+ its missing close), DailyQuest #54, PackStore #59.
2. **WO-C**: Title + HeroSelect + StoryIntro + onboarding + load overlay (#15–21).
3. **WO-D**: build-mode flow (#35–37, 40) onto the kit.
4. **WO-B finish**: EndStateView verify + retire GameOverScreen/WaveCelebration/arena result into it (#29, 30, 32, 68).
5. **Small-diff batch**: audit §3.7 + Party Shop medallion/X, quest-tracker overlap, move-cluster skin, Pi sign-in, TroopTraining font, RaidDeploy header.
6. **Deletion sweep**: all ✂ rows (G) — pass by removal.
7. **TESTED column**: extend fleet with capture phases — front-end screens, battle-posture drive, EndState force-show, build-mode walk; every capture then EYES-reviewed.

**Standing rule:** update this matrix in the same breath as each surface lands. 100% = every row ✅/✂-executed/🔒-proven. Nothing else counts.

---

## Work log

**2026-07-03 ~09:30 — batch 1 (factory chrome) + first WO-F conversion, IN VERIFICATION**
(code complete + brace-checked; gate/build/windowed-shots queued behind the owner's open
editor session — a watcher auto-launches the proof chain when it closes):
- Factory: `BuildObsidianPanel/Modal` take `medallionIcon`; the Blink medallion socket now
  seats a per-panel emblem (crest fallback — never blank). Nine framed panels pass theirs:
  skill tree/talent, alchemy/potion, jeweler/gem, upgrade/hammer, character/armor,
  gear shop/sword, rumor board/quest, inventory/bag, vendor wares/coin.
- Ruling from the pack art: the gold X **is Blink's own close** (`close_normal.png`) in the
  template's top-right notch — CONFORMANT; only ad-hoc text-X chips violate canon.
- #51 ClanChatPanel: arbiter-registered (modals now close it) + real Close added. Was
  squatting unstyled over every open modal in the bot captures.
- #55 QuestTrackerHud: hides while `PanelManager.AnyOpen` (modal stand-down discipline).
- **#44 HelpMenu: CONVERTED** — UIDocument/UITK retired; kit modal (FrameCore + settings
  medallion + shared Close + scrim), Obsidian button rows, kit ToastCard; AdminOverlay
  PanelSettings handoff preserved via on-demand runtime asset. This is the WO-F REFERENCE
  RECIPE: (1) BuildObsidianModal lazily on first open, (2) rows = BuildObsidianButton in
  `chrome.layout.body`, (3) keep/add PanelManager registration, (4) chrome's shared Close
  replaces any bespoke close row, (5) preserve every action handler verbatim.
- Known stale artifact: `panel_EquipmentPanel.png` in ui-shots is -nographics noise
  (05:43) — EquipmentPanel is NOT_REGISTERED in the hub walk; needs its own capture route.
