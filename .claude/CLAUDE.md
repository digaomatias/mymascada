# MyMascada - Development Guidelines

## Project Overview
A personal finance management application with AI-powered transaction categorization and proactive notifications. Self-hosted on a local Linux server (Mordor), with architecture designed to support eventual SaaS deployment.

## Local Development Setup

### Prerequisites
- .NET 10 SDK (see `global.json` for version)
- Node.js v22+
- Docker (for PostgreSQL + Redis)

### Start Infrastructure
```bash
docker compose -f docker-compose.dev.yml up -d
```
Starts PostgreSQL (port 5432) and Redis (port 6379).

### Start Backend
```bash
cd src/WebAPI
dotnet run
```
Backend runs on `http://localhost:5126`. Applies EF migrations automatically on startup.

### Start Frontend
```bash
cd frontend
npm install
npm run dev
```
Frontend runs on `http://localhost:3000`.

### Environment
Config is in `.env` at the repo root. Key values:
- `PUBLIC_API_URL=http://localhost:5126`
- `FRONTEND_URL=http://localhost:3000`
- DB credentials: see `.env`

### Test User Account
- Credentials stored in `.env` (not committed to repo)

## Architecture

Clean Architecture with four layers:

- **Domain** (`MyMascada.Domain`): Entities, Value Objects, Domain Events. No dependencies.
- **Application** (`MyMascada.Application`): Business logic, CQRS with MediatR, DTOs, interfaces.
- **Infrastructure** (`MyMascada.Infrastructure`): EF Core, external services, repository implementations.
- **WebAPI** (`MyMascada.WebAPI`): REST endpoints, DI config, middleware, Swagger.

## Technology Stack
- **Backend**: ASP.NET Core (.NET 10), EF Core, PostgreSQL
- **Auth**: ASP.NET Core Identity, JWT
- **Patterns**: CQRS (MediatR), Repository + Unit of Work, FluentValidation, AutoMapper
- **Frontend**: Next.js (App Router), React, TypeScript, Tailwind CSS
- **i18n**: next-intl (frontend), IStringLocalizer with .resx (backend)
- **Testing**: xUnit, FluentAssertions, NSubstitute, Playwright (E2E), Vitest (frontend)
- **Logging**: Serilog

## Infrastructure
- **Mordor** (`192.168.50.225`): Self-hosted server running Forgejo and production deployment via Docker Compose.

## Frontend Component Rules
- **Dropdowns/selects**: Use `<Select>` from `@/components/ui/select`
- **Buttons**: Use `<Button>` from `@/components/ui/button`
- **Inputs**: Use `<Input>` from `@/components/ui/input`, or apply `input` CSS class
- **Modals**: Use `<BaseModal>` from `@/components/modals/base-modal`
- **Confirmation dialogs**: Use `<ConfirmationDialog>` from `@/components/ui/confirmation-dialog`

## PR Screenshots (Required for UI Changes)

If your PR modifies any files under `frontend/src/app/` or `frontend/src/components/`:
1. Start the dev environment (infrastructure + backend + frontend)
2. Navigate to the affected page in a browser
3. Take a screenshot using Playwright MCP (`mcp__playwright__browser_take_screenshot`) or similar
4. Upload the screenshot and include it in the PR description body

This is mandatory for UI PRs. Non-UI PRs (backend only, config, tests) don't need screenshots.

## Internationalization (i18n)

### CRITICAL: All User-Facing Strings Must Be Localized
**NEVER hardcode user-facing text.** Every string users see must go through the localization system.

### Supported Locales
- `en` — English (default)
- `pt-BR` — Brazilian Portuguese

### Frontend (next-intl)
1. Add key to `/frontend/messages/en.json`
2. Add Portuguese translation to `/frontend/messages/pt-BR.json`
3. Use `useTranslations` hook:
```typescript
const t = useTranslations('sectionName');
const tCommon = useTranslations('common');
```

### Backend (ASP.NET Core)
Resource files in `src/WebAPI/Resources/`:
- `Validation/ValidationMessages.{locale}.resx`
- `Exceptions/ExceptionMessages.{locale}.resx`
- `Business/BusinessMessages.{locale}.resx`

### Key Naming: camelCase, grouped by section. Common actions in `common` section.

### What to Localize
UI labels, buttons, headings, validation messages, error messages, toasts, empty states, email content.

### What NOT to Localize
Log messages, code comments, technical errors, API endpoints, DB field names.

## Coding Standards

### Development Workflow
- Always compile frontend and backend before committing.
- Run `dotnet build` and `npm run build` to verify.

### Critical Honesty Guidelines
**NEVER claim success when something is broken.**
- Use ❌ for anything broken, failing, or not working
- Only use ✅ for functionality fully tested and verified end-to-end
- Prioritize fixing broken functionality over implementing new features
- Never move on from broken functionality without user approval

### Configuration Protection
**DO NOT modify configuration files without user approval:**
- Shell scripts (`.sh` files)
- Environment files (`.env`, `appsettings.json`)
- Build config (`package.json`, `tsconfig.json`)
- Database config, connection strings, API URLs/ports

When errors occur: investigate root cause, ask the user, don't change configs as a quick fix.

### Database Balance Protection
**NEVER modify account balances without explicit user permission.**
- Don't update `Accounts.CurrentBalance` without approval
- Analysis and calculations are fine; actual updates require confirmation
- Always explain impact before executing balance changes

## Testing Requirements

### Testing Workflow
1. Implement Feature (Backend + Frontend)
2. Unit Tests (xUnit)
3. Integration Tests (API validation)
4. Playwright E2E Tests (complete user workflow)
5. Manual Testing (using test user account)

### Unit Testing: Business Logic
- **Scenario-based testing**: Test business scenarios, not just method calls
- **Assert outcomes**: Verify actual results, not just that methods were called
- **Edge cases**: Boundary conditions, error cases, unusual inputs
- **Direction testing**: For financial logic, verify money flow, amounts, signs

### Playwright MCP Testing
**CRITICAL: Playwright = UI testing ONLY**
- No backend shortcuts (no curl, no direct API calls)
- Fix frontend build issues before attempting Playwright testing
- Test every interactive element: buttons, forms, dropdowns
- Complete full user journeys from start to finish via UI
- Document all issues found

## Business Logic & Data Standards

### Transaction Amount Convention (Source of Truth)
- **Expenses**: Always stored as **negative** amounts
- **Income**: Always stored as **positive** amounts
- `isIncome = transaction.amount > 0`

```csharp
// Backend normalization
var normalizedAmount = candidate.Type == TransactionType.Expense
    ? -Math.Abs(candidate.Amount)
    : Math.Abs(candidate.Amount);
```

```typescript
// Frontend
const isIncome = transaction.amount > 0;
```

### Manual Transactions Start Unreviewed
All transactions (including manual) start as `IsReviewed = false` so they flow through the AI categorization pipeline on `/transactions/categorize`.

## Obsidian Documentation

**Vault path:** `Projects/MyMascada/` | **Hub page:** `Projects/MyMascada.md`

### File-to-Note Mapping

When you change these source files, update the corresponding Obsidian note:

| Source files changed | Obsidian note to update |
|---|---|
| `src/Domain/Entities/`, `src/Domain/Enums/` | `Architecture/Domain Model.md` |
| `src/Application/Features/*/Categorization*` | `Architecture/Categorization Pipeline.md` |
| `src/Infrastructure/Persistence/`, DbContext | `Architecture/Data Access.md` |
| `src/WebAPI/Controllers/` | `Backend/API Reference.md` |
| Auth middleware, JWT, OAuth code | `Backend/Authentication and Authorization.md` |
| Hangfire config, `BackgroundJobs/` | `Backend/Background Jobs.md` |
| `IFeatureFlags`, optional services | `Backend/Feature Flags.md` |
| `frontend/src/app/`, layouts, contexts, hooks | `Frontend/Overview.md` |
| `frontend/src/components/` | `Frontend/Component Map.md` |
| `frontend/messages/`, i18n config | `Frontend/Internationalization.md` |
| Feature-specific application/frontend code | Relevant `Features/*.md` note |
| External service integrations | Relevant `Integrations/*.md` note |
| `docker-compose.yml`, `deploy/`, Dockerfiles | `Operations/Deployment.md` |
| `.github/workflows/`, `.forgejo/` | `Operations/CI-CD.md` |
| Security middleware, rate limiting, CORS | `Operations/Security.md` |
| Test files, test configs | `Operations/Testing.md` |
| Architectural decisions, conventions | `Decisions and Conventions.md` |
