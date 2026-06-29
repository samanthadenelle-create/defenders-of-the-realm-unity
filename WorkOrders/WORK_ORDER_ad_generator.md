# WORK ORDER — Ad Generator (Rewarded-Ad Hook Logic + Creative Ad Generator)

**Status:** READY TO IMPLEMENT (design complete; data + schema shipped)
**Author:** CLI agent (design only — no `.cs` written, per §2/§13)
**Silo:** Monetization/Backend (§9 — isolated lane)
**Date:** 2026-06-28
**Owner data-first directive:** every decision below is DATA in a JSON catalog read by a
THIN runtime interpreter. No hardcoded `if (placement == "store")` branches — the table
decides; the interpreter just reads, gates, and dispatches (memory: *owner-thinks-in-data-structures*).

---

## 0. Why one work order, two senses

The owner uses "ad generator" two ways. This WO covers BOTH, sharing one design spine
(a catalog + a thin interpreter + a self-reporting flow trace):

- **(A) Rewarded-Ad HOOK LOGIC** — *where/when* a rewarded-ad offer appears in-game and
  *what reward* it grants. Data: `ad-placements.json`. Interpreter: `AdGateService`.
- **(B) Creative AD GENERATOR** — *templated promo creatives* (store-screenshot + headline
  + body) generated from game records (hero/pack/feature). Data: `ad-creatives.json`.
  Interpreter: `AdCreativeGenerator`.

Both are flag-gated by a new `FeatureFlags.AdGenerator` (`ff.adgenerator`, default OFF —
"unflag when proven", consistent with the existing flag law).

---

## Files shipped with this WO (data + schema)

| File | Sense | Role |
|---|---|---|
| `Assets/Resources/Data/Canonical/ad-placements.json` | A | Rewarded-ad placements, cooldowns, daily caps, reward table, global config. Self-documenting `_schema`. |
| `Assets/Resources/Data/Canonical/ad-creatives.json` | B | Creative templates + feature subjects + **3 generated sample AdSpecs**. Self-documenting `_schema`. |

Both follow the canonical JSON convention already in this folder (`_comment`, `_sources`,
`_schema`/`_schemaNotes`, `version`). The `_schema` block IS the schema — same pattern as
`packs.json` (`_schemaNotes.packDef`), so no separate JSON-Schema file is needed and the
loader stays a plain `JsonUtility`/Newtonsoft deserialize.

---

## A. REWARDED-AD HOOK LOGIC

### A.1 Problem with what exists
`Assets/_Modules/Village/Monetization/RewardedAdManager.cs` is a SINGLE hardcoded gate:
one fixed `CooldownSeconds = 480`, one implicit reward (whatever the call-site's `onReward`
lambda does), no placement identity, no daily cap, no reward table. That is a control-flow
gate, not a data model. We keep it as the **SDK seam** (its `ShowAdInternal` virtual is the
real-ad hook) and put the POLICY in data above it.

### A.2 Data model (`ad-placements.json`)
Three tables (full field docs live in the file's `_schema`):

- **`global`** — `defaultCooldownSeconds`, `hardDailyCap` (sum across all placements),
  `adProvider` (`stub`|`unityads`|`admob`), `respectDoNotSell`, `covenantLine`.
- **`rewards[]`** — `{ id, kind (currency|timeskip|harvest|buff|cosmeticTrial), grant, description, maxStack }`.
  `grant` is shaped by `kind` and resolves through EXISTING services
  (EconomyService for `currency`, OfflineHarvestService for `harvest`, BuildTimerService for
  `timeskip`, battle controller for `buff`). Reward vocabulary = the same currencies as
  `packs.json` (glimmer/crystals/food/coins) so there is one economy language.
- **`placements[]`** — `{ id, enabled, surface, rewardId (FK), cooldownSeconds, dailyCap,
  requiresFlag, priority, prompt {headline,body,cta} }`. `id` is the STABLE key a call-site
  passes; `surface` groups them; `priority` breaks ties when two placements target one surface.

### A.3 Thin interpreter — `AdGateService` (CLI to author; NOT in this WO)
A small MonoBehaviour/service, ~120 lines, NO per-placement branches:

```
bool CanOffer(string placementId)          // enabled && flag ok && cooldown elapsed && under dailyCap && under hardDailyCap
AdOffer GetOffer(string placementId)        // returns prompt + resolved reward.description, or null
void Show(string placementId, Action onGranted)
```

`Show` flow (all FlowTrace-instrumented per §12):
1. `FlowTrace.Step("AdGate","offer", placementId)` → `CanOffer` guard; fail-fast with `Warn` if blocked (cooldown/cap), return.
2. record per-placement `lastShownRealtime` + increment per-placement + global day counters (persisted in `GameState`/PlayerPrefs keyed by local day).
3. delegate presentation to the existing `RewardedAdManager.TryShowAd` (the SDK seam) — provider chosen by `global.adProvider`; `stub` grants immediately for headless/devnet.
4. on genuine completion → `ResolveReward(rewardId)` dispatches by `reward.kind` to the matching service via a `Dictionary<kind, Func>` map (DATA dispatch, not `switch` sprawl); `maxStack>0` queues unclaimed grants, `0` applies immediately.
5. `FlowTrace.Step("AdGate","granted", rewardId)`; invoke `onGranted`.

**Caps & cooldown are realtime** (`Time.realtimeSinceStartup`, survives `timeScale=0`) like
the current manager. Daily caps reset on local-day rollover.

### A.4 Call-sites (data-addressed, no new branches)
Each surface just asks the table by id:
- Store crystals button → `place.store.crystals`
- Harvest screen "double" → `place.harvest.doubler`
- Build timer card → `place.build.skip`
- Battle defeat screen → `place.defeat.continue` (gated `ff.overworldencounter`)
- Daily reward → `place.daily.chest`

Adding/removing/retuning an ad hook = edit JSON, zero code change.

---

## B. CREATIVE AD GENERATOR

### B.1 Concept
An ad creative = a **template bound to a subject record**. The generator reads
`templates[]`, picks a subject row from a named canonical catalog (`packs.json`,
`weapons.json`, or the inline `_featureSubjects`), interpolates `{tokens}` from the subject's
fields, and emits an **AdSpec** (headline + body + cta + screenshot recipe + palette). This
mirrors the Gear/Item generators already in the project (data row → spec), so it's the same
muscle, not a new paradigm.

### B.2 Data model (`ad-creatives.json`)
- **`brand`** — title/tagline/palette/logo lockup (from BRAND_BIBLE).
- **`templates[]`** — `{ id, format, subjectType, headlinePattern, bodyPattern, ctaPattern,
  screenshotRecipe {scene,camera,subjectBinding,overlays[]}, palette, tokens[], audience }`.
  Patterns hold `{tokens}`; `{name}`, `{tagline}`, `{pricing.usd}`, `{flavor}`,
  `{contents.cosmetics.count}` resolve by FIELD PATH on the subject; `{brand.*}` from brand.
- **`_featureSubjects.subjects[]`** — the only subjectType without an existing catalog
  (player-facing pillars → capture scene/camera).
- **`generated[]`** — emitted AdSpecs. THREE worked samples shipped (see B.4).

### B.3 Thin interpreter — `AdCreativeGenerator` (CLI to author; NOT in this WO)
```
AdSpec Generate(string templateId, string subjectId)
List<AdSpec> GenerateAll()        // cross every template with its subjectType's catalog
```
Flow: load template → load subject record by `subjectType` → for each `{token}` resolve by
path (missing → `FlowTrace.Warn` + drop the clause, NEVER ship a literal `{token}`) →
fill patterns → attach `screenshotRecipe` (the screenshot is captured by the existing
**headless screenshot fleet**, run-defenders skill; `status: pending-capture` until then) →
return AdSpec. `GenerateAll` is how a marketing batch (every pack, every hero) is produced
in one pass.

### B.4 Three sample generated AdSpecs (in `ad-creatives.json` → `generated[]`)
1. **`ad.hero.knight.001`** — hero spotlight, portrait 1080x1920. Headline "Become the
   Knight"; subject = V1 KnightOnly hero. Capture: HeroSelect carousel.
2. **`ad.pack.lanternlight.001`** — pack value, square 1080. Headline + tagline + `$4.99` +
   "2 cosmetics" pulled VERBATIM from `packs.json`. Capture: PackStore card focus, covenant
   line overlaid.
3. **`ad.feature.harvest.001`** — feature hook, landscape 1200x628 (FB/Google sizes).
   Headline "Grow the Heart-Grove"; subject = `harvest-loop` pillar. Capture: Village2
   grove_wide.

### B.5 Output destinations
One AdSpec serves three sinks (no re-authoring): (1) store/ASO screenshots, (2) in-game
cross-promo cards, (3) external creative export (PNG + copy block) for paid UA channels.
Generated PNGs land in `Assets/Promo/Generated/` (gitignored bucket; recipe is the source
of truth, the PNG is a build artifact).

---

## Acceptance criteria

### Sense A — rewarded hook
- [ ] `AdGateService` loads `ad-placements.json`; ZERO per-placement `if/switch` on placement id.
- [ ] `CanOffer` honors: `enabled`, `requiresFlag`, per-placement cooldown, per-placement `dailyCap`, `global.hardDailyCap`.
- [ ] Reward resolution dispatches by `reward.kind` through a kind→handler map to EXISTING services (Economy/OfflineHarvest/BuildTimer/battle); no new currency invented.
- [ ] Presentation delegates to `RewardedAdManager` (SDK seam preserved); `stub` provider grants headlessly.
- [ ] Every step FlowTrace-instrumented (`[Flow:AdGate]`); a blocked offer logs WHY (cooldown vs cap vs flag), never silently.
- [ ] Daily caps persist across sessions and reset on local-day rollover.
- [ ] Adding a new ad hook requires JSON-only edit (prove with a 6th placement, no recompile beyond data reload).

### Sense B — creative generator
- [ ] `AdCreativeGenerator.Generate(templateId, subjectId)` returns a fully-filled AdSpec for all 3 samples reproduced from data (matches the shipped `generated[]`).
- [ ] Token resolution reads field PATHS from the real `packs.json`/`weapons.json` records (no copy-paste of pack copy into the generator).
- [ ] A missing/typo token logs `FlowTrace.Warn` and drops the clause — never emits a literal `{token}` into headline/body.
- [ ] `GenerateAll` crosses each template with its catalog (e.g. all 5 packs → 5 AdSpecs) in one pass.
- [ ] Screenshot recipes name a real scene/camera capturable by the headless fleet; `status` flips `pending-capture`→`captured` when the PNG exists.

### Shared
- [ ] New `FeatureFlags.AdGenerator` (`ff.adgenerator`, default OFF). Both services no-op when off.
- [ ] Brace gate + CompileGate green; no `.unity` hand-edits; no new `System.Reflection`.
- [ ] Canon: add a one-line entry to `PIPELINE_STATE.md` §8 (monetization) pointing at these two data files.

## What NOT to touch
- Do NOT modify `RewardedAdManager.cs` beyond (optionally) letting `AdGateService` call its
  public `TryShowAd` — it stays the SDK seam.
- Do NOT add a real ad SDK in this WO (`adProvider: stub` only; SDK is a later platform pass).
- Do NOT invent new currencies — reward `grant.currency` must be one already in `packs.json`.
- Do NOT hand-author per-creative copy in code — copy comes from the subject record + pattern.
- Do NOT wire purchase/Stripe — rewarded ads are out-of-store (no wallet rail).

## Lane / coordination
Monetization/Backend lane (§9) — isolated, parallel-safe. No VillageSceneBuilder, no scene
files, no shared serialization bottleneck. Single-committer reconciliation per §11.
