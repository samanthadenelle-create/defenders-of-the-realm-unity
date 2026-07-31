# WO-810 — Brom's Rumor Board layout rework (crowded → scannable)

**Status:** READY TO IMPLEMENT · **WIREFRAME SIGNED by owner 2026-07-30** — CLI implements to the spec below  
**Minted:** 2026-07-30  
**Lane:** UI / Quests (single lane — owns RumorBoard panel)  
**Origin:** owner screenshot 2026-07-30 — *"too crowded and needs better organized"* → owner review: *"I love it!"*  
**Capture:** `C:\Users\Elden\OneDrive\Pictures\Screenshots\Screenshot 2026-07-30 204153.png` (session asset also under Grok attachments)  
**Roles:** Claude = wireframes + explicit layout spec (DONE, signed); CLI = implement `RumorBoardPanel` to that spec  
**Wireframe (signed):** `docs/UI/WO-810_rumor_board/wireframes.html` — before/after desktop + portrait mobile, defect→fix map, component anatomy. Artifact: https://claude.ai/code/artifact/197d6ddf-bca6-430d-afc0-9934176da858  
**Related:** WO-795 (no stack / scroll), WO-779 (spacing/legibility), `UI_REVIEW/08_Rumor Board/` if present  

---

## ✅ APPROVED LAYOUT — explicit spec (owner-signed; CLI builds to THIS)

Master–detail board. Open the signed wireframe (`docs/UI/WO-810_rumor_board/wireframes.html`) for the
rendered before/after; this section is the binding build spec.

**The one load-bearing move:** the **Accept CTA leaves the list row and lives in the detail pane.**
That alone fixes the `ACC…` truncation (CTA finally has width), un-crushes the list (rows become
title + one-line hook), and gives the empty right pane a permanent job (always shows the selection).

### Header
- Medallion (quest crest) + gilt title "Brom's Rumor Board" + the kit **corner × Close** (no giant
  full-width Close bar — reclaim that height for content).

### Filters (defect 1 — "Endgame" clip)
- One **horizontally-scrollable row of pill chips** under the header — NOT five square plates dividing
  the body width. Full labels: **All · Story · Daily · Gear · Endgame** (from `RumorBoardVM.TabLabels`).
- Selected chip = gilt fill **+ underline marker + leading ◆ glyph** (shape, not colour alone —
  colour-blind law). Each chip ≥ `MinTouchPx` (112 ref-px).

### Left pane ≈ 42% — the list (defects 4, 6)
- **In Progress**: when empty → ONE quiet dim line ("In Progress — nothing underway"), no section
  slab. When populated → a compact section above Rumors, each active quest a card.
- **Rumors & Requests**: list of **cards** in a `MakeScrollZone` scroll well (WO-795 — rows never
  stack/overlap). Card = **line 1 title (bold parchment) + state pip (New / Tracked)**, **line 2
  one-line objective hook (dim, single-line ellipsis)**. NO button in the row.
- **Selection**: whole card is the select-target; selected card = **3px gilt left-border + warmer
  fill**. First available rumor **auto-selected on open and on tab change** (so the detail is never
  blank).

### Right pane ≈ 58% — the detail (defects 2, 3)
- Obsidian-dark plate (NOT the tan slab). Always bound to the selection.
- Contents top→bottom: **tag row** (type · level · region) · **gilt title** · **full tale/body** ·
  **objectives** list · **rewards** row (XP / crystals / items) · **primary CTA** pinned at bottom.
- **Primary CTA** = full word, ≥112px: **Accept** (available) → **Track** (active) → **Turn in**
  (ready to hand in), + a secondary action (e.g. Track) beside it. ASCII-only TMP.

### Empty states (no silent blanks)
- Empty In Progress → one dim line (above). Empty filter result → one dim line inside the list.
- Nothing selectable at all → detail shows a short authored prompt (existing `ShowDetailEmpty` copy is
  fine) — but with auto-select this only appears when the list is genuinely empty.

### Portrait mobile (1080×1920)
- Same rules; panes **stack** (list ~38% top, detail below). Chips still scroll; Accept still full-word.

**MVVM:** all of the above is **View layout/chrome** in `RumorBoardPanel.cs`. The VM already exposes
title (`ItemVM.Name`), hook (`HookFor`), objective (`ObjectiveFor`), tracked flag, daily projection,
Accept/Track. Only extend `RumorBoardVM` if the detail needs a **rewards** projection not already
available from the quest catalog — and if so, keep `RumorBoardVMTests` green (update, don't weaken).

---

## Why (from the shot)

**Brom's Rumor Board** is unusable as a glance board:

| Defect (visible) | Player impact |
|------------------|---------------|
| Category tabs **All / Story / Daily / Gear / Endgame** crammed, labels clip (**"Endgame"** truncated) | Can't tell filters apart |
| Left column stacks **In Progress** + **Rumors & Requests** + Accept CTAs in a narrow dark well | Dense, hard to scan |
| Accept buttons show **"ACC…"** (truncated) | CTA unreadable; MinTouchPx at risk |
| Quest titles/bodies collide with action column | Crowding / overlap class |
| Detail pane is a **huge empty parchment** while list is crushed left | Space waste; master-detail imbalance |
| Section headers + empty "In Progress" still take vertical space | Noise when nothing active |
| Footer **"Close"** competes with content for height | Modal feels shorter than it is |

This is the same **clip / no-scroll / chrome collision** class as WO-779 / WO-795 — fix structure, not more font shrink.

---

## Goal

One **master–detail** Rumor Board that:

1. Filters are **fully legible** (wrap, icon+label, or scrollable chip row — never mid-word clip).  
2. Quest rows are **one clear card**: title · one-line objective · primary CTA (**Accept** / **Track** / **Turn in**) with full word and ≥ `MinTouchPx` (112).  
3. **In Progress** collapses when empty (or one-line empty state, not a half panel).  
4. **Detail** (right) fills with selected rumor: full title, body, objectives, rewards, secondary actions — not a blank rectangle with orphan teaser text.  
5. Works portrait mobile **and** desktop window (screenshot is desktop; layout must not depend on ultra-wide empty detail).  
6. ASCII-only TMP; colorblind-safe selected filter (marker, not color alone).  

**Do not** change quest grant logic, daily generation, or catalog data except labels if copy is truncated by design.

---

## Code baseline

| Piece | Path |
|-------|------|
| View | `Assets/_Modules/Village/Hero/RumorBoardPanel.cs` |
| VM | `Assets/_Modules/Village/Hero/RumorBoardVM.cs` |
| Bootstrap / PanelRouter | `RumorBoardPanelBootstrap.cs`, `PanelId.RumorBoard` |
| Openers | `QuestTrackerHud`, HudKit quest/context → `PanelRouter.Open(RumorBoard)` |
| Tests | `Assets/Tests/EditMode/RumorBoardVMTests.cs` (preserve behavior; update if projection fields added for layout) |

MVVM law: layout/chrome in the **View**; list data stays in **VM**. Prefer restyle View + kit zones; only extend VM if detail needs richer projection fields already available from backend.

---

## Scope

### Claude (read-only — do first)
1. Wireframe **before → after** on 1080×1920 **and** ~16:9 desktop (match owner shot aspect).  
2. Propose one primary layout (CLI lean below).  
3. Tab treatment: 5 filters without truncation (icons + short labels, or 2-row wrap, or horizontal scroll chips).  
4. Row anatomy + empty states.  
5. Image pairs for owner sign-off.  

**CLI lean (challenge if wrong):** classic master–detail  
- **Left ~40%:** scrollable list (In Progress section optional at top; Rumors list below)  
- **Right ~60%:** detail parchment always bound to selection (default select first available)  
- **Top:** title + filter chips under medallion (not five square buttons eating list height)  
- **Bottom:** single Close in chrome (existing kit close preferred over second full-width bar if Frame already has Close)

### CLI (after sign-off)
1. Rebuild panel layout via `ElarionUiKit` / Frame (code-built only — no UXML).  
2. Fix truncation: full **Accept** (or **Accept quest**), full filter names.  
3. List in `MakeScrollZone` / scroll well when rows exceed body (WO-795).  
4. Selecting a row fills detail; empty selection shows short help only inside detail, not mid-list.  
5. Touch targets ≥ 112 ref-px on Accept / filters / Close.  
6. Screenshot-verify headed or AutoPilot panel capture if available.  
7. Keep `RumorBoardVMTests` green; add layout regression only if useful (optional string length / no mid-clip asserts are weak — prefer screenshot).  

---

## Acceptance

- [ ] Owner signs layout image pair  
- [ ] No tab label clips at 1080×1920 or desktop window used in the shot  
- [ ] Accept / primary CTA fully readable (not `ACC…`)  
- [ ] List scrolls; no stacked overlapping rows  
- [ ] Detail shows full tale for selected rumor (not permanent blank)  
- [ ] Empty In Progress does not dominate the board  
- [ ] Panel still opens from quest HUD icon / HudKit  
- [ ] MVVM: View stays dumb; backend behavior unchanged  

---

## Do NOT

- Redesign quest economy / daily spawn rules  
- Add new quest types in this WO  
- Hand-edit `.unity` scenes  
- UXML / UIDocument  
- Shrink fonts below `FontFloorMobile` to "fit" crowding (reflow instead)  
- Touch Barracks / raid WOs  

---

## Owner feel bar

> I open Brom’s board, I can read every filter, I see my open jobs, I pick a rumor, I read the whole story on the right, I hit **Accept** without guessing what the button says.

---

## Files

- `RumorBoardPanel.cs` (primary)  
- `RumorBoardVM.cs` (only if detail fields missing)  
- `RumorBoardPanelBootstrap.cs` (registration only if needed)  
- Optional: `docs/UI/WO-810_rumor_board/` for Claude mockups  
