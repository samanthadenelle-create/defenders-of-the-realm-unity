**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 699 — Hero-select ability chips: empty names + stale F slot → one data source (SEL-1)

**Status: READY TO IMPLEMENT** (UI-seat live-screenshot ticket SEL-1, 2026-07-13: the hero-select
screen renders empty Q/F/E/R ability chips — slots show, names blank, and an F slot survives the
WO-614 Q/W/E/R rail ruling).
**Lane:** UI/Data (hero-select). **Type:** EXISTING (display/data-source defect).

## UI-seat triage (screenshot-evidenced; verify cause from code per §12)

The layout code is innocent — `HeroSelectController` already renders slot + name pairs from
`HeroCatalog`, and `abilities.json` has the Knight kit fully authored with names ("Heroic Leap"
et al.). The break is in the hop between them; candidate roots (CLI cites the real one):
1. the catalog mirror drops the `Name` field;
2. the name label renders zero-width in the row band;
3. the catalog rows are LEGACY hand-authored entries — which would also explain the stale
   F slot surviving the WO-614 Q/W/E/R rail ruling.

## The fix (either way, one source of truth)

- **Slots AND names both re-sourced from `abilities.json` + the WO-614 rail mapping** —
  no hand-authored duplicates anywhere in the select screen's data path. The stale F slot
  dies as a consequence (the rail mapping defines Q/W/E/R only).
- Instrument the hop (`[Flow:HeroSelect]` step on catalog row build: slot, abilityId, name,
  resolved-vs-empty) so the actual dead link is a one-read verdict before the edit (§12).

## RULED (PO pin delegated to CLI, taken per the recommendation, 2026-07-13)

**Hero-select shows the FULL class kit as an ASPIRATIONAL PREVIEW** — the select screen sells
the fantasy of the class. Post-WO-614 a fresh Knight owns only Q (W/E/R unlock through the
tree), so:
- All four Q/W/E/R chips render with their real names/icons from the kit data.
- Not-yet-owned skills carry a quiet unlock cue AS TEXT (e.g. sub-line "Unlocks through the
  talent tree") — never color-only, never a mystery lock glyph (tofu risk; ASCII).
- **The data answers, not the screen:** owned-vs-unlockable derives from the same
  abilities/rail data the game uses (no select-screen-only flags).

## Acceptance
- [ ] Hero-select shows Q/W/E/R (no F) with real ability names from abilities.json; Knight
      spot-check: Q "Heroic Leap" + the three tree-unlocked kit skills named.
- [ ] Unlockable skills readable as such in greyscale (text cue).
- [ ] grep proves no hand-authored ability name/slot literals remain in the select path.
- [ ] [Flow:HeroSelect] names the data source per chip; COMPILE_GATE_OK + fleet panel probe +
      owner felt-pass (PO closes).

## What NOT to touch
abilities.json content (authored) · the WO-614 rail/loadout runtime behavior · locked-class
carousel scope (WO-559/584 canon).

*Cross-refs:* ticket SEL-1 (UI-seat live screenshot) · WO-614 (Q/W/E/R rail ruling) ·
`docs/UI_BLINK_TEMPLATE_CANON.md` · [[hud-ability-routing-skilltree-to-hotswap]] (fixed class
kit = the arc identity this screen previews).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
