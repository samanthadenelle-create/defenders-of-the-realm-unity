// =============================================================================
// api/admin/console.js - THE COMMAND CENTER CONSOLE (WO-1244), the surface
// WO-1169 specced.
// -----------------------------------------------------------------------------
// Serves ONE self-contained HTML page. No framework, no build step, no CDN, no
// webfonts, no remote images - WO-1244: "It must work one-handed on a phone."
// The owner will see an exploit or a failed purchase on her phone, not at a
// desk, and a control she cannot reach in seconds is not a control.
//
// ⛔ WHY THE PAGE ITSELF IS NOT KEY-GATED, SAID PLAINLY.
// -----------------------------------------------------------------------------
// A browser NAVIGATION cannot send an X-Admin-Key header, and putting the key in
// the URL would write it into history, the address bar, referrers and every log
// on the way. So the SHELL is public and carries no data at all: it is markup
// and script, and it knows nothing until a key is typed into it. Every byte of
// data comes from api/admin/stats.js (read, ADMIN_DASH_KEY) and
// api/admin/ops.js (write, ADMIN_DASH_KEY + ADMIN_OPS_KEY). This is exactly the
// shape site/admin.html already uses, and the shell is noindex.
//
// ⛔ THE KEYS ARE NEVER STORED. They live in two JS variables for the life of
// the tab. Not localStorage, not sessionStorage, not a cookie, not the URL.
// Reloading asks again, by design.
//
// ⛔ TWO HALVES, KEPT APART AT THE ENDPOINT, NOT IN THIS UI.
//   READ  -> GET  /api/admin/stats?view=overview    (players and telemetry)
//            GET  /api/admin/stats?view=purchases   (money, server-settled)
//            GET  /api/admin/stats?view=ops         (toggles, promos, issues)
//   WRITE -> POST /api/admin/ops                    (second key, four actions)
// The read endpoints cannot write. Nothing this page does can change that,
// which is the point of putting the boundary there instead of here.
//
// ⛔ COLOUR IS NEVER THE SIGNAL. The owner is red/green colourblind. Every state
// on this page is a WORD - "CLOSED", "open", "ALERT", "ACTIVE", "DISABLED",
// "verified", "unverified" - and the one accent colour is used for emphasis
// only, never to carry meaning that is not also written out.
//
// ⛔ NO WALLET, NO EMAIL, NO REAL NAME IS EVER RENDERED. Player ids arrive
// already masked from stats.js; promo bindings arrive as a boolean; bug reports
// arrive as "verified" / "unverified". There is no field on this page that can
// display an address, and no request it makes returns one.
//
// ASCII ONLY IN THE PAGE (WO-1244 rule 6). Every byte of the served HTML, CSS
// and script below is 7-bit ASCII - no em-dashes, no arrows, no glyphs - so it
// survives every transport this repo has ever mangled text through. The house
// marks in THIS file's comments are the only exception and they never ship: they
// stop at the PAGE template. test/command-center.test.js pins that.
// =============================================================================

const PAGE = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="robots" content="noindex, nofollow">
<title>Elarion Command Center</title>
<style>
  :root{
    --ink:#07060a; --panel:#12111a; --panel2:#191823; --line:#2b2937;
    --text:#ece8f5; --dim:#9a94ad; --accent:#e8b84b; --tap:48px;
  }
  *{box-sizing:border-box}
  html,body{margin:0;padding:0}
  body{background:var(--ink);color:var(--text);
    font:16px/1.5 ui-sans-serif,system-ui,-apple-system,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
    -webkit-text-size-adjust:100%;padding-bottom:env(safe-area-inset-bottom)}
  .wrap{max-width:900px;margin:0 auto;padding:12px}
  header{position:sticky;top:0;z-index:5;background:var(--ink);border-bottom:1px solid var(--line);
    padding:10px 12px;display:flex;gap:8px;align-items:center;justify-content:space-between;flex-wrap:wrap}
  .brand{font-weight:700;letter-spacing:.03em}
  .brand span{color:var(--dim);font-weight:400;font-size:13px;display:block}
  button,input,select,textarea{font:inherit}
  button{background:var(--panel2);color:var(--text);border:1px solid var(--line);border-radius:10px;
    padding:12px 14px;min-height:var(--tap);cursor:pointer}
  button:active{transform:translateY(1px)}
  button.primary{background:var(--accent);color:#1a1405;border-color:var(--accent);font-weight:700}
  button[aria-pressed="true"]{border-color:var(--accent);color:var(--accent);font-weight:700}
  input,select,textarea{background:var(--panel2);color:var(--text);border:1px solid var(--line);
    border-radius:10px;padding:12px;min-height:var(--tap);width:100%}
  textarea{min-height:80px}
  label{display:block;margin:12px 0 5px;color:var(--dim);font-size:13px}
  nav{display:flex;gap:6px;overflow-x:auto;padding:10px 12px 0;-webkit-overflow-scrolling:touch}
  nav button{white-space:nowrap;padding:10px 14px}
  .card{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:14px;margin:12px 0}
  .card h2{margin:0 0 4px;font-size:16px}
  .note{color:var(--dim);font-size:13px;margin:4px 0 0}
  .tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px}
  .tile{background:var(--panel2);border:1px solid var(--line);border-radius:10px;padding:12px}
  .tile .k{color:var(--dim);font-size:11px;text-transform:uppercase;letter-spacing:.08em}
  .tile .v{font-size:26px;font-weight:700;line-height:1.15;margin-top:2px}
  .tile .s{color:var(--dim);font-size:12px;margin-top:2px}
  .hero{background:linear-gradient(145deg,#211a12 0%,var(--panel) 52%);border-color:#5d4923;
    padding:18px;overflow:hidden;position:relative}
  .hero:after{content:"";position:absolute;width:190px;height:190px;border:1px solid #5d4923;
    border-radius:50%;right:-90px;top:-110px;opacity:.55}
  .eyebrow{color:var(--accent);font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.12em}
  .hero-number{font-size:52px;font-weight:800;line-height:1;margin:8px 0 4px;letter-spacing:-.04em}
  .metric-grid{display:grid;grid-template-columns:1.3fr 1fr 1fr;gap:10px;margin-top:14px}
  .metric-grid .tile:first-child{border-color:#5d4923}
  .chart{display:flex;align-items:flex-end;gap:5px;height:150px;padding:18px 2px 0;border-bottom:1px solid var(--line)}
  .bar-wrap{height:100%;flex:1;min-width:8px;display:flex;align-items:flex-end;position:relative}
  .bar{width:100%;min-height:2px;background:var(--accent);border-radius:4px 4px 0 0;opacity:.82}
  .bar-wrap:hover .bar,.bar-wrap:focus .bar{opacity:1}
  .chart-key{display:flex;justify-content:space-between;color:var(--dim);font-size:11px;margin-top:6px}
  .coverage{height:8px;background:var(--panel2);border-radius:10px;overflow:hidden;margin:10px 0 5px}
  .coverage span{display:block;height:100%;background:var(--accent)}
  .row{display:flex;gap:8px;flex-wrap:wrap;align-items:center}
  .grow{flex:1 1 180px}
  .scroll{overflow-x:auto;-webkit-overflow-scrolling:touch}
  table{border-collapse:collapse;width:100%;min-width:460px;font-size:14px}
  th,td{text-align:left;padding:8px;border-bottom:1px solid var(--line);white-space:nowrap;vertical-align:top}
  th{color:var(--dim);font-size:11px;text-transform:uppercase;letter-spacing:.06em}
  td.wrapcell{white-space:normal;min-width:200px}
  .state{display:inline-block;border:1px solid var(--line);border-radius:6px;padding:2px 8px;
    font-size:12px;font-weight:700;letter-spacing:.04em}
  .state.on{border-color:var(--accent);color:var(--accent)}
  .alert{border-color:var(--accent)}
  .alert h2{color:var(--accent)}
  .toggle{border:1px solid var(--line);border-radius:12px;padding:12px;margin:10px 0;background:var(--panel2)}
  .toggle .top{display:flex;justify-content:space-between;gap:8px;align-items:center;flex-wrap:wrap}
  .toggle .name{font-weight:700;letter-spacing:.04em;text-transform:uppercase}
  .muted{color:var(--dim)}
  .msg{margin:10px 0;padding:10px;border:1px solid var(--line);border-radius:10px;
    background:var(--panel2);font-size:14px;white-space:pre-wrap}
  .msg.bad{border-color:var(--accent)}
  .gate{max-width:440px;margin:8vh auto}
  code{background:var(--panel2);border:1px solid var(--line);border-radius:5px;padding:1px 5px;font-size:12px}
  .none{color:var(--dim);font-style:italic}
  @media (max-width:520px){ .wrap{padding:8px} .tile .v{font-size:22px} .hero-number{font-size:44px}
    .metric-grid{grid-template-columns:1fr 1fr}.metric-grid .tile:first-child{grid-column:1/-1} }
</style>
</head>
<body>

<section id="gate" class="wrap gate">
  <div class="card">
    <h2>Elarion Command Center</h2>
    <p class="note">Owner only. The key is held in memory for this tab and is never saved
      anywhere - reloading asks again, by design.</p>
    <label for="key">Admin key (read)</label>
    <input id="key" type="password" autocomplete="off" spellcheck="false" placeholder="ADMIN_DASH_KEY">
    <div class="row" style="margin-top:14px">
      <button class="primary grow" id="enter">Open</button>
    </div>
    <p class="note" id="gateMsg"></p>
    <p class="note">Flipping a toggle or authoring a promo needs a SECOND key
      (<code>ADMIN_OPS_KEY</code>). You are asked for it the first time you write, not now.</p>
  </div>
</section>

<div id="app" hidden>
  <header>
    <div class="brand">Command Center<span id="stamp">loading</span></div>
    <div class="row">
      <select id="days" aria-label="Window" style="width:auto;min-width:120px">
        <option value="7">7 days</option>
        <option value="30" selected>30 days</option>
        <option value="90">90 days</option>
      </select>
      <button id="refresh">Refresh</button>
    </div>
  </header>
  <nav id="tabs">
    <button data-tab="players" aria-pressed="true">Players</button>
    <button data-tab="toggles" aria-pressed="false">Toggles</button>
    <button data-tab="money" aria-pressed="false">Money</button>
    <button data-tab="issues" aria-pressed="false">Player issues</button>
    <button data-tab="promos" aria-pressed="false">Promos</button>
    <button data-tab="board" aria-pressed="false">Tickets</button>
  </nav>
  <div class="wrap" id="body"></div>
</div>

<script>
(function(){
  'use strict';

  // In-memory only. Never persisted, never put in a URL. See the file header.
  var READ_KEY = null;
  var OPS_KEY = null;

  var state = { tab:'players', days:30, overview:null, ops:null, money:null, err:null };

  var $ = function(id){ return document.getElementById(id); };

  function esc(v){
    if (v === null || v === undefined) return '';
    return String(v).replace(/[&<>"']/g, function(c){
      return c === '&' ? '&amp;' : c === '<' ? '&lt;' : c === '>' ? '&gt;'
           : c === '"' ? '&quot;' : '&#39;';
    });
  }
  function when(iso){
    if (!iso) return 'never';
    var t = Date.parse(iso);
    if (!isFinite(t)) return esc(iso);
    var mins = Math.round((Date.now() - t) / 60000);
    var ago = mins < 1 ? 'just now'
            : mins < 60 ? mins + ' min ago'
            : mins < 1440 ? Math.round(mins/60) + ' h ago'
            : Math.round(mins/1440) + ' d ago';
    return new Date(t).toISOString().replace('T',' ').slice(0,16) + ' (' + ago + ')';
  }
  function n(v){ return (v === null || v === undefined) ? '-' : String(v); }
  function usd(v){
    if (v === null || v === undefined) return '-';
    return '$' + (Math.round(Number(v)*100)/100).toFixed(2);
  }

  function getJson(url){
    return fetch(url, { method:'GET', headers:{ 'X-Admin-Key': READ_KEY } })
      .then(function(r){ return r.json().then(function(j){ return { status:r.status, body:j }; }); });
  }

  // Every WRITE goes here, and only here. Separate endpoint, second key.
  function postOps(payload){
    if (!OPS_KEY){
      OPS_KEY = window.prompt('Write key (ADMIN_OPS_KEY). Asked once per tab; never saved.');
      if (!OPS_KEY) return Promise.resolve({ status:0, body:{ ok:false, code:'CANCELLED' } });
    }
    payload.by = 'console';
    return fetch('/api/admin/ops', {
      method:'POST',
      headers:{ 'Content-Type':'application/json', 'X-Admin-Key':READ_KEY, 'X-Admin-Ops-Key':OPS_KEY },
      body: JSON.stringify(payload)
    }).then(function(r){ return r.json().then(function(j){ return { status:r.status, body:j }; }); })
      .catch(function(e){ return { status:0, body:{ ok:false, code:'NETWORK', hint:String(e) } }; });
  }

  function load(){
    $('stamp').textContent = 'loading';
    var d = state.days;
    return Promise.all([
      getJson('/api/admin/stats?view=overview&days=' + d),
      getJson('/api/admin/stats?view=ops&days=' + d),
      getJson('/api/admin/stats?view=purchases&days=' + d)
    ]).then(function(res){
      state.err = null;
      state.overview = res[0].status === 200 ? res[0].body : null;
      state.ops = res[1].status === 200 ? res[1].body : null;
      state.money = res[2].status === 200 ? res[2].body : null;
      if (!state.overview) state.err = 'player metrics failed: ' + esc((res[0].body && res[0].body.error) || res[0].status);
      else if (!state.ops) state.err = 'ops read failed: ' + esc((res[1].body && res[1].body.error) || res[1].status);
      $('stamp').textContent = 'read ' + new Date().toISOString().replace('T',' ').slice(0,16);
      render();
    }).catch(function(e){
      state.err = 'network: ' + String(e);
      render();
    });
  }

  // ---- players and telemetry --------------------------------------------
  function renderPlayers(){
    var o = state.overview;
    if (!o) return '<div class="card"><p class="none">Player telemetry is unavailable.</p></div>';
    var a = o.active || {};
    var rows = (o.per_day || []).slice().reverse();
    var newest = rows.length ? rows[rows.length - 1] : null;
    var max = rows.reduce(function(m,r){ return Math.max(m,Number(r.active_players)||0); },1);
    var knownSessions = Number(a.sessions_30d || 0);
    var anonSessions = Number((o.anonymous || {}).sessions || 0);
    var coverage = knownSessions + anonSessions ? Math.round(knownSessions * 100 / (knownSessions + anonSessions)) : 0;
    var chart = rows.map(function(r){
      var value = Number(r.active_players)||0;
      var height = Math.max(2,Math.round(value * 100 / max));
      return '<div class="bar-wrap" tabindex="0" title="' + esc(r.day) + ': ' + value +
        ' active players, ' + n(r.sessions) + ' sessions"><div class="bar" style="height:' + height + '%"></div></div>';
    }).join('');
    var h = '<div class="card hero"><div class="eyebrow">Live player pulse</div>' +
      '<div class="hero-number">' + n(a.today) + '</div><h2>Active players in the last 24 hours</h2>' +
      '<p class="note">Unique identified players who opened the game. Refreshed ' + when(o.generated_at) + '.</p>' +
      '<div class="metric-grid">' +
      '<div class="tile"><div class="k">7-day active</div><div class="v">' + n(a.d7) + '</div><div class="s">unique players</div></div>' +
      '<div class="tile"><div class="k">30-day active</div><div class="v">' + n(a.d30) + '</div><div class="s">unique players</div></div>' +
      '<div class="tile"><div class="k">Sessions today</div><div class="v">' + n(a.sessions_today) + '</div><div class="s">game opens</div></div>' +
      '</div></div>';
    h += '<div class="card"><h2>Daily active players</h2><p class="note">One bar per UTC day. Focus a bar for its exact count.</p>' +
      (rows.length ? '<div class="chart" aria-label="Daily active-player chart">' + chart + '</div><div class="chart-key"><span>' +
        esc(rows[0].day) + '</span><span>Peak ' + max + '</span><span>' + esc(newest.day) + '</span></div>'
        : '<p class="none">No identified-player sessions in this window.</p>') + '</div>';
    h += '<div class="card"><h2>Telemetry health</h2><div class="tiles">' +
      '<div class="tile"><div class="k">Identified coverage</div><div class="v">' + coverage + '%</div><div class="s">known sessions vs all sessions</div></div>' +
      '<div class="tile"><div class="k">Anonymous sessions</div><div class="v">' + anonSessions + '</div><div class="s">cannot be counted as people</div></div>' +
      '<div class="tile"><div class="k">Events received</div><div class="v">' + n((o.totals||{}).total_events) + '</div><div class="s">all time</div></div></div>' +
      '<div class="coverage" aria-label="Identified telemetry coverage ' + coverage + ' percent"><span style="width:' + coverage + '%"></span></div>' +
      '<p class="note">' + esc((o.anonymous||{}).note || '') + '</p></div>';
    var fresh = (o.new_players_per_day || []).slice(0,7);
    h += '<div class="card"><h2>New players</h2><p class="note">First-ever identified event, grouped by UTC day.</p>' +
      '<div class="scroll"><table><tr><th>Day</th><th>New players</th><th>Active players</th><th>Sessions</th></tr>';
    if (!fresh.length) h += '<tr><td colspan="4" class="none">No new identified players in this window.</td></tr>';
    fresh.forEach(function(r){
      var day = (o.per_day || []).filter(function(x){ return x.day === r.day; })[0] || {};
      h += '<tr><td>' + esc(r.day) + '</td><td>' + n(r.new_players) + '</td><td>' + n(day.active_players) + '</td><td>' + n(day.sessions) + '</td></tr>';
    });
    return h + '</table></div></div>';
  }

  // ---- toggles ------------------------------------------------------------
  function renderToggles(){
    var o = state.ops;
    if (!o) return '<div class="card"><p class="none">No data.</p></div>';
    var t = o.toggles || {};
    var h = '';
    if (t.server_closed){
      h += '<div class="card alert"><h2>SERVER IS CLOSED</h2><p class="note">' +
           'The whole game is sealed. Every other area is closed too, whatever its own row says.' +
           '</p></div>';
    }
    h += '<div class="card"><h2>Kill switches</h2>' +
         '<p class="note">' + esc(t.note || '') + ' Sealed now: ' + n(t.sealed_count) + ' of ' +
         ((t.areas || []).length) + '. Enforcement takes about 5 s; the player banner about 40 s.</p>';
    (t.areas || []).forEach(function(a){
      h += '<div class="toggle" data-area="' + esc(a.area) + '">' +
             '<div class="top"><span class="name">' + esc(a.area) + '</span>' +
             '<span class="state' + (a.closed ? ' on' : '') + '">' + esc(a.state) + '</span></div>' +
             '<p class="note">Last flipped ' + when(a.updated_at) + ' by ' +
                esc(a.updated_by || 'nobody') + '.' +
                (a.note ? ' ' + esc(a.note) : '') + '</p>' +
             (a.message ? '<p class="note">Banner: ' + esc(a.message) + '</p>' : '') +
             (a.closed
               ? '<div class="row" style="margin-top:8px"><button class="primary open-btn">Re-open ' +
                 esc(a.area) + '</button></div>'
               : '<label>Banner text players will see (required to seal)</label>' +
                 '<input class="seal-msg" type="text" maxlength="200" autocomplete="off" ' +
                 'placeholder="Raids are closed while we fix an exploit.">' +
                 '<div class="row" style="margin-top:8px"><button class="seal-btn">Seal ' +
                 esc(a.area) + '</button></div>') +
           '</div>';
    });
    h += '</div>';

    h += '<div class="card"><h2>Recent operator writes</h2><p class="note">' +
         'Every write this console makes leaves a row. Attribution and timestamp, not a colour.</p>' +
         '<div class="scroll"><table><tr><th>When</th><th>Action</th><th>Target</th>' +
         '<th>Outcome</th><th>By</th></tr>';
    var hist = o.ops_history || [];
    if (!hist.length) h += '<tr><td colspan="5" class="none">No writes recorded yet.</td></tr>';
    hist.forEach(function(r){
      h += '<tr><td>' + when(r.at) + '</td><td>' + esc(r.action) + '</td><td>' + esc(r.target) +
           '</td><td>' + esc(r.outcome) + '</td><td>' + esc(r.operator) + '</td></tr>';
    });
    h += '</table></div></div>';
    return h;
  }

  // ---- money --------------------------------------------------------------
  function renderMoney(){
    var m = state.money;
    if (!m) return '<div class="card"><p class="none">No purchase data (read failed or not configured).</p></div>';
    var s = m.settled || {};
    var f = m.quote_funnel || {};
    var d = m.disagreement || {};
    var orphans = d.client_events_without_entitlement || [];

    var h = '';

    // THE ALERT FIRST. A client-completed purchase with no entitlement is a
    // grant that may have been handed out with nothing settled behind it.
    h += '<div class="card' + (orphans.length ? ' alert' : '') + '">' +
         '<h2>' + (orphans.length ? 'ALERT: ' + orphans.length + ' client purchase(s) with NO server entitlement'
                                  : 'No client/server disagreement in this window') + '</h2>' +
         '<p class="note">' + esc(d.note || '') + '</p>';
    if (orphans.length){
      h += '<div class="scroll"><table><tr><th>Tx signature</th><th>Pack</th><th>Player</th>' +
           '<th>Events</th><th>Latest</th><th>Review</th></tr>';
      orphans.forEach(function(r){
        h += '<tr><td class="wrapcell">' + esc(r.tx_signature) + '</td><td>' + esc(r.pack_id) +
             '</td><td>' + esc(r.player_masked) + '</td><td>' + n(r.events) + '</td><td>' +
             when(r.latest) + '</td><td><button class="purchase-ack" data-tx="' +
             esc(r.tx_signature) + '">Acknowledge - no action</button></td></tr>';
      });
      h += '</table></div>';
    }
    h += '</div>';

    // Client and server side by side, NEVER blended into one number.
    h += '<div class="card"><h2>Client said / server settled</h2>' +
         '<p class="note">Two different questions. They are shown apart on purpose; a blended ' +
         'figure would hide exactly the disagreement above.</p><div class="tiles">' +
         '<div class="tile"><div class="k">Client reported</div><div class="v">' +
            n(d.client_completed_events) + '</div><div class="s">purchase_completed events</div></div>' +
         '<div class="tile"><div class="k">Server settled</div><div class="v">' +
            n(d.server_settled_window) + '</div><div class="s">entitlements in window</div></div>' +
         '<div class="tile"><div class="k">Settled, client silent</div><div class="v">' +
            n(d.settled_without_client_event) + '</div><div class="s">analytics understates sales</div></div>' +
         '</div></div>';

    h += '<div class="card"><h2>Settled purchases (server truth)</h2><div class="tiles">' +
         '<div class="tile"><div class="k">All time</div><div class="v">' + n(s.all_time) +
            '</div><div class="s">' + n(s.all_time_buyers) + ' buyers</div></div>' +
         '<div class="tile"><div class="k">All time USD</div><div class="v">' + usd(s.all_time_usd_anchor) +
            '</div><div class="s">usd_anchor sum</div></div>' +
         '<div class="tile"><div class="k">Window</div><div class="v">' + n(s.window) +
            '</div><div class="s">' + n(s.window_buyers) + ' buyers</div></div>' +
         '<div class="tile"><div class="k">Window USD</div><div class="v">' + usd(s.window_usd_anchor) +
            '</div><div class="s">last settled ' + when(s.last_settled_at) + '</div></div>' +
         '</div><p class="note">' + esc(m.revenue_note || '') + '</p></div>';

    h += '<div class="card"><h2>Quote to settle funnel</h2>' +
         '<p class="note">' + esc(f.definition || '') + '</p><div class="tiles">' +
         '<div class="tile"><div class="k">Issued</div><div class="v">' + n(f.issued) + '</div></div>' +
         '<div class="tile"><div class="k">Consumed</div><div class="v">' + n(f.consumed) +
            '</div><div class="s">' + (f.consumed_pct === null || f.consumed_pct === undefined
                ? 'no data yet' : f.consumed_pct + '%') +
            (f.low_n ? ' - low n, unreliable' : '') + '</div></div>' +
         '<div class="tile"><div class="k">Expired unpaid</div><div class="v">' +
            n(f.expired_unconsumed) + '</div></div>' +
         '<div class="tile"><div class="k">Live now</div><div class="v">' + n(f.live) + '</div></div>' +
         '</div></div>';

    var na = m.needs_attention || {};
    var naRows = na.rows || [];
    h += '<div class="card' + (naRows.length ? ' alert' : '') + '"><h2>' +
         (naRows.length ? naRows.length + ' settlement(s) NOT marked fulfilled' : 'Every settlement is fulfilled') +
         '</h2><p class="note">' + esc(na.note || '') + ' This console cannot re-grant: that is a ' +
         'write on the money tables and it does not exist here.</p>';
    if (naRows.length){
      h += '<div class="scroll"><table><tr><th>When</th><th>SKU</th><th>Status</th><th>USD</th>' +
           '<th>Player</th></tr>';
      naRows.forEach(function(r){
        h += '<tr><td>' + when(r.created_at) + '</td><td>' + esc(r.sku) + '</td><td>' +
             esc(String(r.status).toUpperCase()) + '</td><td>' + usd(r.usd_anchor) + '</td><td>' +
             esc(r.wallet_masked) + '</td></tr>';
      });
      h += '</table></div>';
    }
    h += '</div>';
    return h;
  }

  // ---- player issues ------------------------------------------------------
  function renderIssues(){
    var o = state.ops;
    if (!o) return '<div class="card"><p class="none">No data.</p></div>';
    var r = o.reports || {};
    var rows = r.rows || [];
    var h = '<div class="card"><h2>Player issues (' + rows.length + ')</h2>' +
            '<p class="note">' + esc(r.note || '') + ' Identity reads "verified" or "unverified"; ' +
            'a burst of unverified means auth is broken, which is itself the signal. No address is ' +
            'shown here, ever.</p>';
    if (!rows.length){
      h += '<p class="none">No reports. bug_reports has never accepted a row on some deployments - ' +
           'an empty list is not proof the channel works.</p>';
    } else {
      h += '<div class="scroll"><table><tr><th>Id</th><th>When</th><th>What</th><th>Route</th>' +
           '<th>Version</th><th>Platform</th><th>Identity</th><th>Shot</th></tr>';
      rows.forEach(function(x){
        h += '<tr><td>' + n(x.report_id) + '</td><td>' + when(x.created_at) + '</td>' +
             '<td class="wrapcell">' + esc(x.description) + '</td><td>' + esc(x.route) + '</td>' +
             '<td>' + esc(x.app_version) + '</td><td>' + esc(x.platform) + '</td>' +
             '<td>' + esc(x.identity) + '</td><td>' + (x.has_screenshot ? 'yes' : 'no') + '</td></tr>';
      });
      h += '</table></div>';
    }
    h += '</div>';
    return h;
  }

  // ---- promos -------------------------------------------------------------
  function renderPromos(){
    var o = state.ops;
    if (!o) return '<div class="card"><p class="none">No data.</p></div>';
    var p = o.promos || {};
    var rows = p.rows || [];
    var h = '<div class="card"><h2>Author a promo code</h2>' +
      '<p class="note">Set a PACK SKU or crystals/coins, never both - the sku would silently win, ' +
      'so this refuses instead. Blank caps mean unlimited. Codes are stored uppercase because the ' +
      'client uppercases before sending.</p>' +
      '<label for="pc">Code</label><input id="pc" type="text" autocomplete="off" spellcheck="false" placeholder="LAUNCH2026">' +
      '<label for="ppack">Reward pack sku (optional)</label><input id="ppack" type="text" autocomplete="off" placeholder="hearth-spark">' +
      '<div class="row"><div class="grow"><label for="pcry">Crystals</label>' +
      '<input id="pcry" type="number" inputmode="numeric" min="0" placeholder="0"></div>' +
      '<div class="grow"><label for="pcoin">Coins</label>' +
      '<input id="pcoin" type="number" inputmode="numeric" min="0" placeholder="0"></div></div>' +
      '<label for="pmsg">Message shown to the player (optional)</label>' +
      '<input id="pmsg" type="text" maxlength="200" autocomplete="off">' +
      '<div class="row"><div class="grow"><label for="pmax">Max redemptions (blank = unlimited)</label>' +
      '<input id="pmax" type="number" inputmode="numeric" min="0"></div>' +
      '<div class="grow"><label for="pper">Per player limit (blank = none)</label>' +
      '<input id="pper" type="number" inputmode="numeric" min="0"></div></div>' +
      '<label for="pexp">Expires (blank = never)</label><input id="pexp" type="datetime-local">' +
      '<div class="row" style="margin-top:12px"><button class="primary grow" id="pcreate">Create code</button></div>' +
      '<p class="note">Private, wallet-bound codes are NOT authored here on purpose: it would mean ' +
      'typing an address into a page and reading it back out of a list. Use the SQL editor for those. ' +
      'This console can see THAT a code is bound and never to whom.</p></div>';

    h += '<div class="card"><h2>Codes (' + rows.length + ')</h2><p class="note">' + esc(p.note || '') +
         '</p><div class="scroll"><table><tr><th>Code</th><th>State</th><th>Grants</th>' +
         '<th>Used</th><th>Cap</th><th>Expires</th><th>Bound</th><th></th></tr>';
    if (!rows.length) h += '<tr><td colspan="8" class="none">No promo codes yet.</td></tr>';
    rows.forEach(function(c){
      var grants = c.reward_pack_sku ? ('pack ' + c.reward_pack_sku)
        : ([c.reward_crystals ? c.reward_crystals + ' crystals' : null,
            c.reward_coins ? c.reward_coins + ' coins' : null].filter(Boolean).join(' + ') || 'nothing');
      h += '<tr data-code="' + esc(c.code) + '">' +
           '<td>' + esc(c.code) + '</td>' +
           '<td><span class="state' + (c.state === 'ACTIVE' ? ' on' : '') + '">' + esc(c.state) + '</span></td>' +
           '<td class="wrapcell">' + esc(grants) + '</td>' +
           '<td>' + n(c.redemptions) + '</td>' +
           '<td>' + (c.max_redemptions === null ? 'unlimited' : n(c.max_redemptions)) + '</td>' +
           '<td>' + (c.expires_at ? when(c.expires_at) : 'never') + '</td>' +
           '<td>' + (c.is_bound ? 'private' : 'public') + '</td>' +
           '<td><button class="promo-flip" data-code="' + esc(c.code) + '" data-active="' +
              (c.active ? '1' : '0') + '">' + (c.active ? 'Disable' : 'Enable') + '</button></td></tr>';
    });
    h += '</table></div></div>';
    return h;
  }

  function renderBoard(){
    return '<div class="card"><h2>Tickets</h2>' +
      '<p class="note">There are TWO ticket systems and they are deliberately not merged.</p>' +
      '<p class="note"><strong>Dev work</strong> lives in <code>WorkOrders/*.md</code> and is ' +
      'rendered by <code>python tools/board_build.py</code> into <code>BOARD.html</code> at the repo ' +
      'root. The repo is the source of truth, so the board is derived and cannot drift. It is a local ' +
      'file and is not served from the internet.</p>' +
      '<p class="note"><strong>Player issues</strong> are the "Player issues" tab here, read from ' +
      '<code>bug_reports</code>. Do not fold them into BOARD.html: it is generated, so anything ' +
      'written there is overwritten on the next run.</p></div>';
  }

  function render(){
    var h = '';
    if (state.err) h += '<div class="msg bad">' + esc(state.err) + '</div>';
    if (state.flash) h += '<div class="msg' + (state.flashBad ? ' bad' : '') + '">' + esc(state.flash) + '</div>';
    h += state.tab === 'players' ? renderPlayers()
       : state.tab === 'toggles' ? renderToggles()
       : state.tab === 'money' ? renderMoney()
       : state.tab === 'issues' ? renderIssues()
       : state.tab === 'promos' ? renderPromos()
       : renderBoard();
    $('body').innerHTML = h;
  }

  function flash(text, bad){
    state.flash = text; state.flashBad = !!bad;
    render();
  }

  function opsResult(r, okText){
    if (r.body && r.body.ok){
      flash(okText + ' ' + (r.body.state ? ('State is now ' + r.body.state + '.') : '') +
            (r.body.note ? ' ' + r.body.note : '') +
            (r.body.warning ? ' WARNING: ' + r.body.warning : ''), false);
      load();
      return;
    }
    var code = (r.body && r.body.code) || ('HTTP ' + r.status);
    if (code === 'OPS_UNAUTHORIZED') OPS_KEY = null;
    flash('REFUSED: ' + code + ((r.body && r.body.hint) ? ' - ' + r.body.hint : ''), true);
  }

  // ---- wiring -------------------------------------------------------------
  $('enter').addEventListener('click', function(){
    var k = $('key').value.trim();
    if (!k){ $('gateMsg').textContent = 'A key is required.'; return; }
    READ_KEY = k;
    $('gateMsg').textContent = 'Checking...';
    getJson('/api/admin/stats?view=ops&days=30').then(function(r){
      if (r.status !== 200){
        READ_KEY = null;
        $('gateMsg').textContent = 'Refused: ' + ((r.body && r.body.error) || r.status);
        return;
      }
      $('key').value = '';
      $('gate').hidden = true;
      $('app').hidden = false;
      load();
    }).catch(function(e){
      READ_KEY = null;
      $('gateMsg').textContent = 'Network error: ' + e;
    });
  });
  $('key').addEventListener('keydown', function(e){ if (e.key === 'Enter') $('enter').click(); });

  $('tabs').addEventListener('click', function(e){
    var b = e.target.closest('button[data-tab]');
    if (!b) return;
    state.tab = b.getAttribute('data-tab');
    state.flash = null;
    Array.prototype.forEach.call($('tabs').querySelectorAll('button'), function(x){
      x.setAttribute('aria-pressed', x === b ? 'true' : 'false');
    });
    render();
  });

  $('refresh').addEventListener('click', function(){ state.flash = null; load(); });
  $('days').addEventListener('change', function(){ state.days = Number($('days').value) || 30; load(); });

  $('body').addEventListener('click', function(e){
    var seal = e.target.closest('.seal-btn');
    if (seal){
      var box = seal.closest('.toggle');
      var area = box.getAttribute('data-area');
      var msg = (box.querySelector('.seal-msg') || {}).value || '';
      if (!msg.trim()){ flash('REFUSED: MESSAGE_REQUIRED_TO_SEAL - the player banner has nothing to say without it.', true); return; }
      if (!window.confirm('Seal ' + area + '? Players will be refused server-side within about 5 seconds.')) return;
      seal.disabled = true;
      postOps({ action:'maintenance.seal', area:area, message:msg })
        .then(function(r){ opsResult(r, 'Sealed ' + area + '.'); });
      return;
    }
    var open = e.target.closest('.open-btn');
    if (open){
      var area2 = open.closest('.toggle').getAttribute('data-area');
      if (!window.confirm('Re-open ' + area2 + '? This also clears the banner text.')) return;
      open.disabled = true;
      postOps({ action:'maintenance.open', area:area2 })
        .then(function(r){ opsResult(r, 'Re-opened ' + area2 + '.'); });
      return;
    }
    var flip = e.target.closest('.promo-flip');
    if (flip){
      var code = flip.getAttribute('data-code');
      var makeActive = flip.getAttribute('data-active') !== '1';
      if (!window.confirm((makeActive ? 'Enable ' : 'Disable ') + code + '?')) return;
      flip.disabled = true;
      postOps({ action:'promo.set_active', code:code, active:makeActive })
        .then(function(r){ opsResult(r, (makeActive ? 'Enabled ' : 'Disabled ') + code + '.'); });
      return;
    }
    var ack = e.target.closest('.purchase-ack');
    if (ack){
      var tx = ack.getAttribute('data-tx');
      if (!window.confirm('Acknowledge this mismatch as reviewed with no refund or grant? The source event stays in history.')) return;
      ack.disabled = true;
      postOps({ action:'purchase.alert_acknowledge', txSignature:tx,
                reason:'Reviewed false positive; no payment or entitlement action required.' })
        .then(function(r){ opsResult(r, 'Acknowledged purchase alert. Source telemetry preserved.'); });
      return;
    }
    if (e.target.id === 'pcreate'){
      var draft = {
        action:'promo.create',
        code: $('pc').value,
        rewardPackSku: $('ppack').value,
        rewardCrystals: $('pcry').value,
        rewardCoins: $('pcoin').value,
        message: $('pmsg').value,
        maxRedemptions: $('pmax').value,
        perPlayerLimit: $('pper').value,
        expiresAt: $('pexp').value ? new Date($('pexp').value).toISOString() : ''
      };
      if (!window.confirm('Create code ' + String(draft.code).toUpperCase() + '? It grants value to every player who redeems it.')) return;
      e.target.disabled = true;
      postOps(draft).then(function(r){ opsResult(r, 'Created ' + (r.body && r.body.code) + '.'); });
    }
  });
})();
</script>
</body>
</html>`;

module.exports = async (req, res) => {
    if (req.method !== 'GET') {
        return res.status(400).json({ error: 'Method not allowed' });
    }
    // The shell carries no data, but it is still not something to index or cache.
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    res.setHeader('Cache-Control', 'no-store, must-revalidate');
    res.setHeader('X-Robots-Tag', 'noindex, nofollow');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Referrer-Policy', 'no-referrer');
    return res.status(200).send(PAGE);
};

module.exports.PAGE = PAGE;
