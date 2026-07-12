#!/usr/bin/env python3
"""
Defenders of the Realm – Documentation CLI
Single entry point for Master Index + Semantic Search
"""

import argparse
import sys
from pathlib import Path

# Make sure we can import the sibling modules
sys.path.insert(0, str(Path(__file__).parent))

# Windows cp1252 consoles can't print the scripts' emoji — force UTF-8 stdout.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass

def main():
    parser = argparse.ArgumentParser(
        description="Defenders of the Realm Documentation System",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python tools/docs_cli.py index
  python tools/docs_cli.py search "free placement pillager"
  python tools/docs_cli.py search "vfx delay" --top 5
  python tools/docs_cli.py all
        """
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    # index command
    index_parser = subparsers.add_parser("index", help="Generate / refresh docs/00_MASTER_INDEX.md")
    
    # search command
    search_parser = subparsers.add_parser("search", help="Semantic search across all docs")
    search_parser.add_argument("query", help="Search query")
    search_parser.add_argument("--top", type=int, default=8, help="Number of results (default: 8)")
    search_parser.add_argument("--json", action="store_true", help="Output as JSON")

    # all command
    all_parser = subparsers.add_parser("all", help="Run index + show quick search help")

    args = parser.parse_args()

    # generate_master_index.main() re-parses sys.argv itself — hand it a clean argv
    # so our subcommand word ("index"/"all") doesn't hit its parser.
    if args.command == "index":
        from generate_master_index import main as generate_index
        sys.argv = [sys.argv[0]]
        generate_index()
    elif args.command == "search":
        # docs_search exposes an argv-parsing main(), not a search() function —
        # rebuild argv for it.
        from docs_search import main as search_main
        sys.argv = [sys.argv[0], args.query, "--top", str(args.top)]
        if args.json:
            sys.argv.append("--json")
        search_main()
    elif args.command == "all":
        from generate_master_index import main as generate_index
        print("→ Generating Master Index...\n")
        sys.argv = [sys.argv[0]]
        generate_index()
        print("\n→ Semantic search is ready. Try:")
        print('   python tools/docs_cli.py search "your query here"')
    else:
        parser.print_help()

if __name__ == "__main__":
    main()
