**Status:** BLOCKED — THE CODE IS BUILT, GATED OFF BY A FLAG (reconciled 2026-08-22). R1, R2 and R3 are ALL RESOLVED
in code; acceptance is blocked on `FeatureFlags.MapTab`, which is `defaultOn: false`. *(Was: READY - PARTIAL -
2026-08-21 CLI, gate-green (COMPILE_GATE_OK + REGRESSION_OK 234/234).)*

> ### VERIFIED AT SOURCE 2026-08-22 — the "REMAINING R1/R2/R3" table below is STALE. All three are closed.
> * **R1 (parchment never paints atmosphere) — CLOSED.** `Assets/_Modules/Village/Hero/RealmMapPanel.cs:38`
>   now imports `DeNelle.Core.World` for `RealmPin`/`RealmPinKind`; the Withering band is real
>   (`WitheringOuterPx :167`, `WitheringInnerPx :170`, `WitheringCartouchePx :172`, `BuildWithering` built at
>   `:291` and defined at `:447-461`), and the panel subscribes to `RealmPinBoard.Changed` at `:232`.
> * **R2 (VM has no biome field) — CLOSED.** `Assets/_Modules/Village/Hero/RealmMapVM.cs:87`
>   (`public readonly string Biome;`), assigned at `:101`, surfaced as `SelectedBiome` at `:218-224`, fed from
>   `RealmRegionDef.Biome` at `:382`.
> * **R3 (pin board stays empty — no producer) — CLOSED.** `Assets/_Modules/Village/World/RealmPinProducers.cs`
>   exists and publishes per-source (`RealmPinSources.Hero` / `.Dungeons` / `.Raids` / `.Army`, `:91-94`);
>   consumers read `RealmPinBoard.Pins` at `RealmMapVM.cs:264` and `HudMinimapWidget.cs:370`.
>
> **THE REAL BLOCKER IS THE FLAG, NOT THE CODE:** `Assets/_Modules/Core/FeatureFlags.cs:734` —
> `public static bool MapTab => Get("maptab", defaultOn: false);`. Per CLAUDE.md 7, Map is a Bag tab held OFF
> because realm travel is a WO-827 stub. **Nobody can felt-verify this WO until the owner turns the flag on**
> (PlayerPrefs `ff.maptab`), so it cannot be closed by writing more code.


# WORK ORDER 829 — Map atmosphere + content pins (Withering, biomes, raids/dungeons/rumors)

**Status: READY TO IMPLEMENT**  
**Minted:** 2026-08-01  
**Program:** WO-825  
**Silo:** UI art direction + light systems  
**Depends on:** **826** required · **827** for live pins · **828** optional for minimap pins  
**Roles:** Claude art/copy optional · CLI implement  

---

## Why

A correct node graph still feels flat without **edge of the world**, **biome personality**, and **reasons to open the map** (not only travel). This WO is the immersion **spice** layer — not the engine.

## Goal

1. **Withering / Wound edge** on the parchment (visual + one-line lore).  
2. **Biome personality** per node (tint / icon / short epithet).  
3. **Content pins** so the map answers “what can I do in the world?”  
4. Stay data-driven; no scene hand-edits.

---

## Scope

### 1. Withering border (from realm-map.json `withering` block)

- Draw a darkened / cracked edge band on the parchment (shader or layered Images).  
- Tooltip / detail line when selected: short canon (Elarion last green sanctuary — no Avalon).  
- `weeklyRealmThreat` stays false unless event system exists; do not fake threatened weekly.

### 2. Biome language

| biome token | Map treatment |
|-------------|---------------|
| forest | green-moss node ring |
| swamp | murky teal |
| ice | pale blue |
| fire | ember orange |
| cosmic | violet/star |
| (home) | gilt heart / tree crest |

Icons: letter fallback OK if no sprite; prefer single atlas later.

### 3. Content pins (toggleable layers or always-on small glyphs)

| Pin | Data source | Action on tap |
|-----|-------------|----------------|
| **You** | hero zone / home | center detail on current region |
| **Raid target** | available raid camps / Village2 targets if any | open Raids flow only if 820 Ready; else toast + train redirect |
| **Dungeon** | known dungeon portals / RoomForge entries if cataloged | detail + “Travel marker” if 827 |
| **Rumor** | active tracked rumor with world anchor if any | open Rumor Board or mark objective |
| **Army / Barracks** | barracks built | marker only (822 synergy) |
| **Threat** | outposts uncleared in zone | detail count “X camps” |

Cap visible pins; overflow `+N`. Colorblind: legend row or detail text.

### 4. Audio / juice (light)

- Open map: one parchment SFX if AudioService has a fit; else skip.  
- Discover region: short sting once (827 discovery).  
- Do not add new music system.

### 5. Narrative hygiene

- Add region titles used on map to canon strings if missing (`canon-strings` / owner pass).  
- Descriptions in JSON are placeholder-grade — **do not** rewrite all lore unless owner supplies; show as-is with FlowTrace if empty.

### 6. Minimap mirror (optional)

- If 828 shipped: show subset of pins (objective + 1 threat) on corner map.  
- Same projection helpers; no duplicate game logic.

---

## Acceptance

- [ ] Parchment shows Withering/edge treatment and biome-tinted nodes  
- [ ] At least three pin types live when content exists (you, one threat or barracks, one other)  
- [ ] Raid pin never bypasses full-army gate  
- [ ] Locked regions do not show spoilery pin details (fog keeps secrets)  
- [ ] COMPILE_GATE_OK; no Avalon copy  
- [ ] PO felt: “the map feels like a world, not a menu”  

## Do NOT

- Implement full weekly threat event  
- Full dungeon authoring  
- Replace 826/827 architecture  
- Heavy VFX that tanks mobile  

## Paste for CLI

```text
Implement WORK_ORDER_829_map_atmosphere_content_pins.md on top of 826/827.
Withering edge, biome node tints, content pins (raid/dungeon/rumor/army) with fog rules.
No army-gate bypass. Elarion not Avalon.
```

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: leave it to do.

> **CLI 2026-08-21:** 601806082 - Core half (RealmPins + RealmAtmosphereStyle) landed. REMAINING: the parchment rendering in RealmMapPanel, and a producer for content pins (they read 0 until one publishes).

---

## REMAINING — named (do NOT flip DONE until both land)

| # | Hole | Evidence |
|---|---|---|
| **R1** | **Parchment never paints atmosphere** | `RealmMapPanel` has **zero** refs to `RealmPin` / `RealmAtmosphereStyle` / `Withering`. `BuildNode` still tints by `NodeState` only (gold/cleared/fog). Plate is flat parchment + gold trim — no Withering edge band. |
| **R2** | **VM has no biome field** | `RealmMapVM.NodeRow` omits `Biome` even though `RealmRegionDef.Biome` exists in catalog. |
| **R3** | **Pin board stays empty** | Only publish path is `VillageHudController.SetMinimapPoi`; **no callers**. `TownHudBridge.PushMinimapPois` was deleted. Minimap + parchment read 0 pins until a producer publishes. |

Core that already landed (keep; do not rebuild): `RealmPins` / `RealmPinBoard`, `RealmAtmosphereStyle`, `RealmMapCatalog.Withering`, minimap `LayoutPins` consumer.

---

## SOLUTION — concrete close-out (research 2026-08-17)

### S1 — Wire parchment to the tables that already exist

1. **`RealmMapVM.NodeRow`** — add `Biome` (+ optional epithet) from `RealmRegionDef` via `RealmAtmosphereStyle.Biome(...)`.
2. **`RealmMapPanel.BuildNode`** — for non-locked nodes: ring = `Biome(…).Ring`, glyph = style glyph; home → `Biome("home")`. Locked/fog: no spoiler tint.
3. **Open/BuildUI** — if `RealmMapCatalog.Withering.EdgeBorder`, add darkened rim `Image` using `RealmAtmosphereStyle.WitheringEdge`; detail line = `WitheringLore`. Never invent weekly threat.
4. **Pin layer** — subscribe `RealmPinBoard.Changed`; draw ≤ `MaxVisiblePins` with `RealmAtmosphereStyle.Pin(kind)`; respect `RevealsDetail(regionState)`; show `+N` overflow.

### S2 — Named pin producers (board stays empty without these)

Publish under **stable source ids** (replace-by-source, never rebuild one flat list every tick — that bug is documented in `RealmPins.cs`):

| sourceId | Kind | Data |
|---|---|---|
| `"hero"` | You | hero world XZ |
| `"dungeons"` | Dungeon | AuthoredPortal seats from `DungeonWorldPortalSpawner` |
| `"raids"` | RaidTarget | available camps — **marker only**; tap re-checks army gate |
| `"army"` | Army | barracks built |
| `"rumors"` | Rumor | tracked rumor with world anchor (if any) |

Ship static/authored pins first; live discovery writer still depends on WO-827.

### S3 — Acceptance (closes PARTIAL)

- [ ] Parchment shows Withering edge + biome-tinted nodes  
- [ ] ≥3 pin kinds live when content exists (You + one other + one threat/army/dungeon)  
- [ ] Locked regions hide spoilery pin detail  
- [ ] Raid pin never bypasses army gate  
- [ ] No Avalon copy; `COMPILE_GATE_OK`

**Do not** mark DONE on core-only. R1+R2+R3 are the player-felt half.
