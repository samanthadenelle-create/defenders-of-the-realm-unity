# Release notes candidate - submission of 2026-09-03

**Status:** DRAFT, awaiting owner approval. Nothing here is pasted into
`publishing/config.yaml` until she signs off.
**Replaces:** the `new_in_version` text at `config.yaml:155-159`, which has been
banner-flagged **STALE since 2026-08-22** and describes none of the work since.
**Build:** `2026.09.03.353999` (Firebase release id 353999)

---

## THE TEXT (paste this into `new_in_version`)

> Structures now show wear from the first scratch, so you can see what needs
> repair before you pay for it. The ground no longer shimmers in town. Talent
> descriptions read in full, and the Hero screen no longer prints every label
> twice. New effects light the Arcane Spire, treasure chests, daily rewards and
> your area spells, and area spells now draw a ring showing exactly where they
> land. A retreat no longer leaves you stuck in a finished battle.

**Character count:** 466. ASCII only - no em dash, no ellipsis character, no
smart quotes. *(The tofu oracle fails on any codepoint above 126, and the
reviewer reads this text on a device that may not carry the glyph.)*

---

## PROVENANCE - every claim, and where it was proven

⭐ **The rule this table exists for:** a listing must not describe a feature the
reviewer cannot reach. Each line below names the change and how it was verified,
so the owner can strike any claim she cannot see on her own device.

| Claim | Change | Verified |
|---|---|---|
| wear from the first scratch | WO-1352 - a scuff rung added below smolder; albedo darkens 12% to 34% across 100%-50% HP in three steps | oracle sweeps 201 HP samples for silence + monotonicity; owner ruled the design |
| see what needs repair before you pay | closes the gap where repair was offered above 0.0001 damage but nothing was visible until 50% HP | her own device toast: "Repaired 1 structures for Wood 35, Iron 7" on a structure showing nothing |
| ground no longer shimmers | the z-fight fixer's scene gate never matched `Main_Castle_Overworld` (underscore), so it had never run in the hub | 35 MB Player.log: zero fixer lines in thousands of hub-scene lines |
| talent descriptions read in full | the description band seated 2 line boxes for a string needing 3, with overflow set to Truncate (draws no ellipsis) | owner screenshot before/after |
| Hero screen labels once | the words were baked into four card PNGs and live text drew on the same plate | agent opened the PNGs; oracle red 15 ways on HEAD |
| Arcane Spire / chests / daily rewards | owner-tagged VFX keys wired verbatim | catalog rows verified present by row, not by exit code |
| area spells draw a ring | the reticle scales from the ability's own radius - the same value the damage sweep uses | frost nova 5.2m and meteor 9.0m proven proportional |
| retreat no longer stuck | `Enemy.OnDisable` now revokes the pursuit pulse; retreat destroys via `Destroy()`, not `Die()` | WO-1337 |

---

## ⚠ CHECK BEFORE PASTING

1. **The purchase sentence.** `config.yaml:151-154` warns that if purchases stay
   OFF in the production build, the purchase claim must come out of
   `new_in_version` AND `long_description` AND `testing_instructions`, or the
   listing declares a feature the reviewer cannot reach. **This candidate makes
   no purchase claim at all**, so it is safe either way - but the OTHER two
   fields still need that check before submitting.
2. **The 404s that caused the rejection are FIXED and verified** - both
   `https://echoes-of-elarion.vercel.app/privacy` and `/terms` return HTTP 200
   with the correct page titles, at the exact URLs `config.yaml` names as
   `privacy_policy_url` and `license_url`.
3. **The privacy policy content is already correct.** Canon claimed it falsely
   states the app has no ads; read at source, `PRIVACY_POLICY.md:85-96` is a
   complete and accurate Advertising section naming Unity LevelPlay, mediation
   partners, user-initiated rewarded ads only, and consent handling. **No legal
   rewrite is needed and the resubmission should not wait for one.**
4. Delete the STALE banner at `config.yaml:146-150` in the same edit that pastes
   this text, so the next reader does not distrust a current note.

---

## What this release deliberately does NOT claim

Honest omissions, so nobody adds them back without evidence:

- **No claim about hero art quality.** The 50k decimation was reverted after the
  owner reported the Mage deformed in motion; heroes ship at full resolution.
- **No claim about download size.** Heroes moved to the CDN earlier (49 MB off
  the APK), but the APK is 457 MB and that is not a headline.
- **No claim about the Night Market rotation.** Its aura is a database knob and
  currently sits on the legacy ring; the owner has not chosen a final look.
- **No claim about the tutorial.** The talent-point beat now completes on a real
  spend, but its wording is still placeholder awaiting her copy.
