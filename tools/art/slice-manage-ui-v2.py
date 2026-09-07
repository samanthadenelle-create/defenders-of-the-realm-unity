#!/usr/bin/env python3
"""
Slice Manage UI icon sheets into individual PNG files.
Uses visual grid detection and manual label maps.
"""

import os
import sys
from PIL import Image, ImageDraw
from pathlib import Path

# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
# location, never hardcode a drive letter. tools/art/<script>.py -> parents[2].
REPO_ROOT = Path(__file__).resolve().parents[2]

def safe_mkdir(path):
    """Create directory if it doesn't exist."""
    Path(path).mkdir(parents=True, exist_ok=True)

def extract_grid_icons(sheet_path, sheet_name, grid_config):
    """
    Extract icons from sheet using grid configuration.
    grid_config: list of sections, each with:
    - name: section identifier
    - labels: list of icon labels in reading order
    - bbox: (x1, y1, x2, y2) bounding box of section on sheet
    - grid: (cols, rows) grid dimensions
    - exclude_bottom: pixels to exclude at bottom of each cell (for labels)
    """
    print(f"\nProcessing: {sheet_path}")

    if not os.path.exists(sheet_path):
        print(f"  ERROR: File not found")
        return 0

    try:
        img = Image.open(sheet_path)
        print(f"  Image size: {img.width} x {img.height}")
    except Exception as e:
        print(f"  ERROR: Could not open image: {e}")
        return 0

    total_extracted = 0
    output_base = str(REPO_ROOT / "ArtSource" / "ManageUiSliced" / sheet_name)

    for section in grid_config:
        section_name = section["name"]
        labels = section["labels"]
        x1, y1, x2, y2 = section["bbox"]
        cols, rows = section["grid"]
        exclude_bottom = section.get("exclude_bottom", 30)

        section_dir = os.path.join(output_base, section_name)
        safe_mkdir(section_dir)

        cell_width = (x2 - x1) / cols
        cell_height = (y2 - y1) / rows

        label_idx = 0
        for row in range(rows):
            for col in range(cols):
                if label_idx >= len(labels):
                    break

                # Calculate cell position
                cell_x1 = int(x1 + col * cell_width)
                cell_y1 = int(y1 + row * cell_height)
                cell_x2 = int(x1 + (col + 1) * cell_width)
                cell_y2 = int(y1 + (row + 1) * cell_height)

                # Exclude label area at bottom
                icon_y2 = cell_y2 - exclude_bottom

                # Crop icon (exclude label)
                crop_box = (cell_x1, cell_y1, cell_x2, icon_y2)

                # Validate
                if (crop_box[2] > img.width or crop_box[3] > img.height or
                    crop_box[0] < 0 or crop_box[1] < 0):
                    print(f"  WARN: Crop out of bounds for {labels[label_idx]}")
                    label_idx += 1
                    continue

                try:
                    icon_crop = img.crop(crop_box)
                    label = labels[label_idx]
                    output_file = os.path.join(section_dir, f"{label}.png")
                    icon_crop.save(output_file)
                    print(f"    ✓ {label}.png ({icon_crop.width}x{icon_crop.height})")
                    total_extracted += 1

                except Exception as e:
                    print(f"    ERROR: {labels[label_idx]}: {e}")

                label_idx += 1

    print(f"  Section total: {total_extracted}")
    return total_extracted

# Configuration for each sheet based on visual inspection
SHEET_CONFIGS = {
    "ChatGPT Image Sep 6, 2026, 09_52_23 AM (1).png": [
        {
            "name": "01_NAVIGATION_TABS",
            "labels": ["tab-build", "tab-army", "tab-research", "tab-queue"],
            "bbox": (22, 95, 460, 180),
            "grid": (4, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "02_FILTER_TABS_BUILD",
            "labels": ["filter-all", "filter-economy", "filter-defense", "filter-craft", "filter-storage", "filter-chic"],
            "bbox": (480, 95, 1050, 180),
            "grid": (6, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "03_UI_BUTTONS",
            "labels": ["btn-upgrade", "btn-train", "btn-research", "btn-queue", "btn-view", "btn-go-heart"],
            "bbox": (1050, 65, 1530, 200),
            "grid": (3, 2),
            "exclude_bottom": 25,
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
            "bbox": (25, 255, 1520, 505),
            "grid": (10, 3),
            "exclude_bottom": 20,
        },
        {
            "name": "05_TROOPS",
            "labels": [
                "troop-footman", "troop-archer", "troop-spearman", "troop-field-cleric",
                "troop-shield-guard", "troop-outrider", "troop-catapult", "troop-battlemage",
                "troop-arctic-legionnaire"
            ],
            "bbox": (18, 540, 900, 650),
            "grid": (9, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "06_RESEARCH_SCHOOLS",
            "labels": ["research-cathedral", "research-armorer", "research-forge", "research-barracks"],
            "bbox": (910, 540, 1520, 650),
            "grid": (4, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "07_RESOURCE_ICONS",
            "labels": ["res-wood", "res-stone", "res-iron", "res-crystal", "res-gold"],
            "bbox": (18, 700, 470, 800),
            "grid": (5, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "08_STATUS_ICONS",
            "labels": [
                "status-locked", "status-available", "status-hourglass", "status-crown",
                "status-menu", "status-warning", "status-error", "status-check"
            ],
            "bbox": (475, 700, 1130, 800),
            "grid": (8, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "09_OTHER_UI_ELEMENTS",
            "labels": ["progress-bar-bg", "progress-bar-fill", "frame-lg", "frame-selected", "frame-locked", "frame-mp",
                      "badge-level", "badge-spicer", "badge-gamer-popup-dial", "badge-spicer-upl",
                      "badge-rowarth", "badge-spicer"],
            "bbox": (250, 795, 1530, 880),
            "grid": (12, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "10_BG_AND_DECOR",
            "labels": ["panel-bg", "badge-leaf-brp"],
            "bbox": (1160, 700, 1530, 850),
            "grid": (2, 1),
            "exclude_bottom": 20,
        },
    ],

    "ChatGPT Image Sep 6, 2026, 09_52_23 AM (2).png": [
        {
            "name": "TAB_ICONS",
            "labels": ["tab-build", "tab-army", "tab-research", "tab-queue"],
            "bbox": (22, 35, 450, 160),
            "grid": (4, 1),
            "exclude_bottom": 25,
        },
        {
            "name": "RESOURCE_ICONS",
            "labels": ["icon-wood", "icon-stone", "icon-iron", "icon-crystal", "icon-gold", "icon-time", "icon-lock", "icon-plus", "icon-check", "icon-max"],
            "bbox": (500, 35, 800, 180),
            "grid": (5, 2),
            "exclude_bottom": 20,
        },
        {
            "name": "STATE_BADGES",
            "labels": ["badge-available", "badge-locked", "badge-inprogress", "badge-max", "badge-queue", "badge-researched", "badge-new", "badge-upgrade"],
            "bbox": (820, 35, 1530, 160),
            "grid": (4, 2),
            "exclude_bottom": 20,
        },
        {
            "name": "BUILDING_ICONS",
            "labels": [
                "bld-lumbermill", "bld-quarry", "bld-ironmine", "bld-crystalmine", "bld-barracks", "bld-cathedral",
                "bld-forge", "bld-armorer", "bld-weaponsmith", "bld-craftingstation", "bld-jeweler", "bld-archer-tower",
                "bld-ballista", "bld-arcane-spire", "bld-catapult", "bld-skyballista", "bld-palisade", "bld-stonegate",
                "bld-storeyard", "bld-lumberyard", "bld-foundry", "bld-echoplace", "bld-store", "bld-healingcaravan"
            ],
            "bbox": (25, 220, 750, 620),
            "grid": (6, 4),
            "exclude_bottom": 20,
        },
        {
            "name": "TROOP_ICONS",
            "labels": [
                "troop-footman", "troop-archer", "troop-spearman", "troop-cleric", "troop-shieldguard",
                "troop-outrider", "troop-catapult", "troop-battlemage", "troop-echo-legionnaire"
            ],
            "bbox": (770, 220, 950, 620),
            "grid": (3, 3),
            "exclude_bottom": 20,
        },
        {
            "name": "RESEARCH_ICONS",
            "labels": ["res-arcane", "res-defense", "res-weapons", "res-army"],
            "bbox": (970, 220, 1530, 350),
            "grid": (4, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "UI_BUTTONS",
            "labels": ["btn-upgrade", "btn-primary", "btn-secondary", "btn-train", "btn-research", "btn-disabled"],
            "bbox": (970, 370, 1530, 620),
            "grid": (2, 3),
            "exclude_bottom": 20,
        },
        {
            "name": "FILTER_TABS",
            "labels": ["filter-all", "filter-economy", "filter-defense", "filter-craft", "filter-storage", "filter-civic"],
            "bbox": (25, 660, 600, 800),
            "grid": (6, 1),
            "exclude_bottom": 25,
        },
        {
            "name": "QUEUE_UI_ELEMENTS",
            "labels": ["queue-icon", "queue-badge", "progress-bar"],
            "bbox": (620, 660, 980, 800),
            "grid": (3, 1),
            "exclude_bottom": 25,
        },
        {
            "name": "MISC_ICONS",
            "labels": ["panel-frame", "tile-frame", "icon-upgrade", "icon-train", "icon-research", "icon-bank", "icon-x"],
            "bbox": (1000, 660, 1530, 800),
            "grid": (7, 1),
            "exclude_bottom": 25,
        },
    ],

    "ChatGPT Image Sep 6, 2026, 09_52_23 AM (3).png": [
        {
            "name": "TOP_TAB_BUTTONS",
            "labels": ["btn-build", "btn-army", "btn-research", "btn-queue"],
            "bbox": (25, 20, 520, 145),
            "grid": (4, 1),
            "exclude_bottom": 30,
        },
        {
            "name": "TOP_ACTION_BUTTONS",
            "labels": ["btn-upgrade", "btn-train", "btn-locked", "icon-back", "icon-close", "icon-menu"],
            "bbox": (550, 20, 1530, 145),
            "grid": (6, 1),
            "exclude_bottom": 30,
        },
        {
            "name": "FILTER_AND_STATE_ICONS",
            "labels": [
                "filter-all", "filter-economy", "filter-defense", "filter-craft", "filter-storage", "filter-civic",
                "icon-available", "icon-inprogress", "icon-locked", "icon-max", "icon-queue"
            ],
            "bbox": (25, 160, 1530, 280),
            "grid": (11, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "RESOURCE_ICONS",
            "labels": [
                "icon-wood", "icon-stone", "icon-iron", "icon-crystal", "icon-gold", "icon-time",
                "icon-population", "icon-health", "icon-attack", "icon-defense", "icon-speed", "icon-mana"
            ],
            "bbox": (25, 295, 1530, 370),
            "grid": (12, 1),
            "exclude_bottom": 20,
        },
        {
            "name": "BUILDING_ICONS_LARGE",
            "labels": [
                "building-lumbermill", "building-quarry", "building-ironmine", "building-crystalmine", "building-barracks",
                "building-cathedral", "building-forge", "building-armorer", "building-jeweler", "building-crafting",
                "building-archer-tower", "building-ballista", "building-arcane-spire", "building-catapult", "building-skyballista",
                "building-palisade", "building-stonegate", "building-stonegate2", "building-lumberyard", "building-storeyard",
                "building-foundry", "building-echoplace", "building-healing-caravan", "building-store"
            ],
            "bbox": (25, 385, 1150, 720),
            "grid": (10, 3),
            "exclude_bottom": 20,
        },
        {
            "name": "TROOP_ICONS",
            "labels": [
                "troop-footman", "troop-archer", "troop-spearman", "troop-cleric", "troop-shieldguard",
                "troop-outrider", "troop-catapult", "troop-battlemage", "troop-echo-legionnaire"
            ],
            "bbox": (25, 720, 1000, 880),
            "grid": (10, 1),
            "exclude_bottom": 25,
        },
        {
            "name": "RESEARCH_ICONS_FINAL",
            "labels": ["research-arcane", "research-skills", "research-weapons", "research-tactics"],
            "bbox": (550, 720, 1000, 880),
            "grid": (4, 1),
            "exclude_bottom": 25,
        },
        {
            "name": "BUILDING_HEART",
            "labels": ["building-heart"],
            "bbox": (1180, 720, 1530, 880),
            "grid": (1, 1),
            "exclude_bottom": 25,
        },
    ],
}

def main():
    source_dir = str(REPO_ROOT / "ArtSource" / "ManageUiSheets")

    if not os.path.exists(source_dir):
        print(f"ERROR: Source directory not found: {source_dir}")
        return 1

    total_all = 0

    for filename, grid_config in SHEET_CONFIGS.items():
        sheet_path = os.path.join(source_dir, filename)
        sheet_name = Path(sheet_path).stem
        count = extract_grid_icons(sheet_path, sheet_name, grid_config)
        total_all += count

    # Count the 10 square images (single icons)
    square_files = [f for f in os.listdir(source_dir) if f.endswith(".png") and
                    (f.startswith("ChatGPT Image Sep 6, 2026, 09_54") or
                     f.startswith("ChatGPT Image Sep 6, 2026, 09_54_37"))]

    if square_files:
        print(f"\n{len(square_files)} square images found (1254x1254) - these appear to be single hero/feature images, not icon grids")
        print("These will not be sliced as they are individual assets, not contact sheets.")

    print(f"\n=== TOTAL ICONS EXTRACTED: {total_all} ===")
    return 0

if __name__ == "__main__":
    sys.exit(main())
