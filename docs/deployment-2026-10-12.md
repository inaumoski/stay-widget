# StayWidget hosting evidence — 2026-10-12

Owner authorized hosting all Funtoolio apps on the Contabo VPS. Public origin: https://staywidget.funtoolio.com/. Active release: `20261012-002`, built from the existing local working tree using .NET SDK 10.0.401. This is a hosted preview, not evidence that every commercial launch prerequisite is complete. Source changes remain local; no commit or push performed in this deployment task. Earlier working-tree changes were preserved.

## Implementation and verification

PostgreSQL. Full test suite: 82 passed. Six migrations explicitly applied to a fresh production database. Public sign-in reaches the common issuer with its registered HTTPS callback. Linux production keys use a persistent encryption certificate. Confirmation email remains disabled pending sender/provider setup, as authorized by the owner.

Ubuntu 24.04, ASP.NET runtime 10.0.12, Caddy HTTPS and systemd with a dedicated nonlogin service identity. PostgreSQL apps use version 16, separate databases and restricted runtime roles; migrations were explicit, not startup operations. Fresh production databases contain no imported local user records. Private configuration, storage and persistent keys are outside public releases. All app ports and database access are loopback-only. Public page availability and exact HTTPS sign-in routing were checked; real Google account login and cross-product human sign-out remain unverified.

## Recovery and remaining dependencies

Daily host backups include all five databases, private configuration/files/keys and active releases. An encrypted off-server snapshot was saved on the operator workstation. All five database dumps restored successfully into isolated disposable databases; complete application/key/session recovery is not yet rehearsed. Recurring off-server transfer, backup pruning, remote alerts and agreed recovery time/data-loss targets remain follow-up work. Five-minute local journal monitoring checks service/readiness, disk and backup age; no external alert delivery configured.

Keep database recovery separate from code rollback: retain compatible release directories and verify schema compatibility before switching; never restore an older database automatically or silently discard newer records. Reboot remains pending after OS updates; existing provider cloud-init/network wait failures require console/recovery review before reboot. Root/password SSH was explicitly preserved. Email delivery, analytics dashboard ingestion, monitored contact details and effective policy review remain unverified. StayWidget email is disabled. No paid account/service was created.

## Subsequent footer update

Active release is now `20261012-005`, deployed through a limited web assembly/symbols and footer stylesheet update over the prior release. The previous release is retained. All five products now have complete ecosystem homepage links, excluding themselves. Focused live browser/layout checks passed; see implementation status. Initial deployment and recovery evidence above remains historical.

## Subsequent SEO release

Active release is now `20261012-006`; see [SEO evidence](seo-2026-10-12.md). Public-copy/sitemap/static-cache updates only; previous releases retained, no database changes. Search Console verification/submission remains owner follow-up.

## Subsequent robots.txt release

Active release is now `20261012-010`; see [robots deployment evidence](robots-2026-10-12.md). Owner-authorized crawl-policy updates only, with previous artifacts retained.

## Canonical URL release — 2026-10-25

Owner-authorized release `20261025-001` is now active, superseding the historical releases above. Limited metadata/sitemap/public-route update; prior release retained, runtime/dependencies/configuration/database preserved. Live sitemap/canonical and public/private smoke checks pass. See implementation-status.md for actual scope and verification. No commit/push.
