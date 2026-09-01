import io, subprocess, os
ROOT=r'D:\eoa'
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
