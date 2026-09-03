# WORK ORDER 1341 - Hero panel labels render twice (one string, two producers)

**Status:** FIXED 2026-09-03 - code complete and ungated; lead gates + commits, owner felt-verifies and closes
**Silo:** HUD / UI (`DeNelle.HUD`) + regression
**Reported by:** owner, device screenshot, build `2026.09.03.353742`, the HERO panel
**Owner ruling (verbatim):** *"Should match font and format of Manage screen"*
**Owner follow-up (verbatim):** *"in hero its also duplicated"*
**Severity:** high for a cosmetic. This is the **first screen a new player reads on the route we are
about to teach** - the owner has confirmed the skill tree is reached via **Hero -> Skills**, and
WO-1340 is adding the FTUE beat that teaches exactly that path. Retention is the stated top business
problem, and right now that screen is doubled and self-contradictory.

---

## 1. The defect

Every label on the Hero panel rendered TWICE, overlapping, in two different fonts, and the two
copies did not agree on the wording:

| card | copy A (serif, gold, upper) | copy B (sans, white, lower) |
|---|---|---|
| Bag | `BAG` / `Manage your items` | `BAG` / `Browse every carried item by category` |
| Equipment | `EQUIPMENT` / `Equip weapons & armor` | `EQUIPMENT` / `Review worn gear on your hero` |
| Skills | `SKILLS` / `Learn and equip abilities` | `SKILLS` / `Learn and improve hero talents` |
| Loadout | `LOADOUT` / `Customize gear sets` | `LOAD OUT` / `Choose the abilities equipped for bat...` |

Eight doubled labels on one screen - which is why the whole screen reads as duplicated.

## 2. Diagnosis - ONE STRING, TWO PRODUCERS. The second producer is a TEXTURE.

**Producer B (code, the survivor)** - `Assets/_Modules/HUD/PlayerDeckWorkspace.cs`
- title: `BuildCard` -> `ElarionUiKit.BuildObsidianButton(grid, spec.Title, ...)` (was line 96)
- subtitle: `BuildCard` -> `ElarionUiKit.Label(button.transform, spec.Purpose, ...)` (was line 217)
- copy authored in `CardsFor(PlayerDeckKind.Hero)` (was lines 381-384)

**Producer A (art, DELETED as a producer)** - the words are **BAKED INTO THE PNG**:
- `Assets/Resources/UI/ElarionMedieval/cards/bag.png` - "BAG" / "Manage your items"
- `Assets/Resources/UI/ElarionMedieval/cards/equipment.png` - "EQUIPMENT" / "Equip weapons & armor"
- `Assets/Resources/UI/ElarionMedieval/cards/skills.png` - "SKILLS" / "Learn and equip abilities"
- `Assets/Resources/UI/ElarionMedieval/cards/loadout.png` - "LOAD OUT" / "Customize gear sets"

`BuildCard` mounts those sprites as the card face and then draws its live text into the plate at
`anchorMin.x = 0.48` - which is the exact region the artist painted the words into. Hence a pixel
overlap rather than a near miss. `LOAD OUT` vs `Loadout` is the giveaway that two different authors
wrote the same string.

### It is NOT a doubled mount - checked first, as instructed
- `ObsidianNavigationWorkspace.RenderCurrent` (`Assets/_Modules/Core/UI/ObsidianNavigationWorkspace.cs:175-179`)
  **destroys every content child** before it renders. A second render cannot stack.
- `BuildShell` is idempotent (`if (_canvas != null) return;`, line 116).
- `PlayerDeckWorkspace.Install` is an `_instance`-guarded `DontDestroyOnLoad` singleton (line 36).
- The panel is registered under exactly one `PanelId` (`PanelId.HeroDeck`, line 46).
- The header's title/shadow TMP pair is deliberate and already reconciled in code
  (`ObsidianNavigationWorkspace.cs:185-196`); `modal-frame-16x9.png` carries no baked text.

So the screen has one mount and one builder. The duplication is **eight strings with two
producers each**, and one of the producers is a texture. That is why the fix lints an **art
reference** and a **format**, and does not hunt for a second `Build` call.

## 3. Why the ART is the producer that goes, and the code text survives

The kit standard is **stated outright in the reference implementation** -
`Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:606`:

> *"The approved kit cards are text-safe layered faces: illustration and border are art, while
> title, purpose, count and interaction remain live."*

Verified by opening the PNGs, not by reading comments:

| card art | consumer | baked text? |
|---|---|---|
| `cards/buildings.png` | Manage (the reference) | **NO** - illustration left, EMPTY plate right |
| `cards/quests.png` | Journey deck | **NO** - medallion left, EMPTY plate right |
| `cards/realm-store.png` | Realm deck | **NO** - illustration left, EMPTY plate right |
| `cards/bag.png` `equipment.png` `skills.png` `loadout.png` | **Hero deck** | **YES** |

The four Hero PNGs are the **only** cards in the kit that break the standard. Every other card in
the game already has exactly one producer. Therefore the art is the defect, the live text is the
survivor, and the survivor is the only one of the two that *can* be restyled to match Manage.

## 4. ART CALL FOR THE OWNER (not actioned here)

Per the standing rule, a label baked into artwork is **not re-authored by an agent**. The four PNGs
are **left untouched on disk**. What changed is that the Hero cards **no longer mount them**, so
they render through the existing text-free branch (`card-frame-empty` + the concept medallion) -
the same treatment every other non-illustrated card in the game gets.

**The ask:** re-author those four PNGs to the `buildings.png` standard - identical composition,
with the **words removed** from the right-hand plate. When they land, restoring the illustration is
a one-word-per-line change: add the art key back as the 5th `Route(...)` argument and drop that key
from `BakedLabelCardArt` in the regression. Nothing else needs to move. Until then the cards are
correct but plainer.

*Precedent: the store's network indicator was baked into `network-frame.png` and printed "Mainnet"
over a Devnet session. Same class - a string with an owner outside the code.*

## 5. The format, copied out of Manage (not invented)

Reference: `ManageScreenPanel.cs:620-635`. Every number below is now **read out of that file by the
regression** rather than restated, so Manage stays the standard by construction.

| | before (Hero deck) | after (= Manage) |
|---|---|---|
| title case | `spec.Title` ("Bag") | `ToUpperInvariant()` |
| title size | `34f`, **Bold** | `36f` |
| title align | Left | **Center** |
| title fit | `FitSingleLine(face, 22f, 34f)` | `FitSingleLine(face, 30f, 40f)` |
| purpose size | `FontLabel` (40) | `FontMicro` (32) |
| purpose align | Left | **Center** |
| purpose fit | `FitSingleLine(purpose, 16f, FontLabel)` | `FitSingleLine(purpose, 24f, 30f)` |
| purpose overflow | `enableWordWrapping=false` + `TextOverflowModes.Ellipsis` | neither (Manage sets neither) |
| text plate x0 | `0.48` / `0.27` | `0.49` illustrated / `0.27` text-free (`TextPlateX0`) |

The truncated `Choose the abilities equipped for bat...` was **TMP inserting U+2026 at render
time** because the label hard-set `TextOverflowModes.Ellipsis` with wrapping off - the source
string was ASCII. Removing the overflow mode (as Manage does) removes the generated glyph too.

Hero copy was shortened to Manage's single-line register (`"Towers, walls & gates"`,
`"Town structures & upgrades"`) so no card can truncate at the 24px floor:

| card | purpose |
|---|---|
| Bag | `Every item you carry` |
| Equipment | `Gear worn by your hero` |
| Skills | `Learn and improve talents` |
| Loadout | `Abilities equipped for battle` |

Locked copy `"Unavailable - complete its requirement first"` -> `"Complete its requirement first"`
(the `[ LOCKED ]` badge already carries the non-colour signal, per WO-1311).

## 6. Files changed

**`Assets/_Modules/HUD/PlayerDeckWorkspace.cs`**
- `CardsFor(Hero)` lines 428-431: the four `Route(...)` calls **drop their art key**, with a
  block comment naming the defect, the standard, and how to restore the art.
- `BuildCard` line 99: title uppercased **at construction**, as Manage does - one writer for the
  face string, never re-assigned later.
- `BuildCard` lines 159-181: title format = Manage's.
- `BuildCard` lines 229-235: purpose format = Manage's; ellipsis/nowrap removed.
- `BuildCard` line 215: locked badge shares the plate origin.
- new `TextPlateX0(bool)` line 245 - one owner for the plate's left edge.

**`Assets/Editor/Regression/HudLabelFitRegression.cs`** - extended, no new suite.

### Deliberately NOT touched
`HeroSkillTreePanelMvvm.cs` (WO-1310 layout solver), `tutorial-steps.json` (WO-1340), `BOARD.html`
/ `tools/board_build.py` / `tools/owner_validations.py` (WO-1339), `Assets/HeroContent`,
`PackStore` / `NightMarket*` / `packs.json` / `canon-strings.json` / `hud-areas.json`, `Enemy.cs` /
`BattleArena.cs` / `PanelManager.cs` / `BattleQuiescenceGate.cs` (WO-1337), and **the four PNGs**.
`ManageScreenPanel.cs` is **read-only** here - Manage is the standard, Hero conforms to it.
The `DeckCard_Skills` GameObject name is **unchanged**, so WO-1340's highlight anchor is safe.

## 7. Oracle - `HudLabelFitRegression` Case 6 `[deck-card-labels]`

Extended the existing suite (marker `HUD_LABEL_FIT_OK` / `HUD_LABEL_FIT_FAIL`, already registered
at `DataRegression.cs:592`). No new suite, no new marker.

- **6a one producer per label.** Each of the 4 Hero routes must carry exactly 3 string literals
  (title, purpose, concept). A 4th is an art key, i.e. a second producer in the same plate.
- **6b the label-baked PNGs are mounted by nothing.** `BakedLabelCardArt` records the four
  verified-by-eye keys; none may appear as a literal in the deck.
- **6c format parity, read OUT of Manage.** Title size / alignment / fit floors and the purpose fit
  floors are extracted from `ManageScreenPanel.cs` and required to equal the deck's. **Restyle
  Manage and this fails until Hero follows** - it cannot pass by recomputing the deck's own
  constants back at itself.
- **6d the truncation cannot return.** `TextOverflowModes.Ellipsis` / `enableWordWrapping = false`
  are banned on the purpose label (the `[ LOCKED ]` badge keeps its own, it cannot truncate).
- **6e ASCII-only** across the authored Hero block.

### Proven RED first

Simulating Case 6 against `HEAD` (the doubled state) vs the working tree:

```
=== BASELINE (HEAD, the doubled state): RED (15 failures)
   - 6a route has 4 literals: Route("Bag", "Browse every carried item by category", "inven
   - 6a route has 4 literals: Route("Equipment", "Review worn gear on your hero", "armor",
   - 6a route has 4 literals: Route("Skills", "Learn and improve hero talents", "skill", P
   - 6a route has 4 literals: Route("Loadout", "Choose the abilities equipped for battle",
   - 6b deck references baked art key "bag"
   - 6b deck references baked art key "equipment"
   - 6b deck references baked art key "skills"
   - 6b deck references baked art key "loadout"
   - 6c title size: manage='36f' deck='34f'
   - 6c title align: manage='TextAlignmentOptions.Center' deck='TextAlignmentOptions.Left'
   - 6c title fit: manage='30f, 40f' deck='22f, 34f'
   - 6c purpose fit: manage='24f, 30f' deck='16f, ElarionUi.FontLabel'
   - 6c purpose not FontMicro/Centred
   - 6d purpose hard-sets Ellipsis
   - 6d purpose disables wrapping
=== AFTER FIX (working tree): GREEN (0 failures)
```

**The RED run caught a real bug in the oracle itself and it is worth recording:** the first draft
sliced the Hero block from the first `case PlayerDeckKind.Hero:` in the file - which is
`SubtitleFor`, not `CardsFor`. It found a block with no `Route(` in it and reported `routes=0`,
i.e. it would have **passed on nothing** in both states. Case 6 now anchors on
`List<Card> CardsFor(` first, and the reason is written into the code. This is exactly why the
brief demands RED before GREEN.

## 8. Acceptance criteria

1. `HUD_LABEL_FIT_OK` on a fresh gate log (judge the marker, never the exit code).
2. `COMPILE_GATE_OK` on a fresh log; brace + NUL clean on both edited files.
3. `UI_CAPTURE_OK` and **open the PNGs**: the Hero panel shows each card's title and subtitle
   **exactly once**, centred in the right-hand plate, gold title over parchment subtitle, at the
   same size and alignment as the Manage launcher cards.
4. No label truncated and no `...` on any card.
5. Owner felt-verify: Hero -> Skills reads clean as the FTUE route (then PO closes).

## 9. Open item for the owner

Re-author `cards/bag.png`, `cards/equipment.png`, `cards/skills.png`, `cards/loadout.png` text-free
to the `buildings.png` standard. Until then the four Hero cards render with the concept medallion
instead of the painted illustration - correct and single-producer, but plainer than before.

---
*Provenance: CLI edit-only lane, 2026-09-03. Number 1341 minted from the `CLI_LANES_WO_NUMBERS.md`
banner by the lead; banner bumped in that same edit. Not gated, not committed, not built here.*
