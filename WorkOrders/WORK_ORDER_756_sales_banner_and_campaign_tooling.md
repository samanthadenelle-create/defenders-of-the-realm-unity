<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-19
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-19) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — 756: In-Game Sales Banner + Owner Campaign-Authoring Tool

**Status:** SPEC — READY
**Author:** Monetization/Promo Architect (design only — no `.cs` written, per CLAUDE.md §2/§13)
**Silo:** Monetization/Backend (§9 — isolated, parallel-safe lane; no scene files, no VillageSceneBuilder)
**Date:** 2026-07-19
**North star:** the OWNER creates + schedules sales campaigns HERSELF (data-first, no code per campaign),
and the game shows an honest banner for whichever campaign is active right now.
**Covenant (binding, honesty law):** honest countdowns only, NO fake scarcity, NO dark patterns, the base
game is never gated behind an offer. A campaign is a *discount/bonus surface*, never a wall. Mirrors the
WO-754 ad covenant + the PackStore "You are never required to spend anything. Ever." line
(`PackStore.cs:196-198`).

---

## 0. TL;DR — this is a DATA-DRIVEN AUTHORING TOOL + a code-built banner, mirroring tools we already ship

Two pieces, both built by cloning existing, proven patterns — **do NOT reinvent**:

1. **Owner Campaign Builder** — a `Defenders > Monetization > Campaign Builder` **EditorWindow** that is a
   near-verbatim clone of the **Motion Caster** (`Assets/Editor/MotionCasterWindow.cs:49`,
   `[MenuItem("Defenders/Animation/Motion Caster")]:151`): list existing rows on the left, edit fields in
   the center, **pick target SKUs from the existing pack catalog**, **preview** the result, **Save** to a
   dual-copy canonical JSON through a guarded static writer. The owner authors a whole campaign
   (name / offer / SKUs / copy / art / window / priority / caps) and hits Save — no code, ever.

2. **In-game Sales Banner** — a **code-built uGUI** component (the PackStore /
   `ElarionUiKit.BuildObsidianModal` language, `PackStore.cs:155`; the ObjectiveBannerUi HUD-strip pattern,
   `ObjectiveBannerUi.cs:29`) that reads the **currently-active** campaign from a Core seam and renders it
   on the store screen + a dismissible HUD strip. Honest countdown, tap → store/offer. At purchase, the
   campaign's discount is applied to the pack price through the PackStore price path.

The seam that connects them is **`promo-campaigns.json`** — the owner's Save writes it, the runtime reads
it. Same shape as: owner authors `motion-castings.json` in Motion Caster → the game reads it via
`MotionCastings.Resolve` (`MotionCastings.cs:183`); owner authors packs, the game reads them via
`PackCatalog` (`PackCatalog.cs:149`).

**NOTHING like this exists yet** (verified §1). There is orphaned `ad-creatives.json` (external ad export,
no in-game consumer) and `ad-placements.json` (rewarded-ad data, WO-754) and `PromoCodeService` (backend
code redemption) — none is a scheduled in-game sales banner. This WO does not touch any of them.

---

## 1. SME AUDIT — what exists, what to clone, what NOT to touch (cite before you build)

### The authoring-tool pattern to CLONE (owner-in-the-loop editor → dual-copy JSON)

| Piece | File:line | What we reuse |
|---|---|---|
| **Motion Caster window** (the template) | `Assets/Editor/MotionCasterWindow.cs:49` (`EditorWindow`), menu `:151`, `OnEnable` scans sources `:160`, list column `:593`, preview column `:787`, binding column `:913`, Save `:1088` | The whole window shape: `[MenuItem("Defenders/...")]`, list-left / edit-center / preview, `Save…` button → static writer, inline never-silent HelpBox feedback (`_lastSaveMsg`), owner is red/green colorblind → **text cues never hue-only** (`:62`, `:850`). |
| **`MotionCastings` interpreter + `WriteRow`** (the writer template) | `Assets/Editor/MotionCastings.cs:80`; `WriteRow` guarded save `:241`; **dual-copy write** (StreamingAssets + Resources mirror, byte-identical) `:316-329`; `manual:true` = CANON, never overwritten without explicit confirm `:274`; `EnsureLoaded`/`Reload` cache `:346`/`:123` | `CampaignCatalogWriter.WriteCampaign` copies this verbatim: validate → write both canonical copies → `Reload()` → `AssetDatabase.ImportAsset` both. Overwriting an existing campaign requires the same explicit owner confirm. |
| Editor reads a catalog **cross-assembly without referencing it** | `MotionCasterWindow.cs:379` (HovlVfxCatalog keys via `SerializedObject`), `:640` (`FindVillageType` reflection seam), `:416` (SfxId via `Type.GetType`) | The Campaign Builder must list **pack SKUs** for the target picker. Preferred: `DeNelle.Editor` → reference `DeNelle.Wallet`'s `PackCatalog.Packs` directly IF the asmdef allows (verify at build); else read `packs.json` inline via the same JSON read. See §3.2. |

### The runtime data-catalog pattern to CLONE (WebGL-safe typed loader)

| Piece | File:line | What we reuse |
|---|---|---|
| **`PackCatalog`** — typed loader over `packs.json` | `Assets/_Modules/Wallet/PackCatalog.cs:149`; `PackDef` typed model `:84`; `LoadCatalog` via **`CanonicalJson.Read`** `:259-287`; `EnsureLoaded`/`Reload` `:252`/`:188`; `Find(sku)` `:169`; a **covenant firewall** that drops non-sanctioned data at load `:230` | `PromoCampaignCatalog` is a straight clone: `[Serializable]` typed models with `[JsonProperty]`, `CanonicalJson.Read("Data/Canonical/promo-campaigns.json")`, `EnsureLoaded`/`Reload`, plus a **window/covenant firewall** (drops malformed or dishonest campaigns at load). |
| **`CanonicalJson.Read`** — the ONE WebGL-safe read | `Assets/_Modules/Core/Data/CanonicalJson.cs:41` (Resources-first, StreamingAssets fallback; source-swappable seam) | The only way `PromoCampaignCatalog` reads its JSON. Never `File.ReadAllText(streamingAssetsPath)` (throws in WebGL). |
| **The SKUs a campaign targets** | `PackDef.Sku` `:87`, `PackDef.Pricing` (`usd`/`usdc`/`sol`/`skr`) `:99`, `PackDef.AmountFor`/`AmountLabel` `:106`/`:122`, `PackCatalog.Packs` `:157` | `targetSkus[]` are FKs into `PackCatalog`. Discount math reuses `PackDef.AmountFor(currency)`. |

### The store / banner UI pattern to CLONE (code-built uGUI — NO UXML in gameplay, CLAUDE.md §8)

| Piece | File:line | What we reuse |
|---|---|---|
| **`PackStore`** — code-built store on the Obsidian frame | `Assets/_Modules/Wallet/PackStore.cs:50`; lazy build on first open `:149`; `ElarionUiKit.BuildObsidianModal` `:155`; `BuildObsidianButton` `:373`; `MakeText` helper `:587`; **every step FlowTrace-instrumented, never a silent blank** `:163`,`:290`; purchase flow `:448`; discount seam target `:372-380` (the Buy label + `Purchase(pack, currency)` call) | The banner mounts **into** the store body (a strip above the pack list) and the discount is applied on the pack cards here. The banner's own build helpers (`MakeText`, `ZoneRect`) are copied from this file. |
| **`ObjectiveBannerUi`** — code-built HUD strip (the dismissible-strip template) | `Assets/_Modules/Core/UI/ObjectiveBannerUi.cs:29`; top-centre non-blocking `:32`; kit language (obsidian glass + gold rule + parchment) `:5`; unscaled-time fade `:31`; `Show/Hide` static API `:57` | The dismissible **HUD sales strip** is this component's twin: top-anchored, non-blocking, kit-styled, one CTA + one dismiss. Kit-promotion candidate later, per its own `:14` note. |
| **`ElarionUiKit`** tokens | `Assets/_Modules/Core/UI/ElarionUiKit.cs:309` (`MinTouchPx = 112`), `:503` (`StorePanelAnchorMin/Max`), `:812` (`BuildObsidianModal`), `BuildObsidianButton` | Banner tap-target ≥ `MinTouchPx`; kit colors via `ElarionUi.*` (`PackStore.cs:174` uses `ElarionUi.ParchmentDim`/`Gold`/`Parchment`). ASCII-only copy (owner rule). |
| **`FeatureFlags`** | `Assets/_Modules/Core/FeatureFlags.cs:621` (`Get(name, defaultOn)`, PlayerPrefs `ff.<name>`); monetization flags are **barred from the URL allow-list** `:632-651` | New flag `ff.salesbanner` (default **OFF** — "unflag when proven"). **Do NOT** add it to `s_urlActivatableFlags` (monetization flag). |
| **Cross-assembly service registry** | `CoreServices` (Hud/Audio/…/Ads slots) — the exact pattern WO-754 `IAdService` follows (`WORK_ORDER_754…:141`) | `IPromoService` gets a `CoreServices.Promo` slot the SAME way, so any module (store, HUD) resolves the active campaign without referencing the Wallet assembly. |

### Existing promo-adjacent code — REUSE the brand block, do NOT conflate with these

| Thing | File:line | Verdict |
|---|---|---|
| `ad-creatives.json` (data-driven **external** ad-creative generator: store screenshots / cross-promo art) | `Assets/Resources/Data/Canonical/ad-creatives.json`; `brand{}` block `:15-20`; templates `:21` | **Orphaned** — its interpreter (`WORK_ORDER_ad_generator.md`) is unimplemented; it produces EXPORT creatives (1080×1920 etc.), not an in-game banner. **REUSE its `brand{}` block** (title "Echoes of Elarion", tagline, `obsidian-gold` palette, `logo_eoa_gold_on_obsidian`) as the banner's default brand tokens so both stay on-brand. Do not build the banner on top of its export schema. |
| `ad-placements.json` + `RewardedAdManager` (WO-754) | rewarded-ad placements/rewards | **Different concern** (opt-in rewarded video). The sales banner composes *beside* it (§4), never inside it. |
| `PromoCodeService` (backend one-time code redemption) | `Assets/_Modules/Core/Promo/PromoCodeService.cs:43` | **Different concern** (operator codes → crystals). A campaign could *advertise* a code, but this WO does not touch redemption. The banner may deep-link to the code UI (`PromoCodeUI.cs`) as one `cta` route (§3.4). |

**Conclusion:** every ingredient — the editor shell, the guarded dual-copy writer, the WebGL-safe typed
loader, the code-built store/HUD banner language, the Core service registry, the feature-flag gate, and the
brand tokens — already exists in the tree. This WO assembles a **promo-campaign** vertical out of them.

---

## 2. CAMPAIGN DATA MODEL — `promo-campaigns.json` (dual-copy, versioned)

### 2.1 Files (dual-copy canonical, the §1 rule)
- `Assets/StreamingAssets/Data/Canonical/promo-campaigns.json` (source + desktop fallback)
- `Assets/Resources/Data/Canonical/promo-campaigns.json` (WebGL-safe mirror — `CanonicalJson.Read` reads
  this first). The writer keeps both **byte-identical** exactly as `MotionCastings.WriteRow` does
  (`MotionCastings.cs:316-329`).

### 2.2 Schema (owner-readable, hand-diffable — `clip`-path philosophy of `MotionCastings.cs:23`)

```json
{
  "_comment": "OWNER-AUTHORED sales campaigns for Echoes of Elarion. Authored via Defenders > Monetization > Campaign Builder; read at runtime by PromoCampaignCatalog. The runtime shows the ONE active campaign (now within [startUtc,endUtc] AND active:true) of highest priority. Honest countdowns only; no fake scarcity.",
  "version": 1,
  "brand": {
    "title": "Echoes of Elarion",
    "palette": "obsidian-gold",
    "logoLockup": "logo_eoa_gold_on_obsidian"
  },
  "campaigns": [
    {
      "id": "launch-founders-week",
      "title": "Founder's Week",
      "active": true,
      "offerType": "percent_off",           // percent_off | bonus_grant | bundle | first_buy | daily_deal
      "targetSkus": ["lanternlight", "founders-vow"],
      "discountPercent": 20,                  // used by percent_off (0..90)
      "bonus": { "kind": "crystals", "amount": 0 },  // used by bonus_grant (kind from the pack economy vocabulary)
      "banner": {
        "headline": "Founder's Week - 20% off",
        "subcopy": "Light the first lanterns. Ends soon.",
        "ctaLabel": "Open the Store",
        "ctaRoute": "store",                  // store | sku:<sku> | promocode | url:<https...>
        "artPath": "Assets/Promo/Banners/founders_week.png"  // optional; empty -> kit obsidian plate
      },
      "startUtc": "2026-07-20T00:00:00Z",
      "endUtc":   "2026-07-27T00:00:00Z",
      "priority": 100,                        // higher wins when windows overlap
      "frequencyCap": { "hudPerDay": 3, "dismissible": true },
      "createdUtc": "2026-07-19T18:00:00Z",
      "source": "campaign-builder"
    }
  ]
}
```

### 2.3 Typed C# model (`[Serializable]` + `[JsonProperty]`, the `PackDef` pattern `PackCatalog.cs:84`)
- `PromoCampaignData { int Version; PromoBrand Brand; List<PromoCampaign> Campaigns; }`
- `PromoCampaign { Id, Title; bool Active; string OfferType; List<string> TargetSkus; int DiscountPercent;
  PromoBonus Bonus; PromoBanner Banner; string StartUtc, EndUtc; int Priority; PromoFrequencyCap
  FrequencyCap; string CreatedUtc, Source; }`
- `PromoBanner { Headline, Subcopy, CtaLabel, CtaRoute, ArtPath; }`
- `PromoBonus { Kind; int Amount; }`  ·  `PromoFrequencyCap { int HudPerDay; bool Dismissible; }`
- `OfferType` parsed to an enum `{ PercentOff, BonusGrant, Bundle, FirstBuy, DailyDeal }` with an
  unknown-value → **skip + FlowTrace.Warn** (never a silent bad offer, §12).

### 2.4 Honesty firewall (load-time, mirrors `PackCatalog.EnforceCovenant` `:230`)
`PromoCampaignCatalog` drops/repairs at load, never silently:
- `endUtc <= startUtc`, or an un-parseable date → **drop** the campaign + `FlowTrace.Fail`.
- `discountPercent` outside `0..90` → clamp to range + `FlowTrace.Warn` (no "99% off" fake-scarcity).
- `targetSkus` containing a SKU absent from `PackCatalog` → drop that SKU + `Warn` (keep the campaign if it
  still has ≥1 valid SKU; else drop).
- `offerType` unknown → drop + `Warn`.
- A campaign with `active:false` is retained in data (owner's draft) but **never** resolved as active.

---

## 3. THE THREE PARTS

### 3.1 Runtime seam — `IPromoService` + `PromoCampaignCatalog` (Core, mirrors CoreServices.Ads)

**`Assets/_Modules/Core/Promo/IPromoService.cs`** (namespace `DeNelle.Core.Promo`):
```csharp
public interface IPromoService
{
    /// <summary>The single active campaign now (in-window, active, highest priority), or null.</summary>
    PromoCampaignView ActiveCampaign { get; }
    /// <summary>Discount fraction 0..0.9 this campaign applies to a SKU (0 if none/not targeted).</summary>
    float DiscountFractionFor(string sku);
    /// <summary>Seconds remaining until the active campaign's endUtc (>=0), for the honest countdown.</summary>
    double SecondsRemaining { get; }
    event System.Action ActiveCampaignChanged;
}
```
- `PromoCampaignView` = a Core-safe read model (no Wallet types) carrying id/title/headline/subcopy/
  ctaLabel/ctaRoute/artPath/targetSkus/offerType/endUtc — so the HUD (`DeNelle.HUD`) and store
  (`DeNelle.Wallet`) both consume it without a cross-dependency.
- `CoreServices.Promo` slot + `RegisterPromo`/`UnregisterPromo` — copied verbatim from the Audio/Ads slot
  (the WO-754 §3.1 recipe), same double-register `FlowTrace.Warn` + null-check discipline.

**`PromoCampaignCatalog`** (static, `DeNelle.Core.Promo` or a leaf — see asmdef note §5): the `PackCatalog`
clone (`CanonicalJson.Read` → parse → firewall → cache). `ResolveActive(DateTime utcNow)` returns the
in-window, `active:true`, highest-`priority` campaign (ties broken by earliest `endUtc`, then id ordinal).

**`PromoService`** (the registered `IPromoService` impl — plain C# or a tiny bootstrap MonoBehaviour like
`PromoCodeService.EnsureExists` `:55`): resolves `ActiveCampaign` from the catalog, re-resolves on a cheap
timer (once/minute — a campaign flips active/inactive on the wall clock; never per-frame), raises
`ActiveCampaignChanged`, computes `SecondsRemaining` from real UTC (honest). Registered by a
`[RuntimeInitializeOnLoadMethod]` bootstrap so `CoreServices.Promo` is non-null everywhere.

### 3.2 Owner editor — `Defenders > Monetization > Campaign Builder` (clone MotionCasterWindow)

**`Assets/Editor/CampaignBuilderWindow.cs`** (`DeNelle.Editor`, `EditorWindow`), `[MenuItem("Defenders/
Monetization/Campaign Builder")]`. Layout = MotionCaster's three columns:

- **Left — campaign list** (`MotionCasterWindow.cs:593` clone): every campaign from
  `promo-campaigns.json`, each row shows `title` + a **TEXT** status tag computed from the wall clock —
  `[LIVE]` / `[SCHEDULED <date>]` / `[ENDED]` / `[DRAFT active:false]` (text, not color — owner is
  colorblind, `:62`). `+ New Campaign` button. Selecting a row loads it into the editor.
- **Center — edit fields** (the binding-column clone `:913`):
  - `Title` (text), `Active` (toggle), `Offer Type` (popup over the enum).
  - **Target SKUs** — a multi-toggle list built from the pack catalog. **Source:** if
    `DeNelle.Editor`'s asmdef references `DeNelle.Wallet`, call `PackCatalog.Packs` directly and list
    `pack.Sku` + `pack.Name` + `pack.UsdReference`; **else** (asmdef bars it) read
    `packs.json` inline with the same `CanonicalJson`-style read + a `SerializedObject`/JObject parse — the
    exact "list a catalog cross-assembly" seam MotionCaster uses for VFX keys (`:379`). A SKU that is toggled
    but later removed from packs.json shows a `WARN unknown SKU` text flag on load.
  - `Discount %` (int slider 0..90, shown only for `percent_off`) · `Bonus kind`+`amount` (shown for
    `bonus_grant`, kind popup over the pack economy vocabulary: crystals/food/coins/glimmer).
  - `Headline`, `Subcopy`, `CTA label` (text fields, **ASCII-only** validation → inline warn), `CTA route`
    (popup: Store / a specific SKU / Promo code / URL), `Art path` (`ObjectField`→ path, optional).
  - **Window:** `Start (UTC)` + `End (UTC)` as text fields `yyyy-MM-ddTHH:mm:ssZ` with a parse-validate +
    live "runs 7 days / ENDS BEFORE START (invalid)" helper line. `Priority` (int). `HUD per day` (int),
    `Dismissible` (toggle).
- **Preview** (the MotionCaster preview-column role `:787`): render the **actual banner** — reuse the
  runtime banner build in an editor host, OR a faithful IMGUI mock (headline + subcopy + CTA + a live
  countdown computed from End−now + the resolved discounted price of the first target SKU, e.g.
  "~~$4.99~~ $3.99"). The owner sees exactly what ships before saving — the MotionCaster "preview bundle in
  the stage" principle (`:816`).
- **Save** (`SaveBinding` clone `:1088`): `CampaignBuilderWindow` → `CampaignCatalogWriter.WriteCampaign
  (campaign, allowOverwrite)`:
  - Validate (same firewall as §2.4 — refuse an invalid window / empty title / no valid SKU, **loudly**).
  - If overwriting an existing id → **explicit owner confirm dialog** (the `manual:true` canon rule
    `MotionCastings.cs:274` / `MotionCasterWindow.cs:1123`).
  - Write **both** canonical copies byte-identically, `AssetDatabase.ImportAsset` both, `Reload()`, and an
    inline persistent `_lastSaveMsg` HelpBox: "SAVED 'Founder's Week' — LIVE now / goes live <date>. The
    game will show it when `ff.salesbanner` is ON."
  - A `Deactivate` / `Delete campaign` button (delete requires confirm) so the owner ends a sale without
    hand-editing JSON.

**Owner start-to-finish:** open the window → `+ New Campaign` → type a title, pick `percent_off` + 20% →
toggle the two target packs from the list → type headline/subcopy/CTA → set start/end UTC + priority →
watch the live preview + discounted price → **Save**. The sale is now data; with `ff.salesbanner` ON the
banner shows it in-game on its start date and hides it on its end date. No code, no rebuild.

### 3.3 Runtime banner — code-built uGUI (store strip + dismissible HUD strip)

**`Assets/_Modules/Wallet/SalesBannerController.cs`** (or `DeNelle.HUD` for the HUD strip — see §5), all
code-built in the kit language (NO UXML, CLAUDE.md §8), ASCII-only, ≥`MinTouchPx` tap target:

- **Store strip:** mounts into the PackStore body above the pack list. When `CoreServices.Promo?
  .ActiveCampaign != null`, draws an obsidian plate (art from `artPath` if present, else the kit plate) with
  `Headline`, `Subcopy`, an **honest live countdown** ("Ends in 2d 14h", recomputed from
  `SecondsRemaining`, hidden when the campaign has no end urgency), and a CTA button. It's built with
  `PackStore`'s own `MakeText`/`ZoneRect`/`BuildObsidianButton` helpers (`:587`/`:545`/`:373`) and
  FlowTrace-instrumented like the store (`:163`) so a missing element self-reports, never a blank strip.
- **HUD strip:** the `ObjectiveBannerUi` twin (`ObjectiveBannerUi.cs:29`) — top-anchored, non-blocking,
  kit-styled, ONE CTA + a **dismiss (x)** when `frequencyCap.dismissible`. Frequency: shows at most
  `hudPerDay` times/day (PlayerPrefs day-count key, the `PromoCodeService` PlayerPrefs-dedup style
  `:206-222`); dismiss hides it for the session. Never auto-opens a modal; it is a button the player may
  press. Gated on `ff.salesbanner` AND a non-null active campaign.
- **Tap routing:** `ctaRoute` → `store` opens the PackStore via the existing `MarketplaceInteractor`
  open-path (the same interactor PackStore closes through, `PackStore.cs:236` reflection seam) · `sku:<sku>`
  opens the store scrolled/highlighted to that pack · `promocode` opens `PromoCodeUI` · `url:<...>` →
  `Application.OpenURL`. Unknown route → open store + `FlowTrace.Warn`.

### 3.4 Discount at purchase — hook the PackStore price path (additive, PackDef stays immutable)

The active campaign's discount is applied where the pack price is shown + charged (`PackStore.cs:372-380`):
- Add `IPromoService.DiscountFractionFor(sku)` consult in `PackStore.BuildPackCard`: when a targeted SKU
  has a live `percent_off`, render the Buy label as the discounted amount + a struck-through reference
  (e.g. "Buy 48 SKR" with "~~60 SKR~~"), tagged with the campaign title. Non-targeted packs unchanged.
- Pass the discounted amount into the pay path: `Purchase(pack, currency)` (`:448`) computes the charged
  amount from `pack.AmountFor(currency) * (1 - discount)` and hands it to `WalletService.Pay`. **Keep
  `PackDef` immutable** — the discount is a computed override at the call site, never a mutation of the
  cached catalog. (If `WalletService.Pay(PackDef, currency)` has no amount-override overload, add one
  additively; do not change the un-discounted signature's behavior.)
- `bonus_grant`/`first_buy`/`daily_deal`/`bundle` offer types affect **grant/eligibility**, not the sticker
  price — for the FIRST CUT they render on the banner + card copy only; wiring their grant seams
  (extra crystals on purchase, first-buy doubling) is a Phase-2 follow-on (§5). `percent_off` is the fully
  wired offer type in cut 1.

---

## 4. ARCHITECTURE — how it composes (HP B2B: data-only, one owner per concern)

- **One owner per concern:** the JSON is the single source; `PromoCampaignCatalog` is the ONE reader;
  `PromoService` is the ONE resolver of "what's active"; `CampaignCatalogWriter` is the ONE writer (editor).
  Presentation (`SalesBannerController`) never parses JSON — it consumes `CoreServices.Promo` (presentation
  is a separate layer, CLAUDE.md architecture law).
- **Composes with WO-754 (ads):** parallel Core seams — `CoreServices.Ads` (rewarded) and
  `CoreServices.Promo` (sales). Both isolated in the Monetization lane (§9). A store screen can show BOTH a
  rewarded-crystals offer and a sale banner; they never reference each other.
- **Composes with WO-755 (pack catalog):** the campaign `targetSkus[]` are FKs into `PackCatalog`
  (`packs.json`). When WO-755 evolves the pack catalog/authoring, campaigns keep resolving by SKU string —
  the firewall (§2.4) drops any SKU that no longer exists. If WO-755 ships a pack-authoring editor, the
  Campaign Builder's SKU picker reads from the same `PackCatalog` surface (one catalog, two authoring tools).
- **Feature-flag gated:** `ff.salesbanner` default OFF ("unflag when proven"); NOT URL-activatable
  (`FeatureFlags.cs:632`). With it OFF the game is byte-behavior-identical to today.
- **Testable / headless:** an EditMode/DataRegression test seeds a fixture `promo-campaigns.json` with three
  campaigns (past / live / future + an overlapping higher-priority one) and asserts `ResolveActive` picks
  the right one at a fixed `utcNow`; asserts the firewall drops an inverted window and clamps a 99% discount;
  asserts `DiscountFractionFor` is 0 for a non-targeted SKU. A headless AutoPilot pass opens the store with a
  fixture live campaign and confirms the banner builds (FlowTrace `[Flow:Promo]` step) + the discounted Buy
  label renders. No owner playtest needed to prove the data path (§12 headless-first).

### What ships FIRST vs the full editor
- **Cut 1 (ship the banner + one campaign):** the data model + `promo-campaigns.json` seeded with **one
  hand-authored `percent_off` campaign**; `PromoCampaignCatalog` + `IPromoService`/`CoreServices.Promo` +
  `PromoService`; `SalesBannerController` (store strip + HUD strip, honest countdown, tap→store); the
  PackStore `percent_off` discount hook; `ff.salesbanner`; the regression + headless proof. This proves the
  whole vertical with zero editor work — the owner can already run a sale by editing one JSON block.
- **Cut 2 (the full owner tool):** `CampaignBuilderWindow` + `CampaignCatalogWriter` (list/create/edit/pick-
  SKUs/preview/save/deactivate). Now the owner authors campaigns with no JSON.
- **Phase 3 (offer breadth):** wire `bonus_grant` / `first_buy` / `daily_deal` / `bundle` grant seams;
  optional analytics funnel (`campaign_viewed`/`campaign_cta`/`campaign_purchase` via `EventTracker`,
  `PackStore.cs:305` style); optional A/B priority scheduling.

---

## 5. ASSEMBLY / PLACEMENT NOTES (verify at implement — do not guess, §12)
- `IPromoService` + `PromoCampaignView` + `PromoCampaignCatalog` + models live in **`DeNelle.Core`**
  (namespace `DeNelle.Core.Promo`, alongside `PromoCodeService`'s namespace) so HUD + Wallet both consume
  them with no new cross-dependency. `CoreServices.Promo` slot lives with the other slots.
- `SalesBannerController`: the **store strip** part is fine in `DeNelle.Wallet` (it already builds store UI);
  the **HUD strip** part belongs in `DeNelle.HUD` (which references Core only). If one component must serve
  both, put it in `DeNelle.HUD` and let PackStore mount the strip via a Core interface — verify the asmdef
  graph before choosing (Village→Wallet is one-way, `PackStore.cs:229`).
- `CampaignBuilderWindow` + `CampaignCatalogWriter`: **`DeNelle.Editor`**. Verify whether `DeNelle.Editor`
  may reference `DeNelle.Wallet` for `PackCatalog`; if not, use the inline-JSON SKU read (§3.2) — the
  MotionCaster cross-assembly-read precedent (`MotionCasterWindow.cs:379`).
- The writer mirrors **only the production path** to the Resources copy (MotionCastings guards test
  fixtures from clobbering the real mirror, `MotionCastings.cs:323`) — copy that guard.

---

## 6. WHAT NOT TO TOUCH
- Do **NOT** hand-edit any `.unity` scene (§3) or `VillageSceneBuilder` (§9 lane is scene-free).
- Do **NOT** build any UI in UXML/UITK — code-built uGUI only (CLAUDE.md §8, PackStore WO-F conversion).
- Do **NOT** mutate `PackDef`/`PackCatalog` cached data to apply a discount — compute at the call site.
- Do **NOT** rebuild the ad-creative generator (`ad-creatives.json`), the rewarded-ad system (WO-754), or
  `PromoCodeService` — only REUSE the brand tokens + (optionally) deep-link to `PromoCodeUI`.
- Do **NOT** add `ff.salesbanner` to the URL-activatable allow-list (`FeatureFlags.cs:636`) — monetization.
- Do **NOT** ship fake scarcity: countdowns are real UTC math; discounts clamp ≤90%; a campaign hides itself
  the instant its window closes.
- ASCII-only copy in all banner strings (owner rule). Text status cues, never hue-only (owner colorblind).
- Brace-gate + `CompileGate` green; null-conditional on every `CoreServices.Promo?.` call; no silent catch.

---

## 7. ACCEPTANCE CRITERIA
- [ ] `promo-campaigns.json` dual-copy (StreamingAssets + Resources) shipped, versioned, with one seed
      `percent_off` campaign; `_comment` documents the schema.
- [ ] `PromoCampaignCatalog` loads via `CanonicalJson.Read` (WebGL-safe); firewall drops inverted windows /
      unknown SKUs / unknown offer types and clamps discounts to 0..90, each with a FlowTrace line.
- [ ] `IPromoService` + `PromoCampaignView` + `CoreServices.Promo` slot (Register/Unregister) mirror the
      Audio/Ads slot; `PromoService` registered by a bootstrap so the slot is non-null everywhere.
- [ ] `ResolveActive` returns the in-window, active, highest-priority campaign (regression-proven at a fixed
      `utcNow` with past/live/future/overlap fixtures).
- [ ] `SalesBannerController` renders (store strip + dismissible HUD strip) code-built, kit-styled,
      ASCII-only, honest countdown from real UTC, tap → store/offer; gated on `ff.salesbanner` (default OFF)
      + a non-null active campaign; every build step FlowTrace-instrumented, never a silent blank.
- [ ] PackStore applies a live `percent_off` to targeted SKUs: discounted Buy label + struck-through
      reference on the card, discounted amount charged through `WalletService.Pay`; `PackDef` unmutated;
      non-targeted packs unchanged.
- [ ] `Defenders > Monetization > Campaign Builder` window: lists campaigns with text status tags,
      creates/edits all fields, picks target SKUs from the pack catalog, previews the banner + discounted
      price, and Saves via `CampaignCatalogWriter` (dual-copy, byte-identical, overwrite-confirm, import +
      reload, inline never-silent save message); can Deactivate/Delete a campaign.
- [ ] `ff.salesbanner` (default OFF) added; NOT in the URL allow-list.
- [ ] EditMode/DataRegression test + a headless AutoPilot pass prove the data path + banner build with no
      owner playtest.
- [ ] Canon: one-line entries in `PIPELINE_STATE.md` §8 + `docs/MASTER_CATALOG.md` pointing at
      `IPromoService` + `promo-campaigns.json` + `ff.salesbanner` + this WO.

## 8. LANE / COORDINATION
Monetization/Backend lane (§9) — isolated, parallel-safe (new Core interface + new data catalog + new editor
window + one additive PackStore price hook; no scene files, no VillageSceneBuilder). Single-committer
reconciliation per §11. Composes cleanly with WO-754 (ads) and WO-755 (pack catalog) — shared only through
`CoreServices` slots and the `packs.json` SKU FK.
```
