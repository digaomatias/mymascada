# Production Email Setup (Resend)

Production email is enabled in `fly.toml` / `deploy/fly/fly.api.toml` using the
**Resend HTTP API** provider (`Email__Provider = 'resend'`). The backend already
ships three providers behind `IEmailServiceFactory` — `smtp`, `postmark`, and
`resend` — selected via `Email__Provider`. Resend was chosen for production
because it needs no SMTP server, uses a single API key, and the provider
(`ResendEmailService`) is plain `HttpClient` with no extra dependencies.

## 1. Required secret

The API key is the only secret. Set it once per Fly app:

```bash
fly secrets set Email__Resend__ApiKey=re_xxxxxxxxxxxxxxxx -a mymascada-api
```

Get the key from https://resend.com → API Keys (a "Sending access" key
restricted to the `mymascada.com` domain is sufficient).

Non-secret settings already live in `[env]` in the Fly config:

| Variable | Value |
|---|---|
| `Email__Enabled` | `true` |
| `Email__Provider` | `resend` |
| `Email__DefaultFromEmail` | `no-reply@mymascada.com` |
| `Email__DefaultFromName` | `MyMascada` |

### Failure mode if the secret is missing

Email configuration is evaluated once at startup
(`FeatureFlagsExtensions.IsEmailConfigured`). If `Email__Resend__ApiKey` is not
set, the app does NOT crash — it registers the NoOp email service, the
`email` health check reports unhealthy, and registration falls back to
direct (non-verified) registration. So set the secret **before** deploying
this config, or password-reset emails will still not be sent.

## 2. DNS requirements (SPF/DKIM)

Resend requires domain verification before it will send from
`no-reply@mymascada.com`. In Resend → Domains → Add Domain (`mymascada.com`),
it will issue records to add at the DNS host:

1. **MX** on `send.mymascada.com` → `feedback-smtp.<region>.amazonses.com`
   (priority 10) — return-path / bounce handling.
2. **TXT (SPF)** on `send.mymascada.com` → `v=spf1 include:amazonses.com ~all`.
3. **TXT (DKIM)** on `resend._domainkey.mymascada.com` → key provided by Resend.
4. **TXT (DMARC, recommended)** on `_dmarc.mymascada.com` →
   `v=DMARC1; p=none;` to start.

Copy the exact values from the Resend dashboard (region and DKIM key are
account-specific). Wait for the domain to show **Verified** before relying on
delivery.

## 3. Password-reset links and the mobile app

The reset email template (`EmailTemplates/{locale}/password-reset.body.html`)
links to `PasswordReset__FrontendResetUrl` with `?token=...&email=...`:

```
https://app.mymascada.com/auth/reset-password?token=...&email=...
```

That page exists in the web frontend (`frontend/src/app/auth/reset-password/page.tsx`)
and reads `token`/`email` from the query string, so mobile users who tap the
link complete the reset in the browser and then log in from the app. This is
the accepted v1 behaviour; a `mymascada://` deep link into the mobile app's
reset screen can be added later without backend changes (only the URL config
changes).

Email verification uses the same pattern via
`EmailVerification__FrontendVerifyUrl` → `https://app.mymascada.com/auth/verify-email`.

## 4. Verifying after deploy

```bash
fly deploy
curl -s https://mymascada-api.fly.dev/health   # "email" check must be healthy
```

Then trigger a real email: use the app's Forgot Password flow and confirm the
message arrives (check Resend dashboard → Emails for delivery status).

## Alternative providers

`Email__Provider` also accepts `smtp` (settings under `Email__Smtp__*`,
secret: `Email__Smtp__Password`) and `postmark`
(secret: `Email__Postmark__ServerToken`). Resend also exposes an SMTP endpoint
(`smtp.resend.com:465`, username `resend`, password = API key) if SMTP is ever
preferred — but the native HTTP provider is the supported production path.
