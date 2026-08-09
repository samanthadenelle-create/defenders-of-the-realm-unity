> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: sec.3 tell is DONE - `StructureFactory.cs:776` now calls `CollectorStackView.Attach`, and two regression suites fail if that caller disappears. sec.4 (ambient HUD collector chip) is deliberately DEFERRED per WO-901:65.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 900 — The collector "I am full" tell

**Status:** READY TO IMPLEMENT - partial (reconciled 2026-08-09, per this file's own 08-08 banner - sec.3's tell is delivered (`StructureFactory.cs:776` calls `CollectorStackView.Attach`, pinned by two regression suites); sec.4's ambient HUD collector chip is still outstanding, deliberately deferred per WO-901 line 65)

**Status:** PARTIAL — sec.3 shipped; sec.4 HUD chip deferred (reconciled 2026-08-08, see banner)
**Author:** read-only RCA agent (§13), orchestrated by CLI, 2026-08-04
**Origin:** owner, 2026-08-04 — *"we need to somehow convey to player when capacity is full."*
**Lane (§9):** Presentation / UI. **File-disjoint from WO-859** — 859 touches `ResourceCollector`'s state
and capacity members; 859 only READS `IsFull` / `FilledSteps` / `StepChanged`, which already exist
(`ResourceCollector.cs:72-89`).
**Ships with:** WO-859, same batch-gate, one commit wave (CLAUDE.md §11 pattern).

**Why separate from 859:** it is a presentation lane with a `UI_CAPTURE_OK` gate that 858 does not need,
and its core action is *wiring dead code* — which must not be able to block an economy fix from gating.

---

## 1. Goal

The player can tell, **without opening a modal**, that a collector has stopped earning.

---

## 2. RCA — the tell is fully built and has ZERO callers

`Assets/_Modules/Village/Buildings/Progression/CollectorStackView.cs` (437 lines) already implements the
entire CoC tell:

| Feature | line |
|---|---|
| pooled prop pile / world-space fallback fill bar | — |
| near-full **amber band at 85%** | `:53` |
| redundant `N/20` numeric readout | `:276-289` |
| `"!"` bang, shown only when full | `:337` |
| one-shot `VFXManager.Play(LevelUp_Celebration)` glint | `:363-368` |
| one-time toast *"{Building} is full - collect it, or upgrade it to hold more."* | `:370-377` |

**`CollectorStackView.Attach` has no caller anywhere in the tree.** Grep over all `.cs` returns only the
definition, comments, and two regression allowlist rows. Already recorded at
`WorkOrders/WORK_ORDER_783_sme_findings_fix_wave.md:186` and
`Assets/Editor/Regression/UiObsidianConformanceRegression.cs:168` — **and never fixed.**

`ResourceCollector.StepChanged` (`:82`) fires into no subscriber.

The only live readout is inside the hidden Echoes modal: `EchoWorkforceVM.cs:175-176` composes
`"Pending P   Echo S%   Collectors C%"` from `ResourceCollectorService.MaxFillFraction()` (`:47-55`).

**What the player sees today when a collector fills: nothing.** `Accrue` clamps silently
(`ResourceCollector.cs:169`) and the wallet number simply stops moving.

**This is a WIRING fix, not a UI build.**

---

## 3. Part A — wire the dead view (~2 lines)

Call `CollectorStackView.Attach(col)` at `StructureFactory.cs:744-752`, immediately after
`col.Configure(buildingId)`. `Attach` self-skips origin-parked DDOL fallback hosts (`:100-102`), so it
correctly decorates only real placed collectors.

That delivers, for free: the fill tell, the amber band, the `N/20`, the `"!"`, the glint, the bob and the
toast.

**Two defects to fix while wiring:**
- **Toast spam** — `ShowFullToast` (`:370-377`) fires per collector; three filling in one frame = three
  toasts. Aggregate or throttle.
- **Font** — the view uses legacy `UnityEngine.UI.Text` with `LegacyRuntime.ttf` (`:389-394`), not
  TMP/`ElarionUiKit`. It is **exempt** from the MVVM ratchet (`UiMvvmConformanceRegression.cs:91`:
  *"world-space diegetic stack decorator (injected model) - conformant by exemption"*), so this is legal —
  but legibility in landscape at mobile distance is **unverified**. This is the specific thing
  `UI_CAPTURE_OK` must judge.

⚠ **Fraction-band note (the documented culling law).** The view's text uses a **fixed** world-space canvas
`sizeDelta` (1.6 x 0.9, `:254`) with the count label at full-rect anchors and the `"!"` at a **fixed 60x80
px** rect (`:272`). These are fixed pixel bands, **not** fractions of a parent — the culling failure mode
does not apply. **Do NOT "clean this up" into anchor fractions.**

---

## 4. Part B — the ambient HUD tell

Route through a new Core-side status gate, **exactly mirroring `ObsidianQueueGate`**
(`Assets/_Modules/Core/UI/ObsidianQueueGate.cs:38-73`):

```csharp
struct CollectorStatus { bool Available; int FullCount; int TotalCount; int MaxFillPct; int TotalPending; int Version; }
```
published by a Village-side owner and **polled** by `HudKitController`.

**On `IVillageHud` or through a seam? -> Through the Core gate, NOT `IVillageHud`.**
`IVillageHud` (`Assets/_Modules/Core/HUD/IVillageHud.cs`) is an **imperative push** interface (`SetWave`,
`SetCrystals`, `SetResources`); this is a **polled status snapshot**, which is what the queue chip already
does via a gate. Two live precedents (`ObsidianQueueGate`, `HarvestPanelGate`) and one live consumer
pattern (`HudKitController.BuildQueueStatusChip:678-723` + `FormatQueueChip:752-766`).
**No reflection -> nothing added to the `tools/regression/static_gate.py` allowlist.**

> Honest note: adding an `IVillageHud` member here would **not** let any of the 15 existing allowlist
> entries be deleted — those are unrelated bridges. The "right end state" argument does not apply here.

**Placement:** a compact line appended to the existing **Builders chip band** (`HudArea.QueueStatus`,
`HudAreasHost.cs:114`), or a peer chip in the same right column. Reuse
`ElarionUiKit.BuildObsidianButton`, which already carries the `MinTouchPx` (112) floor
(`HudKitController.cs:677`). If tappable, tapping calls the **existing**
`ResourceCollectorService.CollectAll()` (`:17-33`) — not a new command.

### ⚠ Copy law — the two-"full"s problem
WO-857 is adding bank `current/max` chips. To stop the player seeing two different notions of "full":

- **"Storage" / "Bank" / `current/max` belongs to WO-857** (the wallet).
- **"Collectors" belongs to WO-900.** Copy: `Collectors 2/3 full` + `tap to collect`.
  **Never the word "Storage".**
- **Cross-WO dependency — name it, do not build it:** once WO-857 lands, a full bank means the Collect tap
  cannot bank. The collector tell must then read `Bank full` instead of `tap to collect`. **WO-900 ships
  with the collect wording; WO-857 owns adding the headroom check.** Flagged in both WOs so neither ships
  a lie.

ASCII only. **Text-encoded state, never colour alone** (owner is red/green colourblind). Landscape-only.

---

## 5. Acceptance criteria

1. Placing a collector produces a visible fill tell within one frame of `Configure`; the tell tracks
   `FilledSteps` and is **event-driven off `StepChanged`**, never per-frame polling of the model.
2. At 100% the `"!"`, the bob, the glint and **exactly one** toast fire — three simultaneous fills produce
   **one** aggregated toast.
3. The HUD line reads `Collectors N/M full` and is visible **without opening any modal**.
4. Origin-parked fallback collectors get **no** view (guaranteed by `:100-102` — assert it).
5. The word "Storage" appears nowhere in this WO's copy.
6. **`UI_CAPTURE_OK` — the screenshots are OPENED and READ**, in landscape, at three states: empty, 85%
   near-full, full, plus the HUD chip. Compile-green does not prove a panel looks right
   (memory: `headless-screenshot-verify-ui-before-build`).

---

## 6. Regression

- **Extend `UiObsidianConformanceRegression.cs`** — the note at `:168` (*"Attach() currently has NO
  caller"*) becomes an **assertion that it does**, so the tell can never silently die again.
  **This is the case that would have caught the original defect.**
- **`CollectorIncomeRegression` case 12 `[tell-wired]`** — `StructureFactory`'s `ResourceCollector` case
  references `CollectorStackView.Attach`.

---

## 7. What NOT to touch

- **No new reflection bridge, ever. No new `static_gate.py` allowlist entry.**
- **Do not rebuild `CollectorStackView` — wire it.** Do not convert it to TMP/MVVM in this WO; if the
  capture shows a legibility problem, log that as its own holistic ticket.
- **Do not touch the resource dock chips** (`HudKitController.BuildResourceChips:994-1113`) — WO-857.
- **Do not re-introduce a hand-built world-space bubble on any gameplay object** (the WO-856 `CrystalMine`
  lesson). `CollectorStackView` **is** the separate presentation layer, injected with the model; the
  gameplay object never builds UI.

---

## 8. §15 canon updates

- `docs/MASTER_CATALOG/village-systems.md:130` — lists `CollectorStackView (437)` as if live; it has never
  run. Correct once wired.
- `docs/qa/GAMEPLAY_GAPS_2026-07-26.md:79` — cites `CollectorStackView.cs:367` as if the VFX fires.
