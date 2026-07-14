# NIGHT REPORT — 2026-07-13 overnight session (WO-714 program, waves 1+2)

**For the owner's morning + the next CLI. Everything below is COMMITTED AND PUSHED through
`16b71a2d`. The tree is clean (one stray ProjectSettings touch from the killed build — safe
to discard or ignore).**

## DONE tonight (all gated: COMPILE_GATE_OK + DataRegression 8-red baseline each wave)

### WebGL preview — LIVE for your phone
**https://defenders-of-the-realm-v2-q4q7n4dlu.vercel.app** (ship build, deployed 19:28; prod
untouched). Contains everything through the founding arc + earned economy + placement flow.

### Fleet run B — the regression you ordered, GREEN
- **AssertStewardSurvivesNewGame: 11/11 PASS** — poison the save, injector survives, New Game,
  Sylas returns. The FTUE-1 class can never quietly pass a fleet again.
- AssertFoundingArc: 12/12 PASS (the pet-house singleton friendly-fire is fixed).
- Remaining fleet noise = known classes only (CavePortal pathing lineage, WO-705 duplicate
  UIDocument, tree-shader verify artifacts, WO-712 island diag pending).

### WO-714 Obsidian conformance — waves 1 + 2 SHIPPED (9 lanes, 9 commits)
- **P1 kit factory** (additive): BuildTabRow · BuildWalletRow · RaritySlot + SparseSlotGrid ·
  ShowToast · PanelOpenCloseFx promoted · close-band reserved on the procedural path ·
  font hard floor · BlinkChromeActive art-presence gate · SpacedDisplayName.
- **W1** Shop/PartyShop (merchant grammar, hue-only tints killed) · **W2** EndState/wave report
  (sprite-first rows, REP-1 CTA preserved) · **W3** Quests/Guide (FrameQuest master-detail) ·
  **W4** Raid screens (zone conversion retired the hand-tuned dodges) · **W5** Hero loadout
  (P4 slot grammar) · **W7** Boss/world bars (pack nameplates, boss emblem by shape) ·
  **W8** Settings/Pause — REACHABLE at last (pause chip + routing; they were code-built but
  nothing ever opened them) · **W9** Title/HeroSelect chrome (PetSelect deferred: UITK
  conversion needs its own WO) · **WO-713** Inventory/character — including your ruling:
  **GENERIC WALLET active, no Pi symbol** (pi/skr skins dormant for the crypto arc), plus the
  two U+2692 tofu sites killed.

## NOT done (the two kills stopped the tail of the conveyor)
1. **No Windows exe on disk** — the last two build+capture launches were stopped mid-build
   (Builds/Windows was wiped first, so the folder is empty). One command rebuilds:
   `powershell -ExecutionPolicy Bypass -Command "Remove-Item -Recurse -Force 'Builds\Windows' -ErrorAction SilentlyContinue; .\build-windows.ps1"`
2. **The image-pair capture run** — the pipeline is identified and ready:
   `./run-autopilot-fleet.ps1 -Count 1 -SeedStart 9500 -TimeoutMin 12 -Graphics` (windowed
   real-rendering; CaptureExtraPanels shoots panel_<Screen>.png), then
   `powershell -ExecutionPolicy Bypass -File build-ui-review.ps1` assembles
   **UI_REVIEW/INDEX.html** — the side-by-side contact sheet you review. Per your law: no
   screen is called done until its pair matches; the pairs are the sign-off artifact.

## Morning order of operations
1. Build (command above) → 2. Graphics capture run → 3. build-ui-review.ps1 → 4. Open
   UI_REVIEW/INDEX.html, mark PASS/FIX per screen → 5. FIX screens loop back through agents
   until the pairs match ("go again till matches").

## Open threads (unchanged from the evening)
WO-705 duplicate-UIDocument RCA (now two PanelSettings variants, JupiterSwapHost common
suspect) · WO-712 courtyard navmesh island diag (instrument-only lane, ready) · WO-706
portraits (UI seat, 10 images incl. containers + bryn.jpg) · WO-702 copy pass + capstone/
voice pins on WO-709 · sell-refund-on-free-copy ruling · PetSelect UITK conversion WO ·
WO-711 further dungeon annotations · touch 45-vs-90 rotate pair call.
