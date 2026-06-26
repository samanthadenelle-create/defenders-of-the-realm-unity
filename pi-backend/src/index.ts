// =============================================================================
// Pi Payment Backend - standalone Cloudflare Worker (two-phase, server-mediated).
// V1-minimal. State in KV (no external DB). The secret PI_API_KEY lives ONLY here.
//
// CORRECTED against the real Pi Platform API (verified 2026-06-26):
//   - approve:  POST https://api.minepi.com/v2/payments/{payment_id}/approve   (id in PATH)
//   - complete: POST https://api.minepi.com/v2/payments/{payment_id}/complete  (body: { txid })
//   - auth header: `Authorization: Key <APIKEY>`   (NOT Bearer)
// Source: pi-apps/pi-platform-docs platform_API.md.
//
// Flow (client is the Unity WebGL game in the Pi Browser, via the .jslib bridge):
//   Pi.createPayment -> onReadyForServerApproval(paymentId)   -> bridge POSTs /approve
//                    -> onReadyForServerCompletion(paymentId, txid) -> bridge POSTs /complete
//   On /complete success the Unity side calls PackStore.ApplyPackContents (the local grant).
//   (V1 = client self-grants on a SERVER-VERIFIED completion. Harden later with a
//    server-held entitlement + a game-server webhook; fine for the proof-of-loop.)
// =============================================================================

export interface Env {
  PAYMENT_KV: KVNamespace;
  PI_API_KEY: string; // secret: `wrangler secret put PI_API_KEY`
  PI_APP_ID: string;  // secret: `wrangler secret put PI_APP_ID`
}

const PI_BASE = "https://api.minepi.com"; // swap for the sandbox base when testing on Testnet

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    const cors = {
      "Access-Control-Allow-Origin": "*", // TODO V1.1: tighten to the Pi app origin
      "Access-Control-Allow-Methods": "POST,OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
      "Content-Type": "application/json",
    };
    if (request.method === "OPTIONS") return new Response(null, { headers: cors });

    const piHeaders = { Authorization: `Key ${env.PI_API_KEY}`, "Content-Type": "application/json" };

    // ---- POST /approve : mark the payment approved on Pi's server ----
    if (url.pathname === "/approve" && request.method === "POST") {
      const { paymentId, amount, memo, userId } = await request.json<any>();
      if (!paymentId) return json({ error: "missing paymentId" }, 400, cors);

      // idempotency: don't re-approve a known payment
      const existing = await env.PAYMENT_KV.get(`pay:${paymentId}`);
      if (existing) return json({ approved: true, note: "already-known" }, 200, cors);

      await env.PAYMENT_KV.put(
        `pay:${paymentId}`,
        JSON.stringify({ paymentId, userId: userId ?? "unknown", amount, memo, status: "pending", ts: Date.now() }),
        { expirationTtl: 60 * 60 * 24 } // 24h
      );

      const pr = await fetch(`${PI_BASE}/v2/payments/${paymentId}/approve`, { method: "POST", headers: piHeaders });
      if (!pr.ok) return json({ approved: false, status: pr.status, body: await safeText(pr) }, 400, cors);

      return json({ approved: true }, 200, cors);
    }

    // ---- POST /complete : prove the txid to Pi, mark completed ----
    if (url.pathname === "/complete" && request.method === "POST") {
      const { paymentId, txid } = await request.json<any>();
      if (!paymentId || !txid) return json({ error: "missing paymentId or txid" }, 400, cors);

      const rec = await env.PAYMENT_KV.get(`pay:${paymentId}`, "json") as any;
      if (!rec) return json({ error: "unknown paymentId" }, 404, cors);
      if (rec.status === "completed") return json({ success: true, entitlement: rec.entitlement ?? "pi_pack_small", note: "already-completed" }, 200, cors);

      const pr = await fetch(`${PI_BASE}/v2/payments/${paymentId}/complete`, {
        method: "POST",
        headers: piHeaders,
        body: JSON.stringify({ txid }),
      });
      if (!pr.ok) return json({ success: false, status: pr.status, body: await safeText(pr) }, 400, cors);

      rec.status = "completed";
      rec.txid = txid;
      rec.entitlement = "pi_pack_small"; // V1: one pack type; map per-pack later
      await env.PAYMENT_KV.put(`pay:${paymentId}`, JSON.stringify(rec), { expirationTtl: 60 * 60 * 24 * 30 });

      return json({ success: true, entitlement: rec.entitlement }, 200, cors);
    }

    // ---- POST /reconcile : for incompletePaymentFound on app start ----
    if (url.pathname === "/reconcile" && request.method === "POST") {
      const { paymentId, txid } = await request.json<any>();
      if (!paymentId) return json({ error: "missing paymentId" }, 400, cors);
      const rec = await env.PAYMENT_KV.get(`pay:${paymentId}`, "json") as any;
      if (rec && rec.status === "completed") return json({ reconciled: true, entitlement: rec.entitlement }, 200, cors);
      // unknown/pending + a txid => try to complete it now
      if (txid) {
        const pr = await fetch(`${PI_BASE}/v2/payments/${paymentId}/complete`, { method: "POST", headers: piHeaders, body: JSON.stringify({ txid }) });
        if (pr.ok) {
          await env.PAYMENT_KV.put(`pay:${paymentId}`, JSON.stringify({ paymentId, txid, status: "completed", entitlement: "pi_pack_small", ts: Date.now() }), { expirationTtl: 60 * 60 * 24 * 30 });
          return json({ reconciled: true, entitlement: "pi_pack_small" }, 200, cors);
        }
      }
      return json({ reconciled: false }, 200, cors);
    }

    return json({ error: "not found" }, 404, cors);
  },
};

function json(obj: unknown, status: number, headers: Record<string, string>): Response {
  return new Response(JSON.stringify(obj), { status, headers });
}
async function safeText(r: Response): Promise<string> {
  try { return (await r.text()).slice(0, 300); } catch { return ""; }
}
