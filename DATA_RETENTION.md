# Data Retention Policy

This document defines the data retention periods for all data classes in MyMascada. Since MyMascada is self-hosted, the instance operator is the data controller and may adjust these policies to comply with local regulations.

## Retention Periods

| Data Class | Retention Period | Enforcement |
|---|---|---|
| Financial transactions | Retained while account is active; deleted on account deletion | Account deletion flow |
| Account balances | Retained while account is active; deleted on account deletion | Account deletion flow |
| Categories, budgets, rules | Retained while account is active; deleted on account deletion | Account deletion flow |
| Audit logs | 2 years minimum | Manual review |
| AI chat messages | 90 days (configurable via `DATA_RETENTION_CHAT_DAYS`) | Automated — `DataRetentionService` daily job |
| Auth tokens (refresh/reset) | 7 days after expiry/revocation | Automated — `TokenCleanupService` daily job |
| Akahu credentials | Retained while connected; revoked and deleted on disconnect or account deletion | Disconnect/account deletion flow |
| User settings & preferences | Retained while account is active; deleted on account deletion | Account deletion flow |
| Notifications | Retained while account is active; deleted on account deletion | Account deletion flow |
| Database backups | 30 days (operator-managed) | Operator responsibility |

## Automated Cleanup Jobs

The following Hangfire recurring jobs enforce retention automatically:

- **`cleanup-expired-chat-messages`** — Runs daily at 3:30 AM. Soft-deletes AI chat messages older than the configured retention period (default: 90 days).
- **`cleanup-expired-refresh-tokens`** — Runs daily at 3:00 AM. Hard-deletes expired/revoked refresh tokens older than 7 days.
- **`cleanup-expired-password-reset-tokens`** — Runs daily at 3:15 AM. Hard-deletes expired/used password reset tokens older than 7 days.

## Configuration

| Environment Variable | Default | Description |
|---|---|---|
| `DATA_RETENTION_CHAT_DAYS` | `90` | Number of days to retain AI chat messages before cleanup |

## Account Deletion

When a user account is deleted, all associated data is removed, including:

- Financial transactions and account balances
- Categories, budgets, goals, and rules
- AI chat messages and token usage records
- Notifications and user preferences
- Bank sync connections and credentials

## Operator Responsibilities

As the self-host operator, you are responsible for:

- Managing database backup retention (recommended: 30 days)
- Reviewing and adjusting retention periods for your jurisdiction
- Ensuring compliance with applicable data protection regulations (e.g., GDPR, CCPA)
