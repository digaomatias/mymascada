# Privacy

## Overview

MyMascada is a self-hosted personal finance application. When you self-host MyMascada, **you are the data controller**. The project itself does not collect, store, or transmit any of your data to the project maintainers or any third party.

## Data Storage

All data is stored locally in your own PostgreSQL database:

- User accounts and authentication credentials
- Financial transactions and account balances
- Categories, budgets, and rules
- Application settings and preferences

No data leaves your server unless you explicitly configure optional external services.

## Telemetry

MyMascada does **not** include any telemetry, analytics, or usage tracking. No data is sent to the project maintainers or any analytics service.

## Optional External Services

The following external services can be optionally configured. When enabled, data is shared with these providers according to their respective privacy policies:

### OpenAI API (AI Categorization)

- **What is shared**: Transaction descriptions and amounts are sent to the OpenAI API for categorization suggestions.
- **When**: Only when AI categorization is triggered (during transaction review).
- **How to disable**: Do not set the `OPENAI_API_KEY` environment variable. Rule-based and manual categorization remain available.
- **Privacy policy**: [OpenAI Privacy Policy](https://openai.com/privacy/)

### Google OAuth (Sign-In)

- **What is shared**: Standard OAuth flow -- Google provides your email address and profile name to MyMascada for authentication.
- **When**: Only when a user chooses "Sign in with Google".
- **How to disable**: Do not set `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`. Email/password authentication is always available.
- **Privacy policy**: [Google Privacy Policy](https://policies.google.com/privacy)

### Akahu (Bank Syncing -- New Zealand Only)

- **What is shared**: Bank account information and transaction data are synced through Akahu's API.
- **When**: Only when bank syncing is configured and active.
- **How to disable**: Do not set the `AKAHU_*` environment variables. Manual import via CSV/OFX is always available.
- **Privacy policy**: [Akahu Privacy Policy](https://www.akahu.nz/privacy)

### Email Provider (SMTP/Postmark)

- **What is shared**: Email addresses and notification content are sent through your configured email provider.
- **When**: For password resets, email verification, and notification delivery.
- **How to disable**: Set `EMAIL_ENABLED=false` (the default). The application functions without email, but password reset requires administrator intervention.

## Self-Hosters' Responsibilities

As a self-hoster, you are responsible for:

- Securing your server and database
- Complying with applicable data protection laws in your jurisdiction
- Managing backups and data retention
- Configuring HTTPS for production deployments
- Protecting credentials and API keys stored in your `.env` file

## Data Deletion

Since you control the database, you can delete any data at any time:

- Individual transactions can be deleted through the UI
- User accounts can be removed through the application
- Complete data removal is possible by deleting the PostgreSQL database volume

## Questions

If you have questions about data handling in MyMascada, please [open a discussion](https://github.com/digaomatias/mymascada/discussions) on GitHub.
