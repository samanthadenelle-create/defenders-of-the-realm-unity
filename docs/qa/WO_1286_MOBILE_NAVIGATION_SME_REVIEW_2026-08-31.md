# WO-1286 mobile navigation SME review - 2026-08-31

## Verdict

**PASS - strongest shippable navigation design within the approved Obsidian visual system and the
existing gameplay-authority boundaries.** The redesign replaces destination recall with a stable,
recognition-led card hierarchy; makes Back, Close and Done predictable; and keeps common content at
two selections or fewer. No open finding in the WO-1286 surface is severe enough to block release.

The pre-release expansion also reviewed the complete captured panel inventory. Build card bounds,
Hero portrait carousel touch widths, and Rumor Board's unused kit header were tightened; the final
global suite is geometry-clean across 100/100 canvases and touch-clean across 100/100 panels.

## Review scorecard

| Discipline | Assessment | Evidence |
|---|---|---|
| Information hierarchy | Pass | Five stable peers: Realm, Build, Manage, Hero and Journey. Categories live below their owning peer instead of competing on the HUD. |
| Recognition and legibility | Pass | Cards pair a word label, icon or monogram fallback, and short purpose copy. Locked states state the requirement in words and never depend on color alone. |
| Reachability and efficiency | Pass | HUD to workspace is selection one; card to content is selection two. Raids have one stable home under Journey and Store one stable home under Realm. |
| Consistency | Pass | Migrated surfaces share Obsidian chrome, title placement, Close-to-world behavior and Back-at-depth behavior. Build placement uses the explicit Done contract. |
| Reversibility | Pass | `NavigationStack<T>` provides deterministic root, push, replace and one-step back semantics. Back refuses at root; Close always exits to play. |
| Accessibility | Pass | The focused suite reports no control below the 112 px authored touch floor, no intersecting controls, and no text/plate geometry failures at all three landscape targets. The HUD uses five wide labeled peers. |
| Resilience | Pass | Missing art falls back to a readable monogram. Refresh redraws current state without duplicating history. Build and Manage keep their existing catalog, progression and placement authorities. |
| Product integrity | Pass | No gameplay, economy, billing, price, reward, persistence or save-schema authority changed. Existing `PanelRouter`, `RaidEntryGate` and placement seams remain authoritative. |

## Findings resolved during review

- Removed Build subtitle/title collision and reserved a stable copy band inside every card.
- Corrected stale shadow-title updates that could overwrite Close.
- Fixed Manage's pre-view-model empty launcher and made all four categories spatially stable.
- Replaced ambiguous gray-only locks with explicit requirements and noninteractive locked cards.
- Raised Manage content above the Close band and removed duplicate initial rendering.
- Replaced Build's asynchronous duplicate paint paths with atomic shared `Refresh()`.
- Removed the conditional duplicate Raid HUD destination; Journey is now the sole Raid home.
- Replaced the direct Store HUD duplicate with Realm and retained Store as a Realm card.
- Reduced the action deck to five active peers so labels remain readable at the mobile reference.

## Verification record

- Final EditMode XML, 2026-08-31 07:41:35: **1,033 passed, 0 failed, 0 skipped**.
- Data regression marker: **REGRESSION_OK 332/332**.
- Focused capture marker: **NAVIGATION_CAPTURE_OK 15/15 frames; geometry=clean; touch=clean**.
- Full release capture: **UI_GEOMETRY_OK 100 canvases; UI_TOUCH_OK 100/100 panels**.
- Target surfaces: 1920x1080, 2340x1080 and Seeker-reference 2670x1200.
- The Realm, Hero, Journey, Manage and Build PNGs were opened and reviewed at both baseline and
  Seeker-reference sizes. Titles, purposes, lock explanations and Close/Back affordances were legible;
  no clipping, collision or false affordance remained in the scoped surfaces.

## Executive decision

Ship this architecture as the navigation baseline. Keep transactional and queue-heavy destinations
as rows after entry; use cards for destination recognition. New top-level destinations must justify
displacing one of the five peers rather than adding another HUD face. Camera/wall clipping and legacy
panel geometry remain separately owned defects and must not be folded into this navigation result.
