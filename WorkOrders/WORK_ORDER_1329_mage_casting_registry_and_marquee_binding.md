# WORK ORDER 1329 - The marquee fire spell belongs to the MAGE, so give the mage a casting registry

**Status:** READY TO IMPLEMENT
**Silo / Lane:** VFX / abilities / motion registry
**Type:** EXISTING mechanism, MISSING plumbing
**Minted:** 2026-09-02 (CLI) on a direct owner ruling.
**Severity:** P3 - a finished mechanism sits dormant.

## The owner's ruling

Asked whether the marquee fire spell (`firespell_Cast` -> her tagged `Spell_Fire_9`) should bind to
the knight's `cast` row tonight, or move to the mage with real plumbing, she chose:
**"It belongs to the mage - do the plumbing."**

## The state WO-1305 left, verified at source

- `Spell_Fire_9` -> key `firespell_Cast` is **owner-tagged**: `VfxManualPicks.json:921-925`,
  `manual:true`. `HovlVfxCatalog.asset:1052` carries the key with `IsLoop: 0`.
- `MarqueeSpellVfx` is a **string-set registry only** - it instantiates nothing and pools nothing;
  `VFXManager.PlayKey` remains the single spawn owner. The two-VFX-stack scar is NOT widened, and
  must not be widened by this ticket either.
- **`firespell_Cast` has NO CONSUMER.** Repo-wide it appears only in the picks JSON, the caster index
  and the baked catalog. The prefab->key half is her tag; the **key->ability** half was deliberately
  left unmade, because choosing which ability wears a marquee effect is a creative pick and the CLI
  never makes those.

## THE BLOCKER, and it is the actual work

`RegistryTarget` is **hardcoded `"knight"`**, and the mage has **zero rows** in
`motion-castings.json`. So the mage cannot currently be addressed by the motion/VFX registry at all.

This ticket is that plumbing: make the casting registry address the mage as a first-class target,
then bind `firespell_Cast` to the mage's cast.

## Constraints

- **VFX SELECTION IS THE OWNER'S, ALWAYS.** She has tagged exactly one key here. Bind THAT key,
  verbatim. Do not pick, substitute or "improve" any effect. If the plumbing exposes further hooks
  with no owner-tagged key, LEAVE THEM UNWIRED and report them as awaiting her tag.
- Do not hardcode a second target string. If `RegistryTarget` must stop being a constant, make it
  RESOLVE from the acting class - do not replace one hardcoded name with two.
- A marquee moment may use a sequenced multi-part prefab; special-case the PRESENTATION only. Never
  add a second spawner or a second pool. Pool by default, bounded and lazy, one owner per concern.
- A VFX that fails to resolve must `FlowTrace.Warn`, never fail silently - a silent miss is
  indistinguishable from a spell with no effect authored. `VFXManager.CanPlayKey` (added by WO-1305)
  is the read-only probe for exactly this; reuse it rather than writing a second one.
- Coordinate: WO-1306 also touches mage ability data. Read its RESULT before editing shared rows.

## Acceptance

- [ ] The mage is addressable by the casting registry without a hardcoded class name.
- [ ] `firespell_Cast` fires on the mage's cast, and the key is the owner-tagged one, unchanged.
- [ ] Any hook left unwired is NAMED as awaiting an owner tag.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs.
- [ ] Owner felt-verifies and closes - a marquee spell is judged by eye, never by a gate.
