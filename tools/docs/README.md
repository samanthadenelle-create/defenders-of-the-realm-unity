# Defenders of the Realm – Documentation System

**Single Source of Truth + Semantic Search**

## Quick Start

```bash
# From your Unity project root
python tools/docs/docs_cli.py index          # Build / refresh the Master Index
python tools/docs/docs_cli.py search "query" # Semantic search
python tools/docs/docs_cli.py all            # Do both
```

## What it does

| Command | Result |
|---------|--------|
| `index` | Creates/updates `docs/00_MASTER_INDEX.md` – the only file you need to open |
| `search "phrase"` | Semantic search across every markdown file |
| `all` | Index + ready-to-use confirmation |

## Files

- `docs_cli.py`          → Unified CLI (use this)
- `generate_master_index.py` → Builds the beautiful master index
- `docs_search.py`       → TF-IDF semantic search engine

## Installation

```bash
./install_to_project.sh /path/to/your/UnityProject
```

Or just drop the three `.py` files into `tools/docs/`.

---

This system was designed so Claude (or you) can maintain a clean, searchable, always-up-to-date documentation set with a single source of truth.
