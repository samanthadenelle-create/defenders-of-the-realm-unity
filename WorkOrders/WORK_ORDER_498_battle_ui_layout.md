# WORK_ORDER_498 — BATTLE UI LAYOUT (mobile combat HUD — target mockup)

**Status:** SPEC · HUD/Presentation lane · owner mockup 2026-06-23 (the visual TARGET)
**Goal:** a visually appealing mobile battle HUD. The mockup is INSPIRATION, not a literal copy — owner:
"not everything is applicable." The CORE asks distilled:
1. **ROLE READABILITY — the #1 thing:** at a glance, tell WHO is the Healer vs DPS vs Tank vs Wizard
   (role icon + color on/above each enemy; a small top legend optional). This is the real value.
2. **Visually appealing** battle UI (polished, not programmer-art).
3. **Ring cooldowns** on the ability buttons (radial cooldown overlay).
Mostly a LAYOUT/SKIN pass on EXISTING systems (see WO-497 reuse map), not new logic. Code-built UI (UXML
does NOT ship — CLAUDE.md §8). Lives on `BattleArenaHud` / the battle-mode HUD.

## The mockup (owner 2026-06-23) — layout spec
- **Top-left — hero plate:** "Knight" name + a shield/role emblem + a big green HP bar (sci-fi/gilt frame).
- **Top-center — enemy ROLE LEGEND:** a row of role chips: Tank (shield), Healer (green cross), Wizard
  (purple staff), DPS (red daggers) — each labeled. A readability key for the family. (+ a small extra icon.)
- **Right edge — vertical icon RAIL:** settings gear on top + a few stacked icons (de-emphasized, far-right —
  matches the owner's "settings far right" HUD edit). Slim vertical strip.
- **Center — enemies in the field:** each enemy carries a floating HP bar (red current / blue chip) AND a
  small ROLE ICON above it; a ground reticle/ring under the locked/target unit.
- **Bottom-left — movement:** a round virtual JOYSTICK + two small round buttons above it (profile, menu/list).
- **Bottom-center — "Basic Attack"** pill button (wide, dark, gilt text).
- **Bottom-right — ABILITY ARC (skill-tree-driven):** buttons fanned bottom-right, each a colored disc with a
  CLEAR DISTINCT icon + a radial COOLDOWN RING + label. **The abilities come from the SKILL TREE — the
  player's UNLOCKED skills populate the bar (owner 2026-06-23), NOT a hardcoded 4.** So the bar is dynamic:
  it reads the hero's unlocked ability set and renders a button per ability (Dash/Knockback/Taunt/Ultimate are
  the V1 Knight examples). Each needs a clear, readable icon so the player instantly knows what each does.

## Reuse vs build (per WO-497)
- **EXISTS — reuse/skin:** enemy + hero HP bars, damage numbers, hit-flash/shake/hit-stop, the Q/W/E/R
  ability framework (`HeroAbilities`/`AbilityCatalog`/`HeroAbilitiesHudBridge`), `HeroTargetIndicator` reticle.
- **BUILD (layout):** the mobile combat HUD arrangement — joystick (mobile move; desktop keeps WASD), the
  4-button ability ARC bottom-right with cooldown rings + icons, the "Basic Attack" pill, the enemy role-icon
  frames (role icon above each enemy's existing HP bar), the top role legend, the hero plate styling.
- **AUTHOR (data/art):** the 4 ability buttons' icons + the role icons (Tank/Healer/Wizard/DPS) + the Knight
  kit `AbilityDef`s in `abilities.json` (WO-494). Role colors: tank gray/blue, healer green, wizard purple, DPS red.
- **DESIGN CALL (owner):** the mockup shows a HEALER — our family is mage/tank/warrior. Add a healer role, or
  relabel (the legend is generic; the spawned family is what matters).

## ★ EXACT WO (owner 2026-06-23) — 9-ZONE LAYOUT (authoritative build spec) ★
Mobile-first battle UI, solo Knight vs family (Tank/Healer/Wizard/DPS). Isolated open battle scene, NO
companions. Landscape-focus (portrait-friendly). Sleek premium fantasy, **dark semi-transparent HUD** so the
backdrop shines through. Only build: **skills, weapon skills, enemy mapping/designation.** Unity UI Canvas
(Screen Space - Overlay). Minimal, LARGE touch targets, high contrast, readable small-screen. Mockup =
`Resources/Arena/Backdrops/aU9vc` (the reference image). **OVERNIGHT = the BONES (assemble to this spec, wire
to existing systems); FINESSE (exact look/feel) = together tomorrow.**

Divide the screen into a 3x3 grid; each zone = its own anchored RectTransform container:
1. **Top-Left — Knight Health + Resources:** shield-emblem + name "Knight" + big green HP bar (gilt frame);
   resource pips below. Anchor top-left. Reuse `HeroHealth` for the bar.
2. **Top-Center — Enemy Family Overview:** 4 role chips (Tank shield / Healer green cross / Wizard purple
   staff / DPS red daggers) each with a mini HP bar + label. Anchor top-center, horizontal row. This is the
   role-designation key. Dynamic: chip dims/greys when that role is dead.
3. **Top-Right — Timer / Wave info + Pause:** the time-box countdown (WO-494 star timer) + a pause button.
   Anchor top-right. (Mockup's right rail = Settings + Audio toggle here too, minimal.)
4. **Middle-Left — Current Target Info:** big enemy PORTRAIT + role of the locked target (HeroTargetIndicator).
   Anchor mid-left. Updates on target change.
5. **Center — battle scene:** LEAVE EMPTY (the environment/fight shows through). No HUD here.
6. **Middle-Right — Quick Focus buttons:** tap to prioritize a role ("Focus Healer" / "Focus Wizard"). Anchor
   mid-right vertical stack. Sets HeroTargetIndicator's locked target.
7. **Bottom-Left — Movement Joystick:** round virtual joystick (mobile); desktop keeps WASD. Anchor bottom-left.
8. **Bottom-Center — Basic Attack + Weapon Skill:** wide "Basic Attack" pill + (if separate) a weapon-skill
   button. Anchor bottom-center.
9. **Bottom-Right — 4 large Ability Buttons:** Dash / Knockback / Taunt / Ultimate, each a colored disc +
   icon + **radial COOLDOWN RING** + label, fanned in an arc. Anchor bottom-right. **Skill-tree-driven**
   (unlocked abilities populate the bar; the 4 are V1 Knight examples). Dynamic: cooldown ring sweeps + dims.

**ICONS — use the existing CATALOGS (owner 2026-06-23, don't placeholder):** `Assets/_Modules/Core/UI/RpgUiCatalog.cs`
(IconSword/IconShield/IconCombat/IconHeart/RoleIcons sprite sheets) · `Assets/_Modules/Village/Hero/ItemIconCatalog.cs`
(gear) · `abilities.json` already has per-ability `icon`(glyph) + `color`(hex). The ability buttons + role chips
should pull sprites from these. ALSO search for the fuller spell-icon / weapon-skill-icon sheets the owner says
exist (grep Resources for spell/skill icon sheets) and wire those for the ability/weapon-skill buttons.

Per zone deliver: exact UI elements · anchoring/sizing · fantasy colors/icons (placeholder ok for finesse) ·
dynamic behavior (cooldown sweep, HP fill, dim-on-death). Role colors: Tank gray/blue · Healer green ·
Wizard purple · DPS red. Code-built (UXML doesn't ship). Wire to existing systems (WO-497): HeroHealth,
HeroAbilities/AbilityCatalog (ability buttons + cooldowns), HeroTargetIndicator (target/focus).

## Notes
- Mobile-first: large tap targets, cooldown-ring overlays, the joystick. Desktop falls back to existing
  WASD + Tab-cycle + the ability hotkeys (don't break the desktop path).
- Reuse `VillageHudController` icon-button + cluster helpers where possible; the battle HUD is its own mode
  (BattleHudVisibilityManager Battle mode, now firing in the arena via BattleLock).
- This is the felt-tuning-heavy layer (button placement/size, joystick feel) — build the layout, then the
  owner tunes positions live. The mockup is the north star.
