# WORK ORDER 1346 - The Arcane Tower gets a soft ambient aura, and only once it is actually built

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Structure presentation + VFX wiring of an owner-tagged key
**Type:** EXISTING structure, ambient presentation ADDED (and very likely REPLACING an existing one)
**Minted:** 2026-09-03 (CLI) from an owner tag and two words of spec.
**Severity:** P3 - ambient polish. Low risk, high visibility: the tower is a permanent fixture the
player looks at constantly.

## THE RULE THAT GOVERNS THIS TICKET

⛔ **The owner tags VFX keys in the Caster. The CLI maps key -> named hook VERBATIM and NEVER picks,
substitutes or rescales a prefab.** (Memory `vfx-map-owner-tags-no-creative-pick`.) She is **red/green
colourblind** - the aura may never carry meaning by hue.

## HER TAG, VERBATIM FROM `Assets/Editor/VfxManualPicks.json`

| key | prefabPath | isLoop | scale |
|---|---|---|---|
| `ArcaneTower_Aura` | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_electric.prefab` | **`true`** | 1.0 |

> *"arcane tower vfx (after built) softly"*

⭐ **`isLoop: true` - the FIRST correct loop flag of the evening**, and the right one for an ambient
aura. No loop conflict to report on this ticket (contrast WO-1343, WO-1344, WO-1345).

**Re-read the file before wiring** - it has changed under us repeatedly tonight and it always wins over
this table.

## HER SPEC IS FIVE WORDS, AND BOTH HALVES ARE BINDING

### "(after built)" - the aura is gated on the BUILT state

It must **NOT** play:
- while the structure is under construction / on a scaffold,
- while the build job is sitting in the **Obsidian queue** (Builder/Train/Research - the single home for
  all timed work),
- on a ghost/preview placement in **build mode** (strategic placement is always on and structures are
  movable, so a preview can exist at any time).

It **MUST**:
- begin when the build completes,
- be present on a **reload** for a tower that was already built - ⚠ **this is the case most likely to be
  missed**: wiring it to the build-completion EVENT alone gives an aura that vanishes on every relaunch
  until the player builds another tower. Drive it from the tower's **state**, not only from the event.
- **tear down when the tower is destroyed.** A destroyed structure is not repaired - it is rebuilt fresh
  at full cost - and its `Destructible` owner is responsible for tearing down its VFX
  (memory `destroyed-items-no-rebuild-full-cost-and-vfx-cleanup`). A surviving aura over an empty plot
  is the exact failure that memory exists to prevent.
- Behave correctly across an **upgrade**. Report what happens at level change; do not stack one aura per
  level.

### "softly" - subdued intensity, and it is a TUNABLE, not a constant

> **Owner standing ruling, repeated today:** *"be smart, dont make it need a code change, make it
> tweakable from a db call"* / *"i have been screaming this for months."* **Default answer: YES.**

"Softly" is HER instruction about intensity - so honouring it is following her tag, not overriding it.
But **the exact value is a judgement she has not made yet**, and she will only make it looking at her
phone. So:

- Express softness through an **intensity/alpha multiplier on the spawned INSTANCE**, defaulted to a
  subdued value and **exposed as a remote tunable** so she can dial it in ~40 seconds instead of a
  ~30-minute rebuild.
- ⛔ **Do NOT modify `Fog_electric.prefab` itself.** It is a shared pack asset; editing it changes every
  other user of it, silently. Adjust the instance.
- ⛔ Do not change her `scale: 1.0`, and do not write to `VfxManualPicks.json` at all. Read it freely.

**The tunables invariant:** no row, no network, or a parse failure MUST yield the built-in soft default
exactly. **SIX sources must change together** for a new key (registry, service, table, `TUNABLE_KEYS`
allowlist, and the Command Center editor + its schema) - enumerate all six in the RESULT. Surface it in
the **Command Center**: *"should be in command center so you dont need to be a rocket scientist."*

## ⛔ THE SECOND-SPAWNER RISK IS REAL AND NAMED

`VfxManualPicks.json` **already contains two Arcane Tower VFX keys**:

```
AuraOverArcaneTower_Aura
FireFromTower-ArcaneTowerLevel3_Aura
```

**Find what they are wired to BEFORE you add anything.** If `AuraOverArcaneTower_Aura` already drives an
ambient aura on this tower, her new tag **REPLACES** it - it does not join it. Two ambient auras on one
structure is the "one owner, one lifecycle" rule broken (CLAUDE.md s7), and it will read as a muddy
double effect that no amount of "softly" fixes.

⛔ **Do not add a second spawner or a second pool.** Report what each of those two keys drives and what
you did with them.

⚠ `FireFromTower-ArcaneTowerLevel3_Aura` is level-gated firing VFX - a **different concern**. Do not
disturb it.

## Instrumentation

`FlowTrace`: key requested, prefab resolved or null, the tower and its build state at the moment of the
decision, the applied intensity, and spawn/despawn transitions. **A missing VFX and a genuinely subtle
VFX are indistinguishable without this** - and this one is *authored* to be subtle, which makes the
ambiguity permanent unless the trace resolves it. ⛔ Never strip FlowTrace (CLAUDE.md s12).

## ⛔ LIVE LANES - stay out

- **WO-1343** (agent live): night-store aura, `Aura_*` tunable rotation, tree-foot + boss-death unbound
  hooks, `KnightShieldBash_Impact`, the tagger investigation.
- **WO-1344** (agent live): the FTUE pointer replacing the yellow Glow highlight.
- **WO-1345** (agent live): the AoE targeting reticle.
- ⚠ **FOUR of you are now wiring owner-tagged VFX keys simultaneously. Do NOT edit a shared VFX
  resolver, registry or spawner** - report the collision to the lead instead of editing it.
  ⚠ **WO-1343 is also authoring tunables.** If a tunables registry or the Command Center editor needs a
  new row, **report it rather than editing the same file** - two agents adding keys to one registry is a
  merge conflict at best and a silently dropped key at worst.
- **WO-1342**: `HeroSkillTreePanelMvvm.cs`, `SkillsPanelLayoutRegression.cs`, `hero-talents.json` twins.
- **WO-1341**: `PlayerDeckWorkspace.cs`, `HudLabelFitRegression.cs`.
- **WO-1340**: `tutorial-steps.json` + tutorial registry. **WO-1339**: `BOARD.html`,
  `tools/board_build.py`, `tools/owner_validations.py`, `proof/owner-validations.json`.
- **WO-1316**: `tools/web-ship.ps1`, `tools/command-centre.ps1`.
- **WO-1337**: `Enemy.cs`, `BattleArena.cs`, `PanelManager.cs`, `BattleQuiescenceGate.cs`.
- Decimation: `Assets/HeroContent`, hero FBX + `.meta`.

## Constraints

- ⛔ **Never hand-edit a `.unity` scene.** Runtime injectors or the sanctioned builder method only.
- ⚠ **`RepoProps.MaxStructureLevel = 6` is the SINGLE ceiling** - never re-hardcode a level ceiling if
  level enters your logic.
- UXML does not work in builds. ASCII-only in player-facing strings. Phone-first landscape.
- Do not run a Unity gate, do not commit, do not build. The lead does all three.

## Acceptance

- [ ] The aura plays on a BUILT Arcane Tower and on a reload of an already-built one.
- [ ] It does NOT play during construction, in the queue, or on a build-mode preview.
- [ ] It tears down on destruction. Behaviour across upgrade reported; no per-level stacking.
- [ ] What `AuraOverArcaneTower_Aura` drives, and what you did with it. **Exactly one ambient aura owner
      on this tower.**
- [ ] Intensity is subdued by default and dialable by tunable; SIX sources enumerated; the no-row
      invariant proven.
- [ ] `Fog_electric.prefab` is **unmodified on disk**. Say so explicitly.
- [ ] No prefab chosen, substituted or rescaled by the implementer. Say so explicitly.
- [ ] An oracle pins the key -> prefab mapping against `VfxManualPicks.json`, that the aura is gated on
      built state, and the no-row invariant. **Prove it RED first; report the mutation.**
- [ ] Brace + NUL check per `.cs` file.
- [ ] ⛔ **Owner felt-verifies on device and CLOSES** - "softly" is a judgement only she can make, and
      the tunable is there so her verdict costs a knob, not a rebuild.
