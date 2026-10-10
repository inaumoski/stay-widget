# Local development and M1–M5 local verification

M1–M4 are implemented and M5 is locally prepared locally on .NET SDK 10.0.401, EF Core 10.0.12 and Npgsql EF provider 10.0.3. NuGet sources are explicitly restricted to nuget.org by this repository. Supported packages are pinned centrally; recheck servicing updates deliberately.

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

Migration is explicit: ordinary application startup never migrates. `InitialInventory` creates tenant-consistent foreign keys, request/confirmation uniqueness and the GiST per-unit range-overlap exclusion constraint with `btree_gist`. PostgreSQL-specific constraints are intentionally authored in that migration's SQL. Review them as well as the generated EF snapshot. `OwnerInventoryLifecycle` backfills M1 publication/unit rules and guest snapshots, adds nullable manual reservation/block links and tenant FKs, and replaces the exclusion with an active-allocation constraint. Apply it explicitly after a private database backup. Its downgrade refuses manual/cancelled/inactive M2 data; do not assume an M1 binary can run on the M2 schema. Use a reviewed pre-M2 database restore for rollback when needed.

## Common sign-in and local fixtures

The owner authorized a distinct local Funtoolio client. `staywidget-local` uses `https://localhost:7259/signin-funtoolio` and separate randomly generated credentials, stored only in the auth application's existing user-secrets and StayWidget's ignored local settings. `tools/register-local-auth.py` adds that client while preserving other settings. It is a local setup operation, not a production provisioning script; do not run it against unrelated installations. Restarting the local issuer applies its existing registration procedure. This task restarted the confirmed local issuer process and its log reported registration of this client, with no existing-client update message and no auth migration or source-code change.

The application challenges common Funtoolio sign-in using code/PKCE, state/nonce, only openid/profile scopes and the Google authentication requirement. The issuer owns Google; StayWidget does not request Gmail permissions or store direct Google credentials. Validated issuer/subject maps to a local user; email/display-name matches do not merge identities. Sign out uses an antiforgery-protected POST, clears StayWidget's session and, when common authentication is configured, redirects through the issuer's OIDC end-session endpoint. The registered /signout-callback-oidc callback finishes on the public homepage as a GET without automatically challenging sign-in. Tokens remain unsaved; logout sends the registered client ID. Non-widget pages allow only self and the validated configured HTTPS issuer origin in CSP form-action so Chromium can follow the logout redirect; embedded guest/demo pages retain self-only form-action. This clears the issuer browser session, not other products' existing cookies, the Google account session or any account data. With common authentication disabled, logout remains local and ends on the same homepage.

For repeatable disposable testing, explicitly enabled Development plus loopback access permits **Use Alex** / **Use Sam** at `/SignIn?failed=true`. Each gets its own local identity. That option is never available in Production or from a non-loopback request. The database helper enables it in private development settings; default tracked configuration disables it. It is not proof of Google identity. Production requires configured common sign-in and an explicit persistent data-key directory.

The real local issuer challenge has been checked through to its Google sign-in page. A complete user-performed Google callback/account/session flow is still unverified; no external Google login was automated. Public hostname and production registration remain future launch decisions.

## Tests and evidence

```powershell
python tools/run-tests.py
& $stayDotnet ef migrations has-pending-model-changes --configuration Release --project src/StayWidget.Persistence --startup-project src/StayWidget.Web -- --environment Development
```

The test runner reads only this project's ignored local credentials and passes them privately to the fixture. The fixture accepts only the dedicated loopback port/user, creates a random `staywidget_test_*` database, applies the real migration and drops only that named database after completion. It never runs against the preview database or a sibling/production database. Chromium from the already-installed Playwright browser set is used; install the matching browser separately on a fresh workstation if needed.

Verification on 2026-10-10: **79 passing tests**, no skipped tests, Release build without warnings, and EF reported no pending model changes. Tests exercise calendar/DST boundaries, capacity/deadlines, identical and changed submission retries, competing confirmation, database overlap protection even when application checks are bypassed, tenant/identity separation, secret status lookups, monetary/revision guards and the mobile end-to-end browser flow. Guest browser context remained cookie-free; Sam could not see Alex's request data. Owner confirmation rejected malformed monetary input and missing antiforgery protection. Owner/status page framing and no-store headers were checked. Screenshot review found readable desktop and 390px mobile layouts; the browser checked overflow at 390px and at 320px with doubled body text and an unbroken 120-character heading. That enlargement is a reflow check, not a claim of manual browser-zoom/device testing. Captures are ignored under `artifacts/qa`. These checks are not a full accessibility audit.

The browser test exposed and verified a correction to availability search: guest contact fields are not required until actual request submission. M2 adds manual bookings/blocks and their edits/cancellation, decline, multi-unit selection, publication/seasons/stay rules, an owner calendar/list and private booking CSV. Tests additionally cover booking/block/edit races, migration preservation, turnover, request snapshot/policy changes, worker lease and retention. M3 additionally verifies isolated cross-origin embeds, loader resizing/message validation, guest withdrawal/cancellation requests, origin policy, capability scope/revocation/expiry and abuse limits. M4 also implements public pages, plan/branding entitlements, private feedback and optional aggregate counters.

M2 limits: one property with at most five units, fixed EUR agreement record, one open window per unit, 1-28-night stays and turnover 0/1. Guest arrivals start tomorrow; owner manual entries may start today, and edits may preserve an existing past arrival. Moving reservations between units, complete account closure/backup deletion coordination remain deferred. Guest withdrawal and cancellation requests are implemented in M3; only the owner can release confirmed inventory. There is no external calendar sync or email. Booking-only CSV is capped explicitly at 10,000 rows; no partial export is produced. Signed form context, honeypot and bounded network/global/property admission controls are present. Transaction pages are noindex and no-store; only published widget routes allow self and owner-attested embedding origins. Owner, hosted request and private status pages remain unframeable. The versioned loader is cached for 300 seconds with revalidation and ETag. This is not ready for live bookings.

Stop the preview with Ctrl+C in its terminal. Stop the dedicated cluster only with its marked path:

```powershell
& '../shop-organizer/.local/pgsql/bin/pg_ctl.exe' -D './.local/pgdata' -m fast -w stop
```

Keep `.local`, `appsettings.Local.json`, database data, keys and guest-link fragments private. The owner authorized source publication to https://github.com/inaumoski/stay-widget; deployment remains outside the current scope.

## M2 worker and preview

The bounded minute worker starts with the app unless `StayWidget:MaintenanceEnabled=false`. It has no email or external jobs. PostgreSQL lease/cursor state prevents duplicate batches across app instances; a failed tick logs only a failure type and retries next minute. Expiry checks remain synchronous for decisions/status even if maintenance is unavailable. Operational job monitoring and readiness/alerting remain M5 work.

See implementation-status.md for the actual retention defaults, tested limits and preview migration/backup evidence. Those defaults do not establish legal retention obligations. No restore rehearsal or account-deletion pipeline is claimed.

Verified design references: [PostgreSQL constraints](https://www.postgresql.org/docs/current/ddl-constraints.html) and [ASP.NET Core hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0). Runtime behavior is covered by this project's tests rather than assumed from the references.

## M3 embed configuration and migration

`StayWidget:PublicOrigin` is the trusted canonical HTTPS origin used in generated snippets and origin checks. Development defaults to `https://localhost:7259`; when using a different local port, set the matching origin explicitly. Production refuses startup without this configuration. It must agree with the reverse proxy's public origin and the selected OIDC callback registration. Request Host headers do not select snippet destinations. No production proxy setup has been performed.

`EmbedAndGuestCapabilities` adds origin settings, validated accent, suspension and separate guest action hashes/revocation. Older status links remain view-only. Back up privately before explicit migration. A 23,108-byte pre-M3 dump exists in the ignored migration-backups directory; the migration is applied locally. Downgrade refuses action-capability/revocation data; plan a reviewed pre-migration database restore for older-schema rollback. No restore rehearsal is claimed.

Open `/Owner/Installation` after local fixture sign-in, publish a unit and acknowledge the exact embedding origins. HTTPS origin only in production; HTTP loopback is a Development-only installation test exception. Nested builder frames need all ancestor origins approved. See [widget installation](widget-installation.md) for supported mechanisms, limits and unresolved real-builder checks.

Tests use disposable HTTPS app ports 6269/6271 and loopback cross-origin host fixtures on 6280/6281. They do not install on real WordPress/Wix sites. Chromium's third-party-cookie-phaseout option and disabled guest storage provide focused cookie-independent evidence, not universal browser compatibility. Captures include `artifacts/qa/embedded-mobile-receipt.png`. No emails or provider actions occur in these fixtures.

## M4/M5 local verification

The Release suite now has 66 passing tests and no skips/warnings. M4 browser checks include public SEO/sitemap exclusions, a nonpersistent synthetic demo, private feedback/Custom persistence and forged-field exclusion, plan-controlled branding, and normal/enlarged-text mobile reflow. Production-mode tests use a synthetic unused issuer and isolated test database; they verify canonical Host, explicit one-hop proxy handling, private readiness and rejection of Development login, not a real Google callback.

The M4 migration explicitly backfills Pro for existing properties and refuses unsafe downgrade once feedback or plan/branding/installation state exists. A private 24,314-byte pre-M4 dump was taken before the local preview migration. All four migrations are applied. A separate synthetic pg_dump/pg_restore rehearsal copied test Data Protection keys and recovered encrypted retry receipts, accepted status and inventory overlap protection in disposable databases. Private rehearsal output is ignored under .local/restore-rehearsal. This does not verify off-server encryption, actual Linux execution or erasure-ledger reapplication.

`dotnet publish src/StayWidget.Web -c Release --no-build --no-restore -o artifacts/release/portable` succeeded; required assets and absence of appsettings.Local.json were checked. The framework-dependent artifact is ignored and contains no production credentials. See [operations](operations.md) and [release readiness](release-readiness.md) for actual launch gates, configuration and the selected staywidget.funtoolie.com origin/contact. AggregateMetrics defaults off; no tracker or provider account is configured.

## Optional availability calendar follow-up — 10 October 2026

The owner requested a guest availability calendar and default-selected display checkbox. Implemented the per-property installation preference, nightly availability endpoint and accessible month table in actual embeds/hosted request pages and the stateless demo. Availability comes from the existing unit season/capacity, confirmed allocations, turnover gaps and manual blocks; pending requests do not hold inventory. Month navigation, date selection, retry/fallback and mobile iframe resizing are verified. Owners can fill calendar-year or November–December season dates with presets and add/remove unavailable-date blocks. One open window per unit and the 365-day guest arrival horizon remain explicit limits.

Latest Release suite: **69 passed, 0 failed, 0 skipped**, no build warnings; EF reports no pending model changes. Tests include private-data/tenant exclusion, concurrent inventory coverage, leap year and exclusive December 31 boundaries, default/on/off settings, owner presets and embedded calendar navigation/failure/retry. The mobile calendar screenshot was visually inspected. Payments, email and external calendar synchronization remain deferred.

A private 29,219-byte pre-calendar dump was taken before applying `20261010172624_WidgetAvailabilityCalendar` only to the marked loopback preview. Existing properties are backfilled with the calendar enabled; downgrade refuses to discard a saved disabled preference. All five migrations are applied locally. The refreshed ignored portable artifact contains 47 files / 9,515,703 bytes, including both new scripts and no private local settings. Source publication is recorded in Git history; no production deployment or external provider action was performed.

## Guest confirmation email preview

The current Release suite has 78 passing tests and no skips/warnings. All six migrations are applied to the privately backed-up preview; the pre-email dump is 29,332 bytes. The new migration preserves old requests with an empty email, adds the encrypted queue and refuses data-discarding downgrade. Tests never enable live Brevo; browser child servers explicitly select Disabled. Email-specific tests use mocks and a private temporary sink, remove only their own preview file, and verify copied-key/database recovery.

Ignored local configuration selects Email:Mode=Sink with an absolute .local/email-sink path outside webroot. Confirming a new widget request with an email writes a private JSON preview through the background worker; no real email is sent. Sink files contain guest data and private capabilities, must remain ignored, and should be removed when no longer needed. Publish defaults Disabled. Do not run sending workers against restored production contacts. [Email setup](email.md) describes Brevo activation, sender, cancellation semantics and status/recovery limits.

## Guest calendar range selection

The latest suite has 79 passing tests. The mobile calendar browser flow checks alternating check-in/check-out, rejected same-day checkout, keyboard selection, departure on an unavailable night, manual-field synchronization and a range across months. Selected nights and In/Out markers are visible in artifacts/qa/calendar-range-mobile.png (ignored). No schema/configuration change is needed. The calendar is a selection aid; complete availability and confirmation still govern inventory.
