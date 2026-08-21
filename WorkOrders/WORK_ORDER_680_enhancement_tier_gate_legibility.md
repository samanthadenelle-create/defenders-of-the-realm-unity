**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 680 — Enhancement panel: Tier-2 unlock dead-end + "Unlock Maxed" state bug

**Status: READY TO IMPLEMENT** (owner report + screenshot 2026-07-12, preview `9ncz1sks9`:
"I can complete everything on Tier 1 but there is no upgrade to unlock Tier 2 — I assumed
upgrading the building to level 2, but not seeing that").
**Lane:** UI/Progression (follow-up to the shipped WO-675 redesign). **Type:** EXISTING.

## Read-only RCA (cited; CLI captures the proving state before editing)

Two progressions render into ONE band header, and both of its states are wrong in the screenshot:

1. **"Unlock Maxed" button** — the locked band's header CTA is the VILLAGE-tier unlock
   (panel comment: "the synthetic Unlock action rides the first locked tier band's header").
   Its label composes "Unlock" + `CostFor(VillageTierRowId)`, and `BuildingUpgradeVM.
   PrependVillageTierRow` (:385) sets that cost string to **"Maxed"** when
   `VillageTierService.IsMax` — the owner's save is at Village Tier 3/3, so the button renders
   "Unlock Maxed" and does nothing. **When IsMax, the header must render NO action at all**
   (the village gate is open; it isn't the blocker).
2. **"Unlock Tier 1 first"** — the actual blocker is the BUILDING's own tier ladder:
   `BuildCity` reads `CurrentTier = ModifierService.TierOf(buildingId)` and Tier 2 requires
   Tier 1 OWNED. The Tier-1 tile ("Tier 1 — Ignite the Forge", 700W/450F, shown affordable +
   unowned in the screenshot) **IS the "upgrade the building" action the owner was looking
   for** — but nothing says so. It reads as a perk, not as the tier key. That's the legibility
   dead-end: the gate text points at "Tier 1" while the thing to tap looks like ordinary loot.
3. **Verify a real gate bug while in there (§12):** buy Tier 1 in a capture session and confirm
   `ModifierService.TierOf` advances + the Tier-2 band re-renders unlocked in the SAME open
   panel (the VM raises Changed on Select — prove the refresh path, don't assume).

## Fix (bounded, View + VM strings only — no progression logic changes)

- **Header CTA states:** village gate open (IsMax OR requirement met) → render the requirement
  text ONLY when the building tier is the blocker ("Unlock Tier 1 below to open Tier 2"), no
  button. Village gate closed → the Unlock button with a REAL cost ("Unlock · 500 crystals").
  Never compose "Unlock" + a state word. (Colorblind law: state carried by text, as ever.)
- **Make the tier tile read as the key:** the tier-N tile gets a distinct treatment — crown
  glyph + "UPGRADES FORGE TO TIER N" sub-line (verbiage law: Enhancement/Unlock language) — so
  "upgrade the building structure" is visibly THAT tile. On owning it, the next band unlocks
  in-place (prove per #3).
- **Gate copy names the action:** "Unlock Tier 1 first" → "Unlock 'Ignite the Forge' to open
  Tier 2" (compose from the tier tile's display name — data-driven, no hardcoding).
- Add the standard step-in/out traces on band-state resolution (which gate: village vs
  building-tier vs cost) so the next "why is this locked" is one log read.

## AMENDMENT — mockup conformance (owner, 2026-07-12, Farm Enhancements screenshot: "same
## with food, refer to the mockup we did")

The Farm panel (resource-building `BuildResource` path) shows three drifts from the APPROVED
mockup — fix for ALL buildings (Farm/Lumbermill/Forge resource ladders AND city perk grids),
one shared band builder, never per-screen:

- **A1 Footer clipping:** the Tier-2 tile renders half-hidden UNDER the wallet-chip strip. The
  band host must RESERVE the footer band inside the body zone (the exact close-band-reservation
  pattern already documented in ElarionUiKit — "reserve the band AT THE FACTORY"; scroll content
  never extends beneath chips/Close). Fix at the factory/zone level, not per screen.
- **A2 Tile anatomy per mockup:** tiles currently stack a big numeral + "Level 1" (redundant
  twice) + effect + state. Mockup anatomy: icon (or crown-tier glyph ONCE) / name / one-line
  effect ("+20 Food per tick") / state-or-cost line. Drop the duplicate numeral row.
- **A3 Sparse-grid law:** a band with one tile renders its remaining columns as EMPTY slot
  plates (dim, no interaction) so the band reads as a grid, per UI_BLINK_TEMPLATE_CANON §4 and
  the approved mockup — never one floating tile in a void.
- **A4 Verify on BOTH panel families:** the resource ladder (Farm) and the city perk grid
  (Forge) share the band builder after this — screenshot-vs-mockup check on each (canon §7).

## AMENDMENT UNPARKED (owner F8 2026-07-13 23:39, verbatim: "does this look at all like the
## mock up?" -> CLI verdict: DOES NOT MATCH; owner: "it's either it matches or does not")

Fresh captured evidence (Forge Enhancements panel, exe 18:18, screenshot
`flag_20260713-232518_05.png`), four divergences beyond A1-A3:
- **A5 The Tier-1 card sits on a PURPLE filigree plate** — not the obsidian palette (recessed
  near-black well per the mockup / UI_BLINK_TEMPLATE_CANON).
- **A6 Card icons render pixelated over an alpha CHECKERBOARD** — sprite import/alpha defect
  (missing-texture class), not design.
- **A7 Tier-2 card clips its own bottom text line** (A1's clipping class confirmed live —
  "Unlock 'Ignite the Forge' to open Tier 2" cut at the plate edge).
- **A8 Chip-row number sizing inconsistent** (the highlighted 50k renders ~3x its siblings) —
  if scale means affordable/selected, the meaning must be carried by text/shape, not size alone.

**VERIFICATION LAW (owner rulings, this session): "we verify with data — screenshot versus
mockup" · "if not correct, go again till matches" · "I want side-by-side image proof."**
The fix loop is machine-side: implement -> build -> capture the SAME panel state -> compose a
SIDE-BY-SIDE image (capture | mockup) -> compare -> iterate until they match. The DELIVERABLE
to the owner is the side-by-side image pair itself (the image-pair sign-off discipline);
words like "close" or "structurally matches" are not acceptance. Binary: matches or does not.

## Acceptance
- [ ] At Village Tier max: no dead "Unlock Maxed" button anywhere; requirement text names the
      tier tile. Below max with a locked band: button shows real crystal cost and works.
- [ ] Buying the Tier-1 tile immediately opens the Tier-2 band in the same panel session.
- [ ] Owner repro path on preview: Forge panel → buy Ignite the Forge → Tier 2 usable.
- [ ] Farm/Forge/Lumbermill: no tile ever clips under the footer chips; tiles match the mockup
      anatomy (no duplicate numeral+Level rows); single-perk bands show empty slot plates.
- [ ] COMPILE_GATE_OK + fleet panel probes green + owner felt-pass against the mockup (PO closes).

## What NOT to touch
Tier costs/gating rules (VillageTierService, BuildingTierCatalog) · the WO-675 band/chip
layout (working) · §0/§11 as ever.

*Cross-refs:* WO-675 (the redesign this polishes) · WO-432 (tier gate) · screenshot on ticket UPG-1.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
