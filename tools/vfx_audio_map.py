#!/usr/bin/env python3
"""
vfx_audio_map.py - generate docs/reference/VFX_AUDIO_WIRING_MAP.md FROM the repo.

WHAT EXISTS / WHAT IS WIRED / WHAT IS ORPHANED, for VFX prefabs and audio, without
anyone having to trace code. Same contract as tools/board_build.py: the repo is the
single source of truth, this doc is a DERIVED VIEW that is cheap to regenerate, so it
cannot drift the way a hand-maintained map does.

    python tools/vfx_audio_map.py            # report only, always exits 0
    python tools/vfx_audio_map.py --check    # exits 1 if any BROKEN row exists

THE QUESTION THIS ANSWERS. Every gate in this repo asserts a thing EXISTS; almost none
assert it is CONSUMED (docs/reference/AUDIT_2026-08-09.md, the headline finding). The
catalogs ARE the authoritative inventory of VFX prefabs and sounds - so the interesting
number is not what is in them, it is which rows anything actually asks for.

    WIRED      declared + an asset resolves + at least one caller
    ORPHAN     declared, nothing calls it              -> debt, a work queue, NOT a failure
    BROKEN     a caller asks for a key/id that resolves to NOTHING at runtime  -> defect
    FALLBACK   called, no prefab, but the runtime documents a procedural stand-in
    UNRESOLVED a call site whose key is built at runtime; the link CANNOT be proven here

BROKEN is the only --check failure. ORPHAN is recorded, never gated: the owner paid for
these packs, so "declared in a catalog, wired to nothing" is a purchase waiting to ship.
UNRESOLVED is counted separately and never folded into ORPHAN - inflating the work queue
with keys that are actually in use is the fastest way to make this map distrusted.

STATIC ONLY. Parses .asset / .meta / .cs / .json text. Unity never runs. See the
"Blind spots" section of the emitted doc for exactly what that cannot see.
"""
import os, re, sys, json, glob, datetime
from collections import defaultdict, OrderedDict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
OUT = os.path.join(ROOT, "docs", "reference", "VFX_AUDIO_WIRING_MAP.md")

HOVL_CATALOG = "Assets/Resources/VFX/HovlVfxCatalog.asset"
VFX_CATALOG = "Assets/Resources/VFX/VFXCatalog.asset"
VFXTYPE_CS = "Assets/_Modules/Village/Vfx/VFXType.cs"
SFXID_CS = "Assets/_Modules/Audio/SfxId.cs"
MUSICTRACK_CS = "Assets/_Modules/Core/Audio/MusicTrack.cs"
PROCEDURAL_SFX_CS = "Assets/_Modules/Audio/ProceduralSfx.cs"
AUDIO_BOOTSTRAP_CS = "Assets/_Modules/Audio/AudioBootstrap.cs"
SELFCONTAIN_CS = "Assets/Editor/Regression/VfxResourceSelfContainmentRegression.cs"

# The SfxClipLibrary Resources path AudioService loads (const in AudioService.cs).
SFX_LIBRARY_RESOURCE = "Audio/SfxClipLibrary"

# Code corpus. Everything that can hold a call site; the gitignored art packs hold no code.
CODE_DIRS = ["Assets/_Modules", "Assets/Editor", "Assets/Tests"]
# Data corpus - key strings live in DATA as often as in code (motion-castings.json wires
# vfxKey / vfxProjectile / vfxImpact / sfxId). A data reference IS a consumer; missing them
# is how an orphan list comes to name keys that are demonstrably in use.
DATA_DIRS = ["Assets/Resources", "Assets/StreamingAssets", "Assets/Data",
             "Assets/_Modules", "Assets/Editor", "Assets/Dialogue"]
DATA_EXTS = (".json", ".asset")
MAX_FILE_BYTES = 4_000_000
AUDIO_EXTS = (".mp3", ".wav", ".ogg", ".aiff", ".aif")

# Files that are the DECLARATION or the PLUMBING of a key, not a consumer of it. A key
# referenced only from here is still an orphan - VFXManager naming its own fallback does
# not make an effect reachable by gameplay (audit: "9 reachable only from VFXManager's own
# fallback").
#
# PER DOMAIN, deliberately. VFXManager is plumbing for VFX KEYS - it is the facade every
# PlayKey goes through - but it is a genuine CONSUMER of SfxId, because VfxToSfx() pairs a
# played VFXType with a sound. Folding the two together marked 10 SfxId values ORPHAN when
# the audit's own measurement is that all 16 have a call site; the honest statement is that
# those 10 fire ONLY through that pairing, which is a note on a WIRED row, not an orphan.
VFX_SELF = {
    "Assets/_Modules/Village/Vfx/VFXType.cs",
    "Assets/_Modules/Village/Vfx/VFXCatalog.cs",
    "Assets/_Modules/Village/Vfx/HovlVfxCatalog.cs",
    "Assets/_Modules/Village/Vfx/VFXManager.cs",
    "Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs",
}
SFX_SELF = {
    "Assets/_Modules/Audio/SfxId.cs",
    "Assets/_Modules/Audio/SfxClipLibrary.cs",
    "Assets/_Modules/Audio/ProceduralSfx.cs",
    "Assets/_Modules/Audio/AudioService.cs",
}
MUSIC_SELF = {
    "Assets/_Modules/Core/Audio/MusicTrack.cs",
    "Assets/_Modules/Audio/MusicTrack.cs",
    "Assets/_Modules/Audio/AudioService.cs",
    "Assets/_Modules/Audio/AudioBootstrap.cs",
    "Assets/_Modules/Core/Audio/IAudioService.cs",
}
# The VFX->SFX pairing lives here; an SfxId whose only runtime caller is this file is
# played, but only as a rider on a VFXType.
VFX_SFX_PAIRING = "Assets/_Modules/Village/Vfx/VFXManager.cs"

# The one DATED, CITED number in this file: what the 2026-08-09 reverse audit measured BY
# HAND for the same two VFX inventories. Kept so the doc can say whether the number moved -
# and labelled as a cross-check, not a regression bar, because that audit's "consumed" was
# a human judgement and this tool's is a mechanical rule. They will not agree exactly, and
# a tool that pretends they should would be manufacturing a false alarm every re-run.
AUDIT_BASELINE = {
    "hovl": (62, 140, "docs/reference/STACK_UTILIZATION_2026-08-09.md - 'Hovl VFX keys 62/140 (44%)'"),
    "vfxtype": (76, 95, "docs/reference/STACK_UTILIZATION_2026-08-09.md - 'VFXType ordinals 76/95 (80%)'"),
}


# ---------------------------------------------------------------------------
#  small io helpers
# ---------------------------------------------------------------------------
def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def read(relpath, limit=None):
    p = os.path.join(ROOT, relpath.replace("/", os.sep))
    try:
        with open(p, encoding="utf-8", errors="replace") as f:
            return f.read() if limit is None else f.read(limit)
    except OSError:
        return ""


_BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.S)
_LINE_COMMENT = re.compile(r"//[^\n]*")


def strip_comments(text):
    return _LINE_COMMENT.sub("", _BLOCK_COMMENT.sub("", text))


# ---------------------------------------------------------------------------
#  1. enum inventories
# ---------------------------------------------------------------------------
def parse_enum(relpath, enum_name):
    """[(member, ordinal)] in declaration order. Handles explicit '= N' and implicit runs."""
    text = read(relpath)
    if not text:
        return []
    m = re.search(r"\benum\s+" + re.escape(enum_name) + r"\b", text)
    if not m:
        return []
    i = text.find("{", m.end())
    if i < 0:
        return []
    depth, j = 0, i
    while j < len(text):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
            if depth == 0:
                break
        j += 1
    body = strip_comments(text[i + 1:j])
    out, nxt = [], 0
    for tok in body.split(","):
        tok = tok.strip()
        if not tok:
            continue
        mm = re.match(r"^([A-Za-z_]\w*)\s*(?:=\s*(-?\d+))?$", tok)
        if not mm:
            continue
        val = int(mm.group(2)) if mm.group(2) is not None else nxt
        out.append((mm.group(1), val))
        nxt = val + 1
    return out


# ---------------------------------------------------------------------------
#  2. catalog inventories (.asset YAML, read as text - Unity never runs)
# ---------------------------------------------------------------------------
_GUID_RE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")


def parse_hovl_catalog():
    """[(key, guid_or_None, lineno)] in file order. Duplicate keys are preserved (last wins
    at runtime, BuildLookup) so the doc can report the shadowing."""
    rows, cur = [], None
    for n, line in enumerate(read(HOVL_CATALOG).splitlines(), 1):
        km = re.match(r"\s*-\s*Key:\s*(.*)$", line)
        if km:
            if cur:
                rows.append(cur)
            cur = [km.group(1).strip(), None, n]
            continue
        if cur is not None and re.match(r"\s*Prefab:", line):
            g = _GUID_RE.search(line)
            cur[1] = g.group(1) if g else None
    if cur:
        rows.append(cur)
    return [tuple(r) for r in rows]


def parse_vfx_catalog():
    """[(ordinal, guid_or_None, lineno)] from VFXCatalog.asset Entries[]."""
    rows, cur = [], None
    for n, line in enumerate(read(VFX_CATALOG).splitlines(), 1):
        tm = re.match(r"\s*-\s*Type:\s*(-?\d+)\s*$", line)
        if tm:
            if cur:
                rows.append(cur)
            cur = [int(tm.group(1)), None, n]
            continue
        if cur is not None and re.match(r"\s*Prefab:", line):
            g = _GUID_RE.search(line)
            cur[1] = g.group(1) if g else None
    if cur:
        rows.append(cur)
    return [tuple(r) for r in rows]


# ---------------------------------------------------------------------------
#  3. guid -> asset path, and the gitignored-art-root rule
# ---------------------------------------------------------------------------
def gitignored_art_roots():
    """Read the roots out of VfxResourceSelfContainmentRegression.cs rather than copying
    them. That file declares itself the SINGLE HOME of this rule, and its own header states
    why: two derivations of one rule is how a tool and its gate come to disagree while both
    report success."""
    text = read(SELFCONTAIN_CS)
    m = re.search(r"GitignoredArtRoots\s*=\s*\{(.*?)\};", text, re.S)
    if not m:
        return []
    return re.findall(r'"([^"]+)"', m.group(1))


def build_prefab_guid_index():
    """guid -> 'Assets/...' path, from *.prefab.meta only. Catalog Prefab fields are prefab
    references, so scanning 8.8k prefab metas beats scanning 61k metas for the same answer."""
    idx = {}
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        for fn in filenames:
            if not fn.endswith(".prefab.meta"):
                continue
            full = os.path.join(dirpath, fn)
            try:
                with open(full, encoding="utf-8", errors="replace") as f:
                    head = f.read(400)
            except OSError:
                continue
            g = _GUID_RE.search(head)
            if g:
                idx[g.group(1)] = rel(full)[:-5]  # drop '.meta'
    return idx


def resolve_prefab(guid, guid_idx, ignored_roots):
    """(state, path). state in RESOLVED | GITIGNORED | MISSING | NULL."""
    if not guid or guid == "0" * 32:
        return "NULL", ""
    path = guid_idx.get(guid)
    if not path:
        return "MISSING", ""
    for r in ignored_roots:
        if path.lower().startswith(r.lower()):
            return "GITIGNORED", path
    return "RESOLVED", path


# ---------------------------------------------------------------------------
#  4. corpus scan - one pass, everything the map needs
# ---------------------------------------------------------------------------
_STRING_RE = re.compile(r'"((?:[^"\\\n]|\\.)*)"')
_ENUMREF_RE = re.compile(r"\b(VFXType|SfxId|MusicTrack)\.([A-Za-z_]\w*)")
_RESLOAD_AUDIO_RE = re.compile(r'Resources\.Load\s*<\s*AudioClip\s*>\s*\(\s*([^)]*)')
_PLAYKEY_RE = re.compile(r"\bPlayKey\s*\(\s*([^,)]*)")
_SFXPOS_RE = re.compile(r"\bPlaySfxAtPosition\s*\(\s*([^,)]*)")
_PLAYMUSIC_RE = re.compile(r"\bPlayMusic\s*\(\s*([^,)]*)")
_PLAYVFX_RE = re.compile(
    r"\bPlay(?:Impact|Aura|Loop|Oneshot|Projectile|Casting|Death|Environment|Dungeon|At)\s*\(\s*([^,)]*)")


def file_kind(path):
    p = "/" + path
    if "/Tests/" in p:
        return "test"
    if "/Editor/" in p:
        return "editor"
    return "runtime"


# An argument that is a TYPE + PARAMETER NAME is a method DECLARATION, not a call. Without
# this, every 'void PlayMusic(MusicTrack track)' signature and every interface member counted
# as an unprovable dynamic call site - noise that would make the UNRESOLVED list unreadable
# and, worse, unbelievable.
_PARAM_DECL_RE = re.compile(r"^[A-Za-z_][\w.<>\[\], ]*\s+[A-Za-z_]\w*$")
# An argument that mentions any enum member IS statically resolvable, however it is spelled -
# fully qualified (DeNelle.Audio.SfxId.LevelUp) or inside a ternary
# (won ? MusicTrack.Victory : MusicTrack.Defeat). The enum-reference pass already counted both.
_ENUM_ARG_RE = re.compile(r"\b(?:VFXType|SfxId|MusicTrack)\.[A-Za-z_]\w*")


def scan_code():
    """Returns (strings, enumrefs, dynamic, load_sites, files_scanned).
       strings   : literal -> [(path, kind, line)]
       enumrefs  : (Enum, Member) -> [(path, kind, line)]
       dynamic   : [(path, line, api, expr)]  - call sites whose key is not a literal
       load_sites: 'Sfx/X' -> [(path, kind, line, has_null_coalesce)]
    """
    strings = defaultdict(list)
    enumrefs = defaultdict(list)
    load_sites = defaultdict(list)
    dynamic = []
    count = 0
    for d in CODE_DIRS:
        base = os.path.join(ROOT, d.replace("/", os.sep))
        for dirpath, dirnames, filenames in os.walk(base):
            for fn in filenames:
                if not fn.endswith(".cs"):
                    continue
                full = os.path.join(dirpath, fn)
                rp = rel(full)
                try:
                    if os.path.getsize(full) > MAX_FILE_BYTES:
                        continue
                    with open(full, encoding="utf-8", errors="replace") as f:
                        text = f.read()
                except OSError:
                    continue
                count += 1
                kind = file_kind(rp)
                for n, line in enumerate(text.splitlines(), 1):
                    # Comments are stripped per line so a key that only appears in a doc
                    # comment does NOT count as a caller. A commented example is not wiring.
                    code = _LINE_COMMENT.sub("", line)
                    if not code.strip():
                        continue
                    for s in _STRING_RE.findall(code):
                        strings[s].append((rp, kind, n))
                    for e, mem in _ENUMREF_RE.findall(code):
                        enumrefs[(e, mem)].append((rp, kind, n))

                    # Literal audio loads, WITH whether the call falls back (`?? Generate…`).
                    # A missing file behind a `??` is not broken; it is a documented stand-in.
                    for m in re.finditer(r'Resources\.Load\s*<\s*AudioClip\s*>\s*\(\s*"([^"]+)"\s*\)', code):
                        tail = code[m.end():m.end() + 60]
                        load_sites[m.group(1)].append((rp, kind, n, "??" in tail))

                    # Call-site shapes are matched on a line whose STRING LITERALS ARE BLANKED,
                    # so a log message that merely mentions PlayMusic(Victory) or an interpolated
                    # $"PlayKey('{key}')" trace is never mistaken for a call. Literals are captured
                    # separately above/below, where they can be matched exactly.
                    blanked = _STRING_RE.sub('""', code)
                    for api, rx in (("PlayKey", _PLAYKEY_RE), ("PlaySfxAtPosition", _SFXPOS_RE),
                                    ("PlayMusic", _PLAYMUSIC_RE), ("VFXManager.Play*", _PLAYVFX_RE),
                                    ("Resources.Load<AudioClip>", _RESLOAD_AUDIO_RE)):
                        for arg in rx.findall(blanked):
                            a = arg.strip()
                            if not a or a == '""':
                                continue            # empty, or a literal handled elsewhere
                            if _ENUM_ARG_RE.search(a):
                                continue            # statically resolvable enum reference
                            if _PARAM_DECL_RE.match(a):
                                continue            # a method signature, not a call
                            dynamic.append((rp, n, api, a[:90]))
    return strings, enumrefs, dynamic, load_sites, count


def scan_data():
    """Returns (values, files_scanned): every string VALUE that appears in a data file,
    mapped to {(file, kind)}. Catches motion-castings.json vfxKey/sfxId wiring.

    KIND MATTERS HERE, badly. Assets/Editor/VfxCasterLibraryIndex.json is an EDITOR index
    that lists every pack key by name; counting it as a consumer marked all 140 Hovl keys
    WIRED and produced a zero-orphan map - i.e. exactly the false all-clear this tool exists
    to prevent. An editor index proves a key was CATALOGUED, never that anything plays it.
    """
    values = defaultdict(set)
    seen, count = set(), 0
    for d in DATA_DIRS:
        base = os.path.join(ROOT, d.replace("/", os.sep))
        for dirpath, dirnames, filenames in os.walk(base):
            for fn in filenames:
                if not fn.endswith(DATA_EXTS):
                    continue
                full = os.path.join(dirpath, fn)
                rp = rel(full)
                if rp in seen:
                    continue
                seen.add(rp)
                if rp in (HOVL_CATALOG, VFX_CATALOG):
                    continue  # the inventory itself is not a consumer of itself
                try:
                    if os.path.getsize(full) > MAX_FILE_BYTES:
                        continue
                    with open(full, encoding="utf-8", errors="replace") as f:
                        text = f.read()
                except OSError:
                    continue
                count += 1
                kind = "editor" if file_kind(rp) == "editor" else "data"
                for s in _STRING_RE.findall(text):
                    if s and len(s) < 120:
                        values[s].add((rp, kind))
    return values, count


def index_resources():
    """resource-path (no extension, forward slashes, lowercase) -> [asset paths] for every
    audio file under any Assets/**/Resources/ root - how Resources.Load<AudioClip>(name)
    actually resolves."""
    idx = defaultdict(list)
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        p = rel(dirpath)
        m = re.search(r"(?:^|/)Resources(?:/|$)", p)
        if not m:
            continue
        head = p[:m.end()].rstrip("/")
        if not head.endswith("Resources"):
            continue
        for fn in filenames:
            if not fn.lower().endswith(AUDIO_EXTS):
                continue
            full = rel(os.path.join(dirpath, fn))
            resname = full[len(head) + 1:]
            resname = os.path.splitext(resname)[0]
            idx[resname.lower()].append(full)
    return idx


# ---------------------------------------------------------------------------
#  5. buckets
# ---------------------------------------------------------------------------
class Row(object):
    __slots__ = ("key", "asset", "asset_note", "callers", "bucket", "note", "self_files")

    def __init__(self, key, self_files=frozenset()):
        self.key = key
        self.asset = ""
        self.asset_note = ""
        self.callers = []      # [(path, kind, line)]
        self.bucket = ""
        self.note = ""
        self.self_files = self_files

    def caller_kinds(self):
        return set(k for _, k, _ in self.callers)

    def real_callers(self):
        """Callers that are neither the declaration/plumbing of the key nor test/editor-only.
        This is the audit's CONSUMED definition: reachable on a path a player can hit."""
        return [c for c in self.callers
                if c[1] in ("runtime", "data") and c[0] not in self.self_files]

    def caller_summary(self, limit=3):
        if not self.callers:
            return "-"
        seen, out = set(), []
        for path, kind, line in self.callers:
            name = os.path.basename(path)
            tag = "" if kind == "runtime" else " (%s)" % kind
            label = name + tag
            if label in seen:
                continue
            seen.add(label)
            out.append(label)
            if len(out) >= limit:
                break
        extra = len(set(c[0] for c in self.callers)) - len(out)
        return ", ".join(out) + (" +%d more" % extra if extra > 0 else "")


def classify(row, asset_ok, fallback_ok=False):
    real = row.real_callers()
    if not real:
        row.bucket = "ORPHAN"
        why = []
        if any(c[1] == "mention" for c in row.callers):
            why.append("string mentions only, no load call")
        if any(c[0] in row.self_files for c in row.callers):
            why.append("only its own declaration/plumbing references it")
        if row.caller_kinds() & {"editor", "test"}:
            why.append("editor/test refs only")
        if not row.callers:
            why.append("no reference anywhere in the scanned corpus")
        if why:
            row.note = (row.note + " " if row.note else "") + "; ".join(why)
        return
    if asset_ok:
        row.bucket = "WIRED"
        return
    if fallback_ok:
        row.bucket = "FALLBACK"
        return
    row.bucket = "BROKEN"


# ---------------------------------------------------------------------------
#  6. build the whole map
# ---------------------------------------------------------------------------
def build():
    ignored_roots = gitignored_art_roots()
    guid_idx = build_prefab_guid_index()
    strings, enumrefs, dynamic, load_sites, n_cs = scan_code()
    data_values, n_data = scan_data()
    res_idx = index_resources()

    M = OrderedDict()
    M["meta"] = {"n_cs": n_cs, "n_data": n_data, "n_prefab_meta": len(guid_idx),
                 "n_ignored_roots": len(ignored_roots)}

    # -- Hovl VFX keys ------------------------------------------------------
    hovl = parse_hovl_catalog()
    seen_keys = defaultdict(int)
    hovl_rows, hovl_keys = [], set()
    for key, guid, ln in hovl:
        seen_keys[key] += 1
        hovl_keys.add(key)
        r = Row(key, VFX_SELF)
        state, path = resolve_prefab(guid, guid_idx, ignored_roots)
        r.asset = path
        r.asset_note = state
        for c in strings.get(key, []):
            r.callers.append(c)
        for f, k in sorted(data_values.get(key, ())):
            r.callers.append((f, k, 0))
        # GITIGNORED resolves on THIS machine and on no fresh clone. Recorded as an asset
        # (it is not broken here) but the note carries the exposure - the standing debt the
        # self-containment oracle baselines rather than hides.
        classify(r, state in ("RESOLVED", "GITIGNORED"))
        if state == "GITIGNORED":
            r.note = (r.note + "; " if r.note else "") + "prefab is in a GITIGNORED art root"
        elif state == "MISSING":
            r.note = (r.note + "; " if r.note else "") + "prefab GUID resolves to no asset on disk"
        elif state == "NULL":
            r.note = (r.note + "; " if r.note else "") + "catalog row has no prefab"
        hovl_rows.append(r)
    M["hovl"] = hovl_rows
    M["hovl_dupes"] = sorted(k for k, n in seen_keys.items() if n > 1 and k)

    # -- VFXType ordinals ---------------------------------------------------
    vfxtype = parse_enum(VFXTYPE_CS, "VFXType")
    ordinal_to_name = {v: k for k, v in vfxtype}
    catalog_rows = parse_vfx_catalog()
    cat_by_ord = {}
    for ordv, guid, ln in catalog_rows:
        cat_by_ord[ordv] = guid
    vt_rows = []
    for name, ordv in vfxtype:
        if name == "None":
            continue
        r = Row(name, VFX_SELF)
        guid = cat_by_ord.get(ordv)
        if ordv not in cat_by_ord:
            state, path = "NOROW", ""
        else:
            state, path = resolve_prefab(guid, guid_idx, ignored_roots)
        r.asset, r.asset_note = path, state
        r.callers = list(enumrefs.get(("VFXType", name), []))
        for f, k in sorted(data_values.get(name, ())):
            r.callers.append((f, k, 0))
        # A VFXType with no prefab is NOT broken: VFXManager documents and implements a
        # procedural stand-in (ProceduralFallback / ProceduralLoopFallback), so something
        # renders. Calling that BROKEN would cry wolf; it gets its own bucket.
        classify(r, state in ("RESOLVED", "GITIGNORED"), fallback_ok=True)
        if state == "NOROW":
            r.note = (r.note + "; " if r.note else "") + "no VFXCatalog row"
        elif state == "GITIGNORED":
            r.note = (r.note + "; " if r.note else "") + "prefab is in a GITIGNORED art root"
        elif state == "MISSING":
            r.note = (r.note + "; " if r.note else "") + "prefab GUID resolves to no asset on disk"
        elif state == "NULL":
            r.note = (r.note + "; " if r.note else "") + "catalog row has no prefab"
        vt_rows.append(r)
    M["vfxtype"] = vt_rows
    M["vfxtype_orphan_ordinals"] = sorted(o for o in cat_by_ord if o not in ordinal_to_name)

    # -- SfxId --------------------------------------------------------------
    sfx = parse_enum(SFXID_CS, "SfxId")
    proc_cases = set(re.findall(r"case\s+SfxId\.(\w+)", read(PROCEDURAL_SFX_CS)))
    lib_paths = sorted(glob.glob(os.path.join(ASSETS, "**", "SfxClipLibrary*.asset"), recursive=True))
    lib_rows = {}
    for lp in lib_paths:
        txt = open(lp, encoding="utf-8", errors="replace").read()
        cur = None
        for line in txt.splitlines():
            im = re.match(r"\s*-\s*Id:\s*(-?\d+)", line)
            if im:
                cur = int(im.group(1))
                continue
            if cur is not None and re.match(r"\s*Clip:", line):
                g = _GUID_RE.search(line)
                lib_rows[cur] = g.group(1) if g else None
                cur = None
    sfx_rows = []
    for name, ordv in sfx:
        if name == "None":
            continue
        r = Row(name, SFX_SELF)
        has_lib = ordv in lib_rows and lib_rows[ordv]
        has_proc = name in proc_cases
        r.asset = ("SfxClipLibrary row" if has_lib else
                   ("ProceduralSfx.For synthesised clip" if has_proc else ""))
        r.asset_note = "LIBRARY" if has_lib else ("PROCEDURAL" if has_proc else "NONE")
        r.callers = list(enumrefs.get(("SfxId", name), []))
        classify(r, bool(has_lib or has_proc))
        # The audit's sharpest audio finding, reproduced mechanically: an id whose ONLY
        # runtime caller is VFXManager's VfxToSfx pairing is played as a rider on a VFXType.
        # If that paired VFXType is itself never played, the sound is silent in practice -
        # WIRED on paper, inaudible in the build.
        runtime_callers = set(c[0] for c in r.real_callers())
        if runtime_callers == {VFX_SFX_PAIRING}:
            r.note = (r.note + "; " if r.note else "") + \
                "fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the " \
                "paired VFXType is not played"
        sfx_rows.append(r)
    M["sfx"] = sfx_rows
    M["sfx_library_assets"] = [rel(p) for p in lib_paths]
    M["sfx_library_resource"] = SFX_LIBRARY_RESOURCE

    # -- MusicTrack ---------------------------------------------------------
    music = parse_enum(MUSICTRACK_CS, "MusicTrack")
    boot = read(AUDIO_BOOTSTRAP_CS)
    assigns = defaultdict(list)
    for fn_, track, resname in re.findall(
            r"\b(TryAssignClip|TryAddClip)\s*\(\s*\w+\s*,\s*MusicTrack\.(\w+)\s*,\s*\"([^\"]+)\"", boot):
        assigns[track].append(resname)
    music_rows, music_missing = [], []
    for name, ordv in music:
        r = Row(name, MUSIC_SELF)
        names = assigns.get(name, [])
        hits = [n for n in names if res_idx.get(n.lower())]
        misses = [n for n in names if not res_idx.get(n.lower())]
        for mn in misses:
            music_missing.append((name, mn))
        r.asset = ", ".join(res_idx[h.lower()][0] for h in hits) if hits else ""
        r.asset_note = "RESOURCES" if hits else ("MISSING" if names else "NONE")
        r.callers = list(enumrefs.get(("MusicTrack", name), []))
        classify(r, bool(hits))
        if misses:
            r.note = "Resources.Load name(s) with no file: " + ", ".join(misses)
        elif not names:
            r.note = "no AudioBootstrap assignment (Inspector/other-code wiring only)"
        music_rows.append(r)
    M["music"] = music_rows
    M["music_missing"] = music_missing

    # -- Resources/Sfx string-keyed clips -----------------------------------
    # A SECOND audio inventory, parallel to SfxId: GameSfx / EnemyCombatAudio and the
    # motion-castings.json sfxId column load clips BY NAME out of Resources/Sfx.
    sfxfile_rows, sfx_missing = [], []
    sfx_res = {k: v for k, v in res_idx.items() if k.startswith("sfx/")}
    for lower, files in sorted(sfx_res.items()):
        f0 = files[0]
        cut = f0.lower().rfind("resources/") + len("resources/")
        resname = os.path.splitext(f0[cut:])[0]   # correct-cased, extension-free: what a
        r = Row(resname)                          # Resources.Load call actually passes
        r.asset = f0
        r.asset_note = "RESOURCES"
        # STRONG references: an actual Resources.Load<AudioClip>("<name>") call.
        loads = load_sites.get(resname, [])
        loaded_at = set((p, n) for p, k, n, hc in loads)
        for p, k, n, hc in loads:
            r.callers.append((p, k, n))
        # WEAK references: the name appears as a string but not as a load - e.g. inside
        # AudioService's CombatSfxResourceNames PRE-WARM array. Pre-warming a clip is not
        # playing it, so a name that appears ONLY in such a list is still an orphan; it is
        # recorded as a 'mention' so the row shows why rather than looking unreferenced.
        for p, k, n in strings.get(resname, []):
            if (p, n) not in loaded_at:
                r.callers.append((p, "mention", n))
        base = resname.rsplit("/", 1)[-1]
        for f, k in sorted(data_values.get(base, ())):
            r.callers.append((f, k, 0))
        classify(r, True)
        if r.bucket == "ORPHAN" and any(c[1] == "mention" for c in r.callers):
            r.note = (r.note + "; " if r.note else "") + \
                "named in a string list (pre-warm) but never loaded — may still be reached " \
                "by one of the dynamic loads in section 3"
        if len(files) > 1:
            r.note = "%d files share this Resources name" % len(files)
        sfxfile_rows.append(r)

    # Names REQUESTED but with no file. Split by whether the call site coalesces to a
    # generated clip (`Resources.Load<AudioClip>("Sfx/TowerFire") ?? GenerateTowerFire()`).
    # With the `??` the sound still plays - synthesised, not authored - so calling it BROKEN
    # would cry wolf and train people to ignore this section.
    for lit in sorted(s for s in strings if s.startswith("Sfx/")):
        # "Sfx/" alone is a concatenation PREFIX (Resources.Load<AudioClip>("Sfx/" + name)),
        # not a resource name. The concatenated call is already counted as UNRESOLVED; listing
        # the prefix as a broken load would be a fabricated defect.
        if lit.endswith("/") or res_idx.get(lit.lower()):
            continue
        loads = load_sites.get(lit, [])
        covered = any(hc for _, _, _, hc in loads)
        sfx_missing.append((lit, strings.get(lit, []), covered))
    M["sfxfiles"] = sfxfile_rows
    M["sfxfiles_missing"] = sfx_missing

    # -- BROKEN: a caller naming a Hovl key no catalog defines --------------
    # Two shapes: a PlayKey("literal") in code, and a vfxKey/vfxProjectile/vfxImpact value
    # in data. Both resolve to nothing at runtime (PlayKey logs "no HovlVfxCatalog row").
    broken_keys = []
    code_key_calls = defaultdict(list)
    for d in CODE_DIRS:
        base = os.path.join(ROOT, d.replace("/", os.sep))
        for dirpath, dirnames, filenames in os.walk(base):
            for fn in filenames:
                if not fn.endswith(".cs"):
                    continue
                full = os.path.join(dirpath, fn)
                rp = rel(full)
                try:
                    with open(full, encoding="utf-8", errors="replace") as f:
                        text = f.read()
                except OSError:
                    continue
                for n, line in enumerate(text.splitlines(), 1):
                    code = _LINE_COMMENT.sub("", line)
                    for arg in _PLAYKEY_RE.findall(code):
                        a = arg.strip()
                        if a.startswith('"') and a.endswith('"') and len(a) > 1:
                            code_key_calls[a[1:-1]].append((rp, file_kind(rp), n))
    for k, sites in sorted(code_key_calls.items()):
        if k not in hovl_keys:
            broken_keys.append(("PlayKey(\"%s\")" % k, sites))
    # data-declared keys
    data_key_fields = ("vfxKey", "vfxProjectile", "vfxImpact")
    data_broken = defaultdict(set)
    for jf in ("Assets/Resources/Data/Canonical/motion-castings.json",
               "Assets/StreamingAssets/Data/Canonical/motion-castings.json"):
        txt = read(jf)
        if not txt:
            continue
        try:
            doc = json.loads(txt)
        except ValueError:
            continue

        def walk(o):
            if isinstance(o, dict):
                for k, v in o.items():
                    if k in data_key_fields and isinstance(v, str) and v:
                        if v not in hovl_keys:
                            data_broken[v].add(jf)
                    walk(v)
            elif isinstance(o, list):
                for v in o:
                    walk(v)
        walk(doc)
    for k in sorted(data_broken):
        broken_keys.append(("%s (data vfx key)" % k, [(f, "data", 0) for f in sorted(data_broken[k])]))
    M["broken_calls"] = broken_keys
    M["dynamic"] = dynamic
    return M


# ---------------------------------------------------------------------------
#  7. render
# ---------------------------------------------------------------------------
def table(rows, asset_header="asset"):
    out = ["| key | bucket | %s | called by | note |" % asset_header,
           "|---|---|---|---|---|"]
    for r in rows:
        a = r.asset if r.asset else "-"
        if len(a) > 62:
            a = "..." + a[-59:]
        note = r.asset_note + ((" - " + r.note) if r.note else "")
        out.append("| `%s` | **%s** | %s | %s | %s |" %
                   (r.key, r.bucket, a, r.caller_summary(), note))
    return "\n".join(out)


def group_orphans(rows):
    """Orphans grouped by prefix domain - a work queue reads by pack/domain, not A-Z.

    Keys with no '_' (SfxId values, clip filenames) have no domain to group BY, and forcing
    one produces a list of one-item groups that is strictly harder to read than a flat list.
    So: group only when grouping actually buys something."""
    orphans = [r for r in rows if r.bucket == "ORPHAN"]
    if not orphans:
        return OrderedDict()
    if sum(1 for r in orphans if "_" in r.key) < len(orphans) * 0.6:
        return OrderedDict([("", orphans)])
    groups = defaultdict(list)
    for r in orphans:
        k = r.key
        pre = k.split("_", 1)[0] if "_" in k else "(no prefix)"
        if k.startswith("PP_"):
            pre = "PP — Unity Particle Pack"
        groups[pre].append(r)
    return OrderedDict(sorted(groups.items(), key=lambda kv: (-len(kv[1]), kv[0])))


def render(M):
    stamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    hovl, vt, sfx, music, sfxfiles = M["hovl"], M["vfxtype"], M["sfx"], M["music"], M["sfxfiles"]
    allrows = hovl + vt + sfx + music + sfxfiles
    sfx_hard = [x for x in M["sfxfiles_missing"] if not x[2]]
    sfx_soft = [x for x in M["sfxfiles_missing"] if x[2]]
    wired = sum(1 for r in allrows if r.bucket == "WIRED")
    orphan = sum(1 for r in allrows if r.bucket == "ORPHAN")
    broken = sum(1 for r in allrows if r.bucket == "BROKEN") + len(M["broken_calls"]) + len(sfx_hard)
    fallback = sum(1 for r in allrows if r.bucket == "FALLBACK") + len(sfx_soft)
    unresolved = len(M["dynamic"])
    meta = M["meta"]

    L = []
    A = L.append
    A("# VFX & AUDIO WIRING MAP — what exists, what is wired, what is orphaned")
    A("")
    A("> **GENERATED FILE — DO NOT HAND-EDIT.** Regenerate in ~seconds:")
    A("> `python tools/vfx_audio_map.py` (report-only, exits 0) ·")
    A("> `python tools/vfx_audio_map.py --check` (exits 1 if any **BROKEN** row exists).")
    A("> Generated **%s**. The repo is the source of truth; this doc is a derived view, so it" % stamp)
    A("> cannot drift the way a hand-maintained map does. Edit the catalogs/code, re-run, commit both.")
    A("")
    A("## Summary")
    A("")
    A("| bucket | count | meaning |")
    A("|---|---|---|")
    A("| **WIRED** | %d | declared + an asset resolves + at least one non-test caller |" % wired)
    A("| **ORPHAN** | %d | declared, nothing consumes it — **debt / work queue**, not a failure |" % orphan)
    A("| **BROKEN** | %d | a caller asks for a key/id that resolves to NOTHING at runtime — **defect** |" % broken)
    A("| FALLBACK | %d | called, no prefab, but `VFXManager` renders a documented procedural stand-in |" % fallback)
    A("| UNRESOLVED | %d | call sites whose key is built at runtime — the link **cannot be proven statically** |" % unresolved)
    A("")
    A("`VFX_AUDIO_MAP_OK %d/%d/%d` (wired/orphan/broken) · fallback=%d unresolved=%d" %
      (wired, orphan, broken, fallback, unresolved))
    A("")
    A("Scanned: **%d** `.cs` files (`%s`), **%d** data files (`.json`/`.asset`), "
      "**%d** prefab `.meta` GUIDs, **%d** gitignored art roots read from "
      "`VfxResourceSelfContainmentRegression.GitignoredArtRoots`." %
      (meta["n_cs"], ", ".join(CODE_DIRS), meta["n_data"], meta["n_prefab_meta"], meta["n_ignored_roots"]))
    A("")
    A("### How a row is bucketed")
    A("")
    A("A **caller** is a reference from runtime code or from a data file. References that")
    A("live only in `/Editor/` or `/Tests/`, or only inside the key's own declaration and")
    A("plumbing (`VFXManager`, `VFXCatalog`, `AudioService`, …), do **not** make a key")
    A("consumed — they are noted on the row instead. That is the audit's CONSUMED")
    A("definition (`docs/reference/STACK_UTILIZATION_2026-08-09.md`), not a looser one.")
    A("")
    A("For `Resources/Sfx` clips a caller must be a real `Resources.Load<AudioClip>(\"<name>\")`.")
    A("A name that merely appears in a string list — `AudioService.CombatSfxResourceNames`")
    A("pre-warms clips it never plays — is shown as `(mention)` and does **not** count:")
    A("pre-warming a clip is not playing it.")
    A("")

    # ---- per-domain summaries
    A("### By domain")
    A("")
    A("| domain | inventory | WIRED | ORPHAN | BROKEN | FALLBACK |")
    A("|---|---|---|---|---|---|")
    for label, rows in (("Hovl VFX keys (`HovlVfxCatalog.asset`)", hovl),
                        ("`VFXType` ordinals (`VFXCatalog.asset`)", vt),
                        ("`SfxId` values", sfx),
                        ("`MusicTrack` values", music),
                        ("`Resources/Sfx/*` clip files", sfxfiles)):
        c = defaultdict(int)
        for r in rows:
            c[r.bucket] += 1
        A("| %s | %d | %d | %d | %d | %d |" %
          (label, len(rows), c["WIRED"], c["ORPHAN"], c["BROKEN"], c["FALLBACK"]))
    A("")
    A("**Cross-check against the 2026-08-09 reverse audit** (did the number move?):")
    A("")
    for dom, rows, label in (("hovl", hovl, "Hovl VFX keys"), ("vfxtype", vt, "`VFXType` ordinals")):
        base, total, cite = AUDIT_BASELINE[dom]
        now = sum(1 for r in rows if r.bucket in ("WIRED", "FALLBACK"))
        A("- %s — audit measured **%d/%d consumed (%d%%)**; this tool measures **%d/%d (%d%%)**. "
          "Source: %s" % (label, base, total, round(100.0 * base / total),
                          now, len(rows), round(100.0 * now / max(1, len(rows))), cite))
    A("")
    A("These two numbers are **not** the same measurement and are not expected to match")
    A("exactly: the audit's \"consumed\" was a human judgement over call sites, this tool's is")
    A("a mechanical rule (runtime-or-data reference, excluding the key's own plumbing). Read")
    A("the delta as a sanity check on the order of magnitude, never as a pass/fail bar.")
    A("")

    # ---- BROKEN first: it is the defect class
    A("## 1. BROKEN — a call site asking for something that will not resolve")
    A("")
    if not any(r.bucket == "BROKEN" for r in allrows) and not M["broken_calls"] and not sfx_hard:
        A("_None._ Every key/id with a caller resolves to an asset (or to a documented")
        A("procedural fallback — see the FALLBACK list below).")
    else:
        A("| what | asked from | why it is broken |")
        A("|---|---|---|")
        for r in allrows:
            if r.bucket != "BROKEN":
                continue
            A("| `%s` | %s | %s |" % (r.key, r.caller_summary(4), r.asset_note +
                                      ((" — " + r.note) if r.note else "")))
        for what, sites in M["broken_calls"]:
            where = ", ".join("%s:%s" % (os.path.basename(p), n) for p, k, n in sites[:4])
            A("| `%s` | %s | no row in `HovlVfxCatalog.asset` — `PlayKey` logs "
              "\"no HovlVfxCatalog row for this key\" and spawns nothing |" % (what, where))
        for lit, sites, covered in sfx_hard:
            where = ", ".join("%s:%s" % (os.path.basename(p), n) for p, k, n in sites[:4])
            A("| `Resources.Load<AudioClip>(\"%s\")` | %s | no audio file at that Resources "
              "path, and the call site does not coalesce to a generated clip |" % (lit, where))
    A("")
    if sfx_soft:
        A("**FALLBACK (not broken, but no authored audio ships):** these `Resources/Sfx` names")
        A("have no file; every load site coalesces to a procedurally generated clip, so the")
        A("event is audible as *synth*, not as the sound someone authored. Dropping a real")
        A("file in at the named path upgrades it with no code change.")
        A("")
        for lit, sites, covered in sfx_soft:
            where = ", ".join("%s:%s" % (os.path.basename(p), n) for p, k, n in sites[:4])
            A("- `%s` — requested from %s" % (lit, where))
        A("")

    # ---- ORPHANS: the work queue
    A("## 2. ORPHAN — declared in a catalog, wired to nothing")
    A("")
    A("This is a **work queue, not a statistic**. Every row below is a purchased/authored")
    A("asset the game never asks for. Grouped by domain so it is obvious which pack a block")
    A("of unused keys belongs to.")
    A("")
    for label, rows in (("Hovl VFX keys", hovl), ("`VFXType` ordinals", vt),
                        ("`SfxId` values", sfx), ("`MusicTrack` values", music),
                        ("`Resources/Sfx/*` clips", sfxfiles)):
        groups = group_orphans(rows)
        n = sum(len(v) for v in groups.values())
        if n == 0:
            continue
        A("### %s — %d orphan%s" % (label, n, "" if n == 1 else "s"))
        A("")
        for pre, rs in groups.items():
            keys = ", ".join("`%s`" % r.key for r in rs)
            if pre:
                A("- **%s** (%d): %s" % (pre, len(rs), keys))
            else:
                A("- %s" % keys)
        A("")

    # ---- UNRESOLVED
    A("## 3. UNRESOLVED — cannot be determined statically")
    A("")
    A("These call sites pass a key that is **built or indirected at runtime** (a variable, a")
    A("field, an interpolated string). A static parse cannot prove which catalog row they")
    A("reach, so the keys they use may appear as ORPHAN above. **They are counted here and")
    A("never folded into the orphan list** — treat the orphan queue as \"probably unused\",")
    A("not \"provably unused\", until these are read by hand.")
    A("")
    if not M["dynamic"]:
        A("_None found._")
    else:
        A("| call site | api | expression |")
        A("|---|---|---|")
        for rp, n, api, expr in sorted(M["dynamic"])[:120]:
            A("| `%s:%d` | `%s` | `%s` |" % (rp, n, api, expr.replace("|", "\\|")))
        if len(M["dynamic"]) > 120:
            A("")
            A("_… and %d more._" % (len(M["dynamic"]) - 120))
    A("")

    # ---- full tables
    A("## 4. Full inventory")
    A("")
    A("### 4.1 Hovl VFX keys — `Assets/Resources/VFX/HovlVfxCatalog.asset`")
    A("")
    A("Requested via `VFXManager.PlayKey(\"<key>\", …)`. A key with no catalog row **no-ops**")
    A("(logged, throttled) — nothing spawns.")
    A("")
    if M["hovl_dupes"]:
        A("> **Duplicate keys** (last row wins in `BuildLookup`, earlier rows are dead): %s" %
          ", ".join("`%s`" % k for k in M["hovl_dupes"]))
        A("")
    A(table(hovl, "prefab"))
    A("")
    A("### 4.2 `VFXType` ordinals — `Assets/Resources/VFX/VFXCatalog.asset`")
    A("")
    A("A `VFXType` with no wired prefab is **not** broken: `VFXManager.ProceduralFallback` /")
    A("`ProceduralLoopFallback` render a stand-in, so something is drawn. Those rows are")
    A("bucketed `FALLBACK`, which reads as \"ships, but with placeholder art\".")
    A("")
    if M["vfxtype_orphan_ordinals"]:
        A("> **Catalog rows whose `Type` ordinal is not a `VFXType` value**: %s — stale rows, "
          "nothing can ever request them." % ", ".join(str(o) for o in M["vfxtype_orphan_ordinals"]))
        A("")
    A(table(vt, "prefab"))
    A("")
    A("### 4.3 `SfxId` — `Assets/_Modules/Audio/SfxId.cs`")
    A("")
    if M["sfx_library_assets"]:
        A("`SfxClipLibrary` asset(s) found: %s" % ", ".join("`%s`" % p for p in M["sfx_library_assets"]))
    else:
        A("> **There is no `SfxClipLibrary` asset anywhere in `Assets/`.** `AudioService`")
        A("> Resources-loads `\"%s\"`, gets null, and every `SfxId` therefore resolves to the" %
          M["sfx_library_resource"])
        A("> **procedurally synthesised** clip from `ProceduralSfx.For(id)` — audible, but not")
        A("> authored audio. Wiring an authored library is a drop-in upgrade with no code change.")
    A("")
    A(table(sfx, "clip source"))
    A("")
    A("### 4.4 `MusicTrack` — `Assets/_Modules/Core/Audio/MusicTrack.cs`")
    A("")
    A("Clips are bound in `AudioBootstrap` by `Resources.Load<AudioClip>(<name>)`; the map")
    A("resolves each name against every `Assets/**/Resources/` root.")
    A("")
    if M["music_missing"]:
        A("> **Assignment names that resolve to no file:** %s" %
          ", ".join("`%s` → `\"%s\"`" % (t, n) for t, n in M["music_missing"]))
        A("")
    A(table(music, "clip"))
    A("")
    A("### 4.5 `Resources/Sfx/*` — the string-keyed audio inventory")
    A("")
    A("A **second** audio path parallel to `SfxId`: `GameSfx`, `EnemyCombatAudio` and the")
    A("`sfxId` / `sfxImpact` columns of `motion-castings.json` load clips **by name** out of")
    A("`Resources/Sfx/`. An orphan here is a shipped audio file nothing plays.")
    A("")
    A(table(sfxfiles, "file"))
    A("")

    # ---- blind spots
    A("## 5. Blind spots — what this map CANNOT see")
    A("")
    A("Named on purpose. A map that quietly omits what it could not resolve is worse than")
    A("one that states its limits.")
    A("")
    A("1. **Serialized references in prefabs and scenes.** A VFX prefab dragged into a")
    A("   MonoBehaviour field, or an `AudioClip` on an `AudioSource` in a `.unity` scene, is a")
    A("   real consumer this map does not count — it only follows *keys and enum ids*. A row")
    A("   marked ORPHAN may still be referenced by GUID from a prefab or scene.")
    A("2. **Runtime-built keys** — see §3 UNRESOLVED.")
    A("3. **Reachability.** \"Has a caller\" is not \"a player reaches it\". A caller behind a")
    A("   dead feature flag or an unreachable branch still counts as WIRED here.")
    A("4. **Gitignored art.** Prefabs under the %d gitignored art roots resolve on this" % meta["n_ignored_roots"])
    A("   machine and on **no fresh clone**; those rows are marked but still counted as having")
    A("   an asset. `VfxResourceSelfContainmentRegression` is the authority on that exposure.")
    A("5. **`SetMusicClip` / `AddMusicClip` from other code or the Inspector.** Only")
    A("   `AudioBootstrap`'s literal assignments are parsed.")
    A("6. **Addressables / asset bundles.** Not consulted; everything here is `Resources` +")
    A("   direct GUID references.")
    A("")
    A("---")
    A("")
    A("Source of truth: `tools/vfx_audio_map.py`. Related canon:")
    A("`docs/reference/AUDIT_2026-08-09.md` (\"every gate asserts a thing EXISTS, almost none")
    A("assert it is CONSUMED\"), `docs/reference/STACK_UTILIZATION_2026-08-09.md`,")
    A("`Assets/Editor/Regression/VfxResourceSelfContainmentRegression.cs`.")
    A("")
    return "\n".join(L), (wired, orphan, broken, fallback, unresolved)


def main():
    check = "--check" in sys.argv
    M = build()
    text, (wired, orphan, broken, fallback, unresolved) = render(M)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(text)
    print("%s written" % rel(OUT))
    print("VFX_AUDIO_MAP_OK %d/%d/%d" % (wired, orphan, broken))
    print("VFX_AUDIO_MAP_DETAIL fallback=%d unresolved=%d" % (fallback, unresolved))
    if broken:
        print("BROKEN rows are listed in section 1 of the doc.")
    if check:
        if broken:
            print("VFX_AUDIO_MAP_CHECK_FAIL %d broken" % broken)
            return 1
        print("VFX_AUDIO_MAP_CHECK_OK 0 broken")
    return 0


if __name__ == "__main__":
    sys.exit(main())
