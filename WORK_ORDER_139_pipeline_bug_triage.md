# WORK ORDER 139 — Pipeline "Rare Bug" Triage (3-auditor sweep)

**Status: TRIAGE — fixes staged for deliberate application (NOT blind-patched)**
**Created:** 2026-05-30 (owner: "clean out the town… there [are] rare bugs in pipeline")
**Source:** three parallel read-only audits (hero/pet/combat · waves/spawn/init · HUD/camera/lifecycle).

> Principle: do NOT blind-patch ~20 changes to core combat/camera/wave logic while the game can't be
> playtested — that trades rare bugs for new ones. Apply the unambiguous can't-regress fixes; review the
> behavior-changing ones with the owner. ✅ = applied · 🔒 = safe, apply next · 👁️ = changes feel/behavior, review first.

## HIGH

1. 👁️ **Camera soup — dual follow-cams fight every frame.** `VillageCamera` + `SmartMobileCamera` both
   added to the SAME camera GO (`VillageSceneBuilder.cs:1869,1876`); both write transform in LateUpdate
   (`VillageCamera.cs:73`, `SmartMobileCamera.cs:160`). `EnforceSoleCamera` only disables other *cameras*,
   never a sibling MonoBehaviour. → the "camera drifts/jitters/pans on its own." **Fix:** in
   `SmartMobileCamera.EnforceSoleCamera`, also `GetComponent<VillageCamera>()?.enabled = false` (or builder
   adds only one). *Review: pick which camera is canonical.*
2. 👁️ **AudioListener leak on pet respawn.** `EnforceSoleCamera` loops disable rogue cameras but leave their
   AudioListeners; pets that die+respawn re-add a listener the one-shot strippers never re-run on
   (`VillageCamera.cs:122`, `SmartMobileCamera.cs:309`). → "2 audio listeners" + audio from wrong position.
   **Fix:** in both loops, `Destroy` AudioListeners on rogue cameras.
3. 🔒 **Boss spawn hardcodes `"spawn-0"`** (`WaveManager.cs:513`) → apex dragon never spawns if that id is
   missing. **Fix:** fall back to `_spawnPoints[0]` when the named point is null (apply to all batches).
4. 🔒 **WaveManager never unsubscribes enemy events** (no OnDisable; subs at `:502,683,568`) → stale
   callbacks into a torn-down manager on the breach→reload loop. **Fix:** `OnDisable` unsubscribes
   `Died`/`ReachedHeart`/dragon, clears `_liveEnemies`/`_liveApexBoss`.
5. 🔒 **EnemyGroupCoordinator `brain.Died` never unsubscribed** (`:68`); coordinator self-destructs 0.1s
   after release → MissingReference if a member dies after. **Fix:** unsubscribe in `ReleaseAll` before
   `Destroy`; null-guard `HandleMemberDied`.
6. ✅ **PlayerAttackController `_enemyLayer` defaults to 0 ("Nothing")** → runtime-built hero's melee hits
   nothing. **APPLIED:** Awake now defaults to `Enemy` layer then `~0` (matches HeroHealth/HeroAbilities).

## MED

7. 🔒 **EnemyBrain `TryAttack()` declared but never implemented/called** (`EnemyBrain.cs:94-99`) → brain
   enemies steer to the hero but deal no brain damage (hero only hurt by HeroHealth's own scan).
   👁️ *Feature gap — implement with care (balance); review.*
8. 🔒 **Enemy telegraph can latch `_telegraphing=true`** if killed mid-coroutine (`Enemy.cs:524,537`).
   **Fix:** reset `_telegraphing=false` + `StopAllCoroutines()` in `Die()`.
9. 🔒 **HeroLocomotion resolves WaveManager once in Start** (`:113`) → victory pose never wires if
   WaveManager spawns late. **Fix:** lazy retry while null.
10. 👁️ **HeroAbilitiesHudBridge pushes every frame with no HUD-bound guard** (`:94`) → 4×/frame
    `AbilityCatalog.Find` from frame 0; warning spam if catalog/HUD not ready. **Fix:** gate on
    `VillageHudController.IsBound` (`:1006`).
11. 👁️ **`EnsureHudReachable` force-sets overlay roots to `Ignore`** for ~2s after bind (`VillageHudController.cs:659`)
    → a panel opened in that window is click-through. **Fix:** only relax non-modal full-screen roots.
12. 🔒 **WaveSystemBridgeBootstrap `s_hooked` static not reset** (editor domain-reload-off) → bridges not
    attached on 2nd play. **Fix:** RuntimeInitialize resetter.
13. 🔒 **NPC injector vs TownsfolkController cache race** → controller caches soon-destroyed placeholders.
    **Fix:** injector re-acquires controller after inject.
14. 🔒 **Apex prefab load assumes `DragonBoss` on root** (`WaveManager.cs:543`). **Fix:** `GetComponentInChildren`.

## LOW
15. 🔒 SkyProgression sky color typo — green value in the blue channel (`SkyProgressionController.cs:160`).
16. 🔒 BeginLoop re-entrancy (no in-flight guard, `WaveManager.cs:439`). 17. 🔒 Enemy `ApplyWaveScaling` dead
    branch (`:340`). 18. 🔒 HeroHealth reflective GameOver `Show` unguarded by try/catch (`:120`).
19. 🔒 Group-spawn falls back to origin when spawn points empty (`WaveManager.cs:485`).

## Verified CLEAN (no action)
- Singleton-destroys-shared-host: all 38 `Instance` sites audited — the two on the shared hero
  (`HeroProgression`, `AttackTimingBonus`) correctly use `Destroy(this)`. No regression of the fatal pattern.
- HeroLocomotion/HeroHitReaction unsubscribe in OnDestroy. EnemyDamageable status timers are documented
  cosmetic no-ops (not a regression).

## Recommended order
Batch the 🔒 event-leak + null-guard fixes (3,4,5,8,9,12,13,14) — invisible, can't-regress — into one
compile-gated pass. The 👁️ camera/HUD ones change feel — do with the owner watching. Visual town cleanup
(KayKit/gates/steps) is a SEPARATE scene-rebuild task needing the owner's eyes + a Village.unity backup.
