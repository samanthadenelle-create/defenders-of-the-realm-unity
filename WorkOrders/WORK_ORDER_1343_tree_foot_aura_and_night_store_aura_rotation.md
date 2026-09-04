# WORK ORDER 1343 - The Tree of Life gets a second aura at its foot, and the Night Store's aura re-rolls in town

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T14:37:26, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-09-03 - shipped in 2026.09.03.353999. Night Store aura replaced (old owner: store.beacon.near -> Marker8_SafeZoneLoop, kept reachable as mode 3 so undoing is one row). FOUR selectable modes on remote tunables - starfall (default) / loot flicker / Aura_* rotation / legacy ring - plus cadence and family mask, switchable with no rebuild. KnightShieldBash_Impact wired to knight.shield-bash per her direct confirmation. HELD pending her retag: atfootprintoftree_Aura, atfootprintoftree_Impact, EliteDeath_Impact - the tagger authored those without her. Tree-foot and boss-death hooks built UNBOUND. Tagger root cause found: VfxCasterWindow.TagSelected reads the key from a never-cleared TextField and the prefab from the live selection - two fields never captured together, and it overwrites an existing key with no diff, warning or confirmation.
**Silo / Lane:** VFX wiring (owner-tagged keys -> named hooks) + a tunable selection policy
**Type:** EXISTING VFX assets, NEWLY TAGGED by the owner. One new selection policy.
**Minted:** 2026-09-03 (CLI) from three owner tags plus a follow-up creative question.
**Severity:** P3 - presentation. It is on the board tonight because the assets are already tagged and
the wiring is mechanical; the only open question is a creative one that this ticket deliberately
defers to her without blocking.

## THE RULE THAT GOVERNS THIS WHOLE TICKET

⛔ **The owner tags VFX keys in the Caster. The CLI maps key -> named hook VERBATIM, and NEVER picks or
substitutes a prefab.** An un-tagged hook is HELD, not filled with a plausible guess.
(Memory `vfx-map-owner-tags-no-creative-pick`.) She is also **red/green colourblind** - never ask her to
choose between effects by hue, and never let an effect carry meaning by colour alone.

## HER TAGS, READ VERBATIM FROM `Assets/Editor/VfxManualPicks.json`

| key | prefabPath | isLoop | scale |
|---|---|---|---|
| `atfootprintoftree_Aura` | `Assets/Spells Pack/Particles/Prefabs/Auras/Aura_Nature.prefab` | `true` | 1.0 |
| `NightStoreoption_Aura` | `Assets/Resources/VFX/Aura/top_down_starfall_line_blue.prefab` | **`false`** | 1.0 |
| `TreeofLifeAura_Aura` *(pre-existing)* | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab` | `true` | 1.0 |

**Verify these are still what the file says before you wire anything.** If a path has changed, the file
wins over this table - a number or path copied into a second doc is this repo's most expensive recurring
bug (CLAUDE.md s2, s5, s16).

## ASK 1 - the foot of the Tree of Life

> *"one for the foot of the tree of life, to go with the other one"*

⭐ **"To go with" means BOTH PLAY. This is ADDITIVE, not a replacement.** `TreeofLifeAura_Aura`
(FireFlies) stays exactly as it is; `atfootprintoftree_Aura` (`Aura_Nature`) is a SECOND aura seated at
the **base** of the tree. If you find yourself removing or re-pointing the FireFlies hook, you have
misread the ask.

- The Tree of Life is the **Heart of Elarion** - the world tree / stone reliquary at scene centre
  `(0,0,0)` (CLAUDE.md s7). Its damageable owner is `HeartController`.
- Seat the new aura at the **foot / base footprint**, not at the canopy or the trunk mid-point. Her key
  is literally `atfootprintoftree`. Ground level, at the base radius.
- It **loops** (`isLoop: true`), so it is ambient and continuous - it is not triggered by an event.
- ⚠ Read the existing FireFlies hook's mount point and lifecycle first and follow the SAME pattern.
  Do not invent a second spawner alongside it - one owner per presence is a hard lesson in this repo
  (CLAUDE.md s7, `EchoWorldPresence`; `PetDeployer.DespawnEcho` is the first despawn path in the game).

## ASK 2 - the Night Store's aura is REPLACED, and it re-rolls in town

> *"then the one night realm or night store is to replace the current one on the night store. its to be
> random when in town every 30~min"*

- `NightStoreoption_Aura` **REPLACES** the Night Store's current aura. This one IS a replacement -
  contrast with Ask 1. Name the current aura in the RESULT so the swap is on the record.
- **Cadence: re-roll at random roughly every 30 minutes while the player is in town.**
  ⚠ **ASSUMPTION, STATED SO SHE CAN CORRECT IT CHEAPLY:** the thing on the 30-minute clock is the
  **aura selection**, not the Night Store's position or availability. Read literally, *"replace the
  current one on the night store"* + *"random ... every 30 min"* describes a rotating VISUAL on a fixed
  store, and the Night Market card was just permanently anchored to the HUD under WO-1335, so the store
  itself is not wandering. **Do not implement a relocating or randomly-appearing vendor.** If she meant
  that, it is a separate ticket and a much larger one.
- "In town" means the aura clock runs in the **town/hub** context only. Do not tick it during a raid,
  a battle or a dungeon.

## ASK 3 - the `Aura_*` family as a slow rotation, IF she prefers it

> *"there is a set of spells called aura ... can we use these slowly one after another instead at the
> night store if the other one doesnt look good"*

She screenshotted the set. It is real and complete - **seven prefabs, one folder**:

```
Assets/Spells Pack/Particles/Prefabs/Auras/
  Aura_Arcane.prefab  Aura_Dark.prefab  Aura_Fire.prefab  Aura_Ice.prefab
  Aura_Light.prefab   Aura_Nature.prefab  Aura_Storm.prefab
```

⭐ **This is a creative choice she has explicitly NOT made yet** - it is conditional on how her tagged
`top_down_starfall_line_blue` looks on device. So **do not choose for her, and do not build one and
throw the other away.**

## THE DESIGN ANSWER - THE SELECTION IS A TUNABLE, NOT A CONSTANT

> **Owner standing ruling, and she has been asking for it for months:** *"be smart, dont make it need a
> code change, make it tweakable from a db call"* / *"i have been screaming this for months."*
> **The default answer is YES.** (`KEY_FACTS.md`; `CANON_GROUND_TRUTH_2026-09-02.md`.)

Author the night-store aura selection as **remote tunables on the existing rail**, so she flips between
her single tagged aura and the seven-prefab rotation - and re-tunes the cadence - **without a rebuild**.
A rebuild is ~30 minutes; a tunable is a ~40-second knob.

Three knobs, minimum:
1. **mode** - single tagged aura (DEFAULT) vs rotate-the-family.
2. **cadence minutes** - default `30`.
3. **the rotation list** - so a prefab she dislikes comes out without a code change.

⚠ **"Slowly one after another"** is her pacing instruction for the rotation mode: an ORDERED, unhurried
walk through the family, not a rapid shuffle and not a hard cut. Cross-fade or let one finish before the
next begins. Do not stack two family auras on the store simultaneously.

### The tunables rail - the shape, and the invariant

- Registry `Assets/_Modules/Core/.../RemoteTunables.cs` -> `RemoteTunablesService.cs` -> the
  `client_tunables` table -> the `TUNABLE_KEYS` allowlist in `api/_lib/tunables.js`.
- Precedence: local PlayerPrefs `ff.tun.*` **>** remote row **>** build default.
- ⛔ **THE INVARIANT: no row, no network, or a parse failure MUST yield today's behaviour EXACTLY.**
  A tunable that changes behaviour when the network is down is a defect, not a feature.
- ⚠ **SIX sources must change together** for a new key (registry, service, table, allowlist, and the
  Command Center Balance tab's editor + its schema). A key added to fewer than all six is invisible or
  unsettable. Enumerate all six in your RESULT.
- Surface these in the **Command Center** so she can set them herself - *"should be in command center so
  you dont need to be a rocket scientist."*

## ⚠ A DETAIL I WILL NOT SILENTLY "FIX" FOR HER

Her `NightStoreoption_Aura` tag carries **`isLoop: false`**, while both tree auras carry `isLoop: true`.
**A one-shot effect on a 30-minute cadence plays once and then the store is bare for 29 minutes.**

That is very likely not what she wants - but **it is her tag and the rule forbids me overriding it.**
So: **honour `isLoop: false` as authored**, and make the behaviour correct anyway by having the cadence
**re-trigger** the effect, or by looping it when the tunable mode says to. Then **report the conflict in
the RESULT in one plain sentence** so she can retag it in seconds if she wants a true loop. Do NOT edit
her `isLoop` value.

## Constraints

- ⛔ **Never hand-edit a `.unity` scene** (resave-corruption history). Runtime injectors or the sanctioned
  builder method only.
- ⛔ **Do not add a second spawner or a second pool.** Sequenced/multi-part VFX are sanctioned only as
  special-cased PRESENTATION for marquee moments - never as a second spawner
  (memory `sequenced-vfx-special-cases-for-special-events`).
- Do not edit `VfxManualPicks.json`'s existing entries. You may READ it freely.
- ASCII-only in any player-facing string.
- ⛔ **LIVE LANES, stay out:** WO-1342 (talent-tree dialog `.cs`), WO-1341 (`PlayerDeckWorkspace.cs`,
  `HudLabelFitRegression.cs`), WO-1340 (`tutorial-steps.json`, tutorial registry), WO-1339
  (`BOARD.html`, `tools/board_build.py`, `tools/owner_validations.py`, `proof/owner-validations.json`),
  WO-1316 (Vercel deploy tooling), the decimation lane (`Assets/HeroContent`, hero FBX/metas), the store
  lane (`PackStore`, `NightMarket*` **presentation is in scope for the aura swap, but pricing/SKUs are
  not**), and WO-1337's files (`Enemy.cs`, `BattleArena.cs`, `PanelManager.cs`,
  `BattleQuiescenceGate.cs`).
- ⛔ Do not touch prices, SKUs, entitlements, grants or `api/_lib/purchase-catalog.js`. The game takes
  real money on mainnet.
- Do not run a Unity gate, do not commit, do not build. The lead does all three.

## Instrumentation

Add `FlowTrace` so a missing aura names ITSELF: which key was requested, which prefab resolved (or
that resolution returned null), where it was seated, which tunable mode was active, and when the
cadence last re-rolled. A silent VFX no-show is indistinguishable from "the artist's prefab is subtle",
and that ambiguity is exactly what costs a felt-test round trip.

⛔ **Never strip FlowTrace afterwards** - instrumentation is permanent; it may be flagged off, never
deleted (CLAUDE.md s12).

## Acceptance

- [ ] `atfootprintoftree_Aura` plays at the BASE of the Heart of Elarion, **alongside** the existing
      FireFlies aura, which is unchanged. Both present.
- [ ] The Night Store's old aura is named in the RESULT and replaced by `NightStoreoption_Aura`.
- [ ] The selection re-rolls on a tunable cadence, default ~30 min, ticking in TOWN only.
- [ ] Rotation mode over the seven `Aura_*` prefabs is implemented and **off by default**, switchable by
      tunable with no rebuild. "Slowly, one after another" - ordered, no two stacked.
- [ ] All three tunables reachable from the **Command Center**; all SIX sources enumerated.
- [ ] With no row / no network / a corrupt payload, behaviour is **byte-for-byte today's**. Prove it.
- [ ] The `isLoop: false` conflict is reported in one sentence and her tag is NOT edited.
- [ ] No prefab was chosen, substituted or scaled by the implementer. Say so explicitly.
- [ ] An oracle pins the key -> prefab mapping against `VfxManualPicks.json` (so a future refactor cannot
      silently re-point her tag) and pins the no-row invariant. **Prove it RED first and report the
      mutation.**
- [ ] Brace + NUL check per `.cs` file.
- [ ] ⛔ **Owner felt-verifies on device and CLOSES** - and specifically decides between her single
      tagged aura and the `Aura_*` rotation. That decision is a tunable flip, not a rebuild.
