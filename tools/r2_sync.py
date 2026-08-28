#!/usr/bin/env python3
"""
r2_sync.py — upload Addressables remote bundles to Cloudflare R2.

WHY THIS EXISTS
    Local Addressable bundles are copied into StreamingAssets and SHIP INSIDE THE
    APK, so moving assets between local groups saves nothing. Only a REMOTE group
    takes bytes out of the download, and a remote group is useless until its
    bundles are actually hosted. This is the upload half of that.

    R2 is the host because its EGRESS IS FREE — the deciding factor for game
    assets, where every player download is egress. (Git LFS was considered and
    rejected: 1 GB/month on the free tier is ~16 downloads of a 62 MB payload.)

CREDENTIALS
    Read from .env.r2 at the repo root, which is gitignored (.gitignore `.env*`).
    Never hardcode them here and never echo the secret — this file IS tracked.

USAGE
    python tools/r2_sync.py --check                 # prove credentials ONLY (not a content gate)
    python tools/r2_sync.py --push ServerData       # ⛔ PUSH THE PARENT, NEVER ServerData/Android
    python tools/r2_sync.py --list
    python tools/r2_sync.py --verify-catalog        # THE CONTENT GATE — run before you ship an APK

⛔ --push TAKES `ServerData`, NOT `ServerData/Android`
    Keys are derived RELATIVE TO THE FOLDER YOU HAND IT. Push `ServerData/Android` and every
    object lands at the BUCKET ROOT (`structure_art_....bundle`) while the game asks for
    `Android/structure_art_....bundle` — a 404 for every remote asset, from a command that
    exits 0. That is the `16e22dba3` bug, and this docstring used to teach it (it read
    `--push ServerData/Android`). A tool whose own usage text names the failure keeps
    producing it. Push the PARENT so the build-target folder becomes the key prefix.

⛔ --check IS NOT A CONTENT GATE
    It round-trips one probe object: it proves credentials and anonymous read AT THAT INSTANT
    and NOTHING about whether YOUR build's bundles are in the bucket. `R2_CHECK_OK` next to a
    build with an unpushed bundle is a green light on a broken APK. The content gate is
    `--verify-catalog`, whose marker is `R2_PARITY_OK`. Do not substitute one for the other.
"""

import argparse
import hashlib
import json
import mimetypes
import os
import re
import sys
import urllib.request

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ENV = os.path.join(REPO, ".env.r2")


def load_env():
    """Parse .env.r2. Fails loudly on a missing key rather than half-configuring."""
    if not os.path.exists(ENV):
        sys.exit(f"FAIL: {ENV} not found. Create it (see tools/r2_sync.py header).")
    cfg = {}
    with open(ENV, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            cfg[k.strip()] = v.strip()

    required = ["R2_S3_ENDPOINT", "R2_BUCKET", "R2_ACCESS_KEY_ID",
                "R2_SECRET_ACCESS_KEY", "R2_PUBLIC_URL"]
    missing = [k for k in required if not cfg.get(k)]
    if missing:
        sys.exit("FAIL: .env.r2 is missing values for: " + ", ".join(missing))
    return cfg


def client(cfg):
    import boto3
    from botocore.config import Config
    return boto3.client(
        "s3",
        endpoint_url=cfg["R2_S3_ENDPOINT"],
        aws_access_key_id=cfg["R2_ACCESS_KEY_ID"],
        aws_secret_access_key=cfg["R2_SECRET_ACCESS_KEY"],
        # R2 speaks S3 but is not AWS; 'auto' is the region it expects.
        region_name="auto",
        config=Config(signature_version="s3v4", retries={"max_attempts": 3}),
    )


def list_bucket(s3, cfg):
    """
    Every key in the bucket -> {"size": int, "etag": str-or-None}.

    `etag` is R2's md5 for a single-PUT object, and None when the ETag carries a `-N`
    multipart suffix (then it is a hash-of-hashes and NOT comparable to a local md5).
    One paginated listing serves --push, --list and --verify-catalog; there is exactly
    one place that can get pagination wrong.
    """
    out = {}
    token = None
    while True:
        kw = {"Bucket": cfg["R2_BUCKET"]}
        if token:
            kw["ContinuationToken"] = token
        page = s3.list_objects_v2(**kw)
        for obj in page.get("Contents", []):
            etag = (obj.get("ETag") or "").strip('"')
            out[obj["Key"]] = {"size": obj["Size"],
                               "etag": None if (not etag or "-" in etag) else etag.lower()}
        if not page.get("IsTruncated"):
            break
        token = page.get("NextContinuationToken")
    return out


def md5_of(path):
    h = hashlib.md5()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def cmd_check(cfg):
    """
    Round-trips a probe object: PUT via S3 -> GET over the PUBLIC url -> DELETE.

    The public GET is the half that matters and the half people skip. Credentials
    can be perfectly valid while the bucket is still private, and then every
    player gets a 401 on content the build believes is hosted. Proving the
    ANONYMOUS read path is the only way to know the game can actually fetch this.
    """
    s3 = client(cfg)
    key = "_healthcheck/probe.txt"
    body = b"defenders-of-the-realm r2 probe"

    s3.put_object(Bucket=cfg["R2_BUCKET"], Key=key, Body=body, ContentType="text/plain")
    print(f"  PUT  ok  s3://{cfg['R2_BUCKET']}/{key}")

    url = cfg["R2_PUBLIC_URL"].rstrip("/") + "/" + key
    try:
        # ⛔ SEND A REAL USER-AGENT OR THIS CHECK LIES.
        # Cloudflare 403s requests with an absent/default urllib User-Agent. The first version of
        # this check omitted it and reported "the bucket is NOT public" against a bucket that was
        # perfectly public — a FALSE NEGATIVE that sent the owner back into the dashboard twice to
        # fix a setting that was already correct. A health check that reports failure on a healthy
        # system is worse than no check: it burns trust in every future green it gives.
        # Verified by experiment: UnityPlayer/* -> 200, curl/* -> 200, empty UA -> 403. The GAME is
        # therefore fine; only the checker was blocked.
        req = urllib.request.Request(url, headers={
            "User-Agent": "UnityPlayer/6000.4.8f1 (UnityWebRequest/1.0, libcurl/8.5.0)"
        })
        with urllib.request.urlopen(req, timeout=20) as resp:
            got = resp.read()
        if got != body:
            sys.exit(f"FAIL: public GET returned {len(got)} bytes, expected {len(body)}.")
        print(f"  GET  ok  {url}  (anonymous read works — players can fetch)")
    except Exception as exc:                                    # noqa: BLE001
        sys.exit(f"FAIL: public GET failed: {exc}\n"
                 "      The bucket is likely NOT public. R2 -> bucket -> Settings ->\n"
                 "      Public access -> R2.dev subdomain -> Allow access.")
    finally:
        s3.delete_object(Bucket=cfg["R2_BUCKET"], Key=key)
        print("  DEL  ok  probe removed")

    print("R2_CHECK_OK")


def cmd_ensure_cors(cfg):
    """Allow public WebGL assets to be fetched from a different web origin."""
    client(cfg).put_bucket_cors(
        Bucket=cfg["R2_BUCKET"],
        CORSConfiguration={"CORSRules": [{
            "AllowedHeaders": ["*"],
            "AllowedMethods": ["GET", "HEAD"],
            "AllowedOrigins": ["*"],
            "ExposeHeaders": ["Content-Length", "ETag"],
            "MaxAgeSeconds": 86400,
        }]},
    )
    print("R2_CORS_OK public GET/HEAD enabled for WebGL CDN assets")


def cmd_push(cfg, folder):
    """
    Uploads a directory tree, skipping objects whose CONTENT already matches.

    ⛔ HAND THIS `ServerData`, NOT `ServerData/Android`. Keys are relative to the folder
    you pass, so pointing at the target folder flattens everything to the bucket root and
    404s in game (see the module docstring — that is the `16e22dba3` bug).

    ⛔ SIZE ALONE IS NOT A SKIP TEST. It used to be, on the reasoning that bundle names are
    content-hashed so changed content always gets a new name. TRUE FOR BUNDLES, FALSE FOR
    THE CATALOG: `catalog_<version>.hash` is a 32-char md5 and therefore ALWAYS EXACTLY 32
    BYTES, and `catalog_<version>.bin` reuses its name whenever `bundleVersion` is reused.
    A size-equal skip on those two silently leaves the OLD catalog hosted — the file that
    says which content is current — so players keep resolving to yesterday's bundles.
    We now compare the md5 against the object's ETag and only skip on a real content match;
    an ETag we cannot compare (multipart, `-N` suffix) is re-uploaded rather than trusted.
    """
    src = folder if os.path.isabs(folder) else os.path.join(REPO, folder)
    if not os.path.isdir(src):
        sys.exit(f"FAIL: '{src}' does not exist. Build Addressables content first — "
                 "with the group set to the Remote profile, or nothing lands here.")
    if os.path.basename(os.path.abspath(src)).lower() in ("android", "webgl", "windows",
                                                          "standalonewindows64", "ios"):
        print(f"WARNING: '{folder}' looks like a BUILD-TARGET folder. Keys are relative to it,\n"
              f"         so these objects will land at the BUCKET ROOT and the game will 404.\n"
              f"         You almost certainly want:  --push {os.path.dirname(folder) or 'ServerData'}")

    s3 = client(cfg)
    existing = list_bucket(s3, cfg)

    sent = skipped = 0
    sent_bytes = 0
    for root, _dirs, files in os.walk(src):
        for name in files:
            path = os.path.join(root, name)
            key = os.path.relpath(path, src).replace("\\", "/")
            size = os.path.getsize(path)
            have = existing.get(key)
            if have and have["size"] == size and have["etag"] and have["etag"] == md5_of(path):
                skipped += 1
                continue
            ctype = mimetypes.guess_type(name)[0] or "application/octet-stream"
            with open(path, "rb") as fh:
                s3.put_object(Bucket=cfg["R2_BUCKET"], Key=key, Body=fh, ContentType=ctype)
            sent += 1
            sent_bytes += size
            print(f"  up {size/1048576:8.2f} MB  {key}")

    print(f"R2_PUSH_OK {sent} uploaded ({sent_bytes/1048576:.1f} MB), {skipped} unchanged")


def cmd_list(cfg):
    s3 = client(cfg)
    objs = list_bucket(s3, cfg)
    total = 0
    for i, key in enumerate(sorted(objs)):
        total += objs[key]["size"]
        if i < 40:
            print(f"  {objs[key]['size']/1048576:8.2f} MB  {key}")
    print(f"R2_LIST_OK {len(objs)} object(s), {total/1048576:.1f} MB total")


#  ─────────────────────────────────────────────────────────────────────────────
#  THE CONTENT GATE  (PROD-011)
#  ─────────────────────────────────────────────────────────────────────────────
#  WHAT IT DEFENDS AGAINST
#      Twice on 2026-08-18 an APK was built whose remote bundle was NOT in the bucket
#      (`structure_art_..._7608a3cb`, and `enemy_art_..._2d9daff5` had never been uploaded
#      at all). Both were caught BY HAND. `16e22dba3` conceded in its own commit body:
#      "NO GATE COULD HAVE CAUGHT THIS."
#
#      The consequence is silent to the pipeline and loud only to the player:
#      `StructureAssetLoader` finds the address registered, the remote load returns null,
#      and `Assets/Resources/Structures` + `Assets/Resources/Enemies` no longer exist — so
#      there is NO FALLBACK. The player gets placeholder geometry. (It IS logged:
#      `Assets/_Modules/.../StructureAssetLoader.cs:139` FlowTrace.Fail names the address.)
#
#  ⛔ BUNDLE NAMES ARE CONTENT-HASHED: EVERY BUILD PRODUCES NEW FILENAMES, SO EVERY BUILD
#     NEEDS A FRESH PUSH. "I pushed yesterday" is never a reason to skip this.

CATALOG_BUNDLE_RE = re.compile(rb"[A-Za-z0-9_\-.]{4,200}\.bundle")


def _detect_target(serverdata_root):
    subs = [d for d in sorted(os.listdir(serverdata_root))
            if os.path.isdir(os.path.join(serverdata_root, d))]
    if len(subs) != 1:
        sys.exit(f"FAIL: cannot pick a build target - {serverdata_root} holds {subs or 'nothing'}.\n"
                 "      Pass the target folder explicitly: --verify-catalog ServerData/Android")
    return subs[0]


def cmd_verify_catalog(cfg, folder):
    """
    Proves that every REMOTE object the shipped player will request is actually in the bucket.

    SOURCE OF TRUTH — and what it does and does not prove:
      1. `Library/com.unity.addressables/aa/<target>/settings.json` is AUTHORITATIVE for WHICH
         catalog the player asks for: its `AddressablesMainContentCatalogRemoteHash` location
         holds the literal URL, e.g. `https://<host>/Android/catalog_2026.08.19.331306.hash`.
         That file is what Unity bakes into the player, so it — not the newest file on disk —
         decides which of the many `catalog_*.bin` in ServerData/ is the live one.
      2. The catalog `.bin` names the bundles. Unity 6 writes it as a BINARY catalog; we do not
         claim to parse its structure. We scrape length-prefixed ASCII `*.bundle` strings out of
         it (safe: every string is preceded by a NUL-padded uint32 length, so a scrape cannot
         run into the preceding bytes), then INTERSECT that set with the `*.bundle` files the
         remote build actually emitted into ServerData/<target>/.
            - the intersection is precisely the REMOTE set for THIS catalog;
            - local/StreamingAssets bundles drop out (they were never written to ServerData);
            - STALE bundles from earlier builds drop out (this catalog does not name them).

      ⚠ WHAT THIS DOES NOT PROVE:
            - it does not prove the APK on disk was built from THIS Addressables state (nothing
              in ServerData/ or Library/ is stamped with the player build). Rebuild content, then
              the player, then verify — in that order.
            - it does not prove the bundles are LOADABLE, only that objects of the right name and
              the right bytes are hosted.
            - it does not prove anonymous public read (that is `--check`, at that instant only).
            - it says nothing about a bundle a *future* catalog will want.
    """
    root = folder if os.path.isabs(folder) else os.path.join(REPO, folder)
    if not os.path.isdir(root):
        sys.exit(f"FAIL: '{root}' does not exist. Build Addressables content first (Remote profile).")
    # Accept either `ServerData` or `ServerData/Android`.
    if os.path.isdir(os.path.join(root, "Android")) or not any(
            f.endswith(".bin") for f in os.listdir(root)):
        target = _detect_target(root)
        target_dir = os.path.join(root, target)
    else:
        target = os.path.basename(os.path.abspath(root))
        target_dir = root

    settings_path = os.path.join(REPO, "Library", "com.unity.addressables", "aa",
                                 target, "settings.json")
    if not os.path.isfile(settings_path):
        sys.exit(f"FAIL: {settings_path} not found - no built Addressables state for '{target}'.\n"
                 "      Build Addressables content, then re-run. Without it there is no proof of\n"
                 "      WHICH catalog the player would request, and this gate refuses to guess.")
    with open(settings_path, encoding="utf-8") as fh:
        settings = json.load(fh)

    remote_hash_url = None
    for loc in settings.get("m_CatalogLocations", []):
        iid = loc.get("m_InternalId", "")
        if iid.startswith("http://") or iid.startswith("https://"):
            remote_hash_url = iid
            break
    if not remote_hash_url:
        sys.exit("FAIL: settings.json names NO remote catalog location. This build would never\n"
                 "      fetch a remote catalog, so either the Remote profile is not selected or\n"
                 "      remote content is disabled. Refusing to report parity on a build that\n"
                 "      cannot use the bucket at all.")

    catalog_stem = os.path.basename(remote_hash_url)[:-len(".hash")]   # catalog_<version>
    print(f"  catalog the player will request: {catalog_stem}")
    print(f"    from {remote_hash_url}")

    cat_bin = os.path.join(target_dir, catalog_stem + ".bin")
    cat_hash = os.path.join(target_dir, catalog_stem + ".hash")
    for p in (cat_bin, cat_hash):
        if not os.path.isfile(p):
            sys.exit(f"FAIL: {p} is missing from the local build output, yet settings.json says the\n"
                     "      player will ask for it. The ServerData tree and the built state disagree -\n"
                     "      rebuild Addressables content before shipping anything.")

    # Cross-check: the live Library catalog must BE this catalog, byte for byte. If it is not,
    # ServerData holds artifacts from a different build than the one the player was stamped with.
    live_bin = os.path.join(REPO, "Library", "com.unity.addressables", "aa", target, "catalog.bin")
    if os.path.isfile(live_bin) and md5_of(live_bin) != md5_of(cat_bin):
        sys.exit(f"FAIL: {cat_bin}\n      differs from the live {live_bin}.\n"
                 "      Mixed build artifacts - do not ship. Rebuild Addressables content.")

    with open(cat_bin, "rb") as fh:
        catalog_bytes = fh.read()
    named = {m.decode("ascii") for m in CATALOG_BUNDLE_RE.findall(catalog_bytes)}
    on_disk = {f for f in os.listdir(target_dir) if f.endswith(".bundle")}
    remote_bundles = sorted(named & on_disk)
    orphans = sorted(on_disk - named)

    if not remote_bundles:
        sys.exit("FAIL: the catalog names NO bundle that exists in "
                 f"{target_dir}. Either the remote build produced nothing or this catalog belongs\n"
                 "      to a different content build. Refusing to print a green marker on an empty set.")

    required = [f"{target}/{catalog_stem}.bin", f"{target}/{catalog_stem}.hash"]
    required += [f"{target}/{b}" for b in remote_bundles]
    local_for = {f"{target}/{os.path.basename(p)}": p
                 for p in [cat_bin, cat_hash] + [os.path.join(target_dir, b) for b in remote_bundles]}

    s3 = client(cfg)
    existing = list_bucket(s3, cfg)

    missing, wrong = [], []
    for key in required:
        have = existing.get(key)
        path = local_for[key]
        size = os.path.getsize(path)
        if have is None:
            missing.append((key, size))
            continue
        if have["size"] != size:
            wrong.append((key, f"size {have['size']} in bucket, {size} locally"))
            continue
        if have["etag"] and have["etag"] != md5_of(path):
            wrong.append((key, "same size, DIFFERENT CONTENT (md5 != ETag)"))
            continue
        print(f"  ok  {size/1048576:8.2f} MB  {key}")

    # Symptom of the `--push ServerData/Android` flatten: bundle/catalog objects sitting at the
    # bucket ROOT with no `<target>/` prefix. Everything the game reads is prefixed, so an
    # un-prefixed one is by definition unreachable -- and evidence that a bad push happened.
    flattened = sorted(k for k in existing
                       if "/" not in k and (k.endswith(".bundle") or k.startswith("catalog_")))
    if flattened:
        print("\n  WARNING: these objects sit at the BUCKET ROOT with no build-target prefix:")
        for k in flattened:
            print(f"           {k}")
        print("           That is the fingerprint of `--push ServerData/Android`, which flattens.\n"
              "           The game never reads them; they are dead bytes you pay to store.\n"
              "           Always push the PARENT: `--push ServerData`.")

    if orphans:
        print(f"\n  note: {len(orphans)} bundle(s) in {target_dir} are NOT named by this catalog\n"
              "        (leftovers from earlier builds; not required, not checked):")
        for b in orphans:
            print(f"        {b}")

    if missing or wrong:
        print()
        for key, size in missing:
            print(f"  MISSING FROM BUCKET  {key}   ({size/1048576:.2f} MB local)")
        for key, why in wrong:
            print(f"  MISMATCH             {key}   ({why})")
        names = " ".join([k for k, _ in missing] + [k for k, _ in wrong])
        print(f"\nR2_PARITY_FAIL {names}")
        print("\n  THE APK WOULD SHIP WITH PLACEHOLDER CONTENT. There is no local fallback:\n"
              "  Assets/Resources/Structures and Assets/Resources/Enemies no longer exist, so a\n"
              "  null remote load is what the player sees.\n"
              "  FIX:  python tools/r2_sync.py --push ServerData      (the PARENT, never ServerData/"
              f"{target})\n"
              "  then re-run this gate. Bundle names are content-hashed, so EVERY content build\n"
              "  needs its own push; a push from a previous build can never cover this one.")
        sys.exit(1)

    print(f"\nR2_PARITY_OK {len(required)} object(s) verified")
    print("  (presence + size + content for every remote object this catalog names.\n"
          "   NOT a public-read check - that is --check, and --check is NOT this.)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="prove credentials + public read ONLY; NOT a content gate (see --verify-catalog)")
    ap.add_argument("--push", metavar="FOLDER",
                    help="upload a folder tree; pass ServerData, NOT ServerData/Android (it flattens)")
    ap.add_argument("--list", action="store_true", help="list bucket contents")
    ap.add_argument("--ensure-cors", action="store_true",
                    help="apply the public GET/HEAD CORS policy required by WebGL")
    ap.add_argument("--verify-catalog", metavar="FOLDER", nargs="?", const="ServerData",
                    dest="verify_catalog",
                    help="THE CONTENT GATE: prove every remote object the built catalog names is "
                         "in the bucket. Marker R2_PARITY_OK / R2_PARITY_FAIL.")
    args = ap.parse_args()

    cfg = load_env()
    if args.check:
        cmd_check(cfg)
    elif args.ensure_cors:
        cmd_ensure_cors(cfg)
    elif args.push:
        cmd_push(cfg, args.push)
    elif args.verify_catalog:
        cmd_verify_catalog(cfg, args.verify_catalog)
    elif args.list:
        cmd_list(cfg)
    else:
        ap.print_help()


if __name__ == "__main__":
    main()
