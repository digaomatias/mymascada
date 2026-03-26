# Fly.io Deployment

MyMascada production deployment on Fly.io.

## Prerequisites

1. Install flyctl: `curl -L https://fly.io/install.sh | sh`
2. Login: `flyctl auth login`
3. Create the apps (first time only):
   ```bash
   flyctl apps create mymascada-api
   flyctl apps create mymascada-web
   ```

## Initial Setup

### 1. Create Postgres cluster

```bash
flyctl postgres create \
  --name mymascada-db \
  --region syd \
  --vm-size shared-cpu-1x \
  --volume-size 10 \
  --initial-cluster-size 1
```

### 2. Attach Postgres to API

```bash
flyctl postgres attach mymascada-db --app mymascada-api
```

This automatically sets `DATABASE_URL` as a secret on the API app.

### 3. Set API secrets

```bash
flyctl secrets set --app mymascada-api \
  JWT_KEY="your-jwt-signing-key" \
  JWT_ISSUER="MyMascada" \
  JWT_AUDIENCE="MyMascadaUsers" \
  FRONTEND_URL="https://mymascada-web.fly.dev" \
  CORS_ALLOWED_ORIGINS="https://mymascada-web.fly.dev,https://mymascada.com"
```

Optional integrations:
```bash
flyctl secrets set --app mymascada-api \
  OPENAI_API_KEY="sk-..." \
  GOOGLE_CLIENT_ID="..." \
  GOOGLE_CLIENT_SECRET="..."
```

### 4. Create persistent volume for Data Protection keys

The API requires a persistent volume at `/app/data` to store Data Protection keys.
Without this volume, keys are lost on container restart, breaking encrypted data
(auth cookies, tokens, etc.).

```bash
flyctl volumes create mymascada_data \
  --region syd \
  --size 1 \
  --app mymascada-api
```

> **Warning:** If the volume is not mounted, the API will log a warning on startup
> but will still run. Encrypted data from previous instances will be unreadable.

### 5. Deploy API

```bash
flyctl deploy --config deploy/fly/fly.api.toml
```

### 6. Deploy Frontend

```bash
flyctl deploy --config deploy/fly/fly.frontend.toml
```

## Custom Domain

```bash
# API
flyctl certs create --app mymascada-api api.mymascada.com

# Frontend
flyctl certs create --app mymascada-web mymascada.com
flyctl certs create --app mymascada-web www.mymascada.com
```

Then point DNS:
- `api.mymascada.com` → CNAME `mymascada-api.fly.dev`
- `mymascada.com` → CNAME `mymascada-web.fly.dev` (or A record from `flyctl ips list`)

After setting custom domains, update the frontend build arg and API env:
```bash
# Redeploy frontend with production API URL
# Edit fly.frontend.toml: NEXT_PUBLIC_API_URL = "https://api.mymascada.com"
flyctl deploy --config deploy/fly/fly.frontend.toml

# Update API secrets
flyctl secrets set --app mymascada-api \
  FRONTEND_URL="https://mymascada.com" \
  CORS_ALLOWED_ORIGINS="https://mymascada.com,https://www.mymascada.com"
```

## Scaling

### Add a region
```bash
flyctl scale count 1 --region iad --app mymascada-api
flyctl scale count 1 --region iad --app mymascada-web
```

### Scale up VM
```bash
flyctl scale vm shared-cpu-2x --memory 1024 --app mymascada-api
```

### Postgres read replica
```bash
flyctl postgres create \
  --name mymascada-db-replica \
  --region iad \
  --vm-size shared-cpu-1x \
  --volume-size 10 \
  --initial-cluster-size 1 \
  --fork-from mymascada-db
```

## Monitoring

```bash
flyctl logs --app mymascada-api
flyctl status --app mymascada-api
flyctl dashboard --app mymascada-api
```

## CI/CD

See `.github/workflows/deploy-fly.yml` for GitHub Actions deployment pipeline.

## Connection string format

Fly Postgres provides `DATABASE_URL` in the format:
```
postgres://user:password@mymascada-db.internal:5432/mymascada?sslmode=disable
```

The API's connection string configuration may need to parse this. Check `appsettings.Production.json` for the expected format.

## Notes

- **Auto-stop:** Both apps scale to zero when idle (saves cost)
- **Auto-start:** Fly proxy wakes apps on incoming requests (~1-2s cold start)
- **Internal networking:** Apps communicate via `.internal` DNS (e.g., `mymascada-api.internal:5126`)
- **SSL:** Automatic via Fly proxy, no Caddy needed
- **Region:** `syd` (Sydney) is primary — closest to NZ
