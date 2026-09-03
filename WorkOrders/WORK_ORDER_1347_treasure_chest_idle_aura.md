# WORK ORDER 1347 - The treasure chest shimmers while it waits to be opened

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Loot / reward presentation + VFX wiring of an owner-tagged key
**Type:** EXISTING object, ambient presentation ADDED
**Minted:** 2026-09-03 (CLI) from an owner tag.
**Severity:** P3 - polish, but it is the reward moment, which is the moment retention is bought.

## THE RULE THAT GOVERNS THIS TICKET

⛔ **The owner tags VFX keys in the Caster. The CLI maps key -> named hook VERBATIM and NEVER picks,
substitutes or rescales a prefab.** (Memory `vfx-map-owner-tags-no-creative-pick`.) She is **red/green
colourblind** - the shimmer may never carry meaning by hue.

## HER TAG, VERBATIM FROM `Assets/Editor/VfxManualPicks.json`

| key | prefabPath | isLoop | scale |
|---|---|---|---|
| `Treasure_Aura` | `Assets/Lana Studio/Casual RPG VFX/Prefabs/Loot/Loot_iddle.prefab` | **`false`** | 1.0 |

> *"treasure chest"*

⚠ `iddle` is the **pack's own typo** for *idle*. That is the real filename - do not "correct" it in a
path string or the load fails.

**Re-read the file before wiring** - it has changed repeatedly tonight and it always wins over this
table.

## MEASURED FROM THE PREFAB, NOT INFERRED FROM ITS NAME

| measured | value | consequence |
|---|---|---|
| ParticleSystems | **10** | it is a composite, not one emitter |
| `looping` | **7 looping, 3 one-shot** | a persistent shimmer punctuated by sparkle accents |
| `scalingMode` | `0` = **Hierarchy** on all 10 | `transform.localScale` scales it cleanly if a chest ever needs a different size |
| longest `lengthInSec` | `4` | see the conflict below |

⚠ **`scalingMode: 0` is `Hierarchy`, NOT `Local`** (Unity: `Hierarchy=0, Local=1, Shape=2`). Do not
re-derive it from memory and conclude the effect cannot be scaled.

## ⚠ THE LOOP CONFLICT - REPORT IT, DO NOT FIX HER TAG

Her tag says `isLoop: false`. The prefab's own systems mostly loop. **Honouring `false` literally means
the shimmer dies after its longest 4-second system and the chest sits DARK while it is still unopened**
- which is the opposite of what an idle loot effect is for.

**Honour `isLoop: false` as authored** and make the behaviour correct by driving the effect's lifetime
from the **chest's own unopened state** - which is the right owner of that lifetime regardless of the
flag. Then **report the conflict in one plain sentence** so she can retag in seconds.

⛔ **Do NOT edit her `isLoop` or `scale`, and do NOT write to `VfxManualPicks.json` at all.**

*(This is the FOURTH loop mismatch tonight - WO-1343, WO-1344, WO-1345 carry the same shape. Report,
never fix. WO-1343's agent is investigating whether the tagger is the cause.)*

## FIRST, ANSWER THIS FROM CODE - DO NOT ASSUME

**"Treasure chest" is not yet pinned to a system.** Before wiring anything, find and REPORT:

- Where treasure chests actually exist - raid/dungeon loot, a town reward, a quest reward, a
  daily/queue payout, or several of these.
- Whether they are a **world object** the player walks to and opens, or a **UI reward screen** element.
  ⚠ This decides everything: a world-space particle composite parented into a Canvas typically renders
  at the wrong scale or depth or not at all, and it will look like the tag simply failed.
- Their lifecycle: spawn -> idle -> opened -> collected -> despawn.

**Wire the aura to the IDLE (unopened) phase**, and make it **stop when the chest is opened** - a
shimmer still playing over an emptied chest reads as a bug and invites a second tap.

⚠ If chests exist in more than one place, wire the one you can prove and **report the others** rather
than guessing which she meant.

## The rest of the family - NAMED, NOT SUBSTITUTED

The same pack ships `Loot_drop.prefab`, `Loot_flicker.prefab` and `Loot_pick_up.prefab`. Those map
naturally onto the spawn and collect moments.

⛔ **Do NOT wire them. She has tagged exactly one key.** Name them in the RESULT as available so she can
tag them if she wants those beats - that is her creative call, not yours.

## Instrumentation

`FlowTrace`: key requested, prefab resolved or null, the chest and its state at the decision, the space
it was placed in (world vs canvas), resolved position, and spawn/despawn transitions.
⛔ Never strip FlowTrace (CLAUDE.md s12).

## Constraints

- ⛔ **Do not add a second spawner or a second pool.** If a chest already has an effect, REPLACE it and
  say what it was. One owner, one lifecycle (CLAUDE.md s7).
- ⛔ Never hand-edit a `.unity` scene. UXML does not work in builds. ASCII-only in player-facing strings.
- ⛔ **Do not modify `Loot_iddle.prefab` on disk** - it is a shared pack asset and editing it silently
  changes every other user of it. Adjust the spawned instance if anything needs adjusting.
- ⛔ Do not touch loot TABLES, drop rates, rewards or grants. This ticket is presentation only.
- Phone-first landscape; the effect must not cover a touch target (>= 112px).
- Do not run a Unity gate, do not commit, do not build. The lead does all three.

## ⛔ LIVE LANES - stay out

Same fence list as WO-1346, which you are implementing alongside this: **WO-1343** (auras, night store,
tunables, tagger investigation), **WO-1344** (FTUE pointer), **WO-1345** (AoE reticle) - all three have
live agents wiring owner-tagged VFX keys. ⚠ **Do NOT edit a shared VFX resolver, registry or spawner** -
report the collision to the lead. Also fenced: **WO-1342**, **WO-1341**, **WO-1340**, **WO-1339**,
**WO-1316**, **WO-1337**'s files, and the decimation lane.

## Acceptance

- [ ] Where treasure chests exist, in what space, and their lifecycle - answered from code with
      file:line. Others reported if there are several.
- [ ] The shimmer plays while a chest is UNOPENED and stops when it is opened.
- [ ] Whether a chest effect already existed, and what happened to it. Exactly one owner.
- [ ] The `isLoop: false` conflict reported in one sentence; her tag NOT edited.
- [ ] `Loot_iddle.prefab` unmodified on disk. Say so explicitly.
- [ ] The other three Loot prefabs named as available, and NOT wired.
- [ ] No prefab chosen, substituted or rescaled by the implementer. Say so explicitly.
- [ ] An oracle pins the key -> prefab mapping against `VfxManualPicks.json` and that the effect is
      gated on the unopened state. **Prove it RED first; report the mutation.**
- [ ] Brace + NUL check per `.cs` file.
- [ ] ⛔ **Owner felt-verifies on device and CLOSES.**
