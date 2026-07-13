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

**2026-07-03 ~12:15 — round 11:50 verified + committed (512d3289); WO-F conversion #2**
- VERIFIED by fresh captures: emblems seated on Talent/Merchant/Crafting frames (shield/
  sword/potion visible), Clan Chat bleed gone, tracker card stands down. Four frames' zones
  were the gap (Talent/Quest/Core measured in; Merchant socket added) — resolver chain proven
  by the Crafting frames working first.
- OPEN: small icon pair still floats right-edge during modals — likely the KIT DOCK not
  standing down in modal posture (dock intents live in HudKitController :596; verify the
  modal occupancy row clears the dock area) OR a legacy launcher. One trace read to settle.
- OPEN: talent-grid red locked-node text readability; Equipment + HelpMenu capture routes.
- **#46 Jukebox (MusicSelectionPanel): CONVERTED** per the HelpMenu recipe — kit FrameCore
  modal, dynamic track rows as Obsidian buttons (selected = Green + ✓), arbiter contract
  kept (battle-lock reject honored), Toggle()/Open() reflection seam kept for the kit dock;
  bootstrap no longer needs a UIDocument/PanelSettings host (menu-scene guard inlined —
  Audio can't reference HUD). DeNelle.Audio.asmdef += UnityEngine.UI. IN VERIFICATION
  (queued behind the owner's editor session; watcher armed).
- **#50 Leaderboard: CONVERTED** per the recipe — kit FrameCore modal (combat medallion),
  profile strip + 3 Obsidian metric tabs (active=Yellow, rebuilt on switch) + uGUI
  ScrollRect ranked list (local row gold-tinted) + the honest source-badge footer; service
  API calls preserved verbatim; bootstrap host-free. KIT ASK logged: no shared scroll
  widget in the kit yet — Leaderboard + the demo both compose ScrollRect inline; extract
  `ElarionUiKit.BuildScrollColumn` when a third caller appears. IN VERIFICATION.
- **#2 Workshop (VillageCraftingPanel): CONVERTED** — kit FrameCrafting master-detail
  (the owner-ratified split template): bodyLeft dark well = recipe Obsidian buttons
  (selected=Yellow, ✓/✗ affordability), bodyRight parchment well = detail in dark INK
  (light text is unreadable on parchment — new Ink/InkDim/InkGood/InkBad palette),
  footer strip = larder readout; Craft CTA Green/Gray + interactable gate. API preserved
  verbatim (Instance/Toggle/Open/Close/IsOpen, PanelRouter PanelId.Crafting, arbiter
  "Workshop", VillageInventory.Changed, TryCraft/CanCraft/Get, glyphs). Bootstrap
  host-free; VillageCraftingPanelInput untouched (routes via PanelRouter). IN VERIFICATION.
- **#4 CosmeticShop: CONVERTED** — kit FrameMerchant modal (coin medallion), category
  tabs (Yellow=active), inline ScrollRect card list, per-card preview (RawImage render or
  tinted swatch on slot_item plate), DEF-197 "short by N" price honesty, Buy/Equip/
  Equipped/Locked state machine + toasts preserved exactly, reflection bridge into
  DeNelle.Cosmetics preserved character-for-character; bootstrap host-free. ShopTheme
  helpers no longer used by this panel. IN VERIFICATION.
- **#5 PetSkillTree: CONVERTED** — kit FramePet modal (tree medallion), species tabs
  (Yellow=active), scroll column of node cards on slot_talent plates (unlocked=green
  tint, unlockable=gold, locked=35% CanvasGroup alpha per spec), tier/type badges as
  palette-graded rich text, Unlock action + honest LockReason preserved; DeNelle.Pets
  reflection bridge + all Extract* accessors preserved verbatim; P-key driver untouched;
  bootstrap host-free. IN VERIFICATION.
- **#47 Settings: CONVERTED** — the UXML screen (SettingsScreen.uxml, canon-flagged
  empty-in-builds) RETIRED; code-built kit FrameSettings modal at sortingOrder 32000
  (settings above every modal): composed uGUI sliders (Blink panel_bar track + gold
  fill/handle) with % labels, mute/shake toggles, quality + difficulty selector rows
  (Yellow=active, rebuilt on switch), difficulty blurb, audio-seam notice, Game Guide +
  Reset Defaults; Back = the chrome Close (raises SettingsClosed — PauseController's
  contract intact); SettingsModel write-through unchanged. DeNelle.Settings.asmdef +=
  UnityEngine.UI + Unity.TextMeshPro (lesson from the Audio gate failure, applied
  proactively). PauseController (#47's opener) still UIDocument — its own conversion NEXT.
- **#54 DailyQuestHud: modal stand-down added** (same pattern as QuestTracker); full
  restyle still queued.
- **#47b PauseController: CONVERTED** — PauseOverlay.uxml RETIRED; kit FrameOptions modal
  at 31500 (below Settings 32000): Resume (Green) / Settings (only when wired — no dead
  control) / Quit to Title (Red); chrome Close = Resume; PauseGate seam, timeScale
  capture/restore, OnApplicationPause auto-pause, SettingsClosed re-show, and the
  quit-unfreezes-first ordering all preserved verbatim. IN VERIFICATION.
- **Focus-loss immunity (driver)**: windowed runs 13:51 + 14:01 FROZE when the owner used
  her machine (window unfocused → player background-pauses while realtime budgets expire;
  proof: break-log dead after t=110 + OnApplicationPause in Player.log; the focused 11:50
  runs completed on identical code). `AutoPilotDriver.RunAll` now sets
  `Application.runInBackground = true`. Needs the next build to take effect.
- **#59 PackStore SURVEYED (next conversion, do on fresh budget — real-money surface):**
  703 lines, already code-built UITK (ignores its UXML — immune to the empty-trap), styled
  via ShopTheme. Contract to preserve verbatim: WalletService purchase flow (async UniTask,
  never async void), per-pack currency rail selection (SOL/USDC/SKR chips), PackCatalog
  render loop, PackPurchased event, treasury-transparency line, CurrencyDisclaimer, the
  cozy covenant "You are never required to spend anything. Ever." VERBATIM, and CloseStore's
  reflection route through MarketplaceInteractor.CloseStore (re-enables HeroLocomotion —
  soft-lock guard) with the locomotion-re-enable fallback. Target: FrameMerchant kit modal
  + scroll card column per the CosmeticShop recipe.
- Conversion queue remaining: PackStore (#59), BuildMenu (#35), DailyQuest restyle (#54),
  then front-end (WO-C), build-mode (WO-D), end-states (WO-B), deletion sweep (G).
- ORPHANED ASSETS after #47/#47b: SettingsScreen.uxml/.uss + PauseOverlay.uxml (+ any
  authoring GameObjects wiring UIDocument source assets) — deletion-sweep candidates once
  verification passes.
- NOTE for the verification chain: SIX conversions ride the next gate (HelpMenu, Jukebox,
  Leaderboard, Workshop, CosmeticShop, PetSkillTree) + the asmdef edit. If the gate names
  errors, fix per file — the recipe is proven, typos are the likely class. After gate:
  build + windowed run + the popup oracle verdict on all six (their closes are now the
  chrome's shared Close, which the oracle finds by name).
