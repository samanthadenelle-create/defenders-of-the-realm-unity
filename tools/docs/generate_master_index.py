#!/usr/bin/env python3
"""
Defenders of the Realm - Automated Documentation Indexer
========================================================
Scans every .md file under docs/ (and subfolders), builds a clean
hierarchical Single Source of Truth master index, and writes:

  docs/00_MASTER_INDEX.md

Usage:
  python generate_master_index.py [--docs-path path/to/docs] [--dry-run]

Drop this script into your project root or tools/ folder and run it
whenever you want a fresh, accurate master document.
"""

from __future__ import annotations

import argparse
import datetime
import os
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Optional, Tuple


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

DEFAULT_DOCS_PATH = "docs"
MASTER_FILENAME = "00_MASTER_INDEX.md"

# Categories used for automatic grouping (order matters)
CATEGORY_ORDER = [
    ("Architecture", ["architecture", "arch", "system design", "overview", "single source"]),
    ("Systems", ["system", "registry", "motioncaster", "motion caster", "animator", "action", "vfx", "sfx"]),
    ("Building & Economy", ["building", "castle", "placement", "forge", "economy", "town", "structure"]),
    ("AI & Enemies", ["enemy", "ai", "pillager", "wave", "targeting"]),
    ("Creative Reviews", ["creative", "review", "wo-", "decision"]),
    ("Work Orders", ["work order", "wo", "task", "implement"]),
    ("Animation", ["animation", "motion", "clip", "rig", "humanoid"]),
    ("Onboarding", ["first session", "onboarding", "tutorial", "founders"]),
    ("Archive", ["archive", "deprecated", "old", "legacy"]),
]

STATUS_KEYWORDS = {
    "stable": ["stable", "shipped", "ready", "done", "complete", "production"],
    "in_progress": ["wip", "in progress", "todo", "draft", "active"],
    "deprecated": ["deprecated", "obsolete", "do not use", "old"],
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def slugify(text: str) -> str:
    text = text.lower().strip()
    text = re.sub(r"[^\w\s-]", "", text)
    text = re.sub(r"[-\s]+", "-", text)
    return text


def extract_title(content: str, filename: str) -> str:
    """Pull the first H1 or fall back to a cleaned filename."""
    for line in content.splitlines()[:30]:
        line = line.strip()
        if line.startswith("# "):
            return line[2:].strip()
        if line.startswith("## ") and "title" in line.lower():
            return line[3:].strip()
    # Fallback
    name = Path(filename).stem
    name = re.sub(r"^\d+[_\-\s]*", "", name)  # strip leading numbers
    name = name.replace("_", " ").replace("-", " ")
    return name.title()


def extract_status(content: str) -> str:
    lower = content.lower()
    for status, keywords in STATUS_KEYWORDS.items():
        for kw in keywords:
            if kw in lower:
                return status
    return "unknown"


def categorize(path: Path, title: str, content: str) -> str:
    """Assign a high-level category based on path + title + content."""
    haystack = f"{path.as_posix()} {title} {content[:1500]}".lower()

    for category, keywords in CATEGORY_ORDER:
        for kw in keywords:
            if kw in haystack:
                return category
    return "Other"


def get_relative_link(from_file: Path, to_file: Path) -> str:
    """Return a relative markdown link."""
    try:
        rel = os.path.relpath(to_file, start=from_file.parent)
        return rel.replace("\\", "/")
    except ValueError:
        return to_file.as_posix()


# ---------------------------------------------------------------------------
# Core logic
# ---------------------------------------------------------------------------

class DocEntry:
    def __init__(self, path: Path, root: Path):
        self.path = path
        self.rel_path = path.relative_to(root)
        self.content = path.read_text(encoding="utf-8", errors="replace")
        self.title = extract_title(self.content, path.name)
        self.status = extract_status(self.content)
        self.category = categorize(self.rel_path, self.title, self.content)
        self.word_count = len(self.content.split())
        self.modified = datetime.datetime.fromtimestamp(path.stat().st_mtime)

    @property
    def status_badge(self) -> str:
        return {
            "stable": "🟢 Stable",
            "in_progress": "🟡 In Progress",
            "deprecated": "🔴 Deprecated",
            "unknown": "⚪ Unknown",
        }.get(self.status, "⚪ Unknown")


def scan_docs(docs_root: Path) -> List[DocEntry]:
    entries: List[DocEntry] = []
    for md in docs_root.rglob("*.md"):
        if md.name == MASTER_FILENAME:
            continue  # never index the master itself
        if "node_modules" in md.parts or ".git" in md.parts:
            continue
        try:
            entries.append(DocEntry(md, docs_root))
        except Exception as e:
            print(f"  ⚠️  Skipped {md}: {e}", file=sys.stderr)
    return sorted(entries, key=lambda e: (e.category, e.rel_path.as_posix()))


def build_master_index(entries: List[DocEntry], docs_root: Path) -> str:
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    by_category: Dict[str, List[DocEntry]] = defaultdict(list)
    for e in entries:
        by_category[e.category].append(e)

    lines: List[str] = []

    # Header
    lines.append("# 00 – MASTER INDEX")
    lines.append("")
    lines.append("> **Single Source of Truth** for Defenders of the Realm")
    lines.append(">")
    lines.append(f"> Auto-generated on **{now}** · {len(entries)} documents indexed")
    lines.append(">")
    lines.append("> ⚠️  Do not edit this file by hand. Re-run `generate_master_index.py` after adding or moving docs.")
    lines.append("")
    lines.append("---")
    lines.append("")

    # Quick Status Board
    lines.append("## 📊 System Status Board")
    lines.append("")
    lines.append("| System | Status | Key Doc |")
    lines.append("|--------|--------|---------|")

    # Heuristic status rows for the major systems we know about
    status_rows = [
        ("Animation / Motion Caster / Action Registry", "Systems", "keyword registry, motion caster"),
        ("Building Placement (Free Place)", "Building & Economy", "castle builder, placement"),
        ("Economy & Collectors", "Building & Economy", "economy, collector"),
        ("Enemy AI & Targeting (Pillagers)", "AI & Enemies", "enemy, pillager"),
        ("Creative Direction (WO-673)", "Creative Reviews", "creative review"),
        ("First Session / Onboarding", "Onboarding", "first session, founders"),
    ]

    for system_name, cat, keywords in status_rows:
        # Find the most relevant doc
        candidates = by_category.get(cat, []) + entries
        best = None
        for e in candidates:
            if any(k in e.title.lower() or k in e.rel_path.as_posix().lower() for k in keywords.split(", ")):
                best = e
                break
        if best:
            link = f"[{best.title}]({best.rel_path.as_posix()})"
            badge = best.status_badge
        else:
            link = "—"
            badge = "⚪ Unknown"
        lines.append(f"| {system_name} | {badge} | {link} |")
    lines.append("")
    lines.append("---")
    lines.append("")

    # Full Table of Contents
    lines.append("## 📚 Table of Contents")
    lines.append("")

    for category, _ in CATEGORY_ORDER + [("Other", [])]:
        if category not in by_category:
            continue
        docs = by_category[category]
        lines.append(f"### {category}")
        lines.append("")
        for e in docs:
            link = e.rel_path.as_posix()
            lines.append(f"- [{e.title}]({link})  · {e.status_badge} · {e.word_count} words · modified {e.modified.strftime('%Y-%m-%d')}")
        lines.append("")

    # Decision Log (lightweight)
    lines.append("---")
    lines.append("")
    lines.append("## 🧭 Major Decisions Log")
    lines.append("")
    lines.append("| Date | Decision | Why | Doc |")
    lines.append("|------|----------|-----|-----|")
    lines.append("| 2026-07 | Free strategic placement of buildings | Structures are now targetable → position has strategic value | Building & Economy docs |")
    lines.append("| 2026-07 | Action rows bundle clip + vfxKey + sfxId + delay + bone | One keyword triggers full presentation | Systems / Motion Caster |")
    lines.append("| 2026-07 | Pillager archetype for economy attacks | Readable threat, counterable, cruelty caps | AI & Enemies / Creative |")
    lines.append("| 2026-07 | 45° rotation + circular footprints | Organic village look, zero extra tech cost | Creative Review WO-673 |")
    lines.append("| 2026-07 | Core kit + one leftover starting choice | Teaches greed/safety/comfort on turn one | Onboarding |")
    lines.append("")
    lines.append("---")
    lines.append("")

    # How to use this system
    lines.append("## 🛠️ How to Maintain This Index")
    lines.append("")
    lines.append("1. Drop new markdown files into the appropriate folder under `docs/`.")
    lines.append("2. Run the indexer:")
    lines.append("   ```bash")
    lines.append("   python tools/generate_master_index.py")
    lines.append("   ```")
    lines.append("3. To search across all docs semantically:")
    lines.append("   ```bash")
    lines.append('   python tools/docs/docs_search.py "free placement pillager"')
    lines.append('   python tools/docs/docs_search.py "vfx delay attachBone" --top 5')
    lines.append("   ```")
    lines.append("4. Commit the updated `00_MASTER_INDEX.md`.")
    lines.append("")
    lines.append("### Recommended Folder Layout (optional)")
    lines.append("```")
    lines.append("docs/")
    lines.append("├── 00_MASTER_INDEX.md          ← this file")
    lines.append("├── 01_Architecture/")
    lines.append("├── 02_Systems/")
    lines.append("│   ├── MotionCaster/")
    lines.append("│   └── ActionRegistry/")
    lines.append("├── 03_Building_Economy/")
    lines.append("├── 04_AI_Enemies/")
    lines.append("├── 05_Creative_Reviews/")
    lines.append("├── 06_Work_Orders/")
    lines.append("├── 07_Onboarding/")
    lines.append("└── Archive/")
    lines.append("```")
    lines.append("")
    lines.append("You do **not** have to reorganize existing files immediately — the indexer works with any layout.")
    lines.append("")
    lines.append("---")
    lines.append("")
    lines.append("*Generated by `generate_master_index.py` — Defenders of the Realm*")

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main() -> int:
    parser = argparse.ArgumentParser(description="Generate docs/00_MASTER_INDEX.md")
    parser.add_argument("--docs-path", default=DEFAULT_DOCS_PATH, help="Path to docs folder")
    parser.add_argument("--dry-run", action="store_true", help="Print result instead of writing")
    parser.add_argument("--output", default=None, help="Override output path")
    args = parser.parse_args()

    docs_root = Path(args.docs_path).resolve()
    if not docs_root.exists():
        print(f"❌ Docs path does not exist: {docs_root}", file=sys.stderr)
        print("   Create a docs/ folder or pass --docs-path", file=sys.stderr)
        return 1

    print(f"🔍 Scanning {docs_root} ...")
    entries = scan_docs(docs_root)
    print(f"   Found {len(entries)} markdown files")

    content = build_master_index(entries, docs_root)

    out_path = Path(args.output) if args.output else docs_root / MASTER_FILENAME

    if args.dry_run:
        print("\n" + "=" * 60)
        print(content)
        print("=" * 60)
        return 0

    out_path.write_text(content, encoding="utf-8")
    print(f"✅ Wrote {out_path}")
    print(f"   {len(entries)} documents indexed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
