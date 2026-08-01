# WO-798 — WC3-style work queue VISUAL (build on the live chip + 5-deep rows)

**Status:** DESIGN INPUT — **superseded for implementation by WO-817** (full CoC+WC3 queue visual system)  
**Minted:** 2026-07-30 · **Rewritten:** 2026-07-30 · **Programmed under 817:** 2026-08-01  
**Master implement WO:** `WorkOrders/WORK_ORDER_817_coc_wc3_queue_visual_system.md`  
**Lane:** UI presentation only (HudKit queue chip + optional modal restyle).  
**Claude:** design pack feeds **817 Phase 0**; no `.cs`.  
**Anchor pack:** `docs/UI/WO-798_wc3_queue/` (`CODE_AS_IS.md` first, then wireframes).  
**Related:** WO-773 · WO-778 · WO-799 · WO-816 (Phase 2 bars under 817)

---

## 0. Binding premise — BUILD ON SHIPPED CODE

This is **not** a blank-slate production dock. Owner already asked for WC3 “5 deep Queued”; CLI shipped a **v1 glance** on the right-column Builders chip. This WO **upgrades that surface** to true WC3 *feel* (icons + progress rings + clearer pending strip), reusing the same seams.

| Layer | Already live (do not replace) | This WO upgrades |
|-------|-------------------------------|------------------|
| Engine | `ObsidianQueueEngine` multi-channel FIFO | **Frozen** |
| Service | `BuildTimerService` + `PublishStatus()` | Extend snapshot if design needs icons / multi-channel entries |
| Core seam | `ObsidianQueueGate.Status` / `QueueEntry` / `RequestToggle` | Additive fields on `QueueEntry` OK; keep poll-by-`Version` |
| HUD glance | `HudKitController.BuildQueueStatusChip` + `FormatQueueRows` (5 text lines) | **Primary canvas** — restyle in place |
| HUD area | `HudArea.QueueStatus` `(0.78, 0.53)–(0.995, 0.865)` | Keep unless owner explicitly moves layout |
| Full panel | `ObsidianQueueHud` (3-channel text modal + Instant/Ad/+slot) | Secondary — restyle to match after glance ships |

**Law:** presentation never touches objects; HUD never references Village. All new glance data continues through `ObsidianQueueGate.PublishStatus`.

Full code map: **`docs/UI/WO-798_wc3_queue/CODE_AS_IS.md`**.

---

## 1. Roles

| Role | Does | Does NOT |
|------|------|----------|
| **Claude (UI)** | Before/after mockups of the **right-column chip**, icon plan, multi-channel decision, image pairs, CLI checklist | Write `.cs`, move the engine, invent a second queue host |
| **CLI** | Implement signed design on HudKit + PublishStatus (+ modal if in scope) | Greenfield bottom dock without owner pick |
| **Owner** | Sign image pairs; pick Q1–Q7 defaults | — |

---

## 2. Why (product)

### What works today
- Glance is **always on** in town (right column) — good WC3 habit.
- **5-deep text rows**: `"> Barracks  9m 30s"` / `"- Arcane Spire"` / `"+N more"` when Builder line non-empty.
- Summary button: `Builders n/m` + soonest timer + optional `Training k`.
- Tap chip → full WORK QUEUE modal (sell-time lives there).
- Engine + offline + Train enqueue (WO-778) are correct.

### What still fails the WC3 bar
- Glance is a **wall of text**, not **portrait + pie + icon strip**.
- `Status.Entries` = **Builder only** — Training/Research barely visible (Training is a chip footnote).
- No progress **geometry** (ring/bar) — only remaining seconds as digits.
- No job **icons** (`PrettyJobLabel` is string-only).
- Modal is still a text log (acceptable v1; should match glance language in phase 2).

**Success:** “I see what my town is making the way I saw units training in WC3 — icons and progress first, without reading a paragraph.”

---

## 3. Code baseline (implementers edit THESE)

### 3.1 Read-only for Claude / edit targets for CLI

| File | Role in v1 glance |
|------|-------------------|
| `Assets/_Modules/Core/UI/ObsidianQueueGate.cs` | `WorkQueueStatus`, `QueueEntry`, `PublishStatus`, `RequestToggle` |
| `Assets/_Modules/Village/Buildings/BuildTimerService.cs` | `PublishStatus()` (~631+), `PrettyJobLabel` |
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | `BuildQueueStatusChip`, `FormatQueueChip`, `FormatQueueRows`, Update poll |
| `Assets/_Modules/HUD/Kit/HudAreasHost.cs` | `QueueStatus` anchors (taller band for 5-deep already) |
| `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs` | Full modal (phase 2 visual parity) |

### 3.2 Live data contract (extend, don't fork)

```csharp
// Core — HUD reads ONLY this
ObsidianQueueGate.Status   // WorkQueueStatus { Available, Builder*/Train*/Research*, SoonestRemainingSec, Entries[], Version }
ObsidianQueueGate.RequestToggle()

// Today QueueEntry:
//   Label (string), RemainingSec (int, -1 if queued), Queued (bool)
// Entries filled from Builder channel only (active then pending, cap 7; chip shows 5)
```

**Likely additive (CLI after design):** on `QueueEntry` (or parallel array):

- `string StructureId` or `IconKey` (so HUD can resolve art without Village)
- `float Progress01` (0..1 for active; 0 for queued) — computed in Village at publish
- `ChannelId` / channel tag if Entries become multi-channel
- Keep `Label` + `RemainingSec` + `Queued` for text fallback + colorblind digits

Publisher remains `BuildTimerService.PublishStatus` on `QueueChanged` + 1s tick. HUD keeps **poll-by-Version** (HudBuildingFocus pattern).

### 3.3 Current paint (replace body of FormatQueueRows, not the host)

- `FormatQueueChip` → summary on button  
- `FormatQueueRows` → multi-line TMP under plate (`_queueRowsLabel` / `_queueRowsPlate`)  
- Plate `raycastTarget = false` today — if chips become tappable for Instant/Ad expand, design must say which layer eats taps without blocking the summary button.

---

## 4. Design target — evolve the RIGHT-COLUMN chip (primary)

Greenfield “bottom production dock” is **optional alternate only** if owner rejects the right column. Default = **stay in `QueueStatus`**.

### 4.1 Before → after (same screen real estate)

```
BEFORE (shipped)                         AFTER (this WO)
┌──────────────────┐                     ┌──────────────────┐
│ Builders 2/2     │  summary button     │ Builders 2/2     │  same button
│ 0:42 | Training 1│                     │ 0:42 | T1 R0     │  optional richer line
├──────────────────┤                     ├──────────────────┤
│ > Wall  0:42     │  text plate         │ [ring|W][ring|T] │  active chips
│ > Tower 3:10     │                     │  · [G][R][+2]    │  pending icon strip
│ - Gate           │                     │ TRAINING (opt)   │  if multi-channel
│ - Barracks Upg   │                     │ [ring|F] ·[F][A] │
│ - Lumberyard     │                     │                  │
│ +2 more          │                     │ +N if needed     │
└──────────────────┘                     └──────────────────┘
  HudArea.QueueStatus (0.78–0.995 x, 0.53–0.865 y)  — KEEP
```

### 4.2 WC3 mapping (onto our channels)

| WC3 | Our game (on this chip) |
|-----|-------------------------|
| Building portrait + pie | Active job **icon + progress ring** + time |
| Units waiting in line | Pending **horizontal icons** FIFO |
| Multiple buildings | Multiple active chips side-by-side (Builder slots) and/or Training/Research sub-rows |
| Command-card full panel | Existing WORK QUEUE modal (sell-time, +slot, deep list) |

### 4.3 Visual rules (binding)

1. Progress = **ring fill + remaining time digits** (not color alone).  
2. ASCII-safe labels only (no tofu arrows/stars).  
3. Max **~5** glance slots (matches owner “5 deep”); overflow `+N`.  
4. Idle: rows plate **hidden**; summary button may stay (`Builders` / idle) — match today’s behavior unless owner wants full hide.  
5. Tap summary button = still `RequestToggle` (modal).  
6. Optional: tap active chip → detail sheet with Instant/Ad (uses existing `BuildTimerService` APIs; may need structureId on entry).  
7. Obsidian black + gold kit; MinTouchPx on anything newly tappable.

---

## 5. Claude deliverables (design pack)

Path: **`docs/UI/WO-798_wc3_queue/`** (starter already there — extend, don’t scatter).

### Required

1. **Before/after image pair** on the **right-column QueueStatus** band (1080×1920):
   - Before = current text 5-deep (screenshot or wireframe of CODE_AS_IS)
   - After = icon + ring + pending strip (primary recommendation)
2. **Chip anatomy** (one active + pending strip + +N) with ref-px sizes.
3. **Multi-channel decision board** (recommend one):
   - **M1:** Builder-only glance (shipped data) + Training footnote on button only  
   - **M2:** Builder 5-deep icons + Training/Research as compact second/third mini-rows when busy  
   - **M3:** Unified entries across channels (needs `Channel` on `QueueEntry`)  
   CLI lean: **M2** (best feel / moderate contract change).
4. **Icon plan:** structureId → catalog/RpgUi glyph; train → troop icon; missing art → role letter glyph (never blank).
5. **States:** S0 idle · S1 one builder · S2 builder deep queue · S3 builder + training · S5 chip expand Instant/Ad (if in scope).
6. **Copy deck** (ASCII): channel headers, empty, `+N more`, tooltips. Never “Obsidian”.
7. **CLI implementation checklist** mapped to §3 files (bullet = one code change).
8. **Do-not list** confirmed against §8.

### Optional (phase 2 mock only)
- Restyle `ObsidianQueueHud` modal to same chip language.  
- Barracks train strip visual parity.  
- Bottom dock alternate (only if owner rejects right column).

---

## 6. Owner decisions (Claude recommends; owner ticks)

| # | Question | CLI lean (challenge if wrong) |
|---|----------|-------------------------------|
| Q1 | Keep right-column QueueStatus vs move to bottom dock? | **Keep right column** (already in HudAreas + posture) |
| Q2 | Multi-channel glance: M1 / M2 / M3? | **M2** |
| Q3 | Progress: ring vs bar? | **Ring** + digits |
| Q4 | Pending max icons before +N? | **4** on chip width |
| Q5 | Instant/Ad on chip expand or modal only? | Modal only for v1 polish; expand = phase 1.5 if easy |
| Q6 | Idle: hide plate only (today) vs hide whole chip? | **Plate only** |
| Q7 | Cancel row UI (WO-799)? | After this visual sign-off; engine WO-799 separate |

---

## 7. Acceptance

### Claude done
- [ ] Pack updated with before/after pairs grounded in **live chip**, not greenfield dock  
- [ ] Owner signed primary after mock  
- [ ] M1/M2/M3 chosen  
- [ ] CLI checklist maps to `ObsidianQueueGate` / `PublishStatus` / `HudKitController` / optional modal  

### CLI done (after sign-off)
- [ ] Glance still fed only by `ObsidianQueueGate.Status` (no Village ref from HUD)  
- [ ] 5-deep (or signed N) icon+ring UI in `QueueStatus`; text fallback if icon missing  
- [ ] Idle plate hide preserved  
- [ ] Tap summary still opens modal  
- [ ] Tofu / colorblind / MinTouchPx oracles green  
- [ ] Headless screenshot: busy Builder line + idle  
- [ ] Extend `ObsidianQueueRegression` if `QueueEntry` shape or publisher contract changes  
- [ ] Felt: deep build queue reads as WC3 production line, not a chat log  

---

## 8. Do NOT

- Do **not** rewrite `ObsidianQueueEngine` / save schema / Train enqueue rules.  
- Do **not** invent a second always-on queue host beside `queueStatusChip` without owner OK.  
- Do **not** drop `PublishStatus` / Version polling for a Village→HUD reference.  
- Do **not** UXML, non-ASCII TMP, color-only state, “Obsidian” in player copy.  
- Do **not** hand-edit `.unity` scenes.  
- Do **not** block on WO-799 cancel UI (engine can land; row cancel chrome waits on visual).  
- Do **not** smuggle full MVVM migration into this pass.

---

## 9. Sequencing

| WO | Relation |
|----|----------|
| **773** | Engine frozen |
| **778** | Chip + modal reachability baseline — **this WO restyles** |
| **799** | Cancel/refund engine — panel cancel chrome after 798 sign-off |
| **779 / 795** | Spacing / no-stack — new chips must obey |
| **774** | Raid — out of scope |

**Order:** Claude design (read-only) → owner signs → CLI implements glance → optional modal parity → then cancel row UI on the new chips.

---

## 10. Claude session boot (paste)

```text
1. START_HERE.md + docs/ARCHITECTURE_PRINCIPLES.md (presentation law)
2. docs/UI/WO-798_wc3_queue/CODE_AS_IS.md   ← live stack (READ FIRST)
3. WorkOrders/WORK_ORDER_798_wc3_style_queue_visual.md  (this file)
4. docs/UI/WO-798_wc3_queue/WIREFRAMES.md + wireframe HTML (target feel)
5. Code skim (read-only): ObsidianQueueGate.cs, BuildTimerService.PublishStatus,
   HudKitController BuildQueueStatusChip / FormatQueueRows

Deliver: before/after mockups of the RIGHT-COLUMN Builders chip upgrading
text 5-deep rows → icon + ring + pending strip. No .cs. Do not invent a
second queue system.
```

---

## 11. SME one-liner

**Evolve the shipped WC3 5-deep text queue under the Builders chip into icon+ring production glance, via `ObsidianQueueGate.Status`, without touching the multi-channel engine.**
