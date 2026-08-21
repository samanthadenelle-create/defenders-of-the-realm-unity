# WORK ORDER 1106 — The "can't afford" reason is unreadable behind the red footprint

**Status:** READY TO IMPLEMENT (⚠ serialize behind the WO-1033/D10 Done-button lane — same file)
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1106 -> 1107 in the same edit
**Lane:** Build-mode presentation (`BuildHudController`). ⚠ **Touches the SAME file as the Done
restyle lane** — do not run both at once.
**Provenance:** owner F8 **seq 2504**, `Main_Castle_Overworld`, verbatim:
> *"the reason you cant afford is not readable as its behind the red shaded outline footprint"*

---

## 1. Traced at source

The block reason is **appended to the ghost's name+cost pill**, not given its own surface:

- `BuildHudController.cs:884` `TrackGhost(screenPoint, valid, blockedReason)` stores it at `:893`.
- `:955-957` — when placement is invalid the pill text becomes `"<name+cost> - <reason>"`.
- `:959` — the pill text is then **tinted salmon** (`0.93, 0.55, 0.45`) for the invalid state.
- The pill is `GhostPillW 620 x GhostPillH 56` (`:~150`), floats `GhostPillLiftPx 96` above the ghost
  anchor, and the label runs `enableAutoSizing` with `fontSizeMin 14` and `NoWrap` (`:405-414`).

**So three things compound into "unreadable":**
1. The reason shares a fixed 620x56 pill with the name and cost, and `NoWrap` + autosize means the
   longer invalid string **shrinks toward 14px** instead of wrapping — the reason is the part that
   loses.
2. The pill floats only 96px above the anchor, so on a larger structure the **red shaded footprint
   occupies the same screen band**, and a translucent pill fill lets the red bleed through the glyphs.
3. The invalid state tints the text **salmon/red ON the red footprint** — lowest possible contrast,
   exactly where legibility matters most.

## 2. ⚠ It is also the open colour-only defect

Canon's carried-forward list still names **the build placement ghost** as *"still colour-only and
OPEN"* (valid/invalid on the red/green axis) — in the one mode where the player commits resources,
and the owner is red/green colourblind. This ticket is the moment to close that: the reason must be
carried by **words on a legible plate**, with hue as at most a redundant reinforcement.

## 3. Fix shape

- **Give the reason its own surface.** Separate it from the name+cost pill — an obsidian plate
  (kit `ToastCard`/`ObsidianFill`, built through `ElarionUiKit`; the `[ui-obsidian]` ratchet hard-fails
  hand-rolled UI) with an **opaque** fill so nothing bleeds through. Do not tint the reason text red.
- **Seat it clear of the footprint**, not merely lifted 96px: place it against a HUD band that the
  ghost never occupies, or lift by the ghost's MEASURED screen-space footprint height rather than a
  constant (the WO-1035 lesson — derive from measured bounds, never a fixed metre/pixel guess).
- **Let it wrap.** A reason that shrinks to 14px to avoid wrapping has chosen the wrong axis.
- Keep `raycastTarget = false` (it must never eat build input).

## 4. Acceptance

- Attempting to place an unaffordable structure shows the reason at full size, fully legible, with the
  red footprint visible behind it but never through the glyphs — proven by a **device screenshot at
  2670x1200**, not a batchmode capture.
- The same screenshot read in greyscale still says why placement failed (words carry it, not hue).
- The reason does not overlap the D14 verb rail or the quick-tab column at either supported aspect.
- Valid placement is unchanged.

## 5. What NOT to touch

- The ghost's own footprint rendering / `PlacementGrid` claim math (WO-986/WO-972 territory).
- The Done control's seat — that is the concurrent lane; land this AFTER it.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `BuildHudController.cs:1141-1145` — opaque reason plate unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
