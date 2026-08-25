#!/usr/bin/env python3
"""WO-1202: migrate quests.json fixed-struct rewards -> typed list with XP + placement resources.

Derives XP from type x chainDepth x stageWeight (owner-locked guidance).
Preserves existing crystals/food/magic/item (parity); adds wood/iron by placement.
Writes BOTH canonical copies byte-identically.
"""
from __future__ import annotations

import json
import math
from copy import deepcopy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/Resources/Data/Canonical/quests.json"
DST_STREAM = ROOT / "Assets/StreamingAssets/Data/Canonical/quests.json"

BASE_XP = {"side": 400, "gear": 650, "main": 900, "endgame": 1600}

# Terminal additive resources by quest id (placement). Non-terminals get 40%.
# Existing crystals/food/magic/item from the file are always preserved (parity floor).
PLACEMENT_TERMINAL = {
    "elarion.welcome": {},
    "forgemaster.first-commission": {"iron": 250, "wood": 200},
    "vendor.supply-run": {"food": 20},
    "vendor.forge": {"iron": 400, "wood": 300},
    "vendor.armorer": {"iron": 350},
    "vendor.lumbermill": {"wood": 1000, "food": 20},
    "vendor.granary": {"food": 40},
    "vendor.jeweler": {},
    "vendor.market": {},
    "vendor.inn": {"food": 20},
    "vendor.stable": {"food": 30},
    "vendor.steward": {"wood": 800, "iron": 400},
    "forgemasters_act1": {},
    "forgemasters_act2": {"wood": 400, "iron": 200},
    "forgemasters_act3": {},
    "forgemasters_act4": {},
    "petbond.sproutling": {"food": 20, "wood": 300},
    "petbond.craghound": {},
    "petbond.frostkit": {},
    "petbond.emberpup": {},
    "petbond.mirewing": {},
    "petbond.glimmermoth": {},
    "petbond.stoneback": {"iron": 150},
    "petbond.aetherfox": {},
}

# Rare gear overrides (owner-locked). stageId -> item id
ITEM_OVERRIDE = {
    ("forgemaster.first-commission", "claim-weapon"): "knight_iron",  # keep
    ("vendor.armorer", "hold-the-line"): "armor_knight_common",
    ("forgemasters_act4", "the-choice"): "ring_heartward",
}


def chain_depth(qid: str, by_id: dict) -> int:
    d = 0
    seen = set()
    cur = qid
    while True:
        q = by_id.get(cur)
        if not q:
            break
        req = q.get("requiresQuestId") or ""
        if not req or req in seen:
            break
        seen.add(req)
        d += 1
        cur = req
    return d


def stage_weight(i: int, n: int) -> float:
    if n <= 1:
        return 1.0
    if i == n - 1:
        return 1.0
    if n == 2:
        return 0.35
    # interpolate 0.35 -> 0.55 across non-terminals
    return 0.35 + (0.55 - 0.35) * (i / max(n - 2, 1))


def legacy_from_reward(rw) -> tuple[int, int, int, str]:
    if not isinstance(rw, dict):
        return 0, 0, 0, ""
    return (
        int(rw.get("crystals") or 0),
        int(rw.get("food") or 0),
        int(rw.get("magic") or 0),
        str(rw.get("grantItemId") or ""),
    )


def lines_from_legacy_list(rw) -> tuple[int, int, int, int, int, int, list[str]]:
    """If already migrated, sum typed list."""
    xp = c = w = ir = f = m = 0
    items: list[str] = []
    if not isinstance(rw, list):
        return xp, c, w, ir, f, m, items
    for line in rw:
        if not isinstance(line, dict):
            continue
        kind = (line.get("kind") or "").strip().lower()
        amt = int(line.get("amount") or 0)
        iid = line.get("id") or ""
        if kind == "xp":
            xp += amt
        elif kind == "crystals":
            c += amt
        elif kind == "wood":
            w += amt
        elif kind == "iron":
            ir += amt
        elif kind == "food":
            f += amt
        elif kind == "magic":
            m += amt
        elif kind == "item" and iid:
            items.append(iid)
    return xp, c, w, ir, f, m, items


def build_lines(
    crystals: int,
    food: int,
    magic: int,
    item_id: str,
    xp: int,
    wood: int,
    iron: int,
) -> list[dict]:
    out: list[dict] = []
    if xp > 0:
        out.append({"kind": "xp", "amount": xp})
    if crystals > 0:
        out.append({"kind": "crystals", "amount": crystals})
    if wood > 0:
        out.append({"kind": "wood", "amount": wood})
    if iron > 0:
        out.append({"kind": "iron", "amount": iron})
    if food > 0:
        out.append({"kind": "food", "amount": food})
    if magic > 0:
        out.append({"kind": "magic", "amount": magic})
    if item_id:
        out.append({"kind": "item", "id": item_id})
    return out


def main() -> None:
    data = json.loads(SRC.read_text(encoding="utf-8"))
    quests = data["quests"]
    by_id = {q["id"]: q for q in quests}

    parity_notes = []
    for q in quests:
        qid = q["id"]
        qtype = (q.get("type") or "side").strip().lower()
        base = BASE_XP.get(qtype, 400)
        depth = chain_depth(qid, by_id)
        chain_mult = 1.0 + 0.25 * depth
        stages = q.get("stages") or []
        n = len(stages)
        place = PLACEMENT_TERMINAL.get(qid, {})

        for i, st in enumerate(stages):
            rw = st.get("reward")
            if isinstance(rw, list):
                # re-run safe: pull legacy axes from list
                _, c0, w0, ir0, f0, m0, items0 = lines_from_legacy_list(rw)
                item0 = items0[0] if items0 else ""
                # Prefer ITEM_OVERRIDE / existing
            else:
                c0, f0, m0, item0 = legacy_from_reward(rw)
                w0 = ir0 = 0

            sid = st.get("stageId") or ""
            override_item = ITEM_OVERRIDE.get((qid, sid))
            if override_item:
                item0 = override_item
            # Keep knight_iron if already present and no override cleared it
            if not item0 and isinstance(rw, dict) and rw.get("grantItemId"):
                item0 = rw["grantItemId"]

            sw = stage_weight(i, n)
            xp = int(round(base * chain_mult * sw))
            # Honest Steel terminal-ish single stage must be ~900
            if qid == "forgemasters_act1":
                xp = max(xp, 900)

            # Placement resources: full on terminal, 40% earlier
            scale = 1.0 if i == n - 1 else 0.4
            wood = w0 + int(round(place.get("wood", 0) * scale))
            iron = ir0 + int(round(place.get("iron", 0) * scale))
            # food/crystals/magic: never reduce below legacy
            food = f0 + int(round(place.get("food", 0) * scale))
            crystals = c0  # placement table rarely adds crystals; keep parity
            magic = m0

            # gather-iron / early forge stages: ensure some iron even if place scaled small
            if qid == "forgemaster.first-commission" and sid == "gather-iron":
                iron = max(iron, 200)
                wood = max(wood, 150)

            lines = build_lines(crystals, food, magic, item0, xp, wood, iron)
            # Parity check vs original struct
            if isinstance(rw, dict):
                oc, of, om, oi = legacy_from_reward(rw)
                if crystals < oc or food < of or magic < om:
                    parity_notes.append(f"REDUCE {qid}/{sid}")
                if oi and item0 != oi and (qid, sid) not in ITEM_OVERRIDE:
                    # item changed unexpectedly
                    parity_notes.append(f"ITEM_CHANGE {qid}/{sid} {oi}->{item0}")

            st["reward"] = lines

    data["_comment"] = (
        "WO-1202 typed reward list. XP derived: base(type)*chainDepth*stageWeight. "
        "Resources follow placement; rare items: knight_iron, armor_knight_common, ring_heartward. "
        "Unknown kinds must Fail loud at dispense."
    )
    data["version"] = max(int(data.get("version") or 2), 3)

    text = json.dumps(data, indent=2, ensure_ascii=True) + "\n"
    SRC.write_text(text, encoding="utf-8")
    DST_STREAM.write_text(text, encoding="utf-8")

    # Summary
    total_stages = 0
    with_xp = 0
    empty = 0
    for q in quests:
        for st in q.get("stages") or []:
            total_stages += 1
            rw = st["reward"]
            if not rw:
                empty += 1
            elif any(x.get("kind") == "xp" for x in rw):
                with_xp += 1

    print(f"Wrote {SRC} and {DST_STREAM} ({len(text)} bytes)")
    print(f"stages={total_stages} with_xp={with_xp} empty={empty}")
    print(f"parity_warnings={len(parity_notes)}")
    for n in parity_notes[:20]:
        print(" ", n)

    # Show key quests
    for qid in ("elarion.welcome", "forgemasters_act1", "vendor.armorer", "forgemasters_act4"):
        q = by_id[qid]
        print("===", qid, q.get("title"))
        for st in q["stages"]:
            print(" ", st["stageId"], st["reward"])


if __name__ == "__main__":
    main()
