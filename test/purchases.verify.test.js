'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { _test } = require('../api/purchases/verify');
const { _test: fulfillTest } = require('../api/purchases/fulfill');
const { DEVNET_CANARY_SKU, DEVNET_PACKS } = require('../api/_lib/purchase-catalog');

const wallet = 'Wallet111111111111111111111111111111111111';
const recipient = 'Treasury11111111111111111111111111111111';
const recipientAta = 'TreasuryAta111111111111111111111111111111';
const mint = 'Mint11111111111111111111111111111111111111';
const contract = { recipient, recipientAta, mint, amountBaseUnits: 25_000_000_000, decimals: 9 };

function exactBaseUnits(value, decimals) {
    assert.ok(Number.isInteger(decimals) && decimals >= 0, 'backend decimals must be a non-negative integer');
    const text = String(value);
    assert.match(text, /^\d+(?:\.\d+)?$/, 'canonical SKR price must be a plain non-negative decimal');
    const [whole, fraction = ''] = text.split('.');
    assert.ok(fraction.length <= decimals, 'canonical SKR price cannot round at backend precision');
    return BigInt(whole + fraction.padEnd(decimals, '0'));
}

test('server canary prices exactly match both canonical client mirrors', () => {
    const streamPath = path.join(__dirname, '..', 'Assets', 'StreamingAssets', 'Data', 'Canonical', 'packs.json');
    const resourcePath = path.join(__dirname, '..', 'Assets', 'Resources', 'Data', 'Canonical', 'packs.json');
    const streamText = fs.readFileSync(streamPath, 'utf8');
    const resourceText = fs.readFileSync(resourcePath, 'utf8');
    assert.equal(resourceText, streamText, 'canonical pack mirrors differ');

    const packs = JSON.parse(streamText).packs;
    const backendSkus = Object.keys(DEVNET_PACKS);
    assert.deepEqual(backendSkus, [DEVNET_CANARY_SKU], 'missing or extra Devnet server canary');
    assert.equal(DEVNET_CANARY_SKU, 'hearth-spark', 'ruled canary SKU drifted');

    for (const sku of backendSkus) {
        const client = packs.find(row => row.sku === sku);
        const server = DEVNET_PACKS[sku];
        assert.ok(client, `server SKU ${sku} is absent from canonical client packs`);
        assert.equal(server.currency, 'SKR', `${sku} is not on the ruled SKR rail`);
        assert.equal(typeof client.pricing?.skr, 'number', `${sku} has no canonical SKR price`);
        assert.equal(exactBaseUnits(client.pricing.skr, server.decimals), BigInt(server.amountBaseUnits),
            `${sku} client/backend SKR price mismatch`);
    }
});

function transaction({ signer = wallet, authority = signer, destination = recipientAta,
                       tokenMint = mint, amount = '25000000000', decimals = 9,
                       failed = false } = {}) {
    return {
        slot: 42,
        meta: { err: failed ? { InstructionError: [0, 'Custom'] } : null },
        transaction: { message: {
            accountKeys: [{ pubkey: signer, signer: true, writable: true }],
            instructions: [{ program: 'spl-token', parsed: { type: 'transferChecked', info: {
                authority, destination, mint: tokenMint,
                source: 'SourceAta111111111111111111111111111111111',
                tokenAmount: { amount, decimals, uiAmount: 25, uiAmountString: '25' },
            } } }],
        } },
    };
}

async function verify(result) {
    const previous = global.fetch;
    global.fetch = async () => ({ ok: true, json: async () => ({ jsonrpc: '2.0', result }) });
    try { return await _test.readFinalizedTransfer('https://rpc.invalid', 'sig', wallet, contract); }
    finally { global.fetch = previous; }
}

test('accepts exactly one finalized server-contract SKR transferChecked', async () => {
    assert.equal((await verify(transaction())).state, 'verified');
});

test('not found is pending and never verified optimistically', async () => {
    assert.deepEqual(await verify(null), { state: 'pending', reason: 'not_finalized' });
});

test('failed transaction is rejected', async () => {
    assert.equal((await verify(transaction({ failed: true }))).reason, 'transaction_failed');
});

test('wrong signer is rejected', async () => {
    assert.equal((await verify(transaction({ signer: 'Attacker1111111111111111111111111111111111' }))).reason,
        'wrong_signer');
});

test('wrong recipient is rejected', async () => {
    assert.equal((await verify(transaction({ destination: wallet }))).reason,
        'transfer_contract_mismatch');
});

test('wrong mint is rejected', async () => {
    assert.equal((await verify(transaction({ tokenMint: wallet }))).reason,
        'transfer_contract_mismatch');
});

test('wrong decimals are rejected', async () => {
    assert.equal((await verify(transaction({ decimals: 6 }))).reason,
        'transfer_contract_mismatch');
});

test('underpayment is rejected', async () => {
    assert.equal((await verify(transaction({ amount: '19999999999' }))).reason,
        'transfer_contract_mismatch');
});

test('same-signature race accepts the matching durable winner', () => {
    const row = { entitlement_id: 7n, wallet, sku: 'hearth-spark', network: 'devnet',
        status: 'verified', currency: 'SKR', expected_lamports: 25_000_000_000, chain_slot: 42 };
    assert.equal(_test.entitlementMatches(row, wallet, 'hearth-spark', 'devnet'), true);
    assert.equal(_test.entitlementMatches(row, recipient, 'hearth-spark', 'devnet'), false);
    assert.deepEqual(_test.entitlementResponse(row, 'signature', 'hearth-spark'), {
        success: true, state: 'verified', sku: 'hearth-spark', txSignature: 'signature',
        currency: 'SKR', amountLamports: 25_000_000_000, chainSlot: 42, entitlementId: '7',
    });
});

test('fulfillment acknowledgement is bound to wallet and sku', () => {
    const row = { wallet, sku: 'hearth-spark' };
    assert.equal(fulfillTest.matches(row, wallet, 'hearth-spark'), true);
    assert.equal(fulfillTest.matches(row, recipient, 'hearth-spark'), false);
    assert.equal(fulfillTest.matches(row, wallet, 'different-pack'), false);
});
