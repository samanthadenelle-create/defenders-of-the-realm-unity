# WORK ORDER 706 — Build-palette portraits: complete the set, forge-style (ART, UI seat)

**Status: READY — UI-seat art task** (owner directive 2026-07-13, felt-test: "same thing you
used on the forge" — every palette tile gets a portrait like `forge.jpg`).
**Lane:** Art/UI (NO code — the resolver already works; this is asset authoring only).

## The rule the code already implements (BuildPaletteUI.cs:412-437, do not change it)
Tile art resolves: `Resources/Portraits/<id>` → `Portraits/<displayName-slug>` →
ConceptIconResolver → obsidian plate with gilt initial (never blank). Missing art = the
letter plate the owner saw ("Mill picture of M").

## Deliverables — 8 portraits, matching the existing forge.jpg style/framing/palette
Save each to `Assets/Resources/Portraits/<name>.jpg` (id-named, so the first resolver hop hits):

1. `mill.jpg` — Mill (the "M" plate in the owner's screenshot)
2. `tower_siege_tower.jpg` — Sky Ballista (Anti-Air)
3. `wall_wood.jpg` — Wooden Palisade
4. `wall_stone.jpg` — Stone Wall
5. `gate_stone.jpg` — Stone Gate
6. `mine_crystal.jpg` — Crystal Mine
7. `fountain_healing.jpg` — Wellspring of Elarion
8. `deco_torch.jpg` — Wall Torch

Match: obsidian-friendly storefront/structure portrait, same aspect + tone as
forge.jpg / market.jpg / jeweler.jpg. Owner is red/green colorblind — read by shape/luminance.

## Known collisions to FLAG (not fix here — catalog naming is a separate ruling)
- id `workshop` (displayName "Forge") and id `collector_forge` (displayName "Forge") and id
  `forge` (displayName "Armorer") all resolve into `forge.jpg`/each other's namespace.
- id `armorer` (displayName "Blacksmith") shows `armorer.jpg` (the Armorer's image).
  → The owner should rule once on the workshop/forge/armorer/blacksmith naming knot; portraits
  then follow the ruling. Until then, author the 8 above only.

## Acceptance
- [ ] All 8 files present at the exact paths above; palette shows zero letter-plates on the
      Town/Defenses/Walls tabs (except any building the owner deliberately leaves plated).
- [ ] Style-consistent with forge.jpg at tile size (thumb legibility, no color-only meaning).
- [ ] Signal ready for CLI reconcile (import + felt-pass ride the next build; PO closes).

## What NOT to touch
BuildPaletteUI.cs / ConceptIconResolver (working as designed) · existing portraits ·
the catalog json (naming knot = owner ruling first).

*Cross-refs:* owner felt-test 2026-07-13 (Hollow/Forge/blank/M report) · BuildPaletteUI.cs:405-437 ·
the Market blank-LABEL mystery is separate (pending F8 capture — not an art issue, market.jpg exists).
