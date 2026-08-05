# WORK ORDER 908 — Side menu: duplicate gear icon + wrong icon formatting

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-05 (CLI, main-line block; banner bumped 908 -> 909 in the same edit)
**Lane:** HUD / UI — presentation only. No gameplay, no data, no scene edits.
**Reported by:** Owner (PO), felt-test on Seeker, native **2670x1200** landscape, build `2026.08.05.312200`
**Routing:** Owner is handing this to the UI team.

---

## Screenshot (attached)

`docs/qa/screens/2026-08-05/gear-menu-double-icon.png`

Captured live from the device at the moment of the report, full native resolution, in-repo so it
travels with this WO. **Open it before starting** — the defect is positional and does not read from
prose alone.

---

## Symptom (owner's words)

> "When I click on the button for the gear on the left side, it does expand the menu, and you can see
> from the screenshot that that does so, but then it adds a second gear icon out of place. And also it
> looks like the formatting of the first gear is wrong on color."

**The menu itself works.** Tapping the gear expands the left-side panel correctly and every row is
present and legible: `Chat / Leaderboard / Music / Settings / Pause`. This ticket is **only** about the
two gear glyphs drawn over that panel.

## What is actually on screen (verified from the capture)

Two gear icons render, in two different styles, neither aligned to the row it belongs to:

| | Where it is | How it looks | What is wrong |
|---|---|---|---|
| **Gear A** | Far-left edge, on the **Music** row | Gold/tan fill, inside its own bordered box | Sits on the WRONG ROW (Music, not Settings) and **hangs outside the panel's left border**, breaking the panel edge |
| **Gear B** | On the **Settings** row | Dark grey outline, no box | **Drawn on top of the "S" in "Settings"**, obscuring the first letter of its own label |

So the owner's "second gear out of place" = **Gear A**, and the "formatting wrong on colour" is the
**mismatch between the two treatments** — one gold-filled-and-boxed, one grey-outlined-and-bare. They
are clearly meant to be one icon, one style, seated in one row.

## Acceptance criteria

1. **Exactly ONE gear icon** renders for this menu. The duplicate is gone.
2. That icon is **seated inside its row**, vertically centred, and **fully inside the panel's left
   border** — nothing overhangs the frame.
3. The icon **does not overlap its own label text**. The word `Settings` reads in full.
4. Icon styling matches the rest of the kit's iconography — **one treatment**, not a gold-boxed variant
   next to a grey-outline variant. If a boxed treatment is correct, every row icon uses it; if bare is
   correct, likewise.
5. Verified at **2670x1200** (the Seeker's real surface) **and** 2340x1080 and 1920x1080. The 2670x1200
   case is mandatory — it is the one the defect was reported on and no current capture uses it.
6. No change to the menu's behaviour, row order, labels, or the expand/collapse interaction.

## Constraints (project law — non-negotiable)

- UI is **code-built uGUI via `ElarionUiKit`**. **No UXML / UIDocument** — it does not render in player
  builds.
- **ASCII-only TMP strings.** Non-ASCII glyphs render as tofu on device.
- **Never convey meaning by colour alone** — the owner is red/green colourblind.
- Touch targets honour `MinTouchPx = 112`; font floor `30`.
- ⚠ **Bands must be FIXED PIXELS, never a fraction of parent.** `ElarionUiKit.ClampMinTouch` grows a
  sub-floor control **symmetrically about its centre**, which is the documented root cause of this
  project's repeated overlap/overhang defects (it broke WO-852, WO-868, WO-865, and both the FOUND
  YOUR TOWN modal and the founding Echo card on 2026-08-05). **If this gear is positioned by a parent
  fraction, that is very likely the root cause of the overhang — check it first.**
- **Do not hand-edit any `.unity` scene** (resave-corruption history). Runtime/code only.

## Investigation hints

- Find the builder for this left-side menu (rows `Chat / Leaderboard / Music / Settings / Pause`) and
  check whether the gear is added **twice** — e.g. once by a shared row factory that already stamps a
  leading icon, and again by an explicit per-row call. A duplicated-icon symptom with two *different*
  styles strongly suggests **two different code paths each adding one**.
- The two distinct treatments are the tell: locate both call sites before changing either.
- Canon note: the shared kit collapsed button **colour** to grey game-wide (2026-07-16) but never
  collapsed **style**, so sibling controls can still diverge visually. Check whether the same
  half-applied standardisation explains the gold-vs-grey mismatch here.

## What NOT to touch

- The menu's rows, labels, ordering, or open/close behaviour.
- `ElarionUiKit` / `ElarionUiKitObsidian` shared kit files — unless the root cause is genuinely in the
  kit, in which case **say so and stop**, because a kit change has a game-wide blast radius and needs
  an owner ruling first.
- Anything outside this panel.

## Verification before "done"

- `COMPILE_GATE_OK` + brace balance on every `.cs` touched.
- A **screenshot at 2670x1200** showing one correctly-seated gear, attached to the RESULT file.
  Compile-green never proves a panel looks right — the owner must never be the first to see it broken.
- Write `WorkOrders/WORK_ORDER_908_gear_menu_duplicate_icon.RESULT.md` when complete.

---

## Also visible in the same capture (NOT part of this WO — logged separately)

Recorded here only so the UI team does not think they were missed:

- The wave banner text (`Wave 3` / `Next wave in 160s`) at top-centre is **clipped behind** the `SE`
  compass badge and the `Start Now` button.
- The right rail shows the word **`Resources` twice** — once as the chip label and again as the
  expanded panel header.

Neither is in scope for WO-908. Do not fix them here.
