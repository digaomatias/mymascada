# Budget-vs-Actual Alerts

**Created:** 2026-06-08
**Status:** Proposed
**Goal:** Deliver proactive in-app notifications when a budget category is approaching its limit (>= 80% spent) or has been exceeded (>= 100%), using infrastructure that is almost entirely already in place.

---

## Table of Contents

- [Executive Summary](#executive-summary)
- [Current Architecture](#current-architecture)
- [Design Decisions](#design-decisions)
- [Phase 1: Trigger Logic — Budget Alert Methods](#phase-1-trigger-logic--budget-alert-methods)
- [Phase 2: Hangfire Job — Periodic Checking](#phase-2-hangfire-job--periodic-checking)
- [Phase 3: Notification Preferences — Surfacing the Threshold Setting](#phase-3-notification-preferences--surfacing-the-threshold-setting)
- [Phase 4: i18n Strings and Frontend Deep Link](#phase-4-i18n-strings-and-frontend-deep-link)
- [Test Plan](#test-plan)
- [Risk Mitigation](#risk-mitigation)

---

## Executive Summary

The budget alert feature is largely a wiring exercise, not a build exercise. Every structural component already exists:

- `BudgetCategory` has `IsApproachingLimit(spending)` (>= 80%) and `IsOverBudget(spending)`.
- `BudgetCalculationService` has `CalculateBudgetProgressAsync()` and `GetCategorySpendingBatchAsync()`, which already compute actual-vs-budget for every category in a single pass.
- `NotificationType` already declares `BudgetThreshold = 10` and `BudgetExceeded = 11`.
- `NotificationPreference` already stores `BudgetAlertPercentage` (nullable `int`), wired through the API and frontend type definitions.
- `NotificationService` already enforces rate limiting (10/type/day), quiet hours, and per-type channel toggles.
- `Notification.GroupKey` provides the idempotency key — the DB has a unique constraint on `(UserId, GroupKey)`.

What is missing is exactly one class: two new methods on `INotificationTriggerService` that call the calculation service, evaluate thresholds, and issue `CreateNotificationAsync` calls with a correctly scoped `groupKey`. A new daily Hangfire job then calls those methods for every user with an active budget.

Estimated effort: **S–M across four small phases, 4–6 days total**.

---

## Current Architecture

### Relevant existing files

| Component | Path |
|---|---|
| Budget entity | `src/Core/MyMascada.Domain/Entities/Budget.cs` |
| BudgetCategory entity | `src/Core/MyMascada.Domain/Entities/BudgetCategory.cs` |
| Budget calculation service | `src/Core/MyMascada.Application/Features/Budgets/Services/BudgetCalculationService.cs` |
| Budget repository interface | `src/Core/MyMascada.Application/Common/Interfaces/IBudgetRepository.cs` |
| Budget repository implementation | `src/Infrastructure/MyMascada.Infrastructure/Repositories/BudgetRepository.cs` |
| Notification trigger interface | `src/Core/MyMascada.Application/Common/Interfaces/INotificationTriggerService.cs` |
| Notification trigger implementation | `src/Infrastructure/MyMascada.Infrastructure/Services/Notifications/NotificationTriggerService.cs` |
| Notification service | `src/Infrastructure/MyMascada.Infrastructure/Services/Notifications/NotificationService.cs` |
| Notification entity | `src/Core/MyMascada.Domain/Entities/Notification.cs` |
| Notification preference entity | `src/Core/MyMascada.Domain/Entities/NotificationPreference.cs` |
| Update preferences command | `src/Core/MyMascada.Application/Features/Notifications/Commands/UpdateNotificationPreferencesCommand.cs` |
| Notification type enum | `src/Core/MyMascada.Domain/Enums/NotificationType.cs` |
| Expired budget job | `src/Infrastructure/MyMascada.Infrastructure/BackgroundJobs/ExpiredBudgetJobService.cs` |
| Hangfire job registration | `src/WebAPI/MyMascada.WebAPI/Program.cs` (lines 433–476) |
| Frontend notification bell | `frontend/src/components/notifications/notification-bell.tsx` |
| Frontend notification types | `frontend/src/types/notifications.ts` |
| Frontend budget nudge component | `frontend/src/components/budget/budget-contextual-nudge.tsx` |
| i18n strings | `frontend/messages/en.json`, `frontend/messages/pt-BR.json` |

### Key verified facts

**`BudgetCategory` methods (the domain logic is ready):**
- `GetEffectiveBudget()` — budgeted + rollover amount.
- `GetRemainingBudget(actualSpending)` — effective budget minus spending.
- `GetUsedPercentage(actualSpending)` — rounds to one decimal place.
- `IsApproachingLimit(actualSpending)` — returns `true` when `usedPercentage >= 80m && < 100m`. The 80% threshold is hardcoded in the entity.
- `IsOverBudget(actualSpending)` — returns `true` when `actualSpending > GetEffectiveBudget()`.

**`Budget` methods:**
- `GetDaysRemaining()`, `GetTotalDays()`, `GetPeriodElapsedPercentage()` — exist and work as described.

**`BudgetCalculationService`:**
- `CalculateBudgetProgressAsync(budget, userId, ct)` — returns a fully populated `BudgetDetailDto` with per-category `IsApproachingLimit` and `IsOverBudget` flags already set. It calls `GetCategorySpendingBatchAsync` internally. The trigger service can call this directly and read the flags off the DTO without re-implementing any calculation.
- `GetCategorySpendingBatchAsync(categoryIds, userId, startDate, endDate, includeSubcategories, ct)` — returns `Dictionary<int, CategorySpendingSummaryDto>` keyed by `categoryId`.

**`NotificationType` enum:** `BudgetThreshold = 10` and `BudgetExceeded = 11` already exist and are unused.

**`NotificationPreference.BudgetAlertPercentage`:** A nullable `int` persisted in the database, updatable via `UpdateNotificationPreferencesCommand`. The validator enforces 1–100. The frontend `NotificationPreferenceDto` type already includes `budgetAlertPercentage: number | null`. However, no settings UI component currently renders this field — it is API-only.

**`INotificationTriggerService`:** Has four methods — `CheckCategorizationReminderAsync`, `CheckRunwayWarningAsync`, `NotifyTransactionReminderAsync`, `NotifyRuleSuggestionsAvailableAsync`. No budget methods.

**`NotificationService.CreateNotificationAsync`:** Enforces idempotency via `ExistsByGroupKeyAsync` (plus DB unique constraint), quiet hours, per-type channel toggles (`ChannelPreferences` JSON), and a rate limit of 10 notifications per type per day via `CreateIfRateLimitNotExceededAsync`.

**`IBudgetRepository`:** Has `GetActiveBudgetsForUserAsync(userId, ct)` (includes `BudgetCategories` and `Category` navigation). Does **not** have a method to retrieve all distinct user IDs that have active budgets — this needs to be added (analogous to `GetUserIdsWithExpiredActiveBudgetsAsync` which already exists).

**Hangfire job pattern:** The `ExpiredBudgetJobService` (daily at 1:00 AM) and `RuleSuggestionGenerationJobService` (weekly, Sunday 4:00 AM) establish the pattern: implement an interface registered in DI, use `IServiceScopeFactory` and create a fresh scope per user, decorate with `[AutomaticRetry]`, register with `recurringJobManager.AddOrUpdate` in `Program.cs`.

---

## Design Decisions

### 1. Threshold model: fixed vs. per-user configurable

**Option A — Fixed thresholds (80% and 100%).**
`BudgetCategory.IsApproachingLimit` already enforces 80%. The trigger service reads that flag directly with no configuration needed.

**Option B — Per-user configurable threshold.**
`NotificationPreference.BudgetAlertPercentage` already exists in the database as a nullable int. When set, it would replace the 80% default. The trigger service would compare `usedPercentage >= preference.BudgetAlertPercentage` instead of calling `IsApproachingLimit`.

**Recommendation: implement Option B immediately.** The field is already persisted and the API command already handles it. The trigger service just needs to read it. Default to 80% when the field is null, which preserves `IsApproachingLimit`'s behavior. This avoids a future migration and delivers user control from day one with trivial code.

### 2. Idempotency: preventing duplicate alerts

The `Notification.GroupKey` DB column has a unique constraint per user. The idempotency key must encode exactly the alert condition that fired, scoped to the budget period so alerts reset for the next period. The approach:

- Approaching threshold: `budget:{budgetId}:cat:{categoryId}:threshold:approaching:{periodStart:yyyy-MM-dd}`
- Exceeded: `budget:{budgetId}:cat:{categoryId}:threshold:exceeded:{periodStart:yyyy-MM-dd}`

This guarantees one "approaching limit" notification and one "exceeded" notification per category per budget period, regardless of how many times the daily job runs. It does not use a percentage band in the key — once the first "approaching" fires, it will not fire again for the same category in the same period even if spending climbs from 82% to 95%. The "exceeded" key is separate and fires exactly once after crossing 100%.

**Important:** Do not include the date the job ran in the key (unlike `CheckCategorizationReminderAsync`, which uses a daily key). The period start date scopes the key to the current budget period, not to a specific day.

### 3. Trigger timing: event-driven vs. periodic

**Option A — Periodic (daily Hangfire job).** Simple. Reuses the existing job infrastructure. Consistent with the expired-budget and recurring-pattern jobs. A new transaction might take up to 24 hours to generate an alert, which is acceptable for a budget health notification.

**Option B — Event-driven (re-check after each transaction categorization).** Delivers alerts within seconds of spending crossing a threshold. Requires hooking into the categorization pipeline, significantly more complex, and could fire during batch imports when many transactions arrive at once.

**Recommendation: periodic first, event-driven as a later enhancement.** The daily cadence is right for budget health. Users are not harmed by a 24-hour delay on a budget warning — they are harmed by alert spam from a batch import.

### 4. Pace alerts (spending faster than the period elapses)

A "pace" alert would fire when `usedPercentage > periodElapsedPercentage` by a meaningful margin — for example, 30% of the period elapsed but 70% of the budget spent.

**Decision: explicitly deferred.** `Budget.GetPeriodElapsedPercentage()` and `BudgetCalculationService.ProjectEndOfPeriodSpending()` already exist to support this. It is a follow-on feature that can be added as a Phase 5 with minimal rework.

---

## Phase 1: Trigger Logic — Budget Alert Methods

**Priority:** P0 — the core feature
**Estimated effort:** M (1–2 days)
**Dependencies:** None — all dependencies already exist

### 1.1 Design

Add two new methods to `INotificationTriggerService`:

```
CheckBudgetThresholdsAsync(userId, ct)
    For each active budget:
        Call CalculateBudgetProgressAsync(budget, userId, ct)
        Read BudgetAlertPercentage from user's NotificationPreference (default 80 if null)
        For each category in BudgetDetailDto.Categories:
            If IsOverBudget and !already notified this period (groupKey check via CreateNotificationAsync):
                Create BudgetExceeded notification
            Else if usedPercentage >= alertThreshold and !already notified:
                Create BudgetThreshold notification
```

The `CreateNotificationAsync` call itself handles the idempotency check and all preference enforcement (quiet hours, channel toggles, rate limiting). The trigger method does not need to duplicate any of those checks.

The implementation follows the exact pattern of `NotifyRuleSuggestionsAvailableAsync` in `NotificationTriggerService.cs`: try/catch, log errors without throwing, build a JSON `data` payload with `href` for deep linking, pass template key strings for title/body.

### 1.2 Data payload

```json
{
  "href": "/budgets/{budgetId}",
  "templateKey": "BudgetThreshold",
  "budgetId": 42,
  "categoryId": 7,
  "categoryName": "Groceries",
  "usedPercentage": 87.3,
  "budgetedAmount": 500.00,
  "actualSpent": 436.50
}
```

The `href` deep-links directly to the budget detail page. `templateKey` lets the frontend render localised copy for the notification bell. The raw values support future push/email rendering.

### 1.3 Task breakdown

| Task | Files | Size | Description |
|---|---|---|---|
| 1.1 | `src/Core/MyMascada.Application/Common/Interfaces/INotificationTriggerService.cs` | S | Add `CheckBudgetThresholdsAsync(Guid userId, CancellationToken ct)` signature |
| 1.2 | `src/Infrastructure/MyMascada.Infrastructure/Services/Notifications/NotificationTriggerService.cs` | M | Implement the method: load active budgets, call `CalculateBudgetProgressAsync`, read `BudgetAlertPercentage` preference, loop categories, build groupKey, call `CreateNotificationAsync` for each threshold breach |
| 1.3 | `src/Infrastructure/MyMascada.Infrastructure/Services/Notifications/NotificationTriggerService.cs` | S | Inject `IBudgetRepository`, `IBudgetCalculationService`, and `INotificationPreferenceRepository` into the constructor (alongside existing `INotificationService` and `ITransactionRepository`) |

---

## Phase 2: Hangfire Job — Periodic Checking

**Priority:** P0 — without the job, Phase 1 is never called
**Estimated effort:** S (half a day)
**Dependencies:** Phase 1

### 2.1 Design

Create `IBudgetAlertJobService` and `BudgetAlertJobService` following the `RuleSuggestionGenerationJobService` pattern:

1. Resolve a list of all user IDs that have active budgets. This requires adding `GetUserIdsWithActiveBudgetsAsync()` to `IBudgetRepository` (analogous to the existing `GetUserIdsWithExpiredActiveBudgetsAsync()`).
2. For each user, create a fresh DI scope and call `INotificationTriggerService.CheckBudgetThresholdsAsync`.
3. Log summary statistics.
4. Register with `recurringJobManager.AddOrUpdate` at a time slot not already occupied (1:30 AM is free).

**Why a new job rather than adding to `ExpiredBudgetJobService`?** The expired-budget job already has a single responsibility and runs at 1:00 AM. Budget alerts have different scope (all users with active budgets, not just those with expired periods) and should be independently retryable and monitorable in the Hangfire dashboard.

### 2.2 Task breakdown

| Task | Files | Size | Description |
|---|---|---|---|
| 2.1 | `src/Core/MyMascada.Application/Common/Interfaces/IBudgetRepository.cs` | S | Add `GetUserIdsWithActiveBudgetsAsync(CancellationToken ct)` signature |
| 2.2 | `src/Infrastructure/MyMascada.Infrastructure/Repositories/BudgetRepository.cs` | S | Implement: `SELECT DISTINCT UserId FROM Budgets WHERE Status = Active AND IsDeleted = false` |
| 2.3 | `src/Core/MyMascada.Application/BackgroundJobs/IBudgetAlertJobService.cs` | S | Define interface with `ProcessAllUsersAsync(CancellationToken ct)` |
| 2.4 | `src/Infrastructure/MyMascada.Infrastructure/BackgroundJobs/BudgetAlertJobService.cs` | M | Implement: enumerate user IDs, fresh scope per user, call `CheckBudgetThresholdsAsync`, catch and log per-user errors, `[AutomaticRetry(Attempts = 3)]` |
| 2.5 | `src/WebAPI/MyMascada.WebAPI/Program.cs` | S | Register `IBudgetAlertJobService` → `BudgetAlertJobService` in DI; add `recurringJobManager.AddOrUpdate` at `Hangfire.Cron.Daily(1, 30)` |

---

## Phase 3: Notification Preferences — Surfacing the Threshold Setting

**Priority:** P1 — users need a way to adjust or disable alerts
**Estimated effort:** S–M (1 day)
**Dependencies:** Phase 1 (the setting has no effect without the trigger)

### 3.1 Current state

`BudgetAlertPercentage` is fully implemented in the backend (entity, repository, API command, validator). The frontend `NotificationPreferenceDto` type and `UpdateNotificationPreferenceRequest` type both include `budgetAlertPercentage`. However, no settings page currently renders this field. The settings section at `/settings` does not have a notifications sub-page — the section exists in the sidebar nav (`en.json` line 1957–1959) but only as a "Coming Soon" placeholder.

### 3.2 Design

**Option A — Full notifications settings page.** Build a complete `/settings/notifications` page with controls for quiet hours, all per-type channel toggles, large transaction threshold, budget alert threshold, and runway warning months.

**Option B — Add the budget alert threshold to the existing `/settings` page** as a simple inline field, deferring the full notifications settings page to later.

**Recommendation: Option B for this phase.** The API and types are ready. A single labeled number input (with helper text "Default: 80%") is sufficient and ships without building a complete settings page. The full notifications settings page can follow separately.

The per-type channel toggles (`ChannelPreferences` JSON) already control whether `BudgetThreshold` and `BudgetExceeded` notifications are delivered — `NotificationService` checks these before creating a notification. No additional backend changes are needed to support disable/enable per notification type; the existing mechanism already covers it.

### 3.3 Task breakdown

| Task | Files | Size | Description |
|---|---|---|---|
| 3.1 | `frontend/src/app/settings/page.tsx` | S | Add "Budget alert threshold" input field bound to `budgetAlertPercentage`; display current value from `GET /api/notifications/preferences`; submit via `PATCH /api/notifications/preferences` |
| 3.2 | `frontend/messages/en.json` | S | Add keys under `settings` for `budgetAlertThreshold`, `budgetAlertThresholdDescription` ("Alert me when a budget category reaches this percentage of its limit"), `budgetAlertThresholdDefault` ("Default: 80%") |
| 3.3 | `frontend/messages/pt-BR.json` | S | Portuguese translations for the same keys |

---

## Phase 4: i18n Strings and Frontend Deep Link

**Priority:** P1 — alerts are useless if the notification bell renders them as raw template keys
**Estimated effort:** S (half a day)
**Dependencies:** Phase 1 (defines the template key names)

### 4.1 Design

The notification bell component at `frontend/src/components/notifications/notification-bell.tsx` renders notifications using the `templates` section of `notifications` in `en.json`. Existing templates (`CategorizationReminder`, `TransactionReminder`, `RuleSuggestionsAvailable`) each have a `title` and `body` using ICU message format.

Add two new templates, using values from the notification's `data` JSON payload for interpolation:

```
"BudgetThreshold": {
  "title": "Budget alert: {categoryName}",
  "body": "{categoryName} is at {usedPercentage}% of its {budgetedAmount} budget"
},
"BudgetExceeded": {
  "title": "Budget exceeded: {categoryName}",
  "body": "{categoryName} has exceeded its {budgetedAmount} budget by {overAmount}"
}
```

The `href` field in the `data` JSON payload (`/budgets/{budgetId}`) provides the deep link. The notification bell should render this as a clickable link when `data` contains a valid `href`. Verify whether the bell already handles `href` from `data` before deciding whether a code change is needed.

### 4.2 Task breakdown

| Task | Files | Size | Description |
|---|---|---|---|
| 4.1 | `frontend/messages/en.json` | S | Add `BudgetThreshold` and `BudgetExceeded` templates under `notifications.templates` |
| 4.2 | `frontend/messages/pt-BR.json` | S | Portuguese translations for both templates |
| 4.3 | `frontend/src/components/notifications/notification-bell.tsx` | S | Verify the component reads `data.href` and renders it as a link; add support if absent |

---

## Test Plan

| Test file | Test name | What it verifies |
|---|---|---|
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_CategoryApproaching_CreatesBudgetThresholdNotification` | When a category is at 85% and user has no prior notification for this period, a `BudgetThreshold` notification is created |
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_CategoryExceeded_CreatesBudgetExceededNotification` | When a category is over 100%, a `BudgetExceeded` notification is created |
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_BothThresholdsCrossed_CreatesTwoNotifications` | A category at 110% generates the exceeded notification (not the threshold notification — only the most severe fires) |
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_Idempotency_DoesNotDuplicateAlert` | Calling `CheckBudgetThresholdsAsync` twice for the same user returns early on the second call because `ExistsByGroupKeyAsync` returns `true` |
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_BelowThreshold_CreatesNoNotification` | A category at 70% does not trigger a notification |
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_NoActiveBudgets_ReturnsImmediately` | A user with no active budgets triggers zero notifications |
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_UserDefinedThreshold_RespectsPreference` | User with `BudgetAlertPercentage = 90` only fires at 92%, not at 85% |
| `BudgetAlertTriggerTests.cs` | `CheckBudgetThresholds_QuietHoursActive_SkipsNotification` | `NotificationService` quiet-hours enforcement blocks notification when tested via a higher-level integration using a real `NotificationService` instance with a mocked preference |
| `BudgetAlertJobServiceTests.cs` | `ProcessAllUsers_CallsTriggerForEachUserWithActiveBudget` | Job enumerates users and calls `CheckBudgetThresholdsAsync` once per user |
| `BudgetAlertJobServiceTests.cs` | `ProcessAllUsers_PerUserErrorDoesNotAbortOtherUsers` | If one user's check throws, remaining users are still processed |
| `BudgetAlertIntegrationTests.cs` | `Job_ToNotification_EndToEnd` | Real `BudgetAlertJobService` with in-memory EF and mocked clock: budget with over-threshold category → `Notification` row created in DB with correct `GroupKey` and `Type` |

---

## Risk Mitigation

### Notification spam per period

The `groupKey` scoped to `{budgetId}:cat:{categoryId}:threshold:{approaching|exceeded}:{periodStart}` guarantees at most two notifications per category per budget period (one approaching, one exceeded). The idempotency check in `NotificationService.CreateNotificationAsync` handles the guard at the application layer; the DB unique constraint on `(UserId, GroupKey)` is the final backstop against races.

Do not encode the job's run date in the key. The existing `CheckCategorizationReminderAsync` uses a daily key (by design — it should fire every day if there are uncategorized transactions). Budget alerts should fire once per period, not once per day.

### Period boundary correctness

`Budget.GetPeriodEndDate()` and `Budget.GetDaysRemaining()` use `DateTimeProvider.UtcNow`, which is the project's injectable clock abstraction. Tests should substitute a fixed `DateTimeProvider` to avoid boundary flakiness. For the daily job, firing at 1:30 AM UTC means the alert for a period ending at midnight UTC fires shortly after rollover — but the expired-budget job at 1:00 AM will have already processed the rollover and created the new period. The alert job at 1:30 AM will therefore evaluate the new period, not the just-ended one, which is correct.

### Performance of checking all users' budgets

The `BudgetAlertJobService` creates a fresh DI scope per user, which bounds `DbContext` memory to one user at a time. `CalculateBudgetProgressAsync` issues two queries per budget (one to fetch categories, one to fetch transactions for the period via `GetByDateRangeAsync`). For a typical self-hosted deployment with fewer than 100 users and 2–5 budgets each, this is negligible. If the user base grows significantly, consider batching: load spending for all users' categories in a single query grouped by user, reducing DB round trips from `O(users * budgets)` to `O(1)`.

### Self-hosted vs. SaaS parity

Budget alerts carry no subscription gating — they are a core feature available to all users regardless of plan. The notification system itself has no tier checks. No work is required here.

### Zero-budget categories generating false alerts

A `BudgetCategory` with `BudgetedAmount = 0` and `RolloverAmount = null` has `GetEffectiveBudget() = 0`. `GetUsedPercentage` returns `100m` whenever `actualSpending > 0`, and `IsApproachingLimit` returns `false` (because `100 >= 100` is not `< 100`), while `IsOverBudget` returns `true` for any spending. The trigger method should skip categories where `GetEffectiveBudget() <= 0` to avoid alerting on intentionally unbudgeted categories. Add this guard in Phase 1's implementation.

### The `BudgetAlertPercentage` preference is not surfaced in the UI yet

Until Phase 3 ships, users cannot adjust the threshold. The default of 80% (from `BudgetCategory.IsApproachingLimit`) is reasonable for an initial release. Phase 3 is marked P1 (not P0) because the feature is useful even before users can configure the threshold, and the backend preference already allows API-level adjustment for power users who know the endpoint.
