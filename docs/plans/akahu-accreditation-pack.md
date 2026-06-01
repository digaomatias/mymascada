# Akahu Accreditation Evidence Pack — MyMascada

> **Purpose:** Assembly guide and evidence index for Akahu app accreditation submission.
> **Date prepared:** 2026-05-15
> **Prepared by:** Rodrigo Leote
> **Authentication bar confirmed:** Read-only (confirmed by Josh at Akahu, 2026-04-03)
>
> Each section maps directly to the Akahu app-review checklist at
> https://developers.akahu.nz/docs/app-accreditation.

---

## 1. Architecture and Data Flow Overview

### What Akahu asks for
A clear explanation of how the application is built, how data flows from Akahu to the end user, and what happens to that data.

### What MyMascada has today
MyMascada is a hosted SaaS personal finance application deployed on Fly.io (API) and Fly.io (Next.js frontend) backed by a managed PostgreSQL database and Redis cache.

**Data flow summary:**
1. A user initiates the Akahu connection from **Settings → Bank Connections** (`/settings/bank-connections`).
2. In hosted OAuth mode the backend generates a cryptographically random state token, stores it server-side via `IOAuthStateStore`, and redirects the user to Akahu's OAuth consent page.
3. After user consent, Akahu returns an authorization code to `GET /settings/bank-connections/callback`. The frontend validates the `state` parameter against the stored value (CSRF protection), then POSTs the code to the backend.
4. The backend (`ExchangeAkahuCodeQuery`) exchanges the code for a user access token via Akahu's token endpoint using HTTP Basic auth (app credentials). The token is **never returned to the browser**. It is encrypted with ASP.NET Data Protection and stored in `AkahuUserCredential` in the database.
5. Subsequent sync operations (manual or webhook-triggered) are executed server-side: the API decrypts the user token, calls Akahu REST endpoints, and writes the resulting transactions and balances into the database.
6. The frontend only ever receives normalized `BankConnection` and `Transaction` DTOs — no Akahu tokens, account IDs, or raw bank data are exposed.
7. Akahu webhooks arrive at `POST /api/webhooks/akahu`. The endpoint verifies the RSA signature before processing any payload.

**Key source files:**
- Backend OAuth flow: `src/Core/MyMascada.Application/Features/BankConnections/Commands/InitiateAkahuConnectionCommand.cs`
- Token exchange: `src/Core/MyMascada.Application/Features/BankConnections/Queries/ExchangeAkahuCodeQuery.cs`
- Token storage model: `src/Core/MyMascada.Domain/Entities/AkahuUserCredential.cs`
- API client (server-side only): `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuApiClient.cs`
- Architecture decision (personal vs OAuth modes): `docs/banking-integration-modes.md`

### Gap
No formal architecture diagram has been produced for submission. A one-page diagram showing the request/response path from user browser → MyMascada API → Akahu API should be created.

### Owner / next step
Rodrigo to draw a simple architecture diagram (draw.io or Excalidraw) and export as PDF or PNG. Attach as `docs/plans/architecture-diagram.pdf`.

---

## 2. OAuth / Token Handling Summary

### What Akahu asks for
Evidence that Akahu user access tokens are handled securely — specifically that they are not exposed to the client browser.

### What MyMascada has today
- **Security issue #223 (closed):** "Akahu access token exposed to frontend browser" — remediated and closed.
- The token exchange is performed entirely server-side. The raw user token is encrypted immediately on receipt using ASP.NET Data Protection (`ISettingsEncryptionService`) and stored in `AkahuUserCredential.EncryptedUserToken`. It is never written to any API response or log.
- Request authentication to Akahu uses `Authorization: Bearer <user_token>` and `X-Akahu-Id: <app_token>` headers, added by `AkahuApiClient.CreateAuthenticatedRequest()`. These headers are constructed server-side per request.
- The OAuth `state` parameter is validated strictly: the callback page checks both that the URL parameter is present AND that it matches the server-stored state before forwarding the code to the backend. If either check fails, the flow is aborted. See `frontend/src/app/settings/bank-connections/callback/page.tsx`.
- Scope is configured via `AkahuOptions.DefaultScopes` (defaults to `ENDURING_CONSENT`). Scope value is logged and stored in `AkahuUserCredential.ConsentScope` for audit.

**Key source files:**
- `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuApiClient.cs` — `CreateAuthenticatedRequest()`, `RevokeTokenAsync()`
- `src/Core/MyMascada.Domain/Entities/AkahuUserCredential.cs` — encrypted storage model
- `frontend/src/app/settings/bank-connections/callback/page.tsx` — state validation

### Gap
A brief regression test confirming the token never appears in browser network traffic has not yet been formally documented. The readiness tracker notes this as a remaining step.

### Owner / next step
Rodrigo to run the hosted OAuth flow in browser dev tools (Network tab), capture a screenshot showing no token in any response, and attach as evidence. Document result in `docs/plans/token-regression-evidence.md`.

---

## 3. Disconnect / Account-Deletion Revoke Behavior

### What Akahu asks for
- The user can disconnect (revoke) their bank connection at any time.
- When a user deletes their account, their Akahu access token is revoked.

### What MyMascada has today

**User-initiated disconnect:**
- Security issue #224 (closed): "No Akahu token revocation on user account deletion" — remediated.
- `DisconnectBankConnectionCommand` (`src/Core/MyMascada.Application/Features/BankConnections/Commands/DisconnectBankConnectionCommand.cs`) calls `IAkahuApiClient.RevokeTokenAsync()` before removing the connection record.
- If revocation fails (e.g., network error), the credential is flagged `IsRevocationPending = true` and queued for background retry via `TokenRevocationRetryJobService`.
- Consent revocation timestamp is recorded in `AkahuUserCredential.ConsentRevokedAt` for compliance audit trail.
- The UI disconnect path: **Settings → Bank Connections → [connection card] → Disconnect**.

**Account deletion:**
- `UserDataDeletionService` (`src/Infrastructure/MyMascada.Infrastructure/Services/UserData/UserDataDeletionService.cs`) runs inside a database transaction and calls `IAkahuApiClient.RevokeTokenAsync()` before deleting `AkahuUserCredentials`. Even if revocation fails, deletion of the stored credentials proceeds, and a warning is logged.
- All bank connections, sync logs, and credentials are deleted as part of the account deletion cascade (steps 9–11 and 32 in `DeleteAllUserDataAsync`).
- Data retention policy: `DATA_RETENTION.md` — "Akahu credentials: Retained while connected; revoked and deleted on disconnect or account deletion."

**Key source files:**
- `src/Core/MyMascada.Application/Features/BankConnections/Commands/DisconnectBankConnectionCommand.cs`
- `src/Infrastructure/MyMascada.Infrastructure/Services/UserData/UserDataDeletionService.cs`
- `src/Infrastructure/MyMascada.Infrastructure/BackgroundJobs/TokenRevocationRetryJobService.cs`

### Gap
End-to-end deletion and disconnect tests have not been formally run and documented for the evidence pack.

### Owner / next step
Rodrigo to run a full delete-account flow in staging, confirm the Akahu token is revoked (check Akahu dashboard or verify subsequent API calls return 401), and document the test result with screenshots in `docs/plans/deletion-revoke-evidence.md`.

---

## 4. Webhook Security and Replay Protection

### What Akahu asks for
Evidence that incoming Akahu webhooks are authenticated and that replay attacks are prevented.

### What MyMascada has today
- Security issue #228 (closed): "No replay protection for Akahu webhooks" — remediated.
- `AkahuWebhookController` (`src/WebAPI/MyMascada.WebAPI/Controllers/AkahuWebhookController.cs`) implements a two-layer protection:
  1. **Signature verification:** Every incoming webhook must carry `X-Akahu-Signature` and `X-Akahu-Signing-Key` headers. The controller calls `IAkahuWebhookSignatureService.VerifySignatureAsync()`, which fetches Akahu's RSA public key from `api.akahu.io/v1/keys/{keyId}` (cached 24 hours by default), reconstructs the expected signature, and returns `false` for any mismatch. Invalid requests receive HTTP 400 before processing.
  2. **Replay protection:** The raw webhook body is SHA-256 hashed. The hash is checked against an in-memory cache keyed by `akahu_webhook_{hash}` with a 24-hour window. A duplicate hash returns HTTP 200 (idempotent) without re-processing.
- Raw response bodies are never logged to prevent incidental exposure of account/transaction data in logs.
- Key ID is validated against a strict format regex before use (max 64 chars, alphanumeric + `-_` only).

**Key source files:**
- `src/WebAPI/MyMascada.WebAPI/Controllers/AkahuWebhookController.cs`
- `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuWebhookSignatureService.cs`
- `src/Core/MyMascada.Application/Common/Interfaces/IAkahuWebhookSignatureService.cs`

### Gap
None identified for the evidence pack. The mechanism is implemented and documented in code. A short plain-English summary suitable for Akahu's reviewer is included above.

### Owner / next step
Include the summary above in the submission. Optionally include a log snippet showing a verified webhook event.

---

## 5. Authentication Model Summary

### What Akahu asks for
A description of the application's authentication model, specifically whether it satisfies the authentication bar for the applicable category (read-only confirmed).

### What MyMascada has today
MyMascada uses JWT-based authentication with HTTP-only refresh tokens. The authentication model:

- **Registration / login:** Email + password (bcrypt hashed) or Google OAuth. No social-only account without password recovery fallback.
- **Sessions:** Short-lived JWT access tokens (Bearer) + long-lived HTTP-only `Secure` refresh tokens. The `Secure` flag issue was addressed in security issue #231 (closed).
- **Password policy:** Strong password requirements enforced at registration. Account lockout after repeated failed attempts.
- **MFA:** Removed from the mandatory path after Akahu confirmed that MyMascada qualifies for the read-only authentication bar (Josh, 2026-04-03). The `TwoFactorEnabled` column was removed in migration `20260326061051_RemoveTwoFactorEnabled`.
- **Rate limiting:** API rate limiting is implemented. CORS policy was hardened in security issue #232 (closed).
- **HTTPS:** All production traffic is served over HTTPS. The refresh token `Secure` flag condition was corrected to always set `Secure` in non-development environments (security issue #231).

**Confirmation needed from Akahu:**
Josh's reply (2026-04-03) invited Rodrigo to send the current authentication process details so Akahu can confirm the design satisfies the read-only bar. This send has not yet been completed and is tracked separately in the Obsidian readiness note `Projects/MyMascada/Integrations/Akahu Accreditation Readiness.md`.

**Key source files / migrations:**
- `src/WebAPI/MyMascada.WebAPI/Migrations/20260326061051_RemoveTwoFactorEnabled.cs`
- Auth middleware and JWT configuration in `src/WebAPI/MyMascada.WebAPI/`

### Gap
Formal confirmation from Akahu that the current auth design satisfies the read-only bar has not been received.

### Owner / next step
Rodrigo to draft a 1-page auth description email to Josh and send. Track response in the readiness tracker.

---

## 6. Privacy Notice

### What Akahu asks for
A public privacy notice that covers Akahu-related data usage — what data is collected via Akahu, how it is used, and reference to Akahu's own privacy policy.

### What MyMascada has today
- **Live privacy page:** `frontend/src/app/privacy/page.tsx` — rendered at `https://mymascada.com/privacy` (last updated February 2026).
- The Akahu section states:
  - _"What is shared: Bank account information and transaction data are synced through Akahu's API."_
  - _"When: Only when you connect a bank account via Akahu."_
  - Links to [Akahu Privacy Policy](https://www.akahu.nz/privacy).
- **Repo policy:** `PRIVACY.md` — mirrors the above for self-hosters.

### Gap
The current privacy notice covers Akahu at a basic level. It does not explicitly mention:
- The read-only access scope (accounts + transactions, 365 days retroactive).
- The duration of access (enduring consent until revoked by the user).
- The user's right to revoke access from within MyMascada settings.

These are details Akahu expects users to understand before granting consent. The privacy notice should be updated with this additional specificity, or the Consumer Information Page (section 8) can carry this detail and be cross-linked from the privacy notice.

### Owner / next step
Update `frontend/src/app/privacy/page.tsx` Akahu section to add scope, duration, and revocation instructions. Update `last updated` date. Link to the Consumer Information Page once it is live.

---

## 7. Data Retention and Deletion Approach

### What Akahu asks for
Evidence that the application has a clear and implemented data retention policy, particularly covering what happens to Akahu-sourced data when a user disconnects or deletes their account.

### What MyMascada has today
- **`DATA_RETENTION.md`** (repo root): formal policy covering all data classes.
  - Akahu credentials: "Retained while connected; revoked and deleted on disconnect or account deletion."
  - Financial transactions: "Retained while account is active; deleted on account deletion."
- **Implementation:** `UserDataDeletionService` performs a full cascade delete of all user data including Akahu credentials in a single database transaction (41 steps). See section 3 above.
- **Automated jobs:**
  - `cleanup-expired-refresh-tokens` — daily at 3:00 AM
  - `cleanup-expired-chat-messages` — daily at 3:30 AM
  - `cleanup-expired-password-reset-tokens` — daily at 3:15 AM
- **Backup retention:** Operator-managed, recommended 30 days. Database backups may contain Akahu-sourced transaction data until the backup window expires.
- **Incident response:** `docs/INCIDENT_RESPONSE.md` covers the scenario of a potential Akahu token compromise and includes NZ Privacy Act 2020 notification obligations.

### Gap
No gap for the evidence pack. The policy and implementation are in place and documented.

### Owner / next step
Include the `DATA_RETENTION.md` link in the submission package. Confirm the 30-day backup window is disclosed in the consumer-facing privacy notice.

---

## 8. Consumer Information Page

### What Akahu asks for
A dedicated public page explaining to users:
- How MyMascada uses their financial account access
- What value they get
- Access scope (what data is read, and for how long retroactively)
- Duration of access
- Akahu's role — with clear Akahu branding/logo

### What MyMascada has today
No dedicated Consumer Information Page exists. The current privacy notice at `/privacy` partially covers Akahu but does not meet the above requirements in full.

### Gap
This page is entirely missing. It is a mandatory accreditation requirement.

### Owner / next step
1. Use the draft content in `docs/plans/akahu-consumer-info-page-draft.md` as the starting point.
2. Build the page at `frontend/src/app/open-banking/page.tsx` (or `/akahu` — confirm URL with Akahu at submission).
3. Obtain the official Akahu logo asset from Akahu for placement where `[INSERT AKAHU LOGO HERE]` is marked in the draft.
4. Link the page from the bank connections settings page and from the privacy notice.
5. The page should be publicly accessible (no login required).

---

## 9. Pen Test Report or Assurance Material

### What Akahu asks for
A recent penetration test report, or an accepted substitute such as SOC 2 Type II or ISO 27001 certification. Akahu explicitly requested the pen test report as part of the submission.

### What MyMascada has today
A formal third-party pen test was conducted (the 10-issue findings set, tracked as GitHub issues #223–#232). All 10 issues are now closed. The audit findings and remediation are evidenced by the closed GitHub issues in the `digaomatias/mymascada` repository.

What is available:
- Closed GitHub security issues: #223, #224, #225, #226, #227, #228, #229, #230, #231, #232
- Summary issue: #233 (informational posture summary)
- `SECURITY.md` — vulnerability disclosure policy
- `docs/INCIDENT_RESPONSE.md` — incident response posture

### Gap
The formal pen test report (the written document produced by the auditor, not just the GitHub issue set) has not been located or attached to this pack. Akahu specifically asked for this document to be sent.

### Owner / next step
Rodrigo to locate the pen test report PDF from the auditor and attach it to the submission. If only the GitHub issue set is available (no separate report document), confirm with Josh whether the issue set plus a written remediation summary satisfies the requirement. Draft a remediation summary covering all 10 issues if needed.

---

## 10. Staging / Test Access Instructions for Akahu Reviewers

### What Akahu asks for
Review access to the application — either a staging environment or a controlled production account — so Akahu's team can verify the user experience end to end.

### What MyMascada has today
- A test user account exists: `test-user@mymascada.local` / `TestPassword123!` (auto-seeded in dev; a parallel test account in staging/production needs to be created).
- The application is reachable at `https://mymascada.com`.
- The bank connections flow is at `/settings/bank-connections`.
- The Hangfire admin dashboard is available at `/hangfire` (requires admin auth).

### Gap
No formal reviewer access package has been prepared. The staging test user would need:
- A pre-configured Akahu connection (or instructions to connect one using Akahu's sandbox).
- Confirmation of the production URL.
- Instructions for the full connect → sync → disconnect → delete flow.

### Owner / next step
Rodrigo to:
1. Create a dedicated `akahu-reviewer@mymascada.com` test account in staging or production.
2. Pre-connect a bank account using Akahu sandbox credentials (if available) or provide step-by-step instructions.
3. Write a one-page "Reviewer Access Guide" covering: login URL, credentials, how to connect a bank, how to disconnect, how to delete the account.
4. Store the guide in `docs/plans/reviewer-access-guide.md`.

---

## 11. INACTIVE / Revoked Account UX

### What Akahu asks for
Users should be alerted when a connected account becomes `INACTIVE` (as reported by Akahu), with a clear path back into the consent flow to re-authorise.

### What MyMascada has today
Partial implementation:
- `SyncStatusIndicator` component (`frontend/src/components/bank-connections/sync-status-indicator.tsx`) renders an "inactive" badge when `connection.isActive === false`. The sync button is disabled for inactive connections.
- `LinkAccountDialog` filters out inactive accounts before presenting them for selection (`accounts.filter(a => a.isActive)`).
- When Akahu sends a `TOKEN/DELETE` webhook, `ProcessAkahuWebhookCommandHandler` marks all the user's Akahu connections as `IsActive = false` and deletes the stored credentials.
- When Akahu sends an `ACCOUNT/DELETE` webhook, the specific connection is marked `IsActive = false`.

What is **not yet confirmed** as implemented:
- A proactive in-app notification or prompt that appears on the dashboard or bank connections page when a connection becomes inactive, prompting the user to re-authorise. The current UX surfaces the "inactive" state on the bank connections settings page, but there is no push notification or dashboard nudge.

### Gap
The INACTIVE state is surfaced in the settings page but there is no proactive alert (notification, dashboard nudge, or email) prompting the user to re-connect. Josh confirmed Akahu does not prescribe exact UX, but expects the controls to be clear and intuitive.

### Owner / next step
1. Confirm whether the existing "inactive" badge on the bank connections page, combined with a re-authorise button, is sufficient for Akahu's reviewers.
2. If not, add a dashboard nudge (the nudge dismissal system already exists in the codebase — `DashboardNudgeDismissals`) for users with inactive connections.
3. Document the final UX decision and include a screenshot in the submission.

---

## Submission-Ready Checklist

| Item | Status | Pointer / Action |
|---|---|---|
| Signed Developer Terms | Unchecked | Rodrigo to complete the Akahu Developer Terms sign-up at https://developers.akahu.nz. Attach signed copy or confirmation email. |
| Privacy Notice (covers Akahu scope + duration + revocation) | Partial | `frontend/src/app/privacy/page.tsx` exists but needs Akahu-specific scope/duration/revocation detail added. |
| Consumer Information Page | Unchecked | Draft in `docs/plans/akahu-consumer-info-page-draft.md`. Needs to be built and deployed at a public URL. |
| Pen test report | Partial | 10 findings all closed (#223–#232). Formal report PDF needs to be located and attached. |
| Architecture diagram | Unchecked | Not yet produced. See section 1. |
| OAuth / token handling evidence | Partial | Code is in place. Token-not-in-browser regression test not yet documented. See section 2. |
| Disconnect revoke evidence | Partial | Code is in place. End-to-end test not yet documented. See section 3. |
| Account deletion revoke evidence | Partial | Code is in place. End-to-end test not yet documented. See section 3. |
| Webhook security summary | Checked | Implementation complete. See section 4. RSA signature verification + 24-hour replay window. |
| Auth model description (for Akahu confirmation) | Unchecked | Draft not yet sent to Josh. See section 5. |
| Data retention policy | Checked | `DATA_RETENTION.md` — policy and implementation complete. |
| Staging / reviewer access package | Unchecked | Not yet prepared. See section 10. |
| INACTIVE account UX | Partial | Badge exists in settings page. No proactive dashboard nudge. Confirm with Akahu whether current coverage is sufficient. |
| HTTPS / transport security | Checked | Production deployment on Fly.io with managed TLS. |
| CORS hardening | Checked | Security issue #232 closed. |
| Dependency vulnerability remediation | Checked | Security issue #225 closed. Ongoing maintenance required. |
| Revocation retry on failure | Checked | `TokenRevocationRetryJobService` — background retry if revoke call fails. |
| Consent audit trail | Checked | `ConsentGrantedAt`, `ConsentRevokedAt`, `ConsentScope` stored in `AkahuUserCredential`. |
