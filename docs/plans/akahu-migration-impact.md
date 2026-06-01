# Akahu Classic → Official Connection Migration: Impact Analysis & Remediation Plan

**Date:** 2026-05-15 — **9 days** until ANZ/ASB/BNZ/Westpac classic connections are revoked (EOD 2026-05-24).

---

## 1. Executive summary

Akahu has migrated to "official open banking" connections for ANZ, ASB, BNZ, and Westpac (launched 2025-12-01; Akahu accredited as an intermediary 2025-12-19). On **end-of-day 2026-05-24** classic connections for those four banks will be revoked. After that, any MyMascada user with an Akahu-linked big-4 account that has not re-authorised will see sync break with `401 Unauthorized` and (eventually) `404 Not Found` on the persisted `acc_xxx` IDs.

The single sentence that drives almost the entire remediation is from Akahu's migration docs:

> "All account and transaction identifiers will change due to the migration. […] Akahu will create a new record for each account; these account records will each receive a new `_id`. For each account that is migrated, Akahu will copy a maximum of 1 year's transaction history. Each copied transaction is assigned a new `_id` and a `_migrated` field that references the `_id` of the original transaction."

What that means concretely for MyMascada:

1. The `acc_xxx` value MyMascada has stored in `BankConnection.ExternalAccountId` (and in the encrypted `AkahuConnectionSettings.AkahuAccountId` blob inside `BankConnection.EncryptedSettings`) will no longer resolve. Every `GET /accounts/{id}` and `GET /accounts/{id}/transactions` call for a big-4 account will 404 once the classic connection is revoked.
2. Every Akahu-sourced transaction MyMascada has imported has `Transaction.ExternalId = trans_xxx` (classic). The dedup pipeline in `ImportAnalysisService.DetectConflictsAsync` looks up duplicates by `candidate.ExternalReferenceId == existing.ExternalId`. When Akahu re-emits the same transaction with a new `_id` after migration, this exact-match check will not match — every imported transaction within the 1-year window risks being treated as new, producing massive duplication unless we either (a) pre-migrate stored IDs using Akahu's `_migrated` field or (b) reinforce the secondary date+amount duplicate detection.
3. The OAuth/Personal-App tokens themselves remain valid (the docs imply re-authorisation is required only to upgrade a classic connection to official — the user has to "re-select which of their accounts they wish to share"). However, until they perform that upgrade flow per-user, MyMascada has no way to obtain the new `acc_xxx`/`trans_xxx` IDs. So both code work (correlation logic) AND user action (re-auth) are required.
4. The `AkahuUserCredential` model is per-user not per-bank. Akahu's official open-banking model still appears to be per-user (one user access token covers all migrated `conn_xxx`), so the entity model can stay; only the contents (re-auth metadata, possibly new scope strings) and consent timestamps need updating.

Rodrigo's own dev/test connections will also need re-authorisation in the dev environment (5-user cap; provided by Josh on 2026-03-10) before he can validate the migration code path. Plan accordingly: do not deploy correlation code without first being able to compare a real before/after `GET /connections` and `GET /accounts` response.

---

## 2. What Akahu's docs actually say

Sources read with WebFetch:
- https://developers.akahu.nz/docs/official-open-banking-migration (primary)
- https://developers.akahu.nz/docs/official-open-banking
- https://developers.akahu.nz/docs/official-open-banking-feature-support-accounts
- https://developers.akahu.nz/docs/authorizing-with-oauth2
- https://developers.akahu.nz/docs/scopes

### 2.1 Connection-level migration shape

The migration doc describes a transition mode in which `GET /connections` returns both the classic and the official entry for each big-4 bank. The official entry carries a `_classic` attribute that points back to the old `conn_xxx`:

> "`_classic`: `conn_cjgaaozdo000001mrnqmkl1m0` (the classic connection's ID)"

There is also a table comparing "Classic ID" vs "Official ID" behaviour across "Current, Strict, Migration, and Developer modes" — the explicit `connection=conn_xxx` query parameter on the authorisation URL lets the developer direct the user to one or the other.

**Implication:** during the migration window, MyMascada can call `GET /connections` for any user and detect whether a given user has a classic-vs-official pair for ANZ/ASB/BNZ/Westpac. That's how we drive an in-product "Upgrade required" UX.

### 2.2 Account-level migration

> "Match each new account with its predecessor and assign a special `_migrated` parameter to the new account record containing the `_id` of the account's predecessor."

So a migrated account looks like:

```json
{
  "_id": "acc_NEWxxxxxxxxxxxxxxxxxx",
  "_connection": "conn_OFFICIAL_xxx",
  "_migrated": "acc_OLDxxxxxxxxxxxxxxxx",
  "name": "Everyday",
  "formatted_account": "01-0000-0000000-00",
  "type": "CHECKING",
  "status": "ACTIVE",
  "balance": {...}
}
```

**Implication:** after the user re-auths, MyMascada can call `GET /accounts` once and build a `{old_acc_id -> new_acc_id}` mapping from the `_migrated` field. That's the per-user migration step that updates `BankConnection.ExternalAccountId` and `AkahuConnectionSettings.AkahuAccountId`.

### 2.3 Transaction-level migration

> "For each account that is migrated, Akahu will copy a maximum of 1 year's transaction history. Each copied transaction is assigned a new `_id` and a `_migrated` field that references the `_id` of the original transaction."

For removed transactions during migration:
> "This webhook event will include both the transaction's `_id` and `_migrated` in the `removed_transactions` array."

**Implication:** if MyMascada wants to preserve transaction history continuity (recommended — otherwise budgets, reconciliations, categorisation history all break) we have two options:

- **Option A (preferred): in-place ExternalId rewrite.** Iterate transactions on the migrated account, fetch the new transactions (which carry `_migrated`), build a `{old_trans_id -> new_trans_id}` map, and `UPDATE Transactions SET ExternalId = new WHERE ExternalId = old`. After this, the existing dedup pipeline keeps working.
- **Option B (lazy/eventual): leave old IDs; rely on date+amount duplicate detection.** Cheap to implement but `ImportAnalysisService.DetectConflictsAsync` currently uses `DateToleranceDays = 0` and `AmountTolerance = 0` for bank-API imports (see `BankSyncService.cs:138-143`), and Akahu's docs warn:
> "It is possible that there will be some changes in transaction data sourced using official connections. You may notice differences in the `date`, `type`, and format of `description`."
  So zero-tolerance won't catch them. Option B as-is = duplicates everywhere.

We will recommend **Option A**, with Option B's tolerance loosened as a safety net during the migration window.

### 2.4 The MIGRATE webhook

> "A single `MIGRATE` event will be emitted, containing the `previous_item_id` and `new_item_id` for the migrated account."

The docs do not fully specify the JSON envelope shape, but inferring from existing Akahu webhook conventions (cf. `AkahuWebhookPayload.cs`), expect something like:

```json
{
  "webhook_type": "ACCOUNT" or "MIGRATE",
  "webhook_code": "MIGRATE",
  "item_id":  /* see below */,
  "previous_item_id": "acc_OLDxxxx",
  "new_item_id":  "acc_NEWxxxx",
  "state":    "<user-guid-we-set-at-subscription>"
}
```

**Open question for Akahu (Q1):** confirm the exact `webhook_type` / `webhook_code` strings and whether `state` is delivered as set during the user's webhook subscription.

### 2.5 Re-auth flow

> "[Users] will be given the option to upgrade … This upgrade requires that the user … completes a new authorisation using the official open banking connection for their bank. […] re-select which of their accounts they wish to share."

So the developer-side flow is:
1. Send the user through `GET https://oauth.akahu.nz/?response_type=code&client_id={app}&redirect_uri=...&scope=ENDURING_CONSENT&state=...&connection=conn_OFFICIAL_xxx` (the `connection` param targets a specific bank).
2. Receive code at the existing redirect URI.
3. Exchange via `POST /v1/token` — same shape MyMascada already uses (`AkahuApiClient.ExchangeCodeForTokenInternalAsync`).
4. Persist the new access token; old token can be revoked or left to expire.
5. Call `GET /accounts` to receive migrated accounts with `_migrated`.

Crucially: the `ENDURING_CONSENT` scope is the same. Per https://developers.akahu.nz/docs/scopes, there's no special "official" scope — `ENDURING_CONSENT`, `ACCOUNTS`, `TRANSACTIONS` are still the relevant ones, plus optionally identity scopes. So the existing `AkahuOptions.DefaultScopes = ["ENDURING_CONSENT"]` is correct.

### 2.6 Per-user vs per-bank consent

Akahu's official open-banking model under their intermediary accreditation still issues a single user-access-token. The user re-selects accounts at each official-bank authorisation, but the resulting token shape (returned at `/v1/token`) is the same. So **MyMascada does not need to migrate `AkahuUserCredential` to per-bank-connection**. We can leave the entity as one-row-per-user.

What we DO need is per-bank-connection consent metadata (`ConsentGrantedAt`, `ConsentCorrelationId`, `ConsentScope`) for each bank a user has officially authorised — because a user could end up with one official connection at ANZ on day X and another at ASB on day Y. The current `AkahuUserCredential` writes those columns at the *user* level which is incorrect once we have multiple official connections per user. (See section 4.6.)

### 2.7 Feature parity

From the feature-support page, ANZ/ASB/Westpac support transactional, savings, loans, credit cards, term deposits; BNZ matches except for term deposits. None support managed funds / KiwiSaver / FX. **Credit cards: ANZ/ASB/BNZ restrict data sharing to the account holder** — a meaningful regression if any MyMascada user was using a joint-cardholder Akahu link before.

### 2.8 What the docs DO NOT say (open questions for Akahu — Q2-Q8)

- **Q2:** Exact `webhook_type` and `webhook_code` strings for the `MIGRATE` event.
- **Q3:** Will `GET /connections` continue to return BOTH classic and official entries between now and the deadline, and what does it return after 2026-05-25 (only official? official with `_classic` still populated?)
- **Q4:** Will the old `acc_xxx`/`trans_xxx` resolve to the new IDs server-side after revocation, or is `GET /accounts/{old}` going to 404 immediately?
- **Q5:** Is the per-connection authorisation URL parameter `connection=conn_xxx` or is it the classic connection ID that triggers the upgrade prompt?
- **Q6:** When a user upgrades for one bank but not another, do they retain access to both via the same user-access-token, or is the previously-issued token invalidated and a new one issued per-upgrade?
- **Q7:** What is the maximum overlap window — can MyMascada perform a sync against BOTH classic and official for the same user during a 24-48h transition to verify the `_migrated` mapping before deleting old IDs?
- **Q8:** Is the dev-environment (Josh's 5-user-cap sandbox) wired up for "official" connections for all four banks already, or only some? (Needed before we can test the code in section 4.)

We should send these to hello@akahu.nz today, copying Josh, and ask for written confirmation by Tue 2026-05-19 latest so we have a buffer.

---

## 3. Code audit — every place that persists or matches on Akahu IDs

All paths below are relative to the repository root.

### 3.1 Persisted identifiers (database)

| Field | Entity | File | Concern |
|---|---|---|---|
| `BankConnection.ExternalAccountId` (varchar(100), indexed) | `BankConnection` | `src/Core/MyMascada.Domain/Entities/BankConnection.cs:40` | Stores `acc_xxx`. Used for webhook routing (`GetByExternalAccountIdAsync` in `ProcessAkahuWebhookCommand.cs:128, 140, 177, 201`) and dedup of "already linked" Akahu accounts (`CompleteAkahuConnectionCommand.cs:92`). Must be rewritten to new `acc_xxx` per user. Index defined in `src/Infrastructure/MyMascada.Infrastructure/Data/ApplicationDbContext.cs:419-420`. |
| `BankConnection.EncryptedSettings` (encrypted JSON of `AkahuConnectionSettings`) | `BankConnection` | `src/Core/MyMascada.Domain/Entities/BankConnection.cs:34`; settings class at `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuSettings.cs:8-24` | Stores `AkahuAccountId` (same `acc_xxx` redundantly) and `LastSyncedTransactionId` (a `trans_xxx` for incremental sync — currently appears unused, but worth confirming). Decrypted and consumed in `AkahuBankProvider.GetSettings` (`AkahuBankProvider.cs:233-240`) and `AkahuBankProvider` uses `settings.AkahuAccountId` as fallback for the `ExternalAccountId` (`AkahuBankProvider.cs:46, 92, 135, 179`). Must be rewritten alongside `ExternalAccountId`. |
| `Transaction.ExternalId` (varchar(100)) | `Transaction` | `src/Core/MyMascada.Domain/Entities/Transaction.cs:50` | Stores `trans_xxx` for every Akahu-imported transaction. Used by `ImportAnalysisService.DetectConflictsAsync` exact-duplicate path (`src/Core/MyMascada.Application/Features/ImportReview/Services/ImportAnalysisService.cs:381-382`), by `ImportUnmatchedTransactionsCommand` (`...:204-225, 266`), by `BulkApproveMatchesCommand` (`...:126-130`), and by the webhook DELETE handler (`ProcessAkahuWebhookCommand.cs:198` → `ITransactionRepository.DeleteByExternalIdsAsync`). All matching breaks post-migration unless we rewrite. |
| `ReconciliationItem.BankReferenceData` (JSON blob) | `ReconciliationItem` | populated in `src/Core/MyMascada.Application/Features/Reconciliation/Services/AkahuTransactionMapper.cs:41-65` | Embeds `BankTransactionId = source.ExternalId` (the old `trans_xxx`). Re-parsed in `ImportUnmatchedTransactionsCommand.cs:192-225` and `BulkApproveMatchesCommand.cs:184` to dedup imports. Open reconciliations that span the migration cut-over will have stale IDs in their item JSON. Either (a) close out reconciliations before re-auth, or (b) rewrite the embedded JSON too. |
| `AkahuUserCredential.EncryptedAppToken`, `EncryptedUserToken` | `AkahuUserCredential` | `src/Core/MyMascada.Domain/Entities/AkahuUserCredential.cs:29, 36` | OAuth/Personal access tokens are *not* invalidated by the migration per se (docs imply tokens persist; only the underlying classic connection inside Akahu is revoked). But the user MUST go through a fresh authorisation to migrate accounts. After re-auth, `ExchangeAkahuCodeQuery` rewrites these tokens (`ExchangeAkahuCodeQuery.cs:96-129`). |
| `AkahuUserCredential.ConsentScope`, `ConsentGrantedAt`, `ConsentCorrelationId`, `ConsentRevokedAt` | `AkahuUserCredential` | same file, lines 53-69 | Currently one row per user; once a user has multiple official connections, the semantics get muddled. (See 4.6.) |

### 3.2 Code paths that produce or compare Akahu IDs

These are the call sites that will silently break or misbehave the moment a user's classic connection is revoked.

**Sync — main hot path:**
- `BankSyncService.SyncAccountAsync` (`src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/BankSyncService.cs:60-266`) → calls `provider.FetchTransactionsAsync(config, from, to, ct)` where `config.ExternalAccountId` is the old `acc_xxx`. `AkahuBankProvider.FetchTransactionsAsync` then calls `GET /accounts/{accountId}/transactions` (`AkahuApiClient.cs:247`). Post-revocation → 404 from Akahu, mapped to `AkahuApiException` (`AkahuApiClient.cs:411`), surfaced in sync log as `LastSyncError`.
- The dedup pipeline (`BankSyncService.cs:139-143`) uses `DateToleranceDays = 0` and `AmountTolerance = 0` — meaning if a user re-auths and we keep the old `Transaction.ExternalId`s, every freshly re-imported transaction within the migration's 1-year copy window will be inserted again. **This is the primary data-corruption risk.**

**Reconciliation:**
- `CreateAkahuReconciliationCommand` (`src/Core/MyMascada.Application/Features/Reconciliation/Commands/CreateAkahuReconciliationCommand.cs:66-346`) builds a `BankConnectionConfig` with `connection.ExternalAccountId` (line 363) and calls `provider.FetchTransactionsAsync`. Same failure mode.
- `AkahuTransactionMapper.CreateBankReferenceData` (`...:41-65`) embeds the old `trans_xxx` into the JSON stored on `ReconciliationItem.BankReferenceData`.
- `ImportUnmatchedTransactionsCommand` (`...:204-225`) reads `bankData.ExternalId` from that JSON and looks it up in `Transaction.ExternalId` via `_transactionRepository.GetByExternalIdAsync(bankData.ExternalId)` — works against pre-migration data; the moment we rewrite ExternalIds, in-flight reconciliations with stale embedded IDs break.

**Webhooks:**
- `ProcessAkahuWebhookCommand.HandleAccountUpdateAsync` (`...:120-136`) looks up the connection by `payload.ItemId` (the Akahu `acc_xxx`). If Akahu sends webhooks against the new account ID and we haven't migrated `BankConnection.ExternalAccountId`, every webhook-driven sync silently drops with "No bank connection found for Akahu account…".
- `HandleAccountDeleteAsync` (`...:138-150`) marks the connection inactive on `DELETE`. We should verify Akahu does NOT send a spurious DELETE for the classic account when it auto-migrates (per docs, the classic account "stops syncing" rather than being deleted — but worth confirming with Q3).
- `HandleTransactionDeleteAsync` (`...:188-199`) deletes by `payload.RemovedTransactions`. The docs say the array will contain both `_id` and `_migrated` so MyMascada will receive old `trans_xxx` values for transactions removed. The current code passes the whole array to `DeleteByExternalIdsAsync` — which is correct (it will delete any matching), but if we've already rewritten ExternalIds to the new values, the old IDs won't match and the delete is a no-op. Likely harmless, but worth thinking about ordering.
- **No `MIGRATE` webhook handler exists.** `AkahuWebhookTypes` (`src/Core/MyMascada.Application/Features/BankConnections/DTOs/AkahuWebhookModels.cs:56-61`) declares only `TOKEN`, `ACCOUNT`, `TRANSACTION`. `ProcessAkahuWebhookCommand.Handle` (`...:49-67`) defaults to "Unknown webhook type" for anything else. **This is one of the few must-add code changes.**

**OAuth/connection flow:**
- `InitiateAkahuConnectionCommand` (`src/Core/MyMascada.Application/Features/BankConnections/Commands/InitiateAkahuConnectionCommand.cs:48-169`) currently builds an authorisation URL via `_akahuApiClient.GetAuthorizationUrl(state, email)`. The URL builder (`AkahuApiClient.cs:44-72`) does NOT pass a `connection` query param. For the migration upgrade flow we likely want to pass `connection=<official-conn-id>` (Q5) to deep-link the user straight to the bank that needs upgrading. Open question whether passing the classic `conn_xxx` works (the docs mention this but aren't precise).
- `ExchangeAkahuCodeQuery` (`src/Core/MyMascada.Application/Features/BankConnections/Queries/ExchangeAkahuCodeQuery.cs:60-162`) overwrites `EncryptedAppToken`/`EncryptedUserToken`. Works for the upgrade case as written, but does NOT trigger the post-upgrade re-key of `BankConnection.ExternalAccountId` for that user's existing connections.
- `CompleteAkahuConnectionCommand` (`...Commands/CompleteAkahuConnectionCommand.cs:51-183`) creates a *new* bank connection. It does not know how to update an existing one to point at a new `acc_xxx`. We need a sibling command `MigrateAkahuConnectionCommand` (section 4.3) that updates rather than inserts.

**Personal App mode (the path Rodrigo uses today):**
- `SaveAkahuCredentialsCommand` (`...Commands/SaveAkahuCredentialsCommand.cs:53-145`) only validates and stores tokens. No interaction with migration. The Personal App tokens themselves should keep working — but the account IDs they see will change after the user upgrades within `my.akahu.nz`. So the same per-user re-mapping logic in section 4.4 applies.

**Reconciliation availability gate:**
- `GetAkahuReconciliationAvailabilityQuery` (`...Queries/GetAkahuReconciliationAvailabilityQuery.cs:40-121`) checks `bankConnection.ExternalAccountId` is non-empty. After migration but before re-auth + re-key, this still passes (the column is non-empty, just stale). The user is allowed to start a reconciliation; the fetch then fails with 404. Cosmetically poor — see section 4.7 for the "migration required" gate.

**Frontend:**
- `frontend/src/app/settings/bank-connections/page.tsx` (the bank-connections settings page). No knowledge of migration state. Needs a banner.
- `frontend/src/app/settings/bank-connections/callback/page.tsx` (OAuth callback handler). Works for upgrade-by-OAuth.
- `frontend/src/components/bank-connections/akahu-setup-dialog.tsx` (Personal token entry). Unchanged.

**Health & background jobs:**
- `src/Infrastructure/MyMascada.Infrastructure/Services/Health/AkahuHealthCheck.cs` — likely calls the API with a configured token. Doesn't reference IDs directly but post-cutover failures may surface here. Sanity check.
- `src/Infrastructure/MyMascada.Infrastructure/BackgroundJobs/TokenRevocationRetryJobService.cs` — retries token revocation; unaffected.

### 3.3 Database indexes & FK considerations

The unique-ish index on `BankConnection.ExternalAccountId` (`ApplicationDbContext.cs:419-420`) is non-unique, which is correct — we don't want to enforce uniqueness while two connections (classic + official) might transiently exist. Good. No schema change required for the ID rewrite itself.

For the migration-time data-fix we'll want a single SQL transaction per user that:
- updates `BankConnection.ExternalAccountId` for each acc-id
- rewrites the encrypted `AkahuConnectionSettings.AkahuAccountId` (re-encrypt with new value)
- updates `Transaction.ExternalId` for every `trans_xxx` whose new equivalent we have from the `_migrated` map

EF Core can do this in-memory but for the transaction set (potentially thousands per user) a raw SQL `UPDATE Transactions SET ExternalId = CASE … END WHERE ExternalId IN (…)` would be cleaner. See section 4.4.

---

## 4. Concrete remediation strategy

### 4.1 Strategy overview

Two operational paths, both required:

**Path A — Server-side correlation (engineer):** code that, given an old account ID and a user, can:
1. Fetch `GET /accounts` with current credentials
2. Identify migrated accounts via `_migrated`
3. Rewrite stored `acc_xxx` in `BankConnection` and `AkahuConnectionSettings`
4. Fetch `GET /accounts/{new}/transactions?start=<oldest-imported-date>` and build `_migrated` map for transactions
5. Rewrite `Transaction.ExternalId` for the user's transactions on that account
6. Mark the connection's "migration complete" timestamp

**Path B — User-driven re-OAuth (UI + UX):** a banner / dialog that:
1. Detects classic big-4 connections that haven't been migrated
2. Surfaces the per-bank "Upgrade" button
3. Drives the user through the existing OAuth flow with `connection=<classic-conn-id>` query param
4. On callback, kicks off Path A automatically for the relevant connection

### 4.2 Add MIGRATE webhook handling

**File:** `src/Core/MyMascada.Application/Features/BankConnections/DTOs/AkahuWebhookModels.cs`

Add `Migrate` (or extend `AkahuWebhookCodes` with `MIGRATE` depending on what Q2 confirms). Add fields:

```csharp
[JsonPropertyName("previous_item_id")]
public string? PreviousItemId { get; init; }

[JsonPropertyName("new_item_id")]
public string? NewItemId { get; init; }
```

**File:** `src/Core/MyMascada.Application/Features/BankConnections/Commands/ProcessAkahuWebhookCommand.cs`

Add a `HandleMigrateEventAsync` branch:

1. Look up `BankConnection` by `previous_item_id` (`GetByExternalAccountIdAsync`).
2. If found, queue a `MigrateAkahuConnectionCommand` (section 4.3) for that connection. Do NOT do the heavy `Transaction` rewrite inside the webhook handler — webhooks should complete quickly. Use Hangfire / the existing background sync job infrastructure.
3. If not found, log and accept the webhook (it'll get picked up on next user-initiated sync).

### 4.3 New command: `MigrateAkahuConnectionCommand`

**Path:** `src/Core/MyMascada.Application/Features/BankConnections/Commands/MigrateAkahuConnectionCommand.cs` (new file)

Inputs: `UserId`, `BankConnectionId` (or `OldExternalAccountId`).
Behaviour:
1. Load `BankConnection`, `AkahuUserCredential`.
2. Decrypt tokens (`appIdToken`, `userToken`).
3. Call `_apiClient.GetAccountsWithCredentialsAsync(appIdToken, userToken, ct)` → list of `AkahuAccountInfo`.
4. Find the account whose `_migrated` equals `connection.ExternalAccountId`. (Requires plumbing `_migrated` through `AkahuAccount` and `AkahuAccountInfo` — see 4.5.)
5. If not found, mark connection with `LastSyncError = "Awaiting re-authorisation"` and bail.
6. If found, compute `newAccountId = account._id`.
7. Update `BankConnection.ExternalAccountId = newAccountId`; re-encrypt `AkahuConnectionSettings` with `AkahuAccountId = newAccountId`.
8. For the transaction set: call `_apiClient.GetTransactionsAsync(appIdToken, userToken, newAccountId, oldestImportedDate, today, ct)`. Each `AkahuTransaction` carries `_migrated` (need to add field on the model — section 4.5). Build `{old_trans_id -> new_trans_id}` map.
9. In a single transaction, `UPDATE Transactions SET ExternalId = newMap[ExternalId] WHERE AccountId = connection.AccountId AND ExternalId IN (oldIds)`.
10. Walk open `ReconciliationItem`s for this account whose `BankReferenceData` JSON contains an old `trans_xxx` and rewrite the JSON. (Optional — see 4.7 for the simpler "force-close open reconciliations" alternative.)
11. Trigger a normal `BankSyncService.SyncAccountAsync(connection.Id, BankSyncType.Initial, ct)` to pick up the post-migration delta.
12. Audit-log the migration with old/new IDs, count of transactions remapped, timestamp.

This command is also the unit that gets invoked from:
- `MIGRATE` webhook handler
- Post-OAuth-callback in `ExchangeAkahuCodeQuery` (after a successful upgrade re-auth, iterate all of this user's active big-4 connections and call `MigrateAkahuConnectionCommand` for each)
- A new admin/debug REST endpoint `POST /api/bankconnections/{id}/migrate-akahu` so Rodrigo can manually trigger it during testing
- A best-effort background job that scans for any big-4 `BankConnection` where the next sync returns 404 and tries the migration

### 4.4 Update `AkahuAccountInfo` / `AkahuTransaction` / `AkahuConnection` to expose `_migrated` and `_classic`

**File:** `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuApiClient.cs`

Add to `AkahuAccount` (line 459):
```csharp
[JsonPropertyName("_migrated")]
public string? Migrated { get; init; }
```

Add to `AkahuConnection` (line 482):
```csharp
[JsonPropertyName("_classic")]
public string? Classic { get; init; }
```

Add to `AkahuTransaction` (line 490):
```csharp
[JsonPropertyName("_migrated")]
public string? Migrated { get; init; }
```

Add `Migrated`/`Connection.Classic` to `AkahuAccountInfo` and `BankTransactionDto` so `AkahuBankProvider.MapTransaction` and `MapToAccountInfo` can pass them up the stack.

**File:** `src/Core/MyMascada.Application/Common/Interfaces/IAkahuApiClient.cs` — extend `AkahuAccountInfo` with `Migrated` / `Classic` properties.

Add a new method to `IAkahuApiClient` that calls `GET /connections` (currently MyMascada has no wrapper). Used to detect which big-4 banks the user has classic-vs-official pairs for:
```csharp
Task<IReadOnlyList<AkahuConnectionInfo>> GetConnectionsWithCredentialsAsync(
    string appIdToken, string userToken, CancellationToken ct = default);
```
The response shape from `GET /connections` includes `_id`, `name`, `logo`, `_classic` (when applicable).

### 4.5 Loosen dedup tolerance during the migration window

**File:** `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/BankSyncService.cs:139-143`

Add a config option (e.g. `AkahuOptions.MigrationFallbackEnabled = true`) that, when set, runs sync with `DateToleranceDays = 2`, `AmountTolerance = 0.005m`, and bumps `DescriptionSimilarityThreshold` to 0.7. This catches cases where Akahu's stated date/type/description drift produces near-duplicates that the exact-match dedup misses.

Apply this only to `ProviderId == "akahu"` and only when the connection has a `LastMigratedAt` within the last 30 days, to avoid permanently weakening dedup on the well-tested classic path.

### 4.6 Per-bank consent metadata

`AkahuUserCredential` currently has one set of consent timestamps per user. Once a user does two upgrades on different days (ANZ now, ASB next week), only the most recent will be reflected — which is misleading for audit/compliance.

Two options:

- **(a) Move consent metadata to `BankConnection`** — most correct. Add `ConsentGrantedAt`, `ConsentScope`, `ConsentCorrelationId`, `ConsentRevokedAt`, `OfficialConnectionId` (the `conn_xxx`), `IsMigrated`, `MigratedAt` to `BankConnection`. New EF migration. Backfill: set `ConsentGrantedAt = AkahuUserCredential.ConsentGrantedAt`, `OfficialConnectionId = NULL` for all existing rows.
- **(b) Keep on `AkahuUserCredential`, accept the limitation** — cheaper but degrades the compliance story.

Recommend (a) but it is **not blocking for May 24**. Ship the migration logic first, do this rename in a follow-up PR.

### 4.7 UI/UX for the user-driven upgrade

**File:** `frontend/src/app/settings/bank-connections/page.tsx`

Add a top-of-page banner (`MigrationBanner` component) that:
- Calls a new query `GET /api/bankconnections/akahu/migration-status` which returns:
  - `pendingConnections: [{ bankName, classicConnectionId, officialConnectionId, externalAccountId }]`
  - `deadline: "2026-05-24T23:59:59+12:00"`
  - `daysRemaining: 9`
- For each pending connection, shows a row with bank name, "Last synced X days ago", and an "Upgrade" CTA.
- The Upgrade CTA fires `apiClient.initiateAkahuConnection({ connectionId: classicConnectionId })` — requires extending the `InitiateAkahuRequest` DTO with an optional `connection` param (the Akahu `conn_xxx`) that the backend appends to the OAuth URL via `AkahuApiClient.GetAuthorizationUrl`.
- After the OAuth callback returns success, automatically kicks off `MigrateAkahuConnectionCommand` for each of the user's affected connections.

**Files modified:**
- `frontend/src/components/bank-connections/migration-banner.tsx` (new)
- `frontend/src/lib/api-client.ts` — add `getAkahuMigrationStatus()` and extend `initiateAkahuConnection()` signature.
- `frontend/src/types/bank-connections.ts` — add `AkahuMigrationStatus`, extend `BankConnection` with `isMigrated`, `migrationDeadline`.
- `src/Core/MyMascada.Application/Features/BankConnections/Queries/GetAkahuMigrationStatusQuery.cs` (new)
- `src/WebAPI/MyMascada.WebAPI/Controllers/BankConnectionsController.cs` — add GET endpoint.

Bonus: surface the banner on the dashboard for ~7 days before the deadline, dismissable per-user but capped (the post-revocation state is destructive — better to nag).

### 4.8 Personal-App-mode users

For users who configured Personal Tokens (the existing primary path), the migration still requires them to log into `my.akahu.nz` and re-authorise the big-4 banks. That happens OUTSIDE MyMascada. Once done, their existing User Token continues to work and will return new `acc_xxx` IDs.

Detection: when `BankSyncService.SyncAccountAsync` fails for an active big-4 connection with `UnauthorizedAccessException` or `AkahuApiException(404)`, check (via `GET /accounts` for that user) whether a new account with `_migrated == connection.ExternalAccountId` exists. If yes, run `MigrateAkahuConnectionCommand` automatically. If no, leave the connection in an "Awaiting re-authorisation" state and surface in the UI.

This means **the same `MigrateAkahuConnectionCommand` handles both OAuth-driven and Personal-App-driven upgrades**, which keeps the surface area small.

### 4.9 Rodrigo's own dev/test connections

The 5-user-cap dev environment (Josh's Bitwarden share, expires 7 days from 2026-03-10 — so the token is already expired; Rodrigo needs to ping Josh for a new one) is the only place we can validate the migration code end-to-end. Before deploying anything to production we need:

- A confirmed dev account whose `GET /connections` returns at least one big-4 official connection with `_classic` set.
- An ability to seed a few transactions on the old account so the rewrite logic has something to chew on.
- A second dev test where we revoke the classic connection manually and verify the sync gracefully transitions to "Awaiting re-authorisation".

If Josh can't provide a same-day classic-vs-official side-by-side dev account, fall back to a unit-test-driven approach: mock `IAkahuApiClient.GetAccountsWithCredentialsAsync` to return a hand-crafted account list including `_migrated` and unit-test `MigrateAkahuConnectionCommand` against it. The webhook handler can be tested with a hand-crafted JSON payload using `AkahuWebhookController` + a real signature from a test key.

---

## 5. Risk-ordered punch list (what ships when)

### MUST ship before EOD 2026-05-24 (P0 — blocks revocation)

1. **(P0-1)** Communicate to all users with active Akahu big-4 connections: in-app banner (4.7) + email blast. Without this, users won't know they need to re-auth. Lead time: 2 days for copy + email plumbing. **Day 1-2.**
2. **(P0-2)** Implement `MigrateAkahuConnectionCommand` (4.3) + the model field additions in 4.4 (`_migrated`, `_classic`). This is the core remediation; it must exist before any user re-auths or we lose history. **Day 2-5.**
3. **(P0-3)** Wire `MigrateAkahuConnectionCommand` into the `ExchangeAkahuCodeQuery` post-callback path so an OAuth-driven re-auth automatically migrates the connection. **Day 5.**
4. **(P0-4)** Add `MIGRATE` webhook handling (4.2) so users on autosync who re-auth via `my.akahu.nz` also get migrated automatically. **Day 5-6.**
5. **(P0-5)** Loosen dedup tolerance during migration (4.5) — small change, big safety net against the data-corruption risk. **Day 6.**
6. **(P0-6)** Add migration-status banner UI (4.7). **Day 6-8.**
7. **(P0-7)** Test against dev environment end-to-end (need Josh's renewed dev token; section 4.9). **Day 7-8.**
8. **(P0-8)** Production smoke test with Rodrigo's own connections (after he migrates them). **Day 8-9.**

### SHOULD ship but can wait until post-24 May (P1 — degraded but not broken)

9. **(P1-1)** Move consent metadata to `BankConnection` (4.6). Better audit story but not user-visible.
10. **(P1-2)** Rewrite stale `trans_xxx` inside `ReconciliationItem.BankReferenceData` JSON during `MigrateAkahuConnectionCommand` (instead of leaving them as zombie pointers). Alternative: force-close any open reconciliation for a big-4 account before its `MigrateAkahuConnectionCommand` runs, with a clear UI message.
11. **(P1-3)** Add `GET /api/bankconnections/akahu/connections` listing the user's classic-vs-official conn objects (helps UI explain *why* the user needs to upgrade).
12. **(P1-4)** Backfill `BankConnection.IsMigrated = true` for all existing classic connections once they're confirmed migrated (so the banner stops appearing).
13. **(P1-5)** Update `docs/banking-integration-modes.md` and the Obsidian `Akahu Bank API.md` to document migration handling.

### NICE-TO-HAVE (P2 — only if time permits, or only for Rodrigo's own use)

14. **(P2-1)** Admin endpoint `POST /api/admin/bankconnections/migrate-akahu-all` that scans for unmigrated big-4 connections and runs `MigrateAkahuConnectionCommand` on each. Useful for forcibly cleaning up post-24-May stragglers.
15. **(P2-2)** Telegram/email notification when a `MIGRATE` webhook fires for a user.
16. **(P2-3)** Metrics: count of pending vs migrated big-4 connections, exposed on the `/health` endpoint.
17. **(P2-4)** Confirmation dialog that previews what's going to happen before running `MigrateAkahuConnectionCommand` (probably overkill).

### ROLLBACK / SAFETY

- Keep classic IDs in a sidecar table (`BankConnectionMigrationLog`) for 90 days post-migration so we can reverse if Akahu has a bug.
- Don't delete the old `Transaction.ExternalId` values — UPDATE them, do not store the old value separately unless we want to track. Decision: store old IDs in `Transaction.Notes` field (already a free-text column) as `[migrated_from:trans_xxx]` so support can trace.

---

## 6. Open questions for Akahu (send today, copy Josh)

In addition to Q1-Q8 in section 2.8, also:

- **Q9:** Is there any way to do a "dry-run" `GET /accounts` against the official connection without the user upgrading? I.e. can we pre-compute the `_migrated` map server-side and rewrite IDs in advance of the user clicking Upgrade?
- **Q10:** When a Personal-App user goes to `my.akahu.nz` and upgrades a connection there, is the existing User Token preserved or invalidated?
- **Q11:** Are webhooks subscribed under the classic connection automatically re-pointed to the official connection post-migration, or do we need to resubscribe?
- **Q12:** For credit-card accounts where the cardholder restriction kicks in, what does `GET /accounts` return for a non-account-holder user — an empty list, an explicit error, or the account with a restricted-data flag?
- **Q13:** Does Akahu emit a single `MIGRATE` event per account or one per `conn_xxx`? If per-account, we may receive many events for a multi-account user.

---

## 7. Acceptance criteria

The migration is "done" when:

- [ ] All four banks (ANZ/ASB/BNZ/Westpac) have `MIGRATE` webhook handling exercised by an end-to-end test using a sandbox account.
- [ ] A simulated `BankSyncService.SyncAccountAsync` call for a pre-migration `acc_xxx` returns a clear `LastSyncError = "Awaiting re-authorisation"` and surfaces in the UI.
- [ ] A simulated re-OAuth flow correctly invokes `MigrateAkahuConnectionCommand`, which (verified in DB) rewrites both `BankConnection.ExternalAccountId` and at least 5 sample `Transaction.ExternalId` rows.
- [ ] A second sync after migration imports zero new transactions for the previously-imported window (no duplicates).
- [ ] The migration banner is present and dismissable in the bank-connections settings page.
- [ ] Email blast to existing big-4-connected users has been sent at least 5 days before the deadline.
- [ ] Rodrigo has personally migrated his own ANZ test connection and confirmed his historical transaction view is intact.

---

## 8. Critical files for implementation

These are the files an implementer will spend ~80% of their time in:

- `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/Providers/AkahuApiClient.cs` — add `_migrated`/`_classic` to response models, add `GetConnectionsWithCredentialsAsync` method.
- `src/Core/MyMascada.Application/Features/BankConnections/Commands/ProcessAkahuWebhookCommand.cs` — add `HandleMigrateEventAsync` branch and update `AkahuWebhookModels.cs` alongside.
- `src/Infrastructure/MyMascada.Infrastructure/Services/BankIntegration/BankSyncService.cs` — loosen dedup tolerance during migration window, plus surface re-auth-required errors.
- `src/Core/MyMascada.Application/Features/BankConnections/Queries/ExchangeAkahuCodeQuery.cs` — chain `MigrateAkahuConnectionCommand` into the post-OAuth flow.
- `frontend/src/app/settings/bank-connections/page.tsx` — add migration banner and per-connection upgrade CTA.

(Secondary but important: `AkahuBankProvider.cs`, the new `MigrateAkahuConnectionCommand.cs`, `AkahuWebhookModels.cs`, `BankConnection.cs` if we go with the consent-metadata move in 4.6, and the new GET migration-status query handler.)
