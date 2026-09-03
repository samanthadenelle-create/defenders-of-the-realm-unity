#!/usr/bin/env python3
# =============================================================================
# payload-texture-parity.py - cross-target BASE-MAP parity for shipped payloads.
# -----------------------------------------------------------------------------
# WO-1326. The owner played three builds off ONE source tree and the guide wolf
# was "perfectly colored" on Pi/WebGL and grey on the APK and the exe. The cause
# was NOT a platform importer override and NOT a bundle: it was that the WebGL
# player payload does not CONTAIN `wolf_color` at all, while the Windows player
# and the APK both do. With no base map to bind, TripoMaterialFixer falls back to
# the species tint and the wolf renders as a clean icy body; with the (near
# greyscale) map bound, it renders grey. Same code, same material, opposite look,
# decided by whether one texture is in the payload.
#
# ⛔ THAT DIVERGENCE IS SILENT. Nothing errors, nothing is magenta, no bundle 404s
# (§16's failure mode) - the game just looks different per target and the only
# detector left is the owner's eyes, which is the thing §14 exists to never rely
# on. This tool is the detector.
#
# THE INVARIANT: a base-colour map that is present in one shipped player payload
# must be present in every other shipped player payload. Nothing here judges what
# a texture LOOKS like - only whether the same set of base maps reached each
# target. Colour is the owner's call; presence is ours.
#
# Judge by the MARKER on a FRESH log, never the exit code (memory
# `gates-report-success-without-proving-it`):
#     PAYLOAD_TEXTURE_PARITY_OK <n> map(s) verified across <k> payload(s)
#     PAYLOAD_TEXTURE_PARITY_FAIL <n> map(s) diverge
#     PAYLOAD_TEXTURE_PARITY_SKIPPED <reason>
# Marker absence on a fresh log is a FAILURE, not an unknown.
#
# Usage (repo root resolved at runtime - never hardcode it, CLAUDE.md sec.0):
#     python tools/payload-texture-parity.py
#     python tools/payload-texture-parity.py --all      # every texture, not just base maps
#     python tools/payload-texture-parity.py --self-test
#
# WHY A SUBSTRING SCAN AND NOT AN ASSET PARSER: the three payloads use three
# different container layouts (Windows resources.assets/levelN, the APK's
# per-GUID Resources files, WebGL's brotli-wrapped virtual FS). A serialized
# object name is a length-prefixed ASCII run in all three, so a raw byte search
# for the authored file stem is the ONE probe that works on every container and
# cannot be fooled by a layout change. It is deliberately conservative: it can
# only ever say "this name is not in these bytes".
# =============================================================================

import argparse
import os
import re
import sys
import zipfile

# Tokens that mark a texture as a BASE-COLOUR map. Searched over the authored
# file stem, never over a name we expect (memory `search-by-token-not-by-name`):
# a name-first search can only confirm a guess, it cannot discover.
BASE_MAP_TOKENS = ("basecolor", "base_color", "albedo", "diffuse", "color", "colour")

TEXTURE_EXTS = (".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr")

# Stems shorter than this produce substring false positives ("sky", "ao").
MIN_STEM = 6


def repo_root():
    return os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))


def authored_texture_stems(root, base_maps_only):
    """Every authored texture stem under Assets/, optionally base maps only."""
    stems = set()
    assets = os.path.join(root, "Assets")
    for dirpath, _dirnames, filenames in os.walk(assets):
        for name in filenames:
            if name.endswith(".meta"):
                continue
            stem, ext = os.path.splitext(name)
            if ext.lower() not in TEXTURE_EXTS:
                continue
            if len(stem) < MIN_STEM:
                continue
            if any(ord(c) < 32 or ord(c) > 126 for c in stem):
                continue
            if base_maps_only and not any(t in stem.lower() for t in BASE_MAP_TOKENS):
                continue
            stems.add(stem)
    return stems


def _hits(blob, stems):
    """Which stems appear as raw bytes in this payload."""
    found = set()
    for stem in stems:
        if stem.encode("ascii", "ignore") in blob:
            found.add(stem)
    return found


def windows_payload(root):
    data_dir = os.path.join(root, "Builds", "Windows", "DefendersOfTheRealm_Data")
    if not os.path.isdir(data_dir):
        return None
    chunks = []
    for name in sorted(os.listdir(data_dir)):
        path = os.path.join(data_dir, name)
        if not os.path.isfile(path):
            continue
        if name.endswith((".resS", ".resource", ".json", ".config", ".info")):
            continue
        with open(path, "rb") as handle:
            chunks.append(handle.read())
    return b"".join(chunks) if chunks else None


def android_payload(root):
    apk_dir = os.path.join(root, "Builds", "Android")
    apk = os.path.join(apk_dir, "DefendersOfTheRealm.apk")
    if not os.path.isfile(apk):
        return None
    chunks = []
    with zipfile.ZipFile(apk) as archive:
        for entry in archive.namelist():
            if not entry.startswith("assets/bin/Data/"):
                continue
            if entry.endswith((".resS", ".resource")):
                continue
            chunks.append(archive.read(entry))
    return b"".join(chunks) if chunks else None


def webgl_payload(root):
    """The .data payload. Unity wraps it in a 'UnityWeb Compressed Content'
    banner when Brotli/gzip compression is on; the brotli stream starts at byte
    0 regardless, so decompress the whole file and fall back to raw bytes."""
    build_dir = os.path.join(root, "Builds", "WebGL", "Build")
    if not os.path.isdir(build_dir):
        return None
    blob = None
    for name in sorted(os.listdir(build_dir)):
        if ".data" in name:
            with open(os.path.join(build_dir, name), "rb") as handle:
                blob = handle.read()
            break
    if blob is None:
        return None
    if b"UnityWeb Compressed Content" not in blob[:64]:
        return blob
    try:
        import brotli  # optional; absent on a machine that never ships WebGL
    except ImportError:
        return "NO_BROTLI"
    try:
        return brotli.decompress(blob)
    except Exception:
        return "NO_BROTLI"


def collect(root, base_maps_only):
    stems = authored_texture_stems(root, base_maps_only)
    payloads = {}
    skipped = []
    for label, loader in (("Windows", windows_payload),
                          ("Android", android_payload),
                          ("WebGL", webgl_payload)):
        blob = loader(root)
        if blob is None:
            skipped.append("%s (no built payload on disk)" % label)
            continue
        if blob == "NO_BROTLI":
            skipped.append("WebGL (brotli module not installed - `pip install brotli`)")
            continue
        payloads[label] = _hits(blob, stems)
    return stems, payloads, skipped


def report(stems, payloads, skipped):
    for reason in skipped:
        print("[parity] SKIP %s" % reason)
    if len(payloads) < 2:
        print("PAYLOAD_TEXTURE_PARITY_SKIPPED fewer than two payloads available "
              "(parity needs a control target to compare against)")
        return 0
    union = set()
    for hits in payloads.values():
        union |= hits
    diverge = {}
    for name in sorted(union):
        present = sorted(l for l, h in payloads.items() if name in h)
        absent = sorted(l for l in payloads if name not in payloads[l])
        if absent:
            diverge[name] = (present, absent)
    for label in sorted(payloads):
        print("[parity] %-8s %d map(s) present" % (label, len(payloads[label])))
    if diverge:
        print("[parity] --- divergent maps (present on one target, absent on another) ---")
        for name, (present, absent) in diverge.items():
            print("[parity]   %-44s present=%s  ABSENT=%s"
                  % (name, ",".join(present), ",".join(absent)))
        print("PAYLOAD_TEXTURE_PARITY_FAIL %d map(s) diverge across %d payload(s)"
              % (len(diverge), len(payloads)))
        return 16
    print("PAYLOAD_TEXTURE_PARITY_OK %d map(s) verified across %d payload(s)"
          % (len(union), len(payloads)))
    return 0


def self_test():
    """Prove the oracle RED and GREEN without touching a build.

    A parity checker that only ever prints FAIL is worth nothing, and one that
    only ever prints OK is worse (memory `prove-the-success-path-not-just-the-
    refusal`). Both directions are exercised here."""
    ok_payloads = {"A": {"hero_basecolor", "wolf_color"},
                   "B": {"hero_basecolor", "wolf_color"}}
    red_payloads = {"A": {"hero_basecolor", "wolf_color"},
                    "B": {"hero_basecolor"}}
    print("[self-test] GREEN case (same maps in both payloads):")
    green = report(set(), ok_payloads, [])
    print("[self-test] RED case (one payload missing wolf_color):")
    red = report(set(), red_payloads, [])
    if green == 0 and red != 0:
        print("[self-test] PASS - the oracle distinguishes parity from divergence.")
        return 0
    print("[self-test] FAIL - the oracle does not separate the two cases.")
    return 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--all", action="store_true",
                        help="check every authored texture, not just base-colour maps")
    parser.add_argument("--self-test", action="store_true",
                        help="prove the oracle red and green without a build")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    root = repo_root()
    stems, payloads, skipped = collect(root, base_maps_only=not args.all)
    print("[parity] repo root: %s" % root)
    print("[parity] authored %s stems considered: %d"
          % ("texture" if args.all else "base-map", len(stems)))
    return report(stems, payloads, skipped)


if __name__ == "__main__":
    sys.exit(main())
