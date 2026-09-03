# WORK ORDER 1327 — RESULT

**Status:** FIXED (code-side), **NOT CLOSED** — the owner must felt-verify.
**Date:** 2026-09-02
**Lane:** VFX / combat feel + mobile perf

---

## The headline: the fix could not go where the work order said it should

The WO located the defect in `Spell_Fire_9.prefab` and prescribed editing its
`CollisionModule` and `LightsModule`. Those readings were **correct** — every number in the
WO's table was verified at source. But:

> ### ⛔ `Assets/Spells Pack/` IS GITIGNORED (`.gitignore:430`).

```
$ git check-ignore -v "Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Fire_9.prefab"
.gitignore:430:/Assets/Spells Pack/   Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Fire_9.prefab
```

A hand-edit to that prefab cannot be committed, cannot be reviewed, never reaches another
machine or CI, and is erased by the next pack re-import — while still silently changing what
*this* machine builds. It is the same class of pack as polyperfect and Quaternius.

**So the prefab was NOT edited.** The clamp went to the one spawn owner instead, which also
fixes every *other* pack prefab carrying the same misconfiguration rather than the single
instance somebody happened to notice.

---

## Did the bounce hypothesis hold up?

**Partly. It is a real misconfiguration. It is NOT the proven root of the owner's two
captures, and the WO's "they are almost certainly this" does not survive contact with the
tree.**

### What was confirmed at source (WO table: all correct)

`Spell_Fire_9.prefab` → `Fireballs` emitter (the prefab owner-tagged to `firespell_Cast` in
`Assets/Editor/VfxManualPicks.json`), `CollisionModule` at prefab line 9118:

| field | value | line |
|---|---|---|
| `type` | `1` (World) | 9121 |
| `quality` | `0` (High) | 9299 |
| `collidesWith.m_Bits` | `4294967295` — all 32 layers | 9297 |
| `m_Bounce` scalar | **1.0** (perfectly elastic) | 9190 |
| `m_Dampen` scalar | **0** | 9137 |
| `minKillSpeed` | **0** | 9293 |

Six of the prefab's seven emitters have collision **disabled**. This one does not. So the
recipe the WO describes is really there.

### What the WO's reading missed

`m_EnergyLossOnCollision` (Unity's "Lifetime Loss") carries **scalar 1 over a zero-valued
curve** — every other emitter in the prefab carries scalar 0. Under the reading where Unity
evaluates a constant `MinMaxCurve` from its scalar, lifetime loss is already 1 and the
particle **already dies on first collision**, which would make an endless ricochet
impossible. Under the curve reading it is 0 and the WO is right. **The YAML alone cannot
settle it**, and no runtime capture exists.

The fix is deliberately written to be terminal under **both** readings (see below).

### The stronger counter-evidence

`VFXManager.EnforceOneshotEmission` — landed **the same day**, in this tree — carries a
root-cause note for **seq 4644 specifically**, with captured data:

> *"the fire spell is wrong. casts at me and stays at me."* … `Cast_FireCharge`'s catalogued
> prefab (`Resources/VFX/Projectiles/Casting_Fire.prefab`) carries FOUR ParticleSystems that
> are all `looping: 1` … the effect BURNED CONTINUOUSLY on the caster for 10.3 seconds …
> matches the captured `[Flow:VFX] live systems=35`.

That is an instrumented root with a trace line behind it. **Seq 4644 is already accounted
for.** Attributing it to the bounce would have been exactly the inference-fix CLAUDE.md §12
forbids.

### What DOES corroborate this defect

The owner's own words about this prefab, quoted verbatim in `MarqueeSpellVfx.cs:14-17`:

> *"the way it displays is there is a wind up directly into **projectiles flying and
> bouncing**"*

She saw the bouncing, in the VFX Caster, and named it. That is a real observation of a real
misconfiguration — it is just not proof that it is what she was reporting in seq 4152/4644.

**Verdict: fix it, say plainly that it is a hygiene/perf fix with a plausible feel payoff,
and let her eyes decide.** Recorded in the code comments, not just here.

---

## Concurrent real-time point lights per cast — the NUMBERS

Verified at source. `Spell_Fire_9` has **exactly two** enabled `LightsModule`s, both pointing
at the same `Point Light` child (`fileID: 3562168499352759841`) as their **prototype**:

| emitter | prefab line | `ratio` | `maxLights` BEFORE | AFTER |
|---|---|---|---|---|
| `Fireballs` | 9340 | 1 | **20** | **2** (ratio → 0.10) |
| `Explosion ` (sub-emitter) | 19094 | 1 | **5** | **2** (ratio → 0.40) |
| the other five emitters | — | 0 | disabled | untouched |

> # **BEFORE: 25 concurrent real-time point lights per cast.**
> # **AFTER: 4.**

Intensity 5, range 5, shadows off — unchanged. **The `Point Light` child was NOT deleted and
NOT disabled**: it is the prototype the modules clone from, and removing it breaks the effect
instead of tuning it. Only `maxLights` and `ratio` moved.

`ratio` moves *with* `maxLights` on purpose: capping `maxLights` alone would attach the four
surviving lights to the first four particles and leave the rest of the burst dark. Scaling
the spawn probability by the same factor keeps them spread across the effect.

---

## What changed

### 1. `Assets/_Modules/Village/Vfx/VFXManager.cs` — the clamp

Two new normalizers, sited next to `EnforceOneshotEmission` because it is the same seam and
the same lifecycle owner:

- **`TameWorldCollision(go, what)`** — for every ParticleSystem on a checked-out host whose
  collision is **enabled** and of type **World**: `bounce` down to the cap, `dampen` and
  `lifetimeLoss` up to its complement. At the shipped default (0%) that is
  **bounce 0 / dampen 1 / lifetimeLoss 1** — the particle stops at the surface it hits and
  terminates. Written as **constants**, which resolves the `m_EnergyLossOnCollision`
  scalar-vs-curve ambiguity above: whichever Unity would have evaluated, after this the
  particle dies at first contact.
  **The clamp is one-way** — it only ever tightens, so it can never make an effect bouncier
  than its author made it, and `vfx.particleBouncePct = 100` is a true no-op.
- **`ClampParticleLights(go, what)`** — spends a per-host budget evenly across the enabled
  `LightsModule`s and scales each `ratio` with it. A host already inside the budget is left
  completely untouched. Never touches a prototype.
- **`NormalizeSpawnedHost(go, what)`** — the single entry point.

Wired into **all three** spawn paths, each *before* `PlayAllParticles` so nothing emits a
frame with the authored values:

| path | file:line context |
|---|---|
| `PlayOneshot` prefab path | `VFXManager.cs`, after `EnforceOneshotEmission` |
| `PlayLoop` prefab path | `VFXManager.cs`, after `VerifyHasParticles(…,"loop")` |
| Hovl key path (**this is the one the fire spell takes**) | `VFXManager.Hovl.cs`, after the `IsLoop` check |

The Hovl call is **unconditional** — unlike `EnforceOneshotEmission`, neither defect has
anything to do with whether the row loops.

Both clamps emit `FlowTrace.Once` naming the host, the count corrected and the knob to flip.
No instrumentation was removed (§12).

### 2. Both dials are on the PROD-022 tunables rail

Per the 2026-09-02 standing rule. Two new knobs:

| key | kind | default | meaning |
|---|---|---|---|
| `vfx.particleBouncePct` | int | **0** | restitution ceiling, percent. `100` = hand the pack back its authored collision. |
| `vfx.maxParticleLights` | int | **4** | concurrent particle-driven real-time lights per VFX host. `0` = off. |

Registered in all four places the rail requires, in this commit:
`Assets/_Modules/Core/Ops/RemoteTunables.cs` (consts + `Registry`), `api/_lib/tunables.js`
(the server allowlist), `docs/PROD022_TUNABLE_FLAGS.md` (rows 10–11), and
`Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` (the pinned literals).
`tools/client-tunables.mjs` imports the allowlist and needed no edit.

Reverting the whole behaviour change is two flag flips, no rebuild:

```powershell
tools\command-centre.ps1 -Tunables -Key vfx.particleBouncePct -Value 100
tools\command-centre.ps1 -Tunables -Key vfx.maxParticleLights -Value 25
```

#### ⭐ The rail's invariant is bent here, and it is written down rather than hidden

The rail's rule is *"every default = today's behaviour, exactly."* These two are **bug
fixes**, so their defaults are the **corrected** values. An empty `client_tunables` table
gives you the fixed collision and the 4-light budget, not the pack's 25 lights and elastic
fireballs. The half of the invariant that still binds — *no row, no network, no parse ⇒
exactly what this build hardcodes* — is intact. Stated in `RemoteTunables.cs`'s header, in
the regression's comment, and in a call-out box in the docs page.

> **⚠ AND THE HONEST LIMIT, as the WO demanded be said plainly:** the numbers baked into the
> **prefab** are **NOT reachable by the tunables rail** and never will be. Only the code-side
> clamp is. That is the second reason the fix had to be code — and it means anyone reading
> `Spell_Fire_9.prefab` in future will still see `bounce: 1.0` and `maxLights: 20`. The
> prefab is not wrong-then-fixed; it is overridden at spawn.

---

## Files changed

| file | change |
|---|---|
| `Assets/_Modules/Village/Vfx/VFXManager.cs` | `TameWorldCollision`, `ClampParticleLights`, `NormalizeSpawnedHost`, two tunable accessors, `using DeNelle.Core.Ops;`, 2 call sites |
| `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs` | 1 call site (the fire spell's path) |
| `Assets/_Modules/Core/Ops/RemoteTunables.cs` | 2 knobs + defaults + registry entries + header exception note |
| `api/_lib/tunables.js` | 2 allowlist entries |
| `docs/PROD022_TUNABLE_FLAGS.md` | rows 10–11 + the invariant-exception call-out |
| `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` | `ExpectedKnobCount` → 14, 5 pinned rows, prose |
| `WorkOrders/WORK_ORDER_1327_*.md` | Status → FIXED |

**Brace / NUL gate (CLAUDE.md §1) — every `.cs` touched:**

```
Assets/_Modules/Village/Vfx/VFXManager.cs                        BALANCED clean
Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs                   BALANCED clean
Assets/_Modules/Core/Ops/RemoteTunables.cs                       BALANCED clean
Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs     BALANCED clean
```

---

## ⚠ Cross-lane note for the committer

`RemoteTunablesDefaultsRegression.cs` counts **all** rail knobs, so this lane could not raise
`ExpectedKnobCount` for its own two without also pinning the **three WO-1330 over-time knobs**
that landed in `RemoteTunables.Registry` and `api/_lib/tunables.js` while this work was in
flight. `ExpectedKnobCount` is now **14** = 8 PROD-022 + 1 (WO-1306) + 3 (WO-1330) + 2
(WO-1327), with all fourteen rows pinned. **The WO-1330 rows in the regression were added by
this lane as bookkeeping, not as review of that lane's design.** Their doc rows (12–14) were
written by their own seat and are left alone.

---

## Deliberately NOT touched

- **`Spell_Fire_9.prefab` itself** — gitignored; see the top of this file. Not one byte.
- **The `Point Light` child.** It is the prototype. Not deleted, not disabled.
- **Anything visual** — no colour, material, intensity, range, prefab swap or substitution.
  The owner owns every VFX call and is red/green colourblind; only *whether it bounces* and
  *how many lights* moved.
- **`collidesWith` / `quality` / `minKillSpeed` on the prefab.** With bounce 0 + dampen 1 +
  lifetimeLoss 1 the particle terminates at first contact, so the layer mask stops mattering.
  Narrowing the mask would have been a second, unproven change. (For the record: the project
  declares only nine layers, and the hero is not on one that could be excluded without also
  excluding the ground — so a mask edit could not have separated "the player's capsule" from
  "the floor" anyway.)
- **WO-1305 part B** (Synty duplicate addresses) and **WO-1329** (the mage casting registry).
- **No regression suite was written for the clamp itself.** The knobs' defaults are pinned
  three ways by the existing tunables oracle; a behavioural pin on the clamp would want a
  PlayMode host and is a separate ticket if wanted.

---

## Acceptance status

- [ ] **A play capture inside the walls shows the fireball terminating on impact** — NOT DONE.
      This agent is edit-only: no Unity gate, no content build, no R2 push was run.
- [x] **Concurrent light count stated as a NUMBER, before and after** — **25 → 4**.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs — **owed by the committer.**
      Note `RemoteTunablesDefaultsRegression` is directly affected and must be read on a fresh log.
- [ ] ⛔ **OWNER FELT-VERIFIES AND CLOSES.** Non-negotiable here. Two of the three claims in
      this ticket are about how a spell *feels* and how a phone *runs*, and **no headless gate
      can see either**. If the effect now reads as under-lit or the fireballs die too early,
      that is a knob flip and not a rebuild.
