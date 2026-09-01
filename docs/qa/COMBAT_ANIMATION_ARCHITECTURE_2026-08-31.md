# Mobile Combat Animation Architecture — 2026-08-31

Status: implementation anchor. Scope: live `BattleArena`, dungeon, outpost, wave, and camp enemies.
The deterministic ATB engine remains intact and is not the live presentation target.

## Evidence-backed RCA

- `EnemyBrain` already supplies Rush, Flank, Kite, Retreat, Suppressed, and Reposition tactics.
  `EnemyGroupCoordinator` also assigns distinct pincer bearings. Replacing this with a new behavior
  tree would destroy working value.
- Contact attacks wait on a telegraph timer, then apply damage before starting `PlayAttack()`. The
  visible weapon therefore does not own the hit. Extracted motion clips currently have no reviewed
  `HitFrame` events.
- Ranged attacks are the better precedent: they root, telegraph, release a visible projectile, and
  apply damage on arrival.
- Purchased ActorCore stock is under-connected: defensive reactions, strafes, back movement, and
  authored chains exist, while the live surface is predominantly one-dimensional locomotion.
- Camps use authored points but do not yet express roles, reservations, facing sectors, actions, or
  alert return behavior. Dungeon darkness ambushes choose a point near the hero, not occupancy slots.

## Architecture ruling

1. Preserve `Enemy`, `EnemyBrain`, the family/formation layer, target resolver, and deterministic ATB.
2. Add one cheap attack-token authority per target. Boss/elite encounters allow one committer;
   ordinary packs default to two. Enemies without a token continue their existing tactical movement.
3. Contact attacks use explicit phases: Telegraph, Commit, Recover. Entry range is checked before
   Telegraph; Commit does not repeatedly distance-gate the animation.
4. Damage is consumed exactly once by `HitFrame`. Until every shipped clip has a reviewed event, a
   short fallback marker keeps legacy controllers functional and reports the fallback path.
5. A hit reaction before `HitFrame` cancels the pending blow and releases its token. Recover remains a
   committed, vulnerable no-attack window.
6. ActorCore/AccuRIG ingestion is manifest-driven and dry-run-first. NavMesh or root motion owns
   translation, never both. No guessed event frames and no recursive FBX rewriting.
7. Camps progress to authored occupancy slots: stable id, role, allowed action/archetype, facing/look
   sector, clearance/NavMesh proof, reservation/fallback, and alert exit/return.

## Delivery waves

- Foundation (this change): token broker, event relay, contact phase synchronization, interrupt and
  recovery lifecycle, pooled-object cleanup, regression assertions.
- Content connection: audit every enemy controller/clip; author reviewed `HitFrame`; map telegraph,
  commit, recover, strafe/backstep, directional hit, and death per archetype.
- World presence: occupancy-slot component and camp/ambush integration; sentry, prowler, pack-left,
  pack-right, ambush, and interaction routines.
- Mobile polish: 4 Hz decision ticks where perception permits, animator culling audit, bounded pack
  budgets, camera-distance readability capture, 30/60 fps device profiling.

## Acceptance gates

- A pack cannot all enter Commit on the same frame.
- Damage and impact occur on the weapon/contact frame; fallback use is observable until eliminated.
- A pre-contact hit reaction cancels the swing and frees the token.
- Recover prevents immediate repeat attacks.
- Every world enemy has a readable pre-aggro job and a valid occupancy slot.
- Representative Seeker capture passes silhouette, telegraph, reaction-time, and frame-budget review.

## Measured baseline (Unity CLI, 2026-08-31)

`DeNelle.Editor.CombatAnimationPipelineAudit.Audit` scanned the imported project without changing or
reimporting purchased sources:

- 365 model assets inspected;
- 342 non-bind animation clips inspected;
- 27 melee attack-like clips identified after excluding cast/projectile motions;
- 9 of those melee clips are dependencies of live enemy Animator Controllers;
- 9/9 live-controller melee clips now contain exactly one reviewed `HitFrame` event;
- each live event was measured on the actual Orc Humanoid retarget using a 1% right-hand velocity
  scan and visually checked in a marked, fine-grained contact sheet;
- 0 invalid Humanoid source-avatar/import failures in the scanned motion library.

Machine-readable evidence: `Builds/combat-animation-audit.json`; review evidence:
`Builds/combat-animation-contact-sheets/`; checked-in authority:
`Assets/Editor/CombatAnimationHitFrames.json`. Unused library stock remains warning-only. The runtime
fallback inspects the active/next clip and waits until after a reviewed event; it cannot pre-empt long
combo contacts. Event-preservation in `ActionClipImporter` prevents reimports from deleting authored
events.
