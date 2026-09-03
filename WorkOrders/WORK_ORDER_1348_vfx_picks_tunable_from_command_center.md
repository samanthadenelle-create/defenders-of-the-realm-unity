# WORK ORDER 1348 - Tag VFX from the Command Center, and see it on the next town load

**Status:** READY TO IMPLEMENT - ⛔ **DISPATCH HELD** until the WO-1343/1344/1345/1346/1347 agents land
and the tree is gated. Three of them are already authoring tunables; a sixth agent in the same registry
is the exact collision each of them was warned about. Release after the gate.
**Silo / Lane:** Command Center + the remote tunables rail + VFX pick resolution
**Type:** EXISTING assets and an EXISTING rail; the PICK moves from build-time to runtime.
**Minted:** 2026-09-03 (CLI) on a direct owner ask, with her own namespace proposal.
**Severity:** P2 - it is the difference between a ~30-minute rebuild and a ~40-second retag, on the one
axis she iterated hardest tonight.

## Her ask, verbatim

> *"is it possible to tag those from the command center? and then change pointer on next town load?"*
> *"realm.vfx(set)"*
> *"that idea"*

**The answer is YES.** Her namespace proposal `realm.vfx(set)` is adopted as the key shape - do not
invent a different one.

## Why this is the right end to the evening

She tagged **nine** VFX keys tonight. Every one of them is a `.json` file read by **editor-time**
tooling, so **every retag - including the four the tagger got wrong - costs a full rebuild.** That is
precisely the cost her standing ruling exists to kill:

> *"be smart, dont make it need a code change, make it tweakable from a db call"* /
> *"i have been screaming this for months."* **Default answer: YES.**

Tonight it produced four bad tags she cannot fix without a build, plus three "which looks better"
questions (the night-store aura, the `Aura_*` rotation, `backlight_coin` vs `_drop`) that **can only be
answered by looking at a phone.** A creative loop whose iteration cost is 30 minutes is a creative loop
she will stop running.

## What exists today - READ IT BEFORE DESIGNING

- `Assets/Editor/VfxManualPicks.json` - her tag file. Written by the Caster tooling; **~29 consumers**
  across editor tools, regression suites and runtime (`VFXManager.Hovl.cs`, `AmbientAuraPolicy.cs`,
  `NightStoreAuraSelector.cs`, `FtueWorldPointer.cs`, `HeroAbilities.cs`, `WeaponVfxMap.cs`,
  `ArcaneTower.cs`, `Enemy.cs`, and more).
  ⚠ **Enumerate the real consumer list from the tree, not from this paragraph** - several of those files
  were created TONIGHT by live agents and the list is still moving.
- The **remote tunables rail**: registry -> `RemoteTunablesService` -> `client_tunables` -> the
  `TUNABLE_KEYS` allowlist in `api/_lib/tunables.js`, surfaced in the Command Center's Balance tab.
- Precedence: local PlayerPrefs `ff.tun.*` **>** remote row **>** build default.

## The shape

1. **The build-time file stays the DEFAULT, and stays authoritative when nothing overrides it.**
   ⛔ Do not delete, replace or stop writing `VfxManualPicks.json`. It is the fallback and the record.
2. **A remote override layer**, keyed `realm.vfx.<key>` per her proposal, resolved at **town/scene
   load**. A row present -> that prefab. No row -> the build-time pick, byte-for-byte.
3. **A Command Center picker** that lists VFX keys and lets her set one - *"should be in command center
   so you dont need to be a rocket scientist."*
4. **Applies on the next town load.** ⛔ **Do NOT build live hot-swapping of already-spawned instances.**
   She set that boundary herself and it is the right one: scene-load resolution is simple, testable, and
   avoids re-parenting live particle systems. Say plainly in the UI when a change will take effect.

## ⛔ THE HARD LIMIT - STATE IT IN THE UI, DO NOT LET IT BE DISCOVERED AS A BUG

**A remotely-chosen prefab must ALREADY be in the build, or on R2 as addressable content. You cannot
point at a prefab that was never shipped.**

This is CLAUDE.md s16's exact lesson: art served remotely with **no local fallback** produces a build
that installs, launches and plays with **tinted capsules and no error on screen** - and it has bitten
this project **three times**. A VFX picker offering an unshipped prefab reproduces that failure in a new
place: she picks, nothing appears, and there is no signal.

**So:**
- The picker must be **constrained to prefabs the running build can actually resolve.** Prefer offering
  a whitelist generated at build time from what shipped.
- If a row names something unresolvable, **fall back to the build-time pick and SAY SO in the trace** -
  never silently render nothing.
- ⚠ Adding a NEW prefab to the pool is still a build. Be honest about that in the UI: this feature
  changes **which shipped effect is used**, not **which effects exist**.

## Constraints

- ⛔ **The tunables invariant: no row, no network, or a parse failure MUST yield today's behaviour
  EXACTLY** - i.e. the build-time file. A tunable that changes behaviour when the network is down is a
  defect. **Prove it.**
- **SIX sources must change together** for a new key (registry, service, table, `TUNABLE_KEYS`
  allowlist, Command Center editor, its schema). Enumerate all six.
- ⚠ **The four bad tags must remain fixable by this feature** - `atfootprintoftree_Aura`,
  `atfootprintoftree_Impact`, `EliteDeath_Impact`, and the missing boss-death key. If the design can
  only override an EXISTING key, it cannot create the boss-death tag she still needs, and half the
  motivation is lost. **Handle key creation, not only key override.**
- ⚠ The Command Center is reachable from her phone. Touch targets >= 112px; she is **red/green
  colourblind** - state applies/pending/failed in WORDS, never by hue.
- ASCII-only in player-facing strings.
- ⛔ Do not touch prices, SKUs, entitlements or `api/_lib/purchase-catalog.js`.
- Do not run a Unity gate, do not commit, do not build. The lead does all three.

## Instrumentation

`FlowTrace` at resolution: the key, whether a remote row existed, the prefab chosen, the source it came
from (**local pref / remote row / build default**), and whether resolution FELL BACK and why. ⛔ Never
strip FlowTrace. **Without the source field, "the override did not work" and "the override worked and
the art is subtle" are indistinguishable** - the single most expensive ambiguity in this whole lane.

## Acceptance

- [ ] She can set a VFX pick from the Command Center on her phone and see it on the next town load.
- [ ] With no row / no network / a corrupt payload, behaviour is **byte-for-byte** the build-time pick.
      Proven.
- [ ] A row naming an unshipped prefab falls back and **traces the reason**; the UI never implies a pick
      applied when it did not.
- [ ] She can CREATE a pick for a key that has no build-time entry (the boss-death case), not only
      override an existing one.
- [ ] All six tunable sources enumerated. Key shape is `realm.vfx.<key>` as she proposed.
- [ ] `VfxManualPicks.json` remains the default and the record; it is not deleted or bypassed.
- [ ] Every current consumer resolves through the new layer - enumerate them from the tree.
- [ ] An oracle pins the fallback invariant and that the picker cannot offer an unresolvable prefab.
      **Prove it RED first; report the mutation.**
- [ ] Brace + NUL check per `.cs` file; any PowerShell parses under 5.1 and it is proven.
- [ ] ⛔ Owner uses it on her phone and CLOSES.
