# WORK ORDER 1330 - A mage DoT and a knight regen, with art matched from the Blink library

**Status:** FIXED (edit-only lane; NOT gated, NOT committed) - one shared OverTimeEngine now carries every over-time effect; mage.wither (DoT) and knight.ironblood (regen) are that engine with the sign flipped, three shared tunables registered, and both VFX keys deliberately left OPEN for the owner tag.
**Silo / Lane:** Abilities / combat status effects / VFX matching
**Type:** EXISTING mechanics, MISSING abilities
**Minted:** 2026-09-02 (CLI) from an owner ruling with explicit creative latitude.
**Severity:** P2 - retention lane. More pressable buttons, earlier.

## The owner's ruling

> *"we have a ton of blink art and spells, have creative match a DoT would be nice"*
> *"or a regen for knight"*
> *"lots of room to interprut"*

She has explicitly granted latitude here. **That latitude covers the DESIGN - it does NOT overturn
the standing rule that the final VFX/art pick is hers.** Propose, do not silently ship a pick. See
"The one line not to cross" below.

## Why this is the retention lane

The owner's stated top business problem: *"our retention number is very low and people are not
returning."* A class whose early kit is a basic attack and a stat bump has nothing to press. This
ticket gives two classes a second real button each, and pairs with WO-1306 (the mage's drain) to
give the mage a coherent identity: **sustain + pressure**. The knight gets **sustain** of its own.

## ⛔ THE MECHANIC DOES NOT EXIST IN THE LIVE PATH. BUILD IT. (owner ruling, 2026-09-02)

Owner, verbatim, correcting the CLI's first reading of this ticket:

> *"it doesnt but it wouldnt be too challenging"*

**She is right and the correction matters.** The CLI grepped, found `CombatStatusTracker.cs` plus
damage-over-time handling inside `DeNelle.BattleATB`, and reported the mechanic as present. But
`DeNelle.BattleATB` is the **superseded turn-based system** - canon records real-time Arena combat
(`ff.dungeonrealtime`, default ON) as the live route. A DoT that exists only in the ATB engine is a
DoT the shipping game cannot cast.

So this ticket BUILDS the over-time effect for the live combat path. Her read is that it is not
challenging - agreed, provided it is built once and built in the right place.

**Reuse what is genuinely reusable, and say what you reused:** `CombatStatusTracker` may be the
right home or may itself be off the live path - PROVE which before extending it. The ATB
implementation is worth READING as a reference for shape (apply / tick / expire / stack rules) even
though its code is not the code that ships.

## What was actually found at source (read before you start, do not re-derive)

Confirmed present at source before this ticket was written:
- `Assets/_Modules/Core/Combat/CombatStatusTracker.cs`
- damage-over-time handling inside the `DeNelle.BattleATB` engine (`BattleState.cs`, `Combat.cs`,
  `Types.cs`), with its own tests
- `dot` / `overTime` / `burn` / `poison` / `bleed` tokens already present in
  `Assets/Resources/Data/Canonical/abilities.json`

⚠ **FIRST TASK - ESTABLISH WHICH PATH IS LIVE, AND BUILD THERE.** Per the owner's correction above,
do NOT assume any of the above is reachable by the shipping game. Determine what the live real-time
combat actually consults when damage is applied, prove it from code, and state the answer in the
RESULT. `DeNelle.BattleATB`'s DoT is reference material, not a foundation.

⚠ **The `dot`/`burn`/`poison`/`bleed` tokens in `abilities.json` are a TRAP, not evidence.** Authored
data proves someone once intended the concept; it proves nothing about whether a runtime consumer
reads it. Grep for the CONSUMER of each token before treating any of it as working. A field nobody
reads is indistinguishable from a field that does not exist - and this repo has a documented history
of exactly that (the WO-783 inert `waves.json` batches; 360 finished gear rows invisible behind a
stale catalog copy).

## The abilities

1. **Mage DoT** - damage applied over time rather than on impact. Pairs with the drain from WO-1306
   (`combat.drainReturnPct`).
2. **Knight regen** - health restored over time. The knight's sustain answer.

Both are the same underlying mechanic with opposite sign, so they should share ONE implementation
path through the existing status tracker. **Two abilities, one mechanism.** If you find yourself
writing a second tick loop, stop - that is the mistake this line exists to prevent.

## ⛔ EVERY TUNABLE VALUE GOES ON THE RAIL

Per the 2026-09-02 standing rule in `KEY_FACTS.md` and `docs/PROD022_TUNABLE_FLAGS.md`: tick
magnitude, tick interval and duration are BALANCE LEVERS and must be tunable from a database call,
not hardcoded. Follow the pattern WO-1306 established with `combat.drainReturnPct`.

- Registry `Assets/_Modules/Core/Ops/RemoteTunables.cs` + allowlist `TUNABLE_KEYS` in
  `api/_lib/tunables.js` + the doc + `ExpectedDefaults` in `RemoteTunablesDefaultsRegression.cs`,
  **all in the same change** - the `[tunable-defaults]` oracle enforces it.
- The invariant that outranks the feature: **no row, no network, no parse => TODAY'S BEHAVIOUR,
  EXACTLY.** Registered defaults must equal what a hardcoded constant would have been.
- Prefer ONE shared knob over per-ability duplicates where the value is genuinely the same concept.

## The art - PROPOSE, DO NOT PICK

Survey the Blink library (`Assets/Blink/`, ~2682 prefabs across the packs; note it is a GITIGNORED
art warehouse and the runtime NEVER loads from `Assets/Blink` directly - its role is a re-skin kit
and a data source).

Produce a **SHORTLIST for each ability** - a handful of candidates, not one - and for each:
- its address/path, and what it actually looks like **DESCRIBED IN WORDS**;
- why it reads as damage-over-time / as healing-over-time;
- whether it loops (a DoT and a regen both need a sustained effect, not a one-shot impact - check
  `IsLoop`, this is a functional requirement, not a taste question).

⛔ **THE OWNER IS RED/GREEN COLOURBLIND.** Never describe a candidate by hue alone and never ask her
to choose between colours. Describe SHAPE, MOTION, RHYTHM, DENSITY, and where it sits on the body.
"A slow pulsing ring at the feet" is useful; "the green one" is not.

## The one line not to cross

The standing rule is that **the owner tags VFX keys and the CLI maps them verbatim** - it never
picks, substitutes or improves a choice. Her *"lots of room to interpret"* grants latitude on the
DESIGN of these abilities. It does not silently transfer her art authority.

So: **build the abilities completely, wire everything that is mechanical, and leave the final VFX
key as the ONE open slot** - clearly named, with your shortlist attached, so a single word from her
closes it. A hook with no owner-tagged key stays unwired and is REPORTED, exactly as WO-1305 did
with `firespell_Cast`.

Related debt to fold in if cheap: `mage.siphon` (WO-1306) is already logged as OWNER-TAG DEBT with
no `concept-icons.json` row, so it renders the crossed-swords default in the bar. Include it in the
same shortlist so she can clear several tags in one pass.

## Acceptance

- [ ] The RESULT names WHICH combat path is live and proves it from code.
- [ ] Both abilities run through ONE shared status-effect implementation.
- [ ] Tick magnitude / interval / duration are on the tunables rail, with defaults equal to a
      hardcoded constant, and the `[tunable-defaults]` oracle green.
- [ ] An oracle pins the effect actually ticking (applied, ticks N times, expires) - prove it RED
      first and report the mutation.
- [ ] Art shortlists attached, described in words, with loop status stated per candidate.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] Owner picks the VFX keys and felt-verifies. **PO closes.**

## What NOT to touch

- Do not build a second status/tick system. Reuse the tracker that is live.
- Do not pick a VFX key. Do not restyle or recolour anything.
- Do not touch `HeroSkillTreePanelMvvm.cs`'s layout solver (WO-1310, awaiting felt-verification) or
  the hot-swap bar internals (WO-1294's lane).
- Coordinate with WO-1306 (mage ability data) and WO-1329 (mage casting registry) before editing
  shared mage rows - read their RESULTs first.
