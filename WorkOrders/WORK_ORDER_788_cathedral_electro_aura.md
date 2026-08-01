# WORK ORDER 788 — Cathedral of Magic: swap the shield aura for an electro rune circle

**Status:** SHIPPED 2026-07-30 (fe87a943; follow-ups f83d4c9f foot-point anchor + fcf5a249 particle bounds).
**Lane:** Lane 9 (VFX/Audio)
**Type:** EXISTING (the aura is wired; only the tagged key changes — a SWAPPABLE default)
**Minted:** 2026-07-30 (owner felt-report from build-mode screenshot + owner aura choice this session)
**Author:** UI/RCA seat. CLI implements + gates. PO felt-verifies + closes.
**Creative authority:** owner CHOSE the effect (owner-tags-VFX rule — CLI wires the chosen key verbatim, never substitutes).

---

## Symptom (owner)

> "Cathedral of Learning should not have a shield aura, can we choose a different one."

In build mode the **Cathedral of Magic** (owner calls it "Cathedral of Learning" — same building,
id `arcane-tower`) shows a holy **shield DOME** aura (the winged radiant burst in the screenshot).
The owner wants a different, non-shield aura.

## Owner's choice (this session)

**Electro rune circle (blue)** — the flat magic-circle ground loop
`Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle electro loop.prefab`.
A crackling blue arcane rune circle on the ground (not a dome/shield). Reads as active spellcasting —
fits a cathedral of magic/learning.

---

## RCA — where the aura is set (proven from code)

The cathedral aura is `"Aegis_Shield"`, applied at TWO surfaces that the differentiation gate forces
to agree:

- `Assets/_Modules/Village/Catalog/StructureFactory.cs:804` — `ArcaneAura.Ensure(root, "Aegis_Shield")`
  (catalog-placed cathedral). Comment: *"subtle, DISTINCT, NON-HEAL aura … SWAPPABLE default — retag
  in the Caster during the visual pass."*
- `Assets/_Modules/Village/HubStructureVisualInjector.cs:425` —
  `ArcaneAura.Ensure(target.gameObject, "Aegis_Shield")` (baked hub landmark).

`Aegis_Shield` resolves to `Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic shield holy
loop.prefab` (`Assets/Editor/VfxCasterLibraryIndex.json:49`) — the holy shield dome the owner is
rejecting.

The chosen electro loop is present in the library index but **un-keyed**
(`VfxCasterLibraryIndex.json:248-249`, `"key":""`), so it must be given a key before it can resolve.

---

## Gate constraints the fix MUST satisfy (`Assets/Editor/Regression/VfxAuraDifferentiationRegression.cs`)

1. **Three distinct keys:** node (PoiCalloutSystem.NodeAuraKey) / cathedral / spire (ArcaneTower =
   `Aura_HeartPulse`) must be three different values (`:71-78`). The new cathedral key must differ
   from both.
2. **Not the old shared sun-loop keys** (`OldNodeKey`/`OldArcKey`) (`:60-65`).
3. **Both cathedral surfaces agree** — StructureFactory == HubStructureVisualInjector (`:67-69`).
4. **The chosen key is CATALOGUED** — present in `HovlVfxCatalogGenerator.cs` Map OR
   `Assets/Editor/VfxManualPicks.json` overlay (`:80-87`).

The electro loop satisfies (1)/(2) once keyed; (3)/(4) are the implementation steps below.

---

## The fix (bounded)

1. **Tag the key.** Add an entry to `Assets/Editor/VfxManualPicks.json` mapping a new key
   `Cathedral_Aura` (name at CLI's discretion; keep it descriptive) →
   `Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle electro loop.prefab`,
   `isLoop: true`. (Manual-overlay path — the gate accepts this as "catalogued.") Also set the key on
   the matching `VfxCasterLibraryIndex.json:248` row so the Caster index reflects it.
2. **Regen the catalog** so `HovlVfxCatalog.asset` carries the new key (the generator/regen step —
   `HovlVfxCatalogGenerator`).
3. **Retag both cathedral surfaces** from `"Aegis_Shield"` → `"Cathedral_Aura"`:
   - `StructureFactory.cs:804`
   - `HubStructureVisualInjector.cs:425`
   (Update the adjacent comments from "Aegis_Shield magic dome" to the electro rune-circle.)
4. **Update the differentiation regression** expected value if it pins the cathedral key literally;
   ensure it still asserts node/cathedral/spire are three distinct catalogued keys.

Note: `Aegis_Shield` stays in the catalog (still used by `DefenseUp-Offhand(Shield)_Aura` context /
available for reuse) — only the cathedral's default reference changes.

---

## Acceptance

- [ ] Cathedral of Magic (catalog-placed AND baked hub landmark) shows the blue electro rune-circle
      ground loop — no shield dome. Verified via `RunCaptureHeadless` screenshot of the hub/build
      mode (memory `headless-screenshot-verify-ui-before-build`).
- [ ] `VfxAuraDifferentiationRegression` passes (`VFX_AURA_DIFF_OK`) — node/cathedral/spire still
      three distinct catalogued keys, both cathedral surfaces agree.
- [ ] The electro loop resolves (no missing-prefab warning) — key catalogued in the manual overlay +
      regen'd asset.
- [ ] Brace/NUL gate passes on every `.cs` edited; `COMPILE_GATE_OK` emitted.
- [ ] Screenshot handed to owner for the felt-pass; **PO closes**.

## What NOT to touch

- Do not change the node or spire auras — only the cathedral's key.
- Do not delete `Aegis_Shield` from the catalog.
- Owner-tags rule: wire the electro loop the owner chose; do not substitute a different effect.
- The `Magic circle electro loop` prefab lives in a **gitignored Hovl pack** — on a fresh clone it
  dangles like the other 117 pack-bound VFX (see WO-785); this WO does not solve pack survivability,
  it just points the cathedral at the owner's chosen key. If WO-785 promotes the wired set into
  tracked `Resources/VFX/`, include this key.

---

*Notion "Work Orders" DB row — pending (add on a tooled session; NOTION_SOURCE_OF_TRUTH.md).*
