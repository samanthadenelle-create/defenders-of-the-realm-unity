# WORK ORDER 80 — Vercel + Neon Backend (War Room, TX Verification, Management)

**Status:** DONE (reconciled 2026-08-09 from the tree, NOT felt-verified — api/ lives in this repo with schema.sql, DB_SETUP.md, admin/, game/; Neon save path live)
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Large — database schema + serverless API routes + Unity helper
**Depends on:** WO-78 (TransactionVerifier), WO-79 (WarRoomWindow)

---

## Goal

Stand up a scalable, low-cost backend on **Vercel** (serverless functions) +
**Neon** (serverless Postgres) that powers:

- Transaction verification and receipt saving
- Real-time War Room / Control Room metrics
- Refunds and support ticket management
- Promo code CRUD
- Discord and Twitter/X webhook dispatch

---

## 1. Neon Postgres Schema

Run the following in the **Neon SQL Editor** (`neon.tech → your project → SQL Editor`):

```sql
-- Players & Wallets
CREATE TABLE players (
    id             SERIAL PRIMARY KEY,
    wallet_address TEXT UNIQUE NOT NULL,
    aether_shards  INTEGER DEFAULT 0,
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- Staking Snapshots (refreshed on wallet connect / daily)
CREATE TABLE staking_snapshots (
    id             SERIAL PRIMARY KEY,
    wallet_address TEXT NOT NULL,
    staked_amount  NUMERIC NOT NULL,
    snapshot_time  TIMESTAMPTZ DEFAULT NOW()
);

-- Transactions & Receipts
CREATE TABLE transactions (
    id                SERIAL PRIMARY KEY,
    signature         TEXT UNIQUE NOT NULL,
    wallet_address    TEXT NOT NULL,
    payment_type      TEXT NOT NULL,       -- 'SOL', 'SKR', 'USDC', 'IAP'
    aether_granted    INTEGER NOT NULL,
    amount_paid       NUMERIC,
    status            TEXT DEFAULT 'confirmed',
    created_at        TIMESTAMPTZ DEFAULT NOW()
);

-- Support Tickets / Refunds / Escalations
CREATE TABLE support_tickets (
    id                    SERIAL PRIMARY KEY,
    wallet_address        TEXT NOT NULL,
    transaction_signature TEXT,
    issue_type            TEXT,            -- 'refund', 'escalation', 'bug'
    description           TEXT,
    status                TEXT DEFAULT 'open',
    created_at            TIMESTAMPTZ DEFAULT NOW()
);

-- Promo Codes & Events
CREATE TABLE promos (
    id          SERIAL PRIMARY KEY,
    code        TEXT UNIQUE NOT NULL,
    multiplier  NUMERIC DEFAULT 1.0,
    expires_at  TIMESTAMPTZ,
    active      BOOLEAN DEFAULT TRUE
);

-- Daily Metrics (for War Room dashboard)
CREATE TABLE daily_metrics (
    date                 DATE PRIMARY KEY,
    dau                  INTEGER DEFAULT 0,
    revenue_usd          NUMERIC DEFAULT 0,
    skr_staked_total     NUMERIC DEFAULT 0,
    transactions_count   INTEGER DEFAULT 0
);
```

---

## 2. Vercel Project Setup

```
your-repo/
└── api/
    ├── verify-transaction.ts
    ├── war-room/
    │   └── metrics.ts
    ├── refund.ts
    ├── promo.ts
    ├── webhook-discord.ts
    └── webhook-twitter.ts
```

Set environment variable in Vercel dashboard:

```
NEON_DATABASE_URL = postgres://user:pass@ep-xxx.neon.tech/neondb?sslmode=require
```

---

## 3. API Routes

### `api/verify-transaction.ts`

```typescript
import { NextApiRequest, NextApiResponse } from 'next';
import { createClient } from '@neondatabase/serverless';

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
    if (req.method !== 'POST') return res.status(405).end();

    const { signature, walletAddress, paymentType, aetherGranted } = req.body;
    if (!signature || !walletAddress) return res.status(400).json({ error: 'Missing fields' });

    const sql = createClient(process.env.NEON_DATABASE_URL!);

    try {
        // Save receipt
        await sql`
            INSERT INTO transactions (signature, wallet_address, payment_type, aether_granted)
            VALUES (${signature}, ${walletAddress}, ${paymentType}, ${aetherGranted})
            ON CONFLICT (signature) DO NOTHING`;

        // Update or create player Aether balance
        await sql`
            INSERT INTO players (wallet_address, aether_shards)
            VALUES (${walletAddress}, ${aetherGranted})
            ON CONFLICT (wallet_address)
            DO UPDATE SET aether_shards = players.aether_shards + ${aetherGranted}`;

        res.status(200).json({ success: true, receiptId: signature });
    } catch (e) {
        console.error(e);
        res.status(500).json({ error: 'Failed to save receipt' });
    } finally {
        await sql.end();
    }
}
```

---

### `api/war-room/metrics.ts`

```typescript
import { NextApiRequest, NextApiResponse } from 'next';
import { createClient } from '@neondatabase/serverless';

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
    const sql = createClient(process.env.NEON_DATABASE_URL!);

    const result = await sql`
        SELECT
            (SELECT COUNT(DISTINCT wallet_address) FROM players)              AS total_players,
            (SELECT COUNT(*)
               FROM transactions
              WHERE created_at >= NOW() - INTERVAL '1 day')                   AS today_transactions,
            (SELECT COALESCE(SUM(aether_granted), 0)
               FROM transactions
              WHERE created_at >= NOW() - INTERVAL '1 day')                   AS today_aether_issued,
            (SELECT COALESCE(SUM(amount_paid), 0)
               FROM transactions
              WHERE created_at >= NOW() - INTERVAL '1 day')                   AS today_revenue_usd,
            (SELECT COALESCE(SUM(staked_amount), 0)
               FROM staking_snapshots
              WHERE snapshot_time >= NOW() - INTERVAL '1 day')                AS total_staked_skr
    `;

    await sql.end();
    res.status(200).json(result[0]);
}
```

---

### `api/refund.ts`

```typescript
export default async function handler(req: NextApiRequest, res: NextApiResponse) {
    if (req.method !== 'POST') return res.status(405).end();

    const { signature } = req.body;
    const sql = createClient(process.env.NEON_DATABASE_URL!);

    try {
        await sql`UPDATE transactions SET status = 'refunded' WHERE signature = ${signature}`;
        // TODO: Trigger on-chain refund via Solana SDK / treasury wallet
        res.status(200).json({ success: true, message: `Refund initiated for ${signature}` });
    } finally {
        await sql.end();
    }
}
```

---

### `api/webhook-discord.ts`

```typescript
export default async function handler(req: NextApiRequest, res: NextApiResponse) {
    const { message } = req.body;
    await fetch(process.env.DISCORD_WEBHOOK_URL!, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: message }),
    });
    res.status(200).json({ sent: true });
}
```

---

### `api/webhook-twitter.ts`

```typescript
// Uses Twitter API v2 — store TWITTER_BEARER_TOKEN in Vercel env vars.
export default async function handler(req: NextApiRequest, res: NextApiResponse) {
    const { tweet } = req.body;
    await fetch('https://api.twitter.com/2/tweets', {
        method: 'POST',
        headers: {
            Authorization: `Bearer ${process.env.TWITTER_BEARER_TOKEN}`,
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ text: tweet }),
    });
    res.status(200).json({ posted: true });
}
```

---

## 4. Unity `BackendAPI.cs` Helper

**Path:** `Assets/_Modules/Backend/BackendAPI.cs`

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System;

public static class BackendAPI
{
    // ← Replace with your deployed Vercel URL after deployment.
    private const string BASE_URL = "https://your-project.vercel.app/api/";

    // ── Transaction verification ────────────────────────────────────────────

    public static async void VerifyTransaction(string signature, string wallet,
                                               string type, int aether)
    {
        var form = new WWWForm();
        form.AddField("signature",     signature);
        form.AddField("walletAddress", wallet);
        form.AddField("paymentType",   type);
        form.AddField("aetherGranted", aether.ToString());

        using var www = UnityWebRequest.Post(BASE_URL + "verify-transaction", form);
        await www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[BackendAPI] VerifyTransaction failed: {www.error}");
        else
            Debug.Log($"[BackendAPI] Receipt saved: {www.downloadHandler.text}");
    }

    // ── War Room metrics ────────────────────────────────────────────────────

    public static async void GetWarRoomMetrics(Action<string> onSuccess)
    {
        using var www = UnityWebRequest.Get(BASE_URL + "war-room/metrics");
        await www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(www.downloadHandler.text);
        else
            Debug.LogError($"[BackendAPI] GetWarRoomMetrics failed: {www.error}");
    }

    // ── Webhooks ────────────────────────────────────────────────────────────

    public static async void PostToDiscord(string message)
    {
        var form = new WWWForm();
        form.AddField("message", message);
        using var www = UnityWebRequest.Post(BASE_URL + "webhook-discord", form);
        await www.SendWebRequest();
    }

    public static async void PostToTwitter(string tweet)
    {
        var form = new WWWForm();
        form.AddField("tweet", tweet);
        using var www = UnityWebRequest.Post(BASE_URL + "webhook-twitter", form);
        await www.SendWebRequest();
    }

    // ── Refund ──────────────────────────────────────────────────────────────

    public static async void Refund(string signature)
    {
        var form = new WWWForm();
        form.AddField("signature", signature);
        using var www = UnityWebRequest.Post(BASE_URL + "refund", form);
        await www.SendWebRequest();
    }
}
```

---

## 5. Deployment Steps

1. Create a free **Neon** project at `neon.tech`, run the schema above.
2. Push the `api/` folder to a GitHub repo connected to **Vercel**.
3. In Vercel: add environment variables:
   - `NEON_DATABASE_URL`
   - `DISCORD_WEBHOOK_URL`
   - `TWITTER_BEARER_TOKEN`
4. Deploy. Vercel auto-detects Next.js API routes.
5. Update `BASE_URL` in `BackendAPI.cs` to your live Vercel domain.
6. In `WarRoomWindow.cs` (WO-79): wire `RefreshMetrics()` to call
   `BackendAPI.GetWarRoomMetrics(...)`.
7. In `TransactionVerifier.cs` (WO-78): the `BackendAPI.VerifyTransaction`
   call is already in place — it becomes live the moment you update the URL.

---

## Files to Create / Edit

| File | Action |
|---|---|
| Neon SQL Editor | **Run** schema above |
| `api/verify-transaction.ts` | **Create** |
| `api/war-room/metrics.ts` | **Create** |
| `api/refund.ts` | **Create** |
| `api/webhook-discord.ts` | **Create** |
| `api/webhook-twitter.ts` | **Create** |
| `Assets/_Modules/Backend/BackendAPI.cs` | **Create** |
| `Assets/Editor/WarRoomWindow.cs` | **Edit** — wire `RefreshMetrics` to `BackendAPI` |
| `Assets/_Modules/Monetization/TransactionVerifier.cs` | Already calls `BackendAPI` — goes live when URL is updated |

---

## Acceptance Criteria

- [ ] `POST /api/verify-transaction` saves a row to `transactions` table
- [ ] Player's `aether_shards` balance in `players` table increments correctly
- [ ] `GET /api/war-room/metrics` returns JSON with `total_players`,
      `today_transactions`, `today_revenue_usd`, `total_staked_skr`
- [ ] `POST /api/refund` sets `transactions.status` to `'refunded'`
- [ ] Discord webhook sends the message text to your server announcement channel
- [ ] `BackendAPI.VerifyTransaction` in Unity sends the form and logs success
- [ ] War Room Refresh button shows live data from Neon
- [ ] Duplicate transaction signatures are silently ignored (ON CONFLICT DO NOTHING)
