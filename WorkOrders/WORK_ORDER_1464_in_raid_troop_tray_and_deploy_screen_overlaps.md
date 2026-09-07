# WO-1464: the in-raid troop tray is unreadable and three raid bands overlap the HUD beneath them

**Status:** READY TO IMPLEMENT
**Silo:** `Assets/_Modules/Village/Troops/RaidDeployController.cs` (in-raid tray + top band) and
`RaidDeployScreen`. Pairs with WO-1462 (same screen, different defect).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1464 -> 1465 in the same edit).

## 1. EVIDENCE

Tray, at source:

```
RaidDeployController.cs:1141-1153   count badge drawn in ElarionUi.Ink on a dark plaque
```

Dark ink on a dark plate, no counts legible, and the raw slab covers the joystick.

`Logs/device/screens/owner-raid-ui-2026-09-06-143701.png`:

```
"2:23"        painted over  "Th... Lv 7"      (hero nameplate)
"0/3"         painted over  the compass
"Troops 10/10" painted over the progress bar
```

`Logs/device/screens/seeker-357453-raid-deploy.png`:

```
"Army: 8 / 10 slots"  painted over the Grom/Sylas row
"where i..."          the Echo quote truncated
```

## 2. FIX SHAPE

- Tray labels and count badges take kit foreground colours against the plaque, not `Ink`; run every label
  through `FitSingleLine`.
- Derive the raid top band's rect from `HudLayoutBands` so it cannot sit on top of the nameplate, compass or
  troop bar - one band authority, as the peaceful HUD already uses.
- Move the tray slab off the joystick rect.
- Deploy screen: army-slot line gets its own row; the Echo quote wraps or fits rather than truncating.

## 3. WHAT NOT TO DO
- Do not nudge coordinates by hand. A hardcoded offset re-breaks at the next aspect ratio; derive from the
  band model.

## 4. ACCEPTANCE
- [ ] A MEASURED overlap case covering the raid top band vs nameplate/compass/troop bar and the tray vs joystick.
- [ ] Fresh captures of the in-raid HUD and the deploy screen opened; no truncation, no overlap.
- [ ] `REGRESSION_OK n/n` on a fresh log.
