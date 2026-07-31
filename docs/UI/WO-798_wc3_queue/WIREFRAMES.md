# WO-798 Wireframes — Warcraft 3–style production glance (portrait)

**Canvas:** 1080 × 1920 reference (phone portrait)  
**Theme:** Obsidian black panels + gold gilt (kit), not WC3 blue UI clone  
**Data:** Builders / Training / Research channels (unchanged engine)

---

## 0. What we're stealing from WC3 (feel, not chrome)

```
WC3 (landscape, per building)          Our game (portrait, per CHANNEL)
---------------------------------      ------------------------------------
[Building portrait]                    [Channel row header: BUILDERS 1/2]
  [progress pie]                         [active slot chip + ring]
  [unit][unit][unit] pending ----->      [icon][icon][icon] pending strip
```

**Keep:** icon-first queue, progress on the *current* job, pending as small icons in a line.  
**Drop:** landscape command-card under a selected unit; full RPG inventory aesthetics.  
**Add:** three parallel channel rows (CoC workers), sell-time Instant/Ad on expand.

---

## 1. Layout A — Persistent production dock (RECOMMENDED)

**When:** any channel has an active or pending job → dock visible.  
**When idle:** dock hidden; only existing **Work** town button remains.

```
┌─────────────────────────────────────────┐  y = 0
│  HUD (existing nameplate / resources)   │
│                                         │
│              3D world                   │
│                                         │
│                                         │
│                                         │
├─────────────────────────────────────────┤  ~ y = 0.62  (dock top)
│  PRODUCTION                             │  gold title, 1 line
│ ┌─────────────────────────────────────┐ │
│ │ BUILDERS          2/2 busy          │ │  channel header
│ │ ┌────┐ ┌────┐  ·  [w][w][+]         │ │  2 active chips + pending strip
│ │ │WALL│ │TOWR│                       │ │
│ │ │ ◔  │ │ ◔  │                       │ │  ring = progress; time under
│ │ │0:42│ │3:10│                       │ │
│ │ └────┘ └────┘                       │ │
│ ├─────────────────────────────────────┤ │
│ │ TRAINING          1/1 · 3 queued    │ │
│ │ ┌────┐  ·  [F][F][A][+1]            │ │  Footman active; pending icons
│ │ │ FT │                              │ │
│ │ │ ◔  │                              │ │
│ │ │1:05│                              │ │
│ │ └────┘                              │ │
│ ├─────────────────────────────────────┤ │
│ │ RESEARCH          idle              │ │  optional: collapse when idle
│ │  ( - free - )                       │ │  or omit row entirely when idle
│ └─────────────────────────────────────┘ │
│  [ Work ]  full panel / sell-time       │  opens existing WORK QUEUE modal
├─────────────────────────────────────────┤
│  town buttons: Build Talk Bag Work Qst  │  existing HudKit row (unchanged)
└─────────────────────────────────────────┘  y = 1
```

### Touch / size (ref-px)

| Element | Min size | Notes |
|---------|----------|--------|
| Active chip | 112 × 112 | MinTouchPx |
| Pending icon | 72 × 72 | still tappable; larger if only 2–3 |
| Channel header | full width × 40 | non-tappable label OK |
| Work (detail) | 88+ height | opens full panel |

### Behavior

| Action | Result |
|--------|--------|
| Tap active chip | Expand **detail sheet** (name, full timer, Instant / Ad if available) |
| Tap pending icon | Tooltip / toast: "Queued · #2" (optional reorder later — out of scope) |
| Tap **Work** | Existing full WORK QUEUE modal (sell-time +slot, long lists) |
| Job completes | Chip flash → next pending slides into active |
| All channels idle | Dock animates out |

**HTML preview:** open `wireframe_A_production_dock.html` in a browser.

---

## 2. Layout B — Compact cluster by Work button

Less WC3, more "badge on the Work button."

```
                                    ┌──────────────┐
                                    │ Work  1:05   │  timer of longest job
                                    │ ●●○  B T R   │  dots = busy channels
                                    └──────────────┘
  [Build] [Talk] [Bag] [Work▲] [Quests]

Tap Work → bottom sheet (not full-screen log):
┌──────────────────────────────────┐
│ TRAINING                         │
│ [chip + ring]  [F][F][A]         │
│ BUILDERS …                       │
└──────────────────────────────────┘
```

**Pros:** minimal chrome. **Cons:** weaker "I see production like WC3" glance.

---

## 3. Layout C — Hybrid (dock + modal)

- **Dock** = Layout A (glance).
- **Modal** = today's WORK QUEUE restyled to same chip language (no text dump).
- Sell-time lives **only** on modal or expanded chip sheet.

Use if owner wants both always-on and a "management" screen.

---

## 4. Chip anatomy (active job)

```
        ┌──────────────┐
        │   ┌──────┐   │
        │  /  62%   \  │   progress RING (not color alone)
        │ │  ICON   │  │   troop / structure / research glyph
        │  \        /  │
        │   └──────┘   │
        │   1m 05s     │   ASCII time
        │   Footman    │   optional 1-line name (ellipsize)
        └──────────────┘
         ↑ tap → detail sheet:
           Footman x1
           Training · 1m 05s left
           [ Instant 40 ] [ Ad skip ]
```

Pending strip:

```
  [F] [F] [A] [+2]
   ^    queued icons left→right = FIFO
```

Empty free slot:

```
  ┌ ─ ─ ─ ─ ┐
  │    +    │   dashed border = free capacity
  │  free   │
  └ ─ ─ ─ ─ ┘
```

---

## 5. State matrix (Claude must mock each)

| ID | State | Dock shows |
|----|--------|------------|
| S0 | All idle | Hidden |
| S1 | 1× Train Footman | Training row only |
| S2 | Train + 3 pending | Chip + strip `+N` |
| S3 | 2 builders + 1 train | Two channel rows |
| S4 | All three busy | Three rows; scroll dock body if needed (WO-795) |
| S5 | Chip expanded | Instant / Ad visible |
| S6 | Full Work modal open | Dock may dim or stay (recommend stay under scrim rules — one modal) |

---

## 6. Channel visual mapping

| Channel | Header | Icon source |
|---------|--------|-------------|
| Builders | BUILDERS n/m | Structure / wall / tower catalog or kit glyph |
| Training | TRAINING | Troop catalog icon / role glyph |
| Research | RESEARCH | Perk / magic / troop-upgrade glyph |

Never show enum strings (`TrainTroop`, `BarracksUpgrade`). Use `FormatJobTarget`-class labels.

---

## 7. What NOT to draw

- Green vs red only for progress (add ring fill + time + marker)
- Unicode pie characters / stars that tofu
- Landscape WC3 command card full width
- Fourth "misc" queue channel
- Job list as multi-line prose (defeats the WO)

---

## 8. CLI implementation notes (after sign-off)

1. Prefer new **presentation host** (e.g. `ProductionDockHud`) observing `BuildTimerService` — keep `ObsidianQueueHud` as detail modal or restyle in place.
2. Open seam stays `ObsidianQueueGate` for modal; dock can be always-on without gate.
3. HUD assembly must not reference Village: if dock lives in HUD, expose a Core model or keep dock in Village DDOL (like current `ObsidianQueueHud`) — **Village DDOL is the existing precedent; keep it.**
4. Screenshot-verify S1 + S3 + S0 before APK.

---

## 9. Owner decision checklist

- [ ] Primary layout: **A** / B / C  
- [ ] Idle: **hide dock** / show empty free slots  
- [ ] Progress: **ring** / bar  
- [ ] Pending max visible before +N: **4** / 5 / 6  
- [ ] Sell-time: on chip expand / only in Work modal  

**CLI product lean (challenge if wrong):** A + hide when idle + ring + 4 + chip expand for Instant/Ad, Work modal for +slot & long queue.
