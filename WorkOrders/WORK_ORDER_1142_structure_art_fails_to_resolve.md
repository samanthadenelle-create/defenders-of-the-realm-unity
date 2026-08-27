**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).

# WORK ORDER 1142 - Structure art fails to resolve: a building is lost on every boot

**Minted:** 2026-08-22 (CLI, banner bumped 1142 -> 1145 in the SAME edit)
**Lane:** Core / Addressables + structure art. **Class:** LIVE DEFECT, reproduced 8/8.
**Found by:** the 2026-08-22 headed autopilot fleet (`run-autopilot-fleet.ps1 -Graphics`), 8 runs,
seeds 1-8. Evidence: `Builds/autopilot-tickets.md`, per-run `player.log`.

## THE MEASURED FAILURE - not inferred, captured

```
[Flow:VisualFactory] model not found via Addressables OR Resources: 'Structures/ArcaneSpire_1'
   - returning null (caller falls back). Check the address exists in the Structure_Art group
     and that its bundle is uploaded to the CDN.
[Flow:Structure]     'tower_arcane_spire': visual 'Structures/ArcaneSpire_1' not found / failed
     to skin - destroying empty root, returning null (caller falls back).
[Flow:BaseLayout]    Spawn: StructureFactory.Create returned null for entry 'tower_arcane_spire'
     at cell (5,17) - structure NOT built (ONE BUILDING LOST; check the entry's prefabPath).
```

**80 errors, reproduced in 8 of 8 runs.** A second address fails the same way:
`Structures/arcane tower` (15 errors x 8 runs) - note the SPACE in that address, which is worth
checking on its own.

## WHY THIS MATTERS MORE THAN A MISSING MESH

**A building the player BUILT does not exist after a reload.** `StructureFactory.Create` returns
null, so `BaseLayoutLoader` skips the entry entirely - the structure is not built, its footprint is
not claimed, and nothing on screen says anything went wrong. The player placed and PAID for a
tower that is silently absent next session.

⚠ **This is the CLAUDE.md section 16 trap one layer in.** Section 16 exists because remote art with
no local fallback "installs perfectly, launches perfectly, and plays" with capsule enemies and no
error on screen. Here the game does not even substitute a placeholder - it drops the building.

## WHAT IS ALREADY KNOWN (verify each; do not trust this list)

- `Structures/ArcaneSpire_1` IS authored in `Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset`
  (verified 2026-08-22), alongside `ArcaneSpire_2`, `_3` and their `_Albedo` rows.
- So the address EXISTS in the group - which makes this most likely a BUNDLE/UPLOAD or a
  build-target problem, not an authoring gap. The trace names both possibilities itself.
- A `StandaloneWindows64` catalog was regenerated and pushed to R2 on 2026-08-22 (`R2_PUSH_OK 2
  uploaded`, `R2_PARITY_OK 42 verified`). The failing run used a Windows build from 09:07, i.e.
  BEFORE that push. **Re-run the fleet against a FRESH Windows build first** - this may already be
  fixed, and proving that costs one build.
- ⛔ `R2_PARITY_OK` verifies every object THIS BUILD'S CATALOG NAMES. If the catalog itself never
  named ArcaneSpire's bundle, parity is green and the asset is still missing. Parity is not
  coverage - check the catalog's contents, not just its verification.

## SCOPE

1. Rebuild Windows, re-run `run-autopilot-fleet.ps1 -Graphics`, and see whether it still reproduces.
2. If it does: determine whether the bundle exists in `ServerData/StandaloneWindows64` and whether
   the catalog names it. Do the same for Android - **the owner plays on the Seeker**, and a defect
   that only shows on Windows is a different (lower) priority than one that ships to the device.
3. Investigate `Structures/arcane tower` separately - a space in an address is suspicious and may be
   a data typo rather than a missing bundle.
4. ⛔ **Do NOT "fix" this by restoring a Resources fallback.** `Assets/Resources/Structures` was
   deliberately deleted (section 16); re-adding a local copy re-creates the duplicated-state problem
   the CDN move solved.

## ACCEPTANCE

- [ ] A fresh fleet run produces ZERO `model not found` errors for structure addresses
- [ ] No `structure NOT built` line in any run
- [ ] Verified on the ANDROID target too, not only Windows
- [ ] If a building genuinely cannot resolve, it FAILS LOUD (visible placeholder or an on-screen
      notice) rather than silently vanishing - a lost building must never be indistinguishable from
      a building the player never placed
