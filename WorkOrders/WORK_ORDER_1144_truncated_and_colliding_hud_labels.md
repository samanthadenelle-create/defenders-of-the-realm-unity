**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).

# WORK ORDER 1144 - Truncated and colliding HUD labels in the town

**Minted:** 2026-08-22 (CLI, banner bumped 1142 -> 1145 in the SAME edit)
**Lane:** HUD / UI. **Class:** presentation defect, captured.
**Evidence:** `autopilot-runs/*/break_24_error.png` from the 2026-08-22 headed fleet run
(reproduced identically in all 8 runs). THE SCREENSHOT IS THE DATA.

## WHAT THE CAPTURE SHOWS

1. **`"Tap to collec"`** - the Collectors chip (top right) truncates mid-word. Not an ellipsis, a
   cut. Reads as a rendering fault rather than an abbreviation.
2. **`"Manag..."`** - the Manage face on the bottom action bar is ellipsised while every sibling
   (Build / Bag / Raids / Quests) fits. It is the longest label in a fixed-width slot.
3. **"TIER UP! Initiate"** renders across the world tree at the screen centre, overlapping the
   scene and unreadable against it.
4. **"Wave 1" / "Next wave in 45s"** collides with the "Start Now" button directly beneath it -
   two live elements occupying the same band.

## NOT IN SCOPE - VERIFIED WITH THE OWNER

The bar shows FIVE faces (Build / Bag / Raids / Quests / Manage) and canon section 7 says six.
**That is correct behaviour: Talk is CONTEXT-GATED and appears only near an NPC** (owner,
2026-08-22). Do not "restore" a sixth face.
*(Section 7's wording does not mention the gating, which is what made it read as a defect. Worth an
owner-confirmed canon touch-up, but not part of this ticket.)*

## CONSTRAINTS

- Code-built uGUI only - UXML does NOT work in player builds.
- `MinTouchPx = 112` - a label fix must not shrink a touch target below the floor.
- Landscape; the capture is 2670x1200. Verify any fix at more than one aspect - a label that fits
  at this width may still cut at another.
- The owner is RED/GREEN COLOURBLIND: never resolve an overlap by recolouring alone; move it,
  reflow it, or give it its own band.
- ⛔ Player-facing sentences come from `canon-strings.json` (both copies, byte-identical,
  ASCII). If a string needs shortening, shorten it THERE - never inline at the call site.

## ACCEPTANCE

- [ ] No truncated word on the Collectors chip or the action bar at 2670x1200 and at one other aspect
- [ ] "TIER UP!" does not overlap the world tree or the wave banner
- [ ] The wave countdown and "Start Now" do not share a band
- [ ] Verified by SCREENSHOT, not by reading layout code - this ticket exists because the numbers
      looked fine and the frame did not
