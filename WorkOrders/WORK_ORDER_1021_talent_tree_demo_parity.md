# WORK ORDER 1021 — Talent tree: close the last four gaps to the Obsidian demo

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1021 → 1022 in the same edit
**Lane:** UI presentation only (`HeroSkillTreePanelMvvm`). File-disjoint from every gameplay lane.
**Provenance:** owner F8 **seq=2333**, `Main_Castle_Overworld`, verbatim **"look at the overcrowding"**,
plus the owner's side-by-side demo/live comparison table (2026-08-15) and her follow-up screenshot
after `61a2a701c` landed ("this is the starting point").
**North star:** `Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/TalentTree.prefab` +
`Panels_Obsidian/Talent_Tree_Panel.png` (the owner's second reference image).

---

## 0. What already shipped — do NOT redo it

`61a2a701c` (**feat(ui): RPG-grade talent tree — gold lines, large art, focus ring**) already closed the
two biggest gaps. Verified at source:

- `NodeSizePx` 112 → **136**, `NodeFocusPx` 148 → **168**
- Connectors 3/5/8 → **6/8/10, solid, always visible**
- `FilterCalmFrontier` loosened to **roots + one child step**, which is what made connectors renderable
  at all (see §1)

**This WO is the remainder only.** Four items. Nothing above is in scope.

---

## 1. Why the graph was empty before — recorded so it is not re-broken

Measured from `Assets/Resources/Data/Canonical/hero-talents.json` on 2026-08-15:

| fact | value |
|---|---|
| knight nodes | **32** |
| knight prerequisite edges | **24** |
| knight nodes with authored x/y | **20** of 32 |
| nodes with NO prerequisite (roots) | **8** |
| ranger / mage authored x/y | **0** of 20 each |

The pre-`61a2a701c` view drew 8 nodes and **zero** edges. Cause: `FilterCalmFrontier` kept only roots on
an unengaged tree, and the connector loop skips an edge when **either** endpoint is invisible. Every one
of the 24 edges runs child→parent, so with all children filtered out, no edge could ever draw. The
"3 above / 5 below" shape in the owner's first screenshot was exactly the 8 roots, split across two
layout paths: the 5 authored tier-1s at `y=0.66`, and `s1n1/s1n2/b1n1` (no authored x/y) falling to the
auto-layout fallback at `y=0.14`.

**Load-bearing invariant for any future frontier filter: never hide a node that is an endpoint of a
visible edge.** Hiding nodes silently deletes graph structure.

---

## 2. The four items

### 2.1 Fit the lattice to the MEASURED body rect (highest value)

**Defect:** `GraphUnitWpx = 1180f` / `GraphUnitHpx = 780f`
(`HeroSkillTreePanelMvvm.cs:137-139`) are absolute reference pixels. `RebuildTracks` maps authored
0..1 through them (`:418-420`). The `FrameTalent` body well is **~1695 x 493 ref px**
(`ZonesFor(FrameTalent)` body = `0.035, 0.115 → 0.965, 0.855`, `ElarionUiKit.cs:384-392`).

Consequences visible in the owner's 2026-08-15 screenshot: a dead black third across the bottom, the
tree hugging the upper-left, and an orphan plate stranded at bottom-centre.

**Fix:** derive the lattice unit from the measured `_graphContent.parent` rect each rebuild, clamped to
a floor so a large tree still scrolls rather than crushing:

```
unitW = Mathf.Max(wellW - GraphPadPx * 2f - NodeFocusPx, MinLatticeWpx)
unitH = Mathf.Max(wellH - GraphPadPx * 2f - NodeFocusPx - RankBandPx, MinLatticeHpx)
```

Guard `wellW/wellH <= 1f` (rect not yet laid out on the first frame) by deferring one frame or falling
back to the current constants — **must not divide by zero or lay out against a zero rect**.

#### 2.1b SPACING LOGIC — owner 2026-08-15: *"needs better spacing logic"* (screenshot 214846)

Scaling the lattice is necessary but **NOT sufficient**. The post-`61a2a701c` capture shows three
distinct spacing defects that a uniform scale will not fix:

1. **Plates OVERLAP.** In the right-hand cluster, adjacent plates touch, and the corner cost pips
   (`1`, `0`) render **on top of the neighbouring plate**. A pip belonging to node A sitting on node B
   is a misread waiting to happen.
2. **A stranded orphan.** One plate sits alone at bottom-centre, below and outside the tree body,
   connected to nothing the eye can follow.
3. **Dead bottom third.** All content occupies the upper ~60% of the well; the lower ~40% is empty
   black while the upper half is crowded.

**Required, and each is testable:**

- **Minimum centre-to-centre pitch** between ANY two plates: `NodeFocusPx * 1.35` (≈227 ref px at the
  current 168). The multiplier exists so the FOCUS size plus its corner pip still clears a neighbour —
  computing clearance from `NodeSizePx` (136) is what allows a focused plate to grow into its
  neighbour. **No two plates may ever visually touch, at any state, at any aspect ratio.**
- **Collision resolution for auto-placed nodes.** `ResolveGraphNorms` currently offsets unauthored
  children by `fan = ((autoIdx++ % 5) - 2) * 0.06f` and `pp.y + 0.16f`
  (`HeroSkillTreePanelMvvm.cs:589-592` — cite corrected 2026-08-15, verified at source: :581-584 are
  only the prereq guard + loop header; the formulas are verbatim at :589-592) — an arbitrary spread with
  **no collision test of any kind** (placement only `Clamp01`s x and caps y at 1.15),
  which is why plates land on each other. Add a separation pass after placement: while any pair is
  closer than the minimum pitch, push both apart along the axis of least overlap; cap the iterations
  (~8) so it always terminates. Authored positions may be nudged but must keep their **relative order**
  — never let a separation pass reshuffle the authored 5×4 knight lattice.
- **Distribute rows over the FULL well height.** When only two tier rows are visible (the current
  roots + one-child frontier), spread those rows across the available height rather than packing them
  at the authored `y` and leaving the bottom empty. Normalise the *visible* y-range to the well, don't
  map the authored 0..1 range through a fixed unit.
- **No stranded nodes.** Any node whose nearest neighbour is further than ~2× the pitch should be pulled
  toward its prerequisite's column. A node with a visible parent must read as attached to it.

**Do the layout in ONE place.** Position solving belongs in `ResolveGraphNorms` (+ the separation pass);
`RebuildTracks` should consume final positions and do no geometry of its own. Splitting placement across
both is what let the authored and fallback paths drift into two different visual languages — the
"3 above / 5 below" split in the first capture.

#### 2.1d ★ FOCUS INFLATION — the "one gold focus plate" premise does not survive multiple tracks

**Owner 2026-08-16, screenshot at WIS 252: *"Still Messy."*** With Wisdom spent-able, the board fills
with **~10 oversized gold plates overlapping each other** and occluding neighbours' art and pips.

**This is a SECOND defect, not just the §2.1b spacing gap.** Traced at source:

`HeroSkillTreeVM.ResolveStates` (`:839-856`) — `nextTaken` is a **local reset on every call**, and the
method is invoked **per track**:

```csharp
bool nextTaken = false;                                   // reset PER TRACK
...
else if (ordered && !nextTaken) { state = SkillNodeState.Next; nextTaken = true; }
```

Its own comment is precise and correct: *"On an ORDERED track **exactly ONE** node may be Next."*
**Per track.** So the number of `Next` nodes on the board equals **the number of ordered tracks.**

The view then does (`HeroSkillTreePanelMvvm.cs:451-452`):

```csharp
bool focus = seat.State == SkillNodeState.Next || (selectedId == seat.Node.Id);
```

…and every `focus` plate renders at `NodeFocusPx` (**168** vs 136) with a thick gold outer ring. **So
the board grows one oversized gold plate per track**, and the file's own header premise —

> *"**One** thick gold FOCUS plate for the selected / next node"*

— is violated by construction the moment there is more than one track. At WIS 252 with everything
affordable, that is the whole board shouting at once, which is why it reads messier *with* currency
than without.

**⚠ The VM is NOT wrong. Do not "fix" `ResolveStates`.** Per-track `Next` is the correct model — each
track legitimately has a next step, and WO-910's Inert rule depends on that loop. **The view is
over-consuming a per-track signal as a board-level one.**

**Required — separate the two ideas the view currently conflates:**

| concept | meaning | treatment |
|---|---|---|
| **SELECTED** | the one node the player tapped — a **board-level singleton** | the big gold focus plate. **At most ONE, ever** |
| **NEXT** (per track) | this track's next step | a **quiet** marker — a rim, a pip, a subtle tint. **Same size as every other plate** |

- ⛔ **`NodeFocusPx` may apply to AT MOST ONE plate on screen.** Assert it.
- The per-track `Next` cue must be **shape/position-carried, not size-carried** — size is the scarce
  channel and selection owns it. (It must also survive greyscale: colourblind law.)
- ⚠ **This interacts with §2.1b's pitch rule.** Once at most one plate is focus-sized, the
  `NodeFocusPx * 1.35` clearance is needed for **that one plate's neighbourhood**, not globally — but
  the rule stays as written, because the focus can move to any node.

**Sequencing note:** §2.1b (pitch + separation) and this are independent and both required. Fixing
spacing alone still leaves ten oversized plates crowding a board sized for them; fixing focus alone
still leaves plates overlapping. ⚠ The owner has now reported "messy" **twice**; landing only one half
will produce a third report.

⚠ **Do NOT delete `GraphUnitWpx` / `GraphUnitHpx`.** `SkillsPanelLayoutRegression [grid]` pins them
against the authored JSON — **by reflection** (`ConstFloat(view, "GraphUnitWpx"...)` at :192-193), so a
RENAME breaks the suite silently too. Leave them as the documented fallback + the regression's anchor.

⚠⚠ **MEASURED 2026-08-16 — THE `[grid]` AUTHORED-PITCH CHECK IS NOW ASSERTING SOMETHING
GEOMETRICALLY IMPOSSIBLE, AND THAT IS THE PROOF §2.1c WAS RIGHT.**

After the owner's shared-pool ruling (3 bases branching 4 -> 4, WO-1105 commit) the tree has **7
rows**: 4 class tiers + 3 shared. Run the arithmetic against the constants the oracle itself uses:

| quantity | value |
|---|---|
| authored lattice height (`GraphUnitHpx`) | **780 px** |
| 7 rows x `NodeSizePx` 136 | **952 px needed** |
| 7 rows x `NodeFocusPx` 168 | **1176 px needed** |
| space below class tier-1 (y 0.66 -> 1.0) | 265 px = **1.95 rows fit, not 3** |

So no authoring of x/y can satisfy `[grid]` for the tree the owner asked for — the check demands
136 px of clearance inside a 780 px space that must hold 952 px of plates. Its advice line
(*"re-author x/y in hero-talents.json, never the plate consts"*) is therefore unfollowable.

**Root cause is the one §2.1c already named:** the check converts AUTHORED 0..1 coordinates to
pixels through the FALLBACK constants (1180x780) — but the runtime no longer lays out that way. It
derives the lattice from the measured well (~1695x493) and then runs the separation pass. Authored
y is now an ORDERING HINT consumed by a solver, not final geometry, so measuring it in plate-pixels
measures a lattice that does not ship.

**Do NOT weaken or delete the check to make a layout pass** — a green `[grid]` over a broken screen
is the exact failure this WO exists to end. The fix stays as ruled: move the pitch assertion onto
the RESOLVED positions (post-separation-pass), and leave the authored data checked for what it
actually promises — presence, ordering, uniqueness, and the 0..1 contract (which the shared pool now
satisfies; `TALENT_STRATEGY_OK`, all seven out-of-range failures cleared).

⚠ **ORACLE GAP — must close IN THE SAME CHANGE as the pitch fix (added 2026-08-15, verified at
source):** the existing `[grid]` oracle measures authored pitch against `L.NodeSize` (136) —
`SkillsPanelLayoutRegression.cs:322-332` — i.e. exactly the NodeSizePx-based clearance this WO
condemns. If only the view changes, `[grid]` keeps GREEN-lighting the defective layout. Move the
pitch basis to the focus-based clearance (`NodeFocusPx * 1.35`) or add a sub-check asserting it;
a view fix without the oracle move ships the same false green this WO exists to kill.

**Architecture note (canon):** per `docs/UI_BLINK_TEMPLATE_CANON.md` §3, frame geometry is tuned in
`ZonesFor` and nowhere else. Per-screen absolute pixel lattices are the thing that rule exists to stop.

#### 2.1c DEVICE MEASUREMENTS 2026-08-16 (owner Seeker felt-test) — AWAITING OWNER RULING

> **Status of this section: SPEC ADDENDUM ONLY. Do NOT implement until the owner rules on it.**
> It records what the 2026-08-16 device captures prove that 2.1b does not yet cover. 2.1b's pitch
> law + separation pass + [grid] oracle move stand unchanged; the items below EXTEND them.

Evidence: two Seeker captures, original 2670x1200, measured on a 2000x899 display scale
(multiply displayed y by ~1.335 for original px):

- `s3.png` — the clean tree, no dialog.
- `s2.png` — the same screen with the Shield Slam spend dialog open.

**1. Measured row overlaps (s3.png, displayed px).** Three tier rows land at:

| row | y-band (displayed) | overlap with next |
|---|---|---|
| Row A (top, 3 large gold plates) | y ~190-380 | A/B overlap ~35 px |
| Row B (middle, green-border plates) | y ~345-510 | B/C overlap ~15 px |
| Row C (bottom, 5 gold plates) | y ~495-610 | — |

Row B's LOCK icons (bottom-right corner of each locked plate) render ON TOP of Row C's plate tops.
Row A's three plates touch with ZERO horizontal gap — they read as one continuous yellow block,
x ~865-1250. Corner cost pips land on NEIGHBOURING plates (the exact misread 2.1b item 1 names,
now measured on device). These numbers are the acceptance bar: after the fix, no two row y-bands
may intersect and no plate may touch another at rest.

**2. LEFT-half dead space — normalise X, not just Y.** In s3.png the band x ~200-310 inside the
panel body is empty while the content crowds the centre-right (Row A starts at x ~865 in a well
that begins ~x 200). 2.1b's "distribute rows over the FULL well height" normalises the VISIBLE
Y range only. Requirement: apply the SAME normalisation to the visible X range (map the visible
min..max x to the well width), or add an explicit centring pass that balances left/right slack.
Either way, the well must not show a dead column on one side while plates collide on the other.

**3. TOP-EDGE CLIP (s2.png).** The top tier row is clipped by the panel's TOP edge — the plates
under the header bar are cut mid-plate (their `0/1` pips sit at the very edge of the body well).
The y-range normalisation of 2.1b MUST inset its target range by **plate half-height PLUS the
focus growth** (`NodeFocusPx/2` at the top and bottom of the well, i.e. the focused size, not
`NodeSizePx/2`) so the first and last rows sit fully inside the mask at every state. Mapping row
centres to the raw well edge is what produces this clip.

**4. SPEND POPUP CLIPS ITS OWN BODY TEXT (separate small defect — do not fold into the graph
work).** In s2.png the confirm dialog's question line — "Spend 2 Wisdom for Shield Slam?" — is
sliced horizontally by the Cancel/CONFIRM button row: only the top ~60% of the glyphs render.
This is a band-budget bug in the SPEND POPUP (the e0513c755 Obsidian FrameCore dialog), not in
`ResolveGraphNorms` / the graph. It gets its own bullet here so it is not lost inside the layout
fix: the dialog body band must reserve a whole line box for the question line (or the button row
must anchor below the measured text height). Track/fix it as its own item.

**5. ORACLE HOLE — why the suite is green over this broken screen (verified at source
2026-08-16).** `Assets/Editor/Regression/SkillsPanelLayoutRegression.cs` header (line 45)
promises `[grid]` guarantees "the graph content can never exceed / be sliced by its container" —
but every `[grid]` check reads AUTHORED data only:

- `:347` `ReadText(TalentsJson)` — the lattice check parses `hero-talents.json` x/y pairs;
- `:320-324` / `:329-333` — the pitch checks measure the tightest AUTHORED column/row gap
  against `L.NodeSize`;
- `:366` — nodes with `x < 0` ("-1 = unset/auto") are `continue`d, i.e. every AUTO-PLACED node
  is EXCLUDED from the oracle entirely;
- the string `ResolveGraphNorms` does not appear anywhere in the file — the RUNTIME output
  (where the fan-out formula at `HeroSkillTreePanelMvvm.cs:589-592` actually lands nodes) is
  never evaluated.

So the oracle certifies the authored lattice while the screen is drawn from the resolved one.
That is exactly why the suite is green over the overlapping rows in s3.png. Requirement: the fix
MUST extend `[grid]` (or add a sibling case) to evaluate the RESOLVED positions — run
`ResolveGraphNorms` + the 2.1b separation pass headlessly and assert the minimum pitch and the
well-inset containment on ITS output — or the separation pass regresses silently the first time
someone touches the fan-out math. This is the same-change coupling rule of 2.1b's "ORACLE GAP"
paragraph, widened from "move the pitch basis" to "test the resolved output".

**PROOF — the acceptance standard for this defect class (owner directive 2026-08-16, verified at
source).** Batchmode UI-capture is **INVALID as proof** here. `Assets/Editor/UICaptureLaunch.cs`
banner (lines 70-85) records why, from captured data: under `-batchmode` no editor window layout
is built, so there is no GameView for `Screen.*` to mirror — it stays on the 640x480 offscreen
default even when the game-view size reflection SUCCEEDS (`Builds/ui-capture-rail.log:475`
"batchmode=True ... screen=640x480"; that run logged `UI_CAPTURE_FIDELITY_DEGRADED 38/38`). The
harness therefore drives ElarionUiKit's INJECTABLE surface — which can render "correct" while the
device renders broken, because this defect's root is runtime `ResolveGraphNorms` against the REAL
well rect. Valid proof, in priority order:

1. **DEVICE screenshot from the Seeker APK** — true 2670x1200, the instrument that caught the
   defect. The fix is proven when the same shot that showed the defect (`s3.png`) clears it.
2. **Headed Unity EDITOR play-mode capture at 2670x1200** — secondary corroboration only, never
   a substitute for (1).

It must be a **TRUE BEFORE/AFTER**: same class tree (s3 = the knight tree at WISD 175), same
device, same resolution. The AFTER image must visibly satisfy ALL of: no plate overlap; pitch
>= `NodeFocusPx * 1.35`; no cost pip over a neighbouring plate; no panel-edge clip; rows spread
across the FULL well height; content CENTRED on X. And the image must be PAIRED WITH NUMBERS —
the measured RESOLVED plate rects (logged or dumped from the running panel), because the `[grid]`
oracle's authored-only read (item 5 above) proves nothing about this screen.

### 2.2 Stop the opaque slab masking the frame

**Defect:** `BuildScrollGraph` paints the viewport `new Color(0.018f, 0.016f, 0.022f, 1f)` at **full
alpha** (`HeroSkillTreePanelMvvm.cs:1231`), covering the `frame_talent` art underneath.

This is a **named failure mode** — `docs/UI/Grok-02-Obsidian-UI-guidance.md` §6: *"Panel looks unstyled /
flat black → opaque solid fill masking the frame → alpha 0 the decorative fill."*

Already mirrored into committed Resources and **currently unused by this panel**:

| asset | path |
|---|---|
| `panel_talent` | `Assets/Resources/RpgUi/panel/panel_talent.png` |
| `deco_talent_1` | `Assets/Resources/RpgUi/decoration/deco_talent_1.png` |
| `deco_talent_2` | `Assets/Resources/RpgUi/decoration/deco_talent_2.png` |

**Fix:** drop the viewport fill to a low-alpha veil (start ~0.35 and tune against the demo PNG) so the
frame reads through, and dress the graph well with `panel_talent`. Keep the viewport **raycastable** —
drag-scroll depends on it. Sprite-first with null fallback (`RpgUiCatalog.Get` returns null when art is
absent; the panel must never blank).

`deco_talent_1/2` are optional polish — use them only if they read well against the graph; do not let
decoration eat node space.

### 2.3 Wisdom chip — stop the ellipsis, move it out of the board

**Defect:** the chip renders **"WIS… 0"** (visible in both owner screenshots). It is pinned over the
graph at `0.72–0.98` (`HeroSkillTreePanelMvvm.cs:1059-1062`).

Two rules broken at once:
- Grok-02 §4 wallet law: *"`CurrencyChip` / `BuildWalletRow` — Gold primary; CompactNumber; **no
  ellipsis**."*
- The demo has nothing floating over the board.

**Fix:** move the chip into the frame's **header band** beside the "TALENT TREE" title, and give it
enough width that "WISDOM" sets in full at the kit font floor. `ZonesFor(FrameTalent)` header is
`0.12, 0.900 → 0.86, 0.975`; the title currently claims `headerX0: 0.04, headerX1: 0.74`, so there is
room on the right. Do not shrink the font below `ElarionUiKit.FontFloor` to make it fit — widen the box.

### 2.4 Locked skill art reads too dark

**Defect:** in the owner's screenshot the locked plates' art is muddy at a glance. The demo shows every
icon at full strength; lock state there is carried by the plate treatment, not by dimming the art.

**Fix:** raise the locked icon tint substantially (from the current dim value toward ~0.75–0.85 alpha)
and let the **corner padlock + the `0/1` rank pip** carry the locked meaning.

⚠ **COLOURBLIND LAW (owner is red/green colourblind) — non-negotiable.** Every state must stay separable
with colour stripped. Locked must remain distinguishable by **padlock glyph + rank pip + plate
treatment**, never by hue or by brightness alone. Verify with a greyscale pass on the capture.

---

## 3. One anomaly to identify — do NOT guess at it

The owner's post-`61a2a701c` screenshot shows two plates carrying a **bare "1"** and a **bare "0"** where
every other plate shows `0/1`.

Those are `BuildQuietCornerPip` (`HeroSkillTreePanelMvvm.cs:810`), which only renders for
Planned / Next / Available / Inert. But **there is no zero-cost node in the data** — measured
2026-08-15: `tierCosts` = 1/2/3/5, `sharedNodeCost` = 2, and every `shared.n*` row carries cost 1 or 2.
A "0" pip should not be renderable.

**Per CLAUDE.md §12: instrument first.** Add a `FlowTrace.Step` in `BuildQuietCornerPip` logging
`node.Id`, `node.State`, `node.WisdomCost`, run the panel, and read which node emits it. Fix what the
trace names. **Do not infer a cause from reading the code.**

---

## 4. Files in scope

| file | change |
|---|---|
| `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs` | all four items + the §3 trace |

**Nothing else.** Specifically **do NOT touch**:

- `HeroSkillTreeVM.cs` — the VM is correct; this is presentation only (dumb-View rule,
  `docs/UI_MVVM_BINDING_MAP.md`)
- `hero-talents.json` — the data is sound (32 nodes, 24 edges, 20 authored positions)
- `WisdomCurrencyService.cs`, `HeroTalentCatalog.cs` — no cost/unlock semantics change
- `TalentTreePanel.cs` — deprecated UIToolkit path, leave dead
- The `61a2a701c` sizing/connector/frontier work — already correct

---

## 5. Explicitly OUT of scope (owner decision required first)

**Multi-rank talents (the demo's `3/3`, `5/5`).** Our nodes are single-purchase — the data carries
`cost`, no rank field — so `0/1 → 1/1` is the correct grammar today. The demo's rank grammar is a
**data + VM change** and is listed as an explicit V1 non-goal in
`docs/TALENT_TREE_NODEGRAPH_BUILD_SPEC_2026-06-28.md` §8 (*"No multi-rank nodes (single-purchase)"*).
**Do not implement rank pips cosmetically to imitate the demo** — that would show progress the save
cannot back.

Also out of scope: the retired footer/CONFIRM strip (owner retired it 2026-08-15) and the bottom Close.

---

## 6. Acceptance criteria

- [ ] Graph fills the body well at 2340x1080 and at 1920x1080 — **no dead band** greater than one node
      pitch on any edge, no orphan plate stranded outside the tree body
- [ ] `frame_talent` art is **visible** behind/around the graph; the panel no longer reads as a flat
      black rectangle
- [ ] Wisdom chip reads **"WISDOM 0"** in full — **no ellipsis at any tested aspect ratio** — and sits in
      the header band, not over the graph
- [ ] Locked skill art is legible at arm's length; locked state still separable **in greyscale** via
      padlock + rank pip
- [ ] All **24 knight prerequisite edges** draw when their endpoints are visible; no edge is dropped
      because a filter hid an endpoint
- [ ] The bare `1` / `0` pip is identified **from a captured trace line** and fixed, or proven correct
      with the trace quoted in the RESULT
- [ ] Panel still renders with `ff.blinkchrome` **ON and OFF** (existing two-state contract)
- [ ] Sprite-first null-safety intact — panel does not blank if `panel_talent` fails to resolve

## 7. Verify (CLAUDE.md §1 + §12 + memory `headless-screenshot-verify-ui-before-build`)

1. Brace-balance check on `HeroSkillTreePanelMvvm.cs`
2. `COMPILE_GATE_OK` (`DeNelle.Editor.CompileGate.Run`) — **Editor must be closed**, project lock
3. `REGRESSION_OK <n>/<n> suites` — `SkillsPanelLayoutRegression [grid]` must still pass (see the §2.1
   warning about the retired constants)
4. `UI_CAPTURE_OK` — **open the PNGs**. Compile-green never proves a panel looks right.
5. **Compare the capture against `Panels_Obsidian/Talent_Tree_Panel.png` / the demo prefab**
   (`docs/UI_BLINK_TEMPLATE_CANON.md` §7 — owner method, BINDING)
6. Greyscale pass on the capture for the colourblind law

**PO (owner) felt-verifies and closes** (§13) — CLI does not close this.
