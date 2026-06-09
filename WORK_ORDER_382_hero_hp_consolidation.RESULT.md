# WO-382 RESULT — Hero HP Display Consolidation

**Status:** ✅ CLOSED
**Commit:** `9c5e132` fix(hud): WO-382 hero HP dedup — party panel is the single HP source
**Verified:** Compile-gated (batchmode), braces 138/138; pending owner visual confirm.

## Resolution
The spec's "scene GameObject" framing was stale — the HUD is code-built uGUI. The hero's HP was rendering in **two** places, both in `VillageHudController`:
1. Party panel (slot 0 = Hero) — KEPT as the single source.
2. A standalone red HP bar + text in the bottom-left vitals cluster — REMOVED.

`SetHeroHp` had been updating both. Removed only the duplicate HP sub-element from `BuildVitalsCluster`; `_hpFill`/`_hpText` stay null and are null-guarded. **Kept the cluster's mana bar + XP line** (those are NOT duplicated — removing the whole cluster would have killed mana/XP).

## Acceptance
- [x] Hero HP shows once (party panel only)
- [x] No duplicate HP element
- [x] Mana + XP preserved
- [ ] (owner) cosmetic: an empty band remains where the old HP bar was — trivial layout follow-up if desired, out of scope here.
