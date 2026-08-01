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
