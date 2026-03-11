# Open Work Triage Report

**Date:** 2026-03-11
**Branch:** `triage-open-work`
**Scope:** 3 open issues, 24 open Dependabot PRs

---

## Open Issues

### Do Now

| # | Title | Size | Effort | Risk | Next Action |
|---|-------|------|--------|------|-------------|
| [#122](https://github.com/digaomatias/mymascada/issues/122) | Refactor: Extract hardcoded API URLs in tests to shared constants | Quick Win | ~1h | None | Create `frontend/e2e/helpers/config.ts`, find-and-replace all `https://localhost:5126` references in test files |
| [#124](https://github.com/digaomatias/mymascada/issues/124) | Bulk review - mark multiple transactions as reviewed | Quick Win | ~2-3h | Low | Existing bulk-action toolbar already supports category + delete. Add a "Mark Reviewed" button + backend endpoint for batch review |

### Defer

| # | Title | Size | Effort | Risk | Next Action |
|---|-------|------|--------|------|-------------|
| [#102](https://github.com/digaomatias/mymascada/issues/102) | Push notification infrastructure (FCM) | Large | 2-3 days | Medium | Mobile-prep feature. Requires new entity, migration, FCM integration, token management. Defer until mobile app work begins |

---

## Dependabot PRs — Batching Strategy

### BLOCKER: Needs Human Decision

| PR | Package | Version Change | Why |
|----|---------|---------------|-----|
| [#143](https://github.com/digaomatias/mymascada/pull/143) | MediatR | 12.5.0 → 14.1.0 | **v13 introduced a commercial license requirement.** MediatR is now dual-licensed (commercial/OSS). You need a license key from [mediatr.io](https://mediatr.io). Options: (a) purchase a license and upgrade, (b) stay on v12.x and close this PR, (c) evaluate alternatives like [Mediator](https://github.com/martinothamar/Mediator) or plain DI. **Close this PR until a decision is made.** |

**Recommended:** Close #143 with a comment explaining the licensing decision is pending. Add `@dependabot ignore this major version` to stop future PRs for MediatR major bumps.

---

### Batch 1: GitHub Actions (Do Now — safe, zero app-code risk)

All are major version bumps but only affect CI pipelines, not application code. Can be merged together safely.

| PR | Package | Version Change | Risk |
|----|---------|---------------|------|
| [#127](https://github.com/digaomatias/mymascada/pull/127) | actions/checkout | 4 → 6 | Low |
| [#128](https://github.com/digaomatias/mymascada/pull/128) | docker/setup-buildx-action | 3 → 4 | Low |
| [#129](https://github.com/digaomatias/mymascada/pull/129) | docker/login-action | 3 → 4 | Low |
| [#126](https://github.com/digaomatias/mymascada/pull/126) | docker/build-push-action | 6 → 7 | Low |

**Strategy:** Merge one-by-one in rapid succession (they all target `main`). If any conflict, rebase the next. Run CI after each.

---

### Batch 2: NuGet Patch/Minor (Do Now — safe, low risk)

All are patch or minor bumps with no breaking changes expected.

| PR | Package | Version Change | Type | Risk |
|----|---------|---------------|------|------|
| [#144](https://github.com/digaomatias/mymascada/pull/144) | Microsoft.Extensions.Logging.Abstractions | 10.0.2 → 10.0.3 | Patch | None |
| [#142](https://github.com/digaomatias/mymascada/pull/142) | MailKit | 4.15.0 → 4.15.1 | Patch | None |
| [#141](https://github.com/digaomatias/mymascada/pull/141) | Asp.Versioning.Mvc + ApiExplorer | 8.1.0 → 8.1.1 | Patch | None |
| [#148](https://github.com/digaomatias/mymascada/pull/148) | Scriban | 6.5.2 → 6.5.5 | Patch | None |
| [#146](https://github.com/digaomatias/mymascada/pull/146) | Microsoft.SemanticKernel | 1.72.0 → 1.73.0 | Minor | Low |
| [#147](https://github.com/digaomatias/mymascada/pull/147) | NSubstitute | 5.1.0 → 5.3.0 | Minor (test-only) | None |

**Strategy:** Merge sequentially, `dotnet build` after each to verify. These are all safe.

---

### Batch 3: Frontend Safe (Do Now — low risk)

Minor/patch bumps with minimal API surface changes.

| PR | Package | Version Change | Type | Risk |
|----|---------|---------------|------|------|
| [#132](https://github.com/digaomatias/mymascada/pull/132) | lucide-react | 0.576.0 → 0.577.0 | Patch | None |
| [#131](https://github.com/digaomatias/mymascada/pull/131) | @headlessui/react | 2.2.4 → 2.2.9 | Patch | None |
| [#130](https://github.com/digaomatias/mymascada/pull/130) | @sentry/nextjs | 10.41.0 → 10.42.0 | Minor | Low |
| [#137](https://github.com/digaomatias/mymascada/pull/137) | tailwind-merge | 3.4.0 → 3.5.0 | Minor | Low |

**Strategy:** Merge sequentially, run `npm run build` after each.

---

### Batch 4: Frontend Moderate (Do Next — test carefully)

Larger minor jumps that warrant a build + smoke test.

| PR | Package | Version Change | Type | Risk |
|----|---------|---------------|------|------|
| [#139](https://github.com/digaomatias/mymascada/pull/139) | jsdom | 28.0.0 → 28.1.0 | Minor (dev-only) | Low |
| [#136](https://github.com/digaomatias/mymascada/pull/136) | @playwright/test | 1.53.1 → 1.58.2 | Minor (dev-only) | Low — removed `_react`/`_vue` selectors (unlikely in use) |
| [#133](https://github.com/digaomatias/mymascada/pull/133) | @types/node | 24.0.10 → 25.4.0 | Major (dev-only) | Low — type defs only, compile-time check |
| [#135](https://github.com/digaomatias/mymascada/pull/135) | react-day-picker | 9.7.0 → 9.14.0 | Minor (big jump) | Medium — date picker is user-facing, verify calendar UI |
| [#134](https://github.com/digaomatias/mymascada/pull/134) | recharts | 3.2.0 → 3.8.0 | Minor (big jump) | Medium — charts are user-facing, verify analytics dashboard |

**Strategy:** Merge dev-only deps first (#139, #136, #133), run tests. Then merge #135 and #134 separately with manual UI verification.

---

### Isolate: Major Version Bumps (Do Next — one at a time, with testing)

Each needs its own merge + full test cycle.

| PR | Package | Version Change | Risk | Notes |
|----|---------|---------------|------|-------|
| [#145](https://github.com/digaomatias/mymascada/pull/145) | Microsoft.NET.Test.Sdk | 17.8.0 → 18.3.0 | Low-Medium | Test tooling only. Merge, run full test suite. If tests pass, ship it. |
| [#151](https://github.com/digaomatias/mymascada/pull/151) | Serilog + Serilog.Extensions.Logging | 4.0→4.2 + **8.0→10.0** | Medium | Serilog.Extensions.Logging jumps two majors (8→10). Review logging config, verify structured logging still works. |
| [#149](https://github.com/digaomatias/mymascada/pull/149) | Sentry.AspNetCore | 5.5.1 → 6.1.0 | Medium-High | Major version. Check for breaking API changes in Sentry init, middleware config, and error reporting. Test error capture end-to-end. |
| [#138](https://github.com/digaomatias/mymascada/pull/138) | ESLint | 9.29.0 → 10.0.3 | High | Major version. ESLint 10 may require config format changes. Run `npm run lint` and fix any issues before merging. |

---

## Execution Order (Priority Checklist)

### Do Now
- [ ] **Batch 1:** Merge GitHub Actions PRs (#126, #127, #128, #129)
- [ ] **Batch 2:** Merge NuGet patch/minor PRs (#141, #142, #144, #148, #146, #147)
- [ ] **Batch 3:** Merge frontend safe PRs (#130, #131, #132, #137)
- [ ] **Issue #122:** Extract hardcoded API URLs in tests
- [ ] **Issue #124:** Add bulk review button to transaction list

### Do Next
- [ ] **Batch 4 (dev-only):** Merge #139, #136, #133
- [ ] **Batch 4 (UI deps):** Merge #135 (react-day-picker) with UI verification
- [ ] **Batch 4 (UI deps):** Merge #134 (recharts) with UI verification
- [ ] **#145:** Upgrade Microsoft.NET.Test.Sdk (run full test suite)
- [ ] **#151:** Upgrade Serilog stack (verify logging pipeline)
- [ ] **#149:** Upgrade Sentry.AspNetCore (verify error tracking)
- [ ] **#138:** Upgrade ESLint 10 (fix config if needed)

### Blockers Needing Human Decision
- [ ] **#143 MediatR 12→14:** Licensing change (commercial license required since v13). **Decision needed:** Buy license, stay on v12, or migrate to alternative. Close PR until decided.

### Defer
- [ ] **Issue #102:** FCM push notification infrastructure — defer until mobile app work starts

---

## Summary

| Category | Count | Estimated Effort |
|----------|-------|-----------------|
| Issues — Quick Wins | 2 | ~3-4h total |
| Issues — Deferred | 1 | N/A |
| Dependabot — Safe to batch-merge | 14 | ~1-2h (sequential merge + verify) |
| Dependabot — Need isolation + testing | 4 | ~4-6h total |
| Dependabot — Blocked (licensing) | 1 | Decision needed |
| **Total PRs** | **24** | |
