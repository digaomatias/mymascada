# Contributing to MyMascada

Thank you for your interest in contributing to MyMascada! This document provides guidelines and instructions for contributing.

## Code of Conduct

By participating in this project, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## How to Contribute

### Reporting Bugs

Before creating a bug report, please check the existing issues to avoid duplicates.

When creating a bug report, please include:

- **Clear title** describing the issue
- **Steps to reproduce** the behavior
- **Expected behavior** vs **actual behavior**
- **Screenshots** if applicable
- **Environment details**:
  - OS and version
  - Docker version (if using Docker)
  - Browser and version (for frontend issues)
  - Relevant configuration (without sensitive data)

### Suggesting Features

We welcome feature suggestions! Before suggesting a new feature:

1. Check if it's already been suggested in the issues
2. Consider if it fits the project's scope (personal finance management)
3. Think about how it would work for both self-hosted and future SaaS deployments

When suggesting a feature, please include:

- **Clear description** of the feature
- **Use case** explaining why it would be useful
- **Possible implementation** approach (optional)
- **Alternatives** you've considered

### Pull Requests

1. **Fork the repository** and create your branch from `main`
2. **Follow the coding standards** outlined below
3. **Add tests** for any new functionality
4. **Update documentation** as needed
5. **Ensure all tests pass** before submitting
6. **Write a clear PR description** explaining the changes

#### PR Process

1. Create a draft PR early if you want feedback on your approach
2. Ensure the PR description includes:
   - What the PR does
   - Why the change is needed
   - How to test the changes
   - Screenshots for UI changes
3. Link any related issues
4. Request review when ready

## Development Setup

### Prerequisites

- .NET 10 SDK
- Node.js 20+ (for frontend)
- Docker and Docker Compose (for full stack development)
- PostgreSQL 16+ (or use Docker)

### Local Development

1. Clone the repository:
   ```bash
   git clone https://github.com/digaomatias/mymascada.git
   cd mymascada
   ```

2. Copy the example environment file:
   ```bash
   cp .env.example .env
   ```

3. Start the development environment:
   ```bash
   docker compose up -d
   ```

See [SELF-HOSTING.md](SELF-HOSTING.md) for detailed setup instructions.

## Coding Standards

### Backend (C#)

- Follow [Microsoft C# coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types
- Use async/await for I/O operations
- Write unit tests for business logic
- Use meaningful names for variables, methods, and classes

#### Architecture

The project follows Clean Architecture:

- **Domain**: Core business entities and rules
- **Application**: Use cases and business logic
- **Infrastructure**: Data access, external services
- **WebAPI**: REST endpoints and configuration

### Frontend (TypeScript/React)

- Follow ESLint configuration provided in the project
- Use TypeScript strict mode
- Use functional components with hooks
- Follow the existing component structure
- Use `next-intl` for all user-facing strings

### Internationalization

All user-facing strings must be localized:

- Add keys to `frontend/messages/en.json`
- Add translations to `frontend/messages/pt-BR.json`
- Use `useTranslations` hook in components

### Commit Messages

- Use clear, descriptive commit messages
- Start with a verb (Add, Fix, Update, Remove, etc.)
- Reference issues when applicable (e.g., "Fix login issue #123")

Example:
```
Add budget alert notifications

- Implement email notifications for budget thresholds
- Add user preferences for notification frequency
- Include tests for notification service

Fixes #45
```

## Testing

### Running Tests

```bash
# Backend unit tests
cd src
dotnet test

# Frontend tests
cd frontend
npm test
```

### Test Requirements

- Unit tests for all business logic
- Integration tests for API endpoints
- Test coverage for edge cases and error handling

## Getting Help

- **Questions**: Open a discussion in GitHub Discussions
- **Bugs**: Create an issue using the bug report template
- **Security**: See [SECURITY.md](SECURITY.md) for vulnerability reporting

## License

By contributing, you agree that your contributions will be licensed under the AGPL-3.0 License.
