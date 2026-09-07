# WO-1539: three enemy models referenced by enemies.json have NO basecolor anywhere, and OgreMage has no mesh

**Status:** SPEC - needs OWNER ACTION (art drop), then implementation
**Silo:** Art / EnemyContent + `EnemyArtCoverageRegression`.
**Source:** wave-two regression `Builds/reg-wave2.log` (422/435), 2026-09-06. Surfaced by
`EnemyArtCoverageRegression`, **registered tonight by WO-1496** - which is precisely why nobody knew.
Minted from the banner (`CLI_LANES_WO_NUMBERS.md`, main line 1539 -> 1541 in the same edit).

## 1. EVIDENCE

```
[every-model-has-art] 3 model(s) referenced by enemies.json have NO basecolor anywhere in the project
   ... OgreMage (NO MESH ...)
```

`OgreMage` is the worst of the three: it has no basecolor AND no mesh. It is referenced anyway - see
**WO-1536**, where `enemies.json:400` gives `ogre` that very `modelKey` and every ogre has silently worn a
stand-in body since.

This is the third member of one family found tonight:

- **WO-1509** - Berserker / Shaman / Necromancer have no albedo; the `EnemyContent` texture tree is empty.
- **WO-1536** - `ogre` points at a model that was never imported.
- **this ticket** - three models referenced with no basecolor at all.

Every one of them fails SILENTLY at runtime and plays on. The pattern is the finding: enemy art has no
gate that runs, so it degrades until someone's eyes catch it.

## 2. WHAT IS NEEDED

1. **LIST the three models by name in the RESULT.** The log truncates; enumerate them from the suite output
   before anything else, so the owner's art drop has an exact worklist.
2. Owner art drop: basecolor for each; a mesh for `OgreMage` or a corrected `enemies.json` row (WO-1536
   decides that one - do not fix it twice).
3. Rebuild and PUSH the R2 enemy bundles via `tools/r2-ship.ps1` - content-hashed names mean this build needs
   its own push (CLAUDE.md sec.16).

## 3. WHAT NOT TO DO
- Do not exempt the three ids to green the suite. It has been unregistered for months; the first thing it
  does must not be to learn to ignore what it found.
- Do not bind a stand-in texture. A wrong body that looks deliberate is worse than an obvious gap.

## 4. ACCEPTANCE
- [ ] The three models named in the RESULT.
- [ ] `EnemyArtCoverageRegression` reports zero failures.
- [ ] `R2_PARITY_OK` on a FRESH log after the bundle rebuild.
- [ ] A device capture of each of the three opened and looked at.
- [ ] `REGRESSION_OK n/n` on a fresh log.
