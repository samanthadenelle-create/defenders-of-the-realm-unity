# WORK ORDER 827 — Realm Map discovery, zone identity, and travel

**Status: READY TO IMPLEMENT — the WO-826 gate is LIFTED (826 shell shipped eb5d0710). ⚠ 2026-08-23 board reconcile: `RegionProgress` exists in GameState/SaveSchema/SaveMigrator and `RealmMapVM` is wired at HEAD — this ticket may be partly shipped; a CLI acceptance pass is owed before anyone re-implements it.**  
**Minted:** 2026-08-01  
**Program:** WO-825  
**Silo:** Core state + Village world + map panel wiring  
**Depends on:** **826** panel shell (or land loader+state first, then wire UI)  
**Roles:** CLI implement; owner rules R2/R3 from WO-825  

---

## Why

1. **Two region taxonomies** fight immersion:  
   - `realm-map.json`: thornwood, mirewood, hollowfrost, emberwastes, starfall-reach  
   - `ZoneManager` / `RegionId`: Village, Goldfields, Stoneback, Mirewood, Ashwood  
   Player walks “Goldfields” while the map says “Thornwood” → world feels fake.  
2. No **RegionProgress** ledger in the live save path for the parchment map (data doc only).  
3. No **travel from map** — only world seams.

## Goal

1. **One identity story** for the player (aliases under the hood OK).  
2. Persist **discovered / cleared** per realm region id.  
3. Derive **locked / discovered / cleared** from gates + ledger.  
4. Enable **Travel** on discovered (or cleared) nodes with a safe, feelable route.  
5. Feed 826 detail + node state from this authority (not hardcoded).

---

## Scope

### 1. Identity alignment (binding — pick path A unless owner R3 overrides)

**Path A (default): Narrative catalog wins for player-facing names**

- Keep `realm-map.json` ids as parchment keys.  
- Add mapping table (JSON or static):

| Live `RegionId` / overworld | Realm map id | Player title |
|-----------------------------|--------------|--------------|
| Village | home / avalon→**display Elarion** | Elarion |
| (east) Goldfields | thornwood or new | from realm-map title |
| … | … | … |

- Document every live zone → realm node. Unmapped zones still play but FlowTrace.Warn once.  
- Coach / zone toast / minimap labels use **realm title** when mapped.

**Path B (only if owner R3):** rewrite realm-map.json to Goldfields/Stoneback/… and retire Thornwood names — bigger narrative cost.

### 2. Save: RegionProgress

- Persist on `GameState` (additive field, no schema bump if project pattern allows default-on-read — mirror troopLevels / gearLevels):

```text
discovered: { "thornwood": true, ... }
cleared:    { "thornwood": true, ... }
```

- Hydrate/dehydrate in GameStateService; New Game empty.  
- `MarkRegionDiscovered(id)` / `MarkRegionCleared(id)` + Save.  
- Derive state:

```text
if cleared[id] -> Cleared
else if discovered[id] OR gateMet(id) -> Discovered (and auto-mark discovered when gateMet)
else -> Locked
```

- Gate evaluation: implement `bestWave` and `regionCleared` from JSON (read BestWave / cleared set from real game stats).

### 3. Discovery triggers (feel)

Mark discovered when **any** of:

- Gate becomes met (evaluate on hub load + after wave clear / region clear).  
- Player **enters** corresponding overworld zone (ZoneManager) for the first time.  
- Map Travel completes to that node (827 travel).  
- Optional: Rumor Board accept that tags a region (if cheap).

FlowTrace.Step on first discover.

### 4. Travel from map (owner R2 default: marker + walk)

**V1 recommended (lower softlock risk):**

1. Player taps Travel on **Discovered** node.  
2. Close map; set **compass objective** + world ping to the region gate / seam / zone anchor.  
3. Toast: “The path to {Title} is marked.”  
4. Existing seam “Travel to …” still works when they arrive.

**V1.1 optional:** Wayshrine instant teleport to a **named SceneLink** or overworld anchor if link id exists in data (`repo.travelLinkId` additive field — only if link is real).

**Never:** teleport into enemy raid scene without army gate (820). Raid remains Raids button / 774.

### 5. Wire RealmMapPanel / VM

- All node states from progress + gate eval.  
- Travel button: interactable iff Discovered or Cleared; Locked shows gate fail reason.  
- After clear of main objective (if/when region defense exists): MarkRegionCleared + reward once (`clearReward` from JSON via ResourceLedger).

### 6. Region main objective (thin if missing)

If no live “region wave defense” system:

- Do **not** invent a full wave mode in this WO.  
- Cleared can be set by: owner Dev command, first outpost clear in zone, or a single stub “Scout complete” interactable — **document which**.  
- Prefer hook to existing overworld content (outpost clear in that zone) over greenfield.

### 7. Tests

- Gate: bestWave 3 → thornwood discoverable when BestWave≥3.  
- regionCleared chain: mirewood locked until thornwood cleared.  
- Save round-trip discovered/cleared.  
- Travel on locked refuses; on discovered sets objective (or teleports if V1.1).

---

## Acceptance

- [ ] Player-facing zone/map names aligned (no Avalon; no silent Goldfields vs Thornwood clash without alias)  
- [ ] Progress persists across app kill  
- [ ] 826 nodes reflect real lock/discover/clear  
- [ ] Travel on discovered does something player-visible (marker or teleport) without softlock  
- [ ] COMPILE_GATE_OK + REGRESSION_OK  
- [ ] 820 raid gate untouched for map travel  

## Do NOT

- Rebuild terrain streaming (WO-34)  
- Open raid deploy from map  
- Village↔HUD asmdef violations  
- Burn discovery on toast alone without ledger Save  

## Paste for CLI

```text
Implement WORK_ORDER_827_realm_map_discovery_travel.md after 826 shell.
RegionProgress save, gate eval, ZoneManager↔realm-map identity aliases,
Travel sets compass/marker (default). Wire RealmMapPanel states. No second catalog.
```
