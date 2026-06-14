import os, struct

ROOT = "Assets"
exts = (".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr")

def png_dim(p):
    with open(p, 'rb') as f:
        h = f.read(26)
    if h[:8] != b'\x89PNG\r\n\x1a\n':
        return None
    w, ht = struct.unpack('>II', h[16:24])
    return (w, ht)

def jpg_dim(p):
    with open(p, 'rb') as f:
        d = f.read()
    i = 2
    while i < len(d) - 9:
        if d[i] != 0xFF:
            i += 1
            continue
        m = d[i + 1]
        if 0xC0 <= m <= 0xCF and m not in (0xC4, 0xC8, 0xCC):
            ht, w = struct.unpack('>HH', d[i + 5:i + 9])
            return (w, ht)
        if m in (0xD8, 0xD9) or 0xD0 <= m <= 0xD7:
            i += 2
            continue
        seg = struct.unpack('>H', d[i + 2:i + 4])[0]
        i += 2 + seg
    return None

def dim(p):
    e = p.lower()
    try:
        if e.endswith('.png'):
            return png_dim(p)
        if e.endswith(('.jpg', '.jpeg')):
            return jpg_dim(p)
    except Exception:
        return None
    return None

def ships(p):
    pl = p.replace(os.sep, '/').lower()
    if '/resources/' in pl:
        return 'SHIP'
    if '/streamingassets/' in pl:
        return 'SHIP'
    if '/psd/' in pl or pl.endswith('.psd'):
        return 'SOURCE'
    if 'demo' in pl or 'example' in pl or 'simple_walk' in pl or '/sample' in pl:
        return 'SOURCE'
    return 'CHECK'

rows = []
for dp, _, fs in os.walk(ROOT):
    if '/library/' in dp.replace(os.sep, '/').lower():
        continue
    for fn in fs:
        if fn.lower().endswith(exts):
            fp = os.path.join(dp, fn)
            try:
                sz = os.path.getsize(fp)
            except Exception:
                continue
            if sz < 512 * 1024:
                continue
            rows.append((sz, ships(fp), dim(fp), fp.replace(os.sep, '/')))

rows.sort(reverse=True)

def fmt(d):
    return f"{d[0]}x{d[1]}" if d else "?"

tot = {'SHIP': 0, 'SOURCE': 0, 'CHECK': 0}
cnt = {'SHIP': 0, 'SOURCE': 0, 'CHECK': 0}
for sz, s, d, p in rows:
    tot[s] += sz
    cnt[s] += 1

print("=== TOTALS (files >=0.5MB on disk) ===")
for k in ('SHIP', 'CHECK', 'SOURCE'):
    print(f"  {k:6} {tot[k]/1048576:7.1f} MB  ({cnt[k]} files)")
print()
print("=== SHIP (Resources/ + StreamingAssets - ALWAYS in build) ===")
for sz, s, d, p in rows:
    if s == 'SHIP':
        print(f"{sz/1048576:6.1f}MB {fmt(d):>10}  {p}")
print()
print("=== CHECK (scene-ref possible - verify vs build report) top 30 ===")
n = 0
for sz, s, d, p in rows:
    if s == 'CHECK':
        print(f"{sz/1048576:6.1f}MB {fmt(d):>10}  {p}")
        n += 1
        if n >= 30:
            break
