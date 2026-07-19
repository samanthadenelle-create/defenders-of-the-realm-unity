# WORK ORDER 750 — Right ActionBar naming + ability ids + Warden's Grace redesign + R clip rebind

**Status:** SPEC — READY TO IMPLEMENT (owner rulings 2026-07-19). Awaiting owner go (needs 2 clip IDs confirmed).
**Classification:** MIXED — naming/id-mint (mechanical) + E ability REDESIGN (new feature) + R anim rebind.
**PO:** Sam. **Source of truth:** `docs/reference/HERO_ANIMATION_DICTIONARY.md` (the known dictionary).

## Owner rulings captured
The live Right ActionBar is **ATTACK pill + Q/W/E/R = 4 skills** (settles the 3-vs-4 drift: it is 4).
Canonical names + animations:

| Slot | Name | Animation | Ability id |
|---|---|---|---|
| ATTACK pill | **Sword Wielding** | atk_slashright/left/spin (basic 3-swing) | none (pill, keep) |
| Q | **Sword Heroic** | fist_flyingkick_m | `knight.q` (exists) |
| W | **Shield Charge** | fist_whirlwindkick_m | **mint `knight.w`** |
| E | **Warden's Grace** | **Mage Spell Cast 5** | **mint `knight.e`** |
| R (ult) | **Radiant Strike** | **Jump into Slash Up** | **mint `knight.r`** |

## Work
1. **Names -> HUD/data.** Apply the 5 canonical displayNames (HUD medallion labels + `abilities.json` knight rows). ASCII-only.
2. **Mint ability ids** `knight.w` / `knight.e` / `knight.r` in `abilities.json` (both dual copies) — W/E/R currently have NO `id` (only Q does), so they can't be addressed/hot-swapped by id. Preserve existing effect/dmg/cd/mana.
3. **E = Warden's Grace REDESIGN** (was taunt -> hybrid support; `HeroAbilities.cs` + `abilities.json`):
   - Instantly heal target ally (or self) **25% max HP + a bonus scaled by Knight Defense**.
   - Apply **Grace Shield** 8s: **-20% incoming damage** + **HoT 5% max HP / 2s**.
   - Cooldown **18-22s** (scales w/ upgrades/pet rarity); **low-to-medium mana**.
   - Progression: upgrades raise heal %, cut cd, or add a team-wide mini-shield at higher tiers (early via knight pet/class tree).
   - **VFX:** radiant golden light from sword/shield, floating runes + particle beams to allies, subtle mobile screen-glow (optimized). Reuse the pooled VFX system (One Model / pooling law); no new double-stack.
   - **Animation:** rebind knight `castHeal` in `motion-castings.json` to **Mage Spell Cast 5** (resolves the old mismatched-heal-clip flag — a mage-cast gesture is now apt).
4. **R = Radiant Strike anim rebind** to **Jump into Slash Up** — replaces the `fist_whirlwindkick_m` clip currently shared with W (the flagged dup). Add/point a knight skill variant to the new clip in `motion-castings.json`.

## BLOCKERS to confirm before implementing (clip identity)
- **"Mage Spell Cast 5"** and **"Jump into Slash Up"** are owner-described motions, not verified file names. CLI must locate the actual clip assets (mocap/motion set) OR the owner points to the exact `.fbx`/`.anim`. Do NOT guess-bind a wrong clip (§12). If absent, flag for import.

## Acceptance
- 5 skills carry their canonical names in HUD + data; W/E/R have ids; hot-swap can address them.
- E performs heal + Grace Shield (HoT + -20% dmg), on the Mage Spell Cast 5 clip; R plays Jump into Slash Up (no W dup).
- Gate: `COMPILE_GATE_OK` + DataRegression `REGRESSION_OK` (no new red); the anim/ability oracles green.
- `docs/reference/HERO_ANIMATION_DICTIONARY.md` matches the shipped state.

## Do NOT
- No UXML; ASCII-only; do not source Grom clips from the DEAD Paladin `weaponskill-animations.json` lane.
- Keep E's redesign behind felt-verify — combat feel is the owner's call.
