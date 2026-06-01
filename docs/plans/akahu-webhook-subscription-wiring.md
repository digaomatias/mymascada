# Akahu Webhook Subscription Wiring — Design Doc

**Status:** Proposed
**Date:** 2026-05-15
**Hard deadline context:** Akahu Official Open Banking migration on 24 May 2026 (9 days out).

---

## 1. Current state

### 1.1 Webhook receive path (already in production)

| Component | File | Notes |
|---|---|---|
| HTTP endpoint | `src/WebAPI/MyMascada.WebAPI/Controllers/AkahuWebhookController.cs` | `POST /api/webhooks/akahu`, `[AllowAnonymous]`. Reads raw body (line 51-54), checks `X-Akahu-Signature` + `X-Akahu-Signing-Key` (lines 57-72), enforces replay protection via SHA-256 of body cached for 24 h (lines 75-102), dispatches `ProcessAkahuWebhookCommand` (line 99). Always returns 200 to suppress Akahu retries. |
| Signature verification | `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuWebhookSignatureService.cs` | Registered via typed `HttpClient` at `BankProviderServiceExtensions.cs:56`. |
| Payload DTO | `src/Core/MyMascada.Application/Features/BankConnections/DTOs/AkahuWebhookModels.cs` | `AkahuWebhookPayload` carries `webhook_type`, `webhook_code`, `state`, `item_id`, `updated_fields`, `new_transactions[_ids]`, `removed_transactions`. Constants `AkahuWebhookTypes` and `AkahuWebhookCodes`. **Missing:** no `MIGRATE` or `WEBHOOK_CANCELLED` codes; no `previous_item_id`/`new_item_id` fields. |
| Handler | `src/Core/MyMascada.Application/Features/BankConnections/Commands/ProcessAkahuWebhookCommand.cs` | Dispatches by `webhook_type`. `TryParseUserId(payload.State)` (line 217) already assumes `state` is the user GUID — confirms the contract this doc adopts. Hands off transaction events to `IBankSyncService.SyncAccountAsync` (line 135, 185); marks connections inactive on `TOKEN/DELETE` and `ACCOUNT/DELETE`. |

### 1.2 Subscription client (built, never invoked)

| Method | File | Lines |
|---|---|---|
| `SubscribeToWebhookAsync(appIdToken, userToken, webhookType, state, ct)` | `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuApiClient.cs` | 336-346 — `POST /webhooks` with `{ webhook_type, state }`. **Does not return the created `webhook_id` to the caller.** |
| `UnsubscribeFromWebhookAsync(appIdToken, userToken, webhookId, ct)` | same | 351-358 — `DELETE /webhooks/{webhookId}`. |
| `ListWebhooksAsync(appIdToken, userToken, ct)` | same | 363-378 — returns `AkahuWebhookSubscriptionInfo` records with `Id`, `WebhookType`, `State`. |
| Interface | `src/Core/MyMascada.Application/Common/Interfaces/IAkahuApiClient.cs` | 78-102. |

A repo-wide grep confirms `SubscribeToWebhookAsync` is **only referenced from `AkahuApiClient.cs` and its interface** — no production caller. That is the gap this design closes.

### 1.3 OAuth completion flow

The flow that yields a usable user token (the only time we have what's needed to subscribe) is:

1. `InitiateAkahuConnectionCommand` — generates state, returns auth URL.
2. User consents at Akahu, redirected back with `code` + `state`.
3. **`ExchangeAkahuCodeQuery`** (`src/Core/MyMascada.Application/Features/BankConnections/Queries/ExchangeAkahuCodeQuery.cs`) — exchanges code for token (line 89), upserts `AkahuUserCredential` with the encrypted `app_token + user_token` (lines 100-129), returns the list of selectable accounts. **This is the moment we receive a fresh user token tied to a known `UserId`.** This is the natural anchor for subscription creation.
4. `CompleteAkahuConnectionCommand` — runs once *per Akahu account the user maps to a MyMascada account*. It uses the already-stored credentials; it can be invoked 0, 1, or N times after step 3 (e.g. user has ANZ + Amex behind the same Akahu token and links them in two separate clicks). **Subscribing here would either over-subscribe per account or, if guarded, depend on user reaching this step at all.**

The same `AkahuUserCredential` is also written by `SaveAkahuCredentialsCommand` (Personal-App mode, `src/Core/MyMascada.Application/Features/BankConnections/Commands/SaveAkahuCredentialsCommand.cs`) — credentials supplied directly, no OAuth round-trip. Webhooks should be subscribed here too, since this is the other on-ramp.

### 1.4 Disconnect / delete paths (need updates)

- `DisconnectBankConnectionCommandHandler` (`src/Core/MyMascada.Application/Features/BankConnections/Commands/DisconnectBankConnectionCommand.cs`) — revokes the user token at lines 72-131. **Note:** it revokes the *entire user-level token*, killing every Akahu connection for that user. That is consistent with the per-user (not per-connection) token model documented at `AkahuUserCredential.cs:11-15`. After revocation, all Akahu-side webhook subscriptions for that user are effectively dead, but the rows are still in the local DB unless we clean up.
- `UserDataDeletionService` (`src/Infrastructure/MyMascada.Infrastructure/Services/UserData/UserDataDeletionService.cs`) — revokes the Akahu token at lines 337-361 then deletes `AkahuUserCredentials` (line 364). Same observation: cleanup of webhook subscription records on our side is missing.

### 1.5 Schema state

`AkahuUserCredential` (`src/Core/MyMascada.Domain/Entities/AkahuUserCredential.cs`) holds tokens, consent metadata, revocation-retry tracking. **No webhook-subscription columns or related entity exists.** DbContext mapping at `src/Infrastructure/MyMascada.Infrastructure/Data/ApplicationDbContext.cs:450-465` (unique index on UserId, soft-delete query filter). Latest migration to that table: `20260326062213_AddTokenRevocationTracking.cs`.

### 1.6 Akahu webhook API behaviour (confirmed via Akahu reference)

The `developers.akahu.nz` reference for `POST /webhooks` confirms:

- Required body: `webhook_type` + `state`; auth via `X-Akahu-Id` + `Bearer user_token` (matches `AkahuApiClient.CreateAuthenticatedRequest` already used at line 338).
- Valid `webhook_type` values: **`TOKEN`, `ACCOUNT`, `TRANSACTION`, `PAYMENT`, `TRANSFER`** (latter two not in scope today).
- The `state` field is echoed back verbatim — Akahu explicitly recommends putting "a unique identifier related to your end user" there.
- The reference enumerates these `(webhook_type, webhook_code)` combinations:
  - `TOKEN/DELETE`
  - `ACCOUNT/CREATE`, `ACCOUNT/UPDATE`, `ACCOUNT/DELETE`, `ACCOUNT/MIGRATE`, `ACCOUNT/WEBHOOK_CANCELLED`
  - `TRANSACTION/INITIAL_UPDATE`, `TRANSACTION/DEFAULT_UPDATE`, `TRANSACTION/DELETE`, `TRANSACTION/WEBHOOK_CANCELLED`
- **`ACCOUNT/MIGRATE`** — emitted when an account is migrated from classic to "official open banking" — payload includes `previous_item_id` and `new_item_id`. This is migration-critical (see §4).
- The reference does **not** document idempotency of POST /webhooks. The conservative assumption is that POSTing the same `(webhook_type, state)` again creates a duplicate subscription. The design treats it as non-idempotent and reconciles via `GET /webhooks` (`ListWebhooksAsync` is already implemented).
- Akahu's "Official Open Banking" landing page is silent on whether the webhook subscription set persists across the migration. **Assumption:** subscriptions persist (the API surface is unchanged), but the design includes a reconciliation job that would heal a wipe scenario.

---

## 2. Target state

After **any** path that successfully establishes a valid `AkahuUserCredential` for a user (OAuth exchange in `ExchangeAkahuCodeQueryHandler` OR Personal-App save in `SaveAkahuCredentialsCommandHandler`), the user has exactly one active Akahu subscription per type:

| `webhook_type` | Why we need it | Handled by `ProcessAkahuWebhookCommand` today? |
|---|---|---|
| `TRANSACTION` | DEFAULT_UPDATE = new/changed transactions, INITIAL_UPDATE = first historical pull, DELETE = removals | yes (lines 152-199) |
| `ACCOUNT` | CREATE/UPDATE/DELETE/MIGRATE for balance and lifecycle. Need to add MIGRATE handling. | partially — adds: MIGRATE |
| `TOKEN` | DELETE = user revoked at Akahu side, must mark connections inactive | yes (lines 69-96) |

For each subscription we persist:
- the returned **`webhook_id`** (currently thrown away by `AkahuApiClient.SubscribeToWebhookAsync`),
- the `webhook_type`,
- linkage to `AkahuUserCredential.UserId` (1:N relationship: one credential → up to ~3 subscriptions).

The `state` value we register is **always the user GUID in `"N"` format** (`UserId.ToString("N")`) — matching `ProcessAkahuWebhookCommand.TryParseUserId` (which accepts any `Guid.TryParse`-compatible form).

Disconnect / data-deletion paths tear down the stored subscriptions by calling `UnsubscribeFromWebhookAsync` for each `webhook_id`, then deleting the rows.

A reconciliation job heals drift between MyMascada's stored subscription rows and Akahu's authoritative list (`GET /webhooks`).

---

## 3. Design decisions

### 3.1 Where the subscribe call lives

**Decision:** introduce a dedicated `IAkahuWebhookSubscriptionService` in the Application layer.

- Interface: `src/Core/MyMascada.Application/Common/Interfaces/IAkahuWebhookSubscriptionService.cs`
- Implementation: `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuWebhookSubscriptionService.cs`
- Public surface:
  - `Task EnsureSubscriptionsAsync(Guid userId, CancellationToken ct)` — idempotent: looks up the user's credential, computes required types minus already-persisted-and-confirmed types, calls `Subscribe…` for the missing ones, persists the returned IDs.
  - `Task TearDownSubscriptionsAsync(Guid userId, CancellationToken ct)` — for disconnect/delete; iterates over persisted rows, calls `Unsubscribe…`, deletes rows. Tolerates 404s and HTTP failures.
  - `Task<ReconcileResult> ReconcileAsync(Guid userId, CancellationToken ct)` — calls `ListWebhooksAsync`, compares to local rows, fixes drift.

**Where it's called from:**

| Caller | When |
|---|---|
| `ExchangeAkahuCodeQueryHandler` | After `_credentialRepository.AddAsync/UpdateAsync` succeeds (currently `ExchangeAkahuCodeQuery.cs:101-129`), **before** the accounts list is fetched on line 132. |
| `SaveAkahuCredentialsCommandHandler` | After Add/Update at `SaveAkahuCredentialsCommand.cs:100-117`. |
| `DisconnectBankConnectionCommandHandler` | Right before `RevokeTokenAsync` at `DisconnectBankConnectionCommand.cs:85`, and only when **this disconnect is the user's last active Akahu connection** (because subscriptions are user-scoped, not per-MyMascada-connection). Add a guard: `if (otherActiveAkahuConnectionsCount == 0) await TearDownSubscriptionsAsync(...)`. |
| `UserDataDeletionService` | New step between current step 31 (revoke token) and step 32 (delete `AkahuUserCredentials`) at `UserDataDeletionService.cs:337-364`. Call `TearDownSubscriptionsAsync` first; tolerate errors (subscriptions die when the token is revoked anyway, so this is best-effort cleanup at Akahu). |

**Why not put it directly inline in the OAuth handler?** Three reasons:
1. The same logic is needed from at least four call sites — a service avoids duplication.
2. Easier to unit-test failure modes (3 subscribe calls × success/transient/permanent failure).
3. Lets the reconciliation Hangfire job reuse the same code path.

**Why not a MediatR `INotification` raised after credential save?** Considered but rejected:
- The codebase doesn't currently publish domain events from these handlers — adding the pattern just for this is more disruptive than a single new service.
- The fire-and-forget feel of a notification handler obscures the explicit failure-handling decisions documented in §3.3.
- We want the subscription work to occur inside the same `CancellationToken` and transactional scope as the credential save.

### 3.2 Where the `webhook_id`s get persisted

Three options were considered:

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| New JSONB column on `AkahuUserCredential` storing `{ TRANSACTION: "wh_...", ACCOUNT: "wh_...", TOKEN: "wh_..." }` | No new table, smallest migration | JSONB queries are awkward in EF; can't easily query "all subscriptions of type X"; can't model per-subscription metadata (last-confirmed-at, status); doesn't extend if Akahu adds new types | Reject |
| Columns on `BankConnection` | Co-located with existing connection record | Subscriptions are **user-scoped** in Akahu, not per `BankConnection`. Storing on each `BankConnection` would either duplicate or require designating a "primary" connection — both ugly | Reject |
| **New entity `AkahuWebhookSubscription`** | Models the relationship correctly (`AkahuUserCredentialId` FK, one row per `(userId, webhookType)`); easy to add per-row fields (last_seen, status); reconciliation is a straightforward LEFT JOIN | New table, EF migration | **Adopt** |

**Schema** (`src/Core/MyMascada.Domain/Entities/AkahuWebhookSubscription.cs`):

```text
class AkahuWebhookSubscription : BaseEntity (int id, soft-delete inherited)
    Guid     UserId               (FK semantics; also indexed on AkahuUserCredentialId)
    int      AkahuUserCredentialId
    string   WebhookId            (Akahu "_id", e.g. "whk_xxx") — MaxLength(100), Required
    string   WebhookType          (TOKEN | ACCOUNT | TRANSACTION) — MaxLength(40), Required
    string?  State                (echo of what we registered = user GUID in "N" form)
    DateTime SubscribedAt
    DateTime? LastReconciledAt
    string?  LastReconcileError   MaxLength(500)
```

- Unique index on `(UserId, WebhookType)` (filtered `WHERE IsDeleted = false`) so re-OAuth doesn't silently create duplicates.
- Unique index on `WebhookId`.
- Soft-delete (consistent with the rest of the codebase, e.g. `AkahuUserCredential`).

**DbContext** — add `DbSet<AkahuWebhookSubscription>` plus a configuration block in `ApplicationDbContext.cs` immediately after the existing `AkahuUserCredential` block (around line 466).

**Repository:**
- Interface `IAkahuWebhookSubscriptionRepository` at `src/Core/MyMascada.Application/Common/Interfaces/IAkahuWebhookSubscriptionRepository.cs`.
- Impl `AkahuWebhookSubscriptionRepository` at `src/Infrastructure/MyMascada.Infrastructure/Repositories/AkahuWebhookSubscriptionRepository.cs`.
- Methods: `GetByUserIdAsync`, `GetByWebhookIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteByIdAsync`, `DeleteByUserIdAsync`.

**Required `IAkahuApiClient` change:** `SubscribeToWebhookAsync` currently returns `Task` and discards the response body. It must return the created `webhook_id`. Change signature to:

```csharp
Task<AkahuWebhookSubscriptionInfo> SubscribeToWebhookAsync(
    string appIdToken, string userToken, string webhookType, string? state = null, CancellationToken ct = default);
```

(The response shape `AkahuWebhookSubscriptionResponse` is already parsed on the LIST path at `AkahuApiClient.cs:567-574` — the POST endpoint returns the same envelope, so the implementation change is minimal: parse and return.)

### 3.3 Failure handling during subscription creation

Decision matrix:

| Outcome | OAuth flow visible behaviour | Background remediation |
|---|---|---|
| All 3 subscribes succeed | normal | none |
| 1-2 of 3 fail with a transient error (5xx / 429 / network) | **OAuth completion still succeeds** (user sees their accounts list). Persisted subscription rows reflect what worked. | Reconciliation job (§3.7) retries the missing ones on next run. |
| All 3 fail | **OAuth completion still succeeds.** Log an `Error` with the user GUID. | Reconciliation job retries. |
| Subscribe returns 401/403 (token rejected) | OAuth completion still succeeds (no point failing the OAuth flow after we already got valid accounts back — token must work for `/accounts`; if it doesn't work for `/webhooks` there's an Akahu-side glitch). | Reconciliation job will fail too; record `LastReconcileError`; surface in admin dashboard later. |

**Rationale:** webhooks accelerate freshness — they are not a hard requirement for the product. Polling/manual sync remains a fallback. Failing OAuth completion because a webhook subscription POST timed out would be a UX disaster, and the reconciliation job catches the gap within a day.

**Implementation:** the `EnsureSubscriptionsAsync` method wraps each individual `SubscribeToWebhookAsync` in its own try/catch, logs warnings, never throws. Returns a small result describing successes/failures for observability, but the caller (OAuth handler) does **not** propagate the failures.

### 3.4 Teardown on disconnect / account-delete

`DisconnectBankConnectionCommand` disconnects **one MyMascada↔Akahu mapping**, not the user-level credential. Today it always revokes the user-level token (lines 85-95), implicitly killing all Akahu connections. Given that, the teardown rule is:

- Before revoking the token, call `IAkahuWebhookSubscriptionService.TearDownSubscriptionsAsync(userId, ct)` — this performs `DELETE /webhooks/{id}` for each persisted row, then deletes the rows.
- Tolerate 404 from Akahu (subscription already gone) and transient errors (the token revoke that follows kills the subscription server-side anyway).
- After the local rows are gone, the existing `DeleteByUserIdAsync` call on the credential repository at `ProcessAkahuWebhookCommand.cs:95` (for the `TOKEN/DELETE` webhook path) needs an equivalent purge — the webhook handler should also tear down sub rows when Akahu informs us the token is revoked.

> **Refinement worth surfacing:** the broad "revoke token kills all of this user's Akahu" semantics in `DisconnectBankConnectionCommand` is preexisting. Per-connection-only disconnect would need a more elaborate redesign — out of scope (§6).

`UserDataDeletionService`:

- Insert a new step between step 31 (revoke token) and step 32 (delete `AkahuUserCredentials`):
  ```
  31b. Delete AkahuWebhookSubscriptions (best-effort unsubscribe at Akahu, then ExecuteDeleteAsync local rows)
  ```
- Schema-level: the delete of `AkahuUserCredentials` cascades to `AkahuWebhookSubscription` via the FK — but we do the explicit pass first so we can call Akahu's unsubscribe endpoint while the tokens are still decryptable. After the loop, `ExecuteDeleteAsync` covers anything that survived.

Webhook-driven teardown (`TOKEN/DELETE`): `ProcessAkahuWebhookCommandHandler.HandleTokenEventAsync` (line 69) currently calls `_credentialRepository.DeleteByUserIdAsync(userId, ct)` at line 95. Add a sibling call to delete local subscription rows (Akahu has already cancelled them upstream, so no need to call DELETE /webhooks).

### 3.5 Idempotency on re-OAuth

Multiple ways a user can land on the same path with existing subscriptions:

1. Re-OAuth flow — user clicks "Connect Akahu" again to add another bank network. `ExchangeAkahuCodeQueryHandler` upserts the credential.
2. User saves new Personal-App tokens via `SaveAkahuCredentialsCommandHandler`.
3. Hangfire reconciliation job runs.

`EnsureSubscriptionsAsync` rules:

1. Fetch local `AkahuWebhookSubscription` rows for `(userId)`.
2. Optionally call `ListWebhooksAsync` once (for sanity) — cache result locally.
3. For each of `{TOKEN, ACCOUNT, TRANSACTION}`:
   - **Local row exists AND Akahu list contains a matching `_id`:** skip (already healthy).
   - **Local row exists, Akahu list does NOT contain it:** Akahu lost the subscription (or migration wiped it). Delete the stale local row, then re-subscribe and persist the new row.
   - **No local row, Akahu has a matching `(webhook_type, state)`:** adopt — persist the Akahu `_id` as our row without POSTing again.
   - **No local row, no Akahu row:** POST `/webhooks`, persist.

The `ListWebhooksAsync` call on every OAuth completion adds one extra Akahu request; the trade-off is correctness in the face of token rotation / partial-failure states. This is acceptable — OAuth completion is rare.

When the user has multiple Akahu connections and re-OAuths to add a third, the subscriptions stay scoped to the (single) `AkahuUserCredential`, which is exactly the right shape.

### 3.6 New `ACCOUNT/MIGRATE` handling

Mandatory for the migration deadline. In `ProcessAkahuWebhookCommandHandler.HandleAccountEventAsync` (file `ProcessAkahuWebhookCommand.cs:98-118`), add a `MIGRATE` case:

- Required new payload fields: extend `AkahuWebhookPayload` with `PreviousItemId` (`previous_item_id`) and `NewItemId` (`new_item_id`).
- New `AkahuWebhookCodes.Migrate = "MIGRATE"` constant in `AkahuWebhookModels.cs`.
- Handler: look up `BankConnection` by `(previous_item_id, "akahu")`; update `ExternalAccountId = new_item_id`. Persist. Optionally trigger a sync.
- Also handle `WEBHOOK_CANCELLED` — log and delete the local `AkahuWebhookSubscription` row matching the `state` (so the reconciliation job re-creates it next pass).

> **Cross-reference:** the sibling doc `docs/plans/akahu-migration-impact.md` §4.2-4.3 describes a heavier `MigrateAkahuConnectionCommand` that also rewrites `Transaction.ExternalId`. The webhook handler should enqueue that command rather than do the heavy work inline.

### 3.7 Reconciliation Hangfire job

`AkahuWebhookSubscriptionReconciliationJobService` modeled after `TokenRevocationRetryJobService.cs`:

- Interface: `src/Core/MyMascada.Application/BackgroundJobs/IAkahuWebhookSubscriptionReconciliationJobService.cs`.
- Impl: `src/Infrastructure/MyMascada.Infrastructure/BackgroundJobs/AkahuWebhookSubscriptionReconciliationJobService.cs`.
- DI: register in `BackgroundJobServiceExtensions.cs` alongside the other revocation services (line 47-49).
- Schedule: in `Program.cs` next to the other `recurringJobManager.AddOrUpdate` calls (around line 461) — daily at 04:00 NZ time-equivalent UTC.
- Iterates: every active, non-soft-deleted `AkahuUserCredential` whose `ConsentRevokedAt is null`. For each, call `EnsureSubscriptionsAsync`.

This is the safety net for §3.3 failures.

---

## 4. Migration compatibility (24 May 2026)

| Concern | Impact on this design |
|---|---|
| Webhook subscription API (`POST/DELETE/GET /webhooks`) | Documented as unchanged — auth via `X-Akahu-Id` + `Bearer user_token` still applies. Our `AkahuApiClient` request-building works against both classic and official tokens. |
| OAuth flow shape | A parallel agent is analysing OAuth changes (see `docs/plans/akahu-migration-impact.md`). Our subscription wiring is decoupled — it triggers off `ExchangeAkahuCodeQueryHandler` (the post-token-exchange step) which is the same regardless of whether the upstream auth is "classic" or "official". |
| `state` echo | Confirmed by Akahu docs to remain. We anchor on user-GUID-as-state — survives the migration. |
| `ACCOUNT/MIGRATE` event | **Critical** — must ship before migration day. Without it, every account that migrates from classic to official will keep its old `item_id` locally while Akahu re-emits transactions under the new `item_id`, causing silent sync breakage. Adding the handler (§3.6) covers this. |
| Webhook subscription persistence across migration | Akahu's public docs don't promise persistence. The reconciliation job (§3.7) heals any wipe within 24 h. We can also enqueue a one-time job to run shortly after midnight 24 May to force reconciliation across all users — see implementation plan step F.4. |
| `webhook_type = PAYMENT` / `TRANSFER` | Not used today (we don't initiate payments). Out of scope. |

**Conclusion:** the design is migration-safe **provided** `ACCOUNT/MIGRATE` handling and the reconciliation job ship together.

---

## 5. Step-by-step implementation plan

### Phase A — Schema & domain (single EF migration)

**A.1** Create entity `AkahuWebhookSubscription` at `src/Core/MyMascada.Domain/Entities/AkahuWebhookSubscription.cs` per §3.2 schema.

**A.2** Add `DbSet<AkahuWebhookSubscription>` to `src/Infrastructure/MyMascada.Infrastructure/Data/ApplicationDbContext.cs` (after line 58) and the modelBuilder config block (after the `AkahuUserCredential` block at line 465). Include:
- HasKey on `Id`.
- Required properties.
- Unique index on `WebhookId`.
- Unique filtered index on `(UserId, WebhookType)` where `IsDeleted = false`.
- FK to `AkahuUserCredential` via `AkahuUserCredentialId` with `DeleteBehavior.Cascade` (so user-data deletion cleans up).
- `entity.HasQueryFilter(e => !e.IsDeleted)`.

**A.3** Generate EF migration: `dotnet ef migrations add AddAkahuWebhookSubscription -p src/WebAPI/MyMascada.WebAPI`. New files: `Migrations/202605xx_AddAkahuWebhookSubscription.cs` + `.Designer.cs` and updated `ApplicationDbContextModelSnapshot.cs`.

### Phase B — Repository

**B.1** Interface `src/Core/MyMascada.Application/Common/Interfaces/IAkahuWebhookSubscriptionRepository.cs`:
`GetByUserIdAsync`, `GetByWebhookIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteByIdAsync`, `DeleteByUserIdAsync`. Soft-delete semantics matching `AkahuUserCredentialRepository`.

**B.2** Impl `src/Infrastructure/MyMascada.Infrastructure/Repositories/AkahuWebhookSubscriptionRepository.cs`.

**B.3** DI registration in `src/WebAPI/MyMascada.WebAPI/Extensions/BankProviderServiceExtensions.cs` next to line 40 (`AkahuUserCredentialRepository`).

### Phase C — API client adjustments

**C.1** Update `IAkahuApiClient.SubscribeToWebhookAsync` signature to return `Task<AkahuWebhookSubscriptionInfo>` (file: `src/Core/MyMascada.Application/Common/Interfaces/IAkahuApiClient.cs` lines 78-84).

**C.2** Update implementation in `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuApiClient.cs` (lines 336-346) to parse and return the `AkahuWebhookSubscriptionResponse` envelope (same shape used at LIST endpoint, lines 363-378).

### Phase D — Subscription service

**D.1** Interface `src/Core/MyMascada.Application/Common/Interfaces/IAkahuWebhookSubscriptionService.cs`:
- `Task<EnsureSubscriptionsResult> EnsureSubscriptionsAsync(Guid userId, CancellationToken ct)`
- `Task TearDownSubscriptionsAsync(Guid userId, CancellationToken ct)`
- `Task<ReconcileResult> ReconcileAsync(Guid userId, CancellationToken ct)`

**D.2** Impl `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuWebhookSubscriptionService.cs`. Decrypts credentials (via `ISettingsEncryptionService`), iterates required types `{ Transaction, Account, Token }`, calls `IAkahuApiClient.SubscribeToWebhookAsync(appToken, userToken, type, state: userId.ToString("N"))`, persists via repo. Wrap each subscribe call in its own try/catch per §3.3.

**D.3** DI registration in `BankProviderServiceExtensions.cs`.

### Phase E — Call site wiring

**E.1** Inject `IAkahuWebhookSubscriptionService` into `ExchangeAkahuCodeQueryHandler` (file `src/Core/MyMascada.Application/Features/BankConnections/Queries/ExchangeAkahuCodeQuery.cs`). After the credential upsert at line 129, call `EnsureSubscriptionsAsync(request.UserId, cancellationToken)` (fire and continue — failures already swallowed inside the service).

**E.2** Same wiring in `SaveAkahuCredentialsCommandHandler` (`src/Core/MyMascada.Application/Features/BankConnections/Commands/SaveAkahuCredentialsCommand.cs`) after line 117.

**E.3** `DisconnectBankConnectionCommandHandler` (`src/Core/MyMascada.Application/Features/BankConnections/Commands/DisconnectBankConnectionCommand.cs`): before line 85's `RevokeTokenAsync` call, run `TearDownSubscriptionsAsync(request.UserId, cancellationToken)`. Wrap in try/catch — never block disconnect.

**E.4** `UserDataDeletionService` (`src/Infrastructure/MyMascada.Infrastructure/Services/UserData/UserDataDeletionService.cs`): insert call between current line 354 (successful token revocation) and line 363 (delete credentials). Same swallowing pattern.

**E.5** `ProcessAkahuWebhookCommandHandler.HandleTokenEventAsync` (file `src/Core/MyMascada.Application/Features/BankConnections/Commands/ProcessAkahuWebhookCommand.cs`): after `_credentialRepository.DeleteByUserIdAsync(userId, ct)` on line 95, call the subscription repo `DeleteByUserIdAsync(userId, ct)` to purge stale local rows. (No Akahu DELETE call needed — Akahu cancels its end when revoking the token.)

### Phase F — Webhook handler additions for migration

**F.1** Extend `AkahuWebhookModels.cs`:
- Add `[JsonPropertyName("previous_item_id")] string? PreviousItemId` and `[JsonPropertyName("new_item_id")] string? NewItemId` to `AkahuWebhookPayload`.
- Add `AkahuWebhookCodes.Migrate = "MIGRATE"` and `AkahuWebhookCodes.WebhookCancelled = "WEBHOOK_CANCELLED"`.

**F.2** Add a `case AkahuWebhookCodes.Migrate:` branch in `HandleAccountEventAsync` (after line 110). New private `HandleAccountMigrateAsync` looks up connection by `previous_item_id`, updates `ExternalAccountId = new_item_id`, persists. Enqueues `MigrateAkahuConnectionCommand` (see sibling doc) for the heavy transaction rewrite.

**F.3** Add `WEBHOOK_CANCELLED` handling — log + delete the matching `AkahuWebhookSubscription` row by `(userId, webhookType)`. Reconciliation job will re-create on next pass.

**F.4** Optional one-time backfill: in `Program.cs`, after the existing recurring registrations (around line 470), enqueue a one-time job before/around 2026-05-24 to call `ReconcileAsync` for every active Akahu user. Documented as a deploy-time toggle; do not commit the job-trigger inside the main path.

### Phase G — Reconciliation background job

**G.1** Interface `src/Core/MyMascada.Application/BackgroundJobs/IAkahuWebhookSubscriptionReconciliationJobService.cs` with `Task ReconcileAllAsync()`.

**G.2** Impl `src/Infrastructure/MyMascada.Infrastructure/BackgroundJobs/AkahuWebhookSubscriptionReconciliationJobService.cs`. Pattern after `TokenRevocationRetryJobService` (file `src/Infrastructure/MyMascada.Infrastructure/BackgroundJobs/TokenRevocationRetryJobService.cs`) — create a scope per credential, swallow per-user errors, track success/fail counts.

**G.3** DI registration in `BackgroundJobServiceExtensions.cs:47-49` block.

**G.4** Schedule in `src/WebAPI/MyMascada.WebAPI/Program.cs` around line 461 — `recurringJobManager.AddOrUpdate(...Daily(4, 0))`. Run at 04:00 server time (after the existing 03:45 revocation retry).

### Phase H — Tests

Place under `tests/MyMascada.Tests.Unit/`.

**H.1** `Services/AkahuWebhookSubscriptionServiceTests.cs` — covers:
- `EnsureSubscriptionsAsync_NoExistingSubscriptions_SubscribesAllThreeTypes`
- `EnsureSubscriptionsAsync_OneTypeAlreadySubscribed_SubscribesOnlyMissing`
- `EnsureSubscriptionsAsync_AllPresentInAkahuButNotLocal_AdoptsRowsWithoutPosting`
- `EnsureSubscriptionsAsync_TransactionSubscribeFails_OtherTwoStillPersisted`
- `EnsureSubscriptionsAsync_CredentialNotFound_NoOp`
- `EnsureSubscriptionsAsync_RegistersStateAsUserGuid`
- `TearDownSubscriptionsAsync_CallsDeleteForEachRow_Tolerates404`
- `TearDownSubscriptionsAsync_AkahuUnauthorized_DeletesLocalRowsAnyway`
- `ReconcileAsync_LocalRowMissingAtAkahu_ReSubscribes`

**H.2** Update `Queries/ExchangeAkahuCodeQueryHandlerTests.cs` (existing file): add assertion that `IAkahuWebhookSubscriptionService.EnsureSubscriptionsAsync` was invoked once with `userId`.

**H.3** New `Commands/SaveAkahuCredentialsCommandHandlerTests.cs` — analogous coverage for Personal-App on-ramp.

**H.4** Update `Commands/DisconnectBankConnectionCommandHandlerTests.cs` (or create if missing) — verify `TearDownSubscriptionsAsync` invoked before `RevokeTokenAsync`.

**H.5** New `Commands/ProcessAkahuWebhookCommandHandlerTests.cs` cases:
- `AccountMigrate_UpdatesExternalAccountId`
- `AccountWebhookCancelled_DeletesLocalSubscriptionRow`
- `TokenDelete_AlsoDeletesSubscriptionRows`

**H.6** Update `Services/AkahuApiClientTests.cs` to cover the new `SubscribeToWebhookAsync` return value parsing — currently no webhook subscribe tests exist.

**H.7** `BackgroundJobs/AkahuWebhookSubscriptionReconciliationJobServiceTests.cs` — one happy-path test mirroring `TokenRevocationRetryJobServiceTests` style if such tests exist.

### Phase I — Documentation

**I.1** Update Obsidian `Projects/MyMascada/Integrations/Akahu Bank API.md` line 53 ("Wire webhook subscription into the OAuth completion flow") — mark complete.

**I.2** Add a section to `docs/banking-integration-modes.md` summarising the subscription-on-credential-save behaviour.

---

## 6. Out of scope (explicitly deferred)

- **Per-MyMascada-connection disconnect that *doesn't* revoke the user-level token.** The user-level token shape means today's disconnect always nukes everything; this design preserves that behaviour. A more granular model needs a separate redesign.
- **`PAYMENT` and `TRANSFER` webhook types.** Not used in product; will need a separate decision when MyMascada adds initiated-payments support.
- **Frontend changes.** No UI flow change is required — subscriptions are transparent to the user. The "Connect Akahu" button keeps working as today.
- **Re-subscribing when a user *only* changes their consent scope** (e.g. adds new bank network without code re-exchange). The reconciliation job covers this within 24 h. If we ever need faster, add a hook on `BankConnection.Add`.
- **Webhook delivery latency / SLA monitoring.** No new metric or alert is part of this design.
- **Multi-tenancy of subscriptions** (e.g. shared-account scenarios via `AccountShare`). Each user owns their own credential and subscriptions; sharing only affects view permissions on transactions post-sync.
- **Replacing `IMemoryCache` replay protection with a distributed cache.** Pre-existing; orthogonal.
- **Akahu "official open banking" OAuth-flow changes.** See sibling doc — this design depends only on the existing post-credential-save extension point and is decoupled.
- **Storing per-subscription delivery history.** `AkahuWebhookSubscription.LastReconciledAt` is the minimum needed; a full delivery-log table can come later if observability requires it.

---

## Critical Files for Implementation

- `src/Core/MyMascada.Application/Features/BankConnections/Queries/ExchangeAkahuCodeQuery.cs`
- `src/Core/MyMascada.Application/Features/BankConnections/Commands/SaveAkahuCredentialsCommand.cs`
- `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuApiClient.cs`
- `src/Core/MyMascada.Application/Common/Interfaces/IAkahuApiClient.cs`
- `src/Core/MyMascada.Application/Features/BankConnections/Commands/ProcessAkahuWebhookCommand.cs`
