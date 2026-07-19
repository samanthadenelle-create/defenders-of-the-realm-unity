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

## Mobile-input ruling (owner, 2026-07-19)
This is a MOBILE game -> keyboard quick-keys have no player value. **Remove all Q/W/E/R key-letter
labels from the HUD medallions** — the ability ICON is the identity (name optional on tap-hold). Distribution is mostly the Android APK, so
touch is the real input; keep the keyboard/gamepad bindings in code as a free fallback for the PC minority
+ headless/dev testing, but NEVER surface a key letter on the touch HUD. The
"Q/W/E/R" in the dictionary are INTERNAL slot ids (0-3) only. Open question flagged for the owner:
whether the player-assignable Hot-Swap bar (`AssignableSkillBar`, a keyboard-MMO idiom) stays on mobile
or is replaced by a fixed icon ability row.

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

## Icon assets (owner-provided 2026-07-19)
Owner supplied a pixel-art ability icon sheet: `C:\Users\Elden\Downloads\grok-image-48d1bd26-dd5c-490e-ac01-b4db9a54f764.png` (2 cols x 3 rows). Cell -> ability:
Sword Basic Attack -> Sword Wielding · Flying Attack -> Sword Heroic (Q) · Shield Charge -> Shield Charge (W) · Warden's Grace -> Warden's Grace (E) · Warden's Grace (2nd, alt variant) -> spare (owner picks one) · Radiant Strike -> Radiant Strike (R).
- **Task:** slice the sheet into 5 per-ability sprites, import to `Resources/HudIcons/<ability>` (owner to move the PNG into the project first, or CLI copies it in), set each `abilities.json` knight row `iconPath` + the HUD medallion icon. All 5 abilities are covered; drop the duplicate Warden's Grace.
- ASCII-only ids; keep the sprite import mobile-safe (ETC2/point-filter for pixel art, no blur).

## Ability SFX (owner, 2026-07-19) — one simple, non-annoying sound each
Assign each ability a soft activation sound, REUSING existing `SfxClipLibrary` clips (no new audio to
source). The knight motions' `sfxId` in `motion-castings.json` are mostly `""` (why Q/W/R are silent) -
fill them (dual-copy):

| Ability | motion / path | sfxId (existing clip) | feel |
|---|---|---|---|
| Sword Wielding (ATK) | attack0 | `SwordSwing` | light swing (already via GameSfx.PlaySwordSwing) |
| Sword Heroic (Q) | knight skill1 | `SwordSwing` | soft leap-swing |
| Shield Charge (W) | knight skill2 | `SwordClash` | muted metallic thud |
| Warden's Grace (E) | knight castHeal | `Heal` | soft chime (already set) |
| Radiant Strike (R) | knight (R variant) | `SpellCast` cast + `Spell_Impact` on land | resonant, not harsh |

**Design rule:** short (<=0.5s), soft attack envelope, low volume, distinct per ability, ONE play per cast
(cooldown-gated - no retrigger spam). WebGL note: verify the chosen clip metas have no divergent platform
overrides (WO-682 SFX_WEBGL_OK); on APK/native this is a non-issue. **Owner felt-verifies "not annoying";
swap the clip id if any grates.**

## Acceptance
- 5 skills carry their canonical names in HUD + data; W/E/R have ids; hot-swap can address them.
- E performs heal + Grace Shield (HoT + -20% dmg), on the Mage Spell Cast 5 clip; R plays Jump into Slash Up (no W dup).
- Gate: `COMPILE_GATE_OK` + DataRegression `REGRESSION_OK` (no new red); the anim/ability oracles green.
- `docs/reference/HERO_ANIMATION_DICTIONARY.md` matches the shipped state.

## Do NOT
- No UXML; ASCII-only; do not source Grom clips from the DEAD Paladin `weaponskill-animations.json` lane.
- Keep E's redesign behind felt-verify — combat feel is the owner's call.
