# PROOF — 2026-08-18 overnight gear + structures

Device: Solana Seeker (SM02G4061955851). Builds: 331306 (before) -> 331367 (after).

| file | what it shows |
|---|---|
| 00_BEFORE_hub_331306.png | baseline, build 331306, before the overnight fixes |
| 01_AFTER_boot_331367.png | PROD-006 VERIFIED: title screen, wallet CHKK...sfkC connected top-right, NO SIGN IN modal |
| 02_AFTER_hub_331367.png  | hub after Continue; Aldwin dialogue; hero carrying gear |
| 03_AFTER_hero_gear_331367.png | hero from behind with sword visible on back |
| 04/05_AFTER_storefronts_*.png | camera pans; storefronts NOT clearly framed - see LIMITS |

## VERIFIED BY MEASUREMENT (not by eye)

PROD-006 sign-in gate
  auto-resume SUCCEEDED - connected at boot with no player action
  NO LoginPanelController:Build line in the whole session - the panel never constructed.

Gear (shield/sword)
  before  worldBounds s(0.42, 0.53, 0.41)
  after   worldBounds s(0.72, 0.92, 0.72)   <- 0.92m longest vs legacy shield_A 0.918m = parity
  trace now names its subject:
    "parent-scale compensate: off-hand id='knight_shield_starter' mesh='ShieldWithItemLogic'
     on 'Hero (Blaise)' ... authored=1.73"
  volume: ~1800 lines/60s BEFORE -> 4 lines for the ENTIRE session AFTER.

PROD-003 Realm Store (measured at bake, prediction made BEFORE the run)
  predicted scale 5.49, bounds (5.12, 4.00, 6.35), collider (3.40, 4.00, 5.50)
  measured  scale 5.49, bounds (5.12, 4.00, 6.35), collider (3.400, 4.000, 5.503)
  REALM_STORE_REACHABLE_OK nearest walkable 0.08m (was 0.33m)

R2 content parity
  R2_PARITY_OK 4 object(s) verified - first real use of the PROD-011 gate on a live build.

## ⚠ LIMITS - what these shots do NOT prove

1. The FOUR hub storefronts fixed tonight (Forge, armorer, jeweler, barracks) are NOT
   visually confirmed. adb swipes pan the camera only slightly and the hero is mid-tutorial,
   so I could not frame them. Their fix is derived + gate-green but UNSEEN.
   NEGATIVE CONTROL, also unseen: the five bakeAxisConversion:0 buildings (pet house,
   arcane tower, market, lumbermill, windmill) must look IDENTICAL to before. If the
   lumbermill is anything but unchanged, revert the hub-injector change wholesale.
2. The shield is confirmed by MEASUREMENT at 0.92m, not by a clear rear view. Whether that
   size LOOKS right is the owner's call; the offsets row is the one knob.
3. The dungeon->town port - the actual PROD-005 acceptance criterion - was NOT exercised.

## OBSERVATIONS FOR THE OWNER'S EYES (not introduced tonight; present in every frame)

- The Heart of Elarion canopy renders as detached floating chunks rather than a canopy.
- The bottom action bar shows FOUR faces (Build/Bag/Quests/Manage); canon says calm(town)
  is SIX (Build, Talk, Bag, Raids, Quests, Manage). Talk and Raids absent in these frames.
Neither is claimed as a defect - both need a ruling on whether they are intended.