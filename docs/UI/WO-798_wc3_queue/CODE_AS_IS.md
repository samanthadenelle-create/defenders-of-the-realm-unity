# WO-798 — Queue code as of HEAD (CLI read 2026-07-30)

Claude: **design against this**, not a blank CoC mock. Partial WC3-style UI already shipped (`631d1e21`).

---

## Stack (layers)

```
┌─────────────────────────────────────────────────────────────┐
│ PRESENTATION (DeNelle.HUD)                                  │
│  HudKitController.BuildQueueStatusChip                      │
│   · summary button "Builders n/m" + soonest timer           │
│   · plate under it: up to 5 text rows (FormatQueueRows)     │
│   · tap → ObsidianQueueGate.RequestToggle()                 │
│  HudAreasHost: HudArea.QueueStatus                          │
│   anchors (0.78, 0.53)–(0.995, 0.865)  right column         │
├─────────────────────────────────────────────────────────────┤
│ CORE SEAM (DeNelle.Core)                                    │
│  ObsidianQueueGate                                          │
│   · ToggleRequested / RequestToggle  (open full panel)      │
│   · WorkQueueStatus Status  (HUD polls; Version change-det) │
│   · QueueEntry { Label, RemainingSec, Queued }              │
│   · PublishStatus(s)  (Village only writes)                 │
├─────────────────────────────────────────────────────────────┤
│ SERVICE (DeNelle.Village)                                   │
│  BuildTimerService                                          │
│   · owns channels, TimeSource, SweepAllChannels ~1s         │
│   · PublishStatus() on QueueChanged + tick                  │
│   · Entries = Builder channel only (active then pending)    │
│   · PrettyJobLabel(structureId)  string title-case, no icon │
│  ObsidianQueueHud  (full WORK QUEUE modal)                  │
│   · 3 channels Builders/Training/Research text list         │
│   · layout.body + scroll, Instant/Ad/+slot                  │
├─────────────────────────────────────────────────────────────┤
│ ENGINE (DeNelle.Core.Jobs) — DO NOT REDESIGN                │
│  ObsidianQueueEngine  pure FIFO / slots / offline cascade   │
│  ChannelId: Builder | Train | Research                      │
│  BuildJobData  StructureId, Kind, StartMs, DurationMs, …    │
└─────────────────────────────────────────────────────────────┘
```

**Asmdef law:** HUD never references Village. Clock + labels computed in Village → pushed through Core → HUD paints.

---

## What the player sees TODAY

### A. Always-on HUD chip (right column) — already “WC3 5-deep” *text*

| Piece | Code | Behavior |
|-------|------|----------|
| Summary button | `BuildQueueStatusChip` | `"Builders 1/2\n9m 30s"` or `"idle"`; if training: `"… \| Training 1"` |
| Rows plate | `_queueRowsPlate` | Hidden when no Builder jobs |
| Rows text | `FormatQueueRows` | Max **5** lines: `"> Barracks  9m 30s"` working, `"- Arcane Spire"` queued, then `"+N more"` |
| Data | `Status.Entries` | **Builder channel only** (not Train/Research in the 5-deep list) |
| Cap published | `PublishStatus` | Up to **7** entries (2 active + 5 queued typical); chip shows 5 |

```
┌─────────────────────┐  QueueStatus area
│  Builders 2/2       │  ← button (tap = open modal)
│  0:42 | Training 1  │
├─────────────────────┤
│ > Wall Upgrade 0:42 │  ← text plate (raycast off)
│ > Tower Build 3:10  │
│ - Gate              │
│ - Barracks Upgrade  │
│ - Lumberyard        │
│ +2 more             │
└─────────────────────┘
```

**Not yet WC3:** no unit/building **icons**, no **progress rings**, no horizontal pending strip, Training/Research not in the 5-deep list (only a Training count on line 2 of the chip).

### B. Full WORK QUEUE modal — text channels

`ObsidianQueueHud`: FrameCore modal, scroll body, all 3 channels, free slots as `"- free"`, sell-time Instant/Ad when price > 0, +slot per channel.

### C. Barracks train strip

`TroopTrainingPanel` reads `ActiveJobsOf(Train)` / pending (inline text, not icons).

---

## Key APIs (stable for design)

```csharp
// Core — HUD reads only this
ObsidianQueueGate.Status          // WorkQueueStatus
ObsidianQueueGate.RequestToggle() // open/close modal

// Village — publisher
BuildTimerService.ActiveJobsOf(ChannelId)
BuildTimerService.PendingJobsOf(ChannelId)
BuildTimerService.SlotCount(ChannelId)
BuildTimerService.QueueChanged
// Instant / Ad / BuySlot already exist for modal
```

`WorkQueueStatus` fields today:

- Per channel busy/slots/queued counts (all 3)
- `SoonestRemainingSec`
- `Entries[]` — **Builder-only** job rows for the 5-deep view
- `Version` for dirty check

---

## Gaps vs WO-798 wireframe Layout A

| Wireframe Layout A | Code today |
|--------------------|------------|
| Icon chips + rings | Text lines only (`>` / `-`) |
| Horizontal pending strip | Vertical text list |
| All 3 channels in dock | 5-deep = Builder only; Train is a chip footnote |
| Bottom production dock | **Right-column** QueueStatus band |
| Idle hides dock | Rows plate hides; **Builders button always shows** |
| Chip expand → Instant/Ad | Instant/Ad only on **full modal** job rows |

---

## Design implication for Claude (important)

**Do not invent a second parallel queue system.** Evolve:

1. **Presentation of `QueueEntry`** — from text lines → icon + ring chips (may need `IconKey` or structureId on `QueueEntry` if catalog art is required; today only `Label` string is published).
2. **Whether Entries stay Builder-only** or become multi-channel (product call).
3. **Layout** — keep right-column (matches shipped HudArea) vs move to bottom dock (wireframe A). Owner must pick; right-column is already in posture/occupancy.
4. Keep `PublishStatus` / poll-by-Version pattern (no Village reference from HUD).

---

## Files to open

| Path | Role |
|------|------|
| `Assets/_Modules/Core/UI/ObsidianQueueGate.cs` | Seam + snapshot structs |
| `Assets/_Modules/Village/Buildings/BuildTimerService.cs` | `PublishStatus`, `PrettyJobLabel` (~631–711) |
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | Chip + `FormatQueueRows` (~603–695, ~1627–1645) |
| `Assets/_Modules/HUD/Kit/HudAreasHost.cs` | `QueueStatus` anchors (~105–110) |
| `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs` | Full modal |
| `Assets/_Modules/Core/Jobs/ObsidianQueueEngine.cs` | Engine (frozen) |

Commit that landed 5-deep text: `631d1e21 feat(hud): WC3-style 5-deep queue rows under the Builders chip`.
