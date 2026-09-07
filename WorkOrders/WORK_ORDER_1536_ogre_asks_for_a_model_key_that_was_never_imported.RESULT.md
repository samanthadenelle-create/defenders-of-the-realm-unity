# WO-1536 RESULT - the ogre now names the body it actually wears
**Status:** IMPLEMENTED - 2026-09-07 uncommitted, awaiting gate. Edit-only lane: no Unity, no git, no commit.

## CHOICE (WO sec.2 asked for it stated)
**Corrected the data; did not import art.** Every ogre already wore `Orc_Shaman` via EnemyFactory's stand-in, so
naming it in `enemies.json` changes zero pixels and makes the data say what the game renders.
**The WO's title is hearsay and I am correcting it: `OgreMage` was NOT "never imported".** `git log --all`:
added `Assets/Resources/Enemies/OgreMage.fbx` in `fc13eb2f1` (06-09), AccuRIG'd `e87559b72` (06-13), **DELETED
in `0cec81a78` (2026-07-01, "size cuts")** - 24.9 MB (`WebGLSizePass408.cs:6`). Not gitignored, absent from HEAD.
So it is an art **RE-IMPORT** ask off that commit, not a commission. Recorded in `_schemaNotes.modelKey`.
**Orc_Shaman art chain (read at source):** `Orc_Shaman.fbx.meta` externalObjects -> guid `a68663ae...` =
`Orc_Shaman.mat`, `_BaseMap` guid `756700e7...` = `OrcTex/Orc_Mage_basecolor.jpg` (on disk); fbx has a
`.tripo-extracted` sentinel. **Expected** `ResolveArt` tier 1 BOUND - not run here; the gate proves it.

## LF / TWIN PROOF (python rb/wb, memory `canonical-json-edits-binary-only-verify-newlines`)
`LF 414 -> 414, CRLF 414 -> 414 (all newlines still CRLF); json.loads OK pre-write; twins byte-identical, len 17647; all bytes ASCII; ogre modelKey -> Orc_Shaman; ReadModelKeys scan replayed = 15 distinct keys, OgreMage gone.`

## CHANGED (line numbers re-grepped after the edits)
- `Assets/{Resources,StreamingAssets}/Data/Canonical/enemies.json:400` (byte-identical twins):
  `"OgreMage"` -> `"Orc_Shaman"`; `:16` `_schemaNotes.modelKey` tail rewritten to the re-import note + hashes.
- `EnemyFactory.cs:633` new `s_rejectedDataKeys`; `:701-710` the rejected-key path was `FlowTrace.Once`
  (Sink.**Info**) - that INFO line is why the wrong body shipped for months. Now `FlowTrace.Fail` (Sink.Error)
  naming the id, deduped per (id+key) so a wave cannot burst the channel. **Stand-in return KEPT** (WO sec.3).
- `EnemyFactory.cs:663-675, 758-770, 786` + `EnemyResolver.cs:83-90` - stale prose corrected (sec.15).
- `OutpostEnemyGroupSpawner.cs:697` - synthesised `ogre` def also said `ModelKey = "OgreMage"` (would trip the
  new Fail on every outpost). Now `Orc_Shaman`.
- `EnemyResolverRegression.cs:212-257` - **`artPendingModelKeys` DELETED, not emptied**; case (b) is now an
  unconditional failure = WO sec.2's durable half (every modelKey in `CommittedModels`, no opt-out).
- `EnemyArtCoverageRegression.cs:10-21,58-68` + `DataRegression.cs:1738-1755` - stale headers.

## ACCEPTANCE - honest
- [x] `ogre` resolves a real committed model (`Orc_Shaman` is in `EnemyResolver.CommittedModels:101`).
- [x] The modelKey-in-CommittedModels case exists, with no exemption list. RED proof = `Builds/reg-wave5c.log`.
- [ ] Capture of the ogre opened - NOT DONE; this lane has no Unity.
- [ ] Zero `ModelForEnemy: ... NOT in the committed` lines in a session - NOT PROVEN; needs a run.
- [ ] `REGRESSION_OK n/n` - **STILL BLOCKED, not by this WO.** That suite failed on TWO cases;
  `[binding-and-sentinel]` stays red (Demon, Necromancer, Skeleton_Golem, Skeleton_Minion, Troll, Troll_Mage,
  Troll_Overlord have no `.tripo-extracted` sentinel). Nearest owner WO-1509 (SPEC); none owns it outright.
  Do NOT add sentinels blind: a sentinel makes the postprocessor honour that FBX's own `externalObjects`, and
  those 7 bind by texture name today, so sentinel + empty remap = the WO-1509 capsule path.
