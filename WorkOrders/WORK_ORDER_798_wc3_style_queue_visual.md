# WO-798 — Warcraft 3–style work queue VISUAL (design / mockups / read-only for Claude)

**Status:** READY FOR UI SEAT (read-only) — **Claude writes the design pack; CLI implements later.**  
**Minted:** 2026-07-30 (owner-requested via CLI, after START_HERE + SAMANTHA + architecture boot)  
**Lane:** UI / product design (presentation only). **No `.cs` from Claude.**  
**Next free after this:** bump `CLI_LANES_WO_NUMBERS.md` to **799**.  
**Implementer (later):** CLI sole committer — only after owner signs the image-pair mockups.

---

## 0. Roles (binding — do not invert)

| Role | Does | Does NOT |
|------|------|----------|
| **Claude (UI seat)** | Visual design, wireframes, annotated mockups, interaction copy, acceptance checklist for feel | Write/edit any `.cs`, run Unity, change engine/queue data |
| **CLI** | Later: implement the signed design on the existing queue surface | Invent a new visual language without owner sign-off |
| **Owner (PO)** | Creative call, image-pair sign-off, felt-verify on device | — |

This WO is a **presentation-layer design order**. Architecture law: *presentation never touches the objects* (`docs/ARCHITECTURE_PRINCIPLES.md` §2). The multi-channel job **engine is already correct** — only how it *looks and sits on the HUD* changes.

---

## 1. Why (product)

### What we already have (do not re-spec)
- **WO-773** multi-channel Obsidian job queue: **Builders / Training / Research**, offline-fair wall clock, FIFO pending, slot caps (`BuildTimerService` + `ObsidianQueueEngine`). Save v35.
- **WO-778** surface completeness: **Work** HUD button → `ObsidianQueueGate.RequestToggle`, target labels (`Footman x1`, `Barracks -> L2`), `layout.body` scroll, Train → `EnqueueTraining`, Instant / Ad / +slot buttons. Shipped.
- Player-facing names: **Builders / Training / Research** — never "Obsidian" (`docs/PAIN_POINTS_2026-07-26.md`).

### The gap
The **engine is CoC-correct**, but the **read is a text dump modal**:
- ASCII markers (`>`, `...`, `- free`)
- Lines of TMP labels in a scroll list
- Opened as a full WORK QUEUE panel, not a glanceable always-on production strip

That is **not** how Warcraft 3 (or CoC's production strip) trains muscle memory. The player should *see* production the way WC3 taught a generation:

> **Portrait + progress + icon strip of what's next** — not a log of sentences.

### North star (WC3 visual language — adapt, don't clone)

Warcraft 3 production UI traits to **steal the feel of** (not the full command card):

1. **Production is glanceable** — when something is building/training, the player knows without opening a novel.
2. **Current job = large chip/portrait** with a **progress ring or bar** (time remaining is visual first, digits second).
3. **Pending jobs = smaller icons in a horizontal FIFO strip** (you *see* the queue of units/buildings coming up).
4. **One row per worker/slot** (in WC3: per building; in our game: per active slot per channel).
5. **Click / tap a chip** expands detail or focuses that channel (optional secondary).
6. **Empty slots** are visible silhouettes ("free worker / free train bay") so capacity is taught by geometry, not the word "free".
7. **Colorblind-safe:** progress is shape + fill + number, never green-vs-red alone (owner is red/green colorblind). ASCII or icon markers remain allowed; no tofu glyphs (LiberationSans SDF — stick to kit + ASCII).

**We keep our domain model:** three **channels** (Builders / Training / Research), not "every building has its own WC3 command card." Visually map:

| WC3 concept | Our game mapping |
|-------------|------------------|
| Building producing | One **active slot** on a channel (busy worker / train bay / lab) |
| Unit icon in queue | Job **target icon** (troop / structure / research) |
| Progress pie under portrait | `FinishMs - now` as ring or bar on the active chip |
| Queued units to the right | Pending FIFO as a **horizontal icon strip** |
| Multiple buildings training | Multiple active slots **side by side** within a channel row |

---

## 2. Architecture constraints (Claude must design *inside* these)

From `START_HERE.md` / `docs/ARCHITECTURE.md` / `ARCHITECTURE_PRINCIPLES.md`:

1. **Presentation-only** — design observes queue state; does not invent new job kinds or timers.
2. **HUD ↔ Village never cross-reference** — open seam stays `ObsidianQueueGate` (Core). Any always-on strip that lives in HUD must call Core only.
3. **Code-built uGUI / ElarionUiKit** — no UXML/UIDocument for gameplay UI.
4. **Portrait mobile first** (1080×1920 ref) — WC3 was landscape desktop; **recompose for portrait**. Prefer bottom or edge production dock, not a landscape command-card clone.
5. **Touch floor** `MinTouchPx` / `TapTarget` (see WO-779 rubric) — chips must be fat enough for thumbs.
6. **One modal at a time** (WO-795) — if the deep panel remains, it must not stack over other modals; prefer the WC3 strip *reducing* need for the full modal.
7. **WO-779** spacing/legibility law applies to any new chrome (layout.body, no title/Close collision, scroll when needed).
8. **Do not redesign the economy** (Instant / Ad / Buy-slot already exist — place them in the new chrome, don't invent new monetization).

### Engine seams Claude may assume (read-only references for annotations)

| Seam | Role |
|------|------|
| `BuildTimerService.ActiveJobsOf(channel)` / `PendingJobsOf` / `SlotCount` | Data for chips + strips |
| `QueueChanged` + 1s tick | Repaint |
| `ObsidianQueueGate.RequestToggle` | Full panel open (if retained) |
| `ObsidianQueueHud.FormatJobLine` / `FormatJobTarget` | Label copy (can become icon tooltips) |
| `TryInstantFinish` / `WatchAdToSkip` / `BuySlot` | Sell-time affordances |

**Out of scope for this WO:** changing cascade/FIFO/offline resolve, save schema, barracks training rules.

---

## 3. Design deliverables (what Claude ships — the "RESULT" of this WO)

Claude produces a **design pack** under something like:

`docs/ui/WO-798_wc3_queue/` (or `UI_REVIEW/WO-798_wc3_queue/`)

### Required artifacts

1. **Problem statement (½ page)** — why text-modal fails vs WC3 glance language (owner-facing).
2. **Visual north-star board** — 3–6 reference stills / annotated crops:
   - WC3 production (unit queue under building)
   - Optional: CoC builder hut row / train camp row (for multi-channel parallel)
   - Note what we adopt vs reject
3. **Layout proposals (at least 2, recommend 1)** on **portrait 1080×1920**:
   - **A. Persistent production dock** (WC3-like strip along bottom or lower-third when any channel is busy; collapses when idle)
   - **B. Compact chip cluster** near the existing **Work** button (expand on tap)
   - **C. (Optional) Hybrid** — thin always-on strip + full WORK QUEUE panel for sell-time / multi-line detail  
   Owner picks one primary.
4. **Channel anatomy** (one diagram per channel row):
   - Header: `BUILDERS 1/2` · `TRAINING` · `RESEARCH`
   - Active slot chip: icon + progress ring + time remaining
   - Pending strip: N icons, overflow `+N` if needed
   - Empty slot silhouette
5. **States** (annotated frames for each):
   - Idle (all free) — strip hidden vs ghost empty slots?
   - One train job active, 3 pending
   - Two builder slots busy + research running (parallel channels)
   - Sell-time: Instant / Ad / +slot placement (must remain reachable; not buried)
6. **Iconography plan** — where icons come from (troop catalog, structure catalog, RpgUi glyphs). Fallback when art missing (role glyph, not blank).
7. **Copy deck** — player-facing strings only (ASCII): channel titles, empty state, tooltips. Never "Obsidian".
8. **Colorblind + contrast** — fill patterns / markers / numbers; black Obsidian chrome; gold gilt accents per kit.
9. **Motion notes** — progress tick, chip pop when a job starts, subtle complete flash (no spam).
10. **Acceptance checklist for CLI** — measurable bullets CLI can implement + screenshot-verify (below §5).
11. **Image pair pack** — for each chosen layout: mockup PNG + short "what to compare" note (owner sign-off path per SAMANTHA / image-pair law).

### Optional stretch (still read-only)
- Building-local strip: when player selects a structure under construction, a WC3-style mini queue on that building (world or follow HUD) — **only if it doesn't violate "presentation never on objects"** (prefer a HUD reaction to selection state, not a component on the building).
- Barracks Train tab: same icon-strip language as the global Training channel (consistency with WO-778 train strip).

---

## 4. Interaction design questions (Claude proposes; owner decides)

Claude's pack must **recommend defaults** for each:

| # | Question | Default lean (CLI product read — challenge if wrong) |
|---|----------|------------------------------------------------------|
| Q1 | Always-on strip when busy vs only after Work tap? | **Always-on when any active job**; idle = no chrome (or tiny Work chip only) |
| Q2 | Full WORK QUEUE modal keep or retire? | **Keep as overflow / sell-time detail**; strip is primary glance |
| Q3 | Progress: ring vs bar? | **Ring on chip** (WC3 pie feel) + optional bar in expanded detail |
| Q4 | Pending strip max visible icons before `+N`? | **4–5** on phone width |
| Q5 | Tap active chip → ? | Expand detail sheet (time, Instant/Ad) without full modal if possible |
| Q6 | How to show free slots? | Empty dashed chip, count in header `1/2 busy` |
| Q7 | Channel order on screen? | Builders → Training → Research (top→bottom or left→right) |

---

## 5. Acceptance (design WO complete when…)

### For Claude (this WO's Done)
- [ ] Design pack path exists with all **Required artifacts** §3.
- [ ] Owner has image-pair sign-off on the **chosen layout** (A/B/C).
- [ ] CLI acceptance checklist is unambiguous (no "make it nicer").
- [ ] Explicit **Do Not** list for implementers (engine, schema, UXML, color-only state).

### For CLI later (implementation WO — split or same number with Status flip)
- [ ] Production dock/strip matches signed mockups at 1080×1920 and device Seeker.
- [ ] Data still comes only from `BuildTimerService` channel APIs (no parallel timer).
- [ ] Colorblind + ASCII/tofu rules hold (`HudUiRegression` / tofu oracle green).
- [ ] Headless screenshot-verify: busy multi-channel state + idle state (`headless-screenshot-verify-ui-before-build`).
- [ ] `ObsidianQueueRegression` extended or companion oracle for "strip exists when active job" if always-on.
- [ ] Felt (owner): train Footman → see **icon + progress** without reading a paragraph; builders can run at the same time.

---

## 6. Sequencing / collisions

| WO | Relationship |
|----|----------------|
| **WO-773** | Engine — **frozen for this design** |
| **WO-778** | Surface complete — **baseline to restyle**, not re-open P0 reachability |
| **WO-779** | Spacing/legibility sweep — new chrome must obey rubric; fold findings into 779 if concurrent |
| **WO-795** | No stacked screens — dock must not fight modals |
| **WO-774** | Raid UI — **out of scope** |

**Dispatch order:** Claude designs now (read-only). Implementation only after owner signs mockups. Prefer **not** concurrent with a massive WO-779 panel rewrite of the same files — if both land, one lane owns `ObsidianQueueHud` + HudKit Work affordance.

---

## 7. Do NOT (Claude + future CLI)

- Do **not** rewrite `ObsidianQueueEngine` / offline resolve / save schema.
- Do **not** introduce UXML/USS for the queue.
- Do **not** put "Obsidian" in player copy.
- Do **not** use non-ASCII TMP glyphs (→, ★, ●, …) that tofu on device.
- Do **not** encode state by color alone.
- Do **not** hand-edit `.unity` scenes.
- Do **not** smuggle MVVM refactors into the visual pass (presentation chrome only; keep View dumb if possible).
- Do **not** invent a fourth channel without a product WO.

---

## 8. SME one-pager (for Claude session boot)

**Product:** Echoes of Elarion — CoC-style multi-channel work (build / train / research) with **WC3-style production glance UI**.  
**Spine:** player controls one hero; city production is autonomous + queued.  
**Law:** presentation layer observes jobs; jobs live in Core/Village services.  
**Current UI:** modal text WORK QUEUE + Work button (WO-778).  
**Target UI:** icon chips, progress rings, horizontal pending strips, portrait-mobile dock.  
**Success:** "I can see what my town is making the way I saw units training in Warcraft 3 — without opening a wall of text."

---

## 9. Suggested Claude first actions (read-only)

1. Read `START_HERE.md` → `CANON_GROUND_TRUTH_*` → this WO → `ObsidianQueueHud.cs` header (skin only).  
2. **Start from the CLI wireframe pack (already authored):**  
   `docs/UI/WO-798_wc3_queue/README.md`  
   - `WIREFRAMES.md` — A/B/C layouts + chip anatomy + state matrix  
   - `wireframe_A_production_dock.html` — open in browser (S0 / S3 / S5)  
   - `layout_A.svg` — vector of recommended dock  
3. Capture 2–3 WC3 production references (fair use, internal only) and map to Layout A.  
4. Polish Layout A (and B if challenging) to full Obsidian kit mockup PNGs.  
5. Post image pairs for owner sign-off.  
6. Freeze acceptance checklist for CLI; do not implement code.

---

**Mint note:** When this file is committed, set `CLI_LANES_WO_NUMBERS.md` next-free to **799** and list **798 = WC3-style queue visual (Claude design pack)**.
