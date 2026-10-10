# Install StayWidget

M3 local-preview guide, checked 10 October 2026. This preview is not ready for live accommodation operations. Published WordPress/Wix installation checks remain release tasks.

## Configure and copy

1. Sign in, configure your property/public contact and policies, and add individually named units. Open requests deliberately. Record outside bookings and unavailable dates manually: there is no external calendar sync.
2. Open **Install widget** from the owner dashboard. Enter up to five exact website origins, such as `https://www.example.com`, and acknowledge your installation authority. Include scheme, hostname and nondefault port; www/non-www are distinct. Paths, credentials, queries and wildcards are rejected. Production requires HTTPS. This is owner attestation, not verified domain ownership.
3. Choose a dark accent. White button text must meet 4.5:1 contrast. The current language is English and owner agreement records use EUR; no automated accommodation quote is provided.
4. Leave **Show availability calendar in the widget** selected (the default), or clear it to use date fields/search only. Save installation settings.
5. Copy the generated loader or iframe snippet and keep its visible hosted fallback. Test a published page on mobile, create a synthetic request, save its private status link and complete the owner decision flow. Clear test bookings afterward.

The installation screen supplies real URLs using the trusted canonical `StayWidget:PublicOrigin` configuration and your public property UUID. The examples below use placeholders: replace `APP_ORIGIN` with that configured HTTPS origin (without a trailing slash) and `PROPERTY_UUID` with the installation screen's UUID. Do not paste private guest links or owner URLs into website code.

## Recommended: loader plus isolated iframe

```html
<div data-staywidget="PROPERTY_UUID">
  <a href="APP_ORIGIN/request/PROPERTY_UUID" target="_blank" rel="noopener">Open reservation request page</a>
</div>
<script src="APP_ORIGIN/embed/v1/loader.js" defer></script>
```

The loader inserts an isolated booking iframe and adjusts its height. It supports up to ten containers per page; repeat loading does not duplicate existing frames. Each instance has its own identifier. Messages carry only layout data, with exact origin/source/instance/payload checks and bounded height (320–1,600px). Longer content scrolls within the frame. Neither this protocol nor the parent website receives private guest capabilities. The guest flow does not depend on cookies or browser storage.

The path `/embed/v1/loader.js` is the versioned public contract. It is cached for 300 seconds with revalidation and ETag. Compatible fixes can remain on v1; any future breaking protocol requires a new version and documented migration/support policy before rollout. No v1 end-of-support date has been set.

Your website must permit the application origin under CSP `script-src` and `frame-src`. StayWidget permits only self and the configured, attested ancestor origins on its published widget route. Cross-window messaging uses exact target origins, following [MDN's postMessage guidance](https://developer.mozilla.org/en-US/docs/Web/API/Window/postMessage). All ancestors must satisfy the widget's [frame-ancestors policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/frame-ancestors).

## Direct iframe and hosted fallback

```html
<iframe src="APP_ORIGIN/w/PROPERTY_UUID"
        title="Reservation requests" width="100%" height="860"
        referrerpolicy="no-referrer"
        sandbox="allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox"></iframe>
<a href="APP_ORIGIN/request/PROPERTY_UUID" target="_blank" rel="noopener">Open reservation request page</a>
```

A direct iframe uses a fixed outer height with internal scrolling. Adjust its container in the website builder. Core search, submission and receipt rendering work without JavaScript; the private status/action page requires JavaScript. Status links open in a new tab because private pages are intentionally unframeable. If a builder blocks frames, scripts or popup links, offer the hosted request page as an ordinary link. The loader resizes only its own iframe, not a website builder's outer container.

## Platform checks

| Platform | Mechanism | Actual evidence and limits |
| --- | --- | --- |
| Plain HTML | Loader or direct iframe | Verified with local cross-origin Chromium fixtures, two instances, mobile and JavaScript-disabled direct-iframe submission. Actual customer-site installation remains a launch check. |
| WordPress | Permitted Custom HTML block | Real published installation unverified. Plan, hosting activation, permissions and plugins may restrict scripts/iframes. WordPress.com's official guide restricts these tags to eligible paid plans with activated hosting; check the customer's actual environment. |
| Wix | Supported Embed HTML or Embed a Site | Real published installation unverified. HTTPS, outer container height, popup support and every nested ancestor origin require verification. Opaque sandbox origins are unsupported. |
| Other builders | Generic snippet or hosted link | No compatibility promise. Test published output, not just editor preview. |

Provider guidance checked 10 October 2026: [WordPress Custom HTML](https://wordpress.com/support/wordpress-editor/blocks/custom-html-block/) and [Wix embed mechanisms](https://support.wix.com/en/article/wix-editor-embedding-a-site-or-a-widget). These references do not establish compatibility with every account or plan. No native plugin, marketplace app or duplicate inventory engine exists in v1.

## Available nights and owner date control

In **Units and seasons**, set the first bookable night and exclusive season end for each unit. **Use calendar year** fills January 1 through January 1 of the next year. **Use November–December** fills November 1 through January 1 of the next year, including December 31. Choose the year, review the dates and save; presets do not save automatically. There is one open window per unit. Guests can request arrivals from tomorrow through the next 365 days in the property timezone.

Use **Add unavailable dates** to block a night or range inside that window. For November 5 only, block November 5 → November 6. Remove or edit the block to reopen those nights. Confirmed reservations and turnover gaps remain unavailable; changing seasons or removing a block does not cancel a booking.

The guest calendar shows **A = Available night** and **× = Unavailable night** for the selected unit and guest count. Choose a green night to fill check-in, then review check-out and check the complete stay. Minimum/maximum stay and full inventory checks still apply. Check-out is excluded from occupied nights, so an unavailable night may still be a valid departure date. Pending requests do not hold nights; outside bookings are unknown until recorded manually. The calendar exposes no guest details, block reasons or booking sources. Visible calendars refresh once a minute when not focused, and when the page becomes visible; confirmation blocks inventory immediately.

The calendar appears in the widget and hosted request page when enabled. Hiding it does not change inventory or disable availability search. If loading fails, retry or use the date fields; without JavaScript, use date fields and submit/search. The embedded public demo includes a synthetic calendar with a sample blocked range and stores nothing.

## Requests and private links

The public `/Demo` page now embeds a working synthetic widget through the same v1 loader using `data-staywidget="demo"`. Its `/demo-widget` frame is self-origin only, noindex and stateless: dates/guest count are validated, contact details are not collected, and simulated request/owner confirmation writes no database records or guest cookies. The page shows a copyable demo snippet and hosted fallback. Customer websites must use their own generated property UUID and approved origins; the demo identifier does not permit external framing or grant real inventory access.

“Request received” means pending, not confirmed. Pending requests do not reserve dates. The owner must check the dashboard daily and contact guests using the supplied phone details; guest confirmation email requires configured Brevo delivery; pending receipts and owner notifications are not emailed. Guests must save the private link shown on the receipt. It grants access to private information: anyone with the complete link can use its scoped permissions.

New private links carry independent view/action capabilities in the URL fragment. The guest page offers a view-only link when sharing status without action access. Older links remain view-only. Withdrawal requires explicit confirmation and applies only while pending. A cancellation request flags a confirmed reservation for owner review; it does not release dates. The owner can keep or cancel a reservation and revoke guest links. Links also expire; suspension and data redaction prevent access. GET requests never perform these actions.

Admission safeguards currently allow five new valid attempts per network per ten minutes and 100 new requests/property/UTC day, with additional global/resource bounds. A rejection shows retry/contact advice and preserves form input. Previously saved requests and exact receipt retries remain available; these are safety limits, not purchased credits. No tracking scripts are added.

## Troubleshooting and release boundaries

- **Frame blocked:** check the exact published origin, redirects between www/non-www and every nested ancestor. Owner/admin/private status routes cannot be embedded. Verify the property is published and not suspended.
- **Script removed or CSP error:** use a permitted HTML block, allow the exact application origin in your site's CSP, or use the direct iframe/hosted link. Do not weaken owner-page framing protection.
- **Cut-off content:** adjust the builder's outer height, permit internal scrolling and test mobile. Loader height has a deliberate upper bound.
- **Submission interrupted:** retry the same form submission. Stable request keys prevent duplicate saved requests; preserve the receipt link. A displayed availability result is not a reservation.
- **Missing notification:** confirmation email is queued only after owner approval when sending is enabled. Check the owner dashboard email state and use the private status page/property contact. Pending receipts, owner notifications and later changes are not emailed. See [email setup](email.md).

Widgets display two visible branded attribution links, combined `rel="sponsored noopener"` for new tabs, using the selected product and playbook portfolio destinations. This follows [Google's sponsored-link qualification guidance](https://developers.google.com/search/docs/crawling-indexing/qualify-outbound-links). These are not guaranteed SEO backlinks. No unrelated ecosystem promotions are injected into customer pages. Pro owners can remove branding through Plan and usage; the server omits markup. Free always displays it. Attribution now uses an allowlisted first-party redirect to the selected product origin https://staywidget.funtoolie.com and the playbook portfolio homepage. No guest token/referrer is added.

Before live intake: select the public origin, verify real Google login and actual installation platforms, supply operator/privacy/property-policy facts, verify off-server recovery and remaining release gates. See [implementation status](implementation-status.md) for actual evidence and unresolved work.
