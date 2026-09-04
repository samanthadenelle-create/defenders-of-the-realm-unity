# WORK ORDER 1344 - The FTUE points with an arrow instead of the yellow thing

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T14:31:56, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-09-03 - shipped in 2026.09.03.353999. ONE code path served both step kinds: the registry returns a RectTransform OR a world Transform, and UiSpotlight projected the world case into a nominal 120px screen rect, drawing the same yellow cutout for both. Her marker now serves exactly the three world beats (founding_greet / founding_walk / founding_defend) and DECLINES for UI-rect beats rather than forcing a world composite into a Canvas, where it would render at the wrong scale or depth or not at all. Input transparency proven from the prefab YAML: class ids 1/4/198/199 only - zero colliders, canvases, graphics or raycasters. Her isLoop:false is CORRECT (rateOverTime 0 on all four systems). Its own oracle shipped a hollow pass that the ratchet caught; resolved to assert THROUGH the fallback.
**Silo / Lane:** Tutorial presentation (FTUE highlight) + VFX wiring of an owner-tagged key
**Type:** EXISTING system, presentation REPLACED by an owner-tagged effect
**Minted:** 2026-09-03 (CLI) from an owner tag plus one sentence of intent.
**Severity:** P2 - it is the first thing a new player is shown, and retention is the stated business
problem.

## THE RULE THAT GOVERNS THIS TICKET

⛔ **The owner tags VFX keys in the Caster. The CLI maps key -> named hook VERBATIM and NEVER picks,
substitutes or rescales a prefab.** An un-tagged or suspect hook is HELD, never filled with a plausible
guess. (Memory `vfx-map-owner-tags-no-creative-pick`.) She is **red/green colourblind** - a pointer may
never carry its meaning by hue.

## HER TAG, VERBATIM FROM `Assets/Editor/VfxManualPicks.json`

| key | prefabPath | isLoop | scale |
|---|---|---|---|
| `FTUEPointerofwheretogo_Aura` | `Assets/Hovl Studio/Map track markers VFX/Prefabs/Marker 1 arrows Loop.prefab` | **`false`** | 1.0 |

> *"added FTUE vfx for pointing instead of the yellow thing"*

⭐ **This is the FIRST tag of the evening the Caster wrote correctly** - key and prefab both match her
stated intent. The four before it did not (see WO-1343's tagger investigation). **Re-read the file
before wiring anyway** - it has changed under us twice tonight, and the file always wins over this
table.

## WHAT "THE YELLOW THING" IS

The tutorial's existing highlight is a **`Glow` mask** - a UI overlay drawn over a target rect. Two
properties of it are load-bearing and must survive the replacement:

1. ⛔ **It NEVER BLOCKS INPUT.** WO-1340 relies on this explicitly - its contextual beat "gates nothing
   by construction". **A pointer that eats taps would soft-lock the FTUE on the very step it is
   teaching.** Whatever you build must be input-transparent. Prove it.
2. It resolves its target **by GameObject name** (e.g. `DeckCard_Skills`). ⛔ **Do not rename or
   re-parent any highlight target rect.**

## ⭐ THE QUESTION THIS TICKET MUST ANSWER FROM CODE, NOT ASSUME

Her prefab comes from a pack called **"Map track markers VFX"** and is a **world-space** arrow marker.
The Glow mask is a **UI overlay on a screen rect**. Those are not the same kind of object, and the
FTUE has **two different kinds of step**:

- **"Tap this thing"** - a UI target: a card, a bar face, a button. Screen space.
- **"Go there"** - a world objective: the gate, a building, an NPC. World space.

Her words - *"pointing"*, *"where to go"* (the key is literally `FTUEPointerofwheretogo`) - point hard
at the **world/navigation** case. **But do not assume it. Read the tutorial highlight code and report
which kinds of step exist and which ones the Glow currently serves**, then wire her marker to the
case(s) it can actually express.

If it turns out the Glow serves BOTH cases through one code path, say so plainly and propose the split
rather than forcing a world-space arrow onto a UI rect - a marker prefab parented into a Canvas
typically renders at the wrong scale, the wrong depth, or not at all, and it will look like the tag
simply failed.

⚠ **A missing VFX and a subtle VFX are indistinguishable without instrumentation** - which is exactly
why the next item is mandatory.

## INSTRUMENTATION

Add `FlowTrace` so a no-show names ITSELF: the key requested, whether the prefab resolved or returned
null, the target it was anchored to, the space it was placed in (world vs canvas), and its resolved
world/screen position. ⛔ **Never strip FlowTrace** - instrumentation is permanent; it may be flagged
off, never deleted (CLAUDE.md s12).

## ⚠ A DETAIL REPORTED, NOT SILENTLY FIXED

Her tag carries **`isLoop: false`** on a prefab whose own name ends in **`Loop`**. A pointer that plays
once and vanishes leaves the player with nothing while the objective is still open.

**Honour `isLoop: false` as authored** - make the behaviour correct by re-triggering or by driving the
lifetime from the step's own active window - and then **report the conflict in one plain sentence** so
she can retag in seconds. ⛔ **Do NOT edit her `isLoop` value, and do not edit
`VfxManualPicks.json` at all.** Read it freely; write nothing.

*(This is the same conflict class as WO-1343's night-store tag. Report both the same way.)*

## RELATIONSHIP TO WO-1340 - LANDED TODAY, UNCOMMITTED

WO-1340 rewrote the `ctx_talents` beat: it now completes on a **real spend** (`talent.learned:first`)
instead of on the dialogue closing, and it highlights the **`DeckCard_Skills`** rect to teach the
route **HERO -> SKILLS**. Its escape hatch is a 240 s release emitting `CTX-STUCK`.

- **Do not change what that beat teaches, when it completes, or what it targets.** This ticket changes
  only HOW a highlight is DRAWN.
- ⚠ Its files are landed but **not yet gated or committed**, so the tree you are reading is newer than
  HEAD. Read the working tree, not `git show`.
- If your change would alter that beat's behaviour rather than its appearance, **stop and report it**.

## ⛔ LIVE LANES - stay out

- **WO-1343** (an agent is live in it right now): the night-store aura, the `Aura_*` tunable rotation,
  the tree-foot and boss-death unbound hooks, `KnightShieldBash_Impact`, and the tagger investigation.
  ⚠ **You and it are both wiring owner-tagged VFX keys. Do NOT both edit a shared VFX resolver,
  registry or spawner** - if your fix wants to touch one, report the collision to the lead instead.
- **WO-1342**: `HeroSkillTreePanelMvvm.cs` (spend popup + its consts),
  `SkillsPanelLayoutRegression.cs`, both `hero-talents.json` twins.
- **WO-1341**: `PlayerDeckWorkspace.cs`, `HudLabelFitRegression.cs`. ⚠ It just restyled the Hero deck
  cards to match Manage - `DeckCard_Skills`' GameObject name is unchanged and MUST stay unchanged.
- **WO-1339**: `BOARD.html`, `tools/board_build.py`, `tools/owner_validations.py`,
  `proof/owner-validations.json`. **WO-1316**: `tools/web-ship.ps1`, `tools/command-centre.ps1`.
- **WO-1337**: `Enemy.cs`, `BattleArena.cs`, `PanelManager.cs`, `BattleQuiescenceGate.cs`.
- The decimation lane: `Assets/HeroContent`, hero FBX + `.meta`.

## Constraints

- ⛔ **Never hand-edit a `.unity` scene** (resave-corruption history).
- ⛔ **Do not add a second spawner or a second pool.** One owner per presence (CLAUDE.md s7).
- UI is code-built uGUI via `ElarionUiKit`. **UXML does not work in builds.**
- ASCII-only in player-facing strings.
- Phone-first landscape; touch targets >= 112px, and the pointer must not shrink or cover one.
- Do not run a Unity gate, do not commit, do not build. The lead does all three.

## Acceptance

- [ ] The FTUE points with her tagged marker; the yellow Glow no longer serves the case(s) it replaces.
- [ ] **Input transparency proven** - the pointer cannot swallow a tap on the thing it points at.
- [ ] Which kinds of FTUE step exist, which the Glow served, and which her marker now serves - answered
      from code, with file:line.
- [ ] WO-1340's beat is unchanged in trigger, completion and target. Say so explicitly.
- [ ] The `isLoop: false` conflict reported in one sentence; her tag NOT edited.
- [ ] No prefab chosen, substituted or rescaled by the implementer. Say so explicitly.
- [ ] `FlowTrace` names a future no-show without another investigation.
- [ ] An oracle pins the key -> prefab mapping against `VfxManualPicks.json` (so a refactor cannot
      silently re-point her tag) and pins input-transparency. **Prove it RED first; report the
      mutation.**
- [ ] Brace + NUL check per `.cs` file.
- [ ] ⛔ **Owner felt-verifies on device and CLOSES** - a pointer is judged by eye, on a phone.
