**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 140 — Hero Animator Factory (Mixamo clips → per-class controllers)

**Status: READY TO IMPLEMENT**
**Priority:** High — heroes have real motion clips now (`Assets/Action/`) but no controller wires them; without this the hero is still a sliding statue.
**Created:** 2026-05-30
**Lane:** Animation / editor-tooling — **`Assets/Editor/` only. Does NOT touch `VillageSceneBuilder.cs`** (that file is frozen by owner; this WO is unaffected).
**Implemented by:** UI (writes the `.cs`, runs the brace gate, signals ready). **CLI compile-verifies + sole-commits.**
**Depends on:** `ActionClipImporter.cs` (already in tree — imports `Assets/Action/` FBX as Humanoid, in-place). Editor must be closed for CLI's headless compile-verify.

---

## Goal

Build a single editor utility — **`HeroAnimatorFactory`** — that composes a real
`AnimatorController` for each hero class (Knight / Ranger / Mage) from the Mixamo
clips in `Assets/Action/`, and writes them to `Assets/Resources/Heroes/<slug>.controller`
so `HeroBodySwapper` loads them at runtime with zero further wiring.

This **replaces** the obsolete `HeroAnimatorSetup.cs` (Tripo-era: it scraped two
generic NLA takes per FBX into an Idle/Walk/Cast stub — there are now 36 dedicated
clips to use instead).

---

## The runtime contract (DO NOT INVENT NEW PARAMETERS)

The factory must author **exactly** the animator parameters the runtime already
drives — verified in code, 2026-05-30. Authoring any other name leaves a dead state:

| Parameter | Type | Driven by | Used for |
|---|---|---|---|
| `Speed` | Float | `HeroLocomotion.cs:236` (`SetFloat`, = velocity magnitude) | Idle ↔ Walk ↔ Run blend |
| `Cast` | Trigger | `HeroAbilities.cs:227` (`SetTrigger` on any ability) | attack / cast |
| `Victory` | Trigger | `HeroLocomotion.cs:137` (`SetTrigger` on win) | victory pose |

- Controllers load via `Resources.Load<RuntimeAnimatorController>("Heroes/" + slug)`
  where slug ∈ `{ "Knight", "Ranger", "Mage" }` (`HeroBodySwapper.cs:95`,
  `SlugFor` at :298). **Output paths are fixed:** `Assets/Resources/Heroes/Knight.controller`,
  `.../Ranger.controller`, `.../Mage.controller`.
- `HeroBodySwapper` already null-guards a missing controller (logs, no crash) — so a
  partial build (missing clips) must degrade gracefully, never throw.

---

## Clip → state mapping (from `Assets/Action/`, 36 Mixamo FBX)

All clips are Humanoid + in-place (per `ActionClipImporter`). Load each
`AnimationClip` sub-asset by FBX name. **Null-guard every clip** — packs may be
partially imported; a missing clip = skip that state + `Debug.LogWarning`, never error.

**Shared (all three classes):**
| State | Clip FBX | Loop | Notes |
|---|---|---|---|
| `Idle` (default) | `standing idle 01` (fallback `Standing Idle 03`) | yes | default state |
| `Walk` (locomotion) | blend: `standing walk forward`/`back`/`right`/`Standing Walk Left` | yes | see blend tree below |
| `Run` | blend: `standing run forward`/`back`/`left`/`right` | yes | high end of Speed |
| `Victory` | `Joyful Jump` (placeholder until a cheer clip exists) | no | `Victory` trigger |
| `Death` | `Falling Forward Death` (fallback `Dying`/`Defeated`) | no | optional; no trigger drives it yet — author state, leave unwired |

**Per-class `Cast`/attack state** (the `Cast` trigger target):
| Class | Cast clip | Source FBX |
|---|---|---|
| Knight | melee swing | `Sword And Shield Attack` |
| Ranger | (no bow clip yet) | fallback `Standing 2H Magic Attack 01`; warn it's a placeholder |
| Mage | spell cast | `Spell Cast` (fallback `Standing 1H Magic Attack 03`) |

> Ranger has no Mixamo bow clip in `Assets/Action/` (the Longbow pack extracted as
> generic-named locomotion only). Use the magic-attack placeholder and `LogWarning`
> so it's visible, not silently wrong. Flag for a future bow-clip pass.

### Locomotion as a 1-D blend tree (recommended)
Single `Walk`-style **blend tree state** keyed on `Speed` is cleaner than discrete
Idle/Walk/Run transitions and matches how `HeroLocomotion` feeds a continuous value:
- `0.0` → Idle clip
- `~0.1–3.5` → walk-forward clip
- `> 3.5` → run-forward clip

(Directional strafe clips are available but `HeroLocomotion` only feeds scalar
`Speed`, not a 2-D vector — so author the 1-D forward blend now; leave the strafe
clips for a later 2-D pass. Note this in the RESULT doc.)

---

## API shape — data-driven spec, ONE build algorithm (not a meta-factory)

Architecture decision (owner-ratified 2026-05-30): **do NOT build a recursive
"factory of factories."** With only ~4 animator families (hero, humanoid-enemy,
large-enemy, dragon) that is over-abstraction — animator authoring is a flat
state-list + blend tree, there is no recursive sub-problem to exploit, and the
existing `EnemyAnimatorFactory` already proves a clean switch-based family pick.

Instead: **parameterize by a spec object.** One generic `Build(spec, path)` method
consumes a per-family data spec. Heroes = 3 specs; enemies = 4 specs; a future
catalog creature = author a new spec, no new code. This is the shape that scales by
DATA, and it sets up a later refactor that lifts `HeroSpec` into a shared
`AnimatorBuildSpec` and unifies hero + enemy factories under one builder — paid for
only when the catalog actually needs it (separate future WO, NOT this one).

```
namespace DeNelle.Editor
{
    // Per-family data. Hero today; generalizes to AnimatorBuildSpec later.
    public struct HeroSpec
    {
        public string slug;            // "Knight" | "Ranger" | "Mage"
        public string controllerPath;  // Resources/Heroes/<slug>.controller
        public string castClipFbx;     // per-class attack clip (see map)
        // shared clip set (idle/walk/run/victory/death) resolved by the builder
    }

    public static class HeroAnimatorFactory
    {
        [MenuItem("Defenders/Animation/Build Hero Animators (Mixamo)")]
        public static void BuildAll();          // iterates the 3 HeroSpecs, Save+Refresh

        // -executeMethod target for CLI headless verify/build:
        //   DeNelle.Editor.HeroAnimatorFactory.BuildAll
        public static void Build(HeroSpec spec); // ONE algorithm, data-driven
    }
}
```
- Keep `Build` **spec-driven, not class-specific** — the only per-hero variation is
  the spec's data (slug, paths, cast clip). No `if (knight) … else if (mage)` inside
  the build algorithm; differences live in the 3 spec literals. This is what makes
  the later unification a data move, not a rewrite.
- Use `UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath`,
  `AddParameter`, `stateMachine.AddState`, `AddTransition`, `CreateBlendTreeInController`.
- Load clips with `AssetDatabase.LoadAllAssetsAtPath(fbxPath)` → filter `AnimationClip`.
  Clip name inside a Mixamo FBX is usually `mixamo.com` or the take name — match by
  the **FBX file** (one clip per file here), not by internal clip name.
- Idempotent: `AssetDatabase.DeleteAsset(controllerPath)` then recreate (mirrors
  `HeroAnimatorSetup.BuildController`).

---

## Files

| File | Action |
|---|---|
| `Assets/Editor/HeroAnimatorFactory.cs` | **CREATE** — the factory (this WO) |
| `Assets/Editor/HeroAnimatorSetup.cs` | **DELETE** after factory verified (obsolete Tripo stub) — or leave dormant; CLI's call |
| `Assets/Resources/Heroes/{Knight,Ranger,Mage}.controller` | regenerated output (committed) |

**Do NOT touch:** `VillageSceneBuilder.cs` (frozen), `HeroLocomotion.cs`,
`HeroAbilities.cs`, `HeroBodySwapper.cs` (they define the contract — match it, don't
edit it), `Village.unity`, any FBX import settings owned by `ActionClipImporter`.

---

## Acceptance criteria

- [ ] `HeroAnimatorFactory.BuildAll` runs headless (`-executeMethod`) with no exceptions.
- [ ] Three controllers written to `Resources/Heroes/{Knight,Ranger,Mage}.controller`.
- [ ] Each controller declares params **exactly** `Speed` (float), `Cast` (trigger),
      `Victory` (trigger) — names matched to the runtime hashes.
- [ ] Default state = Idle; Speed drives Idle→Walk→Run; `Cast` trigger plays the
      class attack; `Victory` trigger plays the victory pose.
- [ ] Every clip lookup null-guarded — a missing Mixamo FBX logs a warning and skips
      that state; the build still completes and writes a valid controller.
- [ ] Brace-balance check passes on `HeroAnimatorFactory.cs`.
- [ ] `HeroBodySwapper` loads each controller at runtime (verify: hero walks + casts,
      no "sliding statue", no "No controller at Resources/Heroes/…" warning).
- [ ] Clips animate the rig (Humanoid retarget working — confirms `ActionClipImporter`
      set them Humanoid; if bones don't move, the FBX is still Generic → fix import first).

## Done checklist (CLAUDE.md §10)

- [ ] Brace gate passed on every `.cs` created/edited
- [ ] No `.unity` hand-edited; no `VillageSceneBuilder.cs` edit
- [ ] Runtime param names match `HeroLocomotion`/`HeroAbilities` (Speed/Cast/Victory)
- [ ] Ranger placeholder-attack warning present (no bow clip yet) — logged, not silent
- [ ] `WORK_ORDER_140_hero_animator_factory.RESULT.md` written when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
