> ## > LIVE ANCHOR = `CANON_GROUND_TRUTH_2026-09-03.md` - read it FIRST (re-stamped 2026-09-03 night, IN THE SAME CHANGE AS THE ANCHOR)
>
> *(The 09-02 pointer this replaces, and the stale-re-stamp warning it carried, are kept below as history - the warning is still the rule.)*
>
> ▶ LIVE ANCHOR = `CANON_GROUND_TRUTH_2026-09-02.md` — read it FIRST (re-stamped here 2026-09-03)
>
> ⚠ **RE-STAMPED LATE, AND THAT IS THE THIRD TIME.** This line read `CANON_GROUND_TRUTH_2026-08-23.md`
> while a 09-02 anchor sat on disk — exactly the failure the note at the foot of this block warns about
> ("this pointer has twice sat days stale behind a newer anchor"). It is now three times. **Re-stamp it
> in the SAME change as any new anchor, not in the next session's audit.** The 08-23 block below stays
> because its subject (the pay path) is still true; it is simply no longer the anchor.
>
> ### 2026-09-03 session - the DAY half. It NOW HAS a dated anchor: `CANON_GROUND_TRUTH_2026-09-03.md`.
> *(This block was written when it did not. The EVENING half is appended at the end of it.)*
> - ⛔ **THE WORLD CLOCK WAS MEASURED AT `timeScale=0.28` IN OPEN TOWN** — 28% speed, no battle, no
>   modal, `inputSuppressed=False`. Every timer, cooldown, animation and the wave clock were wrong
>   together and nothing on screen said so. WO-1353 gives `Time.timeScale` ONE owner with paired
>   acquire/release — owner ruling: *"anything that steps into time slow needs to step to time return"*.
>   ⭐ **The trace named it and the CLI read past it**, theorising a frame-budget story off `fps=40` and
>   asserting it twice before checking the clock. `[Flow:HeroOwner]` prints `timeScale=` on EVERY line.
>   **Read the captured data before forming the theory** (CLAUDE.md §12; memory `never-inference-fix`).
> - ⛔ **THE dApp STORE REJECTED THE APP** — privacy + terms 404. Root: `echoes-of-elarion` was never a
>   stale copy of the game, it was the **marketing and legal site** with its own project and
>   `cleanUrls`, and a repo-root deploy replaced it with the Unity WebGL build. **WO-1316's parity
>   premise was INVERTED** — that divergence was correct and deliberate, and enforcing it destroyed the
>   compliance pages. Both URLs now verified HTTP 200. ⚠ **A gate can be worse than the drift it
>   polices when its premise is wrong.**
> - ⭐ **`publishing/config.yaml` names the listing URLs** (`privacy_policy_url` / `license_url` ->
>   `echoes-of-elarion.vercel.app`). That answers the question WO-1316 was blocked on.
> - ⛔ **THE PRIVACY POLICY IS CORRECT AND NEEDS NO REWRITE.** Canon's claim that
>   `PRIVACY_POLICY.md:87-89` falsely states "no ads" is **STALE AND FALSE** — lines 85-96 are a
>   complete, accurate Advertising section naming Unity LevelPlay, mediation partners, user-initiated
>   rewarded ads only and consent handling, mirrored in `site/privacy.html`. **Do not hold a
>   resubmission for a legal rewrite that is not needed.**
> - **Hero decimation to 50k is REVERTED and PARKED** (owner: *"then we can play with them"*), not
>   cancelled — the Mage deformed in motion. A T-pose render, a bone count, byte-identical `.meta` and
>   a clean `COMPILE_GATE_OK` are ALL compatible with a broken character. **`COMPILE_GATE_OK` does not
>   cover rig errors — grep `Rig Error` separately.** Backups + SHA256 at
>   `Backups/mesh-decimation-2026-09-03/`.
> - **The VFX Caster can author a tag SHE DID NOT MAKE.** `VfxCasterWindow.TagSelected` reads the key
>   from a never-cleared TextField and the prefab from the live selection — two persistent fields never
>   captured together — then overwrites an existing key with **no diff, no warning, no confirmation**.
>   Four bad tags in one hour, including one attributed to her for a choice she did not make. Three keys
>   are HELD pending her retag.
> - **`Assets/Resources/Structures` DOES NOT EXIST** — the R2 migration deleted it. A `texPath` row
>   pointing there resolves null and a Tripo FBX renders **pure WHITE with no error on screen**. And
>   hub structures are **BAKED TWINS** re-skinned by `HubStructureVisualInjector`; they do NOT route
>   through `StructureFactory`, so searching for that class's log lines will always come up empty
>   for them.
> - **`"Main_Castle_Overworld".StartsWith("MainCastle")` is FALSE** (underscore). That one character
>   left `GroundZFightFixer` dead in the hub since the OuterWorld rename. **Allow-lists fail CLOSED and
>   SILENTLY on a rename;** the gate is now an exclusion list.
>
> ### 2026-09-03 EVENING - the production candidate, appended to the block above
> - **`2026.09.04.354315` IS THE PRODUCTION CANDIDATE and it is on her Seeker.** Version read off
>   `ProjectSettings/ProjectSettings.asset:148,177`. Gates, markers on fresh logs (never an exit code):
>   `COMPILE_GATE_OK` `Builds/compile-gate.log` 20:10; `REGRESSION_OK 358/358 suites -- 358 green, 0 red,
>   0 skipped` `Builds/regression.log` 20:13; `R2_PUSH_OK` + `R2_PARITY_OK
>   targets=Android,StandaloneWindows64,WebGL objects=266` 20:21. ⭐ The push log names
>   `Android/catalog_2026.09.04.354315.bin` - which is the §16 proof that the CDN carries THIS build's
>   content-hashed bundles, not a previous build's. Branch `feat/synty-art-retheme`, pushed.
> - ⛔ **SAVE DATA LOSS, AND NO TICKET EXISTS.** `[Flow:BaseLayout] Enter build mode CENSUS: live
>   PlacedStructure(s) in scene=9, loader.Loaded=9, persisted BaseLayout=17` - **eight structures gone**,
>   and the trace names it as an earlier vanish. Emitter
>   `Assets/_Modules/Village/BuildMode/BuildModeController.cs:513-523`. ⚠ Tonight's raw capture is not on
>   disk (unverified from the repo); what IS in the repo is the same shape at **0 of 8**, twice, in
>   `logs/device/*.log` on 2026-08-19/20 - so it is not new and it was never worked. Destruction does not
>   explain it: `Destructible.NotifyBroken` DROPS the persisted record, so a real destruction LOWERS
>   `persisted`. **This is the biggest known defect in the game.**
> - ⛔ **`HudActionBarModel.MaxVisibleFaces` IS 4, NOT 6 - CLAUDE.md §7 was wrong AGAIN.**
>   `Assets/_Modules/Core/HudModel/HudActionBarModel.cs:121`; `HudLabelFitRegression` Case 0
>   (`:266-269`) FAILS if it is not 4, and `SessionShapeRegression:232` pins it too. **The constant and
>   the suites are the authority.** ⚠ That line already carried a 2026-08-26 correction banner about the
>   face count and drifted again within eight days - the PATTERN is the finding, not the number.
> - ⛔ **`publishing/SUBMIT_CHECKLIST.md` Gate A is STALE.** It was filled in tonight (`f1104a5fd`)
>   against APK `2026.09.04.354266`; the shipped build is `354315`. The APK sha256, versionName,
>   versionCode, source commit and path all describe the wrong file. **Marked, not re-derived** - the
>   lead re-records against whichever APK ships.
> - **The 180s hold ceiling still applies to WALLET SIGNING.** Flagged not changed in WO-1360
>   (`3e6ae4274`): signing is user-paced, and if the ceiling fires the world thaws under a live payment -
>   a route into "paid but not granted". **Owner call.** The mechanism already exists:
>   `HoldKind { BoundedBeat, PlayerOwned }` + `AcquirePlayerOwned`, with the ceiling kept as the DEFAULT.
> - **The signing certificate cannot be proven to match the live release** - the live cert's sha256 was
>   never captured (`publishing/SUBMIT_CHECKLIST.md:101`). Cheap close: install over the live store build;
>   Android refuses a mismatched key, so a successful in-place update IS the proof.
> - **The VFX Caster tool is still unfixed.** Her four bad tags were retagged and bound (`7437942c6`), but
>   `VfxCasterWindow.TagSelected` (`Assets/Editor/VfxCasterWindow.cs:1223`) still reads a stale key from a
>   never-cleared field and overwrites with no confirmation. It will do it again.
> - **Two silent server-side 400s, unticketed** (`9d0294c5e`): `/api/entitlements` rejects every guest
>   (53 of 66) - ⛔ do NOT fix by widening `isProvenValueId`; and `/api/catalog/collection` fails 11 of 11,
>   all build-carousel collections.
> - ⛔ **CLAUDE.md §11B (`f1104a5fd`) - never guess, prove it; follow the documented procedure.** Forged
>   twice in one evening, both times a REAL measurement supporting a conclusion it did not support.
>
> ---
>
> *(Superseded as the anchor, kept because its subject is still true:)*
> ## ▶ was: LIVE ANCHOR = `CANON_GROUND_TRUTH_2026-08-23.md` (stamped 2026-08-23)
>
> ⛔ **THE PAY PATH IS ACTIVATED (owner explicit, 2026-08-23, WO-1159).** The single most repeated
> line in this repo's canon — *"the game is published but nobody has ever bought anything"* — is now
> **FALSE**. It appears below in the 08-21 section, in `SESSION_CANON_LOADER.md`, in
> `docs/HANDOVER.md` and in the North Star bullet on this very page. Expect to keep meeting it; the
> **08-23 anchor wins**. Consequence that bites: an economy REMOVAL is **no longer a clean purge**,
> and the "nobody to grandfather or compensate" licence is **WITHDRAWN**.
>
> The `Latest (2026-08-23)` section immediately below is current. **Older dated `Latest (...)` sections
> are history — where one disagrees with a newer section or with the anchor, the newer wins.**
> The 08-18, 08-16 and 08-08 anchors are bannered SUPERSEDED. The 08-08 one is additionally
> **INVERTED** on its two headline sections (the machine block is resolved; the dungeon-stair hunt is
> closed) — do not act on it.
>
> *(This pointer has twice sat days stale behind a newer anchor. Re-stamp it in the SAME change as any
> new anchor.)*
>
> Per CLAUDE.md §15 THIS file is LIVING — edited in place, never snapshotted. The `CANON_GROUND_TRUTH_*`
> anchors are the dated snapshots.
> ## ⛔ EVERY UNIT OF REVENUE SO FAR IS THE OWNER'S OWN (2026-08-25, owner-stated)
>
> Both rails now WORK, and neither has been paid by a stranger:
> - **Purchases:** the 1 SKR canary and both 391 SKR ladder purchases were made by the owner from her
>   own wallet, and the 783 SKR was withdrawn again under 2-of-3 multisig. Money circulated; it did
>   not arrive.
> - **Ads:** the LevelPlay/ironSource dashboard reads **$0.04 over 14 days** - owner verbatim,
>   *"that was only me from testing"*. Her own impressions on her own device.
>
> ⭐ **What that DOES prove is the infrastructure**, and it is not nothing: quote -> wallet -> vault ->
> multisig withdrawal on the money rail, and SDK -> placement -> mediation -> attribution -> a real
> dollar figure on the ad rail. Every link is real and observed.
>
> ⛔ **What it does NOT prove is demand. NOBODY OUTSIDE THIS HOUSEHOLD HAS PAID THIS GAME ANYTHING,
> in money or attention.** Write that plainly wherever revenue is described. This canon spent weeks
> saying "the game is published" while quietly meaning "and has never taken money"; the successor
> mistake is reading "purchases live + $0.04 revenue" as "the game is earning". It is not. It is
> READY to earn, which is a different and still-good fact.

> ## ⛔ OWNER RULING 2026-09-02 EVENING — THE ANDROID APK IS THE PRIORITY. PI IS PARKED.
>
> Owner, verbatim: *"we have spent most of today triaging and trying to get Pi to work, but have
> made almost no progress"* / *"so we need to shift back to the apk. thats the real vision so that
> needs to be the priority"*.
>
> **What that means, operationally:**
> - The **Android APK / Seeker** path is the active lane. Player-felt gameplay defects and polish
>   outrank every Pi/WebGL ticket.
> - **PROD-022** (Pi Browser crash loop) drops from P0-active to a **quiet read-only triage lane** —
>   it keeps collecting evidence, it does not consume the pipeline. The Lane A `[PiLifecycle]`
>   instrumentation is deployed to production (17:30) and gathers data on its own.
> - The Pi/WebGL ticket cluster is **PARKED, not cancelled**. Pi resumes on the owner's word.
>
> **The evidence behind the ruling, so nobody re-litigates it:** of the 27 commits made on 2026-09-02,
> exactly ONE is gameplay (`d45608080`, the wave-clear toast) — and it landed nine minutes AFTER that
> morning's APK was built, so it is not even in the artifact. Everything else was Pi, WebGL, or docs
> about Pi. A day of triage produced no player-facing progress.
>
> ⭐ **The APK on disk is content-proven and was wrongly believed unshippable.** The 05:52 build
> stamped `R2_PARITY_FAILED — DO NOT INSTALL OR DISTRIBUTE` in `Builds/overnight-apk-status.txt`, but
> the 16:30 `r2-ship` run cured it: `Builds/r2-parity.log` verifies `catalog_2026.09.02.352005` — the
> exact catalog that APK requests — plus every enemy and structure bundle it names, and reads
> `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=261`. **The stale FAILED verdict in
> the status file is the trap** (the duplicated-state class, CLAUDE.md §16). Judge the APK by
> `r2-parity.log`, not by a status stamp a later push made obsolete.

> ## ⛔ STANDING RULE 2026-09-02 — A BALANCE VALUE IS A TUNABLE, NOT A CONSTANT. DEFAULT ANSWER: YES.
>
> Owner, verbatim: *"be smart, dont make it need a code change, make it tweakable from a db call"* —
> followed by *"i have been screaming this for months."*
>
> **Read that second sentence as the actual defect.** The idea was never rejected; it kept being
> agreed to in conversation and never written to disk, which is the failure mode this canon already
> named on 08-23: *a ruling recorded but not applied is indistinguishable from no ruling.* It is now
> written down, so no seat needs to be told again.
>
> **THE RULE.** When you are about to hardcode a number that exists to be TUNED — a drain ratio, a
> cooldown, a cost, a rate, a threshold, a duration, a drop chance — you register it on the remote
> tunables rail instead. You do not ask whether it is worth it; the answer is yes by default. You ask
> for a ruling only in the reverse direction: if you believe a value must NOT be tunable, say why.
>
> **THE RAIL ALREADY EXISTS — DO NOT BUILD A SECOND ONE.** It shipped 2026-09-02 for PROD-022.
> Contract and worked example: `docs/PROD022_TUNABLE_FLAGS.md`. Registry/defaults
> `Assets/_Modules/Core/Ops/RemoteTunables.cs` · transport/poll/cache `RemoteTunablesService.cs` ·
> server allowlist `TUNABLE_KEYS` in `api/_lib/tunables.js` · operator surface
> `tools/command-centre.ps1 -Tunables` (and the phone, via `POST /api/admin/ops`) · oracle
> `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs`. **All four sources change in the
> SAME commit**; the `[tunable-defaults]` oracle goes red naming which two disagree.
>
> **THE INVARIANT THAT OUTRANKS THE FEATURE:** *no row, no network, no server, no parse => TODAY'S
> BEHAVIOUR, EXACTLY.* The registered default must equal the value the constant would have had. The
> remote read is an OVERRIDE, never a dependency, and never blocks or delays boot. An empty
> `client_tunables` table is the correct resting state and is what ships.
>
> **WHY THIS IS WORTH REAL EFFORT, in the owner's own economics:** a WebGL rebuild costs ~30 minutes
> and an APK ~10. A knob reaches a running client in ~40 seconds (10s edge cache + 30s poll), and a
> boot-time knob on the next launch. Every balance value left hardcoded converts one of her felt-tests
> into a half-hour round trip — and she is the ONLY person who can judge feel, so that cost lands
> entirely on the one resource the project cannot buy more of.
>
> ⚠ Scope honestly: this is for BALANCE and PRESENTATION levers. It is NOT for anything
> server-authoritative (prices, entitlements, grants) — those stay on the quote/verify rail, where the
> SERVER is the authority and a client-side override would be an exploit.

> ## ⛔ OWNER RULING 2026-09-04 — THE 180s CEILING STAYS ON WALLET SIGNING. THE CALL IS CLOSED.
>
> Owner, verbatim: ***"180 stays on wallet"***.
>
> `PackStore.cs:3075` keeps `WorldHold.Acquire(ReasonPurchase)` at `StuckHoldSeconds`. The purchase
> hold is **NOT** split into signing-vs-settlement and **NOT** converted to `AcquirePlayerOwned`.
> **No code changed — the ruling was to leave it alone.**
>
> ⚠ **Read this as an ACCEPTED exposure, not an absent one.** WO-1360 §4's analysis stands: a
> foreground-but-slow signing leg can still reach the ceiling, and if it fires the world thaws under
> a live payment. `NotifyApplicationPause` (WO-1260) excludes OS-suspended time, which covers the
> common backgrounded case; the residual is the foreground-slow case, and the owner has priced it.
>
> ⛔ **Do not re-open this from WO-1360 §4's own recommendation.** That recommendation is persuasive
> and it was READ and ruled against — a later seat rediscovering the argument has found nothing new.
> Only an **observed** occurrence in a capture is new evidence, and that is a new ticket citing the
> captured line. Recorded in WO-1360 §4 and `CANON_GROUND_TRUTH_2026-09-03.md` §3 item 2.

> ## ⛔ OWNER RULING 2026-09-04 — NOTHING CRYPTO GOES IN THE GOOGLE PLAY AAB.
>
> Owner, verbatim: ***"Nothing Crypto goes in the aab build"***, alongside ***"build a fresh aab at
> 354315"***.
>
> ⚠ **THIS IS NOT A BUILD FLAG, AND THE CURRENT GATE CANNOT SEE THE VIOLATION.** Recon in
> `WorkOrders/WORK_ORDER_1362_google_play_aab_program.md` §2 establishes the shape:
> - **Tier 1 (assembly exclusion) is genuinely clean** — `DeNelle.Wallet.asmdef:22` and
>   `DeNelle.Web3.asmdef:17` carry `"!GOOGLE_PLAY"`, and the merged dex proves no MWA/Solana.
> - **Tier 2 (runtime `#if` guards inside shipping assemblies) does NOT remove strings.**
>   `SkrShowcasePanel.cs:68` guards `Open()`, but the SKR copy at `:77`, `:154`, `:172` sits OUTSIDE
>   the guard and compiles into `global-metadata.dat` regardless. **A runtime guard does not remove a
>   literal from the binary** — only compiling it out or excluding the assembly does.
> - **Arena has no gating at all.** `grep -c GOOGLE_PLAY` = **0** in `ArenaMode.cs`, `ArenaVM.cs`,
>   `GameState.cs`; `DeNelle.Village.asmdef:35` has `"defineConstraints": []`. The **SKR wager loop
>   compiles into and runs in a Play build.**
> - **The gate is blind on purpose.** `GooglePlayPackagingGate.cs:167-176` routes only
>   `.json/.txt/.html/.xml/.uxml` + `Data/Canonical/` to the strict token list; everything else,
>   including `global-metadata.dat`, gets `OpaqueExecutableTokens` (`:36-45`) which **deliberately
>   drops** `solana`, `skr`, `usdc`, `jupiter`, `blockchain`, `crypto`, `web3` to avoid matching
>   `System.Security.Cryptography`. Consequence, proven: `Builds/ui-reskin-final-google-play-aab-v2.log`
>   emitted **`PLAY_ARTIFACT_CLEAN_OK`** on an artifact carrying the USDC mint address and four SKR
>   marketing sentences.
>
> **So "nothing crypto" = convert tier 2 from runtime guards to compile-out, AND make the gate able
> to see it, AND deal with Arena.**
>
> ### ⛔ OWNER RULING 2026-09-04 — ARENA SHIPS ON BOTH CHANNELS. ONE CODE PATH, CURRENCY PER CHANNEL.
> Ruled in three steps inside one exchange, each superseding the last — **read all three, the first
> two are stale on their own**:
> 1. ***"the arena will go to the google play store, just needs to remove crypto"*** — ⛔ supersedes an
>    earlier CUT-ARENA ruling from the same session. Arena is **not** compiled out of Play.
> 2. The Play build wagers **Crystals**.
> 3. ***"both to use same logic just different curency for wagers"*** — ⛔ **ONE Arena code path.** The
>    wager currency is INJECTED per channel, never branched on: **Play = Crystals
>    (`GameState.Resources.Crystals`), Seeker/dApp = SKR as it behaves today.** A forked Arena is
>    forbidden — that is the duplicated-state defect of §2/§5/§16 and WO-1137 all over again.
>
> ⭐ **The seam already exists — extend it, never greenfield:** `CurrencySkinResolver.cs` (`#if
> GOOGLE_PLAY` at `:96/:239/:267`), `CurrencySkin.cs:130`, `PaymentChannelResolver.cs:18/:21/:27`.
>
> ⭐ **WO-1362's "1-2 WEEKS, AND IT IS DESIGN WORK" RESTED ON A FALSE PREMISE.** It assumed Arena
> wagers real SKR. Measured at source: `ArenaWalletService.cs:2` declares itself a **"CLIENT-SIDE SKR
> WAGER STUB"**, `:19` **"NOT real on-chain custody"**, `:38` PlayerPrefs key
> `dotr-arena-skr-balance`, `:41` seeded **500**. The Seeker wager is a number in PlayerPrefs and
> always has been.
>
> ⛔ **THREE TRAPS, all recorded in `WorkOrders/WORK_ORDER_1366_arena_wager_currency_per_channel.md`:**
> - The free **500-seed stub balance must NOT become 500 real Crystals** — that grants premium
>   currency to everyone who ever opened Arena.
> - The Seeker stub must **NOT be promoted to real on-chain SKR**. "Different currency" is not
>   "make it real money"; that is a money-path change needing its own ruling.
> - `GameState.AetherCrystals` is **DEPRECATED** (folded at save v18, `GameState.cs:54-58`) and
>   nothing writes it. Use `Resources.Crystals`, through the existing spend seam — never inline.
>
> ⛔ **AND THE TIERS BECOME TUNABLES.** 50/100/200 and the 2x purse are hardcoded
> (`ArenaCatalog.cs:87/:101/:114/:48`) and are about to price a REAL currency, so the 2026-09-02
> standing rule binds directly: **a balance value is a TUNABLE, default answer YES.** They were
> authored against a free 500-seed stub, so they carry **no information** about correct Crystal
> pricing — register them with today's values as defaults and hand her the knob; do not re-pick them.

> ### ⛔ OWNER RULING 2026-09-04 — NO THROWAWAY AAB. FIX FIRST, BUILD ONCE.
> A fresh AAB was NOT cut from the current tree. The Play artifact is built once, after the purge and
> the size work, and it is expected to be shippable when it is built. **Do not cut a Play AAB "just
> to measure" without asking.**

# KEY FACTS — the living fact sheet (update IN PLACE, never snapshot)

> **Rule (owner directive 2026-07-12):** this file is LIVING — when a fact changes, edit the line
> in the same commit as the change and re-stamp its date. Facts here are code-verified, never
> assumed. If a doc contradicts a line here, the doc is stale. Dated anchors
> (`CANON_GROUND_TRUTH_*`) remain the session snapshots; THIS file is the always-current card.

## ⭐ NORTH STAR — the state we are building toward
- **The product:** "Echoes of Elarion" (chapter) in the "Defenders of the Realm" series — "Echoes of
  a Forgotten Civilization" (retired tagline "Hold the last light" noted in canon-strings.json).
  **V1 = ONE controllable Knight ("Grom")** in an overworld with isolated
  real-time BattleArena combat; **the player builds their own city** (player-defined map pivot
  07-11: Build → place/move/rotate functional structures). *(The "build mode IS the demo" framing is
  RETIRED — see the platform line.)*
- **The platform:** **mobile web in Pi Browser**. **Pi Hackathon: WON (owner, 2026-07-17)** — the
  July-31 deadline + "build mode IS the demo" framing is RETIRED/STALE: there is NO upcoming demo, the
  roadmap is OPEN for the next phase (owner sets the new north star). Any doc still leaning on the
  hackathon deadline is STALE. Desktop is the dev proxy, never the verdict.
- **The bar:** the **ten-year-old test** — "wow, this feels good" on a phone, or it isn't done.
  Feel-first; headless proves binding, only the owner's hands prove feel.
- **The player never sees a failure** — errors are captured loudly in the db, invisible on screen.
- **The economy direction:** V1 ships ZERO crypto; soft currency client-owned now, flips
  server-authoritative (auth scaffolding already built) when currency carries real value; SKR is a
  later, separate arc. Monetization = rewarded-ad income paths, never a wall.
  **⛔ THE APP IS PUBLISHED, BUT THE PAY PATH HAS NEVER BEEN ACTIVATED (owner, 2026-08-21):
  nobody has ever bought anything.** "Published on a store" and "taking money" are DIFFERENT facts,
  and only the first has ever been true here (`FeatureFlags.RealmStorePurchase` is
  `defaultOn:false`, verified at `Assets/_Modules/Core/FeatureFlags.cs`; the mainnet block in
  `SolanaWalletProvider.SendPayment` is unlifted). So an economy/currency REMOVAL is a **clean
  purge**, not a balance-preserving migration — there is nobody to grandfather or compensate. Still
  read-migrate a removed save field so existing dev/test saves LOAD (ordinary defensive
  deserialisation, not value preservation). ⚠ This does NOT license flipping the payment flags.
- **The architecture:** HP B2B — bounded contexts, presentation never touches objects, the One
  Model (entries + capabilities), data-only content ("data only always"), pooled by default.
  Deep-dives: `docs/NORTH_STAR.md` (vision/GTM) · `docs/COMBAT_PIVOT_NORTHSTAR.md` (combat) ·
  `docs/ARCHITECTURE_NORTH_STAR.md` (does the foundation grow into the dream).
- **The operating dream:** the owner plays and rules; agents build in parallel lanes; every bug is
  a captured line; every system self-reports; the fleet + web bots verify before she ever has to.

## Latest (2026-08-23) — GO LIVE: the pay path is activated

*Anchor: `CANON_GROUND_TRUTH_2026-08-23.md`. Read it before this summary.*

- **⛔ THE HEADLINE, AND IT INVERTS THE 08-21 SECTION BELOW: the game now takes real money.**
  `FeatureFlags.RealmStorePurchase` is `defaultOn: **true**`; `WalletService.DefaultNetwork` is
  **Mainnet**; the unconditional mainnet refusal in `SolanaWalletProvider.SendPayment` is replaced
  by the ruled condition. Owner explicit: *"we test everything and make live"*, *"by owner
  explicitly"*. **WO-1159.** Scope ruled: **the full authored ladder, $1.99–$49.99** — the old $5
  early-access cap is **superseded**.
- **The four-step order in `FeatureFlags.cs` was followed, and the order is the whole safety
  argument.** Mainnet decision + lift the block · `DefaultNetwork` off Devnet (`6802e2292`) · a real
  signed transaction SETTLES (the 1-SKR mainnet canaries, owner: *"canary 2 success recorded"*) ·
  **THEN** the flag. Flipping the flag first only ever produces a Buy button in front of free goods.
- **⛔ THE MATCHED PAIR — the one invariant to carry forward.** `RealmStorePurchase = true` is safe
  ONLY while `DefaultNetwork = Mainnet`. On Devnet the tokens are free test tokens and the purchase
  chain COMPLETES: real packs granted for worthless SKR, `purchase_completed` indistinguishable from
  real revenue. `MonetizationActivationRegression` now pins **both**, so moving either one alone
  turns the suite red. Move them together or not at all.
- **⛔ THE CANARIES DO NOT TEST THE QUOTE.** Both answer `pinned: true` with **no quote row and no
  rate** — their amount is a protocol constant. A canary purchase proves the transfer rail and
  proves NOTHING about the WO-1158 quote path. **Verifying "the quote matches" needs a real ladder SKU.**
- **The provider guard carries NO SKU allowlist, on purpose.** Authority over what is sellable and at
  what amount is the SERVER quote (`quotableSkus` in `api/_lib/purchase-catalog.js`); the guard
  asserts only SKR-rail + a positive quoted amount. A second sellable-SKU list on the client would be
  the next "one fact written twice" bug (§2 WO numbers, §5 the assembly table, §16 the R2 push,
  WO-1137's 3-of-28 fallback catalog).
- **⚠ ONE PROMPT IS ACTUALLY TWO ON THE FIRST BUY.** WO-1157's session is minted **lazily on the
  first authed call** (`BackendRequestSigner.cs:230`), not at connect: first purchase of a session =
  session mint + transfer = **2 prompts**; every one after, within 15 min = **1**. That is 3 → 2 → 1,
  not 3 → 1. Minting at connect would make it one throughout — small, contained, NOT done.
- **⭐ CLOSED 2026-08-24 — the revenue vault is 2-of-3.** 2-of-3, timeLock 0, RE-VERIFIED ON CHAIN 2026-08-24 (`node tools/treasury-verify.mjs 9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj --multisig BcHLoNCsnGD6oegywkP19PALKMQYoFeQWTvmPLmp22no` -> "multisig is 2-of-3, timeLock 0 - production-shaped"). Vault sound throughout
  (off-curve Squads PDA `9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj`, SKR ATA present, official
  mint, **decimals 6 read from chain**), linkage proven from multisig. **No multisig blocker remains
  between "tested" and "public sales".**
  ⚠ **This bullet said 1-of-1 until 2026-08-24 and was STALE** — the owner had already raised it. It
  was restated in **eight** files, so it read as corroborated fact and nobody re-derived it. **Read the
  threshold with `tools/treasury-verify.mjs --multisig`, never from this line** (without `--multisig`
  the tool proves the vault but reads NO threshold at all).
- **⛔ 2026-08-24 — FOUR FINISHED-BUT-UNWIRED MECHANISMS FOUND IN ONE DAY, all on the money path.**
  `WalletService.Disconnect` · `CurrencySkinResolver.PublishWalletDisconnected` ·
  `WalletConnectDialog.SetWalletService` · `PackStore.SetWalletService`. Every one complete,
  correct and **called by nothing**. ⚠ The shared failure mode is that NOTHING FAILS LOUDLY: a null
  that merely renders a fallback string ("Price unavailable", "Wallet identity bound") looks exactly
  like a feature working in a degraded state. **When a feature looks half-dead, grep for callers of
  its entry point before reading its logic.**
- **⛔ NO WALLET SAVE HAD EVER BEEN WRITTEN** (fixed 2026-08-24). `player_data` held 21 rows, ALL
  `guest-local-*`. The raw-body guard predated WO-1157's session rail and rejected before
  `authenticate()` ran, so session-authed saves were refused for bytes they never needed. Invisible
  in game because the guest id is device-derived — the town persisted under the wrong key while the
  identity purchases bind to had nothing behind it. Pinned by `test/auth.rawbody.session.test.js`.
- **⚠ THE STORE LOSES A RACE TO THE WALLET, and "check on open" cannot fix it alone.** Device trace:
  the shelf drew at 11:33:48, the connect completed at 11:34:19 — **31 seconds later**. Association
  alone is ~2.8s and AUTO-RESUME fires at boot, so the player reaches the store first. PackStore now
  subscribes to `CurrencySkinResolver.WalletConnectionChanged` **and** checks at open; both halves
  are required (a view built after the connect never sees the event; one built before it needs the
  event).
- **⚠ AUTO-RESUME DOES NOT GO THROUGH `ConnectAsync`.** `TryAutoResumeAsync` → `ConnectForLoginAsync`
  is the SHARED path (auto-resume + login surface). Anything that must happen on every connect —
  the session handshake, for one — belongs THERE, not on the corner-button route. Getting this wrong
  means returning players silently skip it, which is the majority case.
- **Gates, off fresh logs:** `COMPILE_GATE_OK` (0 `error CS`) · **`REGRESSION_OK 270/270 suites`** ·
  backend **37/37**. The 08-21 two-red asset gap (245/247) is CLOSED. `FeatureFlags.Siege` is ON and
  proven (WO-1139, 08-22).
- **LESSON — re-point a pin, never soften it.** Two source pins asserted `defaultOn: false`; the
  ruling moved so both were **re-pointed, neither deleted nor weakened**, and the replacement is
  STRICTER (one value → the matched pair). `MonetizationActivationRegression`'s success string also
  had to be corrected — it claimed *"both public flags remain OFF"*, which after the flip would have
  been a **false success string** (the WO-1138 hollow-pass class, through a door nobody watched).
  **Proven red-then-green:** reverting the flag drove exactly 2 suites RED naming the reason;
  restoring returned 270/270. A money-path pin never seen red is not evidence of anything.

## Latest (2026-08-21) — siege cadence, the Night Market, and the pay-path correction

> ⚠ **SUPERSEDED 2026-08-23 on its headline.** The "PAY PATH HAS NEVER BEEN ACTIVATED" correction
> below was true when written and is now FALSE (see the 08-23 section above). The rest of this
> section stands.

*Anchor: `CANON_GROUND_TRUTH_2026-08-21.md` (file:line citations and the full ruling table live there).*

- **⛔ THE CORRECTION THAT CHANGES HOW YOU PRICE RISK: the game is PUBLISHED on the Solana dApp
  Store, but the PAY PATH HAS NEVER BEEN ACTIVATED — nobody has ever bought anything.** See the
  North Star economy bullet above for what that licenses (clean purges) and what it does NOT
  (flipping the payment flags). Canon states the "live game" half loudly and has never stated the
  other half; every risk estimate that assumed paying players was wrong.
- **Branch `wip/village2-and-f8-tickets`, NOTHING PUSHED.** Save schema **UNCHANGED** by tonight's
  wave — read it off `SaveSchema.CurrentVersion` (`Assets/_Modules/Core/State/SaveSchema.cs`),
  never off a doc. Commits-ahead: `git rev-list origin/<branch>..HEAD`.
- **Gate: `COMPILE_GATE_OK` fresh; `REGRESSION_OK` is ABSENT and that is EXPECTED tonight.**
  `DataRegression` ends 2 short, and **both failures are ticketed ASSET gaps that no code change can
  close** — **WO-1135** (wall tier materials were never tracked; `Assets/Resources/Walls/Materials/`
  does not exist) and **WO-1136** (`staff_A` is geometrically symmetrical, relGap 0, so no sheathe
  orientation is derivable). Read the counts off the newest `Builds/reg-*.log`, never from here.
- **Build: Seeker APK under `Builds/Android/`, with `R2_PARITY_OK` on a fresh `Builds/r2-parity.log`**
  — content is proven hosted, so no capsule enemies (CLAUDE.md §16).
- **Shipped (13 commits):** Night Market store redesign (WO-1050) · PvE siege cadence + the persisted
  Defense Report (**WO-1026 DONE**) · per-camp raid cooldown + difficulty-scaled attrition (WO-728) ·
  battle pass season track + monthly cards (WO-1053) · chest drops read by SILHOUETTE (WO-1132) ·
  convex Finish-Now curve + rescale parity (WO-1129) · per-mesh sheathed-weapon seating · village
  cosmetic seam + armorer instrumentation · realm map pins, dungeon status, offline accrual trust ·
  enemy art pipeline + a new wall-material oracle.
- **STILL OFF:** `FeatureFlags.Siege` (**OFF until WO-1139 lands the ruled stakes** — the cadence
  would otherwise open sieges that resolve and report but TAKE NOTHING) ·
  `FeatureFlags.RealmStorePurchase` OFF, mainnet block unlifted. No cosmetic or SKR rows are authored
  in the battle pass, and a regression FAILS THE BUILD if either is authored before its gate opens.
- **Owner rulings 2026-08-21** (values live in the anchor's table — do not re-copy them):
  per-difficulty raid cooldown; per-difficulty attrition windows; **sub-linear** reward escalation;
  a ladder that TERMINATES in clears and then **PLATEAUS — the camps REMAIN repeatable**; loss
  stakes = **theft ALLOWED** on banked wood/food/iron with a floor, **crystals NEVER stealable**,
  offline sieges included; WO-874 WIRE ruling STANDS; WO-1126 purge glimmer + retire
  `BattlePassManager`; WO-887 unblocked by the owner's own VFX tags. **WO-838 CLOSED** (felt-verified:
  raids render correctly, not white).
  - ⛔ The ladder terminus deliberately **DIVERGES from `TribeManager`'s vanishing camps** — copy the
    shape of a terminating ladder, NEVER the disappearance. A camp that vanishes deletes the loop.
  - ⚠ The stakes ruling **reversed TWICE inside one exchange**; the third is live. WO-1026 records
    all three with the superseded block struck through — read it there before implementing WO-1139.
- **THE LESSON OF THE NIGHT — gates that report success without proving anything, found in TWO
  separate suites in one run.** A missing dependency did `note + return`, and notes feed the SUCCESS
  string, so a SKIP READ AS A PASS. Only one of six was caught by the existing ratchet; the other
  five escaped because its detection window is **four lines**, i.e. its coverage depends on code
  FORMATTING (**WO-1138**). A gate that reports success without proving it does not merely fail to
  catch a bug — it **actively asserts the bug is absent**, and work proceeds on that assertion. That
  is strictly worse than no gate. Related: **WO-1137** (a fallback catalog covering 3 of 28 rows,
  drifted four times, would hand the player a silent 3-row different game).
- **OWED:** owner felt-test of tonight's APK, then WO-1139 (stakes) · WO-1126 (glimmer purge +
  `BattlePassManager` retirement) · WO-874 (wire elite VFX) · WO-887 (map the 5 tagged surface
  impacts) · WO-1133 (inventory redesign — half of it is removal) · WO-1134 (endgame loop, fully
  ruled). Still owner-owed: 823 first-raid softness · 1029/PROD-012 backend + online-required ·
  R5/R6 buy button and season pass.

## Latest (2026-08-18) — the overnight loop: orientation, the sign-in forever-bug, CDN + R2 discipline

*Anchor: `CANON_GROUND_TRUTH_2026-08-18.md` (file:line citations live there).*

- **⛔ STRUCTURE ORIENTATION — `Assets/OffsetForge/offsets.json` IS INERT FOR STRUCTURES.** Nothing
  resolves structure ids through `AttachmentOffsetRegistry` (it is keyed by **hero/enemy attachment
  mesh ids**). `f995c4706` baked ten FBXs and zeroed ten offset rows — a no-op for the town. **The
  LIVE channels are:** (a) `entry.orientation` in `structures-catalog.json`, applied at
  `Assets/_Modules/Village/Catalog/StructureFactory.cs:151-158` **only when `manual == true`**
  (auto-baked rows are advisory and deliberately not applied); (b) hardcoded `pitchDeg` on the `Swap`
  rows in `Assets/_Modules/Village/HubStructureVisualInjector.cs` (~:81-91) for hub-scene swaps.
  Both carried the legacy `-90`, so bake AND correction both applied = models lying down.
- **`structures-catalog.json` version 22 → 23** (2026-08-18) — orientation zeroed for `forge`,
  `workshop`, `jeweler`, `barracks`, `tower_ballista`. **Channel (b) was IN FLIGHT** when written.
- **⛔ EIGHT `-90`s ARE CORRECT AND MUST STAY:** `pet-house`, `market`, `arcane-tower`,
  `collector_farm`, `collector_lumbermill`, `lumberyard`, `foundry`, `silo`. Their FBX metas read
  `bakeAxisConversion: 0`. **A "tidy up the -90s" pass breaks all eight**, incl.
  `collector_lumbermill`, **the FTUE's first building**. The rule: **`-90` is legacy IFF that FBX's
  meta says `bakeAxisConversion: 1`** — check the meta, per asset, every time.
- **Headless gates CANNOT see orientation** (`f995c4706`'s own message: "sits correctly in the town is
  a felt claim"). The instrument exists and was unused —
  `Assets/Editor/WoodenWatchtowerBuilder.cs:277` `UprightAspectMin = 1.2f` (used at `:988`, `:1245`;
  observed 1.70–1.92 upright vs 0.52–0.59 lying down). Filed **PROD-008**.
- **Realm Store (PROD-003)** upright + facing the plaza via owner-authored
  `Quaternion.Euler(0, 180, 90)` on `RealmStorePlacer`'s `opts.LocalRotation`. Owner verbatim:
  *"store is on its side needs rot 90 euler 0,0,90f"* → *"after you stand it up, rotate it 180
  degrees as its facing the wall"*. ⚠ **Deliberately NOT in Offset Forge** —
  `Assets/Editor/TripoAxisBake.cs:147-154` auto-rewrites the `rot` of `axisBaked` rows toward `0.0`,
  so a hand-dialled value parked there is destroyed by the next pass. Measured: scale 5.49,
  boundsSize (5.12, 4.00, 6.35), collider (3.400, 4.000, 5.503), height exactly 4.00 m,
  `REALM_STORE_REACHABLE_OK nearest walkable 0.08m`.
- **⛔ SIGN-IN GATE (PROD-006) — a wallet-only player would have seen SIGN IN on EVERY LAUNCH,
  FOREVER.** `LoginPanelController`'s gate read ONLY `FirebaseAuthService.Instance.IsSignedIn`, but
  this build's identity law (same file, ~:556-557) is that **email/Google success binds NOTHING —
  only the wallet path re-keys the save.** Not a race: wallet published `connected=True` at
  20:21:38.597, the gate decided at 20:21:43.478. Fixed with a pure
  `LoginPanelController.ShouldContinueWithoutLogin(walletConnected, walletIdentityBound,
  firebaseSignedIn)` + `GameStateService.HasAttestedWalletIdentity` (persisted key + device
  attestation, **true synchronously at boot**). **No timing constant added.** Pinned by
  `Assets/Editor/Regression/LoginGateRegression.cs` (`[login-gate]`).
- **MWA session sealing WORKS** — `auto-resume: sealed session present`, `MWA session found for
  CHKK...sfkC`, silent reauthorize ~3.3 s, `auto-resume SUCCEEDED - connected at boot with no player
  action`. `6e9f86cc3` is doing its job; **only the gate ignored it** — do not re-debug MWA from the
  sign-in symptom.
- **CDN, measured on the 2026-08-18 build:** exactly two remote Addressables groups —
  `Structure_Art` **19.71 MiB** + `Enemy_Art` **64.45 MiB** = **~84.26 MiB first run**.
  `m_UseAssetBundleCache: 1` on both → one-time **PER BUILD** (content-hashed names mean each new APK
  re-downloads). Both `PackTogether` with **ZERO labels authored** (78 enemy + 35 structure entries,
  all `m_SerializedLabels: []`) → **all-or-nothing**, no partial-fetch axis.
- **`Assets/_Modules/Core/Addressables/StructureAssetLoader.cs` (~:99-100) uses synchronous
  `WaitForCompletion` — a MAIN-THREAD FREEZE, not async pop-in** — and so do its **five siblings**
  (`AudioAssetLoader`, `EnemyAssetLoader`, `HeroAssetLoader`, `HeroTextureLoader`, `VfxAssetLoader`).
  **No Addressables prewarm exists anywhere in the project.** Worst stall = **FTUE beat 7/8
  `founding_defend`**, which resolves the 64.45 MiB enemy bundle through that blocking call **as
  combat opens**. Filed **PROD-009/010**.
- **⛔ WHY "KEEP THE CDN" (owner ruling) WAS RIGHT:** `m_DisableCatalogUpdateOnStart: 0` → already-
  installed APKs adopt the new remote catalog at launch. Re-pointing an asset local would make
  existing players resolve a path that **does not exist inside their installed build** — **invisible
  buildings for everyone already playing**, with no client change. Re-grouping rehashes bundles and
  forces a full re-download for every existing player.
- **R2 shipping (PROD-011):** every APK build REQUIRES a fresh
  `python tools/r2_sync.py --push ServerData` — **`ServerData`, NOT `ServerData/Android`** (relpath
  keying flattens the latter to the bucket root). ⚠ **The docstring at `tools/r2_sync.py:22` still
  documents the wrong form** — do not copy it. Push **AFTER the build, BEFORE the device install**.
  `--check` proves credentials only, never that your catalog's bundles are present; `--push` skips by
  **SIZE not hash** and `catalog_*.hash` is always exactly 32 bytes. **No gate exists for an
  APK-vs-bucket mismatch** — `16e22dba3`: *"NO GATE COULD HAVE CAUGHT THIS."* Tonight a build shipped
  with an enemy bundle that had never been uploaded; caught by hand.
- **Monetization stays OFF** (verified at source): `FeatureFlags.RealmStorePurchase` `defaultOn:
  false`; Buy CTA renders "Coming soon"; `Purchase()` refuses at entry; `WalletService.Pay`/`PayFlat`
  refuse for the stub; Devnet with a hard mainnet block; `Web3.Wallet` is **never assigned anywhere**
  so `SendPayment` cannot construct a transfer. ⚠ Deliberate asymmetry pinned by
  `Assets/Editor/Regression/PromoRedeemEntryRegression.cs:292` — the store **ENTRY is NOT gated** on
  that flag (it gates BUYING; redeeming spends nothing). Do not "fix" the entry by gating it.
  **Two HARD blockers before real money:** no server-authoritative economy
  (`api/game/save.js:404-421` is an explicit built-to-flip seam — a client can set its own crystal
  balance up to `MAX_RESOURCE`) and no payment-verification endpoint (`@solana/web3.js` is not even a
  dependency). Flipping the flag on devnet would grant real pack contents for worthless tokens and
  emit `purchase_completed` events **indistinguishable from real revenue**.
- **Security fix, UNCOMMITTED at time of writing / NOT DEPLOYED (promotion is the owner's call):**
  `api/promo/redeem.js` + `api/referral/claim.js` now require the signed wallet rail
  (`X-Wallet`/`X-Nonce`/`X-Signature`) via a new **`authenticateGranting()`** in
  `api/_lib/wallet-auth.js` (`:31` — *"⛔ ROUTES THAT GRANT VALUE CALL authenticateGranting(), NOT
  authenticate()"*). The guest rail was **self-asserted bearer trust** — `verifyGuest` regex-checks
  `^guest-local-[0-9a-f]{64}$` and echoes it back; the id is client-minted and unsigned
  (`api/DEPLOY.md:48`), so an attacker could mint unlimited identities to burn `max_redemptions` and
  bypass `per_player_limit`. ⚠ **BREAKING: guest players lose promo redemption and referral
  claiming.** `TEST10` (10 crystals, active, uncapped, unbound), previously seeded unconditionally by
  `api/schema.sql`, is now opt-in behind `SET dotr.seed_test_codes = 'on';`
  (`api/DB_SETUP.md:82-83`, `:209-223`).
- **Known-red regression baseline: 4** — `CaravanStatusChip` (UI-OBSIDIAN), `vfx-self-contained`,
  `vfx-null-slot` (awaiting owner ruling), `WANDERER BUBBLE x4` (needs a dungeon **re-bake in an
  isolated worktree**). **Two NEW reds tonight were FIXED AT SOURCE, not baselined:**
  `[fallback-parity] tower_ballista` code/JSON drift, and a **hollow pass** in the newly written
  `LoginGateRegression`. *(Never restate a suite COUNT from a doc — read the `Builds/` markers.)*
- **Open owner rulings — do NOT answer these:** PROD-012 is-internet-required; pack pricing (five SKUs
  above the $5 early-access cap, up to $49.99); mainnet; the Realm Store vendor NPC body; storefront
  height 4 m vs the 1.25 landmark tier; `vfx-null-slot` retag-or-repair.
- **IN FLIGHT when written:** the hub-injector orientation lane (channel (b)); the gear seating lane —
  a prop measured `worldBounds=(0,0,0)` and a **`parent-scale compensate` firing every frame**.

## Latest (2026-08-15 late) — Grok-stack audit + gate-red fix wave (this seat)
- **The 10-commit pushed stack `fba0b1079..0e4690036` (WO-896 talent tree, WO-986 CoC footprints, portals,
  caravan, tutorial one-guide lock) was audited: it had been PUSHED UNGATED** — the last regression run
  (17:49, RED 158/163) predated the final three commits and no gate ran over HEAD. Re-gated at HEAD:
  `COMPILE_GATE_OK` · `REGRESSION_FAIL 3 (160/163)` · `UI_CAPTURE_OK 68`/`FIDELITY_OK 47` (all 16
  `UI_GEOMETRY_FAIL` = the known WO-941 RumorBoard/RealmMap baseline — nothing new). The 17:49
  talent wire-or-hide + WO-986 `[wiring]` reds HAD been reconciled by the stack's own later commits.
- **Three reds fixed (this seat, `Builds/data-regression-redfix.log` → 163/164):** (1) WO-987 exit
  confirm now registers with PanelManager (`DungeonExitInteractable` Register/NotifyOpened/NotifyClosed,
  DungeonTreasurePanel precedent); (2) `StarterLoadoutRegression` case 6 was silently re-litigating a
  superseded ruling — `af96fe788` (WO-500 D2 Option A) deliberately emptied the forge `blink_` exclusion;
  rewritten as JSON↔VendorDef AGREEMENT (still catches loader drift); (3) the orphan
  `WandererBubbleLegibilityRegression` is REGISTERED as `[wanderer-bubble]` (suite count 163 → 164).
- **⚠ THE ONE REMAINING RED IS HONEST AND PLAYER-FACING: WO-973's bubble fix has had NO EFFECT in play.**
  `[scene-matches-code]` proves `Dungeon_HealersCottage.unity` still carries `_panelWidth 4.4` /
  `_panelHeight 1.7` vs corrected code `1.8` / `0.7` — Unity deserialises the scene copy OVER the
  initialiser. **Fix = re-bake Dungeon_HealersCottage in an ISOLATED WORKTREE** (memory
  `dungeon-scene-shared-tree-corruption`). Until then Bryn's giant bubble ships. The oracle sat
  unregistered (never ran) from 08-14 until tonight.
- **⚠ No talent-tree capture case exists** — the WO-896 P0 UI has zero pixel verification (WO-942 class);
  the capture set has 22 distinct panels, none of them the skill tree. F8 seq 2389–2392 (scene-open +
  3 missing-prefab GUIDs in the hub scene, never-in-git machine-local assets from ~07-04) triaged,
  acked, and ticketed by the UI seat as **WO-1022**; talent demo-parity gaps = **WO-1021**.

## Latest (2026-08-14) — board reconciled, three hollow-assertion bugs fixed, dungeons re-baked
- **Branch `wip/village2-and-f8-tickets`, HEAD `e9c93415`, tree CLEAN, local == origin +5 UNPUSHED** (push
  held for the owner). Save schema still **v38** (`SaveSchema.cs:41`). Gates over the settled tree, read off
  the markers: `Builds/gate-postbake.log` → `COMPILE_GATE_OK` (0 `error CS`) ·
  `Builds/regression-postbake.log` → **`REGRESSION_OK 159/159 suites`**.
- **⚠ THE BOARD WAS LYING ABOUT 13 TICKETS.** `BOARD.html` is derived from the `**Status:**` lines, so it
  was routing agents to rebuild finished systems. **12 WOs had SHIPPED in git while still reading READY or
  "awaiting batch-gate + commit"** — 965/967/968/969/970/971/972/1014/1015/1016/1017/1019, each now citing
  its sha. **WO-1020 is bannered SUPERSEDED by WO-972**: both were minted off the SAME owner capture (F8
  seq 2327) — the UI seat minted a duplicate while the CLI seat shipped the fix. Board moved Ready 511 →
  499, Done 235 → 246, Unlabeled 0. ⚠ **No `.RESULT.md` was fabricated** (RULES 68) — what was verified is
  the shipping commit, not the behaviour, so the RESULT debt stays on the books.
- **⚠ THE DUNGEON RE-BAKE REVERTED AN OWNER RULING, AND THAT IS THE TRANSFERABLE LESSON.** WO-957 changed 13
  `"Extract"` labels to `"Leave"` **in the emitted LAYOUTS** — but layouts are GENERATED from the graphs, and
  the graphs still said `"Extract"`. `DungeonBaker.cs:1703` reads `IsNullOrEmpty(e.label) ? "Leave" : e.label`,
  so the code default is right but **an authored label WINS**. Proven by capture: the first bake stamped
  `label='Extract'` **×15**, `label='Leave'` **×0**. Fixed at the layer that owns it — the 13 labels are now
  `"Leave"` in the three content **GRAPHS**, both dual copies. Re-baked: `label='Leave'` ×13, the only
  remaining `Extract` being the two control fixtures. **This is the "a builder-only row is silently dropped
  by the next regenerate" class applied to an owner pin — fix the SOURCE, never the generated output.**
- **Bake result:** `COMPOSE_ALL_OK 7/7`, zero mate failures, **5 PathComplete**. The 2 PathPartial are exactly
  `dg_descent_probe` + `dg_stair_rig` — the WO-930 control group, deliberately still on the old pair model.
  Layouts are now `version 2` and every one emits `exitRoomId` — but as the **`entry` FALLBACK**, so WHERE the
  one true exit sits is still an owner design pick. Scenes are BINARY (batchmode `SaveScene` ignores
  ForceText) — verified NOT corruption: SerializedFile header + `6000.4.8f1` present, and git reports
  `Bin -> Bin`, i.e. they were already binary before this bake.
- **NEW WOs minted (banner bumped in the same edit each time, next free = 983):**
  **981** = `HeroProgression`'s starter latch is **not persisted — it is INFERRED from hero level** at
  `RestoreFromSave:202` on the assumption a hero past level 1 already got the gift, *which is exactly what
  WO-977 disproves*; so WO-977's retry holds in-session only. §B: the per-level grant at `:259` silently drops
  a point on a null `SkillSystem`, **every level**. **982** = `GraphDungeonComposer` **emits to StreamingAssets
  ONLY**, so every bake silently drifts the dual copy and **Resources — the copy that WINS at runtime — keeps
  the stale one**. ⚠ **This is the ROOT of the 08-08 incident `5f0e23aa` treated as a one-off**; the file was
  fixed, the mechanism was not, and it reproduced across all 7 layouts the next time anyone baked. Nothing
  catches it — `RoomForgeRegression.cs:162` is a hardcoded 3-file list with no `dg_*` in it (audit F24).
- **Three hollow-assertion bugs closed** (from `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`): **WO-977**
  starter skill points (grant first, latch on a MEASURED `AvailablePoints` delta) · **WO-978** four economy
  callers logged the amount *requested* as though it were *credited* — now report measured before/after
  deltas, **plus** a latch-before-grant in `DailyQuestRewardBridge:119` that could mark a daily permanently
  claimed having paid nothing · **WO-979** — ⚠ **its stated premise was REFUTED**: `Bind`'s `hud` parameter was
  never dereferenced anywhere, so wave feedback was never broken; only the trace was. Seam deleted, not "fixed".
- **⚠ 159/159 WAS GREEN ON A TREE WHERE AN OWNER RULING HAD BEEN SILENTLY REVERTED.** The suite count did not
  move before or after the bake that flipped 13 player-facing labels back to "Extract". Not one of 159 suites
  noticed. **The suite is a RATCHET, not a reviewer** — it locks known invariants and cannot read new code.
  This is the audit's own §0 shape ("every gate asserts a thing EXISTS, almost none assert it is CONSUMED")
  demonstrated live, and it is the argument for an external read on state/money/latch changes.
- **WO-980 ruled a DEFECT, not atmosphere** (opened the PNGs): in `docs/proof/2026-08-10-dungeon-headed-AFTER-camera-fix/`
  the hero is clipped at the bottom edge and rendered BEHIND the Talk/Bag buttons, and ~28% of `01_idle` is
  void above the room. **Geometric, so it needs neither the owner's eyes nor a colour call** — but the FIX
  needs a measured headed capture, not a constant picked off a screenshot (`DungeonCameraProfile`:
  `CameraHeight 1.9` / `CameraDistance 3.2` / `LookAtHeight 1.5`). Same capture is WO-973's required first step.
- **The "three tickets, one plume" collapses to TWO** — traced at source: `AmbientAuraPolicy.WithheldAmbientAuraKey`
  is the single literal `"TreeofLifeAura_Aura"` (FireFlies, the Heart tree), which **WO-1002 genuinely closed**.
  `Poi_NodeAura` is a DIFFERENT key → `Magic circle sun loop`, compared by exact key, so it is **never withheld**
  and still plays on every POI beacon. **WO-946 is the live one** and is now bounded: retag, or use the policy's
  existing `ShrinkInsteadOfWithhold` lever. ⚠ **WO-966 deliberately NOT touched** — the overnight report pins
  the dungeon −90 root yaw as untouchable until ruled; two facing systems tuned against each other manufacture
  a third bug.

## Latest (2026-08-10) — the wave-3 settle: 12 lane commits, 143/143 suites, five honest partials
- **Anchor still `CANON_GROUND_TRUTH_2026-08-09.md`** — ⚠ its header is now WRONG on three facts
  (it claims HEAD `19a50616`, "NOT PUSHED", "63 commits ahead"). Read the tree, not that header.
  Branch `wip/village2-and-f8-tickets`. **Save schema v38** (`SaveSchema.cs:41`).
- **The 2026-08-10 morning wave's in-flight lanes are CLOSED OUT.** Three lanes had died mid-write when
  the session expired and the tree did NOT compile: `GearAura.cs` (WO-959, helpers present, call sites
  fine), `EndStateView.cs` (WO-952, four call sites left on the old arity — `error CS7036`), and
  `DungeonExitInteractable.cs` (WO-957/1007/1008, two helper methods referenced but never written —
  `error CS0103`). All three completed by the committer, then gated as one tree.
- **Gates over the settled tree, read off the markers:** `Builds/gate-settle4.log` → `COMPILE_GATE_OK`
  (zero `error CS`) · `Builds/regression-settle3.log` → **`REGRESSION_OK 143/143 suites`** ·
  `Builds/ui-capture-settle.log` → `UI_CAPTURE_OK 62` + `UI_CAPTURE_FIDELITY_OK 44`. ⚠ The
  `UI_GEOMETRY_FAIL x16` in that capture is **WO-941's pre-existing RumorBoard/RealmMap baseline**, not
  new — and **no EndState case is in the capture set**, which is exactly WO-952's missing deliverable.
- **The suite count moved 136 → 143** because seven wave-3 oracles were finally REGISTERED:
  `[barracks-blanktown]` `[echo-hollow-route]` `[harvest-drip]` `[hostile-green]` `[dungeon-cam-958]`
  `[gear-aura-carry]` `[armor-store-window]`. Each lane authored its oracle and left registration to
  the committer on purpose — `DataRegression.cs` is lane-fenced. **Never restate the count; read the marker.**
- **Two reds were found and fixed on the way, both real:** `GuidePointer.cs` (WO-1012) hand-rolled two
  `Image` widgets and tripped the `[ui-obsidian]` HardFailOnNew ratchet — now built through
  `ElarionUiKit.AddImage(rounded:false)`; and `Dungeon/Exit/dungeon_texture` is a `Resources.Load`
  literal with no asset (the KayKit kit is gitignored) — registered as tracked debt in
  `HudUiRegression.MissingResourceBaseline`, since the runtime path already degrades loudly and visibly.
- **SHIPPED + RESULT-filed (7):** WO-950 blank-town drillmaster/teach/phantom-footprint · WO-951 Echo
  Hollow opens the roster · WO-953 harvest "+N" pops through the damage-number pool + gated-faucet
  honesty · WO-956 hostility off the red/green axis · WO-958 dungeon camera in tight rooms · WO-959
  weapon auras only while DRAWN · WO-960 armor-store locked-preview ladder · plus **WO-1012** tutorial/
  FTUE redesign (RESULT filed).
- **⚠ FIVE HONEST PARTIALS — still READY, remaining scope written into each WO body** (a `.RESULT.md`
  forces the board's Done bucket, so none was filed): **WO-952** the geometry fix landed but its capture
  case + `COMPRESSED`-absence oracle do NOT exist · **WO-957/1007/1008** the code landed and the owner's
  "Leave" relabel is now in the data (13 labels, 3 content layouts, both copies byte-identical), **but
  the dungeons have NOT been re-baked** — `_isTrueExit` is a `SerializeField` on BAKED objects, so
  nothing is on screen until a re-bake **in an isolated worktree** · **WO-949** respawn-in-town and the
  3 founding potions landed, the "teach the cost of dying" deliverable did not.
- **WO-85 NEVER STARTED** and now says so: grass + roads already shipped at `cc24da5a`/`bfacf0b3` and
  nothing has touched them since, so the lane is *"why does the shipped terrain not read"*, not "add
  grass" — and **value contrast must carry it** (hue alone is invisible to the owner).
- **Owner directive added to CLAUDE.md §11:** *the pipeline never idles* — the agent pool tops up on
  every lane completion; pin-blocked tickets park with their pins surfaced; one gate, one committer.
- **Owner pins still open:** WO-954 hollow models · WO-947 four cost-basket calls · WO-917 dodge glyph ·
  WO-1013 Arcane Tower/Spire naming · D8 Walls tab · WO-956's deuteranopia risk on the new body tint ·
  WO-960 shelf depth · WO-959's drawn/sheathed mapping.

## Latest (2026-08-09) — the 08-08 ship day: machine unblocked, stairs SOLVED, store re-gated
- **⚡ EVENING-2 WAVE (2026-08-09 ~21:00-23:00, this seat) — the WO-1010 defect-pass close + the Sylas fix.**
  Owner F8s (new product folder `LocalLow\DeNelle\Echoes of Elarion` — **the F8 daemon was watching the
  OLD folder; restarted on the corrected script**, her flags now ping again): (1) *"Sylas is coming
  through as a blink"* — **FIXED**: `HeroBodySwapper.Start()` now probes `Resources/Heroes/<slug>` FIRST
  for non-Knight classes (Ranger.fbx/Mage.fbx were git-TRACKED since `f18b66b4` but unreachable — the
  Blink base load was terminal-on-success); (2) *"This screen is not correct"* — the WO-1010 §7 pass
  closed: D17 sprites live (element/check+rotate authored), D19 seating consumed, always-on touch D-pad
  retired (its reflection seam deleted), ONE skip, P3 hint line, PICK dock 540→410 band-tightening.
  Gates `COMPILE_GATE_OK` + `REGRESSION_OK 133/133` + `UI_CAPTURE_OK 62`/`FIDELITY_OK 44`, PNGs opened
  vs `UI_REVIEW/build_ui_target_wireframe.html` (owner re-pinned it tonight). New WOs off the banner:
  **941** (pre-existing RumorBoard/RealmMap `UI_GEOMETRY_FAIL x16`), **942** (capture-case gaps);
  UI seat minted **WO-1012 tutorial/FTUE redesign** (+ wireframes) — D16's full rework lives there.
  Open: **D8 Walls-tab owner ruling** (conflicts with the 07-13 ruling), tester re-test, felt-verify.
- **Anchor = `CANON_GROUND_TRUTH_2026-08-09.md`** (supersedes 08-08, bannered). Branch
  `wip/village2-and-f8-tickets`, **HEAD `07b756b6` (2026-08-09 23:00), PUSHED 2026-08-10 ~10:12 —
  local == origin** (the 68-commit 08-09 wave; the earlier "HEAD c8320434 / 30 commits" reading was the
  08-08 point-in-time state). ⚠ 2026-08-10: the 5.1 GB stale Grok worktrees under `~\.grok\worktrees`
  were deleted (verified no unique work); the tree carries the 08-10 morning fix wave uncommitted while
  it gates (WO-931 · death-pin rebase · battle-music gate · WO-945).
- **Gates last emitted** (read off the marker files, never off this line): `Builds/gate-ship3.log` 19:36 →
  `COMPILE_GATE_OK` · `Builds/regression-ship3.log` 19:38 → `REGRESSION_OK 130/130 suites` ·
  `Builds/ui-capture-ship.log` 14:30 → `UI_CAPTURE_OK 44`. ⚠ **`Builds/test-results-EditMode.xml` is
  930/930 green but STAMPED 2026-08-04 — five days stale; do not cite it as current evidence.**
- **✅ THE MACHINE BLOCK IS RESOLVED.** Rebooted 2026-08-08 08:07:21; commit charge **45.7 GB of 127.8 GB**,
  11.9 GB physical free, no Unity running. **Windows EXE built 08-08 14:33; Android APK 08-08 20:00
  (572,202,338 bytes); Firebase ran.** ⚠ **The WebGL / web-deploy step NEVER RAN** — `Builds/WebGL` is
  still dated 2026-08-05 and there is **no `Builds/webgl-chain-status.txt`**. That is the open rail.
- **★ THE DUNGEON STAIRS ARE SOLVED — the whole PathPartial hunt is CLOSED.** WO-930 shipped the one-room
  stairwell: `3ab1bfb6` (**first floor-to-floor `PathComplete` in project history**) → `e7163c9c` (skinned,
  0 bad surfaces) → `5f0e23aa` (candle lights + **a caught RED gate: `dg_sunken_vault.json` dual-copy
  drift, Resources held the OLD 17-room layout and Resources WINS at runtime**) → `cb092b7f` (**all 4
  content dungeons PathComplete, 12 descents, 0 mate failures, 14/14 dual-copy parity**;
  `dg_descent_probe`/`dg_stair_rig` left on the old model as controls) → `51a89364` (`RoomPrefabMeta` on
  `StairwellRoom` — the overlap gate had been measuring a **20x10 m room as one 10 m cell**).
  **ROOT CAUSE = STAIR YAW:** `GraphDungeonComposer.SolveMate` hardcoded `yaw = 0f` on vertical sockets, so
  only a Delta of 180 landed the flight in the floor hole. **It was never a property of the stair** — which
  is why four rounds of bucketing the stair's scalars all came back negative. The 08-08 anchor's
  "dump navmesh triangles next" guidance is DEAD; its killed-hypotheses table survives as history.
- **⚠ HEADLESS GATES CANNOT SEE ORIENTATION (transferable).** `70a86c17` **reverts** `bb6dc010`:
  `SkinOptions.PreservePrefabRotation` applied to ALL structures **laid the whole town on its side**
  (13 catalog rows carry a manual -90 that composes to 180), reproducing only on the **dungeon → town
  return path** via `BaseLayoutLoader` — every marker green throughout. The narrow fix is `439e03ee`: a
  per-catalog-row **`RepoProps.preservePrefabRotation`** (default false, **exactly one opt-in:
  `tower_ground_archer`**) with `StructureFactory.OptsFor` as the single reader unifying
  `Create`/`MeasureUprightFootprintMetres`/`GhostPreview`. ⚠ **Still live:** `Resources/Structures` holds
  both a `.fbx` and a same-stem `.prefab`, so `Resources.Load` is **ambiguous**.
- **⚠ SECURITY RE-GATE — `FeatureFlags.RealmStorePurchase` is back to `defaultOn: false` and LOCKED**
  (`576601e3`). `StubWalletProvider` has **NO `#if UNITY_EDITOR`/`DEVELOPMENT_BUILD` guard**, ships in every
  player, fabricates a wallet + a **2000 SKR mock balance** + a base58 signature, and `ApplyPackContents`
  then **grants the pack for ZERO payment** while firing `purchase_completed` with the fake txSig.
  **The submitted store build had a tappable Buy button.** = **WO-931 — IMPLEMENTED 2026-08-10 (option b,
  owner-picked): runtime refusal at BOTH `WalletService.Pay` and `PayFlat` seams** (stub short-circuit +
  `IsRealSigningWallet` belt; loud `FlowTrace.Fail` refusals; regression cases in
  `WalletProviderSelectionRegression` §8; precondition 3 of 3 recorded SATISFIED in the flag's
  DO-NOT-TURN-ON block — **preconditions 1 and 2 remain OPEN, the flag default did NOT move**).
- **Legal + publishing:** `640bfc1c` sets `productName` → **"Echoes of Elarion"** (installs under the store
  listing name). `c8320434` authored `docs/TERMS_OF_USE.md` and hosts it verbatim at `site/terms.html`, live
  at `https://echoes-of-elarion.vercel.app/terms` (verified 200), linked from landing nav + footer;
  governing law **Texas**; ⚠ **no arbitration / class-action / jury-trial waiver — deliberately left for the
  owner's attorney.** Publishing scaffold under `publishing/` + `tools/store_previews_resize.py`.
  ⚠ **TWO OPEN FLAGS:** (a) **`PRIVACY_POLICY.md:87-89` has ONE FALSE SENTENCE on a LIVE page** — it
  describes an Ad button that "grants that time saving immediately without presenting any advertisement",
  and **that button is now ABSENT from the UI entirely** (the core no-ads claim is verified TRUE; only the
  explanatory sentence is stale). **Do NOT edit it — live legal copy is the owner's/attorney's call.**
  (b) **`docs/PUBLISHING_STEPS.md` Rail 1 is OBSOLETE** (bannered): `dapp-store-cli@1.0.0` has **no
  init/create/validate/publish** — the whole surface is `dapp-store --apk-file ... --whats-new ...` — and
  the app must ALREADY exist in the portal with an App NFT. Publisher + app are created **in the web portal
  with a browser wallet**; `publishing/config.yaml` is the verified **paste-source** for that form.
- **⚠ `tools/webbot/` WAS DELETED OUTSIDE GIT.** All four files (`canvas-probe.js`, `introtest.js`,
  `package.json`, `webbot.js`) are **present at HEAD**, **no commit ever deleted them**
  (`git log --diff-filter=D` empty), they are **not gitignored**, and the directory is **absent on disk**.
  This is the Playwright web-build self-test rig. Restorable with `git checkout -- tools/webbot/` —
  **NOT run; it is an open decision for the owner.**
- **Dev tooling out of the shipped player:** `eeb2d389` flips `ff.devresourcetool` **OFF** by default and
  moves DevPanel under Settings (`PanelId.DevPanel` = 17, gated on `PanelRouter.IsRegistered`); `374ccd26`
  ships a **RELEASE desktop player** (verified `DeNelle.DevTools.dll` absent — 206 DLLs, was 207).
  ⚠ **TRAP: the flag flip did NOTHING on this machine** — `FeatureFlags.Get` reads **PlayerPrefs FIRST**
  and this box has `ff.devresourcetool=1` persisted from 08-07. A default change is not a state change on a
  machine that already answered the question.
- **Felt fixes:** `2f10f6ac` — auto-upgrade was handing **every level-2 knight a paid Forge
  `knight_flameblade` for free** (candidates narrowed to owned gear; tri-state ownership survives a
  `VillageInventory.EnsureLoaded` pre-load race). `763d1a60` — nameplates rendered literal
  **`[[missing:market]]` / `[[missing:jeweler]]`** to the player; forge/armorer duplicate; "Lumber Mill"
  renamed across catalog/quests/prefab.
- **⚠ F8 — ONE UNACKNOWLEDGED capture, seq 2248** (2026-08-08 13:17:10, `Main_Castle_Overworld`):
  `Cannot set the parent of the GameObject '[VFX_Harvest_Wood]' while activating or deactivating the parent
  GameObject 'Lumbermill'.` This is the **WO-929** class and WO-929 already names `HarvestAura.cs` — but
  **every proving line in WO-929 is `OutpostEnemy (...)`, a POOLED ENEMY.** This capture proves the same
  illegal `SetParent` fires from a **BUILDING**, so **a fix scoped to the pooled-enemy path is incomplete.**
- **WO board:** `0d75bc06` — an audit found **52 of ~91 WO statuses WRONG**
  (`docs/reference/WO_TRUE_STATUS_2026-08-08.md`); it also surfaced that **WO-884's VFX facade never
  existed**, **WO-898's `crystalsPerBracket` has 0 hits**, and **WO-875/877 were never attempted**.
  WO-930's own file said READY/SHIP-BLOCKING although it shipped (corrected); WO-927 is superseded by its
  own §0. **RESULT-file debt on the live arc: 921/923/924/925/926/927/928/929/930/931/1006/1007/1008/1009 —
  none exist, none fabricated.** ⚠ **Read the next-free WO off the `CLI_LANES_WO_NUMBERS.md` banner — never
  from a doc.** (The banner's own block table had gone stale against its header; the table row was corrected
  2026-08-09.)
- **★ FOUR LONG-STANDING CANON CLAIMS REFUTED AT SOURCE — all CLOSED, stop carrying them** (anchor §9;
  each verified line-by-line by this seat, and each corrected IN PLACE in its own section above):
  **(1) "THE SEAM"** — closed by WO-853; the raid-roadmap prerequisite is **satisfied**, so **WO-774.0 is
  no longer free to defer.** **(2) The "orphan third copy" of the gear catalogs** — `Assets/Data/Canonical`
  **does not exist**, deleted in `c55a5561`; it could not have shadowed the pair anyway because
  `LocalJsonCatalogSource.Read` probes only `Resources.Load<TextAsset>` then `streamingAssetsPath`
  (`LocalJsonCatalogSource.cs:33-52`). *(`CANON_GROUND_TRUTH_2026-07-22.md:193` §5.8 and two design docs
  are stale on this — deliberately not edited.)* **(3) `CatalogBootstrap.RegisterFallback` drift** — all
  three rows are now **field-equal**, including `tower_arcane_spire.visualTexturePath =
  "Structures/ArcaneSpire_Albedo"` (`CatalogBootstrap.cs:307`), so **the pure-white defect is CLOSED**; now
  guarded by `BuildEconomyRegression.cs:1191-1290` gate 12 `[fallback-parity]`. **(4) Dual-copy is
  HEALTHY** — swept 80 files per side, 77 paired, **only `weapons.json` + `armor.json` drift and both are
  the DELIBERATE owner gear ruling**; the 08-08 `dg_sunken_vault.json` drift is FIXED (both sides v1 /
  14 rooms); all dungeon layouts + graphs byte-identical.
- **⚠ NEW GAPS, all OPEN and UNCOVERED** (anchor §10): **three difficulty levers are computed and thrown
  away** (see the Adaptive line above) · **`DataWebRegression` iterates the StreamingAssets root only**
  (`:208` drift, `:356` version), so **a Resources-only file — the copy that WINS at runtime — is never
  drift- or version-checked**; verified Resources-only = `ad-creatives.json`, `ad-placements.json`,
  `widget-params.json`, and **`widget-params.json` has no `version` field at all** · the version check is
  **presence + cross-copy agreement only, never "a change bumps it"** — **24 catalogs had content changed
  with no version bump on their most recent commit** (worst: `enemies.json` +95, `en.json` +265,
  `themes.json` +369, `waves.json`, `abilities.json`) · **`RoomForgeRegression.cs:162`'s dual-copy gate is
  a hardcoded 3-file list containing NO `dg_*` layout** — including `dg_sunken_vault.json`, the exact file
  that drifted, so the next drift ships the same way · **`DungeonBaker` probes ONE path**
  (`placedOrder[0] → placedOrder[last]`, `:432-445`) and is **log-only** — `SaveScene` runs unconditionally
  after a `PathPartial` (`:457-479`, `:490-494`), so a dungeon whose FIRST descent fails is
  indistinguishable from one whose last does, and reachability is gated by the first failure.
- **⚠ WO-930 did NOT delete what its spec said it would — and that is BY DESIGN.** `StairUp`/`StairDown`,
  `IsVertical`, `SEALED_VERTICAL`, the floor holes and ceiling shafts are all **retained as a quarantined,
  gated CONTROL GROUP** (`DungeonMultiLevelRegression.cs:41-63`, explicit **"⚠ DO NOT DELETE"**).
  **`dg_stair_rig` and `dg_descent_probe` are TEST FIXTURES, not stale content or regressions** —
  `[graphs-converted]` asserts they STILL name the retired prefabs so a tidy-up cannot delete the control
  group by accident. Converted layouts, verified: `dg_bonecrypt`, `dg_ember_deep`, `dg_sunken_vault`,
  `dg_stairwell_probe`. The deletion is a future single-commit job (WO-930 §5).
- **⚠ `structures-catalog.json` is `version: 15`** (both copies identical, 29 entries, `_heightCadence`
  present). Any doc saying v6/v7/v8 is a stale point-in-time reading. **Read it off the file, not off a doc.**
- **Still open and CARRIED FORWARD** (the 08-08 anchor dropped these — see the 08-09 anchor §8): the VFX
  **ONESHOT pool saturates 40/40** (different pool, different reclaim path — **NOT closed** by the 08-06
  loop-cap fix) · the **absence** of `SKIPPED - active loops 20/20` across a full wave has **never been
  proven** (owed a fleet run) · **`VFXType` serialises by ORDINAL, appends only** and `Build()` does
  `entries.arraySize = rows.Count` (a builder-only row is silently dropped by the next regenerate) ·
  **WO-910 READY FOR OWNER RULING** (31 dead nodes / 40 player-reachable Ranger+Mage talents; Ranger 1
  usable of 20, Mage 5, both tier-4 capstone rows dead) · **hero select SELF-SKIPS** when the save records
  a class (test a class change with New Game / Play Intro, never Continue) · **`api/` is PREVIEW-only** and
  prod's nonce endpoint has **no CORS** (promotion = owner's call) · still **colour-only and OPEN**: the
  build placement ghost and the hero health bar.

## Latest (2026-08-06) — the VFX night: two P0s, Ranger/Mage unlocked, one height cadence
- **Anchor = `CANON_GROUND_TRUTH_2026-08-06.md`** (supersedes 08-05, bannered). **HEAD `1534dffb`, local is
  43 commits AHEAD of origin — NOT PUSHED.** ⚠ Working tree NOT clean: `ProjectSettings.asset` carries a
  newer APK stamp (`2026.08.05.312459` / code `312459` vs the committed `312348`) and
  `WorkOrders/WORK_ORDER_885`–`894` are untracked. Save still **v36**.
- **Gates last emitted:** `COMPILE_GATE_OK` + **`REGRESSION_OK 120/120 suites`** + `VFX_LOOPFLAG_OK` +
  `VFX_ART_MIRROR_OK` + `PARTICLE_PACK_VFX_BUILD_OK` + `BOSS_FIREBREATH_BUILD_OK`.
  ⚠ **The count moved 117 → 118 → 119 → 120 in eight hours. Read it off the marker, never off a doc.**
- **THE PATTERN (transferable):** six defects, one shape — **a flag authored BY HAND instead of DERIVED
  from the thing it describes.** `IsLoop` · the "self-contained" tracked VFX prefab · `HeroTalentNodeDef.Hidden`
  · `TalentStrategyRegression.HiddenTrees` · the capture harness resolution · `RegisterFallback`.
  **Derive it — and PIN the owner's standing rulings ABOVE the derivation with their reason**, because the
  prefab is the authority on what the art *does*, not on what the game *should do*.
- **⚠ P0 — THE VFX LOOP CAP LEAKED DRY.** `IsLoop` was a sticky checkbox `VfxCasterWindow` force-set true
  for role Projectile/Aura; **53 of 122 picks were wrong.** A loop row never returns its slot (the only
  reclaim frees DESTROYED hosts; pooled objects are never destroyed), **cap 20**. Archer + ballista fire
  `PP_MuzzleFlash` and discard the handle, so after ~20 shots a tower renders **no projectile** and starves
  the Tree of Life aura + every POI marker. **Six F8 sessions on two dates show `SKIPPED - active loops
  20/20`**, naming five victims that were themselves the mis-flagged culprits. Both generators now DERIVE
  the flag from the art (rule: `main.loop` AND a positive rate, emission enabled; authority = the root
  system UNLESS it cannot emit). ⚠ **Not yet proven: the ABSENCE of the message across a full wave — owed a
  fleet run.** ⚠ **A separate, unbundled signature: the ONESHOT pool saturates 40/40** in three captures.
- **⚠ P0 — THE TRACKED VFX PREFABS WERE NOT SELF-CONTAINED.** `CopyAsset` duplicates the **prefab only** —
  never its materials/textures/shaders/meshes/animations. **27 of 28 prefabs, 183 references, 73 distinct
  assets** pointed into gitignored art (magenta/untextured/invisible on any machine without the packs).
  **Now 0**, verified twice; **~23.85 MB mirrored, deduped** to `Assets/Resources/VFX/_Shared/`; enforced by
  a regression that fails on ANY dependency in a gitignored root (`VFX_ART_MIRROR_OK`). ⚠ Two pack
  MonoBehaviours could not be mirrored and were **stripped — `Casting_Fire` no longer spawns a projectile.**
  ⚠ **Lana Studio is NOT gitignored** (only its URP upgrade subfolder is).
- **⚠ RANGER + MAGE UNLOCKED, TREES EMPTY.** `ff.knightonly` defaults **OFF**; roster Knight/Ranger/Mage via
  `DeNelle.Core.State.PlayableHeroes` (**Cleric deliberately out — no authored kit**;
  `ff.knightonly`=1 restores the solo-Knight pivot). Emptying `TalentStrategyRegression.HiddenTrees` — which
  had hardcoded `{ranger,mage}` so guard G3 had **NEVER** audited 40 player-reachable nodes — surfaced **31
  real dead nodes: Ranger ONE usable talent of 20, Mage five, both tier-4 capstone rows dead.** Knight (32)
  and shared (9) green. `hero-talents.json` **UNTOUCHED, md5 unchanged**; the 31 are a dated ratcheted
  baseline (a baseline id that stops reporting dead ALSO fails). **WO-910 = READY FOR OWNER RULING.**
- **⚠ LATENT INVISIBLE-HERO P0, FIXED.** Ranger/Mage have **no FBX**; both fell through to a Blink base body
  and **`Assets/Blink` is gitignored** — on a fresh clone the terminal fallback **returned without
  instantiating anything** after `Start` had destroyed the placeholder. Both bail-outs now build a tracked
  **KayKit** body. ⚠ **Hero select SELF-SKIPS when the save already records a class**
  (`HeroSelectController.OnEnable` → `SceneRouter.GoCastle()`), so testing a class change needs **New Game /
  Play Intro**, never Continue.
- **⚠ ONE HEIGHT CADENCE** (owner ruling, recorded in the data as `_heightCadence`, catalog **v6 → v7 → v8** — 7 = the archer `0ac59581`, 8 = the cadence `d42e2817`, verified at HEAD):
  **1.25** landmark · **1.2** towers (4.8 m, 2.778 m across = 49.9% of a house) · **1.0** building base ·
  **0.75** siege · **0.35** decoration. **WALLS DELIBERATELY EXCLUDED** — the fit is uniform, so narrowing a
  wall **opens PATHABLE GAPS in saved wall runs** and shrinks the navmesh obstacle with them; needs a
  measured audit + a migration decision. **`collector_farm` at 1.4 is a COMPENSATION, not an outlier**
  (windmill blades inflate the Y bounds). **`repo.visualHeight` is DEAD for runtime placement** — deprecated by
  WO-764, authored zero times, no longer read by `StructureFactory.EffectiveVisualHeight` (one legacy
  EDITOR reader survives in `RaidBaseGenerator.cs`). *(This SUPERSEDES the "tower override 7m" line further down this file.)*
- **ACCESSIBILITY — the low-health tell is no longer a colour.** Severity drives **pulse rate 0.85 → 3.2 Hz**,
  **guttering depth** (trough to a tenth of authored density) and simulation speed; **below a quarter health
  the RECIPE SWAPS** to a candle gutter — a shape change, not a hue change. The vignette stays as a
  **redundant** cue; colour-ONLY was the bug. Mutual exclusion is **structural** (one handle field). Still
  colour-only and OPEN: **the build placement ghost** and **the hero health bar**.
- **⚠ THE UI CAPTURE HARNESS WAS GEOMETRY-BLIND** until `7e05e6d3` — only `canvas.scaleFactor` was rewritten,
  never `Screen.*`, so **the resolution in a PNG filename was a LABEL, NOT A LAYOUT** and two panels shipped
  broken behind a green marker. **2670x1200 had never been rendered in this repo.** Several 08-05 UI commits
  are **not** geometry-verified. ✖ **`ClampMinTouch` was CHECKED AND RULED OUT** at three sites tonight
  (bands resolved 117 / 116.7-130.6 / exactly 112.0 px) — check the arithmetic before naming it.
- **⚠ VFX is a CONNECTION problem, not an art problem:** **26 of 79 enum values are wired to real art with
  ZERO gameplay callers**; six whole tracked Lana categories sit at **0% usage**; a GUID sweep of **8,795
  prefabs and 156 scenes found ZERO VFX scripts attached anywhere** (which is what makes `EliteVFXController`
  dead three separate ways — **its 0.7 boss death shake has never fired in the shipped game**).
  ⚠ **`VFXType` serialises by ORDINAL, not name — appends only.** ⚠ `Build()` does
  `entries.arraySize = rows.Count`, so **a row written only by a builder is silently dropped by the next
  regenerate** and the effect falls back to something that still looks like it works.
- **Session ledger (known dictionary):** `docs/reference/SESSION_INDEX_2026-08-06.md` — every defect with
  its proving line, every **refuted** belief with the evidence that killed it, the owner rulings, the open
  items. Earlier half of the same day: `docs/reference/DEFECT_INDEX_2026-08-05.md` (frozen).

## Latest (2026-08-03) — the solo-night wave + the FIRST live server verification
- **Anchor = `CANON_GROUND_TRUTH_2026-08-03.md`** (supersedes 08-02, bannered). **HEAD `56be3ae2`, pushed,
  local==origin, working tree CLEAN.** Gates: `COMPILE_GATE_OK` + **`REGRESSION_OK 104/104 suites`** +
  **`TESTS_OK 912/912` zero reds** + `UI_CAPTURE_OK 28`. Save still **v36**.
- **⚠ The 08-02 anchor pinned `e60b19e5` and 17 commits landed after it** — three boot docs inherited the
  stale sha AND the stale 884/884 count. Read the count off the marker, never off a doc.
- **SERVER, PROBED LIVE (not reported) — this corrects 08-02:** **`auth_nonces` EXISTS**; prod
  `GET /api/auth/nonce` returns **HTTP 200** with a real nonce. ⚠ but the table's only rows were minted by
  the probe — no real client has ever used it. **`api/` is deployed to PREVIEW only** and the game
  hardcodes the prod domain, so the overnight server work is unreachable; **and prod's nonce endpoint has
  NO CORS + `OPTIONS` 400**, so a browser blocks the WebGL wallet rail regardless of the client. Prod is
  proven to be running OLD `api/` code (prose error shape + missing `bugreports`/`authrejects` views).
  `player_data` = **2 test rows, newest 2026-05-31**; `bug_reports` = **0**; `analytics_events` = 80,749
  (web tracing flows fine). **Promoting `api/` to prod is the highest-value action on the board — owner's call.**
- ~~**THE SEAM (verified from code):** nothing can damage a wall, gate or enemy tower...~~
  **⚠ REFUTED + CLOSED 2026-08-09 — do NOT carry this forward.** WO-853 closed the seam from both ends and
  it no longer exists at HEAD. **Dual implementation** (`... : MonoBehaviour, IDamageable,
  IDamageableStructure`) on `Village/Walls/WallSegment.cs:53`, `Village/Gates/Gate.cs:67`,
  `Village/Buildings/DefenseTower.cs:57`, `Village/World/Camps/RaidSpire.cs:61`; **mask widening on BOTH
  troop entry points** so a factory-supplied Enemy-only mask cannot strip it — `TroopController.cs:189`
  (`SetEnemyMask`), `:201-202` (`WithStructureLayer`), `:394` (`Awake`) — with walls staying on the
  **Structure** layer deliberately (that layer is the tower LoS blocker mask; relayering them onto Enemy
  would make towers shoot through walls again); **collider buffer 48 → 128** (`:104`) so wall panels cannot
  crowd enemy colliders out of `OverlapSphereNonAlloc`'s arbitrary-order truncation. Covered by
  `TowerWallLosRegression`, `StructureTargetableRegression:440`, `DefenseTargetableRegression:136`,
  `RaidArenaShapeRegression:363`. **⚠ Consequence: the raid-roadmap prerequisite is SATISFIED, so the
  WO-774.0 drop-and-watch-vs-led ruling is NO LONGER FREE TO DEFER** — it was parked because the seam
  blocked both roadmaps, and that reason is gone. *(verified line-by-line 2026-08-09)*
- **Overnight (15 commits):** enemies actually reach you (own-wounded targeting + `_stopTightenedForHero`
  surviving pooling); pooled-enemy statues fixed; **raids rescaled 2.4% → 20/49/60% of floor with a spire
  objective** (raid walls had NO colliders; no raid scene had a hero spawn point); raid troops animate +
  aren't magenta; unarmed level-1 Mage fixed; defense cap unified at **0.90**; tutorial Hollow step
  completable; **the check-in gate had never run at all** (didn't parse under PS 5.1); `DeNelle.Core.Difficulty`
  → **`DeNelle.Core.Adaptive`** (it shadowed the persisted enum).
- ~~**Adaptive difficulty is INERT** — `WaveManager` records none of the six fields, so every read returns 1.0.~~
  **⚠ HALF-REFUTED 2026-08-09.** All six `EncounterSample` fields **ARE** measured and recorded: six-arg
  ctor + `DynamicDifficulty.RecordEncounter` at `Village/Waves/WaveManager.cs:2471-2484`, armed by
  `BeginEncounterTelemetry` at `:2341`, consumed at `:1761-1762` and `:1876-1877` via `e.ApplyDifficulty`.
  **The REAL defect is narrower and worse-shaped (NEW, HIGH, UNCOVERED): three of the five multipliers are
  computed and have ZERO gameplay consumers** — `EnemyCountMultiplier`, `BossHpMultiplier`,
  `BossDamageMultiplier` (`Core/Difficulty/DynamicDifficulty.cs:119,122,125`), no reader anywhere outside
  `Core/Difficulty` (the only external hits are `DynamicDifficultyRegression.cs:276-292` and
  `Assets/Tests/EditMode/DynamicDifficultyTests.cs`, and **both call `DifficultyMath.*`, never the live
  `DynamicDifficulty.*` properties**). **So every boss wave ignores the softer boss curve the math file
  exists to produce, and the count signal is dead.** `DynamicDifficultyRegression` proves the math/oracle
  only — no `WaveManager` reference, no consumption assertion — so the levers can be correct and unwired
  with the suite green.
  ⚠ **Namespace vs. path — both are right, do not "fix" either:** the folder is
  `Assets/_Modules/Core/Difficulty/`, but all six files in it declare `namespace DeNelle.Core.Adaptive`.
  The 08-03 rename moved the **namespace** (it shadowed the persisted enum) and left the folder alone.
  *(verified 2026-08-09)*
- **Canon health:** `docs/MASTER_CATALOG.md` (the INDEX) was NOT refreshed by WO-836 — only the 19 area
  files were; the index still says Blaise/party-of-4/v30/next-WO-412. **Use it as a filename list only.**
  The area files are code-true as of `b77a178e`, not HEAD. `docs/reference/REGRESSION_COVERAGE_MATRIX.md`
  is two Sundays stale (still says "16 suites") — use its proposed assertions, never its counts.
  RESULT-file debt = **33**, not 31.

## Latest (2026-08-01) — post-reboot ship wave: Realm Map + KayKit NPCs + Queues ruling + release train
- **Anchor = `CANON_GROUND_TRUTH_2026-08-01.md`** (supersedes 07-26, bannered). **HEAD `ac0a52e3`, pushed,
  local==origin.** Gates: `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK 23` (pixels eyeballed).
- **WO-818 ALL PHASES SHIPPED:** 12 KayKit NPC bodies tracked at `Assets/Resources/NPCs/KayKit/`
  (`KAYKIT_STAGE_OK 12/12`, Humanoid); `structures-catalog.json` **v6** dual-copy carries `repo.npcModel`
  on exactly 12 owner rows; `KayKitNpcBody` resolver = KayKit-first → People chain → capsule (one Warn,
  never blank); `NPC_MODELS` oracle pins the 12 verbatim. Body swap = one-word owner JSON retag.
- **WO-826 Realm Map SHIPPED:** parchment panel (Elarion gilt home + 5 fog regions from dual-copy
  `realm-map.json`), strict MVVM, HUD **Map** button (hidden until Onboarded, WO-825 R4), DevPanel entry,
  `REALM_MAP` oracle + 8 EditMode tests. Travel stubbed → WO-827. 825 program IN FLIGHT (827/828/829 next).
- **OWNER RULING: bar Queues button RETIRED** — the right-column **Builders chip** (QueueStatus band,
  above the resources dock) is the ONE Queues entry; calm(town) bar = **6 faces**
  (Build/Talk/Bag/Raids/Map/Quests⇄Upgrade). `ObsidianQueueRegression` 7c enforces the retirement.
- **ProjectSettings dynamic-batching RCA CLOSED** (`ac0a52e3`): reverter proven (twice-captured) to run
  INSIDE `BuildPlayer` after the pre-build set; `DesktopBuild` now re-asserts static=0/dynamic=1
  post-build (the WebGL exceptionSupport-restore pattern). Owner keeps dynamic=1.
- **Dungeon verified from a captured run** (owner-ordered log test): all 7 proving lines + the R-A1
  arena CharacterController guard green. Open: vitals 120/60 placeholder (770.10), placeholder props
  (770.8), `EnvTreeFix VERIFY FAILED 'Skeleton_Mage_Hat'` (minor, unticketed).
- **Release train:** fresh desktop exe (15:17) · Seeker APK built + Firebase App Distribution (testers
  group) + adb install · WebGL→Vercel PREVIEW queued (promotion = owner). Screenshot archive:
  `Builds\ui-capture-archive\2026-08-01\` (23 PNGs).
- **UI seat reconciled:** WO-830 (Echo harvest affinity+synergy: 6 unique affinities
  Wood/Iron/Food/Gold/Crystals/Repairs, 3 disclosed pairs, 1 HIDDEN tri-synergy) + WO-831 (2D emergence
  sprite beat) minted; `docs/qa/UI_REVIEW_2026-08-01.md` (20-panel real-pixel review) banked.
  **WO next-free: see the banner** (`CLI_LANES_WO_NUMBERS.md` is the SOLE authority — never copy the
  number into docs, point at it. As of 2026-08-02 two DISJOINT blocks are in use: the CLI mints the
  main line, the UI seat mints only from a reserved 860–899 block. Five collisions happened on
  2026-08-02 alone, every one caused by a mint that did not bump the banner in the SAME edit.)
- **Verified inventories (cite these):** FeatureFlags = 62 (⚠ XML summaries LIE on 12 defaults —
  trailing `//` comment is truth); save **v36** (`SaveSchema.cs:36`, WO-834 `everBuiltStructureIds`).
- **⚠ CORRECTED 2026-08-02 — the WaveDataTest line that used to sit here was FALSE and dangerous.**
  It read "EditMode reds live in `Assets/Data/Tests/WaveDataTest.cs` (wave-1 ruling open)". There are
  **NO EditMode reds** — the full suite is **884/884 green**. Those two tests were **STALE TESTS**,
  not an open question: the owner ruled smart-composition on 2026-07-30 (`_smartComposition:1`, so
  `waves.json` `enemies[]` batches are inert), and both tests were rewritten to assert the batches
  are EMPTY — a re-add now FAILS the gate. Leaving the old line here invited a session to re-open a
  ruling the owner had already closed, which is the exact failure §15 exists to prevent.
- **Queue ahead:** 827/828/829 (travel/minimap/biomes) · 821 timed research · 837 stockpile caps ·
  838 magenta troops (Phase-A probe first) · 848 restore Android stripping · 851 every-4th-wave boss
  waves · 861 remaining phases. **830/831/835/839/840/841/850/852/860 all SHIPPED 2026-08-02.**

## Latest (2026-07-30) — 12-agent SME fan-out + check-in sweep + WO-783 fix wave
- **Operating model (owner directive):** CLI = **GATEKEEPER**. Dedicated agents write requirements, write
  the tests, and produce **read-only implementation proposals**; the CLI verifies every proposal **against the
  tree**, runs the tests, gates, commits by explicit path, and **screenshot-verifies before anything reaches
  the owner**. Agent output is a proposal, never truth — two claims were REFUTED on verification this session.
  Memory: `cli-gatekeeper-agent-role-model`. *(2026-07-30)*
- **Check-in sweep:** 9 commits, tree clean. Found + fixed **5 folders with tracked contents but an UNTRACKED
  folder `.meta`** (Core/Enemies, Core/Jobs, Village/Troops/Data, both dungeon-graphs) — a GUID-regeneration
  hazard on the second machine. `Assets/UnityTechnologies/` (191 MB Particle Pack) gitignored per big-pack
  policy + logged in `tools/art/REQUIRED_PACKS.md`. *(2026-07-30)*
- **WO-783 fix wave — IMPLEMENTED, all three gates green** (`COMPILE_GATE_OK` + `REGRESSION_OK` +
  `UI_CAPTURE_OK`, pixels opened not just markers):
  - **Raid VICTORY now settles the army.** `ReconcileAfterRaid` had ONE caller (retreat) and `AddVeterancy`
    had **ZERO repo-wide** — *winning a raid was free*. One latched `RaidDeployController.ReconcileRaidEnd(stars)`
    now serves both exits; 3-star clears pay veterancy.
  - **Healer's Cottage is REACHABLE again.** It lost its `AuthoredPortal` row when the east portal was
    rerouted to `dg_starter_loop`, so the richest dungeon in the game (lore/mini-boss/chests/crafting) was
    dev-overlay-only. Third row added, SOUTH `(20,0,-140)` yaw 352 — the only one of the three seats whose
    ground is provably flat (the WO-468 cave corridor pins Y=0). ⚠ navmesh seating still needs a runtime line.
  - **`[ui-obsidian]` ratchet ARMED** (`HardFailOnNew=true`, 0 NEW) + a **namespace-qualified regex blind spot**
    closed that had been hiding `OutpostHub.cs` as a false "resolved".
  - **waves.json authored schedule is DEAD and now says so** — see the standing-truth line below.
  - Echoes-button safe-area inset 16 -> 54 ref px (~7 dp -> ~24 dp on the Seeker).
- **FPV camera: owner RE-AFFIRMED default-ON** (2026-07-30). `ff.dungeonfpv` stays `defaultOn:true`; the
  `PAIN_POINTS` §4 "keep only if felt-tested" gate is CLOSED. Two `DungeonCameraRig` headers that called FPV
  "a STUB with no independent look" and named over-the-shoulder the default were corrected.
  - **⚠ SUPERSEDED 2026-08-07 (WO-920).** `ff.dungeonfpv` is now **`defaultOn:false`** — the shipped explore
    camera is a **LOCKED over-the-shoulder** rig; FPV survives fully wired as an opt-in A/B (`ff.dungeonfpv=1`).
    The 07-26 default-ON was a workaround chosen *instead of* raising the ceiling; **WO-919 removed that premise**
    (composed rooms are now 4 m walls + a ceiling slab, relit dark), so the trade was reversed.
  - **⚠ AND THE SCOPE OF THAT FLAG WAS ALWAYS NARROWER THAN THIS LINE IMPLIES.** `ff.dungeonfpv` only reaches
    the **two hand-built** dungeon scenes that carry a `DungeonCameraRig` (`Dungeon_HealersCottage`,
    `Dungeon_FolksGranary`). The **composed `dg_*` dungeons and `KayKitChallengeOutpost` bake no camera and no
    rig at all** — their camera is the runtime `GameplayCamera (ensured)` + `DeNelle.Village.SmartMobileCamera`
    (`HeroControlEnsurer` L283-295), so the flag never applied to them. Their locked seat is
    `SmartMobileCamera.ApplyDungeonProfileIfNeeded`. Seat + clear colour for **both** pipelines now come from the
    one authority, `DeNelle.Core.World.DungeonCameraProfile`; the scene test is `HubScenes.IsDungeon`.
- **STANDING TRUTH — `waves.json` `enemies[]` batches are INERT.** `_smartComposition:1` in both live hubs
  means `WaveManager` GENERATES every wave's roster; only `countdownSeconds`, `boss`, `apexBoss` survive.
  **19 waves / 55 batches / 148 authored enemies are discarded every session.** Not a code regression (WO-362
  supersession was deliberate) — the *data* was authored 2026-07-11, ~4 weeks AFTER the batches went inert.
  A once-per-session `FlowTrace.Warn` now names it. **OPEN owner ruling (WO-783 D1):** which authority wins.
- **New WOs:** **783** (this wave) · **784** Echo lanes — canon's "3 of 4 stub" is wrong, **all four** are
  write-only, even Harvest bypasses the Core contract · **785** VFX survivability — **117 of 121** owner-tagged
  VFX rows point into gitignored packs with **no runtime fallback**. **782 RESERVED** (night-wrap capsule
  standee). **WO next-free = 786** *(superseded → banner, 832)*. *(2026-07-30)*

## Latest (2026-07-26) — dungeon+raid felt-test wave + Sunday housekeeping
- **Live anchor = `CANON_GROUND_TRUTH_2026-07-26.md`** (delta over 07-22, which stays the deep module
  reference). Branch `wip/village2-and-f8-tickets`, HEAD `7dec0e07`, **local==origin — this wave IS pushed**
  (change from 07-22 push-HELD). Prod untouched. Save still **v34** (no new persisted fields this wave).
- **Dungeons = functional end-to-end loop** (enter → explore → read lore → fight with REAL win/loss → settle
  → leave → Village). Shipped: WO-770.1 (exit + boss back-door), .2 (correct-dungeon return), .3 (real
  victory/defeat carrier via `SceneRouter.PendingBattle.LastOutcome` — a lost fight ends the run), .3b
  (real-time `BattleArena.OnBattleEnded` → shared `SettleEncounter`; fixes the never-released combat lock),
  .4 (readable lore stones + code-built modal), .7 (toast layer + live Bryn dialogue), .9 (stale-read
  `OnEnable` clear). Plus DungeonHero sole-mover + taller camera + Bryn pill-hide. *(2026-07-26)*
- **Raid loop LOCKED to Teleport/Deploy** (COC model, owner 2026-07-26); walk-to retired as the raid loop
  (its `EnemyOutpost`s may return as a light overworld patrol side-activity). When raid work starts, set
  `ff.overworldencounter=0` + `ff.raidwalk` OFF first. WO-771 v2 is the build plan; **nothing built yet.**
  Reuse `RaidBaseGenerator` + `EnemyFactory→Enemy→TargetManager` combat; `IDamageableStructure` must move
  Village→Core; tower-fire is greenfield. V1 = PvE generated bases (skip the deterministic sim). *(2026-07-26)*
- **Firmed WO set (`docs/qa/`):** 770 (dungeon), 771 (raid v2), 772 (shared enemy system — classes/families/
  armor/weapons + `EnemyResolver`, fixes generic-skeleton bug), 773 (common Obsidian job queue). Validation:
  `docs/qa/dungeon-raid-validation-2026-07-26.md`. **772 is BLOCKED on owner ratifying `docs/enemy-codex.md`**
  (review-and-approve gate) — it blocks 770.11 + 771.13. *(2026-07-26)*
- **Non-dungeon felt fixes shipped:** enemies-out-of-castle + battle-mode BattleLock (`e05f92f7`), towers no
  longer shoot through walls (Structure layer + LoS, `2cb3c40d`), MagentaGuard catches Android compile-failed
  shaders (`386a932f`), loading overlay + standard bar (`4edf8dcc`/`7dec0e07`), gate-traversal teleport off —
  walk through the arch (`8c35332f`), collector buildings get vendor NPCs (`804a02a2`, Lever 1 in progress),
  Alchemy recipe scroll-fix (`8ca95735`). *(2026-07-26)*
- **WO next-free = 774** *(superseded → banner, 832)* (761–773 consumed; 770–773 are decimal-sub-order specs in `docs/qa/`). Ticket table:
  `docs/qa/SUNDAY_STATUS_2026-07-26.md`. §6/§7 catalog-drift housekeeping WO + CS-1 ring/amulet non-persist
  ticket still open. *(2026-07-26)*

## Latest (2026-07-22) — SME fan-out + canon refresh + branch hygiene
- **Live anchor = `CANON_GROUND_TRUTH_2026-07-22.md`** (supersedes 07-19). A 17-agent read-only SME fan-out
  (code-verified) confirmed: **code healthy, gates green** (HEAD `148ab637`, local==origin, `REGRESSION_OK`
  16 suites/0 reds, save v34) — **the real debt is DOCUMENTATION DRIFT.** The `MASTER_CATALOG/<area>` sections
  are weeks stale; the 07-22 anchor carries the **§6 catalog-drift ledger** + **§7 comment-vs-code lies
  registry**. Key corrections: home hub = `Main_Castle_Overworld` (MergedWorld ON, one navmesh) not
  MainCastle_Hall; `ff.atbdungeon` doesn't exist (real gate `ff.dungeonrealtime`, dungeons route into
  BattleArena); 23 build scenes; ~70 catalogs; audio 5-group mixer never built (AudioSource-direct fallback only);
  HeroPortraits folder absent; deploy chain writes `CHAIN_DONE` on failure.
- **Branch hygiene:** 2 stale agent worktrees + local branches removed (dungeon work verified already-merged);
  2 stale remotes purged — `feat/tower-core-loop` (`cea673e4`) + `samantha-village-progress-2025-05-23`
  (`40a570a6`). Remotes now `master` + `wip` only.
- **Real bug surfaced (CS-1):** equipped ring/amulet (`equippedRingId`/`AmuletId`) declared + migrator-seeded
  (v26) but no GameState field / no Snapshot-Apply → **reset on reload.** Needs a ticket.
- **Still queued:** §6 catalog-drift + §7 comment-lie fixes as a housekeeping WO; CS-1 ticket. Push HELD.

## Latest (2026-07-20 overnight autonomous loop) — see `OVERNIGHT_RESULT_2026-07-20.md`
- **Regression baseline = REGRESSION_OK, ALL 16 SME P1 suites GREEN, ZERO reds** (2026-07-20). Added
  WAVES_SCHEMA (EW-3) + PACK_COSMETIC_INTEGRITY (ECON-1) + flipped DUNGEON_DRESSING green (real
  DungeonDresser prop pass). **All 5 audit P1s cleared + guarded.** Full green set: WAVE_SCALING /
  ENEMY_REWARDS / WALL_MITIGATION / UPGRADE_AUTHORITY / PACK_GRANT / SFX_RESOLVE / DUNGEON_EXIT /
  FOUNDING_REACH / FTUE_HONESTY / ECHO_CARD_COPY / SHADER_PIN / MODAL_REGISTRATION / CRYSTAL_PRODUCTION /
  WAVES_SCHEMA / PACK_COSMETIC_INTEGRITY / DUNGEON_DRESSING. *(pushed to wip, origin 1d7512b0)*
- **Composed dungeons now DRESSED:** `DungeonDresser.DressRoom` (Assets/Editor/RoomForge) seats ~8 real
  KayKit props (corner torches + floor barrels/crates) per composed room, wired into `DungeonBaker`
  pre-NavMesh (colliders stripped, doorway clearance). Broader dungeon VFX/lighting/battle dressing = next
  pillar follow, NOT built yet.
- **NEW TOOL — headless UI screenshot capture:** `DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless`
  (edit-mode synchronous render; the old Play-mode path never worked under `-batchmode -quit`). Writes
  `Builds\ui-capture\*.png` + `UI_CAPTURE_OK`. **Run it before shipping any UI change** (owner rule: never
  be first to see a broken panel). It already caught 2 real Echo-card bugs pre-build tonight.
- **Newly data-driven (SSOT):** `buildings.json` crystalsPerWave (v2, CrystalMine yield), `enemies.json`
  xp/coinReward (v4), `walls.json` heartDamageMultiplier. All dual-copy identical + version-bumped.
- **Echo card = 6 NAMED SOULS** (Aldwin/Elowen/Corvin/Bran/Doran/Maren) in `EchoRosterCatalog` — each the
  awakened essence of a soul the Heart guards; founding header "An Echo Awakens" (not "Leveled Up to 1").
  Founding card layout screenshot-verified (full copy fits, 3-across buttons, one dismiss).
- **15 top-band modals** now register with PanelManager (back-button + battle-lock arbiter).
- **Builds:** Seeker APK -> Windows -> WebGL launched detached ~06:28; WebGL DEPLOY pending owner `vercel` CLI.

## Persistence / save
- Save schema **v38** — `SaveSchema.cs:41` → `public const int CurrentVersion = 38;` *(re-verified at source 2026-08-10; the const moved lines, read it off the file)*. **v38 = WO-934 army loadout bank** — `ArmyStorage.loadouts` (3 named composition presets) + `activeLoadout` index; additive on nested Army JSON, `MigrateToV38` EnsureLoadouts for empty slots. History: v29 heroLevel/heroXp/heroLifetimeXp; v30 strategicPlacementMigrated WO-673; v31 echoLanes; v32 freeBuildsUsed; v33 echoLanes `lane:level` token WO-738 — deliberate pass-through; v34 persists Tribes/Wards/Arena + pet active-slot; **v35** `obsidianQueue` — WO-773 multi-channel Builder/Train/Research queue, `MigrateToV35` folds legacy buildJobs/pendingBuilds/buildingCooldowns into the Builder channel, idempotent; **v36** WO-834 `everBuiltStructureIds` (the blank-town baked standdown); **v37** WO-911 M2 **the per-job PAID BASKET** — `paidWood/paidFood/paidIron/paidCrystals/paidMagic` on `BuildJobData`, the precondition for the owner's Q1 ruling that **cancel refunds 100% of what was paid, flat**; ⚠ **a pre-v37 job refunds ZERO and says so.** Every bump carries a `SaveMigrator` step so the CORE_SAVE version-triple oracle stays green.
- **Persisted:** BaseLayout, Zones, PartyMemberIds, ArenaDefense, PetName, Settlements. **NOT persisted (truthful red oracles):** Tribes, Wards, Arena W-L record, pet active-slot map, broken-tower state. *(2026-07-12)*
- Local save = PlayerPrefs `dotr-save`, signed (LB-3 HMAC, tamper-rejected); server save/load nonce-auth is built but `BackendAuthConfig.Enforced` = **OFF**. *(2026-07-12)*

## Data catalogs
- **Dual-copy rule: `Resources/Data/Canonical` WINS at runtime** over StreamingAssets. `DATAWEB` oracle enforces content sync. *(2026-07-12)*
- **Gear ruling:** the SMALL curated set is deliberate ("only a few prefabs — nothing decent to use yet") → **Resources is truth for weapons/armor**; sync Resources → StreamingAssets. The 433-weapon StreamingAssets copy is the stale side. *(owner 2026-07-12)*
- Drifted pairs found (sync pending): weapons, armor, daily-quests, skin, stake-rewards, tower-perks. *(2026-07-12)*
- The "six StreamingAssets-only WebGL-broken catalogs" are **already mirrored** (that risk-ledger line is stale). *(2026-07-12)*
- **Echo model (WO-738, owner Path-B ruling):** 6 collectible spirits (identity in the `EchoRosterCatalog` CODE TABLE — no ScriptableObjects, WebGL-safety ruling), balance in `echoes-balance.json` (dual-copy). Each echo has element + level (max 8) + one assigned functional lane (Harvest/Crafting/Defense/Exploration). `EchoBonusCalculator` is the single math source (economy + UI + `EchoSpecializationRegression` oracle all read it). Echoes NEVER fight: Defense = passive offline city-raid bonus, Exploration = dungeons-only — both STUBBED (write to Core `EchoLaneBonuses`, hosts read when they land); **Harvest + Crafting are the felt-now lanes.** Picker reachable via roster-card tap (the wisp-injector path is dead). *(2026-07-17)*

## Backend / web
- **`api/` lives IN THIS REPO and is git-TRACKED** (not gitignored, not a separate React repo). Deploys ride any `vercel deploy` run from the repo root. *(2026-07-12)*
- WebTrace: `?trace=1` → `POST /api/trace` → Neon **`analytics_events`** (`event_name='web_trace'`; no separate web_traces table). **CLI read path = the `[sig]` echo in Vercel runtime logs** (`DATABASE_URL` is sensitive/unpullable). *(proven 2026-07-12)*
- **No TTL cron exists** for trace rows (security H1 — fix pending). Open POSTs (trace/track/bug-report) have **no rate limit**. *(audit 2026-07-12)*
- db-viewer: `tools/db-viewer/index.html` + `api/admin/db.js`, key = `ADMIN_DASH_KEY` (Vercel env, set + redeploy to activate). *(2026-07-12)*

## Web triage — the read path (LIVE as of 2026-07-15; it never was before)
- **`ADMIN_DASH_KEY` is now SET** (Vercel env, preview+production; value in gitignored `.admin-dash-key`).
  It had NEVER been set, so `tools/db-viewer` + the `/triage-web-issue` skill were **dark since written**
  — 70,053 `analytics_events` rows accumulated unread. Endpoint verified live. *(2026-07-15)*
- ⚠ **`vercel logs` CANNOT give you the `[sig]` lines** — proven: even `--json` returns exactly ONE
  message per request (the summary `[web_trace] sess=… lines=N signal=N`); the per-line
  `[sig]` echoes from `api/trace.js:67` are never surfaced. The canon read-path "the `[sig]` echo in
  Vercel runtime logs" gets you `signal=18` but **not the 18 lines**. **Real read path = the admin
  endpoint** → `api/admin/db.js?view=traces` (sessions) → `&session=<id>&order=asc&limit=50` (the
  scene-load HEAD, where TERRAINDIAG / MagentaGuard / catalog resolution live). Header
  `x-admin-key`; base rotates per deploy → `Builds\admin-preview-url.txt`. *(2026-07-15)*
- **`order=asc` + `offset` + `total_batches`/`has_more` added** to the traces view. It was
  `DESC LIMIT 20` with no offset, so long sessions (one ran 2840 batches / 153k lines) were readable
  only from the TAIL = gameplay spam; the diagnostic head was structurally unreachable. *(2026-07-15)*
- **WEB F8 WATCHER LIVE:** `.claude/skills/run-defenders/websig-watch-start.ps1` polls the trace DB and
  emits into the SAME `logs/f8-inbox` with the same PING seq contract, so `f8-check-inbox.ps1` covers
  desktop AND web. Start it alongside `f8-watch-start.ps1`. Proven against the known-bad 07-15 session:
  29 signal hits, fires on the MAGENTA line. *(2026-07-15)*
- **Sessions are attributable from 2026-07-15:** `WebTrace._buildId` = `<version>@<host>` (was
  `Application.version` = **"1.0" for every build**, so a magenta preview and healthy prod were
  indistinguishable). Needs a WebGL rebuild to reach players. *(2026-07-15)*

## Ground / terrain (RCA 2026-07-15 — the magenta ground)
- **The visible ground of `Main_Castle_Overworld` is the `ExteriorTerrain` Terrain**, NOT the courtyard
  tiles (those are dropped to Y=-0.5 + hidden by GroundZFightFixer). It binds its material BY GUID at
  `Main_Castle_Overworld.unity:16016` -> `0eb083914b7ffae4eaf721e2353fea0b` =
  `Assets/Generated/Terrain/ExteriorTerrainMaterial.mat`. *(2026-07-15)*
- **That .mat was NEVER IN GIT** (`git log --all -- <it>` = empty) — `Assets/Generated/` was ignored, so it
  only ever existed on the machine that ran the terrain bake (laptop, baked 05-31). Its siblings
  (ExteriorTerrainData + the 5 .terrainlayers) WERE tracked, committed before the ignore rule → the ground
  was **walkable but MAGENTA** on every other machine. **FIXED:** original recovered from the laptop share +
  `Assets/Generated/Terrain/` is now TRACKED (the whole bake folder). *(2026-07-15)*
- **MagentaGuard could not save it:** `MagentaGuard.cs` gated terrain recovery on `tm != null &&
  IsBrokenShader(...)` — a NULL material short-circuited it, so the fix never fired (the FloorDiag line
  reads `mat='<NULL>' ... broken=False`). Now treats a null materialTemplate AS the break. *(2026-07-15)*
- **The web build is NOT blind — read the live trace, don't hunt a desktop log** (a CLI got this wrong
  2026-07-15 and wrote a bogus WO; the answer was already in START_HERE §3). `FlowTrace.cs:28` defaults
  `Enabled = isEditor || isDebugBuild` **on purpose** (PII: hot-path lines carry wallet ids / save-blob
  lengths / roster), but `WebTrace.cs:162` sets `FlowTrace.Enabled = true` when web tracing activates, and
  BOTH its gates are already open: `FeatureFlags.cs:117` `WebTrace => Get("webtrace", defaultOn: true)` and
  `WebTrace.cs:63` `TraceEndpoint = https://defenders-of-the-realm-v2.vercel.app/api/trace`. So a live web
  session streams `[Flow:*]` to Neon `analytics_events`; **CLI read path = the `[sig]` echo in Vercel runtime
  logs**. ⚠ `WebTrace.cs:11-15`'s header still says "DORMANT BY DEFAULT / default OFF" — **the comment LIES**
  vs `defaultOn: true` (classic: verify from code). *(2026-07-15)*
- Minor, unfixed: `FloorDeepDiag.cs:32` is hard-scoped to `TargetScene = "MainCastle_Hall"`, so it never runs
  in the live merged world `Main_Castle_Overworld`. MagentaGuard's own FloorDiag dump is what actually fires. *(2026-07-15)*
- ⚠ **`/Assets/Resources/Structures/` is gitignored** (`.gitignore:121`) — only **4** models are tracked
  (ArcaneSpire_1/2/3, WizardTower_1); the other ~37 arrive ONLY by manual LAN copy from the laptop.
  **This is DELIBERATE and stays** (owner ruling 2026-07-15): there are exactly **two machines** (this
  desktop + `Kayden-Laptop`, share `\\<ip>\EoA`, user `Kayden-Laptop`) — no CI, no fresh clones, so the
  big-art-out-of-git policy holds and LFS is not worth it.
  **The real risk is TWO-MACHINE DRIFT, and it is not theoretical — it caused BOTH 07-15 bugs:** the
  terrain material existed only on the laptop (magenta ground), and the 22:00 exe was cut 45 min BEFORE
  the 22:45 art copy, shipping 4 of 41 models as placeholders while the build reported SUCCESS.
  **Mitigation = make drift LOUD, not tracked:** a pre-build oracle that fails when
  `structures-catalog.json` `visualPrefabPath` keys do not resolve on disk. Proposed 07-15, owner's call. *(2026-07-15)*

## Builds
- **APK VERSION STAMPING (2026-08-05):** every Android build now stamps a **monotonic
  `AndroidBundleVersionCode`** (minutes since 2026-01-01 UTC — stateless, no counter file to drift
  across the two machines) plus a readable `bundleVersion` (`2026.08.05.312200`) via
  `AndroidBuild.ApplyVersionStamp`. **Before this both were frozen at `1.0` / `1` forever**, so
  (a) Firebase App Distribution folded EVERY tester build into one release — the upload literally
  replies *"re-uploaded already existing release 1.0 (1)"* and testers cannot tell builds apart —
  and (b) `Application.version`, which feeds `WebTrace._buildId` and the bug-report `app_version`
  column, was the constant `"1.0"` that made a magenta preview and a healthy prod indistinguishable
  in the trace DB (the 2026-07-15 incident). Android also refuses an install whose versionCode goes
  backwards, so the frozen code was a latent update failure.
- **Firebase = APK DISTRIBUTION ONLY (owner ruling 2026-08-05):** *"only for storing the APK not
  changing from Neon."* Neon `/api/game/save` remains the save backend; **no Firestore migration**,
  and **no re-adding email/Google/phone auth** (Android ships wallet-first per WO-837/847). The
  Firebase console's "Add Firebase to your Android app" wizard shows the **Android Studio** path —
  its `com.google.gms.google-services` Gradle plugin + `firebase-bom` snippets must **NEVER** be
  applied here: this is Unity (`mainTemplate.gradle` is `com.android.library`, Groovy, template
  tokens), the Firebase **Unity** SDK pre-generates
  `Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` instead, and the
  dependency block between `// Android Resolver Dependencies Start/End` is EDM4U-generated and
  overwritten on every resolve. Installed SDK = **13.14.0** (`firebase-app-unity`/`firebase-auth-unity`);
  any added Firebase package MUST match that version.
- **PROD (current) = the 2026-08-05 build** — deployment `dpl_9vGadbKyPrQ55HR3PaUT53i9CNUh`,
  `https://defenders-of-the-realm-v2-ly1ih48m3.vercel.app`, `target: production`, deployed
  **2026-08-05T23:37Z**, commit `8fdb29a5`, serving `defenders-of-the-realm-v2.vercel.app`.
  *(verified 2026-08-10 against the LIVE Vercel deployment record, not against a doc)*
  - ⚠ **This line used to read *"PROD (current) = the 07-16 six-fix build `q2v5vj86g`, promoted
    2026-07-16"* — STALE BY THREE PRODUCTION DEPLOYMENTS.** The record shows `target: production`
    deploys on **2026-08-03T22:50Z**, **2026-08-04T19:33Z** and **2026-08-05T23:37Z**. A seat trusting
    the old line would have believed prod was three weeks of code behind where it actually is — which
    is exactly the error that kept the "prod runs OLD `api/`" claim alive below (see `docs/HANDOVER.md`
    and the 08-09 anchor's correction notes). **Read the deployment record, never this line, when the
    answer has to be current.**
  - **Rollback target = `Builds/PROD_ROLLBACK.txt`** — ⚠ this doc referenced that file **for weeks
    while it did not exist on disk**; it was finally written 2026-08-10. A referenced rollback target
    that was never written is the same as having no way back, and nothing in the pipeline was checking.
    **STANDING RULE: overwrite `Builds/PROD_ROLLBACK.txt` with the OUTGOING prod deployment id BEFORE
    every promotion.** Recorded after the fact, it points at the thing you are trying to escape.
- **2026-08-01 release train:** fresh desktop exe · Seeker APK v-wave installed on-device + Firebase App
  Distribution (testers) · WebGL→Vercel preview refreshed. Screenshot archive
  `Builds\ui-capture-archive\2026-08-01\`.
- **Web-build self-test = `tools/webbot/`** (Playwright): `webbot.js` drives the DEPLOYED build for
  screenshots + live browser-console `[Flow:*]` capture + a drag-pan engage check; `introtest.js`
  clicks Play Intro. CAVEAT: synthetic clicks do NOT reliably fire Unity uGUI buttons in WebGL — the
  bot verifies boot/render/console + asset serving, but button-driven flows (Play Intro, into Build
  mode) need owner felt-test or the codec/HTTP determinants. Pass the SSO bypass param in the URL. *(2026-07-16)*
- **Intro video plays on web** (owner Q 2026-07-16): determinants all green — `StreamingAssets/Video/
  Defenders.mp4` serves 200 (`video/mp4`, 4MB), codec = **H.264 avc1 + AAC mp4a** (browser-compatible;
  Unity copies StreamingAssets raw so codec IS the web risk), `IntroSequencePlayer` uses VideoPlayer
  **URL source** (not VideoClip) with the WebGL `audioOutputMode=Direct` fix. *(2026-07-16)*
- **Ship WebGL = `BuildOptions.None`** (Development is opt-in `-DevBuild` — NEVER deploy a DevBuild: Development players paint the full-screen error overlay). *(verified WebGLBuild.cs:124 / DesktopBuild.cs:178, 2026-07-12)*
  - ✅ **CLOSED 2026-08-08 (`374ccd26`):** the long-standing "desktop release still ships Development"
    open item is DONE — the desktop player now builds RELEASE, verified by `DeNelle.DevTools.dll` being
    **absent** (206 DLLs, was 207). ⚠ Paired trap from the same commit: `ff.devresourcetool`'s default
    flip to OFF **changes nothing on a machine that already has `ff.devresourcetool=1` in PlayerPrefs** —
    `FeatureFlags.Get` reads PlayerPrefs FIRST. *(2026-08-08)*
- Deploy chain: **prefer `overnight-webgl-deploy.ps1`.** Promotion + push stay the owner's call.
  *(corrected 2026-08-10 — this line used to read "`webgl-vercel-overnight.ps1` detached; markers +
  `DEPLOY_URL` in `Builds/webgl-chain-status.txt`. Preview only", which mixed two scripts into one
  procedure that no single script implements.)*
  - **The two scripts disagree and the markers belong to the OLDER one.** `overnight-webgl-deploy.ps1`
    writes **`Builds\overnight-chain-status.txt`**; the markers canon quotes (`CHAIN_START`,
    `WEBGL_BUILD_OK`, `DEPLOY_URL`, `CHAIN_DONE`) are `webgl-vercel-overnight.ps1`'s. Grepping for a
    marker in the file the other script writes finds nothing and reads as "the chain never ran".
  - `webgl-vercel-overnight.ps1` calls bare **`vercel deploy --yes` with NO token and NO scope**, so it
    fails unless the CLI is already interactively authed — which a detached/overnight run is not.
  - ⚠ **TRAP — never `cd Builds\WebGL` and deploy from there.** That folder carries its own
    `.vercel/project.json` pointing at a **DIFFERENT Vercel project** (`defenders-webgl`,
    `prj_ox8fqdHbD7lkrKEyxy0dtQAjphGc`) than the repo root
    (`defenders-of-the-realm-v2`, `prj_qUmuwr8BN492oZH8yRuvPZMN3e0J`). Deploy from the **repo root** so
    the link resolves to the real project. *(The stray file only exists when `Builds/WebGL/` has been
    built + linked; `Builds/WebGL/` is absent from disk right now, so the trap is dormant, not gone.)*
- Fleet baseline: DataRegression = **REGRESSION_OK, 0 reds** — all 5 long-standing reds fixed 2026-07-19 (R1 arena ground texture, R2 dual-wallet Grant->GameState, R3 pet active-slot persist, R4 core-save Tribes/Wards/Arena persist, R5 orc-raider SSOT enemies.json Hp 130). *(2026-07-19)*; re-certified 2026-08-01 with UI_CAPTURE_OK 23 (103 checks).

## UI / MVVM (WO-744 — DONE 2026-07-18)
- **Strict MVVM across the whole game:** every panel View binds an `IPanelViewModel` and reads NO
  game state at runtime; all state/logic lives in the VM (`CreateDefault` is the sole resolution
  site). All 36 audit panel Views migrated (silos B/C/D/E/F/G) + the landmines: BattleHudUgui behind
  `ff.battlehudvm` (default OFF; ATB feel-sim untouched), DialogueView with the WO-702 truce relocated.
  Spec: `docs/UI_MVVM_MIGRATION_PLAN.md`. *(2026-07-18)*
- **The ratchet is ARMED:** `UiMvvmConformanceRegression` runs in `DataRegression` as `[ui-mvvm]`
  with `HardFailOnNew=true` + an EMPTY baseline — any NEW View that reads game state (EconomyService/
  GameStateService/Find*Type/gameplay catalogs) HARD-FAILS the gate. Non-panel offenders (flow
  controllers, spawners, benign EventSystem/sibling finds, HUD wiring) are allowlisted with reasons. *(2026-07-18)*
- Shared VM seams: `Core.UI.Mvvm.WalletVM` (DTO) + `LiveWalletSource`, `GearIconCatalog` (icon leak),
  promoted `Core.UI.Mvvm.CraftRecipeVM`, `ArenaPaletteVM`, `StructureCardVM`/`PlacedTowerListVM`. *(2026-07-18)*

## Room Forge (WO-740–745 — DONE 2026-07-18)
- Socketed-room dungeon pipeline merged to mainline: 17 default room prefabs + shared KayKit
  materials; JSON compose layouts (`Assets/**/dungeon-layouts/`, dual-copy + `version`); the demo
  bakes clean (`matesOk=2 matesFail=0`, NavMesh `PathComplete`); `RoomForgeRegression` (`[room-forge]`,
  10 cases) + `[Flow:DungeonBake]` + baker hard-gate/re-verify fixes. Editor menus under
  `Defenders/Dungeon/*`. KayKit atlas stays machine-local (big-art-out-of-git). *(2026-07-18)*

## UI / input
- ASCII-only TMP strings (non-ASCII glyphs = tofu □ on device); never meaning by color alone (owner red/green colorblind). HUDUI oracle locks the tofu class. *(2026-07-12)*
- Build-mode touch: uGUI verb bar + PLACE + kit d-pad (publishes `HudMoveInput` → merged with arrow-key read). GhostPreview moves its CHILD visual — probe via `GhostPreview.CurrentPosition`, never the host transform. *(2026-07-12)*
- **Right ActionBar = Attack + Q/W/E/R named skills:** Sword Wielding / Sword Heroic / Shield Charge / Warden's Grace / Radiant Strike. **Mobile HUD shows NO key-letters** (WO-750 SPEC). *(2026-07-19)*
- ⚠ **CORRECTED 2026-08-06 — this line was two rulings stale.** It read "default **4m**, tower override
  **7m**, siege override **3m**". WO-764 replaced the per-class metre overrides with a **`heightMul`
  multiplier** on a 4 m base (tower 1.25 = 5.0 m), and the 2026-08-05 owner cadence ruling moved it again.
  **Live values:** base **4 m** × `heightMul` — **1.25** landmark · **1.2** towers (4.8 m) · **1.0**
  building base · **0.75** siege · **0.35** decoration; recorded in `structures-catalog.json` as
  `_heightCadence`. Walls stay at 1.0 **deliberately**. `repo.visualHeight` is dead. The Y-height audit tool
  (`StructureHeightAudit`) is still the way to print `measuredY` per prefab. *(WO-751 → WO-764 → 2026-08-05)*
- **Destroyed items = NO rebuild + full-cost + VFX cleanup** via a new `Destructible` component (WO-753 in progress). *(2026-07-19)*
- **Headless UI-screenshot pass must run before builds** (felt-test-wave standing rule). *(2026-07-19)*

## Echo canon
- **Echo = the essence of a person the tree of Elarion guards** — 6 named people: Aldwin, Elowen, Corvin, Bran, Doran, Maren. Feeds the WO-752 founding-card overhaul + post-tutorial interjection (SPEC + creative sign-off, awaiting copy). Balance/lane model unchanged (see the Echo model line under Data catalogs). *(2026-07-19)*

## Process
- Boot: **START_HERE.md** routes everything; SAMANTHA.md = the confirmation gate; PREFLIGHT_GATE A/B/C.
- Phone/async triage: `/triage-web-issue` skill — pull the web-trace from the db (`api/admin/db.js`, `X-Admin-Key`=`ADMIN_DASH_KEY`), RCA from the proving line, write the WO left READY for the Windows machine. *(2026-07-12)*
- WO numbering: mint from the `CLI_LANES_WO_NUMBERS.md` banner (**832** as of 2026-08-01; NEVER copy the number — the banner is the only authority; historical: 761–773 consumed — 762 builder-queue, 763 Wisdom, 764 hub-Y-height, 765 capture-Default-Town, 766 Seeker wallet, 767 texture caps, 768 thin-client, 769 Firebase auth, 770 dungeon, 771 raid, 772 enemy, 773 Obsidian queue; earlier: 739-753 consumed — 750 Right-ActionBar naming SPEC, 751 Y-height normalization DONE, 752 Echo founding-card SPEC, 753 Destructible IN PROGRESS; Grok-03 here→there = **716–722** + **715** VFX; see `docs/UI/Grok-03-here-to-there-WO-program.md`), bump in the same edit. ⚠ UI-seat mints in the old 674–685 space collide — translation table in the banner; owner syncing the UI seat 07-13. Collisions resolved 2026-07-13: 677–681 duplicate specs renumbered to 688–692, 682/683/685 dupes to 695/693/694; a fresh 07-13 mint colliding with the 684 board renumbered to **696** (repair-before-upgrade context). *(2026-07-13)*
- Outstanding board: `WorkOrders/WORK_ORDER_684_outstanding_items_board.md` (exact asks + steps).
- ✅ Apex dragon model = **SWAP LANDED 2026-07-24 (WO-760)** — the licensed Asset-Store dragon (product 71047 "Dragon Animated", WDallgraphics; source `Assets/Dragon/`, now git-tracked, not gitignored) ships as `Resources/Enemies/Boss_Dragon.prefab`, built by `DragonAnimatorSetup` + force-tracked `Assets/Generated/Animators/SyndrathDragon.controller`. Old CC-BY-NC 3DHaupt `Dragon.fbx`/2 controllers/materials + the orphan `Prefabs/Village/Generated/Boss_Dragon.prefab` git-rm'd; unlicensed `RedDragon 1.2` stray deleted; `EnemyFactory` dragon keys repointed to `Boss_Dragon`. ⚠ **The earlier "RESOLVED 2026-07-23" claim was PREMATURE** — that commit only repointed comments; the CC-BY-NC model still SHIPPED (Resources includes unused assets) until the 07-24 builder-run + git-rm. Commercial-ship blocker now ACTUALLY cleared; boss "Syndrath the Devourer" retained; fly-in->land->burn-towers->retarget-Tree behavior built (WO-760, felt-verify pending).
