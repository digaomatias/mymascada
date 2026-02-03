**Idioma / Language:** English | [Portugues (BR)](README.pt-BR.md)

# MyMascada

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
[![CodeQL](https://github.com/digaomatias/mymascada/actions/workflows/codeql.yml/badge.svg)](https://github.com/digaomatias/mymascada/actions/workflows/codeql.yml)

A personal finance management application with AI-powered transaction categorization, bank account syncing, and multi-language support. Built for self-hosting with Docker, MyMascada gives you full control over your financial data.

## Features

- **Transaction Management** -- Import transactions via CSV, OFX, or manual entry
- **AI-Powered Categorization** _(optional -- requires OpenAI API key)_ -- Automatic transaction categorization using GPT-4o-mini
- **Rule-Based Categorization** -- Define custom rules to automatically categorize transactions
- **Bank Account Syncing** _(optional -- New Zealand only)_ -- Live account syncing via Akahu
- **Google OAuth Sign-In** _(optional)_ -- Email and password authentication is always available
- **Email Notifications** _(optional)_ -- Password reset, email verification, and account alerts
- **Multi-Language Support** -- English and Brazilian Portuguese
- **PWA Support** -- Installable on mobile devices for a native-like experience
- **Budget Tracking** -- Set budgets and analyze spending patterns
- **Account Reconciliation** -- Compare and reconcile account balances
- **Data Export** -- Export your data in CSV or JSON format
- **Docker Self-Hosting** -- Guided setup with a single script

## Quick Start (Self-Hosting)

```bash
git clone https://github.com/digaomatias/mymascada.git
cd mymascada
./setup.sh
```

The setup script walks you through configuration, including database credentials, optional API keys, and email settings. For detailed instructions, environment variable reference, and production deployment guidance, see [SELF-HOSTING.md](SELF-HOSTING.md).

## Architecture

MyMascada follows Clean Architecture principles with clearly separated layers:

```
Domain  -->  Application  -->  Infrastructure  -->  WebAPI
```

- **Domain** -- Entities, value objects, and domain events with no external dependencies
- **Application** -- Business logic, use cases, CQRS with MediatR, and DTOs
- **Infrastructure** -- Data access (EF Core), external integrations (OpenAI, email, Akahu)
- **WebAPI** -- REST API endpoints, authentication, middleware, and dependency injection

The frontend is a standalone Next.js application that communicates with the backend via REST APIs.

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Backend | ASP.NET Core 10, C# |
| Frontend | Next.js 15, React 19, TypeScript |
| Styling | Tailwind CSS |
| Database | PostgreSQL 16 |
| ORM | Entity Framework Core 10 |
| Caching | Redis 7 |
| Authentication | JWT, Google OAuth |
| AI | OpenAI API (gpt-4o-mini) |
| Internationalization | next-intl (frontend), IStringLocalizer (backend) |
| Background Jobs | Hangfire |
| Testing | xUnit, Playwright |

## Development Setup

### Prerequisites

- .NET 10 SDK
- Node.js 20+
- PostgreSQL 16+
- Redis 7+ (optional for development)

### Backend

```bash
cd src/WebAPI/MyMascada.WebAPI
dotnet restore
dotnet run
```

The API starts at `https://localhost:5126` by default.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

The frontend starts at `http://localhost:3000` by default.

### Running Tests

```bash
# Backend unit tests
dotnet test

# Frontend E2E tests
cd frontend
npx playwright test
```

## Documentation

- [Self-Hosting Guide](SELF-HOSTING.md) -- Deployment, configuration, and production setup
- [Contributing](CONTRIBUTING.md) -- How to contribute to the project
- [Security Policy](SECURITY.md) -- How to report vulnerabilities
- [Privacy](PRIVACY.md) -- Data handling and privacy information
- [Changelog](CHANGELOG.md) -- Release history and changes
- [Code of Conduct](CODE_OF_CONDUCT.md) -- Community guidelines

## License

This project is licensed under the [GNU Affero General Public License v3.0](LICENSE) (AGPL-3.0).

## Contributing

Contributions are welcome! If you find a bug or have a feature request, please [open an issue](https://github.com/digaomatias/mymascada/issues). Pull requests are appreciated -- see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.
