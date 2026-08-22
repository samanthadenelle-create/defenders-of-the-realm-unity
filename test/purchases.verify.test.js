'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { _test } = require('../api/purchases/verify');
const { _test: fulfillTest } = require('../api/purchases/fulfill');

const wallet = 'Wallet111111111111111111111111111111111111';
const recipient = 'Treasury11111111111111111111111111111111';
const recipientAta = 'TreasuryAta111111111111111111111111111111';
const mint = 'Mint11111111111111111111111111111111111111';
const contract = { recipient, recipientAta, mint, amountBaseUnits: 20_000_000_000, decimals: 9 };

function transaction({ signer = wallet, authority = signer, destination = recipientAta,
                       tokenMint = mint, amount = '20000000000', decimals = 9,
                       failed = false } = {}) {
    return {
        slot: 42,
        meta: { err: failed ? { InstructionError: [0, 'Custom'] } : null },
        transaction: { message: {
            accountKeys: [{ pubkey: signer, signer: true, writable: true }],
            instructions: [{ program: 'spl-token', parsed: { type: 'transferChecked', info: {
                authority, destination, mint: tokenMint,
                source: 'SourceAta111111111111111111111111111111111',
                tokenAmount: { amount, decimals, uiAmount: 20, uiAmountString: '20' },
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
        status: 'verified', currency: 'SKR', expected_lamports: 20_000_000_000, chain_slot: 42 };
    assert.equal(_test.entitlementMatches(row, wallet, 'hearth-spark', 'devnet'), true);
    assert.equal(_test.entitlementMatches(row, recipient, 'hearth-spark', 'devnet'), false);
    assert.deepEqual(_test.entitlementResponse(row, 'signature', 'hearth-spark'), {
        success: true, state: 'verified', sku: 'hearth-spark', txSignature: 'signature',
        currency: 'SKR', amountLamports: 20_000_000_000, chainSlot: 42, entitlementId: '7',
    });
});

test('fulfillment acknowledgement is bound to wallet and sku', () => {
    const row = { wallet, sku: 'hearth-spark' };
    assert.equal(fulfillTest.matches(row, wallet, 'hearth-spark'), true);
    assert.equal(fulfillTest.matches(row, recipient, 'hearth-spark'), false);
    assert.equal(fulfillTest.matches(row, wallet, 'different-pack'), false);
});
