# MyMascada Project Notes

## Local Development Setup

### Prerequisites
- .NET SDK (see `global.json` for version)
- Node.js v22+
- Docker (for PostgreSQL + Redis)

### Start Infrastructure
```bash
docker compose -f docker-compose.dev.yml up -d
```
This starts PostgreSQL (port 5432) and Redis (port 6379).

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
- DB credentials: `mymascada` / see `.env`

## PR Screenshots (Required for UI Changes)

If your PR modifies any files under `frontend/src/app/` or `frontend/src/components/`:
1. Start the dev environment (infrastructure + backend + frontend)
2. Navigate to the affected page in a browser
3. Take a screenshot using Playwright MCP (`mcp__playwright__browser_take_screenshot`) or similar
4. Upload the screenshot and include it in the PR description body
5. Screenshots dramatically speed up human review — the reviewer can see exactly what changed

This is mandatory for UI PRs. Non-UI PRs (backend only, config, tests) don't need screenshots.

## Frontend Component Rules

- **Dropdowns/selects**: Always use `<Select>` from `@/components/ui/select` instead of raw `<select>`. It wraps the native element with consistent styling (`input` class, custom chevron, focus ring). Usage is identical to `<select>` — just change the tag name and import.
- **Buttons**: Use `<Button>` from `@/components/ui/button`.
- **Inputs**: Use `<Input>` from `@/components/ui/input`, or apply the `input` CSS class to raw `<input>` elements.
- **Modals**: Use `<BaseModal>` from `@/components/modals/base-modal`.
- **Confirmation dialogs**: Use `<ConfirmationDialog>` from `@/components/ui/confirmation-dialog`.

## Infrastructure

- **Mordor** (`192.168.50.225`): Self-hosted server running Forgejo and the production deployment of MyMascada via Docker Compose.

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
