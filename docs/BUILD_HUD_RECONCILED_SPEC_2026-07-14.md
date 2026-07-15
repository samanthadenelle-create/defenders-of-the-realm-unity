# Dedicated Build HUD — Reconciled Implementation Spec (2026-07-14)

**Status:** READY TO IMPLEMENT. Merges Grok's build-screen guidance + the internal SME code-map + owner rulings (2026-07-14). Mint/confirm a WO number from `CLI_LANES_WO_NUMBERS.md` before commit. Branch `wip/village2-and-f8-tickets`. Build mode IS the Pi "Seekerthon" demo.

## North star (Grok + owner)
A **dedicated Build HUD presentation layer that owns edit-mode chrome end-to-end**, styled like **Clash of Clans**, in **LANDSCAPE**. Keep `BuildModeController` as the BRAIN (enter/exit, arm, place, move, sell, upgrade, ghost, grid, economy, save) — the new HUD owns only layout/states/chrome. Do NOT rebuild placement, grid, BaseLayout, factory, or category JSON. Re-skin + unify chrome ownership.

## Owner rulings (binding)
- **Orientation = LANDSCAPE** (CoC is landscape; WO-700 Android is landscape).
- **Panels stay near-black** (WO-562 black+gold) — do NOT lighten panel fills.
- **Large carousel** shop, **minimize/collapse on select**, **Lean Touch** camera (two-finger pinch=zoom, twist=rotate view), **backup virtual D-pad bottom-left**.
- Tie-breaker for any gap: **what would Clash of Clans do.**
- Touch targets large (min 112px shortest side; close/Exit >=132px); meaning never by color alone (owner red/green colorblind); ASCII-only TMP; code-built uGUI via ElarionUiKit; ZERO UXML.

## Architecture (Grok) — one canvas, one controller, three states
Create **`BuildHudController`** (new; `Assets/_Modules/Village/BuildMode/`, DeNelle.Village) owning ONE `ElarionUiKit`-built LANDSCAPE canvas (1920x1080, MatchWidthOrHeight=0.5) that **parents** the wallet row, tab row, card grid/carousel, the single intent bar, and the selection verbs. It replaces the fragmented set (BuildPlaceButton canvas + LeanTouchBuildDriver verb-bar canvas + BuildSelectionUI canvas + BuildPaletteUI portrait canvas) — end the "seat wars" and dual-rotate stacks. `BuildModeController` calls into it (Show/Hide/SetState/RefreshResources); it calls back into `BuildModeController` for intents (arm/rotate/place/cancel/move/upgrade/sell).

### State model
| State | Shop | Intent bar | World |
|---|---|---|---|
| **Browse** (default) | open (large carousel) | hidden | tap selects a placed building |
| **Placing** | collapsed to "armed card" summary | Rotate L/R · PLACE(check) · Cancel(X) — ONE bar | drop / nudge ghost |
| **Selected** | open or half | Info(opt) · Move · Upgrade · Sell · Cancel | highlight target |

CoC trains this mental model in 30 seconds. Strict mode isolation — keep `BuildModeHudBridge` hiding the combat HUD; the Build HUD is the sole surface.

## Landscape layout (CoC)
- **Top bar:** `BuildWalletRow` — Wood / Iron / Food (+ Gold if store) chips (icon + ASCII number via `ElarionUi.CompactNumber`), left; "BUILD MODE" label center; **Exit "X Done" top-right (>=132px)**.
- **Bottom:** `BuildTabRow` (Town / Defenses / Walls — Walls gated by `FeatureFlags.WallsTab`) above a **large horizontal card carousel/grid** (icon-first tiles, owned/max badge, multi-cost chips, FREE tag). Carousel is the CoC shop bar.
- **Intent bar** (Placing/Selected): center-bottom, ABOVE the shop; single bar family for both place-intents and edit-verbs.
- **D-pad:** bottom-LEFT (backup pan; publishes `HudMoveInput`). LeanTouch two-finger gestures are primary.

## Concrete changes (Grok priority order, merged)
1. **One Build HUD canvas parents palette + selection + place intents** (ends seat wars / dual rotate). [M]
2. **Single place intent bar:** Rotate L/R · PLACE · Cancel, shown only when armed/moving. KILL the duplicate rotate sources (today rotate exists in BOTH `BuildPlaceButton` and the `LeanTouchBuildDriver` verb bar). [S-M]
3. **`BuildWalletRow`** all pools (not crystals-only). [S]
4. **Shop-style carousel/grid:** icon-first tiles, owned/max, cost chips, using kit slots + the art you already resolve (`BuildPaletteUI.ResolveEntryArt`). [M]
5. **`BuildTabRow`** kit tabs for Town/Defenses/Walls. [S]
6. **Collapse shop while placing** to the armed-card summary. [S]  (= owner "minimize on select")
7. **Move the Orient button off player chrome** (dev-menu only) — stops Done/tutorial collisions. [S]
8. **Lean Touch camera** (SME): add `_camYaw` + rewrite `BuildModeController.ApplyBuildCamera` to orbit `_camFocus` at yaw; add public setters `PanFocusBy`/`AdjustZoom`/`AdjustYaw` (yaw SNAPS to 45deg detents). Rewrite `LeanTouchBuildDriver.Update` to call those setters (NOT write `camera.transform.position` — that fights ApplyBuildCamera every frame): pan=`LeanGesture.GetScreenDelta`, zoom=`LeanGesture.GetPinchScale`, twist=`LeanGesture.GetTwistDegrees`. One finger = placement, two fingers = camera (no conflict). [M]
9. **D-pad → bottom-left** backup (keep GO name "BuildDPad"). [S]
- DEFERRED (not Seekerthon-critical): **uGUI Structure Info sheet on first card tap -> deferred arm** (Grok #6; rebuild the dead UITK `BuildStructureInfoPanel` in uGUI — slice 5, keep immediate-arm for now). **Walls-as-mode drag polyline** (WO-708, post-V1) — separate WO, do NOT smuggle in.

## Keep BuildModeController's brain UNTOUCHED (behavior)
No change to placement math, `RequestUiPlaceConfirm` / `RequestUiRotateQuarter` / `ConfirmIntentThisFrame`, the two-step DROP->adjust->PLACE model, `GhostPreview` (moves CHILD `_visual`; probe `GhostPreview.CurrentPosition`), grid, economy, save. Only the setters in #8 are added to it.

## What NOT to do (Grok)
- Don't rebuild BuildModeController / grid / factory for HUD work.
- Don't bring UITK back for preview (Structure Info, when built, is uGUI).
- Don't reintroduce combat diamond / ability bar "for feel" — hide-all is correct.
- Don't make three separate enter buttons — one Build + tabs.
- Don't smuggle wall drag-lines into this chrome ticket (separate WO-708).

## Constraints (every file)
Code-built uGUI via ElarionUiKit; ZERO UXML/UIDocument/PanelSettings; ASCII-only TMP; never meaning by color alone; panels near-black (WO-562, keep); buttons >=112px shortest side (Exit/close >=132px), sizes as NAMED constants; keep GO name "BuildDPad"; brace-balanced + NUL-free every .cs (§0 Windows path via Edit/Write only). The kit-level `MinTouchPx` floor + green/red button-FACE contrast fixes ride a SEPARATE visual lane (ElarionUiKit/ElarionUi) — this lane must NOT edit ElarionUiKit.cs/ElarionUi.cs to avoid collision; set the Build HUD's own control sizes explicitly.

## Delivery slices (Grok — each headless-probeable)
1. HUD unification (one canvas/parent; move existing widgets under it; NO layout redesign, NO feel regression).
2. Verb collapse (one place bar; remove duplicate rotate/cancel).
3. Wallet + tabs kit (`BuildWalletRow` + `BuildTabRow`).
4. Shop grid polish (icon-first + counts) + collapse-on-place + landscape density.
5. (DEFERRED) Info sheet uGUI + deferred arm.
6. (POST-V1) Walls mode (WO-708).
Plus the camera lane (#8) + d-pad (#9) fold into slices 1/4.

## Reuse ledger (Grok) — extend these, don't greenfield
| CoC chrome | Already in tree | Gap |
|---|---|---|
| Category tabs | BuildType + tabs in `BuildPaletteUI` | swap to kit `BuildTabRow` |
| Shop tiles | cards + `ResolveEntryArt` + FREE/cost | grid + owned/max badges |
| Resource strip | economy multi-cost | header crystals-only -> `BuildWalletRow` |
| Confirm place | `BuildPlaceButton` | merge with rotate/cancel into one intent bar |
| Edit verbs | `BuildSelectionUI` | same bar family as place intents |
| Info before place | `BuildStructureInfoPanel` (dead UITK) | rebuild uGUI (deferred) |
| Hide combat HUD | `BuildModeHudBridge` | keep; host build chrome as the dedicated surface |

## Code anchors (verified) 
- Entry/exit/states: `BuildModeController.cs` — `Enter()` :428, `Exit()` :494, `Update()` dispatcher :538, `Arm` :1668, `CancelArmed` :1682, camera `ApplyBuildCamera` :2463 / `UpdateBuildCameraPan` :2477, `RequestUiPlaceConfirm` :177, `EnsureTouchInput` :2758.
- Palette: `BuildPaletteUI.cs` — `EnsureBuilt` :186 (portrait dock 540x264 -> replace), cards :363, `ResolveEntryArt` :529, `CompactNumber` use :605, `ResourcesChanged` sub :131.
- Place button: `BuildPlaceButton.cs` :57-92 (landscape canvas + PLACE + rotate — fold into intent bar).
- Touch/verb/d-pad: `LeanTouchBuildDriver.cs` — canvas :252-254, verb bar :262-281, d-pad :300-305, gesture `Update` :201-228, `HudMoveInput.Set` :324.
- Selection: `BuildSelectionUI.cs` :125-186. Ghost: `GhostPreview.cs` `MoveTo` :167, `CurrentPosition` :197. HUD hide: `BuildModeHudBridge.cs` :60-69. LeanGesture: `Assets/Plugins/CW/LeanTouch/Required/Scripts/LeanGesture.cs` pinch :484 twist :580.

## Headless verification (orchestrator runs, Unity closed)
`AutoPilotDriver.AssertTutorialFirstTower` (arm->drop->PLACE->StructurePlaced), `AssertBuildMoveChain` incl DPAD link (`HudMoveInput.Set`->`ProbeArmedGhostCell`/`GhostPreview.CurrentPosition`), `AssertTouchVerbBarRenderable` (code-built uGUI, no PanelSettings). Editor: `StrategicPlacementRegression`, `BuildEconomyRegression`, `TowerRespawnRegression`. Add a light `_camYaw`/`_camHeight` read-assert for twist/zoom. Then `COMPILE_GATE_OK` + DataRegression at baseline.
