# Felt-test record — 2026-09-02, owner, Windows build 03:33

**Build:** `Builds/Windows/DefendersOfTheRealm.exe` built 2026-09-02 03:33:17
**Content:** `catalog_2026.09.01.351290`, pushed + verified 03:34
(`R2_PUSH_OK 5 uploaded` / `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=228`)
**Gates:** `COMPILE_GATE_OK` 03:30 · `REGRESSION_OK 341/341 suites` 02:45 · `UI_CAPTURE_OK` 03:12

Only the owner can close a felt item (CLAUDE.md sec.13 — headless cannot judge feel). These are her
words, recorded verbatim, not a CLI claim.

---

## PASS — owner-verified

### 1. The fire spell — "the fire works"
Origin: her F8 flag seq 4644, verbatim *"the fire spell is wrong. casts at me and stays at me ...
this isnt working"*.

Two defects, both closed (commit `ba5b7fad0`):
- **Wrong origin.** `SpellVfxFactory.PlayImpact` was never called anywhere in the hero path — WO-875
  wired the cast half at the caster and left the impact half dangling. Compounded by
  `HeroAbilities.RegistryTarget` being hardcoded `"knight"`, so the mage read knight rows: empty
  vfxKey, no projectile, and it impacted with `Melee_Impact` + `Swords_Clash` — a sword clang on a
  fireball.
- **Never despawned.** `Casting_Fire` has all four ParticleSystems `looping:1` while its catalog row
  declares `IsLoop:0`, and `DetectDuration` never read `m.loop` — a 10.3s continuously burning fire
  against a 0.6s cooldown, up to ~17 overlapping (her capture: `live systems=35`).

⚠ STILL OPEN behind this pass: `RegistryTarget` remains hardcoded `"knight"`. Repointing it is a trap
— `motion-castings.json` has mage inheriting humanoid with ZERO rows, so the mage would go from wrong
VFX to NO VFX. The mage needs authored rows (her tagging pass) before that constant can move.

### 2. The archer towers — "the regular towers work"
Origin: her ruling *"one thing i hate is the changes to the archer towers. can you bring my wooden
towers i created in tripo?"* and *"yes i hate those round towers"*.

Closed across three commits (`9dbba0450`, `1fec556d3`):
- catalog re-pointed to her Tripo ladder `Tower_Wooden_Watchtower{,_L2,_L3}`
- the Synty impostor (`SM_Bld_Castle_Wall_Tower_S/M/L_01`, wearing her filenames) re-addressed to
  `Structures/Synty_Tower_Castle_Wall_S/_M/_L` so nothing competes for the address
- `SyntyStructureRetheme`'s three mapping rows DELETED — that file was the source of the masquerade
  and re-running it would have silently reverted the fix
- `FoundingReachabilityRegression` inverted, and falsifiable in BOTH directions

### 3. Gate traversal — "the door works"
Second confirmation. The first (*"i can now go through gates normally"*) was given while the content
catalog was 404ing, so the world was resolving placeholders. This one is with content live — a
STRONGER signal, not a repeat.

⚠ STILL OPEN: the enemy/AI half. `NavMesh.CalculatePath == PathComplete` is now asserted on 8 routes
in `GateTraversalProof`, but that proof has not been RUN since the assertion was added. Walking a
surface and pathing across it are different guarantees. WO-1295 stays open on that.

### 4. Daily chest modal sequencing - "daily chest worked"
WO-1296 item 1 of 3. Origin: the chest offer collided with the Echo unlock popup - two modals racing
for the same layer.

Closed: `OfferAfterDelay` became `OfferWhenUiClear`, which waits for TWO CONSECUTIVE frames of
`!PanelManager.AnyOpen` before presenting, tracks its coroutine handle and stops it on `OnDisable`.
`_offeredThisSession` is now claimed only AFTER the modal actually builds, so a failed build no
longer burns the player's daily offer.

⚠ Known residual, not hit here: the clear-wait is UNBOUNDED. A chronically-open panel means the chest
never offers that session, with no timeout and no log line. It is a revenue surface, so if she ever
reports a missing chest, look here first.


### 5. Hero nameplate - "nameplate was good"
Origin: her verbatim *"see how it says THrain Mana? Why is MAna there"*. The plate read
"Thrain  Lv 2 - Mana"; it now reads identity only.

WO-999 had appended the class resource word (Mana / Vigor / Focus) so the bar would read as a class
economy rather than generic MP. The INTENT was sound; the attachment point was wrong - it labelled
the MP BAR while living on the NAME line, so the word read as part of who the hero is.

⭐ SHE ACCEPTED A STATED TRADE-OFF AND IT HELD. Removing the word leaves only COLOUR separating the
red and blue bars, which this project otherwise forbids because she is red/green colourblind. She was
shown that explicitly, chose deletion anyway, and has now confirmed it reads fine in practice. The
caveat is recorded in-code at the edit site so nobody re-adds the word by reflex; if it ever must come
back, it goes ON THE BAR, never back on the name line.


---

## STILL UNVERIFIED (do not read the above as covering these)

| item | why it is not closed |
|---|---|
| The 0.28 clock leak | Her flag seq 4656 caught it LIVE after this build. Fix routed, not landed. |
| Wave-clear panel | Second pass landed but NOT visually re-verified. The capture fixture builds `Compact = true` while the live screen is the full modal — the gate photographs a screen the player never sees. |
| Intact-structure tap silence (WO-1296 item 2) | not yet exercised |
| Structure burn seating (WO-1296 item 3) | not yet exercised |
| Enemy family prewarm | 4 bad keys observed live (`largehumanoid`, `orchumanoid`, `skeletonhumanoid`, `skeleton`). WO-1303 routed. |
| Thunderbolt | Bound and committed; not yet felt. |
| Pi | Validation-key mismatch unresolved — needs the portal value. Deployed site ~5 days stale. |
