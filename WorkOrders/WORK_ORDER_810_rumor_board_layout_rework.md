# WO-810 — Brom's Rumor Board layout rework (crowded → scannable)

**Status:** READY TO IMPLEMENT  
**Minted:** 2026-07-30  
**Lane:** UI / Quests (single lane — owns RumorBoard panel)  
**Origin:** owner screenshot 2026-07-30 — *"too crowded and needs better organized"*  
**Capture:** `C:\Users\Elden\OneDrive\Pictures\Screenshots\Screenshot 2026-07-30 204153.png` (session asset also under Grok attachments)  
**Roles:** Claude = READ-ONLY wireframes + image pairs first; CLI = implement after owner sign-off of layout  
**Related:** WO-795 (no stack / scroll), WO-779 (spacing/legibility), `UI_REVIEW/08_Rumor Board/` if present  

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
