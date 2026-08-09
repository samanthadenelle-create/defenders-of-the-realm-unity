// =============================================================================
// webbot.js - drive the DEPLOYED WebGL build in a real browser, capture screenshots
// + the live console, and report what the web build actually does.
// -----------------------------------------------------------------------------
// WHY: the AutoPilot fleet drives the Windows exe. The demo target is MOBILE WEB, and
// AutoPilot is compiled OUT of a release WebGL build (#if DEVELOPMENT_BUILD || UNITY_EDITOR,
// docs/WEB_SELF_HEAL_LOOP_PLAN.md step 2) - and we must never DEPLOY a Development build
// (it paints the full-screen error overlay). So the ship build cannot be bot-driven from
// inside. This drives it from OUTSIDE, as a real browser does.
//
// FEEDBACK PATH: Unity WebGL routes Debug.Log to the BROWSER CONSOLE, so page.on('console')
// gives us every [Flow:*] / MagentaGuard / TERRAINDIAG line LIVE - no DB round-trip, no
// ?trace=1 latency. (The DB path still works and is the record; this is the fast loop.)
//
// SCREENSHOTS: writes panel_<Screen>.png into the shots dir that build-ui-review.ps1 already
// reads (LocalLow/DeNelle/<productName>/ui-shots), so the EXISTING image-pair
// assembler pairs them against the Blink mockups in UI_REVIEW/_mapping.json. Reuses that
// system; does not invent a second one.
//
// Usage:
//   node webbot.js --url <deploy-url> [--seconds 120] [--headed] [--shots <dir>]
//   node webbot.js --uicapture --url <deploy-url> [--headed] [--shots <dir>]
//     ^ drives UICaptureMode's per-panel sweep (opens ?uicapture=1&trace=1), screenshots
//       each panel on its "[UICap] SHOWN <file>" console signal into panel_<file>.png, and
//       writes out/uicapture-report.json (each panel paired with its live trace lines).
// =============================================================================
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

function arg(name, def) {
    const i = process.argv.indexOf('--' + name);
    if (i === -1) return def;
    const v = process.argv[i + 1];
    return (v && !v.startsWith('--')) ? v : true;
}

const URL      = arg('url', 'https://defenders-of-the-realm-v2.vercel.app');
const SECONDS  = parseInt(arg('seconds', '120'), 10);
const HEADED   = !!arg('headed', false);
const UICAPTURE = !!arg('uicapture', false);   // drive UICaptureMode's per-panel sweep
// LocalLow/<companyName>/<productName>; productName became "Echoes of Elarion" 2026-08-08
// (store-listing match), which MOVES this folder. Prefer the new one, fall back to the legacy
// folder so shots captured by an older player still pair.
const SHOTS_NEW    = path.join(process.env.USERPROFILE || '', 'AppData', 'LocalLow', 'DeNelle', 'Echoes of Elarion', 'ui-shots');
const SHOTS_LEGACY = path.join(process.env.USERPROFILE || '', 'AppData', 'LocalLow', 'DeNelle', 'Defenders of the Realm', 'ui-shots');
const SHOTS    = arg('shots', (!fs.existsSync(SHOTS_NEW) && fs.existsSync(SHOTS_LEGACY)) ? SHOTS_LEGACY : SHOTS_NEW);
const OUT      = arg('out', path.join(__dirname, 'out'));

fs.mkdirSync(SHOTS, { recursive: true });
fs.mkdirSync(OUT, { recursive: true });

// Lines worth reporting. Mirrors websig-watch-daemon's SignalRx - a real failure, not every Warn.
const SIGNAL_RX = /MAGENTA|material='?NULL|Exception|NullReference|FAILED|not found in Resources|InternalError|softlock|error CS/i;
// Diagnostics we specifically want to SEE even when healthy (they answer "is the ground ok?").
const DIAG_RX   = /TERRAINDIAG|FloorDiag|MagentaGuard|HubStructureVisualInjector|VisualFactory/i;

(async () => {
    const browser = await chromium.launch({
        headless: !HEADED,
        args: HEADED
            // HEADED on a machine WITH a GPU: use the real GPU. SwiftShader (software GL)
            // mis-renders the Unity framebuffer to a bottom sub-rect (half-black shots) and is
            // NOT representative of the real device. Let Chrome pick the hardware GL context.
            ? ['--ignore-gpu-blocklist', '--no-sandbox']
            // HEADLESS has no GPU -> force SwiftShader or the canvas never paints (black).
            : ['--enable-unsafe-swiftshader', '--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--no-sandbox'],
    });
    const ctx = await browser.newContext({
        viewport: { width: 2340, height: 1080 },   // phone landscape (Seeker/Pi Browser target aspect ~2.17)
        deviceScaleFactor: 1,
    });
    const page = await ctx.newPage();

    const console_lines = [];
    const signals = [];
    const diags = [];
    const errors = [];
    const uicapLines = [];      // ordered console buffer consumed by --uicapture sweep
    let dragPanResult = null;   // set during --drive: did the web camera drag-pan engage?

    page.on('console', (msg) => {
        const t = msg.text();
        console_lines.push(t);
        if (SIGNAL_RX.test(t)) signals.push(t);
        if (DIAG_RX.test(t)) diags.push(t);
        if (UICAPTURE) uicapLines.push(t);
    });
    page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
    page.on('requestfailed', (r) => {
        // A failed asset request is a real defect class (the SFX/FSB bug was exactly this).
        errors.push('requestfailed: ' + r.url().split('/').pop() + ' -> ' + (r.failure() && r.failure().errorText));
    });

    let target = URL + (URL.includes('?') ? '&' : '?') + 'trace=1';
    if (UICAPTURE) target += '&uicapture=1';   // opt UICaptureMode into the WebGL sweep
    console.log('[webbot] opening ' + target);
    const t0 = Date.now();
    await page.goto(target, { waitUntil: 'domcontentloaded', timeout: 120000 });

    // ---- --uicapture: drive UICaptureMode's deterministic panel sweep ------------------
    // UICaptureMode (WebGL, ?uicapture=1) opens each UI panel in turn and emits a per-panel
    // console signal: "[UICap] SHOWING <file>", "[UICap] SHOWN <file>", "[UICap] DONE <file>",
    // then "[UICap] SWEEP COMPLETE count=N". We screenshot on each SHOWN (the panel is up and
    // held ~1.2s), pairing each shot with the [Flow:*]/Exception/SIGNAL lines seen while that
    // panel was on-screen. Real pixels come from THIS Playwright screenshot, not the in-build
    // ScreenCapture (which writes to unreachable browser storage).
    if (UICAPTURE) {
        // Wait for the WebGL canvas to exist + paint before the driver reaches the hub.
        let uiCanvasMs = null;
        for (let i = 0; i < SECONDS && !uiCanvasMs; i++) {
            await page.waitForTimeout(1000);
            const has = await page.$('#unity-canvas, canvas');
            if (has) { uiCanvasMs = Date.now() - t0; console.log('[webbot] canvas present at ' + uiCanvasMs + 'ms'); }
        }

        const shots = [];
        let idx = 0;
        let currentTrace = [];
        let sweepComplete = false;
        const uicapLog = [];   // every UICap harness line (drive diagnostics)
        const deadline = Date.now() + SECONDS * 1000;   // overall sweep timeout

        while (!sweepComplete && Date.now() < deadline) {
            while (idx < uicapLines.length) {
                const line = uicapLines[idx++];
                if (/UICap/i.test(line)) uicapLog.push(line.slice(0, 300));
                const mShown = line.match(/\[UICap\] SHOWN (\S+)/);
                if (mShown) {
                    const base = path.basename(mShown[1].trim()).replace(/\.png$/i, '');
                    currentTrace = [];
                    const shotPath = path.join(SHOTS, 'panel_' + base + '.png');
                    await page.screenshot({ path: shotPath });
                    shots.push({ panel: base, file: shotPath, trace: currentTrace });
                    console.log('[webbot] uicap shot ' + base + ' -> ' + shotPath);
                    continue;
                }
                if (/\[UICap\] SWEEP COMPLETE/.test(line)) { sweepComplete = true; continue; }
                // Attribute traces/exceptions/failures to the panel currently on-screen.
                if (shots.length > 0 && (/\[Flow:/.test(line) || /Exception|NullReference/i.test(line) || SIGNAL_RX.test(line))) {
                    currentTrace.push(line.slice(0, 300));
                }
            }
            await page.waitForTimeout(150);
        }

        const uiReport = {
            url: target,
            canvasFirstSeenMs: uiCanvasMs,
            sweepComplete: sweepComplete,
            timedOut: !sweepComplete,
            panelsCaptured: shots.length,
            shotsDir: SHOTS,
            uicapLog: uicapLog.slice(0, 200),
            panels: shots,
            signals: signals.slice(0, 60),
            errors: errors.slice(0, 40),
        };
        fs.writeFileSync(path.join(OUT, 'uicapture-report.json'), JSON.stringify(uiReport, null, 2));

        console.log('\n============ WEBBOT UICAPTURE REPORT ============');
        console.log('canvas first seen (ms) : ' + uiCanvasMs);
        console.log('panels captured        : ' + shots.length + (sweepComplete ? ' (SWEEP COMPLETE)' : ' (TIMED OUT)'));
        shots.forEach(s => console.log('   ++ panel_' + s.panel + '.png  (' + s.trace.length + ' trace lines)'));
        console.log('SIGNAL lines           : ' + signals.length);
        signals.slice(0, 12).forEach(s => console.log('   !! ' + s.slice(0, 200)));
        console.log('page/asset errors      : ' + errors.length);
        errors.slice(0, 10).forEach(s => console.log('   XX ' + s.slice(0, 200)));
        console.log('=================================================');

        await browser.close();
        return;
    }

    // Unity streams ~140MB; give it real time. Poll for the canvas to exist AND paint.
    let canvasSeenMs = null;
    for (let i = 0; i < SECONDS; i++) {
        await page.waitForTimeout(1000);
        if (!canvasSeenMs) {
            const has = await page.$('#unity-canvas, canvas');
            if (has) { canvasSeenMs = Date.now() - t0; console.log('[webbot] canvas present at ' + canvasSeenMs + 'ms'); }
        }
        // Periodic shots so we catch the boot sequence, not just the end state.
        if (i === 15 || i === 45 || i === 90 || i === SECONDS - 1) {
            const p = path.join(OUT, `t${String(i).padStart(3, '0')}.png`);
            await page.screenshot({ path: p });
            console.log('[webbot] shot ' + p);
        }
    }

    // The reachable screen for an un-driven ship build is whatever it booted to (Title).
    // Save it under the name the image-pair assembler expects so it pairs with the mockup.
    const titleShot = path.join(SHOTS, 'panel_Title.png');
    await page.screenshot({ path: titleShot });
    console.log('[webbot] wrote ' + titleShot);

    // ---- DRIVE IN (no AutoPilot in a ship build, so click the canvas like a player) -------
    // Coordinates are read off the captured Title at this exact viewport (1600x900). This is
    // deliberately dumb and will drift if the Title moves - the shot is the check: if the
    // world never loads, the console tells us (no Main_Castle_Overworld TERRAINDIAG).
    if (arg('drive', false)) {
        const canvas = await page.$('#unity-canvas, canvas');
        if (canvas) {
            const box = await canvas.boundingBox();
            const click = async (fx, fy, label) => {
                const x = box.x + box.width * fx, y = box.y + box.height * fy;
                console.log(`[webbot] click ${label} @ ${Math.round(x)},${Math.round(y)}`);
                await page.mouse.click(x, y);
            };
            // Title CTA row sits at ~y=0.90; Continue ~x=0.22, Start New ~x=0.50.
            await click(parseFloat(arg('cx', '0.22')), 0.90, arg('cbtn', 'Continue'));

            // Loading the hub streams the world; give it real time, shooting as it goes.
            for (let i = 0; i < 60; i++) {
                await page.waitForTimeout(1000);
                if (i === 10 || i === 30 || i === 59) {
                    const p = path.join(OUT, `world_t${String(i).padStart(3, '0')}.png`);
                    await page.screenshot({ path: p });
                    console.log('[webbot] world shot ' + p);
                }
            }
            await page.screenshot({ path: path.join(SHOTS, 'panel_World.png') });
            console.log('[webbot] wrote panel_World.png');

            // Exercise the single-pointer drag-to-pan (the web camera fix). A left-button drag
            // across the map should engage the pan path and emit [Flow:Build] ... ENGAGED. We
            // watch the console for that line to confirm the fix runs on the real web build.
            const b2 = await canvas.boundingBox();
            const cx = b2.x + b2.width * 0.5, cy = b2.y + b2.height * 0.55;
            const beforeCount = console_lines.length;
            await page.mouse.move(cx, cy);
            await page.mouse.down();
            for (let s = 0; s < 20; s++) { await page.mouse.move(cx - s * 12, cy - s * 4); await page.waitForTimeout(16); }
            await page.mouse.up();
            await page.waitForTimeout(600);
            dragPanResult = console_lines.slice(beforeCount).some(l => /drag-to-pan ENGAGED/i.test(l));
            console.log('[webbot] drag-to-pan ENGAGED seen in console: ' + dragPanResult);
            await page.screenshot({ path: path.join(OUT, 'world_afterdrag.png') });
        }
    }

    const report = {
        url: target,
        ranSeconds: SECONDS,
        canvasFirstSeenMs: canvasSeenMs,
        consoleLines: console_lines.length,
        dragPanEngaged: dragPanResult,
        signals: signals.slice(0, 60),
        diagnostics: diags.slice(0, 60),
        errors: errors.slice(0, 40),
    };
    fs.writeFileSync(path.join(OUT, 'report.json'), JSON.stringify(report, null, 2));

    console.log('\n=============== WEBBOT REPORT ===============');
    console.log('console lines captured : ' + console_lines.length);
    console.log('canvas first seen (ms) : ' + canvasSeenMs);
    console.log('SIGNAL lines           : ' + signals.length);
    signals.slice(0, 12).forEach(s => console.log('   !! ' + s.slice(0, 200)));
    console.log('DIAGNOSTIC lines       : ' + diags.length);
    diags.slice(0, 12).forEach(s => console.log('   .. ' + s.slice(0, 200)));
    console.log('page/asset errors      : ' + errors.length);
    errors.slice(0, 10).forEach(s => console.log('   XX ' + s.slice(0, 200)));
    console.log('=============================================');

    await browser.close();
})().catch(e => { console.error('[webbot] FATAL ' + e.message); process.exit(1); });
