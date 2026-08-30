# SESSION STATE 2026-08-30 (CLI seat) — IN-FLIGHT WORK

Written at ~96% context. If this session resets, START HERE.

## FOUR AGENTS WERE RUNNING when this was written
Their edits land in the WORKING TREE, uncommitted and UNGATED. If the tree has
changes you did not make, they are probably from these. Gate before trusting any of it.

1. **Google Play readiness SME audit** (read-only) — comprehensive audit of everything
   blocking a Play release. Owner directive: audit, then systematically close every item,
   only report back when complete.
2. **UniTask migration scoping** (read-only) — the AAB long pole. KEY QUESTION IT WAS
   ASKED: removing `com.solana.unity_sdk` from `Packages/manifest.json` removes it from
   the SEEKER build too, which must not happen. So "remove the package" may be the wrong
   frame entirely; the options may be a pre-build manifest swap, a separate Play project,
   or changing what the gate expects.
3. **Battle quiescence softlock** (edits) — device capture: after an ARENA WIN,
   `timeScale` left at 0.04 and battle-lock still HELD by `PursuitBattleProbe.Probe` and
   `BattleArena.<Awake>b__84_0`. Player-facing softlock. REGRESSION of the 2026-08-20
   leaked hit-stop. Told to make the failure SELF-HEAL, not just log.
4. **Two orphaned screens** (edits) — owner reports NO PATH to (a) the SKILL TREE (hero
   talents — she called this "a huge issue") and (b) the DEFENSIVE/BUILDING UPGRADE
   screen. Hypothesis to prove: CLAUDE.md §7's re-point of the `Upgrade` bar face to the
   unified Manage screen orphaned both. Door-level regressions required for both,
   modelled on `ManageTroopsTrainDoorRegression`.

## OWNER RULINGS TODAY — do not re-litigate
- Play pricing: SAME SKU ladder, SAME prices, 30% accepted ("30% when now i get 0 is fine").
- Identity: guest by default, Google sign-in required at FIRST PURCHASE.
- Guest/cloud collision: FLUSH guest state to DB first, THEN offer the choice, never
  overwrite the loser.
- Wallet-only identity regression applies to the APK, NOT the AAB — scope it
  architecturally, do NOT amend the guard.
- PIN-3 answered: TowerSwapService/TowerSwapMenu deleted as dead code.
- ⛔ THE SHIELD IS CORRECT AND IS HANDS-OFF. `offsets.json` is at HEAD. I overwrote her
  hand-dialled values twice today; do not touch them again without an explicit request.

## OPEN, NEEDS THE OWNER
- Two `ForbiddenArtifactTokens` are UNSATISFIABLE: `phantom` matches
  `java.lang.ref.PhantomReference` (every Android dex) and `mwa/` matches base64 in
  ad-SDK strings. The gate has NO passing state until they are made precise. Same list is
  duplicated in `tools/android/assert-google-play-aab-clean.ps1:18` — change both or drift.
- `ANALYTICS_EXCLUDED_PLAYER_IDS` is UNSET, so the owner's own play counts as player
  retention in every Command Center figure.
- Command Center is IMPLEMENTED not DONE — needs a phone-width capture (PO felt-verify).

## KNOWN AND RECORDED, NOT FIXED
- AAB is `PLAY_ARTIFACT_DIRTY` — the `com.solana.unity_sdk` UPM package. Do NOT upload it.
- A Play build has NO ENTITLEMENT WRITER: `PackStoreVM.ApplyPackContents` lives in
  `DeNelle.Wallet`, which compiles out under `GOOGLE_PLAY`. Settlement now fails closed
  and loud rather than silently. Follow-up: move `PackStoreVM` to `DeNelle.Commerce`, and
  note `TownBankCapRegression.cs:335` hardcodes its path and DEGRADES TO A SKIP if it moves.
- `ClaimableCamp.cs:553` calls `VerifyNodeRenders` SYNCHRONOUSLY, but the visual is built
  in `MineNodeVisual.Start()` — it can only ever observe the pre-build state. That is why a
  node lying on its side passed a check whose whole job is verifying the node.
- No `session_end` event, so Command Center's average-online-time is an ESTIMATE and says so.
- `packs.store_visible` exists in Neon and NOTHING reads it — a "push SKU" button would
  flip a value no player can see.

## MY OWN ERRORS TODAY, corrected in the WOs — do not re-inherit them
- I blamed `mwa/` in the AAB on the Mobile Wallet Adapter. FALSE — Lane B worked.
- I claimed Read/Write was the shield's editor-vs-device divergence. It was not.
- I committed ~30 files of another seat's in-flight work via a directory-level `git add`
  (commit 459b8edd, disclosed in its own message). Commit BY EXPLICIT PATH.
