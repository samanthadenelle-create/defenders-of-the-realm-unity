# WORK ORDER 1023 — Talent icon map: 2 duplicate icons, no coverage guard, 4 unused icon pools

**Status:** FIXED 2026-08-15 (`f8b9ad32e`) — awaiting owner felt-verify. *(Status audit 2026-08-24: BUCKET CORRECTION — the prior line predated the commit and still advertised gates/commit as owed; verified at source in `git log`, `f8b9ad32e` (2026-08-15) landed this work. Body unchanged. Prior line: IMPLEMENTED — PENDING GATE)*
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1023 → 1024 in the same edit
**Lane:** Data + editor regression. File-disjoint from WO-1021 (presentation `.cs`) — run in parallel.
**Provenance:** owner ask 2026-08-15 — *"check and confirm which is the strongest match for all trees"*
and *"check all other asset folders for other icons to add to the icon repo"*. Upstream artifact =
Grok's icon repo, landed as `4b41239`.

---

## 0. Audit result — the map is GOOD. Measured, not assumed (2026-08-15)

`Assets/Resources/Data/Canonical/talent-icon-map.json`:

| check | result |
|---|---|
| talent node ids in `hero-talents.json` | 83 |
| map entries | 83 |
| unmapped nodes | **0** |
| orphan entries | **0** |
| `iconPath` disagreement vs `hero-talents.json` | **0** |
| icon PNGs missing under `Assets/Resources/Talents/` | **0** |
| Resources vs StreamingAssets copies | **byte-identical** |

**Do not "improve" the mapping wholesale.** It is complete, and each entry carries a `why` rationale.
Only the three defects below are in scope.

### Archetype coherence per tree (the "strongest match" answer)

| tree | nodes | dominant source | coherence |
|---|---|---|---|
| **mage** | 20 | Elementalist **19/20** (Arcanist 13, Pyro 3, Electro 2) + Cultist 2 | **STRONGEST** — near-single-archetype identity |
| **ranger** | 20 | Assassin **13/20** (Ranger 8, Rogue 3, Hunter 2) + Symbiose nature 4 | **STRONG** — reads hunter/nature |
| **knight** | 32 | Warrior 13/32 across **14** class folders | **WIDEST SPREAD — and correct** (see below) |
| **shared** | 11 | HolyDarkness 5, Elementalist 3, Warrior 2, Assassin 1 | deliberately mixed |

⚠ **Do NOT "fix" the knight spread.** It is not sloppiness — the knight tree carries **economy and
fortification** nodes (`Master Mason`, `Salvager`, `Foreman's Pace`, `Deep Reserves`, `Hardened
Ramparts`, `Farsight Emplacements`) that have **no Warrior-archetype equivalent** in a 25-class spell
icon set. Sourcing those from Geomancer/Enchanter/Hunter is the right call. Narrowing knight to Warrior
folders would make those six nodes *worse*.

**The governing rule, confirmed: match the SKILL's meaning, not the tree's class.** `knight.t1n2
Thunderbolt` correctly uses an Electromancer icon because the skill is lightning. Keep that principle.

---

## 1. DEFECT — two pairs of talents render the IDENTICAL icon

| source art | claimed by |
|---|---|
| `Classes/Assassin/Rogue/Rogue7.png` | `knight.t2n6` **Venombrand** + `ranger.t2n2` **Venomcraft** |
| `Classes/Elementalist/Arcanist/Arcanist1.png` | `mage.t1n1` **Arcane Focus** + `shared.n9` **Arcane Bolt** |

Two different talents showing the same picture is a recognition failure — and it matters more here than
in most games because **the owner is red/green colourblind** (memory
`owner-colorblind-delegate-visual-creative`), so silhouette/art identity carries load that hue cannot.

**Fix — re-tag ONE side of each pair.** Suggested, from classes already used by that tree:

- `knight.t2n6 Venombrand` → keep Rogue7 (the knight has no other poison node); **re-tag
  `ranger.t2n2 Venomcraft`** → another `Assassin/Rogue/Rogue*.png` poison-reading icon. Ranger already
  draws Rogue1/3/4, so pick an unused Rogue index.
- `mage.t1n1 Arcane Focus` → keep Arcanist1; **re-tag `shared.n9 Arcane Bolt`** → an unused
  `Arcanist*.png`. Shared already draws Arcanist7/11.

⚠ **Per the VFX owner-tag pattern (memory `vfx-map-owner-tags-no-creative-pick`): the UI seat picks the
final icon key, CLI wires it verbatim.** If the suggested indices don't read right against the art, hold
and bounce back — do **not** substitute a different creative pick on the implementing side.

After re-tagging, re-run `tools/apply_talent_icon_map.py` (it copies from the gitignored pack into
`Assets/Resources/Talents/`) and commit the new PNG + `.meta` + both JSON copies.

## 2. DEFECT — nothing guards any of this

`grep -rl "talent-icon-map" Assets/Editor/` → **zero hits.** Every property in §0 is true today by
Grok's care and is unpinned tomorrow.

⚠ **This is the WO-996 `armor.json` shape exactly**: two canonical copies, no oracle, drift in *both*
directions unnoticed for weeks because **Resources wins at runtime so the Editor looks fine**.

**Fix — one `DataRegression` case, `[talent-icons]`, asserting:**

1. every node id in `hero-talents.json` has exactly one entry in the map (**no unmapped, no orphan**)
2. no duplicate `blinkSource` across entries (**the §1 defect, pinned so it cannot return**)
3. every `iconPath` resolves to a real sprite under `Assets/Resources/Talents/`
4. every map `iconPath` equals the `iconPath` on the matching `hero-talents.json` node
5. the Resources and StreamingAssets copies are **identical**

Assertion 3 must load through the normal Resources path so it stays honest in a player build, not just
in the Editor.

## 3. OPPORTUNITY — four icon pools already committed and unused

Full sweep of every icon-bearing folder in the tree, 2026-08-15:

| pool | count | committed? | verdict |
|---|---|---|---|
| `Assets/Blink/Art/Icons` (500 spell + 25 emblem + 28 slot + 55 extra) | 608 | **gitignored** | **THE source.** Only **83 of 500** spell icons tapped (~17%). Headroom for every future hero. |
| `Assets/Resources/RpgUi/spellicons` | 160 | ✅ | ⚠ **Only 8 of 25 classes** (Hunter, Arcanist, Electromancer, Pyromancer, Paladin, Barbarian, Deathknight, Guardian). **Entire `Symbiose` group absent**, as are Ranger/Rogue/Priest/Cultist/Cryomancer/Geomancer/Berserker/Dragonknight — all of which the map uses. Harmless today (talent art is copied to `Resources/Talents/` instead), but any *new* consumer reading `spellicons` will hit holes. |
| `Assets/Resources/RpgUi/emblem` (25 class emblems) | 25 | ✅ | **UNUSED.** One emblem per class — natural fit for a **tree header crest** per hero (knight/ranger/mage), which the talent panel currently has no visual identity for. |
| `Assets/Resources/RpgUi/classslot` (`Slot_<Class>` ×25 + Slot1–3) | 28 | ✅ | **UNUSED.** Per-class themed node plates — could replace the generic `slot_talent_N` border **per tree**, giving each hero's tree its own frame identity at zero import cost. |
| `Assets/Blink/.../Icons_Obsidian` | 70 | gitignored | generic UI glyphs. Canon (Grok-02 §5): *"our game icons stay ours"* — **low priority, do not bulk-import.** |
| `Assets/Tech hud elements/Sprites` (Rpg icons, Sword icons, Magic bottles/healing, Skull, Badges) | 329 | **gitignored** | ⚠ **DO NOT ADOPT.** `docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md` §6.4 names the Tech-pack `Resources.Load("Tech hud elements/...")` primaries as clean-build-absent and says the migration should DROP them. Adding a new dependency on this pack moves backwards. |
| `Assets/Resources/ItemIcons` | 484 | ✅ | wrong semantic class — rendered **gear** thumbnails from `GearIconRenderer`, not spells. |
| `Assets/Blink/.../Free_Blink_Icons` | 17 | gitignored | sampler subset of the 608. Nothing unique. |

**Recommended follow-up (NOT this ticket — needs an owner call first):** wire `emblem/` as a per-tree
header crest and `classslot/` as the per-tree node plate. Both are already committed, so it is pure
wiring with no import step. Filed here as a finding; mint separately if the owner wants it.

## 4. Files in scope

| file | change |
|---|---|
| `Assets/Resources/Data/Canonical/talent-icon-map.json` | re-tag 2 entries (§1) |
| `Assets/StreamingAssets/Data/Canonical/talent-icon-map.json` | keep byte-identical |
| `Assets/Resources/Talents/**` | the 2 replacement PNGs + `.meta` (⚠ **git LFS** — icons are LFS-tracked) |
| `Assets/Editor/Regression/DataRegression.cs` | new `[talent-icons]` case (§2) |

**Do NOT touch:** `hero-talents.json` node data (costs/prereqs/positions), `HeroSkillTreePanelMvvm.cs`
(that is WO-1021 — keep the lanes disjoint), or the other 81 icon assignments.

## 5. Acceptance criteria

- [ ] Zero duplicate `blinkSource` values across all 83 entries
- [ ] Coverage still 83/83, zero orphans, both JSON copies byte-identical
- [ ] `DataRegression` `[talent-icons]` case exists and **FAILS if any of the five §2 assertions is
      broken** — prove it by temporarily breaking one and showing the red, then restoring
- [ ] `REGRESSION_OK <n>/<n> suites` with the new case counted
- [ ] The 2 replacement icons are visually distinct from their former twins **in greyscale**
      (colourblind law)
- [ ] `why` rationale written for both re-tagged entries — the field is the map's memory; never leave it
      blank

## 6. Verify

1. `COMPILE_GATE_OK` (includes the WO-434 NUL scan)
2. `REGRESSION_OK <n>/<n> suites`
3. Open the talent tree and confirm the four affected nodes render distinct art — **open the PNGs**
   (memory `headless-screenshot-verify-ui-before-build`)
4. Owner felt-verifies + closes (§13)
