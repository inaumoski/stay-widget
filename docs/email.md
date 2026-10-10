# Guest confirmation email

Implemented follow-up, 10 October 2026. Only owner-approved reservation confirmations are emailed. Pending receipts, owner notifications, rejection, edits and cancellation notifications are not included. Payments remain out of scope. The synthetic public demo sends nothing.

## Guest and owner flow

The real widget/hosted form requires one guest email address, name and phone. Search does not require contact details. Addresses are syntactically checked, not ownership-verified. Guests should check spelling and save their private status link; contact the owner directly if they entered a wrong address. Email is not an identity or ownership key and is never used for marketing.

Owner confirmation commits the reservation, occupied-night allocation and one confirmation-email intent in the same PostgreSQL transaction. Confirmation retries do not create another email. Confirmed nights become unavailable immediately, including turnover gaps; pending enquiries still hold nothing. The visible guest calendar reloads on month/unit/guest changes, visibility return and once per minute when visible and not focused, while complete-stay checks and transactional confirmation remain authoritative.

The message uses the selected sender **noreply@funtoolie.com**, a fixed subject, property/unit, arrival/departure, guest count, agreed EUR total and **the property's configured public owner contact details**. No payment is taken. The owner should enter useful phone/contact instructions in property settings; we do not expose the owner's Google profile/address.

The email links to the existing `/GuestStatus#VIEW.ACTION` capability page. Fragments are absent from HTTP request URLs. Merely opening a link cannot cancel a booking. Guests explicitly request cancellation; owner review and recorded cancellation release dates under property policies. View/action scopes, expiry, revocation, suspension and redaction continue to apply. Anyone holding the complete link can use its permissions; email necessarily discloses it to the recipient and sending provider. Guest data and tokens are excluded from public availability responses, analytics and application delivery logs.

## Configuration

Default `Email:Mode` is `Disabled`; no provider request is made. Local ignored configuration uses `Sink` and an absolute private `.local/email-sink` directory outside webroot. Each sink JSON file is a private development email preview, not proof of delivery. Remove local previews when no longer needed; database retention does not delete sink files or backups.

Production uses the same Brevo provider through its HTTPS transactional API, rather than the SMTP setup described for the earlier portfolio app. This avoids an additional SMTP dependency and exposes explicit provider acceptance/rejection. It does not reuse another product's credentials or sender. Follow the [playbook email guidance](../../funtoolio-playbook/plans/server-setup-and-installation.md#9-email-and-analytics) for verified senders, domain authentication, quota monitoring and real receipt checks. API request shape/authentication follows [Brevo's send endpoint](https://developers.brevo.com/reference/send-transac-email). An **API key** is required for this transport, not an SMTP key.

Configure privately through service environment or a secret store:

```text
Email__Mode=Brevo
Email__Sender=noreply@funtoolie.com
Email__ApiKey=<private transactional API key>
Email__DailyAttemptLimit=200
StayWidget__PublicOrigin=https://staywidget.funtoolie.com
```

Never commit keys or print them in diagnostic output. Brevo mode is refused in Development, and Sink is refused outside Development. Redirects are disabled, the provider endpoint is fixed, HTTP logs are removed and sends have a 15-second timeout. Email options default to the selected sender but do not prove it is verified. Production still requires persistent Data Protection keys and the existing canonical-origin/security configuration.

Before live sending, verify the sender/domain in Brevo, actual account quota shared with other products, API access/outbound HTTPS, canonical link origin, privacy/processor terms, provider retention and tracking/link rewriting settings. No app tracking pixels are added; provider behavior needs a real-inbox inspection, including private-link preservation. Do not infer inbox delivery from acceptance. No provider account, DNS record, credentials or live sender verification was changed during implementation.

## Delivery and recovery

A 10-second worker claims at most one due message per tick. A transaction-scoped PostgreSQL advisory lock serializes claims without holding inventory locks during network I/O. Before contacting the provider it commits an **Unknown** attempt, so a crash after acceptance cannot blindly replay it. A unique request index prevents duplicate confirmation intents. This favors visible manual recovery over duplicate mail; it does not promise exactly-once inbox delivery.

| State | Meaning and action |
| --- | --- |
| Queued | Waiting, sending disabled, a rate-limit retry or daily-budget deferral. Check the configured mode/worker. |
| Accepted | Brevo returned HTTP 201. Inbox delivery/read status is unverified; no delivery webhooks are implemented. |
| LocalPreview | Saved only in the private development sink. |
| Failed | Provider rejected, local link/keys failed, local sink failed or retry limit reached. Contact the guest directly and fix configuration. |
| Unknown | Connection/server failure or interrupted attempt; acceptance may already have happened. Inspect Brevo privately before any deliberate manual sending. Automatic resend is suppressed. |
| Cancelled | Link revoked, property suspended, reservation cancelled/edited, past departure, missing link or queue older than seven days. Contact the guest with current details. |

Owner dashboard request cards show state, safe failure code and attempts. Mode notices distinguish disabled, local sink and production sending. An email failure never releases dates or rolls back a saved reservation. Confirmation snapshots are suppressed if the reservation changes before dispatch; changes after sending must be communicated by the owner, and the private status page remains authoritative.

Only explicit provider HTTP 429 rejection is retried automatically, after 10/20/40/80 minutes, up to five total attempts. Timeouts, server errors and crash outcomes remain Unknown. There is a conservative configurable 1–200 attempts/day application budget (default 200), separate from purchased plans and the actual Brevo shared quota. It may count earlier attempts on a row conservatively; excess work is deferred, not lost reservation intake. No immediate anonymous sending or resend endpoint exists: only an authenticated owner confirmation queues mail, after existing request/network/property limits.

Message recipient/body/link are encrypted in the database queue. Pending requests with an email retain encrypted receipt material until decision/expiry so confirmation after 24 hours can use the original link; submission retry recovery remains limited to 24 hours. Payloads clear after terminal processing; interrupted Unknown payloads and metadata are removed after 30 days by bounded regular maintenance/dispatcher cleanup. Request email redacts with existing guest-contact retention. Private sink files/provider retention/backup deletion need independent operational handling. Reviewed rollback refuses to discard email records; prefer forward fixes and preserve matching database/keys.

Restore rehearsals must keep `Email:Mode=Disabled` and never run a sender against restored production contacts. Monitor Queued age, Failed/Unknown counts, worker health and provider quota; handle guests manually when delivery is unavailable. Live mail and full production operations remain launch verification tasks.
