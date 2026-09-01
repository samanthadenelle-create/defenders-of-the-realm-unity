import io, subprocess, os
ROOT = r'D:\eoa'

MSG = """fix(vfx): commit the mirror ASSETS the impact prefabs reference

Second incomplete-commit catch of the session, same class as the Defense
namespace, found by checking the tree before a push instead of trusting the
path list I staged from.

The mirror builder produced more than the five prefabs already committed:

  Assets/Resources/VFX/_Shared/Materials/  (+ Textures/)   64 untracked files
  Assets/Editor/VfxArtMirrorManifest.json  +128 source->mirror pairs
  Assets/Editor/VfxMirrorRedirect.cs       registers the WO-887 surface set

Without the manifest and the redirect, the mirrors EXIST and nothing resolves to
them. Without _Shared, the mirrored prefabs reference materials that are not in
the repo - so they render correctly on this machine and PINK on any other clone.
That is the same shape as the missing-R2-bundle trap in CLAUDE.md section 16:
art present locally, absent for everyone else, and the build does not complain.

Also here: SiegeCadenceRegression case 5 message. It read "the loop must be
byte-identical to pre-WO-1026 until the owner rules the loss stakes" - the
stakes ARE ruled now and FeatureFlags.Siege defaults ON, so the sentence
described a world that no longer exists. It now says the kill switch must be
absolute, which is what the case actually asserts once the flag is on by
default. The assertion is unchanged; only its explanation was stale.

PROCESS NOTE, because this is twice: staging by explicit path is correct per
CLAUDE.md section 11 and it is what stops one seat clobbering another - but it
silently drops whatever you forget to NAME, and a whole generated directory is
easy to forget. The Codex seat's WO-1141 specifies the missing half and I have
adopted it: after staging, diff --cached --name-only against the intended
allowlist and treat any unexplained path - or any expected path that is ABSENT -
as a stop condition. Ran that here; it is what caught these 67 files.
"""

def sh(a):
    return subprocess.run(a, capture_output=True, text=True, cwd=ROOT)

io.open(os.path.join(ROOT, '.git', 'CMSG'), 'w', encoding='utf-8', newline='\n').write(
    MSG + "\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n")
c = sh(['git', 'commit', '-F', '.git/CMSG'])
print((c.stdout or c.stderr)[-200:])
