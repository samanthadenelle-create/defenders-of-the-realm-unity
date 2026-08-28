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
// NOW: this route calls _lib/wallet-auth.authenticateGranting() — WALLET RAIL
// ONLY. A guest is refused with AUTH_WALLET_REQUIRED. Redeeming grants value, and
// a self-asserted identity may never be handed value on a published game.
// ⛔ Do not "restore guest redeem for convenience". If guests must ever redeem,
//    the answer is a server-side scarcity key an attacker cannot mint (attested
//    device / IP-and-code budget), never trusting the id they chose.
//
// Client : Assets/_Modules/Core/Promo/PromoCodeService.cs
//   POST  application/json   (raw body — bodyParser disabled; the signature is
//                             over the EXACT bytes, same as save.js)
//   Headers: X-Wallet + X-Nonce + X-Signature   (WALLET RAIL ONLY as of 2026-08-18;
//            X-Guest-Id is no longer accepted here — see the correction above)
//   Body  : { playerId, code, supportsPackRewards }
//   Success: { success: true, reward: { crystals, coins, packSku }, message }
//   Failure: { success: false, error: "INVALID_CODE" | "ALREADY_REDEEMED"
//                                    | "EXPIRED" | "PLAYER_LIMIT_REACHED"
//                                    | "REWARD_UNAVAILABLE" }
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
const { AuthCode, authenticateGranting, WALLET_MAX_BODY_BYTES, isGuestId } = require('../_lib/wallet-auth');
const { applyCors, newRef, quietFail, readBodyExact } = require('../_lib/http');
const { logAuthReject } = require('../_lib/audit');

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
    const supportsPackRewards = body.supportsPackRewards === true;

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

    // ── AUTH GATE — WALLET RAIL ONLY (2026-08-18) ──────────────────────────
    // authenticateGranting, NOT authenticate: this route hands out crystals and
    // coins, and a guest id is a value the CLIENT picks. See the correction in
    // this file's header and the honesty note in _lib/wallet-auth.verifyGuest.
    // Fails CLOSED — a thrown auth check is a refusal, never a pass-through.
    let auth;
    try {
        auth = await authenticateGranting(sql, req, rawBody, playerId);
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
        if ((packSku !== '' || hasTieredPack) && !supportsPackRewards) {
            console.error(
                '[promo/redeem] REFUSED-UNBURNED pack reward: client did not advertise ' +
                'supportsPackRewards=true. The code was NOT consumed; update the client and retry.'
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

        // ── 4. Global redemption cap for this code ───────────────────────────
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
                `[promo/redeem] REFUSED-UNBURNED code=${code} — resolves to zero crystals AND zero coins ` +
                `(reward_crystals=${JSON.stringify(promo.reward_crystals)}, reward_coins=${JSON.stringify(promo.reward_coins)}). ` +
                'A code must never burn for nothing. The code was NOT consumed — fix the row, the player can retry.'
            );
            return res.status(200).json({ success: false, error: 'REWARD_UNAVAILABLE' });
        }

        let grantedPackSku = packSku;
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
                        UPDATE promo_codes
                           SET redemption_count = redemption_count + 1
                         WHERE code = ${code}
                         RETURNING redemption_count, tier1_limit,
                                   tier1_pack_sku, tier2_pack_sku
                    ), recorded AS (
                        INSERT INTO promo_redemptions (code, player_id, crystals, coins, pack_sku)
                        SELECT ${code}, ${playerId}, 0, 0,
                               CASE WHEN redemption_count <= tier1_limit
                                    THEN tier1_pack_sku ELSE tier2_pack_sku END
                          FROM claimed
                        RETURNING pack_sku
                    )
                    SELECT pack_sku FROM recorded
                `;
                if (tierRows.length !== 1 || !tierRows[0].pack_sku) {
                    throw new Error('tiered redemption produced no reward snapshot');
                }
                grantedPackSku = String(tierRows[0].pack_sku);
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
                         RETURNING redemption_count, tier1_limit, reward_crystals, reward_coins,
                                   tier2_reward_crystals, tier2_reward_coins
                    ), recorded AS (
                        INSERT INTO promo_redemptions (code, player_id, crystals, coins, pack_sku)
                        SELECT ${code}, ${playerId},
                               CASE WHEN redemption_count <= tier1_limit
                                    THEN reward_crystals ELSE tier2_reward_crystals END,
                               CASE WHEN redemption_count <= tier1_limit
                                    THEN reward_coins ELSE tier2_reward_coins END,
                               NULL
                          FROM claimed
                        RETURNING crystals, coins
                    )
                    SELECT crystals, coins FROM recorded
                `;
                if (tierRows.length !== 1 ||
                    (Number(tierRows[0].crystals) <= 0 && Number(tierRows[0].coins) <= 0)) {
                    throw new Error('tiered currency redemption produced no reward snapshot');
                }
                crystals = Number(tierRows[0].crystals);
                coins = Number(tierRows[0].coins);
            } else {
                await sql`
                    INSERT INTO promo_redemptions (code, player_id, crystals, coins, pack_sku)
                    VALUES (${code}, ${playerId}, ${crystals}, ${coins}, ${packSku || null})
                `;
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
        return res.status(200).json({
            success: true,
            reward: { crystals, coins, packSku: grantedPackSku || null },
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
