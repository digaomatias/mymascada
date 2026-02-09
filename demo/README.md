# MyMascada Demo Instance

A self-contained demo that boots MyMascada with a pre-seeded user and sample financial data. Data resets on every restart -- visitors can explore freely without permanent changes.

## Demo Credentials

| Field | Value |
|-------|-------|
| Email | `demo@mymascada.com` |
| Password | `DemoPass123!` |

## How It Works

The demo uses a Docker Compose override on top of the base `docker-compose.yml`:

1. **API runs in Development mode** -- enables the `/api/testing/create-test-user` endpoint
2. **Registration is blocked** -- invite codes are required but none are configured, so only the pre-seeded demo user can log in
3. **No persistent volumes** -- PostgreSQL and Redis use ephemeral storage; restarting the stack resets all data
4. **Seed container** -- after the API is healthy, a lightweight Alpine container calls the test endpoint to create the demo user with sample accounts and transactions

### Sample data created

- 3 accounts: Checking ($2,500), Credit Card (-$450), Savings ($5,000)
- 10 transactions: salary, freelance, rent, groceries, coffee, gas, Netflix, Amazon, shopping, electric bill
- 1,400+ default categories

## Quick Start (Local)

All commands are run from the **repo root** (`mymascada/`):

```bash
cp demo/.env.demo demo/.env
```

Generate secrets and paste them into `demo/.env`:

```bash
# Database password
openssl rand -base64 32 | tr -d '\n'; echo

# JWT key
openssl rand -base64 64 | tr -d '\n'; echo
```

Start the stack:

```bash
docker compose -p mymascada-demo --env-file demo/.env \
  -f docker-compose.yml -f demo/docker-compose.demo.yml up -d
```

Wait for all containers to be healthy, then open [http://localhost:3001](http://localhost:3001) and log in with the demo credentials above.

### Resetting the demo

Just restart the stack. All data is ephemeral:

```bash
docker compose -p mymascada-demo --env-file demo/.env \
  -f docker-compose.yml -f demo/docker-compose.demo.yml down

docker compose -p mymascada-demo --env-file demo/.env \
  -f docker-compose.yml -f demo/docker-compose.demo.yml up -d
```

## Deploying to a Server

Same process, but update the URLs in `demo/.env` to match your domain:

```env
FRONTEND_URL=https://demo.mymascada.com
PUBLIC_API_URL=https://demo.mymascada.com/api
CORS_ALLOWED_ORIGINS=https://demo.mymascada.com
```

Then set up a reverse proxy (Nginx Proxy Manager, Caddy, etc.) to route `demo.mymascada.com` to the demo frontend port.

## Port Mapping

| Service | Host Port | Container Port |
|---------|-----------|----------------|
| Frontend | 3001 | 3000 |
| API | 5127 | 5126 |
| PostgreSQL | (none) | 5432 |
