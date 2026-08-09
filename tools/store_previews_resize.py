#!/usr/bin/env python3
"""
store_previews_resize.py - normalize images to EXACTLY 1920x1080 for the
Solana dApp Store (all previews must match the first landscape preview).

Usage:
    python tools/store_previews_resize.py <INPUT_DIR> [OUTPUT_DIR]

- Never distorts: images already ~16:9 are upscaled full-bleed; anything else is
  FIT (no crop, no stretch) onto a blurred+darkened cover backdrop, so the frame
  is exactly 1920x1080 with no ugly black bars and nothing important cut off.
- Skips portrait / extreme-aspect junk (AR < 1.2 or AR > 3.0) so a folder full of
  personal screenshots does not get force-fit. Override with --all to convert
  every image regardless of aspect.
- Writes .jpg (quality 92). Originals are never modified.
"""
import os, sys, glob
from PIL import Image, ImageFilter

TARGET = (1920, 1080)
TAR_AR = TARGET[0] / TARGET[1]

def normalize(im):
    im = im.convert("RGB")
    w, h = im.size
    ar = w / h
    if (w, h) == TARGET:
        return im.copy(), "already 1920x1080"
    if abs(ar - TAR_AR) <= 0.006 * TAR_AR:
        return im.resize(TARGET, Image.LANCZOS), "full-bleed resize (~16:9)"
    # FIT (contain) the whole image, no crop, no stretch.
    scale = min(TARGET[0] / w, TARGET[1] / h)
    fw, fh = int(round(w * scale)), int(round(h * scale))
    fg = im.resize((fw, fh), Image.LANCZOS)
    # Blurred, darkened COVER as the backdrop so bars read as intentional.
    cs = max(TARGET[0] / w, TARGET[1] / h)
    bw, bh = int(round(w * cs)), int(round(h * cs))
    bg = im.resize((bw, bh), Image.LANCZOS).filter(ImageFilter.GaussianBlur(28))
    bg = bg.crop(((bw - TARGET[0]) // 2, (bh - TARGET[1]) // 2,
                  (bw - TARGET[0]) // 2 + TARGET[0], (bh - TARGET[1]) // 2 + TARGET[1]))
    bg = Image.eval(bg, lambda p: int(p * 0.55))
    canvas = bg.copy()
    canvas.paste(fg, ((TARGET[0] - fw) // 2, (TARGET[1] - fh) // 2))
    side = (TARGET[0] - fw) // 2
    top = (TARGET[1] - fh) // 2
    return canvas, f"fit+blurpad (AR {ar:.3f}, {'pillar '+str(side)+'px' if side else 'letter '+str(top)+'px'})"

def main():
    args = [a for a in sys.argv[1:] if a != "--all"]
    keep_all = "--all" in sys.argv
    if not args:
        print(__doc__); sys.exit(1)
    src = args[0]
    out = args[1] if len(args) > 1 else os.path.join(src, "_1920x1080")
    os.makedirs(out, exist_ok=True)
    files = []
    for ext in ("*.png", "*.jpg", "*.jpeg", "*.PNG", "*.JPG"):
        files += glob.glob(os.path.join(src, ext))
    files = sorted(set(files))
    done = skipped = 0
    for f in files:
        if os.path.isdir(f):
            continue
        try:
            im = Image.open(f)
            w, h = im.size
            ar = w / h
            if not keep_all and (ar < 1.2 or ar > 3.0):
                print(f"  SKIP  {os.path.basename(f):46s} AR={ar:.2f} (portrait/extreme; use --all to force)")
                skipped += 1
                continue
            out_img, how = normalize(im)
            base = os.path.splitext(os.path.basename(f))[0]
            outp = os.path.join(out, base + ".jpg")
            out_img.save(outp, "JPEG", quality=92)
            assert out_img.size == TARGET
            print(f"  OK    {os.path.basename(f):46s} -> {base}.jpg  [{how}]")
            done += 1
        except Exception as e:
            print(f"  ERR   {os.path.basename(f):46s} {e}")
    print(f"\n{done} converted, {skipped} skipped. All output is exactly 1920x1080 in:\n  {out}")

if __name__ == "__main__":
    main()
