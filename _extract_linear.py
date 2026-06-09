#!/usr/bin/env python3
"""Extract Linear issue data from tool-results JSON/TXT files."""
import json, glob, os, sys
from collections import Counter, defaultdict

BASE = r"C:\Users\Kayden-Laptop\AppData\Roaming\Claude\local-agent-mode-sessions\9fa38a44-bd63-49dc-86c2-036442cfb0f4\bd474dfe-1c13-41dc-bfe8-02995673c9c7\local_b63d9ab5-a4fd-4933-98f3-a09215a536e8\.claude\projects\C--Users-Kayden-Laptop-AppData-Roaming-Claude-local-agent-mode-sessions-9fa38a44-bd63-49dc-86c2-036442cfb0f4-bd474dfe-1c13-41dc-bfe8-02995673c9c7-local-b63d9ab5-a4fd-4933-98f3-a09215a536e8-outputs\619334dd-d078-4c57-8e35-cb97399df605\tool-results"

# Use the Linux mount path if running on Linux
if not os.path.exists(BASE):
    # Try to find files
    import subprocess
    result = subprocess.run(['find', '/sessions', '-path', '*tool-results*', '-name', '*.json'],
                          capture_output=True, text=True, timeout=10)
    files = [f.strip() for f in result.stdout.strip().split('\n') if f.strip()]
    result2 = subprocess.run(['find', '/sessions', '-path', '*tool-results*', '-name', '*.txt'],
                           capture_output=True, text=True, timeout=10)
    files += [f.strip() for f in result2.stdout.strip().split('\n') if f.strip()]
else:
    files = glob.glob(os.path.join(BASE, '*.json')) + glob.glob(os.path.join(BASE, '*.txt'))

all_issues = []
pagination_info = {}

def extract_issues(data):
    """Extract issues from parsed JSON data."""
    global pagination_info
    if isinstance(data, list):
        for item in data:
            if isinstance(item, dict) and 'text' in item:
                try:
                    inner = json.loads(item['text'])
                    extract_issues(inner)
                except:
                    pass
    elif isinstance(data, dict):
        if 'issues' in data:
            all_issues.extend(data['issues'])
            pagination_info = {
                'hasNextPage': data.get('hasNextPage', False),
                'endCursor': data.get('endCursor')
            }

for f in files:
    if not f:
        continue
    try:
        raw = open(f, 'r', encoding='utf-8').read()
        # Try direct JSON parse
        try:
            data = json.loads(raw)
            extract_issues(data)
        except json.JSONDecodeError:
            # Try finding JSON in text
            idx = raw.find('{"issues"')
            if idx >= 0:
                # Find matching end
                depth = 0
                for i in range(idx, len(raw)):
                    if raw[i] == '{': depth += 1
                    elif raw[i] == '}': depth -= 1
                    if depth == 0:
                        try:
                            data = json.loads(raw[idx:i+1])
                            extract_issues(data)
                        except:
                            pass
                        break
    except Exception as e:
        print(f"Error processing {f}: {e}", file=sys.stderr)

# Dedup by id
seen = set()
unique = []
for iss in all_issues:
    iid = iss.get('id', '')
    if iid not in seen:
        seen.add(iid)
        unique.append(iss)

# Extract fields
results = []
for iss in unique:
    proj = iss.get('project') or 'none'
    priority = iss.get('priority', {})
    pname = priority.get('name', 'None') if isinstance(priority, dict) else 'None'
    pval = priority.get('value', 0) if isinstance(priority, dict) else 0
    results.append({
        'id': iss.get('id', ''),
        'title': iss.get('title', '')[:50],
        'priority': pname,
        'pval': pval,
        'project': proj,
        'statusType': iss.get('statusType', ''),
        'status': iss.get('status', ''),
        'createdAt': iss.get('createdAt', ''),
        'completedAt': iss.get('completedAt') or '',
    })

# Output as JSON for easy parsing
output = {
    'total': len(results),
    'pagination': pagination_info,
    'issues': results
}
print(json.dumps(output))
