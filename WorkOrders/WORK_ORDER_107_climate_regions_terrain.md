# WORK ORDER 107 — Climate Regions & Terrain Shifts: The World Beyond Elarion

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High — world feel, player exploration, ties to WO-111 resource nodes
**Scope:** Large — VillageSceneBuilder exterior + new ZoneManager system
**Depends on:** WO-104 (castle walls done), WO-85 (atmosphere pass), WO-52 (WeatherManager)
**Catalog:** docs/polyperfect-asset-catalog.md — all assets must be verified there first

---

## Vision

Elarion sits at the centre of a world with four distinct climate regions — one
per cardinal gate. Each region has its own terrain texture, foliage palette,
ambient weather, and resource type. Enemies come from the region they belong to,
so the direction of attack tells a story. The mine system (WO-111) drops resource
nodes in these regions — players venture out to claim them.

This is the Clash of Clans meets Warcraft geography: one connected map, readable
zones, distinct feels.

---

## Zone Layout

```
                    ┌──────────────┐
                    │  NORTH ZONE  │
                    │  Corrupted   │
                    │  Ashwood     │
                    │  (dark, dead │
                    │   trees, fog)│
                    └──────┬───────┘
                           │ North Gate
   ┌────────────┐    ┌─────┴──────┐    ┌────────────┐
   │ WEST ZONE  │    │            │    │ EAST ZONE  │
   │ Stoneback  │◄───┤  ELARION   ├───►│ Goldfields │
   │ Ridge      │    │  (village) │    │ (rolling   │
   │ (mountain, │    │            │    │  plains,   │
   │  rock, snow│    └─────┬──────┘    │  warm sun) │
   │  at peaks) │          │ South Gate └────────────┘
   └────────────┘    ┌─────┴──────┐
                     │ SOUTH ZONE │
                     │ Mirewood   │
                     │ (swamp,    │
                     │  murk,     │
                     │  cypress)  │
                     └────────────┘
```

---

## Zone Definitions

### North — Corrupted Ashwood
The Withering came from here. Dark, dead trees, ash-grey ground, creeping purple
corruption fog. Enemies: Hollow Ones (feel native, like they belong to this rot).

**Terrain:** `Terrain_Plane_Valley` (low, sunken ground)
**Ground texture:** Dark grey/ash (color `#2A2520`)
**Trees:** `Tree_Dead` (primary), `Tree_Dead_Log_A/B` (fallen), occasional `Tree_Conifer`
**Props:** `Rock_Sharp`, `Rock_Large`, corrupted crystal formations (repurpose portal arch)
**Weather:** WeatherManager fog ON, shooting stars DISABLED, rain = `RainIntensity.Light`
**Ambient light:** Cool blue-grey (`#4A5060`)
**Distance from gate:** 80m reach

### East — Goldfields
Open warm grassland, pastoral, bright. Merchants and trade passed through here.
Enemies arrive in formation across the open ground — visible from far away.

**Terrain:** `Terrain_Plane_Plain` + `Terrain_Plane_Hill1/2` (gentle rolling)
**Ground texture:** Warm yellow-green (`#8A9A40`)
**Trees:** `Tree_Oak` (clustered), `Tree_Birch` (scattered)
**Props:** `Rock_Round` (path edges), `Haystack`, `Scarecrow`, `Fence_Picket`
**Weather:** Minimal — sunny, rare shooting stars
**Ambient light:** Warm golden (`#E8C870`)
**Distance from gate:** 80m reach

### West — Stoneback Ridge
Rocky highlands, elevation drops and climbs. Sparse vegetation, exposed stone,
mountain peaks in the background. Harder terrain = slower enemy movement but
tougher enemies.

**Terrain:** `Terrain_Plane_Hill3/4` (steep), `Terrain_Plane_Slope1–4` (transitions)
**Ground texture:** Grey stone (`#707070`)
**Trees:** `Tree_Conifer` (sparse, windswept)
**Props:** `Rock_Large`, `Rock_Sharp`, `Stone_Round`, `Stone_Big`
**Weather:** WeatherManager rain = `RainIntensity.Heavy` (mountain weather)
**Ambient light:** Cool grey-blue (`#6070A0`)
**Distance from gate:** 80m reach

### South — Mirewood Swamp
Murky, low-lying, waterlogged. Cypress-like dead trees rising from dark water.
The main enemy lane — most waves come from the south. Feels oppressive.

**Terrain:** `Terrain_Plane_Lake` (water patches), `Terrain_Plane_Valley3/4` (low)
**Ground texture:** Dark green-brown (`#3A4A30`)
**Trees:** `Tree_Dead` (tall, sparse), `Tree_Dead_Log_A/B` (horizontal, crossing water)
**Props:** `Rock_Large` (mossy), fog particle emitters
**Weather:** WeatherManager fog HEAVY, rain = `RainIntensity.Light`
**Ambient light:** Dark green (`#3A5040`)
**Distance from gate:** 80m reach

---

## 1. ZoneManager.cs

**Path:** `Assets/_Modules/Environment/ZoneManager.cs`

```csharp
using UnityEngine;

namespace DeNelle.Environment
{
    /// <summary>
    /// Holds references to the four climate zones and drives per-zone
    /// weather, ambient light, and fog settings.
    /// Called by WeatherManager and WaveManager (zone = enemy origin).
    /// </summary>
    public class ZoneManager : MonoBehaviour
    {
        public static ZoneManager Instance { get; private set; }

        [System.Serializable]
        public struct ZoneSettings
        {
            public string name;
            public Color  ambientLight;
            public Color  fogColor;
            public float  fogDensity;
            public bool   enableFog;
            public int    rainIntensity;   // 0=off, 1=light, 2=heavy
        }

        public ZoneSettings northZone;   // Corrupted Ashwood
        public ZoneSettings eastZone;    // Goldfields
        public ZoneSettings westZone;    // Stoneback Ridge
        public ZoneSettings southZone;   // Mirewood Swamp

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ApplyZone(ZoneSettings zone)
        {
            RenderSettings.ambientLight = zone.ambientLight;
            RenderSettings.fog          = zone.enableFog;
            RenderSettings.fogColor     = zone.fogColor;
            RenderSettings.fogDensity   = zone.fogDensity;

            var weather = FindObjectOfType<WeatherManager>();
            if (weather != null) weather.SetRainIntensity(zone.rainIntensity);
        }

        /// <summary>Returns zone settings for the given gate direction.</summary>
        public ZoneSettings GetZone(Vector3 gateWorldPos)
        {
            // Classify by gate position
            if (gateWorldPos.z >  30f) return northZone;
            if (gateWorldPos.z < -30f) return southZone;
            if (gateWorldPos.x >  30f) return eastZone;
            return westZone;
        }
    }
}
```

---

## 2. VillageSceneBuilder — exterior zone placement

Add `BuildClimateZones(Transform exteriorRoot)` method:

For each of the 4 directions, place a 80×80m area of terrain tiles, trees, and
props from the zone spec. Method is called from `BuildExterior()` or the main
`BuildVillage()` after the wall perimeter is placed.

Key placement rule: zone content starts 6m outside the moat ring (moat outer edge
is at ±48/±39 from WO-104). Zone content fills from ±48/±39 outward to ±128/±119.

### North zone placement
```
Center: (0, 0, +80)
Trees: Tree_Dead × 12, Tree_Conifer × 4 — scattered in 80×80 area
Ground: Terrain_Plane_Valley × 20 tiles filling the north area
Props: Rock_Sharp × 6, Rock_Large × 3
```

### East zone placement
```
Center: (+80, 0, 0)
Trees: Tree_Oak × 8 (3 clusters of 2-3), Tree_Birch × 6
Ground: Terrain_Plane_Plain × 15, Terrain_Plane_Hill1 × 5
Props: Haystack × 2, Scarecrow × 1, Rock_Round × 8
```

### West zone placement
```
Center: (-80, 0, 0)
Trees: Tree_Conifer × 6 (sparse)
Ground: Terrain_Plane_Slope1 × 8 (elevation), Terrain_Plane_Hill3 × 4
Props: Rock_Large × 8, Stone_Big × 6, Rock_Sharp × 4
Elevation: raise Y by +3m at outer edge to suggest ridge
```

### South zone placement
```
Center: (0, 0, -80)
Trees: Tree_Dead × 10 (tall, widely spaced), Tree_Dead_Log_A × 4
Ground: Terrain_Plane_Lake × 8 (water patches), Terrain_Plane_Valley3 × 12
Props: Rock_Large × 4 (mossy-looking)
```

---

## 3. Terrain shift transitions

At each zone boundary (the moat ring edge), use `Terrain_Plane_Slope` tiles to
create a natural height transition. The village interior is at Y=0; each zone
transitions ±1-3m over 20m of horizontal distance.

North → dips down (valley feel)
East → gentle roll (hills)
West → rises up (ridge)
South → dips down (swamp feel)

---

## 4. WaveManager integration (future hook)

Each `WaveSpawnPoint` already has a SpawnId (`spawn-0..3`). Map:
- `spawn-0` (south) → Mirewood enemies
- `spawn-1` (east) → Goldfields enemies
- `spawn-2` (west) → Stoneback enemies
- `spawn-3` (north) → Ashwood enemies

When the wave spawns, call `ZoneManager.Instance?.ApplyZone(GetZone(spawnPoint.position))`
to shift the ambient lighting and weather to that zone's feel. Revert to village
ambient on wave clear.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Environment/ZoneManager.cs` | **Create** |
| `Assets/Editor/VillageSceneBuilder.cs` | **Edit** — add `BuildClimateZones()`, call from `BuildVillage()` |
| `Assets/Scenes/Village.unity` | Rebuilt via builder — do NOT hand-edit |

**Do NOT touch:** gameplay code, WaveManager spawn logic, WalletService, any ATB files.

---

## Acceptance Criteria

- [ ] Four visually distinct zones visible beyond the moat ring
- [ ] North zone: dark dead trees, grey ground, feels corrupted
- [ ] East zone: warm open grassland with oak clusters
- [ ] West zone: rocky elevated terrain, sparse conifers
- [ ] South zone: swampy low terrain with dead cypress silhouettes and water patches
- [ ] Terrain slope transitions at moat ring boundary (no abrupt flat edge)
- [ ] ZoneManager.cs compiles and applies ambient light / fog per zone
- [ ] No purple materials (polyperfect atlas — should be fine)
- [ ] 60 FPS maintained on mid-range mobile (LOD + culling already handled by WO-54/53)
- [ ] Rebake required — queue after WO-104 completes
