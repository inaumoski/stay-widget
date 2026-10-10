# Release readiness — 10 October 2026

M1–M4 feature work is implemented. M5 local preparation is verified; **StayWidget is not deployed or cleared for live bookings.**

## Verified locally

- Release suite: **66 passed, 0 failed, 0 skipped**, no build warnings. PostgreSQL integration tests and Chromium owner/guest/mobile/cross-origin flows run in isolated disposable databases. No pending EF model changes; all four migrations applied to the separately backed-up preview.
- Request-only inventory: property-row locks, active GiST overlap backstop, unit/date/capacity/season/turnover rules, idempotency, concurrent confirmation and withdrawal, manual inventory/edit/cancellation, retention and export.
- Embed: direct iframe/loader/hosted fallback, validated origin/source/instance/payloads, repeated loader/multiple instances, mobile and JavaScript-disabled submission, denied unapproved ancestors, cookie-independent guest flow and private scoped actions.
- M4: Free/Pro quotas and over-limit preservation, server-side branding, fixed promotion/continued free access, simple private feedback/Custom, aggregate choice/privacy/retry boundaries, crawlable public pages/private exclusions and nonpersistent demo.
- Production-mode local fixture: canonical Host checks, explicit loopback proxy trust, no forwarded Host trust, private readiness, no Development login and authenticated owner boundaries. Synthetic issuer is deliberately unused; this is not Google callback evidence.
- Synthetic PostgreSQL dump/restore to a different disposable database with copied private Data Protection keys recovered encrypted receipts, private accepted status, active inventory, overlap protection and migration history. Private rehearsal evidence is ignored under `.local/restore-rehearsal`.
- Framework-dependent portable publish: 41 files / 9,450,886 bytes, required static assets present, no appsettings.Local.json and DevelopmentLogin disabled. Artifact is local/ignored. Selected Linux runtime execution has not been tested.
- Screenshots: 390px public pricing and 320px enlarged-text reflow inspected, plus prior owner/widget captures. A small Windows HTTPS read sample observed 119.9 MiB application working set and 56.8ms average across 20 sequential pricing reads (50.2ms p95). Not a capacity test or full accessibility audit.

## External gates still open

| Gate | Required evidence before launch |
| --- | --- |
| Origin and TLS | DNS/certificate/reverse proxy for `staywidget.funtoolie.com`, exact Host and HTTPS forwarding, upstream isolation and renewal monitoring |
| Owner authentication | Distinct production Funtoolio client/callback and complete human Google sign-in/logout/session test |
| Important contact | `contact.stay@funtoolie.com` delivery and a real monitored security/privacy/legal process; no automated application email is introduced |
| Operator/policies | Real operator identity/markets, privacy roles/legal bases, reviewed disclosures/owner policy acceptance and documented rights handling |
| Customer installation | Actual published plain HTML pilot and WordPress/Wix checks for the specific plans, scripts, nested ancestors, mobile sizing and private status popups; restrict claims to verified platforms |
| Ecosystem links | Live checks of playbook portfolio destinations; workspace attempts did not establish reachability |
| Selected Linux host | Runtime/packages, PostgreSQL extension/roles, service isolation, proxy/CSP behavior and real resource/concurrency budget |
| Recovery and privacy | Encrypted off-server DB/key/config backups, monitored freshness, Linux restore and deletion/revocation-ledger reapplication; explicit acceptable data-loss window |
| Operations | Tested alert routing, log/disk retention, worker monitoring, incident ownership and owner export/outage procedure |

No deployment target or credentials were supplied. No production, DNS, mailbox, external login, builder account, paid service or infrastructure changes were attempted. Source commit/push is authorized by “continue all”; publication does not satisfy these gates.

The remaining work is release verification and real operator/hosting setup, not another v1 feature milestone. Email (Brevo), iCal, SaaS billing and guest deposits remain optional separately scoped integrations. See [operations](operations.md), [installation](widget-installation.md) and [implementation status](implementation-status.md).
