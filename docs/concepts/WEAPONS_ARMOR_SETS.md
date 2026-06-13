# Weapons & Armor Sets — Concept Catalog (Tripo Generation Brief)

**Status:** Concept catalog for owner art generation. Not yet bound to any item data.
**Game:** Defenders of the Realm — Unity 6 LTS, URP, mobile target (Seeker tier).
**Purpose:** A ready-to-generate catalog of weapon + armor **sets** for the four hero
classes, plus a tower-imbued elemental weapon trio. Each entry carries a lore line in
the narrative-bible voice and a concise **Tripo-ready text/image-to-3D prompt**.
**Author:** Game-design agent. Owner (Samantha) ratifies all final art calls.
**Date:** 2026-06-13.

**Canon sources read before writing:** `docs/narrative-bible.md` (tone, the Withering,
Heart of Elarion, the Keeper, the three pets), `docs/elemental-codex.md` (the four
schools + their colors), and the live hero/gear code — `HeroAbilities.HeroClass`
(`Knight / Ranger / Mage / Cleric`), `GearLoadout.cs` and the **Aegis of Elarion** set
already wired in code (per-class weapons **Emberbrand** / **Aetherstaff** /
**Heartwood Longbow** / **Hallowed Censer**, all `setId "aegis"`).

> **Naming reconciliation (read this first).** The brief proposed four *new* set
> names (Aegis of Elarion / Frostwarden / Wardweave / Chorister's Vestments). Code
> already ships **one** legendary set called **Aegis of Elarion** whose *armor* is
> shared across all four classes and whose *weapon* is per-class. To stay true to
> canon and avoid a data collision, this catalog treats the four named sets as the
> **per-class signature sets** (the class's themed look + signature weapon), and keeps
> **Aegis of Elarion** as the cross-class **legendary endgame overlay** it already is
> in `GearLoadout.cs`. Where the brief's weapon name differs from the shipped Aegis
> weapon, both are listed and the canon (code) name is marked **[CANON]**. The owner
> decides at generation time which name wins; nothing here changes code.

---

## 1. Shared art style (read before generating anything)

Every item below targets **one** look so the gear reads as a matched family on the
hero and against the village:

- **Low-poly stylized**, matching the polyperfect Low Poly Ultimate Pack village
  aesthetic (flat-shaded faces, chunky readable forms, single-atlas-friendly).
- **Mobile poly budget.** Weapons ≈ **300–1,500 tris**; armor pieces ≈ **800–3,000
  tris**. No micro-detail that vanishes at phone scale — silhouette carries the read.
- **Flat / hand-painted-look color, minimal PBR.** Avoid photoreal metal and noisy
  normal maps; lean on bold base color + a single soft emissive accent for the
  elemental glow. URP-lit, no subsurface tricks.
- **Single object, neutral background, game-ready, centered, T-pose-neutral
  orientation.** No scene props, no ground plane, no character holding it — Tripo
  reconstructs cleaner from an isolated subject.
- **One soft emissive accent per element** (see palette). The glow is the gameplay
  read (matches the elemental-codex aura colors); the rest of the piece stays grounded
  and medieval, never neon.
- **Grounded medieval fable, not high-fantasy bling.** Worn leather, honest steel,
  carved wood, river-stone crystal. The bible's voice: "modern English with old
  bones." Gear should look *used by people who tend a tree*, not minted for a hero.

> **Reference images.** Matching concept reference images (front + 3/4 silhouettes
> per item) are saved alongside this doc in `docs/concepts/weapons-armor/`. Feed the
> reference image **plus** the text prompt to Tripo (image+text-to-3D) for the closest
> match; the prompts here also stand alone for pure text-to-3D.

---

## 2. Per-class palette table

Each class anchors to its school's codex color (see `elemental-codex.md` §1) plus a
material base. The **emissive accent** is the only glowing color on the piece.

| Class | Set name | Element | Material base | Base color | Emissive accent | Metal / trim |
|---|---|---|---|---|---|---|
| **Knight** | Aegis of Elarion | Physical (+ heart-blue ward) | Steel plate | Brushed steel `#9AA0A6` | Heart-blue `#5FA8E0` | Silvered `#C7CDD4` |
| **Ranger** (Sylas) | Frostwarden | Ice | Leather + fur | Deep forest `#3C5142` | Frost-blue `#80CCFF` | Bone-pale `#D8D2C2` |
| **Mage / Keeper** | Wardweave | Aether | Layered cloth | Twilight violet `#4A3A6B` | Aether violet `#9B6FFF` | Pale gold `#C9A86A` |
| **Cleric / Healer** | Chorister's Vestments | Aether (light) | Cloth + linen | Vestment white `#F2EEE3` | Warm candle-gold `#FFD79A` | Soft gold `#D9B45C` |

> The Mage and Cleric share the Aether school but split it tonally: the Mage is
> **violet corruption-answering ward-light** (`#9B6FFF`), the Cleric is **warm
> restorative candle-light** (`#FFD79A`) — the same distinction the codex draws
> between Hollow Caster violet and Hollow Mender warm-white.

---

## 3. Knight — "Aegis of Elarion" (Physical + heart-blue ward)

The valley's shield-line. Honest steel that has held the gate a hundred times, fitted
with a chip of the Heart's crystal so the bearer feels the song through the metal.

> **Code note:** `GearLoadout.cs` ships the Knight's Aegis weapon as **Emberbrand**
> (a stored-aether shock finisher). The brief asks for a **Greatsword**. Treat
> Emberbrand as the *legendary* sword skin and "Greatsword" as the base steel form of
> the same silhouette — generate the greatsword; the heart-blue glow is the Emberbrand
> tier.

### 3.1 Great Helm

*Lore:* Forged so the wearer's breath fogs the visor and reminds them they still
breathe — unlike the Hollow Ones at the gate. A thin seam of heart-crystal runs the
crown so the Keeper's call reaches even a closed helm.

*Tripo prompt:* `Low-poly stylized medieval great helm, full steel barrel helm with
narrow eye-slit and breathing holes, brushed steel base color, a thin pale-blue glowing
crystal seam along the crown ridge, flat-shaded faces, chunky readable silhouette,
single object, centered, neutral grey background, game-ready, mobile poly budget`

### 3.2 Plate Cuirass

*Lore:* Plate that has been dented and beaten flat again more times than its smith can
count. The Folk re-rivet it after every long march; it is older than the current Keeper.

*Tripo prompt:* `Low-poly stylized medieval steel plate cuirass torso armor, layered
breastplate with shoulder pauldrons and a faint heart-blue glowing crystal set at the
sternum, brushed steel with silvered trim, worn honest metal not ornate, flat-shaded,
strong silhouette, single object, centered, neutral background, game-ready, low poly`

### 3.3 Greatsword (legendary: Emberbrand) **[CANON: Emberbrand]**

*Lore:* A two-handed blade that drinks a little of the Heart's light with each block
and gives it back as a shock on the next swing. The Folk call its glow "the gate's
last word."

*Tripo prompt:* `Low-poly stylized two-handed medieval greatsword, long straight steel
blade with a central fuller, simple crossguard, leather-wrapped grip, a heart-blue
glowing crystal in the pommel and a faint blue edge-glow, brushed steel, flat-shaded,
clean bold silhouette, single object, blade vertical, centered, neutral background,
game-ready, mobile low poly`

### 3.4 Tower Shield

*Lore:* Tall enough to plant in the soil and stand behind. Its face bears the carved
sigil of Elarion — a tree inside a ring — worn smooth by the hands of every Knight who
held it before.

*Tripo prompt:* `Low-poly stylized medieval tower shield, tall rectangular kite-topped
steel shield with riveted edges, a carved engraved tree-inside-a-ring sigil glowing
faint heart-blue at the center, brushed steel with silvered rim, flat-shaded, bold
readable silhouette, single object, front-facing, centered, neutral background,
game-ready, low poly`

---

## 4. Ranger — "Frostwarden" (Ice; hero Sylas)

Sylas's kit — the scout who walks the cold edges of the valley where the Ice Wolf first
came down the mountain. Leather and fur over frost-touched steel; built to move quiet
and strike from the dark.

> **Code note:** the shipped Aegis weapon for the Ranger is **Heartwood Longbow**
> (pierce + mark). The Frostwarden longbow below is the Ice-themed base; Heartwood is
> the legendary tier of the same bow silhouette.

### 4.1 Hooded Cloak

*Lore:* Frost rimes the hem but never the hood — the Warden stays warm inside it,
watching. Cut from oilskin and northern wolf-fur the way the old scouts taught.

*Tripo prompt:* `Low-poly stylized ranger hooded cloak and hood, deep-green oilcloak
with a wolf-fur collar lightly rimed with pale frost-blue at the hem, weathered leather
straps, flat-shaded, soft draping silhouette, single object, centered, neutral grey
background, game-ready, mobile low poly`

### 4.2 Light Brigandine

*Lore:* Riveted plates between layers of boiled leather — quiet, flexible, made for a
scout who must run a lane and loose three arrows before the dark notices.

*Tripo prompt:* `Low-poly stylized light brigandine chest armor, boiled dark-leather
torso with small riveted steel plates and a fur shoulder mantle, frost-blue glowing
stitching accent, fitted and flexible not bulky, flat-shaded, lean silhouette, single
object, centered, neutral background, game-ready, low poly`

### 4.3 Longbow (legendary: Heartwood Longbow) **[CANON: Heartwood Longbow]**

*Lore:* A tall recurve of frost-hardened yew. Loosed arrows leave a thread of cold in
the air; what they strike moves slower, as if winter remembered it.

*Tripo prompt:* `Low-poly stylized tall recurve longbow, frost-hardened pale wood limbs
with bone tips and a leather-wrapped grip, a faint frost-blue glow along the string and
limbs, simple elegant curve, flat-shaded, clean tall silhouette, single object, vertical,
centered, neutral background, game-ready, mobile low poly`

### 4.4 Twin Daggers

*Lore:* For when the dark gets close. Their blades are forged with a sliver of frost so
a cut bites twice — once with steel, once with cold.

*Tripo prompt:* `Low-poly stylized pair of matching ranger daggers, short leaf-shaped
steel blades with leather-wrapped grips and bone pommels, a faint frost-blue edge-glow,
flat-shaded, sharp clean silhouettes, two identical daggers crossed, single object group,
centered, neutral background, game-ready, low poly`

---

## 5. Mage / Keeper — "Wardweave" (Aether; violet ward-light)

The Keeper's own attire. Not a battlemage's armor but a *warden's* — layered cloth
woven with threads that carry the Heart's violet light, so the wearer is always a little
inside the song.

> **Code note:** the shipped Aegis weapon for the Mage is **Aetherstaff** (spells hit
> harder up close). The Wardweave Ward-Staff is the base; Aetherstaff is its legendary
> tier.

### 5.1 Wide-brim Hat

*Lore:* The Keepers have worn the same shape of hat for a thousand years — broad enough
to keep the valley rain off a long night at the ward-stones. The band glows when the
song stirs.

*Tripo prompt:* `Low-poly stylized wide-brim wizard hat, soft pointed felt hat with a
broad drooping brim, deep twilight-violet fabric, a glowing pale-violet band with a small
crystal at the front, flat-shaded, classic readable wizard-hat silhouette, single object,
centered, neutral grey background, game-ready, mobile low poly`

### 5.2 Layered Robe

*Lore:* Many thin layers, each woven with a different verse of the song, so the cloth
itself remembers the wards. Violet light seeps along the seams when the Keeper casts.

*Tripo prompt:* `Low-poly stylized layered wizard robe, long flowing multi-layered cloth
robe in twilight violet with pale-gold trim, glowing pale-violet runic seams down the
front, wide draping sleeves, flat-shaded, tall draping silhouette, single object, standing
upright, centered, neutral background, game-ready, low poly`

### 5.3 Ward-Staff (legendary: Aetherstaff) **[CANON: Aetherstaff]**

*Lore:* Cut from a fallen Elarion branch, never carved — only asked. At its head it
cradles a knot of raw heart-crystal that brightens with every ward the Keeper sets.

*Tripo prompt:* `Low-poly stylized wizard ward-staff, tall gnarled pale wooden staff with
a natural fork at the top cradling a glowing violet crystal cluster, leather wrap at the
grip, faint violet glow, flat-shaded, elegant tall silhouette, single object, vertical,
centered, neutral background, game-ready, mobile low poly`

### 5.4 Spell Focus — floating ward-orb

*Lore:* A palm-sized sphere of crystal that does not need to be held; it orbits the
Keeper's hand, listening. When a ward fails, it dims a heartbeat before the Keeper feels
it go.

*Tripo prompt:* `Low-poly stylized floating magic orb spell focus, a faceted violet
crystal sphere wrapped in three thin curved gold rings orbiting it, soft inner pale-violet
glow, no chain no handle, flat-shaded, clean round silhouette, single object, centered,
neutral dark-grey background, game-ready, low poly`

---

## 6. Cleric / Healer — "Chorister's Vestments" (Aether-light; white + gold)

The valley's healers sing the same song the Keeper hears, only gentler. Their vestments
are the warm side of Aether — white and candle-gold, made to mend rather than to ward.

> **Code note:** the shipped Aegis weapon for the Cleric is **Hallowed Censer**
> (heal-also-wards). The Relic Censer-mace below is exactly that item — generate it as
> the canon Hallowed Censer.

### 6.1 Circlet / Veil

*Lore:* A thin gold circlet holding a pale veil — not to hide the face but to soften
the light the wearer carries, so the dying are not blinded on their way out.

*Tripo prompt:* `Low-poly stylized cleric circlet with a light veil, a thin gold band
with a small warm-glowing white gem at the brow and a sheer pale cloth veil draping
behind, flat-shaded, delicate readable silhouette, single object, front-facing, centered,
neutral grey background, game-ready, mobile low poly`

### 6.2 Vestment Robe

*Lore:* White linen with gold thread at the hem, kept clean by the Folk no matter the
march. The gold catches the Heart's light and gives a little of it back to whoever
stands near.

*Tripo prompt:* `Low-poly stylized cleric vestment robe, long flowing white linen robe
with warm-gold trim and embroidered hem, a soft warm-gold glow at the chest, wide gentle
sleeves, flat-shaded, tall draping silhouette, single object, standing upright, centered,
neutral background, game-ready, low poly`

### 6.3 Relic Censer-mace (legendary: Hallowed Censer) **[CANON: Hallowed Censer]**

*Lore:* A swinging censer on a short gilded haft, heavy enough to turn a Hollow One's
skull and holy enough to mend the wound it leaves on an ally. It smells of the orchard
in summer.

*Tripo prompt:* `Low-poly stylized cleric censer-mace, a short gilded mace haft topped
with an ornate pierced golden censer ball that emits a warm-gold glow and faint smoke,
small chain links at the top, flat-shaded, bold readable silhouette, single object,
vertical, centered, neutral dark-grey background, game-ready, mobile low poly`

### 6.4 Light Staff

*Lore:* A slender white staff crowned with a sunburst of soft gold. The Cleric plants it
where the wounded gather; the light it sheds is the closest thing the valley has to the
Heart's own.

*Tripo prompt:* `Low-poly stylized cleric light staff, a slender pale wooden staff topped
with a small radiant golden sunburst holding a warm-glowing white gem, flat-shaded,
elegant tall silhouette, single object, vertical, centered, neutral background, game-ready,
low poly`

---

## 7. Elemental weapon trio (tower-imbuement tie-in)

These three answer the **tower imbuements** in `elemental-codex.md` §4 (Flame Tower /
Frost Tower / Arcane Tower). They are the hero-held counterparts to the towers' empowered
shots — a weapon the Keeper or a class hero can carry that *is* a walking imbuement. Each
maps to one school's codex color and glow intensity.

| Weapon | School | Codex color | Tower tie-in |
|---|---|---|---|
| **Eternal Ember** (flame blade) | Flame | Red→gold `#FF4400`/`#FF9900` | Flame Tower — Inferno (DoT Burn) |
| **Glacial** (frost spear) | Ice | Frost-blue `#80CCFF` | Frost Tower — Ice Nova |
| **Wardlight** (aether wand) | Aether | Violet `#9B6FFF` | Arcane Tower — Aether surge |

### 7.1 Eternal Ember — flame blade

*Lore:* Forged in the first hearth-fire the valley ever lit — the same fire the Flame
Pup remembers. Its edge never goes cold; what it cuts, the Withering cannot easily reclaim.

*Tripo prompt:* `Low-poly stylized flaming longsword, a steel blade with glowing
ember-orange cracks running its length and a red-hot edge, simple bronze crossguard with
a small glowing ember gem in the pommel, faint heat-glow, flat-shaded, clean bold
silhouette, single object, blade vertical, centered, neutral dark-grey background,
game-ready, mobile low poly`

### 7.2 Glacial — frost spear

*Lore:* A spear whose head is a single grown shard of mountain ice that never melts —
gifted, the Folk say, by the same cold that the Ice Wolf walked down from. It slows what
it pierces, the way winter slows a river.

*Tripo prompt:* `Low-poly stylized frost spear, a long pale wooden haft topped with a
faceted translucent frost-blue ice crystal spearhead, thin rime of frost along the upper
shaft, soft cold blue glow, flat-shaded, clean tall silhouette, single object, vertical,
centered, neutral background, game-ready, low poly`

### 7.3 Wardlight — aether wand

*Lore:* The smallest of the three and the oldest in spirit — a short wand of Elarion-wood
tipped with a chip of the Heart's crystal, the same kind from which the Aether Sprite was
born. It does not strike so much as *ward*: a flick sets a thread of violet light between
the Keeper and the dark.

*Tripo prompt:* `Low-poly stylized aether magic wand, a short slender pale-wood wand
wrapped in thin gold wire, tipped with a small glowing violet crystal shard emitting a
soft pale-violet light, flat-shaded, delicate clean silhouette, single object, horizontal
or slight angle, centered, neutral dark-grey background, game-ready, mobile low poly`

---

## 8. Generation checklist (per item)

Before exporting a Tripo result for any item above:

- [ ] Single isolated object on a neutral background (no scene, no ground, no hand)
- [ ] Silhouette reads at phone scale (squint test — is the form obvious?)
- [ ] Within poly budget (weapon ≤ ~1,500 tris, armor ≤ ~3,000 tris)
- [ ] Exactly one elemental emissive accent, in the class's codex color (§2)
- [ ] Material reads flat / hand-painted, not photoreal PBR
- [ ] Matched to its set's base color + trim (consistent family look)
- [ ] Reference image saved to `docs/concepts/weapons-armor/<class>_<item>.png`

---

## 9. Item count

**21 items total**, across 5 sets:

- Knight "Aegis of Elarion" — **4** (Great Helm, Plate Cuirass, Greatsword/Emberbrand, Tower Shield)
- Ranger "Frostwarden" — **4** (Hooded Cloak, Light Brigandine, Longbow/Heartwood, Twin Daggers)
- Mage/Keeper "Wardweave" — **4** (Wide-brim Hat, Layered Robe, Ward-Staff/Aetherstaff, Spell Focus orb)
- Cleric/Healer "Chorister's Vestments" — **4** (Circlet/Veil, Vestment Robe, Censer-mace/Hallowed Censer, Light Staff)
- Elemental weapon trio — **3** (Eternal Ember, Glacial, Wardlight)

---

*Living doc. Owner ratifies set names, the Aegis/per-class naming reconciliation (§intro),
and final palette before any item is generated or bound to gear data.*
