**Status:** READY TO IMPLEMENT — owner ruling 2026-08-21: leave on the to-do.

# WORK ORDER 828 — Live minimap immersion (hub + overworld, cheap)

**Status: READY TO IMPLEMENT**  
**Minted:** 2026-08-01  
**Program:** WO-825  
**Silo:** HUD kit  
**Depends on:** none hard; benefits from 827 objective provider  
**Prior art:** `docs/vfx/minimap_spec_v2.md` (QUEUED — this WO **implements** the cheap path)  
**Roles:** CLI implement  

---

## Why

Compass gives **bearing** but not **place**. Players in `MainCastle_Hall` / `Main_Castle_Overworld` get lost; immersion needs a **you-are-here** diagram. Spec already ruled **against** live second Camera → RenderTexture (mobile cost).

## Goal

Ship a **small corner minimap** (code-built kit widget):

- Static / baked backdrop (dark glass first; optional top-down sprite later).  
- Hero dot + optional objective + culled threat/POI dots.  
- Near-zero GPU: RectTransform moves only.  
- Complements `HudCompassWidget` — does not replace it.  
- Presentation-only; providers via HudKitController (HUD → Core only).

---

## Scope

### 1. Widget

- `Assets/_Modules/HUD/Kit/HudMinimapWidget.cs` (name to house style).  
- Size ~120–160 dp corner; safe-area inset; posture via `hud-areas.json` (calm town + calm explore; hide in hard combat/raid if posture says so — match compass rows).  
- Backdrop: `ElarionUi` dark glass + gold hairline; optional `Resources/UI/Minimap/hub_outline` sprite if art exists (fallback solid OK).  
- **No** new Camera, no RenderTexture in V1.

### 2. Projection

- World → map: fixed linear scale + offset (mirror old `ProjectMiniMap` idea / zone bounds).  
- Configurable: center on hero **or** center on Elarion with hero moving (pick one; default **center on hero** for overworld, **fixed hub bounds** in castle if easier).  
- Clamp dots to panel; fade/cull beyond radius (e.g. 150u).

### 3. Dots (providers)

| Dot | Source (provider / Core snapshot) | Color |
|-----|-------------------------------------|-------|
| Hero | Hero transform | Gold |
| Objective | Same objective as compass (seam / map travel target) | Gilt chevron or cyan |
| Enemies / reps | Enemy list or overworld encounter reps (cull) | Soft red |
| Optional POI | Structure / portal / outpost (cap N) | White/grey |

Wire providers like compass (reflection or Core gate) — **no Village reference from HUD**.

### 4. Danger / region tint (visual only)

- Optional panel edge tint from danger tier / zone (ZoneManager or Core snapshot).  
- Label chip under map: short region title (from 827 alias if available; else ZoneManager display name).  
- Colorblind: title text always present.

### 5. POI API

Replace stubs in `VillageHudController` if still live path:

- `SetMinimapPoi(kind, x, z)` / `ClearMinimapPois` → forward to widget or Core HudModel.  
- Quest / 822 barracks marker can register a POI.

### 6. Feature flag

- Prefer always-on in hub/overworld once stable; or `ff.minimap` default **ON** for hub postures.  
- Headless: assert widget builds; dot count updates with fake providers.

### 7. Performance

- Update dots @ 4–10 Hz, not every frame if expensive.  
- Zero alloc hot path where practical.

---

## Acceptance

- [ ] In hub + overworld postures, corner minimap visible with hero dot moving  
- [ ] Objective from compass/map-travel appears when set  
- [ ] No RT camera; no mobile fill spike by design  
- [ ] Raid/enemy-owned scenes hide or combat posture hides per hud-areas  
- [ ] COMPILE_GATE_OK; FlowTrace tag `Minimap`  
- [ ] Does not break compass layout (WO-795 density)  

## Do NOT

- Implement parchment full map here (826)  
- Live RT minimap  
- UXML  
- Show currency clutter on the minimap  

## Paste for CLI

```text
Implement WORK_ORDER_828_live_minimap_immersion.md per docs/vfx/minimap_spec_v2.md cheap path.
HudMinimapWidget + hud-areas + providers. No RenderTexture. Complements compass.
```

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner explicitly corrected herself from 823 to 828 and said "leave it on to do". Live minimap immersion STAYS in the queue.
