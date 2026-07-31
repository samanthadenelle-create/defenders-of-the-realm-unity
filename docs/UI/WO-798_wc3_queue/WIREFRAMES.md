# WO-798 Wireframes — build on the live QueueStatus chip

**Canvas:** 1080 × 1920 portrait  
**Primary layout:** **right-column** `HudArea.QueueStatus` (already shipped)  
**Read first:** `CODE_AS_IS.md`  
**WO:** `WorkOrders/WORK_ORDER_798_wc3_style_queue_visual.md`

Greenfield bottom “production dock” is **alternate only** (owner must reject right column first).

---

## 0. Live before (shipped — design from this)

Area: `(0.78, 0.53)–(0.995, 0.865)` right side, above ActionRail.

```
┌─────────────────────────┐
│  Builders 2/2           │  ← Obsidian button; tap = RequestToggle
│  0:42 | Training 1      │  ← FormatQueueChip
├─────────────────────────┤
│ > Wall Upgrade  0:42    │  ← FormatQueueRows (TMP, max 5)
│ > Tower Build   3:10    │     ">" working  "-" queued
│ - Gate                  │
│ - Barracks Upgrade      │
│ - Lumberyard            │
│ +2 more                 │
└─────────────────────────┘
  plate hidden when Entries empty
  Data: Status.Entries = Builder only
```

---

## 1. Layout A′ — PRIMARY (upgrade same host)

Same anchors. Replace text plate body with icon production line.

```
┌─────────────────────────┐
│  Builders 2/2           │  summary (keep)
│  0:42 | T1              │
├─────────────────────────┤
│ ┌────┐ ┌────┐  · [G][R] │  active rings + pending strip
│ │ W  │ │ T  │           │
│ │◔0:42│ │◔3:10│         │
│ └────┘ └────┘           │
│ TRAINING (when busy)    │  M2 multi-channel lean
│ ┌────┐ · [F][A]         │
│ │ F  │                  │
│ │◔1:05│                 │
│ └────┘                  │
└─────────────────────────┘
```

### Chip anatomy (active)

```
   ┌────────┐
   │  /--\  │  ring = Progress01 (geometry + digits)
   │ | IC | │  icon or 1–2 letter glyph fallback
   │  \--/  │
   │ 0:42   │  RemainingSec formatted
   └────────┘
   min ~56–72px wide in column; touch ≥88 if tappable
```

### Pending strip

```
 [G] [R] [+2]     left→right = FIFO; Queued=true; RemainingSec=-1
```

### Behavior (match code)

| Action | Result |
|--------|--------|
| Tap summary button | `ObsidianQueueGate.RequestToggle` (unchanged) |
| Empty Entries | Hide plate only (unchanged) |
| Job complete | Next pending becomes active chip (engine already cascades) |
| Tap active chip (optional) | Detail sheet Instant/Ad — needs StructureId on entry |

---

## 2. Layout B — Compact (reject if owner wants less chrome)

Keep summary button only; 5-deep plate gone; badge `•3` on Work/Builders.  
**Worse WC3 feel** — only if space crisis.

---

## 3. Layout C — Bottom dock (alternate)

Full-width dock above town buttons (old wireframe HTML).  
**Only if** owner rejects right column (conflicts with ActionRail / move cluster — re-layout risk).

Open `wireframe_A_production_dock.html` as **feel reference**, not default placement.

---

## 4. Multi-channel options (owner picks)

| ID | Glance shows | Contract change |
|----|--------------|-----------------|
| **M1** | Builder icons only; Training stays on chip line 2 | Minimal — only iconize Entries |
| **M2** | Builder row + Training/Research mini-rows when busy | Publish more rows or second arrays |
| **M3** | One mixed Entries list with channel tags | `QueueEntry.Channel` required |

**CLI lean: M2.**

---

## 5. States to mock

| ID | Data | Paint |
|----|------|--------|
| S0 | Entries empty | Button only, plate off |
| S1 | 1 active builder | One ring chip |
| S2 | 2 active + 5 pending | 2 chips + strip + +N |
| S3 | Builder + Train busy | M2 second row |
| S5 | Expand Instant/Ad | Optional overlay |

---

## 6. CLI checklist (after sign-off)

1. Extend `QueueEntry` if needed (`StructureId` / `IconKey` / `Progress01` / channel).  
2. `PublishStatus` fills new fields (Village).  
3. Replace `FormatQueueRows` text block with chip UI under `_queueRowsPlate` (or rebuild plate children).  
4. Keep Version poll in HudKit Update.  
5. Icon resolve in HUD via Core-safe keys only (or pre-resolved sprite name string).  
6. Regression: Entries still publish; glance non-empty when Builder busy.  
7. Screenshot-verify S2 + S0.

---

## 7. What NOT to draw as “required”

- Second floating queue elsewhere without owner OK  
- Landscape WC3 command card  
- Color-only progress  
- Unicode pie characters that tofu  
