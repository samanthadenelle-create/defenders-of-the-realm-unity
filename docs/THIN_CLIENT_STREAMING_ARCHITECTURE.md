# Thin-Client Streaming Architecture — reality, plan, projected savings

Owner note: *"we did it all with that always being the intent."* Correct — the seams exist
(`HeroAssetLoader`, `AddressablesGroupConfig`), but the migration was never finished. Today
the app is a **fat client**. This doc is the reality + the concrete path to finish it + the
size it would actually save. Code-verified 2026-07-24.

---

## 1. REALITY — it is a FAT client (verified)

The "streams from Addressables" model is **aspirational, not implemented.** Three independent
facts, any one of which alone keeps all art in the APK:

1. **Every Addressables group is LOCAL, none Remote.** Five groups exist; only **Gear** (425
   KayKit meshes) + localization carry content — all with Local build/load paths
   (`AddressableAssetSettings.asset:63-87`, all `*_BundledAssetGroupSchema.asset:37-40`).
2. **No remote host/CDN/catalog exists at all.** `m_BuildRemoteCatalog: 0`, `m_CCDEnabled: 0`,
   Remote.LoadPath = `<undefined>`, no `ServerData/` folder. Nowhere to stream *from*.
3. **The heavy art isn't addressable in the first place.** ~439 `Resources.Load` calls across
   188 files vs ~15 `Addressables.Load*`. `Resources/` = **768 MB, always baked**. The typed
   `AssetReference` registry (`AddressablesGroupConfig.cs`) is unpopulated scaffolding; the
   Hero Addressables seam always falls back to `Resources.Load` (`HeroAssetLoader.cs:95`).

**Current cost:** APK **453 MB** (1.9 GB uncompressed), **textures = 72.9%** (554.8 MB).

### Where the baked weight actually is (real disk measurements)
| Source | MB | Streamable? | Notes |
|---|---:|---|---|
| **`Resources/Enemies`** | **504** | ★★★ ideal | loaded per-wave/encounter — stream on spawn |
| `Resources/Heroes` | 81 | ★★★ | per-selection; = WO-282 (on HOLD), seam built |
| `Resources/RpgUi` | 67 | ★ partial | some UI is boot-critical (keep local) |
| `Resources/Structures` | 32 | ★★ | stream as catalog browsed/placed |
| `Resources/` other (HudIcons/ItemIcons/Portraits/Pets/…) | ~84 | ★ mixed | icons often boot-critical |
| Scene-referenced packs: Polyperfect ~408, Hovl VFX ~232, Art ~176 | ~816* | ★★ | *disk source; baked only where scene-referenced; harder (not in Resources) |

Note: texture-shrink caps are ALREADY applied (0 MB free from re-running) — so **relocation to
remote, not further compression, is the real lever.** (WO-767 = tightening caps = a smaller,
quality-tradeoff win on top.)

---

## 2. TARGET — a thin boot client + remote content

- **Thin APK (boot):** engine (~40-50 MB IL2CPP) + core scenes + boot-critical UI/icons +
  ONE starting hero + catalogs/data + essential SFX. Target **~120-200 MB**.
- **Remote (streamed on demand from a CDN):** enemy roster art, alternate heroes, structure/
  tower catalog art, VFX packs, environment packs, music. Downloaded per-need + cached.
- **Remote catalog ON** → content/balance updates **without a store resubmit** (see §5 — this
  is arguably as valuable as the size win for a live crypto game).

---

## 3. IMPLEMENTATION — phased, measure each step

Sequence by *biggest-win / lowest-risk first*. Each phase is independently shippable + measurable.

**Phase 0 — Stand up remote hosting (the enabler).** Pick a host: **Cloudflare R2 (free
egress — best for a game)**, or Unity **CCD** (turnkey, free tier then paid), or S3/Vercel Blob.
Set the Remote profile LoadPath to the URL, `BuildRemoteCatalog = 1`. No content moves yet;
this just makes "remote" possible. *(Reconciles WO-281 addressables/SBP build blocker.)*

**Phase 1 — Enemies → Remote (the 504 MB win).** Move `Resources/Enemies` out of Resources →
new **Enemies** Remote group → migrate the enemy-spawn load path to `Addressables.LoadAssetAsync`
(load on wave/encounter start, release on clear; pool). Single biggest APK drop. Highest value.

**Phase 2 — Heroes → Remote per-selection (81 MB).** = **WO-282 (un-HOLD)**. `HeroAssetLoader`
seam already built; keep the starting hero local, stream alternates. Register the Heroes group
the loader already probes for.

**Phase 3 — Structures/Towers art → Remote (32 MB + Tripo).** Stream as the build catalog is
browsed/placed (`StructureFactory` load path).

**Phase 4 — VFX + Environment packs → Remote (the big scene-referenced packs).** Hovl VFX,
Lana, Polyperfect, Art. Harder: these are scene/prefab-referenced, so they must be made
addressable and scene refs converted to `AssetReference`. Biggest code surface.

**Phase 5 — Code migration + UX.** Migrate the ~439 `Resources.Load` call sites to async
addressable loaders (the populated `AddressablesGroupConfig` registry is the home for the typed
refs). Add: a **first-run download screen** (pull boot-essential remote bundles), background
streaming for the rest, **offline fallback/cache**, handle lifecycle (load-before-use, release,
no leaks). This is the risk-heavy part (async load timing).

---

## 4. PROJECTED SAVINGS (grounded, ranged — validate by doing Phase 1)

| Scope | Source moved remote | Est. thin APK | Streamed to CDN |
|---|---|---|---|
| **Phase 1 only** (Enemies) | ~504 MB | **~250-320 MB** | ~150-200 MB |
| **Phase 1-3** (Enemies+Heroes+Structures) | ~617 MB | **~180-260 MB** | ~200-280 MB |
| **All phases** (+ VFX/environment packs) | ~1.0 GB+ source | **~120-180 MB** | ~300-400 MB |

- These are estimates from disk-source sizes; the in-APK (compressed) drop tracks the 555 MB
  texture share. **The honest way to get a real number is to do Phase 1 and measure** — one
  folder move + one load-path change, then rebuild and read the APK size.
- **The bytes don't vanish — they move to a CDN** (first-run/on-demand download). The user's
  total download isn't smaller; the *install* is, and they only pull content they use.

---

## 5. HONEST CAVEATS + the hidden bonus

**Caveats:**
- **CDN cost:** egress at scale costs money. **Cloudflare R2 has free egress** → the right pick.
- **First-run friction:** a download screen on first launch (mitigate: keep boot-essentials local).
- **Offline:** streamed content must cache + fall back gracefully.
- **Effort/risk:** relocating ~600 MB-1 GB, migrating ~439 load sites, a build+upload pipeline,
  and async load-timing bugs. **This is a multi-week milestone, not a toggle** — which is why
  WO-282 was parked. Sequence it AFTER current polish + wallet (WO-766).

**The hidden bonus (why it's worth finishing beyond size):** a remote catalog lets you push
**new enemies, events, art, and balance WITHOUT a store resubmit.** For a live crypto game that
is a live-ops superpower — ship content between Play/dApp-store reviews. Arguably as valuable
as the install-size win.

**Cheaper interim (Play-specific):** an **AAB + Play Asset Delivery** gives device-split +
on-demand asset packs *without* the full remote re-architecture — a middle path that shrinks
the *Play* download now while the full thin-client is done later.

---

## 6. Reconciliation with existing WOs
- **WO-282** heroes Resources→Addressables (**HOLD**) = Phase 2. Un-HOLD when this proceeds.
- **WO-281** addressables/SBP build blocker = part of Phase 0.
- **WO-191** WebGL size optimization = related (WebGL benefits identically; remote bundles help most).
- **WO-767** tighten texture caps = an *additional* quality-tradeoff win, orthogonal to streaming.
- New master WO for this migration: **WO-768** (see WorkOrders/).

## 7. Recommendation
Not needed for **Seeker testing now** (sideload the 453 MB fat APK — fine). **Worth finishing
before Google-at-scale** for install conversion + the live-ops catalog. Start with **Phase 0 +
Phase 1 (Enemies)** to bank the biggest, cleanest win and get a REAL measured APK number before
committing to the full migration.
