// =============================================================================
// api/promo/redeem.js — Vercel Serverless Function
// -----------------------------------------------------------------------------
// Redeems an operator-issued promo code. Reads the code catalog (promo_codes),
// enforces the gates, then records the redemption (promo_redemptions) and
// returns the reward.
//
// IDENTITY-GATED (security audit 2026-08-15). playerId used to be taken STRAIGHT
// FROM THE BODY with no signature, no nonce and no header check, and it reaches
// OTHER players: POST a victim's id with a live code and UNIQUE(code, player_id)
// locks them out of it forever, while a loop of invented ids burns a launch
// code's max_redemptions before anyone real arrives. It now goes through the SAME
// rail /api/game/save uses.
//
// ⚠ CORRECTED 2026-08-18 — THE 08-15 AUDIT DID NOT CLOSE THAT HOLE. It closed it
// for BASE58 WALLET IDS ONLY. The paragraph above used to end:
//     "...a base58 id demands an ed25519 signature over the exact body bytes plus
//      a single-use nonce; a guest-local id demands the matching X-Guest-Id. No
//      second auth scheme, no weaker path."
// The guest half of that sentence WAS the weaker path. `X-Guest-Id` carries NO
// signature (BackendRequestSigner.TryAttachAsync:111-114 attaches the header and
// returns) and the server only regex-checks it and echoes it back
// (_lib/wallet-auth.verifyGuest). The id is MINTED BY THE CLIENT, so an attacker
// chooses it: every fresh `guest-local-<64 hex>` is a brand-new "player". That
// burns max_redemptions (step 4 below counts ROWS) and steps over per_player_limit
// (step 5 counts rows keyed by that same chosen id). The cap was decorative.
// The stale comment is kept above, struck through in words rather than deleted,
// because "an audit says this is closed" is exactly what stopped anyone looking.
//
// THEN (2026-08-18 → 2026-09-06): this route called _lib/wallet-auth.authenticateGranting()
// — WALLET RAIL ONLY. A guest was refused with AUTH_WALLET_REQUIRED. Redeeming grants
// value, and a self-asserted identity may never be handed value on a published game.
// ⛔ Do not "restore guest redeem for convenience". If guests must ever redeem,
//    the answer is a server-side scarcity key an attacker cannot mint (attested
//    device / IP-and-code budget), never trusting the id they chose.
//
// ═════════════════════════════════════════════════════════════════════════════
// ⚖ RULING REVERSED 2026-09-06 (WO-1440, owner ruling) — GUESTS MAY REDEEM.
// ═════════════════════════════════════════════════════════════════════════════
// The paragraph above is kept VERBATIM and is not retracted: its analysis is still
// correct, and the next reader must be able to see that this was a considered trade
// and not an oversight. What changed is the CONCLUSION, not the facts.
//
// WHAT FORCED IT. The FIRSTWATCH acquisition campaign went public on X on 2026-09-06
// ("500 free crystals, code FIRSTWATCH, first 500 players"), and the post sends people
// to the SOLANA dAPP STORE — i.e. to the ALREADY-PUBLISHED build (2026.08.17.328845),
// which reaches a player only through a store submission and review. Almost everyone
// arriving from that post plays as a guest, and every one of them was refused here.
// A client fix could not reach them inside the campaign's life; only this file could.
//
// THE OWNER'S REASONING, with the Sybil risk stated to her explicitly beforehand: an
// acquisition promo that refuses everyone it exists to acquire has zero value, while
// the exposure is bounded — the worst case is one actor farming the code's cap, not an
// unbounded drain.
//
// ⚠ THE RESIDUAL RISK, STATED HONESTLY AND NOT SOFTENED:
//   1. A guest id is still self-asserted and still unlimited to mint. per_player_limit
//      stops the ACCIDENTAL DOUBLE-TAP (which is most real traffic) and nothing more.
//      It must never be described as anti-abuse.
//   2. THE IP BUDGET IS COST, NOT A WALL. A VPN, a phone on cellular, or a handful of
//      networks defeats it. It raises the price of farming; it does not forbid it.
//   3. ⛔ AND THE ONE THAT IS BIGGER THAN THE RULING ASSUMED — MEASURED ON THE LIVE ROW
//      2026-09-06: FIRSTWATCH has `max_redemptions = NULL`. Its "500" is `tier1_limit`,
//      a TIER BOUNDARY, not a cap: redemption 501+ still succeeds and still pays
//      `tier2_reward_crystals/coins` (100/100 on the live row) to every fresh guest id
//      until `expires_at` (2026-10-01). So the bound the ruling relies on does not
//      currently exist on this code — the tail is unbounded, not 500. This file will
//      NOT invent a cap the operator did not author (that would silently delete a
//      deliberately-authored tier-2 reward). Closing it is one operator statement:
//          UPDATE promo_codes SET max_redemptions = 500 WHERE code = 'FIRSTWATCH';
//      Step 4 below then enforces it. Surfaced to the owner in the WO-1440 result.
//
// HOW IT IS BUILT (the abuse controls that do NOT depend on a client-chosen id):
//   * auth  — authenticatePromoRedeem() (NOT authenticateGranting()): one named
//             function, one caller, so the exception cannot spread by being a default.
//             A guest result carries `unproven: true`.
//   * kill  — PROMO_GUEST_REDEEM_ENABLED=false locks guests back out of THIS route
//             with no redeploy, without killing guest saves the way GUEST_SAVE_ENABLED
//             would.
//   * cap   — the code's own global bound, enforced ATOMICALLY (step 4 for
//             max_redemptions; the single-statement UPDATE…RETURNING + INSERT CTE for
//             the tiered ordinal). Measured under 50-way concurrency, WO-1440.
//   * ip    — a fixed-window grant budget per (hashed IP, code), GUEST RAIL ONLY, and
//             counted only on grants that are actually about to be paid. See step 5b.
//   * audit — every redemption row carries `ip_hash`, so a farm is one GROUP BY away
//             and the rows are still there to claw back from.
//
// ⛔ SCOPED TO THIS ROUTE. The wallet-only rule stands everywhere else — purchases,
//    saves, entitlements, referral claim. Nothing outside this file was weakened.
//
// Client : Assets/_Modules/Core/Promo/PromoCodeService.cs
//   POST  application/json   (raw body — bodyParser disabled; the signature is
//                             over the EXACT bytes, same as save.js)
//   Headers: X-Wallet + X-Nonce + X-Signature, or X-Session + X-Wallet (wallet rail),
//            or X-Guest-Id (guest rail — RE-ACCEPTED 2026-09-06, see the ruling above)
//   Body  : { playerId, code, supportsInlinePackRewards }
//   Success: { success: true, reward: { crystals, coins, packSku, contents }, message }
//   Failure: { success: false, error: "INVALID_CODE" | "ALREADY_REDEEMED"
//                                    | "EXPIRED" | "PLAYER_LIMIT_REACHED"
//                                    | "REWARD_UNAVAILABLE" | "RATE_LIMITED" }
//   RATE_LIMITED is HTTP 200 like every other business rejection, deliberately: the
//   published client branches on the JSON body and reads any non-2xx as "couldn't
//   reach the server". A 429 would tell the player the wrong story. RATE_LIMITED is
//   unmapped in PromoCodeService.MapErrorKey, so it lands on the calm unknown-error
//   line — and the code is NOT consumed, so it stays retryable tomorrow.
//
// GATE → ERROR mapping (per schema.sql, table 3):
//   row missing / active=false                 → INVALID_CODE
//   NOW() > expires_at (when not null)          → EXPIRED
//   global redemptions >= max_redemptions       → ALREADY_REDEEMED
//   this player already redeemed this code      → ALREADY_REDEEMED
//   player's distinct redeemed codes >= per_player_limit → PLAYER_LIMIT_REACHED
//   reward_pack_sku set (client cannot pay it) → REWARD_UNAVAILABLE  ← NOT consumed
//   reward is zero crystals AND zero coins     → REWARD_UNAVAILABLE  ← NOT consumed
//
// Driver: @neondatabase/serverless
// Status codes: 200 | 400 | 401 | 500
//   NOTE: a *business* failure (bad/expired/used code) is returned as 200 with
//   { success:false, error } — the client reads the JSON body, not the HTTP
//   status, to map the user-facing message. 4xx/5xx are reserved for malformed
//   requests / auth refusals / server faults.
// =============================================================================

const { neon } = require('@neondatabase/serverless');
const { AuthCode, authenticatePromoRedeem, WALLET_MAX_BODY_BYTES, isGuestId } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject, logApiEvent, hashIp } = require('../_lib/audit');

// ── THE IP BUDGET (WO-1440) ──────────────────────────────────────────────────
// The one signal a client cannot choose. GUEST RAIL ONLY.
//
// ⛔ WHY 20 PER 24 HOURS, AND WHY NOT LOWER. The limit has to survive a SHARED NAT —
// a household, a dorm, a campus, a conference hall, and above all mobile CARRIER-GRADE
// NAT, which can put many unrelated players of a mobile game behind one address. It
// also has to make farming cost something. At 20, draining the 500-redemption tier-1
// band takes at least 25 distinct networks, while no plausible real venue reaches 20
// redemptions OF THIS ONE CODE in a day at this campaign's scale (the code's whole
// tier-1 band is 500 players worldwide). A tighter limit would start costing real
// acquisitions — the exact failure the reversal exists to prevent — and buys little,
// because anyone willing to farm can rent addresses.
//
// ⚠ PRECISELY WHAT IS COUNTED, because "grants, not attempts" is a near-miss and a
// near-miss in a rate limiter is how you punish the wrong people: the unit is spent by
// any attempt that has ALREADY CLEARED every other gate and is about to be paid. A bad
// code, an expired code, a code this guest already redeemed, or a zero-reward code
// never reaches step 5b and therefore never costs a household anything. An attempt that
// is itself over the limit DOES still increment (the UPSERT reserves, then judges), so
// a caller who is being refused stays refused rather than being let back in by trying
// less often — deliberate, and it only ever affects someone already past the line.
//
// FIXED window, not sliding: the budget refills in one step 24h after the CURRENT
// window's first grant. Said plainly because the two differ at the boundary.
const PROMO_IP_WINDOW_SECONDS = 24 * 60 * 60;
const PROMO_IP_MAX_GRANTS_PER_WINDOW = 20;

/**
 * Reserve one unit of this (IP, code) budget, atomically, in a single UPSERT —
 * the same shape wallet-auth.touchGuestRate uses, for the same reason: two
 * statements would race exactly where the money is.
 *
 * ⛔ FAILS CLOSED, and that is a DELIBERATE DIVERGENCE from touchGuestRate, which
 *    fails OPEN. That helper guards saves — "we could not check" must never cost a
 *    player their progress, and the rail it protects grants nothing. This one is the
 *    last non-forgeable gate in front of a payout, so an unreadable table must
 *    resolve to "do not pay", never to "go ahead". Availability there, correctness
 *    here — the same split api/admin/ops.js draws against _lib/maintenance.js.
 *    The refusal does NOT consume the code, so the player can retry once it is fixed.
 *
 * @returns {Promise<{ok:boolean, error?:string, grants?:number, degraded?:boolean}>}
 */
async function reserveIpBudget(sql, ipHash, code) {
    // No IP at all (a runtime that forwarded no header). Refuse rather than pay an
    // unattributable guest: this is the only abuse signal on this rail, and a caller
    // who can suppress it would otherwise get an unlimited one.
    if (!ipHash) {
        console.error('[promo/redeem] REFUSED-UNBURNED guest redeem with no resolvable caller IP.');
        return { ok: false, error: 'RATE_LIMITED', degraded: true };
    }
    try {
        const rows = await sql`
            INSERT INTO promo_ip_budget (ip_hash, code, window_started_at, grants, total_grants, last_grant_at)
            VALUES (${ipHash}, ${code}, NOW(), 1, 1, NOW())
            ON CONFLICT (ip_hash, code) DO UPDATE SET
                window_started_at = CASE
                    WHEN promo_ip_budget.window_started_at < NOW() - (${PROMO_IP_WINDOW_SECONDS} * INTERVAL '1 second')
                    THEN NOW() ELSE promo_ip_budget.window_started_at END,
                grants = CASE
                    WHEN promo_ip_budget.window_started_at < NOW() - (${PROMO_IP_WINDOW_SECONDS} * INTERVAL '1 second')
                    THEN 1 ELSE promo_ip_budget.grants + 1 END,
                total_grants = promo_ip_budget.total_grants + 1,
                last_grant_at = NOW()
            RETURNING grants, total_grants
        `;
        const grants = rows && rows[0] ? Number(rows[0].grants) : 1;
        if (grants > PROMO_IP_MAX_GRANTS_PER_WINDOW) {
            return { ok: false, error: 'RATE_LIMITED', grants: grants };
        }
        return { ok: true, grants: grants };
    } catch (err) {
        // LOUD: this is the abuse gate itself failing on a value-granting route.
        console.error(
            '[promo/redeem] IP BUDGET UNAVAILABLE — refusing the guest grant (fail-closed). ' +
            'Apply api/migrations/20260906_0019_promo_guest_redeem_ip_budget.sql. Cause: ' + (err && err.message)
        );
        return { ok: false, error: 'REWARD_UNAVAILABLE', degraded: true };
    }
}

/**
 * Did a lost claim lose to the CAP, or to a fault?
 *
 * The claiming UPDATEs at step 6 return zero rows for either reason, and the two
 * deserve opposite answers: a finished campaign is ALREADY_REDEEMED (final, honest,
 * stop retrying), a fault is REWARD_UNAVAILABLE (unburned, retryable). This is read
 * ONLY after a claim has already failed, so it costs the happy path nothing.
 *
 * Reads the COUNTER, not the ledger, because the counter is what the predicate
 * tested. A read error answers false — the caller then reports the fault it was
 * about to report anyway, which is the conservative direction here.
 */
async function capReached(sql, code, maxRedemptions) {
    if (maxRedemptions == null) return false;
    try {
        const rows = await sql`SELECT redemption_count FROM promo_codes WHERE code = ${code} LIMIT 1`;
        return rows.length === 1 && Number(rows[0].redemption_count) >= Number(maxRedemptions);
    } catch (_) {
        return false;
    }
}

async function handler(req, res) {
    if (applyCors(req, res, 'POST, OPTIONS')) return;

    const ref = newRef();

    if (req.method !== 'POST') {
        return quietFail(res, 400, AuthCode.METHOD_NOT_ALLOWED, ref);
    }

    // The wallet signature covers the EXACT raw bytes, so read them ourselves
    // (with a hard cap) rather than trusting a re-serialised parsed body.
    let rawBody, exactBytes;
    try {
        const read = await readBodyExact(req, WALLET_MAX_BODY_BYTES);
        rawBody = read.buffer;
        exactBytes = read.exact;
    } catch (err) {
        if (err && err.code === 'BODY_TOO_LARGE') {
            return quietFail(res, 400, AuthCode.PAYLOAD_TOO_LARGE, ref);
        }
        console.error('[promo/redeem] Body read error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    let body;
    try {
        body = JSON.parse(rawBody.toString('utf8'));
    } catch (err) {
        console.error('[promo/redeem] Body parse error:', err);
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    if (!body || typeof body !== 'object') {
        return quietFail(res, 400, AuthCode.BAD_PAYLOAD, ref);
    }

    // No "anonymous" fallback any more: an id nobody can prove is exactly the
    // hole this gate closes, and authenticate() would reject it anyway.
    const playerId = body.playerId != null ? String(body.playerId).trim() : '';
    const code = body.code != null ? String(body.code).trim().toUpperCase() : '';
    // Legacy supportsPackRewards meant "this APK can PackCatalog.Find(sku)" and is
    // deliberately ignored: only the explicit inline capability prevents a burn.
    const supportsInlinePackRewards = body.supportsInlinePackRewards === true;

    if (!playerId) {
        return quietFail(res, 400, AuthCode.PLAYER_ID_MISSING, ref);
    }
    if (!code) {
        return res.status(400).json({ error: 'Missing code' });
    }

    let sql;
    try {
        sql = neon(process.env.DATABASE_URL);
    } catch (err) {
        console.error('[promo/redeem] DB init error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // A signature can only be verified against the ORIGINAL bytes. If the runtime
    // parsed the body out from under us, say so precisely instead of emitting a
    // lying AUTH_BAD_SIGNATURE (see _lib/http.readBodyExact).
    // ⛔ SCOPED TO THE SIGNATURE PATH (2026-08-24). A session bearer does NOT sign the body:
    // wallet-auth.js verifyWallet() accepts `x-session` and returns via:'session' without ever
    // reading `payload`. This guard predates WO-1157's session rail and rejected BEFORE
    // authenticate() ran, so a session-authed call was refused for lacking bytes it never needed.
    // Same defect fixed in api/game/save.js, where it had silently 500ed EVERY wallet save in
    // production — all 21 rows in player_data were guest rows.
    const hasSessionHeader = !!(req.headers && req.headers['x-session']);
    if (!exactBytes && !isGuestId(playerId) && !hasSessionHeader) {
        await logAuthReject(sql, req, {
            code: AuthCode.SERVER_ERROR, ref, identity: playerId, mode: 'wallet',
            detail: { reason: 'raw_body_unavailable_bodyparser_active' },
        });
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }

    // ── AUTH GATE — WALLET RAIL, PLUS THE ONE SCOPED GUEST EXCEPTION ───────
    // authenticatePromoRedeem, NOT authenticateGranting: the owner's 2026-09-06
    // ruling admits a guest HERE and nowhere else (see this file's header and the
    // function's own docblock). A guest comes back marked `unproven: true` and must
    // still clear the IP budget at step 5b before anything is paid.
    // Fails CLOSED — a thrown auth check is a refusal, never a pass-through.
    let auth;
    try {
        auth = await authenticatePromoRedeem(sql, req, rawBody, playerId);
    } catch (err) {
        console.error('[promo/redeem] Auth check error:', err);
        return quietFail(res, 500, AuthCode.SERVER_ERROR, ref);
    }
    if (!auth.ok) {
        // LOUD server-side (a full audit row + a runtime line), QUIET to the
        // player (a stable code + ref; the client maps 401/400 to its one calm
        // "we couldn't confirm your identity" sentence — never raw JSON).
        await logAuthReject(sql, req, {
            code: auth.code, ref, identity: auth.identity, mode: auth.mode, detail: auth.detail,
        });
        const status = (auth.code === AuthCode.PLAYER_ID_BAD_SHAPE ||
                        auth.code === AuthCode.PLAYER_ID_MISSING ||
                        auth.code === AuthCode.WALLET_MALFORMED) ? 400 : 401;
        return quietFail(res, status, auth.code, ref);
    }

    try {
        // ── 1. Look the code up in the catalog ────────────────────────────────
        const codeRows = await sql`
            SELECT code, reward_crystals, reward_coins, message,
                   active, max_redemptions, per_player_limit, expires_at,
                   bound_wallet, reward_pack_sku,
                   tier1_pack_sku, tier1_limit, tier2_pack_sku,
                   tier2_reward_crystals, tier2_reward_coins, redemption_count
            FROM promo_codes
            WHERE code = ${code}
            LIMIT 1
        `;

        if (codeRows.length === 0 || codeRows[0].active === false) {
            return res.status(200).json({ success: false, error: 'INVALID_CODE' });
        }

        const promo = codeRows[0];

        // ── 1b. Wallet binding ───────────────────────────────────────────────────────
        // bound_wallet NULL  → a public code, open to anyone (launch promos, influencer
        //                      codes, apology grants). Unchanged behaviour.
        // bound_wallet SET   → a PRIVATE code, redeemable ONLY by that player id.
        //
        // WHY THIS EXISTS: the owner wants DEV codes that grant resources outright. On a
        // PUBLISHED game a code like that is a free-money exploit the moment it leaks —
        // shared on a forum, screenshotted, or pulled from a support ticket. Binding it
        // makes a leak inert: anyone else who tries it gets INVALID_CODE and the code is
        // NOT consumed, so the owner's own grant still works afterwards.
        //
        // ⚠ THE BINDING IS ONLY AS STRONG AS `playerId`, AND THAT IS THE WHOLE POINT OF
        // DOING IT HERE: playerId at this line has already been through
        // _lib/wallet-auth.authenticate — a base58 wallet id demanded an ed25519 signature
        // over the exact body bytes plus a single-use nonce. So this compares against a
        // PROVEN identity, not a claimed one. Never move this check anywhere that runs
        // before authenticate(), and never accept a wallet from the body: that is exactly
        // the hole the 2026-08-15 audit closed on this endpoint (playerId used to be taken
        // straight from the body, letting anyone burn a victim's code).
        //
        // Returns INVALID_CODE, deliberately — not a distinct "NOT_YOURS". A private code
        // should be indistinguishable from a nonexistent one to anyone who is not its
        // owner; a distinct error would confirm the code is real and worth hunting for.
        if (promo.bound_wallet != null && String(promo.bound_wallet).trim() !== '' &&
            String(promo.bound_wallet).trim() !== playerId) {
            return res.status(200).json({ success: false, error: 'INVALID_CODE' });
        }

        // ── 1c. reward_pack_sku — REFUSE BEFORE THE BURN (added 2026-08-18) ──────────
        // schema.sql:301 has defined reward_pack_sku since 2026-08-17 ("SET = grant
        // this pack's whole contents"), and step 1 above did not SELECT it until this
        // change. A pack-sku code therefore passed EVERY gate, reached step 6, and
        // INSERTed a promo_redemptions row — which UNIQUE(code, player_id)
        // (schema.sql:381) makes PERMANENT — and then returned
        // { crystals: 0, coins: 0 }. The client's PromoCodeService.ApplyReward logs
        // "reward carried no crystals and no coins — nothing to grant" and stops.
        // Net effect: the player's code is spent forever and they get NOTHING, with
        // no path back short of an operator deleting the row by hand.
        //
        // ⛔ THE RULE THIS ENCODES: A CODE MUST NEVER BURN FOR ZERO REWARD.
        //
        // WHY REFUSE RATHER THAN "HONOUR IT HERE": honouring it is not this file's to
        // do. schema.sql:311-323 is explicit that the CLIENT applies pack contents
        // through PackStoreVM.ApplyPackContents — the same seam a real purchase uses —
        // and the client's RedeemResponse (PromoCodeService.cs) has NO pack field to
        // receive a sku through. Inventing a server-side expansion of packs.json here
        // would create the second definition of "a bundle of resources" that
        // schema.sql:307-310 exists to forbid, and it would drift. So the honest,
        // smallest, LOSSLESS move is: refuse, and do not consume.
        //
        // A refusal is RETRYABLE; a burn is not. The moment the client learns to carry
        // a sku, the very same code in the player's hand still works. That asymmetry
        // is the whole justification for this branch.
        //
        // This is an OPERATOR authoring error, not a player error, so it is LOUD in
        // the runtime log (the owner authored a code the shipped client cannot pay)
        // and QUIET to the player: REWARD_UNAVAILABLE is unmapped in
        // PromoCodeService.MapErrorKey, so it lands on the calm KeyErrUnknown line —
        // never a wall of JSON. It is deliberately NOT ALREADY_REDEEMED, which would
        // tell the player their good code was spent.
        //
        // TO ENABLE PACK CODES LATER: add the sku to RedeemResponse + apply it via
        // PackStoreVM.ApplyPackContents client-side, THEN replace this refusal with a
        // pass-through of promo.reward_pack_sku. Not before — the burn is one-way.
        const packSku = promo.reward_pack_sku != null ? String(promo.reward_pack_sku).trim() : '';
        const tier1PackSku = promo.tier1_pack_sku != null ? String(promo.tier1_pack_sku).trim() : '';
        const tier2PackSku = promo.tier2_pack_sku != null ? String(promo.tier2_pack_sku).trim() : '';
        const hasTieredPack = tier1PackSku !== '' || tier2PackSku !== '';
        const hasTieredCurrency = !hasTieredPack && promo.tier1_limit != null &&
            promo.tier2_reward_crystals != null && promo.tier2_reward_coins != null;
        if ((packSku !== '' || hasTieredPack) && !supportsInlinePackRewards) {
            console.error(
                '[promo/redeem] REFUSED-UNBURNED pack reward: client did not advertise ' +
                'supportsInlinePackRewards=true. The code was NOT consumed; update the client and retry.'
            );
            return res.status(200).json({ success: false, error: 'REWARD_UNAVAILABLE' });
        }

        // ── 2. Expiry ─────────────────────────────────────────────────────────
        if (promo.expires_at != null && new Date(promo.expires_at).getTime() < Date.now()) {
            return res.status(200).json({ success: false, error: 'EXPIRED' });
        }

        // ── 3. This player already redeemed this code? ───────────────────────
        const already = await sql`
            SELECT 1 FROM promo_redemptions
            WHERE code = ${code} AND player_id = ${playerId}
            LIMIT 1
        `;
        if (already.length > 0) {
            return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
        }

        // ── 4. Global redemption cap for this code — the CHEAP EARLY-OUT ─────
        //
        // ⛔ THIS CHECK IS NOT THE CAP. It is a fast, friendly refusal for the
        //    overwhelmingly common case (the code is simply finished). The CAP ITSELF
        //    is the `(max_redemptions IS NULL OR redemption_count < max_redemptions)`
        //    predicate carried by the claiming UPDATE inside every grant statement at
        //    step 6, which serialises on the promo_codes row.
        //
        // ⚠ WHY THAT DISTINCTION IS WRITTEN THIS HARD (MEASURED 2026-09-06, WO-1440):
        //   this SELECT COUNT(*) and the INSERT below are TWO statements, and on the
        //   Neon HTTP driver two statements are two transactions. A probe of exactly
        //   this shape — cap 20, fifty concurrent actors — GRANTED ALL FIFTY: every
        //   caller read a count under the cap before any of them had inserted. An
        //   OVERSHOOT OF 30 on a cap of 20 (tools/wo1440-maxredemptions-race-probe.mjs).
        //   The old comment called this "Global redemption cap for this code", which is
        //   what stopped anyone measuring it. On a code that pays real currency that
        //   race is real money, so the authority moved into the single statement that
        //   claims the ordinal. Never restore a bare count-then-insert here.
        if (promo.max_redemptions != null) {
            const countRows = await sql`
                SELECT COUNT(*)::int AS n FROM promo_redemptions WHERE code = ${code}
            `;
            if (countRows[0].n >= promo.max_redemptions) {
                return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
            }
        }

        // ── 5. Per-player cap on DISTINCT codes redeemed ─────────────────────
        if (promo.per_player_limit != null) {
            const distinctRows = await sql`
                SELECT COUNT(DISTINCT code)::int AS n
                FROM promo_redemptions
                WHERE player_id = ${playerId}
            `;
            if (distinctRows[0].n >= promo.per_player_limit) {
                return res.status(200).json({ success: false, error: 'PLAYER_LIMIT_REACHED' });
            }
        }

        // ── 6. Record the redemption (snapshot the reward for audit) ─────────
        let crystals = promo.reward_crystals || 0;
        let coins    = promo.reward_coins    || 0;

        // STRUCTURAL BACKSTOP (added 2026-08-18) — the last line before the burn.
        // The pack-sku refusal at 1c closes the one KNOWN way a code reached this
        // point paying nothing. This closes the CLASS: any code that would grant
        // zero crystals AND zero coins is refused here, un-consumed, whatever put it
        // in that state (a mis-authored row, a future column this file forgets to
        // SELECT, a NULL where an integer was meant). The invariant is worth more
        // than the branch that discovered it: INSERTing below is IRREVERSIBLE
        // (UNIQUE(code, player_id), schema.sql:381) and there is no un-burn.
        // A message-only "thanks for playing" code is also refused, deliberately:
        // spending a player's one-shot code on a sentence is still spending it, and
        // a refusal can be undone by authoring a reward while a burn cannot.
        if (crystals <= 0 && coins <= 0 && packSku === '' && !hasTieredPack && !hasTieredCurrency) {
            console.error(
                '[promo/redeem] REFUSED-UNBURNED reward resolves to zero crystals AND zero coins ' +
                `(reward_crystals=${JSON.stringify(promo.reward_crystals)}, reward_coins=${JSON.stringify(promo.reward_coins)}). ` +
                'A code must never burn for nothing. The code was NOT consumed — fix the row, the player can retry.'
            );
            return res.status(200).json({ success: false, error: 'REWARD_UNAVAILABLE' });
        }

        // ── 5b. IP BUDGET — the one gate a client cannot forge (WO-1440) ─────
        // GUEST RAIL ONLY. A proven wallet is never counted: a household of wallet
        // holders behind one router must not be able to lock each other out, and a
        // wallet is already a scarcity key.
        //
        // Placed HERE on purpose — after every cheap gate (bad code, expiry, already
        // redeemed, per-player, zero reward) and immediately before the grant — so a
        // refusal or a typo can never spend a real household's budget. Only an attempt
        // that was actually about to be paid costs a unit.
        //
        // ⚠ KNOWN, ACCEPTED, AND SMALL: the unit is reserved here but the ledger insert
        // below can still lose a UNIQUE(code, player_id) race and answer ALREADY_REDEEMED,
        // in which case one unit was spent for no grant. That needs the same guest id to
        // arrive twice simultaneously, and it costs a household 1 of 20. Refunding it
        // would mean a compensating write on a failure path, which is a worse trade than
        // the error it corrects.
        const ipHash = hashIp(req);
        if (auth.unproven === true) {
            const budget = await reserveIpBudget(sql, ipHash, code);
            if (!budget.ok) {
                await logApiEvent(sql, playerId, 'promo_guest_ip_budget_refused', {
                    ref: ref, code: code, ipHash: ipHash,
                    grants: budget.grants ?? null,
                    max: PROMO_IP_MAX_GRANTS_PER_WINDOW,
                    windowSeconds: PROMO_IP_WINDOW_SECONDS,
                    degraded: budget.degraded === true,
                });
                // NOT consumed — a refusal is retryable, a burn is not (the same
                // asymmetry the REWARD_UNAVAILABLE branches above are built on).
                return res.status(200).json({ success: false, error: budget.error });
            }
        }

        let grantedPackSku = packSku;
        let grantedContents = null;
        try {
            if (hasTieredPack) {
                if (tier1PackSku === '' || tier2PackSku === '' ||
                    !Number.isInteger(Number(promo.tier1_limit)) || Number(promo.tier1_limit) <= 0) {
                    console.error('[promo/redeem] REFUSED-UNBURNED malformed tiered pack configuration.');
                    return res.status(200).json({ success: false, error: 'REWARD_UNAVAILABLE' });
                }

                // One statement owns ordinal selection AND the redemption insert. The row update
                // serializes concurrent claims; a duplicate insert rolls the whole statement back,
                // including redemption_count, so tier 500 cannot be skipped or double-issued.
                const tierRows = await sql`
                    WITH claimed AS (
                        UPDATE promo_codes AS pc
                           SET redemption_count = pc.redemption_count + 1
                          FROM packs AS p
                         WHERE pc.code = ${code}
                           -- THE global cap, enforced where it is atomic (WO-1440).
                           AND (pc.max_redemptions IS NULL OR pc.redemption_count < pc.max_redemptions)
                           AND p.sku = CASE WHEN pc.redemption_count + 1 <= pc.tier1_limit
                                            THEN pc.tier1_pack_sku ELSE pc.tier2_pack_sku END
                           AND p.active = TRUE
                           AND (
                               jsonb_path_exists(p.contents, '$.economy.* ? (@ > 0)') OR
                               jsonb_array_length(COALESCE(p.contents->'cosmetics', '[]'::jsonb)) > 0 OR
                               jsonb_array_length(COALESCE(p.contents->'convenience', '[]'::jsonb)) > 0
                           )
                         RETURNING pc.redemption_count, p.sku AS pack_sku, p.contents
                    ), recorded AS (
                        INSERT INTO promo_redemptions
                            (code, player_id, crystals, coins, pack_sku, contents, redemption_ordinal, ip_hash)
                        SELECT ${code}, ${playerId},
                               COALESCE((contents#>>'{economy,crystals}')::int, 0),
                               COALESCE((contents#>>'{economy,coins}')::int, 0),
                               pack_sku, contents, redemption_count, ${ipHash}
                          FROM claimed
                        RETURNING crystals, coins, pack_sku, contents
                    )
                    SELECT crystals, coins, pack_sku, contents FROM recorded
                `;
                if (tierRows.length !== 1 || !tierRows[0].pack_sku) {
                    // Zero rows now has TWO causes: the pack is missing/inactive/empty,
                    // or the atomic cap predicate refused the claim. Disambiguate before
                    // answering — telling a player "reward unavailable" when the truth is
                    // "the campaign is finished" sends them back to retry forever.
                    if (await capReached(sql, code, promo.max_redemptions)) {
                        return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
                    }
                    console.error('[promo/redeem] REFUSED-UNBURNED tiered pack missing, inactive, or empty.');
                    return res.status(200).json({ success: false, error: 'REWARD_UNAVAILABLE' });
                }
                grantedPackSku = String(tierRows[0].pack_sku);
                grantedContents = tierRows[0].contents;
                crystals = Number(tierRows[0].crystals) || 0;
                coins = Number(tierRows[0].coins) || 0;
            } else if (hasTieredCurrency) {
                if (!Number.isInteger(Number(promo.tier1_limit)) || Number(promo.tier1_limit) <= 0 ||
                    Number(promo.tier2_reward_crystals) < 0 || Number(promo.tier2_reward_coins) < 0) {
                    console.error('[promo/redeem] REFUSED-UNBURNED malformed tiered currency configuration.');
                    return res.status(200).json({ success: false, error: 'REWARD_UNAVAILABLE' });
                }
                const tierRows = await sql`
                    WITH claimed AS (
                        UPDATE promo_codes
                           SET redemption_count = redemption_count + 1
                         WHERE code = ${code}
                           -- THE global cap, enforced where it is atomic (WO-1440).
                           AND (max_redemptions IS NULL OR redemption_count < max_redemptions)
                         RETURNING redemption_count, tier1_limit, reward_crystals, reward_coins,
                                   tier2_reward_crystals, tier2_reward_coins
                    ), recorded AS (
                        INSERT INTO promo_redemptions
                            (code, player_id, crystals, coins, pack_sku, redemption_ordinal, ip_hash)
                        SELECT ${code}, ${playerId},
                               CASE WHEN redemption_count <= tier1_limit
                                    THEN reward_crystals ELSE tier2_reward_crystals END,
                               CASE WHEN redemption_count <= tier1_limit
                                    THEN reward_coins ELSE tier2_reward_coins END,
                               NULL, redemption_count, ${ipHash}
                          FROM claimed
                        RETURNING crystals, coins
                    )
                    SELECT crystals, coins FROM recorded
                `;
                if (tierRows.length === 0 && await capReached(sql, code, promo.max_redemptions)) {
                    // The claim was refused by the cap predicate, not by a fault. This is
                    // the ordinary end of a campaign, so answer it as one — and never as
                    // the 500 the throw below would have produced.
                    return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
                }
                if (tierRows.length !== 1 ||
                    (Number(tierRows[0].crystals) <= 0 && Number(tierRows[0].coins) <= 0)) {
                    throw new Error('tiered currency redemption produced no reward snapshot');
                }
                crystals = Number(tierRows[0].crystals);
                coins = Number(tierRows[0].coins);
            } else if (packSku !== '') {
                const packRows = await sql`
                    WITH claimed AS (
                        UPDATE promo_codes
                           SET redemption_count = redemption_count + 1
                         WHERE code = ${code}
                           -- THE global cap, enforced where it is atomic (WO-1440).
                           AND (max_redemptions IS NULL OR redemption_count < max_redemptions)
                         RETURNING redemption_count
                    ), selected AS (
                        SELECT p.sku, p.contents, c.redemption_count
                          FROM packs AS p, claimed AS c
                         WHERE p.sku = ${packSku} AND p.active = TRUE
                           AND (
                               jsonb_path_exists(p.contents, '$.economy.* ? (@ > 0)') OR
                               jsonb_array_length(COALESCE(p.contents->'cosmetics', '[]'::jsonb)) > 0 OR
                               jsonb_array_length(COALESCE(p.contents->'convenience', '[]'::jsonb)) > 0
                           )
                    ), recorded AS (
                        INSERT INTO promo_redemptions
                            (code, player_id, crystals, coins, pack_sku, contents, redemption_ordinal, ip_hash)
                        SELECT ${code}, ${playerId},
                               COALESCE((contents#>>'{economy,crystals}')::int, 0),
                               COALESCE((contents#>>'{economy,coins}')::int, 0),
                               sku, contents, redemption_count, ${ipHash}
                          FROM selected
                        RETURNING crystals, coins, pack_sku, contents
                    )
                    SELECT crystals, coins, pack_sku, contents FROM recorded
                `;
                if (packRows.length !== 1) {
                    if (await capReached(sql, code, promo.max_redemptions)) {
                        return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
                    }
                    console.error('[promo/redeem] REFUSED-UNBURNED pack missing, inactive, or empty.');
                    return res.status(200).json({ success: false, error: 'REWARD_UNAVAILABLE' });
                }
                grantedPackSku = String(packRows[0].pack_sku);
                grantedContents = packRows[0].contents;
                crystals = Number(packRows[0].crystals) || 0;
                coins = Number(packRows[0].coins) || 0;
            } else {
                // WO-1440: was a bare INSERT, so `max_redemptions` was enforced ONLY by
                // the count-then-insert at step 4 — measured to over-issue 50 grants
                // against a cap of 20. The claim now happens in the SAME statement as
                // the insert, serialising on the promo_codes row exactly as the tiered
                // paths already did, and the ordinal is recorded for the same audit.
                const plainRows = await sql`
                    WITH claimed AS (
                        UPDATE promo_codes
                           SET redemption_count = redemption_count + 1
                         WHERE code = ${code}
                           AND (max_redemptions IS NULL OR redemption_count < max_redemptions)
                         RETURNING redemption_count
                    ), recorded AS (
                        INSERT INTO promo_redemptions
                            (code, player_id, crystals, coins, pack_sku, contents, redemption_ordinal, ip_hash)
                        SELECT ${code}, ${playerId}, ${crystals}, ${coins}, NULL, NULL,
                               redemption_count, ${ipHash}
                          FROM claimed
                        RETURNING crystals, coins
                    )
                    SELECT crystals, coins FROM recorded
                `;
                if (plainRows.length !== 1) {
                    // The only way to lose the claim here is the cap predicate.
                    return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
                }
            }
        } catch (insertErr) {
            // UNIQUE(code, player_id) — lost the race against a concurrent redeem.
            // Treat as already redeemed (idempotent, no double-grant).
            if (insertErr && insertErr.code === '23505') {
                return res.status(200).json({ success: false, error: 'ALREADY_REDEEMED' });
            }
            throw insertErr;
        }

        // ── 7. Success ────────────────────────────────────────────────────────
        // Every GUEST grant is attributable after the fact (WO-1440). The ledger row
        // now carries ip_hash; this adds the same facts to the queryable event stream
        // beside the auth rejects, so "who took this campaign" is one read on either
        // side. Never throws — a failed audit must not fail a grant that already landed.
        if (auth.unproven === true) {
            await logApiEvent(sql, playerId, 'promo_guest_redeem', {
                ref: ref, code: code, ipHash: ipHash,
                crystals: crystals, coins: coins,
                packSku: grantedPackSku || null,
            });
        }

        return res.status(200).json({
            success: true,
            reward: { crystals, coins, packSku: grantedPackSku || null, contents: grantedContents },
            message: promo.message ?? null,
        });
    } catch (err) {
        console.error('[promo/redeem] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
}

module.exports = handler;
// MUST be assigned AFTER the handler export. `module.exports.config = ...`
// followed by `module.exports = handler` silently DISCARDS the config and leaves
// the runtime body parser ON, which drains the stream the raw-body reader needs.
// See api/game/save.js:427-432 and _lib/http.readBodyExact.
module.exports.config = { api: { bodyParser: false } };
