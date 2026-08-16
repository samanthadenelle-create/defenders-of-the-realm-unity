#!/usr/bin/env python3
"""
Apply Blink talent icon map → Assets/Resources/Talents/*

Reads Assets/Resources/Data/Canonical/talent-icon-map.json, copies each
selected Blink PNG into the catalog iconPath destination, and writes
docs/BLINK_ICON_MAP.md inventory + match table.

Run from repo root:
  python tools/apply_talent_icon_map.py
"""
from __future__ import annotations

import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BLINK = ROOT / "Assets" / "Blink" / "Art" / "Icons"
MAP_PATH = ROOT / "Assets" / "Resources" / "Data" / "Canonical" / "talent-icon-map.json"
DOC_PATH = ROOT / "docs" / "BLINK_ICON_MAP.md"
RES_TALENTS = ROOT / "Assets" / "Resources" / "Talents"


# ── Skill → best Blink source (relative to Assets/Blink/Art/Icons/) ──────────
# Chosen by theme: shield/defense → Guardian/Paladin; bows → Ranger/Hunter;
# fire → Pyromancer; arcane → Arcanist; heal → Priest; nature → Druid; etc.
# Each destination resource stays under Talents/<hero>/ so iconPath JSON is stable.

SKILL_MAP: list[dict] = [
    # ── KNIGHT (Guardian / Paladin / Barbarian / Priest / Electromancer / Pyromancer)
    {"id": "knight.t1n1", "name": "Iron Resolve", "dst": "knight/knight_01.png",
     "src": "Classes/Warrior/Guardian/Guardian1.png", "why": "armored shield stance — passive DR"},
    {"id": "knight.t1n2", "name": "Thunderbolt", "dst": "knight/knight_02.png",
     "src": "Classes/Elementalist/Electromancer/Electromancer1.png", "why": "lightning bolt ranged"},
    {"id": "knight.t1n3", "name": "Guardian Stance", "dst": "knight/knight_03.png",
     "src": "Classes/Warrior/Guardian/Guardian6.png", "why": "shield emblem — block chance"},
    {"id": "knight.t1n4", "name": "Mending Salve", "dst": "knight/knight_04.png",
     "src": "Classes/HolyDarkness/Priest/Priest4.png", "why": "holy tablet / heal ritual"},
    {"id": "knight.t1n5", "name": "Throwing Spear", "dst": "knight/knight_05.png",
     "src": "Classes/Assassin/Ranger/Ranger8.png", "why": "flying projectile spear/arrow"},
    {"id": "knight.t2n1", "name": "Shield Slam", "dst": "knight/knight_06.png",
     "src": "Classes/Warrior/Guardian/Guardian2.png", "why": "spiked shield bash"},
    {"id": "knight.t2n2", "name": "Emberbrand Throw", "dst": "knight/knight_07.png",
     "src": "Classes/Elementalist/Pyromancer/Pyromancer1.png", "why": "fire throw / burn"},
    {"id": "knight.t2n3", "name": "Warden's Roar", "dst": "knight/knight_08.png",
     "src": "Classes/Warrior/Barbarian/Barbarian3.png", "why": "war cry / taunt energy"},
    {"id": "knight.t2n4", "name": "Pinning Spear", "dst": "knight/knight_09.png",
     "src": "Classes/Assassin/Hunter/Hunter8.png", "why": "hunting spear / pin"},
    {"id": "knight.t2n5", "name": "Bulwark", "dst": "knight/knight_10.png",
     "src": "Classes/Warrior/Guardian/Guardian10.png", "why": "heavy defense plate"},
    {"id": "knight.t3n1", "name": "Suppressing Volley", "dst": "knight/knight_11.png",
     "src": "Classes/Warrior/Guardian/Guardian5.png", "why": "shield bristling with projectiles"},
    {"id": "knight.t3n2", "name": "Oathmend", "dst": "knight/knight_12.png",
     "src": "Classes/HolyDarkness/Priest/Priest2.png", "why": "holy mend over time"},
    {"id": "knight.t3n3", "name": "Legendary Vanguard", "dst": "knight/knight_13.png",
     "src": "Classes/HolyDarkness/Paladin/Paladin2.png", "why": "gold-lit knight helm — elite tank"},
    {"id": "knight.t3n4", "name": "Retaliation Surge", "dst": "knight/knight_14.png",
     "src": "Classes/Warrior/Guardian/Guardian8.png", "why": "broken shield rebound / reflect"},
    {"id": "knight.t3n5", "name": "Sweeping Cut", "dst": "knight/knight_15.png",
     "src": "Classes/Warrior/Barbarian/Barbarian1.png", "why": "wide melee arc"},
    {"id": "knight.t4n1", "name": "Eternal Aegis", "dst": "knight/knight_16.png",
     "src": "Classes/Warrior/Guardian/Guardian4.png", "why": "party bubble / full invuln"},
    {"id": "knight.t4n2", "name": "Second Wind", "dst": "knight/knight_17.png",
     "src": "Classes/HolyDarkness/Priest/Priest1.png", "why": "self restore"},
    {"id": "knight.t4n3", "name": "Last Stand", "dst": "knight/knight_18.png",
     "src": "Classes/Warrior/Guardian/Guardian7.png", "why": "kneeling last stand silhouette"},
    {"id": "knight.t4n4", "name": "Holy Retribution", "dst": "knight/knight_19.png",
     "src": "Classes/HolyDarkness/Paladin/Paladin5.png", "why": "holy fire retribution"},
    {"id": "knight.t4n5", "name": "Champion's Combo", "dst": "knight/knight_20.png",
     "src": "Classes/Warrior/Berserker/Berserker4.png", "why": "flurry / multi-hit"},
    {"id": "knight.t2n6", "name": "Venombrand", "dst": "knight/knight_21.png",
     "src": "Classes/Assassin/Rogue/Rogue7.png", "why": "venom / poison on weapons"},
    # Steward path
    {"id": "knight.s1n1", "name": "Provider's Bond", "dst": "knight/knight_22.png",
     "src": "Classes/Symbiose/Druid/Druid3.png", "why": "growth / harvest bond"},
    {"id": "knight.s1n2", "name": "Deep Reserves", "dst": "knight/knight_23.png",
     "src": "Classes/Symbiose/Enchanter/Enchanter5.png", "why": "stockpile / capacity"},
    {"id": "knight.s2n1", "name": "Master Mason", "dst": "knight/knight_24.png",
     "src": "Classes/Elementalist/Geomancer/Geomancer2.png", "why": "stone / repair craft"},
    {"id": "knight.s2n2", "name": "Foreman's Pace", "dst": "knight/knight_25.png",
     "src": "Classes/Symbiose/Enchanter/Enchanter2.png", "why": "speed craft / haste work"},
    {"id": "knight.s3n1", "name": "Salvager", "dst": "knight/knight_26.png",
     "src": "Classes/Symbiose/Enchanter/Enchanter8.png", "why": "reclaim / salvage materials"},
    {"id": "knight.s4n1", "name": "Bountiful Banners", "dst": "knight/knight_27.png",
     "src": "Classes/HolyDarkness/Paladin/Paladin8.png", "why": "banner / wave bounty"},
    # Bulwark / tower path
    {"id": "knight.b1n1", "name": "Keen Ballistics", "dst": "knight/knight_28.png",
     "src": "Classes/Assassin/Hunter/Hunter2.png", "why": "aimed projectile damage"},
    {"id": "knight.b2n1", "name": "Farsight Emplacements", "dst": "knight/knight_29.png",
     "src": "Classes/Assassin/Hunter/Hunter5.png", "why": "range / sight"},
    {"id": "knight.b2n2", "name": "Hardened Ramparts", "dst": "knight/knight_30.png",
     "src": "Classes/Warrior/Guardian/Guardian12.png", "why": "wall fortification"},
    {"id": "knight.b3n1", "name": "Standing Orders", "dst": "knight/knight_31.png",
     "src": "Classes/Warrior/Dragonknight/Dragonknight3.png", "why": "command / fire rate"},
    {"id": "knight.b4n1", "name": "Warden of Elarion", "dst": "knight/knight_32.png",
     "src": "Classes/HolyDarkness/Paladin/Paladin10.png", "why": "village-wide defense aura"},

    # ── RANGER
    {"id": "ranger.t1n1", "name": "Quick Draw", "dst": "ranger/ranger_01.png",
     "src": "Classes/Assassin/Ranger/Ranger4.png", "why": "drawn bow — attack speed"},
    {"id": "ranger.t1n2", "name": "Hunter's Mark", "dst": "ranger/ranger_02.png",
     "src": "Classes/Assassin/Hunter/Hunter1.png", "why": "hunter mark / prey tag"},
    {"id": "ranger.t1n3", "name": "Tumble Step", "dst": "ranger/ranger_03.png",
     "src": "Classes/Assassin/Ranger/Ranger3.png", "why": "diving dodge / tumble"},
    {"id": "ranger.t1n4", "name": "Nature's Gift", "dst": "ranger/ranger_04.png",
     "src": "Classes/Symbiose/Druid/Druid1.png", "why": "nature regen"},
    {"id": "ranger.t1n5", "name": "Arrow Storm Prep", "dst": "ranger/ranger_05.png",
     "src": "Classes/Assassin/Ranger/Ranger2.png", "why": "quiver / multishot prep"},
    {"id": "ranger.t2n1", "name": "Windstrider Boots", "dst": "ranger/ranger_06.png",
     "src": "Classes/Assassin/Rogue/Rogue3.png", "why": "swift feet / move speed"},
    {"id": "ranger.t2n2", "name": "Venomcraft", "dst": "ranger/ranger_07.png",
     "src": "Classes/Assassin/Rogue/Rogue6.png",
     "why": "blade dripping venom into a cauldron — literal poison CRAFT; re-tagged off Rogue7 (WO-1023: Rogue7 stays with knight.t2n6 Venombrand; duplicate icon = recognition failure, and the horizontal blade+cauldron silhouette is distinct from Rogue7's fist-gripped dagger in greyscale)"},
    {"id": "ranger.t2n3", "name": "Eagle Vision", "dst": "ranger/ranger_08.png",
     "src": "Classes/Assassin/Hunter/Hunter4.png", "why": "sight / crit range"},
    {"id": "ranger.t2n4", "name": "Deep Freeze", "dst": "ranger/ranger_09.png",
     "src": "Classes/Elementalist/Cryomancer/Cryomancer2.png", "why": "ice slow arrows"},
    {"id": "ranger.t2n5", "name": "Shadow Veil", "dst": "ranger/ranger_10.png",
     "src": "Classes/Assassin/Rogue/Rogue1.png", "why": "stealth cloak"},
    {"id": "ranger.t3n1", "name": "Bloodbound Draw", "dst": "ranger/ranger_11.png",
     "src": "Classes/HolyDarkness/Priest/Priest6.png", "why": "life return / lifesteal heal"},
    {"id": "ranger.t3n2", "name": "Emberhead", "dst": "ranger/ranger_12.png",
     "src": "Classes/Elementalist/Pyromancer/Pyromancer4.png", "why": "burning arrows"},
    {"id": "ranger.t3n3", "name": "Leafcloak", "dst": "ranger/ranger_13.png",
     "src": "Classes/Symbiose/Druid/Druid5.png", "why": "leaf / nature dodge"},
    {"id": "ranger.t3n4", "name": "Beast Companion", "dst": "ranger/ranger_14.png",
     "src": "Classes/Symbiose/Beastmaster/BeastMaster1.png", "why": "summon wolf companion"},
    {"id": "ranger.t3n5", "name": "Precision Strike", "dst": "ranger/ranger_15.png",
     "src": "Classes/Assassin/Ranger/Ranger1.png", "why": "deadly precision blade/shot"},
    {"id": "ranger.t4n1", "name": "Storm of Arrows", "dst": "ranger/ranger_16.png",
     "src": "Classes/Assassin/Ranger/Ranger10.png", "why": "arrow rain ult"},
    {"id": "ranger.t4n2", "name": "Windstrider Legend", "dst": "ranger/ranger_17.png",
     "src": "Classes/Assassin/Ranger/Ranger12.png", "why": "legendary mobility"},
    {"id": "ranger.t4n3", "name": "Phantom Hunter", "dst": "ranger/ranger_18.png",
     "src": "Classes/Assassin/Ranger/Ranger5.png", "why": "hooded phantom archer"},
    {"id": "ranger.t4n4", "name": "Nature's Fury", "dst": "ranger/ranger_19.png",
     "src": "Classes/Symbiose/Druid/Druid8.png", "why": "nature DoT fury"},
    {"id": "ranger.t4n5", "name": "Elarion's Arrow", "dst": "ranger/ranger_20.png",
     "src": "Classes/Assassin/Ranger/Ranger15.png", "why": "pierce / chain arrow"},

    # ── MAGE (wizard folder in Resources)
    {"id": "mage.t1n1", "name": "Arcane Focus", "dst": "wizard/wizard_01.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist1.png", "why": "arcane bolt focus"},
    {"id": "mage.t1n2", "name": "Mana Flow", "dst": "wizard/wizard_02.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist5.png", "why": "mana veins / flow"},
    {"id": "mage.t1n3", "name": "Warded Flesh", "dst": "wizard/wizard_03.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist3.png", "why": "arcane ward body"},
    {"id": "mage.t1n4", "name": "Spellweaver", "dst": "wizard/wizard_04.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist2.png", "why": "spell weave / CDR"},
    {"id": "mage.t1n5", "name": "Rune Binding", "dst": "wizard/wizard_05.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist8.png", "why": "rune chain"},
    {"id": "mage.t2n1", "name": "Aether Surge", "dst": "wizard/wizard_06.png",
     "src": "Classes/Elementalist/Electromancer/Electromancer4.png", "why": "surge on kill"},
    {"id": "mage.t2n2", "name": "Manaweave", "dst": "wizard/wizard_07.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist6.png", "why": "draw mana back"},
    {"id": "mage.t2n3", "name": "Arcane Shield", "dst": "wizard/wizard_08.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist4.png", "why": "arcane shell"},
    {"id": "mage.t2n4", "name": "Flame Mastery", "dst": "wizard/wizard_09.png",
     "src": "Classes/Elementalist/Pyromancer/Pyromancer3.png", "why": "fire mastery core"},
    {"id": "mage.t2n5", "name": "Blink Mastery", "dst": "wizard/wizard_10.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist10.png", "why": "blink / teleport weave"},
    {"id": "mage.t3n1", "name": "Cataclysm Prep", "dst": "wizard/wizard_11.png",
     "src": "Classes/Elementalist/Pyromancer/Pyromancer8.png", "why": "meteor prep radius"},
    {"id": "mage.t3n2", "name": "Spell Echo", "dst": "wizard/wizard_12.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist12.png", "why": "double cast echo"},
    {"id": "mage.t3n3", "name": "Aether Form", "dst": "wizard/wizard_13.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist9.png", "why": "aether body / cost cut"},
    {"id": "mage.t3n4", "name": "Runic Overload", "dst": "wizard/wizard_14.png",
     "src": "Classes/Elementalist/Electromancer/Electromancer8.png", "why": "power overload buff"},
    {"id": "mage.t3n5", "name": "Void Rift", "dst": "wizard/wizard_15.png",
     "src": "Classes/HolyDarkness/Cultist/Cultist6.png", "why": "void stun zone"},
    {"id": "mage.t4n1", "name": "Cataclysm", "dst": "wizard/wizard_16.png",
     "src": "Classes/Elementalist/Pyromancer/Pyromancer12.png", "why": "ultimate blast"},
    {"id": "mage.t4n2", "name": "Aetherweaver Ascension", "dst": "wizard/wizard_17.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist15.png", "why": "ascension spell power"},
    {"id": "mage.t4n3", "name": "Eternal Arcana", "dst": "wizard/wizard_18.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist18.png", "why": "permanent arcana"},
    {"id": "mage.t4n4", "name": "Reality Rift", "dst": "wizard/wizard_19.png",
     "src": "Classes/HolyDarkness/Cultist/Cultist10.png", "why": "DoT zone rift"},
    {"id": "mage.t4n5", "name": "Elarion's Legacy", "dst": "wizard/wizard_20.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist20.png", "why": "legacy auto-recast"},

    # ── SHARED
    {"id": "shared.n1", "name": "Vitality", "dst": "shared/shared_01.png",
     "src": "Classes/HolyDarkness/Priest/Priest3.png", "why": "max HP vitality"},
    {"id": "shared.n2", "name": "Resilience", "dst": "shared/shared_02.png",
     "src": "Classes/Warrior/Guardian/Guardian9.png", "why": "damage reduction"},
    {"id": "shared.n3", "name": "Wisdom Surge", "dst": "shared/shared_03.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist7.png", "why": "wisdom / knowledge surge"},
    {"id": "shared.n4", "name": "Battle Instinct", "dst": "shared/shared_04.png",
     "src": "Classes/Warrior/Berserker/Berserker2.png", "why": "crit instinct"},
    {"id": "shared.n5", "name": "Aether Bond", "dst": "shared/shared_05.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist11.png", "why": "mana bond regen"},
    {"id": "shared.n6", "name": "Legendary Resolve", "dst": "shared/shared_06.png",
     "src": "Classes/HolyDarkness/Paladin/Paladin12.png", "why": "revive / resolve"},
    {"id": "shared.n7", "name": "Swift Recovery", "dst": "shared/shared_07.png",
     "src": "Classes/HolyDarkness/Priest/Priest8.png", "why": "OOC regen"},
    {"id": "shared.n8", "name": "Elarion's Blessing", "dst": "shared/shared_08.png",
     "src": "Classes/HolyDarkness/Paladin/Paladin1.png", "why": "all-stats blessing"},
    {"id": "shared.n9", "name": "Arcane Bolt", "dst": "shared/shared_09.png",
     "src": "Classes/Elementalist/Arcanist/Arcanist17.png",
     "why": "three streaking bolt projectiles — reads ranged magic dart; re-tagged off Arcanist1 (WO-1023: Arcanist1 stays with mage.t1n1 Arcane Focus; the triple-comet silhouette is distinct from Arcanist1's single braided streak in greyscale)"},
    {"id": "shared.n10", "name": "Mend", "dst": "shared/shared_10.png",
     "src": "Classes/HolyDarkness/Priest/Priest5.png", "why": "self heal skill"},
    {"id": "shared.n11", "name": "Dash", "dst": "shared/shared_11.png",
     "src": "Classes/Assassin/Rogue/Rogue4.png", "why": "blink dodge dash"},
]


def inventory_tree() -> list[tuple[str, int]]:
    rows = []
    for d in sorted(BLINK.rglob("*")):
        if not d.is_dir():
            continue
        n = len(list(d.glob("*.png")))
        if n:
            rel = d.relative_to(BLINK).as_posix()
            rows.append((rel, n))
    return rows


def apply_copies() -> tuple[int, list[str]]:
    ok = 0
    missing = []
    for row in SKILL_MAP:
        src = BLINK / row["src"]
        dst = RES_TALENTS / row["dst"]
        if not src.is_file():
            missing.append(row["src"])
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        ok += 1
    return ok, missing


def write_map_json() -> None:
    payload = {
        "version": 1,
        "sourceRoot": "Assets/Blink/Art/Icons",
        "destRoot": "Assets/Resources/Talents",
        "note": "iconPath in hero-talents.json stays Talents/<folder>/<file> (no .png). Re-run tools/apply_talent_icon_map.py after remapping.",
        "skills": [
            {
                "id": r["id"],
                "name": r["name"],
                "iconPath": "Talents/" + r["dst"].replace("\\", "/").rsplit(".", 1)[0],
                "blinkSource": r["src"],
                "why": r["why"],
            }
            for r in SKILL_MAP
        ],
    }
    MAP_PATH.parent.mkdir(parents=True, exist_ok=True)
    MAP_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def write_doc(copied: int, missing: list[str]) -> None:
    inv = inventory_tree()
    total = sum(n for _, n in inv)
    lines = [
        "# Blink Icon Map — Talent Skills",
        "",
        f"**Pack root:** `Assets/Blink/Art/Icons` ({total} PNGs)",
        f"**Runtime copies:** `Assets/Resources/Talents/` (Resources.Load path in `iconPath`)",
        f"**Machine map:** `Assets/Resources/Data/Canonical/talent-icon-map.json`",
        f"**Apply script:** `tools/apply_talent_icon_map.py`",
        "",
        "## Pack layout",
        "",
        "| Folder | Count | Role |",
        "|--------|------:|------|",
    ]
    for rel, n in inv:
        role = (
            "class skill set (20 icons)" if rel.startswith("Classes/") else
            "class portrait emblem" if rel == "Emblems" else
            "promo / backgrounds" if rel == "Extra" else
            "class slot frames" if "Slots" in rel else
            "misc"
        )
        lines.append(f"| `{rel}` | {n} | {role} |")

    lines += [
        "",
        "## Class family → hero tree",
        "",
        "| Hero | Primary Blink families |",
        "|------|------------------------|",
        "| Knight | Warrior/Guardian, HolyDarkness/Paladin+Priest, Warrior/Barbarian+Berserker |",
        "| Ranger | Assassin/Ranger+Hunter+Rogue, Symbiose/Druid+Beastmaster, Elementalist ice/fire |",
        "| Mage | Elementalist/Arcanist+Pyromancer+Electromancer, HolyDarkness/Cultist (void) |",
        "| Shared | Priest, Paladin, Arcanist, Guardian, Rogue |",
        "",
        "## Skill matches",
        "",
        "| Skill id | Name | Blink source | Why |",
        "|----------|------|--------------|-----|",
    ]
    for r in SKILL_MAP:
        lines.append(f"| `{r['id']}` | {r['name']} | `{r['src']}` | {r['why']} |")

    lines += [
        "",
        f"## Last apply",
        "",
        f"- Copied: **{copied}** / {len(SKILL_MAP)}",
    ]
    if missing:
        lines.append(f"- Missing sources ({len(missing)}):")
        for m in missing:
            lines.append(f"  - `{m}`")
    else:
        lines.append("- Missing sources: none")
    lines.append("")
    DOC_PATH.parent.mkdir(parents=True, exist_ok=True)
    DOC_PATH.write_text("\n".join(lines), encoding="utf-8")


def sync_shared_icon_paths_in_catalog() -> None:
    """Ensure shared.n9/n10/n11 point at unique shared_09/10/11 files we just created."""
    cat = ROOT / "Assets/Resources/Data/Canonical/hero-talents.json"
    if not cat.is_file():
        return
    data = json.loads(cat.read_text(encoding="utf-8"))
    fixes = {
        "shared.n9": "Talents/shared/shared_09",
        "shared.n10": "Talents/shared/shared_10",
        "shared.n11": "Talents/shared/shared_11",
    }
    changed = False
    for n in data.get("shared") or []:
        i = n.get("id")
        if i in fixes and n.get("iconPath") != fixes[i]:
            n["iconPath"] = fixes[i]
            changed = True
    if changed:
        cat.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
        # dual copy if present
        sa = ROOT / "Assets/StreamingAssets/Data/Canonical/hero-talents.json"
        if sa.is_file():
            sa.write_text(cat.read_text(encoding="utf-8"), encoding="utf-8")
        print("updated shared iconPath for n9-n11")


def main() -> None:
    if not BLINK.is_dir():
        raise SystemExit(f"missing Blink icons at {BLINK}")
    write_map_json()
    copied, missing = apply_copies()
    write_doc(copied, missing)
    sync_shared_icon_paths_in_catalog()
    print(f"copied {copied}/{len(SKILL_MAP)} icons")
    if missing:
        print("MISSING:")
        for m in missing:
            print(" ", m)
    print("map ->", MAP_PATH.relative_to(ROOT))
    print("doc ->", DOC_PATH.relative_to(ROOT))


if __name__ == "__main__":
    main()
