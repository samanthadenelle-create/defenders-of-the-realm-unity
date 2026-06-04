# Magic VFX Library — the Spells Pack, mapped to gameplay

We already own a full elemental VFX pack at **`Assets/Spells Pack/Particles/Prefabs/`**.
This is the creative menu: what's on disk, and where to drop each piece to get that
"fantasy magic" vibe. No new art needed — it's all here, URP-ready, CC-cheap particles.

> **Why this doc exists:** owner wants "defensive auras + lots of things creative could
> use to add that fantasy magic vibe." This is that menu. Same play as the Quaternius
> town and the Tripo heroes — a cheap pack unlocks a ton of polish.

---

## The shape of the pack

**7 elements**, each a complete kit: **Fire · Ice · Storm · Nature · Dark · Light · Arcane**

| Family | Per element | What it is | Best use |
|---|---|---|---|
| **Auras** | 1 each (7) | Looping ground/body glow | Buff zones, shrines, the Heart, ward stones |
| **Shields** | 1 each (7) | Bubble/barrier | Heart-shielded, hero block, armored-tower upgrade |
| **Tomes** | 1 each (7) | Floating spellbook glow | Ambient magic props, scroll pickups, Arcane Tower |
| **Castings** | 2–4 each (~20) | Charge-up at the caster's hands | Tower fire wind-up, hero cast tell |
| **Projectiles** | 2–4 each (~20) | The flying bolt | Tower shots, hero ranged spells |
| **Explosions** | 2–4 each (~20) | Impact burst | Where the projectile lands |
| **Spells** | 76 base | Full self-contained effects | Hero abilities, ultimates, set-pieces |
| **Variations** | huge | Color recolors (Red/Blue/Green/Purple/Yellow) | Reskin any element to fit a tower/hero theme |

Paths: `Auras/Aura_<Element>.prefab`, `Shields/Shield_<Element>.prefab`,
`Tomes/Tome_<Element>.prefab`, `Projectiles/{Casting,Projectiles,Explosion}/<...>.prefab`,
`Spells/Spell_<Element>[_N].prefab`, `Variations/Spells/<Element>/...`.

---

## Defensive auras — the "fantasy magic vibe" the owner asked for

These are the cheap, high-impact wins. Drop a looping aura on a static object and the
whole scene reads as enchanted:

- **Heart of Elarion** → `Aura_Nature` or `Aura_Light` pulsing at the base of the Tree of Life — the world tree literally glowing.
- **Holy Shrine / buff zone** (DEF-244) → `Aura_Light` (golden) on the ground ring; step in = buffed.
- **Healing well / Heartwood** → `Aura_Nature` (green) soft loop.
- **Ward stones** around the wall → small `Aura_Arcane` (blue) markers — defensive runes.
- **Tower tier-3 glow** → matching-element aura at the tower base to signal "upgraded/charged."
- **Corrupted enemy camp** (DEF-187) → `Aura_Dark` so a hostile camp reads as cursed from range.

## Shields — defensive payoff moments

- **Heart under attack** → `Shield_Light` bubble flashes when the Heart takes a hit (defensive feedback).
- **Hero block / parry** → `Shield_Arcane` brief flare on a successful block.
- **Armored-tower upgrade** → `Shield_Ice`/`Shield_Fire` standing barrier as a visible upgrade state.

## Tower roster VFX chains (DEF-244)

Each elemental tower = one full chain: **Casting** (wind-up) → **Projectile** (travel) → **Explosion** (impact), plus an **Aura** for its T3 glow.

| Tower | Cast | Travel | Impact | T3 glow |
|---|---|---|---|---|
| **Arcane Fire** (the beloved swirling fireball) | `Casting_Fire_2` | **`Spell_Fire_6`** ← owner's "fireball_6" | `Explosion_Fire` | `Aura_Fire` |
| **Frost** | `Casting_Ice_2` | `Projectile_Ice` | `Explosion_Ice` | `Aura_Ice` |
| **Storm** | `Casting_Storm` | `Projectile_Storm` | `Explosion_Storm` | `Aura_Storm` |
| **Arcane Bolt** | `Casting_Arcane_2` | `Projectile_Arcane` | `Explosion_Arcane` | `Aura_Arcane` |
| **Holy** | `Casting_Light_2` | `Projectile_Light` | `Explosion_Light` | `Aura_Light` |
| **Nature/Poison** | `Casting_Nature` | `Projectile_Nature` | `Explosion_Nature` | `Aura_Nature` |

> The Arcane Fire Tower's signature is **`Spell_Fire_6`** — the swirling fireball that grows
> and circles before it lands. Owner-nostalgic; this is the hero VFX of the tower roster.

## Hero / ability VFX

- Map hero abilities onto the **76 base `Spell_*`** prefabs — full self-contained effects, no chain assembly needed. Pick by class theme (Mage→Arcane/Fire, Cleric→Light/Nature, etc.) and recolor via the **Variations** tree to match the hero's palette.

---

## Mobile WebGL budget (the discipline that keeps it cheap)

Particles are cheap **per emitter**, and lavish use on **hero-facing moments** (tower fires,
the shrine aura, the Heart, hero spells) is a big vibe win for near-zero cost. The one real
cost on mobile WebGL is **overdraw** — many big overlapping *transparent* particles eat fillrate.

**Rules of thumb:**
- **Pool everything** through the existing `VfxPool` — never `Instantiate`/`Destroy` per shot.
- Cap simultaneous emitters + per-system particle counts; small/medium auras are basically free, giant full-screen washes are not.
- Reserve the big screen-filling effects for **rare payoffs** (boss, ultimate, wave-clear) — not every frame.
- Keep persistent looping auras **low particle count + modest size** (they run continuously).

Net: **go big on magic, just budget it.** Lots of small/medium auras + tower chains = cheap
and gorgeous; a handful of restrained big ones for the payoff beats.
