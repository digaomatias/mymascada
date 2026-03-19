# MyMascada Frontend Code Review

**Date:** 2026-03-19
**Scope:** `frontend/src/` — Next.js 16 + React 19 + Tailwind v4
**Reviewer:** Automated (Claude)
**Files reviewed:** ~165 TypeScript/TSX files (~57,600 LOC)

---

## Summary

| Severity | Count |
|----------|-------|
| Critical | 10 |
| Major | 30 |
| Minor | 28 |
| Nitpick | 10 |
| **Total** | **78** |

---

## Critical Issues

### C-1. AuthContext value object recreated on every render — cascading re-renders across entire app

**File:** `src/contexts/auth-context.tsx:319-330`

The `value` object passed to `AuthContext.Provider` is a new object on every render. None of the handler functions (`login`, `register`, `logout`, `loginWithToken`, `refreshUser`, `retryTokenValidation`, `refreshAccessToken`) are wrapped in `useCallback`. Since AuthProvider wraps the entire app (root layout), **every state change in AuthProvider triggers re-renders across the entire app tree**.

**Fix:** Wrap all handler functions in `useCallback`. Wrap the `value` object in `useMemo`:
```tsx
const value = useMemo(() => ({
  user, isLoading, isAuthenticated,
  login, register, loginWithToken, logout,
  refreshUser, retryTokenValidation, refreshAccessToken,
}), [user, isLoading, isAuthenticated, login, register, ...]);
```

---

### C-2. `next-pwa` v5.6.0 is incompatible with Next.js 16

**File:** `package.json:40`

`next-pwa` v5.6.0 was last updated in 2022 for Next.js 12-13. The project uses Next.js 16.1.6. The webpack plugin API has changed substantially. With `disable: true` in development, this masks build issues that may surface in production (broken service worker, silent failures).

**Fix:** Migrate to `@ducanh2912/next-pwa` (maintained fork) or `serwist` (spiritual successor with full Next.js 15+ support). If PWA is not critical, remove `next-pwa` entirely.

---

### C-3. Missing `<Suspense>` boundary around `useSearchParams` — build errors in Next.js 15+

**Files:**
- `src/app/auth/verify-email/page.tsx:4,16`
- `src/app/transactions/[id]/edit/page.tsx:67`

`useSearchParams()` is called directly in the default export without a `<Suspense>` wrapper. In Next.js 15+, this causes the page to opt out of static rendering and can cause hydration mismatches. Other pages (dashboard, import, reset-password) correctly use the `<Suspense>` wrapper pattern.

**Fix:** Split into an inner content component and wrap in `<Suspense>`:
```tsx
function VerifyEmailContent() { /* uses useSearchParams */ }
export default function VerifyEmailPage() {
  return <Suspense fallback={...}><VerifyEmailContent /></Suspense>;
}
```

---

### C-4. Transactions page is 1791-line god component with 17+ useState hooks

**File:** `src/app/transactions/page.tsx` (1791 lines)

A single component (`TransactionsPageContent`) contains 17+ `useState` hooks (lines 135-150), inline filter panel JSX (~200 lines), full transaction table with mobile/desktop variants, touch event handlers, 5 dialog/modal renderings, pagination, bulk action toolbar, category assignment inline, and transfer creation inline. Any small state change (toggling a menu, typing in search) re-renders the entire 1791-line tree.

**Fix:** Extract into sub-components: `TransactionFilters`, `TransactionTable`, `TransactionRow`, `BulkActionsBar`, `TransactionDialogs`. Move the `Transaction` interface to `types/transaction.ts`.

---

### C-5. `Category` interface duplicated 7 times with divergent shapes

**Files:**
- `src/components/forms/category-picker.tsx:42`
- `src/components/forms/category-filter.tsx:13`
- `src/components/forms/category-autocomplete.tsx:12`
- `src/components/forms/transaction-form.tsx:28`
- `src/components/modals/create-transaction-modal.tsx:29`
- `src/components/rules/rule-builder-wizard.tsx:25`
- `src/components/bank-category-mappings.tsx:55`

Each file defines its own `Category` interface with slightly different fields (some include `canonicalKey`, `fullPath`, `isSystem`, others don't). Type mismatches are latent.

**Fix:** Create a single `Category` type in `types/category.ts`. Use `Pick<>` or `Partial<>` for component-specific subsets.

---

### C-6. `BankTransaction` interface duplicated 8 times

**Files:** `reconciliation-file-upload.tsx:15` (exported), `reconciliation-modal.tsx:63`, `create-transaction-modal.tsx:19`, `transaction-comparison.tsx:16`, `reconciliation-details-view.tsx:29`, `reconciliation-balance-cards.tsx:15`, `draggable-transaction-card.tsx:18`, `manual-matching-modal.tsx:15`

The canonical export exists in `reconciliation-file-upload.tsx`, yet 7 other files redefine their own local copies.

**Fix:** Move to `types/reconciliation.ts` and import everywhere.

---

### C-7. `Transaction` interface duplicated 11+ times across pages and components

**Files:**
- `src/app/transactions/page.tsx:61`
- `src/app/transactions/[id]/page.tsx:43`
- `src/app/transactions/[id]/edit/page.tsx:20`
- `src/app/transactions/categorize/page.tsx:34`
- `src/app/categories/[id]/page.tsx:41`
- `src/components/transaction-list.tsx:50`
- `src/components/modals/edit-transaction-modal.tsx:18`
- `src/components/forms/inline-transfer-creator.tsx:20`
- `src/components/dashboard/cards/recent-transactions-card.tsx:11`
- `src/components/forms/categorization-ribbon.tsx:22`
- `src/components/forms/llm-categorization-section.tsx:20`

**Fix:** Define a single `Transaction` type in `types/transaction.ts`.

---

### C-8. `CSVAnalysisResult` interface duplicated 4 times

**Files:**
- `src/components/forms/csv-mapping-review.tsx:91`
- `src/components/forms/csv-mapping-review-reconciliation.tsx:88`
- `src/components/forms/ai-csv-upload.tsx:21`
- `src/components/forms/ai-csv-upload-reconciliation.tsx:21`

**Fix:** Move to `types/csv-import.ts`.

---

### C-9. `parseDateWithFormat` function copy-pasted across two files

**Files:**
- `src/components/forms/csv-mapping-review.tsx:20-80` (~60 lines)
- `src/components/forms/csv-mapping-review-reconciliation.tsx:20-79` (~60 lines)

Identical code. A bug fix in one won't reach the other.

**Fix:** Extract into `lib/date-utils.ts`.

---

### C-10. Onboarding page redirects to non-existent path

**File:** `src/app/onboarding/page.tsx:14`

`router.replace('/login')` redirects to `/login`, but the login page is at `/auth/login`. This causes a 404.

**Fix:** Change to `router.replace('/auth/login')`.

---

## Major Issues

### M-1. 37 API methods return `Promise<unknown>` — destroying type safety

**File:** `src/lib/api-client.ts`
**Lines:** 250, 294, 348, 373, 388, 406, 424, 546, 577, 590, 621, 628, 638, 646, 710, 714, 718, 722, 726, 737, 762, 781, 790, 815, 836, 847, 863, 876, 906, 1133, 1270, 1282, 1291, 1303, 1313, 1327, 1354, 1960

37 API methods return `Promise<unknown>`. Every call site must use unsafe `as` casts. This is the most pervasive type safety problem in the codebase.

**Fix:** Define response DTOs for each endpoint: `getAccounts(): Promise<AccountDto[]>`, etc.

---

### M-2. `post()` and `put()` accept `any` for request bodies

**File:** `src/lib/api-client.ts:119,127`

```ts
async post<T>(endpoint: string, data?: any): Promise<T>
async put<T>(endpoint: string, data?: any): Promise<T>
```

**Fix:** Change to `data?: unknown` or use specific request types.

---

### M-3. All data fetching is client-side — no Server Components used

**Files:** Every authenticated `page.tsx`

Every page uses `'use client'` and fetches data via `apiClient` in `useEffect`. Zero pages leverage Server Components or Server Actions. This means no streaming, no request deduplication, full loading spinners before content, and larger client bundles.

**Fix:** Convert list/detail pages to Server Components with server-side data fetching.

---

### M-4. Inconsistent auth guard patterns — two different approaches

~10 pages use the `useAuthGuard` hook. 20+ pages use manual `useAuth() + useEffect` redirect logic (duplicated in each page).

**Fix:** Standardize on `useAuthGuard` for all pages, or add a Next.js `middleware.ts` for server-side auth.

---

### M-5. No `middleware.ts` — unauthenticated users download the full app bundle

There is no Next.js middleware for server-side route protection. Unauthenticated users load the entire JS bundle before being redirected client-side. For a financial app, this briefly flashes page structure before redirect.

**Fix:** Add `middleware.ts` that checks for auth tokens and redirects server-side.

---

### M-6. No route-specific error boundaries

**File:** Only `src/app/error.tsx` exists globally.

If `/transactions` throws, the entire app shows the global error page. No contextual recovery.

**Fix:** Add `error.tsx` for major route groups: `transactions/`, `settings/`, `accounts/`, etc.

---

### M-7. Debug `console.log` statements left in production code

**Key locations:**
- `src/lib/api-client.ts:648-649` — logs request URL and parameters
- `src/components/forms/category-picker.tsx:180-206` — `[CategoryPicker Debug]` on every suggestion update
- `src/components/import-review/import-review-screen.tsx:97-98,213-243` — extensive emoji-decorated debug logs
- `src/app/accounts/[id]/reconcile/page.tsx:224` — `console.log` debug statement
- 90+ total `console.log/error` statements across 30 files

**Fix:** Remove all debug logs. Replace `console.error` with a structured logging utility.

---

### M-8. `exportTransactionsCsv` and `exportUserData` bypass token refresh

**File:** `src/lib/api-client.ts:435-464,1884-1901`

These methods use raw `fetch()` calls, bypassing the centralized `request()` method. They don't benefit from automatic 401 token refresh, error handling, or URL rewriting.

**Fix:** Create a `requestRaw()` variant or add manual 401 detection.

---

### M-9. Request header merge order bug in `request()` method

**File:** `src/lib/api-client.ts:147-163`

```ts
const config: RequestInit = {
  headers: { 'Content-Type': 'application/json', ...options.headers },
  credentials: 'include',
  ...options,  // This can overwrite headers with the caller's original headers
};
```

The `...options` spread can overwrite the already-merged headers.

**Fix:** Merge headers explicitly after all spreads.

---

### M-10. No `AbortController` support in API client

**File:** `src/lib/api-client.ts:138-225`

The `request()` method does not accept an `AbortSignal`. The vast majority of API methods don't support cancellation. Components cannot cancel in-flight requests on unmount. Only 2 of 48 page files use `AbortController`.

**Fix:** Add optional `signal?: AbortSignal` parameter to commonly used methods.

---

### M-11. Duplicated token refresh logic in auth-context (4 copies)

**File:** `src/contexts/auth-context.tsx:57-115, 252-270, 273-292, 295-316`

The "try getCurrentUser, catch 401, try refreshToken, clear if that fails" pattern is repeated in `initializeAuth`, `refreshUser`, `retryTokenValidation`, and `refreshAccessToken`.

**Fix:** Extract a shared `validateOrRefreshSession()` helper.

---

### M-12. `window.confirm()` used instead of `<ConfirmationDialog>`

**Files:**
- `src/app/rules/page.tsx:162,457`
- `src/app/categories/page.tsx:132`
- `src/app/categories/[id]/page.tsx:190`

`window.confirm()` is used for destructive actions. This breaks the design language, can't be styled or localized.

**Fix:** Replace with `<ConfirmationDialog>`.

---

### M-13. Native `<select>` used instead of `<Select>` component (11 instances)

**Files:**
- `src/app/transactions/page.tsx:1025,1043,1058,1073,1088,1103` (6 instances)
- `src/app/categories/new/page.tsx:239`
- `src/app/categories/[id]/edit/page.tsx:294`
- `src/app/import/page.tsx:663`
- `src/app/rules/page.tsx:593,612`

Violates the project's component rules requiring `<Select>` from `@/components/ui/select`.

---

### M-14. Inconsistent color tokens — `gray-*` and `slate-*` used instead of `ink-*`

**Files:** `src/app/import/page.tsx` (15+ occurrences), `src/app/auth/register/page.tsx`, `src/app/privacy/page.tsx`, `src/app/terms/page.tsx`, `src/app/settings/billing/page.tsx`, `src/app/settings/bank-connections/callback/page.tsx`

The design system uses `ink-*` tokens, but many pages use raw Tailwind `gray-*` or `slate-*`.

---

### M-15. Privacy and Terms pages not localized

**Files:** `src/app/privacy/page.tsx` (199 lines), `src/app/terms/page.tsx` (139 lines)

All text is hardcoded in English. Per project rules, all user-facing strings must be localized.

---

### M-16. `CategoryPicker` dropdown lacks ARIA roles

**File:** `src/components/forms/category-picker.tsx`

The combobox/listbox pattern has no `role="combobox"`, `aria-expanded`, `aria-haspopup`, `aria-activedescendant`, `role="option"`, or `role="listbox"` attributes.

---

### M-17. Custom modal in Rules page lacks focus trap and ARIA attributes

**File:** `src/app/rules/page.tsx`

The rule preview modal uses a raw `fixed` div instead of `<BaseModal>`. No focus trap, no escape-to-close, no `aria-modal`/`role="dialog"`.

---

### M-18. No focus management on route navigation

**Files:** All page files

When navigating between pages, focus is not managed. No `aria-live` regions for dynamic content updates.

---

### M-19. Unused dependencies: `@google-cloud/local-auth` and `google-auth-library`

**File:** `package.json:23,36`

Neither is imported anywhere in `src/`. They are server-side Node.js libraries adding unnecessary weight and attack surface.

**Fix:** Remove from `dependencies`.

---

### M-20. `tailwind.config.ts` is dead code in Tailwind v4

**File:** `tailwind.config.ts`

The project uses `@tailwindcss/postcss` v4.2.1 with `@import 'tailwindcss'` and `@theme` directives in `globals.css`. No `@config` directive references the config file. The `content` paths and `zIndex` extensions are not being applied.

**Fix:** Delete `tailwind.config.ts` or add `@config '../tailwind.config.ts'` to `globals.css`.

---

### M-21. Two duplicate account creation modals

**Files:**
- `src/components/modals/add-account-modal.tsx` (51 lines) — uses `BaseModal`
- `src/components/modals/account-creation-modal.tsx` (81 lines) — hand-rolls its own overlay

**Fix:** Consolidate into one component using `BaseModal`.

---

### M-22. `create-transaction-modal.tsx` does not use `BaseModal`

**File:** `src/components/modals/create-transaction-modal.tsx:148-311`

Manually renders overlay. No focus trap, no Transition animations, no `aria-modal`.

**Fix:** Refactor to use `<BaseModal>`.

---

### M-23. `getConfidenceColor` function duplicated 8 times

**Files:** `category-picker.tsx:317`, `csv-mapping-review-reconciliation.tsx:551`, `duplicates-modal.tsx:38`, `transaction-comparison.tsx:54`, `manual-matching-modal.tsx:97`, `rule-suggestions-view.tsx:93`, `bank-category-mappings.tsx:251`, `confidence-indicator.tsx:14`

Some have different thresholds, creating inconsistency.

**Fix:** Extract to `lib/confidence-utils.ts`.

---

### M-24. `csv-mapping-review.tsx` and `csv-mapping-review-reconciliation.tsx` share ~80% code

**Files:**
- `src/components/forms/csv-mapping-review.tsx` (1033 lines)
- `src/components/forms/csv-mapping-review-reconciliation.tsx` (875 lines)

Same interfaces, same `parseDateWithFormat`, `detectDateFormat`, column mapping UI, date format selector, amount convention handling.

**Fix:** Extract shared logic into a base component or hook.

---

### M-25. `transaction-list.tsx` is 1453 lines

**File:** `src/components/transaction-list.tsx` (1453 lines)

Handles search, 7 filter types, pagination, bulk selection, bulk actions, inline transfer creation, transfer grouping, CSV export, long press detection.

**Fix:** Extract sub-components and a `useTransactionList` custom hook.

---

### M-26. Duplicate `formatCurrency` function with incompatible signatures

**Files:**
- `src/lib/utils.ts:8` — accepts `locale` parameter
- `src/types/budget.ts:176` — hardcoded to `'en-NZ'`

`budget-health-insight.tsx` imports the hardcoded version, never respecting the user's locale.

**Fix:** Delete from `budget.ts`, use `@/lib/utils` everywhere.

---

### M-27. `formatDate` and `formatDateTime` hardcoded to `'en-US'` locale

**File:** `src/lib/utils.ts:15,36`

The app supports `en` and `pt-BR`, but date formatting ignores locale.

**Fix:** Add `locale` parameter, pass from i18n context.

---

### M-28. Dual account type systems (`AccountType` 0-based + `BackendAccountType` 1-based)

**File:** `src/lib/utils.ts:83-174`

Two enums, a mapping object, and two conversion functions for the same concept.

**Fix:** Use only the backend representation and convert at the form boundary.

---

### M-29. Stale closure bug in `useDropdownBehavior` scroll restoration

**File:** `src/hooks/useDropdownBehaviorHook.tsx:37,40`

`scrollPosition` state is in the dependency array but also set inside the effect. The cleanup function captures the stale value, and the dependency triggers unnecessary re-runs.

**Fix:** Use a ref instead of state for scroll position.

---

### M-30. `ImportAnalysisService` class is entirely dead code (~460 lines)

**File:** `src/lib/import-analysis-service.ts:25-462`

Neither `analyzeImportCandidates` nor `createAnalysisSummary` is called anywhere. The app uses the backend API instead.

**Fix:** Remove the file and its test file.

---

## Minor Issues

### m-1. Context value objects not memoized in 3 additional contexts

**Files:**
- `src/contexts/dashboard-context.tsx:70`
- `src/contexts/features-context.tsx:60`
- `src/contexts/locale-context.tsx:52-59`
- `src/contexts/ai-suggestions-context.tsx:151-158`

**Fix:** Wrap each `value` in `useMemo`.

---

### m-2. `isTokenStructurallyValid` defined inside AuthProvider component

**File:** `src/contexts/auth-context.tsx:132-156`

Pure function with no state dependencies. Recreated on every render.

**Fix:** Move outside the component as a module-level function.

---

### m-3. `formatDateTime` has no error handling unlike `formatDate`

**File:** `src/lib/utils.ts:36-45`

**Fix:** Add the same try/catch guard.

---

### m-4. `formatDateTime` is never used

**File:** `src/lib/utils.ts:36-45`

Exported but never imported.

**Fix:** Remove.

---

### m-5. `classifyAmount` is never used

**File:** `src/lib/utils.ts:76-80`

**Fix:** Remove.

---

### m-6. No nested layouts — `<AppLayout>` re-mounts on every navigation

Every authenticated page independently wraps itself in `<AppLayout>`.

**Fix:** Create a `(dashboard)/layout.tsx` route group for authenticated pages.

---

### m-7. Hardcoded `bg-[#faf8ff]` magic value in 23 loading states

23 pages use the same hardcoded color for loading backgrounds.

**Fix:** Define as a design token.

---

### m-8. 18 `eslint-disable react-hooks/exhaustive-deps` suppressions in pages

**Files:** `analytics/page.tsx`, `analytics/trends/page.tsx`, `categories/[id]/page.tsx`, `categories/[id]/edit/page.tsx`, `import/page.tsx`, `accounts/[id]/page.tsx`, `rules/page.tsx`, `transactions/page.tsx`, `transactions/[id]/page.tsx`, `transactions/[id]/edit/page.tsx`, and more.

**Fix:** Wrap fetch functions in `useCallback`, then remove the suppressions.

---

### m-9. 13 additional `eslint-disable react-hooks/exhaustive-deps` in components

**Files:** `transaction-list.tsx`, `reconciliation-modal.tsx`, `transfers-modal.tsx`, `create-transaction-modal.tsx`, `edit-transaction-modal.tsx`, `share-account-modal.tsx`, `rule-suggestions-view.tsx`, `duplicates-modal.tsx`, `rule-builder-wizard.tsx`, `csv-mapping-review.tsx`, and more.

---

### m-10. Categories detail fetches 1000 transactions client-side for stats

**File:** `src/app/categories/[id]/page.tsx`

Should use a server-side aggregation endpoint.

---

### m-11. Build tools in `dependencies` instead of `devDependencies`

**File:** `package.json`

`@types/react`, `@types/react-dom`, `eslint`, `eslint-config-next`, `typescript`, `postcss` should be in `devDependencies`.

---

### m-12. `tsconfig.json` targets ES2017 but uses ES6 lib

**File:** `tsconfig.json:3-8`

`lib: ["dom", "dom.iterable", "ES6"]` should be `ES2017` or `ESNext` to match target.

---

### m-13. Hardcoded English strings in `category-icons.tsx` labels

**File:** `src/lib/category-icons.tsx:174-197`

`availableIcons` labels are hardcoded English strings.

---

### m-14. Hardcoded English strings in `goal-type-config.ts`

**File:** `src/lib/goals/goal-type-config.ts`

`heroMetric`, `pickGoalNudge`, `JOURNEY_STAGES`, `TRACKING_STATE_STYLES` all use hardcoded English strings.

---

### m-15. Hardcoded English strings in various components

**Files:** `add-account-modal.tsx`, `account-creation-modal.tsx`, `add-transaction-modal.tsx`, `description-autocomplete.tsx`, `category-autocomplete.tsx`, `transaction-list.tsx`, `smart-back-button.tsx`

---

### m-16. `accountTypeLabels` not localized

**File:** `src/lib/utils.ts:114-122`

---

### m-17. `lucide-react` used alongside `@heroicons/react` (violates icon standard)

**Files:** `src/components/reconciliation/draggable-transaction-card.tsx`, `src/components/ui/dropdown-menu.tsx`

**Fix:** Replace with Heroicons equivalents. Remove `lucide-react` from `package.json`.

---

### m-18. Runtime utility functions in `types/budget.ts`

**File:** `src/types/budget.ts:91-117,157-181`

`getTrendColor`, `getRecommendationBadgeColor`, etc. belong in `lib/budget-utils.ts`.

---

### m-19. `BackButton` and `SmartBackButton` overlap in functionality

**Files:** `src/components/ui/back-button.tsx` (75 lines), `src/components/ui/smart-back-button.tsx` (84 lines)

**Fix:** Consolidate into one component.

---

### m-20. Duplicated group selection logic in `duplicates-modal` and `transfers-modal`

**Files:** `src/components/modals/duplicates-modal.tsx:96-173`, `src/components/modals/transfers-modal.tsx:86-117`

**Fix:** Extract a `useGroupSelection` custom hook.

---

### m-21. `use-transaction-filters` hook returns 29 values and mixes concerns

**File:** `src/hooks/use-transaction-filters.ts:92-129`

Mixes URL filter state, selection mode state, bulk processing state, and reference data.

**Fix:** Split into focused hooks.

---

### m-22. `CoachingInsightCard` accepts unused props (lint-suppressed)

**File:** `src/components/dashboard/coaching-insight-card.tsx:10-19`

`monthlyIncome` and `monthlyExpenses` are accepted but unused.

---

### m-23. `use-device-detect.ts` uses `resize` event instead of `matchMedia`

**File:** `src/hooks/use-device-detect.ts:6-20`

`matchMedia` listener fires only on breakpoint change, not every pixel.

---

### m-24. Naming convention inconsistency: `useDropdownBehaviorHook.tsx`

**File:** `src/hooks/useDropdownBehaviorHook.tsx`

All other hooks use kebab-case. Rename to `use-dropdown-behavior.tsx`.

---

### m-25. `workbox-webpack-plugin` likely unnecessary with `next-pwa`

**File:** `package.json:52`

Not imported anywhere. `next-pwa` bundles workbox internally.

---

### m-26. Global `*:focus { outline: hidden }` removes outlines for all focus methods

**File:** `src/app/globals.css:412-414`

**Fix:** Change to `*:focus:not(:focus-visible) { @apply outline-hidden; }`.

---

### m-27. `mobile-overrides.css` uses `!important` excessively

**File:** `src/styles/mobile-overrides.css:7-8,13,36-39`

Forces ALL `[role="listbox"]` to bottom-sheet style on mobile.

---

### m-28. `getSuggestionsForTransaction` mutates state as a side effect

**File:** `src/contexts/ai-suggestions-context.tsx:60-77`

A getter function that triggers state updates (cache cleanup), cascading re-renders.

**Fix:** Use `useRef` for cache or remove cleanup from the getter.

---

## Nitpick Issues

### n-1. Dead API methods in `api-client.ts`

**File:** `src/lib/api-client.ts`

The following methods are defined but never called:
- `healthCheck()` (line 242)
- `isAuthenticated()` (line 326)
- `getCsvFormats()` (line 990)
- `downloadCsvTemplate()` (line 1107)
- `uploadCsvFile()` (line 1125)
- `validateCsvMappings()` (line 1074)
- `importOfxFile()` (line 914)
- `getRuleSuggestionsSummary()` (line 1545)
- `getBankCategoryMapping()` (singular, line 1693)
- `getReconciliations()` (line 1266)
- `getReconciliation()` (line 1282)
- `updateReconciliation()` (line 1298)
- `getReconciliationItems()` (line 1323)
- `acceptShare(token)` and `declineShare(token)` (token-based, line 1933)

**Fix:** Remove unused methods.

---

### n-2. Commented-out `TransactionListResponse` interface

**File:** `src/app/transactions/page.tsx:79-85`

---

### n-3. `export const dynamic = 'force-dynamic'` on client components

**Files:** `src/app/settings/bank-connections/page.tsx:466`, `src/app/settings/bank-connections/callback/page.tsx:193`, `src/app/settings/telegram/page.tsx:312`

This is a Server Component config; on `'use client'` pages it does nothing.

---

### n-4. `ImportReviewTypes` const export is an anti-pattern

**File:** `src/types/import-review.ts:274`

Redundant re-export of enums as a runtime object.

---

### n-5. `isLoadingAiSuggestions` prop declared but never used in `CategoryPicker`

**File:** `src/components/forms/category-picker.tsx:112`

---

### n-6. `DEFAULT_DESCRIPTION_SIMILARITY_THRESHOLD` never referenced

**File:** `src/lib/import-analysis-service.ts:20`

---

### n-7. `shimmer` keyframe animation in CSS may be unused

**File:** `src/app/globals.css:434-441`

---

### n-8. Unused `React` default import in `useDropdownBehaviorHook.tsx`

**File:** `src/hooks/useDropdownBehaviorHook.tsx:1`

---

### n-9. Confidence scoring inconsistency (0-100 vs 0-1 scale)

**File:** `src/lib/import-analysis-service.ts:111-162`

Scores assigned on 0-100 scale but compared against 0-1 thresholds.

---

### n-10. `global-error.tsx` has hardcoded purple button color

**File:** `src/app/global-error.tsx:45`

`backgroundColor: '#6b46c1'` — violates the "purge purple" design decision.

---

## Top 10 Priority Fixes

1. **C-1**: Memoize AuthContext value (every state change re-renders the entire app)
2. **C-2**: Update or replace `next-pwa` v5 for Next.js 16 compatibility
3. **C-4**: Break up the 1791-line transactions page god component
4. **C-5/6/7/8**: Consolidate all duplicated type definitions into `types/`
5. **M-1**: Type the 37 `Promise<unknown>` API methods properly
6. **M-5**: Add `middleware.ts` for server-side auth to prevent flash of authenticated UI
7. **M-7**: Remove all debug `console.log` statements from production code
8. **C-3**: Add `<Suspense>` boundaries around `useSearchParams` calls
9. **M-19**: Remove unused `@google-cloud/local-auth` and `google-auth-library` dependencies
10. **C-10**: Fix onboarding redirect to correct `/auth/login` path

---

## Metrics Snapshot

| Metric | Value |
|--------|-------|
| Total files reviewed | ~165 |
| `'use client'` files | 165 (100% — no server components in use) |
| `useEffect` hooks | 59 across 40 files |
| `useMemo`/`useCallback`/`React.memo` | 137 across 40 files |
| `console.log/error` statements | 92 across 30 files |
| `eslint-disable` suppressions | 31 |
| `any` / `unknown` types | 5 `any`, 37 `Promise<unknown>` |
| `dynamic()` / lazy imports | 1 (only `BankCategoryMappings`) |
| `Suspense` boundaries | 8 pages (of 48) |
| `AbortController` usage | 6 files (of 48 that fetch data) |
| Files >500 lines | 23 |
| Files >1000 lines | 5 (`api-client.ts`, `transactions/page.tsx`, `transaction-list.tsx`, `category-picker.tsx`, `csv-mapping-review.tsx`) |
