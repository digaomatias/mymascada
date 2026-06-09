# Mobile Push Notifications (FCM)

The backend delivers push notifications to the MyMascada mobile app via Firebase
Cloud Messaging. Push is an additional delivery channel layered on top of the
existing in-app notification system — every push corresponds to an in-app
`Notification` row created through `INotificationService.CreateNotificationAsync`.

## Architecture

```
NotificationService.CreateNotificationAsync
  ├─ preference / quiet-hours / rate-limit checks (existing)
  ├─ creates in-app Notification row (existing)
  └─ push dispatch (new, fail-soft)
       ├─ skipped when ChannelPreferences[type].push == false
       ├─ PushContentFormatter — expands i18n template keys to English copy,
       │   builds data payload with a mobile "route" hint
       └─ IPushNotificationService (FirebasePushNotificationService)
            ├─ loads the user's devices (UserDevices table)
            ├─ IFcmClient (FirebaseFcmClient) — FirebaseAdmin SDK multicast send
            └─ prunes tokens FCM reports as Unregistered/InvalidArgument
```

## Device registration API

| Endpoint | Description |
|---|---|
| `POST /api/v1/devices` `{ "token": "<fcm token>", "platform": "ios"\|"android" }` | Idempotent upsert. Re-registering refreshes `LastSeenAt`; a token previously owned by another user is reassigned. Auth required. |
| `DELETE /api/v1/devices/{token}` | Unregister on logout. Idempotent, only removes tokens owned by the caller. |

The mobile app should call `POST /api/v1/devices` after login and on every
`onTokenRefresh` callback, and `DELETE /api/v1/devices/{token}` on logout.

## Data payload (deep linking)

Each push carries a data payload consumed by the mobile tap handler
(`lib/core/services/notification_service.dart`):

- `route` — GoRouter path the app navigates to (e.g. `/transactions`,
  `/dashboard/budgets`, `/dashboard/rules/suggestions`)
- `notificationId` — the in-app notification id
- `notificationType` — e.g. `BudgetExceeded`

## Configuration

Firebase credentials are resolved in this order (first match wins):

1. **`Firebase:ServiceAccountJson`** configuration value — env var
   **`Firebase__ServiceAccountJson`**. Accepts either the raw Firebase service
   account JSON or a path to the JSON file. On fly.io:

   ```bash
   fly secrets set Firebase__ServiceAccountJson="$(cat service-account.json)"
   ```

2. **`GOOGLE_APPLICATION_CREDENTIALS`** env var pointing at the service account
   JSON file (standard Google application-default credentials).

When neither is set the service is **fail-soft**: a warning is logged once and
push delivery is skipped, so development environments work without Firebase.

The service account is created in the Firebase console for the same Firebase
project the mobile app's `google-services.json` / `GoogleService-Info.plist`
belong to (Project settings → Service accounts → Generate new private key).

## User preferences

Per-type push toggles reuse the existing `NotificationPreference.ChannelPreferences`
JSON (`{ "BudgetExceeded": { "inApp": true, "push": false }, ... }`). Push is
enabled by default and skipped only when explicitly set to `false`. Quiet hours
and rate limits apply before any channel dispatch, so they cover push too.

## Localization caveat

Trigger-generated notifications store i18n template keys instead of copy
(`CategorizationReminder`, `TransactionReminder`, `RuleSuggestionsAvailable`).
`PushContentFormatter` expands these to English at send time because the OS
renders the push body. Localized push copy would require data-only messages
rendered client-side — not implemented.
