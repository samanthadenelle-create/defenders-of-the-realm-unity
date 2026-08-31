// =============================================================================
// api/assetlinks.js — Vercel Serverless Function (Digital Asset Links statement)
// -----------------------------------------------------------------------------
// Serves the Android Digital Asset Links statement list for this host. Reached
// at the WELL-KNOWN path via a REWRITE in vercel.json:
//
//     /.well-known/assetlinks.json   ->   /api/assetlinks
//
// A rewrite (NOT a redirect) is mandatory: the Digital Asset Links verifier does
// NOT follow redirects — a 30x is treated as "no statement", which is
// indistinguishable from the 404 this file exists to fix.
//
// ── WHY THIS EXISTS (the wallet-connect root cause, 2026-08-05) ──────────────
// Mobile Wallet Adapter (MWA) wallets verify the CALLING dapp: they take the
// `identity.uri` sent in the `authorize` request, fetch
// `<identityUri>/.well-known/assetlinks.json`, and check that an `android_app`
// statement names the calling package + its signing certificate. Per the MWA
// spec a wallet SHOULD decline with ERROR_AUTHORIZATION_FAILED (-1) when the
// caller cannot be verified.
//
// We were shipping the Solana Unity SDK's DEFAULT identity
// (`https://solana.unity-sdk.gg/`, SolanaMobileWalletAdapter.cs:18-21), whose
// /.well-known/assetlinks.json returns HTTP 404 — so no wallet could ever
// verify us and every connect died with -1. Latency confirmed it: 6.76s on the
// first attempt (remote fetch -> 404) collapsing to ~1.1s on retries (cached
// negative). SolanaWalletProvider.cs now sends THIS host as identityUri.
//
// ── THE FINGERPRINT ──────────────────────────────────────────────────────────
// SHA-256 of the certificate the shipped APK is actually signed with, extracted
// with `apksigner verify --print-certs` (Signer #1, CN=DeNelle Studios).
//
// !! LAUNCH LANDMINE !!  This list is an ARRAY and MUST stay one.
// If the game ever ships through Google Play with **Play App Signing**, Google
// RE-SIGNS the APK/AAB with ITS OWN key, and the fingerprint below stops
// matching what wallets see on a Play install — reproducing this exact bug on
// launch day. The fix is to APPEND (never replace) the Play App Signing
// certificate's SHA-256 from
//   Play Console -> your app -> Test and release -> Setup -> App signing
// so BOTH the local/Firebase-App-Distribution build and the Play build verify.
// =============================================================================

// Package name of the Android build (Player Settings -> Other -> Package Name).
const ANDROID_PACKAGE_NAME = 'com.denellestudios.echoesofelarion';

// All certificates are intentional: the first signs direct/Seeker installs;
// the remaining three are Play's deployment, hybrid-classical, and hybrid-PQC
// App Signing certificates exported from Play Console on 2026-08-30. Google
// requires every hybrid-signing fingerprint to be registered with API providers.
// Never replace one distribution rail with the other.
const SHA256_CERT_FINGERPRINTS = [
    '73:36:66:CE:4C:E2:C8:72:AB:65:30:EB:28:D6:DB:F1:E1:9D:E2:6D:88:ED:59:D1:B5:C0:20:9C:3D:A6:24:43',
    'F6:60:24:BE:21:44:49:CE:9F:98:F1:88:9D:E6:26:F7:E4:07:74:A1:B6:40:7D:81:C5:77:AD:0F:D8:91:62:7A',
    '79:04:B9:95:FB:A5:11:EB:8C:BA:DE:D6:9B:BC:A9:39:3F:88:7D:74:50:DE:8A:4B:2F:CF:34:DA:9D:15:66:66',
    'F3:3E:18:94:1F:3B:68:FB:10:FE:25:35:E2:A1:4F:71:63:CA:2A:A2:D6:C9:7F:64:0B:B2:9C:8F:EA:3C:F6:44',
];

const STATEMENTS = [
    {
        relation: ['delegate_permission/common.handle_all_urls'],
        target: {
            namespace: 'android_app',
            package_name: ANDROID_PACKAGE_NAME,
            sha256_cert_fingerprints: SHA256_CERT_FINGERPRINTS,
        },
    },
];

module.exports = async (req, res) => {
    // Wallet apps fetch this from their own process/origin; keep it wide open.
    // It is public, non-secret, integrity-checked data by design.
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, HEAD, OPTIONS');
    if (req.method === 'OPTIONS') { return res.status(204).end(); }

    if (req.method !== 'GET' && req.method !== 'HEAD') {
        return res.status(400).json({ error: 'Method not allowed' });
    }

    // Content-Type MUST be application/json — the Android verifier rejects
    // anything else (and Vercel would otherwise sniff text/plain for a rewrite).
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    // Cacheable but not forever: 1h at the edge with stale-while-revalidate, so
    // appending a Play App Signing fingerprint goes live within the hour rather
    // than being pinned by a long-lived CDN copy.
    res.setHeader('Cache-Control', 'public, max-age=3600, s-maxage=3600, stale-while-revalidate=86400');

    return res.status(200).send(JSON.stringify(STATEMENTS));
};
