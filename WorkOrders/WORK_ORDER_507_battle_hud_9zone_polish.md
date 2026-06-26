# WORK_ORDER_507 — BATTLE HUD: 9-zone polish (first pass)

**Status:** READY TO IMPLEMENT (all decisions resolved owner+Grok 2026-06-24) · HUD/Presentation lane · 2026-06-24
**Origin:** owner directive + Grok Prompt-2 brief, synthesized against the real code (SME extrapolation, not a
verbatim copy). Builds on `WORK_ORDER_498` (the authoritative 9-zone mockup spec).

## 1. Goal
Take the WO-498 9-zone HUD from BONES to a sleek, premium, mobile-first battle HUD — dark glassy chrome, role
readability, dynamic ability arc with radial cooldowns — without breaking the logic/presentation split.

## 2. Current state
- `Assets/_Modules/Village/Arena/BattleHud9Zone.cs` — the 3x3 zone scaffold, FLAG-GATED `FeatureFlags.BattleHud9Zone`
  (default OFF; preview `PlayerPrefs ff.battlehud9zone=1`). `Build()` calls `BuildZone1HeroPlate()` ... per zone.
- `Assets/_Modules/Village/Arena/BattleArenaHud.cs` — the current minimal LIVE overlay (title + enemy HP + Flee
  + victory/defeat banner + stars). Decision needed: does the 9-zone REPLACE this, or layer over it? (see 5d)

## ★ AUTHORITATIVE MOCKUP (owner 2026-06-24 — sketch + Grok style render) ★
This supersedes the generic zone list below where they differ. Build the LAYOUT BONES to this.
**Core rules:** dark semi-transparent panels · **anchored TIGHT to screen edges/corners, compact** ·
**NO overlap** with the center or each other · the **center stays clear** (the fight/backdrop shows through).
- **Top-Left** — Player stats (HP red bar + a second resource/level bar) + **BUFFS** row (gear/consumable buff icons).
- **Top-Center** — **TARGET** enemy name + **lock state** (Locked/Unlocked) + a lock toggle button.
- **Top-Right** — **Settings** gear + **FLEE** (Flee lives here now, top-right — move it from BattleArenaHud's top-left).
- **Mid-Left** — **TARGET CYCLE** list: a vertical list of the enemies (portrait + name + HP bar each) to cycle/select targets. Dynamic from the spawned family (Tank/DPS/DPS/Healer).
- **Mid-Center** — EMPTY (fight shows through).
- **Mid-Right** — **FOCUS AREA**: heal-toggle (toggle 1-2 skills to healing), Attack, and a mode switch (attack / ranged / spell, "off-tree"). [NEW systems — placeholder buttons for now.]
- **Bottom-Left** — **Virtual D-PAD / joystick** (movement) + a couple small utility icons.
- **Bottom-Center** — utility row: **Potion · Rapid Heal · Desperate WS** + **Stars earned + timer-to-keep-star-level**. [Potion/heal/desperate = NEW consumables, placeholder; stars+timer = wire to BattleStarRating/the time-box.]
- **Bottom-Right** — **ABILITY ARC** (NOT a 12-slot grid — owner: "we don't need all those slots"): big **THRUST** anchor at the far-right (basic attack), the actual unlocked abilities (Q/W/E/R, skill-tree-driven) **arcing up-left** from it, each a disc + icon + radial cooldown ring.
**Wire-now (has data):** HP bar, target+lock, the ability arc (THRUST + unlocked abilities), settings, Flee, d-pad, stars+timer.
**Placeholder (new systems to build later):** buffs row, full target-cycle interactivity, focus-area mode-switch, Potion/Rapid Heal/Desperate WS consumables.

## 3. The 9 zones (per WO-498 — implement as anchored RectTransforms)
1 Top-Left Knight plate (name + green HP + shield + resource pips) · 2 Top-Center enemy family overview (role
chips Tank/Healer/Wizard/DPS + mini HP + icons, dim-on-death) · 3 Top-Right timer + pause · 4 Mid-Left current
target portrait + role · 5 Center EMPTY · 6 Mid-Right quick-focus buttons · 7 Bottom-Left virtual joystick ·
8 Bottom-Center Basic Attack + weapon skill · 9 Bottom-Right ability ARC (circular buttons + radial cooldown
rings, dynamic from skill tree).

## 4. KEEP (must hold)
- **Code-built uGUI** (Canvas, Screen Space - Overlay) — **NO UIToolkit/UXML** (does not ship in builds, §8).
- **Logic/presentation split** — the HUD READS state + fires existing public intents only: `HeroHealth` (zone 1),
  `HeroAbilities`/`AbilityCatalog` (zones 1/8/9), `HeroTargetIndicator` (zones 4/6), `Enemy`+`EnemyBrain.Role`
  (zones 2/4). It owns no game logic.
- **Mobile-first** (joystick, big tap targets) + **desktop kept** (WASD + Tab-cycle + ability hotkeys).
- **Role readability #1** + radial cooldown rings + dynamic ability bar (unlocked skill-tree abilities populate it).
- Icons from real catalogs (don't placeholder): `RpgUiCatalog.cs`, `ItemIconCatalog.cs`, `abilities.json`
  (per-ability `icon` glyph + `color` hex). Role colors: Tank gray/blue, Healer green, Wizard purple, DPS red.

## 5. RESOLVED (owner + Grok, 2026-06-24) — these are now spec, not options
a. **Generic dynamic role chips — YES.** Build the family-overview chips DYNAMICALLY from whatever enemies
   actually spawned (read `EnemyBrain.Role`/equivalent). Do NOT hardcode a Healer if it's not in the family.
b. **Resource pips — DROP for the first pass.** The Knight runs on cooldowns (no mana pool). Zone 1 = a clean
   HP bar only. (Later: repurpose pips for ability charges / a focus meter IF such a resource exists.)
c. **Joystick — visual + INPUT.** Hook the visual joystick to drive the existing locomotion via touch-drag.
   Desktop keeps WASD.
d. **REPLACE BattleArenaHud — YES.** The 9-zone HUD replaces the minimal overlay; fold the encounter title,
   primary enemy HP, Flee button, and victory/defeat+stars result banner INTO the new zones.
e. **Icons — use what exists.** Pull from `RpgUiCatalog`, `abilities.json` (glyph + color), and the existing
   spell-icon sheets. Design to available assets — assume NO new full sprite set.

## 6. Acceptance criteria
- All 9 zones build as anchored RectTransforms, dark glassy chrome, large touch targets; role chips readable
  with icon+color; ability arc populates DYNAMICALLY from the unlocked ability set with working radial cooldown
  rings; desktop path (WASD + hotkeys) intact. Gate-clean.
- BONES vs FINESSE: structure + wiring + dynamic population are CLI gate-provable; exact pixel positions, sizes,
  glassy look, and joystick feel are OWNER felt-tuned live (the consts/anchors are exposed).

## 7. Do NOT touch
The combat systems (read-only), the fight lifecycle, gear JSON. Keep it behind `FeatureFlags.BattleHud9Zone`
until the owner blesses it on.
