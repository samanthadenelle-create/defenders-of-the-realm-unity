# WORK ORDER 779 — UI Spacing / Layout / Legibility Conformance Sweep

**Status:** READY TO IMPLEMENT — thin slice landed (1b3b9364 Echoes-button safe area); 55-screen rubric NOT run. Overlaps WO-795 waves 1-2.
> ⚠ Cross-reference 2026-08-01: overlaps WO-795 (no-stacked-screens scroll standard) — 795's waves 1-2 already shipped (4461f9ee, 583bc0ac) and `docs/qa/UI_REVIEW_2026-08-01.md` carries fresh findings; reconcile with 795 before running this WO's 55-screen sweep.
- **Lane:** 4 (UI/HUD)
- **Minted:** 2026-07-27 (UI seat)
- **Source:** read-only review of all ~55 code-built UI screens (7-silo fan-out)
- **Branch for this WO's spec:** `claude/ui-spacing-layout-review-bqas0h`
- **Implementer:** CLI (sole committer; writes/build-verifies all `.cs`)

> This WO is a **conformance spec**, not a one-shot fix list. It gives CLI a
> measurable rubric + a per-screen checklist to **compare every screen against
> until each one displays correctly**. Work the screens in the priority order in
> §7, tick the boxes in §8 as each passes, and re-run the verification gate in §9
> after each lane. The WO is DONE only when every non-dead screen in §8 passes
> and the gate in §9 is green.

---

## 0. Relationship to existing UI WOs (read before starting — avoid collisions)

This sweep is the **spacing / layout / legibility / tap-target** pass. It is
adjacent to, and must not duplicate or fight, these in-flight programs:

- **WO-714** Obsidian conformance PROGRAM (pack styling across all screens) — this
  WO is the *layout/legibility* counterpart to 714's *skin* work. Where a screen
  is already being reskinned under 714, fold these criteria into that pass.
- **WO-717** unstyled-class kill · **WO-718** kit-law regression — extend 718's
  regression rather than writing a parallel one (see §9).
- **WO-744** strict-MVVM whole-game UI migration (6/7 silos landed; BattleHud +
  Dialogue landmines pending). **Do NOT restructure a view's VM binding here** —
  presentation-layer spacing/size/theme only. If a screen still needs its MVVM
  migration, do the visual fix in a way that survives that migration (inline
  styles / kit calls in the view builder), and note it.

**Architecture law (CLAUDE.md §ARCHITECTURE):** presentation is a separate layer
that never touches gameplay objects. Every change in this WO is presentation-only.
Do not smuggle structural refactors into this player-facing work.

---

## 1. Goal / Definition of Done

Every code-built UI screen reads as **one designed mobile-first UI** on the
1080×1920 portrait reference canvas:

1. All player-facing text is on the font ladder (≥ `FontFloorMobile` = 30 ref-px).
2. Every tappable control is ≥ `TapTarget` (88 ref-px; kit clamps to 112).
3. All spacing/radii come from the `ElarionUi` scale, not magic numbers.
4. Every screen routes chrome through the shared kit (`ElarionUi` / `ElarionUiKit`
   / `ShopTheme`) — no ad-hoc palettes, no duplicated style helpers.
5. Nothing overflows, overlaps, clips, or runs off-screen at the reference size on
   the shortest supported portrait aspect; long content scrolls.
6. No UXML/USS-dependent styling on any shipped screen (canon §8 — renders empty
   in player builds).

---

## 2. The canonical rubric — what "correct" means (compare every screen against this)

Source of truth: `Assets/_Modules/Core/UI/ElarionUi.cs`,
`ElarionUiKit.cs`, `ElarionUiKitObsidian.cs`, `ShopTheme.cs`.
All values are **reference-px on the 1080×1920 portrait canvas** (kit canvases use
`CanvasScaler` @ 1080×1920, match 0.5; UITK screens must use a PanelSettings with
the same reference so literals are comparable).

| Rule | Constant | Pass test |
|---|---|---|
| Font floor | `ElarionUi.FontFloorMobile` = **30** | No player-facing `fontSize` literal < 30. `FitBlock/FitSingleLine` floors ≥ 30 (kit `FontHardFloor` 20 is last-resort only). |
| Font ladder | Title 88 / Head 64 / Body 50 / Label 40 / Micro 32 | Sizes reference `ElarionUi.Font*`, not literals. Type hierarchy reads (headers ≥ one ladder step above body). |
| Tap target | `ElarionUi.TapTarget` = **88** (kit `MinTouchPx` 112) | Every button/toggle/chip/slider-thumb effective touch height ≥ 88. Never set `minHeight = Auto` on a kit button. |
| Spacing | `PadCard` 12 / `PadPanel` 18 | Padding/margins/gaps reference these (or a deliberate multiple), not raw 3/4/6/8/10/14/20. |
| Radius | `RadiusSm/Md/Lg` = 6/10/16 | Corner radii reference these constants. |
| Palette | `ElarionUi.*` / `ShopTheme.*` tokens | No hardcoded `new Color(...)` for panel fill / trim / text / buttons / scrim. Use `PanelStone(Dark)`, `Gold`, `Gilt`, `Parchment(Dim)`, `Ink`, `Affordable`, `Danger`, `Scrim`. |
| Chrome | kit builders | Panels via `StylePanel`/`BuildObsidianModal`; buttons via `StyleButton`/`BuildObsidianButton`; scrim via `StyleScrim`; title via `MakeTitle`; toasts via `ElarionUiKit.ToastCard`; scroll via `ShopTheme.StyleScrollWell`. No re-implemented `MakeText`/`SetBorder`/`SetRadius`/`AddButton` helpers. |
| Overflow | — | Long lists/detail in a `ScrollView`/scroll well; no silent `if (top < X) break;` truncation; fixed-height cards set `overflow = Hidden` or flex to content. |
| Orientation | 1080×1920 portrait | `CanvasScaler.referenceResolution` = (1080,1920) unless the surface is verified orientation-locked to landscape (Build Mode). No stray (1920,1080). |
| Safe area | — | Edge-anchored elements inset for notch/home-indicator (see §5.6). |
| No UXML | canon §8 | No shipped screen depends on `.uxml`/`.uss` for layout, fonts, or state styling. |
| Colorblind | — | Selected/disabled/error states carry a non-color affordance (marker glyph, label, position), not color alone. |

---

## 3. Reference-quality screens — copy these patterns

These already pass. Use them as the worked example for each pattern; do **not**
change them except where a specific finding is listed in §8.

- **`BuildStructureInfoPanel.cs`** — the model for a UITK modal (StyleScrim /
  StylePanel / MakeRule / StyleButton, on-ladder fonts, kit tap-target floor).
- **`HelpMenu.cs`** — the model kit conversion (BuildObsidianModal +
  BuildButtonColumn/AddColumnButton + shared Close + ToastCard).
- **`DialogueView.cs`** — on-ladder authored + FitBlock/FitSingleLine + HUD-clearance geometry.
- **`PauseController.cs`**, **`MusicSelectionPanel.cs`** — clean portrait modals via BuildButtonColumn.
- **`GearGrantToast.cs`** — the model toast (ElarionUiKit.ToastCard, null-label guard).
- **`LoadingOverlay.cs`** — clean full-screen overlay (correct scaler, canvas order, raycast block).
- **`FloatingHealthBar.cs`**, **`VirtualJoystick.cs`** — the model world-space / overlay controls.

---

## 4. Dead / retired surfaces — RETIRE, don't restyle

Do not spend polish here. Preferred action = delete + de-wire; if deletion is
out of scope, leave as-is and note it. Confirm each is truly unreachable first.

- `SplashLoading.cs` — `_splash.Play()` never called; bumper cut. Retire the component.
- `LevelUpSkillPopup.cs` — `PopupRetired = true` (hard no-op). Delete popup **and**
  stop `LevelUpSkillPopupBootstrap.cs` from installing a UIDocument per scene for it.
- `TowerUpgradeButton.cs` — "no longer the canonical surface." Retire if unattached.
- `PetSelectController.cs` — bypassed by `FeatureFlags.BypassPetSelect`. Either port
  to the Obsidian kit like its siblings or gate it fully; don't leave a half-styled reachable fallback.
- `PackStore.cs` dead currency dict — `_selectedCurrency` is never written; collapse
  `SelectedCurrency()` to `_defaultCurrency` and delete the dict + vestigial `StyleChip` scaffolding.
- `VillageCraftingPanelInput` (in `VillageCraftingPanelBootstrap.cs`) — K/F paths
  removed; the per-frame `Update()` no longer opens anything. Remove the class + fix the stale header comment.
- `DevPanelController.cs` — tagged for removal, dev-gated. Leave alone.

---

## 5. Systemic sweeps (do these first — they clear most of §8)

### 5.1 UXML → code-built conversion (P0/P1 correctness — **do first**)
These render **empty/unstyled in player builds** (canon §8). Convert each to a
code-built inline-style / kit surface (mirror `BuildStructureInfoPanel` / `HelpMenu`):
- `Web3/JupiterSwapPanelController.cs` (+ `Resources/JupiterSwapPanel.uxml`/`.uss`).
  **Also** drive disabled + error states via inline `style` (backgroundColor/color),
  NOT USS class toggles (`swap-confirm-btn--disabled`, `swap-status--error`) — the
  money-path button must visibly disable and errors must turn red in a build.
- `Wallet/WalletConnectDialog.cs` — route connect/disconnect/address/status/badge
  through `ElarionUi.StyleButton` (enforces `minHeight = TapTarget` + `FontBody`);
  hide `_networkBadge` until Connected (currently forced Flex every refresh).
- `Dungeons/UI/CraftingPanelController.cs` (`CraftingPanel.uxml`/`.uss`).
- `Dungeons/UI/DungeonHudController.cs` (`DungeonHud.uxml`/`.uss`).

### 5.2 Font-ladder sweep (P1 — biggest single win)
Replace every player-facing `fontSize` literal with an `ElarionUi.Font*` constant,
and wrap variable-length body labels in `ElarionUiKit.FitBlock`/`FitSingleLine`
with the floor at `FontFloorMobile` (30), never a 8–26 literal band. Screens: see §8.

### 5.3 Tap-target sweep (P1)
Route hand-rolled buttons through `ElarionUi.StyleButton` / `ElarionUiKit`
builders (they self-clamp to 88/112). Fix explicit sub-88 heights and remove any
`minHeight = StyleKeyword.Auto` on a kit button. Screens: see §8.

### 5.4 Shared-helper de-duplication (P2 cleanliness)
Promote the copy-pasted helpers to the kit and delete the local copies:
- `MakeText` — duplicated verbatim in `BuildMenu`, `TowerManagerPanel`,
  `BuildPaletteUI`, `BuildSelectionUI`. Add `ElarionUiKit.MakeText` (uGUI) and use it.
- `MakeCard`/`ApplyIconBtn`/`StyleTextField`/`StylePrimaryBtn`/`MakeDivider` —
  duplicated between `InviteFriendsUI` and `PromoCodeUI`. One shared helper.
- Local `SetBorder`/`SetRadius`/`Hex`/`TintSlider`/`AddImage`/`AddButton`/local
  `ButtonKind` in Arena/Tower/Seating files — use the `ElarionUi`/`ElarionUiKit` equivalents.
- Consider `ElarionUiKit.Slider` / `ElarionUiKit.Toggle` helpers (Settings hand-rolls ~135 lines).

### 5.5 Off-canon palette re-theme (P1)
`InviteFriendsUI.cs` and `PromoCodeUI.cs` use a private violet/lavender palette —
re-theme onto black+gold canon (`ElarionUi.PanelStoneDark`, `Gold`, `Gilt`,
`Parchment`, `StyleButton`, `StyleScrim`, `MakeTitle`).

### 5.6 Safe-area + top-center HUD stack (P2)
- Add a shared safe-area inset helper and apply to all edge-anchored elements
  (see per-screen list in §8).
- The four top-center banners (`WaveCountdownUI` top 8, `AlertIntelSystem` top 56,
  `TutorialHudOverlay` top 74/116) stack by uncoordinated magic constants. Add a
  shared **top-center HUD stacker** (a vertical container the three register into)
  so they never overlap and inset for the notch once.

### 5.7 Orientation reference audit (P1)
Confirm Build Mode / battle orientation lock, then fix stray landscape references:
- `BossHealthBar.cs` L180 `(1920,1080)` → `(1080,1920)` (portrait game).
- `BuildPlaceButton.cs` L59 `(1920,1080)` vs sibling `BuildFeedbackToast` `(1080,1920)`
  — reconcile to the verified Build-Mode orientation.
- `BuildPaletteUI.cs` 1560px-wide dock only fits landscape — verify lock or make responsive.

---

## 6. What NOT to touch

- Do **not** edit any `.unity` scene file by hand (CLAUDE.md §3).
- Do **not** change gameplay logic, VM bindings, save schema, or data catalogs.
- Do **not** alter the reference-quality screens in §3 except for their explicitly
  listed findings.
- Do **not** shrink the font ladder to hide overflow — grow the band / scroll / fit instead (ElarionUi L104-110).
- Do **not** touch world-space combat math beyond the billboard/scale fixes named in §8.
- Do **not** introduce `System.Reflection` in bridge scripts; keep `?.` on cross-module service calls.

---

## 7. Priority order (work top-down)

1. **§5.1 UXML → code-built** (correctness; 4 screens likely invisible in builds).
2. **§5.2 font-ladder sweep** across all §8 screens.
3. **§5.3 tap-target sweep** across all §8 screens.
4. **§5.5 re-theme Invite/Promo** + **§5.4 de-dup helpers**.
5. **Discrete layout bugs (P1):** ArenaAttack overlap, HeroSelect portrait columns + 8px floor, BattleHud geometry rescale.
6. **§5.7 orientation** audit + fixes.
7. **§5.6 safe-area + HUD stacker**, scroll wells for truncating lists.
8. **§4 retire dead surfaces.**

---

## 8. Per-screen conformance checklist (the "compare against" ledger)

Legend: ☐ = fix + verify, ✅REF = reference-quality (leave unless a finding is listed),
💀 = dead/retire per §4. Line numbers are from the review; re-confirm on HEAD.

### Silo A — Onboarding
- **TitleController.cs** — ☐ L252-264 fallback title block off-ladder (26/24 < FontMicro) → ladder constants. ☐ L277-278 bottom button band y0.070, add safe-area inset.
- **StoryIntroController.cs** — ☐ L192 cinematic line 56/44 off-ladder → FontHead/FontBody. ☐ L290-297 `_lineLabel` has NO FitBlock (long beats overflow) → add `ElarionUiKit.FitBlock`. ☐ L291 hardcoded color → `ElarionUi.Parchment`. ☐ L303-307 top-right Skip y0.925-0.985, add notch inset.
- **HeroSelectController.cs** — ☐ **L771-794 private `FitLine`/`FitBlock` with 8px floor → replace with `ElarionUiKit.FitSingleLine`/`FitBlock` (enforce 20/30 floor).** ☐ L227-229 3-column landscape layout on portrait → stack vertically or scrollable specs pane. ☐ L456-509 ~11 cramped fraction bands (resolved by the above two). ☐ L717 glyph 96 off-ladder.
- **OnboardingFlow.cs** — ☐ L310-314 `_body` no FitBlock → add. ☐ L317-323 Skip/Next ~91px (marginal), verify ≥88 on shortest aspect.
- **FoundingChoiceController.cs** — ✅REF.
- **OnboardingPanelGuard.cs** — ✅ (no UI).
- **SplashLoading.cs** — 💀 retire (§4).
- **PetSelectController.cs** — 💀 port-to-kit or gate (§4). If kept: buttons 48/52 < 88; fonts 11-34 off-ladder; magic spacing.

### Silo B — HUD
- **LeaderboardPanel.cs** — ☐ L115/205/207/234/236/238 text 12-18 → ladder + FitSingleLine. ☐ L226 row height 34 magic → tie to text. ☐ L164-165 spacing 3 / pad(8,8,6,6) → PadCard.
- **QuestTrackerHud.cs** — ☐ L107 medallion 52px tap target (owner-approved art size) → pad touch region to 88. ☐ L108 right-edge (-10,0) safe-area inset. ☐ L134/153 icon 34 / glyph 26 off-ladder.
- **AdminOverlay.cs** — dev tool, light touch: ☐ L175 scrim → `ElarionUi.Scrim`; L309 input bg → token; L270 38px rows / L182 device-px width acceptable for dev, note only.
- **ClanChatPanel.cs** — ☐ L183/195/224/229/256/280 chat text 11-18 → ladder + FitBlock. ☐ **L265-270 phrase chips 32px tap target → ≥88** (primary interaction). ☐ L219 row height magic. ☐ L146-155 composer band only 10% of body — verify no clip on tall phone.
- **CosmeticShopPanel.cs** — ☐ L289/357/413/415/423 card text 12-16 → ladder + FitBlock. ☐ **L385/L429-461 Buy/Equip button ~40px → ≥88** (purchase control). ☐ L276 glimmer authored 16 (FitSingleLine-rescued) → author on ladder.
- **DailyQuestHudBootstrap.cs** — ✅ (no UI).
- **DialogueView.cs** — ✅REF. (Optional P2: L287/L735-738 affiliation 26 / option 20-26 sit under 30 floor — conscious tradeoff, leave unless easy.)
- **HelpMenu.cs** — ✅REF. (P2 content: L224/L229 long strings may overflow toast — shorten copy, not styling.)
- **InviteFriendsUI.cs** — ☐ **§5.5 re-theme off violet palette (L46-55).** ☐ L133/149/182/212/362/397/430 fonts 12-22 device-px → ladder. ☐ **L400-447 action buttons ~34px → ≥88** (close X L377 already 120 — mirror it). ☐ §5.4 de-dup vs Promo.
- **PromoCodeUI.cs** — ☐ **§5.5 re-theme (L44-51).** ☐ L125/164/297/318/333 fonts 12-17 → ladder. ☐ **L321-335 Redeem ~36px → ≥88.** ☐ §5.4 de-dup vs Invite.

### Silo C — Build / BuildMode
- **BuildMenu.cs** — ☐ L219/241/320/346/366/368/465/505/532/539/545 fonts 13-16 → ladder (headers → FontHead/Body). ☐ L501 `if (top<0.40) break;` truncation → scroll well. ☐ L355-357 fixed Build button can overlap flowing rows (latent) → reflow. ☐ §5.4 MakeText.
- **TowerManagerPanel.cs** — ☐ L133/150 fonts 13/14 → ladder. ☐ L178 `top<0.24` truncation → scroll. ☐ §5.4 MakeText.
- **TowerSwapMenu.cs** — ☐ L294/303/589 info readouts 12 (next to 64 title) → FontLabel/Micro. ☐ **L382 type chips ~30px / L444-453 currency buttons ~25px → ≥88.** ☐ L343 "Processing..." 16. ☐ L214-216 no maxHeight/ScrollView → add. ☐ L371-453 magic spacing/radii → scale. ☐ L604-616 X close → `StyleCloseButton` convention.
- **TowerUpgradeButton.cs** — 💀 retire if unattached (§4); else route through `StyleButton` (fixes L61 44px, L62 16px, L65 hardcoded blue).
- **LevelUpSkillPopup.cs** + **LevelUpSkillPopupBootstrap.cs** — 💀 delete popup + stop per-scene install (§4).
- **ProgressBar.cs** — world-space; P2 only: L44/L47 colors → `PanelStoneDark`/`Affordable`.
- **BuildPaletteUI.cs** — ☐ L237/407/538/596/610/619/644 carousel fonts 12-16 → ladder. ☐ **L219 1560px dock vs 1080 canvas → §5.7 verify landscape lock or make responsive.** ☐ §5.4 MakeText.
- **BuildPlaceButton.cs** — ☐ **§5.7 L59 (1920,1080) reference reconcile.** ☐ L73 PLACE band ~81px / L87-92 rotate bands ~70px & narrow → ≥88 + verify no label clip.
- **BuildSelectionUI.cs** — ☐ L166 title 17 → ladder. ☐ L170-200 action buttons ~78px → ≥88. ☐ §5.4 MakeText.
- **BuildStructureInfoPanel.cs** — ✅REF. (P2: L140 fixed 300px width wraps a 64px name to 4-5 lines → `maxWidth`/%/smaller name; L141 `maxHeight 86%` no ScrollView → add if content can spill.)
- **BuildingUpgradePanelMvvmBootstrap.cs** — ✅ (no UI).

### Silo D — Village gameplay / overlays
- **TalentTreePanel.cs** — ☐ **L295 `minHeight = Auto` collapses Unlock button (~21px) → remove / set `TapTarget`.** ☐ L221/273/277/281 node fonts 11-13 → ladder. ☐ L147-150 88px title overruns 520px card row → FontHead / wrap / stack. ☐ L247-253 node card spacing off-scale. ☐ verify still reachable (may be dead — replaced by MVVM panels).
- **HeroSkillTreePanelBootstrap.cs** — ✅ (no UI).
- **RumorBoardPanel.cs** — ☐ L446/459 `FitBlock(...,10,15)` caps detail body at 15 < 30 floor → named floor. ☐ L194/338/352/445/474/492 fonts 12-16 → ladder. ☐ L262-271 tab band ~82px (borderline) → verify ≥88.
- **WaveCountdownUI.cs** — ☐ L165/167 hardcoded gold + 22px → `ElarionUi.Gilt` + FontHead/Body. ☐ L162 top=8 no inset → §5.6. ☐ L165-177 hand-rolled styling → shared helper (dup of AlertIntel).
- **WaveCelebrationManager.cs** — ✅ (routes through EndStateView).
- **AlertIntelSystem.cs** — ☐ L203-204 hardcoded gold + 24px → tokens + ladder. ☐ L199-226 hand-rolled banner (dup of WaveCountdown) → shared helper. ☐ L200 top=56 brittle magic offset → §5.6 stacker.
- **EchoTutorialUI.cs** — ☐ L92-130 hand-rolled card + 22px Text → `ElarionUiKit.ToastCard` (mirror GearGrantToast). ☐ L95-98 bottom-left inset.
- **GearOfferChoiceUI.cs** — ☐ L90-119 hand-rolled Image buttons + 26px Text → `ElarionUi.StyleButton`. (Layout/tap targets OK — 96px.)
- **GearGrantToast.cs** — ✅REF. (P2: L78-82 y=-120 add notch inset.)
- **WelcomeBackPopup.cs** — ☐ **L148 Collect button 42px → `TapTarget`** (primary action). ☐ L123/125/139/149/183/186 fonts 12-22 → ladder. ☐ L109-156 hand-rolled panel/button → `StylePanel`/`StyleButton`/`MakeTitle`; pad 20/24 → PadPanel.
- **BossHealthBar.cs** — ☐ **§5.7 L180 (1920,1080) → (1080,1920).** ☐ L189-192 top strip notch inset. (Otherwise well-themed.)
- **FloatingHealthBar.cs** — ✅REF.
- **ThreatSkullPlate.cs** — ☐ L225-226 billboard `LookRotation` with no up-vector (tip-flat bug) → pass `Vector3.up` (mirror FloatingHealthBar L559-577). ☐ L162-163 per-axis scale compensation (mirror FloatingHealthBar).
- **TutorialHudOverlay.cs** — ☐ L180/187/193/213 FTUE fonts 11-15 → ladder. ☐ L39-42/167-176 hand-duplicated palette → tokens. ☐ L169/211 top 74/116 magic → §5.6 stacker.
- **PetIntroduction.cs** — ☐ **L242 Defend/Gather buttons 46px → `TapTarget`.** ☐ L139-158/240 fonts 15-22 → ladder. ☐ L125-134/237-245 scrim/card/buttons hand-styled → `StyleScrim`/`StylePanel`/`StyleButton`/`MakeTitle`.
- **VirtualJoystick.cs** — ✅REF.

### Silo E — Arena / Dungeon
- **ArenaDefensePaletteUI.cs** — ☐ L215/231-243 fixed 116×108 card + two FontLabel labels overflow → widen / FontMicro name / flex + `overflow=Hidden`. ☐ L139-140/216-219 magic spacing → PadCard. ☐ L128-129 bottom anchor safe-area inset. ☐ L145 magic placeholder string.
- **ArenaAttackPaletteUI.cs** — ☐ **L139-145 Cancel & Launch buttons x-ranges OVERLAP ~32px → separate centers/half-widths.** ☐ L143-145/L301 "Launch Raid" at FontBody won't fit ~162px button → shorten / auto-size floor / widen. ☐ L233-322 duplicated helpers + local `ButtonKind` → shared kit.
- **SeatingEditorOverlay.cs** — dev-only, P2: fonts 10-15 off-ladder; steppers 26×22 << 88; spacing/radius off-scale; body not scrollable. Note-level unless promoted to player-facing.
- **TowerPlacementRotateMenu.cs** — ☐ **player-facing: L366-996 all fonts 8-18 off-ladder → ladder.** ☐ **L670-707 Confirm/Cancel/Reset ~29px + not via StyleButton → route through `StyleButton` (88 floor); Rst 26×22 → ≥88.** ☐ L385-388 empty-text `hammer` label (dead) → remove. ☐ L308-350 fixed non-scrolling column overflows once fonts corrected → ScrollView. ☐ magic spacing + local helpers → kit.
- **CraftingPanelController.cs** — ☐ **§5.1 UXML → code-built.** (L61 TickChar "OK" ASCII fallback is correct — keep.)
- **DungeonHudController.cs** — ☐ **§5.1 UXML → code-built.** (C# fill logic is clean — keep.)
- **DungeonToastView.cs** — ✅REF. (P2: L83 y=-180 / L84 560×84 magic literals — verify notch clearance.)
- **VillageCraftingPanelBootstrap.cs** — 💀 remove vestigial `VillageCraftingPanelInput` + stale header (§4).

### Silo F — Wallet / Store
- **JupiterSwapPanelController.cs** — ☐ **§5.1 UXML → code-built + inline disabled/error states.**
- **WalletConnectDialog.cs** — ☐ **§5.1 UXML → code-built; StyleButton on all controls; badge hidden until Connected.**
- **PackStore.cs** — ☐ L174-376 fonts 11-20 (retired ladder) → ladder / `ElarionUiKit.Label` + FitBlock. ☐ **L155-156 `StorePanelAnchor` 0.325-0.675 = 35% width strip → near-full-width portrait anchor.** ☐ L349-398 re-verify Buy button ≥112 at true portrait width (ClampMinTouch re-inflation risk) — author absolute px > 112. ☐ L317/589-590 magic spacing → scale. ☐ L321/328/345/367/580 ad-hoc colors → tokens. ☐ L203-208 negative-y footer anchors → positive/real footer. ☐ L174-176/268 treasury address overflow → mid-ellipsis `Shorten()` + fit. ☐ §4 dead currency dict. (Note: ShopTheme is UITK-only; PackStore is uGUI post-WO-F — add/use a **uGUI** card+well helper in ElarionUiKit rather than ShopTheme.)
- **PackStoreBootstrap.cs** — ✅ (no UI).

### Silo G — Settings / Battle / Core
- **SettingsController.cs** — ☐ **L552 toggle box 44×44 → ≥88** (every settings toggle). ☐ L507 slider thumb 22px → ~40-48. ☐ L231-256 selector chip rows 0.055 body vs 112 clamp spill → ≥0.065 band. ☐ L182-194/443/454/520/535/542 literal 30/34 fonts → constants + lift captions to FontLabel/Body (hierarchy). ☐ L174 vs L180 two controls both "Music" → rename. ☐ §5.4 Slider/Toggle helper.
- **PauseController.cs** — ✅REF.
- **MusicSelectionPanel.cs** — ☐ L108 subtitle FitBlock min 24 < 30 floor → keep ≥30 + grow band / shorten copy. (Otherwise clean.)
- **BattleHudUgui.cs** — ☐ **systemic geometry↔ladder mismatch (P1) — pixel rects sized for the retired ~11-18 ladder never rescaled ~3.4×.** L297/303/306 88px title in 34px rect in 76px panel spills into WAVE line. L724/761/774-776 40px name + 32px bar labels in 44/12px rects overlap. **L431-434/472/521/587/606 command buttons 40px → ≥88** (primary combat controls). L642/682-685 party slot stack 247 > 232 clips. L336/396/641 no safe-area. → Rescale panel/element/button geometry (≈×2 heights) or convert command/party rows to self-clamping kit builders. Likely its own follow-up given the MVVM landmine (WO-744) — coordinate.
- **PanelRouter.cs** — ✅REF (router; layering delegated to PanelManager).
- **LoadingOverlay.cs** — ✅REF.
- **VillageLoadOverlay.cs** — ☐ L126/175 literal 64/30 → `ElarionUi.FontHead`/`FontFloorMobile`. (Otherwise clean.)
- **DevPanelController.cs** — 💀 dev-gated, tagged for removal — leave (§4).

---

## 9. Verification gate (run after each lane; WO DONE when green)

CLI is sole gate-runner (CLAUDE.md §1/§12 — instrument, don't guess). Do NOT
claim a screen fixed on faith; capture the evidence.

1. **Brace/NUL gate:** `DeNelle.Editor.CompileGate.Run` → `COMPILE_GATE_OK` after every `.cs` edit.
2. **Kit-law regression (extend WO-718):** add/extend an assertion that scans built
   UI screens for: any player-facing `fontSize < FontFloorMobile`; any kit button
   with `minHeight` below `TapTarget` or set to `Auto`; any `CanvasScaler` reference
   ≠ (1080,1920) on a non-orientation-locked surface. Fail the marker if violated.
   Reuse `Assets/Editor/Regression/HudUiRegression.cs` as the harness home.
3. **Headless screenshot pass:** drive each screen via the AutoPilot fleet
   (`run-defenders` skill) and capture a screenshot at the 1080×1920 reference AND
   the shortest supported portrait aspect. Compare against the §2 rubric:
   - no text clipped / overflowing its container
   - no overlapping controls
   - no element off-screen or under a simulated notch inset
   - every interactive element visually ≥ the 88px touch box
4. **F8 clean:** no new `[Flow:*]`/Guard `Fail` lines from any converted screen on load.
5. **Per-screen sign-off:** tick §8 as each screen passes 1-4. PO (owner)
   felt-verifies + closes (CLAUDE.md §13 — headless can't judge feel).

---

## 10. Acceptance criteria (review line by line before RESULT)

- [ ] All §5.1 UXML screens are code-built; disabled/error states render in a build.
- [ ] No player-facing `fontSize` literal < 30 remains; ladder constants used throughout.
- [ ] Every interactive control ≥ 88 effective touch height; no `minHeight = Auto` on kit buttons.
- [ ] All spacing/radii/colors reference `ElarionUi`/`ShopTheme` tokens; no ad-hoc palettes.
- [ ] `InviteFriendsUI`/`PromoCodeUI` re-themed to black+gold; shared helpers de-duplicated.
- [ ] ArenaAttack overlap, HeroSelect portrait columns + 8px floor, BattleHud geometry all resolved.
- [ ] No stray `(1920,1080)` reference on a portrait surface; Build-Mode orientation confirmed.
- [ ] Safe-area insets applied; top-center HUD banners share a non-overlapping stacker.
- [ ] Truncating lists scroll; no `if (top < X) break;` silent cut.
- [ ] Dead surfaces (§4) retired or explicitly deferred with a note.
- [ ] §9 gate green on every non-dead screen; §8 fully ticked.
- [ ] Canon updated (CLAUDE.md §15): note the sweep in the load-bearing UI docs / this WO's RESULT.

---

_Authored by the UI seat (spec/RCA only — UI never edits `.cs`). CLI implements,
build-verifies, and is sole committer. Full review evidence: 7-silo read-only
fan-out, 2026-07-27._
