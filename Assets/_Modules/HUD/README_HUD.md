# Dark Fantasy Mobile HUD (HUD-001) — README

Production-ready, code-built uGUI dark fantasy mobile portrait HUD for Defenders of the Realm (Unity 6 LTS + URP, 1080x1920 ref resolution).

**Status**: Implemented per updated WORK ORDER HUD-001. Ties into existing architecture. Uses **Lean Touch exclusively** for all input (D-Pad, taps, hero selection, build, skills, modal). No Unity Button.onClick or legacy Input for HUD controls.

## Deliverables
- `HUDManager.cs` — main orchestrator. Self-builds the full rich reference layout at runtime.
- `VirtualDPadLean.cs` — Lean Touch D-Pad (radius-constrained knob, normalized Vector2 static `Move`, pre-wired LeanFingerDown/Update/Up).
- `README_HUD.md` (this file) + updates to module `README.md`, `PROJECT_INDEX.md`, `docs/README.md`.
- HUD is drop-in: add `HUDManager` component to any GameObject (or create `HUD_Root.prefab` from the runtime hierarchy). It creates `HUD_Root` child with Canvas + all elements.

The lean vital-only `VillageHudController` (resources + wave/heart via `IVillageHud` / `CoreServices.Hud`) is **untouched and coexists**. Use the rich HUDManager for battle / map / PatriciaLight / Defend-the-Tower scenes.

## Visual Style (matches reference + prompt)
- Dark misty/foggy bg (#0F0F14 range) + moody low-alpha overlay.
- Metallic silver/gold beveled frames (colored Images).
- Semi-transparent dark panels (#1A1A1A @ ~0.85–0.88 alpha).
- Red HP fills, blue MP fills, glowing skill icons (larger low-alpha child Image rings).
- Parchment/gold text, pulsing "Unleash the Horde" (scale + alpha), rotating compass needle stub + pulsing red enemy dots.
- All hit areas thumb-friendly (>> 48dp on 1080x1920 ref; D-Pad travel radius ~100–110 px).

## Layout
- **Top Center**: Circular Compass (metal ring + rotating gold needle + 4–5 pulsing red enemy direction dots).
- **Top Right**: "UNLEASH THE HORDE" large red rune button (pulses when idle/ready) + small Mini-Map (Elarion Glade label + Tree dot + path dots).
- **Left Stack**: Metallic frames — Tree of Life (🌳 + red bar + text), Hero (portrait + name + red/blue bars), 2 Allies (tap to select). Resources row below (🍖 Food / 🪵 Wood / ⛏️ Iron / 💎 Cryst) with live counters.
- **Bottom Left**: Fantasy D-Pad (metal/stone frame, draggable knob constrained to radius).
- **Right**: Large circular Build (🔨 hammer) button. When hero selected (tap left Hero frame): Build hides, 4-skill Diamond appears (Top=Ultimate, Right=Primary, Bottom=Utility, Left=Secondary). Knight defaults: Whirl/Parry/Stab/Rally (glowing). Healer & Wizard sets also provided; switch `_heroClass` or drive from real hero data.
- **Bottom Bar**: Horizontal metallic action bar — left hero portrait + red HP + blue MP, right horizontal skill hotkeys (Q/E/1/2/3/4/R + icons) with vertical cooldown fills (reference style).
- **Build Modal** (centered overlay, hidden by default): 4 tabs (Towers/Defenses, Upgrades, Allies, Traps), 3x3 grid of cards (icon + name + cost strings), "Drag on ground to place (Lean Touch)" hint. Taps log + fire `BuildRequested` + close. Wire to real `BuildModeController` / `LeanTouchBuildDriver` / `GhostPreview` by extending `OnBuildCardLeanTap`.

## Input — Lean Touch ONLY (per updated WO)
- **All** interactive elements use attached Lean components at runtime in `WireLeanInput()`:
  - D-Pad: `LeanFingerDown`, `LeanFingerUpdate`, `LeanFingerUp` on the DPad root → calls into `VirtualDPadLean.OnFinger*`.
  - Buttons / panels / modal tabs / cards / close: `LeanFingerTap` → callbacks (`OnUnleashLeanTap`, `OnBuildLeanTap`, `OnSkillLeanTap(slot)`, `OnLeftPanelLeanTap`, etc.).
- `HUDManager` ensures a `LeanTouch` instance exists (adds if missing).
- Multi-touch supported (move + tap skill at the same time works).
- No `Button`, no `onClick`, no `Input.Get*`, no Input System polling for HUD actions.
- D-Pad outputs via `VirtualDPadLean.Move` (static `Vector2`, zero when released) + internal event.

**Integration with locomotion**:
- `HeroLocomotion.ReadMoveInput()` already ORs `VirtualJoystick.Move`.
- Added loose reflection helper `ReadHudDpadMove()` that does `Type.GetType("DeNelle.HUD.VirtualDPadLean, DeNelle.HUD")` + static `Move` property read. No hard asm reference from Village → HUD. When the rich HUD is in the scene, D-Pad movement drives the hero.

## Tying into Existing Architecture (no regression)
- **Events / public API** (preferred for loose coupling):
  - `BuildRequested`, `AbilityRequested(int slot)` — existing systems can subscribe.
  - Public setters: `SetTreeHp(current, max)`, `SetHeroHp`, `SetAllyHp(index, cur, max)`, `SetResources(food, wood, iron, crystals)`, `SetWave`, `SetEnemyDirectionDots(float[] anglesDeg)`.
- **Loose reflection wiring inside HUDManager** (same pattern as `HeartHudBridge`, `WaveHudBridge`):
  - On enable it scans for `EconomyService` (or `Economy`) by `Type.Name`, subscribes to `OnChanged` (snapshot with Wood/Food/Iron/Crystals), calls `SetResources`.
  - Scans for `HeartController`, subscribes to `OnHealthChanged(float)`, calls `SetTreeHp` (scale 0–100 matches existing bridges).
  - Scans for `WaveManager`, subscribes to `OnWaveStarted(int)`, seeds compass dots.
- **Ability / Build / Wave actions on tap**:
  - Skill taps → reflection `Find by name "HeroAbilities"` → `CastAbility` / `ActivateAbility` / `UseAbility`(slot).
  - Build tap → fires event + reflection to `BuildModeController.EnterBuildMode` (or falls back to self modal). Existing `BuildButtonBridge` / `LeanTouchBuildDriver` continue to work.
  - Unleash → reflection `WaveManager.StartWave` / `BeginLoop`.
- **Cross-assembly rules obeyed**: `DeNelle.HUD` asmdef references only `DeNelle.Core`, `DeNelle.Data`, Unity UI/TMP, Lean* packages. **No reference to `DeNelle.Village`**. All Village → rich HUD pushes happen either via public setters called by battle controllers or via the same name+reflection bridges already used for the lean `VillageHudController`.
- Coexists with `CoreServices.Hud` (`IVillageHud`) / lean `VillageHudController`. Do not register the rich HUD as the Core `Hud` unless you extend `IVillageHud` for battle-specific methods.

## Setup & Prefab
1. Ensure Lean Touch is in the project (it is) and the three asmdef refs (`LeanTouch`, `LeanCommon`, `CW.Common`) are present in `DeNelle.HUD.asmdef` (they are).
2. In a battle/map scene (e.g. PatriciaLight, a future Defend-the-Tower variant, or any test scene): create an empty GameObject, add `HUDManager`. Play → full rich HUD appears.
3. To make a reusable prefab:
   - Enter Play Mode (or use a small editor utility to call `BuildUI` in edit time if you expose it).
   - Select the generated `HUD_Root` (or the GO with HUDManager) in the Hierarchy.
   - Drag to `Assets/Prefabs/` or `Assets/_Modules/HUD/Prefabs/` as `HUD_Root.prefab`.
   - The D-Pad child already carries the `VirtualDPadLean` + LeanFinger* components from runtime wiring (re-apply the LeanFinger* components on the prefab root/DPad child if you want them serialized; the manager re-attaches at runtime safely).
4. For scenes that already have a `LeanTouch` (most battle scenes via `LeanTouchBuildDriver` etc.), the manager only adds if missing.
5. Portrait 1080x1920 reference scaling is set on the CanvasScaler (match 0.5). Test on device/emulator at target aspect.

## Class-specific Skills
- `_heroClass` string drives the diamond labels (Knight / Healer / Wizard provided).
- Extend the `_classSkills` dict or drive `_heroClass` from real hero data (`HeroAbilities._heroClass`, `AbilityCatalog`, or a `HeroController` class id).
- Slots map to existing ability indices (0=ULT/Top, 1=Primary/Right, 2=Secondary/Bottom, 3=Utility/Left). Taps call the existing cast method with that index.

## Real-time Updates & Compass
- Resources / HP / wave via the public setters (or the loose reflection subs inside the manager).
- Compass enemy dots: call `SetEnemyDirectionDots(new float[] { 12f, 85f, ... })` from a WaveSpawner / perception system (angles 0° = north/+Z). The manager animates positions + pulse.
- Needle rotates with a slow scan + time offset (easy to drive from hero forward or average threat later).

## Performance / Mobile Notes
- Pure uGUI, no UI Toolkit at runtime (per project rule).
- Minimal Update work (only cooldown pulse, needle, dot positioning, unleash scale — all cheap).
- Lean Touch is the project standard for mobile (already used by `LeanTouchBuildDriver`, aim drivers, etc.).
- Thumb areas intentionally oversized.

## Coexistence with Lean Vital HUD
- `VillageHudController` (the slim "key vital information only" bar) remains the `IVillageHud` implementation for the main Village scene.
- This rich HUD is additional / scene-specific. Do not put both on top of each other in the same view without z-order or camera culling adjustments.
- Crafting stays behind shops (Yarn + `NPCCommandBridge` + `ShopPanel`). Resource gathering stays in the world (Harvest sites, nodes). This HUD surfaces only the vital + command surfaces (build, skills, wave, D-Pad, selection).

## Testing Checklist (Acceptance Criteria)
- [ ] Portrait on device/emulator (1080x1920 or close) — no clipping, proper scaling.
- [ ] All elements interactive, thumb >48dp.
- [ ] Hero frame tap toggles Build button ↔ glowing skill diamond with correct per-class labels.
- [ ] Skill taps call existing ability system (no console error, VFX/attack fires).
- [ ] Build button either opens existing `BuildModeController` flow or the self modal (tabs switch, cards respond).
- [ ] D-Pad: smooth constrained knob, outputs normalized Vector2, moves hero (via the reflection path in `HeroLocomotion`).
- [ ] Unleash starts wave (via reflection or event).
- [ ] HP bars, resources, compass dots, wave info update live from real events (Economy, Heart, Wave) or bridges.
- [ ] Lean Touch only — no conflicts with existing Lean drivers in the scene.
- [ ] Zero compile errors/warnings for Android + iOS Player Settings.
- [ ] No runtime console errors in the flows.
- [ ] Self-contained drop-in (works in multiple scenes without scene edits beyond adding the component).

## Known Next Polish (optional follow-ups)
- Drive `_heroClass` + real hero portrait from `HeroBodySwapper` / `HeroHealth` / `GearLoadout` at runtime.
- Populate build modal grid from real `BuildPaletteUI` / catalog data + costs (Economy spend on tap).
- Feed real enemy positions/angles from `WaveManager` / `EnemyGroupSpawner` / perception into `SetEnemyDirectionDots`.
- Add a small `BattleHudBridge` (in Village or a battle module) that finds `HUDManager` by name and pushes HP/resources on the same reflection pattern.
- Pre-bake a `HUD_Root.prefab` with the Lean components already serialized on children (manager can skip re-attach if present).
- Rotating needle driven by average threat or hero aim instead of time.
- Resource pop animations on change (scale punch on the `TextMeshProUGUI` rects).

## References
- Updated WORK ORDER HUD-001 (full visual + Lean + integration spec).
- `docs/LEANTOUCH_NOTES.md` (project usage of LeanFinger*, static events, asmdef requirements).
- `Assets/_Modules/HUD/README.md` (module map — lean `VillageHudController` is still the documented unified village HUD).
- Heart/Wave/HeroAbilitiesHudBridge + `CoreServices` for the established loose wiring pattern.

Contact: Samantha / Grok for review. Iterate live.

**Sign-off only after**: clean Android/iOS builds (zero errors/warnings), manual verification on device/emulator of all flows, Lean exclusively handling input, no regression in core gameplay (locomotion, abilities, build, waves, economy).