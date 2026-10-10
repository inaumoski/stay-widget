# StayWidget — product and implementation plan

Prepared 2026-10-10 under planning-only authorization. The owner subsequently authorized starting M1 and adding its distinct local common-login client. [Implementation status](implementation-status.md) and [local development evidence](local-development.md) record the resulting implementation and limits. Unimplemented architecture, limits and policies below remain recommendations for review. Customer interviews are optional research, not a development prerequisite.

## 1. Evidence, instructions and decisions

The owner clarified the requested playbook spelling to `funtoolio-playbook` and authorized creation of `stay-widget`. The target folder did not exist; there is no existing application architecture, plan, framework pin, deployment or data to preserve. No `AGENTS.md` was found in the workspace root or inspected parents. Applicable source instructions are the playbook's `AGENTS.md` and auth repository instructions for the read-only auth assessment; no auth files will change in this task.

The governing entry point is [funtoolio-plan-small-product](../../funtoolio-playbook/skills/funtoolio-plan-small-product/SKILL.md), using its [planning format](../../funtoolio-playbook/skills/funtoolio-plan-small-product/references/planning-format.md). It is the only relevant general planning skill; the other skills below supply conditional details, not competing planning workflows. The [planning prompt](../../funtoolio-playbook/templates/plan-instructions.txt) and [status template](../../funtoolio-playbook/templates/implementation-status.template.md) inform document structure. This plan is the product specification; status records execution evidence. Do not create application scaffolds or an automatically loaded `AGENTS.md` under planning authorization.

Observed reusable knowledge: sibling products use .NET, public server-rendered pages, product-owned entitlement policies and independent data. Funtoolio Auth exists locally and its docs describe ASP.NET Core Identity/OpenIddict with Google, local issuer `https://localhost:7443/`, retained compatibility identifiers and separate product cookies/users. Its deployment and complete real-provider lifecycle are not established by this task. Do not copy credentials, security contracts, database access or runtime code from siblings blindly.

Read-only SDK inventory reported .NET SDK 10.0.401 at `%LOCALAPPDATA%/Fantoolie/dotnet`. Official support guidance lists .NET 10 as active LTS through 14 November 2028. Npgsql documents an EF Core provider 10 release. Choose supported matching servicing versions at implementation; no package restore or OS/database compatibility test has run. [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [Npgsql 10 release](https://www.npgsql.org/efcore/release-notes/10.0.html).

### Portfolio adoption and explicit conflicts

| Area | StayWidget decision and authoritative detail |
| --- | --- |
| Design | Follow [design guidance](../../funtoolio-playbook/references/design-system.md) and [accessible UI skill](../../funtoolio-playbook/skills/funtoolio-accessible-web-ui/SKILL.md). Give StayWidget its own Sand & Sea theme; limited owner widget colors are separate from a full theme editor. |
| Owner login | Follow [shared-login skill](../../funtoolio-playbook/skills/funtoolio-shared-login/SKILL.md), [shared-login reference](../../funtoolio-playbook/references/shared-login.md) and [auth identity](../../funtoolio-auth/docs/product-identity.md). Common Funtoolio Google login satisfies the brief without a new direct Google integration or Gmail scopes. |
| Analytics | The brief's **no tracking scripts** overrides the planning default of public visits tracking through Umami. Refer to [analytics skill](../../funtoolio-playbook/skills/funtoolio-product-analytics/SKILL.md) for exclusions and minimized reporting, but use only bounded aggregate first-party counters as specified in section 13. No Umami/GA tag or Cloud stream adoption. |
| Email | The explicit **without email** instruction overrides the earlier guest-email flow and conditional Brevo default. V1 uses receipt/status pages and owner polling, not notification delivery. Brevo is a later feature under [existing email guidance](../../funtoolio-playbook/plans/server-setup-and-installation.md#9-email-and-analytics). |
| Ecosystem footer | Use [portfolio footer guidance](../../funtoolio-playbook/references/portfolio-footer.md) on StayWidget's own public marketing pages, with “More from our ecosystem”, live destinations and current-site omission. Do not insert that project list into customer widgets or transactional guest pages. |
| Pricing | Follow [pricing skill](../../funtoolio-playbook/skills/funtoolio-product-pricing/SKILL.md) and [portfolio defaults](../../funtoolio-playbook/skills/funtoolio-product-pricing/references/portfolio-defaults.md). Free/Pro/Custom with default free Pro launch access; no initial automatic billing. Section 11 contains only StayWidget's proposed numbers. |
| Feedback | Signed-in owners get the [simple feedback workflow](../../funtoolio-playbook/skills/funtoolio-product-feedback/SKILL.md), with [simple collection reference](../../funtoolio-playbook/skills/funtoolio-product-feedback/references/simple.md): private submission, committed acknowledgement and direct database review. No product-feedback inbox, guest feedback accounts or conversation UI. Owner reservation-request management is a separate core product feature and is not prohibited by that feedback rule. |
| Policies/support | Adapt [service/support guidance](../../funtoolio-playbook/references/service-terms-support-and-feedback.md). Keep guest booking terms separate from SaaS terms and product-feedback permissions. Real operator details, jurisdictions and contacts remain launch decisions. |

The brief permits up to six material questions; this overrides the template's broader question allowance. Planning does not authorize installing any linked skill. Shared sign-in is an existing external dependency; StayWidget itself still has one process and one database.

## 2. Scope, positioning and release boundaries

Positioning: **“Add simple direct-booking requests to your existing accommodation website.”** Market to one independently managed property per account and individually named rentable units. This is a request and inventory tool, not an OTA, PMS, channel manager, revenue manager, payment processor, travel-insurance product or legally reviewed compliance system.

| Inventory model | Assessment |
| --- | --- |
| Individually identifiable units | Recommend v1: Room 1, Garden Cottage, Apartment A each has its own capacity/calendar; a request selects exactly one unit. Straightforward exclusion constraints and owner decisions. |
| Pooled room types | Defer: selling a quantity of interchangeable rooms requires capacity accounting per night, allocation/reallocation and oversell rules. Poor fit for 1–5 units and unnecessary for the first slice. |

**Smallest vertical slice:** authenticated owner creates one property and one unit; guest requests dates through a hosted request page; owner confirms; the system blocks those nights and rejects competing confirmation. Include request terminology, receipt/status capability, database integrity and basic tenant isolation from the start. Loader and polished calendar follow this proof.

**First usable release:** M1–M5, with 1–5 units under launch Pro, property setup, requests, expiry, owner dashboard, calendar plus accessible list, manual bookings/blocks, cancellation/editing, loader/direct iframe/hosted fallback, public docs, pricing/entitlements, owner feedback, backup/restore and abuse protections. Owners explicitly accept the lack of email and calendar sync before activating the widget. No accommodation quote is displayed.

**First commercial release:** only after first usable release evidence and a separately approved SaaS billing rollout. Add tested provider subscription handling, purchase consent, legal/tax disclosures and negotiated plan transitions; consider email before charging because dashboard polling is a substantial business limitation. Do not advertise email or sync as delivered features before their own milestones pass.

Exclude direct OTA APIs, real-time channel management, dynamic pricing/rate plans, group/multi-unit requests, multi-property organizations, housekeeping, restaurant/spa bookings, loyalty, native apps, public accommodation directory, AI recommendations, arbitrary uploads, card collection/no-show charging, SMS/WhatsApp integrations, native builder plugins and automatic guest payments. No self-managed mail server, Redis, distributed queue, microservices, Kubernetes or shared speculative platform framework.

## 3. Requests, confirmation and cancellation policy

| Mode | UX and complexity | Decision |
| --- | --- | --- |
| A: request | “Request received — awaiting property approval.” No inventory hold; owner checks external commitments and agrees terms before confirming. | V1 |
| B: instant without payment | Immediately committed inventory and clear confirmation; owners must trust inventory accuracy and manage no-shows. | Later product decision |
| C: instant with hosted payment | Temporary holds, expiry, verified payment events, refunds and reconciliation of payment/inventory races. | Deferred, not an incremental label change |

**Recommended v1 policy:** a pending request does not reserve inventory or establish a confirmed stay. Multiple pending requests for identical dates are allowed and displayed to the owner. Never tell each guest that dates are “held”. Initial availability is a current system snapshot, not a guarantee of approval or knowledge of outside bookings.

Pending requests expire at the earlier of submission + 48 hours or 00:00 on arrival day in the property's configured time zone. Store an explicit UTC deadline; commands check it even if the sweep job is delayed. V1 accepts arrival tomorrow or later, never same-day requests. The receipt shows the actual deadline and no guaranteed owner response. Expired requests require a new submission; no silent reopening. The owner sees age, expiry and overlapping pending requests.

Before confirming, the owner reviews external bookings, contacts the guest directly, and agrees the final price and property terms. The confirmation form requires acknowledgement that these details were agreed and records a bounded structured final agreed amount/currency and terms-version reference; this is an owner-entered agreement record, not an automated quote or tax engine. Guest acceptance is handled externally in v1; the system does not claim to prove contract formation. Owner confirmation is operationally final within StayWidget: dates become occupied immediately. Properties requiring an additional contract/deposit step must complete it outside StayWidget before clicking Confirm. Do not label an awaiting-deposit request “confirmed”. Show this limitation before signup and at confirmation; legal effect requires jurisdiction-specific review.

At confirmation, atomically validate pending state, deadline, unit capacity, active/public unit, dates, seasonal window, turnover and live occupancy. A conflict or changed policy returns a recoverable failure; the request remains pending until the owner declines or it expires. Other overlapping pending requests are not automatically declined: flag them for review rather than silently losing enquiries. Two owners/tabs accepting conflicting requests cannot both succeed.

Owners can decline with a short public reason separate from a private note. Owners can cancel a confirmed reservation after handling property cancellation obligations; cancellation releases its occupancy atomically and remains in history. Guests may withdraw pending requests through a scoped capability and confirmation screen. For confirmed stays, the guest can **request cancellation**, creating an owner-visible flag; dates stay occupied until the owner records cancellation. Manual reservations have the same occupancy protection. No refund or no-show financial action exists in v1.

**No-email consequence:** guests receive an on-screen receipt and must save a private status link; acceptance/decline appears there after the owner responds. Owners must check the dashboard at least daily and contact guests themselves when needed. No browser push, email promise, background notification or unseen-delivery badge. A lost status link requires contacting the property; no public reference-code lookup exposes guest data. During an outage, owners use exported records and independent contacts, and avoid promising availability they cannot verify.

## 4. Architecture and proposed component layout

Recommend ASP.NET Core 10 Razor Pages with small first-party JavaScript for date enhancements, async availability, loader/resizing and owner calendar. Native date fields and a keyboard-accessible list remain usable; do not introduce a complex draggable calendar in v1. Server services own validation and reservation commands. Built-in DI, feature modules and one PostgreSQL database, using EF Core/Npgsql 10 with reviewed SQL migrations for PostgreSQL-specific constraints.

```text
Property website                    Owner browser (top level)
  loader-v1 -> isolated iframe         -> StayWidget owner pages
  direct iframe / hosted link               |
              |                             | OIDC redirects
              v                             v
             HTTPS reverse proxy       Existing Funtoolio Auth
                    |
           StayWidget ASP.NET Core process
       Public pages | Widget | Guest capability | Owner pages
        Inventory/request commands | Entitlements | Feedback
                    |           |
              PostgreSQL    Bounded hosted workers
             dedicated DB   expiry/retention/aggregate cleanup
                    |
             encrypted off-VM backup
```

Planned layout below. M1 implements the three source projects and keeps unit, database and browser checks together in `tests/StayWidget.Tests` until separate test projects provide useful isolation:

```text
src/StayWidget.Web/        Razor pages, endpoint adapters, OIDC, DI,
                          feature folders, resources, scoped CSS/JS
src/StayWidget.Domain/     Date/occupancy rules and request transitions
src/StayWidget.Persistence/EF mappings, constraints, migrations, queries
tests/StayWidget.UnitTests/
tests/StayWidget.IntegrationTests/  isolated real PostgreSQL
tests/StayWidget.BrowserTests/      browser and cross-origin fixture
docs/                     specification, actual evidence, later runbooks
```

Keep application commands in feature folders within Web initially; do not create an interface for every class or a separate platform/service package. The domain has no HTTP, ORM or UI dependency. Use explicit `TimeProvider`, `DateOnly` dates, nullable references and bounded request bodies. One PostgreSQL provider; SQL Server/InMemory results cannot prove exclusion/concurrency correctness.

React would add a build/dependency pipeline without enough benefit for this small calendar. Server-side Blazor adds persistent connection pressure and embed reconnection concerns; WebAssembly adds download/runtime cost. A host-rendered Web Component makes CSS, host scripts, CSP, accessibility and version conflicts harder. Revisit only after measuring a demonstrated UX need.

Owner authentication uses common Funtoolio OIDC with separate client registration, Authorization Code/PKCE and host-only Secure/HttpOnly cookies. Map validated issuer/subject to a local user; do not use email as an ownership key. No Gmail scopes, cross-product cookies or access to the auth database. The normal Sign in action directly starts the OIDC challenge; provide bounded failure/logout recovery. Issuer setup is a separately scoped prerequisite, not permission to modify auth. Guest routes ignore owner cookies as authority and never require owner login.

## 5. Embedding and installation

| Choice | Assessment |
| --- | --- |
| Direct iframe | Most robust isolation, easy builder URL integration; fixed-height/container sizing requires documentation. Supported fallback. |
| Small loader inserting iframe | Recommend primary plain HTML/self-hosted WordPress snippet: isolated UI plus measured height and multi-instance support. A blocked loader still leaves a hosted-page link. |
| Host DOM/Web Component | Defer host CSS/JS collisions, maintenance and CORS/public write API exposure; no justified SEO advantage. |

The generated installation snippet contains a property public ID, a target container, deferred first-party HTTPS loader and a permanently visible fallback link. Public IDs identify a property, not grant admin access. No guest details, auth token or private feed URL belongs in installation code. No executable snippet is generated during planning.

Publish a stable protocol-major loader path such as `/embed/v1/loader.js`, with short cache validation and backward-compatible security fixes; version internal assets immutably. Keep old protocol support for an announced period. Offer immutable release URLs for advanced hosts that accept manual upgrades, but do not force nontechnical owners to update for every compatible change. Future releases need a cached-client compatibility test.

Loader scans all matching containers once, can initialize multiple instances, and does not double-insert on repeated script execution. Assign each iframe a random instance ID, accessible title and bounded initial height. Debounced `ResizeObserver` in the child proposes height; no guest content or reservation data crosses to the parent. Verify exact `event.origin`, `event.source === frame.contentWindow`, version, instance ID, message type and finite height within a published clamp (recommend 320–1,600 CSS px). Parent sends initialization to the exact configured widget origin. Child replies only to an approved parent origin learned from validated initialization, not a URL it blindly trusts. Instance ID is routing, not authentication. Reject unrelated windows, unexpected objects and repeated floods. No wildcard destinations for sensitive messages. [Browser messaging guidance](https://developer.mozilla.org/en-US/docs/Web/API/Window/postMessage).

For a direct iframe with no loader, use internal scrolling and a documented fixed-height wrapper; dynamic height cannot be assumed. Guest writes use same-origin endpoints inside the frame with bounded signed form context and capability tokens, not third-party session cookies. The hosted page remains available without third-party storage. Core public selection/form entry should render without JavaScript; private status and enhanced retry capability can require a small first-party script, disclosed with a direct-contact fallback when scripts are disabled. Do not place a guest secret in hidden host-page markup or send it to the embedding page.

**Embedding policy:** owner registers exact HTTPS website origins. Use a self-service challenge file/meta verification before marking a plain website origin verified; fetching verification pages itself needs SSRF protection. V1 may accept a clearly labeled owner-attested origin for trial installation without building server URL fetching. Domain possession is not verified merely by an account assertion. Publishing implies the owner attests authorization, and abuse reporting remains necessary. Verified-domain automation can be a later subtask.

Widget responses use route-specific `Content-Security-Policy: frame-ancestors` for approved origins, with no blocking `X-Frame-Options` on that route. All ancestor frames must be allowed; builders may use nested frames or opaque sandbox origins. Avoid broadly allowing every builder customer via a wildcard just to make installation work. Recommend Wix Embed a Site pointing to the hosted widget URL, with a tested container; if the platform's actual ancestor chain cannot be narrowly approved, offer a top-level request-page link. Do not accept arbitrary `parentOrigin` query values as CSP authority.

Owner/admin, guest private-status and auth routes use `frame-ancestors 'none'` and matching denial headers. Marketing pages default to no framing. Availability/config APIs stay same-origin to the iframe; no wildcard credentialed CORS. Framing permission does not authorize reservation access or prevent bots from calling public endpoints.

| Platform | Support boundary / verification |
| --- | --- |
| Plain HTML | Loader + iframe, direct iframe or hosted link. Host CSP must allow loader `script-src` and widget `frame-src`; test HTTPS, width and multiple instances. |
| Self-hosted WordPress | Custom HTML block where user permissions/security filtering allow scripts/iframes; plugin/theme/CSP combinations need smoke tests. No native plugin. |
| WordPress.com | Restricted tags depend on paid/activated hosting features; do not promise free-plan custom embeds. Hosted hyperlink remains a fallback. [Official Custom HTML guidance](https://wordpress.com/support/wordpress-editor/blocks/custom-html-block/). |
| Wix | Use supported Embed HTML/Embed a Site. HTTPS is required; outer sizing may constrain the iframe independently of inner responsiveness. Verify the real editor/plan, published mobile page and ancestor behavior; do not promise automatic resizing of the Wix outer container. [Official embed guidance](https://support.wix.com/en/article/wix-editor-embedding-a-site-or-a-widget). |
| Other builders | Supported only when the generic embed or hosted link actually works. No marketplace apps or universal compatibility claim. |

A future WordPress plugin can wrap the existing snippet in a shortcode/settings page. It must not create a second inventory engine or database.

## 6. Journeys, screens, design and accessibility

Proposed StayWidget palette: Sand & Sea, with warm sand background `#F8F5EF`, white surface, dark slate text `#172A32`, muted slate `#4B6068`, deep teal primary `#0B6068` and white button text. These are proposed tokens, not an audited palette; check real text/control/focus/disabled/state combinations. Use system fonts, 16–18px body copy and restrained spacing/radii. Do not copy another product's full screen or palette.

Owners can select one approved widget accent/background preset or constrained colors that pass actual contrast rules. Validate values as colors; no CSS strings, URLs, HTML or arbitrary fonts. UI language defaults to English and only offers completed translations; use resource keys from the start without promising other languages. Pricing currency and property time zone do not follow browser language.

| Screen / journey | Main behavior and recovery |
| --- | --- |
| Signup | Clear request-only/no-email/no-sync explanation before activation; common Sign in; local account, terms version and one property setup. Failed/cancelled sign-in returns a usable retry page. |
| Property setup | Display name, public contact method, IANA time zone, one agreement currency, check-in/out text, property policies, language, limited theme and permitted origins. Draft until required fields/terms are ready; no fabricated public contact. |
| Unit setup | Unique display name, guest capacity, bookable date window, min/max stay, enabled flag, turnover choice. Unit deactivation hides new requests but preserves existing reservations. |
| Requests dashboard | Pending count, oldest age/deadline, same-unit conflicts, status filters and explicit manual refresh. Poll only while visible at a modest interval; dashboard badge is not an email notification. |
| Calendar | Individual-unit rows, nights and status legend plus equivalent chronological list. Read-only calendar selection can open native forms; no drag-rescheduling. Date/range labels remain understandable without color. |
| Guest search | Property identity/contact/policies visible; date fields and guest count; available named units; no availability reveals guest details or internal reasons. Empty/invalid/unavailable states offer changed dates or direct contact. |
| Guest request | Exactly one unit, dates/count, name and phone with international guidance; short structured acknowledgements. No passport, uploads, marketing checkbox or sensitive questionnaire. Review then submit. |
| Receipt/status | “Request received — not a confirmed reservation”, deadline, saved private link and contact route. Copy/open link is an explicit user action. Status reveals dates/unit/state and agreed booking details only to the capability holder; omit phone/private notes. |
| Owner decision | Review current inventory and outside commitments; external contact/price agreement; Confirm or Decline with recheck. A stale/conflicting decision preserves entered notes and explains why it failed. |
| Manual booking/block | Add individual-unit reservation or unavailable range, review affected dates, conflict-safe commit. Editing uses a revision; stale writes do not overwrite a newer decision. |
| Cancellation | Guest withdraws pending request or requests confirmed-stay cancellation; owner cancellation releases inventory only after an explicit review. Show before/after dates. |
| Installation | Live preview, builder-specific steps, plain hosted-page fallback, visible no-email/no-sync responsibilities and tested domains. Preview uses synthetic guest data. |
| Plan/feedback | Public comparison and owner usage; simple signed-in private feedback form, no feedback-management UI. |

Target WCAG 2.2 AA, subject to real review. Keyboard-first native controls, explicit labels/errors, focus recovery, 44px touch targets, 320px layout, 200% zoom and enlarged text. Announce request results with a restrained status region. No date-picker keyboard trap; native date input/fallback text format and validation remain available. Recheck availability after recoverable errors without discarding entered contact details. Avoid unrequested persistent browser drafts. Test iframe scrolling, mobile keyboards and top-level fallback in actual supported browsers.

## 7. Inventory rules and database correctness

**Dates:** stay dates are `DateOnly` / PostgreSQL `date` in the property time zone, not UTC instants. Occupied nights use `[check-in, check-out)`. June 10 → June 12 occupies June 10 and June 11. With same-day turnover allowed, a new guest may arrive June 12. With a one-night turnover gap, the reservation's inventory range also blocks June 12; the next arrival is June 13. Show occupied nights separately from the cleaning buffer.

Defaults: min stay 1 night, max stay 28, tomorrow-or-later arrival, rolling 365-day request horizon, positive integer guests not exceeding unit capacity. Owners can set min/max within those safety bounds. One explicit bookable window per unit covers seasonal opening; nights must lie inside it. Additional closures use manual blocks. Complex repeating seasons, occupancy categories and arrival-weekday restrictions are deferred. New units start closed/draft until the owner deliberately opens a window. Existing accepted future dates are not silently cancelled when the public window changes.

Pending requests never create occupancy. Confirmed online/manual reservations and active hard availability blocks do. Cancelled reservations, inactive blocks, expired/declined/withdrawn requests do not. Internal past reservations remain recorded until retention cleanup. A hard block cannot overlap a confirmed stay; the owner must resolve that stay first. Manual blocks may initially reject overlaps with other blocks too, prompting merge/edit rather than ambiguous duplicated occupancy. For imported external events later, source records may overlap each other; normalize them into nonoverlapping effective blocked intervals under the unit lock.

**Time:** use UTC instants for submissions, expiry, audit and worker leases, with IANA time zone for arrival-day rules. DST changes do not alter the number of calendar nights. Resolve the expiry instant at local start of arrival day using a tested rule for ambiguous/nonexistent midnight; use the first valid instant for that date. Store the resolved deadline so later time-zone/config edits do not secretly move an existing request's deadline. Changing property time zone requires an explicit reviewed effect on future validations, not reinterpretation of stored dates.

### One inventory ledger, two business records

Keep ReservationRequest and Reservation separate. An enquiry has mutable decision/expiry state and no capacity claim; a reservation has confirmed dates and inventory. Confirmed reservations link uniquely to accepted requests when applicable; manual reservations have no request. Snapshot submitted dates, policies and contact rather than silently rewriting the original enquiry after a booking edit. A minimal InventoryAllocation table unifies active booking and hard-block ranges for one database overlap rule; separate tables with independent constraints would not prevent booking-versus-block collisions.

Use non-null finite `daterange`, canonical `[)`, nonempty/ordered dates, and a PostgreSQL GiST exclusion constraint on active allocations: equal UnitId cannot have overlapping InventoryRange. Install `btree_gist` only during a later authorized migration. UUID equality operator support is documented. Backstop with one allocation per reservation, type/link consistency and tenant-consistent composite foreign keys. Do not permit arbitrary active ledger edits through the UI. This is a design specification, not executed SQL. [PostgreSQL range constraints](https://www.postgresql.org/docs/17/rangetypes.html#RANGETYPES-CONSTRAINT), [btree_gist](https://www.postgresql.org/docs/17/btree-gist.html).

### Confirmation and mutation protocol

1. Authenticate the local owner and resolve the property from ownership, never from a posted owner ID. Start a transaction and acquire property lock, unit lock(s) in sorted ID order, then request/reservation row lock. Apply this order to all inventory/config/quota writes; the tiny per-property serialization is an acceptable v1 tradeoff.
2. Re-read state/revision and compute time after locks are obtained. Enforce pending/unexpired, enabled unit, capacity, current seasonal/turnover rules and quota prerequisites. Recheck confirmed allocations and hard blocks inside the transaction, not only the earlier search.
3. Insert a reservation, its active allocation and an activity record; mark the request accepted and link it to that reservation. Unique linkage and exclusion protection remain the final database backstop. Commit all or none.
4. Return success only after commit. On exclusion conflict, rollback, return 409 with a safe explanation and reload availability. No partial accepted request, allocation or activity can remain. Bound retries for deadlock/transient failures using the same idempotency key.

Ordinary READ COMMITTED with this common lock protocol and the exclusion constraint is the recommended starting point; SERIALIZABLE is an alternative with retry cost, not a substitute for disciplined constraints. Optimistic revision tokens catch stale UI changes but cannot alone prevent phantom range overlap. Endpoints also handle constraint errors from competing connections that bypass an earlier availability read.

Request expiry/withdrawal and owner acceptance use the same request serialization; expiry also checks the exact deadline in every command. If withdrawal/expiry wins, confirmation fails; if confirmation wins while valid, later withdrawal becomes a cancellation-request action, not release. For a same-command retry after deadline, return the already committed result rather than creating another booking.

Editing a confirmed reservation locks old/new units in sorted order, checks expected revision and atomically updates dates/allocation. Exclude its own allocation during overlap search; the database checks others. Failure rolls back to original dates. V1 can constrain edits to the same unit to reduce complexity; reassignment is a later reviewed feature. Changing cleaning-gap policy must recompute affected future allocations under locks or reject the change with collisions, not silently invalidate the invariant. Unit archival never deletes future inventory; deletion is prohibited while reservations need it.

**Idempotency:** guest submit uses a cryptographically random 256-bit submission key per deliberate attempt, scoped to property. A unique constraint and normalized payload digest create one enquiry across double-clicks/timeouts/retries; identical retry returns the original receipt, changed payload with the same key returns 409. Server rechecks current availability on the first write; pending requests remain nonblocking. Owner commands use scoped command keys and expected revisions. Never use guest phone/date matching as deduplication or disclose another guest's enquiry.

Generate guest bearer secrets on the server; store only their hash. Preserve an encrypted receipt response for up to 24 hours under protected service keys so an identical submission retry can recover the same private link without a second enquiry. Receipt cache access still requires the original high-entropy submission key; expire cached recovery data independently of request retention. Protect both secrets from logs. After the recovery window, show committed-result acknowledgement without exposing a token; the guest uses their saved link or contacts the property. Test failure after commit/before HTTP response.

**Guarantee boundary:** the database prevents overlapping active allocations per unit recorded inside StayWidget. It cannot know an unentered phone/OTA booking, another independent inventory system, operator misinformation, stale future calendar feeds or obligations under property terms. Availability pages and signup must state that outside bookings must be blocked manually. Never advertise zero double bookings across channels.

## 8. Data model, ownership and lifecycle

All product data stays in the StayWidget database. Proposed UUID primary keys are identifiers, not authorization. Every property-owned row carries PropertyId; composite foreign keys ensure its Unit/Request/Reservation belongs to the same property. EF query filters may help, but every command and lookup must enforce ownership explicitly. Avoid a membership table while there is only one owner and one property per account; add staff membership only in a future scoped task.

| Entity | Required fields / invariants / indexes |
| --- | --- |
| LocalUser + ExternalIdentity | Local ID, minimal display/profile, unique `(Issuer, Subject)`, terms version/time. No direct Google/password store in StayWidget. Property owner uses local ID. |
| Property | Unique OwnerUserId for v1, unique unguessable public identifier, display/contact/policies/version, IANA time zone, agreement currency, language/theme, publication state, revision. One account owns at most one property. |
| Unit | PropertyId, unique name per property, capacity, enabled, open window, min/max nights, turnover 0/1, revision. Do not recycle a public ID for another unit. |
| ReservationRequest | PropertyId/UnitId, submitted dates/count/name/phone, policy snapshot/version, state, CreatedAtUtc/ExpiresAtUtc, decision timestamps, public reason/private note separately, revision, submission-key hash/payload digest. Unique property-scoped key; index `(PropertyId, State, CreatedAtUtc)` and deadline sweep. Contact fields belong here, no reusable guest-profile entity. |
| GuestCapability | RequestId/PropertyId, hashed secret, permission (`View` or `WithdrawPending/RequestCancellation`), expiry/revoked timestamp. Separate action capability or explicit narrowed scope; no authority from reference number alone. Index token hash. |
| Reservation | PropertyId/UnitId, unique nullable RequestId, active/cancelled state, current dates/count, final externally agreed amount/currency and terms reference, cancellation-request flag, timestamps/revision, minimal contact snapshot for manual bookings. No payment state. |
| InventoryAllocation | PropertyId/UnitId, reservation link or manual-block kind, inventory range, active flag, private block reason, revision. Exclusive link/kind checks, booking unique link, GiST overlap exclusion. Manual block business data can live here rather than a redundant AvailabilityBlock entity. |
| ActivityRecord | PropertyId, actor local user/system, entity/action, timestamp, safe state/date deltas. No tokens/full phone or raw request body. Index property/time; bounded audit, not event sourcing. |
| PropertyEntitlement | Unique PropertyId, adopted base plan, effective launch access, resource overrides only for approved Custom, revision. Keep campaign policy in catalog; no portfolio subscription or automatic negotiated grant. |
| ProductFeedback | Simple skill's private table linked to local owner; reserved internal Comment/Response never returned. No guest access or feedback-management endpoints. |
| DailyAggregate | Date, allowed property/event/source bucket, count; no visitor/device/profile ID. Counters are approximate demand signals, not an inventory/audit source of truth. |

No QuoteSnapshot, Notification, CalendarSource, ImportedEvent, Payment or inventory-hold entity in v1. Add those only in their deferred milestone. Cache encrypted retry receipts in a minimal expiry-bound field/table, not a permanent token vault.

Proposed retention, subject to actual markets/contract review: pending/declined/expired/withdrawn request contact data for 90 days after closure; confirmed/manual booking personal data for 12 months after departure or cancellation, with minimal nonpersonal occupancy/history for 24 months. Do not purge active/upcoming reservations to meet a quota. Raw diagnostic logs 7 days, security records 30 days where justified, aggregate counters 90 days and activity metadata 12 months. Product-feedback closure retention 12 months unless needed for a current improvement, coordinated with the simple skill. No phone or free text in retained “anonymous” history.

Account closure disables new requests first, gives an explicit export/transition path, warns about future bookings, revokes capabilities and removes product personal data after a proposed 30-day recovery period where no required legal retention applies. Resolve future reservations as part of closure; do not silently erase customer obligations. Central Funtoolio account deletion is a separate coordinated identity task. Backups age out on a proposed 30-day cycle; restored databases reapply deletion/revocation records before returning to service. CSV export requires owner auth, formula-injection protection, no token columns and explicit handling as guest data.

## 9. State machines and authorization endpoints

| Record | Allowed transitions / behavior |
| --- | --- |
| Request | New valid submit → Pending. Pending → Accepted, Declined, Expired or Withdrawn. Those decisions are terminal for the enquiry; Accepted links to one Reservation. No generic posted status setter. |
| Reservation | Create from accepted request or manual entry → Confirmed. Confirmed → Cancelled by owner. Date edits preserve Confirmed only after inventory revalidation. No automatic no-show deletion or Completed occupancy release; past dates naturally fall outside future searches. |
| Cancellation request | Confirmed → cancellation flag set by scoped guest capability; owner clears/resolves flag by keeping or cancelling the booking. Repeats acknowledge one flag, not multiple cancellations. |
| Allocation | Created active with reservation/manual block; cancellation/removal → inactive atomically. Reactivation is a new checked command. Booking allocation cannot be removed independently. |
| Notification/payment | Absent in v1. Future state is orthogonal to request/reservation and cannot undo committed inventory through delivery failure. |

Expired/withdrawn/declined receipts remain viewable until capability expiry, with minimal data. If a booking is edited/cancelled later, the accepted enquiry's original snapshot stays intact while its status page separately displays current reservation status. Stale decisions return a conflict with safe current status; repeated identical actions return committed results.

Proposed routes are future contracts; final handler names can adapt to Razor Pages:

| Route / method | Actor and response boundary |
| --- | --- |
| `GET /`, `/pricing`, `/demo`, `/installation`, `/terms`, `/privacy`, `/contact` | Public marketing/help. Demo isolated from real inventory and guest data. |
| `GET /embed/v1/loader.js` | Public cacheable compatible loader; no secrets or customer data. |
| `GET /w/{publicId}` and `GET /request/{publicId}` | Public published property widget / top-level fallback; public DTO only, approved framing for widget. |
| `GET /api/widget/{publicId}/configuration` | Names/capacities/theme/policies/public contact and service limitations; no owner user IDs, private origins, guest details or commercial secrets. |
| `POST /api/widget/{publicId}/availability` | Bounded dates/count; available named units and request eligibility, not detailed reservations/block reasons. No arbitrary calendar history. |
| `POST /api/widget/{publicId}/requests` | Anonymous signed-context submit, abuse limits, server validation/idempotency; committed receipt and private capabilities only. Never trust owner/property IDs in posted DTO. |
| `GET /guest/status` + `POST /api/guest/status` | Top-level no-store shell; capability in URL fragment, first-party script sends bearer secret in body/header. Server does not log fragment/body/token; response is scoped to one request. No public enumeration by request ID. |
| `POST /api/guest/withdraw` / `request-cancellation` | Separate action capability, explicit confirmation, POST only, current-state check/idempotency. Opening/scanning a link never cancels inventory. |
| `GET /owner/*`, owner property/unit/request/calendar/export pages | Authenticated local owner, top-level only, authorized property predicates, private/no-store/noindex. |
| Owner POST confirm/decline/manual-reservation/edit/cancel/block/configure | Authenticated owner, antiforgery, ownership and revision, bounded command key; common transaction/lock protocol. |
| Owner POST product-feedback / Custom request | Private simple submission; no entitlement granted by submitting a Custom enquiry. |
| Future private feed/source routes | Not built in v1; separate capabilities/owner auth and SSRF controls if later selected. |

Unknown/private objects return nonrevealing not-found responses. Owner HTTP requests require application-cookie antiforgery. Anonymous guest operations have no ambient authority cookies; a short-lived signed property/form context, same-origin request checks where available and abuse controls protect public writes without relying on third-party cookies. Those checks do not establish guest identity and are not bot-proof. Guest action bearer tokens remain the real scoped authority; no wildcard CORS. Never send cookies or credentials to host pages.

Generate at least 256 random bits per guest secret, hash for verification, constant-time comparisons and revocation. Proposed view/action expiry: terminal enquiry + 30 days; for accepted reservations through departure + 30 days, capped at the supported request horizon plus that grace. Owner date edits deliberately update or revoke capabilities according to the new schedule. Revocation and property disablement are checked on every access. No local storage requirement; show an explicit Save this link warning. Use `Referrer-Policy: no-referrer`, `Cache-Control: no-store`, strict CSP, no external assets and token-redacted telemetry. A fragment avoids server URL logs but does not make a link harmless: it is a bearer credential and can be forwarded or stolen. Do not put real guest links in screenshots/support reports.

## 10. Security, privacy and abuse boundaries

| Threat | Required design / verification |
| --- | --- |
| Cross-property access | Local owner checks on every read/write/export; composite tenant FKs; attacker-supplied IDs ignored/rejected. Test direct URL/DTO attempts, not just hidden navigation. |
| Spam / fake reservations | Pending requests never hold inventory. Property/IP/global bounded rates, short-lived form context, small field lengths, hidden honeypot and timing signals as heuristics; uniform errors. A CAPTCHA is a separately selected escalation, not a default third-party script. |
| Inventory abuse | Owner-only confirmed allocation, server recheck and exclusion protection; disable new enquiries temporarily without deleting accepted bookings. Quota validation before mutation; no false success when blocked. |
| XSS | Encode guest/property text, plain-text policies with limited formatting if needed, no arbitrary HTML/CSS/URLs. Avoid executing text or loading supplied images/files. Sanitize CSV exports. |
| CSRF / clickjacking | Antiforgery for owner cookie commands; owner/status routes unframeable. Guest capability actions require scoped bearer authority, explicit POST confirmation and no ambient cookies. |
| Tokens/secrets | No guest secrets in logs/referrers/analytics/exports. OIDC and encryption keys outside Git, persistent protected keys with backups/revocation. Receipt response cache has short expiry. |
| Unsafe postMessage | Exact source/origin, versioned bounded schema, instance routing; only layout messages. Reject malicious same-origin sibling windows as well as wrong origins. |
| Guest data leakage | Minimal public DTOs and capabilities, private no-store pages, field-level output selection, sanitized logs and synthetic demos. No guest list in availability results. |
| Third-party-cookie restrictions | Guest writes/status do not require cookies. Owner auth is top-level. Test iframe cookie/storage blocking on mobile/browser privacy modes. |
| Future calendar/verification SSRF | No importer in v1. Later outbound fetch must constrain DNS/IP/redirects/egress and credentials as section 14 requires. |
| Malicious embedding / phishing | Exact owner-attested/verified origins and honest property identity. Frame allowlisting reduces misuse but cannot guarantee host authenticity or block every copied hosted link; monitored abuse reports and property suspension remain operator responsibilities. |

Starting safety proposals, to tune under load: max 16 KiB submission body, name 120 characters, phone 40, no free guest questionnaire; rate limit 5 submission attempts per 10 minutes per network address and 100 per property per day, with short-lived privacy-minimized rate buckets. Shared NAT traffic must have a recoverable contact fallback; these are safety controls, not subscription “request credits”. A property-specific exceptional increase needs verified abuse/capacity assessment. Rejections before creation return explicit 429/retry advice; accepted requests are never discarded because a plan count changed. No “unlimited requests” advertising.

Proposed privacy allocation, subject to review: property owner generally determines guest booking purposes; StayWidget generally processes that guest data on their instructions, while the SaaS operator separately determines account/security/billing purposes. Actual controller/processor duties, subprocessors, contracts, transfers and applicable North Macedonia/EU/guest-market law need review before live intake. This is a proposed role assessment, not legal approval. [EDPB role guidance](https://www.edpb.europa.eu/sme/learn-the-basics/data-controller-or-data-processor_en).

Collect only guest name, contact phone, dates, selected unit and count; an email field is unnecessary in the no-email first release. Property owners may need legally required registration outside StayWidget; no passport upload here. Document lawful purpose, retention, rights/contact, export/erasure and breach response. SaaS terms are distinct from property booking/cancellation terms, with versioned acceptance evidence where needed. A Privacy page does not prove compliance. Maintain monitored security/privacy/legal channels even when routine support is self-service and there is no product-generated email.

## 11. Accommodation prices, SaaS plans and attribution

### Guest accommodation pricing

| Option | Decision |
| --- | --- |
| A: no automated quote | V1. Widget states “Price and terms are agreed directly with the property before confirmation.” No misleading total or “free booking” claim. |
| B: nightly estimate + explicit fees | Optional later milestone; easier than a guaranteed total but requires price snapshots and tax/rounding decisions. |
| C: guaranteed confirmed total | Defer until complete pricing rules and contract/payment semantics are agreed. |

V1 records the externally agreed final amount/currency at owner confirmation for guest clarity, with a property-defined tax-included/excluded notice where applicable. Use one configured agreement currency per property, independent of the SaaS subscription currency; store the owner-entered nonnegative amount as a decimal with explicit precision and supported currency fraction digits. Reject unsupported precision rather than silently round an externally agreed amount. No amount is shown as a quote before the owner confirms. It does not calculate taxes or bind a quote during submission. Changing a property policy or currency later does not rewrite a saved agreement. If a later estimate is added: decimal amounts, declared fraction-digit policy, deterministic midpoint rounding, nightly line items plus explicit fees, clear tax inclusion, and saved displayed assumptions/version. Price changes require a newly communicated and agreed amount; no silent recomputation of a pending receipt. No currency conversion, complex promotions or dynamic rates.

### Proposed product commercial model

Follow the linked pricing skill rather than duplicate entitlement algorithms. StayWidget's recommended offer is a hypothesis, not researched market pricing or a promise of checkout:

| Resource / feature | Free | Pro | Custom |
| --- | --- | --- | --- |
| Proposed standard SaaS price | $0 | US $9/month per property; annual plan deferred | Contact for a quote |
| Properties / owners | 1 / 1 | 1 / 1 | Still single-property v1; no implied multi-property platform |
| Active rentable units | 1 | 5 | Negotiated bounded limit, initially propose maximum 10 only after capacity review |
| Requests | No monthly commercial cap; published safety limits apply | Same | Individually assessed, no unlimited promise |
| Core requests/calendar/manual blocks/export | Included | Included | Same plus explicitly agreed supported features |
| Branding | Two visible branded attribution links | Removable via server-side entitlements | Negotiated |
| Email in v1 | None | None | None; a Custom quote does not create an email feature |
| Personal/history retention | Same safety/privacy policy for all; no active-booking quota purge | Same | Any variation requires actual legal/operational support |
| Future sync frequency | No sync promise | No sync promise | No sync promise; later review can propose export and 30-minute import polling |

New/existing eligible accounts default to **Pro at 100% discount until 2027-02-01 00:00 UTC, exclusive**, using the portfolio's fixed campaign, not a rolling trial. Continue free access after that boundary until billing is ready and its rollout approved; no card, automatic charge or automatic downgrade. After the cutoff display continued free early access, not an expired discount or amount due. The owner can test Free entitlement behavior explicitly; promotion expiry must not disable extra units or erase bookings. Active unit limits block adding/re-enabling units, not request review, cancellation/export or existing inventory.

Custom uses the private signed-in submission mechanism with a distinct request category and optional deliberate contact details. A submission grants no Custom access. No promised SLA, bespoke work or priority support. Monitor important contacts independently of optional product feedback.

### Free widget attribution

Text: **“Booking requests by StayWidget · Funtoolio”**. Only the two natural branded links, targeting the brief's provisional `https://funtoolio.com/staywidget/` and `https://funtoolio.com/`; verify those destinations are live before publishing generated snippets. The final product hosting origin is still undecided and can differ from the attribution landing page.

Recommend `rel="sponsored noopener"` if opening a new tab; optionally use one combined `rel="sponsored nofollow noopener"`. Same-tab links need only `rel="sponsored"`. Do not inject unrelated ecosystem links into customer websites, hide attribution, keyword-stuff anchors or promise SEO value. Paid removal is a server entitlement that omits the attribution markup, not CSS hiding or a posted `hideBranding` flag. [Google link qualification guidance](https://developers.google.com/search/docs/crawling-indexing/qualify-outbound-links).

Default launch Pro therefore permits branding removal if the owner chooses. StayWidget's own public-page ecosystem footer is independent of widget attribution and is never rotated into guest booking content.

## 12. Operations, resources and unavoidable responsibilities

Deploy later as one ASP.NET Core process behind HTTPS/Caddy on Ubuntu, dedicated service identity and private loopback port, with a separate PostgreSQL database and restricted runtime/migration roles. Owner OIDC is an external dependency; do not share auth tables, filesystem keys or another product's credentials. Production SDK/OS, Npgsql/PostgreSQL target major and extension permissions remain prerequisites.

Initial budget assumptions: tens of properties in a pilot, at most five units each, mostly short availability queries, no uploads, no email/import workers and one app instance. Reserve an estimated 256–512 MiB normal app RSS with a provisional 768 MiB memory ceiling, a bounded database pool around 10 connections, and modest CPU shares. These are starting experiments, not measured requirements or a concurrency guarantee. On a shared 6-vCPU/12-GB host with 6–10 products, budget PostgreSQL/proxy/OS and neighbors explicitly; do not allocate the entire host to StayWidget or promise burst isolation from shared vCPUs. Load-test representative calendar/query/abuse traffic before setting limits. Disk needs DB, logs, retained releases and backups, not just code; alert on disk and connection pressure.

Use a `BackgroundService` for bounded expiry/retention cleanup with database-scoped leases so restarts or accidental multiple app instances cannot duplicate work. Commands enforce expiry even when workers are down. Persist job progress where needed; memory timers are not reliable state. Retry transient cleanup with bounded backoff; record failures without guest data. V1 has no mail queue or calendar polling jobs. No Redis, hosted queue subscription or Hangfire-style infrastructure is required for this scope.

Persist protected Data Protection/receipt-cache keys outside releases; separate signing/encryption purpose scopes, permissions and rotation. Keep auth/client/DB secrets in protected service configuration, never browser assets. Health checks expose basic liveness publicly and detailed database/worker readiness only to the operator. Log safe operation IDs, latency, constraint conflicts, rate rejection and worker lag; no guest contacts, tokens, form bodies or raw OIDC claims.

Use encrypted daily off-VM database backups plus key/config backups with controlled access; recommend a 30-day retention and a database-aware restore rehearsal before launch. Daily-only backups imply a provisional RPO of up to 24 hours and an internal recovery target of one working day after operator access, neither a contractual promise. If lost accepted bookings at that RPO are unacceptable, add tested WAL/PITR before launch or restrict the pilot; provider snapshots alone are insufficient. Owner CSV exports complement backup and incident preparedness, not continuous sync.

Version releases, build away from production where possible, review migrations, back up before a change, apply migrations explicitly with a separate role, then smoke-test public/owner/widget paths and a synthetic reservation. Use additive compatibility where possible; database migrations may prevent binary-only rollback. Never launch with Development mode or test sign-in. Deploying is separately authorized after readiness passes.

Developers/agents sharing the VM need separate OS identities, isolated directories/containers/resources and no access to production database, secrets, key material, backups or deployment privileges. Do not run dependency installs or untrusted agent commands as a production service identity. A shared host does not make development data trustworthy.

Cash cost is shared host allocation plus domain/HTTPS operations, off-VM encrypted backup storage and operator time. No incremental email/analytics provider subscription in v1. Future Brevo and payment/other data-service costs require live quota/pricing/country verification then; no remembered provider price is a launch budget. Do not buy accounts or backup services under planning authorization.

Unavoidable work: security/runtime patching, certificate renewal monitoring, failed jobs/backups, restore drills, uptime/capacity/abuse incidents, data-rights requests and product defects. Property owners must maintain outside inventory, check requests at least daily, contact guests, agree price/policies, and handle cancellation/disputes. The operator must provide a real monitored important-contact route and incident notice; “minimal support” is documentation-first, not zero responsibility. No SLA or guarantee of eliminating all double bookings.

The demo must use a separate synthetic property/data scope with automatic reset and capped fake requests; never expose real guest data or let a marketing demo consume production owner inventory. Disable all future outbound notification/payment hooks in demos and staging.

## 13. SEO and measurement

Own public routes: product homepage, pricing, safe demo, installation, limitations/FAQ, terms/privacy/contact. Render useful distinct HTML, truthful titles/descriptions/H1, canonical origin from trusted configuration, sitemap of actual indexable public routes, favicon and social metadata. No fabricated reviews, demand claims or accommodation listings. English at unprefixed routes; future languages need real equivalents and reciprocal hreflang.

Owner dashboards, guest token/status/action pages, widgets and transaction responses are noindex and excluded from sitemaps. Use headers where HTML meta is unavailable. Do not automatically add `indexifembedded`; there is no intended search-indexed accommodation content to justify it. Robots rules are discovery hints, not authentication; never publish private data and assume robots hides it. Staging uses access protection and noindex; only an authorized launch enables production indexing.

No tracking scripts, visitor cookies, fingerprinting or identity stitching. Minimized counters are a recommendation allowed by the brief, not unique-visitor analytics. Count actual public product-page responses using fixed routes after bot/internal filtering as far as feasible; report **page requests**, not deduplicated people. Operational logs and aggregate measurement still need accurate disclosure and retention; cookie-free does not prove a consent exemption. Guest data/URLs/tokens never enter aggregates.

| Signal | Definition and limits |
| --- | --- |
| Installation | Owner clicks Activate and records installation instructions acknowledged; separate “observed widget loaded” day flag, not a claim to detect every external install or verified domain. |
| Widget views | Server-rendered published widget responses, classified as embed or hosted fallback. Bots/reloads/cache can distort counts; not Funtoolio homepage visitors. |
| Requests submitted | Count committed new request records, never attempts/retries or frontend button clicks. Source is the transaction outcome. |
| Attribution clicks | Optional first-party allowlisted redirect route increments an aggregate then redirects immediately to the fixed public destination; no guest token/referrer parameters and no navigation dependency on counter success. These are clicks, not visits to the destination. |
| Product-page visits | Own server page-request aggregates; Funtoolio landing-page visits belong to that host's actual reporting, not inferred from widget loads. |
| Paid conversions | Zero/not available in v1. Future counted only from trusted settled SaaS billing events, separate from accommodation payments. |

Do not build a dashboard per guest or transmit activity to a Cloud stream. Keep counters bounded and noncritical; failures do not fail enquiries/confirmations. Provide a simple operator aggregate report later only if useful; no customer-tracking product. Prefer acceptance/occupancy correctness over optimizing funnel numbers.

## 14. Deferred integrations

### F1/F2 — iCal

Recommend **omit iCal from the first usable release**. Owners manually add external reservations or date blocks immediately, with prominent signup/install reminders and a last-reviewed inventory prompt. This is unsuitable for an owner unwilling to maintain outside inventory; explain before collecting an account, not only in a technical FAQ. Export-only is the next lower-risk step; import/export is a separate milestone, never a real-time guarantee.

An eventual per-unit private export contains confirmed StayWidget occupancy with stable opaque event UIDs and date-only boundaries, no guest names/phones/prices. Do not export pending requests, imported events or every general manual block by default; keeping originated bookings separate reduces loops. A tokenized feed grants read access, is revocable, sensitive and no-store/noindex. All-day `DTEND` is exclusive; time-zoned/timed feeds need provider-specific interpretation tests. iCalendar describes events; availability meaning and supported fields are provider conventions. Do not infer prices, capacity, identities or complete booking terms from a busy event. [RFC 5545](https://www.rfc-editor.org/rfc/rfc5545).

Future import: CalendarSource and ImportedEvent with source/unit attribution, stable `(source, UID, recurrence instance)` identity, fingerprints, provider-specific date handling, last successful poll and last attempt/error. A complete successful snapshot can reconcile removed/cancelled/changed entries; a failed/partial response cannot erase previous blocks. Handle duplicate UIDs, recurrence limits, changed dates, timezone changes and unsupported event formats explicitly. Source pauses and stale feeds preserve last known blocks with a visible warning rather than claiming fresh availability. A newly imported event conflicting with a confirmed StayWidget booking flags a conflict; it cannot silently cancel or rewrite that booking.

Recommend a later bounded polling interval of 30 minutes with stagger/backoff and manual refresh limits, subject to provider permission/quota. Other sites may also poll exports slowly. Polling, outages and interpretation differences create an unknown-delay window; show last successful sync prominently. Keep exported originated events distinct and tag provenance to avoid reimport/export feedback loops; test integrations where external providers rewrite UIDs. Imported events never receive guest tokens or directly change request decisions.

For every supplied feed/verification URL: HTTPS only, no userinfo, reject localhost/private/link-local/multicast/reserved/metadata IPv4 and IPv6 targets; validate all resolved addresses, redirects and final destinations and constrain actual connection resolution to prevent DNS rebinding. Prefer disabling redirects or a small limit with full revalidation. Enforce egress firewall rules, timeout, max 1 MiB decompressed body, bounded event count/recurrence expansion and parser depth/time; reject content fetching references. No shared ambient cookies/auth headers. Encrypt full private feed URL at rest, hide it after save, redact logs and rotate/revoke. Library URL parsing alone is insufficient. [OWASP SSRF guidance](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html).

### F3 — email, explicitly absent in v1

Only after separate approval, use the portfolio's existing Brevo provider and verified per-product sender; verify account quota, required DNS, country/processor requirements and real inbox delivery then. Do not run a mail server. Milestone adds guest email with typo-correction/verification policy, notifications and a transactional outbox; no mail-specific tables or configuration in v1.

Future events: guest “Request received — pending”, owner new-request notice, guest acceptance/decline, booking cancellation/update. Enqueue durable outbox rows in the same business transaction; workers lease/retry with exponential bounded backoff, distinguish queued/provider-accepted/delivered-if-supported/permanent-failure, expose owner delivery failures and allow controlled resend. Use unique event/recipient/template keys, but acknowledge that SMTP crash/retry can duplicate delivery; do not claim exactly-once inbox delivery. Development sink intercepts all mail; demos cannot send.

Email neither creates nor releases inventory. A committed receipt remains true even if mail fails. Prevent recipient changes/resends from becoming an email relay: no arbitrary subjects/body/recipient lists, caps per property/address, fixed templates and no marketing. Optional pre-submit email verification reduces typos but delays a request and needs its own expiring token; decide explicitly, with no pending inventory hold. Signed status links keep minimal scoped permissions and high-entropy secrets; email scanners performing GET must never cancel.

### F4/F5 — two distinct payment streams

**SaaS subscription:** StayWidget owner purchases product access from the SaaS operator; later follow [portfolio payments skill](../../funtoolio-playbook/skills/funtoolio-payments/SKILL.md), with adopted commercial policy, authenticated ownership, trusted provider events, entitlement updates and explicit purchase consent. Do not add checkout simply to render plan cards. Promotion cutoff alone never creates charges or downgrades.

**Guest accommodation payment:** property owner is the merchant/payee. Compare their own hosted payment links versus a provider's supported connected-merchant model. Verify country/account eligibility, currencies, contractual role, payouts/disputes and fees for the actual operators before selection. Do not assume the SaaS provider can process accommodation guest payments or collect money into the developer account to forward manually. No raw cards stored or emailed.

If payment-backed instant booking/deposits are later selected: explicitly introduce AwaitingPayment/hold state with short expiry and inventory protection, authenticated server payment creation, signed webhook verification, unique provider event IDs, idempotent processing and reconciliation. Never confirm on a browser success redirect alone. Define a race where a verified payment arrives after hold expiry/reallocation: do not force an overlap; put paid-but-unconfirmed into an operator-visible reconciliation/refund path. Define deposits, unpaid balances, cancellation/refund authority, reconciliation jobs and provider outages before a commercial promise. Refund completion and reservation cancellation are separate coordinated states.

## 15. Meaningful verification plan

The table below is the full release acceptance catalog. The M1 subset has now been exercised; [status](implementation-status.md) and [local verification](local-development.md) record the actual results. Remaining checks are proposed, not implied to have passed. Use real isolated PostgreSQL for migrations/exclusion/race tests; no mock/InMemory success can establish inventory correctness.

| Layer | Required checks |
| --- | --- |
| Domain | June 10→12 nights, June 12 turnover 0/1, leap/month/year boundaries, DST and unusual local midnight, min/max stay, capacity, seasonal window, horizon, exact 48-hour/arrival deadline, campaign cutoff. |
| PostgreSQL | Confirm-confirm race, confirm-block/manual-booking/edit race, same-request duplicate decisions, withdraw/expiry-confirm race, constraint enforcement through separate connections, transaction rollback and lock order, active/inactive allocations, unchanged dates after failed edit. |
| Idempotency | Double-click/concurrent submits; lost response after commit; identical payload recovery; same key changed payload rejected; expired encrypted recovery not leaked; retry returns committed result across deadline. |
| Tenant/security | Other owner IDs/routes/exports, composite FK mismatch, guest status enumeration, forged fields/state/entitlement, token expiry/revocation, CSRF and framing policies, XSS/private note/phone leakage, redacted logs and cache/referrer behavior. |
| Widget/browser | Plain HTML and hosted fallback, multiple/repeated loader instances, exact postMessage source/origin/height bounds, unrelated same-origin window messages, blocked cookies/storage, no JS contact fallback, keyboard/date errors, mobile/zoom and calendar list. |
| Plans/feedback | Active-unit quota including restore/re-enable/concurrency, free launch access and post-cutoff continued free behavior, server-controlled branding, Custom enquiry without entitlement, private feedback acknowledgement/dedup and no internal field leaks. |
| Operations | Bounded expiry/retention restart/retry, worker down with correct synchronous expiry, encrypted restore including keys/deletion records, synthetic outage/export workflow, public/private health/log scope, no external sending in demo. |
| Platform launch gates | Published HTTPS HTML site, self-hosted WordPress, exact Wix editor/mobile/ancestor flow; separately verify WordPress.com plan restrictions. Record tested versions, configs and unsupported cases instead of “works everywhere”. |
| Future email | Outbox rollback/retry/lease crash, idempotent business event, potential SMTP duplicate wording, provider rejection/permanent failure, typo correction, token link scanner safety. Not a v1 test requirement. |
| Future calendar/payment | SSRF/private IPv6/redirect/rebinding/compression/recurrence resource limits; stale/partial feeds, duplicates/cancellations/loop handling; webhook signature/replay/order/late-payment reconciliation. Deferred to their milestones. |

Future commands may use `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release` and a selected browser runner **after** a solution, SDK pin and isolated test database exist and implementation is authorized. No fake filenames or executed results are asserted here. Choose exact commands in the implementation README when they can actually run. Pin supported package versions and document required extension privileges before migrations.

## 16. Milestones and bounded future implementation tasks

M1–M5 make the first usable release. Each future task requires explicit authorization. Read this plan/status and applicable instructions, update actual evidence, and stop at that milestone; do not commit, push, provision or deploy unless separately authorized. No repeated request for permission is needed for ordinary work inside an authorized milestone.

### M1 — one complete request-to-confirmation slice (large)

- **Goal/scope:** .NET solution only when authorized; local common-login integration, one property/unit, hosted anonymous request with receipt/status link, owner pending list/confirmation, allocation constraint and unavailable dates. No loader, full calendar, billing, email or sync.
- **Expected components:** Domain date/state rules, Persistence mappings/migration, Web composition/OIDC and minimal property/request/owner pages; unit/integration/browser fixtures and actual README commands.
- **Dependencies:** Supported SDK/provider versions, isolated PostgreSQL and authorized extension/migration setup, separately supplied StayWidget OIDC client registration and issuer availability. Do not modify auth or reuse its credentials silently.
- **Acceptance/tests:** one unit end-to-end; simultaneous conflicting confirmations permit one winner; request remains pending/nonblocking until acceptance; duplicate submission creates one record; forged cross-property access fails; receipt is explicitly pending; deadlines and capability access work; no email/third-party cookies.
- **Future Codex task:** “Implement only M1 from docs/implementation-plan.md. Preserve request-only semantics, common sign-in, cookie-independent guests and the database overlap invariant. Create only M1 components and meaningful real-PostgreSQL tests in an isolated development environment. Record commands/results in README/status. No email, billing, loader, calendar sync, production changes, commit or push.”

### M2 — complete owner inventory lifecycle (large)

- **Goal/scope:** up to five units under launch access, setup/publication, bookable windows/capacity/stay/turnover rules, manual reservations/blocks, edits/cancellation/expiry, accessible calendar/list and safe CSV export.
- **Expected components:** owner feature pages/commands, inventory queries, revisions/activity, bounded expiry worker and retention specification implementation; related browser/integration checks.
- **Dependencies:** M1, agreed no-quote/manual-price confirmation policy and contact/terms fields; test clock and common mutation lock protocol.
- **Acceptance/tests:** booking/block/edit races fail safely; cancellation releases dates atomically; stale edits preserve original dates; no upcoming booking purge; seasonal/turnover changes expose conflicts; guest cancellation requests do not free confirmed inventory; export contains no secrets and resists spreadsheet formulas.
- **Future Codex task:** “Implement only M2 on the verified M1 foundation. Complete owner setup/manual inventory/calendar-list/export and expiry behavior. Preserve tenant/date/overlap rules and original enquiry snapshots. Test races and accessible flows; update status. No new integrations or deployment.”

### M3 — robust embed and guest installation flow (large)

- **Goal/scope:** versioned loader + isolated iframe, direct iframe and hosted fallback, permitted origins/CSP, resize protocol/multiple instances, owner preview/snippet/help, no-email saved status/action links and proportionate guest abuse controls.
- **Expected components:** widget/guest pages, loader/child messaging JS, public DTOs, route-specific headers, installation UI/docs and cross-origin browser fixtures.
- **Dependencies:** M1–M2, selected production-origin configuration model; actual builder smoke tests can remain M5 launch gates, never falsely marked passing.
- **Acceptance/tests:** exact source/origin/payload rejection, repeats do not duplicate iframes, blocked third-party storage still permits request, no guest secrets leak to parent/logs, owner/status pages unframeable, pending withdrawal correct, attribution qualified, request-only/no-email/no-sync limitations visible.
- **Future Codex task:** “Implement only M3. Add isolated embeds and scoped guest capabilities without third-party cookies. Use first-party versioned JS and route-specific framing/CSP. Test adversarial messaging, guest actions and plain-site/mobile fallback. Do not install builder plugins, tracking scripts, email or sync.”

### M4 — public product, commercial defaults and owner feedback (medium)

- **Goal/scope:** useful homepage/pricing/demo/install/limitations/policy drafts, StayWidget theme, public ecosystem footer, Free/Pro/Custom catalog and default promotional Pro, unit/branding entitlements, simple owner feedback/Custom intake, bounded aggregate measurement.
- **Expected components:** public page metadata, trusted origin/sitemap, catalog/quota policy, private simple feedback persistence/form, synthetic demo and daily fixed-event counts.
- **Dependencies:** M2–M3, proposed pricing limits reviewed; operator/contact/market details needed to finalize public policies. No checkout dependency.
- **Acceptance/tests:** server quota/branding enforcement, exact cutoff with continued free access, no charge/downgrade, feedback acknowledgement only after commit and internal fields excluded, public marketing footer absent from widget, no tracking scripts/private sitemap routes, demo cannot mutate real inventory.
- **Future Codex task:** “Implement only M4 using the linked pricing/feedback/design guidance and this product's explicit numbers/exceptions. Keep policies marked draft until real facts are reviewed. Prove entitlements/feedback/privacy and aggregate counting; no provider accounts, checkout, Cloud tracker or deployment.”

### M5 — first usable release readiness (large)

- **Goal/scope:** complete regression/security/mobile/manual QA, supported-platform published-page smoke evidence, operational runbook, proposed Linux publish/config/migration/rollback procedure, local restore rehearsal and measurable resource budget.
- **Expected components:** actual runbooks/checklist, meaningful remaining tests, publish instructions/artifact only if specifically authorized, evidence/status and documented incident responsibilities.
- **Dependencies:** M1–M4; real operator/property policy/contact details, selected hostname/TLS/issuer readiness and host/backup plan for launch. Access to live builder sites or hosting requires that environment's authorization.
- **Acceptance/tests:** full proposed invariant suite passes; tested install matrix with explicit limits; sanitized logs; working owner export/outage instructions; restorable database and keys in disposable environment; no demo/test login enabled in production package; no unverified feature promises.
- **Future Codex task:** “Complete only M5 readiness for StayWidget. Run authorized local/disposable verification, document real results and unresolved launch prerequisites, and prepare reviewable release/rollback instructions. Do not access production or deploy; report the exact evidence needed for separate launch approval.”

### F1 — optional private iCal export (medium)

- **Goal/components:** originated confirmed bookings per unit as privacy-minimized feed; stable UIDs, date boundaries, scoped revocable secrets and owner installation docs.
- **Dependencies:** usable release and explicit feature authorization; provider export consumption verified.
- **Tests/acceptance:** no PII/pending/imported events, exclusive checkout, stable changed/cancelled semantics and revocation; no sync guarantees.
- **Future task:** “Implement only F1 export as specified in section 14, without import jobs or new provider accounts. Verify privacy, dates, tokens and consumer compatibility; update evidence.”

### F2 — optional iCal import/export (large)

- **Goal/components:** bounded fetch/parser worker, CalendarSource/ImportedEvent, source-attributed blocked ranges, stale/conflict UI and reconciliation.
- **Dependencies:** F1 review, explicit provider/date support matrix, outbound egress/SSRF controls and parser license/security assessment.
- **Tests/acceptance:** all SSRF/resource-failure gates; partial failure preserves prior blocks; deterministic duplicate/update/cancel handling; overlap warns without cancelling accepted booking; loops and polling delay documented.
- **Future task:** “Implement only F2 with source separation and strict protected fetching. Do not weaken egress validation for convenience; validate actual permitted providers and races, with no real-time/zero-double-booking claim.”

### F3 — optional Brevo email (large)

- **Goal/components:** email field/policy, transactional outbox, worker/sink, receipt/decision/cancellation templates and owner delivery-status UI.
- **Dependencies:** separate approval, verified sender/account quota and privacy terms; no email sending under local implementation alone.
- **Tests/acceptance:** inventory independent of delivery, exact business-event dedup, transient/permanent failure recovery, no abuse relay, no real mail from demos; actual inbox test only when explicitly authorized.
- **Future task:** “Implement only F3 notifications through the selected Brevo transport with a development sink. Keep business states independent, test retry/failure/privacy, and list sender/delivery prerequisites; do not send externally without authorization.”

### F4 — SaaS billing / first paid commercial release (large)

- **Goal/components:** explicit owner purchase flow, provider adapter, verified webhooks/reconciliation and approved entitlement transition, valid tax/legal/commercial disclosures.
- **Dependencies:** usable-release evidence, provider/operator eligibility, review of price and promotion/free-extension policy; sandbox and live activation separately scoped.
- **Tests/acceptance:** ownership/payment event isolation, valid signed/trusted settlement, no automatic promotion-date billing/downgrade, consistent refunds/cancellation policy, existing reservations safe above limits.
- **Future task:** “Implement only an explicitly selected F4 billing slice using the portfolio payment skill. Preserve existing free access pending rollout; test sandbox behavior and trusted confirmation. No live charges or production activation under sandbox authorization.”

### F5 — guest payments or instant booking (large, separate discovery gate)

- **Goal/components:** validated property-merchant model, optional deposits, inventory holds/expiry, payment reconciliation/refunds and a revised contract/confirmation state machine.
- **Dependencies:** actual country/account/provider research, owner-selected merchant responsibilities and separate approval for B/C booking semantics.
- **Tests/acceptance:** signed/idempotent events, hold-expiry vs payment race, paid-but-unconfirmed remediation, no card storage/developer pass-through money model; operational refund ownership documented.
- **Future task:** “First specify only the chosen F5 provider/merchant and hold/payment state machine; do not implement until approved. Subsequent implementation must protect inventory on late payments and separate SaaS revenue from property guest funds.”

## 17. Six material decisions and approval boundary

These questions are for owner review of a concrete plan, not a prerequisite to preparing it. No response authorizes implementation automatically.

1. **Request confirmation policy:** approve nonblocking pending requests, 48-hour/arrival-day expiry, tomorrow-or-later arrival, external price/terms agreement before operational confirmation, and owner-controlled final cancellation? Recommended yes; changing these affects M1 domain rules.
2. **No-email communication:** approve required name/phone, guest-saved status link and daily owner dashboard checking for the first pilot? Recommended yes for the requested scope; if receipt/decision email is essential, move F3 into release scope explicitly.
3. **Public location:** which origin should serve StayWidget, and should `funtoolio.com/staywidget/` be the real attribution landing page? Recommend a dedicated chosen product subdomain plus the portfolio landing page. Local M1 does not need a public DNS decision; embed/launch does.
4. **Development dependencies:** authorize a dedicated local StayWidget PostgreSQL database and distinct Funtoolio OIDC development client as part of a later M1 task, or supply existing isolated configuration? Recommend dedicated development resources; no sibling credentials or issuer changes inferred.
5. **Offer:** approve Free 1 unit, Pro 5 units with proposed US $9/month standard price, promotional default Pro and no paid checkout yet? Recommended starting hypothesis; preserve the portfolio free-extension/no-auto-downgrade policy.
6. **Pilot/operator scope:** which property market/jurisdiction and first website platform should launch use, and what real operator/important-contact route will be published? Recommend one verified plain HTML or self-hosted WordPress pilot, with Wix separately tested. Required before real guest intake/legal publication, not optional market interviews.

The original planning task stopped for review. The owner subsequently authorized starting M1 and explicitly approved its distinct local auth client; status is the source of truth for actual progress and authorization. Future milestones remain bounded by their scope. The owner subsequently authorized committing and pushing M1 to `inaumoski/stay-widget`. No current authorization covers paid provider accounts, external mail, production access/infrastructure, deployment or PR. M2 is the next recommended implementation task after the M1 preview review.
