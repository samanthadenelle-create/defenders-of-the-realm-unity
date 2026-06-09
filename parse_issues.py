#!/usr/bin/env python3
"""Parse Linear issues from tool-results JSON files."""
import json, glob, os, sys

RESULTS_DIR = sys.argv[1] if len(sys.argv) > 1 else "."

# Find all list_issues result files
files = sorted(glob.glob(os.path.join(RESULTS_DIR, "*.json"))) + \
        sorted(glob.glob(os.path.join(RESULTS_DIR, "*.txt")))

all_issues = []
has_next = False
cursor = None

for f in files:
    if "list_issues" not in f and "list_issues" not in os.path.basename(f):
        # Try reading anyway
        pass
    try:
        raw = open(f, 'r', encoding='utf-8').read()
        # The file might be a JSON array with a text field containing JSON
        data = json.loads(raw)
        if isinstance(data, list):
            for item in data:
                if isinstance(item, dict) and 'text' in item:
                    inner = json.loads(item['text'])
                    if 'issues' in inner:
                        all_issues.extend(inner['issues'])
                        pi = inner.get('pageInfo', {})
                        has_next = pi.get('hasNextPage', False)
                        cursor = pi.get('endCursor')
        elif isinstance(data, dict) and 'issues' in data:
            all_issues.extend(data['issues'])
            pi = data.get('pageInfo', {})
            has_next = pi.get('hasNextPage', False)
            cursor = pi.get('endCursor')
    except Exception as e:
        # Try as plain text with JSON embedded
        try:
            raw = open(f, 'r', encoding='utf-8').read()
            # Find JSON object
            start = raw.find('{"issues"')
            if start >= 0:
                inner = json.loads(raw[start:])
                all_issues.extend(inner.get('issues', []))
                pi = inner.get('pageInfo', {})
                has_next = pi.get('hasNextPage', False)
                cursor = pi.get('endCursor')
        except:
            pass

# Dedup by id
seen = set()
unique = []
for iss in all_issues:
    iid = iss.get('id', '')
    if iid not in seen:
        seen.add(iid)
        unique.append(iss)

print(f"PARSED: {len(unique)} unique issues from {len(files)} files")
print(f"HAS_NEXT_PAGE: {has_next}")
if cursor:
    print(f"CURSOR: {cursor}")

# Extract needed fields
results = []
for iss in unique:
    proj = iss.get('project') or 'none'
    priority = iss.get('priority', {})
    pname = priority.get('name', 'None') if isinstance(priority, dict) else 'None'
    pval = priority.get('value', 0) if isinstance(priority, dict) else 0
    results.append({
        'id': iss.get('id', ''),
        'title': (iss.get('title', '')[:50]),
        'priority': pname,
        'pval': pval,
        'project': proj,
        'statusType': iss.get('statusType', ''),
        'status': iss.get('status', ''),
        'createdAt': iss.get('createdAt', ''),
        'completedAt': iss.get('completedAt', ''),
    })

# 1. Counts
from collections import Counter
st_counts = Counter(r['statusType'] for r in results)
total = len(results)
print(f"\n=== 1. COUNTS ===")
print(f"Total: {total}")
for st in ['completed', 'started', 'backlog', 'unstarted', 'canceled', 'duplicate', 'triage']:
    print(f"  {st}: {st_counts.get(st, 0)}")

# 2. Started issues
print(f"\n=== 2. STARTED ISSUES ===")
started = [r for r in results if r['statusType'] == 'started']
started.sort(key=lambda x: x['pval'])
print(f"{'ID':<10} | {'Title':<52} | {'Priority':<10} | {'Project'}")
print('-' * 100)
for r in started:
    print(f"{r['id']:<10} | {r['title']:<52} | {r['priority']:<10} | {r['project']}")

# 3. Backlog + unstarted grouped by project
print(f"\n=== 3. BACKLOG + UNSTARTED BY PROJECT ===")
bu = [r for r in results if r['statusType'] in ('backlog', 'unstarted')]
from collections import defaultdict
by_proj = defaultdict(list)
for r in bu:
    by_proj[r['project']].append(r)

for proj in sorted(by_proj.keys()):
    items = by_proj[proj]
    items.sort(key=lambda x: x['pval'])
    count = len(items)
    print(f"\n--- {proj} ({count} issues) ---")
    print(f"{'ID':<10} | {'Title':<52} | {'Priority':<10} | {'Status Type'}")
    print('-' * 95)
    for r in items[:8]:
        print(f"{r['id']:<10} | {r['title']:<52} | {r['priority']:<10} | {r['statusType']}")
    if count > 8:
        print(f"  +{count - 8} more")

# 4. New issues since 2026-06-03T19:00:00Z
print(f"\n=== 4. NEW ISSUES SINCE 2026-06-03T19:00:00Z ===")
cutoff = "2026-06-03T19:00:00"
new_issues = [r for r in results if r['createdAt'] > cutoff]
new_issues.sort(key=lambda x: x['createdAt'])
if new_issues:
    print(f"{'ID':<10} | {'Title':<52} | {'Priority':<10} | {'Created At'}")
    print('-' * 100)
    for r in new_issues:
        print(f"{r['id']:<10} | {r['title']:<52} | {r['priority']:<10} | {r['createdAt']}")
else:
    print("None found.")

# 5. Completed since 2026-06-03T19:00:00Z
print(f"\n=== 5. COMPLETED SINCE 2026-06-03T19:00:00Z ===")
completed = [r for r in results if r['completedAt'] and r['completedAt'] > cutoff]
completed.sort(key=lambda x: x['completedAt'])
if completed:
    print(f"{'ID':<10} | {'Title':<52} | {'Priority':<10} | {'Completed At'}")
    print('-' * 100)
    for r in completed:
        print(f"{r['id']:<10} | {r['title']:<52} | {r['priority']:<10} | {r['completedAt']}")
else:
    print("None found.")
