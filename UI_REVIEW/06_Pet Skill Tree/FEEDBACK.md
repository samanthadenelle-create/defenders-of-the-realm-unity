# 06_Pet Skill Tree -- review

screen   : Pet Skill Tree
panelId  : PetSkillTree
frame    : FramePet
template : Pet_Panel.png
delivered: panel_PetSkillTree.png

## Verdict (mark one)
- [ ] PASS
- [ ] FIX

## Notes

## VERDICT (owner, 2026-07-13 night review)
**DESIGN QUESTION, not a style fix:** "I think echo skill tree isn't ever used since they
don't defend - unless we add it as a passive offline type item." The screen's existence is
the issue: echoes never fight, so a combat-shaped tree is dead UI. Direction: convert to
PASSIVE/OFFLINE workforce perks (see WO-709 note) or retire the screen.
