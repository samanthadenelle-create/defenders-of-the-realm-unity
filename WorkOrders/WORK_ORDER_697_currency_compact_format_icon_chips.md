**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 697 — Currency readouts: compact numbers + icon-first chips + content-fit width (RES-1)

**Status: READY TO IMPLEMENT** (owner-relayed from the UI seat's live-screenshot ticket RES-1,
2026-07-13: six-digit resource values clip/truncate in the resource panel).
**Lane:** UI (kit + View). **Type:** EXISTING (display defect) + one kit-level rule.

## The three moves, in order of value (owner-stated)

1. **Compact formatting at five digits and up** — `98.6k`, `100k` (the grammar already in the
   approved mockups; genre-standard once economies hit six figures). A shared **`CompactNumber`
   formatter lands in the kit ONCE** (`ElarionUiKit` / `ElarionUi`), so wave rewards, costs, and
   any future six-digit readout inherit it rather than each surface rediscovering the truncation
   bug. Thresholds: < 10,000 renders verbatim; >= 10,000 renders compact with one decimal below
   100k (`98.6k`), none at/above (`100k`, `1.2m` class when it comes).
2. **Icon-first rows** — drop the text labels entirely; the mirrored currency icons carry
   identity (colorblind-safe: icon = shape identity, never color-only). This makes the resource
   panel THE SAME `CurrencyChip` component the panel footers already use — **one widget
   everywhere**, no bespoke resource-row rendering.
3. **Content-fit width as the safety net** — the chip/panel sizes to its content so nothing can
   ever clip again, whatever the number.

## Kit-level rule (permanent — bake into the kit, not a comment)

**Single-line fit/ellipsis on a currency VALUE is forbidden — format the number instead.**
A currency readout never shrinks below the font floor and never ellipsizes; it compacts via
`CompactNumber`. Enforce at the kit seam (the chip builder calls the formatter itself), so no
caller can reintroduce the bug.

## Reuse / reconcile (do not greenfield)

- `CurrencyChip` (panel footers, WO-675/676/693 grammar) = the one widget; the resource panel
  adopts it.
- The mirrored `currency_*` icon set (`RpgUiCatalog`).
- Old board rows **WO-431** (resource panel Obsidian frame + per-resource icons + dynamic width)
  and **WO-440** (collapsed-to-edge resource panel) overlap this surface — reconcile: this WO
  supersedes WO-431's width/icon scope (note it on the rows); WO-440's collapse behavior is
  untouched/separate.
- Exclusions: `FitBlock`/`FitSingleLine` remain valid for NON-currency text (WO-693's floor
  rule); this WO only forbids them on currency values.

## Acceptance

- [ ] 98,600 wood renders "98.6k" beside its icon in the resource panel, wave report, and any
      cost chip — one formatter, spot-check all three.
- [ ] Resource panel rows are icon + value only (no text labels); identity readable in
      greyscale (icon shape carries it).
- [ ] No currency value anywhere can ellipsize or shrink below the font floor — grep proves the
      chip builder owns the formatting (no per-surface number formatting remains).
- [ ] Content-fit: a 7-digit test value neither clips nor overflows the chip/panel.
- [ ] Desktop + phone-aspect verified by screenshot; COMPILE_GATE_OK + HUDUI oracle green +
      owner felt-pass (PO closes).

## What NOT to touch

Economy values/math · non-currency text fit rules (WO-693) · panel layout beyond the chip swap
+ content-fit sizing.

*Cross-refs:* ticket RES-1 (UI-seat live screenshot) · WO-675/676/693 (chip/state grammar) ·
WO-431/440 (superseded/adjacent scope — banner their rows) · `docs/UI_BLINK_TEMPLATE_CANON.md`.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
