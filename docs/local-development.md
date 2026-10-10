# Local development and M1 verification

M1 is implemented locally on .NET SDK 10.0.401, EF Core 10.0.12 and Npgsql EF provider 10.0.3. NuGet sources are explicitly restricted to nuget.org by this repository. Supported packages are pinned centrally; recheck servicing updates deliberately.

## Development database

`tools/dev-database.py` creates/starts only this project's marked PostgreSQL cluster in ignored `.local/pgdata`, listening on `127.0.0.1:55439`. It creates `staywidget_dev`, generates independent random credentials and writes ignored `src/StayWidget.Web/appsettings.Local.json`. It uses the already-present portable PostgreSQL binaries at `../shop-organizer/.local/pgsql/bin`; only binaries are reused, not Shopdesk data/settings/credentials. Set `STAYWIDGET_PG_BIN` to an independently installed binary directory on another machine. This development role owns its disposable cluster; production needs separate restricted runtime/migration roles.

From `stay-widget`:

```powershell
$stayDotnet = Join-Path $env:LOCALAPPDATA 'Fantoolie/dotnet/dotnet.exe'
python tools/dev-database.py
& $stayDotnet restore StayWidget.slnx --configfile NuGet.Config
& $stayDotnet tool restore --configfile NuGet.Config
& $stayDotnet run --project src/StayWidget.Web --no-launch-profile -- --environment Development --migrate
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& $stayDotnet run --project src/StayWidget.Web --no-launch-profile --urls https://localhost:7259
```

Migration is explicit: ordinary application startup never migrates. `InitialInventory` creates tenant-consistent foreign keys, request/confirmation uniqueness and the GiST per-unit range-overlap exclusion constraint with `btree_gist`. PostgreSQL-specific constraints are intentionally authored in that migration's SQL. Review them as well as the generated EF snapshot.

## Common sign-in and local fixtures

The owner authorized a distinct local Funtoolio client. `staywidget-local` uses `https://localhost:7259/signin-funtoolio` and separate randomly generated credentials, stored only in the auth application's existing user-secrets and StayWidget's ignored local settings. `tools/register-local-auth.py` adds that client while preserving other settings. It is a local setup operation, not a production provisioning script; do not run it against unrelated installations. Restarting the local issuer applies its existing registration procedure. This task restarted the confirmed local issuer process and its log reported registration of this client, with no existing-client update message and no auth migration or source-code change.

The application challenges common Funtoolio sign-in using code/PKCE, state/nonce, only openid/profile scopes and the Google authentication requirement. The issuer owns Google; StayWidget does not request Gmail permissions or store direct Google credentials. Validated issuer/subject maps to a local user; email/display-name matches do not merge identities. Local logout clears only StayWidget's session; it does not claim global logout or account deletion.

For repeatable disposable testing, explicitly enabled Development plus loopback access permits **Use Alex** / **Use Sam** at `/SignIn?failed=true`. Each gets its own local identity. That option is never available in Production or from a non-loopback request. The database helper enables it in private development settings; default tracked configuration disables it. It is not proof of Google identity. Production requires configured common sign-in and an explicit persistent data-key directory.

The real local issuer challenge has been checked through to its Google sign-in page. A complete user-performed Google callback/account/session flow is still unverified; no external Google login was automated. Public hostname and production registration remain future launch decisions.

## Tests and evidence

```powershell
python tools/run-tests.py
& $stayDotnet ef migrations has-pending-model-changes --project src/StayWidget.Persistence --startup-project src/StayWidget.Web -- --environment Development
```

The test runner reads only this project's ignored local credentials and passes them privately to the fixture. The fixture accepts only the dedicated loopback port/user, creates a random `staywidget_test_*` database, applies the real migration and drops only that named database after completion. It never runs against the preview database or a sibling/production database. Chromium from the already-installed Playwright browser set is used; install the matching browser separately on a fresh workstation if needed.

Verification on 2026-10-10: **16 passing tests**, no skipped tests, Release build without warnings, and EF reported no pending model changes. Tests exercise calendar/DST boundaries, capacity/deadlines, identical and changed submission retries, competing confirmation, database overlap protection even when application checks are bypassed, tenant/identity separation, secret status lookups, monetary/revision guards and the mobile end-to-end browser flow. Guest browser context remained cookie-free; Sam could not see Alex's request data. Owner confirmation rejected malformed monetary input and missing antiforgery protection. Owner/status page framing and no-store headers were checked. Screenshot review found readable desktop and 390px mobile layouts; the browser checked overflow at 390px and at 320px with doubled body text and an unbroken 120-character heading. That enlargement is a reflow check, not a claim of manual browser-zoom/device testing. Captures are ignored under `artifacts/qa`. These checks are not a full accessibility audit.

The browser test exposed and verified a correction to availability search: guest contact fields are not required until actual request submission. The iframe/loader, manual booking tools, rejection/cancellation, calendar UI, entitlement/pricing pages and feedback are later milestones, not reported as implemented.

M1 limitations: one unit per property, fixed EUR agreement record, no expiry sweep (every decision/status checks expiry synchronously), no guest destructive actions, no cancellation/rejection, no external calendar sync, no email, no complete retention/deletion/export operations. Signed form context and a basic IP limiter are present; full public abuse/resource policies and embed-specific headers arrive in M3. All preview pages are noindex, no-store and unframeable; M3 will allow approved framing only on the widget route. This is not ready for live bookings.

Stop the preview with Ctrl+C in its terminal. Stop the dedicated cluster only with its marked path:

```powershell
& '../shop-organizer/.local/pgsql/bin/pg_ctl.exe' -D './.local/pgdata' -m fast -w stop
```

Keep `.local`, `appsettings.Local.json`, database data, keys and guest-link fragments private. The owner authorized source publication to https://github.com/inaumoski/stay-widget; deployment remains outside the current scope.
