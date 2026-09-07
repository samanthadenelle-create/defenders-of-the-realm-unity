# WO-1502: the documented backend test command does not run, game/load and auth/nonce have no tests, 23 endpoints are untested

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:07:35, build 2026.09.07.359076). PRIOR STATUS: FIXED - 2026-09-06: package.json scripts.test = node --test test/*.test.js; test/game.load.test.js (11) + test/auth.nonce.budget.test.js; four env vars documented in ACCESS_AND_SECRETS section 2A (the two Google Play secrets belong in section 2, owner to confirm); still ZERO gates run node --test
**Silo:** `api/` tests + `package.json` + `docs/ACCESS_AND_SECRETS.md` + the runbooks.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1502 -> 1503 in the same edit).

## 1. EVIDENCE

The documented command fails outright on Node 24:

```
node --test test/      ->   Cannot find module 'D:\eoa\test'
node --test test/*.test.js   ->   works: 345 tests, 342 pass, 3 fail (all builders-hour, WO-1449)
```

`package.json` has NO `scripts` block at all, so there is no `npm test` either. Every runbook that says
`node --test test/` has been un-runnable.

Coverage and configuration gaps found in the same pass:

```
api/game/load.js       no tests            api/auth/nonce.js   no tests      23 endpoints untested
showcase / leaderboard / profile endpoints      no CORS wrapper
SOLANA_MAINNET_RPC_URL   no fallback - unset SILENTLY disables mainnet verification
undocumented env vars:  COMMUNITY_SHOWCASE_VOTING_ENABLED, INSTALL_BRAG_CRYSTALS, SOLANA_RPC_URL,
                        GOOGLE_PLAY_ACCOUNT_BINDING_KEY, GOOGLE_PLAY_PACKAGE_NAME, SOLANA_DEVNET_SKR_MINT
                        -- absent from every .md; docs/ACCESS_AND_SECRETS.md is the canonical home
```

## 2. FIX SHAPE

- Add `scripts.test` to `package.json` with the WORKING invocation; fix every runbook that quotes the broken
  one, in the same commit.
- Tests for `game/load` and `auth/nonce` FIRST - load is the path WO-1447 shows is dropping the whole town,
  nonce is the path WO-1452 shows defeats the session cap. Then work down the 23.
- Add the CORS wrapper to showcase / leaderboard / profile.
- Make an unset `SOLANA_MAINNET_RPC_URL` FAIL LOUDLY rather than silently disabling mainnet verification.
- Document all six env vars in `docs/ACCESS_AND_SECRETS.md` - one authoritative list, not per-file comments.

## 3. WHAT NOT TO DO
- Do not add a `test/index.js` shim to make the broken command work. Fix the command.

## 4. ACCEPTANCE
- [ ] `npm test` runs the suite; the runbooks quote the same command.
- [ ] `game/load` and `auth/nonce` covered; the untested-endpoint count stated before and after.
- [ ] Unset mainnet RPC URL fails loudly; proven.
- [ ] All six env vars in `ACCESS_AND_SECRETS.md`.
- [ ] `node --test` green across `test/` (the builders-hour failures close under WO-1449).
