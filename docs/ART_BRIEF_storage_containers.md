# Art Brief — Resource Storage Containers (wood / iron / food)

**Status: DRAFT for owner art pass** (owner ask 2026-07-13: CoC-style visual storage for
goods). **Owner is the artist** — this brief gives the design law, per-container direction,
and Tripo-ready prompts. Implementation (catalog rows, fill-state visuals) = a future WO once
models exist; minting waits for the banner.

## The design law (why CoC storages read instantly)

1. **The container displays the RESOURCE, not a label.** Logs in the crib, ingots in the bin,
   grain in the window — a stranger names each building's job in one look.
2. **Fill level IS the UI.** The model has 3-4 visible fill states (empty / low / half / full)
   driven by the stored amount — no floating bars needed in the world.
3. **Full = tempting.** A visibly full container telegraphs raid value — this is a FEATURE:
   it powers the built siege-loot targeting (`ISiegeLootTarget`, raiders prefer full
   collectors) and the WO-672 damage stakes ("what burns stops earning"). The player FEELS
   why they should collect or defend.
4. **Distinct silhouettes.** Horizontal open frame (wood) vs squat heavy bin (iron) vs tall
   round-roofed crib (food) — tellable apart at build-mode camera height, colorblind-safe by
   SHAPE, per the standing law.
5. **Tiers add capacity + armor, never confusion.** Same silhouette per resource across
   tiers; higher tier = bigger + reinforced (the wall-ladder grammar: wood → iron bands →
   rune-tempered).

## Per-container direction + Tripo prompts

**Scale note for all:** single grid cell (~3m footprint), low-poly stylized fantasy matching
the polyperfect/Tripo town set; bake fill-state variants as swappable child meshes or shape
keys (CLI wires per the StructureTierVisual/ReskinForLevel pattern later).

### WOOD — the Lumber Crib
Open corner-post frame; cut logs stack end-on between the posts. Empty = the bare frame still
reads "wood goes here."
> **Tripo T1:** "Low-poly stylized medieval lumber storage crib, four wooden corner posts with
> a simple open timber frame, stacked cut logs visible end-on between the posts, hand-painted
> fantasy game asset, warm brown wood, single small structure on a flat base"
> **T2:** add "iron-banded corner posts, taller frame, neatly stacked log pyramid"
> **T3:** add "carved gilt trim on the frame, faint golden rune on the center log"

### IRON — the Ore Bin
Squat, heavy, open-topped bin with metal banding; ingot bricks stack above the rim as it fills.
> **Tripo T1:** "Low-poly stylized medieval ore storage bin, squat heavy wooden bin with iron
> bands, open top with grey metal ingot bricks stacked inside, hand-painted fantasy game
> asset, dark timber and steel, single small structure on a flat base"
> **T2:** add "riveted iron plating on the sides, taller stack of gleaming ingots"
> **T3:** add "blue-glowing rune seals on the bands, polished silver ingots" (the
> rune-tempered top-tier grammar from the wall ladder)

### FOOD — the Granary
Raised round crib on short stilts, conical thatched roof, one dark slat-window on the front —
the golden grain level shows through it like a gauge.
> **Tripo T1:** "Low-poly stylized medieval granary, small round grain silo on short wooden
> stilts, conical thatched straw roof, front hatch window showing golden grain inside,
> hand-painted fantasy game asset, cream plaster walls, single small structure on a flat base"
> **T2:** add "second grain window, timber reinforcement ring, fuller roof"
> **T3:** add "gilt weathervane wheat-sheaf on the roof peak, carved gold trim"

### (Later) CRYSTALS — the Reliquary
Premium currency deserves the premium read: an open stone cradle holding a visible cluster of
glowing crystals; fill = cluster size + glow. Park until the crystal-faucet arc (WO-679) needs it.

## Reference images to gather (owner side — for Tripo image-conditioning or eye reference)
Search terms that find the right shapes fast: "medieval lumber crib", "firewood log store
open frame", "medieval granary raised staddle stones", "grain silo thatched roof medieval",
"blacksmith ore bin ingot stack", plus CoC's own gold/elixir storages for the FILL-STATE
grammar (study how their fill reads, not their style).

## Ties to built systems (for the eventual WO)
Fill states ← `ResourceCollector` pending fraction (the "pending bubble VFX" fast-follow this
replaces — the MODEL is the bubble) · raid preference ← `ISiegeLootTarget` (built) · damage
tells ← WO-672 `damage-states.json` · placement ← standard catalog rows (type Collector/
Resource, 3m footprint) · capacity tiers ← storage-tier ladder (monetization spec §offline
storage) · echo deposit animation target (WO-659 embodied workers).
