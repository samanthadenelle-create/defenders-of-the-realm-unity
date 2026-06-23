# WO-278: Full Village Rebuild — Modular Medieval Village Pieces
**Linear:** [DEF-242](https://linear.app/defenders-of-the-realm/issue/DEF-242/p0-full-village-rebuild-modular-medieval-village-pieces-exact)
**Lane:** World/Environment
**Status:** READY TO IMPLEMENT
**Priority:** HIGHEST — P0

## THIS IS THE #1 PRIORITY. Nothing else ships until the village looks right.

See DEF-242 in Linear for the complete spec — exact piece lists per building,
counts, positions, NPC assignments, build rules, and acceptance criteria.

## Summary

Tear down ALL current placeholder buildings. Rebuild every structure using
modular pieces from `Assets/Medieval Village/FBX/` (176 pieces available).

### Buildings to construct:

1. **Forge** (SE) — open-air, brick walls, chimney, Blacksmith NPC
2. **Arcane Tower** (NE) — tall enclosed tower, narrow windows, tower roof
3. **Market** (W) — open-air stalls, canopy, wagon, Merchant NPC
4. **Barracks** (NW) — two-story, stone base + plaster upper, Armorer NPC
5. **Pet House** — small pen near Heartwood
6. **Farm** — fenced field with tool shed
7. **Stables** — SM_Stables_Medieval from polyperfect

### CRITICAL — Verification Required (per DEF-236):
- [ ] Screenshot of EACH building from 2 angles in Play mode before marking Done
- [ ] All pieces have MeshColliders — no walkthrough
- [ ] All materials URP — no pink
- [ ] Every building faces Heartwood center
- [ ] Root cause documented for any issues found during build

## Do NOT Touch
- Village.unity (never hand-edit — use VillageSceneBuilder)
- Heartwood tree (stays at 0,0,0)
- Wall perimeter (separate ticket — DEF-236)
