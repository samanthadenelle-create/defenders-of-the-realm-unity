// =============================================================================
// DRAFT — copy this file to defenders-of-the-realm/api/leaderboard.ts
//
// GET  /api/leaderboard         → top 25 wave-survival rows
// POST /api/leaderboard          { identity, displayName?, bestWave, heroClass? }
//                                → upserts, returns the now-stored row
//
// Identity is either a lower-cased Solana base58 wallet (preferred) or a
// caller-supplied anonymous device id (for pre-wallet builds). Matches the
// no-auth, write-light pattern of bug-report.ts.
// =============================================================================

import type { VercelRequest, VercelResponse } from '@vercel/node';
import {
  ensureSchema,
  hasDatabaseUrl,
  readLeaderboardTop,
  upsertLeaderboardScore,
} from './_db.js';

const DEFAULT_LIMIT = 25;
const MAX_LIMIT = 100;
const MAX_IDENTITY_LEN = 80;     // Solana base58 wallets are 32-44; padded for anon ids
const MAX_DISPLAY_LEN = 32;
const MAX_HERO_LEN = 24;
const MIN_WAVE = 1;
const MAX_WAVE = 99999;          // sanity cap; the in-game progression caps long before this

function readBody(req: VercelRequest): unknown {
  const raw = req.body;
  if (raw && typeof raw === 'object') return raw;
  if (typeof raw === 'string' && raw.length > 0) {
    try { return JSON.parse(raw); } catch { return {}; }
  }
  return {};
}

function parseLimit(req: VercelRequest): number {
  const raw = req.query.limit;
  const s = Array.isArray(raw) ? raw[0] : raw;
  const n = s ? Number.parseInt(s, 10) : DEFAULT_LIMIT;
  if (!Number.isFinite(n) || n <= 0) return DEFAULT_LIMIT;
  return Math.min(n, MAX_LIMIT);
}

function trimToOrNull(value: unknown, max: number): string | null {
  if (typeof value !== 'string') return null;
  const t = value.trim();
  if (t.length === 0) return null;
  return t.length > max ? t.slice(0, max) : t;
}

export default async function handler(
  req: VercelRequest,
  res: VercelResponse,
): Promise<void> {
  if (!hasDatabaseUrl()) {
    res.status(500).json({ error: 'Leaderboard storage is not configured.' });
    return;
  }
  try {
    await ensureSchema();
  } catch (e) {
    res.status(500).json({ error: 'Database initialization failed.' });
    return;
  }

  if (req.method === 'GET') {
    const limit = parseLimit(req);
    try {
      const rows = await readLeaderboardTop(limit);
      res.status(200).json({ rows, limit });
    } catch (e) {
      res.status(500).json({ error: 'Leaderboard read failed.' });
    }
    return;
  }

  if (req.method === 'POST') {
    const body = readBody(req) as Record<string, unknown>;
    const identity = trimToOrNull(body.identity, MAX_IDENTITY_LEN);
    if (!identity) {
      res.status(400).json({ error: 'Field "identity" is required.' });
      return;
    }
    const bestWaveRaw = body.bestWave;
    const bestWave =
      typeof bestWaveRaw === 'number'
        ? Math.floor(bestWaveRaw)
        : Number.NaN;
    if (!Number.isFinite(bestWave) || bestWave < MIN_WAVE || bestWave > MAX_WAVE) {
      res.status(400).json({ error: 'Field "bestWave" must be an integer 1..99999.' });
      return;
    }
    const displayName = trimToOrNull(body.displayName, MAX_DISPLAY_LEN);
    const heroClass = trimToOrNull(body.heroClass, MAX_HERO_LEN);

    try {
      const row = await upsertLeaderboardScore({
        identity: identity.toLowerCase(),
        displayName,
        bestWave,
        heroClass,
        updatedAt: Date.now(),
      });
      res.status(200).json({ row });
    } catch (e) {
      res.status(500).json({ error: 'Leaderboard upsert failed.' });
    }
    return;
  }

  res.setHeader('Allow', 'GET, POST');
  res.status(405).json({ error: 'Method not allowed — use GET or POST.' });
}
