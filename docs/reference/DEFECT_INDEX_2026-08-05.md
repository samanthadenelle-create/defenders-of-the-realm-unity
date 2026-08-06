# DEFECT INDEX — 2026-08-05 felt-test session

> ## ▶ CONTINUED IN `SESSION_INDEX_2026-08-06.md` (added 2026-08-06 — body unchanged, §15)
> This ledger covers `fe44ddc7` → `8fdb29a5` (the felt-test day: dungeon P0, wallet, catalog fallback
> drift). **Fourteen more commits landed the same evening/overnight** — the VFX loop-cap P0, the 183 pack
> dependencies in the tracked VFX prefabs, the Ranger/Mage unlock and their 31 dead talent nodes, the
> invisible-hero fallback, the structure height cadence, and the colourblind low-health tell. They are
> indexed in `SESSION_INDEX_2026-08-06.md`, which also carries **five further REFUTED beliefs** — including
> a direct correction to §6's assumption chain: **`ClampMinTouch` was checked and RULED OUT at three
> separate sites**, so measure the band before naming that guard.
> Live anchor: `../../CANON_GROUND_TRUTH_2026-08-06.md`.

> **Known dictionary** (SUNDAY_HOUSEKEEPING §2). Every row carries its **proving line** or
> **source citation** so any single fact is re-verifiable at a glance rather than re-derived.
> Built from session history, not from memory. Where a claim was later **refuted**, the
> refutation is recorded next to it — a wrong belief that cost time is itself a durable fact.
>
> Session shape: owner felt-tested on a **Seeker (Android, native 2670x1200)** while the CLI
> triaged live from `adb` screencaps + logcat. 22 tickets opened; 12 fixed and shipped in the
> first two waves; 9 of the 22 were found *while fixing the others*.

---

## 0. HOW TO READ THIS

| Column | Meaning |
|---|---|
| **PROVEN** | A captured line or a source read establishes it. Cite included. |
| **REFUTED** | Believed during the session, then disproven. The refuting evidence is named. |
| **OPEN** | Recorded, not yet resolved. |
| **RULING** | An owner decision. Implement it; do not re-litigate it. |

---

## 1. THE DEFECT CLASSES (reusable — these recur)

These are the patterns, not the instances. Each one bit more than once tonight.

### 1.1 Built and wired to nothing
The 2026-08-04 anchor already named this as the dominant shape. It held all night.
- `CollectorStackView` (437 lines) with zero `Attach` callers · `EliteVFXController` never attached ·
  `ISiegeLootTarget.PendingLoot` zero readers *(prior sessions)*
- **NEW tonight:** the `<queries>` block in `MobileWalletAdapter.androidlib/AndroidManifest.xml`
  never reaches the APK (§4.3); `WalletEndpoints.MwaChain()` has **zero callers** — dead code that
  looks like the cluster lever and is not.

### 1.2 Fraction-of-parent bands + symmetric touch-floor grow
`ElarionUiKit.ClampMinTouch` grows a sub-floor control **about its centre**, so a band authored as a
fraction of a parent that resolves under `MinTouchPx = 112` both closes its own gap and eats the band
above it. Shipped five times: WO-852, WO-868, WO-865, FOUND YOUR TOWN, and (different mechanism, same
family) the Echo card.
**Fix pattern:** fixed-pixel bands via the kit's `BuildButtonColumn`/`AddColumnButton`.

### 1.3 Content anchored to `chrome.content` instead of `chrome.layout.body`
The black plate **is** `layout.body`, whose floor the kit **raises at runtime** to reserve a close
band. Content on `chrome.content` uses panel fractions and does not track that moving floor.
**Instance:** Echo card caption at panel y 0.30-0.39 vs a resolved plate floor of 0.4305 / 0.4276 /
0.4071 — outside the plate at *every* resolution, not just the Seeker.

### 1.4 Fractions taken before the panel is resized
Reservations computed against a pre-extension panel, then the panel grown ~2x with none recomputed.
**Instance:** Victory screen — content got ~17% of a 907px panel, `BuildBody` scale **0.363**.

### 1.5 Comments lie
Verified instances this session alone:
| Claim in source | Reality | Cite |
|---|---|---|
| `_isTeleporting` "clamp/movement skips this frame" | `WarpTo` sets and clears it **within one synchronous call**, so `Update` never observes it true | `HeroLocomotion.cs:301`/`:338` |
| `BattleArena` "the hostile(activebattle) row carries no town-only widget — no …heart…" | The shipped `hud-areas.json` **does** carry `heartStatus`, `buildButton`, `hpPotionSlot` | `BattleArena.cs:519` |
| `ConsumableUseService` header + `consumables.json` `_schemaNotes`: mana effect "DEFERRED" | `ApplyMana` calls `hero?.RestoreManaOverTime`, which **exists and is implemented** | `ConsumableUseService.cs:219`/`:235`, `HeroAbilities.cs:438` |
| MagentaGuard recovery "demoted to Warn so it no longer trips F8" | A **companion `ProbeFail` two lines up** still fired at error level for the same offender | fixed in `449b16bb` |
| `Enemy.cs:912` — arena orcs hold `HeroCombatEngagement` | They never can; the arena supplies a non-null tether | `Enemy.cs:911-914` |

### 1.6 A fallback that has drifted from the thing it mirrors
`CatalogBootstrap.RegisterFallback` is the JSON-load-failure path. **All three rows had drifted.**
Full table in §5.

### 1.7 Green markers that cannot see the defect class they exist to catch
- **UI capture harness:** `RenderCanvasToPng` rewrites only `canvas.scaleFactor`, never `Screen.*`,
  while the kit computes zone geometry **at build time** from `Screen.*`. Every PNG shares ONE
  layout; the resolution in the filename is a **label, not a layout**. The Echo card was captured at
  two sizes, passed green all night, and was broken on device.
- **Dungeon settle guard:** `DungeonRealtimeSettleRegression` is a **source-lint** that greps
  `DungeonController.cs` for the strings `SettleEncounter`/`ExitToVillage`/`OnBattleEnded +=`. It
  proves the bridge *exists*, never that a fight can *reach* it. Passed green while the dungeon was
  unplayable.

---

## 2. THE P0 — DUNGEON UNPLAYABLE (ticket 14)

**PROVEN.** The dungeon had never been completable. Fixed in `219924ca`; the owner then entered,
fought and won — the first dungeon victory in the project's history.

**Root cause:** the hero staged into `BattleArena` as a **partial hero**.
`HeroControlEnsurer.Ensure` early-returns in non-village scenes (`IsVillageScene` at `:42-47` matches
Village/Castle/CastleHub/raid only), **and** `HeroHealth` was never attached by `Ensure` in *any*
scene — it comes from `HeroHealthBootstrap`, which polls for `HeroAbilities`, which a composed
dungeon Keeper deliberately does not carry (`GearLoadout.cs:875-880`). She could not damage the
enemy; `EnemyBrain` damages only through `HeroHealth`, so the enemy could not damage her. Mutual
null-target deadlock → the fight could never resolve → `battleLock` never released.

**Proving lines (device, 2026-08-05):**
```
15:25:37.019 [Flow:Hero] Ensure: no hero in non-village scene 'Dungeon_HealersCottage' — nothing to ensure (skipping).
15:25:37.220 [Flow:Dungeon] SeedHeroVitalsFromLiveHero: no live HeroHealth on 'Keeper'
15:25:56.x   [Flow:HudKit] attack fired but no PlayerAttackController in scene      (x5)
             [Flow:EnemyLoco] Idle_A x77   vs   [Flow:EnemyAggro] inRange=True x69
```
Aware, adjacent, idle — because there was nothing to hit.

**Why five prior fix waves missed it:** nine commits touched this seam
(`82e1f3a4` restore hero control · `c012d1f4` phantom arena/warp guard · `7794e18c`
abandoned-encounter teardown · `223256a6` defeat-exit freeze · `fb358585` arena double-mover) —
every one fixed a mover, a camera, a teardown or a settle bridge. **Not one attached the missing
components.** Not a regression; a never-addressed path.

**Verification line for future runs:**
`[Flow:HeroEnsure] combat components ensured on 'Keeper' … attack=ADDED health=ADDED loco=True`

---

## 3. WHAT THE FIRST-EVER DUNGEON VICTORY THEN EXPOSED

Winning ran `Resolve() → RestoreCavernMood()` and `Destroy(stage)` **inside a dungeon for the first
time**, surfacing three latent defects at once.

### 3.1 The screen goes black — PROVEN, and it is not a fade
**REFUTED first:** "the fade mask never lifts". `ScreenFader` is `sortingOrder = 10000`
(`ScreenFader.cs:58`) vs the HUD kit's 4000 (`HudAreasHost.cs:85`) — an opaque fader would bury the
HUD, and the HUD was visible.

**Actual cause — two global leaks from the arena into a scene it does not own:**
1. The stage prefab carries a **scene-wide Directional light**: `KeyLight`,
   `ForestClearingArena.prefab:1491`, `m_Type: 1`, intensity 1.05. It lit the whole dungeon for the
   fight and died with `Destroy(stage)` (`BattleArena.cs:2178`).
2. `ApplyCavernMood` (`BattleArena.cs:1142`) overwrites **global** `RenderSettings.ambientLight` to
   `(0.18,0.17,0.22)`. The dungeon's authored ambient is `(0.05,0.05,0.055)` at intensity 0.05
   (`Dungeon_HealersCottage.unity:23,26,27`) — a **~20x drop** when restored.

The dungeon is authored pitch-dark **by design** (`Lantern.cs:1-26`, `TorchWardenDress.cs:14-18`).
The lantern was working the whole time — it is the warm pool bottom-left in the capture.

### 3.2 The player cannot move — PROVEN, and off-mesh was a red herring
**REFUTED:** "off-mesh landing = immobility". In an **unbaked** scene (every dungeon)
`agent.isOnNavMesh == False` is the **normal, correct** state — the CharacterController is the mover
(`BattleArena.cs:1513-1519` already documents this).

**REFUTED:** "the dungeon repositions her, so suppress the warp". `SettleEncounter`'s victory branch
(`DungeonController.cs:1368-1372`) grants loot and **moves nothing** — "hero resumes in place" is
about the *run*, not the *pose*. Suppressing would have stranded her at `(5000,0,5000)`.

**Actual cause — `HeroLocomotion.Update` (`:1085-1152`):**
```csharp
if ((_agent == null || !_agent.isOnNavMesh) && !_isTeleporting) {
    p.x = Mathf.Clamp(p.x, -50f, 50f);
    p.z = Mathf.Clamp(p.z, -50f, 50f);
    transform.position = p;      // :1152 — UNCONDITIONAL, EVERY FRAME
}
```
`ArenaCentre = (5000,0,5000)` (`BattleArena.cs:81`) clamped to ±50 is **exactly `(50.00,0.00,50.00)`**
— the mystery position that appeared with no `WarpTo` line. The block also writes
`transform.position` every frame onto a live CharacterController, racing `DungeonHero.Update`.

**Gap in the first fix brief:** a CharacterController-only gate would NOT have fixed it —
`DungeonController.SetHeroCharacterController(false, "arena staged")` (`:1451`) deliberately disables
the CC for the whole fight, so the clamp fires anyway. The second gate is
`BattleArena.IsArenaPosition()` (`:93`), a public static already built for this question.

### 3.3 `return-home` goes nowhere — PROVEN, and it is a tag not a route
`PrimaryRoute` is documented as a FlowTrace tag (`EndStateVM.cs:68`), hard-coded `"return-home"` for
**every** arena win including dungeons (`:114-117`). There is no missing scene load — that path
exists only on **defeat** (`DungeonController.cs:1354-1364`). The tag sent triage hunting a
transition that was never designed.

---

## 4. WALLET — three layers, each hiding the next

### 4.1 Layer 1: the NRE — PROVEN, FIXED (`c457150d`)
`AddComponent<Web3>()` skips Unity's deserialization pass, so `Web3.solanaWalletAdapterOptions`
(`Web3.cs:88`, no initializer) is null and `LoginWalletAdapter` dereferences it on its **first
statement** (`Web3.cs:264`). A second null waited deeper —
`SolanaMobileWalletAdapter.cs:54`/`:68-70` — so fixing only the WebGL options would have moved the
NRE, not removed it.
**Before:** failed in **16.5 ms**, never reached the network. **After:** 3.5 s / 1.9 s — a real
round-trip.

### 4.2 Layer 2: authorization refused — PROVEN cause, identity verification
Jupiter returned MWA `code=-1` = `ERROR_AUTHORIZATION_FAILED`, over the wire:
`D/al: Responding with error for id=1 (code=-1, message=authorization request failed)`

We ship the SDK's **default identity** `https://solana.unity-sdk.gg/`
(`SolanaMobileWalletAdapter.cs:18-21`, constructed bare at `SolanaWalletProvider.cs:508`).
**That URL returns HTTP 404 for `/.well-known/assetlinks.json` — fetched and verified.**
The MWA spec prescribes exactly `ERROR_AUTHORIZATION_FAILED` when the calling package cannot be
verified against an `android_app` statement. The check is **structurally unpassable** as shipped —
it is magicblock's domain, not ours.

**Latency signature confirms it independently:**
| attempt | session established → error |
|---|---|
| 1 | **6.756 s** (DNS + TLS + fetch → 404) |
| 2 | 1.077 s |
| 4 | 2.560 s |
| 5 | 1.109 s |
A cached negative. **A human decline does not get faster with repetition.**

**REFUTED — "the user declined":** inferred from a touch 113-331 ms before each error. Owner
testimony ("didn't stay open, instantly closed") plus the latency collapse both disprove it.
**REFUTED — "devnet is the blocker":** a cluster refusal has its **own** code,
`ERROR_CHAIN_NOT_SUPPORTED = -7`. We got `-1`.
**REFUTED — "the orientation conflict killed the handshake":** the display genuinely rotated
(Jupiter's activity is portrait-locked; sheet logged `1200x2670` against our 2670x1200), **but the
activity survived** — `Displayed …+263ms`, rendering at 98-114 fps 2.3 s in, then a well-formed
JSON-RPC error over a still-open socket, and `session terminated` **after**. No relaunch, no destroy
pair. Real UX defect; **not** the failure mechanism.

### 4.3 Layer 3: `<queries>` never reaches the APK — PROVEN, BLOCKING
`Assets/Plugins/Android/MobileWalletAdapter.androidlib/AndroidManifest.xml:31-37` declares the
package-visibility block. The **packaged manifest contains zero occurrences of `queries`.**
**Cause:** the module ships a `build.gradle` applying `com.android.library`, and AGP's default
manifest path is `src/main/AndroidManifest.xml` — Unity's legacy folder-root `.androidlib` manifest
is silently ignored. (The sibling `FirebaseApp.androidlib` uses `project.properties`, the legacy
path, and merges fine.)
**Latent today** (implicit launching is not gated by package visibility, only *querying* is).
**Fatal the moment we target a package** — `startActivity` with `setPackage(<invisible>)` throws.

### 4.4 Settled facts
- **Seed Vault is not a separate integration.** Solana Mobile docs, verbatim: *"If you are building
  a mobile dApp, you should just use Mobile Wallet Adapter."* `com.solanamobile.wallet` **is** an
  MWA wallet fronting Seed Vault. The owner's ruling is implemented by *routing the association*,
  not by adopting another SDK.
- **Five wallets claim `solana-wallet://`** on the device; `pm resolve-activity` returns **Jupiter**,
  so the Seeker wallet was never offered and no chooser appeared.
- **Release signing SHA-256** (from `apksigner verify --print-certs`, Signer #1, CN=DeNelle Studios):
  `73:36:66:CE:4C:E2:C8:72:AB:65:30:EB:28:D6:DB:F1:E1:9D:E2:6D:88:ED:59:D1:B5:C0:20:9C:3D:A6:24:43`

---

## 5. CATALOG FALLBACK DRIFT — all three rows (ticket 18)

`CatalogBootstrap.RegisterFallback` vs `structures-catalog.json`. **PROVEN by field-by-field audit.**

| row | field | fallback was | catalog |
|---|---|---|---|
| `tower_ground_archer` | `visualPrefabPath` | `PatriciaLight/tower2` **(module DELETED 2026-06-09)** | `Structures/Tower_Castle_Round` |
| | `cost` / `maxLevel` / `upgradeCost` / `orientation` | **absent** → priced off legacy crystals, `maxLevel` defaults **1** = non-upgradeable | authored |
| `tower_wall_wizard` | `displayName` | `"Wizard Tower"` | **`"Ballista"`** |
| | `visualPrefabPath` | `PatriciaLight/tower2` | `Structures/WizardTower_1` |
| | `mustSitOn` + `requiresSupport` | `WallWalk` + `true` → **unplaceable without a wall-walk** | `Ground`, absent |
| | `orientation` | absent | `manual true / euler (-90,0,0)` → would ship **on its side** |
| `tower_arcane_spire` | `visualTexturePath` | **absent** → renders **pure white** | `Structures/ArcaneSpire_Albedo` |

**Guard added:** reflects `RegisterFallback`, invokes it against a cleared registry, deep-compares
**every public field** of the constructed objects against the catalog. A field added to `RepoProps`
tomorrow is covered the day it lands. Rides `BUILDECON_OK`, tagged `[fallback-parity]` — no new suite,
so the `REGRESSION_OK n/n` count is untouched.

---

## 6. OWNER RULINGS IN FORCE (from this session)

| # | Ruling | Note |
|---|---|---|
| R1 | **Archer tower = `heightMul 1.2`** (4.8 m, base 2.778 m = 49.9% of a house, stays 1x1) | Chosen over the literal 1.5x after the CLI showed 1.5 would make it **bigger** — the ruling had been formed against a stale 7 m that WO-764 already cut to 5 m |
| R2 | **Seeker wallet is the primary MWA target**, others only as fallback | |
| R3 | **Relax the landscape lock during the handshake, restore after** | **Not shipped** — the dossier proved orientation is not the failure. Held pending evidence it is needed |
| R4 | **Dungeon darkness is DESIGN.** Add a first-dungeon tutorial + a torch; "extremely minimal light till torch" | |
| R5 | **"If you cannot walk and navigate through the dungeon it's a fail"** | The hard pass/fail on the dungeon lane |
| R6 | Potion quick-slots: quantity badge showing **zero**, mana potion icon (not a crystal), zero-tap feedback naming both remedies | |
| R7 | Right rail: Echoes / Builders / Resources unified to **one collapsed chip style**, expand on tap | Open |
| R8 | Hub spawn moved **in front of** the tree, not inside it | Shipped + proven |
| R9 | Every ticket carries its **screenshot** from here on | Standing |

---

## 7. COMMITS (session, `fe44ddc7` → HEAD)

| commit | lane |
|---|---|
| `c457150d` | wallet connect NRE — Web3 options graph |
| `13c0e728` | FOUND YOUR TOWN — fixed-pixel column |
| `449b16bb` | MagentaGuard — vendor particle trail slots |
| `14c9dc98` | WO-908 gear icons + banner bump |
| `219924ca` | **dungeon P0 softlock** + exit arch off the spawn |
| `56f1139c` | wave clock stands down during tutorial |
| `aa321ba4` | potion slots + Heart bar scene gate |
| `ee2a2855` | Echo card plate anchoring |
| `c374bd44` | Victory screen geometry |
| `bfe9f0c3` | hub spawn out of the tree |
| `0ac59581` | archer tower 1.2x (R1) |
| `dcc9675f`, `5d0382e3` | Android version stamps |

**Proven on device:** dungeon softlock (owner won a fight) · hub spawn (4 trace lines) · Heart bar
gate (logged its decision) · wallet NRE (now fails later, differently, at real authorization).

---

## 8. METHOD NOTES THAT COST TIME TONIGHT

1. **A post-hoc `adb logcat -d` destroyed evidence.** The `main` ring defaults to **256 KiB**; this
   game's `[Flow:Offset]` emits ~5,000 lines with a stack trace each and wraps it in seconds. 56 s of
   boot output was evicted and read as "the feature never ran". Grow to 64 MiB or stream from before
   launch. **Before concluding "it never ran", prove the capture window covers the event** — check
   for a known-unconditional trace line in the same window.
   → memory `logcat-ring-buffer-destroys-evidence`
2. **`adb input tap` drives uGUI; `adb input swipe` does NOT drive the virtual d-pad.** Menus and
   buttons are automatable; locomotion is not. That is why the dungeon needed the owner's hands and
   why the AutoPilot probe exists.
3. **Agents reading the file caught defects in the CLI's briefs six times** — each would have shipped:
   the Heart gate would have hidden the bar permanently and never re-fired; the Echo caption would
   have ellipsized to "…kee…" at 1080p; the dungeon fix would have left the hero invulnerable; the
   wave fix would have frozen the HUD countdown; the potion toast pointed at a building that cannot
   craft potions; the CC-only clamp gate would have missed the arena case entirely.
   **Pattern: reliable at "what is broken" from captured data; unreliable at "how to fix it" when
   reasoning from an RCA summary instead of the file.**

---

*Built from session history 2026-08-05. Frozen ledger — banner, never rewrite (§15).*
