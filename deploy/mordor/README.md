# MyMascada on Mordor

Production runs on Mordor, the home server (`192.168.50.225`, `ssh mordor`). It is
for personal, invite-only use; Fly.io was retired in October 2026.

## How it fits together

```
browser / mobile app
  -> Cloudflare (proxied DNS: app., api., apex, www)
  -> home router, port 443
  -> nginx-proxy-manager (server-nginx-proxy-manager-1, network server_default)
       app.mymascada.com  -> mymascada-web:3000
       api.mymascada.com  -> mymascada-api:5126
       mymascada.com, www -> 301 to https://app.mymascada.com
  -> mymascada-api -> mymascada-postgres (internal network only)
```

| Container | Purpose |
|---|---|
| `mymascada-postgres` | Postgres 17, volume `mymascada_pg17_data` |
| `mymascada-api` | ASP.NET API + Hangfire; volume `mymascada_api_data` holds the Data Protection keys |
| `mymascada-web` | Next.js frontend |
| `mymascada-backup` | daily `pg_dump -Fc` into `/home/rodrigo/apps/mymascada/backups` |

## Files on Mordor

Everything lives in `/home/rodrigo/apps/mymascada`:

| Path | What |
|---|---|
| `repo/` | clone of this repository; `deploy.sh` checks out the deployed commit here |
| `.env` | compose interpolation (`DB_PASSWORD`), mode 600 |
| `api.env` | API secrets (JWT key, Google OAuth, Akahu, Resend, OpenAI, Firebase, admin key), mode 600 |
| `backups/` | nightly database dumps |
| `deployed-sha` / `failed-sha` | deploy bookkeeping |

Secrets are never committed. Non-secret config is in `docker-compose.yml` and
ships with the next merge to `main`.

## Deploys

`mymascada-deploy.timer` runs `deploy.sh` every 5 minutes. When `origin/main`
has moved it builds the images on Mordor, runs the EF migration bundle
(`/app/migrate.sh`), then rolls the stack and waits for health checks. A merged
PR is live roughly 5 minutes plus build time later.

Mordor only ever pulls from GitHub, so nothing on the internet can trigger code
on the server. (The repo is public, which is why this is not a self-hosted
GitHub Actions runner.)

```bash
journalctl -u mymascada-deploy -f               # watch deploys
systemctl list-timers mymascada-deploy.timer    # next run
~/apps/mymascada/repo/deploy/mordor/deploy.sh --force      # redeploy main now (clears a pin)
~/apps/mymascada/repo/deploy/mordor/deploy.sh <sha>        # deploy/roll back to a commit and pin it
```

If a commit fails to build, migrate or pass health checks, `deploy.sh` restores
the previous checkout and images (kept as `mymascada-{api,web}:rollback`), brings
them back up, and records the commit in `failed-sha` so the timer skips it until
`main` moves again. **Migrations are not rolled back**, so keep them compatible
with the previous release (additive changes).

`deploy.sh <sha>` writes the current `main` into `pinned-main`; the timer leaves
the pinned deploy alone until `main` moves to a new commit, then resumes.
Commits from before `deploy/mordor/` existed are refused, because checking them
out would delete the deploy script itself.

### Changing a secret

```bash
ssh mordor
nano ~/apps/mymascada/api.env       # KEY='value' - single-quoted, one line each
~/apps/mymascada/repo/deploy/mordor/deploy.sh --force
```

Multi-line values (the Firebase service-account JSON) must be compacted to one
line first: `jq -c . service-account.json`.

## Installing the timer (one-off)

```bash
sudo cp ~/apps/mymascada/repo/deploy/mordor/mymascada-deploy.{service,timer} /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now mymascada-deploy.timer
```

The units point at the script inside `repo/`, so script changes deploy with the
code. Re-copy the unit files only if they change.

## Backups and restore

`mymascada-backup` keeps 7 daily, 4 weekly and 6 monthly dumps in
`~/apps/mymascada/backups`. After each dump, `backup-hooks/10-dp-keys` also saves
the API's **Data Protection keys** to `backups/keys/dp-keys-YYYYMMDD.tgz` (mode
600, kept 30 days). A dump is only useful together with its keys: without them,
encrypted bank tokens and settings cannot be decrypted. Copy both off Mordor
if you want protection against losing the disk.

Restore a dump and its keys:

```bash
cd ~/apps/mymascada
sudo systemctl stop mymascada-deploy.timer
docker stop mymascada-api
docker run --rm -v mymascada_api_data:/data -v "$PWD/backups/keys:/k:ro" alpine \
  sh -c 'rm -rf /data/keys && tar xzf /k/dp-keys-<date>.tgz -C /data'
docker exec -i mymascada-postgres pg_restore -U mymascada -d mymascada \
  --clean --if-exists --no-owner --no-privileges < backups/daily/<file>.dump
docker start mymascada-api
sudo systemctl start mymascada-deploy.timer
```

## TLS

Cloudflare proxies all four hostnames. nginx-proxy-manager holds Let's Encrypt
certificates for the origin and renews them over HTTP-01 through Cloudflare.
