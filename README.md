# StayWidget

Add simple direct-booking requests to an existing accommodation website.

**M1–M4 implemented; M5 local readiness prepared — 10 October 2026.** This is an early local preview, not ready for live bookings.

StayWidget is intended for a small B&B, guesthouse or holiday-rental business with one property and 1–5 individually identifiable units. Guests request dates anonymously; an owner confirms or declines. A pending request is not a reservation and does not hold inventory.

The implementation uses ASP.NET Core Razor Pages on .NET 10, focused first-party JavaScript and PostgreSQL with EF Core/Npgsql. The local implementation includes one property with up to five individually named units, anonymous requests, private receipt/status links, confirmation/decline, manual bookings and blocks, date edits/cancellation, an owner calendar/list, an optional guest availability calendar (shown by default), private CSV export and bounded expiry/retention maintenance. Database overlap protection covers bookings and blocks together. Common Funtoolio OIDC is connected to a distinct local client; a complete user Google callback remains unverified. Guests need no account or cookies. There is no automated email, accommodation quote, payment or calendar sync.

M3 adds an isolated iframe with an optional resizing JavaScript loader, owner installation settings/snippets, approved framing origins, private guest withdrawal/cancellation-request capabilities and abuse controls. Guests can withdraw pending requests; cancellation of a confirmed reservation still requires the owner. M4 adds public pages, Free/Pro/Custom offers, server-enforced unit/branding entitlements, private owner feedback and optional first-party aggregate counters. M5 adds production guards, health checks, a verified disposable database/key restore and release/incident instructions. Do not use this preview for actual accommodation operations yet.

- [Implementation plan](docs/implementation-plan.md): decisions, inventory rules, access boundaries, operations, tests and bounded implementation milestones.
- [Implementation status](docs/implementation-status.md): actual evidence, current authorization and next action.
- [Local development](docs/local-development.md): setup, common-login configuration, commands and verified limits.
- [Release and operations](docs/operations.md): configuration, migration/rollback, monitoring, feedback review and privacy/recovery procedures.
- [Release readiness](docs/release-readiness.md): actual local evidence and outstanding launch gates.
- [Widget installation](docs/widget-installation.md): snippets, platform limits, private links and troubleshooting.
- [Planning skill](../funtoolio-playbook/skills/funtoolio-plan-small-product/SKILL.md): source workflow and linked portfolio defaults.

The verified first slice is: owner signs in locally and creates one unit → guest requests dates → owner confirms → those dates become unavailable, including under concurrent confirmation. **69 tests pass**, including real PostgreSQL inventory races, migration preservation, retention, scoped guest actions and mobile cookie-free cross-origin embed flows.

Preview: https://localhost:7259/. Common sign-in is available; explicitly enabled loopback development fixtures are at https://localhost:7259/SignIn?failed=true. They do not verify Google identity.

From this folder, after the database helper has prepared the local cluster:

```powershell
python tools/run-tests.py
```

See the local development document for restore, explicit migration and preview commands. Default configuration disables development login; ignored local settings enable it only in Development on loopback.

The selected production origin is `https://staywidget.funtoolie.com`, with important contact `contact.stay@funtoolie.com`. DNS/TLS, contact delivery/monitoring and operator/legal facts remain unverified. The product attribution destination now uses that selected origin; portfolio destinations retain the playbook’s `funtoolio.com` spelling and require live checks. Git repository: https://github.com/inaumoski/stay-widget. M1 was committed and pushed as `7635b4b`; M2–M5 source is included in this delivery; commit/push is authorized by “continue all”, with exact revision recorded in Git history. No production deployment has been made. Independent secrets, database data, keys and screenshots are ignored.
