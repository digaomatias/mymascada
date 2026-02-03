import { test, expect, Page } from '@playwright/test';

test.describe('Reconciliation Details View', () => {
  let page: Page;

  test.beforeEach(async ({ browser }) => {
    page = await browser.newPage();
    
    // Navigate to reconciliation page (assuming we have an existing reconciliation)
    await page.goto('/accounts/1/reconcile');
    
    // Mock the authentication
    await page.addInitScript(() => {
      localStorage.setItem('auth_token', 'mock-token');
    });

    // Mock the API calls for reconciliation details
    await page.route('**/api/reconciliation/*/details*', async (route) => {
      const mockResponse = {
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
            itemType: 1, // Matched
            matchConfidence: 0.98,
            matchMethod: 1, // Exact
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
            itemType: 1, // Matched
            matchConfidence: 0.75,
            matchMethod: 2, // Fuzzy
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
            itemType: 2, // UnmatchedBank
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
            itemType: 3, // UnmatchedApp
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
      await route.fulfill({ json: mockResponse });
    });

    // Mock other required API calls
    await page.route('**/api/accounts/*', async (route) => {
      await route.fulfill({
        json: {
          id: 1,
          name: 'Test Checking Account',
          type: 1,
          currentBalance: 2500.75,
          currency: 'USD'
        }
      });
    });

    await page.route('**/api/reconciliation', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          json: {
            id: 1,
            accountId: 1,
            statementEndDate: '2024-01-15T00:00:00Z',
            statementEndBalance: 2500.75,
            status: 0 // InProgress
          }
        });
      }
    });

    await page.route('**/api/reconciliation/*/match-transactions', async (route) => {
      await route.fulfill({
        json: {
          exactMatches: 4,
          fuzzyMatches: 3,
          unmatchedBank: 2,
          unmatchedApp: 1
        }
      });
    });
  });

  test.afterEach(async () => {
    await page.close();
  });

  test('should display reconciliation summary statistics', async () => {
    // Go through reconciliation flow to reach review step
    await simulateReconciliationFlow(page);

    // Check summary statistics are displayed
    await expect(page.locator('text=Total Items')).toBeVisible();
    await expect(page.locator('text=10').first()).toBeVisible(); // Total items
    await expect(page.locator('text=4').first()).toBeVisible(); // Exact matches
    await expect(page.locator('text=3').first()).toBeVisible(); // Fuzzy matches
    await expect(page.locator('text=2').first()).toBeVisible(); // Unmatched bank
    await expect(page.locator('text=1').first()).toBeVisible(); // Unmatched system

    // Check match percentage
    await expect(page.locator('text=70.0% Match Rate')).toBeVisible();
  });

  test('should display tabbed interface with correct counts', async () => {
    await simulateReconciliationFlow(page);

    // Check all tabs are present with correct counts
    await expect(page.locator('button:has-text("Exact Matches") >> text=4')).toBeVisible();
    await expect(page.locator('button:has-text("Fuzzy Matches") >> text=3')).toBeVisible();
    await expect(page.locator('button:has-text("Unmatched Bank") >> text=2')).toBeVisible();
    await expect(page.locator('button:has-text("Unmatched System") >> text=1')).toBeVisible();
  });

  test('should switch between tabs and show appropriate content', async () => {
    await simulateReconciliationFlow(page);

    // Default should show exact matches
    await expect(page.locator('text=Exact Matches (4)')).toBeVisible();
    await expect(page.locator('text=Grocery Store Purchase')).toBeVisible();

    // Switch to fuzzy matches
    await page.click('button:has-text("Fuzzy Matches")');
    await expect(page.locator('text=Fuzzy Matches (3)')).toBeVisible();
    await expect(page.locator('text=Gas Station ABC')).toBeVisible();

    // Switch to unmatched bank
    await page.click('button:has-text("Unmatched Bank")');
    await expect(page.locator('text=Unmatched Bank (2)')).toBeVisible();
    await expect(page.locator('text=Online Subscription')).toBeVisible();

    // Switch to unmatched system
    await page.click('button:has-text("Unmatched System")');
    await expect(page.locator('text=Unmatched System (1)')).toBeVisible();
    await expect(page.locator('text=Cash Withdrawal')).toBeVisible();
  });

  test('should display transaction details with proper formatting', async () => {
    await simulateReconciliationFlow(page);

    // Check exact match details
    await expect(page.locator('text=Grocery Store Purchase')).toBeVisible();
    await expect(page.locator('text=$100.50')).toBeVisible();
    await expect(page.locator('text=2024-01-15')).toBeVisible();
    await expect(page.locator('text=High Confidence')).toBeVisible();
    await expect(page.locator('text=Exact Match')).toBeVisible();
  });

  test('should show side-by-side comparison for fuzzy matches', async () => {
    await simulateReconciliationFlow(page);

    // Switch to fuzzy matches tab
    await page.click('button:has-text("Fuzzy Matches")');

    // Check that both bank and system transaction details are visible
    await expect(page.locator('text=Bank Transaction')).toBeVisible();
    await expect(page.locator('text=System Transaction')).toBeVisible();
    await expect(page.locator('text=Gas Station ABC')).toBeVisible();
    await expect(page.locator('text=Gas Station')).toBeVisible();
    await expect(page.locator('text=$50.25')).toBeVisible();
    await expect(page.locator('text=$50.00')).toBeVisible();
  });

  test('should filter transactions by search term', async () => {
    await simulateReconciliationFlow(page);

    // Type in search box
    await page.fill('input[placeholder="Search by description..."]', 'grocery');

    // Wait for the API call with search parameter
    await page.waitForResponse(
      response => response.url().includes('searchTerm=grocery')
    );

    // Should only show matching transactions
    await expect(page.locator('text=Grocery Store Purchase')).toBeVisible();
  });

  test('should filter transactions by amount range', async () => {
    await simulateReconciliationFlow(page);

    // Fill in amount filters
    await page.locator('input[placeholder="0.00"]').nth(0).fill('50.00'); // Min amount
    await page.locator('input[placeholder="0.00"]').nth(1).fill('100.00'); // Max amount

    // Wait for the API call with amount parameters
    await page.waitForResponse(
      response => response.url().includes('minAmount=50') && response.url().includes('maxAmount=100')
    );

    // Should filter appropriately
    // This would be tested with actual filtering logic
  });

  test('should clear all filters', async () => {
    await simulateReconciliationFlow(page);

    // Add some filters
    await page.fill('input[placeholder="Search by description..."]', 'test');
    await page.locator('input[placeholder="0.00"]').nth(0).fill('50.00');

    // Click clear filters
    await page.click('button:has-text("Clear Filters")');

    // Verify filters are cleared
    await expect(page.locator('input[placeholder="Search by description..."]')).toHaveValue('');
    await expect(page.locator('input[placeholder="0.00"]').nth(0)).toHaveValue('');
  });

  test('should complete reconciliation successfully', async () => {
    await simulateReconciliationFlow(page);

    // Mock the update reconciliation call
    await page.route('**/api/reconciliation/*', async (route) => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          json: {
            id: 1,
            status: 1 // Completed
          }
        });
      }
    });

    // Click complete reconciliation
    await page.click('button:has-text("Complete Reconciliation")');

    // Should see success message
    await expect(page.locator('text=Reconciliation Complete!')).toBeVisible();
  });

  test('should navigate back to matching step', async () => {
    await simulateReconciliationFlow(page);

    // Click back button
    await page.click('button:has-text("Back to Matching")');

    // Should be back on matching step
    await expect(page.locator('text=Match Transactions')).toBeVisible();
  });

  test('should display empty state when no data matches filters', async () => {
    await simulateReconciliationFlow(page);

    // Mock API to return empty results
    await page.route('**/api/reconciliation/*/details*', async (route) => {
      const mockResponse = {
        reconciliationId: 1,
        summary: {
          totalItems: 0,
          exactMatches: 0,
          fuzzyMatches: 0,
          unmatchedBank: 0,
          unmatchedSystem: 0,
          matchPercentage: 0
        },
        exactMatches: [],
        fuzzyMatches: [],
        unmatchedBankTransactions: [],
        unmatchedSystemTransactions: []
      };
      await route.fulfill({ json: mockResponse });
    });

    // Add a search filter
    await page.fill('input[placeholder="Search by description..."]', 'nonexistent');

    // Should show empty state
    await expect(page.locator('text=No Exact Matches')).toBeVisible();
    await expect(page.locator('text=No transactions match your current filters.')).toBeVisible();
  });

  test('should show color-coded transaction status indicators', async () => {
    await simulateReconciliationFlow(page);

    // Check exact matches have green indicators
    await expect(page.locator('[class*="text-green-600"]')).toBeVisible();

    // Switch to fuzzy matches and check yellow indicators
    await page.click('button:has-text("Fuzzy Matches")');
    await expect(page.locator('[class*="text-yellow-600"]')).toBeVisible();

    // Switch to unmatched and check red indicators
    await page.click('button:has-text("Unmatched Bank")');
    await expect(page.locator('[class*="text-red-600"]')).toBeVisible();
  });

  // Helper function to simulate the reconciliation flow up to the review step
  async function simulateReconciliationFlow(page: Page) {
    // Step 1: Start reconciliation
    await page.fill('input[type="number"]', '2500.75');
    await page.click('button:has-text("Start Reconciliation")');

    // Step 2: Skip import (assume we have sample transactions)
    await page.click('button:has-text("Add Sample Transaction")');
    await page.click('button:has-text("Continue to Matching")');

    // Step 3: Match transactions
    await page.click('button:has-text("Match Transactions")');

    // Wait for the review step to load
    await expect(page.locator('text=Reconciliation Review')).toBeVisible();
  }
});

test.describe('Reconciliation Details Error Handling', () => {
  let page: Page;

  test.beforeEach(async ({ browser }) => {
    page = await browser.newPage();
    await page.goto('/accounts/1/reconcile');

    // Mock authentication
    await page.addInitScript(() => {
      localStorage.setItem('auth_token', 'mock-token');
    });
  });

  test.afterEach(async () => {
    await page.close();
  });

  test('should handle API errors gracefully', async () => {
    // Mock API failure
    await page.route('**/api/reconciliation/*/details*', async (route) => {
      await route.fulfill({
        status: 500,
        json: { message: 'Internal server error' }
      });
    });

    // Navigate to review step (would need to simulate flow)
    // Should show error message via toast
    await expect(page.locator('text=Failed to load reconciliation details')).toBeVisible();
  });

  test('should show error state when reconciliation ID is missing', async () => {
    // Navigate directly to a review step without reconciliation ID
    await page.goto('/accounts/1/reconcile?step=review');

    // Should show error message
    await expect(page.locator('text=Error: Reconciliation ID not found')).toBeVisible();
  });
});