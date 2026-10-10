# Public visit tracking

Implemented at the owner's request using the [playbook analytics skill](../../funtoolio-playbook/skills/funtoolio-product-analytics/SKILL.md). The owner supplied the Cloud snippet with website ID `37851bad-5d45-4228-b8f2-c1c6b44f4ab6`. This is the existing shared DoApprove/Shopdesk stream, now also selected for StayWidget. Website IDs are public settings; no account credentials or API keys are included.

## Coverage and configuration

Only exact `/`, `/Pricing`, `/Demo`, `/Install` and `/Limitations` pages on the configured HTTPS public origin are eligible. Production and isolated Testing qualify; Development, staging, loopback and mismatched hosts do not. Widgets, hosted reservation forms, guest links, owner pages, feedback, sign-in, callbacks, privacy/terms/contact, errors, assets, health, robots and sitemap are excluded. No tracker, resource hints or analytics CSP exceptions appear on excluded pages.

Tracked source settings enable the selected stream, but the local preview stays untracked. At the already selected public origin, configure `StayWidget__PublicOrigin=https://staywidget.funtoolie.com`. This task does not deploy that origin or verify DNS. `Analytics__Enabled=false` is the server kill switch; `Analytics__WebsiteId` overrides the stream if a later separately approved ID is provided. Missing/invalid IDs make tracking inactive. Cloud script and collector endpoints are fixed to `https://cloud.umami.is/script.js` and `https://gateway.umami.is/api/send`; browser queries cannot select them. Public CSP permits only these exact endpoints alongside the existing policy.

## Data and visitor choices

One manual native view per eligible page display. No second custom pageview event, launch/completion event, automatic click/history/performance tracking, replay, heatmap or identity linking is added. The wrapper uses `data-auto-track=false`, search/hash exclusion and a before-send hook that rejects identify/custom events and rebuilds each payload from a fixed catalogue. It includes website ID, approved hostname, fixed public path/title, empty referrer, language and screen size. No guest/property/account IDs, form content, arbitrary titles, queries, fragments or raw referrers are sent. Existing no-referrer headers also protect SDK and collector requests. Umami still receives ordinary network transport information; this is not an anonymity claim.

Website visit settings in the eligible public footer provides keyboard-accessible opt-out. The local-storage key `staywidget.analytics-choice.v1` contains only disabled/expiry and lasts 180 days. Stored opt-out, DNT and GPC prevent SDK loading and sends. Withdrawing tracking blocks future sends immediately; opting back in applies on the next eligible display without backfilling earlier events. Malformed/unreadable preference storage defaults to no collection; a write failure turns off the current page and explains browser privacy signals for future pages. Without JavaScript, tracking is off. Tracker failures never delay navigation or affect reservations. No backend proxy or replay bypasses visitor choice.

This follows the portfolio's minimized opt-out design. Provider processing, audience/jurisdiction, retention, transfers and any required opt-in must be assessed for the actual operator before launch. The draft Privacy notice discloses this boundary; adding a script does not finalize legal policies.

## Reporting and costs

In the existing Umami dashboard filter by hostname `staywidget.funtoolie.com`. Fixed StayWidget titles distinguish overlapping paths such as `/` within the shared stream. Report estimated public visits separately from requests, widget views and installation acknowledgements in the product's optional first-party counters. Do not count iframe activity as portfolio-homepage visits or sum mixed-stream unique visitors as independent product audiences. No booking/contact data or owner conversions are sent to Cloud.

The [Umami pricing page](https://umami.is/pricing), checked 10 October 2026, lists Hobby as one website, 100K events/month and six-month retention. This selected stream shares its usage with other products; it does not establish additional website entitlements or verified multi-host account acceptance. Actual account limits/domain settings and live StayWidget ingestion remain unverified. No account changes, paid upgrade or self-hosted service were created. A future self-hosted migration should use distinct product IDs and an explicit dated reporting cutover.

## Verification

Tests run against isolated disposable PostgreSQL and a synthetic public HTTPS host, intercepting all Cloud script/collector traffic. They check one view per allowed page, bounded fields, sensitive URL/title/referrer exclusion, identify/custom-event rejection, cookie and request-header boundaries, private-route exclusion, DNT/GPC/opt-out, mobile keyboard controls and navigation with a blocked SDK. Existing Development browser flows also remain tracker-free. Results and published artifact inspection are recorded in implementation-status.

The normal suite uses an offline contract fixture. For SDK compatibility use the previously downloaded official Cloud script in ignored `artifacts/umami-script.js` and set `STAYWIDGET_UMAMI_SCRIPT_FIXTURE` to its absolute path before running the test runner. Reused Shopdesk fixture SHA256: `91a876d767646fd5b7701b6fabf97f8a99ae53b94e7e5b58d465bad1e5d763e0` (4,810 bytes). A fresh Cloud download returned HTTP 403 during this task, so compatibility evidence applies to that recorded SDK copy; reverify if the live SDK changes. No test events are sent to Cloud, and no real dashboard ingestion is claimed.

Official implementation references: [tracker configuration](https://docs.umami.is/docs/tracker-configuration), [manual object payloads](https://docs.umami.is/docs/tracker-functions).
