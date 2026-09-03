# WO-1294 RESULT — Blink Skill Identity, Three-Slot Hot-Swap, Troop Portraits

**Status:** FIXED (2026-09-02) — data + oracle on disk, NOT closed. The lead still owes the batch
gate; the owner still owes the felt test and the section-9 screenshots (headless cannot see a bar).

---

## 1. What the ticket actually found on arrival

Most of WO-1294 had already landed across earlier passes, and the ticket was still sitting READY. On
arrival, verified from the tree (not from comments):

| WO clause | State found |
|---|---|
| Three hot-swap slots | ALREADY DONE — `AssignableSkillBar.SlotCount = 3`; no fourth slot anywhere in runtime UI, prefs serialization, HUD (`HudKitController` builds slots 0/1/2), or regressions |
| Nine troop portraits imported | ALREADY DONE — all nine PNGs under `Assets/Resources/RpgUi/troop/`, role `RpgUiCatalog.RoleTroop` wired, every `troops.json` `iconId` matches its own canonical id |
| Troop portraits reach the surfaces | ALREADY DONE — `TroopTrainingPanel.TroopIcon` (Barracks/Manage) and `RaidDeployScreen:356` both resolve `RpgUiCatalog.Get(RoleTroop, iconId)` with the role-glyph fallback kept only for a genuinely missing asset |
| Tree reads left-to-right, default row visible | OWNED BY WO-1310 (commit `03263bc39`), awaiting the owner's felt-verify — deliberately untouched |
| **One-icon identity (section 4)** | **BROKEN — the real remaining defect, see below** |

## 2. The defect, with the data that proves it

The talent tree resolves node art through `talent-icon-map.json`. The hot-swap slot, the assignment
picker and the combat HUD resolve through `concept-icons.json`. **Nothing compared the two files.**
`TalentIconMapRegression` guards the tree side only; `concept-icons.json` was guarded by nothing at all.

The shared/universal pool was re-tagged under WO-1023 and `concept-icons.json` was never followed, so
three skills drew one picture on their talent node and a *different* picture in the hot-swap slot and
on the combat HUD — the exact split section 4 forbids:

```
node        abilityId                TREE (blinkSource)   HOT-SWAP/HUD (concept-icons)
shared.n9   universal.arcane-bolt    Arcanist17           Arcanist6      <-- SPLIT
shared.n10  universal.mend           Priest5              Paladin15      <-- SPLIT
shared.n11  universal.dash           Rogue4               Deathknight11  <-- SPLIT
```

Knock-on: `Arcanist6` was therefore worn by **both** `universal.arcane-bolt` and `mage.manaweave`,
which are simultaneously placeable for a mage — two different spells rendering as the identical purple
dart, and the owner is red/green colourblind so hue cannot separate them. That is the acceptance line
"no two simultaneously visible skills use the same icon" failing in the shipped data.

The other 22 ability-granting nodes already agreed.

## 3. What was changed

### `Assets/Resources/Data/Canonical/concept-icons.json` + its StreamingAssets mirror (byte-identical)

Four rows re-pointed. **No new creative pick was made** — every value is copied from the art the tree
already authors in `talent-icon-map.json`, so this is propagation, not an art decision:

| row | was | now | source of the value |
|---|---|---|---|
| `universal.arcane-bolt` | Arcanist6 | **Arcanist17** | `shared.n9` blinkSource |
| `universal.mend` | Paladin15 | **Priest5** | `shared.n10` blinkSource |
| `universal.dash` | Deathknight11 | **Rogue4** | `shared.n11` blinkSource |
| `mage.arcane-bolt` | Arcanist6 | **Arcanist17** | follows its twin — the file's own recorded rationale is that this row exists only because `universal.arcane-bolt` carried Arcanist6; leaving it behind would have re-created the split |

`Arcanist6` now belongs to `mage.manaweave` alone, which is what its own tree node (`mage.t2n2`)
already authored. The `_comment` carries the full provenance so the next reader can tell a ruling from
an oversight.

### `Assets/Editor/Regression/ConceptIconIdentityRegression.cs` (NEW) + wired in `DataRegression.cs`

Marker `CONCEPT_ICON_IDENTITY_OK` / `_FAIL`, log tag `[concept-icons]`. Six assertions, each one an
acceptance criterion made enforceable instead of "true by care alone":

1. `concept-icons.json` Resources and StreamingAssets copies byte-identical.
2. For every ability-granting talent node, tree `blinkSource` basename == the `concept-icons` row name
   for that node's `abilityId` — the section-4 identity contract, the gap this WO fell into.
3. Every ability-granting node's `abilityId` has a `concept-icons` row (a missing row is not "no icon";
   it silently renders the crossed-swords default, i.e. Knight art on someone else's spell).
4. No two different ability ids that can be on screen together share one icon. "Together" = same class
   prefix, or either side `universal.*`. Same-concept twins are allow-listed **by name with a written
   reason**, never silently.
5. Every `spellicons`/`troop` row returns real art through the same `RpgUiCatalog` call the runtime
   makes (an on-disk check would pass an unimported texture).
6. `AssignableSkillBar.SlotCount == 3`, all nine canonical troop portraits load, every `troops.json`
   `iconId` is one of them.

Dry-run of the identical logic against the post-edit tree: **26 ability-granting nodes checked, 0
failures, 1 logged owner-tag debt (see 3b)** — the suite is expected GREEN on the lead's gate.

## 3b. What the oracle caught within minutes of being written

Running the suite's logic against the live tree surfaced a SECOND, concurrent instance of the same
defect shape: **WO-1306** (landing in parallel this evening) added `mage.t1n3 -> mage.siphon`, the
mage's new cost-1 base grant. Its talent node has real tree art; `mage.siphon` has **no
`concept-icons.json` row**, so the skill the retention lens most wants a new mage to press would show
the crossed-swords default in the hot-swap slot and on the combat HUD.

That is NOT failed here, because WO-1306's own `abilities.json` comment records the row as
deliberately held for an **owner art tag**, and failing it would force the CLI to make the creative
pick the owner-tags-the-art rule reserves. It is instead carried in a named `DeliberatelyUnauthored`
allow-list **with its reason**, logged loudly every run as `OWNER-TAG DEBT`, and it is one owner tag
away from closing. Silently skipping it was not an option; that is how the original split survived.

## 4. Deliberately NOT done, and why

- **`HeroSkillTreePanelMvvm.cs` was not opened, not read into an edit, and not modified.** The WO-1310
  layout fix (axis rotation, `SolveGraphLatticePx`, lattice/pitch maths, content extents, node-plate
  label sizing) is untouched, and `TalentTreeShapeRegression` rule 6 `[viewport]` is unaffected — this
  change adds no code path that panel executes and no data field it reads.
- **`mage.heal` vs `universal.mend`** both resolve `Priest5` after the fix. They are two different heals
  both literally named "Mend" and are co-placeable for a mage. Separating them means **picking art**,
  which is the owner's call, so it is allow-listed in the oracle with its reason and recorded as
  UNAUTHORED in `concept-icons.json` rather than guessed at. **One owner tag closes it.**
- **`mage.drain` / `mage.poison`** were already flagged in-file as awaiting an owner tag; left alone.
- **Section 9 evidence** (contact sheet, three-ratio screenshots, tree -> assignment -> combat capture,
  state captures, Barracks/Manage captures) — headless cannot capture a bar. Needs a Windows player run.
- **Section 3 tree composition** — WO-1310's lane, explicitly off-limits this pass.

## 5. Retention lens

The acceptance lens for this lane was "can a new player see and use a distinct ability fast". The fix
is squarely on it: before this change a mage's shared-pool Arcane Bolt and their Manaweave were the
same picture, and three skills changed appearance between the screen where you learn them and the bar
where you press them — the two places recognition has to survive. One skill is now one picture from
tree to thumb.

## 6. Files changed

| file | brace check |
|---|---|
| `Assets/Editor/Regression/ConceptIconIdentityRegression.cs` (new) | BALANCED, clean (no NUL) |
| `Assets/Editor/Regression/DataRegression.cs` (one wiring block) | BALANCED, clean (no NUL) |
| `Assets/Resources/Data/Canonical/concept-icons.json` | JSON parses; twin copy byte-identical |
| `Assets/StreamingAssets/Data/Canonical/concept-icons.json` | JSON parses; twin copy byte-identical |
| `WorkOrders/WORK_ORDER_1294_...md` | Status -> FIXED |

No Unity gate was run and nothing was committed — that is the lead's.
