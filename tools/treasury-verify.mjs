// tools/treasury-verify.mjs — WO-1155 / MON002 treasury pre-flight.
//
// ⛔ THIS SCRIPT NEVER SIGNS ANYTHING. It holds no key, imports no keypair, sends no
// transaction. It is READ-ONLY against mainnet. Creating and funding the treasury is the
// owner's act, signed by her wallet; this exists so that what she creates is PROVEN before a
// single lamport is authored anywhere.
//
// WHY IT EXISTS: the recipient the project was carrying —
//   2VePaneS3xX2EdzSbe4JdiovRffboLJV4yNVmVTkeuCg
// — is a PLAIN WALLET, not a Squads vault, and nobody noticed from the address alone. You
// cannot tell by looking. The discriminator is mathematical: a normal wallet's pubkey lies ON
// the ed25519 curve, while a program-derived address (which every Squads vault is) is chosen
// precisely because it lies OFF it. That is check 2 below and it is the whole reason this file
// is not just a comment in a work order.
//
// Run:  node tools/treasury-verify.mjs <vaultPubkey> [--rpc <url>]
// Judge by the MARKER on the output — TREASURY_VERIFY_OK / TREASURY_VERIFY_FAIL — never by the
// exit code (CLAUDE.md §8: this repo's runners exit 0 on refusals).

import { Connection, PublicKey } from '@solana/web3.js';
import { getAssociatedTokenAddressSync, getAccount, getMint, TOKEN_PROGRAM_ID } from '@solana/spl-token';

// The official Solana Mobile SKR mint. NOT ours — we never minted it and never hold it.
const SKR_MAINNET_MINT = new PublicKey('SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3');
// Squads V4. Verified against the skill's program table AND docs.squads.so.
const SQUADS_V4_PROGRAM = new PublicKey('SQDS4ep65T869zMMBKyuUq6aD6EgTu8psMjkvj52pCf');
// ⚠ CLASSIC token program, deliberately. Token-2022 derives a DIFFERENT ATA for the same
// owner+mint, so guessing wrong here produces a real-looking address that never receives a cent.
const EXPECTED_TOKEN_PROGRAM = TOKEN_PROGRAM_ID;

const args = process.argv.slice(2);
const vaultArg = args.find(a => !a.startsWith('--'));
const rpcIdx = args.indexOf('--rpc');
const rpc = rpcIdx >= 0 ? args[rpcIdx + 1] : 'https://api.mainnet-beta.solana.com';

const problems = [];
const queryProblems = [];
const notes = [];
function fail(s) { problems.push(s); }
function queryFail(s) { queryProblems.push(s); }
function ok(s) { notes.push('  ok    ' + s); }
function isRpcFailure(e) {
  const s = String(e?.message ?? e ?? '');
  return /(fetch|network|socket|timeout|timed out|rate.?limit|ECONN|ENOTFOUND|server error|service unavailable|gateway|\b(?:401|403|408|425|429|500|502|503|504)\b|-32\d{3})/i.test(s);
}

if (!vaultArg) {
  console.log('usage: node tools/treasury-verify.mjs <vaultPubkey> [--rpc <url>]');
  console.log('TREASURY_VERIFY_FAIL no vault pubkey given');
  process.exit(0);
}

let vault;
try {
  vault = new PublicKey(vaultArg);
} catch {
  console.log(`TREASURY_VERIFY_FAIL '${vaultArg}' is not a valid base58 public key`);
  process.exit(0);
}

const connection = new Connection(rpc, 'confirmed');
console.log(`treasury pre-flight — vault=${vault.toBase58()}`);
console.log(`rpc=${rpc}\n`);

// ── 1. THE MINT IS THE AUTHORITY ON DECIMALS ─────────────────────────────────────────────
// Read decimals from CHAIN, before any amount is authored. A doc said 9 (our Devnet test
// mint's value); mainnet SKR is 6. At 6 decimals, 1_000_000_000 base units is 1,000 SKR on a
// row meant to move exactly 1 — and /verify runs AFTER settlement, so the guard would have
// failed with the money already gone. Never take this number from a document.
let decimals = null;
try {
  const mint = await getMint(connection, SKR_MAINNET_MINT, 'confirmed', EXPECTED_TOKEN_PROGRAM);
  decimals = mint.decimals;
  ok(`SKR mint ${SKR_MAINNET_MINT.toBase58()} decimals=${decimals} (1 SKR = ${10 ** decimals} base units)`);
  if (decimals !== 6) {
    fail(`SKR mint reports decimals=${decimals}. Every authored amount in the repo assumes 6. ` +
         `STOP and re-derive them — do not adjust this script to match.`);
  }
} catch (e) {
  if (isRpcFailure(e)) queryFail(`could not read the SKR mint: ${e.message}`);
  else fail(`could not read the SKR mint: ${e.message}`);
}

// ── 2. IS IT ACTUALLY A VAULT? ───────────────────────────────────────────────────────────
// The check that would have caught the plain wallet on day one.
const onCurve = PublicKey.isOnCurve(vault.toBytes());
if (onCurve) {
  fail(`vault is ON the ed25519 curve — it is a PLAIN WALLET, not a program-derived Squads ` +
       `vault. A single key can move these funds. This is acceptable ONLY for a throwaway ` +
       `1-SKR rail test, NEVER as the production treasury.`);
} else {
  ok('vault is OFF-curve (program-derived) — consistent with a Squads vault');
}

// ── 3. OWNED BY SQUADS V4? ───────────────────────────────────────────────────────────────
// Off-curve alone is not enough: plenty of PDAs belong to other programs.
try {
  const info = await connection.getAccountInfo(vault);
  if (!info) {
    fail(`vault account does not exist on chain at ${rpc}. It has never been funded or created.`);
  } else if (info.owner.equals(SQUADS_V4_PROGRAM)) {
    ok(`vault is owned by Squads V4 (${SQUADS_V4_PROGRAM.toBase58()})`);
  } else if (info.owner.toBase58() === '11111111111111111111111111111111') {
    // System-owned means one of two OPPOSITE things, and only the curve test tells them apart:
    // a Squads vault is a System-owned PDA holding SOL (fine), and an ordinary wallet is also
    // System-owned (not fine). Reporting "System-owned PDA" without consulting check 2 made
    // this script contradict itself on its very first run — it called a plain wallet a PDA.
    if (onCurve) {
      notes.push('  note  System-owned AND on-curve — that is an ordinary wallet, see the failure above');
    } else {
      ok('vault is a System-owned PDA (normal for a Squads vault holding SOL)');
      notes.push('  note  confirm in the Squads UI that this PDA is vault index 0 of YOUR multisig — ' +
                 'off-curve + System-owned does not by itself prove WHICH multisig owns it');
    }
  } else {
    fail(`vault is owned by ${info.owner.toBase58()}, which is neither Squads V4 nor the System ` +
         `Program. Do not author this address.`);
  }
} catch (e) {
  if (isRpcFailure(e)) queryFail(`could not read the vault account: ${e.message}`);
  else fail(`could not read the vault account: ${e.message}`);
}

// ── 4. THE SKR TOKEN ACCOUNT ─────────────────────────────────────────────────────────────
// ⚠ Must EXIST as a deliberate, funded step. Do not let a transfer create it incidentally.
const ata = getAssociatedTokenAddressSync(SKR_MAINNET_MINT, vault, true, EXPECTED_TOKEN_PROGRAM);
console.log(`derived SKR ATA: ${ata.toBase58()}`);
console.log(`  (owner=${vault.toBase58()}, mint=${SKR_MAINNET_MINT.toBase58()}, classic token program)\n`);
try {
  const acct = await getAccount(connection, ata, 'confirmed', EXPECTED_TOKEN_PROGRAM);
  if (!acct.mint.equals(SKR_MAINNET_MINT)) {
    fail(`ATA holds mint ${acct.mint.toBase58()}, not SKR. Wrong account.`);
  } else if (!acct.owner.equals(vault)) {
    fail(`ATA authority is ${acct.owner.toBase58()}, not the vault. Wrong account.`);
  } else {
    ok(`SKR ATA exists, holds the official mint, authority is the vault` +
       (decimals !== null ? ` (balance ${Number(acct.amount) / 10 ** decimals} SKR)` : ''));
  }
} catch (e) {
  const message = `SKR ATA ${ata.toBase58()} could not be read (${e.name}: ${e.message}).`;
  if (isRpcFailure(e)) queryFail(message);
  else fail(message + ` Create it deliberately and funded - never let the first transfer create it.`);
}

// ── 5. THE MULTISIG THAT CONTROLS IT ─────────────────────────────────────────────────────
// Checks 2-4 prove this is A vault holding SKR. They do NOT prove WHO can spend it, and that
// is the question that decides whether revenue is safe. Pass --multisig <addr> to close it:
// we derive vault[0] from the multisig and require it to EQUAL the address above, then read
// the threshold. Deriving is proof; a screenshot is testimony.
//
// The multisig address is NOT the one in the Squads UI's URL — that URL carries the VAULT.
// Find it by deriving, or from the vault's transaction history (any Squads-owned account it
// references).
const msIdx = args.indexOf('--multisig');
const msArg = msIdx >= 0 ? args[msIdx + 1] : null;
if (msArg) {
  let sdk = null;
  try { sdk = await import('@sqds/multisig'); }
  catch { fail(`--multisig needs the Squads SDK: run \`npm install\` (it is a devDependency).`); }
  if (sdk) {
    try {
      const msPda = new PublicKey(msArg);
      const [derived] = sdk.getVaultPda({ multisigPda: msPda, index: 0 });
      if (derived.equals(vault)) {
        ok(`multisig ${msArg} derives vault[0] == this address (linkage PROVEN, not asserted)`);
      } else {
        fail(`multisig ${msArg} derives vault[0] = ${derived.toBase58()}, which is NOT this ` +
             `address. Either the wrong multisig, or the vault belongs to someone else.`);
      }
      const ms = await sdk.accounts.Multisig.fromAccountAddress(connection, msPda);
      const threshold = Number(ms.threshold);
      const members = ms.members.length;
      // ⛔ THE PRODUCTION GATE. A 1-of-1 is a single key with extra steps: one signature moves
      // every cent, instantly. It is fine for a throwaway canary and NOT fine for revenue.
      // Adding members does not change the vault address, so raising this later re-authors
      // nothing — which is exactly why there is no excuse for shipping without it.
      if (threshold < 2 || members < 3) {
        fail(`multisig is ${threshold}-of-${members}. ONE key can move every cent` +
             (Number(ms.timeLock) === 0 ? ', instantly (timeLock 0)' : '') +
             `. Acceptable for the 1-SKR canary; NOT acceptable for public sales — raise to ` +
             `2-of-3 in the Squads UI first. The vault ADDRESS does not change, so nothing ` +
             `here needs re-authoring afterwards.`);
      } else {
        ok(`multisig is ${threshold}-of-${members}, timeLock ${ms.timeLock}s — production-shaped`);
      }
    } catch (e) {
      if (isRpcFailure(e)) queryFail(`could not read the multisig: ${e.message}`);
      else fail(`could not read the multisig: ${e.message}`);
    }
  }
} else {
  notes.push('  note  pass --multisig <addr> to PROVE which multisig owns this vault and check ' +
             'its threshold — without it, checks 2-4 prove it is a vault but not whose');
}

// ── verdict ──────────────────────────────────────────────────────────────────────────────
console.log(notes.join('\n'));
if (problems.length === 0 && queryProblems.length === 0) {
  console.log(`\nTREASURY_VERIFY_OK ${vault.toBase58()} is a vault, holds an SKR ATA at ${ata.toBase58()}, mint decimals=${decimals}`);
} else if (problems.length === 0) {
  console.log('\nquery unavailable:');
  for (const p of queryProblems) console.log('  WARN  ' + p);
  console.log(`\nTREASURY_VERIFY_UNREACHABLE ${queryProblems.length} RPC query failure(s) - explicit ship acknowledgement required`);
} else {
  console.log('\nproblems:');
  for (const p of problems) console.log('  FAIL  ' + p);
  for (const p of queryProblems) console.log('  WARN  ' + p);
  console.log(`\nTREASURY_VERIFY_FAIL ${problems.length} problem(s) — do NOT author this address anywhere`);
}
