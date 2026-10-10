# StayWidget

Add simple direct-booking requests to an existing accommodation website.

**M1 implemented locally — 10 October 2026.** The owner authorized starting implementation after reviewing the plan. This is an early local preview, not ready for live bookings.

StayWidget is intended for a small B&B, guesthouse or holiday-rental business with one property and 1–5 individually identifiable units. Guests request dates anonymously; an owner confirms or declines. A pending request is not a reservation and does not hold inventory.

The implementation uses ASP.NET Core Razor Pages on .NET 10, focused first-party JavaScript and PostgreSQL with EF Core/Npgsql. M1 includes one property/unit setup, anonymous requests, private receipt/status links, owner confirmation and database overlap protection. Common Funtoolio OIDC is connected to a distinct local client; a complete user Google callback remains unverified. Guests need no account or cookies. There is no automated email, accommodation quote, payment or calendar sync.

The loader/iframe follows in M3. Manual bookings, blocking, rejection/cancellation and calendar management follow in M2; do not use this preview for actual accommodation operations yet.

- [Implementation plan](docs/implementation-plan.md): decisions, inventory rules, access boundaries, operations, tests and bounded implementation milestones.
- [Implementation status](docs/implementation-status.md): actual evidence, current authorization and next action.
- [Local development](docs/local-development.md): setup, common-login configuration, commands and verified limits.
- [Planning skill](../funtoolio-playbook/skills/funtoolio-plan-small-product/SKILL.md): source workflow and linked portfolio defaults.

The verified first slice is: owner signs in locally and creates one unit → guest requests dates → owner confirms → those dates become unavailable, including under concurrent confirmation. **16 tests pass**, including real PostgreSQL races and a mobile cookie-free browser flow.

Preview: https://localhost:7259/. Common sign-in is available; explicitly enabled loopback development fixtures are at https://localhost:7259/SignIn?failed=true. They do not verify Google identity.

From this folder, after the database helper has prepared the local cluster:

```powershell
python tools/run-tests.py
```

See the local development document for restore, explicit migration and preview commands. Default configuration disables development login; ignored local settings enable it only in Development on loopback.

The public hostname is undecided. `https://funtoolio.com/staywidget/` and `https://funtoolio.com/` remain provisional attribution destinations. Git repository: https://github.com/inaumoski/stay-widget. The owner authorized committing and pushing M1; no production deployment has been made. Independent secrets, database data, keys and screenshots are ignored.
