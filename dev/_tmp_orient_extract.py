import json

path = r"Assets/StreamingAssets/Data/Canonical/structures-catalog.json"
with open(path, encoding="utf-8") as f:
    data = json.load(f)

targets = ["forge", "workshop", "jeweler", "barracks", "armorer"]
by_id = {e["id"]: e for e in data["entries"]}

print("--- exact ids ---")
for t in targets:
    e = by_id.get(t)
    if not e:
        print(f"{t}: NOT FOUND")
        continue
    o = e.get("orientation") or {}
    print(
        f"{t}: displayName={e.get('displayName')!r} "
        f"euler={o.get('euler')} manual={o.get('manual')} corrected={o.get('corrected')}"
    )

print("--- ids containing target tokens ---")
for e in data["entries"]:
    eid = e.get("id", "")
    if any(t in eid for t in targets):
        o = e.get("orientation") or {}
        print(
            f"{eid}: displayName={e.get('displayName')!r} "
            f"euler={o.get('euler')} manual={o.get('manual')} corrected={o.get('corrected')}"
        )
