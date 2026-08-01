# WO-817 — Queue system visual overhaul: **CoC channels + WC3 production glance**

**Status:** READY TO IMPLEMENT (master program — phases below) · ⚠ RE-SCOPE 2026-08-01: the bar Work/Queues button was RETIRED (eb5d0710); the Keep-table row naming "RequestToggle / Work button | Keep" is stale — the right-column Builders chip is the sole Queues entry; do NOT re-add a bar button (ObsidianQueueRegression 7c fails it).  
**Minted:** 2026-08-01  
**Lane:** Queue presentation only (engine frozen)  
**Roles:** Claude = visual pack / image pairs for phases; CLI = implement after sign-off per phase  
**Program hub (adjacent):** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md`  
**Code map:** `docs/UI/WO-798_wc3_queue/CODE_AS_IS.md`  

### Supersedes / folds (do not implement as competing designs)
| Prior WO | Role under 817 |
|----------|----------------|
| **798** | Design notes absorbed → **Phase 0 mock + Phase 2–3 glance** |
| **801** | Implement icons/multichannel → **Phase 3** |
| **816** | Timer bars → **Phase 2** (ship early) |
| **799** | Cancel/refund **engine** separate; **cancel row chrome** → Phase 5 after visual rows exist |
| **773 / 778** | Engine + reachability **done** — do not reopen |

---

## 1. Product north star (owner intent)

> The **entire idea of queues** should look and feel like **Clash of Clans** and **Warcraft 3** — not a text log, not an idle-game feed.

### Steal from **Clash of Clans**
| CoC | Our game |
|-----|----------|
| Builder huts / parallel workers | **Builder channel** slots |
| Barracks train queue | **Train channel** |
| Lab research queue | **Research channel** |
| Job portrait + **timer bar** + remaining time | Active job row |
| Pending units as small icons in a line | Pending FIFO strip |
| Instant / gem finish on active job | Instant crystals / Ad skip (APIs exist) |
| Always know “something is cooking” | Persistent HUD glance when busy |

### Steal from **Warcraft 3**
| WC3 | Our game |
|-----|----------|
| Building production portrait + pie/progress | Active job **icon + progress** |
| Queue of unit icons under production | Pending **icon strip** left→right |
| Glanceable without a novel | Right-column / dock production strip |
| Cancel training | WO-799 engine + Phase 5 UI |

### Keep (our model — correct)
- Multi-channel **Builders / Training / Research** (not one global FIFO)  
- Offline-fair wall clock  
- Player copy: never “Obsidian”  
- Presentation never references Village from HUD (Core snapshot only)

### Kill (current feel)
- Text-only `"> Barracks  9m 30s"` as the primary language  
- Full list rebuild that feels like a debug console  
- Train/Research invisible on the glance  
- Empty giant modal with prose timers  

---

## 2. Visual system (binding language)

### 2.1 Active job unit (everywhere)
```
┌──────┬─────────────────────────────┐
│ ICON │  Job name (ellipsis)         │
│      │  [████████░░░░]  1m 05s      │  gold Stat/Loading bar + digits
└──────┴─────────────────────────────┘
```
- **Icon:** structure / troop / research glyph (fallback 1–2 letter)  
- **Bar:** fill 0..1 = progress; **digits** always (colorblind)  
- **Queued:** empty bar or no bar + small icon in strip; **no fake countdown**  

### 2.2 Pending strip (WC3 / CoC)
```
Active chip(s)  ·  [i][i][i][+N]
```
- FIFO left→right  
- Max ~4–5 visible + `+N`  

### 2.3 Channel block (CoC parallel workers)
```
BUILDERS  2/2 busy
  [active rows...]
  pending strip...
TRAINING  1/1
  ...
RESEARCH  idle / busy
```
- Free slots: dashed empty chip or `free` silhouette (capacity teaching)

### 2.4 Surfaces (same language, different density)

| Surface | Density | Behavior |
|---------|---------|----------|
| **HUD glance** (`QueueStatus`) | Compact | Always when any job; hide plate when fully idle; tap → full panel |
| **WORK QUEUE modal** | Full | All 3 channels, Instant/Ad/+slot, cancel when 799 ready |
| **Barracks train strip** | Channel slice | Training only, same row component |
| **World scaffold** (stretch) | Billboard | Thin bar + M:SS on building under construction |

### 2.5 Chrome
- Obsidian black + gold gilt (kit) — **not** WC3 blue UI clone, **not** CoC flat green UI clone  
- Feel = **those games’ production grammar** in **our** art  
- ASCII digits; no tofu glyphs  

---

## 3. Code baseline (engine frozen)

```
BuildTimerService ──PublishStatus──► ObsidianQueueGate.Status
                                          ▲
HudKit / future glance ──── poll Version ─┘
ObsidianQueueHud ◄── ActiveJobsOf / PendingJobsOf (Village)
```

| Keep | Change |
|------|--------|
| `ObsidianQueueEngine` | No |
| Channels Builder/Train/Research | No |
| `RequestToggle` / Work button | Keep |
| `QueueEntry` text-only | **Extend** Progress01, IconKey/StructureId, channel |
| Text FormatQueueRows / FormatJobLine | **Replace** with shared visual rows |
| Instant/Ad/BuySlot APIs | Wire into visual rows only |

---

## 4. Implementation phases (one WO, shipable slices)

### Phase 0 — Visual lock (Claude, 1–2 days)
- Image pairs: HUD glance busy + WORK QUEUE modal + train strip  
- Approve **bar primary** (pie optional later)  
- Approve multi-channel glance **M2** (Builder full + Train/Research when busy) unless owner says M1  

**Exit:** owner signs mock → Phase 1+ unblocked.

### Phase 1 — Snapshot completeness (CLI)
- `QueueEntry`: `Progress01`, `DurationSec`, `StructureId` or `IconKey`, `Channel` (or separate train/research arrays)  
- `PublishStatus`: all channels’ busy/queued + **entries for glance** (not Builder-only if M2)  
- Pure `QueueTimerFormat.FormatRemaining`  
- Oracles: progress 0 queued / increases when active; version bumps  

### Phase 2 — Timer bars (was WO-816) ★ ship first vertical slice
- Shared `QueueTimerRow` (name + `BuildObsidianBar` Stat/Loading + time)  
- Wire **ObsidianQueueHud** + train strip + HUD active rows  
- Smooth fill between publishes  
- Acceptance: bars move; digits accurate; offline snap correct  

### Phase 3 — Icons + WC3 strip (was WO-798/801)
- Icon left of name (catalog / RpgUi / letter fallback)  
- Pending horizontal icon strip  
- Multi-channel glance per signed M1/M2/M3  
- Compose with Phase 2 bars (icon + bar, not text log)  

### Phase 4 — Modal + glance cohesion
- Modal channel blocks match glance language  
- Idle free-slot silhouettes  
- Scroll / no clip (WO-795)  
- Summary chip: optional soonest mini-bar under `Builders n/m`  

### Phase 5 — Interaction chrome
- Cancel on row when **WO-799** engine ready  
- Instant / Ad placement consistent with CoC “finish now”  
- Optional chip expand detail sheet  

### Phase 6 — World (stretch)
- `UnderConstructionVisual` thin bar under M:SS  

---

## 5. Acceptance (program done when)

- [ ] Player can answer “what’s cooking?” from HUD without opening a wall of text  
- [ ] Active jobs = **icon (or letter) + bar + time** on modal and glance  
- [ ] Pending = **icon strip**, not only `"- Label"`  
- [ ] Builders, Training, Research all readable when busy (if M2+)  
- [ ] Feels like **CoC workers + WC3 production**, in Obsidian art  
- [ ] Engine/offline/Instant math unchanged  
- [ ] Colorblind: fill + digits; ASCII  
- [ ] Screenshots: busy multi-channel + idle  
- [ ] Prior WOs 798/801/816 marked SUPERSEDED-by-817 or closed into phases  

---

## 6. Do NOT

- Rewrite `ObsidianQueueEngine` / save schema for jobs  
- Single global FIFO (un-CoC)  
- UXML  
- HP-red bars for builds  
- “Obsidian” in player copy  
- Block Phase 2 on full icon art (letter fallback OK)  
- Invent a fourth channel without a product WO  

---

## 7. Files (core)

| Layer | Paths |
|-------|--------|
| Snapshot | `ObsidianQueueGate.cs`, `BuildTimerService.PublishStatus` |
| Modal | `ObsidianQueueHud.cs` |
| HUD | `HudKitController.cs`, `HudAreasHost.cs` (anchors if needed) |
| Train | `TroopTrainingPanel.cs` |
| Kit | `ElarionUiKit` / shared `QueueTimerRow` |
| World stretch | `UnderConstructionVisual.cs` |
| Design pack | `docs/UI/WO-798_wc3_queue/` (extend as `WO-817` mocks) |

---

## 8. Dispatch for agents

```text
WO-817 is the master queue visual program.
Phase 2 (bars) can ship before full icons.
Do not implement separate competing UIs for 798/801/816 — fold into 817 phases.
Engine frozen. Presentation only.
```

## 9. Owner one-liner

**CoC: parallel workers + train/lab queues. WC3: production icons and progress. Us: three channels, one visual language, Obsidian chrome.**
