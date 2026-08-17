<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 706 — Build-palette portraits: complete the set, forge-style (ART, UI seat)

**Status: READY — UI-seat art task** (owner directive 2026-07-13, felt-test: "same thing you
used on the forge" — every palette tile gets a portrait like `forge.jpg`).
**Lane:** Art/UI (NO code — the resolver already works; this is asset authoring only).

## The rule the code already implements (BuildPaletteUI.cs:412-437, do not change it)
Tile art resolves: `Resources/Portraits/<id>` → `Portraits/<displayName-slug>` →
ConceptIconResolver → obsidian plate with gilt initial (never blank). Missing art = the
letter plate the owner saw ("Mill picture of M").

## Deliverables — 10 portraits, matching the existing forge.jpg style/framing/palette
Save each to `Assets/Resources/Portraits/<name>.jpg` (id-named, so the first resolver hop hits).
*(Updated per WO-707 rulings 2026-07-13: `mill.jpg` DROPPED — the mill retires, Farm is the food
producer and farm.jpg exists; `mine_crystal.jpg` DROPPED — Crystal Mine leaves the palette, it's
a world node; the three NEW storage containers added.)*

1. `lumberyard.jpg` — Lumberyard (NEW storage: wood, pallet stacks — WO-707)
2. `foundry.jpg` — Foundry (NEW storage: iron — WO-707)
3. `silo.jpg` — Silo (NEW storage: grain — WO-707)
4. `tower_siege_tower.jpg` — Sky Ballista (Anti-Air)
5. `wall_wood.jpg` — Wooden Palisade
6. `wall_stone.jpg` — Stone Wall
7. `gate_stone.jpg` — Stone Gate
8. `fountain_healing.jpg` — Wellspring of Elarion
9. `deco_torch.jpg` — Wall Torch
10. `bryn.jpg` — Bryn the Wanderer (dungeon torch-warden; currently LOANING Portraits/apothecary
    to satisfy the dialogue portrait standard — replace with her own face).
11. `market.jpg` exists but the tile becomes **Store (Buy Packs)** — re-author only if the
    current image reads as a generic market rather than a pack store (UI-seat judgment).

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
