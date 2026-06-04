# Defenders of the Realm — Admin Management Console Spec
**Owner:** Samantha / DeNelle Studios  
**Target repo:** `defenders-of-the-realm-v2` (Vercel backend)  
**Audience:** Kayden (CLI implementation)

---

## Overview

A password-protected web dashboard at `/admin` in the existing Vercel backend. Samantha can manage all live game parameters, run sales, control events, grant or refund crystals to specific players, and toggle maintenance mode — without touching code or redeploying.

**Architecture correction:** ServerConfig values must live in the **Postgres database**, not Vercel env vars (env vars require a redeploy to change — useless for live ops). The console writes to a `game_config` table; `api/game/load.js` reads from it; Unity gets fresh config on every player sync.

---

## 1. Database Changes

### New table: `game_config`

```sql
CREATE TABLE IF NOT EXISTS game_config (
  key         TEXT PRIMARY KEY,
  value       TEXT NOT NULL,
  updated_at  TIMESTAMPTZ DEFAULT now()
);

-- Seed defaults (matches ServerConfig.Default in Unity)
INSERT INTO game_config (key, value) VALUES
  ('bossWaveCrystalDropChance',  '0.45'),
  ('bossWaveCrystalMin',         '1'),
  ('bossWaveCrystalMax',         '3'),
  ('bossWaveInterval',           '5'),
  ('packSaleActive',             'false'),
  ('packSaleDiscountPct',        '0'),
  ('packSaleLabel',              ''),
  ('activeEventName',            ''),
  ('eventBonusCrystals',         '0'),
  ('eventExpiryUtc',             '0'),
  ('eventDisplayText',           ''),
  ('empowermentCostMultiplier',  '1.0'),
  ('crystalRefundRate',          '0.5'),
  ('maintenanceMode',            'false'),
  ('maintenanceMessage',         '')
ON CONFLICT (key) DO NOTHING;
```

---

## 2. Backend API Changes

### 2a. Update `api/game/load.js`

Replace env-var reads with DB reads for the config block:

```js
// At top of handler — fetch config from DB
const configRows = await db.query('SELECT key, value FROM game_config');
const cfg = Object.fromEntries(configRows.rows.map(r => [r.key, r.value]));

// Return in response alongside player data
return res.json({
  success: true,
  data: playerState,
  config: {
    bossWaveCrystalDropChance:  parseFloat(cfg.bossWaveCrystalDropChance ?? '0.45'),
    bossWaveCrystalMin:         parseInt(cfg.bossWaveCrystalMin ?? '1'),
    bossWaveCrystalMax:         parseInt(cfg.bossWaveCrystalMax ?? '3'),
    bossWaveInterval:           parseInt(cfg.bossWaveInterval ?? '5'),
    packSaleActive:             cfg.packSaleActive === 'true',
    packSaleDiscountPct:        parseInt(cfg.packSaleDiscountPct ?? '0'),
    packSaleLabel:              cfg.packSaleLabel ?? null,
    activeEventName:            cfg.activeEventName || null,
    eventBonusCrystals:         parseInt(cfg.eventBonusCrystals ?? '0'),
    eventExpiryUtc:             parseInt(cfg.eventExpiryUtc ?? '0'),
    eventDisplayText:           cfg.eventDisplayText || null,
    empowermentCostMultiplier:  parseFloat(cfg.empowermentCostMultiplier ?? '1.0'),
    crystalRefundRate:          parseFloat(cfg.crystalRefundRate ?? '0.5'),
    maintenanceMode:            cfg.maintenanceMode === 'true',
    maintenanceMessage:         cfg.maintenanceMessage || null,
  }
});
```

### 2b. New `api/admin/config.js` — read + write config

```
GET  /api/admin/config          → returns all game_config rows as { key: value }
POST /api/admin/config          → body: { key, value } → upserts one row
POST /api/admin/config/bulk     → body: { config: { key: value, ... } } → upserts many
```

All admin routes require the header:  
`Authorization: Bearer <ADMIN_SECRET>`  
where `ADMIN_SECRET` is a Vercel env var (this one CAN be env var since it never changes).

### 2c. New `api/admin/players.js` — player lookup + grants

```
GET  /api/admin/players?q=<wallet_or_email>  → search players table
GET  /api/admin/players/:playerId            → single player record + balances
POST /api/admin/players/:playerId/grant      → body: { crystals: N, reason: "..." }
POST /api/admin/players/:playerId/refund     → body: { crystalCost: N, itemId: "..." }
```

**Grant** adds N to `aether_crystals` in the players table and logs to a new `admin_actions` table.  
**Refund** applies the `crystalRefundRate` from `game_config` and credits the result.

### 2d. New `api/admin/event.js` — quick event controls

```
POST /api/admin/event/start   → body: { name, bonusCrystals, expiryUtc, displayText }
POST /api/admin/event/end     → clears activeEventName, eventBonusCrystals, eventExpiryUtc
```

### 2e. New `admin_actions` audit log table

```sql
CREATE TABLE IF NOT EXISTS admin_actions (
  id          SERIAL PRIMARY KEY,
  action      TEXT NOT NULL,          -- 'grant', 'refund', 'config_change', 'event_start', etc.
  target_id   TEXT,                   -- playerId if player action
  detail      JSONB,                  -- full payload for auditability
  performed_at TIMESTAMPTZ DEFAULT now()
);
```

Every write through the admin API inserts a row here. This gives you a full audit trail.

---

## 3. Admin Console UI (`/admin`)

A single-page HTML/CSS/JS app served as a static file from the Vercel project. No framework needed — plain fetch + DOM is enough given the scope. Protect with the same `ADMIN_SECRET` (stored in `localStorage` after the login form; cleared on logout).

### Login screen
Simple centered form: password field + "Enter Console" button.  
On success: store token in `sessionStorage`, show the dashboard.

### Dashboard layout

**Top nav bar:**  `Defenders Admin` | Live Config | Events | Players | Ops | Audit Log  
**Status bar:** Shows `maintenanceMode` state in red if active. Shows active event name + expiry countdown if running.

---

### Tab 1 — Live Config

A clean form showing every `game_config` parameter with appropriate input types.

**Boss Wave Drops section:**
| Label | Input | Notes |
|---|---|---|
| Drop Chance | Slider 0–100% | Maps to 0.0–1.0 |
| Min / Max Crystals | Number inputs | Validated min ≤ max |
| Boss Wave Every Nth | Number input | Integer ≥ 1 |

**Economy section:**
| Label | Input | Notes |
|---|---|---|
| Empowerment Cost Multiplier | Slider 0.1×–3.0× | 1.0 = normal |
| Crystal Refund Rate | Slider 0–100% | Applied to all refunds |

**[ Save Config ]** button — POSTs all values via `/api/admin/config/bulk`. Shows success/error toast.

---

### Tab 2 — Events & Sales

**Active Event panel:**
- Event name field (slug, e.g. `founders_weekend`)
- Display text field (what players see in HUD)
- Bonus crystals per wave: number input
- Expiry: datetime-local picker (auto-converts to UTC unix timestamp)
- **[ Start Event ]** / **[ End Event ]** buttons

**Pack Sale panel:**
- Sale Active: toggle
- Discount %: number input (0–100)
- Banner label: text field (e.g. "Founders Weekend 🎉")
- **[ Apply Sale ]** button
- Current sale status shown in a coloured badge (green = active, grey = inactive)

---

### Tab 3 — Players

**Search bar:** wallet address or email → calls `GET /api/admin/players?q=...`

**Player card (shown after search):**
- Wallet address, email, join date
- Current balances: Coins / Aether Crystals / Voidshards / Best Wave
- Last synced timestamp

**Actions:**
- **Grant Crystals:** number input + reason field → `POST /api/admin/players/:id/grant`
- **Refund:** item ID field + crystal cost field → `POST /api/admin/players/:id/refund` (applies `crystalRefundRate` automatically, shows "Player receives N crystals back")

---

### Tab 4 — Ops

**Maintenance Mode:**
- Large toggle — red = maintenance on, green = live
- Message field: text shown to players in game
- **[ Push ]** button — writes both values to `game_config`

**Quick Actions:**
- [ Reset Boss Drop to Defaults ] — one-click restores defaults without touching other values
- [ End All Events ] — clears event fields in one click
- [ Clear Sale ] — sets packSaleActive = false

---

### Tab 5 — Audit Log

Table showing last 100 rows from `admin_actions`:
| Time | Action | Player | Detail |
|---|---|---|---|
| 2026-05-27 14:32 | grant | 0xABC…123 | +10 crystals — "Founder reward" |
| 2026-05-27 12:01 | event_start | — | founders_weekend, +2/wave, expires 2026-06-03 |

Refreshes on tab open. No pagination needed for v1.

---

## 4. Security

- All `/api/admin/*` routes check `Authorization: Bearer <ADMIN_SECRET>` header. Return 401 if missing or wrong.
- `ADMIN_SECRET` set once in Vercel env vars (this is fine — it doesn't need to change live).
- Console password field submits to a `/api/admin/auth` endpoint that returns `{ ok: true }` if the secret matches. Token stored in `sessionStorage` only (gone on tab close).
- No admin routes are exposed in the Unity client — Unity only calls `api/game/save` and `api/game/load`.

---

## 5. Implementation Order (for Kayden)

1. Add `game_config` and `admin_actions` tables to `schema.sql`, run migration
2. Update `api/game/load.js` to read config from DB
3. Add `api/admin/config.js` (read + bulk write)
4. Add `api/admin/players.js` (search, grant, refund)
5. Add `api/admin/event.js` (start, end)
6. Add `api/admin/auth.js` (token check)
7. Build `/admin/index.html` — single-file, all tabs, fetch-based, `sessionStorage` auth
8. Verify: update a config value → reload Unity in Play mode → log confirms new value received

---

## 6. What Samantha Controls After This Ships

| Task | How |
|---|---|
| Run a 30% off sale this weekend | Players tab → Sales → set 30%, set label, toggle on, Save |
| Start a double-crystal event | Events tab → fill name/bonus/expiry → Start Event |
| End an event early | Events tab → End Event (one click) |
| Grant crystals to a player who reported a bug | Players tab → search wallet → Grant 5, reason "bug report compensation" |
| Process a refund request | Players tab → Refund → enter item cost → system calculates 50% back |
| Increase boss drop chance for a patch | Live Config → slide drop chance to 60% → Save |
| Emergency maintenance | Ops tab → flip Maintenance toggle → write message → Push |
| Check who did what and when | Audit Log tab |
