> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit a35163e1's own body says "NOT DONE, and not smuggled: WO-899 section 4" and "Appearance is NOT verified - no UI capture was taken", while this WO's acceptance requires UI_CAPTURE_OK.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 899 — HUD polish: analog joystick + wide compass + attack-button blend

**Status:** PARTIAL (reconciled 2026-08-08) · **Silo:** HUD/UI · **For:** CLAUDE CLI · **Date:** 2026-08-07
**PO:** Samantha (owner) · **Author:** UI seat
**Owner asks (felt-test):** (1) replace the boxy joypad with a cleaner analog joystick; (2) make the compass wider so heading changes + enemy bearings read clearly; (3) blend the attack/sword button so it stops looking amateur.
All three are code-built HUD (no prefabs) via `HudKitController` + `ElarionUiKit`/`ElarionUiKitObsidian`. `FeatureFlags.CombatHud611` defaults ON (`FeatureFlags.cs:589`) — the live branches.

---

## 1. Movement — replace the digital D-pad with a clean ANALOG joystick
**Today (grounded):** the bottom-left "^ < > v" cross is `ElarionUiKit.BuildVirtualDPad` (`ElarionUiKit.cs:2754-2835`), built at `HudKitController.cs:560-566` under `CombatHud611`. It is a **custom 4-zone DIGITAL D-pad** (steel bars + hub + 4 transparent chevron press-zones with ASCII glyphs) that emits **discrete unit vectors** into `HudMoveInput.Set` (`HudMoveInput.cs:25-28`). NOT Lean Touch, not analog. (A parallel real analog `VirtualJoystick.cs` exists but is touch-only and separate; `HeroLocomotion.ReadMoveInput` L1502-1588 additively ORs all sources.)

**Do:** build a clean **floating analog stick** HUD-kit widget and swap it in at `HudKitController.cs:560-566` (replace `BuildVirtualDPad`). Design:
- A thin **semi-transparent base ring** (gold-dim rim ~0.35 alpha, dark fill ~0.35 alpha) + a **filled knob** that drags toward the thumb, clamped to the ring radius.
- **Continuous output:** knob deflection → `Vector2` in −1..1 (magnitude = distance/radius), fed to **`HudMoveInput.Set(v)`** every frame while held, `Set(Vector2.zero)` on release. This is the ONLY contract — `HeroLocomotion` already clamps + eases it, so no locomotion change.
- Optional: floating origin (recenters under first touch in the bottom-left zone) like `VirtualJoystick`; or fixed. Keep it minimal, not the boxy steel cross.
- Keep it behind `CombatHud611` (same gate); the old `BuildVirtualDPad` stays as the `else`/fallback.

**Accept:** bottom-left shows a clean analog ring+knob (not the steel cross); movement is smooth/analog (magnitude-scaled), fed via `HudMoveInput.Set`; no HeroLocomotion regression; touch + editor both work.

## 2. Compass — widen the octagon into a readable heading STRIP
**Today (grounded):** `HudCompassWidget` (`HudCompassWidget.cs`) mounts a **compact center square** (`_strip.anchorMin=(0.42,0.34)`, `anchorMax=(0.58,0.99)`, L159-160) rendering `ElarionUiKit.BuildCompass` — a single rotating **octagon** badge (WO-438). It **already** computes heading yaw (`UpdateCardinal` L225-245, `+Z=N/+X=E/-Z=S/-X=W`), plots **enemy-bearing red pips** across a ±60° fan (`UpdateEnemyTicks` L272-317, `BearingToStripX` L323-331, `FovDegrees=120`), and an objective needle. The bearing math is all there.

**Do:** widen it to a horizontal **compass strip** and show cardinal ticks:
- **Widen the mount** to a wide top-center band (e.g. `anchorMin.x≈0.30`, `anchorMax.x≈0.70`, short height) — a strip, not a square.
- **Render cardinal ticks** (N · NE · E · SE · S · SW · W · NW) positioned along the strip using the SAME `yaw` math, so the ticks **scroll as you turn** with your current heading fixed under a **center caret** (the octagon's rotating-badge role becomes the strip's moving tick row + a static center marker).
- **Keep the enemy pips** — they already ride `BearingToStripX`; spread them across the wider strip. Consider raising `FovDegrees` (e.g. 120→160) so more of the field shows before an enemy becomes an edge-arrow; keep the off-strip edge-arrow behavior + the red apex-up triangle (shape-first, colourblind-safe).
- Keep the objective needle as a distinct marker on the strip.

**Accept:** heading reads clearly and the strip scrolls as Grom/camera turns; enemy bearings sit at their real direction across a wider band (edge-arrows when off-strip); still colourblind-safe by shape; reuse existing yaw/`BearingToStripX` math (don't re-derive).

## 3. Attack/sword button — blend it into the bar (refine Grok's pill)
**Today (grounded):** `HudKitController.cs:426-443` builds the bottom-right attack button via `ElarionUiKit.BuildAttackPill` (`ElarionUiKit.cs:3035-3069`): the sword icon (`UiStyle.Icon("energy-sword","attack","sword","melee")`, scale 0.86, `preserveAspect`) sits on a **bespoke procedural teal `CombatPillSprite`** (stadium: teal fill + gold-dim border + glow, `CombatPillSprite` L2861-2915). The other bar buttons (`BuildObsidianButton`, `ElarionUiKitObsidian.cs:617`) use an **`RpgUiCatalog` obsidian plate**. So the sword IS framed, but the **teal pill differs from the obsidian bar** and the energy-sword sprite is horizontal/content-cropped seated flat — that mismatch is the "amateur / doesn't blend" read.

**Do (refine, don't rebuild Grok's pill — Grok made the hit button; tighten it):**
- **Harmonize the plate to the bar's material:** bring the attack pill into the **same obsidian + gold language** as the ability buttons (reuse the `RpgUiCatalog` slot/button plate, or restyle `CombatPillSprite` to obsidian-with-gold-rim + a subtle combat accent), so the whole bottom bar reads as ONE set instead of a lone teal pill.
- **Composite the icon so it integrates, not pasted:** use a cleaner, well-cropped sword/attack glyph centered with correct aspect; add a subtle **inner shadow / soft inset glow** behind the icon so it sits *in* the plate; verify `preserveAspect` + inset scale so it isn't stretched or floating. Keep the cooldown ring/count.
- Keep the touch floor (112px) + the `HudCommands.Attack` binding unchanged.

**Accept:** the attack button reads as part of the action bar (matching plate material), the sword icon is crisp, centered, and integrated (no pasted-sprite look), cooldown/count still work; owner felt-confirms it no longer looks amateur.

---

## 4. Dodge-button icon + empty-slot "add skill" default (owner 2026-08-07)
**Dodge button needs an icon.** The "Dodge/Attack" button reads as bare TEXT with no icon. Assign a dodge/roll glyph — `UiStyle.Icon("dodge","roll","dash","tumble")` — via `SetIcon` the same way the attack pill does (`HudKitController.cs:430-431`), so it reads as an icon button matching the bar, not a text label. Blend it on its plate like §3.

**Empty (unmapped) ability slots → a default "add skill" affordance.** `BuildActionSlot` leaves `icon.enabled=false` when no ability is mapped (`_empty` flag; `SetIcon`, `ElarionUiKitConformance.cs:220-225`), so an unmapped slot renders **blank** — the player can't tell it's fillable vs broken. When a slot has no ability:
- Show a **default placeholder** — a faint gold **"+"** (or "add skill") glyph on a dimmed slot frame, so it reads "slot a skill here."
- **On tap**, a short hint — "Add a skill to activate" — routing the player to the skill-tree loadout (WO-896) to assign one. (The empty action slot and the skill-tree loadout slots are the two ends of the same assignment.)
- Drive it from the action-bar model (slot has no mapped ability) reusing the existing `_empty` state — don't invent a new slot type.

**Accept:** the dodge button shows a clear dodge icon (blended like §3); every empty action slot shows the "add skill" placeholder (+ tap hint), never a blank; a filled slot shows its ability icon exactly as today.

## Files
- `Assets/_Modules/HUD/Kit/HudKitController.cs` — swap sites: move cluster `:560-566`, attack `:426-443`, compass `:583-585`.
- `Assets/_Modules/Core/UI/ElarionUiKit.cs` — new analog-stick builder (replaces/besides `BuildVirtualDPad :2754`); `BuildCompass :2638` + `OctagonSprite`; `BuildAttackPill :3035` + `CombatPillSprite :2861`.
- `Assets/_Modules/HUD/Kit/HudCompassWidget.cs` — widen mount `:159-160`, cardinal-tick strip using `UpdateCardinal`/`BearingToStripX` math.
- `Assets/_Modules/HUD/Kit/HudMoveInput.cs` — the movement contract (`Set`), unchanged; new stick feeds it.
- Reference: `Assets/_Modules/Village/Hero/VirtualJoystick.cs` (existing analog stick to draw styling from), `HeroLocomotion.cs:1502-1588` (do not change).

## Gates / verify
`COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK`. **Headless capture the overworld + a combat scene** and open the PNGs — confirm: clean analog stick, wide readable compass strip with enemy pips, and the attack button matching the bar. Owner felt-closes all three.

## RESULT
`WorkOrders/WORK_ORDER_899_hud_polish_joystick_compass_attack.RESULT.md` — before/after screenshots of each element.
