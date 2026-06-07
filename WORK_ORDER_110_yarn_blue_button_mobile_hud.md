# WORK_ORDER_110 — Fix Yarn Spinner Blue Button + Mobile-First HUD Redesign (Chunk 9)

**Status: READY TO IMPLEMENT**

**Context:** Follows WO-109 (NPC Yarn + equip/craft), WO-108 (castle with stationed NPCs using DialogueRunner + NPCCommandBridge), prior code-built UIs (Canvases like EquipmentPanel, NPCUpgradeStation). Yarn active for NPCs. HUD in DeNelle.HUD (VillageHudController + UIDocument/.uxml + code panels/bootstraps). Project rule: UXML does NOT work reliably in builds — code-built Canvas preferred for mobile/core UI.

**Problems:**
- Yarn blue button (default continue from RPGDialoguePresenter.lineCompleteImage / continueSprite in ClassicRPG or base setup) appears in NPC dialogues (Forge, Armorer, etc.), breaking immersion.
- HUD not mobile-optimized (small targets, clutter, possible UXML reliance, not thumb/portrait friendly).

**Priorities (user):**
1. Fix blue button first (smooth, immersive dialogue for all NPCs: auto-advance on tap, clean themed continue, no overlap).
2. Mobile HUD redesign (large taps, minimal, fantasy, resources visible, actions bottom, portrait/landscape).

**Non-negotiables (Claude.md + project):**
- Nav reads first (done).
- No .unity hand-edits.
- Brace check (exact python) after every .cs edit.
- HUD → Core only (use GameStateService for resources/events; events for actions; no direct Village).
- Code-built UIs (Canvas + RectTransform/Image/Button/Text) for mobile/build reliability.
- Reuse: Yarn (runners from builder, LineAdvancer per notes, CompanionPresenter for portraits, NPCCommandBridge, DialogueEventBus), existing HUD bootstraps/panels (Compass etc.), Economy/GameState for data, code UI patterns (e.g. previous panels).
- Update READMEs/indices.
- Mobile: Large hit areas (80-150px+), safe margins, anchors for orientations, fantasy theme (earthy #2c2115, stone #8b5e3c, gold #d4af37, parchment text).
- Economy for resources (via GameState or OnChanged pushes).

## Architecture / Fix Proposal (as required)

**1. Yarn Blue Button Triage & Fix:**
- Source (review + Yarn notes): `RPGDialoguePresenter.lineCompleteImage` (UI.Image, `[MustNotBeNull]`) shows default `continueSprite` (blue in samples/prefabs) mid/complete line. Also possible default action button/canvas from DialogueRunner + presenter without custom config. Appears because NPCs use attached DialogueRunner (builder PlaceNpcStation) + likely default or CompanionPresenter (subclass of RPG for portraits).
- Fix (non-fork, least invasive per notes):
  - Runtime hide/replace: In NPCCommandBridge.Install (or new shared DialogueUIHelper attached to runners/global), after runner setup: Find presenter (runner.GetComponentInChildren<RPGDialoguePresenter>() or dialogueViews), set its lineCompleteImage.color = Color.clear (or .enabled=false), swap .sprite to custom fantasy (load "UI/ContinueArrow" or create primitive + color; parchment/stone theme, arrow or "Continue" text).
  - Custom themed continue: Add code Canvas (high sort, "DialogueContinueCanvas") with large (120px+) fantasy "Continue" button (Image stone-bordered, Text "Continue" or icon in gold, bevel for medieval). Show only when line complete (listen to runner state, LineAdvancer, or DialogueEventBus). On click/tap: Call currentRunner.RequestNextLine() or LineAdvancer logic. Position below dialogue box (text/portrait from presenter).
  - Smooth flow: Configure LineAdvancer (InputMode = InputActions + <Pointer>/press for tap-to-hurry/advance, separateHurryUpAndAdvanceControls=false — as per Yarn notes for mobile). This unifies tap (no blue needed). Custom button purely visual/theme. Dialogue box (from presenter) top or bottom-center, continue below it. High canvas layer, pause other input during dialogue or use masks.
  - Theme: Medieval fantasy — dark wood box, gold text, stone-framed continue button (match castle). Reuse PortraitCache if needed.
  - For all NPCs: Since bridge already on their runners (builder), central config here covers Forge/Armorer/etc. Update any global dialogue setup.
  - Fallback: If presenter hard to reach, global DialogueManager listens for any runner start and manages one custom continue UI + advance proxy.
- Result: No blue button, clean tap advance + themed visual continue, immersive fantasy flow, no HUD overlap (sort/layer).

**2. Mobile-First HUD Redesign:**
- Current: VillageHudController (UIDocument + VillageHud.uxml/.uss for shell/abilities/build/heart/wave). Passive (IVillageHud pushes from Village integrator). Bootstraps for CompassHud, other panels (many code-built per README). Resources likely via GameState/Economy events.
- Proposal (code-built Canvas core for mobile/builds + fantasy, large taps, minimal clutter, as per rules):
  - **Root Canvas**: Code-built (add in VillageHudController or new MobileHudCanvas.cs + bootstrap in HUD; Canvas, Screen Space - Overlay, scale with CanvasScaler for resolutions). High but below dialogue layer.
  - **Top Bar — Resources (full width, ~80-100px tall for touch)**: Horizontal, dark wood bg (#2c2115) with stone bevel borders (#8b5e3c). 4 large slots (icon 50-70px + bold count text 24-32pt, fantasy "carved" via outline/glow). Order: Wood | Food | Iron | Crystals (pull from GameStateService.State.Resources / .Wood etc. or Economy snapshot; subscribe to ResourcesChanged / OnChanged for live updates). Gold accents (#d4af37) for crystals. Tap for future details.
  - **Top-Right**: Compass (integrate/reuse CompassHud as child or larger icon 100px; fantasy rose style if possible).
  - **Bottom-Left**: Large "Build" button (120-150px square/circle, hammer icon, thick stone border, subtle press scale). Easy left-thumb. Event: BuildRequested (existing, integrator to BuildMode).
  - **Bottom-Right**: Hero + Abilities. Circular portrait (100-120px, from PortraitCache or tinted sprite, tap for hero panel). Horizontal row of 4 ability buttons (90-110px squares, ability icons, cooldown radial/overlay + small text). Large targets, fantasy borders. Data pushed via existing ability setters.
  - **Edge (e.g. top-left or bottom floating)**: Pause/Menu (60-80px gear, stone frame). Event for pause.
  - **Layout/Responsive**: Anchors (top for bar/compass, bottom for actions — left/right split). Safe 40-60px margins from screen edges (thumb zones). Portrait: Compact or stack if needed; Landscape: Spread. CanvasScaler + flexible Rects. No clutter — only these essentials visible.
  - **Theme**: Medieval fantasy cohesive with castle (earthy darks, warm stone/wood, gold text/icons, subtle bevels for "carved" look). Use Unity UI primitives + colors (no heavy assets for perf). Consistent with prior code UIs (e.g. NPC panels).
  - **Mobile Perks**: Min 80-100px+ hit areas + padding (per mobile guidelines). Pointer events. Large fonts. Responsive anchors handle portrait/landscape. Low draw (simple UI).
  - **Data/Events (isolation)**: Resources from GameStateService (Core — no Village dep) + subscribe to its ResourcesChanged (or Economy if bridged via events). Other (wave, heart, abilities) via existing IVillageHud pushes / setters. Buttons raise UnityEvents (BuildRequested, etc.) — integrator (Village) hooks without HUD knowing Village.
  - **Implementation**: Enhance VillageHudController (build Canvas + children in Awake/Start if no UIDocument or as overlay; similar to code panels like EquipmentPanel). Or new MobileHudCanvas.cs + HUD bootstrap. Keep UIDocument for legacy display if needed, but make interactive core code-built. Wire Economy/GameState for resources. Update bootstraps.
- Result: Thumb-friendly (bottom actions, large everything), uncluttered (essentials only), resources always visible (Economy-tied), fantasy medieval (matches Last Bastion castle), works orientations, code-built for builds/mobile.

**Steps (prioritized):**
1. Yarn fix (config + custom themed continue button in DialogueUI; update NPC setup/bridge for advance).
2. HUD redesign (code Canvas layout in HUD controller per proposal; Economy/GameState wire).
3. Polish (tap flow, layers for no overlap, builder/bootstrap if HUD/dialogue attachment, tests via scene).
4. Gates: Braces, README updates (HUD/DialogueUI), indices.

**Files (see impl):**
- DialogueUI/: NPCCommandBridge.cs (or new helper), CompanionPresenter config.
- HUD/: VillageHudController.cs (or new MobileHud), README, possible bootstrap.
- Possibly builder for any attachment, or Dialogue/ assets.
- Update Village/_Modules/HUD/README.md, DialogueUI/README.md, indices if new.

This fixes immersion (no blue, clean dialogue) + delivers usable mobile HUD while obeying rules. End-to-end with prior NPC Yarn work.

WO created. Owner: provide custom sprite for continue if wanted beyond code theme; final art pass later. Implement after proposal.