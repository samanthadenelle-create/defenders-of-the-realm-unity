// introtest.js — click "Play Intro" on the deployed build and prove whether the video plays.
// SUCCESS marker: "[IntroSequence] Video playing full-screen."
// FAILURE markers: "falling back to slate", VideoPlayer errorReceived text.
const { chromium } = require('playwright');
const fs = require('fs'); const path = require('path');
function arg(n, d) { const i = process.argv.indexOf('--' + n); if (i === -1) return d; const v = process.argv[i + 1]; return (v && !v.startsWith('--')) ? v : true; }
const URL = arg('url', ''); const OUT = path.join(__dirname, 'out'); fs.mkdirSync(OUT, { recursive: true });

(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--enable-unsafe-swiftshader','--use-gl=angle','--use-angle=swiftshader','--ignore-gpu-blocklist','--no-sandbox','--autoplay-policy=no-user-gesture-required'] });
  const ctx = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const page = await ctx.newPage();
  const lines = [];
  page.on('console', m => lines.push(m.text()));
  page.on('pageerror', e => lines.push('pageerror: ' + e.message));

  await page.goto(URL + (URL.includes('?') ? '&' : '?') + 'trace=1', { waitUntil: 'domcontentloaded', timeout: 120000 });
  // wait for canvas + title
  for (let i = 0; i < 40; i++) { await page.waitForTimeout(1000); if (await page.$('#unity-canvas, canvas')) break; }
  await page.waitForTimeout(6000);
  await page.screenshot({ path: path.join(OUT, 'intro_pretitle.png') });

  const canvas = await page.$('#unity-canvas, canvas');
  const box = await canvas.boundingBox();
  // Focus the Unity canvas first (WebGL ignores input until focused) — a benign center tap,
  // then the real Play Intro click. Bottom row, rightmost button = Play Intro (~x0.806, y0.897).
  await page.mouse.click(box.x + box.width * 0.5, box.y + box.height * 0.5);
  await page.waitForTimeout(500);
  const bx = box.x + box.width * 0.806, by = box.y + box.height * 0.897;
  await page.mouse.move(bx, by); await page.mouse.down(); await page.waitForTimeout(90); await page.mouse.up();
  console.log('[introtest] clicked Play Intro @ ' + Math.round(bx) + ',' + Math.round(by));

  const t0 = Date.now();
  let verdict = 'UNKNOWN';
  for (let i = 0; i < 20; i++) {
    await page.waitForTimeout(1000);
    if (i === 3 || i === 8 || i === 15) await page.screenshot({ path: path.join(OUT, `intro_t${i}.png`) });
    if (lines.some(l => /Video playing full-screen/i.test(l))) { verdict = 'PLAYING'; break; }
    if (lines.some(l => /falling back to slate|Video failed to prepare|errorReceived/i.test(l))) { verdict = 'FALLBACK/ERROR'; break; }
  }
  await page.screenshot({ path: path.join(OUT, 'intro_final.png') });

  const intro = lines.filter(l => /IntroSequence|Video|slate|mp4|VideoPlayer|Intro/i.test(l));
  console.log('\n=========== INTRO TEST ===========');
  console.log('verdict: ' + verdict);
  console.log('total console lines: ' + lines.length);
  console.log('intro/video lines (' + intro.length + '):');
  intro.slice(0, 30).forEach(l => console.log('  ' + l.slice(0, 200)));
  console.log('--- last 18 console lines (context) ---');
  lines.slice(-18).forEach(l => console.log('  . ' + l.slice(0, 160)));
  console.log('==================================');
  fs.writeFileSync(path.join(OUT, 'intro_report.json'), JSON.stringify({ verdict, intro }, null, 2));
  await browser.close();
})().catch(e => { console.error('FATAL ' + e.message); process.exit(1); });
