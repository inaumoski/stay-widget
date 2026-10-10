# StayWidget release and operations

Prepared 10 October 2026 for M5. **No production deployment or Linux execution has occurred.** Local tests and a disposable restore provide evidence, not approval for live guest intake. Production origin selected by the owner: `https://staywidget.funtoolie.com`. Important contact: `contact.stay@funtoolie.com`; delivery/monitoring unverified. This spelling is intentional and distinct from the playbook's `funtoolio.com` ecosystem destinations.

## Release gates

Before real bookings, record the operator identity/markets, finalize reviewed policies and versioned owner agreement, verify the contact mailbox is monitored, verify actual hosting/DNS/TLS and register a distinct production Funtoolio OIDC client with callback `https://staywidget.funtoolie.com/signin-funtoolio`. No Gmail scopes. Complete a human Google callback/logout/session test; the local challenge alone is insufficient. Verify actual published WordPress/Wix or restrict the advertised pilot to tested platforms.

Confirm off-server encrypted database/key/config backups, a restore rehearsal on the selected Linux environment and a monitored incident route. The local Windows rehearsal is not proof of encrypted off-server recovery, Linux execution, provider uptime or production capacity. Verify external ecosystem destinations before publishing; attempts from this workspace did not establish their reachability. The selected product destination replaces the original provisional `funtoolio.com/staywidget/` attribution URL.

## Proposed host shape and budget

Use one modular monolith, one PostgreSQL database, one private service user and a loopback reverse proxy upstream. A shared Ubuntu VPS is suitable for a small pilot, subject to actual measurements. Start with a 512–768 MiB application memory ceiling and one CPU allowance, then measure peak resident memory, GC, latency and worker duration before setting final limits. PostgreSQL and the reverse proxy need their own budgets; leave RAM for the OS and 6–10 other products. No Redis, Kubernetes or distributed queue is required.

These are proposed ceilings, not benchmark results. Budget 1–2 GiB combined database/backup working space initially and bounded disk/log alerts, then measure growth with actual request volume and retention. PostgreSQL connections are bounded by a proposed runtime connection-string `Maximum Pool Size=20;Timeout=5;Command Timeout=10`. Global request-body limit is 64 KiB, allowing bounded 4,000-character Unicode forms. Core requests have network/global/day safety limits. Monitor queue-free rejections so a shared network doesn't silently strand guests.

Keep coding agents and development tools under a different nonprivileged identity/container with no production keys, database credentials, sockets or release write access. Sharing a VPS is not authorization to grant agents production access.

## Reproducible artifact and configuration

Local portable publish command:

```powershell
$stayDotnet = Join-Path $env:LOCALAPPDATA 'Fantoolie/dotnet/dotnet.exe'
python tools/run-tests.py
& $stayDotnet publish src/StayWidget.Web -c Release --no-build --no-restore -o artifacts/release/portable
```

The framework-dependent artifact needs a supported .NET 10 ASP.NET Core runtime on the selected host. Verify servicing support when deploying. There are no native application workers or frontend build dependencies. Publish output must contain compiled Razor pages, `wwwroot/site.css`, guest scripts and `wwwroot/embed/v1/loader.js`; private local configuration must be absent. Default tracked configuration disables authentication/development login and therefore cannot start in Production without explicit configuration. No secrets belong in the artifact.

Production key names (set through a private service environment/config file; never committed):

| Key | Required behavior |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | Loopback-only upstream, e.g. `http://127.0.0.1:5280` |
| `ConnectionStrings__StayWidget` | Dedicated runtime role/database; restricted permissions, bounded pool/timeouts |
| `StayWidget__PublicOrigin` | `https://staywidget.funtoolie.com` |
| `StayWidget__DataRoot` | Persistent private Data Protection key directory outside releases/wwwroot |
| `StayWidget__BehindLoopbackProxy` | `true` only with a single trusted loopback proxy |
| `StayWidget__DevelopmentLogin` | `false`; Production rejects true |
| `StayWidget__AggregateMetrics` | `false` by default; enable only after privacy/choice review |
| `StayWidget__MaintenanceEnabled` | `true` for bounded inventory/feedback cleanup and counter flush |
| `Authentication__Enabled` | `true` |
| `Authentication__Issuer`, `ClientId`, `ClientSecret` | Verified distinct production common-auth registration |

The app accepts forwarded client IP/scheme from explicit IPv4/IPv6 loopback proxies only, one hop, when enabled. It does not trust forwarded Host. The proxy must overwrite forwarded headers and preserve the canonical Host; the app rejects other production hosts/ports. Do not expose the upstream publicly or clear proxy trust lists. Redirect HTTP at the proxy and serve HTTPS with monitored renewal. Widget pages have their own exact ancestor CSP; owner/admin/private pages remain unframeable.

Proposed systemd service values: nonroot `User=staywidget`, `WorkingDirectory=/srv/staywidget/current`, `ExecStart=/usr/bin/dotnet /srv/staywidget/current/StayWidget.Web.dll`, private `EnvironmentFile=/etc/staywidget/runtime.env`, `Restart=on-failure`, `NoNewPrivileges=true`, `PrivateTmp=true`, `ProtectSystem=strict`, `ProtectHome=true`, `ReadWritePaths=/var/lib/staywidget`. Test restrictions and memory/CPU ceilings on the actual host before applying. Do not copy these into infrastructure under this local task.

This proxy/runtime approach follows [Microsoft proxy guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0) and [Linux hosting guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0); the selected host configuration remains unverified.

## Controlled migration and rollback

1. Build/test the exact revision, hash and inventory the artifact, and verify the target app/database identities. Record migration compatibility and the available previous artifact.
2. Put the proxy into a maintenance response before a schema change; prevent owner/guest writes and stop this app's workers. Never point tests at production.
3. Take a consistent private database dump and back up keys/config with encryption off-server. Check success and restore to an isolated target before relying on it. Retain the verified rollback artifact.
4. Apply reviewed migrations explicitly using a separate migration role and private environment: `dotnet StayWidget.Web.dll --migrate`. Normal startup does not migrate. Runtime needs only its required table DML/sequence access; migration needs extension/schema rights. Verify `btree_gist` availability with the hosting administrator.
5. Switch the release symlink, restart the service, check readiness and run authorized synthetic workflow checks, then remove maintenance. If readiness fails, keep maintenance and preserve diagnostics.

M2/M3/M4 migrations have deliberate downgrade guards when new inventory/capability/feedback/plan state would be lost. Do not force a destructive downgrade. Prefer a compatible forward fix; an older database restore loses all writes since the backup and requires explicit operator acceptance/reconciliation. Schema and binary rollback are separate decisions. Do not erase later guest decisions simply to make an old binary start.

## Health, monitoring and incidents

`/health/live` gives only `live`; it says nothing about inventory or dependencies. `/health/ready` requires a loopback effective client, bounds the database check to three seconds and verifies the latest migration; failure is 503 with no private diagnostics. The proxy should deny external `/health/ready` as another layer. A successful readiness response does not verify email delivery, OIDC availability, backups, legal facts or Google callbacks.

Monitor health, 5xx/429 rates, DB/disk usage, backup freshness, TLS expiry and both bounded minute workers. Workers log failure type rather than submitted text, secrets or private URLs. Inventory deadlines are enforced synchronously even if cleanup is down. Default logging is Warning without sensitive EF data or HTTP body logging. Keep proxy access logs free of query strings/referrers/auth data, disable logs for capability-related routes, and use a reviewed short retention (proposed diagnostics 7 days; justified security records 30 days). Configure journald/log rotation and alerts on the host; none are provisioned locally.

When owners cannot access bookings: the operator acknowledges through the monitored important contact, stops misleading intake when needed, protects the database, diagnoses without guest-data disclosure, restores/repairs under maintenance, reconciles decisions made offline and records the incident. Owners use prior private CSV exports and property contact/calendar methods; exports are sensitive and not continuous synchronization. Never advertise zero support or guaranteed recovery. No synthetic fixture login is allowed in production.

## Backup, recovery and privacy lifecycle

Recommended baseline: encrypted daily off-VM PostgreSQL dumps plus Data Protection keys and private configuration, 30-day rolling retention, restricted access and restore verification. Daily backups imply a provisional maximum 24-hour data-loss window; internal recovery target one working day after operator access, neither a customer guarantee. If losing accepted bookings in that window is unacceptable, add tested WAL/PITR or limit the pilot before launch. Provider snapshots alone are insufficient. Protect key files on disk with service permissions; the local key-copy test does not prove at-rest backup encryption.

Restore to a new isolated database under maintenance. Verify schema, active bookings/blocks, owner/tenant separation and encrypted receipt retry using restored keys. Reapply the separately retained deletion/revocation/suspension ledger **before** serving traffic; reconcile external/manual bookings and notify affected owners through the verified operator channel. Never restore revoked guest access or erased personal data inadvertently. Test export and a conflicting confirmation before returning to service.

Account closure is an operator-coordinated workflow, not an implemented self-service deletion button: verify the requester proportionately; pause intake; offer export and resolve/transition upcoming bookings rather than silently erasing obligations; record a proposed 30-day recovery deadline and any lawful retention exception in a private case record. Coordinate central auth separately. After the deadline, lock property then local user in one transaction, remove allocations → reservations → requests → activity → units → property → local user, with expected identity/revision checks. Feedback cascades with the local-user FK, including private Comment/Response. Keep an encrypted minimal erasure/revocation ledger for backup reapplication; do not retain the removed free text in it. This procedure needs an actual monitored operator and ledger/backup controls before launch. The test proves feedback FK cascade, not a complete automated closure product.

## Private feedback review

No app inbox, list/detail API, reply service or operator UI exists. Use separately authorized database administration and treat all submitted text as untrusted. A bounded read:

```sql
SELECT "Id", "Kind", "Body", "Contact", "Status", "Comment", "Created", "Revision"
FROM "Feedback" ORDER BY "Created" DESC, "Id" LIMIT 50;
```

For edits, begin a transaction, lock the submitter's `Users` row `FOR UPDATE`, reload the candidate and verify its expected revision. Update with `WHERE "Id"=:id AND "Revision"=:expected`, increment Revision, stamp Updated and assert exactly one affected row before commit; otherwise rollback. Do not modify Body, Kind, Contact, SubmissionKey or NoticeVersion. Response is reserved/private and must remain untouched by ordinary review. Record first Reviewed on the first explicit decision, not a read. New cannot be restored after review.

States are New, Reviewed, Planned, InProgress, Closed. Closure needs Closed timestamp and Delivered/Duplicate/NotPlanned/CannotReproduce/Spam reason; reopen to Reviewed clears only closure fields, preserves first review and Comment. Only use Delivered when available to affected users. Constraints enforce valid state/closure shape; cleanup takes the same user lock and rechecks. Closed and unreviewed New rows expire after 12 months in bounded batches; selected ongoing work remains until closed. Submission retries neither reopen nor change managed records. Review periodically; no routine response deadline is promised.

## Aggregate reporting

Optional fixed-event counts have no tenant/user identifiers, URLs, referrers or guest content. Disabled locally/unconfigured by default; DNT/GPC prevent recording when enabled. At most two days × eleven in-memory event keys and bounded daily counts flush once a minute. Flush failure/restart can lose counts; counters are approximate and never the reservation source of truth. Known bots/prefetches are filtered narrowly, not perfectly. Persisted counters expire after 90 days.

Report `page-*` as page requests, `widget-view` as widget responses, `hosted-view` as fallback responses, `request-submitted` as committed-new-request events (not attempts/retries), `installation` as one owner acknowledgement, and `attribution-*` as redirect clicks. Do not infer verified installs, unique visitors, Funtoolio homepage visits or paid conversions. Paid conversions are unavailable; real billing is deferred. A bounded operator query is `SELECT "Day","Event","Count" FROM "Metrics" ORDER BY "Day" DESC,"Event" LIMIT 200;`. No public metrics dashboard or Cloud stream is added.

## Confirmation email operations

Follow [email setup](email.md) for private Brevo configuration, selected noreply@funtoolie.com sender, queue states, quota, recipient contact and cancellation semantics. The worker is independent of reservation correctness. Confirmed dates remain unavailable on any sending failure. Disabled mode is mandatory for recovery fixtures; private development uses Sink. Monitor aged Queued / Failed / Unknown records and contact guests manually when needed. Verify sender/domain authentication, live inbox receipt, provider privacy/tracking and the canonical link before enabling live sends. Email delivery is not proved by health/ready or HTTP 201. No payments are introduced.
