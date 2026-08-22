> # ⚠ SUPERSEDED 2026-08-21 by `CANON_GROUND_TRUTH_2026-08-21.md`.
> Frozen point-in-time record - do NOT rewrite the body, do NOT read as current state.

# CANON GROUND TRUTH — 2026-08-18

**Supersedes `CANON_GROUND_TRUTH_2026-08-16.md`** (now bannered/frozen). Per CLAUDE.md §15 this is
the single live anchor: every other doc loses to it on conflict.

**Branch:** `wip/village2-and-f8-tickets`. **No HEAD sha, no "N commits ahead", no suite count and no
next-free WO number is recorded in this file as a live fact** — read them off `git status` /
`git rev-list origin/..HEAD`, off the gate MARKER files under `Builds/`, and off the
`CLI_LANES_WO_NUMBERS.md` banner respectively. Every one of those was found stale in a prior sweep.
Where a number appears below it is dated and labelled as a point-in-time reading.

**Written overnight 2026-08-18 while the CLI seat runs a fix-and-verify loop.** The owner's standing
instruction for the night: *"Check in everything and notate the record as well."* Items marked
**IN FLIGHT** were still moving at the time of writing — re-verify them at source before acting.

> **⚠ RECENCY DOES NOT CERTIFY A DOC.** Newest-wins is a tiebreaker between sources, never evidence
> that any one of them is right. Every fact below was verified at source on 2026-08-18, or captured
> from the owner's device tonight — the file and line are named so the next reader re-verifies
> instead of inheriting.

---

## 0. THE HEADLINE — a correction pass corrected the WRONG FILE, twice

Commit `f995c4706` ("Tripo models arrive UPRIGHT — axis conversion baked at import, ten -90 offsets
retired") set `bakeAxisConversion: 1` on ten structure FBXs and zeroed ten rows in
`Assets/OffsetForge/offsets.json`.

**For STRUCTURES those offset rows are INERT.** Nothing resolves structure ids through
`AttachmentOffsetRegistry` — that registry is keyed by **hero/enemy attachment mesh ids**. Zeroing a
structure's row in `offsets.json` changes nothing a structure ever reads.

**The two channels that are actually LIVE for structure orientation:**

| # | Channel | Where | When it applies |
|---|---|---|---|
| **(a)** | `entry.orientation` in `structures-catalog.json` | applied at `Assets/_Modules/Village/Catalog/StructureFactory.cs:151-158` | only when `orientation.manual == true` (auto-baked rows are advisory and deliberately NOT applied) |
| **(b)** | hardcoded `pitchDeg` on the `Swap` rows | `Assets/_Modules/Village/HubStructureVisualInjector.cs` (~:81-91) | hub-scene visual swaps |

Both still carried the legacy `-90`. So the **import bake AND the legacy correction were BOTH
applying** — the models lay down.

**Fixed tonight — channel (a)** for `forge`, `workshop`, `jeweler`, `barracks`, `tower_ballista`
(their orientation rows now read `[0,0,0]`, `manual: true`). Catalog `version` went **22 → 23**
(verified in `Assets/Resources/Data/Canonical/structures-catalog.json` on 2026-08-18).

**Channel (b) is IN FLIGHT as of writing — NOT done.** At the time this anchor was written every
`Swap` row in `HubStructureVisualInjector.cs` still carried `pitchDeg = -90f`. Verify at source
before assuming either state.

### ⛔ THE HIGHEST-VALUE LINE IN THIS DOCUMENT — eight `-90`s are CORRECT AND MUST STAY

These eight catalog rows still carry a live `orientation` of `[-90, 0, 0]` with `manual: true`, and
their FBX metas read **`bakeAxisConversion: 0`** — i.e. they were never baked, so the `-90` is the
only thing standing them up:

```
pet-house · market · arcane-tower · collector_farm
collector_lumbermill · lumberyard · foundry · silo
```

**A future "tidy up the remaining -90s" pass would lay all eight on their side** — including
`collector_lumbermill`, which is the **FTUE's first building**. The rule is not "-90 is legacy"; the
rule is **"-90 is legacy IFF that FBX's meta says `bakeAxisConversion: 1`."** Check the meta, per
asset, every time.

### The transferable lesson

The pass fixed **the file it could see** rather than **the file that is read**, and **no gate could
tell** — headless gates cannot see orientation, a point `f995c4706`'s own commit message conceded
("sits correctly in the town is a felt claim").

The instrument for exactly this **already exists in-repo and was not used**:
`Assets/Editor/WoodenWatchtowerBuilder.cs:277` — `private const float UprightAspectMin = 1.2f;`, an
aspect measurement used at `:988` and `:1245`. Observed values: **1.70–1.92 upright vs 0.52–0.59
lying down** — a wide, unambiguous margin. (A second copy of the same idea lives at
`Assets/Editor/CraftPixPeopleBuilder.cs:237`, tuned to `1.02f` for people.) Generalising this into a
gate that measures every placed structure is filed as **PROD-008**.

---

## 1. Realm Store — PROD-003 placed upright and facing the plaza

Placed via an **owner-authored** rotation on `RealmStorePlacer`'s `opts.LocalRotation`:

```csharp
Quaternion.Euler(0, 180, 90)
```

Owner's words, verbatim and canon:
> "store is on its side needs rot 90 euler 0,0,90f"

then

> "after you stand it up, rotate it 180 degrees as its facing the wall"

**⚠ It is deliberately NOT in Offset Forge.** `Assets/Editor/TripoAxisBake.cs:147-154` regex-rewrites
the `rot` of any `axisBaked` row back toward `0.0`, so a hand-dialled value parked there is silently
destroyed by the next auto pass. *(Precision note, verified at source 2026-08-18: the regex as
written matches `"rot":{"x": -90` specifically — it zeroes the **x** component of a baked row. The
principle stands — Offset Forge is auto-rewritten and is not a safe home for a hand-dialled
structure rotation — but do not paraphrase it as "any value there is deleted".)* The same method
also stamps `"axisBaked": true` alongside, and hard-errors if the baked count and the cleared count
disagree, with the reason spelled out in the log: *"a baked model that keeps its -90 lands UPSIDE
DOWN, and a cleared offset on an unbaked model lays it FLAT."*

**Measured after the re-run (2026-08-18, point-in-time):** scale **5.49**, boundsSize
**(5.12, 4.00, 6.35)**, collider size **(3.400, 4.000, 5.503)**, height exactly **4.00 m**;
`REALM_STORE_REACHABLE_OK nearest walkable 0.08m`. A **falsifiable prediction made BEFORE the run
matched to three decimals** — that is the standard, not a nicety.

---

## 2. Sign-in gate — PROD-006, a forever-bug caught before it shipped

`LoginPanelController`'s continue-without-login gate read **ONLY**
`FirebaseAuthService.Instance.IsSignedIn`.

But this build's **identity law** (same file, ~:556-557) states that an email/Google success **binds
NOTHING** — only the **wallet** path re-keys the save. Therefore **a wallet-only player is never
Firebase-signed-in**, and the SIGN IN panel would have presented **on every launch, forever**.

**Proven not a race** (device capture, 2026-08-18): the wallet published `connected=True` at
**20:21:38.597**; the gate made its decision at **20:21:43.478** — 4.9 s later. The gate had the
answer and ignored it.

**Fix:** a pure decision seam
`LoginPanelController.ShouldContinueWithoutLogin(walletConnected, walletIdentityBound, firebaseSignedIn)`
plus `GameStateService.HasAttestedWalletIdentity` (a persisted key + device attestation, true
**synchronously at boot**). **No timing constant was added** — no sleep, no retry window, no "wait
1 s for the wallet". Pinned by `Assets/Editor/Regression/LoginGateRegression.cs` under the
`[login-gate]` tag, which asserts both the seam's signature and that
`HasAttestedWalletIdentity` is honest at boot (guest ⇒ false).

**MWA session sealing WORKS** — device capture shows `auto-resume: sealed session present`,
`MWA session found for CHKK...sfkC`, a silent reauthorize of ~3.3 s, and
`auto-resume SUCCEEDED - connected at boot with no player action`. Commit `6e9f86cc3` is doing its
job; **only the gate was ignoring it.** Do not go re-debugging MWA on the strength of the sign-in
symptom.

---

## 3. CDN / content delivery — measured on the 2026-08-18 build

Point-in-time readings from the 2026-08-18 build:

- Exactly **two remote Addressables groups**: `Structure_Art` (**19.71 MiB**) and `Enemy_Art`
  (**64.45 MiB**) → first-run remote total **~84.26 MiB**.
- `m_UseAssetBundleCache: 1` on both → the download cost is **one-time PER BUILD**. Content-hashed
  bundle names mean **each new APK re-downloads everything**.
- Both groups are `PackTogether` with **ZERO labels authored** — 78 enemy + 35 structure entries all
  carry `m_SerializedLabels: []`. There is no partial-fetch axis: it is **all-or-nothing**.
- **`Assets/_Modules/Core/Addressables/StructureAssetLoader.cs` (~:99-100) calls
  `handle.WaitForCompletion()` — a SYNCHRONOUS MAIN-THREAD FREEZE, not async pop-in.** Its **five
  sibling loaders do the same**: `AudioAssetLoader.cs`, `EnemyAssetLoader.cs`, `HeroAssetLoader.cs`,
  `HeroTextureLoader.cs`, `VfxAssetLoader.cs` (all under
  `Assets/_Modules/Core/Addressables/`, verified 2026-08-18).
- **No Addressables prewarm exists anywhere in the project.**
- **The larger, unseen stall is FTUE beat 7/8 `founding_defend`**, which resolves the **64.45 MiB
  enemy bundle** through that blocking call **as combat opens** — the worst possible moment.

Filed as **PROD-009 / PROD-010**.

### ⛔ Why "keep the CDN" (owner ruling) was RIGHT — do not re-litigate it

- `m_DisableCatalogUpdateOnStart: 0` means **already-installed APKs adopt the new remote catalog at
  launch**. Re-pointing an asset to a **local** path would make existing players resolve a path that
  **does not exist inside their installed build** — **invisible buildings for everyone already
  playing**, with **no client change** and no way to see it from here.
- **Re-grouping also rehashes bundles**, forcing a **full re-download for every existing player**.

This is a **live game on the Solana dApp Store**. The blast radius of an Addressables reorganisation
lands on installed devices, not on this tree.

---

## 4. R2 shipping discipline — PROD-011

> **⚠ UPDATED 2026-08-20 — the "no gate exists" line below is now CLOSED.** Push + verify live once, in
> `tools\r2-ship.ps1` (`R2_PARITY_OK`), wired into the ship chains; the binding rule is **CLAUDE.md §16**.
> The body below is kept as the 08-18 record — read §16 for what to do today.

**Every APK build REQUIRES a fresh push:**

```
python tools/r2_sync.py --push ServerData
```

- **`ServerData`, NOT `ServerData/Android`.** The relpath keying flattens the latter to the bucket
  root. ⚠ The docstring at **`tools/r2_sync.py:22` still documents the WRONG form**
  (`--push ServerData/Android`) — verified still wrong on 2026-08-18. Do not copy it.
- **Order: push AFTER the build, BEFORE the device install.**
- `--check` proves **credentials only** — it never proves that *your* catalog's bundles are present
  in the bucket.
- `--push` **skips by SIZE, not hash**, and `catalog_*.hash` is **always exactly 32 bytes** — so a
  changed catalog hash file can be skipped as "same size".
- **No gate exists for an APK-vs-bucket mismatch.** Commit `16e22dba3` conceded outright: *"NO GATE
  COULD HAVE CAUGHT THIS."*
- **Tonight a build shipped with an enemy bundle that had never been uploaded.** It was caught by
  hand. Treat the manual push as a mandatory step of the build ritual, not an optimisation.

---

## 5. Monetization stays OFF — and exactly why

Verified at source 2026-08-18:

- `FeatureFlags.RealmStorePurchase` — `defaultOn: false` (pinned by
  `Assets/Editor/Regression/ImpulsePackRegression.cs`).
- The Buy CTA renders **"Coming soon"**; `Purchase()` **refuses at entry**.
- `WalletService.Pay` / `PayFlat` **refuse** for the stub.
- Network is **Devnet**, with a **hard mainnet block**.
- `Web3.Wallet` is **never assigned anywhere**, so `SendPayment` **cannot construct a transfer** even
  if every gate above were flipped.

⚠ Note the deliberate asymmetry, pinned by
`Assets/Editor/Regression/PromoRedeemEntryRegression.cs:292`: the **store ENTRY is not gated** on
`RealmStorePurchase` — that flag gates **BUYING**. Redeeming spends nothing, so it stays reachable.
Do not "fix" the entry by gating it.

**Two HARD blockers before real money can move:**

1. **No server-authoritative economy.** `api/game/save.js:404-421` is an **explicit, built-to-flip
   seam** — as it stands a client can set its own crystal balance up to `MAX_RESOURCE`.
2. **No payment-verification endpoint.** `@solana/web3.js` is not even a dependency.

**Flipping the flag on devnet would grant real pack contents for worthless tokens** and emit
`purchase_completed` events **indistinguishable from real revenue** — poisoning the analytics that
the pricing decision will later be made from.

---

## 6. Security fix landed tonight — grant-bearing endpoints moved to the wallet rail

**Status at time of writing: UNCOMMITTED in the tree. NOT DEPLOYED — promotion is the owner's call.**

`api/promo/redeem.js` and `api/referral/claim.js` now require the **signed wallet rail**
(`X-Wallet` + `X-Nonce` + `X-Signature`) via a new **`authenticateGranting()`** in
`api/_lib/wallet-auth.js`, which states the rule in-file: *"⛔ ROUTES THAT GRANT VALUE CALL
`authenticateGranting()`, NOT `authenticate()`."* (`api/_lib/wallet-auth.js:31`.)

**The hole it closes:** the guest rail was **self-asserted bearer trust** — `verifyGuest`
regex-checks `^guest-local-[0-9a-f]{64}$` and echoes it back. The id is **minted by the client** and
carries no signature (`api/DEPLOY.md:48`), so an attacker could **mint unlimited identities** to burn
a promo's `max_redemptions` and bypass `per_player_limit` wholesale.

⚠ **BREAKING — say this out loud when it deploys: guest players lose promo redemption and referral
claiming.** That is the intended trade, not an oversight.

**`TEST10`** (10 crystals, active, uncapped, unbound) was seeded **unconditionally** by
`api/schema.sql`. It is now **opt-in** behind `SET dotr.seed_test_codes = 'on';` — a plain paste of
`schema.sql` leaves it unseeded (`api/DB_SETUP.md:82-83`, `:209-223`).

---

## 7. Regression baseline — known reds

**Known-red baseline: 4.** *(Never restate a suite COUNT from a doc — read it off the marker files
under `Builds/`. The three entry points emit DISTINCT markers: `REGRESSION_OK` /
`CHECKIN_SUITE_OK` / `SESSION_GUARDS_OK`.)*

| Red | Note |
|---|---|
| `CaravanStatusChip` | UI-OBSIDIAN |
| `vfx-self-contained` | |
| `vfx-null-slot` | **awaiting owner ruling** — retag or repair |
| `WANDERER BUBBLE x4` | needs a dungeon **re-bake in an ISOLATED WORKTREE** (shared-tree bakes NUL-corrupt the `.unity`) |

**Two NEW reds appeared tonight and were FIXED AT SOURCE, not baselined:**

1. `[fallback-parity] tower_ballista` — code/JSON drift.
2. A **hollow pass** in the newly written `LoginGateRegression` — a test that asserted nothing. Worth
   naming: a brand-new guard's first job is to be **proven red**, and this one wasn't until it was
   checked.

---

## 8. Open owner rulings — DO NOT ANSWER THESE

Parked for the owner. An agent that decides one of these on its own is out of lane.

- **PROD-012** — is internet required? (bears directly on §3: 84 MiB of remote content on first run.)
- **Pack pricing** — five SKUs sit **above the $5 early-access cap**, up to **$49.99**.
- **Mainnet** — when, and on what proof.
- **The Realm Store vendor NPC body.**
- **Storefront height: 4 m vs the 1.25 landmark tier.**
- **`vfx-null-slot`** — retag or repair.

---

## 9. What this anchor does NOT restate

By design, so it cannot rot:

- **HEAD sha / commits-ahead** → `git status`, `git rev-list origin/..HEAD`.
- **Gate results and suite counts** → the MARKER files under `Builds/`; check the marker AND the log's
  mtime, never the exit code (batchmode exits 0 on refusals).
- **Next free WO number** → the `CLI_LANES_WO_NUMBERS.md` banner, SOLE authority, two disjoint blocks.
- **Board state** → `BOARD.html`, regenerated by `python tools/board_build.py`; the repo is the
  source, the board is a derived view.
- **Save schema version** → `SaveSchema.CurrentVersion` at
  `Assets/_Modules/Core/State/SaveSchema.cs:41`. (Was **v38** as of the 08-16 anchor; read the const.)

Everything in the 08-16 anchor that this file does not contradict **still stands** — read it as
history, not as guidance.
