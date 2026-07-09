# F8 Inbox

Runtime folder for the persistent F8 watcher. The daemon (`f8-watch-daemon.ps1`) writes here on every real capture (F8 flagged / error / softlock).

| File | Purpose |
|------|---------|
| `PING.json` | Monotonic capture counter — agents poll via `f8-check-inbox.ps1` |
| `LATEST_CAPTURE.md` | Most recent capture + auto-harvested `[Flow:*]` context |
| `capture-*.md` | Historical captures |
| `ACK.json` | Last acknowledged seq (after triage) |
| `daemon.pid` | Running daemon process id |

**Start daemon (once per session / login):**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\run-defenders\f8-watch-start.ps1
```

Capture files are gitignored; this README stays in repo.