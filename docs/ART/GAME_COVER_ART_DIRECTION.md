# Echoes of Elarion — Game Cover Art Direction

**Status:** Art-direction spec (production-level). Canon-grounded 2026-06-28.
**Sources:** `SESSION_CANON_LOADER.md`, `CANON_GROUND_TRUTH_2026-06-26.md`,
`docs/COMBAT_PIVOT_NORTHSTAR.md`, `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md`,
`docs/audio-mix-spec.md` (tone bible line), live art in
`Assets/Resources/Heroes/` + `Assets/Resources/Enemies/OrcTex/`.
**Scope:** This doc is art direction + a paste-ready generation prompt. It does
not touch code. The image is produced by an external tool/human.

> **Canon locks used here (do not deviate):**
> - Title: **Echoes of Elarion** (working subtitle / series tag: *Defenders of the Realm*).
> - Hero: **ONE Knight, "Grom"** — single Tripo self-rigged model, **static armor**, weapon + shield are the only visible flair. No party, no companions. (`ff.singlehero`, `ff.knightonly` ON.)
> - World: **Elarion**, centred on the **Heart of Elarion** — a living **world-tree / Tree of Life** at (0,0,0) that is **dimming** as its aether (life-force) is siphoned.
> - Threat: **the Hollow Ones** (grief-born husks) + the **Orc** legion (warrior/tank/mage) under an Orc Necromancer. **Tone bible line (binding):** *"the Hollow Ones are grief, not Sauron."* Mourning and decay, not cackling evil.
> - Reclaim loop: drive the darkness back → the tree heals → **echoes/spirits** of life are released and multiply around a brighter tree. The cover should imply *the tree is worth saving and still has light left.*
> - UI / brand palette: **Obsidian black + gold**, with a **life-force green/teal** (the tree's glow + echo spirits) as the soul accent.

---

## 1. CONCEPT — the single striking image

**"The Last Light of the Heart."**

A lone armored Knight (Grom) stands his ground on a root-shelf at the base of the
colossal **Heart of Elarion world-tree**, sword lowered but ready, kite shield
forward, **silhouetted against the one part of the tree still glowing gold-green
with life**. Behind and around him the world is going grey: the encroaching
**Hollow** — a tide of dim, ash-grey husk-silhouettes and a looming orc host —
bleeds in from the dark edges, leaching colour out of the land. Tiny motes of
living light (**echo spirits**) drift up from the tree's roots past the Knight,
the visual promise that as long as one defender stands, the light is not out.

The single idea in one line: **one knight, one tree, the last warm light against an
advancing grey grief.** Heroic but melancholic — defiance, not triumph.

This is *key art that sells the fantasy of the whole game*: you are the only thing
you directly control, holding the line for a living world that heals when you push
the dark back.

---

## 2. COMPOSITION

**Orientation of the master frame:** landscape **16:9** hero key art (everything
else is a crop of this — see §6). A **portrait 2:3** alternate is also speced for
storefront capsules.

**Depth planes:**
- **Foreground (lower third, framing):** the Knight, hero-scale, standing on a
  twisting exposed **root-shelf**, placed on the **left vertical third**, facing
  into frame (toward the threat on the right). Bottom-edge roots + a few fallen
  grey leaves anchor the eye and create a "stage." Rim-lit gold-green from the tree
  behind him; his front in cool shadow.
- **Midground:** the massive **trunk of the Heart-tree** rises up the right-center,
  bark cracked with veins of glowing gold-green sap. From the roots, **echo spirits**
  (soft teal-green wisps with a warm core) drift upward — a diagonal current of light
  leading the eye from the Knight up into the canopy. Opposite him, lower-right and
  receding, the **Hollow tide**: indistinct ash-grey humanoid husks + a few hulking
  **orc silhouettes** (broad-shouldered, tusked, jagged weapons) pressing in, mostly
  in shadow so they read as a *mass*, not individuals.
- **Background:** the tree's **canopy** half-alive — one side gold-green and
  luminous, the other side bare, grey, dying — a literal split between life and the
  Hollow. Beyond, a low **castle/rampart silhouette** (the hub home) on the horizon,
  and a **dimmed sky**: an overcast dusk with one break of warm light behind the
  living half of the canopy.

**Focal point & reading path:** the eye lands first on the Knight's **rim-lit
shield + the gold-green light blooming behind him**, then rides the upward diagonal
of echo-motes into the glowing canopy, then sweeps down-right into the grey Hollow
mass — a clean give→take→tell: *hero → hope → threat.*

**Rule of thirds:** Knight on the left third; the tree's lit core on the upper
right intersection; the Hollow advancing along the lower-right third; the title in
the upper band or lower band per crop (§5).

**Lighting / time of day:** **golden-hour-going-to-dusk**, heavily directional.
The ONE warm key light is the tree's own life-glow (gold-green) raking from behind
the Knight (strong rim/halo separation). Fill is cold and low — ash-blue twilight
on everything the Hollow touches. Strong atmospheric depth: volumetric god-rays
through the canopy on the living side; thin grey haze/ash drifting on the Hollow
side. High contrast, cinematic, slightly desaturated overall EXCEPT the protected
gold-green core, which is the only saturated thing in frame.

---

## 3. CHARACTER — the Knight, "Grom"

Match the in-game model and texture (see `Assets/Resources/Heroes/Knight.fbx` +
`KnightArmored_basecolor.jpg`): a **grounded, realistic human knight**, NOT
cartoon/low-poly on the cover — render him as the *idealized hero version* of that
model.

- **Build & read:** adult human male, broad but agile, full but practical plate —
  reads as a seasoned defender, not a fantasy demigod.
- **Armor (STATIC, canon — no glowing magic armor):** **dark weathered steel**
  plate over **brown leather** straps and gambeson, scratched and battle-worn,
  matte not chrome. Subtle warm steel highlights only where the tree-light catches
  edges. A simple half-cloak or tabard is acceptable for silhouette, kept muted
  (deep charcoal or oxblood, faint gold trim to tie to the brand — do NOT make it
  gaudy). No exposed magical runes on the body armor.
- **Helmet/face:** prefer **helmet off or open-faced** so there's a human, weary,
  resolute expression — jaw set, eyes catching the tree-light. The grief tone wants
  a *person*, not a faceless tank. (If helmeted, leave the face visible through an
  open visor.)
- **Weapon:** a straight **knightly arming/longsword**, point-down or held low and
  ready at his side (not raised in a hero pose — defiant readiness, weight implied).
  Edge catches a thin gold-green rim from the tree.
- **Shield:** a **kite/heater shield** held forward/across the body (the shield is
  a real mechanic in-game — make it prominent). Its face can carry a simple
  **gold-on-dark emblem** that echoes the brand (a stylized tree/heart or a wall
  segment) — understated, weathered metal, not a shiny crest.
- **Pose:** **three-quarter stance, planted**, facing into frame toward the
  advancing Hollow, shield slightly leading. Body language = *I am not moving.*
  Cape/leaves/ash catching a light wind for life in the frame.
- **Expression:** weary defiance — grief and resolve, jaw set. He has been here a
  long time and will stay.

---

## 4. ENVIRONMENT — Elarion

- **The Heart of Elarion (the world-tree, the star of the set behind the Knight):**
  a **colossal ancient living tree** — gnarled, broad-rooted, the obvious heart of
  the realm. Bark is dark and cracked, and through the cracks runs **glowing
  gold-green living sap** (the aether/life-force). The canopy is **split**: one half
  full, luminous, alive (warm gold-green); the other half bare, ashen, dimming where
  the Hollow's siphon has reached. At the very base, nestled in the roots, a hint of
  a **stone reliquary / shrine** (the Heart proper). This split tree IS the theme:
  life vs. the encroaching grey.
- **Echo spirits:** small drifting **wisps of soft teal-green light with a warm
  white core**, rising from the roots like embers-in-reverse — gentle, mournful,
  hopeful. A handful, not a swarm (canon: a small bounded workforce). They are the
  game's signature VFX — put them in the hero's light path.
- **The encroaching Hollow:** the threat is **decay and grief made visible** —
  desaturation creeping in from the frame edges, grey ash drifting, the ground going
  colourless and cracked on the Hollow side. The Hollow Ones themselves are
  **gaunt, ash-grey, semi-translucent husk silhouettes** — hollow-eyed, slumped,
  more *sorrowful* than monstrous (per the bible). Among them, **orc shapes** from
  the actual roster (broad, tusked, hide-and-iron, jagged cleavers/axes — base palette
  from `Orc_*_basecolor`: green skin, brown leather, grey-blue metal) read as the
  muscle of the host. Keep them in shadow/silhouette so they're a *tide*, not a
  character lineup.
- **Castle / hub:** a low fortified **castle rampart** on the far horizon behind the
  living side — the home you defend — small, distant, a reason-to-fight, not a focal
  element.

**Color palette (binding):**
| Role | Color | Use |
|---|---|---|
| Brand base | **Obsidian black / near-black charcoal** | Frame edges, Hollow side, shadows, title bar |
| Brand accent | **Antique / burnished gold** (#C8A24A-ish) | Title, shield emblem, armor edge trim, rim glints |
| Life-force soul accent | **Gold-green → teal-green** (luminous, #6FCF8E → #3FB6A8) | Tree sap glow, canopy living half, echo spirits, hero rim light |
| Hero / world neutrals | **Weathered steel grey + brown leather** | Knight armor, roots, bark |
| The Hollow | **Desaturated ash grey-blue** | Husks, dying canopy, creeping desaturation, haze |

The discipline: **almost the whole frame is dark, desaturated, and cold — the ONLY
saturated, warm, alive thing is the tree's gold-green light and the echo spirits,
with the Knight rim-lit by it.** Gold is for the brand/UI elements; gold-green is
for the living world. That single contrast is the entire mood.

---

## 5. TITLE / LOGO TREATMENT

- **Wordmark:** **ECHOES OF ELARION** — a **high-contrast serif / fantasy display
  face** with engraved, weathered character (think chiseled-in-stone or
  forged-in-metal — formal, ancient, slightly distressed; NOT a soft rounded casual
  face). Letterforms with sharp serifs and tapered strokes.
- **Treatment:** **burnished gold on obsidian black** — gold leaf with subtle
  metallic bevel and fine edge-wear, sitting over a near-black band/gradient so it
  reads at thumbnail size. A *very* subtle gold-green inner glow on "ELARION" only
  can tie the title to the tree-light (use sparingly). Avoid heavy bevels/drop
  shadows that scream stock-fantasy.
- **Subtitle / series tag:** small gold/grey caps beneath — *DEFENDERS OF THE REALM*
  — letter-spaced, secondary weight.
- **Placement:** for **landscape key art**, the wordmark sits in the **lower third**
  (so the tree + Knight own the upper frame) OR an upper band if the storefront
  needs clear sky there — keep it off the Knight and off the tree's lit core. For
  **portrait capsules**, stack the wordmark in the **lower third** over the
  darkened root/Hollow base, Knight + tree above it.
- **Optional brand mark:** a small **gold sigil** — a stylized **tree-within-a-wall-
  segment** (ties the world-tree to the `Defenders` "built one segment at a time"
  brand note) — can lock up to the left of or above the wordmark.
- **Tagline (canon — locked 2026-06-28, WO-570; set small in gold/grey caps):**
  - *"Hold the line"*
  - (Retired alternates, do not use: "The Heart is dimming. Push the dark back.";
    "One knight. One living world. The grey is coming.")

---

## 6. FORMAT — deliverables & aspect ratios

Produce a **master 16:9 landscape painting at high res** (e.g. 3840×2160), then crop
/ re-layout the title for each target. Keep the Knight + tree composition working in
both landscape and a portrait re-stack.

| Asset | Aspect | Pixel size | Notes |
|---|---|---|---|
| **Master key art** | 16:9 | 3840×2160 | Source of all crops; full scene |
| **Steam header capsule** | ~46:21 | 460×215 | Tight crop on Knight + lit tree, title legible small |
| **Steam small capsule** | ~46:21 | 231×87 | Wordmark must read at this size — test it |
| **Steam main/large capsule** | 92:43 | 920×430 | Knight left, tree + title right |
| **Steam vertical capsule** | 2:3 | 600×900 | Portrait re-stack, title lower third |
| **Steam page background** | 16:9 | 1920×1080 | Darkened, title removed, atmospheric |
| **Steam library hero** | ~16:6.2 | 3840×1240 | Wide cinematic crop, title off-center, safe zones for overlay |
| **Steam library capsule (portrait)** | 2:3 | 600×900 | As vertical capsule |
| **itch.io cover** | 4:3 (≈) | 630×500 | itch's listed cover ratio; portrait-ish crop |
| **itch.io banner** | wide | 960×320+ | Wide atmospheric strip |
| **Mobile/app store icon** | 1:1 | 1024×1024 | Brand sigil OR tight Knight-helm/shield + tree-glow; readable tiny |
| **App store feature/key art** | 16:9 & 9:16 | 1920×1080 / 1080×1920 | Landscape + portrait promo |
| **Social/share card** | 1.91:1 | 1200×630 | OG card, title + Knight |

**Safe zones:** keep the Knight's face/shield and the title clear of all crop edges;
maintain a darker, low-detail margin around the frame so any crop and any UI overlay
sits cleanly. Provide one **title-on** and one **title-off (clean plate)** version of
the master.

---

## 7. MOOD / REFERENCE TOUCHSTONES

Use these for *mood, light, and emotional register* — not to copy:

1. **Hades / Supergiant key art** — single hero, dramatic rim-light against a dark
   field, bold readable silhouette + ornate gold typography. (Hero-against-dark legibility.)
2. **Dark Souls / Elden Ring cover key art** — lone armored figure dwarfed by a
   colossal sacred structure (here, the world-tree), melancholic grandeur, muted
   palette with one sacred glow. (Scale + mournful awe — closest tonal match.)
3. **Ori and the Blind Forest** — luminous life-spirit light vs. encroaching decay,
   the "fragile glowing life in a dark world" emotional core. (The echo-spirit / tree
   glow feeling, and "grief, not evil.")
4. **The Lord of the Rings — the White Tree of Gondor imagery** — a sacred living
   tree as the heart/symbol of a realm worth defending. (The Heart-tree's iconography.)
5. **Diablo / ARPG box key art** — grounded weathered armor realism, dramatic
   directional light, high production-value rendering. (The render quality bar + the
   weathered-steel knight believability.)

**One-line tonal north star:** *Elden Ring's lonely sacred scale + Ori's fragile
living light — a single weary knight holding the last warm glow of a dying world-
tree against a grey tide of grief.*

---

## 8. READY-TO-USE GENERATION PROMPT

> Paste into a top-tier image model (Midjourney / Imagen / Flux / SDXL-class).
> Render the **16:9 master** first; re-run with aspect-ratio flags for the portrait
> capsule (2:3) and icon (1:1). For the title, prefer compositing the wordmark in
> afterward (image models render text poorly) — or accept stylized non-text and add
> the logo in post.

### Primary prompt

```
Epic fantasy video game cover key art, cinematic, production quality, highly detailed digital painting, dramatic golden-hour-into-dusk lighting.

A lone weary human knight stands his ground on a twisting tree-root shelf at the base of a colossal ancient living world-tree. The knight is positioned on the left third of the frame, three-quarter view, planted defiant stance, facing right into the scene. He wears battle-worn dark weathered steel plate armor over brown leather straps, matte not shiny, scratched and realistic; helmet off revealing a human face set in weary defiance, jaw clenched, eyes catching the light. He holds a straight knightly longsword lowered point-down at his side and a kite shield forward across his body, the shield bearing a subtle gold tree emblem. He is strongly rim-lit from behind by warm gold-green light, his front in cool shadow.

Behind and above him rises an enormous sacred world-tree (the Heart of Elarion), its dark cracked bark glowing with veins of luminous gold-green sap. The canopy is split: one half full, golden-green and radiant with god-rays piercing through; the other half bare, ashen and dying. Small drifting wisps of soft teal-green spirit light with warm white cores rise from the roots in a diagonal current past the knight (echo spirits).

From the shadowed lower-right, a grey encroaching tide closes in: gaunt ash-grey translucent hollow husk figures, hollow-eyed and sorrowful, and a few hulking tusked orc silhouettes with jagged weapons (green skin, hide and iron), kept in silhouette as an advancing mass. The color is draining out of the land on that side — desaturated ash-blue, drifting grey haze, cracked colourless ground. A small distant fortified castle rampart sits on the horizon behind the living half of the tree under an overcast dusk sky with one warm break of light.

Mood: melancholic heroic defiance, grief and hope, a last warm light against an advancing grey darkness. The entire frame is dark, cold and desaturated EXCEPT the saturated warm gold-green glow of the tree, the spirit motes, and the knight's rim light. High contrast, strong atmospheric depth, volumetric light, cinematic composition, rule of thirds.

Palette: obsidian black and burnished antique gold, luminous gold-green and teal life-force glow, weathered steel grey and brown leather. Style of Elden Ring and Ori cover art, lonely sacred scale. Ultra-detailed, sharp focus on the knight and shield, 8k, dramatic, award-winning fantasy game key art.

--ar 16:9 --style raw --quality 2
```

### Negative prompt

```
cartoon, low-poly, flat shading, cel-shaded, anime, chibi, cute, bright cheerful colors, oversaturated everywhere, neon, daylight, clear blue sky, multiple heroes, party of characters, companions, crowd of named characters, modern clothing, sci-fi, guns, lens flare clutter, busy cluttered background, text artifacts, garbled letters, watermark, signature, logo, UI elements, HUD, frame border, deformed anatomy, extra limbs, extra fingers, mutated hands, blurry, low detail, jpeg artifacts, shiny chrome armor, glowing magic armor runes, gaudy ornate crest, photo of a real person, plastic look.
```

### Title compositing note (post / human)
After generating the clean plate, add the wordmark **ECHOES OF ELARION** in a
chiseled high-contrast distressed serif, burnished **gold on near-black**, in the
**lower third** (landscape) or lower third (portrait), clear of the Knight and the
tree's lit core; small caps subtitle **DEFENDERS OF THE REALM** beneath; optional
small gold tree-within-a-wall-segment sigil; tagline in small gold/grey caps:
*"Hold the line"* Export title-on and title-off (clean plate) masters, then
crop to every size in §6, re-testing wordmark legibility at the small-capsule size.

---

## 9. ACCURACY CHECKLIST (verify the delivered cover against canon)

- [ ] Exactly **ONE** hero — a Knight. No party, no companions, no pets in frame.
- [ ] Knight armor is **weathered dark steel + brown leather, matte, static** — not glowing/magical/chrome.
- [ ] **Sword + kite shield** both visible; shield prominent (it's a real mechanic).
- [ ] The **world-tree (Heart of Elarion)** is the dominant background element, **split living/dying**, with **gold-green glowing sap**.
- [ ] **Echo spirits** present as soft teal-green motes (a handful, drifting up — not a swarm).
- [ ] The **Hollow** reads as **grief/decay/desaturation**, sorrowful husks + orc silhouettes — *not* a generic snarling demon horde ("grief, not Sauron").
- [ ] Palette holds **obsidian + gold + life-force gold-green**, dark/cold everywhere except the protected glow.
- [ ] Title **gold-on-black**, legible at small-capsule size; clean-plate version also delivered.
- [ ] Master 16:9 + portrait 2:3 + 1:1 icon crops all read correctly.
```
