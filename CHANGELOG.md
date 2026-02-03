# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2025-02-03

### Added

- Initial open source release
- Transaction management with manual entry, CSV import, and OFX import
- AI-powered transaction categorization using OpenAI API
- Budget tracking with alerts and notifications
- Recurring transaction detection and upcoming bills widget
- Multi-account support (checking, savings, credit)
- Transfer detection between accounts
- Category management with customizable rules
- Dashboard with financial overview and spending insights
- User authentication with JWT tokens and refresh token rotation
- Internationalization support (English and Brazilian Portuguese)
- Docker deployment with multi-stage builds
- Interactive setup wizard (`setup.sh`)
- Comprehensive self-hosting documentation
- HTTPS support with Let's Encrypt integration
- Security headers (CSP, HSTS, X-Frame-Options)
- PostgreSQL database with Entity Framework Core
- Clean Architecture with CQRS pattern (MediatR)

### Security

- Non-root Docker containers
- Secure password generation in setup wizard
- Environment-based configuration (no hardcoded secrets)
- JWT validation with configurable token lifetimes
