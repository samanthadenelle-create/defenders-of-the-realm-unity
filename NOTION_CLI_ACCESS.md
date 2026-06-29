# Notion → Claude CLI access (handoff for WO-457)

Goal: let Claude CLI read/update the **Work Orders** Notion DB directly via a Notion
Integration token + the official SDK. Nothing migrates — Notion stays source-of-truth mirror.

---

## Known values (filled in — no need to look these up)

- **NOTION_DATABASE_ID** = `f3115f05ecf940cf8968bd82bbbdff9f`
  (this is the 32-hex *database_id* from the DB URL — NOT the AI `data-source`/collection
  id `5f66b263-...`, which the SDK will reject)
- **Status** select options (must match exactly): `Done`, `In progress`, `Ready`, `Held`,
  `Blocked`, `Spec`, `Dropped`, `Verify-Close`
- Other props: `WO` (number), `Title` (title), `Notes` (text), `Priority`, `Lane`, `Source`

---

## Step 1 — ONLY Sam can do this (Claude/CLI cannot)

A token is a secret that must be minted in the Notion UI:

1. Go to https://www.notion.so/my-integrations → **New integration** (internal).
   Copy the **Internal Integration Secret** — it starts with `secret_` (or `ntn_`).
2. Open the **Work Orders** DB in Notion → top-right **•••** → **Connections** →
   add your new integration. (Without this share, every call returns 403.)
3. Hand the token to CLI, or set it yourself in the PowerShell session in Step 2.

---

## Step 2 — CLI runs this (PowerShell on Windows)

```powershell
# session env (paste the real secret from Step 1)
$env:NOTION_TOKEN="secret_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
$env:NOTION_DATABASE_ID="f3115f05ecf940cf8968bd82bbbdff9f"

mkdir notion-wo-bot; cd notion-wo-bot
npm init -y
npm i @notionhq/client
```

Then write `set-wo.js` (full script is in WO-457, body unchanged) and call it:

```powershell
node .\set-wo.js 405 "In progress"
node .\set-wo.js 405 "Blocked" "Blocked on WO-410 perf baseline."
node .\set-wo.js 405 "Done" "Closed after verification."
```

---

## Troubleshooting
- **403 / permission** → DB not shared with the integration (Step 1.2), or wrong token.
- **"Invalid Status"** → string must match a Status option above exactly.
- **"No Work Order found"** → that ticket's `WO` number is blank or not unique.

Once verified, flip WO-457 from **Spec → Done** in Notion.
