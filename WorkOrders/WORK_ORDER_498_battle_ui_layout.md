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

## Notes
- Mobile-first: large tap targets, cooldown-ring overlays, the joystick. Desktop falls back to existing
  WASD + Tab-cycle + the ability hotkeys (don't break the desktop path).
- Reuse `VillageHudController` icon-button + cluster helpers where possible; the battle HUD is its own mode
  (BattleHudVisibilityManager Battle mode, now firing in the arena via BattleLock).
- This is the felt-tuning-heavy layer (button placement/size, joystick feel) — build the layout, then the
  owner tunes positions live. The mockup is the north star.
