# STRINGS_AUDIT — Player-Facing Copy Registry

**Status:** durable registry (per memory `audit-outputs-as-known-dictionaries` — a
source-cited dictionary, not a one-off report). **Read-only audit; no code was changed.**
**Created:** 2026-08-16. **Tree audited:** `wip/village2-and-f8-tickets` @ `0e4690036`.

Every row cites `file:line` read at source this session (memory
`assert-only-what-you-read-at-source`). Where a row states "no callers" or "dead key",
that is a *measured* result of an exhaustive scan, not an inference — the scan method is
stated in [§0](#0-method--coverage-boundary).

**What this registry is for:** it is the standing answer to "where does player-visible text
live, and which of it is wrong". Fixing a row means *editing the row too* — a fixed defect
gets struck through with the WO number, never deleted, so the registry stays a dictionary of
what the copy layer has ever gotten wrong.

---

## 0. Method + coverage boundary

### What was scanned

| Corpus | Extent | How |
|---|---|---|
| Canonical JSON data | **15,258 string values** across every `.json` under `Assets/Resources/Data/` | full recursive walk of every string leaf; keys beginning `_` (metadata/provenance) excluded from player-facing verdicts |
| `en.json` key graph | all **228** non-`_` keys | each key grep'd as a quoted literal across **1,765** `.cs` / `.json` / `.uxml` / `.uss` files |
| C# string literals | **11,842** candidate literals repo-wide; **4,715** narrowed to text-rendering modules | literal extraction with `Debug.Log*` / `FlowTrace.*` / `Guard.*` / `[Tooltip]` / `[Header]` / comment lines filtered out |
| C# text sinks (second pass) | every `.text =`, `new Label(`, `ElarionUiKit.Label/Button`, toast/notify/status helper and their call sites across `Assets/_Modules` | independent sweep, run to cross-check the literal extraction; it found rows the pattern scan missed (V-00, V-03b, P-07) and is folded in throughout |
| Colour-coded UI state | affordability, difficulty, lock, placement, queue-state sites | grep of `Color.*`, `new Color(`, `style.color`, `.color =`, hex literals in UI modules, then each site read in context |

### Dual-copy verified

`Assets/Resources/Data/Canonical/` and `Assets/StreamingAssets/Data/Canonical/` were compared
byte-for-byte for all five headline files — `canon-strings.json`, `dialogues.json`, `en.json`,
`lore-fragments.json`, `tutorial-steps.json`. **All five are identical.** The dual-copy rule
(§7) is currently held.

### Coverage boundary — stated honestly

1. **Prefab-baked text was not opened.** `.prefab` / `.unity` / `.uxml` assets can carry TMP
   text set in the editor. The one lead found is real and is filed as
   [P-04](#p-04-lorem-ipsum-baked-into-blink-obsidian-prefabs) — `widget-params.json` is an
   *extract* of the Blink Obsidian prefabs and it contains lorem ipsum, which means the source
   prefabs do too. Whether those specific prefab objects are instantiated at runtime is
   **unverified** and needs a prefab-level pass.
2. **Runtime reachability is asserted only where a call chain was read.** Rows marked
   *reachability unverified* mean the string exists and is wired, but no captured run proves a
   player reaches it. Per §12 of `CLAUDE.md` this registry **locates** defects; it does not
   conclude runtime behaviour without data. Confirming the highest-severity rows wants one
   headless capture each.
3. **`Builds/` and `Library/` copies were excluded** — they are outputs, not sources.
4. **Non-canonical `Assets/Resources/Data/*.json`** (outside `Canonical/`) was walked for
   strings but not individually voice-reviewed. `orientation-recipes.json` is **JSONL, not
   JSON**, and failed the parser — it was skipped (it carries no player copy).
5. **Voice review is a judgement pass**, not a mechanical one. §6 lists what a grep for the
   retired-vocabulary set surfaced plus what reading turned up; it is not a claim that every
   line was read for register.

---

## 1. Placeholder text reachable by a player

> Ranked first because a placeholder on screen is unrecoverable at ship. Two of these are
> **live**, meaning a wired call chain delivers them to a rendering surface.

### P-01 — `journal-vault` lore stone renders a literal `[PLACEHOLDER — NOT CANON]` block

**Severity: critical. Live.** This is the seed defect and the audit confirms it end-to-end.

| Fact | Citation |
|---|---|
| The placeholder body text | `Assets/Resources/Data/Canonical/lore-fragments.json:79` |
| Flagged `"placeholder": true` | `Assets/Resources/Data/Canonical/lore-fragments.json:75` |
| Authoring note demanding replacement before ship | `Assets/Resources/Data/Canonical/lore-fragments.json:76` |
| Provenance note: paragraph 2 "is NOT canon" | `Assets/Resources/Data/Canonical/lore-fragments.json:9` |
| File header: placeholders "MUST be replaced ... do not treat a placeholder as canon" | `Assets/Resources/Data/Canonical/lore-fragments.json:2` |

The exact string a player reads:

> `[PLACEHOLDER — NOT CANON] The struck-through draft text. The Hidden Vault is a Unity-side expansion room; this fragment has no verbatim source in the narrative bible. Source from the narrative team before ship, or cut the journal-vault stone.`

**Why it reaches the screen, proved by the call chain.** The Healer's Cottage layout carries a
*clean, in-voice* inline body for this stone
(`Assets/Resources/Data/Canonical/dungeons/healers-cottage.json:262-265`) — "The seed is not the
only thing I leave…". That copy is fine. But `LoreStone.Read()` **prefers the fragment set over
the inline copy**:

```
LoreFragment fragment = _loreFragments?.Find(_def.id);
if (fragment != null && fragment.Body != null && fragment.Body.Length > 0)
{
    if (!string.IsNullOrEmpty(fragment.Title)) title = fragment.Title;
    body = fragment.Body;          // <-- the placeholder wins
}
```
`Assets/_Modules/Dungeons/LoreStone.cs:219-224`

and the fragment set **is** handed to every stone at build time —
`Assets/_Modules/Dungeons/DungeonController.cs:1280` (`stones[idx].SetLoreFragments(_loreFragments)`).
So the good inline prose is dead code and the placeholder is the shipping text. `Read()` itself
is reachable: `Assets/_Modules/Dungeons/LoreStone.cs:190` requests the shared interact button
(`MobileInteractButton.Request(this, "Read", Read)`), wired by WO-770.4 precisely to make lore
openable.

**The guard exists and is inert.** `LoreStone.IsPlaceholderFragment`
(`Assets/_Modules/Dungeons/LoreStone.cs:85-92`) was written to catch exactly this. An exhaustive
grep for the symbol across all of `Assets/` returns **exactly one line — its own declaration**.
Zero callers. Nothing tests it, nothing gates on it, no regression asserts it. The property is
the whole defence and it was never connected.

**Fix shape (three parts, all needed):**
1. Source paragraph 2 from the narrative team, or cut the `hidden-vault` stone from the layout.
2. Give `IsPlaceholderFragment` a caller — the honest one is a **gate**, not a log: a
   placeholder fragment must fall back to the inline layout copy rather than override it, and
   `FlowTrace.Warn` the substitution so it is visible in a capture.
3. Add a regression that **fails** on any `"placeholder": true` fragment whose id appears in a
   shipped dungeon layout. A guard with no test is how this one stayed inert.

### P-02 — `canon-strings.json` ships six unverified placeholder proper nouns, one of which is a shipped NPC

**Severity: high. Live for `Bryn`.**

`Assets/Resources/Data/Canonical/canon-strings.json` `_namesNotInSources` states verbatim:

> "Bryn, Mara, Tovin, Eira, Aelf, Mira were requested by the task brief but do NOT appear in
> narrative-bible.md or story.ts. **Placeholder values** mirror the requested spelling;
> **verify against canon before shipping**."

`Bryn` is not hypothetical — **Bryn the Wanderer is a shipped, speaking NPC** in the Healer's
Cottage: `Assets/Resources/Data/Canonical/lore-fragments.json:16` (`"speaker": "Bryn the
Wanderer"`), rendered through `Assets/_Modules/Dungeons/Wanderer/Bryn.cs:203`. `Mira` is spoken
of by name in shipped lore (`lore-fragments.json:52`, "Mira would have known what to say") and
carved into the vault stone (`lore-fragments.json:78`, "M.M. + A.M.").

Also carried in the same file with a self-declared unverified value: `firstLightEvent`
(`_firstLightNote`: "value here matches the requested wording but is **unverified**").

**Fix shape:** the owner ratifies these six names (a one-word answer each), the notes are
rewritten from "placeholder, verify" to a ratification line with a date — the same pattern
`_bossSyndrathNote` already uses correctly for Syndrath ("agent-authored content the owner
pulled in and **ratified 2026-05-19**").

### P-03 — `hero-talents.json` ships seven `(NEW ability — stub)` / `temp buff (V2)` notes

**Severity: medium. Reachability unverified** — these sit on `effect.note`, and whether the
talent tooltip renders `note` was not traced.

| Path | Value | Citation |
|---|---|---|
| `trees.ranger.nodes[2].effect.note` | `(NEW ability — stub)` | `Assets/Resources/Data/Canonical/hero-talents.json` |
| `trees.ranger.nodes[4].effect.note` | `(NEW ability — stub)` | same |
| `trees.ranger.nodes[14].effect.note` | `(NEW ability — stub)` | same |
| `trees.ranger.nodes[15].effect.note` | `(NEW ability — stub)` | same |
| `trees.mage.nodes[13].effect.note` | `temp buff (V2)` | same |
| `trees.mage.nodes[14].effect.note` | `(NEW ability — stub)` | same |
| `trees.mage.nodes[15].effect.note` | `(NEW ability — stub)` | same |

Each also carries a non-ASCII em dash — see [§5](#5-non-ascii-in-player-facing-strings).

**Fix shape:** first establish whether `effect.note` is rendered. If yes, these are P-01-class
defects. If no, rename the field `_note` so the underscore convention keeps it out of the
player-facing surface by construction — the same convention the rest of the canonical files use.

### P-04 — Lorem ipsum baked into Blink Obsidian prefabs

**Severity: medium. Reachability unverified — needs a prefab pass (see
[§0 boundary 1](#coverage-boundary--stated-honestly)).**

`widget-params.json` describes itself as "Extracted uGUI widget parameters from the Blink
Obsidian prefabs" (`Assets/Resources/Data/Canonical/widget-params.json`, `.note`). Four extracted
`text.content` values are lorem ipsum, which means **the source prefabs carry lorem ipsum text
objects**:

- `.prefabs[38].objects[46].text.content` — "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod…"
- `.prefabs[39].objects[5].text.content` — same
- `.prefabs[52].objects[35].text.content` — same
- `.prefabs[53].objects[13].text.content` — same

Also extracted: three `Placeholder` text objects at
`CharacterCreation/Username/Text Area/Placeholder`, `LoginScreen/Password/Text Area/Placeholder`,
`LoginScreen/Username/Text Area/Placeholder` (`.prefabs[34].objects[34]`, `.prefabs[46].objects[13]`,
`.prefabs[46].objects[15]`). Those three are **fine** — they are TMP `InputField` placeholder
slots, the legitimate use of the word, and the code path builds its own placeholder labels
(`Assets/_Modules/Onboarding/LoginPanelController.cs:557-585`,
`Assets/_Modules/HUD/BugReportView.cs:242-250`). Recorded here so a future reader does not
re-flag them.

**Fix shape:** open the four named prefabs; if the lorem objects are active in a hierarchy the
game instantiates, blank or delete them. Then add the lorem/placeholder string set to the
compile gate's scan (it already scans `.cs` for NUL bytes per `CLAUDE.md` §1 — this is the same
class of pre-ship byte check, one directory wider).

### P-05 — `ad-placements.json` ships `adProvider: "stub"`

`.global.adProvider` = `"stub"` (`Assets/Resources/Data/Canonical/ad-placements.json`). Almost
certainly an internal provider key, not display copy — but it is the value a "provider name" UI
would print, and `Assets/_Modules/Core/Ads/IAdService.cs:176` already exposes
`ProviderName => "None"`. **Verify before ship** that no surface renders the provider string.

### P-07 — Unbuilt-feature and dev-vocabulary copy shown to players

**Severity: medium. All live.** Not placeholders in the marker sense, but the same failure —
development state leaking onto the screen.

| # | String | Citation | Problem |
|---|---|---|---|
| P-07a | `"Source: … ranks shown are **placeholder** rivals until the online ladder is connected."` | `Assets/_Modules/HUD/LeaderboardVM.cs:124` | ships the literal word *placeholder* to the player |
| P-07b | `"Audio mixer not wired yet - volumes persist and apply when it lands."` | `Assets/_Modules/Settings/SettingsController.cs:216` | "not wired yet" / "when it lands" is engineering vocabulary |
| P-07c | `"Raids are turned off in this build."` | `Assets/_Modules/Village/Hero/RaidEntryBridge.cs:147` | "in this build" — players do not have builds |
| P-07d | `"Ad rewards are not available in this build."` | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1011` | same leak |
| P-07e | `"Raid under construction — battleground not in this build."` | `Assets/_Modules/Village/Hero/RaidDeployScreen.cs:510` | same leak (also §S-24, §N-53) |
| P-07f | `"Tower definition asset missing for the {name}."` | `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs:510` | exposes the asset pipeline |
| P-07g | `"No talent tree found for '{_currentHeroSlug}'."` | `Assets/_Modules/Village/Talents/TalentTreePanel.cs:207` | prints an internal slug id |
| P-07h | `$"Purchase failed - {result.Error}"` / `$"Purchase failed - {ex.Message}"` | `Assets/_Modules/Wallet/PackStore.cs:535`, `:543` | **raw exception text to the player, on a payment surface** — highest-consequence row in this table |
| P-07i | `$"Transaction failed: {result.Error}"` | `Assets/_Modules/Village/Buildings/TowerSwapService.cs:235` | same, raw provider text |
| P-07j | `"Training: (queue offline)"` | `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs:557` | "offline" has no player meaning here |
| P-07k | `"Coming soon"` / `"Coming Soon"` ×3 + `"Abilities revealed at launch"` | `Assets/_Modules/Onboarding/HeroSelectController.cs:344`, `:436`, `:617`, `:513` | legitimate if the classes really are gated at launch — **owner call**, but note `:617` puts it on the **primary CTA button** |
| P-07l | `"Coming soon"` | `Assets/_Modules/Wallet/PackStore.cs:382` | store card chip; same owner call |
| P-07m | `"Nothing here yet - check back next season."` | `Assets/_Modules/HUD/CosmeticShopPanel.cs:389` | **good** phrasing of an unbuilt state — cite this as the pattern for P-07a..e |
| P-07n | `"Outpost sealed -- reopening in a future update"` | `Assets/_Modules/Village/World/RetiredSeamNotice.cs:54` | **good** — in-world framing, names the resolution |

**Fix shape:** P-07m and P-07n show the house pattern — say it in world terms and name when it
resolves. P-07h/P-07i must never print an exception; catch, log via `FlowTrace.Fail`, and show a
written message.

### P-06 — Not a defect: `population-milestones.json` "PLACEHOLDER" numbers

`_authoringNotes` says "Numbers are PLACEHOLDERS — owner-tune here with NO code change". This is
tuning data under an `_`-prefixed metadata key, not copy. **No action.** Listed so the next
scan does not re-raise it.

---

## 2. `[[missing:]]` markers and broken lookups

### The mechanism

`VillageStrings.Resolve` returns a **visible on-screen debug marker** for any key it cannot find:

```
Debug.LogWarning($"[VillageStrings] Missing canonical key '{key}'.");
return $"[[missing:{key}]]";
```
`Assets/_Modules/Village/VillageStrings.cs:131-132`

`BuildingName` has a second one for a null def — `return "[[missing:building]]"`
(`Assets/_Modules/Village/VillageStrings.cs:76`). The pattern is deliberate and good (loud
failure beats a blank label), but it means **any missing key is a raw debug token in the player's
face**, so the key graph has to be airtight.

### M-01 — Two `buildingDesc.*` keys are referenced by data and absent from `en.json`

**Severity: high (latent — arms the moment building flavour text is wired).**

| Referenced key | Referenced at | Present in `en.json`? |
|---|---|---|
| `buildingDesc.market` | `Assets/Resources/Data/Canonical/buildings.json:134` (building `market`) | **NO** |
| `buildingDesc.jeweler` | `Assets/Resources/Data/Canonical/buildings.json:147` (building `jeweler`) | **NO** |

`en.json` carries exactly seven `buildingDesc.*` keys — `crystalMine` (`:170`), `petHouse`
(`:171`), `arcaneTower` (`:172`), `farm` (`:173`), `workshop` (`:174`), `lumbermill` (`:175`),
`forge` (`:176`). Neither `market` nor `jeweler` is among them.

Latent **only** because `VillageStrings.BuildingDescription`
(`Assets/_Modules/Village/VillageStrings.cs:81-85`) currently has no callers. The first panel to
show building flavour text prints `[[missing:buildingDesc.market]]` on the Store card and
`[[missing:buildingDesc.jeweler]]` on the Jeweler card.

**Fix shape:** author the two missing entries in `en.json` (and the StreamingAssets twin), in
the register of the existing seven. Then add a regression that walks `buildings.json`
`descriptionKey` values and asserts each resolves in `en.json` — the check that would have caught
this at authoring time.

### M-02 — `armorer` reuses the `forge` description (wrong flavour text, not a missing key)

`Assets/Resources/Data/Canonical/buildings.json:120` — the building `armorer` (displayName
`blacksmith`) carries `"descriptionKey": "buildingDesc.forge"`, the *same key* already used by
the `forge` building at `:105`. It resolves, so no marker appears — but the Blacksmith would
show the Armorer's copy. A key-coverage test would pass this; only a uniqueness check catches
it. Given `buildingDesc.forge` reads "The armorer's fire never cools" (`en.json:176`) this may
be deliberate — **owner call**, flagged rather than asserted.

### M-03 — 194 of 228 `en.json` keys are dead

**Severity: high — this is the headline structural finding of the audit.**

Measured: each of the 228 non-`_` keys grep'd as a quoted literal across 1,765 source files.
**34 are referenced. 194 are not.** `en.json` is 85% dead weight, which means the file is not a
trustworthy answer to "where does this string live" — and that is exactly why hardcoded literals
keep getting written instead (see [§3](#3-silent-refusals)).

**The 34 live keys**, for the record:

| Family | Keys | Consumer |
|---|---|---|
| `buildingDesc.*` (7) | `arcaneTower`, `crystalMine`, `farm`, `forge`, `lumbermill`, `petHouse`, `workshop` | `buildings.json` (`descriptionKey`) |
| `hero.*` (12) | `{mage,knight,ranger,cleric}.{name,role,blurb}` | `Assets/_Modules/Onboarding/HeroCatalog.cs` |
| `heroSelect.*` (3) | `title`, `subtitle`, `diveVillage` | `Assets/_Modules/Onboarding/HeroSelectController.cs` |
| `intro.coldOpen.*` (3) | `line1`, `line2`, `line3` | `Assets/_Modules/Onboarding/CanonStrings.cs` |
| `petSelect.*` (3) | `title`, `subtitle`, `confirm` | `Assets/_Modules/Onboarding/PetSelectController.cs` |
| `tutorial.steps.*` (6) | `1`, `2`, `3`, `5`, `6`, `forceField` | `Assets/_Modules/Onboarding/OnboardingFlow.cs:202-210` |

**Dead families, grouped** (all 194; each is a family that a surface either never had or now
hardcodes):

| Dead family | Count | Note |
|---|---|---|
| `tooltip.*` | 54 | The entire tooltip corpus — every resource, every button, every ability tooltip. Nothing reads any of it. |
| `heartVoice.*` (+`.alt.*`) | 19 | Heart-state narration |
| `wave.warning.*` | 12 | Per-element wave warnings |
| `victory.*` / `defeat.*` | 16 | Outcome copy |
| `petAmbient.*` / `petCaption.*` | 13 | Also retired vocabulary — see [§6](#6-voice-inconsistency) |
| `tutorial.first*` | 11 | Superseded by `tutorial-steps.json` |
| `keeperAmbient.*` | 8 | Keeper journal lines |
| `heartDamage.*` (+`.alt.*`) | 9 | Damage-threshold narration |
| `swap.*` | 14 | **See M-04 — actively contradicted by code** |
| `milestone.*` | 6 | First-time milestone copy |
| `shopkeeperPanic.*` | 6 | |
| `gate.first*` | 3 | |
| `elementBlurb.*` / `resourceBlurb.*` | 6 | |
| `movementHint.*` | 3 | |
| `title.tagline*` | 3 | **Also retired copy — see [§6](#v-01--the-en-json-tagline-is-the-retired-one)** |
| `realmMap.revealHint`, `returningPlayer`, `heroSelect.confirm`, `heroSelect.jumpAction`, `tutorial.steps.4`, `tutorial.steps.7` | 6 | singletons |

**Note on `tutorial.steps.4` and `.7`:** they are dead while `.1/.2/.3/.5/.6` are live, because
`OnboardingFlow`'s beat table (`Assets/_Modules/Onboarding/OnboardingFlow.cs:202-210`) simply
does not use them. Harmless, but it is the tell that this file drifted rather than being
maintained.

**The structural fact behind the number: there is no localization layer in the runtime path.**
Only two consumers read strings from data at all — `Assets/_Modules/Onboarding/CanonStrings.cs`
(`Locale(key)` against `en.json`, which is why all 34 live keys are Onboarding or
`buildings.json`) and the Guide/Glossary catalogs
(`Assets/_Modules/Village/UI/Guide/GuideContentCatalog.cs:75`,
`Assets/_Modules/Village/UI/Guide/GlossaryCatalog.cs:83`). **Every other player-facing string in
the game is a literal compiled into an assembly.** The 194 dead keys are not neglect; they are
the shape of a string table that was authored and then never wired.

**The one place someone tried to bridge it** — `Assets/_Modules/Village/Walls/WallRepairStrings.cs`
is the only file in the tree that carries `// LOCALIZE:` key comments next to its literals
(`:45`-`:73`). **Nothing consumes those keys.** It is the intent, preserved and unconnected —
the same shape as §P-01's inert guard.

**Fix shape:** this is a decision, not a patch. Either (a) declare `en.json` the string table
and start *reading* it — a large, real piece of work; or (b) mark the dead families
`_deprecated` / move them to an archive file so the live 34 are unambiguous. What must not
continue is the current state, where the file *looks* authoritative and is 85% fiction.

### M-04 — The swap panel hardcodes copy that already exists as `en.json` keys

`Assets/_Modules/Web3/JupiterSwapPanelController.cs:17`, in its own header comment:

> "v1 uses hardcoded English strings (the `swap.*` keys exist in en.json for a [later pass])"

Fourteen keys — `swap.title`, `swap.inputLabel`, `swap.outputLabel`, `swap.rateLabel`,
`swap.feeLabel`, `swap.networkLabel`, `swap.confirm`, `swap.poweredBy`, `swap.statusEnter`,
`swap.statusLoading`, `swap.statusConnect`, `swap.statusSigning`, `swap.statusError`,
`swap.statusFailed` (`en.json:251-264`) — sit unread while the panel renders its own literals.
This is the M-03 failure mode caught in the act, with the author's own comment as the citation.

Two of those unread keys are also the only `…` (U+2026) ellipsis characters in `en.json`
(`swap.statusLoading` "Getting rate…", `swap.statusSigning` "Sending to wallet for approval…") —
so wiring them naively would import a tofu bug. See [§5](#5-non-ascii-in-player-facing-strings).

### M-05 — No `en.json` key resolves through a fallback; every miss is a visible marker

Recorded as a property, not a defect. `VillageStrings.Locale` and `.Canon`
(`Assets/_Modules/Village/VillageStrings.cs:51-66`) have **no** default-value overload. There is
no way to ask for a key "softly". Any future caller that requests an optional key gets
`[[missing:…]]` on screen. If optional keys are wanted, that overload is the place to add them.

---

## 3. Silent refusals

> The pattern that cost the owner the most time: copy that reports a *state* without naming an
> *action*. A refusal is only finished when the player knows what to do next.

### The project already knows how to do this — three reference implementations

Cite these when writing new refusal copy; they are the house standard and they are good.

| Reference | Citation | Why it works |
|---|---|---|
| `"No troops yet - train troops at the Barracks to start a raid."` | `Assets/_Modules/Core/HudModel/HudActionBarModel.cs:256` | names the blocker, the place, and the goal |
| `"This cannot be bought. Wait it out, or watch an ad to speed it up."` | `Assets/_Modules/Core/Jobs/JobRushPolicy.cs:139` | a hard refusal that still offers two exits |
| `"No troops to deploy — train at the Barracks first."` | `Assets/_Modules/Village/Troops/RaidDeployController.cs:779` | (fix the em dash, keep the copy) |

And the best structural example in the tree — `BuildMenuVM` makes the **CTA label itself** carry
the reason:

```
/// The Build CTA's label: the verb when it can be paid for, the concrete reason
/// when it cannot. The button STATES its state; the grey face only reinforces it.
public string BuildCtaLabelFor(TowerBuildOption option)
{
    if (option.IsEmpty) return "Build";
    return CanAfford(option.Cost) ? "Build" : ShortfallFor(option.Cost);
}
```
`Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:536-541`

`UpgradeCostLineFor` in the same file (`:546-558`) splits what was one collapsed "cost == 0" case
into **five distinct player-legible states** — `NotBuilt` → "The crew is still raising this
tower.", `Maxed` → "Fully upgraded - Lvl {n} of {max}.", `Free` → "Costs nothing - the next level
is free.", `Unpriced` → "This tower cannot be upgraded any further.". That is the altitude the
rest of the copy should be written at.

### S-tier: refusals that name no next action

**Ranked by how often a player hits them.**

| # | String | Citation | What it should say instead |
|---|---|---|---|
| S-01 | `"Not enough resources"` | `Assets/_Modules/Village/BuildMode/BuildFeedbackToast.cs:139` | Name the axis and the gap — the same file already has per-axis strings at `BuildModeController.cs:3149-3152`; use those and drop the generic. |
| S-02 | `"Not enough resources."` | `Assets/_Modules/Village/Crafting/GearCraftingService.cs:86` | "Need 40 more Iron. Mine it, or buy Iron at the Store." |
| S-03 | `"Not enough resources."` | `Assets/_Modules/Village/Crafting/JewelerCraftingService.cs:78` | as S-02 |
| S-04 | `"Not enough resources."` | `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs:461` | as S-02 |
| S-05 | `"Army cap full - deploy or expand."` | `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs:460` | **near-pass** — "expand" does not say *how*. "Army cap full - deploy your warband, or upgrade the Barracks." |
| S-06 | `"Army cap full or not enough resources."` | `Assets/_Modules/Village/Hero/TroopTrainingVM.cs:263` | Two different problems in one string, so it names neither. Split into the two real cases. |
| S-07 | `"Army is full."` | `Assets/_Modules/Village/Troops/BarracksService.cs:334` | as S-05 |
| S-08 | `"Training queue is full."` | `Assets/_Modules/Village/Troops/BarracksService.cs:351` | "Training queue is full (5 of 5). Wait for a unit to finish, or buy a slot." |
| S-09 | `"Queue full - "` | `Assets/_Modules/Village/Troops/ArmyMusterPanel.cs:466` | as S-08 |
| S-10 | `"Builders queue is full ({n}/…"` | `Assets/_Modules/Village/BuildMode/BuildModeController.cs:1881` | states the count (good) but no exit. Add "Finish a job, or buy a slot in Manage." |
| S-11 | `"Not enough Glimmer."` | `Assets/_Modules/HUD/CosmeticShopPanel.cs:489` | name the shortfall and where Glimmer comes from |
| S-12 | `"Not enough gold for "` | `Assets/_Modules/Village/Hero/PartyShopVM.cs:1116` | name the gap; say sell-to-vendor |
| S-13 | `"Not enough resources for "` | `Assets/_Modules/Village/Hero/ShopVM.cs:442` | as S-12 |
| S-14 | `"Need more "` | `Assets/_Modules/Village/Hero/GearProgression.cs:301` | fragment — completes to a bare resource name, still no action |
| S-15 | `"Need more "` | `Assets/_Modules/Village/Troops/BarracksService.cs:123` | as S-14 |
| S-16 | `"Economy unavailable."` | `Assets/_Modules/Village/Hero/PartyShopVM.cs:1113` | This is an internal failure shown to a player. Either recover, or say "Something went wrong. Reopen the shop." |
| S-17 | `"Economy unavailable."` | `Assets/_Modules/Village/Hero/ShopVM.cs:429` | as S-16 |
| S-18 | `"Upgrades unavailable."` | `Assets/_Modules/Village/BuildMode/BuildModeController.cs:2452` | as S-16 |
| S-19 | `"Queues unavailable."` | `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:536` | as S-16 |
| S-20 | `"Work queue unavailable."` | `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:230` | as S-16; **also** uses retired "Work" — see [§6](#6-voice-inconsistency) |
| S-21 | `"That raid is unavailable right now."` | `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:306` | "right now" implies a wait with no duration. Say why and when. |
| S-22 | `"No raids available."` | `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:208` | empty state with no path in |
| S-23 | `"This raid has no battleground yet."` | `Assets/_Modules/Village/Hero/RaidDeployScreen.cs:502` | Honest, but tells the player nothing to do. Pair with S-24. |
| S-24 | `"Raid under construction — battleground not in this build."` | `Assets/_Modules/Village/Hero/RaidDeployScreen.cs:510` | dev-facing phrasing (`"in this build"`) leaking to players; also non-ASCII |
| S-25 | `"No gear available for this slot."` | `Assets/_Modules/Village/Hero/EquipmentPanel.cs:911` | "…Craft or buy a helm at the Armorer." |
| S-26 | `"No troops available."` | `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs:236` | as S-25 |
| S-27 | `"No entries yet."` | `Assets/_Modules/HUD/LeaderboardVM.cs:153` | "…Clear a raid to post a score." |
| S-28 | `"No clan yet"` | `Assets/_Modules/HUD/ClanChatVM.cs:167` | "…Join or found a clan from the Clan menu." |
| S-29 | `"Nothing to drop."` | `Assets/_Modules/Village/Hero/InventoryVM.cs:378` | |
| S-30 | `"Nothing to do for that item."` | `Assets/_Modules/Village/Hero/PartyShopVM.cs:573` | reads as a bug report, not copy |
| S-31 | `"That item cannot be used."` | `Assets/_Modules/Village/Hero/InventoryVM.cs:338` | say *why* — wrong class? wrong place? |
| S-32 | `"That item cannot be equipped."` | `Assets/_Modules/Village/Hero/InventoryVM.cs:388` | as S-31; `PartyShopVM.cs:594` has the same string as `"That item can't be equipped."` — **inconsistent contraction across two panels for one concept** |
| S-33 | `"That item can't be improved."` | `Assets/_Modules/Village/Hero/PartyShopVM.cs:650` | |
| S-34 | `"That cannot be used."` | `Assets/_Modules/Village/Hero/BagConsumableUseEffect.cs:53` | |
| S-35 | `"Cannot be used during a fight."` | `Assets/_Modules/Village/Hero/BagConsumableUseEffect.cs:58` | **near-pass** — implies "after the fight". Say it: "…Use it once the fight ends." |
| S-36 | `"Already at full health."` | `Assets/_Modules/Village/Hero/BagConsumableUseEffect.cs:76` | benign, but "Save it for later." finishes the thought |
| S-37 | `"That cannot be re-polished."` | `Assets/_Modules/Village/Crafting/JewelPolishService.cs:239` | |
| S-38 | `"Cannot empower now."` | `Assets/_Modules/Village/Buildings/UI/TowerEmpowerButton.cs:165` | "now" again implies a wait with no tell |
| S-39 | `"This tower cannot be upgraded any further."` | `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs:706` + `BuildMenuVM.cs:553` | terminal state, acceptable — logged for completeness |
| S-40 | `"Already at max level."` | `Assets/_Modules/Village/Hero/GearProgression.cs:239` | terminal, acceptable |
| S-41 | `"That skill is already on the bar."` | `Assets/_Modules/Village/Talents/HeroLoadoutVM.cs:174` | |
| S-42 | `"No free slot (or already on the bar)."` | `Assets/_Modules/Village/Talents/HeroLoadoutVM.cs:193` | The parenthetical means the code does not know which happened, and hands that ambiguity to the player. Split it. |
| S-43 | `"That slot is already empty."` | `Assets/_Modules/Village/Talents/HeroLoadoutVM.cs:204` | |
| S-44 | `"Can't change skills during battle."` | `HeroLoadoutVM.cs:147` + `HeroSkillTreeVM.cs:1351` | **near-pass**; add "Change them after the fight." |
| S-45 | `"Can't"` | `Assets/_Modules/Village/Talents/HeroLoadoutPanelMvvm.cs:144` | A one-word refusal. Worst string in the registry for information density. |
| S-46 | `"Outpost already claimed."` | `Assets/_Modules/Village/UI/EndState/EndStateVM.cs:405` | |
| S-47 | `"No ad available right now."` | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1016` | "…Try again in a few minutes." |
| S-48 | `"Ad skip unavailable right now."` | `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:406` | as S-47 |
| S-49 | `"Extra slot: unavailable"` | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:515` | the sibling at `:508` (`"Extra slot: locked - awaken a 3rd Echo"`) is a **pass**; this fallback branch is not |
| S-50 | `"Already built"` | `Assets/_Modules/Village/BuildMode/BuildFeedbackToast.cs:141` | |
| S-51 | `"You already have a Warden"` | `Assets/_Modules/Onboarding/PetSelectController.cs:329` | in the FTUE, so it hits new players first |
| S-52 | `"Referral reward already claimed."` | `Assets/_Modules/Core/Referral/InviteFriendsUI.cs:180` | |
| S-53 | `"You've already used a referral code."` | `ReferralService.cs:199` + `:294` | |
| S-54 | `"You've already redeemed that code."` / `"This code has already been used."` / `"You've already redeemed the maximum number of promo codes."` | `Assets/_Modules/Core/Promo/PromoCodeService.cs:106`, `:223`, `:225` | |
| S-55 | `"That code has expired."` | `Assets/_Modules/Core/Promo/PromoCodeService.cs:224` | |
| S-56 | `"That email is already registered."` | `Assets/_Modules/Core/Auth/FirebaseAuthService.cs:82` | on a sign-**up** form this must say "Sign in instead." — the sibling constants at `:86-89` (`TooManyAttempts`, `NetworkError`, `RetryHint`) all correctly name an action, so this one is the outlier in its own file |
| S-57 | `"Live model unavailable"` / `"Portrait view - live model unavailable"` | `Assets/_Modules/Village/Hero/EquipmentPanel.cs:1272-1273` | internal failure shown verbatim to the player |
| S-58 | `"No polish attempts left for this stone."` | `Assets/_Modules/Core/Catalog/DungeonRunPayout.cs:138` | |
| S-59 | `"Nothing to muster - the army is empty."` | `Assets/_Modules/Village/Troops/ArmyMusterService.cs:220` | **near-pass** — states the cause; add the Barracks pointer |
| S-60 | `"Cancelled. Nothing to refund."` | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1050` | **See note below.** |

### S-tier addendum: rows found in the second sweep

| # | String | Citation | Note |
|---|---|---|---|
| S-61 | `"Locked"` | `Assets/_Modules/Village/BuildMode/BuildFeedbackToast.cs:140` | **The build-refusal toast is a split case.** `:135`-`:138` are *good* — "Ground is too uneven here", "Too close to another building", "Would block the gate", "Outside the build area" all name the problem spatially. `:139`-`:142` (`"Not enough resources"`, `"Locked"`, `"Already built"`, `"Can't build there"`) are the dead ends. Fix four strings in one file. |
| S-62 | `"Can't build there"` | `Assets/_Modules/Village/BuildMode/BuildFeedbackToast.cs:142` | the default case — worst of the four, since it fires when nothing else matched |
| S-63 | `"Could not cancel that."` / `"Could not move that."` / `"Could not buy a slot."` / `"Could not finish that."` | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1053`, `:1064`, `:1081`, `:990` | four sibling notices, none says why |
| S-64 | `"Could not buy a slot."` | `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:384` | duplicate of S-63 on the legacy HUD |
| S-65 | `"Swap unavailable right now."` / `"Could not read wallet balance."` | `Assets/_Modules/Village/Buildings/TowerSwapService.cs:177`, `:202` | on a paid surface |
| S-66 | `"Payment failed. Please try again."` | `Assets/_Modules/Village/Buildings/TowerSwapService.cs:227` | "try again" is not a next step when the cause is unknown; same at `Assets/_Modules/Web3/SwapVM.cs:239`, `:247` |
| S-67 | `"Service unavailable. Restart the game."` | `Assets/_Modules/Core/Promo/PromoCodeUI.cs:188` | "restart the game" is a non-answer |
| S-68 | `"Can't upgrade the Barracks."` / `"Can't upgrade this troop."` | `Assets/_Modules/Village/Hero/BarracksPanel.cs:393`, `:403` | |
| S-69 | `"No inventory."` / `"Nothing happens."` / `"That had no effect."` / `"No hero to equip."` | `Assets/_Modules/Village/Hero/InventoryVM.cs:339`/`:372`, `:347`, `:359`, `:389` | `"Nothing happens."` and `"That had no effect."` read as bugs |
| S-70 | `"CRAFT - unavailable"` / `"SET GEMS - not enough resources"` | `Assets/_Modules/Village/Items/CraftingPanelMvvm.cs:249`; `JewelerPanelMvvm.cs:275` | **their siblings do it right** — `CraftingPanelMvvm.cs:248` `"CRAFT - missing {n} ingredient(s)"` and `JewelerPanelMvvm.cs:272`/`:274` name the gap. Only the fallback branches fail. |
| S-71 | `"No recipes loaded."` | `Assets/_Modules/Village/Crafting/VillageCraftingPanel.cs:146` | "loaded" is dev vocabulary; contrast the good `CraftingPanelMvvm.cs:144` `"No recipes.\nDefeat enemies to gather ingredients."` |
| S-72 | `"No listed bonuses for this tier."` / `"This building has no enhancement path yet."` | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:964`, `:804` | |
| S-73 | `"No talents to show yet."` | `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs:458` | |
| S-74 | `"No defenders registered."` / `"No troops registered."` | `Assets/_Modules/Village/Arena/ArenaDefensePaletteUI.cs:194`; `ArenaAttackPaletteUI.cs:181` | "registered" is dev vocabulary |
| S-75 | `"Audio not ready."` | `Assets/_Modules/Audio/MusicSelectionPanel.cs:170` | |
| S-76 | `"No Target"` | `Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs:1753` | target frame cleared state — acceptable as a frame label, logged for completeness |
| S-77 | `"Locked"` | `Assets/_Modules/Dungeons/ComposedLockedPort.cs:35` | the bare default; **`:47` `"Locked - need key"` is the good variant in the same file.** Make `:35` match `:47`. |
| S-78 | `"Locked"` / `"LOCKED"` | `Assets/_Modules/HUD/CosmeticShopPanel.cs:475`; `Assets/_Modules/Onboarding/HeroSelectController.cs:430` | see also §4 — these are the word that *should* accompany a colour, so they pass §4 while failing §3 |

**More reference implementations found in the second sweep** — cite these alongside the three at
the head of §3: `Assets/_Modules/Village/BuildMode/BuildFeedbackToast.cs:135-138`;
`Assets/_Modules/Village/Hero/RaidDeployScreen.cs:482`, `:519`;
`Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:124`, `:125`;
`Assets/_Modules/Village/Troops/ArmyMusterPanel.cs:268` ("No troops unlocked yet - upgrade the
Barracks."); `Assets/_Modules/Village/Walls/WallRepairStrings.cs:69`, `:73`;
`Assets/_Modules/Village/Hero/HeroAbilities.cs:852` ("{name} cancelled - you moved. Stand still
to shoot."); `Assets/_Modules/Village/Items/CraftingPanelMvvm.cs:144`;
`Assets/_Modules/Onboarding/LoginPanelController.cs:388`, `:398` (both offer "tap Play as Guest
to start now" — a refusal that always leaves a door open).

### On S-60 — the refund line: the money bug is fixed, the copy gap is not

The reported defect ("a cancel that says *Nothing to refund.* for gold that WAS taken") **was
fixed today**. `ManageScreenVM.Cancel` now has three branches
(`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1041-1050`):

- `:1045` — `"Cancelled. Refunded " + refunded.Describe() + "."`
- `:1048` — `"Cancelled. The " + unrefunded + " spent on it is not returned."`
- `:1050` — `"Cancelled. Nothing to refund."`

and the fix is **pinned by a regression** — `EconomySweepRegression`
(`Assets/Editor/Regression/EconomySweepRegression.cs:273`, `:281`) fails the gate if the
"Nothing to refund" literal survives without the unrefunded-currency branch. The comment at
`Assets/_Modules/Village/Buildings/BuildTimerService.cs:1226` records the original defect, and
`Assets/_Modules/Core/State/SaveMigrator.cs:703` handles the pre-v37 job case honestly
("Cancelling one refunds ZERO — the charge was never recorded and is not reconstructable").

**This is the model for how a copy defect should be closed** — fix, then pin with a test that
fails on regression. What remains is only the §3 rule: branch `:1050` is *honest* but still names
no next action. Low severity; listed for completeness, not as an open bug.

### S-tier note on the two "silent" defects the owner named

- **"a raid button that vanished rather than explaining"** — fixed; the replacement is S-tier
  reference row `HudActionBarModel.cs:256`.
- **"a crate that auto-targeted with no prompt"** — a *missing* string, so it is invisible to a
  string audit by construction. Recorded here so the class is not forgotten: **absent copy is
  the hardest silent refusal to find, and no scan of existing strings can surface it.** The only
  net is the §12 discipline — a `FlowTrace.Warn` on every branch that changes state without
  telling the player.

---

## 4. Colour-only meaning

> Binding: the owner is red/green colourblind (memory
> `owner-colorblind-delegate-visual-creative`). Meaning must never ride on hue.
> **"Matches the existing pattern" is not a pass** — the established patterns are themselves
> offenders.

### The house rule, already written down in the codebase

```
/// One ASCII line stating what a build costs AND what is on hand, per axis:
/// "Wood: 70 (+250), Iron: 40 (-12)". Zero axes are omitted. The +/- mark is the
/// TEXT encoding of affordability - the owner is red/green colourblind, so a colour
/// may only ever reinforce a state that is already spelled out.
```
`Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:495-499`

**"A colour may only ever reinforce a state that is already spelled out"** is the rule. Every row
below is measured against it.

### Sites that PASS — cite these as the pattern

| Site | Citation | The word/glyph that carries it |
|---|---|---|
| Raid difficulty badge | `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:273` | The tint at `:229`/`:255`/`:271` is green/yellow/red — **but** `DifficultyLabel(...)` (`:329-332`) prints `Regular` / `Hard` / `Extreme` into the badge chip. The colour reinforces a word. **Not an offender** — the brief's suspicion is discharged. |
| Arena raid CTA | `Assets/_Modules/Village/Arena/ArenaPanel.cs:309` | `canAfford ? "RAID   {n} SKR" : "NEED MORE SKR"` — the label changes, not just the fill |
| Build palette cards | `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:941-945` | dimming is *accompanied* by a Built chip and reason words; the comment at `:940-942` states the rule explicitly, and `:1076-1079` records that a previous colour-only version was fixed for exactly this reason |
| Build cost summary | `Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:512-519` | `+`/`-` glyph per axis |
| Build CTA | `Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:539-540` | label becomes the shortfall |
| Manage extra-slot row | `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:508` | `"Extra slot: locked - awaken a 3rd Echo"` |
| Lock chips | `Assets/_Modules/Core/UI/ElarionUiKit.cs:3547`, `:3606`; `ElarionUiKitObsidian.cs:1784` | literal `"LOCKED"` / `"LOCKING"` / `"UNLOCKED"` words |
| Barracks lock | `Assets/_Modules/Village/Troops/BarracksService.cs:280` | `"Locked - unlocks at Barracks Level "` |

### C-tier: sites that FAIL

| # | Site | Citation | Colour encodes | Word/glyph that should carry it |
|---|---|---|---|---|
| C-01 | Arena **defense** palette cost label | `Assets/_Modules/Village/Arena/ArenaDefensePaletteUI.cs:239` | `affordable ? ElarionUi.Affordable : ElarionUi.Danger` on the price text | The card dims (`:228`, opacity 0.45) and disables (`:229`) — **opacity is not a word.** Append the shortfall: `"120 pts (need 40 more)"`. Directly contradicts its sibling `ArenaPanel.cs:309`, which does it right. |
| C-02 | Arena **attack** palette cost label | `Assets/_Modules/Village/Arena/ArenaAttackPaletteUI.cs:224-225` | same green/red price tint | same fix as C-01. Note `:221-223` *does* dim the name label too — still hue+opacity only. |
| C-03 | Healing Fountain upgrade cost | `Assets/_Modules/Village/Buildings/HealingFountain.cs:399-401` | gold `(1,0.85,0.3)` vs red `(1,0.3,0.3)` on the cost line | **Partial pass** — the label at `:396` already reads `"Upgrade cost: {cost} Coins  (you have {coins})"`, so a player *can* compute it. But the state is not *stated*. Add `" - need {gap} more"` when short. |
| C-04 | Crafting Craft CTA | `Assets/_Modules/Village/Crafting/VillageCraftingPanel.cs:224-228` | `canCraft ? ObsidianButtonColor.Green : ObsidianButtonColor.Gray` | The label stays `"Craft"` in **both** states while `btn.interactable` flips at `:232`. So the button greys, does nothing on tap, and never says why. **This is simultaneously a §4 and a §3 defect.** Fix: label becomes the shortfall, exactly like `BuildMenuVM.BuildCtaLabelFor`. |
| C-05 | Cosmetic shop price + buy button | `Assets/_Modules/HUD/CosmeticShopPanel.cs:454`, `:484` | `ParchmentDim` vs `Gold` price; `Yellow` vs `Gray` button | The refusal toast at `:489` (`"Not enough Glimmer."`) only fires **on tap** — before the tap, affordability is hue-only. Put the shortfall on the card. |
| C-06 | Manage finish-now button | `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:765` | `CanAffordFinish ? Yellow : Gray` | The VM comment at `ManageScreenVM.cs:442` says the button is deliberately offered when unaffordable — so the greyed state is *load-bearing* and must be worded. Add the crystal shortfall to the label. |
| C-07 | Talent tree node availability | `Assets/_Modules/Village/Talents/TalentTreePanel.cs:14` | Header comment: `"Available — prereqs met, affordable (green outline, button active)"` | A green **outline** as the availability signal. The tier headers at `:408-410` are text, but node state is not. Needs a per-node word or glyph (a lock glyph / "Ready" / "Need 2 pts"). |
| C-08 | Shared palette semantics | `Assets/_Modules/Core/UI/ElarionUi.cs:6`, `:79` | The kit defines `Affordable` (green) and `Danger`/unaffordable (red) as *named colour roles* | **The systemic offender.** `:79` describes `Danger` as a "TEXT/GLYPH accent", which invites exactly the C-01/C-02 usage — tinting a glyph and calling it done. The doc comment should be amended to state the BuildMenuVM rule: *this colour may only reinforce a state already spelled out in words.* Fixing the palette's documentation is the cheapest way to stop new offenders. |

**Palette/theme files** (for whoever does the systemic pass): `Assets/_Modules/Core/Theme/Theme.cs`,
`Assets/_Modules/Core/UI/ElarionUi.cs`, `Assets/_Modules/Core/UI/ElarionUiKit.cs`,
`Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs`, `Assets/_Modules/Core/UI/ShopTheme.cs`,
`Assets/_Modules/Village/Vfx/HostilePalette.cs`.

**Greyscale check is the gate.** Per memory `owner-colorblind-delegate-visual-creative`, never
ask the owner to judge hue — screenshot the surface, desaturate, and confirm every state is still
distinguishable. That is the acceptance criterion for every C-row above.

---

## 5. Non-ASCII in player-facing strings

> TMP renders anything outside its atlas as **tofu**. Screen strings are ASCII-only.
> Code comments are irrelevant and are excluded throughout. Data values and string literals are
> the real surface.

### The project has both the rule and a tool — neither is applied globally

**The rule, written into `glossary.json`'s authoring law:**

> "(3) ASCII only (the build TMP font renders non-ASCII as tofu, so `'--'` not an em dash)"
> — `Assets/Resources/Data/Canonical/glossary.json` `_comment`

**The precedent** — `tutorial-steps.json` was deliberately cleaned:

> "four EM DASHES (U+2014) were replaced with ASCII hyphens in the `ctx_*` objective texts. Those
> four were PLAYER-VISIBLE strings and every non-ASCII glyph renders as TOFU on device (binding
> project rule)" — `Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json` `_note` (v4, WO-1014)

**The tool** — a sanitizer exists:

```
public static string Ascii(string s)   // ' ' .. '~' kept; '→' -> "->"; '×' -> 'x'; else ' '
```
`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1307-1320`

**Two problems with it.** (a) It is applied only inside the Manage screen — `Ascii(` has 41 call
sites, all local; `ElarionUiKit.Label` / `.Button`, the shared constructors every panel uses, do
**not** sanitize. (b) Its em-dash handling is wrong: `—` is not in its special-case list, so it
falls to `else sb.Append(' ')` — an em dash becomes **a space**, turning `"Cost — 40"` into
`"Cost   40"` (double space) rather than `"Cost - 40"`. Fix the mapping *and* promote the call
into the shared label/button constructors, and the entire class of defect below disappears by
construction.

### N-tier: data values

`—` (U+2014 em dash) is by far the dominant offender. **Every table below is player-facing copy
under a non-`_` key.**

| # | File | Field(s) | Chars | Count |
|---|---|---|---|---|
| N-01 | `abilities.json` | `.classes.*.abilities.*.icon` | `✚ ✦ ➹ ☄ ✹ ❄ ➾ ✷ ⚡ ⚔` | 34 icon glyphs |
| N-02 | `abilities.json` | `.classes.*.abilities.*.description` | `—` | 39 |
| N-03 | `weapons.json` | `.weapons[*].icon` | `🗡 🏹 🛡 🪓 🪄 🔮 ⚔ 🔨 🕯 🔪` + `U+FE0F` | 96 |
| N-04 | `weapons.json` | `.weapons[*].flavor` | `—` | 11 |
| N-05 | `armor.json` | `.armor[*].icon` | `🧥 🛡` + `U+FE0F` | 24 |
| N-06 | `armor.json` | `.armor[*].flavor`, `.saga` | `—` | 16 |
| N-07 | `accessories.json` | `.accessories[*].icon` | `💍 📿` | 10 |
| N-08 | `accessories.json` | `.accessories[*].flavor` | `—` | 8 |
| N-09 | `walls.json` | `.tiers[*].emoji` | `🪵 🧱 ⚙ 🛡` + `U+FE0F` | 6 |
| N-10 | `chat-phrases.json` | `.phrases[*].emoji`, `.phrases[*].text` | `👋 ⚠ 🛡 🙏 🏮 ⚔ 🆘` + `—` | 9 |
| N-11 | `hero-talents.json` | `.trees.*.nodes[*].description` | `—` | **53** |
| N-12 | `troop-upgrades.json` | `.upgrades[*].flavorText`, `.specialAbilities[*].description` | `—` | 32 |
| N-13 | `daily-quests.json` | `.templates[*].label` | `—` | **38** — every daily-quest label |
| N-14 | `en.json` | 20 values | `—` | 20 |
| N-15 | `en.json` | `heartDamage.threshold0`, `swap.statusLoading`, `swap.statusSigning` | `…` U+2026 | 3 |
| N-16 | `lore-fragments.json` | `.fragments[*].title`, `.body[*]` | `—` | 11 |
| N-17 | `dungeons/healers-cottage.json` | `.loreStones[*].title`, `.body[*]`, `.bryn.firstEncounterLine` | `—` | 8 |
| N-18 | `enemies.json` | `.enemies[*].flavor` | `—` | 5 |
| N-19 | `enemy-roles.json` | `.roles.*.description`, `.creatures[*].behavior` | `—` | 10 |
| N-20 | `realm-map.json` | `.homeBase.description`, `.regions[*].description` | `—` | 6 |
| N-21 | `cosmetics.json` | `.items[*].description` | `—` | 6 |
| N-22 | `packs.json` | `.packs[*].tagline`, `.theme` | `—` | 16 |
| N-23 | `crafting-recipes.json` | `.ingredients[*].description` | `—` | 14 |
| N-24 | `gear-recipes.json` / `jeweler-recipes.json` | `.recipes[*].saga` | `—` | 9 |
| N-25 | `structures-catalog.json` | `.entries[*].description` | `—` + `§` | 14 |
| N-26 | `pets.json` | `.pets[*].bondRanks[*].perkDescription` | `—` | 3 |
| N-27 | `themes.json` | `.themes.*.description` | `—` | 7 |
| N-28 | `building-tiers.json` | `.buildings[0].tiers[1].perks[1].effect` | `—` | 1 |
| N-29 | `canon-strings.json` | `.theHeartTagline` | `—` | 1 |
| N-30 | `skin.json` | `.skins.pi.currencySymbol` | `π` U+03C0 | 1 — **intentional**, a currency symbol; needs an atlas entry, not a rewrite |
| N-31 | `wallets.json` | `.rewardsDistributor.provider`, `.explorerNote` | `—` | 2 |
| N-32 | `talent-icon-map.json` | `.skills[*].why` | `—` | 6 |
| N-33 | `spawn-areas.json` | `.areas[*].note` | `—` | 5 |
| N-34 | `ad-creatives.json` | `.templates[1].headlinePattern`, `.generated[*].headline` | `—` | 5 |
| N-35 | `audio-mix.json` | `.tracks.*.notes` | `—` | 5 — likely internal, verify |

**On the emoji icon fields (N-01, N-03, N-05, N-07, N-09, N-10).** These are a **different
problem from the em dashes** and must not be bulk-replaced. They are deliberate icon glyphs, and
`Assets/_Modules/Core/UI/ConceptIconResolver.cs:93` / `:150` shows the intended design: real pack
art with a **glyph fallback**. The question to answer per family is whether the fallback path is
reachable on device, and if so whether the TMP atlas carries those code points. If it does not,
every fallback is a tofu box. **Do not "fix" these by deletion** — they are the accessibility
backstop for missing art, and per §4 a glyph is exactly what carries meaning without hue.

**ASCII-clean, verified — do not re-flag:** `dialogues.json`, `tutorial-steps.json`,
`glossary.json`, `quests.json`, `buildings.json`, `guide-content.json`. These prove the standard
is achievable; they are the model.

### N-tier: C# string literals (player-facing surfaces only)

Filtered to text-rendering modules, with `Debug.Log*` / `FlowTrace.*` / `Guard.*` / `[Tooltip]`
excluded. **These are the ones a player reads.**

| # | String | Citation | Char |
|---|---|---|---|
| N-40 | `"Once, the Heart of Elarion blazed — a world-tree whose light was the breath of all living things."` | `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs:116` | `—` |
| N-41 | `"The Hollow Ones rose — not monsters, but the broken, drawn to the last warmth they could feel."` | `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs:120` | `—` |
| N-42 | 4 opening-cinematic beats | `Assets/_Modules/Onboarding/StoryIntroController.cs:444`, `:446`, `:451`, `:454` | `—` |
| N-43 | `"— Tier I —"`, `"— Tier II —"`, `"— Tier III —"` | `Assets/_Modules/Village/Talents/TalentTreePanel.cs:408-410` | `—` — **section headers, always on screen** |
| N-44 | `"N points — Spend"`, `"Skill points — Spend"`, `"1 skill point — Spend"` | `Assets/_Modules/Village/Buildings/UI/LevelUpSkillPopup.cs:36`, `:278`; `LevelUpVM.cs:140` | `—` |
| N-45 | `"Cost: …"`, `"Refund: +…"` | `Assets/_Modules/Village/Hero/ShopVM.cs:58` | `…` |
| N-46 | `"CHAIN ×{_chain}"` | `Assets/_Modules/Village/Hero/AttackTimingBonus.cs:162` | `×` — in-combat floating text |
| N-47 | `"Yaw: XX°"`, `"Yaw: {…}°"` | `Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:42`, `:459` | `°` |
| N-48 | `"Preview & Orient — "` | `Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:113` | `—` |
| N-49 | 4 build-card descriptions (`"A defensive tower — auto-fires…"`, `"A wall segment — blocks…"`, `"A gate — a controlled opening…"`, `"A resource structure — gathers…"`) + `"Sturdier — higher durability tier"` | `Assets/_Modules/Village/BuildMode/StructureCardVM.cs:181`, `:243-246` | `—` |
| N-50 | `"Locked — complete the saga first."` | `Assets/_Modules/Village/Crafting/GearCraftingService.cs:158`; `JewelerCraftingService.cs:135` | `—` |
| N-51 | `"Your mines filled up — keep them defended and check in sooner to catch every shard."` | `Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs:138` | `—` |
| N-52 | `"Rally set — idle troops will muster there."`, `"Retreat? Tap again to confirm — survivors come home, the fallen recover."`, `"No troops to deploy — train at the Barracks first."`, `"{name} armed — tap the ground to deploy."` | `Assets/_Modules/Village/Troops/RaidDeployController.cs:429`, `:512`, `:779`, `:821` | `—` |
| N-53 | `"Assault to recon — drop troops on the field"`, `". Begin Assault — drop them on the field."`, `"Raid under construction — battleground not in this build."` | `Assets/_Modules/Village/Hero/RaidDeployScreen.cs:395`, `:488`, `:510` | `—` |
| N-54 | `"{petName} walks the watch beside you. A Warden bonds once — that bond can't be traded away."`, `"Want another companion? More Wardens can be found out in the realm — earned through quests or summoned at the marketplace."` | `Assets/_Modules/Onboarding/PetSelectController.cs:336`, `:347` | `—` — **FTUE copy** |
| N-55 | `"Freezing burst — 26 dmg + freeze in a ring."`, `"Charge behind your shield — knocks back, slows, breaks guard."` | `Assets/_Modules/Onboarding/HeroCatalog.cs:129`, `:149` | `—` |
| N-56 | 6 SKR panel body lines (incl. `"Cosmetic & convenience perks only — modest thank-yous, never pay-to-win."`, `"Coming soon — testnet preview. No wallet connected."`) | `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:69`, `:71`, `:75`, `:166`, `:192`, `:243` | `—` |
| N-57 | `"(allies — V2)"` | `Assets/_Modules/Village/Talents/HeroTalentCatalog.cs:86` | `—` |
| N-58 | `"enqueue REFUSED — line full at depth {maxDepth}"` | `Assets/_Modules/Core/Jobs/ObsidianQueueEngine.cs:90` | `—` — verify whether this reaches a toast or is diagnostic only |

### N-60 — NPC dialogue barks: the single largest concentration of the violation

**Every hardcoded NPC bark in the tree contains an em dash.** These are spoken lines a player
reads constantly in town.

| File | Lines |
|---|---|
| `Assets/_Modules/Village/NPCs/TownsfolkDialogue.cs` | `:129`, `:131`, `:139`, `:144`, `:151`, `:163`, `:184`, `:194`, `:196`, `:221`, `:240`, `:246`, `:248`, `:254`, `:256`, `:262`, `:263`, `:264` |
| `Assets/_Modules/Village/NPCs/CompanionDialogue.cs` | `:61`, `:65`, `:76`, `:80`, `:91`, `:95`, `:98`, `:106`, `:111` |
| `Assets/_Modules/Village/NPCs/SylasFirstMeeting.cs` | `:390`, `:393`, `:400`, `:403`, `:410`, `:418`, `:428` |
| `Assets/_Modules/Dungeons/Wanderer/WandererDialogue.cs` | `:66`, `:75`, `:83`, `:110` |

Example (`TownsfolkDialogue.cs:254`): `"It comes from the SKY, Keeper — ground walls won't save
us. We need spears that reach the clouds."`

**Note the irony:** `dialogues.json` — the *data* home for dialogue — is ASCII-clean (§5). These
barks are ASCII-dirty precisely *because* they are hardcoded instead of living in that file.
§M-03 and §5 are the same defect seen from two angles.

### N-61 — Degree sign `°` (U+00B0) cluster

| Site | Citation |
|---|---|
| Build preview yaw readout | `Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:42`, `:459` |
| Rotate menu | `Assets/_Modules/Village/UI/RotateModelMenu.cs:191` |
| Tower placement rotate menu | `Assets/_Modules/Village/UI/TowerPlacementRotateMenu.cs:430`, `:824`, `:854`, `:855`, `:856` |

Replace with `" deg"` — the same substitution `tutorial-steps.json` made for em dashes. Note
`ManageScreenVM.Ascii` would map `°` to a **space**, silently producing `"Yaw: 90 "` — another
reason to fix that mapping before promoting it (§5 head).

### N-62 — Ellipsis `…` in wallet + raid copy

`"Connecting…"` — `Assets/_Modules/Wallet/WalletConnectDialog.cs:222`, `:244`;
`"Assaulting {name}…"` — `Assets/_Modules/Village/Hero/RaidDeployScreen.cs:524`.
Use `...`.

**Debug-overlay only — lower priority, but the owner sees them.**
`Assets/_Modules/HUD/AdminOverlay.cs` carries ~12 em-dash literals (`:201`, `:296`, `:403`,
`:553`, `:557`, `:628`, `:799`, `:926`, `:951`, `:968`, `:988`, `:1008`) and
`Assets/_Modules/HUD/DebuggingController.cs:155`, `:160` use `•` (U+2022).
`Assets/_Modules/DevTools/DevPanelController.cs` uses `◆` (U+25C6, `:349`) and `✕` (U+2715,
`:354`) plus ~20 em dashes and 2 ellipses. `Assets/_Modules/Village/UI/SeatingEditorOverlay.cs:246`,
`:586` and `Assets/_Modules/HUD/OwnerDevToolsOverlay.cs` likewise. The only true IMGUI surface in
`_Modules` is `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs:584` (`"What looks wrong?
(Enter = save, Esc = save blank)"`) and `:620` (`"  FLAGGED"`) — both ASCII, both fine.
Owner-only surfaces; fix after the player-facing set.

---

## 6. Voice inconsistency

**Canon reference:** world is **Elarion**; tagline **"Echoes of a Forgotten Civilization"**;
enemies are **the Hollowed** / **the Hollow Ones**; an **Echo** is *the awakened essence of one of
the people the Heart guards*. Renames: HUD "Pets" → **"Echoes"**, "Work" → **"Queues"**.
**"Avalon" is retired.** Source: `CLAUDE.md` §7 + `canon-strings.json` `_taglineLegacyNote`.

### The register benchmark

The plain, hard vocabulary the brief names — `bonecrypt`, `sunken_vault` — is matched well by
`glossary.json` and `dialogues.json`. `glossary.json`'s Hollow Ones entry is the register at its
best:

> "The undead that come in the waves. They are risen Folk, not monsters -- grief that walks."

Anything jarringly softer or more ornate than that is the outlier.

### V-00 — **"Hold the last light." is live on the loading screen**

**Severity: critical. Live, hardcoded, and seen on every load.**

`Assets/_Modules/Core/UI/VillageLoadOverlay.cs:65` — one of six rotating loading-screen tips:

> `"Hold the last light."`

`CLAUDE.md` §7 names this exact string as **retired** ("tagline = **'Echoes of a Forgotten
Civilization'** (retired 'Hold the last light', 2026-07-24)"). It is not a dead key like V-01 —
it is a compiled literal on a surface every player sees on every scene load.

The five siblings in the same rotation are **on-canon and good** (`:64` "Elarion holds because we
hold the line.", `:66` "The Echoes rest in the Hollow, waiting to be called.", `:67`, `:68`,
`:69` "Stone remembers. So do we."). Only `:65` is the survivor. **One-line fix.**

### V-01 — The `en.json` tagline is the retired one

| Key | Value | Citation |
|---|---|---|
| `title.tagline` | **"Hold the Chord. Defend the Spire."** | `Assets/Resources/Data/Canonical/en.json:9` |
| `title.tagline.legacy.bible` | "Tend the Heart. Hold the dark." | `en.json:10` |
| `title.tagline.legacy.story` | "Tend the Lantern. Hold the dark." | `en.json:11` |

`canon-strings.json` `_taglineLegacyNote` names this **exact string** as retired:

> "The prior tagline 'Hold the line' (WO-570) plus the earlier **'Hold the Chord. Defend the
> Spire.'** … plus the Spire/Chord/Lantern/Stone-Choir motifs are **RETIRED**"

and `canon-strings.json` carries the correct value twice (`tagline`, `titleTagline` = "Echoes of
a Forgotten Civilization"). The live key is dead (§M-03), so nothing renders it **today** — but a
key literally named `title.tagline` holding retired copy is a loaded gun for the next person who
wires a title screen.

**Fix:** set `en.json:9` to the canon tagline or delete the key and point at
`canon-strings.json.tagline`. Two sources of truth for one tagline is the same duplicated-state
drift `CLAUDE.md` §5/§2 warn about.

### V-02 — The opening cinematic is written entirely in the retired Spire/Chord motif

**Severity: high if reachable.** `StoryIntroController.ReactOpeningCinematic`
(`Assets/_Modules/Onboarding/StoryIntroController.cs:436-456`) is a 14-beat narration, and the
retired motif is its **entire spine**:

| Beat | Citation |
|---|---|
| "So the Folk raised a **spire** of pale stone over the Tree's ashes," | `:442` |
| "and bound its last song inside. **The spire has held the note** ever since." | `:443` |
| "Three have kept watch over **the spire's chord** —" | `:446` |
| "Sir Bram the knight, Nessa the ranger, and one **Chorister** still learning the song —" | `:451` |
| "waiting for the one **the chord will answer to**." | `:452` |
| "…yet **the spire steadies** when you step beneath it." | `:454` |
| "Welcome home. **The chord is yours now**." | `:456` |

Per `canon-strings.json` `_taglineLegacyNote`, the "Spire/Chord/Lantern/**Stone-Choir**" motifs are
retired — and "Chorister" is Stone-Choir vocabulary. This is potentially **the first copy a new
player reads**.

**Reachability caveat (honest):** the array is consumed at
`Assets/_Modules/Onboarding/StoryIntroController.cs:172`, and `TitleController` holds a serialized
reference (`Assets/_Modules/Onboarding/TitleController.cs:69`). `Play()` is gated on
`GameState.Onboarded == false` (stated at
`Assets/_Modules/Village/Progression/SpirePlansCelebration.cs:21-23`, which explains why it built
a separate controller rather than reuse this one). So this is the **cold-open path on a fresh
save**. Whether it plays, or the video/slate path in
`Assets/_Modules/DialogueUI/IntroSequencePlayer.cs` supersedes it, is **not proven** and wants one
headless first-launch capture before rewriting 14 beats.

**Contrast — `IntroSequencePlayer`'s slates are on-canon** (`IntroSequencePlayer.cs:113-125`):
"Once, the Heart of Elarion blazed…", "The Hollow Ones rose — not monsters, but the broken…", "A
knight, **Grom**…", "Reclaim the light of Elarion." Correct world, correct enemies, correct hero
name. If two intro paths exist, **this is the one that is canon-current** and
`StoryIntroController`'s is the stale twin. Resolving which one ships is the actual fix; rewriting
both is waste.

### V-03 — "pet" survives in player-facing quest objectives

`Assets/Resources/Data/Canonical/quests.json`, quest `vendor.stable` ("Wild Hearts") mixes both
vocabularies **inside one quest**:

| Stage | `objectiveText` | Verdict |
|---|---|---|
| `tame-beast` | "Track a **wild echo** beyond the walls and win its measure." | correct |
| `train-ability` | "Train a **pet** ability with Fenn Wildmane." | **retired** |
| `put-to-work` | "Ask the **Echo Warden** at the **Echo Hollow** to set your bonded **pet** to harvest." | mixed — correct nouns, retired object |

A player reads all three in sequence in the same quest log. This is the clearest single voice
defect in the tree.

### V-03b — "Pet" survives on three live UI surfaces

| Site | String | Surface | Note |
|---|---|---|---|
| `Assets/_Modules/HUD/CosmeticShopPanel.cs:339` | `AddTab("Pet", "pet", 1)` | **visible cosmetic-shop tab label** | the tab *label* is player-facing; the `"pet"` category key beside it is internal and fine |
| `Assets/_Modules/Pets/PetDeployer.cs:999` | `tm.text = def.Species ?? def.Id ?? "Pet"` | **world-space nameplate above the companion** | renders the literal `Pet` when species and id are both empty — a fallback that ships the retired word |
| `Assets/_Modules/HUD/HelpMenuVM.cs:213` | `"Reset Hero & Pet"` | Help-menu row label | |

**Correctly migrated already — do not re-flag:** the pet-select screen's *copy* is fully on canon
(`Assets/_Modules/Onboarding/PetSelectController.cs:245`, `:329`, `:336`, `:347`, `:429`, `:436`
— "Echoes await in Elarion", "Visit the **Echo Hollow**"), and the HUD count chip reads
`"Echoes {n}/{max}"` (`Assets/_Modules/Village/Harvest/EchoUnlockFeedback.cs:331`). Only class
names, element names (`pet-select-root`, `pet-card-*`) and catalog ids (`pet-house`) remain, and
those are **internal** — same key/value split as V-04.

### V-04 — `pets.json` bond-rank descriptions say "pet"

Player-facing `perkDescription` values (`Assets/Resources/Data/Canonical/pets.json`):

- `.pets[0].bondRanks[2]` — "The hero regenerates mana faster while this **pet** is deployed."
- `.pets[0].bondRanks[3]` — "The Heart takes 15% less damage while this **pet** defends."
- `.pets[0].bondRanks[4]` — "Every 5th attack releases an Aether nova around the **pet**."
- `.pets[1].bondRanks[4]` — "On a kill, the **pet** bursts into flame, hitting nearby foes."
- `.pets[2].bondRanks[3]` — "Enemies near the Heart are chilled while this **pet** defends."

**Note on scope:** the *filename* `pets.json`, the id keys, and the ~69 `pets[*]` key paths are
**internal** and out of scope — renaming them is a data-schema change with save implications, and
`canon-strings.json` already models the right pattern (`petHouse` key → `"Echo Hollow"` value:
*keys stay, values move to canon*). Only the five **values** above need rewriting.

### V-05 — `en.json` `petCaption.*` / `petAmbient.*` / `petSelect.*` families

13 dead keys (§M-03) carrying "Pet"/"Pup"/"Sprite" vocabulary, plus
`buildingDesc.petHouse` (`en.json:171`) — which is **live** via `buildings.json:53`. Its value is
already correct ("the **echoes** rest here"), so no fix needed, but note the key/value split is
the right pattern and matches V-04's recommendation.

`petSelect.title` = "Choose Your First **Warden**" (`en.json:158`) and `petSelect.confirm` = "Bond
With This **Warden**" (`en.json:160`) — these are **live** (`PetSelectController.cs`). "Warden" is
canon (`canon-strings.json.wardens` = "the Wardens") but is a *different* concept from an Echo.
Whether the starter companion is a Warden or an Echo is an **owner call**, flagged not asserted —
`PetSelectController.cs:329` (`"You already have a Warden"`) and `:336`/`:347` are consistent with
each other, so this is a coherent alternative vocabulary, not an accident.

### V-06 — "workforce" in guide + pack copy

Player-facing, `Assets/Resources/Data/Canonical/`:

| File | Field | Text |
|---|---|---|
| `guide-content.json` | `.sections[11].body[1]` | "…your **Echo workforce** grows…" |
| `guide-content.json` | `.sections[22].body[1]` | "…the Lumber Mill and your **workforce** both feed the same pile." |
| `guide-content.json` | `.sections[23].body[1]` | "…tied to the **Echo workforce** that grows as you clear waves." |
| `guide-content.json` | `.sections[23].body[2]` | "A thriving Hollow means a thriving **workforce**…" |
| `packs.json` | `.packs[9].tagline` | "Patron of the echoes — …let the **workforce** roll." |
| `packs.json` | `.packs[9].theme` | "…Built around the **echo workforce**." |
| `cosmetics.json` | `.items[26].description` | "…those who keep the **workforce** humming." |

**Judgement, flagged not asserted:** "workforce" is not on the retired list — `glossary.json`
itself uses it (`groups.village.intro`: "Your economy, your **workforce**, and the work that takes
time"). But it is industrial-register against a world whose Echoes are *"the awakened essence of
one of the people the Heart guards"* (`guide-content.json` `.sections[12].body[0]` — which is the
canon definition, verbatim and correct). Calling awakened souls a "workforce" is the single
biggest register clash in the corpus. **Owner call**; a candidate replacement is "the Echoes" or
"your Echoes".

### V-07 — "Work" vs "Queues"

| Site | String | Verdict |
|---|---|---|
| `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:194` | `"WORK QUEUE"` | **The panel title of the queue HUD** — the retired framing at full size. `ObsidianQueueHud.cs:117` states this HUD is "NOT installed — superseded by the WO-911 Manage/Queues screen", so it is *probably* dead code — but the literals are compiled in and the file is still reachable via `ObsidianQueueHud.Show`. **Verify, then delete the file or fix the strings.** |
| `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:230` | `"Work queue unavailable."` | **Retired label.** Should be "Queues". (Also §3 S-20.) Same reachability caveat as above. |
| `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:433` | `"Queued - {n}th in line ({time} of work)"` | **Live**, and on the *replacement* screen. `"of work"` reads as the retired noun. Suggest "of build time" / "remaining". |
| `Assets/Resources/Data/Canonical/glossary.json` `_comment` | "the timed-work system is the **WORK QUEUE** with Builders / Training / Research channels" | Contradicts `CLAUDE.md` §7 ("Work" → "Queues") and the shipped bar face, which is **"Manage"** (`Assets/_Modules/HUD/Kit/HudKitController.cs:613`). Three names for one system. |
| `Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json` `founding_timers.objective.text` | `"Work takes time - watch the Builders ledger"` | **Pass** — "work" as a plain noun, not a label. No change. |
| `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:536` | `"Queues unavailable."` | correct term |

**Fix:** pick one player-facing name for the system and make `glossary.json`, the bar face, and
the empty-state strings agree. The bar face says "Manage", so that is the de-facto answer.

### V-08 — "Avalon": clean in copy, one dormant key

**No player-facing string in the entire corpus contains "Avalon"** — verified across all 15,258
data values and the C# literal set. The only survivors are two keys in
`Assets/Resources/Data/Canonical/canon-strings.json`:

- `"avalon": "Avalon"`
- `"avalonEpithet": "the last green sanctuary"`

Neither is referenced. They are a resolvable retired name sitting in the canon file with **no
`_legacyNote`** — unlike `_guardianLegacyNote`, `_selaLegacyNote`, `_garranLegacyNote`, which all
correctly annotate their retired entries. **Fix:** add an `_avalonLegacyNote` or delete both keys.
Cheap, and it closes the last door.

### V-09 — "minion": clean

Zero occurrences in any player-facing data value or UI literal. **No action.**

### V-10 — Contraction inconsistency across panels

One concept, two spellings, two panels:

- `"That item cannot be equipped."` — `Assets/_Modules/Village/Hero/InventoryVM.cs:388`
- `"That item can't be equipped."` — `Assets/_Modules/Village/Hero/PartyShopVM.cs:594`

Same for `"Can't change skills during battle."` (`HeroLoadoutVM.cs:147`) against the formal
register elsewhere. Minor, but it is the tell that no style rule is written down. **Recommend:
contractions allowed** (they match the warm, plain register of `dialogues.json`), applied
consistently.

### V-11 — "Spire" as a tower name: not a defect

`Arcane Spire` / `Runed Spire` / `Warded Spire` (`towers.json` `.levels[1-3].name`,
`structures-catalog.json` `.entries[19].displayName`) and `SpirePlansCelebration` use "spire" as a
**building noun**. The retirement in `_taglineLegacyNote` is of the *tagline motif* (spire-as-the-
world's-heart), not the common noun. **No action** — recorded so this is not re-flagged.

`en.json:198` (`tooltip.buttonBuild.body`, "raise a spire") is dead (§M-03) and reads in the
retired motif sense; if that key is ever revived, rewrite it.

---

## 7. How to add player-facing copy correctly

Six steps. Each cites the file that proves it.

### 1. Put the string in data, not in code — and use the right file

| Kind of string | File | Proof |
|---|---|---|
| Proper nouns (names, places, buildings) | `Assets/Resources/Data/Canonical/canon-strings.json` | resolved via `VillageStrings.Canon` — `Assets/_Modules/Village/VillageStrings.cs:51-55` |
| Localizable copy (descriptions, prose) | `Assets/Resources/Data/Canonical/en.json` | resolved via `VillageStrings.Locale` — `Assets/_Modules/Village/VillageStrings.cs:62-66` |
| Definitions / help | `Assets/Resources/Data/Canonical/glossary.json` | data-only by design — its `_comment`: "Adding or editing a term is a DATA change, no code" |
| Tutorial beats | `Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json` | |
| Dialogue | `Assets/Resources/Data/Canonical/dialogue/dialogues.json` | |
| Lore | `Assets/Resources/Data/Canonical/lore-fragments.json` | |

The rule as originally written: *"the Village UI must NEVER hardcode a canon string … the Unity
agent never types these inline"* — `Assets/_Modules/Village/VillageStrings.cs:4-5`. §M-03 and
§M-04 are what happens when it is not followed. **If you are typing a sentence inside a `.cs`
file, stop and ask which of the six files above owns it.**

### 2. Write the twin — the dual-copy rule

Every canonical file exists **twice**, byte-identical:

- `Assets/Resources/Data/Canonical/<file>.json`
- `Assets/StreamingAssets/Data/Canonical/<file>.json`

Because the loader tries Resources first and falls back to StreamingAssets —
`Assets/_Modules/Village/VillageStrings.cs:102` (`DeNelle.Core.CanonicalJson.Read`, "Resources
first, StreamingAssets fallback"). Editing one and not the other means the two platforms read
different copy.

`glossary.json` states the rule and names its enforcement: *"This file has a byte-identical twin
under `Assets/StreamingAssets/Data/Canonical/` (asserted by **GlossaryRegression**)"*. **If your
file has no twin-assertion regression, add one** — that is the only reason the five headline
files are still in sync (verified this audit, §0).

### 3. ASCII only

The TMP atlas renders anything outside it as tofu. Use `-` or `--`, never `—`; `...`, never `…`;
`->`, never `→`; `x`, never `×`; no emoji in prose.

- The rule: `glossary.json` `_comment` clause (3).
- The precedent: `tutorial-steps.json` `_note` (v4, WO-1014) — four em dashes replaced *because
  they were player-visible*.
- The sanitizer: `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1307-1320`. **Fix its
  em-dash mapping and promote the call into `ElarionUiKit.Label`/`.Button`** and this step becomes
  automatic instead of a discipline (§5).

Icon glyphs are the deliberate exception — but only where a resolver provides art with the glyph
as fallback (`Assets/_Modules/Core/UI/ConceptIconResolver.cs:93`), and only if the atlas carries
the code point.

### 4. Never let colour alone carry meaning

*"A colour may only ever reinforce a state that is already spelled out"* —
`Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:495-499`.

Patterns that satisfy it:

- **Put the state in the label.** `BuildCtaLabelFor` returns the verb *or the shortfall* —
  `Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:539-540`.
- **Add a glyph.** The `+`/`-` affordability mark —
  `Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:512-519`.
- **Add a word chip.** `"LOCKED"` / `"UNLOCKED"` —
  `Assets/_Modules/Core/UI/ElarionUiKit.cs:3547`, `:3606`; difficulty badge —
  `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs:273`.

**Acceptance test: screenshot it, desaturate it, and confirm every state is still
distinguishable.** Never ask the owner to judge hue (memory
`owner-colorblind-delegate-visual-creative`).

### 5. A refusal must name the next action

State the **blocker**, the **gap**, and the **exit**. Model the shape on:

- `"No troops yet - train troops at the Barracks to start a raid."` —
  `Assets/_Modules/Core/HudModel/HudActionBarModel.cs:256`
- `"This cannot be bought. Wait it out, or watch an ad to speed it up."` —
  `Assets/_Modules/Core/Jobs/JobRushPolicy.cs:139`
- Five distinct states instead of one collapsed case —
  `Assets/_Modules/Village/Buildings/UI/BuildMenuVM.cs:546-558`

**A disabled control that does not say why is a silent refusal** — that is the C-04 /
`VillageCraftingPanel.cs:224-232` defect. And a button that *vanishes* is worse than one that
explains: that was tonight's raid-button bug, fixed by `HudActionBarModel.cs:256`.

**Never author copy that hides money movement.** `ManageScreenVM.Cancel`
(`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1041-1050`) must always name what was
returned and what was not.

### 6. Stay in voice

Check against `Assets/Resources/Data/Canonical/canon-strings.json` (the authority) and
`CLAUDE.md` §7. World **Elarion**, never Avalon. Enemies **the Hollowed** / **the Hollow Ones**.
An **Echo** is *the awakened essence of one of the people the Heart guards* — never "pet", never
"minion". Tagline **"Echoes of a Forgotten Civilization"**; the Spire/Chord/Lantern/Stone-Choir
motifs are retired (`canon-strings.json` `_taglineLegacyNote`). Register: plain and hard, the
`glossary.json` Hollow Ones entry as benchmark.

**When you retire a term, annotate it, do not silently leave it.** `canon-strings.json` does this
correctly three times (`_guardianLegacyNote`, `_selaLegacyNote`, `_garranLegacyNote`): keep the
key so old data does not hard-error, change the **value**, and add a dated note saying what
superseded it. §V-08 (`avalon`) is the one place this was skipped.

### 7. Pin it with a regression

The only reason the §3 refund defect is closed rather than latent is
`Assets/Editor/Regression/EconomySweepRegression.cs:273`, `:281` — it **fails the gate** on the
bad literal. Contrast §P-01: `IsPlaceholderFragment` is a perfectly good guard with **zero
callers and zero tests**, so it never fired.

**A copy rule with no test is a comment.** Regressions this audit says are missing:

1. Every `descriptionKey` in `buildings.json` resolves in `en.json` (§M-01).
2. No shipped dungeon layout references a `"placeholder": true` fragment id (§P-01).
3. No player-facing data value under a non-`_` key contains a non-ASCII character, with an
   allow-list for the deliberate icon fields (§5).
4. The Resources/StreamingAssets twins are byte-identical for **every** canonical file, not just
   glossary (§7 step 2).
5. No player-facing string matches the retired-vocabulary set — `Avalon`, `\bpets?\b`, `minion`
   (§6).

---

## Appendix — severity roll-up

| Rank | Row | Effort | Why it is ranked here |
|---|---|---|---|
| 1 | **P-01** journal-vault placeholder | small + 1 guard + 1 test | Live, in a shipped dungeon; the guard written to catch it has zero callers |
| 2 | **V-00** `"Hold the last light."` on the loading screen | **one line** | A retired tagline, hardcoded, on every scene load. Cheapest high-value fix in the registry. |
| 3 | **M-03** 194/228 dead `en.json` keys | large / a decision | Structural. There is no localization layer at all — it is the *cause* of §3, §5 and §6. |
| 4 | **V-02** opening cinematic in retired motif | verify, then medium | Possibly the first copy a new player reads. **Verify reachability before rewriting 14 beats** — `IntroSequencePlayer` may already supersede it. |
| 5 | **M-01** two missing `buildingDesc` keys | small + 1 test | Arms a raw `[[missing:]]` marker on the next panel wired |
| 6 | **P-07h/i** raw exception text on payment surfaces | small | Consequence is highest even though the row count is lowest |
| 7 | **C-08 / C-01 / C-02 / C-04** colour-only | medium | Binding project law. C-08 (the palette doc comment) is the systemic root — fix it first and new offenders stop appearing. |
| 8 | **§5 N-tier** non-ASCII | medium, mostly mechanical | ~450 data values + ~90 UI literals. **Fix `ManageScreenVM.Ascii`'s em-dash mapping and promote it into `ElarionUiKit.Label`/`.Button`** and most of this class closes by construction. |
| 9 | **§3 S-01..S-78** silent refusals | broad | Individually small, collectively the owner's biggest time sink. Several files have a *good* branch next to a *bad* one (S-61, S-70, S-77) — those are near-free. |
| 10 | **V-03 / V-03b / V-04 / V-06** vocabulary | small | Player-visible; V-03 mixes both vocabularies inside one quest |
| 11 | **P-07a..e, j** dev vocabulary on screen | small | P-07m/P-07n show the house pattern to copy |
| 12 | **P-02** unverified canon names | one owner decision | |
| 13 | **P-03** talent stub notes | verify first | Rename the field `_note` if it is not rendered |
| 14 | **P-04** lorem ipsum in prefabs | needs a prefab pass | See §0 boundary 1 |
| 15 | **V-08** dormant `avalon` keys | two lines | Closes the last Avalon door |

### Cross-cutting: the three fixes that close the most rows

1. **Promote + fix the ASCII sanitizer.** `ManageScreenVM.Ascii`
   (`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:1307-1320`) already exists; its em-dash
   and degree mappings are wrong (both fall through to a space) and it is only called locally.
   Fix the mapping, move it into `ElarionUiKit.Label` / `.Button`, and most of §5 stops being a
   discipline.
2. **Amend the palette doc comment.** `Assets/_Modules/Core/UI/ElarionUi.cs:79` currently invites
   the C-01/C-02 pattern. Restate the `BuildMenuVM.cs:495-499` rule there and new §4 offenders
   stop being written.
3. **Add the five regressions in §7 step 7.** Every defect in this registry that is *closed*
   rather than latent is closed by a test (§3 S-60). Every defect that is latent — P-01's guard,
   `WallRepairStrings`' `// LOCALIZE:` keys, M-01's key graph — is latent because nothing fails
   on it.
