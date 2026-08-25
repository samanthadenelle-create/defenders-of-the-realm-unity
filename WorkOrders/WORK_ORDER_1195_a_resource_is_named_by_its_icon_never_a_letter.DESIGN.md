# WORK ORDER 1195 — A resource is named by its ICON, never a letter (DESIGN)

- **Status:** DESIGN SPEC — READY (opens with a small OWNER DECISION GATE; see §6)
- **Lane:** 4 (UI legibility / conformance)
- **Author:** UI seat, on `claude/ui-spacing-layout-review-bqas0h` (2026-08-25)
- **Relationship to the CLI ticket:** the CLI seat created `WorkOrders/WORK_ORDER_1195_a_resource_is_named_by_its_icon_never_a_letter.md` (the symptom/evidence/constraints). **This `.DESIGN.md` supplies the design + mockup that ticket was waiting on — merge it into the ticket; do not create a duplicate WO number.**
- **Branch of record for all citations:** `origin/wip/village2-and-f8-tickets` @ `9499408` (the tree the CLI implements on). My local branch is ~a month stale; every `file:line` below was read from `wip`.
- **Deliverable type:** design spec + image-generation briefs (§7), handed to CLI for implementation. The UI seat does not write `.cs`.

---

## 1. The ask (owner)

> "If we're gonna use a chip, use a chip everywhere… In builder's mode it just says WIS. I don't want to see 30W 140I 10C. Looks like I'm reading a formula. I'd like to see a little wood symbol, 140."

A resource amount must read as **[icon] 140**, not `140I`. And it must be the **same chip everywhere** — the owner explicitly pointed at the chips already used under the Resources tab in the HUD: *"we have chips already used under resources tab in hud, reference those."*

## 2. The headline: the fix already exists — this is a REUSE job, not a new component

The HUD Resources tab the owner named is `HudKitController.BuildResourceChips` (`Assets/_Modules/HUD/Kit/HudKitController.cs:1562`), and it is built on the kit's canonical icon+number component **`ElarionUiKit.CurrencyChip`** (`Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs:784`). `CurrencyChip`:

- Resolves the resource ICON as **data** through the one allowed registry — `UiStyle.Icon(kind)` → `ConceptIconResolver` → `concept-icons.json` → `RpgUiCatalog.Get(role,name)`, with fallback `RpgUiCatalog.Get("currency","currency_"+kind)` (`ElarionUiKitObsidian.cs:833-840`). **No second icon registry.**
- Renders the amount **compact-formatted, value-only** (`WriteAmount` → `ElarionUi.CompactNumber(v)`, `:754`).
- **Bakes in the colourblind rule:** when the shape-icon resolves it drops the text tag (icon carries identity, shape not colour, `:842-857`); the letter/tag renders *only* as the no-art fallback so a chip is "never a naked number."

**So WO-1195 is: route the letter-painting formatters and the "WIS" strip through the same icon resolution the HUD chip already uses, and drop the trailing `W/I/F/C/G` letters.** No new widget.

The CLI's open design question — **TMP inline `<sprite=N>` vs a built Image+Label layout** — is settled by the code: there is **no TMP inline-sprite pipeline anywhere** in the project (`git grep '<sprite'` → 0 hits; no `TMP_SpriteAsset` exists). **Use the built layout (Image + Label / `CurrencyChip`).** A `<sprite=N>` tag would need a sprite asset that does not exist and must not be introduced (it would be a second registry).

## 3. SME findings — the full letter bucket (read-only RCA, from `wip`)

**Same formatter, written independently 6× for players, already drifted** (some use `CompactNumber`, some don't; crystal renders `C` in some places, `*N` in another; coins are `G` / "Gold" / absent):

| # | Site | file:line | Letters emitted | CompactNumber? |
|---|---|---|---|---|
| 1a | `BuildPaletteUI.CostLabel` (build-card cost) | `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:1610-1622` | `W F I C` | ✅ yes |
| 1b | `BuildStructureInfoPanel.CostLabel` (info + next-tier) | `Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs:341-350` | `W F I` + crystal as `*N`; `"Free"` | ❌ **no (drift)** |
| 1c | `BarracksPanelVM.CostStr` | `Assets/_Modules/Village/Hero/BarracksPanelVM.cs:404-412` | `W I F C G` | ❌ **no (drift)** |
| 1d | `PartyShopVM` cost | `Assets/_Modules/Village/Hero/PartyShopVM.cs:1647-1651` | `W I F C` + `" Gold"` | ✅ yes |
| 1e | `ShopVM` cost | `Assets/_Modules/Village/Hero/ShopVM.cs:724-728` | `W I F C` + `" Gold"` | ✅ yes |
| 1f | `TroopTrainingVM.CostString` | `Assets/_Modules/Village/Hero/TroopTrainingVM.cs:464-471` | `W I F` | ❌ **no (drift)** |

**The "WIS" strip** (build-mode bottom resource bar) — `Assets/_Modules/Village/BuildMode/BuildWalletRow.cs`. It draws each chip's identity as a **TMP letter**, not an image (`Chip(...)`, `:104-107`), and the letters are hardcoded in the data source `LiveWalletSource.cs:79-90` (`new WalletVM.Entry("wood", IconRoleLetter, "W", wood)`, with `IconRoleLetter = "letter"`). **Good news:** the DTO `WalletVM.Entry` already carries `IconRole` + `IconName` and its own doc says "Icons are role+name strings resolved by the View" — the strip is **icon-ready**; the source just fills it with letters. Fix = set `IconRole`/`IconName` to the concept-icon address and have `Chip` draw an Image.

**Not player-facing — do NOT change:** `Assets/Editor/Regression/DataRegression.cs:3735-3738` (an editor-only harness mirror of the letter format).

**Adjacent "spells the word out" family (NOT letters, but icon-less)** — flag for the owner in §6 whether WO-1195 should also iconify these: `BuildingUpgradeVM.cs:1572-1587` (`" Wood"/" Iron"/…`), `WallRepairController.cs:730-737` (`" wood"/" iron"/…`), `EndStateVM.cs` spoils rows (already a `{Label,Amount}` DTO shape).

**Two `ResourceCost` types feed the above (part of the drift):** `DeNelle.Core.Catalog.ResourceCost` (lowercase `wood/food/iron/crystals`) and `DeNelle.Village.ResourceCost` (PascalCase `Wood/Food/Iron/Crystals/Coins`, `EconomyService.cs:82`). The fix must handle both field shapes.

## 4. concept-icons.json — the resource icons exist (the only allowed registry)

Verified entries (`Assets/Resources/Data/Canonical/concept-icons.json:209-247`, mirrored in `StreamingAssets/`), all role `currency`:

| conceptId | role | sprite name |
|---|---|---|
| `gold` | currency | currency_gold |
| `wood` | currency | currency_wood |
| `iron` | currency | currency_iron |
| `food` | currency | currency_food |
| `crystal` / `crystals` | currency | currency_crystal |

Plural aliases (`woods`/`irons`/…) also present. **No `stone` entry** (see §5). **No `wisdom`/`magic` currency entry** — if the "WIS" strip's build-mode wallet includes Wisdom/Magic, that concept id must be authored before it can be iconified, or it keeps a text label as the documented fallback.

The sprite PNGs exist at `Assets/Resources/RpgUi/currency/currency_{wood,iron,food,crystal,gold}.png`.

## 5. ⚠ Two constraints that shape the design

**(a) ENUMERATE — never hardcode a resource-name list.** `FOUNDATIONAL_RULINGS.md` (verbatim):

> "**Never hardcode a resource-name list.** … A 'stone' written into a rule goes stale the day WO-1163 lands — that is why ruling 5 was recorded structurally."

Every site in §3 hardcodes a 4/5-resource list, and so does the reference `BuildResourceChips` (`kinds[]`/`names[]` at `HudKitController.cs:1598-1606`, no Stone, index-mapped to `e.Gold/Wood/Iron/Food/Crystals`). **Reusing that pattern *verbatim* inherits the food/stone hardcode WO-1163 will collide with.** The design therefore says: iterate a single wallet source rather than name resources. Candidate enumeration sources found: `Core/ResourceType.cs:28` (`Iron/Wood/Food/AetherCrystal` — Stone already retired by DEF-121; no Gold), `IEconomy` (`Village/IEconomy.cs:26-30`: `Coins/Wood/Iron/Food/Crystals`), and `LiveWalletSource.Refresh` which already builds an ordered `WalletVM` (`:76-90`) — the closest thing to an enumerable wallet DTO a View can iterate.

**(b) COLOURBLIND — icon must read by SHAPE, keep the name.** `FOUNDATIONAL_RULINGS.md §4`: "The owner is red/green colourblind. This rule deliberately never asks her to choose between two hues." The codebase already learned that **icon-only at chip size fails this** — `HudKitController.cs:1583-1587` adds a sibling NAME label to every resource chip because the icons "were distinguishable mainly by HUE at ~30 px — a straight breach of the colourblind rule."

> ⚠ **UNVERIFIED PIXELS — a check the CLI must do before shipping icon-only.** The `currency_*.png` files are Git-LFS pointers on `wip`; I could not confirm the four resource icons read *apart in greyscale at chip size*. By subject (log / ingot / food / crystal / coin) they are different objects and should be shape-distinct, and project law is shape-not-colour — but the CLI must **pull the LFS assets and eyeball a greyscale render at the real chip size**. If they differ mainly by tint, that is a finding to escalate. **Mitigation regardless: keep an always-visible name/tag beside the icon** in the chip contexts (as `BuildResourceChips` already does). In the tight inline cost strings, see the §6 gate.

## 6. ⛔ OWNER DECISION GATE (small)

1. **Inline cost strings — icon+number only, or icon+number+word?** In the HUD Resources tab there is room for icon + name + number. In a build-card cost line ("[wood]140 [iron]10") space is tight and the name would crowd it. Options:
   - **(i) Icon + number only** in cost strings (relies on icon shape alone). Cleanest, matches "little wood symbol, 140" — **but** leans on §5(b) passing the greyscale check.
   - **(ii) ★ Icon + number, with the name shown on the affordability/detail surfaces** (the Resources tab and the build-mode wallet strip keep the name label; the compact per-card cost line uses icon+number). **UI-seat recommendation** — honours the owner's exact phrasing on cards while keeping a named, colourblind-safe readout on the strip the owner actually reads "WIS" from.
2. **Scope of "everywhere":** does WO-1195 also convert the **word-spelling** family (§3: BuildingUpgrade, WallRepair, EndState) to chips, or only the **letter** sites + the WIS strip? (UI-seat leans: fix the letter sites + WIS strip now — that is the reported pain — and file the word-spelling family as a fast-follow so this ticket lands.)
3. **Wisdom/Magic in the WIS strip:** if the build-mode wallet shows a Wisdom/Magic pool, it has **no concept-icon** yet (§4). Author `wisdom` in concept-icons.json, or keep a text label for that one pool as the documented fallback? (owner ruling owed.)

## 7. Image-generation briefs (for the owner to render)

Reference the **existing HUD Resources-tab chip** as the visual target. Dark obsidian plate (`#050506` fill, thin `#3a3a2a`/gold inner rim), parchment-cream number (`#EAD9B0`), a small square resource icon in a left "well," the compact number to its right. **Distinguish resources by icon shape, never colour** (owner red/green colourblind).

- **Brief A — before/after, build-card cost line.** TOP: the current formula-looking string `30W  140I  10C` in cream text. BOTTOM: the same as chips — `[wood-log icon] 30   [iron-ingot icon] 140   [crystal icon] 10`. Caption: "Cost reads as icons, not a formula."
- **Brief B — the WIS strip, before/after.** TOP: a build-mode bottom bar showing letter badges `W  I  F  C  G` each with a number ("looks like WIS"). BOTTOM: the same strip with the little resource icons replacing the letters, number beside each, and a small name under/beside each icon (colourblind-safe). Caption: "Same chip as the HUD Resources tab."
- **Brief C — the reference (for fidelity).** A faithful render of one HUD Resources-tab row: name label at left, then the chip = icon well + compact number. This is the pattern the other two must match. Caption: "Reference — reuse this chip everywhere."

Icons to depict (shape-distinct, greyscale-legible): **wood = a cut log / timber**, **iron = an ingot/bar**, **food = a wheat sheaf or loaf**, **crystal = a faceted gem**, **gold = a coin/coin-stack**.

## 8. Acceptance criteria (checkable)

- [ ] **AC-1 — no player-facing resource letters.** After the fix, none of the 6 sites in §3 emit a trailing `W/I/F/C/G` (or `*N`) to a player. Grep for `+ "W"`/`+"W"` etc. in `Assets/_Modules/**` returns only the editor-only `DataRegression.cs` mirror.
- [ ] **AC-2 — icon resolves through concept-icons.json only.** Every resource glyph is obtained via `UiStyle.Icon(id)` / `CurrencyChip` (the `concept-icons.json` path). No new icon registry, no `<sprite=N>`, no hardcoded sprite stems. Proving line: a `[Flow:UI]` step logging the resolved conceptId → sprite name per chip.
- [ ] **AC-3 — one chip everywhere.** The build-card cost, the WIS strip, and the HUD Resources tab render the same icon+number atom (`CurrencyChip` or the shared `UiStyle.Icon`+Label pair). No bespoke per-screen resource glyph.
- [ ] **AC-4 — enumerated, not hardcoded.** The resource set iterates a single wallet source (`IEconomy`/`WalletVM`/`ResourceType`), so WO-1163's food→stone swap touches ONE list. No new hardcoded `{wood,iron,food,crystal}` array added.
- [ ] **AC-5 — colourblind-safe.** Name/tag label kept beside the icon on the Resources tab and the WIS strip; per §6.1 decision for cost lines. CLI has pulled LFS + eyeballed greyscale icon distinctness (§5b) and recorded the result.
- [ ] **AC-6 — compact numbers everywhere.** All six sites route amounts through `ElarionUi.CompactNumber` (fixes the 1b/1c/1f drift where they don't).
- [ ] **AC-7 — chips are read-outs, not controls.** No resource chip is grown to `MinTouchPx`; `LayoutOracle` allow-list unchanged (no waiver).

## 9. Constraints (binding)

- **ASCII-only** strings (TMP tofu on device). A resolved Image icon is fine; a `<sprite=N>` tag is not used (no pipeline — §2).
- **Never meaning by colour alone** — owner red/green colourblind (`FOUNDATIONAL_RULINGS.md §4`); icons carry meaning by **shape**, name label retained per §5(b)/§6.
- Build through **`ElarionUiKit`** — reuse `CurrencyChip` / `UiStyle.Icon`; do not hand-roll a widget (trips the `[ui-obsidian]` ratchet).
- **Enumerate**, do not hardcode a resource list (`FOUNDATIONAL_RULINGS.md`; WO-1163).
- **No second icon registry** — `concept-icons.json` is the only source (its own `_comment`: "no icon choices live in C#").
- **`LayoutOracle` TouchBaseline allow-list stays at ArmyMuster + EquipDrawer. NO waiver.**

## 10. What NOT to touch

- Do not change `DataRegression.cs:3735-3738` (editor-only, not player-facing).
- Do not introduce a TMP sprite asset or `<sprite=N>` (§2).
- Do not implement a Stone resource (out of scope; WO-1163 owns food→stone and is blocked on an owner ruling — just don't hardcode against it).
- Do not add a crystal cap or alter amounts — this ticket is presentation only.

## 11. What this is NOT

Not a new component (reuse `CurrencyChip`). Not an economy/balance change. Not the second-bank capacity readout (that is WO-1194 — held pending evidence). Bonus note for WO-1194 when it resumes: `FOUNDATIONAL_RULINGS.md` already answers its open crystal question — **crystals are UNCAPPED (`[no-crystal-cap]`, `TownBankCapacity.cs:238-242`,`:478-482`)**, so "current of capacity" is meaningless for them; show crystals as a plain compact number, not "X of Y."
