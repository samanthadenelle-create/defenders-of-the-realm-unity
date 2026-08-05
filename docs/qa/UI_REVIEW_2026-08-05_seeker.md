# UI REVIEW — Seeker device captures, 2026-08-05

**23 screenshots, native 2670x1200, real device pixels.** Build `2026.08.05.312200` /
commit `2caca14a` — *"WO-864..883 UI rework, WO-869/870 VFX, WO-871 build workers, WO-855 economy
rebalance."* Captures live in `docs/qa/screens/2026-08-05/`.

**Everything below survived the UI rework.** All 8 previously-ticketed defects reproduce; several
are materially worse than their tickets describe. 26 further findings were not on any list.

> **One-sentence verdict:** the kit standardised *colour* but never standardised *shape, size,
> baseline, or where text is allowed to live* — so almost every panel is a set of correctly-coloured
> pieces that do not line up, and in four places text or buttons are drawn over content the player
> needs to read.

---

## P0 — breaks comprehension or blocks the task

**1. The build-mode placement ghost is a flat solid green cylinder.**
Valid-vs-invalid placement signalled by **colour alone, on the red/green axis**, in the mode where
the player commits resources. Hard project rule broken in the most consequential place. It is also
an opaque untextured primitive — no tower silhouette, no footprint, no radius, no grid.
*(`seeker-archer-tower`, `seeker-tutorial-wave`)*

**2. Hero status bars are unlabelled and distinguished by colour only.**
Green / blue / yellow stacked top-left. No icons, no numbers, no labels. In `seeker-dungeon-softlock`
the hero is at roughly **4% health** and the only signal is a green sliver a fingernail wide. The
most-looked-at element in the game, unreadable and rule-breaking.

**3. The wave banner is unreadable.**
"Wave 6 / Next wave in 186s" in pale gold with **no backing plate**, behind BOTH the compass badge
and the Start Now button — a three-element pile-up in one 200x150 patch. The player cannot read the
wave number or the countdown: the clock of the core loop.

**4. FOUND YOUR TOWN slices its own explanation.** *(fix committed `13c0e728`, unverified on device)*
Buttons flush, rounded-top over square, drawn **over the third line of the paragraph** — and roughly
**35% of the modal below them is empty**. The copy was cut for no reason.

**5. The dungeon's green pillars are placeholder geometry.** *(fix committed `219924ca`, unverified)*
`crop_bars` settles it: perfectly uniform flat green, **zero lighting response**, against a fully
shaded brick wall behind. In `crop_floor` one ends in mid-air. ~15% of screen, splitting the view in
thirds — and green, the same green as "health".

**6. The numeral "1" renders as a bare vertical stroke.** ← NEW, and everywhere.
The display font's `1` has no foot and no flag, so at HUD size it reads as a pipe or capital I:
"Echoes **I/6**", "SKILL **|75**", resources "**2|8 / |5 / ||0 / 45|**", gold "**||3**".
**Every number in the game is being misread.** Fix: a font with a footed 1, or tabular figures for
all numerics.

**7. Dungeon combat controls are a pile, not a layout.**
Four controls: a yellow burst in a circle; crossed axes in a square inside a circular gold ring; a
blue ability whose **square artwork is clipped by a round mask**; and "Dodge/ Attack" — a circle of
wrapped text with a trailing slash and no icon. **Three of the four overlap.** Below them a pill
whose sprite **has no alpha** — the opaque grey rectangle is visible. This is the surface the player
must hit under pressure. *(`crop_npc`)*

**8. The Low-Oil warning is clipped in half.**
Bottom 40% cut off by the HUD plate above it, tiny dark-on-red text, red-only. A critical failure
warning for the lantern mechanic that cannot be read.

---

## P1 — visibly broken, costs trust

**9. Victory screen** *(fix committed `c374bd44`, unverified)* — content in the top third, **~55%
of the plate below Continue empty**. Three **plain gold diamonds** for the rating: no points, no
outline, no empty slots, so 3-of-3 vs 3-of-5 is indistinguishable — sitting *on* the "Time 0:14"
line. Crossed-swords icon over the "f" in "safer". Three reward icons in three treatments, one a
**white box**. "Victory!" centres ~60px right of Continue's centre.

**10. Founding Echo card** *(fix committed `ee2a2855`, unverified)* — overflows in **both**
directions: "keeper" entirely on the metal frame, and the flavour copy runs **past the plate's right
edge** ("you" half on black, half on metal). Three buttons, three shapes, three widths, three
heights, no shared baseline — and **"Close" sits in the middle**, between the two positive actions.

**11. "Resources" appears twice and the two overlap** — a collapsed chip and an expanded panel whose
header repeats the word in a different size and colour, overlapping the chip, **clipped by the right
screen edge**, rows **icon-only with no names**.

**12. Side menu** *(WO-908)* — double gear confirmed, plus: the panel runs **off the left and bottom
edges**; the Music row is ~17px taller than its siblings; row gaps are 30/25/20/30px; and the **Heart
of Elarion bar draws on top of the menu**.

**13. Resources presented two incompatible ways** — build mode shows **letters** (W/I/F/C/G), the hub
rail shows **icons only**. Neither decodes alone, and they never co-occur to teach each other.

**14. Village content inside the dungeon — four bleeds**, not one: the Heart bar, the "Echoes 1/6"
town chip, a **daylit tan town wall**, a **town NPC in a headscarf**, and an anvil. Two lighting
environments in one frame. *(`crop_npc`)*

**15. Action bar** — "Upgr..." truncated; **three type sizes in one row** because each button shrinks
its own label; six widths, four top edges; and **"Talk" is permanently in the other style** (taller,
rounded, lighter) so it looks selected in every capture.

**16. Hero select** — **four empty grey boxes** under the ability line, unlabelled; stat pips read as
one grid block so rows can't be matched to labels; description **truncated mid-sentence**; "powered
by SKR" floating with no plate; **bottom 45% empty**; no confirm CTA and no Back button visible.

---

## P2 — craft level (abbreviated)

Rounded-vs-square split on every multi-button screen · framed screens mis-centred (title centres
beside the shield boss, buttons centre on the frame) · **no scrim behind any modal**, live HUD leaks
at the edges · **the frame's top-right corner sprite is broken** (asset, not a one-off) · right rail
has three different right edges, the "!" chip runs **off-screen** · PLACE and Cancel visually
identical · two different "skip tutorial" affordances · action bar **draws over the hero's legs** ·
**thumb reach**: controls sit dead centre on a 2670-wide landscape phone, bottom-right is empty ·
gold-bordered box does **four unrelated jobs** · title screen pillarboxed, "Connect Wallet" the only
green button · Echoes subtitle says "now **x1** to every node's yield" — x1 means no bonus ·
**magenta specks in the hub sky** (Unity's missing-material colour).

---

## THE SYSTEMIC CAUSES — six patterns explain ~80% of the list

**A. Two button styles, never collapsed.** Style1 square, Style2 rounded, both grey. Disagreeing
rows: founding modal, Echo card, hub action bar ("Talk"), hero select, dungeon hotbar (four squares
in three frame styles + two circles **overflowing the container**), dungeon ability cluster.
→ *Rule to write: one radius, one height, one vertical centre per row; buttons never overlap the plate.*

**B. Fraction bands + the touch-floor centre-grow.** Documented elsewhere; the mechanical cause
behind the founding modal and several overlaps.

**C. Text is allowed to leave its plate.** Founding, Echo card (both axes), Echoes subtitle, Victory
title. **One bug class, not four.** → *No text element may render outside its panel.*

**D. Typography unmanaged.** Three typefaces on hero select alone; three sizes in one button row;
auto-shrink producing "Upgr..."; mixed casing (PLACE vs Cancel); and the **"1"-as-pipe** problem
underneath all of it.

**E. Panels ignore screen edges and each other.** Menu off left and bottom; Resources clipped right;
"!" chip clipped; Builders' grid 12px left of its own header; compass over Start Now over the wave
banner. **There is no layout grid and no safe-area inset.**

**F. Meaning by colour alone — broken in five places.** Build placement ghost · health/mana/XP bars ·
Heart of Elarion bar · resource rail icons · Low Oil warning. The owner is red/green colourblind and
this is a hard project rule.

**G. Tofu: NONE.** Zero tofu boxes in 23 captures. The `--` double-hyphens are consistent with the
ASCII rule and should stay. One artefact: the `* ` before "FOUND YOUR TOWN" looks like a fallback
where an ornament was intended.

---

## THE TEMPLATE ALREADY EXISTS — promote it

**`hub-stuck-cannot-move` (the Echoes roster) is the best-composed screen in the build.** It fills
its frame, uses a real 3x2 grid of identical tiles, keeps text inside its plate, and distinguishes
locked/unlocked **by lightness rather than hue** — the one screen that would pass the colourblind
rule. Make it canonical and give the six template-less screens (Hero Loadout, Game Guide, Echo
Workforce, Raid Selection, Raid Deploy, Troop Training):

1. Ornate frame with the content plate **filling it top to bottom** — no top-third crush. Fix the
   broken corner sprite first; centre titles on the **frame**, not beside the shield boss.
2. A **tile grid** for any collection, identical sizes, flush gaps.
3. **One button style** — one radius, one height, one baseline; a single primary, de-emphasised
   secondaries, **destructive never in the middle**.
4. **Every state labelled in words as well as colour** ("(best)", "Locked", "Unlocks at wave 10").
5. A **scrim** behind every modal.
6. Text that **cannot** render outside its plate; **no auto-shrink truncation on buttons** — shorten
   the label instead ("Upgrade", not "Upgr...").
7. **Footed "1"**, and quantities on anything countable.

Those seven also fix roughly two-thirds of the P1/P2 list on the screens that already exist.

---

## CAPTURES THAT WOULD RESOLVE WHAT COULD NOT BE JUDGED

1. Build mode with an **invalid** placement — decides how urgent P0 #1 is (is invalid red?).
2. An Echo on a **non-matching** resource — does the non-"(best)" line render red?
3. Hero select with **Knight** selected — does a confirm CTA or Back button exist at all?
4. The **Bag** and **Map** screens — one tap away, entirely unreviewed.
5. Two hub frames a second apart — settles the floating leaves and a resource-value mismatch
   (`F 80 / C 451` in build mode vs `110 / 451` in the hub rail).
6. A dungeon frame with the camera panned right — how much village geometry is actually in there.

---

*Review performed against real device pixels, 2026-08-05. The four `crop_*` images are the proof
shots for findings 5, 7, 10 and 14 — keep them attached to those tickets.*
