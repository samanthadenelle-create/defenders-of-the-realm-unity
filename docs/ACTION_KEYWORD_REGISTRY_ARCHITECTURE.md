# Action Keyword Registry — Architecture (motion-castings.json)

**Status:** DESIGN — approved shape for WO-670 (Motion Caster) to write into and the
controller builders to read from.
**Minted:** 2026-07-11 (owner ask, verbatim: "create a separate tool stand alone where
i load in the model, and you load the rig with all the motion options, and I can select
what I want and tie it back to keyword that we can save to each type Enemy family or
player" + "then when we want a action we reference the keyword").
**Lens:** ARCHITECTURE_PRINCIPLES.md is binding — One Model (§2b: entries + composable
capabilities, systems are READERS, add by entry not by code), presentation never touches
objects (§2), tests are the permission gate (§2c), never silent (§5).

## 0. What this is, in One Model terms

This registry is the One Model applied to motion. **Targets** (enemy family or hero
class) are catalog **entries**; **keywords** (attack0, cast, taunt, death2 …) are the
composable **capability slots** an entry retains; every consumer — controller bakes,
abilities, AI brains, HUD buttons — is a **READER** that asks "what does entry X do for
keyword Y?" and never hardcodes per-type. Adding a new enemy family's full motion set
becomes **adding an entry**, not writing a builder.

It is a lookup table (JSON) over a thin interpreter, exactly the owner's data-structure
mental model, self-reporting through FlowTrace/LogWarning at every miss.

**One file, one name:** `motion-castings.json` — the name WO-670 already minted. This
document defines its shape and resolution semantics; WO-670's tool is the writer;
the builders are the first readers. Do NOT mint a second "action registry" file.

## 1. Data model — the file shape

Authoring copy: `Assets/StreamingAssets/Data/Canonical/motion-castings.json`.
**AMENDED 2026-07-11 (gate finding):** the DataRegression core-datahub oracle enforces
the dual-copy rule on EVERY canonical StreamingAssets file — so the
`Resources/Data/Canonical/` mirror ships from day one (byte-identical; WO-670's tool
writes BOTH). Runtime still doesn't read it in V1; Phase 2's runtime reader inherits a
mirror that already exists.

```json
{
  "version": 1,
  "_comment": "KEYWORD -> ACTION registry. Targets (enemy family | hero class) x keywords -> clip (+optional vfx/sfx). Owner picks are manual:true = CANON, never overwritten (Offset Forge law, WO-490). Editor-consumed (controller bakes) in V1 - no Resources mirror until a runtime reader exists; at that point the dual-copy rule (Resources copy WINS) applies.",

  "vocabulary": {
    "locomotion": ["idle", "walk", "run", "combatIdle", "combatWalk", "combatRun",
                    "injuredIdle", "injuredWalk", "injuredRun"],
    "attack":     ["attack0", "attack1", "attack2", "attack3", "heavy",
                    "skill1", "skill2"],
    "cast":       ["cast", "castChannel"],
    "reaction":   ["hit", "block", "parry", "dodge", "knockdown", "gettingUp"],
    "death":      ["death0", "death1", "death2", "death3", "death4", "death5"],
    "signature":  ["taunt", "unsheathe", "victory", "windup"]
  },

  "targets": {
    "humanoid": {
      "_comment": "root archetype default - the shared-rig baseline (AnimatorSetup families)"
    },
    "orc": {
      "inherits": "humanoid",
      "attack0": {
        "clip": "Assets/Action/Knight/standing melee combo attack ver. 1.fbx",
        "guid": "<unity-guid>",
        "vfxKey": "",
        "sfxId": "",
        "manual": true,
        "pickedUtc": "2026-07-11T18:00:00Z",
        "source": "motion-caster"
      }
    },
    "orc-mage":    { "inherits": "orc" },
    "orc-warrior": { "inherits": "orc" },
    "orc-tank":    { "inherits": "orc" },
    "hollow":      { "inherits": "humanoid" },
    "knight":      { "inherits": "humanoid" }
  }
}
```

Row fields:

| Field | Meaning |
|---|---|
| `clip` | **Primary reference: asset path** (FBX or extracted `.anim`). Human-readable, hand-diffable, matches every existing seam (builders load by path; weaponskill json names clips). The owner reads and reasons over this file — a GUID-primary file would be opaque to her. |
| `guid` | **Secondary/repair reference.** Written by the tool at save. If the path 404s at read, the loader tries `AssetDatabase.GUIDToAssetPath(guid)`, warns, and (in-editor) self-heals the path. GUIDs survive file moves; paths survive human reading. Both, with path primary, is the right call — also because a future runtime reader cannot use AssetDatabase GUIDs at all (Addressables keys are addresses, derived from the path/name). |
| `vfxKey` / `sfxId` | Optional paired effect keys — SAME namespace as `VFXManager.PlayKey` / HovlVfxCatalog keys (one vocabulary, no parallel key space). Data only — see §4 for who reads them. |
| `manual` | `true` = owner pick = CANON. **No code path ever overwrites a manual row** (Offset Forge / WO-490 law, principle §4). Auto/migration passes may only fill absent keywords or overwrite `manual:false` rows. |
| `pickedUtc`, `source` | Provenance: `motion-caster` (tool), `migrated-weaponskill`, `auto`. Debuggability + the manual-preservation gate needs to know who wrote what. |

### Inheritance / fallback chain

`inherits` gives single-parent inheritance, **max depth 3, cycle-guarded**:

```
orc-berserker → orc → humanoid → (registry exhausted) → builder's hardcoded default
```

Resolution per `(target, keyword)`:
1. Exact target row.
2. Walk `inherits` upward (≤3 hops; a cycle or over-depth is a load-time LogWarning + chain truncation).
3. Registry miss → **the calling builder's current hardcoded pick** (today's constants stay in the builders as the terminal default — that is what makes empty-registry byte-identical).
4. Every fall-through step self-reports: editor-side `Debug.LogWarning("[MotionCasting] miss 'orc-berserker.attack0' -> family 'orc' -> ...")`, runtime-side (Phase 2) `FlowTrace.Warn("MotionCast", ...)`. **A miss is never a silent T-pose** — if even the builder default is null the builder already warns-and-skips the state (existing behavior, e.g. KnightPackageControllerBuilder.Clip() at Assets/Editor/KnightPackageControllerBuilder.cs:676-683).

## 2. Keyword vocabulary — fixed contract, one source

**Closed vocabulary, not open strings.** Open strings are the VFX-two-stack scar in
data form: two agents invent `attackA` vs `attack_0` and consumers silently miss. The
vocabulary above is exactly the animator-contract keys the builders already imply
(idle/walk/run/attackN/heavy/skillN/cast/castChannel/hit/deathN/taunt/dodge/block/
parry/unsheathe/windup/victory + injured/knockdown from the Knight builder).

Where it's declared — ONE source, two views, one gate:
- **The `vocabulary` block in motion-castings.json is the declaration.** Categorized
  (locomotion/attack/cast/reaction/death/signature) using the Knight_Anim_Inventory
  taxonomy, so the Motion Caster's category chips and the melee/caster lint read
  straight from it.
- **`DeNelle.Core.Combat.ActionKeywords`** (new, Core, pure consts next to
  `AnimParams`) mirrors it for compile-time use by runtime consumers (Phase 2) and
  tests.
- **An EditMode test asserts JSON vocabulary == ActionKeywords constants** — the sync
  gate. Drift fails the gate, so there is effectively one source.

Save-time validation in the Motion Caster: an unknown keyword is a save ERROR; a
new keyword = version bump + vocabulary row + the one reader that consumes it
(add-by-entry discipline).

**Melee/caster rule encoded, not remembered:** the `cast` category keywords must bind
clips whose inventory taxonomy is Cast (docs/animations/Knight_Anim_Inventory.md rule:
cast-type actions fire cast clips, never swings). The tool lints this at save (override
requires an explicit confirm), and an EditMode test asserts no `cast`/`castChannel` row
resolves to an `atk_*`/`*Slash*`-taxonomy clip.

## 3. Resolution points — bake-time V1, runtime Phase 2 (tradeoff named)

**V1 (this design + WO-670): bake-time only.** A new editor-side reader,
`Assets/Editor/MotionCastings.cs`:

```csharp
// DeNelle.Editor — the ONE interpreter over motion-castings.json.
public static class MotionCastings
{
    // Resolve (target, keyword) through the inheritance chain; returns
    // builderDefault on miss. Logs "[MotionCaster] '<t>.<kw>' -> '<clip>' (manual)"
    // on a hit (the WO-670 acceptance log line) and a Warn on each fall-through.
    public static AnimationClip Resolve(string target, string keyword,
                                        AnimationClip builderDefault);
    public static bool TryGetRow(string target, string keyword, out CastingRow row);
    public static IReadOnlyList<string> Vocabulary { get; }
}
```

The three builders consume it (§5) when baking `.controller` assets. Empty/absent file
⇒ every `Resolve` returns `builderDefault` ⇒ **byte-identical outputs** (the
behavior-preserving gate).

**Phase 2 (later, deliberate — its own WO): runtime `ActionResolver`.** A Core service
(`CoreServices.Actions`) answering `keyword -> (trigger, variant)` for
HeroAbilities/Enemy/AI/HUD, and — with WO-545 per-family Addressables — `keyword -> clip`
applied via `AnimatorOverrideController` at load (the BuildOrcHumanoidController
override pattern, applied at runtime). That requires the Resources mirror + dual-copy
sync and moves failure to play-time (FlowTrace + the §5 watchdog discipline).

**The tradeoff, explicitly:**
- *Bake-time* (right for V1): deterministic, diffable `.controller` assets; zero
  runtime cost; testable headless via DataRegression; preserves the proven pipeline's
  baked craftsmanship (single-cadence authority, anti-chop crossfade bands, explicit
  blend thresholds — `useAutomaticThresholds=false` is load-bearing). Cost: a pick
  needs a rebake to be seen in-game, and all variants ship baked.
- *Runtime* (right later): per-family clip streaming (Addressables), data-driven swaps
  without rebakes, live A/B. Cost: override-construction at load, dual-copy burden,
  play-time failure modes, and a runtime re-encoding of transition/cadence knowledge
  that today lives safely in the builders.
- *Easy-vs-right:* the easy move is jumping straight to a runtime interpreter ("it's
  just a dictionary"). It isn't — the controllers encode tuned transition timing the
  interpreter would have to reproduce. Right is bake-time first, runtime when
  Addressables per-family makes it pay (leverage, §3 of the principles).

## 4. Action = animation + effects — where the join lives

The row **carries** optional `vfxKey`/`sfxId` (unified with the HovlVfxCatalog /
`VFXManager.PlayKey` key namespace and the abilities.json `vfxCast`/`vfxProjectile`/
`vfxImpact` keys from WO-614/8b70cda0) — but the registry is DATA; **nothing in the
registry or in gameplay objects plays anything.**

The join lives in the presentation layer, on the existing seams:
- **Hero abilities:** `abilities.json` per-ability vfx keys remain authoritative —
  the choke point is `HeroAbilities.PlayCastVfxKey` (Assets/_Modules/Village/Hero/
  HeroAbilities.cs:1199, called from `CastResolved` :595). An ability-driven cast
  NEVER also fires the keyword row's vfx (one owner per concern — the two-VFX-stack
  scar). Registry `vfxKey` applies only to non-ability actions.
- **Enemies / non-ability actions:** a thin Village-side presentation reader
  (precedents: `EnemyTypeVfxSet` cast/projectile/impact keys; `WeaponTrailController`
  subscribing to the Core-pure `ActorAnimator.AttackStarted` event,
  Assets/_Modules/Core/Combat/ActorAnimator.cs:53). Phase 2's binder subscribes to
  that event, resolves the acting entry's keyword row, and `VFXManager.PlayKey`s the
  key. Gameplay code path untouched; presentation observes.
- V1 ships rows motion-only (`vfxKey` empty); the columns exist so the Motion Caster
  can grow "pick the paired effect" without a schema break.

## 5. Consumer seams — first keyword references (all additive)

| Call site | Change |
|---|---|
| `Assets/Editor/KnightPackageControllerBuilder.cs` — clip consts :83-103, `SpellCastClips` :108-115, `ResolveComboClips` :426, death table :349-354 | Each hardcoded pick becomes `MotionCastings.Resolve("knight", "<keyword>", <currentConst>)`: attack0-2, cast, skill1/skill2 (Cast_q..r slots), hit, death0-5, unsheathe, victory, block, injured*, knockdown/gettingUp. |
| `Assets/Editor/BuildOrcHumanoidController.cs` — shared consts :31-46, role consts :49-64 | Base states resolve target `orc`; the three overrides resolve `orc-mage` / `orc-warrior` / `orc-tank` (which `inherit: orc`). Keywords: idle/walk/run, combatIdle/Walk/Run, attack0, cast, windup, hit, death0, injured*. |
| `Assets/Editor/AnimatorSetup.cs` — `FindClip` keyword search :325-332 | Registry lookup FIRST per archetype target (`hollow`→HumanoidEnemy, `largebody`→LargeEnemy, `boss`, `hero-legacy`, `pet`, `npc`), keyword-search fallback unchanged. |
| Runtime, Phase 2 only | `HeroAbilities.CastResolved` variant pick (HeroAbilities.cs:534-576) asks ActionResolver; `Enemy.cs` `_actor?.PlayAttack/PlayCast` sites (:460-464, :1416, :1466, :1563) gain death/attack VARIETY by keyword (death0..N random pick per family) — after WO-623 (ActorAnimator-only drive) lands, so there is one drive path to extend. |

Every change is behavior-preserving: registry empty ⇒ today's output byte-identical.

## 6. Permission-gate tests (§2c)

EditMode (extend `Assets/Tests/EditMode` / `Data/Tests` patterns; headless-runnable):
1. **Schema/vocabulary sync:** motion-castings.json parses; every target row keyword ∈
   vocabulary; vocabulary == `ActionKeywords` consts; `inherits` chains acyclic, ≤3.
2. **Empty-registry parity:** bake each builder with the file absent vs `{}` — hash of
   the serialized `.controller` identical (DataRegression-style marker:
   `MOTIONCAST_PARITY_OK` / `_FAIL`).
3. **Manual-row preservation:** run the auto/migration pass over a fixture with a
   `manual:true` row → row byte-identical after (the Offset Forge law, mechanized).
4. **Fallback chain:** fixture `orc-berserker` missing `attack0` → resolves the `orc`
   row; missing everywhere → builder default AND `LogAssert.Expect(LogType.Warning, …)`
   proves the miss self-reported (never-silent gate).
5. **Cast-keyword lint:** no `cast`/`castChannel` row resolves to an attack-taxonomy
   clip (the melee/caster rule as a test).
6. **Dual-copy sync** (activates with Phase 2 only): StreamingAssets copy ==
   Resources copy, mirroring the existing catalog dual-copy gate.

## 7. Migration — weaponskill-animations.json

**Recommendation: parallel seam in V1; fold in Phase 2.** They answer different
questions — weaponskill-animations.json is the **ability join table**
(ability/skill → trigger + combo + clip; read by the runtime at cast-time AND by the
builder via `JsonClipToPackage`), while motion-castings is the **target action table**
(target × keyword → clip). Folding now conflates the two axes and touches runtime
readers (HeroAbilities/BattleController) mid combat-pivot for zero player-felt gain.

Phase 2 end-state ("when we want an action we reference the keyword"):
1. A one-shot migrator emits `manual:false, source:"migrated-weaponskill"` registry
   rows for the clip halves (knight combo rows → `attack1`/`attack2`, cast rows →
   `skill1`/`skill2`).
2. weaponskill-animations.json rows shrink to ability → **keyword** references
   (`"anim": "skill1"`) — the clip column retires; abilities.json ability defs
   eventually carry the keyword directly, next to their vfx keys.
3. Owner re-picks in Motion Caster flip rows to `manual:true` and become canon.

## 8. WO-670 tool write contract (what Motion Caster must honor)

- Writes ONLY `Assets/StreamingAssets/Data/Canonical/motion-castings.json`, shape §1.
- Never overwrites `manual:true`; owner picks always save `manual:true` + `pickedUtc`
  + `source:"motion-caster"` + both `clip` path and `guid`.
- Validates keyword ∈ vocabulary and the cast-category lint at save.
- Target list = enemies.json `family` values + hero class ids (knight, mage, ranger,
  cleric) + the archetype roots (humanoid, largebody) — read from data, not hardcoded.
- Logs `[MotionCaster] '<target>.<keyword>' -> '<clip>' (manual)` on save and the
  builders log the same line on consume (the WO-670 acceptance line).

## 9. Risks (top 3)

1. **Three sources of truth during transition** (registry / builder consts /
   weaponskill json). Mitigation: fixed resolve order (registry → builder default),
   provenance + hit/miss logging on every resolve, parity test #2.
2. **Vocabulary drift / open-string sprawl** (the data-shaped VFX-two-stack failure).
   Mitigation: closed vocabulary, save-time validation, sync test #1.
3. **Retarget gap:** an owner-picked clip that isn't extracted/retargeted for the
   target's avatar bakes a dead state. Mitigation: Motion Caster previews through the
   REAL retarget (WO-670 §4), the loader warns when a picked FBX clip isn't
   Humanoid-compatible with the target family's rig, and misses always fall back
   loudly (never a T-pose).
