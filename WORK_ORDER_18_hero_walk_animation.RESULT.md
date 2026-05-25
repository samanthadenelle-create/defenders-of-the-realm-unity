# WORK ORDER 18 — RESULT (BLOCKED — requires manual Mixamo round-trip)

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** Diagnosis driven to a definitive conclusion. The fix is **blocked on a manual/external step** (Mixamo round-trip or a Tripo re-export with animation) that an autonomous agent cannot perform. Per #35, documented here with a precise owner action list. **No code changed; no half-finished controllers left on disk.**
**Editor:** Unity 6000.4.8f1

---

## 1. Root cause — CONFIRMED (was "unknown" in the WO)

WO-18 §3.3 flagged it as unknown whether the hero FBXes actually contain NLA animation tracks. **They do not.** I ran `HeroAnimatorSetup.Setup` headlessly (batchmode `-executeMethod`) against all three heroes. Every one reported:

```
[HeroAnimatorSetup] Could not find two NLA clips inside 'Assets/Resources/Heroes/Mage.fbx'   — importer left untouched.
[HeroAnimatorSetup] Could not find two NLA clips inside 'Assets/Resources/Heroes/Knight.fbx' — importer left untouched.
[HeroAnimatorSetup] Could not find two NLA clips inside 'Assets/Resources/Heroes/Ranger.fbx' — importer left untouched.
```

`ConfigureFbxImporter` walks `importer.defaultClipAnimations` and needs two non-dupe NLA takes (Walk = longer, Cast = shorter). All three FBXes expose **fewer than two** → the Tripo "Send To Unity" NLA tracks were not preserved in these exports. **No `.controller` could be generated** (and none was — `Resources/Heroes/*.controller` count = 0). The controller-generation path (§4.2) is therefore impossible until the FBXes carry animation data; §4.4 (Mixamo round-trip) is the required route.

## 2. Secondary blocker — the runtime hookup is also broken (missing Wizard assets)

Even once per-hero controllers exist, they will not reach the runtime hero body as the code currently stands:

- `Assets/Models/Wizard/` is **empty** — `Wizard.fbx` and `Wizard.controller` are gone (the gitignored-`Assets/Models` / fresh-clone trap). `VillageSceneBuilder.BuildHero` builds the scene hero from `Assets/Models/Wizard/Wizard.fbx` + assigns `Wizard.controller`; both are now missing, so the scene's baked `HeroBody` Animator has a null/unresolved controller.
- `HeroBodySwapper` (which the WO forbids modifying) gets the new body's controller by **snapshotting the OLD (Wizard) body's `runtimeAnimatorController`** and reattaching it — it has **no fallback** to load `Resources/Heroes/<slug>.controller`. With the Wizard controller missing, that snapshot is **null**, so the swapped-in Mage/Knight/Ranger body is left with no controller → static, regardless of any controller generated in step 1.

So WO-18 has **two** gaps: (a) no animation data in the FBXes, and (b) no path for a generated controller to reach the runtime body.

## 3. What I did (autonomous portion)

- Confirmed on disk: 0 hero `.controller` files; `Resources/Heroes/{Mage,Knight,Ranger}.fbx` present; `Assets/Models/Wizard/` empty.
- Ran `HeroAnimatorSetup` headlessly for all three heroes → definitively established **no NLA tracks** (above). Importers were left untouched (verified: no `.fbx.meta` changes).
- Verified the runtime hookup gap by reading `HeroBodySwapper` (snapshot-only) + `BuildHero` (Wizard.fbx/controller source).
- Did **not** fabricate a fix: no clipless/empty controller was created (it would satisfy AC1's letter but animate nothing), no FBX was swapped to a KayKit rig (owner rolled that back — "use the paid-for Tripo models throughout"), and `HeroLocomotion`/`HeroBodySwapper` were not modified (hard rule).

## 4. Acceptance criteria status

| AC | Status |
|---|---|
| 1. `.controller` per hero on disk | ❌ **blocked** — FBXes have no animation data to build from |
| 2. WASD plays Walk / releasing → Idle | ❌ blocked (no controller + runtime hookup gap) |
| 3. Cast trigger plays Cast | ❌ blocked |
| 4. Build succeeds, replicates | n/a — last build is green (WO-08), but there's nothing new to verify in-build |
| 5. Commits per hero | n/a — no animation fix to commit |
| 6. This RESULT.md | ✅ |

## 5. Owner action list (to unblock)

1. **Supply animation data for each hero FBX** (the §4.4 route, manual/external):
   - Either re-export from Tripo with the two NLA tracks preserved, OR Mixamo round-trip: upload `Resources/Heroes/<Hero>.fbx` → Auto-rig → download Walk + Cast (the long + short clips) → re-import. Back up each FBX to `.fbx.bak` first (hard rule).
   - Then re-run the controller generation — either the menu items (`Defenders → Animation → Setup Wizard/Knight/Ranger Animator`) or a batch `-executeMethod` of `HeroAnimatorSetup.Setup(fbx, controller)` per hero. With NLA tracks present this will write `Resources/Heroes/<Hero>.controller` (Idle/Walk/Cast on Speed+Cast) — tracked, so it survives clones.
2. **Restore the runtime hookup** (pick one):
   - **(a)** Restore `Assets/Models/Wizard/Wizard.fbx` + `Wizard.controller` so the scene's baked hero body has a valid controller for `HeroBodySwapper` to snapshot; **or**
   - **(b)** *(needs owner sign-off — it touches the WO-18-protected `HeroBodySwapper`)* add a fallback: when `controllerSnapshot == null`, load `Resources.Load<RuntimeAnimatorController>("Heroes/" + slug)` (the generated per-class controller) and assign it. This is the more durable fix — it removes the dependency on the missing Wizard assets entirely. Recommended.

## 6. Note for the planner

This is the **third** instance of the gitignored-`Assets/Models` fresh-clone trap (after WO-05's hex material and WO-10's prefab gaps): the scene/build reference assets that live only in the un-tracked `Assets/Models`. The hero animation pipeline's dependence on `Models/Wizard/Wizard.controller` is fragile for exactly this reason — option 5.2(b) above (load the tracked `Resources/Heroes/*.controller`) would harden it.
