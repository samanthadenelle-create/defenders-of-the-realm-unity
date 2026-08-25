# WORK ORDER 826 — Realm Map parchment UI (immersive overworld screen)

**Status:** DONE — SHIPPED 2026-08-01 (eb5d0710); REALM_MAP oracle green, capture verified. RESULT: WORK_ORDER_826_realm_map_parchment_ui.RESULT.md  
**Minted:** 2026-08-01  
**Program:** WO-825  
**Silo:** HUD / UI (code-built uGUI; dual-copy data already exists)  
**Depends on:** none for shell; travel actions stub until **827**  
**Roles:** Claude = optional wireframe/image pair · CLI = implement  

---

## Why

`realm-map.json` already defines Elarion + five fog-shrouded regions with `mapPoint`, gates, biomes, and descriptions — but the player has **no screen** that presents it. Immersion dies without a “map of my world.”

## Goal

Ship a **full-screen Realm Map panel** that:

1. Loads **only** from dual-copy `realm-map.json` (Resources + StreamingAssets parity).  
2. Draws **home base Elarion** + region **nodes** at `mapPoint` (percent of map rect).  
3. Shows derived state: **locked** (fog) / **discovered** / **cleared** / (optional) **threatened**.  
4. Uses ElarionUi / Obsidian kit language (parchment plate + gold gilt) — **not** CoC green chrome clone.  
5. Is openable from hub without DevPanel (entry wire: HUD Map control and/or Wayshrine — see §Entry).  
6. Presentation layer: **HUD or Village UI** that reads Core snapshot / JSON; **never** Village gameplay objects from HUD assembly.

Until 827, taps on nodes show **detail only** (title, description, gate reason, state) — Travel CTA can be present but disabled with “Coming with discovery” or wire to 827 stub.

---

## Scope

### 1. Data loader (Core or Village pure)

- Deserialize `realm-map.json` → typed records (`RealmMapData`, `RealmRegionDef`, `HomeBaseDef`, gate union).  
- Ignore `_comment` / `_sources` / `_schemaNotes`.  
- Dual-copy oracle in DataRegression: byte-parity or field parity of `regions[].id` + `mapPoint` + gates.  
- **Do not** invent a second region list in C# constants.

### 2. Progress read model (minimal for UI)

Until full **827** ledger:

- Derive state for UI:
  - Home: always visible / never locked.  
  - Region: if no save fields yet → treat as **locked** except optionally first region when gate trivial (document).  
- Prefer reading future `GameState.RegionProgress` if 827 lands first; else temporary defaults + FlowTrace.Once that progress is stubbed.

### 3. Panel UI (code-built)

Suggested files (adjust to house style):

- `Assets/_Modules/Village/Hero/RealmMapPanel.cs` (or HUD kit panel if Core-only snapshot is enough)  
- `Assets/_Modules/Village/Hero/RealmMapVM.cs` — all state projection  
- Open via existing PanelManager / Elarion modal pattern (WO-795: scroll wells, no stack fight)

**Layout (landscape):**

```
┌─────────────────────────────────────────────────────────┐
│  REALM MAP                              [X close]       │
│  ┌───────────────────────────────────────────────────┐  │
│  │           fog / parchment backdrop                  │  │
│  │     [node]     [node]                               │  │
│  │           [ELARION ★ you]                           │  │
│  │     [node]              [node]                      │  │
│  │                    [node]                           │  │
│  └───────────────────────────────────────────────────┘  │
│  Detail: Title · State · Gate · 2-line description      │
│  [ Travel ]  (enabled only when 827 + discovered)       │
└─────────────────────────────────────────────────────────┘
```

Portrait: map top ~55%, detail bottom. Nodes ≥ 48dp touch (112px law where CTAs).

**Node visual language:**

| State | Look |
|-------|------|
| locked | Dark fog disc; lock or ? ; no travel |
| discovered | Clear biome-tint ring; title on select |
| cleared | Check / gilt crest; still selectable |
| threatened | Pulse red/amber ring (if event false, unused) |
| home | Distinct crest; “Elarion” always |

Connectors: optional lines along `adjacency` (subtle gold). Skip if noisy on first ship.

### 4. Entry points

1. **HUD:** add `mapButton` (or reuse free slot) in `hud-areas.json` calm/town — dual-copy — label **Map**. Opens panel.  
2. **Wayshrine (optional same PR):** if a structure/NPC exists, interact → same Open. If no Wayshrine art, HUD-only is OK; document.  
3. **Dev:** DevPanel “Open Realm Map” for headless/UICapture.

### 5. Accessibility / laws

- Colorblind: state text in detail pane, not color-only.  
- ASCII player strings; **Elarion** not Avalon.  
- FlowTrace system tag `RealmMap` on open/select/close.  
- WO-795: detail description scrolls if long; no truncation mid-word.

### 6. Capture / tests

- UICapture or editor shot: map open, one locked + home selected.  
- EditMode: JSON loads; five region ids present; home id maps to Elarion title.  

---

## Acceptance

- [ ] COMPILE_GATE_OK + REGRESSION_OK (incl. dual-copy realm-map check)  
- [ ] Hub → Map opens parchment UI with Elarion + all catalog regions laid out by mapPoint  
- [ ] Locked vs non-locked visually distinct + gate text in detail  
- [ ] Close restores hub control; no softlock  
- [ ] No Avalon in player-facing strings  
- [ ] Travel may be disabled until 827 — but UI slot reserved  

## Do NOT

- Hand-edit `.unity` scenes  
- Live camera render-to-texture for the parchment  
- Rewrite `ZoneManager` here (827)  
- Full terrain streaming (WO-34)  
- UXML  
- Mount/bash writes to `.cs`  

## Paste for CLI

```text
Implement WORK_ORDER_826_realm_map_parchment_ui.md.
Load realm-map.json, code-built parchment Realm Map panel, HUD Map entry,
fog states + detail pane. Travel CTA stub OK until 827. Elarion not Avalon.
Gate + dual-copy oracle. Brace-check every .cs.
```
