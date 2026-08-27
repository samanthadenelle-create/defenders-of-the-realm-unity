# WORK ORDER 1066 — Weapon effects, Elarion names, and semantic VFX verbs

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Parent:** WO-1063 · **Requires:** WO-1065 vocabulary

## Effect budget

| Tier | Identity allowance |
|---|---|
| Common | Stats-only reliable baseline |
| Uncommon | Element or one small passive |
| Rare | Element plus one controlled effect |
| Epic | Stronger signature with stack/cooldown cap |
| Legendary | Build-changing but bounded signature |

Initial tuning envelopes: Burn 15–25% proc and about 8–12% of normal-hit damage over 4s, one stack;
Poison longer/lower DPS, max two stacks; Slow 20–30% for 2–3s; Armor break 8–12% for 4s; Cleave
20–30%; Pierce one extra target at 60–70%; Execute +15% below 25% HP. Ward/heal returns require an
internal cooldown. All values remain owner-tunable.

Rimeshot currently promises slow magnitude with no consumer: wire it or hold the promise. Flameblade
must not say “adds fire damage” until its damage/status payload makes that true.

## Narrative curation

Mechanics first, names second. Use:

| Identity | Vocabulary | Tradition |
|---|---|---|
| Physical | iron, warden, vigil, oath, bastion | Emberhand/Oathweld |
| Flame | ember, rekindled, hearth, cinder, dawn | Emberhand |
| Ice | rime, stilling, winterglass, frostbound | Last-Pressing |
| Aether | heartlight, pressing, dawnsong, aether | Last-Pressing |
| Poison | mire, thorn, venom, blight, greenwake | Heartwood |
| Nature | heartwood, root, briar, wildsong | Heartwood |

Each curated item requires unique Elarion name, functional subtitle/type, makers mark where
appropriate, flavor, exact effect, strengths and resistance. Examples such as **The Rimebound Vigil**
or **Mirewood Thornbow** are guidance, not permission to auto-name without owner review.

## Semantic VFX verbs

Weapon data requests intent:

```json
"vfx": {
  "held": "weapon.aura.flame",
  "attack": "weapon.swing.flame",
  "hit": "weapon.hit.flame",
  "proc": "status.apply.burn"
}
```

Vocabulary includes `weapon.aura|swing|hit.{physical|flame|ice|aether}`,
`status.apply|refresh.{burn|poison|slow|armor-break}`, `affinity.vulnerable`, and
`affinity.resisted`. One registry resolves verbs to owner-tagged keys. Catalogs never contain raw
third-party prefab/key names. Missing mapping logs and suppresses only presentation.

## Curation artifact and gates

Check in a table for every weapon:

`id | old/final name | class/family/tier | element | effect/parameters | matchups | mark | VFX verbs | price input | readiness`

Assert every advertised effect has a consumer; limits hold; verbs resolve to owner-approved entries;
missing VFX never changes gameplay; copy equals behavior; live names are unique and non-placeholder.

## Do not

- Do not give every weapon an effect.
- Do not price VFX/lore as power.
- Do not switch by weapon id or bypass owner VFX tagging.
- Do not flatten multilayer VFX prefabs.
