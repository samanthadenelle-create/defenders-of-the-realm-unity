# WORK_ORDER_110 — Fix Yarn Spinner Blue Button + Mobile-First HUD Redesign (Chunk 9)

**Status: READY TO IMPLEMENT**

**Context:** Builds on previous chunks (WO-108 castle, WO-109 NPC Yarn dialogue + equip/craft, builder wiring for NPCs with DialogueRunner + NPCCommandBridge, code-built UIs, Economy as resource source). Yarn is active for stationed NPCs. HUD is in DeNelle.HUD (passive, some UIDocument + code panels). UXML has known build issues; prefer code-built Canvases for mobile/reliability.

**Problems to solve:**
- Yarn blue button (default continue indicator from RPGDialoguePresenter.lineCompleteImage / continueSprite in ClassicRPG addon or base) appears during NPC dialogues, breaking immersion.
- HUD not mobile-optimized (small taps, clutter, not thumb-friendly, possibly UXML reliant).

**Priorities:**
1. Fix Yarn blue button first (clean dialogue feel for all NPCs).
2. Mobile HUD redesign.

**Non-negotiables (Claude.md):**
- Nav reads first (done).
- No .unity edits (use builder/bootstraps if placement needed).
- Brace check after every .cs.
- HUD → Core only (use GameStateService for resources, events for actions; no Village refs).
- Code-built UIs (Canvas + Image/Button/Text) for mobile and build reliability.
- Reuse: Existing Yarn (DialogueRunner, LineAdvancer config per notes, CompanionDialoguePresenter, NPCCommandBridge, DialogueEventBus), HUD bootstraps/panels, Economy/GameState for data, code UI patterns from previous (e.g. NPC panels, modals).
- Update READMEs/indices for new files/changes.
- Mobile: Large hit areas (min ~80-120px buttons), portrait/landscape anchors, fantasy theme (earthy #2c2115 bg, #8b5e3c borders, #e8d5a3 text, gold accents).
- Economy for any resource display (via GameState or pushed events).

## Proposed Architecture / Fixes

**1. Yarn Spinner Blue Button Fix:**
- Source (from review + Yarn notes): The "blue Next" / continue is `RPGDialoguePresenter.lineCompleteImage` (UI.Image) showing default `continueSprite` (blue in package samples/prefabs). It appears mid-line or on complete. Also possible default action button or canvas from Yarn setup when DialogueRunner + presenter are used without custom config.
- For NPCs: Runners attached in builder (PlaceNpcStation), using NPCCommandBridge + likely shared or default presenter (CompanionDialoguePresenter for portraits).
- Solution (non-forking, least invasive per Yarn notes):
  - Runtime config: In NPCCommandBridge.Install (or new shared DialogueUIHelper / setup attached to runners or global), find the active presenter (runner.dialogueViews or GetComponentInChildren<RPGDialoguePresenter>), set lineCompleteImage.color = new Color(1,1,1,0) or disable it, swap sprite to custom fantasy "parchment arrow" or none.
  - Replace with custom: Add a persistent or per-dialogue code Canvas (layer high) with a large, themed "Continue" button (Image with stone/wood color + bevel, Text "Continue" or arrow icon in fantasy font, 120px+ hitbox). Show/hide based on dialogue state (listen to DialogueRunner.onDialogueComplete or LineAdvancer, or use DialogueEventBus if available). On tap/click: Call current runner.RequestNextLine() or the LineAdvancer's hurry/advance (configure LineAdvancer with InputMode=InputActions + Pointer press for mobile tap-to-advance/hurry as per Yarn notes).
  - Smooth flow: Use LineAdvancer globally or per runner for tap (no separate blue). Custom button only for visual "continue" when line complete. Position dialogue box (text + portrait from presenter) top-center or bottom, with button below it. No overlap with HUD (dialogue pauses game actions or uses high sort order).
  - Theme: Medieval - dark wood bg for dialogue box, gold text, stone-bordered continue button. Load simple sprite or use primitive + color.
  - Fallback: If presenter not easily accessible, wrap in a global DialogueManager that manages one custom continue UI for all runners.
- This keeps existing portrait injection (CompanionPresenter) and commands (NPCCommandBridge).

**2. Mobile-First HUD Redesign:**
- Current: VillageHudController (UIDocument + .uxml/.uss for shell + ability bar, build button, etc.). Passive via IVillageHud pushes. Other panels (CompassHud, etc.) via bootstraps (some code). Resources likely pushed or from GameState.
- Proposal (code-built for reliability/mobile, fantasy theme, large taps, minimal clutter):
  - **Root**: Code Canvas (add in VillageHudController or new MobileHudCanvas.cs in HUD, DontDestroy or scene-persistent via bootstrap). Sort order high but below dialogue.
  - **Top Bar (Resources, full width ~10% height)**: Horizontal layout, dark wood (#2c2115) bg with stone bevel borders. 4 large slots (icon + bold count text, 60-80px icons, fantasy carved look via color/outline). Left-to-right: Wood, Food, Iron, Crystals (pull from GameStateService.State or Economy snapshot via events; subscribe to ResourcesChanged / OnChanged). Large fonts for readability on small screens. Tap any for tooltip if needed (future).
  - **Top-Right Corner**: Compass (reuse/enhance existing CompassHud as child or integrated; larger 100px for tap, or static display). Fantasy: Compass rose style.
  - **Bottom-Left**: Large "Build" button (square/circle 120-150px diameter, hammer icon, thick stone border, "Build" label). Easy thumb reach. On tap: Raise event (integrator/Village hooks to BuildModeController). Visual feedback (scale on press).
  - **Bottom-Right**: Hero section - Circular portrait (100-120px, from PortraitCache or simple sprite, tap opens hero info/talents if wired). Next to it or below: 4 ability buttons in row (80-100px squares, ability icons, cooldown radial or overlay, hotkey hint small). Large hit areas, fantasy borders. Cooldowns updated via pushes.
  - **Bottom Edge or Top-Left/Right Float**: Pause/Menu (50-70px gear icon, stone frame). Tap opens pause (settings, etc.).
  - **Layout/Responsive**: Anchors (top for bar/compass, bottom for actions). Safe margins (50px+ from edges for thumbs/corners). Portrait: Stack if needed; Landscape: Spread. No overlapping elements. Minimal: Only these; hide during dialogue or full screen moments.
  - **Theme**: Medieval fantasy - Earthy darks, warm golds (#d4af37 accents), subtle wood grain via colors or simple 9-slice if sprites. Consistent with castle (stone/wood). Use Unity UI for perf (no heavy Toolkit for interactive core).
  - **Data/Events**: Resources from GameStateService (Core, no Village dep) + subscribe to its ResourcesChanged or Economy if bridged. Other data pushed via existing IVillageHud (abilities, wave, heart). Buttons raise UnityEvents (BuildRequested etc.) for decoupling.
  - **Mobile Perks**: Pointer events, large sizes (min 80px touch target per guidelines), no tiny text. Testable with LeanTouch if present. Portrait/landscape via CanvasScaler + anchors.
  - **Integration**: Enhance VillageHudController (or add MobileHudBootstrap) to build the Canvas UI in code (similar to previous code panels like EquipmentPanel, NPCUpgradeStation). Wire Economy/GameState for resources. Keep existing UIDocument for non-interactive or legacy if needed, but prioritize code for mobile buttons. Update bootstraps.
- This makes HUD thumb-friendly, uncluttered, always-visible resources (tied to Economy), actions at bottom, fantasy cohesive with castle.

**Implementation Order (prioritized):**
1. Yarn blue button fix (config + custom themed continue button in DialogueUI).
2. HUD redesign (code Canvas in HUD module, layout as proposed, Economy/GameState wiring).
3. Polish: Smooth advance (LineAdvancer), no overlaps (sort orders), builder/bootstrap if needed for attachment.
4. Test: Dialogue in village (no blue, tap works, custom button), HUD on mobile sim (large taps, layout holds in orientations, resources update).

**Files (see implementation for exact):**
- New/Modify in DialogueUI: Custom continue logic or config in Companion/NPCCommandBridge, perhaps new DialogueContinueButton.cs.
- HUD: VillageHudController.cs (build code UI), new or modified panels, README.
- Possibly Dialogue/ or builder for config.
- Update indices, HUD/DialogueUI READMEs.

This delivers immersion-fixed dialogue + usable mobile HUD while following all rules (code UI, isolation, reuse, gates).

WO ready. Owner to provide exact sprites/icons if custom art needed beyond code colors.