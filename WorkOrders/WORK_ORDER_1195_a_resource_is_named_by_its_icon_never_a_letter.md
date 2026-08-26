# WORK ORDER 1195 - a resource is named by its ICON, never by a letter

**Status:** READY - PARTIAL 2026-08-25: formatter, call-site conversion, source oracle, and registration landed at `0c65af9b0` + `905fe686b`; approved stone/magic/wisdom art/mapping and headed greyscale/NEED captures remain open. Do not close.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1195 -> 1196 in the same edit)
**Silo:** UI / consistency
**Origin:** owner, 2026-08-25.

---

> *"The same thing with the one in builder's mode on the bottom - it doesn't give you a chip, it
> gives you a letter, which I've always hated. If we're gonna use a chip, use a chip everywhere.
> There needs to be the consistency you would expect. In builder's mode it just says WIS. It should
> be the chip in all of those - even in the ones talking about the price of things. When you're
> talking about building an item I don't want to see `30W 140I 10C`. Looks like I'm reading a
> formula. I'd like to see a little wood symbol, 140."*

## The law

⭐ **Wherever a quantity of a resource is shown to the player, the resource is identified by its
ICON. A single-letter abbreviation is never acceptable.**

The player should read *[wood icon] 140*, never `140W`, never `WIS`, never `30W 140I 10C`.

## Confirmed at source - and it is the SAME FORMATTER WRITTEN THREE TIMES

| Site | Shape |
|---|---|
| `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:1618-1621` | `CompactNumber(c.wood) + "W"`, `+ "F"`, `+ "I"`, `+ "C"` |
| `Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs:345-347` | `c.wood + "W"`, `+ "F"`, `+ "I"` - and it does NOT use `CompactNumber` |
| `Assets/_Modules/Village/Hero/BarracksPanelVM.cs:406` | `c.Wood + "W"` ... |

⚠ Note the second one already drifted from the first: it skips the kit's compact formatter, so a
five-digit cost renders differently in two places today. **One fact written three times, and the
copies have already diverged** - this repo's dominant failure mode, showing up in the exact code the
owner is complaining about.

⛔ **So the fix is ONE formatter, not three edits.** Every site routes through it. A fourth caller
must be unable to reinvent the letter form.

⚠ **"WIS" in the build-mode bottom bar is NOT located yet.** The owner names it explicitly. Find it
before implementing and add it to the table above - it may be a separate letter-strip rather than
this formatter.

## The engineering question this raises (section 4 - decide it, and say which you chose)

An icon beside a number inside a single text label means either:

- **(a) a TMP inline sprite** - `<sprite=...>` in the string, requiring a TMP sprite asset built from
  the same art the chips use; or
- **(b) built layout** - an `Image` plus a text element per resource, laid out as a row.

⭐ **Whichever is chosen, the ICON MUST RESOLVE THROUGH THE EXISTING DATA PATH.** The chips already
resolve their icons through the **CurrencyChip concept resolver from `concept-icons.json`**
(`gold/wood/iron/food/crystal` -> `Icons_Obsidian`). ⛔ Do NOT hardcode a sprite reference and do NOT
introduce a second icon registry - the icon choice is DATA and there is exactly one source for it.

⚠ (a) is cheaper at every call site but adds a sprite-asset build step and interacts with font
fallback; (b) is more code per site but reuses what already renders. **State the trade-off and the
choice in your report.**

## Constraints

- ⛔ **ASCII-only strings.** A `<sprite=N>` tag is ASCII and legal; a literal emoji or a non-ASCII
  glyph is NOT - it renders as a tofu box on device.
- ⛔ **Never meaning by colour alone** - the owner is red/green colourblind. The icon is the identity;
  it must be distinguishable by SHAPE, not by tint. ⚠ If wood/stone/iron icons differ mainly by
  colour, say so - that is a finding, not something to work around.
- ⛔ A cost that cannot render its icon must degrade to the **full word** (`Wood 140`), never back to
  the letter. State the fallback.
- Any tappable element stays at or above `ElarionUiKit.MinTouchPx` (112). ⚠ Most of these are LABELS,
  not controls - do not inflate a label to a touch target.

## ⚠ One canon conflict to surface, not to silently override

`RULES.md` QR-5.11 uses **`NEED 80W 30I`** as its worked example of "give every state a word + shape,
never a colour alone." That example is now superseded on the letter form - the principle (never
colour alone) stands, the abbreviation does not. ⛔ Fix that example in the same change, or a future
seat will cite canon against this ruling.

## Acceptance criteria

1. No player-facing surface renders a resource quantity with a single-letter suffix. Prove it with a
   repo-wide search for the pattern, and quote the search.
2. All cost/price surfaces route through ONE formatter. A new caller cannot produce the letter form.
3. The two formatters that already diverged (compact vs non-compact) are reconciled - one behaviour.
4. An oracle pins it: a resource quantity is rendered with an icon (or the full word fallback), never
   a letter. ⛔ Register it in `DataRegression.cs` - an unregistered oracle never runs.
   ⭐ It must go RED if someone re-adds `+ "W"` at any call site. State what makes it fail.
5. Icons resolve through `concept-icons.json`; no second registry, no hardcoded sprite.

## Related, do not conflate

**WO-1194 Part 2** is the ambient resource readout (three thin lines, current-of-capacity, a Harvest
button). This ticket is the *naming convention everywhere else* - costs, prices, build cards, barracks
training. They share the icon resolver and should agree, but they are different surfaces.
⚠ **WO-1163 retires food for stone.** Do not hardcode a four-resource list in the new formatter -
enumerate, so the set can change without touching every call site again.

---

## OWNER RULING 2026-08-25 - the ruling CONFIRMS this ticket's rule, and LOCATES it

**Owner, 2026-08-25.** Binding text lives in `FOUNDATIONAL_RULINGS.md` **section 13** (a cost is
written as an icon and a quantity, never a letter) - ⛔ cite it, do not paraphrase it here.

⭐ **What it does for this ticket:** it does not raise the bar, it **confirms the rule and names the
first place it must land.** The build screen's cost strings - currently rendered `130I` / `400W` /
`10C` - are the surface the owner called out by name, and they are exactly the letter-suffix form this
ticket exists to retire. Replace the letter with the resource chip/icon, in the place the cost string
already occupies.

⚠ This does not change any acceptance criterion above. It tells the seat where to start: criterion 1's
repo-wide search should find the build-screen cost path first, and criterion 2's single formatter is
what that path must route through.

⚠ **Scope note, so nobody over-reads it:** section 13 is about cost strings. The ambient posture-driven
HUD resource dock is out of scope and unchanged - see that section.

---

## IMPLEMENTATION SPEC 2026-08-25 - the build-screen cost strings (CLI lead)

Written from a source sweep, not from the ticket body. ⛔ **The ticket's own table was WRONG about the
size of this.** It named three sites. There are **seven** letter-form emitters, **six more** word-form
emitters that are separate copies of the same fact, and **one** renderer that already draws icon+number
by REVERSE-PARSING a formatted string through a SECOND icon registry. Thirteen copies, not three.

### 1. Every site found

**A. The letter form - RETIRED by ruling 13. Seven copies, already drifted four ways.**

| # | Site | Emits today |
|---|---|---|
| A1 | `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:1609-1623` `CostLabel` | `CompactNumber(wood)+"W"`, `+"F"`, `+"I"`, `+"C"`, joined with two spaces; `IsZero` -> empty. ⭐ **This is the surface the owner named** - the build palette card price band, placed at `:1417-1428`. |
| A2 | `Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs:341-350` `CostLabel` | `wood+"W"`, `+"F"`, `+"I"` - **no `CompactNumber`** - and crystals as **`"*"+n`**, not `"C"`. `IsZero` -> `"Free"`. |
| A3 | `Assets/_Modules/Village/Hero/BarracksPanelVM.cs:403-412` `CostStr` | `W I F C G`, no compact, joined with one space, zero -> `"Free"`. |
| A4 | `Assets/_Modules/Village/Hero/PartyShopVM.cs:1643-1653` `CostString` | compact; coins as the **word** `" Gold"`, the other four as letters. |
| A5 | `Assets/_Modules/Village/Hero/ShopVM.cs:720-730` `CostString` | byte-identical body to A4, declared **instance** instead of `static`. |
| A6 | `Assets/_Modules/Village/Hero/TroopTrainingVM.cs:463-471` `CostString` | `W I F`, no compact, no crystals, no coins. |
| A7 | `Assets/Editor/Regression/DataRegression.cs:3733-3740` `CostStr` | the letter form baked into a **test helper**, joined with `"+"`. ⚠ A regression that speaks the retired grammar teaches the next seat it is legal. |

⚠ **Four independent drifts inside one "same" formatter:** compact vs raw numbers (A1 vs A2), crystals
as `C` vs `*N` (A1 vs A2), zero as empty vs `"Free"` (A1 vs A3), and three different separators
(two-space, one-space, `"+"`). A five-digit cost renders differently in two places **today**. This is
one fact written seven times.

**B. The word form - not the letter, still not an icon, and still a copy. Six more.**

| # | Site | Emits today |
|---|---|---|
| B1 | `Assets/_Modules/Village/Buildings/NPCUpgradeStation.cs:189-197` | `"{n} Wood"`, no compact, comma-joined. |
| B2 | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs:1569-1578` | `"{n} Wood"` compact, **joined with U+00B7**. |
| B3 | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs:1580-1589` `ResourceCostString` | same, via `ResourceBuildingProgression.LabelFor`, plus `"{n} Magic"`. |
| B4 | `Assets/_Modules/Village/Items/JewelerVM.cs:319-328` `CostLabel` | `"Wood 140"` - word FIRST, number second. Opposite order to B1/B2. |
| B5 | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1415-1423` `DescribeCost` | lowercase `"400 wood, 200 food"`. |
| B6 | `Assets/_Modules/Village/Walls/WallRepairController.cs:724+` `DescribeMaterials` | lowercase `"12 wood, 4 iron"`. |

⚠ Adjacent, same family: `Assets/_Modules/Village/Hero/ShopPanel.cs:788-792` `PriceString` -> `"{n} Gold"`.

**C. The renderer that already does icon+number - and is the anti-pattern.**

`Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1798-1820` `BuildCostChips`
plus `:1909-1926` `CurrencyIconFor`. It takes B2's **finished string**, splits it on the middle dot at
`:1801`, then keyword-sniffs each token (`Consider("wood","currency_wood")` and four siblings) and calls
`RpgUiCatalog.Get("currency", name)` **directly**.

⛔ Three defects in one place, and all three are what this spec exists to prevent:
- it **reverse-parses a display string** to recover data the VM already had and threw away;
- it is a **second icon registry** - it bypasses `ConceptIconResolver` / `concept-icons.json` and
  hardcodes the five sprite names, which ticket criterion 5 forbids;
- ⛔ its separator is **non-ASCII and live**, authored at `BuildingUpgradeVM.cs:1577` and `:1588`,
  violating the ticket's own ASCII constraint on a string that reaches TMP.

⭐ `JewelerVM.cs:307-316` `BuildCostChips` is the **only site that already got the shape right**: it
emits `CostChipLineVM(currencyId, name, amount)` - structured data, icon key intact, no string to
re-parse. ⭐ **Generalise that struct; do not invent a new one.**

### 2. The single formatter - `DeNelle.Core.UI.CostFormat`. ⛔ It does NOT exist. Create it.

There is no shared cost formatter today. `ElarionUi.CompactNumber`
(`Assets/_Modules/Core/UI/ElarionUi.cs:140`) is the one shared **number** formatter and 6 of the 13
sites already call it - it is the precedent to copy, and the reason four of the others drifted is that
they did not.

⭐ **`CostFormat` returns STRUCTURED PARTS, never a display string.** A formatter that returns a string
is exactly how site C ended up parsing one back.

    // Assets/_Modules/Core/UI/CostFormat.cs   (assembly DeNelle.Core, namespace DeNelle.Core.UI)
    public readonly struct CostPart {
        public readonly string ConceptId;   // "wood" | "iron" | "food" | "crystal" | "gold" ...
        public readonly string Word;        // "Wood" - the ASCII fallback identity
        public readonly int    Amount;
        public readonly string AmountText;  // ElarionUi.CompactNumber(Amount) - ALWAYS compact
    }
    public static IReadOnlyList<CostPart> Parts(IEnumerable<(string conceptId,int amount)> raw);
    public static string Words(IReadOnlyList<CostPart> parts);   // "Wood 140  Iron 130" - fallback ONLY

- ⛔ **Zero amounts are skipped by the formatter**, so no caller decides that again.
- ⛔ **No hardcoded four/five-resource list.** Callers hand it `(conceptId, amount)` pairs; the set is
  enumerated. ⚠ WO-1163 swaps food for stone and must not require touching thirteen files a second time.
- Each of the 13 sites keeps a **thin adapter** turning its own cost type
  (`Core.Catalog.ResourceCost`, `Village.ResourceCost`, `EcoCost`, `JewelerRecipeCost`, `TroopDef`,
  `CoreCost`) into those pairs. The adapter is the only per-site code that survives.
- ⛔ Delete all seven `A*` bodies and all six `B*` bodies. ⭐ A fourth caller must be unable to
  reinvent the letter form because **there is no string to append a letter to.**

**Reachability - answered, and it is clean.** `Assets/_Modules/Village/DeNelle.Village.asmdef`
references `DeNelle.Core`, and `ConceptIconResolver` / `UiStyle` / `ElarionUi` all live in
`Assets/_Modules/Core/UI/` inside `DeNelle.Core`. ⭐ **Every site listed above already sees the icon
path. NO asmdef change is required, and none may be added** - an asmdef reference is an architecture
decision, and this change does not need one.

### 3. The composition rule - icon then quantity, in the existing slot

⛔ **No relayout, no new row, no new panel.** The cost occupies the rect it occupies now: for A1 that
is `BuildPaletteUI.cs:1424-1428`, the `MakeText` band at `(0.06,0.03)-(0.94,0.24)` of the card. The
band's rect, its neighbours and the card geometry are unchanged; what fills it changes.

⚠ **Two UI stacks are in play and a seat must not discover this mid-implementation.** A1
(`BuildPaletteUI`) is **uGUI + TMP** (`using TMPro; using UnityEngine.UI;`). A2
(`BuildStructureInfoPanel`) is **UI Toolkit** (`using UnityEngine.UIElements;` at `:34`). They cannot
share a builder.

⭐ **Choose (b) built layout, not (a) TMP inline sprites.** The trade-off, stated:
- (a) is cheaper per call site, but **UI Toolkit cannot consume a TMP sprite tag at all**, so (a)
  cannot serve A2 - it would force a second mechanism, which is the defect being fixed. It would also
  require a TMP sprite asset built from the currency PNGs, i.e. a **second copy of the art**.
- (b) costs one small builder per stack and reuses the sprites already loaded.

Two thin renderers, one input type, one icon lookup:

    // uGUI:       ElarionUiKit.CostRow(Transform parent, IReadOnlyList<CostPart>, Vector2 min, Vector2 max, ...)
    // UI Toolkit: CostRowElement.Build(IReadOnlyList<CostPart>)  -> VisualElement

- Each part renders **icon left, number right**, parts laid left-to-right across the existing rect.
- ⛔ **Both renderers resolve the sprite through `UiStyle.Icon(part.ConceptId)`**
  (`Assets/_Modules/Core/UI/UiStyle.cs:318`, which wraps `ConceptIconResolver.Resolve`). ⛔ Never
  `RpgUiCatalog.Get("currency", ...)` directly - that call at `BuildingUpgradePanelMvvm.cs:1925`
  **is the second registry** and must be deleted, not copied.
- ⚠ Icons are **not** uniformly sized on disk (see section 5): normalise **fit-to-height** to the
  band, never by raw scale.
- ⛔ Icons are **labels, not controls**. `raycastTarget = false`; do not inflate them toward
  `ElarionUiKit.MinTouchPx`.
- ⚠ `BuildPaletteUI.cs:1419` prefixes the unaffordable case with `"NEED " + CostLabel(cost)`. That is a
  **string concatenation onto the formatter's output** and it will not survive the change. Keep the
  word `NEED` as its own leading text element in the same band, before the first icon. ⭐ The WO-1010
  D9 rule it enforces - unaffordable says so **in a word**, never by red-vs-green - is untouched and
  must still hold after the rebuild.
- Site C is **rewritten, not patched**: `BuildingUpgradeVM` publishes `IReadOnlyList<CostPart>`
  alongside its text, and `BuildCostChips` consumes the parts. `CurrencyIconFor`, `LeadingNumber`, the
  middle-dot split and both middle-dot joins are **deleted**.

### 4. When the icon is missing

`ConceptIconResolver.Resolve` returns **null** for an unmapped concept or absent art - by documented
contract, silently, and today every caller just keeps a glyph fallback. ⛔ That default is not
acceptable here: silent-null is how a cost renders as a bare number with no identity at all.

The rule, in order:
1. ⛔ **Never blank.** A part with no icon renders **the full ASCII word in the icon's place**:
   `Wood 140`. The slot is filled, the identity is legible.
2. ⛔ **Never the letter.** `W`, `I`, `F`, `C`, `G` are retired and must not exist as a fallback path.
   ⭐ Because `CostPart` carries `Word` and no single-letter field, the letter is **unreachable by
   construction** - that is the point of the struct.
3. **Make it visible, not silent.** The renderer emits `FlowTrace.Warn("CostFormat", "no icon for
   concept=<id>")` via `FlowTrace.Once` per concept, so a missing mapping appears in `break-log.jsonl`
   and the F8 harness instead of being absorbed. ⛔ Per CLAUDE.md section 12, no catch swallows this.
4. The oracle (section 7) asserts every enumerated resource resolves an icon, so a missing one goes
   **RED in the gate**, not merely warm in a log.

### 5. Accessibility - load-bearing, and the art has real findings

The owner is red/green colourblind. ⛔ **An icon set separable mainly by hue fails this ticket.**
Icons must differ in **SHAPE / silhouette**, and the change is not done until it survives greyscale.

⚠ Read at source - `Assets/Resources/RpgUi/currency/` holds exactly **five** PNGs:

| Concept | File | Silhouette | Finding |
|---|---|---|---|
| wood | `currency_wood.png` | photoreal stacked logs, approx 470x400 | end-grain discs |
| iron | `currency_iron.png` | grey rectangular ingot, 271x243 | ⭐ cleanest silhouette of the five |
| crystal | `currency_crystal.png` | painted blue crystal cluster | distinct spiky form |
| gold | `currency_gold.png` | stack of coins | ⚠ discs, like wood |
| food | `currency_food.png` | **flat-vector logo: tractor + fields + water tower, 1200x1200** | ⛔ see below |

⛔ **`currency_food.png` is a stock agribusiness LOGO, not a game icon.** Flat corporate vector, a
different art language from the other four, 1200px against iron's 271px, and at build-card chip size it
collapses to an unreadable green-and-orange blob. ⚠ **It fails the greyscale check by being illegible,
not by being the wrong hue** - and it is the icon on a resource the build palette shows constantly.
⚠ This is a **finding for the owner, not something to work around**: it needs replacement art.

⚠ **Second risk: wood vs gold.** Log ends and coin edges are both clusters of discs; at roughly 24px in
greyscale they are the likeliest confusion in the set. Neither is hue-dependent, so the fix is
silhouette, not tint.

⛔ **Missing entirely:** `stone` (WO-1163's replacement for food - **no art, no `concept-icons.json`
row**), `wisdom` (though `ElarionUiKitObsidian.cs:707` declares `CurrencyKind.Wisdom` and `:144` looks
it up, getting null today), and `magic` (emitted as a word by B3). Verified against
`Assets/Resources/Data/Canonical/concept-icons.json`, which maps `gold/wood/iron/food/crystal` plus the
alias `crystals`, and nothing else in this family.

**Required:** a greyscale check on the finished build-screen capture. Desaturate the screenshot; every
cost part must remain identifiable from silhouette alone, with no hue information. ⚠ It is a capture
gate, not a source read - see section 7.

### 6. ASCII-only strings

Every string this change authors is ASCII. ⛔ Non-ASCII renders as tofu in TMP on device.
⚠ This change **removes** live non-ASCII: the middle dot authored at `BuildingUpgradeVM.cs:1577` and
`:1588` and parsed at `BuildingUpgradePanelMvvm.cs:1801`. Once parts are structured there is no
separator string at all; if any joined text survives, it joins on a plain ASCII hyphen.

### 7. Acceptance

**Dev-lane closable (source plus headless gate):**
1. `DeNelle.Core.UI.CostFormat` exists; all 13 sites in section 1 route through it; each keeps only an
   adapter. `CompactNumber` behaviour is uniform - ⭐ the compact/non-compact split between A1 and A2
   is gone, and so is the `"C"` vs `"*N"` crystal split.
2. ⭐ **A repo-wide SOURCE assertion, not a spot check.** A new oracle scans every `.cs` under
   `Assets/` for the letter-suffix pattern (a resource amount concatenated with a bare `"W"`, `"I"`,
   `"F"`, `"C"` or `"G"`) and **fails on any hit, allowlist size zero**. ⚠ The current tree has
   **27 such lines across 7 files** - that count must reach 0 and the oracle must quote its search.
   ⛔ It must be a **source** scan: a runtime oracle cannot see a call site nobody invoked, and the
   defect is precisely a formatter that got copied into places no test exercises.
3. A second oracle asserts every enumerated cost resource resolves a non-null icon through
   `ConceptIconResolver`, and that no site calls `RpgUiCatalog.Get("currency", ...)` directly.
   ⛔ It goes RED if `currency_stone` is still missing when WO-1163 lands.
4. ⛔ **Both oracles are registered in `DataRegression.cs`** - an unregistered oracle never runs -
   and `A7` (`DataRegression.cs:3733-3740`) is rewritten so the suite itself stops speaking the
   retired grammar.
5. `RULES.md:386` QR-5.11's worked example `NEED 80W 30I` is corrected. The principle stands; the
   abbreviation does not. ⛔ Left alone, a future seat cites canon against ruling 13.
6. Compile gate green (`COMPILE_GATE_OK`), `REGRESSION_OK n/n suites` on a fresh log. ⛔ Judge by the
   marker, never the exit code.

**⛔ OPS-OWNED - these need a headed capture and CANNOT be closed by a code-writing seat:**
7. A build-palette capture showing icon plus quantity in the card's existing price band, with the card
   geometry and neighbours unchanged. ⛔ "No relayout" is a visual claim; a source read cannot prove it.
8. The **greyscale** pass of that same capture (section 5). ⭐ Screenshots are the primary evidence for
   a visual defect - `FlowTrace` shows what the code believes, the capture shows what the player sees.
9. The unaffordable card still leads with the word `NEED` and is distinguishable from the affordable
   one in greyscale.
10. `currency_food.png` legibility at shipped chip size - ⚠ expected to FAIL and to route back to the
    owner as an art request, not to be quietly resized.

### 8. ⚠ Still UNRULED - do not implement it

`FOUNDATIONAL_RULINGS.md` section 13, subsection *"STILL UNRULED - the build palette card's image"*:
whether the build palette card keeps its building image is **open and NOT decided**. The owner floated
removing the image for text-plus-costs; the lead countered with a thumbnail-plus-text card. ⛔ **Neither
is recorded as decided, and a seat that implements either one is inventing a ruling.** This spec
changes the composition of the cost string inside the card and touches the card's art band not at all.


---

## OWNER ART RULINGS (2026-08-26, via the UI seat contact sheet - all four explicit choices)

Contact sheet shown at actual chip size (96px): `tmp/wo1195_icon_contact_sheet.png`.

1. **FOOD -> `Assets/Resources/HudIcons/hud_food.png`** (the distinct 332x321 art, NOT the
   byte-identical-to-currency 1200px illustration). Replaces the current chip art.
2. **MAGIC -> the Arcanist emblem** (`Assets/Resources/RpgUi/emblem/Arcanist.png`). ⛔ The
   aether shard was DISQUALIFIED: silhouette-identical to the existing crystal chip (both
   faceted shards) - fails the greyscale gate.
3. **WISDOM -> the spellbook tome** (`Assets/Resources/ItemIcons/blink_spellbook1h_01.png`).
   128px source is adequate for a 96px chip.
4. **STONE -> KEEP AS-IS.** Already mapped (`concept-icons.json` rows `stone`/`stones` ->
   `currency/currency_stone`) and pinned by `CostFormatSourceRegression.cs:126,159-173` - the
   WO's "stone is missing" premise was stale. Optional housekeeping only: downscale the 1.5MB
   source with no visual change.

Implementation notes for CLI (mechanics, not creative):
- `concept-icons.json` `role` resolves under `Resources/RpgUi/<role>/`. Arcanist is addressable
  as `{role:"emblem", name:"Arcanist"}`; hud_food and the tome live OUTSIDE RpgUi, so either
  copy/recut them into `RpgUi/currency/` under canonical names (currency_food replacement,
  currency_magic, currency_wisdom) or extend the resolver - prefer the copy, no resolver change.
- Add `magic`/`wisdom` (+ plurals) rows; keep the dual StreamingAssets copy md5-identical.
- Normalise all three fit-to-height like their siblings; icons are labels
  (`raycastTarget=false`), never inflated to MinTouchPx.
- The letter-suffix oracle allowlist stays ZERO; greyscale check of the acceptance capture must
  show coin/log/ingot/rock/crystal/food/emblem/tome all separable by silhouette.
