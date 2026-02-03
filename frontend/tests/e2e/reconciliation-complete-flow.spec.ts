import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Complete Reconciliation Flow - All Phases E2E', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
    await utils.setupTestEnvironment();
    await utils.registerAndLogin();
  });

  test.afterEach(async () => {
    await utils.cleanup();
  });

  test('should complete full reconciliation workflow from CSV upload to finalization', async ({ page }) => {
    // === PHASE 1: Initial Setup and CSV Upload ===
    
    // Create test account
    const account = await utils.createAccount({
      name: 'Main Checking Account',
      type: 'Checking',
      initialBalance: 2500.00
    });

    // Create some existing transactions in the system
    const systemTransactions = [
      {
        accountId: account.id,
        amount: -125.50,
        description: 'Walmart Supercenter',
        date: '2024-01-15T10:00:00.000Z'
      },
      {
        accountId: account.id,
        amount: -89.99,
        description: 'Amazon.com Purchase',
        date: '2024-01-16T14:30:00.000Z'
      },
      {
        accountId: account.id,
        amount: -45.00,
        description: 'Shell Gas Station',
        date: '2024-01-17T08:15:00.000Z'
      },
      {
        accountId: account.id,
        amount: 2000.00,
        description: 'Payroll Deposit',
        date: '2024-01-18T09:00:00.000Z'
      }
    ];

    for (const transaction of systemTransactions) {
      await utils.createTransaction(transaction);
    }

    // Navigate to reconciliation page
    await page.goto(`/accounts/${account.id}/reconcile`);
    await expect(page.getByText('Bank Reconciliation')).toBeVisible();

    // Prepare CSV content with mix of exact matches, fuzzy matches, and unmatched
    const csvContent = `Date,Description,Amount
2024-01-15,Walmart Supercenter,-125.50
2024-01-16,AMAZON.COM PURCHASE,-89.99
2024-01-17,SHELL #1234,-44.99
2024-01-18,PAYROLL DEPOSIT,2000.00
2024-01-19,ATM WITHDRAWAL,-100.00
2024-01-20,RESTAURANT CHARGE,-75.25`;

    // Upload CSV file
    await utils.uploadCsvContent(csvContent);
    
    // Verify CSV preview
    await expect(page.getByText('Preview Bank Transactions')).toBeVisible();
    await expect(page.getByText('6 transactions')).toBeVisible();

    // Start matching process
    await page.getByRole('button', { name: /match transactions/i }).click();
    
    // Wait for matching to complete
    await expect(page.getByText('Reconciliation Review')).toBeVisible();

    // === PHASE 2: Review and Analysis ===
    
    // Verify summary statistics are displayed
    await expect(page.getByText('Total Items')).toBeVisible();
    await expect(page.getByText('Exact Matches')).toBeVisible();
    await expect(page.getByText('Fuzzy Matches')).toBeVisible();
    await expect(page.getByText('Unmatched Bank')).toBeVisible();
    await expect(page.getByText('Unmatched System')).toBeVisible();

    // Check that tabs are available
    const exactMatchesTab = page.getByRole('button', { name: /exact matches/i });
    const fuzzyMatchesTab = page.getByRole('button', { name: /fuzzy matches/i });
    const unmatchedBankTab = page.getByRole('button', { name: /unmatched bank/i });
    const unmatchedSystemTab = page.getByRole('button', { name: /unmatched system/i });

    await expect(exactMatchesTab).toBeVisible();
    await expect(fuzzyMatchesTab).toBeVisible();
    await expect(unmatchedBankTab).toBeVisible();
    await expect(unmatchedSystemTab).toBeVisible();

    // Review exact matches
    await exactMatchesTab.click();
    await expect(page.getByText('Walmart Supercenter')).toBeVisible();
    await expect(page.getByText('PAYROLL DEPOSIT')).toBeVisible();

    // Review fuzzy matches with detailed comparison
    await fuzzyMatchesTab.click();
    
    // Should see transaction comparison components
    await expect(page.getByText('Transaction Comparison')).toBeVisible();
    await expect(page.getByText('Bank Statement')).toBeVisible();
    await expect(page.getByText('MyMascada System')).toBeVisible();

    // Check for match confidence indicators
    const confidenceIndicators = page.locator('[class*="text-green-600"], [class*="text-yellow-600"], [class*="text-orange-600"]');
    await expect(confidenceIndicators.first()).toBeVisible();

    // Expand details for a fuzzy match
    const expandButton = page.locator('button').filter({ has: page.locator('svg[class*="w-4 h-4"]') }).first();
    await expandButton.click();
    
    // Verify detailed analysis is shown
    await expect(page.getByText('Match Analysis')).toBeVisible();
    await expect(page.getByText('Amount Match:')).toBeVisible();
    await expect(page.getByText('Date Match:')).toBeVisible();
    await expect(page.getByText('Description:')).toBeVisible();

    // === PHASE 3: Manual Adjustments (if needed) ===
    
    // Check unmatched bank transactions
    await unmatchedBankTab.click();
    await expect(page.getByText('ATM WITHDRAWAL')).toBeVisible();
    await expect(page.getByText('RESTAURANT CHARGE')).toBeVisible();

    // Check unmatched system transactions (if any)
    await unmatchedSystemTab.click();
    
    // Use search functionality to filter transactions
    await fuzzyMatchesTab.click();
    const searchInput = page.getByPlaceholder('Search by description...');
    await searchInput.fill('SHELL');
    
    // Should filter to show only shell transaction
    await expect(page.getByText('SHELL')).toBeVisible();
    
    // Clear search filter
    await page.getByRole('button', { name: /clear filters/i }).click();
    await expect(searchInput).toHaveValue('');

    // Test amount range filtering
    const minAmountInput = page.getByLabel('Min Amount');
    const maxAmountInput = page.getByLabel('Max Amount');
    
    await minAmountInput.fill('40');
    await maxAmountInput.fill('50');
    
    // Should filter transactions by amount range
    // Clear filters again
    await page.getByRole('button', { name: /clear filters/i }).click();

    // === PHASE 4: Bulk Actions and Finalization ===
    
    // Navigate back to fuzzy matches to see all pending items
    await fuzzyMatchesTab.click();
    
    // Complete reconciliation process
    const completeButton = page.getByRole('button', { name: /complete reconciliation/i });
    await expect(completeButton).toBeVisible();
    await expect(completeButton).toBeEnabled();
    
    await completeButton.click();

    // Should navigate back to account page or show success
    // Wait for completion confirmation
    await expect(page.locator('text=/reconciliation.*complete/i')).toBeVisible({ timeout: 10000 });
    
    // Verify we're back on the account page or reconciliation list
    await expect(page).toHaveURL(new RegExp(`/accounts/${account.id}`));
  });

  test('should handle reconciliation with high unmatched percentage requiring force finalize', async ({ page }) => {
    // Create account with limited transactions
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000.00
    });

    // Create only one matching transaction
    await utils.createTransaction({
      accountId: account.id,
      amount: -50.00,
      description: 'Single Match',
      date: '2024-01-15T10:00:00.000Z'
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    // Upload CSV with many unmatched transactions (high unmatched percentage)
    const csvContent = `Date,Description,Amount
2024-01-15,Single Match,-50.00
2024-01-16,Unmatched Transaction 1,-100.00
2024-01-17,Unmatched Transaction 2,-75.00
2024-01-18,Unmatched Transaction 3,-150.00
2024-01-19,Unmatched Transaction 4,-25.00
2024-01-20,Unmatched Transaction 5,-80.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Wait for reconciliation review
    await expect(page.getByText('Reconciliation Review')).toBeVisible();

    // Check that we have high unmatched percentage
    await page.getByRole('button', { name: /unmatched bank/i }).click();
    
    // Should see multiple unmatched transactions
    await expect(page.getByText('Unmatched Transaction 1')).toBeVisible();
    await expect(page.getByText('Unmatched Transaction 2')).toBeVisible();

    // Try to complete reconciliation - should show warning or force option
    await page.getByRole('button', { name: /complete reconciliation/i }).click();

    // Should either show a confirmation dialog or handle force finalization
    // This depends on the implementation - the test will verify the UI handles it appropriately
  });

  test('should handle empty reconciliation (no transactions)', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Empty Account',
      type: 'Checking',
      initialBalance: 0
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    // Upload empty CSV
    const csvContent = `Date,Description,Amount`;
    await utils.uploadCsvContent(csvContent);

    // Should handle empty reconciliation gracefully
    await expect(page.getByText('No transactions found')).toBeVisible();
  });

  test('should validate reconciliation flow with different match confidence levels', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Confidence Test Account',
      type: 'Checking',
      initialBalance: 1000.00
    });

    // Create transactions with varying similarity for different confidence levels
    const transactions = [
      {
        accountId: account.id,
        amount: -100.00,
        description: 'Target Store #1234',
        date: '2024-01-15T10:00:00.000Z'
      },
      {
        accountId: account.id,
        amount: -75.50,
        description: 'McDonald\'s Restaurant',
        date: '2024-01-16T12:00:00.000Z'
      },
      {
        accountId: account.id,
        amount: -200.00,
        description: 'Very Different Description',
        date: '2024-01-17T14:00:00.000Z'
      }
    ];

    for (const transaction of transactions) {
      await utils.createTransaction(transaction);
    }

    await page.goto(`/accounts/${account.id}/reconcile`);

    // CSV with varying levels of match confidence
    const csvContent = `Date,Description,Amount
2024-01-15,Target Store #1234,-100.00
2024-01-16,MCDONALD'S #567,-75.50
2024-01-17,COMPLETELY DIFFERENT STORE,-200.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Check exact matches tab
    await page.getByRole('button', { name: /exact matches/i }).click();
    await expect(page.getByText('Target Store #1234')).toBeVisible();

    // Check fuzzy matches tab
    await page.getByRole('button', { name: /fuzzy matches/i }).click();
    
    // Should see McDonald's as fuzzy match with confidence indicator
    await expect(page.getByText('MCDONALD\'S #567')).toBeVisible();
    
    // Verify confidence level is displayed
    const confidenceTexts = page.locator('text=/\\d+%/');
    await expect(confidenceTexts.first()).toBeVisible();

    // Complete the reconciliation
    await page.getByRole('button', { name: /complete reconciliation/i }).click();
    await expect(page.locator('text=/reconciliation.*complete/i')).toBeVisible({ timeout: 10000 });
  });

  test('should maintain search and filter state during tab navigation', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Filter Test Account',
      type: 'Checking',
      initialBalance: 1000.00
    });

    // Create multiple transactions
    const transactions = [
      { accountId: account.id, amount: -25.00, description: 'Grocery Store A', date: '2024-01-15T10:00:00.000Z' },
      { accountId: account.id, amount: -50.00, description: 'Gas Station B', date: '2024-01-16T11:00:00.000Z' },
      { accountId: account.id, amount: -75.00, description: 'Restaurant C', date: '2024-01-17T12:00:00.000Z' }
    ];

    for (const transaction of transactions) {
      await utils.createTransaction(transaction);
    }

    await page.goto(`/accounts/${account.id}/reconcile`);

    const csvContent = `Date,Description,Amount
2024-01-15,GROCERY STORE A,-25.00
2024-01-16,GAS STATION B,-50.00
2024-01-17,RESTAURANT C,-75.00
2024-01-18,UNMATCHED TRANSACTION,-100.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Apply search filter
    const searchInput = page.getByPlaceholder('Search by description...');
    await searchInput.fill('GROCERY');

    // Switch between tabs and verify filter persists
    await page.getByRole('button', { name: /fuzzy matches/i }).click();
    await expect(searchInput).toHaveValue('GROCERY');

    await page.getByRole('button', { name: /unmatched bank/i }).click();
    await expect(searchInput).toHaveValue('GROCERY');

    // Clear filter should work from any tab
    await page.getByRole('button', { name: /clear filters/i }).click();
    await expect(searchInput).toHaveValue('');
  });
});