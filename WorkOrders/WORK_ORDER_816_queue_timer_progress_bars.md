# WO-816 — Queue timer UI: progress bars that feel in-game (not text-only countdowns)

**Status:** READY TO IMPLEMENT — **absorbed as Phase 2 of WO-817** (CoC/WC3 queue visual system)  
**Minted:** 2026-08-01 (CLI, code review of queue countdown display)  
**Master:** `WorkOrders/WORK_ORDER_817_coc_wc3_queue_visual_system.md`  
**Lane:** Queue presentation (HUD glance + WORK QUEUE modal + shared format)  
**Roles:** CLI implements; Claude optional mock of bar row anatomy  
**Related:** WO-778 · WO-798/801 · WO-799 · `UnderConstructionVisual`  

---

## 0. Code review — how timers display TODAY

### Data (correct — keep)
| Piece | Behavior |
|-------|----------|
| Clock | Wall-clock unix ms (`TimeSource.NowUnixMs`) |
| Job | `BuildJobData`: `StartMs`, `DurationMs`, `FinishMs` |
| Service | `BuildTimerService.RemainingSeconds(id)`, **`Progress(id)` already 0..1** for Builder jobs |
| Publish | `PublishStatus()` on `QueueChanged` + ~1s `Update` tick → `ObsidianQueueGate.Status` |
| Engine | Offline-fair; no change |

### Presentation (the problem — text-only)

| Surface | File | What player sees |
|---------|------|------------------|
| **HUD QueueStatus chip** | `HudKitController.FormatQueueChip` / `FormatQueueRows` | `"Builders 1/2"` + `"9m 30s"`; rows `"> Barracks  9m 30s"` / `"- Arcane Spire"` — **one TMP block, no fill** |
| **WORK QUEUE modal** | `ObsidianQueueHud.FormatJobLine` + rebuild list | `"> Footman x1  1m 05s"` pure string; full list destroyed/rebuilt on each Refresh (~1s) |
| **Barracks train strip** | `TroopTrainingPanel` | Same `FormatJobLine` text |
| **World scaffold** | `UnderConstructionVisual` | Dim mesh + floating **M:SS label only** — no bar |

### Kit already available (reuse)
- `ElarionUiKit.BuildObsidianBar(..., ObsidianBarKind.Stat | Loading | Xp, ...)` + `BarHandle.SetValue` / fill contract  
- Wave HUD already uses bars for progress (`BuildWaveBlock`)  
- **No queue surface uses a bar today**

### Why it feels out of game
1. **Digits without geometry** — mobile games / CoC / WC3 show **fill** (bar or pie); raw `"9m 30s"` reads as debug/UI kit, not craft.  
2. **1s step jumps** — remaining text ticks once per second; no continuous fill motion.  
3. **No shared component** — three surfaces reinvent string time; bars would drift if not one builder.  
4. **Queued vs active** look almost the same (`>` vs `-`) without empty-vs-filling bar.  
5. **Colorblind** — bars must still show **digits + fill amount**, never color alone (owner law).

---

## 1. Recommendation (best practice for this project)

### Primary: **compact progress BAR + time digits** (not pie as primary)
| Choice | Why |
|--------|-----|
| **Horizontal fill bar** under/beside job name | CoC, most mobile builders, readable on portrait; kit already has bars |
| **Remaining time as secondary** (`1m 05s`) right of name or on bar | Digits stay; bar carries “how much left” at a glance |
| **Pie/ring optional later** for tiny HUD icons only | WC3-like; harder on thin right-column chip; **defer** unless 801 icons land first |
| **Gold / Stat / Loading tint** (`ObsidianBarKind.Stat` or `Loading`) | Matches Obsidian chrome — “belongs” with HP/XP language without looking like health |

### Motion
- Prefer **smooth fill**: either  
  - **A)** publish `Progress01` + `RemainingSec` each 1s and **lerp** fill client-side between ticks, or  
  - **B)** HUD/modal tick fill every frame from last snapshot + elapsed unscaled time (predict until next publish)  
- Recommended: **B for modal/open panel**, **A or B for glance** — never trust frame-time as authority; snap to service on each `PublishStatus`.

### Queued jobs
- **Empty bar** (0 fill) + label `"queued"` / position in line — **no fake countdown**  
- Active: fill = elapsed/duration  

---

## 2. Goal

Replace text-only queue countdowns with a **shared in-game timer row** (name + bar + time) on:

1. **WORK QUEUE modal** job rows (all channels)  
2. **HUD QueueStatus** active entries (at least top 1–5 active; queued stay compact)  
3. **Train strip** in Barracks (same component)  
4. **Optional stretch:** world `UnderConstructionVisual` thin billboard bar under M:SS  

Engine/clock **unchanged**. Presentation-only + small snapshot fields.

**Success bar:** Player feels build/train timers like CoC builders — “I see the job cooking” — not a terminal log.

---

## 3. Data seam (additive)

### Extend `ObsidianQueueGate.QueueEntry`
```csharp
public string Label;
public int RemainingSec;      // -1 if queued
public bool Queued;
public float Progress01;      // 0..1 active; 0 queued; NEW
public int DurationSec;       // total length; 0 if unknown; NEW (optional but useful for display)
public string StructureId;    // NEW optional — Instant/Ad/cancel without Village from modal if needed later
```

### `BuildTimerService.PublishStatus`
- For each active entry: set `Progress01` from same math as `Progress()` (generalize Progress to any channel job, not Builder-only structureId lookup).  
- Queued: `Progress01 = 0`, `RemainingSec = -1`.  
- Keep `Version` bump on publish.

### Pure helper (recommended)
`QueueTimerFormat.FormatRemaining(int sec)` — single place for `9m 30s` / `45s` / `1h 2m` (ASCII).  
Both HudKit and ObsidianQueueHud call it (or move into Core UI next to gate).

---

## 4. Shared UI component

### `QueueTimerRow` (code-built; kit-friendly)
Build once, reuse:

```
[ optional icon 32–40px ]  Job name (ellipsis)
[████████░░░░] 1m 05s     ← bar track + fill + time label
```

| Element | Spec |
|---------|------|
| Bar height | 10–14 ref-px (thin; not HP thickness) |
| Bar kind | `ObsidianBarKind.Stat` or `Loading` (gold-ish) |
| Fill | Left→right; 0 = empty, 1 = complete |
| Time | ASCII compact; font ≥ FontMicro/Label floor |
| Queued | Empty bar + `"queued"` or `"#2"`; no progressive fill |
| Touch | Row may host Instant/Ad (modal); bar itself non-raycast |

Prefer factory on `ElarionUiKit` or small Village helper used by modal + train strip; HUD may use a slimmer variant.

### Update policy
- **Do not** Destroy/rebuild entire modal list every second if avoidable — keep row instances and `SetValue(progress)` + time text.  
- On `QueueChanged` (job set change): rebuild rows.  
- On 1s tick / frame predict: update fills only.

---

## 5. Surface-specific work

### 5.1 WORK QUEUE modal (`ObsidianQueueHud`)
- Replace pure text job lines with `QueueTimerRow`.  
- Free slots: keep `"- free"` text or empty slot chrome (no bar).  
- Instant / Ad buttons stay on active rows (existing).  
- Channel headers unchanged.

### 5.2 HUD glance (`HudKitController` QueueStatus)
- Summary button: keep `Builders n/m` + soonest remaining; **optional** thin bar under summary for **soonest** job only.  
- Rows plate: for each **active** entry, name + mini-bar + time (not multi-line raw `"> X  9m"`).  
- Queued: short `"- Label"` or empty mini-bar.  
- Max 5 entries still; plate hide when empty.  
- Compose with future **WO-801 icons** (icon left of name when ready).

### 5.3 Barracks train strip
- Use same row component / FormatJobLine retirement for active train jobs.

### 5.4 World (stretch / same WO if small)
- `UnderConstructionVisual`: add thin world-space quad/bar behind or below TMP M:SS driven by `Progress(key)`.  
- If timeboxed, ship HUD+modal first; world bar as acceptance stretch.

---

## 6. Colorblind / accessibility / law
- Fill **and** digits always.  
- No color-only “almost done” (optional second digit intensity is fine).  
- ASCII only.  
- MinTouchPx on Instant/Ad/Close, not on the bar track.  
- Fits WO-779/795: no overlap; scroll if many rows.

---

## 7. Acceptance

- [ ] Active jobs show **visible fill** that moves toward complete over real time  
- [ ] Remaining time digits still accurate (service snap each publish)  
- [ ] Queued jobs do **not** animate a fake countdown fill  
- [ ] WORK QUEUE modal + HUD glance both use bar language (shared helper)  
- [ ] Train strip matches  
- [ ] Offline catch-up still correct (fill jumps to right progress on load)  
- [ ] Tofu / HudUi / queue regression green  
- [ ] Felt: “timers feel like the rest of the Obsidian HUD / CoC builders”  
- [ ] Screenshot-verify busy Builder + Train channel  

---

## 8. Do NOT

- Change queue engine, durations, offline resolve, or Instant/Ad economy math  
- Invent a second clock (always derive from StartMs/DurationMs/now)  
- UXML  
- Make bars look like HP (avoid red health bar for builds)  
- Block on full WO-801 icon pack (bars ship without icons)  
- Full WC3 pie as only control (bar primary)  

---

## 9. Implementation order (CLI)

1. Extend `QueueEntry` + `PublishStatus` with `Progress01` (+ DurationSec if easy).  
2. `QueueTimerFormat` + unit tests for remaining string.  
3. `QueueTimerRow` builder (kit bar + labels).  
4. Wire **ObsidianQueueHud** (highest visibility when open).  
5. Wire **HudKit** glance rows.  
6. Wire **TroopTrainingPanel** strip.  
7. Optional world bar on `UnderConstructionVisual`.  
8. Headless/screenshot + felt.

---

## 10. Files (expected)

| Area | Paths |
|------|--------|
| Snapshot | `ObsidianQueueGate.cs`, `BuildTimerService.PublishStatus` |
| Modal | `ObsidianQueueHud.cs` |
| HUD | `HudKitController.cs` (`BuildQueueStatusChip`, FormatQueue*) |
| Train | `TroopTrainingPanel.cs` |
| Kit | `ElarionUiKit` / new small helper |
| World stretch | `UnderConstructionVisual.cs` |
| Tests | format helper + optional progress math |

---

## 11. Relationship to WO-798 / 801

| WO | Role |
|----|------|
| **816** (this) | **Timer feel** — bars + digits on existing text rows |
| **798/801** | Icon chips / multi-channel glance chrome |

Implement **816 even if icons wait** — bars alone upgrade “game feel” a lot. When 801 lands, put **icon + name** above the same bar.

---

## 12. Claude paste (optional mock)

```text
Read WorkOrders/WORK_ORDER_816_queue_timer_progress_bars.md.
Mock one queue job row: name + gold Stat bar fill + "1m 05s", queued empty bar.
No .cs. Match Obsidian black/gold, not HP red.
```
