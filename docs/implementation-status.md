# StayWidget implementation status

Last updated: 2026-10-10

- **Current phase:** Features, local M1.
- **Current milestone:** M1 — one request-to-confirmed-inventory slice.
- **State:** Implemented and verified locally; real Google callback/account flow remains unverified.
- **Authorization:** After the planning task, the owner said “ok start implementation”; work was scoped to M1. The owner also authorized adding StayWidget's distinct local common-login client. This supersedes the earlier planning-only restriction for this milestone and its isolated local database/client setup. The owner subsequently authorized committing and pushing M1 to `https://github.com/inaumoski/stay-widget.git`. No production access, email sending, provider purchases, deployment or PR is authorized.
- **Completed evidence:** Created Domain/Persistence/Web/test projects, explicit inventory migration, one-property/one-unit setup, owner confirmation, anonymous hosted requests and cookie-independent private guest receipt/status. Requests do not hold dates. Confirmation locks property inventory and commits enquiry state, reservation and allocation together, with a GiST overlap constraint as the database backstop. Created a dedicated marked loopback PostgreSQL cluster on port 55439 with independent credentials. Added `staywidget-local` in private auth user-secrets/local app configuration and restarted the confirmed local issuer to apply registration; no auth source or migration changed.
- **Build/test state:** Release build succeeds without warnings. `python tools/run-tests.py`: 16 passed, 0 failed, 0 skipped, against isolated disposable PostgreSQL databases and a Chromium mobile end-to-end fixture. EF reports no pending model changes. Common-login challenge reached the local issuer's sign-in page with exact callback, PKCE/state/nonce, Google requirement and minimal scopes; no external Google login was automated. Desktop/mobile screenshots visually inspected; mobile had no horizontal overflow. See local-development.md for scope and limits.
- **Next action:** Review the local preview and perform the owner's real Google callback check; M2 is the next bounded implementation milestone.
- **Pending decisions:** Public hostname, operator/contact/legal facts and builder compatibility remain launch prerequisites. M1 uses EUR agreement records and one unit; M2 expands owner/manual inventory lifecycle. Full retention, guest cancellation, abuse policy, embedding, plans and feedback are not implemented yet.
- **Repository state:** M1 source and docs tracked in Git on the existing remote main history; authorized publication destination is `https://github.com/inaumoski/stay-widget`. Private settings/data/keys/artifacts ignored. No PR or deployment.
- **Deployment state:** Not deployed; no production access or changes.
- **Adopted playbook:** Read from `../../funtoolio-playbook`, published baseline 0.1.0 plus current working-tree guidance on 2026-10-10. Read-only HEAD was `d3fa107`; this is not a claim that all guidance read is committed at that revision. Skills/references are linked, not installed or copied. No manifest was generated.

| Milestone | Goal | Status | Evidence |
| --- | --- | --- | --- |
| P0 | Product decisions and reviewable plan | Complete | Three Markdown planning artifacts |
| M1 | Thin request-to-confirmed-inventory slice | Implemented locally | 16 tests; real DB overlap/races, mobile guest flow and common-login challenge; real Google callback unverified |
| M2 | Owner setup, calendar and complete manual inventory lifecycle | Not started | Proposed acceptance criteria only |
| M3 | Embedding, guest status, installation and abuse protection | Not started | Proposed acceptance criteria only |
| M4 | Public pages, plans, signed-in feedback and measurement | Not started | Proposed acceptance criteria only |
| M5 | First usable release readiness | Not started | Deployment remains separately authorized |
| F1 | Optional private iCal export | Deferred | Not part of first usable release |
| F2 | Optional protected iCal imports | Deferred | Separate safety and provider gates |
| F3 | Optional Brevo email | Deferred | No mail transport/outbox in v1 |
| F4 | SaaS billing and first paid commercial offer | Deferred | No checkout or charges |
| F5 | Optional guest deposits/payment model | Deferred | Merchant/provider decision required |

The local preview is running at https://localhost:7259/ with the separate local issuer at https://localhost:7443/. Development fixtures are opt-in and loopback-only. M1 completion is not a claim of full product readiness, real Google callback verification, builder compatibility or legal/production approval. See [local setup and evidence](local-development.md).
