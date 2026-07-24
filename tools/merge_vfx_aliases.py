# Merge code-key aliases into VfxManualPicks.json so built-in PlayKey call sites
# resolve to owner-picked prefabs after HovlVfxCatalog regenerate.
import json
import os

path = os.path.join("Assets", "Editor", "VfxManualPicks.json")
with open(path, encoding="utf-8") as f:
    data = json.load(f)
rows = {r["key"]: r for r in data["rows"]}


def path_of(key: str) -> str:
    return rows[key]["prefabPath"]


aliases = [
    ("Melee_Impact", path_of("Weaponskillsword_Impact"), False, 1.0),
    ("Melee_Slash", path_of("KnightThrust_Impact"), False, 1.0),
    ("Cleave_Impact", path_of("KnightWeaponskill_Impact"), False, 1.15),
    ("Heal_Cast", path_of("NoneMageHealingCast_Cast"), False, 1.0),
    ("Heal_Aura", path_of("softhealingaura_Aura"), True, 1.0),
    ("Fountain_Heal_Aura", path_of("HealingFountain_Aura"), True, 1.0),
    ("Fireball_Cast", path_of("Fire_Cast"), False, 1.0),
    ("Fireball_Impact", path_of("FireImpact_Impact"), False, 1.0),
    ("Fireball_Projectile", path_of("FireballTower_Projectile"), True, 1.0),
    ("Arcane_Cast", path_of("SimpleCast_Cast"), True, 1.0),
    ("Arcane_Projectile", path_of("ARcaneTower_Projectile"), True, 1.0),
    ("Arcane_Impact", path_of("PP_PlasmaExplosionEffect"), False, 1.0),
    ("Frost_Projectile", path_of("ArcherTower-Ice_Projectile"), True, 1.0),
    ("Frost_Impact", path_of("Freezing_Impact"), False, 1.0),
    ("Thunderbolt_Cast", path_of("ElectricitySpell_Cast"), True, 1.0),
    ("Thunderbolt_Projectile", path_of("ElectricitySpell_Cast"), True, 1.0),
    ("Thunderbolt_Impact", path_of("Electricityimpact_Impact"), False, 1.0),
    ("Spear_Projectile", path_of("RangerTowerBaseProjectile_Projectile"), True, 1.0),
    ("Aegis_Cast", path_of("DefenseUp-Offhand(Shield)_Aura"), True, 1.0),
]

oneshot_keys = {
    "Weaponskillsword_Impact",
    "KnightWeaponskill_Impact",
    "KnightThrust_Impact",
    "FireImpact_Impact",
    "FireballImpact_Impact",
    "Electricityimpact_Impact",
    "DragonFire_Impact",
    "onweaponskillmaybe_Impact",
    "NoneMageHealingCast_Cast",
    "Fire_Cast",
    "PosionCloud_Cast",
    "MageMeoteorAOE_Cast",
    "PP_PlasmaExplosionEffect",
    "PP_SparksEffect",
    "PP_WoodImpacts",
}
for k in oneshot_keys:
    if k in rows:
        rows[k]["isLoop"] = False

for key, prefab, is_loop, scale in aliases:
    rows[key] = {
        "key": key,
        "prefabPath": prefab,
        "isLoop": is_loop,
        "scale": scale,
        "manual": True,
    }

out = {"rows": sorted(rows.values(), key=lambda r: r["key"])}
with open(path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, indent=4)
    f.write("\n")

print(f"VfxManualPicks: {len(out['rows'])} rows (+{len(aliases)} code-key aliases)")
for a in aliases:
    print(f"  alias {a[0]} -> {os.path.basename(a[1])} loop={a[2]}")
