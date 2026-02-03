# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

We take the security of MyMascada seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### How to Report

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them through one of the following methods:

**GitHub Security Advisories**: Use [GitHub's private vulnerability reporting](https://github.com/digaomatias/mymascada/security/advisories/new) to submit a report directly. This ensures the report stays private until a fix is available.

### What to Include

Please include the following details in your report:

- Type of vulnerability (e.g., SQL injection, XSS, authentication bypass)
- Full paths of source file(s) related to the vulnerability
- Location of the affected source code (tag/branch/commit or direct URL)
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### Response Timeline

- **Initial Response**: Within 48 hours of receiving your report, we will acknowledge receipt and provide an expected timeline for assessment.
- **Assessment**: Within 7 days, we will assess the vulnerability and determine its severity.
- **Resolution**: Depending on severity, we aim to release a fix within:
  - Critical: 7 days
  - High: 14 days
  - Medium: 30 days
  - Low: 60 days

### Safe Harbor

We consider security research conducted consistent with this policy to be:

- Authorized in accordance with any applicable anti-hacking laws
- Exempt from any restrictions in our terms of service that would interfere with conducting security research
- Lawful, helpful to the overall security of the Internet, and conducted in good faith

We will not initiate legal action against you for security research conducted consistent with this policy. If legal action is initiated by a third party against you and you have complied with this security policy, we will take steps to make it known that your actions were authorized.

## Security Best Practices for Self-Hosters

When self-hosting MyMascada, please follow these security recommendations:

### Environment Configuration

- Never commit `.env` files or `appsettings.Development.json` to version control
- Use strong, unique passwords for database and JWT secrets (minimum 32 characters)
- Rotate secrets periodically, especially after any suspected breach

### Network Security

- Always use HTTPS in production (Let's Encrypt certificates are free)
- Keep your reverse proxy (nginx, Traefik, etc.) updated
- Consider using a Web Application Firewall (WAF)
- Restrict database access to localhost or trusted IPs only

### Application Security

- Keep MyMascada and all dependencies updated
- Enable automatic security updates for your host OS
- Regularly backup your database and test restoration
- Monitor logs for suspicious activity

### Docker Security

- Run containers as non-root users (already configured by default)
- Keep Docker and Docker Compose updated
- Use Docker secrets for sensitive configuration in production swarm deployments

## Acknowledgments

We appreciate the security research community's efforts in helping keep MyMascada and its users safe. Reporters of valid security issues will be acknowledged here (with permission).
