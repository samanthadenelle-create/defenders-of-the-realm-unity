# WORK ORDER 825 — Immersive world / realm map **program** (master)

**Status: READY — PROGRAM / DISPATCH AUTHORITY**  
**Minted:** 2026-08-01 (CLI / Grok — owner: immersive map beyond today)  
**Silo:** World / HUD / Narrative  
**Children:** **826** parchment Realm Map UI · **827** discovery + travel + zone identity · **828** live minimap · **829** atmosphere + content pins  

---

## 1. Why immersion is low today (code truth)

| Layer | What exists | Player feels |
|-------|-------------|--------------|
| **Realm Map data** | `realm-map.json` dual-copy (Elarion + 5 regions, gates, mapPoints, fog states) | **Almost nothing** — Unity UI/runtime for this screen is largely **unbuilt** (port-notes: later / deferred) |
| **Live world zones** | `ZoneManager` / `RegionId`: Village, Goldfields, Stoneback, Mirewood, Ashwood | Names **diverge** from realm-map.json (Thornwood, Hollowfrost, …) → map and feet disagree |
| **Navigation** | `HudCompassWidget` (heading + objective chevron + enemy ticks) | Direction only — **not** a map of the realm |
| **Minimap** | Spec `docs/vfx/minimap_spec_v2.md`; `SetMinimapPoi` **stubbed** deferred | No corner map; Blink Minimap prefab not the live kit path |
| **Travel** | Seams / `SceneLink` / “Travel to …” prompts; overworld = `Main_Castle_Overworld` | Walk + prompts — no **tap node on map → go** fantasy |
| **Raid “map”** | `RaidSelectionScreen` camp list | Functional, not a **world** |
| **Terrain scale** | WO-34 streaming 1km² design (old) | Separate from parchment map; do not conflate |

**Canon names:** home base is **Elarion** (not Avalon). Player copy must say Elarion.

---

## 2. Immersion north star (what “better” means)

Steal **feel**, not skins:

| Fantasy | Reference | Our version |
|---------|-----------|-------------|
| Parchment / campaign map of the realm | WC3 campaign map, many RPGs | Full-screen **Realm Map** from Wayshrine / HUD Map |
| Fog of war / unknown lands | CoC clouds on locked, RPG fog | locked / discovered / cleared / threatened |
| “I am here” + where danger is | Minimap + zone tint | Corner minimap + danger tier (cheap, not RT camera) |
| Travel as a choice | Fast travel nodes when unlocked | Tap **discovered** node → confirm → seam/teleport |
| Story on the edge of the map | Withering / Wound | Visual border + short lore, not a wall of text |

**Not V1 goals:** live second camera RT minimap; full 1km streaming rebuild (WO-34 stays architecture backlog); async “someone attacked my base.”

---

## 3. Binding ship order

```
825 (this)     — owner reads; rules soft choices in §5
    │
826            — parchment Realm Map UI + load realm-map.json + fog states (LOOK)
    │
827            — align ZoneManager ↔ realm regions + discovery ledger + travel from map
    │
828            — corner minimap + POIs + compass synergy (always-on immersion)
    │
829            — Withering edge art, biome tint, pins (raids/dungeons/rumors/army)
```

Parallel only 828 design vs 826 design if file-disjoint; **827 after 826 shell** so travel has a screen to land on.

---

## 4. PO “map feels immersive” bar

- [ ] From hub, player can open a **Realm Map** that looks like a place in the world (parchment / gilt kit), not a debug list  
- [ ] Home **Elarion** is clearly the center; at least **one** outer region shows locked fog vs discovered  
- [ ] “You are here” reads on map and/or minimap  
- [ ] Compass still works; minimap does not fight it (complementary)  
- [ ] Travel from a **discovered** node is possible without DevPanel  
- [ ] Region names on map match what the overworld / coach says (identity fix in 827)  
- [ ] Locked region shows **why** locked (gate text), not a dead button  

---

## 5. Owner rulings needed (record answers in RESULT)

| # | Question | Default if silent |
|---|----------|-------------------|
| R1 | Map art: **stylized parchment** (recommended) vs painted satellite vs abstract nodes only? | Parchment + nodes |
| R2 | Travel: **instant Wayshrine teleport** to region gate vs **walk to seam** after map sets a marker? | Marker + walk first; teleport later if friction |
| R3 | Zone identity: **rename live zones** to realm-map catalog **or** rewrite realm-map to Goldfields/Stoneback/…? | Prefer **unify on narrative realm-map names** with ZoneManager aliases |
| R4 | First open: free after onboard **or** only via Wayshrine building? | Wayshrine if placed; else HUD Map once Onboarded |

---

## 6. Children (do not implement all in one PR)

| WO | File | One line |
|----|------|----------|
| **826** | `WORK_ORDER_826_realm_map_parchment_ui.md` | Full-screen Realm Map UI + loader + fog |
| **827** | `WORK_ORDER_827_realm_map_discovery_travel.md` | Progress ledger, gates, zone align, travel |
| **828** | `WORK_ORDER_828_live_minimap_immersion.md` | Cheap corner minimap + POIs |
| **829** | `WORK_ORDER_829_map_atmosphere_content_pins.md` | Withering, biomes, raid/dungeon/rumor pins |

---

## 7. Roles

- **Owner:** R1–R4; close §4 bar after felt  
- **Claude:** parchment wireframes / node art direction for 826 & 829 (no `.cs` unless CLI)  
- **CLI:** implement children in order; gate each  

### Paste boot

```text
Read WORK_ORDER_825_immersive_world_map_program.md then ONE child (826 first).
Realm map data already lives in realm-map.json — do not invent a second catalog.
Elarion not Avalon. Presentation never touches Village objects from HUD.
```
