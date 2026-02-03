import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Reconciliation Transaction Comparison - Phase 2', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
    await utils.setupTestEnvironment();
    await utils.registerAndLogin();
  });

  test.afterEach(async () => {
    await utils.cleanup();
  });

  test('should display reconciliation details with tabbed interface', async ({ page }) => {
    // Create test account and transactions
    const account = await utils.createAccount({
      name: 'Test Checking Account',
      type: 'Checking',
      initialBalance: 1000
    });

    // Create some transactions for reconciliation
    await utils.createTransaction({
      accountId: account.id,
      amount: -100.50,
      description: 'Grocery Store Purchase',
      date: new Date().toISOString()
    });

    // Navigate to reconciliation page
    await page.goto(`/accounts/${account.id}/reconcile`);

    // Wait for page to load
    await expect(page.getByText('Bank Reconciliation')).toBeVisible();

    // Upload a test CSV for reconciliation
    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},Grocery Store Purchase,-100.50
${new Date().toISOString().split('T')[0]},MCDONALD'S #123,-25.99`;

    await utils.uploadCsvContent(csvContent);
    
    // Start reconciliation process
    await page.getByRole('button', { name: /match transactions/i }).click();
    
    // Wait for reconciliation results
    await expect(page.getByText('reconciliation review', { exact: false })).toBeVisible();

    // Check that tabs are present
    await expect(page.getByRole('button', { name: /exact matches/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /fuzzy matches/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /unmatched bank/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /unmatched system/i })).toBeVisible();
  });

  test('should show detailed transaction comparison for fuzzy matches', async ({ page }) => {
    // Create test account
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000
    });

    // Create a transaction that will be a fuzzy match
    await utils.createTransaction({
      accountId: account.id,
      amount: -25.99,
      description: 'McDonald\'s Restaurant',
      date: new Date().toISOString()
    });

    // Navigate to reconciliation page
    await page.goto(`/accounts/${account.id}/reconcile`);

    // Upload CSV with similar but not exact transaction
    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},MCDONALD'S #123,-25.99`;

    await utils.uploadCsvContent(csvContent);
    
    // Start reconciliation
    await page.getByRole('button', { name: /match transactions/i }).click();
    
    // Navigate to fuzzy matches tab
    await page.getByRole('button', { name: /fuzzy matches/i }).click();

    // Should show transaction comparison component
    await expect(page.getByText('Transaction Comparison')).toBeVisible();
    await expect(page.getByText('Bank Statement')).toBeVisible();
    await expect(page.getByText('MyMascada System')).toBeVisible();

    // Check for match confidence indicator
    await expect(page.locator('[class*="text-green-600"], [class*="text-yellow-600"], [class*="text-orange-600"]')).toBeVisible();

    // Should show both descriptions
    await expect(page.getByText('McDonald\'s Restaurant')).toBeVisible();
    await expect(page.getByText('MCDONALD\'S #123')).toBeVisible();
  });

  test('should allow expanding comparison details', async ({ page }) => {
    // Setup similar to previous test
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000
    });

    await utils.createTransaction({
      accountId: account.id,
      amount: -50.00,
      description: 'Amazon Purchase',
      date: new Date().toISOString()
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},AMAZON.COM PURCHASE,-50.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();
    await page.getByRole('button', { name: /fuzzy matches/i }).click();

    // Find and click expand button
    const expandButton = page.locator('button').filter({ has: page.locator('svg') }).first();
    await expandButton.click();

    // Should show detailed analysis when expanded
    await expect(page.getByText('Match Analysis')).toBeVisible();
    await expect(page.getByText('Amount Match:')).toBeVisible();
    await expect(page.getByText('Date Match:')).toBeVisible();
    await expect(page.getByText('Description:')).toBeVisible();
  });

  test('should display match confidence with proper styling', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000
    });

    // Create exact match
    await utils.createTransaction({
      accountId: account.id,
      amount: -100.00,
      description: 'Store Purchase',
      date: new Date().toISOString()
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},Store Purchase,-100.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Check exact matches tab
    await page.getByRole('button', { name: /exact matches/i }).click();
    
    // Should show high confidence
    await expect(page.locator('[class*="text-green-600"]')).toBeVisible();
  });

  test('should show search and filter functionality', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000
    });

    await utils.createTransaction({
      accountId: account.id,
      amount: -75.50,
      description: 'Grocery Store',
      date: new Date().toISOString()
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},Grocery Store,-75.50
${new Date().toISOString().split('T')[0]},Gas Station,-40.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Should show search input
    await expect(page.getByPlaceholder('Search by description...')).toBeVisible();
    
    // Should show amount filters
    await expect(page.getByPlaceholder('0.00')).toHaveCount(2); // Min and Max amount
    
    // Should show clear filters button
    await expect(page.getByRole('button', { name: /clear filters/i })).toBeVisible();
  });

  test('should handle search filtering correctly', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000
    });

    await utils.createTransaction({
      accountId: account.id,
      amount: -75.50,
      description: 'Grocery Store',
      date: new Date().toISOString()
    });

    await utils.createTransaction({
      accountId: account.id,
      amount: -40.00,
      description: 'Gas Station',
      date: new Date().toISOString()
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},Grocery Store,-75.50
${new Date().toISOString().split('T')[0]},Gas Station,-40.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Search for specific transaction
    await page.getByPlaceholder('Search by description...').fill('Grocery');
    
    // Should filter results to show only grocery transaction
    await expect(page.getByText('Grocery Store')).toBeVisible();
    // Gas Station should not be visible (filtered out)
    await expect(page.getByText('Gas Station')).not.toBeVisible();
  });

  test('should show summary statistics correctly', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000
    });

    await utils.createTransaction({
      accountId: account.id,
      amount: -100.00,
      description: 'Store Purchase',
      date: new Date().toISOString()
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},Store Purchase,-100.00
${new Date().toISOString().split('T')[0]},Unknown Transaction,-50.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Should show summary statistics
    await expect(page.getByText('Total Items')).toBeVisible();
    await expect(page.getByText('Exact Matches')).toBeVisible();
    await expect(page.getByText('Fuzzy Matches')).toBeVisible();
    await expect(page.getByText('Unmatched Bank')).toBeVisible();
    await expect(page.getByText('Unmatched System')).toBeVisible();
    
    // Should show match percentage
    await expect(page.getByText(/\d+\.?\d*% Match Rate/)).toBeVisible();
  });

  test('should show complete reconciliation button', async ({ page }) => {
    const account = await utils.createAccount({
      name: 'Test Account',
      type: 'Checking',
      initialBalance: 1000
    });

    await utils.createTransaction({
      accountId: account.id,
      amount: -100.00,
      description: 'Store Purchase',
      date: new Date().toISOString()
    });

    await page.goto(`/accounts/${account.id}/reconcile`);

    const csvContent = `Date,Description,Amount
${new Date().toISOString().split('T')[0]},Store Purchase,-100.00`;

    await utils.uploadCsvContent(csvContent);
    await page.getByRole('button', { name: /match transactions/i }).click();

    // Should show action buttons
    await expect(page.getByRole('button', { name: /back to matching/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /complete reconciliation/i })).toBeVisible();
  });
});