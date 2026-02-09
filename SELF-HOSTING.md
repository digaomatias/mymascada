**Idioma / Language:** English | [Portugues (BR)](SELF-HOSTING.pt-BR.md)

# Self-Hosting MyMascada

A guide to deploying MyMascada on your own server using Docker.

---

## Quick Start

### Prerequisites

- Docker Engine 24+ with Docker Compose v2
- 1 GB RAM minimum (2 GB recommended)
- `openssl` installed (for generating secrets)

### Steps

1. **Clone the repository**

   ```bash
   git clone https://github.com/digaomatias/mymascada.git
   cd mymascada
   ```

2. **Run the setup script**

   ```bash
   chmod +x setup.sh
   ./setup.sh
   ```

   The script generates secure passwords, walks you through configuration, and
   starts the application using pre-built Docker images from GitHub Container
   Registry. Alternatively, copy `.env.example` to `.env` and edit it manually,
   then run:

   ```bash
   docker compose pull && docker compose up -d
   ```

3. **Open the application**

   Visit `http://localhost:3000` in your browser and create your first account.

> **Note:** Pre-built images are published for every release (`linux/amd64` and
> `linux/arm64`). Building from source is only needed if you want to customize
> the build (e.g., change `NEXT_PUBLIC_API_URL` at build time). To build from
> source, run `docker compose up -d --build` instead.

---

## Requirements

| Requirement | Minimum | Recommended |
|---|---|---|
| Docker Engine | 24+ | Latest stable |
| Docker Compose | v2 | Latest stable |
| RAM | 1 GB | 2 GB |
| Disk | 1 GB | 5 GB+ (depends on transaction volume) |
| OS | Linux, macOS, or Windows with WSL2 | Linux (Debian/Ubuntu) |
| Domain name | Optional | Required for HTTPS |

---

## Configuration Reference

All configuration is done through environment variables in the `.env` file. Run
`./setup.sh` for guided configuration, or copy `.env.example` and edit manually.

### Database

| Variable | Required | Description | Default |
|---|---|---|---|
| `DB_USER` | Yes | PostgreSQL username | `mymascada` |
| `DB_PASSWORD` | Yes | PostgreSQL password. The setup script generates one automatically. | -- |
| `DB_NAME` | Yes | PostgreSQL database name | `mymascada` |

### JWT Authentication

| Variable | Required | Description | Default |
|---|---|---|---|
| `JWT_KEY` | Yes | Secret key for signing JWT tokens. Must be at least 32 characters. Generate with `openssl rand -base64 64`. | -- |
| `JWT_ISSUER` | No | JWT issuer claim | `MyMascada` |
| `JWT_AUDIENCE` | No | JWT audience claim | `MyMascadaUsers` |

### Application URLs

| Variable | Required | Description | Default |
|---|---|---|---|
| `FRONTEND_URL` | Yes | Public URL where users access the frontend (no trailing slash) | `http://localhost:3000` |
| `FRONTEND_PORT` | No | Host port mapped to the frontend container. Extracted from `FRONTEND_URL` by `setup.sh`. The container always listens on 3000 internally. | `3000` |
| `API_URL` | No | Internal URL the frontend container uses to reach the API. In Docker Compose this is the service name. Change only if you modify the network setup. | `http://api:5126` |
| `PUBLIC_API_URL` | Yes | Public URL that browsers use to reach the API. For local access: `http://localhost:5126`. When using a reverse proxy (Caddy or Nginx), set this to the same domain as `FRONTEND_URL` since the proxy routes `/api/*` to the API. | `http://localhost:5126` |
| `CORS_ALLOWED_ORIGINS` | Yes | Comma-separated list of allowed origins. Must include `FRONTEND_URL`. | `http://localhost:3000` |

### Beta Access

| Variable | Required | Description | Default |
|---|---|---|---|
| `BETA_REQUIRE_INVITE_CODE` | No | Set to `true` to require invite codes for new registrations | `false` |
| `BETA_VALID_INVITE_CODES` | No | Comma-separated list of valid invite codes | -- |

### AI Categorization (OpenAI)

| Variable | Required | Description | Default |
|---|---|---|---|
| `OPENAI_API_KEY` | No | OpenAI API key for AI-powered transaction categorization | -- |
| `OPENAI_MODEL` | No | OpenAI model to use | `gpt-4o-mini` |

To enable AI categorization, add your API key to `.env`:

```
OPENAI_API_KEY=sk-proj-your-key-here
```

The default model (`gpt-4o-mini`) provides a good balance of accuracy and cost for
transaction categorization. You can switch to a different model by setting
`OPENAI_MODEL`, but `gpt-4o-mini` is recommended for this workload.

After setting the key, restart the API container:

```bash
docker compose restart api
```

AI categorization suggestions will then appear during transaction review. If no API
key is set, the feature is silently disabled and rule-based categorization remains
available.

### Google OAuth

| Variable | Required | Description | Default |
|---|---|---|---|
| `GOOGLE_CLIENT_ID` | No | Google OAuth client ID | -- |
| `GOOGLE_CLIENT_SECRET` | No | Google OAuth client secret | -- |

Set the authorized redirect URI in the Google Cloud Console to:
`{FRONTEND_URL}/api/auth/google-callback`

### Bank Sync (Akahu)

| Variable | Required | Description | Default |
|---|---|---|---|
| `AKAHU_ENABLED` | No | Set to `true` to enable the Akahu bank sync feature. Each user enters their own tokens via Settings. | `false` |
| `AKAHU_APP_SECRET` | No | Akahu App Secret -- only needed for Production App OAuth flow | -- |

Akahu provides live bank account syncing for New Zealand banks. To enable it, set
the flag in `.env`:

```
AKAHU_ENABLED=true
```

After changing the setting, restart the API container:

```bash
docker compose restart api
```

A "Bank Connections" option will appear in Settings. Each user then enters their own
Akahu **App Token** and **User Token** through the Settings page -- these are stored
per-user and not configured at the server level. You can find your tokens on the
[Akahu Developers](https://my.akahu.nz/developers) dashboard.

If `AKAHU_ENABLED` is not set or is `false`, the bank sync feature is hidden
entirely and CSV/OFX import remains available.

### Email Notifications

| Variable | Required | Description | Default |
|---|---|---|---|
| `EMAIL_ENABLED` | No | Enable email features | `false` |
| `EMAIL_PROVIDER` | No | Email provider: `smtp` or `postmark` | `smtp` |
| `EMAIL_FROM_ADDRESS` | No | Sender email address | `noreply@example.com` |
| `EMAIL_FROM_NAME` | No | Sender display name | `MyMascada` |
| `SMTP_HOST` | No | SMTP server hostname | -- |
| `SMTP_PORT` | No | SMTP server port | `587` |
| `SMTP_USERNAME` | No | SMTP authentication username | -- |
| `SMTP_PASSWORD` | No | SMTP authentication password | -- |
| `SMTP_USE_STARTTLS` | No | Use STARTTLS encryption | `true` |
| `SMTP_USE_SSL` | No | Use implicit SSL | `false` |
| `POSTMARK_SERVER_TOKEN` | No | Postmark server token (when `EMAIL_PROVIDER=postmark`) | -- |
| `POSTMARK_MESSAGE_STREAM` | No | Postmark message stream | `outbound` |

### Reverse Proxy

| Variable | Required | Description | Default |
|---|---|---|---|
| `DOMAIN` | No | Domain name for the built-in Caddy proxy. Caddy provisions HTTPS automatically via Let's Encrypt when a real domain is set. | `localhost` |

---

## Optional Features

MyMascada works out of the box with only the required configuration. Each optional
feature adds functionality but is not needed for core operation.

**AI Categorization (OpenAI)** -- Automatically categorizes imported transactions
using OpenAI. Without it, use manual categorization and rule-based matching, which
are always available.

**Google OAuth** -- Adds a "Sign in with Google" button to the login page. Without
it, email and password authentication is always available.

**Bank Sync (Akahu)** -- Automatic bank account synchronization for New Zealand
banks via the Akahu API. Without it, import transactions manually using CSV or OFX
files.

**Email Notifications** -- Enables password reset emails, email verification, and
notification delivery. Without it, the application still works but password reset
requires direct database intervention by an administrator.

---

## Production Deployment (HTTPS)

For production use, you should serve the application over HTTPS. Two options are
provided.

### Option 1: Built-in Caddy (recommended)

Caddy is included in the Docker Compose file as an optional profile. It
automatically provisions and renews SSL certificates via Let's Encrypt.

1. Set your domain in `.env`:

   ```
   DOMAIN=finance.example.com
   FRONTEND_URL=https://finance.example.com
   PUBLIC_API_URL=https://finance.example.com
   CORS_ALLOWED_ORIGINS=https://finance.example.com
   ```

   `PUBLIC_API_URL` must match `FRONTEND_URL` because Caddy routes `/api/*`
   requests to the API container through the same domain.

2. Ensure ports 80 and 443 are open on your server and your DNS A record points
   to the server's public IP.

3. Start with the proxy profile:

   ```bash
   docker compose --profile proxy up -d
   ```

Caddy handles SSL certificate provisioning, renewal, and HTTP-to-HTTPS redirection
automatically. Both the API (`/api/*`) and frontend (`/`) are served through a
single domain.

### Option 2: External Nginx

An example Nginx configuration is provided at `deploy/nginx.conf.example`.

1. Copy the example config:

   ```bash
   sudo cp deploy/nginx.conf.example /etc/nginx/sites-available/mymascada
   sudo ln -s /etc/nginx/sites-available/mymascada /etc/nginx/sites-enabled/
   ```

2. Edit the config and replace `finance.example.com` with your domain.

3. Set the public API URL in `.env` to match your domain (Nginx routes `/api/*`
   to the API):

   ```
   FRONTEND_URL=https://finance.example.com
   PUBLIC_API_URL=https://finance.example.com
   CORS_ALLOWED_ORIGINS=https://finance.example.com
   ```

4. Obtain SSL certificates with certbot:

   ```bash
   sudo certbot --nginx -d finance.example.com
   ```

5. Start the application without the Caddy profile:

   ```bash
   docker compose up -d
   ```

   Nginx proxies to ports 5126 (API) and 3000 (frontend) on the host.

6. Reload Nginx:

   ```bash
   sudo nginx -t && sudo systemctl reload nginx
   ```

---

## Updating

Pull the latest images and restart the containers. Database migrations run
automatically on startup.

```bash
docker compose pull && docker compose up -d
```

If using the Caddy proxy:

```bash
docker compose pull && docker compose --profile proxy up -d
```

To pin a specific version instead of `latest`:

```bash
# Example: pin to v1.0.1
docker compose pull ghcr.io/digaomatias/mymascada/api:1.0.1
docker compose pull ghcr.io/digaomatias/mymascada/migration:1.0.1
docker compose pull ghcr.io/digaomatias/mymascada/frontend:1.0.1
docker compose up -d
```

---

## Backup and Restore

### Database Backup

```bash
docker compose exec postgres pg_dump -U mymascada mymascada > backup_$(date +%Y%m%d).sql
```

### Automated Backup Schedule

Set up a cron job for regular automated backups:

```bash
# Edit crontab
crontab -e

# Add daily backup at 2:00 AM (adjust path as needed)
0 2 * * * cd /path/to/mymascada && docker compose exec -T postgres pg_dump -U mymascada mymascada | gzip > /path/to/backups/mymascada_$(date +\%Y\%m\%d).sql.gz

# Optional: remove backups older than 30 days
0 3 * * * find /path/to/backups -name "mymascada_*.sql.gz" -mtime +30 -delete
```

### Database Restore

```bash
docker compose exec -T postgres psql -U mymascada mymascada < backup.sql
```

For compressed backups:

```bash
gunzip -c mymascada_20250201.sql.gz | docker compose exec -T postgres psql -U mymascada mymascada
```

### Docker Volumes

The following Docker volumes contain persistent data:

| Volume | Contents |
|---|---|
| `postgres-data` | PostgreSQL database files |
| `redis-data` | Redis cache and session data |
| `api-logs` | Application log files |
| `api-data` | Application data (data protection keys, uploads) |
| `caddy-data` | SSL certificates (if using Caddy) |
| `caddy-config` | Caddy configuration state (if using Caddy) |

To back up a volume manually:

```bash
docker run --rm -v mymascada_postgres-data:/data -v $(pwd):/backup \
  alpine tar czf /backup/postgres-data.tar.gz -C /data .
```

### Data Protection Keys

ASP.NET Core uses Data Protection keys to encrypt cookies, tokens, and other
sensitive data. These keys are stored in the `api-data` volume. **If you lose
this volume, all existing authentication tokens will be invalidated** and users
will need to log in again.

Always include the `api-data` volume in your backups:

```bash
docker run --rm -v mymascada_api-data:/data -v $(pwd):/backup \
  alpine tar czf /backup/api-data.tar.gz -C /data .
```

---

## Troubleshooting

**"DB_PASSWORD is required"**
The `.env` file is missing or `DB_PASSWORD` is not set. Run `./setup.sh` or copy
`.env.example` to `.env` and set `DB_PASSWORD` to a secure value.

**Migration fails on startup**
Check the migration container logs:
```bash
docker compose logs migration
```
Common causes: the database is not ready yet (usually resolves on retry), or a
previous migration left the database in an inconsistent state. If the postgres
container is not healthy, check its logs with `docker compose logs postgres`.

**Frontend cannot reach the API**
Verify that `CORS_ALLOWED_ORIGINS` in `.env` includes your `FRONTEND_URL`. If
using a reverse proxy, make sure both the internal Docker URL and the external
public URL are included. For example:
```
CORS_ALLOWED_ORIGINS=https://finance.example.com,http://localhost:3000
```

**Email not sending**
1. Confirm `EMAIL_ENABLED=true` in `.env`.
2. Verify SMTP credentials are correct.
3. Check the API container logs for email errors:
   ```bash
   docker compose logs api | grep -i email
   ```
4. Some SMTP providers require app-specific passwords or have sending limits.

**Containers keep restarting**
Check container logs for the failing service:
```bash
docker compose logs --tail=50 api
docker compose logs --tail=50 frontend
```

**Port conflicts**
If ports 3000 or 5126 are already in use on the host, either stop the conflicting
service or modify the port mappings in `docker-compose.yml`. When using the Caddy
proxy, ports 80 and 443 must also be available.

**Checking service health**
```bash
docker compose ps
curl http://localhost:5126/health
```

---

## Architecture Overview

The Docker Compose stack runs the following services:

```
                        +-------------------+
                        |   Caddy (proxy)   |  :80, :443
                        |   (optional)      |
                        +--------+----------+
                                 |
                    +------------+------------+
                    |                         |
           +-------v-------+       +---------v---------+
           |    Frontend    |       |       API         |
           |   (Next.js)   |       |  (ASP.NET Core)   |
           |    :3000       |       |     :5126         |
           +---------------+       +----+----+---------+
                                        |    |
                              +---------+    +----------+
                              |                         |
                     +--------v--------+     +----------v---------+
                     |   PostgreSQL    |     |       Redis        |
                     |    :5432        |     |      :6379         |
                     +-----------------+     +--------------------+
```

On first startup, a one-time **migration** container runs EF Core migrations
against the database and then exits. The API container waits for the migration to
complete before starting.
