# Google Play readiness audit - 2026-08-30

Evidence: the REAL artifact Builds/Android/EchoesOfElarion-GooglePlay.aab (517,307,020 bytes,
built 14:49) - merged manifest, global-metadata.dat, dependencies.pb.

## RED #1 - THE AAB CANNOT BE UPLOADED AT ALL
493 MiB against Play 200 MB limit. Base module compressed 491.96 MiB; uncompressed 1,152 MiB of
which base/assets/bin/Data alone is 1,002 MiB. No Play Asset Delivery.
ProjectSettings.asset:189 androidSplitApplicationBinary: 0. THE CONSOLE REFUSES THE UPLOAD.
This outranks every other finding including the crypto contamination. Fix = Split Application
Binary -> PAD install-time pack, 1-2 days, own lane, own headless verify (changes how the player
loads bin/Data at boot and touches the Addressables catalog path).

## BLOCKS RELEASE
#2 artifact crypto-dirty: global-metadata.dat holds 76x solana, 35x Skr, 12x Jupiter, the literal
   "SKR is a REAL Solana / Seeker token", "Stake natively at Stake.solanamobile", and the USDC mint.
#3 CurrencySkinResolver.cs:282-288 force-resolves the SKR skin on EVERY Android build - ABOVE the
   skin.json read, so editing skin.json cannot fix it. Yields $SKR, "Spend $SKR", SolanaWallet.
#4 SettingsController.cs:252-260 has an UNCONDITIONAL Wallet section with Connect/Disconnect,
   not skin-gated, and DEAD in a Play build - fails crypto AND broken-functionality policy.
#5 Privacy + Terms linked in-app are crypto-framed (site/terms.html:52,152,215; privacy.html:60).
#9 no IN-APP account deletion route (web-only). Play requires both once accounts ship.

## BLOCKS MONETISATION - one ordered chain
#8 identity (no client caller for api/auth/google-session.js) -> #6 entitlement writer
(PackGrantBridge.RegisterApplier has ONE caller, in DeNelle.Wallet, which is !GOOGLE_PLAY) ->
#7 store UI + restore (PackStore/PackStoreVM/PurchaseGate are ABSENT from the AAB - there is NO
buy surface in a Play build at all; Play REQUIRES restore) -> #10 promo reflection.

## RISK
#16 the AABs catalog was NEVER PROVEN PUSHED: r2-parity verified catalog 348233 (the Seeker APK)
    while the AAB is stamped 348218. A reviewer resolving nothing sees capsule enemies and
    placeholder buildings with NO error -> Minimum Functionality rejection.
#11 Play App Signing re-signs with a Google key on the SAME package id as the live dApp-Store
    build - the two become mutually un-installable. OWNER DECISION, cheap now, expensive later.
#12 android.permission.DUMP (androidx.work) - the "excessive permissions" signal already raised
    once by a publisher. #13 FOREGROUND_SERVICE with NO foregroundServiceType.
#14 AD_ID is live and ads DO init (RewardedAdSkip defaults ON) - Data Safety must declare it.
#15 LevelPlayInitializer.cs:39-41 states the OPPOSITE of the code ("defaults OFF").
#17 Arena SKR wager loop. #18 a DEVELOPMENT Play build will not compile. #19 preferExternal.
#20 site/admin.html wallet dashboard on the domain the Play listing will link.

## VERIFIED GREEN
targetSdk 36 pinned (meets the 31 Aug 2026 floor by ONE DAY); minSdk 26, ARM64, IL2CPP;
Play Billing Library 8.0.0 (meets the v8 floor, also one day); versionCode monotonic and legal;
MWA androidlib GENUINELY EXCLUDED - no com/denelle/defenders/mwa/ in any dex, wallets.json absent
(WO-1282 Lane B worked); permission surface otherwise clean; ad consent ordering correct;
wallet really is compiled out.

## TWO THINGS THAT CHANGE THE GATE
1. The two "unsatisfiable" tokens are the LEAST of it. The scan is ALSO blind to SKR, wallet,
   usdc, nft, blockchain, crypto, stake, web3, seeker. Narrowing phantom/mwa WITHOUT widening the
   list makes the gate go green on an artifact still shipping "SKR is a REAL Solana token".
   Widen and narrow in the SAME edit - and the list is duplicated in
   tools/android/assert-google-play-aab-clean.ps1:18 (section 16 drift).
2. Core/Platform/Generated/StakeRewardsFallbackData.g.cs BAKES stake-rewards.json into C#.
   Quarantining the JSON achieves NOTHING - the content is already in global-metadata.dat.
   Any file-moving exclusion approach misses this whole class of leak.

## OWNER-ONLY
CLOSED TESTING, 12 testers x 14 CONTINUOUS days, if the Play account is personal and created
after 13 Nov 2023 - CHECK THIS FIRST, it is a 2-week calendar dependency gating everything.
Plus IARC rating, Data Safety, ads declaration, privacy URL, financial-features declaration,
Play Console service account (GOOGLE_PLAY_SERVICE_ACCOUNT_JSON absent), Play-sized screenshots.
Opportunity: LevelPlay is stuck Temporary for want of an https listing URL - a Play listing
supplies exactly that. Get verified for live inventory once listed.

## ORDER - if only one thing starts, start #1
Wave 0 parallel: #1 size | #12/#13/#19 manifest | #3->#4->#17 copy | #15 docs.
Wave 1: #2 completion (rebuild + AssertBuiltArtifact, widen+narrow tokens together) | #16 | #18.
Wave 2 ordered: #8 -> #6 -> #7 -> #10, with #9 in the SAME release as sign-in.
Wave 3 owner: listing, forms, ratings, #11.

## STATED UNCERTAINTIES
Whether #13 faults at runtime vs merely being unjustified; whether #9 binds a build with no
account creation; whether R2 asset bundles read as post-install code delivery (probably not under
IL2CPP - no dex/JAR/.so - but reasonably rather than certainly confident). #17 and #20 were
agent-sourced and not re-opened at source.
