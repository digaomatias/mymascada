import { test, expect, Page } from '@playwright/test';

// Mobile viewport (iPhone)
const MOBILE_VIEWPORT = { width: 375, height: 812 };

// Create a structurally valid JWT that won't expire during tests
function createMockJwt(): string {
  const header = Buffer.from(JSON.stringify({ alg: 'HS256', typ: 'JWT' })).toString('base64url');
  const payload = Buffer.from(JSON.stringify({
    sub: '1',
    email: 'test@example.com',
    exp: Math.floor(Date.now() / 1000) + 3600
  })).toString('base64url');
  const signature = Buffer.from('mock-signature').toString('base64url');
  return `${header}.${payload}.${signature}`;
}

const MOCK_JWT = createMockJwt();
const MOCK_USER = {
  id: 1,
  email: 'test@example.com',
  username: 'testuser',
  firstName: 'Test',
  lastName: 'User'
};

const mockReconciliationDetails = {
  reconciliationId: 1,
  summary: {
    totalItems: 10,
    exactMatches: 4,
    fuzzyMatches: 3,
    unmatchedBank: 2,
    unmatchedSystem: 1,
    matchPercentage: 70.0
  },
  exactMatches: [
    {
      id: 1,
      reconciliationId: 1,
      transactionId: 1,
      itemType: 1,
      matchConfidence: 0.98,
      matchMethod: 1,
      bankTransaction: {
        bankTransactionId: 'BANK_001',
        amount: -100.50,
        transactionDate: '2024-01-15T10:00:00Z',
        description: 'Grocery Store Purchase',
        bankCategory: 'FOOD'
      },
      systemTransaction: {
        id: 1,
        amount: -100.50,
        description: 'Grocery Store Purchase',
        transactionDate: '2024-01-15T10:00:00Z',
        categoryName: 'Groceries',
        status: 2
      },
      createdAt: '2024-01-15T10:00:00Z',
      updatedAt: '2024-01-15T10:00:00Z',
      displayAmount: '$100.50',
      displayDate: '2024-01-15',
      displayDescription: 'Grocery Store Purchase',
      matchTypeLabel: 'Exact Match',
      matchConfidenceLabel: 'High Confidence'
    }
  ],
  fuzzyMatches: [
    {
      id: 2,
      reconciliationId: 1,
      transactionId: 2,
      itemType: 1,
      matchConfidence: 0.75,
      matchMethod: 2,
      bankTransaction: {
        bankTransactionId: 'BANK_002',
        amount: -50.25,
        transactionDate: '2024-01-14T14:30:00Z',
        description: 'Gas Station ABC',
        bankCategory: 'GAS'
      },
      systemTransaction: {
        id: 2,
        amount: -50.00,
        description: 'Gas Station',
        transactionDate: '2024-01-14T14:30:00Z',
        categoryName: 'Transportation',
        status: 2
      },
      createdAt: '2024-01-14T14:30:00Z',
      updatedAt: '2024-01-14T14:30:00Z',
      displayAmount: '$50.25',
      displayDate: '2024-01-14',
      displayDescription: 'Gas Station ABC',
      matchTypeLabel: 'Fuzzy Match',
      matchConfidenceLabel: 'Medium Confidence'
    }
  ],
  unmatchedBankTransactions: [
    {
      id: 3,
      reconciliationId: 1,
      itemType: 2,
      bankTransaction: {
        bankTransactionId: 'BANK_003',
        amount: -25.99,
        transactionDate: '2024-01-13T16:45:00Z',
        description: 'Online Subscription',
        bankCategory: 'SUBSCRIPTION'
      },
      createdAt: '2024-01-13T16:45:00Z',
      updatedAt: '2024-01-13T16:45:00Z',
      displayAmount: '$25.99',
      displayDate: '2024-01-13',
      displayDescription: 'Online Subscription',
      matchTypeLabel: 'Unmatched Bank',
      matchConfidenceLabel: ''
    }
  ],
  unmatchedSystemTransactions: [
    {
      id: 4,
      reconciliationId: 1,
      transactionId: 3,
      itemType: 3,
      systemTransaction: {
        id: 3,
        amount: -75.00,
        description: 'Cash Withdrawal',
        transactionDate: '2024-01-12T12:00:00Z',
        categoryName: 'Cash',
        status: 2
      },
      createdAt: '2024-01-12T12:00:00Z',
      updatedAt: '2024-01-12T12:00:00Z',
      displayAmount: '$75.00',
      displayDate: '2024-01-12',
      displayDescription: 'Cash Withdrawal',
      matchTypeLabel: 'Unmatched System',
      matchConfidenceLabel: ''
    }
  ]
};

/**
 * Set up all API mocks needed for the reconciliation page to load.
 * The API client rewrites /api/ to /api/latest/ and calls the backend at localhost:5126.
 * Must be called BEFORE page.goto().
 */
async function setupApiMocks(page: Page) {
  // Catch-all for API calls to the backend - intercept everything going to localhost:5126
  await page.route('**/api/latest/auth/refresh', async (route) => {
    await route.fulfill({
      json: {
        isSuccess: true,
        token: MOCK_JWT,
        refreshToken: 'mock-refresh-token',
        user: MOCK_USER,
        expiresAt: new Date(Date.now() + 3600000).toISOString()
      }
    });
  });

  await page.route('**/api/latest/auth/me', async (route) => {
    await route.fulfill({ json: MOCK_USER });
  });

  await page.route('**/api/latest/Features', async (route) => {
    await route.fulfill({
      json: {
        aiCategorization: false,
        bankConnections: false,
        telegramNotifications: false,
        budgets: true,
        goals: true,
        wallets: true,
        googleAuth: false,
        aiChat: false
      }
    });
  });

  await page.route('**/api/latest/accounts/*', async (route) => {
    await route.fulfill({
      json: {
        id: 1,
        name: 'Test Checking Account',
        type: 1,
        typeDisplayName: 'Checking',
        currentBalance: 2500.75,
        currency: 'USD'
      }
    });
  });

  await page.route('**/api/latest/reconciliation/*/details*', async (route) => {
    await route.fulfill({ json: mockReconciliationDetails });
  });

  await page.route('**/api/latest/reconciliation', async (route) => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        json: {
          id: 1,
          accountId: 1,
          statementEndDate: '2024-01-15T00:00:00Z',
          statementEndBalance: 2500.75,
          status: 0
        }
      });
    } else {
      await route.continue();
    }
  });

  await page.route('**/api/latest/reconciliation/*/match-transactions', async (route) => {
    await route.fulfill({
      json: {
        exactMatches: 4,
        fuzzyMatches: 3,
        unmatchedBank: 2,
        unmatchedApp: 1
      }
    });
  });

  await page.route('**/api/latest/akahu/**', async (route) => {
    await route.fulfill({
      json: { isAvailable: false, unavailableReason: 'Not connected' }
    });
  });

  await page.route('**/api/latest/categories*', async (route) => {
    await route.fulfill({ json: [] });
  });

  await page.route('**/api/latest/wallets*', async (route) => {
    await route.fulfill({ json: [] });
  });

  await page.route('**/api/latest/notifications*', async (route) => {
    await route.fulfill({ json: { items: [], totalCount: 0, unreadCount: 0 } });
  });

  // Also catch any non-rewritten /api/ calls
  await page.route('**/api/auth/**', async (route) => {
    if (route.request().url().includes('/refresh')) {
      await route.fulfill({
        json: {
          isSuccess: true,
          token: MOCK_JWT,
          refreshToken: 'mock-refresh-token',
          user: MOCK_USER,
          expiresAt: new Date(Date.now() + 3600000).toISOString()
        }
      });
    } else if (route.request().url().includes('/me')) {
      await route.fulfill({ json: MOCK_USER });
    } else {
      await route.fulfill({ json: {} });
    }
  });
}

/**
 * Simulate the reconciliation flow up to the review step.
 */
async function simulateReconciliationFlow(page: Page) {
  // Wait for the reconciliation page to load
  await expect(page.locator('text=Start Bank Reconciliation')).toBeVisible({ timeout: 15000 });

  // Step 1: Start reconciliation - fill the currency input
  // The CurrencyInput changes placeholder on focus, so use type() after clicking
  const balanceInput = page.locator('input[placeholder="Enter ending balance"]');
  await balanceInput.click();
  await page.keyboard.type('2500.75');

  // Scroll the Start Reconciliation button into view and click
  const startButton = page.locator('button:has-text("Start Reconciliation")');
  await startButton.scrollIntoViewIfNeeded();
  await startButton.click();

  // Step 2: Import - add sample and continue
  await expect(page.locator('button:has-text("Add Sample Transaction")')).toBeVisible({ timeout: 10000 });
  await page.click('button:has-text("Add Sample Transaction")');
  await page.click('button:has-text("Continue to Matching")');

  // Step 3: Match transactions
  await expect(page.locator('button:has-text("Match Transactions")')).toBeVisible({ timeout: 10000 });
  await page.click('button:has-text("Match Transactions")');

  // Wait for the review step to load
  await expect(page.locator('text=Reconciliation Review')).toBeVisible({ timeout: 15000 });
}

test.describe('Reconciliation Mobile Responsive', () => {
  let page: Page;

  test.beforeEach(async ({ browser }) => {
    page = await browser.newPage({ viewport: MOBILE_VIEWPORT });

    // Set up auth token and dismiss cookie consent via init script (runs before page loads)
    await page.addInitScript((token) => {
      localStorage.setItem('auth_token', token);
      localStorage.setItem('refresh_token', 'mock-refresh-token');
      // Pre-dismiss cookie consent so it doesn't block interactions
      localStorage.setItem('cookie_consent_accepted', 'true');
    }, MOCK_JWT);

    // Set up all API mocks BEFORE navigating
    await setupApiMocks(page);

    // Navigate to the reconciliation page
    await page.goto('/accounts/1/reconcile');
  });

  test.afterEach(async () => {
    await page.close();
  });

  test('tabs are visible and not truncated on mobile', async () => {
    await simulateReconciliationFlow(page);

    // All 4 tab buttons should be present
    const exactTab = page.locator('button:has-text("Exact Matches")');
    const fuzzyTab = page.locator('button:has-text("Fuzzy Matches")');
    await expect(exactTab).toBeVisible();
    await expect(fuzzyTab).toBeVisible();

    // Tab container should have overflow-x: auto for scrollability
    const tabContainer = exactTab.locator('..');
    const overflowX = await tabContainer.evaluate(el => getComputedStyle(el).overflowX);
    expect(overflowX).toBe('auto');
  });

  test('tab switching works on mobile viewport', async () => {
    await simulateReconciliationFlow(page);

    // Click on Fuzzy Matches tab
    await page.click('button:has-text("Fuzzy Matches")');
    await expect(page.locator('text=Gas Station ABC')).toBeVisible();

    // Scroll to and click on Unmatched Bank tab
    const unmatchedBankTab = page.locator('button:has-text("Unmatched Bank")');
    await unmatchedBankTab.scrollIntoViewIfNeeded();
    await unmatchedBankTab.click();
    await expect(page.locator('text=Online Subscription')).toBeVisible();

    // Scroll to and click on Unmatched System tab
    const unmatchedSystemTab = page.locator('button:has-text("Unmatched System")');
    await unmatchedSystemTab.scrollIntoViewIfNeeded();
    await unmatchedSystemTab.click();
    await expect(page.locator('text=Cash Withdrawal')).toBeVisible();
  });

  test('transaction cards render without horizontal overflow on mobile', async () => {
    await simulateReconciliationFlow(page);

    // Check the page has no horizontal scrollbar
    const hasHorizontalOverflow = await page.evaluate(() => {
      return document.documentElement.scrollWidth > document.documentElement.clientWidth;
    });
    expect(hasHorizontalOverflow).toBe(false);

    // Transaction description should be visible
    await expect(page.locator('text=Grocery Store Purchase')).toBeVisible();
  });

  test('action buttons are accessible on mobile for unmatched bank transactions', async () => {
    await simulateReconciliationFlow(page);

    // Switch to Unmatched Bank tab
    const unmatchedBankTab = page.locator('button:has-text("Unmatched Bank")');
    await unmatchedBankTab.scrollIntoViewIfNeeded();
    await unmatchedBankTab.click();

    // Action buttons should be visible (stacked below content on mobile)
    const matchButton = page.locator('button:has-text("Match")').first();
    await expect(matchButton).toBeVisible();

    const createButton = page.locator('button:has-text("Create")').first();
    await expect(createButton).toBeVisible();

    const importButton = page.locator('button:has-text("Import")').first();
    await expect(importButton).toBeVisible();
  });

  test('quick matching help text is hidden on mobile', async () => {
    await simulateReconciliationFlow(page);

    // Switch to Unmatched Bank tab
    const unmatchedBankTab = page.locator('button:has-text("Unmatched Bank")');
    await unmatchedBankTab.scrollIntoViewIfNeeded();
    await unmatchedBankTab.click();

    // The quick matching help block should be hidden on mobile (uses hidden md:block)
    const helpText = page.locator('text=Quick Matching');
    await expect(helpText).toBeHidden();
  });

  test('balance cards are visible on mobile', async () => {
    await simulateReconciliationFlow(page);

    // Balance cards should be visible in a 2-column grid
    const balanceCardsGrid = page.locator('.grid.grid-cols-2').first();
    await expect(balanceCardsGrid).toBeVisible();
  });

  test('selecting items and bulk actions work on mobile', async () => {
    await simulateReconciliationFlow(page);

    // Switch to Unmatched Bank tab
    const unmatchedBankTab = page.locator('button:has-text("Unmatched Bank")');
    await unmatchedBankTab.scrollIntoViewIfNeeded();
    await unmatchedBankTab.click();

    // Select all checkbox should be visible
    const selectAllLabel = page.locator('text=Select All');
    await expect(selectAllLabel).toBeVisible();

    // Click the select-all checkbox
    const selectAllCheckbox = page.locator('label:has-text("Select All") input[type="checkbox"]');
    await selectAllCheckbox.check();

    // Should show selected count
    await expect(page.locator('text=1 item(s) selected')).toBeVisible();
  });

  test('fuzzy match comparison cards render properly on mobile', async () => {
    await simulateReconciliationFlow(page);

    // Switch to Fuzzy Matches tab
    await page.click('button:has-text("Fuzzy Matches")');

    // Comparison card title should be visible
    await expect(page.locator('text=Transaction Comparison')).toBeVisible();

    // Both transaction descriptions should be visible
    await expect(page.locator('text=Gas Station ABC')).toBeVisible();
    await expect(page.locator('text=Gas Station').first()).toBeVisible();
  });

  test('filters section is usable on mobile', async () => {
    await simulateReconciliationFlow(page);

    // Search input should be visible
    const searchInput = page.locator('input[placeholder="Search by description..."]');
    await expect(searchInput).toBeVisible();

    // Clear filters button should be visible
    const clearButton = page.locator('button:has-text("Clear Filters")');
    await expect(clearButton).toBeVisible();
  });

  test('complete reconciliation and back buttons are accessible on mobile', async () => {
    await simulateReconciliationFlow(page);

    // Scroll to bottom to see action buttons
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));

    const backButton = page.locator('button:has-text("Back to Matching")');
    await expect(backButton).toBeVisible();

    const completeButton = page.locator('button:has-text("Complete Reconciliation")');
    await expect(completeButton).toBeVisible();
  });
});
