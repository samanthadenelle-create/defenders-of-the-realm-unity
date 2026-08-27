# RESULT — UI-001 bounce (2026-08-27): town VFX along world Y

**Date:** 2026-08-27
**Seat:** CLI
**WO Status:** left unchanged (READY TO IMPLEMENT / bounced from Fixed) per implementer brief.
**Commit:** none (do not commit).
**Scope:** disable the town Y-column only. Night Market layout, packs.json, HudKitController, Addressables grouper, VFX catalog rows, VFXType ordinals, and scene files were not touched.

## What the owner saw

Owner felt-test 2026-08-27: *"there is a VFX exiting about town along Y and it needs removed or turned off."*

Device evidence: `logs/f8-inbox/device/SM02G4061955851/flag_20260827-164913_06.png` — build-mode bird's-eye of the hub plaza with a single gold vertical shaft rising out of town. Color matches the Realm Store mast (`1.00, 0.72, 0.18`).

## What was turned off, and why

**Emitter id:** `StoreBeacon_AlwaysOn/LightMast`
**File:** `Assets/_Modules/Village/Vfx/RealmStoreBeacon.cs`
**Why this one:** WO-1052 Layer A is an always-on Unity cylinder 18 m tall along world +Y (scale.y=9, local Y=9, gold unlit). It is the remaining town Y-column. `HubAmbientVfxInjector.EnableTreeAura` (the teal 9 m cone off the Heart tree) was already `false`. Heart-tree FireFlies (`TreeofLifeAura_Aura`) stay — owner confirmed those 2026-08-19. Marker8 `store.beacon.near` is a ground ring (`startSpeed` 0), not Y-travel, and stays proximity-gated.

Kill switch: `EnableVerticalMast = false` (`static readonly`, not const, so the ON builder is not CS0162). Existing `LightMast` children are stripped. No catalog row edited, no loop slot consumed (the mast never used one; the near ring still returns its handle on ring-exit / disable / destroy).

## Device-log proof line

On hub load the log must contain:

```
[Flow:RealmStoreBeacon] Y-column emitter id='StoreBeacon_AlwaysOn/LightMast' DISABLED (UI-001 owner bounce 2026-08-27: VFX exiting town along world Y). Not spawned; zero VFX loop slots. Point light + proximity Marker8 ring remain.
```

If a leftover mast is present it also prints `found live and stripped`.

## Not done

- Night Market store rebuild, packs.json badges, HudKitController.
- Unity / compile gate / commit / push.
- `VFXType` / IsLoop / catalog generators.
- WO Status line (left as the bounce).
