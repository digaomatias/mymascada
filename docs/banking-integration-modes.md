# Banking Integration Modes (Akahu) — Architecture Note

## Context
MyMascada currently supports Akahu via **user-provided personal tokens** (`app_token + user_token`) stored per user. We now also need to support first-party Akahu app credentials (`Akahu__AppIdToken` + `Akahu__AppSecret`) to enable hosted OAuth, without breaking existing personal-token users.

## Options considered

### A) Mutate existing Akahu flow with flags/modes sprinkled in current code
- **Pros:** fastest to ship.
- **Cons:** mode checks spread through controllers/handlers/UI; higher regression risk; harder to extend to future providers.
- **Migration risk:** medium/high due to implicit branching and duplicated logic.

### B) Add separate provider/integration ID for first-party Akahu
- Example: `akahu-personal` and `akahu-oauth` as separate providers.
- **Pros:** explicit separation.
- **Cons:** duplicates provider implementation details, complicates UX (looks like 2 providers for same bank network), migration requires remapping provider IDs.
- **Migration risk:** medium (data/model branching).

### C) Hybrid strategy abstraction (recommended)
- Keep provider ID stable (`akahu`), but introduce provider capability/mode resolution:
  - `supportedAuthModes`
  - `defaultAuthMode`
- Runtime resolver decides mode from environment/config.
- UI reads provider metadata and adapts behavior.
- Existing personal-token endpoints remain intact for backward compatibility.

## Decision
Adopt **Option C (hybrid strategy abstraction)**.

### Why
1. **Lowest migration risk:** existing provider IDs/endpoints remain usable.
2. **Best UX path:** user still chooses “Akahu” once; app picks sensible default mode.
3. **Future extensibility:** same mode metadata model can power additional providers and auth strategies.

## Implemented behavior matrix

| Environment config | Reported Akahu modes | Default mode | Connect behavior |
|---|---|---|---|
| `Akahu__AppIdToken` missing OR `Akahu__AppSecret` missing | `personal_tokens` | `personal_tokens` | Existing personal credentials flow |
| Both `Akahu__AppIdToken` and `Akahu__AppSecret` set | `personal_tokens`, `hosted_oauth` | `hosted_oauth` | Hosted OAuth initiation (no user token entry required) |

Notes:
- Personal mode is **not removed**.
- Existing Akahu personal-token API endpoints are preserved.
- OAuth exchange endpoint now supports fallback to server-configured AppIdToken when request token is omitted (compat shim for current frontend behavior).

## Config
- `Akahu__Enabled=true`
- `Akahu__AppIdToken` (required for hosted OAuth default mode)
- `Akahu__AppSecret` (required for hosted OAuth default mode)
- `Akahu__RedirectUri` (used by OAuth URL/token exchange)
- `Akahu__OAuthBaseUrl` and `Akahu__ApiBaseUrl` (optional overrides)

## Follow-on opportunities
- Add provider-agnostic connect endpoint (`/bankconnections/{providerId}/initiate`) once another connector is introduced.
- Persist user-selected mode preference when both modes are available.
