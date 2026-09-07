import io, subprocess, os
from pathlib import Path

# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
# location, never hardcode a drive letter. dev/tmp/<script>.py -> parents[2].
ROOT = str(Path(__file__).resolve().parents[2])
def sh(a): return subprocess.run(a, capture_output=True, text=True, cwd=ROOT)
def commit(paths, msg):
    real=[]
    for p in paths:
        real.append(p)
        m=p+'.meta'
        if os.path.exists(os.path.join(ROOT,m)): real.append(m)
    sh(['git','add','--']+real)
    st=sh(['git','diff','--cached','--name-only']).stdout.strip()
    if not st:
        print('  (nothing staged) SKIP'); return
    io.open(os.path.join(ROOT,'.git','CMSG'),'w',encoding='utf-8',newline='\n').write(msg+"\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n")
    c=sh(['git','commit','-F','.git/CMSG'])
    print('  committed %d file(s)'%len(st.splitlines()), '' if c.returncode==0 else 'ERR '+(c.stderr or c.stdout)[:300])
