<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 79 — Management War Room / Control Room

**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Medium — Unity Editor window (quick start) + web architecture guide
**Depends on:** WO-80 (Vercel + Neon backend) for live data; works standalone with stubs

---

## Goal

Give you (the developer) a single **War Room** dashboard to monitor everything
in real time: metrics, revenue, staking data, transactions, refunds,
escalations, promo codes, and social media posting hooks.

---

## Part A — Unity Editor War Room (Quick Start)

**Path:** `Assets/Editor/WarRoomWindow.cs`

Fastest to set up. Opens via **Defenders → War Room / Control Room** in the
Unity menu bar.

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class WarRoomWindow : EditorWindow
{
    [MenuItem("Defenders/War Room / Control Room")]
    public static void ShowWindow() => GetWindow<WarRoomWindow>("⚔ War Room");

    private Vector2 _scroll;

    // Cached metric strings — refreshed via BackendAPI
    private string _dau            = "—";
    private string _revenueToday   = "—";
    private string _txCount        = "—";
    private string _skrStaked      = "—";

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // ── Header ────────────────────────────────────────────────────────────
        GUILayout.Label("📊  MANAGEMENT WAR ROOM", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(4);

        // ── Live Metrics ──────────────────────────────────────────────────────
        GUILayout.Label("📈  LIVE METRICS", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Total Players:",         _dau);
        EditorGUILayout.LabelField("Today Transactions:",   _txCount);
        EditorGUILayout.LabelField("Today Revenue (USD):",  _revenueToday);
        EditorGUILayout.LabelField("Total Staked SKR:",     _skrStaked);

        if (GUILayout.Button("🔄 Refresh Metrics"))
            RefreshMetrics();

        GUILayout.Space(8);

        // ── Revenue Breakdown ─────────────────────────────────────────────────
        GUILayout.Label("💰  Revenue Breakdown", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("SOL payments:", "—");
        EditorGUILayout.LabelField("SKR payments:", "—  (incl. staking bonuses)");
        EditorGUILayout.LabelField("USDC payments:", "—");
        EditorGUILayout.LabelField("IAP:", "—");

        GUILayout.Space(8);

        // ── Recent Transactions + Refund ──────────────────────────────────────
        GUILayout.Label("🔄  Recent Transactions", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("(Populate from BackendAPI after WO-80 is live)");
        if (GUILayout.Button("Refund Last SKR Payment"))
        {
            // TODO: Call BackendAPI.Refund(lastSignature)
            Debug.Log("[WarRoom] Refund requested — wire to WO-80 backend.");
        }

        GUILayout.Space(8);

        // ── Promo / Event Manager ─────────────────────────────────────────────
        GUILayout.Label("🎟️  Promo & Event Manager", EditorStyles.boldLabel);
        if (GUILayout.Button("Create New Promo Code"))
        {
            // TODO: Open promo creation dialog or call BackendAPI.CreatePromo(...)
            Debug.Log("[WarRoom] Promo creation — wire to WO-80 /api/promo.");
        }
        if (GUILayout.Button("Activate Double Lumber Weekend"))
        {
            // TODO: Toggle server-side event flag via BackendAPI
            Debug.Log("[WarRoom] Double Lumber Weekend activated.");
        }

        GUILayout.Space(8);

        // ── Social Media Hooks ────────────────────────────────────────────────
        GUILayout.Label("📣  Social Media Hooks", EditorStyles.boldLabel);
        if (GUILayout.Button("Post to X/Twitter: 'Wave 12 Clear Leaderboard!'"))
        {
            // TODO: Call BackendAPI → /api/webhook-twitter
            Debug.Log("[WarRoom] Twitter hook — wire to WO-80 webhook.");
        }
        if (GUILayout.Button("Post to Discord Announcement"))
        {
            // TODO: Call BackendAPI → /api/webhook-discord
            Debug.Log("[WarRoom] Discord hook — wire to WO-80 webhook.");
        }

        GUILayout.Space(8);

        // ── Escalations Queue ─────────────────────────────────────────────────
        GUILayout.Label("🚨  Escalations Queue", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("• Player X — stuck ATB (high priority)\n• Player Y — missing Aether after payment", MessageType.Warning);
        if (GUILayout.Button("Mark Selected Resolved"))
        {
            // TODO: PATCH /api/support-tickets/{id}/resolve
            Debug.Log("[WarRoom] Ticket marked resolved.");
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshMetrics()
    {
        // Once WO-80 backend is live, replace with a real HTTP call.
        // BackendAPI.GetWarRoomMetrics(json => { /* parse and update fields */ });
        _dau          = "Refresh wired in WO-80";
        _txCount      = "—";
        _revenueToday = "—";
        _skrStaked    = "—";
        Repaint();
    }
}
#endif
```

---

## Part B — Web War Room (Recommended Long-Term)

Once WO-80 (Vercel + Neon) is deployed, build a Next.js + Tailwind dashboard
that consumes the `/api/war-room/metrics` endpoint. Advantages over the Unity
Editor window:

- Accessible from any browser / phone (check revenue while away from your desk)
- Real-time metrics via Neon's serverless edge functions
- Multi-panel layout: metrics, transactions, promo CRUD, support queue
- Social posting webhooks integrated directly

### Recommended folder structure (same repo, `/web` or separate Vercel project)

```
web/
├── pages/
│   ├── index.tsx          ← main War Room dashboard
│   ├── transactions.tsx   ← full transaction log + refund
│   ├── promos.tsx         ← promo code manager
│   └── support.tsx        ← escalations queue
├── components/
│   ├── MetricCard.tsx
│   ├── TxRow.tsx
│   └── PromoEditor.tsx
└── lib/
    └── api.ts             ← thin wrapper over /api/* routes
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/Editor/WarRoomWindow.cs` | **Create** |
| `BackendAPI.cs` (Unity helper — see WO-80) | Wire HTTP calls to War Room buttons |
| `web/` folder (optional) | **Create** for web dashboard (post WO-80) |

---

## Acceptance Criteria

- [ ] **Defenders → War Room / Control Room** menu item opens the window
- [ ] Metrics section renders without errors (shows `—` placeholders until backend live)
- [ ] Refresh button calls `BackendAPI.GetWarRoomMetrics` without crashing
- [ ] All action buttons log correctly to the Console until backend endpoints are live
- [ ] Window is Editor-only (`#if UNITY_EDITOR`) — zero runtime overhead

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `site/admin.html, api/admin/stats.js:1-3` — War Room superseded by web admin. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
