> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit 287ac354 shipped it, but the commit itself concedes overflow remains and mitigates it with a MORE BELOW cue - against this WO's acceptance criterion "Nothing is clipped".
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 896 — Skill tree: simplify to a connected progression line (kill the dense grid)

**Status:** IMPLEMENTED — awaiting owner felt-close (2026-08-15 sparse Obsidian graph)

> ## OWNER CONFIRMATION 2026-08-15 (BINDING visual north star)
> Owner, on the **Obsidian kit demo Talent Tree** (screenshot
> `C:\Users\Elden\OneDrive\Pictures\Screenshots\Screenshot 2026-08-15 175853.png`):
>
> *"Thats what I really always wanted"* / *"isnt that much better?"*
>
> **Yes.** That demo is the **authoritative look**, not the dense grid and not a still-busy
> “horizontal tracks + MORE BELOW” mitigation of `287ac354`.
>
> ### Demo properties that MUST land in the live Hero Skills panel
> | Property | Demo |
> |----------|------|
> | Density | **Few large nodes**, lots of dark empty space |
> | Graph | **Sparse tree** with gold connectors between prerequisites (not a packed spreadsheet) |
> | Focus | **One big outlined plate** = current / selected |
> | State | Ranks on plate (`3/3`, `5/5`); locked = dim; available = clear |
> | Chrome | Dark frame, gold trim, crest **TALENT TREE** title, single **Confirm** |
> | Feel | Calm, scannable, progression readable in one glance |
>
> ### What to kill
> - Dense multi-row icon grids  
> - Clipped top/side rows and “MORE BELOW” as a substitute for fit  
> - Competing labels / overlapping chrome  
> - Any “busy” layout that fails owner focus even if “technically tracks”
>
> Live panel: `HeroSkillTreePanelMvvm.cs` (+ VM). Kit: `ElarionUiKit` / `RpgUiCatalog.FrameTalent` /
> Obsidian talent demo surface (`ObsidianComponentGalleryBuilder` · TalentTree).

**Silo:** UI / talents · **For:** CLAUDE CLI · **Date:** 2026-08-05  
**PO:** Samantha (owner) · **Author:** UI seat  
**Owner ruling (original):** *"this skill tree is hard to read — simplify the tree, just have the skills connected by a line showing progression."*  
**Owner ruling (2026-08-15):** match the **Obsidian demo** above — that is the product.

## 0. Problem (grounded)
`Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs` renders talents as a **dense multi-row icon GRID**.
In the screenshot the top row is **clipped off**, rows are cramped, and there is no visible relationship between
nodes — the player can't read the progression. Owner: hard to read.

## 1. Redesign — a connected progression LINE per track
Replace the grid with **horizontal tracks, each a single line of nodes connected by a progression line** (left→right
= earlier→later). One track per talent section (e.g. the class path `Knight`, and `Universal · any class`).

**Each node:** a round talent plate (icon) + a short name label under it, in one of three states — read by SHAPE/fill, not colour:
| State | Node | Connecting line (to its left) |
|-------|------|-------------------------------|
| **Owned** | filled gold plate | solid gold |
| **Next / available** | outlined gold plate (slightly larger — the focus) | gold up to it |
| **Locked** | dim plate + lock glyph | dim/hairline |

- Nodes sit on the line, evenly spaced, vertically centered; the line runs THROUGH the node centers so progression reads at a glance.
- **Tap a node → the right detail panel** fills with that talent's name/tier/description ("Select a talent → tap any node to read what it does before you confirm" stays as the empty-state hint).
- Tracks stack vertically with a labeled header each (`Knight path`, `Universal · any class`); the panel scrolls only if there are more tracks than fit — no clipped top row.

**Reference the rendered mockup** (this session) as the visual target: two clean tracks of connected nodes, gold=owned, outline=next, dim+lock=locked.

## 2. Keep (unchanged behavior)
- Right **detail panel** (Select a talent + selected node info).
- The **loadout bar** — 4 slots (`1 Emberbrand Throw`, `2 empty`, `3 empty`, `4 Thunderbolt`).
- **Cancel / Respec <cost>c / Confirm** footer + Close. The WIS/points header chip.
- Confirm/respec economy + tap-to-read-before-confirm flow.

## 3. Files
- `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs` — replace the grid builder with the track/line builder; add the connecting-line draw (a thin `Image` between adjacent node centers, gold/dim by progression).
- Its VM — expose, per track, an ordered node list with `{icon, name, state: owned|next|locked}` (derive from the existing talent/tier data — do NOT invent a new model).
- Use `ElarionUiKit` node plates + `docs/UI_BLINK_TEMPLATE_CANON.md` chrome.

## 4. Acceptance criteria
**Layout / readability:**
- [ ] The tree is **tracks of nodes connected by a visible progression line** — NOT a dense grid.
- [ ] **Nothing is clipped** — no cut-off top row; every node + its name is fully visible (scroll only if tracks overflow).
- [ ] Progression reads at a glance: owned (gold) → next (outlined) → locked (dim + lock), by shape/fill not colour.
- [ ] The line runs through node centers and visibly shows order/unlock direction.
- [ ] Tapping a node populates the detail panel; the empty-state hint shows until a node is tapped.
**Kept intact:**
- [ ] Loadout bar (4 slots), Respec/Confirm/Cancel/Close, WIS header — all present and uncramped.
**Engineering:** `COMPILE_GATE_OK` + `REGRESSION_OK`; MVVM preserved; built with the Obsidian kit (no bespoke chrome).
- [ ] Headless UI capture of the skill screen — **open the PNG**, confirm the connected-line layout with no clipping, attach to RESULT.
**Owner felt-close:** opens Grom's skills, immediately reads the progression as a line of connected nodes, taps one to see what it does.

## 5. RESULT
`WorkOrders/WORK_ORDER_896_skill_tree_progression_line_redesign.RESULT.md` — sparse graph shipped 2026-08-15;
`COMPILE_GATE_OK` + `SKILLS_PANEL_LAYOUT_OK`. Owner felt-close still open (visual product).
