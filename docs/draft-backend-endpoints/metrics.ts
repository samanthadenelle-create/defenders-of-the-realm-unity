// =============================================================================
// DRAFT — copy this file to defenders-of-the-realm/api/metrics.ts
//
// POST /api/metrics  { identity?, eventName, payload?, clientTs? }
//                    → appends one telemetry row, returns { eventId }
//
// Write-only. The team queries the metrics table with SQL out-of-band.
// Matches the no-auth, length-capped pattern of bug-report.ts.
// =============================================================================

import type { VercelRequest, VercelResponse } from '@vercel/node';
import { ensureSchema, hasDatabaseUrl, insertMetricEvent } from './_db.js';

const MAX_IDENTITY_LEN = 80;
const MAX_EVENT_NAME_LEN = 64;
const MAX_PAYLOAD_BYTES = 4 * 1024; // 4 KB JSON cap — prevents log flooding

function newEventId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return `met_${crypto.randomUUID()}`;
  }
  return `met_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 10)}`;
}

function readBody(req: VercelRequest): unknown {
  const raw = req.body;
  if (raw && typeof raw === 'object') return raw;
  if (typeof raw === 'string' && raw.length > 0) {
    try { return JSON.parse(raw); } catch { return {}; }
  }
  return {};
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
  if (req.method !== 'POST') {
    res.setHeader('Allow', 'POST');
    res.status(405).json({ error: 'Method not allowed — use POST.' });
    return;
  }
  if (!hasDatabaseUrl()) {
    res.status(500).json({ error: 'Metrics storage is not configured.' });
    return;
  }

  const body = readBody(req) as Record<string, unknown>;
  const eventName = trimToOrNull(body.eventName, MAX_EVENT_NAME_LEN);
  if (!eventName) {
    res.status(400).json({ error: 'Field "eventName" is required.' });
    return;
  }
  const identity = trimToOrNull(body.identity, MAX_IDENTITY_LEN);

  // Payload must serialise into MAX_PAYLOAD_BYTES — anything larger gets the
  // 400 instead of silently truncating.
  let payload: unknown = body.payload ?? null;
  try {
    const json = JSON.stringify(payload);
    if (json && json.length > MAX_PAYLOAD_BYTES) {
      res.status(400).json({ error: `Payload exceeds ${MAX_PAYLOAD_BYTES}-byte cap.` });
      return;
    }
  } catch {
    res.status(400).json({ error: 'Payload is not JSON-serialisable.' });
    return;
  }

  const clientTsRaw = body.clientTs;
  const clientTs =
    typeof clientTsRaw === 'number' && Number.isFinite(clientTsRaw)
      ? Math.floor(clientTsRaw)
      : null;

  try {
    await ensureSchema();
    const eventId = newEventId();
    await insertMetricEvent({
      id: eventId,
      identity: identity ? identity.toLowerCase() : null,
      eventName,
      payload,
      clientTs,
      createdAt: Date.now(),
    });
    res.status(200).json({ eventId });
  } catch (e) {
    res.status(500).json({ error: 'Metric insert failed.' });
  }
}
