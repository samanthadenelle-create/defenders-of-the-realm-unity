# WORK ORDER 864 — Queue UI: CoC-style horizontal icon-card rail

**Status:** READY TO IMPLEMENT
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** HUD/UI — `ObsidianQueueHud.cs` (the Work Queue modal). Presentation only; no timer/economy logic change.
**WO#:** UI-seat block (860–899); 860–863 used, **864**=this.
**Origin:** owner 2026-08-03, referencing her CoC "Builder's Queue — Side Elevation" spec diagram — *"can you make
our queues similar in visual to these?"*

---

## 1. Target visual (owner's reference)
A **horizontal row of queue-slot CARDS on a unified rail/base-frame**, each card carrying:
- a **building/troop ICON**,
- a **name + level** (e.g. "Cannon Lv 14", "Barracks Lv 12"),
- a **countdown timer** with a **clock/status indicator** (e.g. a clock icon + "0:45"),
- **troops occupy 1 slot each**, collapsed to one card with a **stack count badge** (e.g. "x5") when several of the
  same troop queue,
- timers **count down SEQUENTIALLY** (only the active slot ticks; the rest are queued),
- a decorative **base frame + rail + support bracket** tying the slots together, with the builder/settings controls at
  the end.

## 2. Current state (RCA, from `ObsidianQueueHud.cs`)
Today the queue is a **vertical scrolling list of ASCII TEXT rows**, grouped by the 3 channels (Builders/Training/
Research): `Refresh()` (`:182-228`) emits `AddChannelHeader` + per-slot `AddJobRow`/`AddTextRow` — `"> Barracks -> L2
1m 30s"`, `"... Footman x1 (queued)"`, `"- free"`. No icons, no cards, no rail, no progress fill.
**Data already available** (so this is presentation only): `BuildTimerService.ActiveJobsOf/PendingJobsOf/SlotCount(channel)`;
per job `BuildJobData` has `StructureId`, `JobKind`, `StartMs`, `FinishMs`, `TargetTier` — enough for icon lookup +
name/level (`JobTargetLabel` already derives "Barracks -> L2" etc.) + live timer (`FinishMs-now`) + a progress fill
(`FinishMs-StartMs` = total).

## 3. The redesign
**THREE SEPARATE, VISUALLY-DISTINCT QUEUES (owner, explicit):** render Builders, Training, and Research as **three
independent CoC-style queues** — each its OWN titled rail + base-frame (own header, own row of icon-cards, own
+slot), stacked in the modal with clear separation between them. NOT one list with three sub-headers, NOT a merged
global queue. Each reads as its own builder's queue.

Replace the vertical text rows with a **horizontal icon-card rail PER QUEUE**:
- **Per queue:** a header ("BUILDERS  2/3") + a **horizontal row of slot cards** (one per `SlotCount(channel)`),
  active jobs first, then queued, then "free" empty-slot cards. **Each queue's slot count is INDEPENDENT and can
  differ** (owner: e.g. 2 Builder slots vs 5 Training/Research slots) and **grows when the player buys a slot** — so
  each rail sizes to ITS OWN `SlotCount`, reflowing as slots are added. If a rail's slots exceed the modal width, that
  rail **scrolls horizontally** (min touch size per card); the three rails size independently of each other.
- **Card anatomy** (reuse `ElarionUiKit` card/frame primitives; fixed-pixel bands per the WO-841 lesson):
  - **Icon** — building portrait (`Portraits/<slug>`, the `BuildingUpgradePanel.LoadPortrait` pattern) or troop icon
    (`TroopCatalog`). Guard-wrapped with a text/initial fallback so a missing icon never blanks the card.
  - **Name + level** — reuse `JobTargetLabel` (it already yields "Cannon", "Barracks -> L2", "Footman x1").
  - **Timer + clock** — a small **clock ICON** (a sprite from `RpgUiCatalog`, NOT a glyph — LiberationSans SDF has no
    clock glyph → tofu) + the `FormatTime` countdown. Active slot ticks (the existing 1s `Update` refresh); queued
    slots show their duration in a **queued** state (dimmed / "queued" text, colorblind-safe by text+shape).
  - **Stack badge** — when N identical troop-train jobs are pending in a channel, collapse them to ONE card with a
    "xN" count badge (the CoC "Barbarian x5"), instead of N separate cards.
  - **Optional progress fill** — a thin bar under the icon (0..1 = `1 - remaining/total`). This is WO-816's scope
    (`ObsidianQueueGate.QueueEntry` needs `TotalSec`); reference it, don't duplicate. Nice-to-have, not required.
- **Rail/base-frame** — the cards sit on a shared horizontal rail with the Obsidian frame chrome (base frame + a
  subtle bracket), matching the reference's "one connected queue" read rather than free-floating rows.
- **Actions preserved** — the existing Instant-finish (`price c`), Ad-skip, and +slot buttons stay reachable (on the
  card, a long-press, or a per-channel action row) — do NOT drop the `BuildTimerService` action hooks.

## 4. Constraints (keep)
- Colorblind-safe: status by **icon + text + shape**, never color alone. ASCII-only TMP strings; clock/status = sprites.
- Channels stay SEPARATE (Builders/Training/Research) — do not merge into one global list.
- No new timer/economy logic — `ObsidianQueueHud` stays a dumb skin over `BuildTimerService`.
- Fixed-pixel card/label bands (WO-841 lesson) so nothing culls/overlaps at the mobile resolutions.

## 4b. Lightweight / performance (owner: "not too heavy on display")
This must stay cheap — the visual is rich but the cost is bounded. Requirements:
- **Hidden modal — zero cost when closed** (as today; it only builds/renders while open). Keep it that way; nothing
  persistent added to the always-on HUD by this WO.
- **Do NOT full-rebuild every second.** The existing 1s `Update` tick currently destroys + rebuilds ALL rows
  (`Refresh` `:186-188`). In the card design, the 1s tick must update **only the active card(s)' timer text + progress
  fill** — NOT tear down/rebuild the cards. Rebuild cards **only on `QueueChanged`** (a job starts/finishes/adds/
  removes/reorders). This is the WO-836 cheap-tick lesson; it avoids per-second layout churn + fit-guard re-arm.
- **Cache icon sprites** — load each building/troop icon ONCE and reuse (the `LoadPortrait` cache pattern); never
  reload sprites on every refresh.
- **Bounded + flat** — card count is small (per-queue `SlotCount`, ~2–5 each); keep visuals flat (no shadows/blur/
  gradients/overdraw-heavy layers), no per-frame work. Horizontal scroll uses the existing scroll primitive.

## 5. Files to edit
- `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs` — `Refresh()` + new card/rail builders (replace the
  `AddChannelHeader`/`AddJobRow`/`AddTextRow` vertical-text path). Add an icon-lookup helper (Guard fallback).
- (Reuse `ElarionUiKit` card/frame primitives + `RpgUiCatalog` clock/status sprites; no new kit file.)
- (Progress fill = WO-816; reference, don't duplicate.)

## 6. Acceptance criteria (headless UI-capture, editor CLOSED)
- [ ] The Work Queue shows each channel as a HORIZONTAL row of icon-cards on a rail — icon + name/level + clock+timer
      per card, matching the reference read.
- [ ] The active slot's timer ticks live; queued slots show a distinct queued state; free slots read as empty cards.
- [ ] N identical queued troop trains collapse to one card with a "xN" stack badge.
- [ ] Missing icon falls back to text/initial (Guard) — never a blank card.
- [ ] Instant-finish / Ad-skip / +slot actions still work.
- [ ] **Lightweight:** the 1s tick updates only timer text + fill (no full card rebuild); cards rebuild only on
      `QueueChanged`; icons cached; no cost when the modal is closed.
- [ ] `RunCaptureHeadless` renders it cleanly at the mobile resolutions (no overlap/culling); `CompileGate` green.

## 7. Do NOT
- Do NOT merge the 3 channels; do NOT change `BuildTimerService`/economy logic.
- Do NOT use non-ASCII glyphs for the clock/status (use sprites); no color-only state.
- Do NOT duplicate WO-816's progress-bar plumbing — reference it.
- Do NOT hand-edit scenes.
