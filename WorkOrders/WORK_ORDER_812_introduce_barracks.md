# WO-812 — ADD the Barracks (design changed; placeable path was never shipped)

**Status:** READY TO IMPLEMENT  
**Minted:** 2026-07-30 · **Clarified:** 2026-07-30 (owner: *“since we changed it, we never added it”*)  
**Lane:** Village build catalog + Barracks entry (single lane)  
**Origin:** owner — Barracks is never built / never present after hub redesign  
**Roles:** CLI implements; Claude optional copy for founding teach  

---

## Why (the real gap)

### What changed historically
1. **Older path:** editor/tool dropped a baked `CastleBarracks` into the hub; injectors found it by name.  
2. **Charter WO-724 OPTION A:** “not a buildable; surface bake on founding + `ff.barracks`.”  
3. **Hub / default-town / strategic-placement rework:** many storefronts became **placeable catalog** rows (`structures-catalog.json`).  
4. **What never happened:** Barracks was **never added** as a catalog placeable **and** the bake/`CastleBarracksPlacer` path is unreliable on the current hub — so players get **neither** “I built a Barracks” **nor** a guaranteed prebuilt.

### Proof (code / data)
| Expected place | Reality |
|----------------|---------|
| `structures-catalog.json` placeable id | **No `barracks` row** (Forge, Lumberyard, Echo Hollow, etc. exist) |
| Build menu military / defense | No Barracks card |
| Runtime | Looks for `GameObject.Find("CastleBarracks")` — **if missing, drillmaster no-ops** |
| Unlock | `BarracksUnlock` = `ff.barracks` (default **ON**) + `Onboarded` — unlock alone does **not** create a building |
| Progression | `barracks.json` + train panels exist **orphaned** from placement |

So the army/raid ladder (train → army → deploy) is **UI-complete** but **world-incomplete**.

---

## Goal

**Ship a real Barracks the player can obtain** after founding:

1. **Placeable** structure in the build catalog (primary — matches “we never added it”).  
2. **First Barracks free or prepaid** so founding does not softlock behind 900 wood.  
3. Placed Barracks opens **train / Barracks panel** (same `structureId` `"barracks"` dialogue / train path).  
4. Optional: if a legacy `CastleBarracks` bake still exists, do not double-spawn; prefer **one** live Barracks.

**Success bar:** New game → finish founding → Build menu shows **Barracks** → place it → talk/train Footman. No DevPanel, no name-find.

---

## Scope (CLI)

### 1. Catalog — ADD the building (dual-copy)
Add to **both**:
- `Assets/Resources/Data/Canonical/structures-catalog.json`
- `Assets/StreamingAssets/Data/Canonical/structures-catalog.json`

Suggested row (tune costs to house style; singleton):

```json
{
  "id": "barracks",
  "displayName": "Barracks",
  "type": "Resource",
  "kind": "Cell",
  "visualPrefabPath": "Structures/barracks",
  "repo": {
    "behaviorId": "GameplayBuilding",
    "singleton": true,
    "buildCost": 0,
    "cost": { "wood": 0, "food": 0, "iron": 0, "crystals": 0 },
    "navSurface": "Blocker",
    "placement": {
      "mustSitOn": "Ground",
      "footprint": 5.0,
      "noOverlap": true,
      "checkAffordable": true
    },
    "notes": "WO-812: army train hub. First place free/prepaid; progression is BarracksService level not structure tier alone."
  },
  "orientation": {
    "corrected": false,
    "manual": true,
    "euler": [-90.0, 0.0, 180.0],
    "offset": [0.0, 0.0, 0.0],
    "scale": 1.0,
    "note": "match HubStructureVisualInjector barracks swap if needed"
  }
}
```

- Confirm `Resources/Structures/barracks` (or prefab path used by injector) loads; if only polyperfect path works, document and use that Resources path.  
- Wire **build category** so it appears under Defense / Military / Structures (match existing `build-categories.json` mapping).  
- `building-tiers.json` already has `"id": "barracks"` — keep aligned.

### 2. Founding / free first place
- After `Onboarded` (or on first Build open post-founding): ensure player can place **one free Barracks** (prepaid / free-build list / zero cost + freebie grant — reuse existing free-build patterns e.g. FTUE prepaid or free structure grants).  
- Optional FTUE/objective: “Raise the Barracks” completion on `build.structure_placed:barracks` (or project’s structure placed signal).  
- **Default Town / StrategicPlacement:** if prebuilt town is chosen, either include a Barracks in baked layout rows **or** still grant free place — pick one, no doubles.

### 3. Runtime identity (stop depending only on name find)
- Placed instance must resolve **structureId / building id `barracks`** for:
  - `BuildingInteractable` / Talk → train or Barracks panel  
  - `BarracksNpcInjector` drillmaster: prefer **find by structure id / Building component**, fallback `CastleBarracks` name for legacy  
- `BarracksUnlock`: still gates train UI if flag OFF; when flag ON + onboarded + **Barracks placed or baked present** → train allowed.  
  - Clarify predicate: unlock feature **and** (has barracks structure in world OR unlock alone if you auto-grant place). Document in code.

### 4. Deprecate half-states
- If placeable path is live: **do not** permanently hide a second bake under `!IsUnlocked` without checking placeable ownership.  
- Prefer: unlock controls **feature** (train); **catalog** controls **presence**.  
- Fix stale “default OFF” comments on `FeatureFlags.Barracks`.

### 5. Intro beat (light)
- One teach: highlight Build card or world ping after grant (“Place the Barracks to train troops”).  
- Sylas / rumor optional one-liner.

### 6. Proof
- Catalog regression: id `barracks` present dual-copy.  
- Place in EditMode/headless or BuildMode path if available.  
- Felt: place → train Footman → army has troop.  
- `ff.barracks=0` still blocks train (feature off) even if mesh placed (or hide card — pick one consistent with flag docs).

---

## Acceptance

- [ ] `barracks` in both structure catalogs, appears in Build UI  
- [ ] Player can place first Barracks without grinding a huge cost wall  
- [ ] Placed Barracks is the train entry (drillmaster and/or interact)  
- [ ] No silent “no CastleBarracks in scene” as the only path  
- [ ] No double Barracks in default town without design  
- [ ] Raid/army ladder can start without DevPanel  

---

## Do NOT

- Leave only `GameObject.Find("CastleBarracks")` as the sole presence path  
- Require `ff.basebuilding` for barracks  
- Delete troop/barracks progression data  
- UXML / hand-edit hub `.unity` without a rebuild tool if bake changes  

---

## Files (expected)

| Area | Paths |
|------|--------|
| Catalog | `structures-catalog.json` ×2, `build-categories.json` if needed |
| Place / free | free-build grant / FTUE / `BuildModeController` prepaid patterns |
| Interact | `BuildingInteractable`, Barracks NPC injector |
| Unlock | `BarracksUnlock.cs`, `FeatureFlags` comments |
| Visual | `HubStructureVisualInjector` coexistence rules |

---

## Relationship to other WOs

| WO | Link |
|----|------|
| **806** | Barracks UX spine — needs a building that exists first (**this WO first**) |
| **807** | Troop power UI — after train works |
| **774** | Raid loadout — needs trained army |

**Dispatch:** implement **812 before** polishing Barracks UI (806).

---

## Claude paste (optional)

```text
Read WorkOrders/WORK_ORDER_812_introduce_barracks.md.
Barracks was never added to structures-catalog after hub redesign.
Copy for Build card + founding "place the Barracks" beat only. No .cs.
```
