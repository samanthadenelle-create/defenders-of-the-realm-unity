# WO-793 — Tree quests route through an NPC + a "correct item" marker symbol

**Status:** SPEC STUB — needs full spec pass (owner-requested feature, NOT a bug)
**Minted:** 2026-07-30 from an owner F8 (verbatim below), classified NEW-FEATURE per docs/TICKET_PIPELINE.md
**Owner F8 (22:47 UTC, Main_Castle_Overworld):** "Shouldnt this quests require a NPC to speak with at
the Tree? Should be denoted by a special symbol letting you know you are selecting the correct item"

**Intent:** quest interactions at the Heart of Elarion should go through a person (an NPC at the Tree),
not a bare world-object tap - and quest-relevant targets need a distinct marker symbol so the player
knows they are selecting the right thing. Screenshot: flag_20260730-223644_06/07.png.

**Notes for the spec pass:** reuse the one Yarn runner + DialogueCommandSink verbs (never a bespoke
panel); the marker is presentation - a reader of quest state, never logic on the object (ARCH §2);
symbol + text, never colour-only (owner is red/green colourblind); candidate seam = the existing
quest system + PoiCalloutSystem for the marker. WWCD applies for the marker idiom (CoC "!" style).
