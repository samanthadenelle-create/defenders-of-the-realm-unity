> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the tree is GREEN - `site/index.html:86` carries the canon tagline, landed in `f329c8d5`. Only the Vercel PROD deploy is outstanding, and a deploy leaves no repo artifact, so this WO can never be closed from git alone - someone must check the live site.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 916 — Marketing site: ship canon tagline to production (Vercel)

**Status:** DONE — owner-confirmed 2026-08-21.

**Status: PARTIAL — tree green, Vercel prod deploy outstanding** (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-07 (CLI / Grok — residual of audit finding #5)  
**Silo:** Site / Ops (no Unity gameplay code)  
**Roles:** CLI or owner with Vercel auth — deploy only after tree is green on the tagline  
**Depends on:** `f329c8d5` restored `site/index.html` tagline to **Echoes of a Forgotten Civilization**  
**Related:** CLAUDE.md §7 canon strings; `canon-strings.json`; live site `https://echoes-of-elarion.vercel.app/`

---

## 0. One-line truth

The **repo** no longer ships the retired “last light of the Heart” line on the marketing page, but **production** may still serve the old HTML until someone runs a verified `vercel --prod` (or project equivalent). A dApp Store reviewer reads the **live** URL, not HEAD.

---

## 1. Grounded state

| Layer | State |
|-------|--------|
| Canon | Tagline = **"Echoes of a Forgotten Civilization"** (retired “Hold the last light” / “last light of the Heart”, 2026-07-24) |
| Repo `site/index.html` | `<p class="tagline">Echoes of a Forgotten Civilization</p>` + comment forbidding the retired family |
| `site/README.md` | May still describe the redesign as “last light of the Heart” marketing — **stale copy; fix in same pass** |
| Live production | **Unknown until curl/fetch after deploy** — never trust CHAIN_DONE / local file alone |

---

## 2. Scope

### Phase A — Preflight (read-only)

1. Confirm HEAD `site/index.html` tagline matches canon-strings / CLAUDE.md §7.  
2. Grep `site/` for retired strings: `last light`, `Hold the last light`, `last light of the Heart`.  
3. Fix any remaining retired marketing copy in `site/` (README, meta description if it still sells the old line).  
4. **Do not** change Unity `productName` / app label (separate owner decision — APK name vs save path).

### Phase B — Deploy production

1. Use the project’s documented site deploy path (prefer `site/` project / existing Vercel project for **echoes-of-elarion**, not the WebGL demo project unless they are the same).  
2. `vercel deploy --prod` (or the script the repo already uses for marketing — do not invent a second chain).  
3. Record `DEPLOY_URL` / production alias.  
4. **Never trust** a script marker alone (`CHAIN_DONE` has lied before — see historical canon).

### Phase C — Verify live

```text
# Intent: fetch production HTML and assert tagline substring
curl -sL https://echoes-of-elarion.vercel.app/ | findstr /C:"Echoes of a Forgotten Civilization"
# Must NOT find retired lines:
curl -sL https://echoes-of-elarion.vercel.app/ | findstr /I "last light"
```

Also open Privacy URL still live: `https://echoes-of-elarion.vercel.app/privacy`.  
If Vercel SSO blocks anonymous fetch, note it and use an authenticated check — SSO must not ship to public reviewers without a public path.

### Phase D — Out of scope

- WebGL game build deploy  
- Firebase / app store listing screenshots  
- Renaming Android package or Unity productName

---

## 3. Acceptance

- [ ] No retired tagline strings under `site/` (except a one-line “retired — do not use” comment if useful).  
- [ ] Production HTML contains **Echoes of a Forgotten Civilization**.  
- [ ] Production HTML does **not** contain “last light of the Heart” / “Hold the last light”.  
- [ ] Deploy URL + timestamp + verifying command output in RESULT.  
- [ ] Privacy page still reachable.

---

## 4. RESULT

`WorkOrders/WORK_ORDER_916_site_canon_tagline_vercel_prod.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `site/index.html:86 green` — prod deploy + live URL check remains. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: 916 is done. ⚠ The 2026-08-21 audit read this as OPEN - STILL VALID (evidence above). Owner review supersedes it; the audit line is kept so the evidence survives a reopen.
