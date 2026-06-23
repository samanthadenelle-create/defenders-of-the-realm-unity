# WORK ORDER 176 — Tower visual polish (functional but ugly → fits the game's look)

**Status: READY TO IMPLEMENT**
**Priority:** Medium — visual quality; the tower is a core, always-visible defensive structure.
**Date:** 2026-05-31
**Lane:** Architect / Art — tower prefab/material + placement scale. Likely `VillageSceneBuilder` /
tower prefab + materials. CLI/designer; no gameplay change.
**Source:** owner playtest — *"tower needs made to be better. it's currently functional yet ugly."*

---

## The problem
The defensive tower works but **doesn't fit the game's art language**. From the screenshot:
- It's a **heavy, realistic stone-cannon tower** that clashes with the **chibi / stylized low-poly** look
  of the hero, pets, and village (the polyperfect _M aesthetic).
- It reads **dark / flat-lit / untextured-ish** (likely a URP material or lighting issue, or the wrong
  prefab tier), and the **cannon barrel** sticking out looks bolted-on / out of proportion.
- Scale/proportion feels off next to the chibi hero.

## The goal
Make the tower **look like it belongs** — same stylized low-poly fantasy language as the village, properly
lit/materialed, correctly proportioned, and clearly readable as *this game's* defensive tower. Keep its
gameplay/collision/targeting intact (visual + prefab swap only).

## What to do (CLI/designer's call on exact approach)
1. **Pick a tower mesh that matches the art language** — use a **polyperfect `_M` Medieval tower** that
   matches the village's low-poly style (the catalog has `Tower_Medieval_Big/Small`, `Tower_Castle_Round`,
   etc. — `docs/polyperfect-asset-catalog.md`). Avoid a realistic/mismatched mesh; match the chibi-friendly
   stylization the rest of the world uses.
2. **Fix materials/lighting** — ensure URP materials render correctly (the dark/flat look suggests a
   material or lighting issue — same family as the magenta/URP fixes; run the polyperfect URP material fix
   if needed). It should read crisp and lit like the hero/village, not muddy.
3. **Proportion + scale** — size it sensibly next to the chibi hero (imposing but not absurd); seat it flat
   on the ground; lose/clean up the awkward bolted-on cannon if it doesn't fit (or restyle to a stylized
   arcane/medieval emplacement that matches).
4. **Tier visuals (ties WO-114 / DEFENSE_DEPTH)** — if towers upgrade (L1→L3+), each tier should read
   visually distinct (bigger/fancier) — confirm the upgrade visuals also use the matching stylized set.
5. **Consistency** — whatever style is chosen, apply it so **all towers** (and ideally walls/buildings)
   share one coherent look — the tower shouldn't be the odd one out.

## Constraints
- **Visual/prefab/material only** — keep tower gameplay (range/damage/targeting/collision, `Tower.cs` /
  `TowerData`) unchanged. This is a look pass.
- Use polyperfect `_M` stylized assets (mobile-light, matches the village); confirm prefab names in the
  catalog; missing prefab → `Debug.LogWarning` + keep functional (CLAUDE.md §4).
- If the swap touches `VillageSceneBuilder` placement, it's the single-writer architect lane (coordinate
  with Agent 1); editor-closed rebake. Brace-gate any `.cs`.

## Acceptance criteria
1. The tower visually **matches the game's stylized low-poly art language** (fits beside the chibi hero/village) — no longer a mismatched realistic/dark mesh.
2. Materials render correctly (lit, crisp, URP-correct — not dark/flat); proportion/scale sensible; seated on ground.
3. Tower gameplay (range/damage/targeting/collision) unchanged.
4. Upgrade tiers (if present) read visually distinct + share the style; all towers consistent.
5. Brace balance; editor-closed rebake if placement changed.

## Note
- **Pet T-pose** (also in the screenshot) is NOT this WO — it's the shared animator-param bug, covered by
  WO-166 (pet) + WO-174 (hero) + WO-163 (NPC). Fix there as one animation pass; don't duplicate here.

## Done checklist (CLAUDE.md §10)
- [ ] Tower mesh swapped to a stylized polyperfect `_M` tower matching the village look
- [ ] URP materials correct (lit/crisp); proportion/scale/seating fixed; cannon restyled or removed if mismatched
- [ ] Gameplay unchanged; upgrade tiers consistent + distinct; all towers share the style
- [ ] Brace balance; rebake (editor closed) if placement touched
- [ ] `WORK_ORDER_176_tower_visual_polish.RESULT.md` when complete
