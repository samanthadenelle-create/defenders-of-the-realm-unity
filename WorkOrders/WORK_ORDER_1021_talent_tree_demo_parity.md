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

⚠ **Do NOT delete `GraphUnitWpx` / `GraphUnitHpx`.** `SkillsPanelLayoutRegression [grid]` pins them
against the authored JSON. Leave them as the documented fallback + the regression's anchor; re-pointing
that oracle is its own ticket.

**Architecture note (canon):** per `docs/UI_BLINK_TEMPLATE_CANON.md` §3, frame geometry is tuned in
`ZonesFor` and nowhere else. Per-screen absolute pixel lattices are the thing that rule exists to stop.

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
