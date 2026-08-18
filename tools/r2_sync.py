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
    python tools/r2_sync.py --check                 # prove credentials + bucket work
    python tools/r2_sync.py --push ServerData/Android
    python tools/r2_sync.py --list
"""

import argparse
import hashlib
import mimetypes
import os
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
        with urllib.request.urlopen(url, timeout=20) as resp:
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


def cmd_push(cfg, folder):
    """
    Uploads a directory tree, skipping objects whose size already matches.

    Skips by size, not hash: Addressables bundle names are CONTENT-HASHED, so a
    changed bundle gets a NEW name rather than the same name with new bytes. Size
    is therefore a sufficient and much cheaper check.
    """
    src = folder if os.path.isabs(folder) else os.path.join(REPO, folder)
    if not os.path.isdir(src):
        sys.exit(f"FAIL: '{src}' does not exist. Build Addressables content first — "
                 "with the group set to the Remote profile, or nothing lands here.")

    s3 = client(cfg)
    existing = {}
    token = None
    while True:
        kw = {"Bucket": cfg["R2_BUCKET"]}
        if token:
            kw["ContinuationToken"] = token
        page = s3.list_objects_v2(**kw)
        for obj in page.get("Contents", []):
            existing[obj["Key"]] = obj["Size"]
        if not page.get("IsTruncated"):
            break
        token = page.get("NextContinuationToken")

    sent = skipped = 0
    sent_bytes = 0
    for root, _dirs, files in os.walk(src):
        for name in files:
            path = os.path.join(root, name)
            key = os.path.relpath(path, src).replace("\\", "/")
            size = os.path.getsize(path)
            if existing.get(key) == size:
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
    total = count = 0
    token = None
    while True:
        kw = {"Bucket": cfg["R2_BUCKET"]}
        if token:
            kw["ContinuationToken"] = token
        page = s3.list_objects_v2(**kw)
        for obj in page.get("Contents", []):
            count += 1
            total += obj["Size"]
            if count <= 20:
                print(f"  {obj['Size']/1048576:8.2f} MB  {obj['Key']}")
        if not page.get("IsTruncated"):
            break
        token = page.get("NextContinuationToken")
    print(f"R2_LIST_OK {count} object(s), {total/1048576:.1f} MB total")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="prove credentials + public read work")
    ap.add_argument("--push", metavar="FOLDER", help="upload a folder tree (e.g. ServerData/Android)")
    ap.add_argument("--list", action="store_true", help="list bucket contents")
    args = ap.parse_args()

    cfg = load_env()
    if args.check:
        cmd_check(cfg)
    elif args.push:
        cmd_push(cfg, args.push)
    elif args.list:
        cmd_list(cfg)
    else:
        ap.print_help()


if __name__ == "__main__":
    main()
