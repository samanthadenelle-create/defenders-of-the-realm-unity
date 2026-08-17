<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-340 — PlayerPrefs migration: migrate legacy pet/party data to GameState

**Status:** READY TO IMPLEMENT

**Depends on:** WO-301 (party roster exists in GameState), WO-297 (pet slots exist)

**Lane:** 7 (Persistence/Backend)

---

## Summary

Old saves used PlayerPrefs blobs for pet unlock tracker and party roster. GameState now owns this data (WO-301, WO-297). This WO **migrates PlayerPrefs → GameState on load** so existing players' pet unlocks and party rosters survive the save-format change.

---

## Files to edit

- `Assets/_Modules/Core/Persistence/GameState.cs`
  - Add method `private void MigratePlayerPrefsToGameState()` called in OnEnable/Awake
  - Read old PlayerPrefs keys: `"pet_unlocked_*"`, `"party_roster_json"`, etc.
  - Deserialize into Pets list + PartyRoster
  - Clear old PlayerPrefs keys after migration (log "Migrated X pets, Y party members")
- No changes to SaveSchema (already done in WO-339)

---

## Acceptance criteria

- [ ] Reads all legacy pet PlayerPrefs keys (check PetUnlockTracker.cs for the key naming scheme)
- [ ] Deserializes pet data into `GameState.Pets` list without crash
- [ ] Reads legacy party roster JSON and populates `GameState.PartyRoster`
- [ ] Logs migration summary (pet count, party count, timestamp)
- [ ] Old PlayerPrefs keys cleared after migration
- [ ] Brace balance check passes
- [ ] Build succeeds
- [ ] No crash on fresh save (PlayerPrefs keys don't exist) — code guards with HasKey()

---

## What NOT to do

- Do NOT edit PetUnlockTracker.cs or legacy code
- Do NOT change the PlayerPrefs schema (read-only legacy format)
- Do NOT create new quests or pet content

---

## Notes

After this lands, PetUnlockTracker.cs and any legacy PlayerPrefs code can be marked obsolete/deprecated (but left in place for a few releases for safety).
