#!/usr/bin/env python3
"""
Slice Manage UI icon sheets into individual PNG files.
Analyzes grid structure, extracts icons, and saves with labels from the sheets.
"""

import os
import sys
from PIL import Image
import json
from pathlib import Path

# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
# location, never hardcode a drive letter. tools/art/<script>.py -> parents[2].
REPO_ROOT = Path(__file__).resolve().parents[2]

# Define the sheets and their known label maps
# Based on manual inspection of the first large sheet
SHEET_CONFIGS = {
    "ChatGPT Image Sep 6, 2026, 09_52_23 AM (1).png": {
        "sections": [
            {
                "name": "01_NAVIGATION_TABS",
                "labels": ["tab-build", "tab-army", "tab-research", "tab-queue"],
                "grid": (4, 1),  # 4 cols, 1 row
                "start_pos": (22, 95),  # Top-left of first icon
                "cell_size": (110, 110),  # Approx cell dimensions
            },
            {
                "name": "02_FILTER_TABS_BUILD",
                "labels": ["filter-all", "filter-economy", "filter-defense", "filter-craft", "filter-storage", "filter-chic"],
                "grid": (6, 1),
                "start_pos": (480, 95),
                "cell_size": (110, 110),
            },
            {
                "name": "03_UI_BUTTONS",
                "labels": ["btn-upgrade", "btn-train", "btn-research", "btn-queue", "btn-view", "btn-go-heart"],
                "grid": (3, 2),
                "start_pos": (1050, 95),
                "cell_size": (150, 110),
            },
            {
                "name": "04_BUILDINGS",
                "labels": [
                    "building-lumbermill", "building-quarry", "building-iron-mine", "building-crystal-mine",
                    "building-barracks", "building-echo-hollow", "building-cathedral", "building-crafting-station",
                    "building-weaponsmith", "building-armorer",
                    "building-jeweler", "building-archer-tower", "building-ballista", "building-arcane-spire",
                    "building-catapult-lair", "building-sky-ball-towers", "building-wooden-palisade",
                    "building-stone-wall", "building-stone-gate", "building-lumberyard",
                    "building-storeyard", "building-foundry", "building-healing-caravan", "building-silo"
                ],
                "grid": (10, 3),  # Rough: 10 cols x ~3 rows
                "start_pos": (25, 255),
                "cell_size": (140, 155),
            },
            {
                "name": "05_TROOPS",
                "labels": [
                    "troop-footman", "troop-archer", "troop-spearman", "troop-field-cleric",
                    "troop-shield-guard", "troop-outrider", "troop-catapult", "troop-battlemage",
                    "troop-arctic-legionnaire"
                ],
                "grid": (9, 1),
                "start_pos": (18, 540),
                "cell_size": (110, 120),
            },
            {
                "name": "06_RESEARCH_SCHOOLS",
                "labels": ["research-cathedral", "research-armorer", "research-forge", "research-barracks"],
                "grid": (4, 1),
                "start_pos": (910, 540),
                "cell_size": (110, 120),
            },
            {
                "name": "07_RESOURCE_ICONS",
                "labels": ["res-wood", "res-stone", "res-iron", "res-crystal", "res-gold"],
                "grid": (5, 1),
                "start_pos": (18, 700),
                "cell_size": (110, 110),
            },
            {
                "name": "08_STATUS_ICONS",
                "labels": [
                    "status-locked", "status-available", "status-hourglass", "status-crown",
                    "status-menu", "status-warning", "status-error", "status-check"
                ],
                "grid": (8, 1),
                "start_pos": (475, 700),
                "cell_size": (110, 110),
            },
            {
                "name": "09_OTHER_UI_ELEMENTS",
                "labels": ["progress-bar-bg", "progress-bar-fill"],
                "grid": (2, 1),
                "start_pos": (18, 795),
                "cell_size": (200, 70),
            },
            {
                "name": "10_UI_FRAMES_AND_BADGES",
                "labels": [
                    "frame-lg", "frame-selected", "frame-locked", "frame-mp",
                    "badge-level", "badge-spicer", "badge-gamer-popup-dial", "badge-spicer-upl",
                    "badge-rowarth", "badge-spicer"
                ],
                "grid": (10, 1),
                "start_pos": (250, 795),
                "cell_size": (100, 70),
            },
            {
                "name": "R6_STUER_ICONS",
                "labels": ["badge-leaf-brp"],
                "grid": (1, 1),
                "start_pos": (1160, 700),
                "cell_size": (140, 140),
            },
        ]
    }
}

def safe_mkdir(path):
    """Create directory if it doesn't exist."""
    Path(path).mkdir(parents=True, exist_ok=True)

def extract_icons_from_sheet(sheet_path, config):
    """Extract icons from a single sheet based on configuration."""
    print(f"\nProcessing: {sheet_path}")

    if not os.path.exists(sheet_path):
        print(f"  ERROR: File not found: {sheet_path}")
        return 0

    try:
        img = Image.open(sheet_path)
        print(f"  Image size: {img.size}")
    except Exception as e:
        print(f"  ERROR: Could not open image: {e}")
        return 0

    total_extracted = 0
    sheet_name = Path(sheet_path).stem
    output_base = str(REPO_ROOT / "ArtSource" / "ManageUiSliced" / sheet_name)

    for section in config.get("sections", []):
        section_name = section["name"]
        labels = section["labels"]
        start_x, start_y = section["start_pos"]
        cell_w, cell_h = section["cell_size"]
        cols, rows = section["grid"]

        section_dir = os.path.join(output_base, section_name)
        safe_mkdir(section_dir)

        label_idx = 0
        for row in range(rows):
            for col in range(cols):
                if label_idx >= len(labels):
                    break

                # Calculate icon position
                icon_x = start_x + col * cell_w
                icon_y = start_y + row * cell_h

                # Estimate icon region (exclude bottom text area)
                # Typically text is in bottom 15-25% of cell
                text_margin = int(cell_h * 0.25)
                crop_h = cell_h - text_margin

                crop_box = (icon_x, icon_y, icon_x + cell_w, icon_y + crop_h)

                # Validate crop box is within image bounds
                if (crop_box[2] > img.width or crop_box[3] > img.height or
                    crop_box[0] < 0 or crop_box[1] < 0):
                    print(f"  WARN: Crop out of bounds for {labels[label_idx]} at ({row},{col})")
                    label_idx += 1
                    continue

                try:
                    icon_crop = img.crop(crop_box)

                    # Save with label name
                    label = labels[label_idx]
                    output_file = os.path.join(section_dir, f"{label}.png")
                    icon_crop.save(output_file)
                    print(f"    ✓ {label}.png")
                    total_extracted += 1

                except Exception as e:
                    print(f"    ERROR extracting {labels[label_idx]}: {e}")

                label_idx += 1

    print(f"  Total extracted from sheet: {total_extracted}")
    return total_extracted

def main():
    source_dir = str(REPO_ROOT / "ArtSource" / "ManageUiSheets")

    if not os.path.exists(source_dir):
        print(f"ERROR: Source directory not found: {source_dir}")
        return 1

    # Get all PNG files
    sheet_files = sorted([f for f in os.listdir(source_dir) if f.endswith(".png")])
    print(f"Found {len(sheet_files)} sheet files")

    total_all = 0

    # Process sheets with known configs
    for filename, config in SHEET_CONFIGS.items():
        sheet_path = os.path.join(source_dir, filename)
        count = extract_icons_from_sheet(sheet_path, config)
        total_all += count

    # For sheets without explicit config, we'll need manual analysis
    processed_files = set(SHEET_CONFIGS.keys())
    unprocessed = [f for f in sheet_files if f not in processed_files]

    if unprocessed:
        print(f"\nUnprocessed sheets (need manual config): {unprocessed}")

    print(f"\n=== TOTAL EXTRACTED: {total_all} ===")
    return 0

if __name__ == "__main__":
    sys.exit(main())
