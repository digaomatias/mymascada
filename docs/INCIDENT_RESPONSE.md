# Incident Response Runbook

This document defines how to handle production incidents for MyMascada. It covers severity classification, role assignments, response procedures for common scenarios, and post-incident review.

For vulnerability disclosure and reporting, see [SECURITY.md](../SECURITY.md).

---

## Table of Contents

- [Severity Levels](#severity-levels)
- [Roles](#roles)
- [General Response Process](#general-response-process)
- [Scenario: Credential Compromise](#scenario-credential-compromise)
- [Scenario: Data Breach](#scenario-data-breach)
- [Scenario: Service Outage](#scenario-service-outage)
- [Scenario: Third-Party Provider Incident](#scenario-third-party-provider-incident)
- [Communication Templates](#communication-templates)
- [Post-Incident Review](#post-incident-review)
- [NZ Privacy Act Obligations](#nz-privacy-act-obligations)
- [Appendix: Key Contacts and Resources](#appendix-key-contacts-and-resources)

---

## Severity Levels

| Severity | Definition | Response SLA | Examples |
|----------|-----------|-------------|----------|
| **P1 -- Critical** | Active data breach, credential leak, or complete service outage affecting all users | Acknowledge within **15 minutes**, begin mitigation immediately | Database credentials exposed publicly; Akahu tokens leaked; production database compromised |
| **P2 -- High** | Partial outage, security vulnerability being actively exploited, or data integrity issue | Acknowledge within **1 hour**, begin mitigation within 2 hours | Auth bypass discovered in production; bank sync corrupting transaction data; API unresponsive |
| **P3 -- Medium** | Degraded service, non-critical security issue, or isolated data problem | Acknowledge within **4 hours**, resolve within 24 hours | Slow API responses; single user's data inconsistency; failed background jobs piling up |
| **P4 -- Low** | Minor issue with workaround available, cosmetic problems, or informational security finding | Acknowledge within **24 hours**, resolve within 7 days | Incorrect UI labels; non-sensitive log verbosity issue; stale cache entries |

When in doubt, **escalate up** -- treat the incident as one level higher until you've confirmed the actual impact.

---

## Roles

### Incident Commander (IC)

The person who declares the incident and coordinates the response. For a small self-hosted deployment, this is typically the server administrator.

**Responsibilities:**

- Declare incident severity and open a tracking thread (Telegram group, GitHub issue, or similar)
- Coordinate containment and remediation actions
- Decide when to communicate externally (if applicable)
- Ensure the post-incident review happens
- Own the incident timeline -- document every action with timestamps

### Responder

Anyone actively working on diagnosing or fixing the incident, directed by the IC.

### Communicator

Handles external communication (user notifications, regulatory bodies). Can be the same person as the IC for small teams.

---

## General Response Process

Every incident follows these phases, regardless of type:

### 1. Detect and Triage (Minutes 0-15)

1. Confirm the issue is real -- check logs, monitoring, and user reports
2. Assign a severity level (see table above)
3. Open an incident thread and record the start time
4. Notify relevant people

### 2. Contain (Minimize Blast Radius)

1. Stop the bleeding -- prevent further damage before root-causing
2. Preserve evidence (logs, database snapshots, screenshots)
3. Document every action taken with timestamps

### 3. Remediate (Fix the Root Cause)

1. Identify the root cause
2. Implement and test the fix
3. Deploy the fix to production
4. Verify the fix resolves the issue

### 4. Recover (Restore Normal Operations)

1. Confirm all services are healthy
2. Re-enable any features that were disabled during containment
3. Notify affected users that the issue is resolved

### 5. Review (Post-Incident)

1. Conduct a post-incident review within 72 hours (see [Post-Incident Review](#post-incident-review))
2. File follow-up tasks for systemic improvements

---

## Scenario: Credential Compromise

**Applies when:** API keys, JWT secrets, database credentials, Akahu tokens, OpenAI keys, or OAuth client secrets are exposed.

### Containment Steps

1. **Identify what was exposed** -- determine which credential(s) and how (public repo commit, log leak, compromised server, etc.)
2. **Revoke immediately:**
   - **JWT secret** -- change `JwtSettings:Secret` in production config and restart the API. All existing user sessions will be invalidated (users must log in again).
   - **Database password** -- change in PostgreSQL (`ALTER USER ... PASSWORD '...'`), update the connection string in production config, restart the API.
   - **Akahu API keys** -- revoke in [Akahu Dashboard](https://my.akahu.nz/developers), generate new keys, update `Akahu:AppToken` and `Akahu:AppSecret` in production config.
   - **OpenAI API key** -- revoke in [OpenAI Dashboard](https://platform.openai.com/api-keys), generate a new key, update `OpenAI:ApiKey` in production config.
   - **Google OAuth client secret** -- rotate in [Google Cloud Console](https://console.cloud.google.com/apis/credentials), update `GoogleAuth:ClientSecret` in production config.
   - **Telegram bot token** -- revoke via [@BotFather](https://t.me/BotFather), create a new token, update `Telegram:BotToken` in production config.
3. **Restart the API** to pick up new credentials:
   ```bash
   # Production (Mordor): app secrets live in /home/rodrigo/apps/mymascada/api.env.
   # The database password is the exception: after ALTER USER, set the new value
   # as DB_PASSWORD in /home/rodrigo/apps/mymascada/.env (compose builds the API
   # connection string and the backup credentials from it). Then:
   /home/rodrigo/apps/mymascada/repo/deploy/mordor/deploy.sh --force

   # Other self-hosted Docker
   docker compose -f docker-compose.prod.yml up -d --force-recreate api
   ```
4. **Audit access** -- check database query logs and API access logs for unauthorized activity during the exposure window.
5. **If database credentials were compromised** -- review database audit logs for data exfiltration. Consider the incident a potential data breach and follow the [Data Breach](#scenario-data-breach) procedure.

### After Containment

- Scrub the exposed credential from any public sources (force-push to remove from git history if committed, request removal from paste sites, etc.)
- If committed to the repo:
  ```bash
  # Use git-filter-repo or BFG to remove the secret from history
  # Then force-push and notify all contributors to re-clone
  ```
- Enable secret scanning on the repository if not already enabled

---

## Scenario: Data Breach

**Applies when:** Unauthorized access to user financial data (transactions, account balances, bank connections, personal information) is confirmed or suspected.

### Containment Steps

1. **Isolate the attack vector** -- if the breach is via a compromised endpoint, deploy an emergency fix or disable the endpoint:
   ```bash
   # Production (Mordor): stop automatic deploys first - a deploy would start the
   # API again - and make sure none is running, then stop the API container.
   sudo systemctl stop mymascada-deploy.timer
   systemctl is-active mymascada-deploy.service   # wait until "inactive"
   docker stop mymascada-api
   # Resume deploys only after remediation:
   #   sudo systemctl start mymascada-deploy.timer

   # Other self-hosted: stop the API container
   docker compose -f docker-compose.prod.yml stop api
   ```
2. **Preserve evidence** before making changes:
   ```bash
   # Snapshot the database
   pg_dump -h <host> -U <user> mymascada > incident_$(date +%Y%m%d_%H%M%S).sql

   # Archive application logs
   # Production (Mordor)
   docker logs mymascada-api > logs_$(date +%Y%m%d_%H%M%S).txt 2>&1

   # Other self-hosted
   docker compose -f docker-compose.prod.yml logs api > logs_$(date +%Y%m%d_%H%M%S).txt
   ```
3. **Determine scope** -- identify which users and what data was accessed. Check:
   - API access logs for unusual patterns (bulk data access, access from unknown IPs)
   - Database query logs for unexpected queries
   - Whether Akahu bank connection tokens were accessed (these grant read access to users' bank accounts)
4. **Revoke Akahu connections** if bank tokens may have been compromised:
   - Contact Akahu support to revoke affected user tokens
   - Notify affected users to re-authorize their bank connections after the breach is contained
5. **Rotate all credentials** -- follow the [Credential Compromise](#scenario-credential-compromise) procedure for all production secrets, as a precaution.

### After Containment

- Determine if the breach triggers **NZ Privacy Act notification requirements** (see [NZ Privacy Act Obligations](#nz-privacy-act-obligations))
- Notify affected users using the [Data Breach -- User Notification](#data-breach--user-notification) template
- File a detailed timeline for the post-incident review

---

## Scenario: Service Outage

**Applies when:** The application is partially or fully unavailable.

### Diagnosis Checklist

| Component | How to Check | Restart Command |
|-----------|-------------|-----------------|
| **API (Mordor)** | `docker ps --filter name=mymascada` | `docker restart mymascada-api` |
| **API (Self-hosted)** | `docker compose -f docker-compose.prod.yml ps` | `docker compose -f docker-compose.prod.yml restart api` |
| **PostgreSQL** | `docker compose -f docker-compose.prod.yml exec db pg_isready` | `docker compose -f docker-compose.prod.yml restart db` |
| **Redis** | `docker compose -f docker-compose.prod.yml exec redis redis-cli ping` | `docker compose -f docker-compose.prod.yml restart redis` |
| **Frontend (Mordor)** | `docker ps --filter name=mymascada-web` | `docker restart mymascada-web` |
| **Hangfire jobs** | Check `/hangfire` dashboard (requires admin auth) | Restart the API (Hangfire runs in-process) |
| **DNS/TLS** | `curl -I https://your-domain.com` | Check DNS provider and certificate expiry |

### Common Outage Causes and Fixes

**Database connection exhaustion:**
```sql
-- Check active connections
SELECT count(*) FROM pg_stat_activity WHERE datname = 'mymascada';

-- Kill idle connections if needed
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'mymascada' AND state = 'idle' AND query_start < now() - interval '10 minutes';
```

**Disk space full:**
```bash
# Check disk usage
df -h

# Clean Docker resources
docker system prune -f

# Check database size
docker compose -f docker-compose.prod.yml exec db psql -U mymascada -c "SELECT pg_size_pretty(pg_database_size('mymascada'));"
```

**Out of memory:**
```bash
# Check memory usage
free -h

# Check container memory
docker stats --no-stream
```

### Production (Mordor) Specific

See `deploy/mordor/README.md` for the full runbook.

```bash
ssh mordor

# Container status and recent logs
docker ps --filter name=mymascada
docker logs --tail 200 mymascada-api

# Last deploys (pull-based timer)
journalctl -u mymascada-deploy --since today
cat /home/rodrigo/apps/mymascada/deployed-sha /home/rodrigo/apps/mymascada/failed-sha

# Roll back: deploy a known-good commit (the timer redeploys main once it moves)
/home/rodrigo/apps/mymascada/repo/deploy/mordor/deploy.sh <good-commit-sha>
```

Upstream of Mordor: Cloudflare DNS (proxied) → home router port-forward 443 →
nginx-proxy-manager (`server-nginx-proxy-manager-1`). If the containers are
healthy but the site is down, check those in that order.

---

## Scenario: Third-Party Provider Incident

**Applies when:** Akahu, OpenAI, Google OAuth, or another external dependency is degraded or down.

### Akahu Downtime

- **Impact:** Bank account syncing and balance updates stop. Existing transaction data remains available.
- **Detection:** Failed Akahu webhook deliveries, errors in bank sync background jobs (check Hangfire dashboard).
- **Action:** No immediate action required -- MyMascada functions normally without Akahu. The sync will resume automatically when Akahu recovers.
- **User communication:** If prolonged (>4 hours), post a notice that bank syncing is temporarily unavailable.
- **Monitor:** [Akahu Status Page](https://status.akahu.nz/)

### OpenAI API Downtime

- **Impact:** AI-powered transaction categorization stops. Manual categorization and rule-based categorization still work.
- **Detection:** Categorization background jobs failing in Hangfire dashboard.
- **Action:** No immediate action required. Uncategorized transactions queue up and will be processed when the API recovers.
- **Monitor:** [OpenAI Status Page](https://status.openai.com/)

### Google OAuth Downtime

- **Impact:** Users cannot sign in via Google OAuth. Email/password authentication still works.
- **Detection:** Users report Google login failures; errors in authentication middleware logs.
- **Action:** No immediate action required. Users can sign in with email/password as a fallback.
- **Monitor:** [Google Workspace Status](https://www.google.com/appsstatus/dashboard/)

### General Approach

1. Confirm the outage is on the provider's side (check their status page, test from a different network)
2. Verify MyMascada gracefully degrades (no cascading failures, error messages are user-friendly)
3. If the provider outage exposes a vulnerability (e.g., auth bypass when OAuth is down), treat as a P1 security incident

---

## Communication Templates

### Internal Incident Alert

Use this in your team's Telegram group or communication channel:

```
INCIDENT [P1/P2/P3/P4]: [Brief description]
Time detected: [UTC timestamp]
Impact: [What's affected, how many users]
Status: [Investigating / Containing / Remediated]
IC: [Name]
Next update: [Time]
```

### Data Breach -- User Notification

```
Subject: Important Security Notice -- MyMascada

We are writing to inform you of a security incident that affected your
MyMascada account.

What happened: [Clear description of what occurred and when]

What data was involved: [Specific data types -- e.g., transaction history,
account names, email address]

What we've done: [Actions taken to contain and remediate]

What you should do:
- Change your MyMascada password immediately
- [If bank tokens were involved] Re-authorize your bank connections in
  Settings > Connected Accounts
- Review your recent transactions for any unauthorized changes
- [If relevant] Monitor your bank accounts for suspicious activity

If you have questions, reply to this email or contact [support email].

We take the security of your financial data seriously and apologize for
this incident.
```

### Service Outage -- User Notification

```
Subject: MyMascada Service Disruption

MyMascada is currently experiencing [brief description of the issue].

Impact: [What features are affected]
Status: Our team is actively working on a resolution.
Workaround: [If any -- e.g., "Manual transaction entry is still available"]

We will update you when the service is fully restored.
```

---

## Post-Incident Review

Conduct a review within **72 hours** of incident resolution. The goal is to learn and improve, not to assign blame.

### Review Template

Create a document or GitHub issue with this structure:

```markdown
# Post-Incident Review: [Incident Title]

**Date:** [Incident date]
**Severity:** [P1-P4]
**Duration:** [Start time] to [Resolution time] ([total duration])
**Incident Commander:** [Name]

## Summary
[2-3 sentence description of what happened]

## Timeline
| Time (UTC) | Event |
|------------|-------|
| HH:MM | [First detection / alert] |
| HH:MM | [Each significant action taken] |
| HH:MM | [Resolution confirmed] |

## Root Cause
[What specifically caused the incident]

## Impact
- Users affected: [Number or description]
- Data affected: [What data, if any]
- Duration of impact: [How long users were affected]

## What Went Well
- [Things that worked during the response]

## What Needs Improvement
- [Things that slowed down detection or response]

## Action Items
| Action | Owner | Due Date |
|--------|-------|----------|
| [Specific improvement] | [Name] | [Date] |
```

### Follow-Up

- Action items from the review should be filed as GitHub issues and tracked to completion
- Review previous incident reports quarterly to identify recurring patterns

---

## NZ Privacy Act Obligations

MyMascada handles financial data for users who may be New Zealand residents. The [Privacy Act 2020](https://www.legislation.govt.nz/act/public/2020/0031/latest/LMS23223.html) has specific breach notification requirements.

### When Notification Is Required

You must notify the **Office of the Privacy Commissioner (OPC)** if a privacy breach:

1. Has caused **serious harm** to affected individuals, **or**
2. Is **likely to cause serious harm**

Financial data (transaction history, account balances, bank connection details) is considered sensitive -- a breach involving this data is likely to meet the "serious harm" threshold.

### Notification Timeline

- Notify the OPC **as soon as practicable** after becoming aware of a notifiable breach
- Best practice: notify within **72 hours** of confirming the breach
- Notify affected individuals at the same time or as soon as possible after notifying the OPC

### How to Notify

1. **Office of the Privacy Commissioner:** Submit via [NotifyUs](https://privacy.org.nz/responsibilities/privacy-breaches/notify-us/) on the OPC website
2. **Affected individuals:** Use the [Data Breach -- User Notification](#data-breach--user-notification) template above

### What to Include in the OPC Notification

- Description of the breach (what happened, when, how)
- Types of personal information involved
- Number of individuals affected (or estimate)
- Steps taken to contain the breach and reduce harm
- Steps individuals can take to protect themselves
- Contact details for follow-up questions

---

## Appendix: Key Contacts and Resources

| Resource | Location |
|----------|----------|
| Production logs (Mordor) | `ssh mordor docker logs mymascada-api` |
| Production logs (Self-hosted) | `docker compose logs api` |
| Hangfire dashboard | `https://[your-domain]/hangfire` |
| Database backups | Mordor: `/home/rodrigo/apps/mymascada/backups` (daily `pg_dump -Fc`, `mymascada-backup` container) |
| Akahu status | https://status.akahu.nz/ |
| OpenAI status | https://status.openai.com/ |
| NZ Privacy Commissioner | https://privacy.org.nz/responsibilities/privacy-breaches/notify-us/ |
| GitHub Security Advisories | https://github.com/digaomatias/mymascada/security/advisories |
| SECURITY.md (vulnerability disclosure) | [SECURITY.md](../SECURITY.md) |
