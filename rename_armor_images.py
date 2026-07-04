#!/usr/bin/env python3
"""
Armor Image Renamer — Defenders of the Realm
=============================================
DROP all Grok armor images into:
    Assets/Resources/ItemIcons/staging/

Then run this script from C:\\EoA\\ :
    python rename_armor_images.py

It sorts the staged files by modification time (the order you saved them
from Grok), maps them to canonical itemIcon names, and copies them to:
    Assets/Resources/ItemIcons/

The mapping is based on the upload order from the 2026-06-27 session.
"""

import os
import shutil
import sys

# ---------------------------------------------------------------------------
# Paths (relative to repo root C:\\EoA)
# ---------------------------------------------------------------------------
STAGING_DIR = os.path.join("Assets", "Resources", "ItemIcons", "staging")
DEST_DIR    = os.path.join("Assets", "Resources", "ItemIcons")

# ---------------------------------------------------------------------------
# Mapping: position (1-indexed, sorted by mtime oldest→newest) → target name
# None = extra/alternate image — copied to staging/extras/ for reference
# ---------------------------------------------------------------------------
ORDER_MAP = {
    1:  "armor_ranger_common.png",       # green leather, hood, bronze medallion
    2:  "armor_knight_common.png",       # rusty worn iron full plate
    3:  "armor_knight_legendary.png",    # black plate, orange calligraphy runes close-up
    4:  None,                            # EXTRA: galaxy/nebula robe (alternate mage epic)
    5:  "armor_mage_rare.png",           # dark floating vest, orange sigil symbols
    6:  "armor_mage_epic.png",           # dark navy, blue-white constellation trim, aether flame
    7:  "armor_mage_uncommon.png",       # dark robe, purple/silver rune strip trim
    8:  "armor_ranger_epic.png",         # dark plate, golden leaf motif, green energy glow
    9:  None,                            # EXTRA: dark silver-swirl long coat
    10: None,                            # EXTRA: nature helmet with leaf plume, glowing green eyes
    11: "armor_ranger_rare.png",         # dark leather chest, green moss patches, stitched
    12: "armor_ranger_uncommon.png",     # brown leather, arrows on back, hood, buckled belt
    13: "armor_knight_rare.png",         # dark navy plate, elaborate gold scrollwork etching
    14: "armor_knight_epic.png",         # full-body black plate, ember/lava fire glow
    15: "armor_knight_uncommon.png",     # gray-white cracked plate, simpler illustrated style
    16: None,                            # EXTRA: black plate, orange runic symbols (alt legendary)
    17: "armor_mage_legendary.png",      # navy/dark celestial robe, gold star trim, moons
    18: "armor_ranger_legendary.png",    # full-length dark coat with embedded leaf shapes
}

EXTRAS_DIR = os.path.join(STAGING_DIR, "extras")

def main():
    # Verify staging dir exists
    if not os.path.isdir(STAGING_DIR):
        print(f"ERROR: Staging folder not found: {STAGING_DIR}")
        print(f"Create it and drop your armor images in before running this script.")
        sys.exit(1)

    # Collect image files
    exts = {".png", ".jpg", ".jpeg", ".webp"}
    files = [
        f for f in os.listdir(STAGING_DIR)
        if os.path.splitext(f)[1].lower() in exts
        and os.path.isfile(os.path.join(STAGING_DIR, f))
    ]

    if not files:
        print(f"No images found in {STAGING_DIR}")
        print("Drop your Grok armor images in there first, then re-run.")
        sys.exit(1)

    # Sort by modification time — oldest first (matches save order from Grok)
    files.sort(key=lambda f: os.path.getmtime(os.path.join(STAGING_DIR, f)))

    print(f"\nFound {len(files)} images in staging. Expected 18.\n")
    if len(files) != 18:
        print(f"WARNING: Expected 18 images, got {len(files)}.")
        print("The mapping was designed for 18 images in a specific order.")
        print("Results may be off if the count differs.\n")

    # Create extras dir
    os.makedirs(EXTRAS_DIR, exist_ok=True)
    os.makedirs(DEST_DIR, exist_ok=True)

    renamed = []
    extras  = []
    skipped = []

    for idx, fname in enumerate(files):
        position = idx + 1
        src = os.path.join(STAGING_DIR, fname)
        target_name = ORDER_MAP.get(position)

        if target_name is None:
            # Extra — stash in extras/
            extra_dest = os.path.join(EXTRAS_DIR, fname)
            shutil.copy2(src, extra_dest)
            extras.append((position, fname, "→ staging/extras/ (alternate/extra)"))
        else:
            dest = os.path.join(DEST_DIR, target_name)
            shutil.copy2(src, dest)
            renamed.append((position, fname, target_name))

    # Report
    print("=" * 60)
    print("RENAMED → Assets/Resources/ItemIcons/")
    print("=" * 60)
    for pos, src_name, dest_name in renamed:
        print(f"  [{pos:02d}] {src_name}  →  {dest_name}")

    if extras:
        print()
        print("EXTRAS → staging/extras/ (not used in game yet)")
        for pos, src_name, note in extras:
            print(f"  [{pos:02d}] {src_name}  {note}")

    print()
    print(f"Done. {len(renamed)} files renamed, {len(extras)} extras preserved.")
    print()
    print("MISSING (still needed):")
    print("  armor_mage_common.png — no plain apprentice robes in this batch.")
    print("  Generate using the prompt in docs/GROK_IMAGE_PROMPTS_GEAR.md")
    print("  and drop it directly into Assets/Resources/ItemIcons/")

if __name__ == "__main__":
    main()
