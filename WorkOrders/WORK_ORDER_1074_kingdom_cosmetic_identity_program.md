# WORK ORDER 1074 — The cosmetic identity program: Kingdom Themes + Heart Aspects + Heraldry

**Status:** SPEC / PROGRAM AUTHORITY — the durable registry for all cosmetic monetization. Individual
packs ship as child tickets; nothing here is implementable until the cosmetic render rail exists
(WO-1176 §4 companion is the pathfinder).
**Minted:** 2026-08-24 (UI seat), banner header bumped 1069 → 1075 in the same edit (with 1069–1073).
**Provenance:** the second external-review paste the owner ADOPTED 2026-08-24 (*"less 'skin shop,'
more medieval luxury real estate with bragging rights"* · *"My kingdom. My Tree. My banner."*).
Refined for canon + this codebase per the standing Grok-draft flow.

---

## 1. The program thesis

Cosmetics sell **ownership of a visual identity inside the world**, not items. A pack is a coherent
faction fantasy across every surface the player already looks at — never *"six unrelated JPEGs in a
medieval loot sack."* This is the years-long content engine that never needs to sell combat power,
and it is what the WO-1073 Patronage ladder and the WO-1070 Vow draw their unlocks from.

## 2. The three reinforcing systems (build in this order)

### 2a. Kingdom Collections — themed everything ($14.99–$29.99, the sweet spot)

⭐ **THE COLLECTION TEMPLATE (adopted refinement, 2026-08-24 third paste): every major collection
spans exactly FIVE SURFACES with one consistent visual language** — this is the authoring schema,
and a pack missing a surface is not a Collection, it is an Accent Pack:

| Surface | What it skins |
|---|---|
| 1. **Heart Aspect** | the Heart of Elarion treatment (layered over tier — §2b) |
| 2. **Castle Architecture** | walls, gates, towers, braziers/decorations |
| 3. **Heraldry** | crest + banner components (§2c pieces, themed) |
| 4. **Army Identity** | troop shields, pennants, insignia |
| 5. **Profile Prestige** | animated frame + an exclusive **title** |

**The adopted starter five, with their worked identities and titles:**

| Collection | Heart | Castle | Heraldry | Title |
|---|---|---|---|---|
| **Hollow Court** | spectral green, dead-root | gothic stone, pointed towers, ghostfire braziers | raven/skull crest, black-green banners | *Lord of the Hollow* |
| **Ember Throne** *(renames the earlier "Ember King" draft)* | molten-root | black volcanic stone, glowing fissures | ember crown | *Ember Sovereign* |
| **Arcane Dominion** | floating runes, crystal growths | magical spires, hovering fragments | arcane sigils | *High Arcanist* |
| **Ironhold** | forged-metal bands, furnace glow | dwarven fortress | hammer/mountain | *Lord of Ironhold* |
| **Frostbound** | frozen crystalline limbs | snow/ice fortress | wolf / frost crown | *Frostwarden* |

(**Golden Age** — white stone + gold trim + marble — stays reserved as the premium prestige
aesthetic; *"wealth without saying '+20% damage because Mastercard'"*.)

⛔ **THE PRESTIGE-EXTENSION RULE — the whale tier is the SAME fantasy, dramatically different
flex.** A $49.99+ prestige pack in a family adds exclusive pieces the $9.99–$29.99 tiers can NEVER
assemble by repeat purchase — e.g. Ember Throne prestige adds an animated lava moat + throne
monument + Heart crown effect + unique arrival animation + legendary heraldry + kingdom sky
treatment. This is WO-1070's uniqueness test applied to cosmetics, and it is exactly the
differentiator the original Founder's Vow promised and never authored (`cosmetics: []`).

⭐ **The dead clone SKUs get reborn here**: `frostfall-bundle` / `embergrove-bundle` /
`bloomtide-bundle` (WO-1165 §8 — identical contents, three ids, one product) become genuine
**Accent Packs** ($4.99–$9.99: banner + frame + shield emblem + one Heart particle) for their
matching seasons/themes. Ids are frozen; contents change.

### 2b. Heart of Elarion Aspects — the collectible centerpiece
⛔ **Canon naming: the tree is the HEART OF ELARION** (CLAUDE.md §7) — the advisor's "Tree of Life"
does not enter data or player copy. Aspects: Bloomtide / Ember / Frost / Astral / Hollow / Storm /
Crystal / Moonlit / Solar / Autumn Ancient (owner curates the launch set).

⛔ **An Aspect LAYERS OVER progression, never replaces it.** A player at Heart stage 5 who buys
Frostbound gets a *stage-5 Frostbound Heart* — the aspect is a reskin function of the current tier
visual, so cosmetics compound with play instead of erasing it. This is the stickiness mechanic and
it is structural: implement as tier-visual × aspect, never as N fixed models.

### 2c. Heraldry — a component system, not skins
Banner = **shape × fabric × emblem × trim × animation**, each a data-authored component; packs and
milestones grant *components*, and players compose. Rare components (the Founder crest, gold
embroidered trim, animated shimmer) are the collector hook — combination beats "equipping Skin #17".
Surfaces, added incrementally: above the castle → troop formations → raid loading screen →
leaderboard cards → profiles → victory screens. **One purchase, seen everywhere — that is the
economics of the whole category.**

## 3. Price architecture (slots into WO-1165 §12's four lanes)

| Tier | Product | Lane |
|---|---|---|
| $4.99–$9.99 | Accent Packs (banner + frame + emblem + one particle) | 🏰 Permanent |
| $14.99–$29.99 | Kingdom Collections | 🏰 Permanent |
| $49.99+ | Prestige Collections — **Founder's Citadel** is WO-1070's §2 | 👑 Patronage |

## 4. ⭐ The canvas mechanic — buy the theme, PLAY it spectacular

Premium collections gain additional visual pieces through **cosmetic achievements, never money**:
own Hollow Court → 100 raid wins unlock spectral ravens → 500 unlock ghostfire fog → restoring the
Heart unlocks the final transformation. A whale buys the canvas but cannot purchase the finished
trophy. Zero stats, zero tempo — but it needs the same oracle discipline as WO-1073 §3.1: the
achievement-unlock table must be assertable as cosmetic-only. Stat sources exist (raid scoring, the
Defense Report, Heart tier) — unlock triggers read them, never add loops.

## 5. ⛔ Constraints (the ones this program will trip without)

1. **One appearance owner.** Every applied cosmetic routes through `CosmeticApplier` /
   `CosmeticOwnershipService`; world-following bodies through the WO-1176 §4a rule. A theme pack
   touching walls, gates, the Heart and troops is the maximal temptation to add parallel skinners —
   forbidden (CLAUDE.md §7 pattern, `EchoWorldPresenceRegression` precedent).
2. **All art on the CDN** (§16): content-hashed bundles, every build its own `r2-ship.ps1` push —
   and these are PAID assets, so the WO-1176 §4b rule binds: a purchased cosmetic that fails to
   resolve is TOLD to the player and retried; never a silent fallback.
3. **Theme identity must not live in hue alone.** The owner is colourblind and cannot QA a
   hue-keyed theme; more importantly a theme readable only by palette fails for colourblind players
   too. Every collection carries silhouette/material/motif identity (Ironhold = rivets + smoke,
   Hollow Court = ravens + dead trees) with palette as reinforcement. Visual creative is delegated
   (standing memory) — the owner rules on BEHAVIOUR and fantasy, never on hues.
4. **Wall/gate skins are VISUAL ONLY** — placement footprints, pathing, and the wall-gap rules
   (`_heightCadence`: walls deliberately excluded from fit) are untouched by any skin. A theme that
   narrows a wall reopens the pathable-gap save-break.
5. Purchase limits (WO-1176 §3) precede any one-time collection; entitlements wallet-keyed.

## 6. Child tickets (mint when their prerequisites land)

Companion rail (WO-1176 §4) → first Accent Pack (rebirth one clone SKU) → Heraldry v1 (castle
banner only) → first Kingdom Collection → Heart Aspects → canvas achievements.

## 7. Acceptance (program-level)

- [ ] Every cosmetic row resolves through the one ownership/application rail; no second skinner
- [ ] Aspect × tier layering proven: upgrading the Heart while an Aspect is active re-renders as
      the new stage in the same Aspect
- [ ] A collection reads correctly in greyscale (silhouette/motif carry identity)
- [ ] Achievement-unlock tables oracle-asserted cosmetic-only
- [ ] No player-facing string says "Tree of Life"
