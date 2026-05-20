// =============================================================================
// DRAFT — copy this block to defenders-of-the-realm/api/_db.ts
//
// Append these helpers below the existing `insertBugReport` + `hasDatabaseUrl`
// in api/_db.ts. They power /api/leaderboard.ts and /api/metrics.ts (also
// included in this draft folder). The CREATE TABLE statements they rely on
// are ALREADY in your _db.ts (added 2026-05-19) — schema and helpers were
// separated because the overnight session only had write access to the
// schema, not the helpers.
// =============================================================================

// ── Leaderboard ──────────────────────────────────────────────────────────────

/** One row in the highest-wave-survived leaderboard. */
export interface LeaderboardRow {
  identity: string;
  display_name: string | null;
  best_wave: number;
  hero_class: string | null;
  updated_at: number;
}

/**
 * Insert / update one identity's best wave. Keeps the maximum — if the
 * incoming score is lower than the stored value, the row is unchanged.
 * Returns the row that's now stored (best of stored vs incoming).
 */
export async function upsertLeaderboardScore(params: {
  identity: string;
  displayName: string | null;
  bestWave: number;
  heroClass: string | null;
  updatedAt: number;
}): Promise<LeaderboardRow> {
  const { identity, displayName, bestWave, heroClass, updatedAt } = params;
  const { rows } = await sql<LeaderboardRow>`
    INSERT INTO leaderboard
      (identity, display_name, best_wave, hero_class, updated_at)
    VALUES
      (${identity}, ${displayName}, ${bestWave}, ${heroClass}, ${updatedAt})
    ON CONFLICT (identity) DO UPDATE SET
      best_wave    = GREATEST(leaderboard.best_wave, EXCLUDED.best_wave),
      display_name = COALESCE(EXCLUDED.display_name, leaderboard.display_name),
      hero_class   = COALESCE(EXCLUDED.hero_class,   leaderboard.hero_class),
      updated_at   = EXCLUDED.updated_at
    RETURNING *
  `;
  return rows[0];
}

/** Read the top-N wave-survival rows, ordered best-wave first. */
export async function readLeaderboardTop(
  limit: number,
): Promise<LeaderboardRow[]> {
  const { rows } = await sql<LeaderboardRow>`
    SELECT * FROM leaderboard
    ORDER BY best_wave DESC, updated_at DESC
    LIMIT ${limit}
  `;
  return rows;
}

// ── Metrics ──────────────────────────────────────────────────────────────────

/**
 * Append one telemetry event. Free-form jsonb payload — the team queries it
 * with SQL.  Write-only from the game's perspective; no read endpoint.
 */
export async function insertMetricEvent(params: {
  id: string;
  identity: string | null;
  eventName: string;
  payload: unknown;
  clientTs: number | null;
  createdAt: number;
}): Promise<void> {
  const { id, identity, eventName, payload, clientTs, createdAt } = params;
  await sql`
    INSERT INTO metrics
      (id, identity, event_name, payload, client_ts, created_at)
    VALUES
      (${id}, ${identity}, ${eventName}, ${JSON.stringify(payload ?? null)}::jsonb, ${clientTs}, ${createdAt})
  `;
}
