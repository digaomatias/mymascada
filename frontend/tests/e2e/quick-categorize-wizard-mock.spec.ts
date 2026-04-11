import { test, expect, Page } from '@playwright/test';

/**
 * E2E Tests for the Quick-Categorize Onboarding Wizard (Mocked)
 *
 * Verifies the Phase 4 UX flow:
 *   1. Page loads grouped uncategorized transactions from the API
 *   2. User categorizes a group → backend records it + moves to the next group
 *   3. When every group is done, the "all set" completion screen is shown
 *   4. The next group the user sees is the next-highest-frequency one
 *
 * All backend calls are mocked so the test is hermetic and runs quickly.
 */

const AUTH_TOKEN = 'quick-cat-mock-token';

// Three groups of uncategorized transactions, ordered by frequency.
const MOCK_GROUPS = {
  groups: [
    {
      normalizedDescription: 'netflix com',
      sampleDescription: 'NETFLIX.COM',
      transactionCount: 5,
      totalAmount: -74.95,
      transactionIds: [101, 102, 103, 104, 105],
      samples: [
        {
          id: 101,
          description: 'NETFLIX.COM 14/03/2026',
          amount: -14.99,
          transactionDate: '2026-03-14T00:00:00Z',
          accountName: 'Main Checking',
        },
        {
          id: 102,
          description: 'NETFLIX.COM 14/02/2026',
          amount: -14.99,
          transactionDate: '2026-02-14T00:00:00Z',
          accountName: 'Main Checking',
        },
      ],
    },
    {
      normalizedDescription: 'pak n save',
      sampleDescription: 'PAK N SAVE PETONE',
      transactionCount: 3,
      totalAmount: -420.5,
      transactionIds: [201, 202, 203],
      samples: [
        {
          id: 201,
          description: 'PAK N SAVE PETONE',
          amount: -150.2,
          transactionDate: '2026-03-18T00:00:00Z',
          accountName: 'Main Checking',
        },
      ],
    },
    {
      normalizedDescription: 'uber eats',
      sampleDescription: 'UBER *EATS',
      transactionCount: 2,
      totalAmount: -56.3,
      transactionIds: [301, 302],
      samples: [
        {
          id: 301,
          description: 'UBER *EATS 12/03/2026',
          amount: -28.15,
          transactionDate: '2026-03-12T00:00:00Z',
          accountName: 'Main Checking',
        },
      ],
    },
  ],
  totalUncategorized: 10,
  groupedTransactions: 10,
};

const MOCK_CATEGORIES = [
  { id: 1, name: 'Entertainment', fullPath: 'Entertainment', parentId: null, type: 2 },
  { id: 2, name: 'Groceries', fullPath: 'Food > Groceries', parentId: null, type: 2 },
  { id: 3, name: 'Restaurants', fullPath: 'Food > Restaurants', parentId: null, type: 2 },
];

async function setupMocks(page: Page) {
  // Pre-seed auth token so the frontend skips the login redirect.
  // The key MUST be `auth_token` — that is the canonical storage key used by
  // `ApiClient.getToken()` in `frontend/src/lib/api-client.ts`. Seeding
  // `token`/`authToken` instead leaves `isAuthenticated` false and the page
  // redirects to /login before the wizard ever mounts.
  await page.addInitScript((token) => {
    try {
      window.localStorage.setItem('auth_token', token);
    } catch {
      // ignore — test still works without pre-seed
    }
  }, AUTH_TOKEN);

  // Current user (free tier so upsell would be visible elsewhere)
  await page.route('**/api/**/auth/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'test-user',
        email: 'test@example.com',
        userName: 'test',
        firstName: 'Test',
        lastName: 'User',
        fullName: 'Test User',
        currency: 'USD',
        timeZone: 'UTC',
        locale: 'en',
        aiDescriptionCleaning: false,
        hasAiConfigured: false,
        isOnboardingComplete: true,
        subscriptionTier: 'Free',
        isSelfHosted: false,
      }),
    }),
  );

  // Categories for the picker. `ApiClient.request()` rewrites `/api/...`
  // → `/api/latest/...`, so the actual URL contains `api/latest/categories`.
  // `**/api/categories*` would require the two segments to be adjacent and
  // would not match — use `**/api/**/categories*` so the glob survives the
  // version rewrite.
  await page.route('**/api/**/categories*', (route) => {
    if (route.request().method() === 'GET') {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_CATEGORIES),
      });
    } else {
      route.fallback();
    }
  });

  // Uncategorized groups — always return the same 3 for simplicity. The
  // wizard keeps its own index, so the test drives which group is visible.
  await page.route('**/uncategorized-groups*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_GROUPS),
    }),
  );

  // Bulk categorize — echo success with the requested count. The response
  // mirrors the real backend shape, including `updatedTransactionIds` so the
  // wizard's partial-success narrowing logic has the same contract in tests
  // as it does in production.
  await page.route('**/bulk-categorize-group', async (route) => {
    const body = JSON.parse(route.request().postData() || '{}');
    const ids: number[] = body.transactionIds || [];
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        transactionsUpdated: ids.length,
        updatedTransactionIds: ids,
        message: `Successfully categorized ${ids.length} transaction(s)`,
        errors: [],
      }),
    });
  });

  // Rule suggestions summary (for the sidebar badge fetch). Same version-
  // rewrite gotcha as the categories mock — use `**/api/**/...` so the
  // rewritten `/api/latest/RuleSuggestions/summary` URL still matches.
  await page.route('**/api/**/RuleSuggestions/summary', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        totalSuggestions: 0,
      }),
    }),
  );
}

test.describe('Quick-Categorize Wizard (mocked)', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
  });

  test('loads groups and walks through the wizard', async ({ page }) => {
    await page.goto('http://localhost:3000/transactions/quick-categorize');

    // Wait for the first group card. The component loads both groups AND
    // categories in parallel before rendering.
    await expect(page.getByTestId('quick-categorize-group-card')).toBeVisible({
      timeout: 15000,
    });

    // First group is the highest-frequency one
    await expect(page.getByTestId('quick-categorize-group-description')).toHaveText(
      'NETFLIX.COM',
    );

    // Pick a category
    await page.getByTestId('quick-categorize-category-select').selectOption('1');

    // Submit — should request bulk-categorize-group and advance
    const bulkCall = page.waitForRequest((req) =>
      req.url().includes('bulk-categorize-group') && req.method() === 'POST',
    );
    await page.getByTestId('quick-categorize-submit').click();
    const firstRequest = await bulkCall;

    // Verify the bulk-categorize call includes the expected transaction IDs.
    // The wizard no longer sends `normalizedDescription` — the backend now
    // derives the normalized key server-side from the transactions (see the
    // crafted-request security fix in BulkCategorizeGroupCommand.cs). The
    // `recordHistory` flag is set on the first chunk of a batch so the ML
    // handler only gets one history event per user action.
    const firstPayload = JSON.parse(firstRequest.postData() || '{}');
    expect(firstPayload.transactionIds).toEqual([101, 102, 103, 104, 105]);
    expect(firstPayload.categoryId).toBe(1);
    expect(firstPayload.recordHistory).toBe(true);
    expect(firstPayload.normalizedDescription).toBeUndefined();

    // Second group now visible
    await expect(page.getByTestId('quick-categorize-group-description')).toHaveText(
      'PAK N SAVE PETONE',
    );

    // Skip the second group
    await page.getByTestId('quick-categorize-skip').click();

    // Third group now visible
    await expect(page.getByTestId('quick-categorize-group-description')).toHaveText(
      'UBER *EATS',
    );

    // Categorize third group
    await page.getByTestId('quick-categorize-category-select').selectOption('3');
    await page.getByTestId('quick-categorize-submit').click();

    // All-done screen appears
    await expect(page.getByTestId('quick-categorize-done-back')).toBeVisible({
      timeout: 10000,
    });
  });

  test('shows empty state when there are no uncategorized groups', async ({ page }) => {
    await page.route('**/uncategorized-groups*', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          groups: [],
          totalUncategorized: 0,
          groupedTransactions: 0,
        }),
      }),
    );

    await page.goto('http://localhost:3000/transactions/quick-categorize');

    await expect(page.getByTestId('quick-categorize-empty-back')).toBeVisible({
      timeout: 15000,
    });
  });
});
