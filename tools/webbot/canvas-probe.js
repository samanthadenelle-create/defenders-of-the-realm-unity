const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch({ headless:false, args:['--enable-unsafe-swiftshader','--use-gl=angle','--use-angle=swiftshader','--ignore-gpu-blocklist','--no-sandbox'] });
  const ctx = await b.newContext({ viewport:{width:2340,height:1080}, deviceScaleFactor:1 });
  const p = await ctx.newPage();
  await p.goto('http://localhost:8000?trace=1', { waitUntil:'domcontentloaded', timeout:120000 });
  await p.waitForTimeout(20000);
  const info = await p.evaluate(() => {
    const c = document.querySelector('#unity-canvas, canvas');
    const r = c ? c.getBoundingClientRect() : null;
    return {
      viewport: { w: window.innerWidth, h: window.innerHeight },
      canvasRect: r ? { x:Math.round(r.x), y:Math.round(r.y), w:Math.round(r.width), h:Math.round(r.height) } : null,
      canvasAttr: c ? { width:c.width, height:c.height, styleW:c.style.width, styleH:c.style.height } : null,
      containerHTML: (document.querySelector('#unity-container,.webgl-content,#gameContainer')||{}).outerHTML?.slice(0,200) || 'none',
    };
  });
  console.log(JSON.stringify(info, null, 2));
  await b.close();
})().catch(e=>{console.error('FATAL',e.message);process.exit(1);});
