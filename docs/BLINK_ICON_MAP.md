# Blink Icon Map — Talent Skills

**Pack root:** `Assets/Blink/Art/Icons` (608 PNGs)
**Runtime copies:** `Assets/Resources/Talents/` (Resources.Load path in `iconPath`)
**Machine map:** `Assets/Resources/Data/Canonical/talent-icon-map.json`
**Apply script:** `tools/apply_talent_icon_map.py`

## Pack layout

| Folder | Count | Role |
|--------|------:|------|
| `Classes/Assassin/Brawler` | 20 | class skill set (20 icons) |
| `Classes/Assassin/DemonHunter` | 20 | class skill set (20 icons) |
| `Classes/Assassin/Hunter` | 20 | class skill set (20 icons) |
| `Classes/Assassin/Ranger` | 20 | class skill set (20 icons) |
| `Classes/Assassin/Rogue` | 20 | class skill set (20 icons) |
| `Classes/Elementalist/Arcanist` | 20 | class skill set (20 icons) |
| `Classes/Elementalist/Cryomancer` | 20 | class skill set (20 icons) |
| `Classes/Elementalist/Electromancer` | 20 | class skill set (20 icons) |
| `Classes/Elementalist/Geomancer` | 20 | class skill set (20 icons) |
| `Classes/Elementalist/Pyromancer` | 20 | class skill set (20 icons) |
| `Classes/HolyDarkness/Cultist` | 20 | class skill set (20 icons) |
| `Classes/HolyDarkness/Medium` | 20 | class skill set (20 icons) |
| `Classes/HolyDarkness/Necromancer` | 20 | class skill set (20 icons) |
| `Classes/HolyDarkness/Paladin` | 20 | class skill set (20 icons) |
| `Classes/HolyDarkness/Priest` | 20 | class skill set (20 icons) |
| `Classes/Symbiose/Beastmaster` | 20 | class skill set (20 icons) |
| `Classes/Symbiose/Druid` | 20 | class skill set (20 icons) |
| `Classes/Symbiose/Enchanter` | 20 | class skill set (20 icons) |
| `Classes/Symbiose/Shaman` | 20 | class skill set (20 icons) |
| `Classes/Symbiose/Shapeshifter` | 20 | class skill set (20 icons) |
| `Classes/Warrior/Barbarian` | 20 | class skill set (20 icons) |
| `Classes/Warrior/Berserker` | 20 | class skill set (20 icons) |
| `Classes/Warrior/Deathknight` | 20 | class skill set (20 icons) |
| `Classes/Warrior/Dragonknight` | 20 | class skill set (20 icons) |
| `Classes/Warrior/Guardian` | 20 | class skill set (20 icons) |
| `Emblems` | 25 | class portrait emblem |
| `Extra` | 55 | promo / backgrounds |
| `Extra/Slots` | 28 | class slot frames |

## Class family → hero tree

| Hero | Primary Blink families |
|------|------------------------|
| Knight | Warrior/Guardian, HolyDarkness/Paladin+Priest, Warrior/Barbarian+Berserker |
| Ranger | Assassin/Ranger+Hunter+Rogue, Symbiose/Druid+Beastmaster, Elementalist ice/fire |
| Mage | Elementalist/Arcanist+Pyromancer+Electromancer, HolyDarkness/Cultist (void) |
| Shared | Priest, Paladin, Arcanist, Guardian, Rogue |

## Skill matches

| Skill id | Name | Blink source | Why |
|----------|------|--------------|-----|
| `knight.t1n1` | Iron Resolve | `Classes/Warrior/Guardian/Guardian1.png` | armored shield stance — passive DR |
| `knight.t1n2` | Thunderbolt | `Classes/Elementalist/Electromancer/Electromancer1.png` | lightning bolt ranged |
| `knight.t1n3` | Guardian Stance | `Classes/Warrior/Guardian/Guardian6.png` | shield emblem — block chance |
| `knight.t1n4` | Mending Salve | `Classes/HolyDarkness/Priest/Priest4.png` | holy tablet / heal ritual |
| `knight.t1n5` | Throwing Spear | `Classes/Assassin/Ranger/Ranger8.png` | flying projectile spear/arrow |
| `knight.t2n1` | Shield Slam | `Classes/Warrior/Guardian/Guardian2.png` | spiked shield bash |
| `knight.t2n2` | Emberbrand Throw | `Classes/Elementalist/Pyromancer/Pyromancer1.png` | fire throw / burn |
| `knight.t2n3` | Warden's Roar | `Classes/Warrior/Barbarian/Barbarian3.png` | war cry / taunt energy |
| `knight.t2n4` | Pinning Spear | `Classes/Assassin/Hunter/Hunter8.png` | hunting spear / pin |
| `knight.t2n5` | Bulwark | `Classes/Warrior/Guardian/Guardian10.png` | heavy defense plate |
| `knight.t3n1` | Suppressing Volley | `Classes/Warrior/Guardian/Guardian5.png` | shield bristling with projectiles |
| `knight.t3n2` | Oathmend | `Classes/HolyDarkness/Priest/Priest2.png` | holy mend over time |
| `knight.t3n3` | Legendary Vanguard | `Classes/HolyDarkness/Paladin/Paladin2.png` | gold-lit knight helm — elite tank |
| `knight.t3n4` | Retaliation Surge | `Classes/Warrior/Guardian/Guardian8.png` | broken shield rebound / reflect |
| `knight.t3n5` | Sweeping Cut | `Classes/Warrior/Barbarian/Barbarian1.png` | wide melee arc |
| `knight.t4n1` | Eternal Aegis | `Classes/Warrior/Guardian/Guardian4.png` | party bubble / full invuln |
| `knight.t4n2` | Second Wind | `Classes/HolyDarkness/Priest/Priest1.png` | self restore |
| `knight.t4n3` | Last Stand | `Classes/Warrior/Guardian/Guardian7.png` | kneeling last stand silhouette |
| `knight.t4n4` | Holy Retribution | `Classes/HolyDarkness/Paladin/Paladin5.png` | holy fire retribution |
| `knight.t4n5` | Champion's Combo | `Classes/Warrior/Berserker/Berserker4.png` | flurry / multi-hit |
| `knight.t2n6` | Venombrand | `Classes/Assassin/Rogue/Rogue7.png` | venom / poison on weapons |
| `knight.s1n1` | Provider's Bond | `Classes/Symbiose/Druid/Druid3.png` | growth / harvest bond |
| `knight.s1n2` | Deep Reserves | `Classes/Symbiose/Enchanter/Enchanter5.png` | stockpile / capacity |
| `knight.s2n1` | Master Mason | `Classes/Elementalist/Geomancer/Geomancer2.png` | stone / repair craft |
| `knight.s2n2` | Foreman's Pace | `Classes/Symbiose/Enchanter/Enchanter2.png` | speed craft / haste work |
| `knight.s3n1` | Salvager | `Classes/Symbiose/Enchanter/Enchanter8.png` | reclaim / salvage materials |
| `knight.s4n1` | Bountiful Banners | `Classes/HolyDarkness/Paladin/Paladin8.png` | banner / wave bounty |
| `knight.b1n1` | Keen Ballistics | `Classes/Assassin/Hunter/Hunter2.png` | aimed projectile damage |
| `knight.b2n1` | Farsight Emplacements | `Classes/Assassin/Hunter/Hunter5.png` | range / sight |
| `knight.b2n2` | Hardened Ramparts | `Classes/Warrior/Guardian/Guardian12.png` | wall fortification |
| `knight.b3n1` | Standing Orders | `Classes/Warrior/Dragonknight/Dragonknight3.png` | command / fire rate |
| `knight.b4n1` | Warden of Elarion | `Classes/HolyDarkness/Paladin/Paladin10.png` | village-wide defense aura |
| `ranger.t1n1` | Quick Draw | `Classes/Assassin/Ranger/Ranger4.png` | drawn bow — attack speed |
| `ranger.t1n2` | Hunter's Mark | `Classes/Assassin/Hunter/Hunter1.png` | hunter mark / prey tag |
| `ranger.t1n3` | Tumble Step | `Classes/Assassin/Ranger/Ranger3.png` | diving dodge / tumble |
| `ranger.t1n4` | Nature's Gift | `Classes/Symbiose/Druid/Druid1.png` | nature regen |
| `ranger.t1n5` | Arrow Storm Prep | `Classes/Assassin/Ranger/Ranger2.png` | quiver / multishot prep |
| `ranger.t2n1` | Windstrider Boots | `Classes/Assassin/Rogue/Rogue3.png` | swift feet / move speed |
| `ranger.t2n2` | Venomcraft | `Classes/Assassin/Rogue/Rogue7.png` | poison craft |
| `ranger.t2n3` | Eagle Vision | `Classes/Assassin/Hunter/Hunter4.png` | sight / crit range |
| `ranger.t2n4` | Deep Freeze | `Classes/Elementalist/Cryomancer/Cryomancer2.png` | ice slow arrows |
| `ranger.t2n5` | Shadow Veil | `Classes/Assassin/Rogue/Rogue1.png` | stealth cloak |
| `ranger.t3n1` | Bloodbound Draw | `Classes/HolyDarkness/Priest/Priest6.png` | life return / lifesteal heal |
| `ranger.t3n2` | Emberhead | `Classes/Elementalist/Pyromancer/Pyromancer4.png` | burning arrows |
| `ranger.t3n3` | Leafcloak | `Classes/Symbiose/Druid/Druid5.png` | leaf / nature dodge |
| `ranger.t3n4` | Beast Companion | `Classes/Symbiose/Beastmaster/BeastMaster1.png` | summon wolf companion |
| `ranger.t3n5` | Precision Strike | `Classes/Assassin/Ranger/Ranger1.png` | deadly precision blade/shot |
| `ranger.t4n1` | Storm of Arrows | `Classes/Assassin/Ranger/Ranger10.png` | arrow rain ult |
| `ranger.t4n2` | Windstrider Legend | `Classes/Assassin/Ranger/Ranger12.png` | legendary mobility |
| `ranger.t4n3` | Phantom Hunter | `Classes/Assassin/Ranger/Ranger5.png` | hooded phantom archer |
| `ranger.t4n4` | Nature's Fury | `Classes/Symbiose/Druid/Druid8.png` | nature DoT fury |
| `ranger.t4n5` | Elarion's Arrow | `Classes/Assassin/Ranger/Ranger15.png` | pierce / chain arrow |
| `mage.t1n1` | Arcane Focus | `Classes/Elementalist/Arcanist/Arcanist1.png` | arcane bolt focus |
| `mage.t1n2` | Mana Flow | `Classes/Elementalist/Arcanist/Arcanist5.png` | mana veins / flow |
| `mage.t1n3` | Warded Flesh | `Classes/Elementalist/Arcanist/Arcanist3.png` | arcane ward body |
| `mage.t1n4` | Spellweaver | `Classes/Elementalist/Arcanist/Arcanist2.png` | spell weave / CDR |
| `mage.t1n5` | Rune Binding | `Classes/Elementalist/Arcanist/Arcanist8.png` | rune chain |
| `mage.t2n1` | Aether Surge | `Classes/Elementalist/Electromancer/Electromancer4.png` | surge on kill |
| `mage.t2n2` | Manaweave | `Classes/Elementalist/Arcanist/Arcanist6.png` | draw mana back |
| `mage.t2n3` | Arcane Shield | `Classes/Elementalist/Arcanist/Arcanist4.png` | arcane shell |
| `mage.t2n4` | Flame Mastery | `Classes/Elementalist/Pyromancer/Pyromancer3.png` | fire mastery core |
| `mage.t2n5` | Blink Mastery | `Classes/Elementalist/Arcanist/Arcanist10.png` | blink / teleport weave |
| `mage.t3n1` | Cataclysm Prep | `Classes/Elementalist/Pyromancer/Pyromancer8.png` | meteor prep radius |
| `mage.t3n2` | Spell Echo | `Classes/Elementalist/Arcanist/Arcanist12.png` | double cast echo |
| `mage.t3n3` | Aether Form | `Classes/Elementalist/Arcanist/Arcanist9.png` | aether body / cost cut |
| `mage.t3n4` | Runic Overload | `Classes/Elementalist/Electromancer/Electromancer8.png` | power overload buff |
| `mage.t3n5` | Void Rift | `Classes/HolyDarkness/Cultist/Cultist6.png` | void stun zone |
| `mage.t4n1` | Cataclysm | `Classes/Elementalist/Pyromancer/Pyromancer12.png` | ultimate blast |
| `mage.t4n2` | Aetherweaver Ascension | `Classes/Elementalist/Arcanist/Arcanist15.png` | ascension spell power |
| `mage.t4n3` | Eternal Arcana | `Classes/Elementalist/Arcanist/Arcanist18.png` | permanent arcana |
| `mage.t4n4` | Reality Rift | `Classes/HolyDarkness/Cultist/Cultist10.png` | DoT zone rift |
| `mage.t4n5` | Elarion's Legacy | `Classes/Elementalist/Arcanist/Arcanist20.png` | legacy auto-recast |
| `shared.n1` | Vitality | `Classes/HolyDarkness/Priest/Priest3.png` | max HP vitality |
| `shared.n2` | Resilience | `Classes/Warrior/Guardian/Guardian9.png` | damage reduction |
| `shared.n3` | Wisdom Surge | `Classes/Elementalist/Arcanist/Arcanist7.png` | wisdom / knowledge surge |
| `shared.n4` | Battle Instinct | `Classes/Warrior/Berserker/Berserker2.png` | crit instinct |
| `shared.n5` | Aether Bond | `Classes/Elementalist/Arcanist/Arcanist11.png` | mana bond regen |
| `shared.n6` | Legendary Resolve | `Classes/HolyDarkness/Paladin/Paladin12.png` | revive / resolve |
| `shared.n7` | Swift Recovery | `Classes/HolyDarkness/Priest/Priest8.png` | OOC regen |
| `shared.n8` | Elarion's Blessing | `Classes/HolyDarkness/Paladin/Paladin1.png` | all-stats blessing |
| `shared.n9` | Arcane Bolt | `Classes/Elementalist/Arcanist/Arcanist1.png` | ranged magic dart |
| `shared.n10` | Mend | `Classes/HolyDarkness/Priest/Priest5.png` | self heal skill |
| `shared.n11` | Dash | `Classes/Assassin/Rogue/Rogue4.png` | blink dodge dash |

## Last apply

- Copied: **83** / 83
- Missing sources: none
