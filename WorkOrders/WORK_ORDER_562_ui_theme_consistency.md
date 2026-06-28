# WORK ORDER 562 — UI Theme Consistency (black + gold, one shared Close, no brown)

**Status:** IMPLEMENTED (worktree `agent-a44895a7ab0d961c5`, branch `wip/village2-and-f8-tickets`)
**Owner mandate (2026-06-28):** "styling consistent across ALL UI — dialogue, pop-ups, HUD, stores,
upgrades — EVERYTHING inherits the theme from the common presentation layer."
**Canon:** BLACK panel fill + GOLD trim, NO per-panel "X" buttons (ONE shared Close), no brown, DRY.

---

## THE KEY LEVERAGE FINDING

There are TWO shared theme layers, both warm-brown, and almost every panel routes through them:

- `ElarionUi` (UIToolkit helpers `StylePanel`/`StyleScrim`/`StyleButton` + palette) — ~15+ panels.
- `UiStyle.UiTheme` (kit tokens `Glass`/`Cell`/`Niche`…) seeded from `ElarionUi.PanelStone/Dark` —
  drives every `ElarionUiKit` uGUI surface (`Panel`/`Card`/`Slot`/`Niche`).
- `ShopTheme` (the two store panels) — also derived from `ElarionUi`.

`ElarionUiKit.BuildObsidianPanel` (the WO-554 chrome) already used its OWN black `ObsidianFill` +
`ObsidianTrim` literals, so it was correct — but everything NOT using it inherited brown.

**=> Retuning the shared palette tokens to obsidian-black + gold trim reskins ALL of those at once.**
That is the bulk of this WO: a few token edits + extend the kit + route the remaining hand-rolled uGUI.

---

## KIT PIECES ADDED (Core.UI — the common layer)

`Assets/_Modules/Core/UI/ElarionUiKit.cs`
- `ToastCard(parent, tone, accentLeft, align)` + `ToastTone` enum + `ToastAccent(tone)` + `ToastParts`
  — the ONE shared non-blocking toast visual: obsidian fill + soft gold rim + tone accent bar +
  WebGL-safe legacy `Text` label. (~line 311)
- `BuildConfirmModal(name,title,message,confirmLabel,cancelLabel,onConfirm,onCancel,…)` + `ConfirmModal`
  — the ONE shared confirm/popup modal (obsidian panel + shared Close + 1–2 kit Buttons). (~line 410)

## TOKEN RETUNE (the global reskin — black + gold)

`Assets/_Modules/Core/UI/ElarionUi.cs`
- `PanelStone` brown(0.172,0.129,0.082) → obsidian(0.055,0.050,0.060,0.96)
- `PanelStoneDark` brown(0.110,0.086,0.058) → obsidian(0.020,0.020,0.025,0.98)
- `Scrim` → deeper near-black (alpha 0.62→0.82)
- `StoneTrim` brown(0.545,0.369,0.235) → runic GOLD(0.831,0.686,0.216)
- `StylePanel` border: 2px@0.55a → **3px@1.0 gold trim**

`Assets/_Modules/Core/UI/UiStyle.cs`
- `UiTheme.CellSelected` warm(0.26,0.20,0.13) → neutral(0.12,0.12,0.14)
- `UiTheme.StoneNiche` warm(0.075,0.060,0.048) → obsidian(0.030,0.030,0.038)
- (Glass/GlassDeep/Cell/LockedBase/PanelFillSolid auto-follow PanelStone/Dark → now black.)

`Assets/_Modules/Core/UI/ShopTheme.cs`
- `StyleCloseButton` violet 34x34 **"X"** → gold labelled **"Close"** chip (kills per-panel X in BOTH stores)
- `MakeGlimmerChip` warm(0.18,0.14,0.08) → obsidian(0.06,0.06,0.07)
- (FrameWood=StoneTrim now gold; PanelBg/SlotBg/WellBg follow PanelStone/Dark → black.)

## HAND-ROLLED uGUI ROUTED THROUGH THE KIT

| File | Was | Now |
|---|---|---|
| `Village/Hero/TroopTrainingPanel.cs` | `PanelFramed`+bespoke Header+`ButtonPack("X",Danger)` | `BuildObsidianPanel` (black+gold+header+shared Close) |
| `Village/Hero/RaidSelectionScreen.cs` | same pattern + "X" | `BuildObsidianPanel` |
| `Village/Hero/RaidDeployScreen.cs` | same pattern + "X" | `BuildObsidianPanel` |
| `Village/Talents/HeroLoadoutPanelMvvm.cs` | `PanelFramed`+brown solidFill(0.07,0.055,0.042)+Header | `BuildObsidianPanel` |
| `Village/Hero/RumorBoardPanel.cs` | hand-rolled Canvas+brown `PanelStoneDark` panel+custom red Close | `BuildObsidianModal` (+`_panelRoot` cache for tab refresh) |
| `Village/NPCs/GearGrantToast.cs` | hand-rolled blue-grey card+gold bar | `ElarionUiKit.ToastCard(Gold,topbar)` |
| `Village/BuildMode/BuildFeedbackToast.cs` | hand-rolled brown(0.10,0.05,0.06) card+red bar | `ElarionUiKit.ToastCard(Danger,leftbar)` |
| `Village/Harvest/UI/WelcomeBackPopup.cs` | blue-grey card(0.09,0.11,0.17) | obsidian card + 2px gold trim + gold title |
| `Village/BuildMode/BuildPreviewModal.cs` | earthy panel/title/buttons | obsidian panel + gold title + gold CTA buttons |

---

## AUDIT LEDGER (every surface → status → action)

**ALREADY CONSISTENT — route through the shared layer, now auto-black+gold via the token retune:**
- WO-554 panels (verified untouched, still route through `BuildObsidianPanel`): EquipmentPanel,
  InventoryUIBuilder, ShopPanel, PartyShopPanelMvvm, CraftingPanelMvvm, BuildingUpgradePanelMvvm,
  HeroSkillTreePanelMvvm. Plus JewelerPanelMvvm, BattleArenaHud, EchoWorkforceHud (also Obsidian).
- UIToolkit via `ElarionUi.StylePanel`: HelpMenu, MusicSelectionPanel, HeroTalentPanel,
  PetSkillTreePanel, TalentTreePanel, TowerManagerPanel, BuildMenu, BuildingUpgradePanel,
  VillageCraftingPanel, BuildStructureInfoPanel, DailyQuestHud, ClanChatPanel, LeaderboardPanel,
  PlayerProgressPanel, CosmeticShopPanel, TowerSwapMenu, TowerPlacementRotateMenu, RotateModelMenu.
  (TalentTreePanel/BuildingUpgradePanel still build a UIElements `✕` close — flagged below.)
- uGUI via kit tokens: VillageHudController/HudTheme, CompassHud, QuestTrackerHud, GateIntelHud,
  GameOverScreen (exemplary), HeroEquipHud, VillageLoadOverlay.

**RESTYLED THIS WO:** the 9 files in the table above.

**NOT UI / VFX (no chrome):** ResourceGainPopup (world-space float text), Core/VFX/Hud,
OutpostConnectorConfirmInjector, OnboardingPanelGuard, CraftingPedestal, HeroPreviewViewer.

**EXCLUDED — owned by other live agents (kit exposes what they need):**
- `HUD/DialogueView.cs` + DialogueUI/CompanionDialoguePresenter (narrative agent). Kit ToastCard +
  ObsidianFill/Trim + ApplyRounded available for a dialogue box.
- HeroSelect carousel (hero-select agent) — kit Panel/Button/Card available.

**DEAD / DEV-ONLY (noted, not restyled):**
- `LevelUpSkillPopup.cs` — `PopupRetired=true`, hard no-op. Dead.
- `DevPanelController.cs` — `#if DEVELOPMENT_BUILD||UNITY_EDITOR`, never in release. Dev-only.
- `PortraitLockOverlay.cs` — owner-disabled (landscape game). Kept off.
- Village.unity / ATB-flat / V2-deferred UI — abandoned per PIPELINE_STATE.

**OWNER-DECISION FLAGS (remaining, low-risk follow-ups — NOT guessed/changed):**
1. `TalentTreePanel` + `BuildingUpgradePanel` still build a per-panel UIElements `✕`/Danger close.
   Canon wants ONE shared Close — needs a shared UIToolkit close helper in `ElarionUi`
   (mirror of `ObsidianCloseButton`). Deferred to avoid guessing UIElements layout.
2. ShopTheme tabs/chips keep the violet **Aether** highlight for *selected* state (canon arcane
   accent). Left as-is; confirm if stores should be pure gold-on-black with no violet.
3. AdminOverlay keeps a RED rim (intentional "danger/admin" signal) — confirm keep.

---

## VALIDATION
- Brace check PASS on all 13 touched `.cs` files.
- WO-554's 7 panels NOT edited; `BuildObsidianPanel`/`ObsidianFill`/`ObsidianTrim` literals unchanged → no regression (they only read MORE-consistent black tokens for any Glass/Cell use).
- No `.unity`, no Reflection, cross-module calls null-safe.

## FILES MODIFIED (13) + NEW (0)
Core/UI: ElarionUi.cs, UiStyle.cs, ShopTheme.cs, ElarionUiKit.cs
Village: NPCs/GearGrantToast.cs, BuildMode/BuildFeedbackToast.cs, BuildMode/BuildPreviewModal.cs,
Hero/TroopTrainingPanel.cs, Hero/RaidSelectionScreen.cs, Hero/RaidDeployScreen.cs,
Hero/RumorBoardPanel.cs, Talents/HeroLoadoutPanelMvvm.cs, Harvest/UI/WelcomeBackPopup.cs
