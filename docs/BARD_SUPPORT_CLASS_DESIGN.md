# The Wandering Bard — recruitable support class (party-buff songs)

> Owner (2026-05-31): a **traveling bard** as a **support class** — met as a wandering encounter, can be
> **recruited into the party**, whose **songs buff the party** (haste / damage / defense auras). Fills the
> support role the party lacks and welds the encounter system → party → battle together. Creative/design;
> grounded in PARTY_OF_FOUR_STORYLINE, ENCOUNTER_SYSTEM, BATTLE_2D_PARTY_DESIGN, WO-169. No code/bake.

## The concept
A wandering minstrel carrying songs through a mourning realm — very on-tone (a bard who remembers the old
songs of Elarion, plays for the Folk who can still hear). Mechanically: the **support archetype** the
party didn't have (Keeper=caster, Bram=tank, Nessa=ranged, spirit=flex). The bard's songs are **sustained
party buffs** that make the *whole party* stronger — pure support, the FF "white-mage/bard" role.

## How you get the bard — a recruit, not a given
- **Met as a wandering encounter** (ENCOUNTER_SYSTEM, the NPC/recruit type) — you find the bard on the
  road (a traveler at a campfire, playing for no one). A short beat, then the option to **recruit** them.
- Recruiting adds the bard to your **roster of party members** (PARTY_OF_FOUR) — slottable into the party.
  Since the starter party is Keeper + Bram + Nessa + spirit, the bard is a **swap-in support option**
  (per-member control + party selection, WO-169) — bring the bard when you want sustained buffs over a 4th
  damage dealer. (Ties the "fixed starter four + recruitable extras to swap in" model.)
- On-tone recruit hook: the bard knows a song tied to the master / the lost Keepers (a lore thread).

## What the bard DOES — party-buff songs (owner-locked)
Songs are **sustained auras** that buff the party while the bard plays (FF bard / aura model). Each song
is a stance the bard maintains; switching songs is the bard's "turn" choice in the ATB battle:
- **Haste song** — party ATB gauges fill faster / +action speed (the classic bard tempo buff).
- **Battle hymn** — party +damage aura.
- **Ward song** — party +defense / −damage taken aura.
- (Songs scale with the bard's level/talents; only one major song active at a time = a real tactical choice.)
- **Ties the targeting/party system (WO-169):** the bard's song is a party-wide effect, so it reinforces
  the "manage the whole party" gameplay — you're not just attacking, you're choosing which song supports
  the current fight. Per-member control (WO-169) lets you command the bard's song or let AI pick.

> Song kit beyond the three buffs (debuffs, heals) = a creative pass later; owner locked **party-buff
> auras** as the core. Keep it focused: a few strong, switchable songs, not a cluttered list.

## ALSO a pet skill (owner 2026-05-31): pets can learn a lesser "song" buff
The support/song fantasy **also lands as a pet skill** — so it threads through BOTH the party (the bard,
full songs) AND the pet system (a lesser version). Not either/or:
- **Pets can learn a "song"/hum buff skill** — a *lesser* aura than the bard's (e.g. a small haste or
  +damage hum, weaker/shorter than the bard's full song). The **Aether Sprite** especially fits ("the
  Light's Child hums an old tune"). Reuses the existing **pet aura/skill system (WO-58)** + the pet skill
  tree (`PetSkillTreeCatalog`) — it's a new pet skill entry, not a new system.
- **The relationship:** the **bard = the master of song** (full, switchable party buffs, the support
  class); **pets = a taste of it** (one lesser song-buff skill). A player without the bard still gets *some*
  support from a singing pet; recruiting the bard is the full version. One support idea, scaled across two
  systems — and it gives the pet skill tree (TALENT_V2's pet side) a flavorful, useful node.
- Same buff-aura tech underlies both (bard song + pet song share the implementation) — cheap to do both.

## How it reconciles (reuse, don't reinvent)
- **Party member** → PARTY_OF_FOUR roster + WO-169 (per-member control, party selection, the FF battle).
  The bard is a `BattleUnit` like any party member; "songs" are its abilities (sustained-aura type).
- **Recruited via** ENCOUNTER_SYSTEM (the NPC/recruit encounter). One more reason to explore.
- **Songs = party-aura abilities** in the ATB engine (Actions/Targeting already support AllAllies buffs) —
  a new ability *type* (sustained stance), data-authored. Could share the buff-aura tech with pet auras
  (WO-58) so it's not a new system.
- **Talents/gear:** the bard rides the same talent (TALENT_V2) + gear/legendary systems as other members —
  song potency scales with ranks.

## Acceptance criteria (for the eventual WO)
1. The bard is **met as a wandering encounter** and **recruitable** into the party roster.
2. The bard is a **support party member** (a `BattleUnit`) usable in the FF battle, slottable/swappable (WO-169), per-member Player/AI control.
3. The bard's **songs are sustained party-buff auras** (haste / +damage / +defense), one major song active at a time, chosen on the bard's turn.
4. Built on the party (PARTY_OF_FOUR/WO-169), encounter (ENCOUNTER_SYSTEM), and ATB aura systems — no new engine; songs are a data-authored ability type (share pet-aura tech where possible).
5. Rides the talent + gear systems (song potency scales); on-tone recruit/lore hook.
6. **Pets can learn a lesser "song" buff skill** (a weaker aura than the bard's) via the existing pet aura (WO-58) + pet skill tree — shares the bard's buff-aura tech; the Aether Sprite especially fits.

## Open questions for owner / creative
- **Song count / kit** — the 3 buff auras now, or add heal/debuff songs later? (Locked: buffs core; rest = creative.)
- **One song at a time, or stackable?** (Recommend one major active = tactical choice; avoids buff-soup.)
- **Bard's place in the story** — pure recruit, or tied to a questline (the song of the lost Keepers)? (Recommend a light lore hook.)
- **Is the bard the ONLY support recruit, or the first of several recruitable extras?** (Could open the "recruit more party members in the world" thread.)

🤖 Creative/design doc (UI lane). Grounded in PARTY_OF_FOUR_STORYLINE, ENCOUNTER_SYSTEM_DESIGN,
BATTLE_2D_PARTY_DESIGN, WO-169, WO-58 pet aura, TALENT_TREE_V2. No code/scene/bake.
