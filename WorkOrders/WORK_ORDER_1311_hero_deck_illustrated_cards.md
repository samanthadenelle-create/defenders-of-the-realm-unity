# WORK ORDER 1311 — Wire the owner's illustrated hero cards, and retire the checkerboard compensation

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — implemented by 66d12f9bb `feat: wire the owner's illustrated hero-deck cards (WO-1311)`. Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02; body unchanged.)* *(Prior line:)* **Status:** READY TO IMPLEMENT
**Silo:** UI / HeroDeck
**Minted:** 2026-09-02 (CLI). Owner supplied the art directly and asked: *"can you use these for the hero screen ones?"*

## Part A — the hero cards have NO art wired

`PlayerDeckWorkspace.cs:229-232` — all four Hero routes omit the optional `ArtKey`:
```
Route("Bag",       "Browse every carried item by category", "inventory", PanelId.Inventory),
Route("Equipment", "Review worn gear on your hero",         "armor",     PanelId.EquipmentPanel),
Route("Skills",    "Learn and improve hero talents",        "skill",     PanelId.HeroSkillTree),
Route("Loadout",   "Choose the abilities equipped for battle","magic",   PanelId.HeroLoadout)
```
Compare `:248`, where the Realm cards DO pass one (`"realm-store"`). With no ArtKey the card falls to
`frames/card-frame-empty` plus a generic concept medallion — which is why the hero screen looks unfinished.

**The art is already placed** (by the lead, from the owner's sheet, split and alpha-keyed):
`Assets/Resources/UI/ElarionMedieval/cards/{bag,equipment,skills,loadout}.png`

Add the matching ArtKey to each of the four routes. That is the whole of Part A.

## Part B — the compensation is ALREADY WRONG TODAY. Measure per sprite.

`PlayerDeckWorkspace.cs:111-114`, verbatim:
> *"The delivered wide-card PNGs include an editor checkerboard in their outer packaging margin. Seat
> the authored card bounds inside a native rectangular mask; do not display or mutate those packaging
> pixels."*

It compensates with a `RectMask2D` plus an art surface over-scaled to
`(-.036, -.136) .. (1.036, 1.112)` — **applied unconditionally to every illustrated card.**

### ⭐ MEASURED 2026-09-02 (opaque bbox vs png size). Do NOT re-derive this.

| card | png | packaging margin (L T R B) |
|---|---|---|
| buildings | 1994x789 | 64, 61, 62, 79 |
| defense-report | 1805x871 | 67, 53, 60, 80 |
| **defense** | 1684x934 | **72, 81, 14, 147** |
| monthly-ledger | 1805x871 | 60, 86, 64, 67 |
| quests | 1774x887 | 47, 62, 47, 74 |
| realm-store | 1798x875 | 41, 64, 44, 78 |
| research | 1789x879 | 30, 87, 34, 97 |
| troops-locked | 1981x793 | 37, 49, 36, 48 |
| **game-guide** | 1821x864 | **0, 0, 0, 0 — TIGHT** |
| **raids** | 1774x887 | **0, 0, 0, 0 — TIGHT** |
| bag / equipment / skills / loadout (new) | 653x301 | 0, 0, 0, 0 — TIGHT |

**Two conclusions, both load-bearing:**

1. **`game-guide` and `raids` are being over-scaled and clipped IN THE SHIPPED BUILD.** They carry no
   margin, and the fixed offset crops one off them anyway. This is a live visual defect that predates
   the owner's new art — not a hypothetical about future assets.
2. **A single fixed offset cannot be right for eight different margins.** `defense` is B147 against
   T81; `research` is T87/B97. The constant is approximately right for a few cards and wrong for the
   rest. Everything is mis-cropped; the two tight ones are merely the most obvious.

### ⭐ OWNER RULING 2026-09-02: **"fix it that way"** — per-sprite measurement.

Derive the correction from each sprite's OWN opaque bounds at load time and apply only what that
sprite actually needs. A tight sprite (margin 0) gets NO correction and renders 1:1. A margined sprite
gets exactly its own margin removed, per edge, not a shared guess.

This self-corrects for margined art, tight art and any future art, needs no per-card constants, and —
importantly — **requires zero re-exports.** The option of re-exporting the existing ten is RULED OUT;
it is unnecessary under this approach and touches art the owner did not ask to change.

Implementation notes:
- `Sprite.textureRect` / `Sprite.rect` and the texture's pixels give the bounds. Reading pixels needs
  `isReadable`; if that is not set on these importers, prefer a route that does not require it (e.g.
  authoring the trim into the sprite's own rect via importer settings, or computing once and caching)
  — **state which you chose and why.**
- Whatever you choose must not cost a per-frame allocation or a texture read per card draw.
- If a sprite's bounds cannot be determined, fall back to NO correction (render 1:1) and
  `FlowTrace.Warn` naming the card. A wrong crop is worse than an uncropped margin.

## Acceptance criteria

1. All four hero cards render their illustration, unstretched and uncropped, at every captured resolution.
2. `game-guide` and `raids` render 1:1 and UNSTRETCHED — they are broken today and must be
   visibly corrected. The eight margined cards must render at least as well as they do now; prove it
   with before/after captures, do not assume it.
3. `Available == false` still visibly differs from available WITHOUT relying on colour alone (owner is
   red/green colourblind). The current path tints to `(.48,.48,.50,.82)`; a tint is a colour-only
   signal and needs a non-colour partner.
4. `button.transition` stays `ColorTint` — the comment at `:130` records that illustrated cards must
   never SpriteSwap to a blank face on hover.
5. Verified by a fresh capture PNG that a human OPENED. `UI_CAPTURE_OK` proves pixels were written,
   not that a card looks right; that marker was green over a wave-clear panel carrying four visible
   defects the same night.

## What NOT to touch

- ⛔ The four `PanelId` destinations (`Inventory`, `EquipmentPanel`, `HeroSkillTree`, `HeroLoadout`) —
  routing works; only the art is missing.
- ⛔ `Assets/Resources/UI/ElarionMedieval/cards/*.png` for the existing ten, unless taking option 2
  WITH the owner's ruling.
- ⛔ The talent tree panel itself — WO-1310 owns that and is a separate lane.
