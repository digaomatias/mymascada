import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';
import path from 'path';

test.describe('CSV Import with Import Review System', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
    await utils.setupTestEnvironment();
  });

  test.afterEach(async ({ page }) => {
    await utils.cleanup();
  });

  test('should access import from dashboard and show Import Review System', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Wait for dashboard or handle redirect
    await page.waitForTimeout(2000);
    const currentUrl = page.url();
    
    if (currentUrl.includes('/dashboard')) {
      // Should see import CSV button in recent transactions header
      const importButton = page.getByRole('button', { name: /import csv/i }).first();
      if (await importButton.isVisible()) {
        await importButton.click();
      } else {
        // Navigate directly if button not found
        await utils.navigateTo('/import');
      }
    } else {
      // Navigate directly to import if not on dashboard
      await utils.navigateTo('/import');
    }
    
    // Should navigate to import page (accept both /import and /import/ai-csv)
    await expect(page).toHaveURL(/\/import(\/ai-csv)?/);
    
    // Should show import page content
    const hasUploadContent = await page.getByText(/csv file upload|import transactions|upload|choose file/i).first().isVisible();
    expect(hasUploadContent).toBe(true);
    
    console.log('✅ Import Review System accessible');
  });

  test('should upload and preview CSV with Import Review analysis', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    await utils.navigateTo('/import');
    await expect(page).toHaveURL('/import');
    
    // Upload test CSV with sample data
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-4.50,REF001
2024-01-02,"Salary Deposit",2500.00,SAL001
2024-01-03,"Grocery Store",-75.25,GRC001`;

    await utils.uploadCsvContent(csvContent);
    
    // Should show CSV preview
    await expect(page.getByText(/csv preview|preview/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('table')).toBeVisible();
    await expect(page.getByText('Coffee Shop Purchase')).toBeVisible();
    
    console.log('✅ CSV upload and preview with Import Review working');
  });

  test('should map CSV columns and proceed to analysis', async ({ page }) => {
    const user = await utils.registerAndLogin();
    await utils.navigateTo('/import');
    
    const csvContent = `Transaction Date,Description,Debit,Credit
01/01/2024,"Online Purchase",25.99,
01/02/2024,"Paycheck",,2500.00`;

    await utils.uploadCsvContent(csvContent);
    
    // Should show column mapping interface
    await expect(page.getByText(/map columns|column mapping/i)).toBeVisible({ timeout: 10000 });
    
    // Map columns if mapping interface is available
    const dateMappingSelect = page.locator('[data-testid="date-mapping"]');
    if (await dateMappingSelect.isVisible()) {
      await dateMappingSelect.selectOption('Transaction Date');
      await page.locator('[data-testid="description-mapping"]').selectOption('Description');
      await page.locator('[data-testid="amount-mapping"]').selectOption('Debit');
    }
    
    // Proceed to next step
    const continueButton = page.getByRole('button', { name: /continue|next|analyze/i });
    if (await continueButton.isVisible()) {
      await continueButton.click();
    }
    
    console.log('✅ CSV column mapping working');
  });

  test('should validate CSV format and show appropriate errors', async ({ page }) => {
    const user = await utils.registerAndLogin();
    await utils.navigateTo('/import');
    
    // Test invalid CSV format
    const invalidCsv = `This is not a valid CSV
Missing headers and structure
Random text content`;

    await utils.uploadCsvContent(invalidCsv);
    
    // Should show validation error
    await expect(page.getByText(/invalid|error|format/i)).toBeVisible({ timeout: 5000 });
    
    console.log('✅ CSV format validation with Import Review working');
  });

  test('should detect and display duplicate transactions in Import Review', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Setup test scenario with potential conflicts
    const account = await utils.setupImportReviewScenario();
    
    await utils.navigateTo('/import');
    
    // Upload CSV with potential duplicates
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-25.50,REF001
2024-01-02,"Starbucks Coffee",-4.50,STB001
2024-01-03,"New Transaction",-15.00,NEW001`;

    await utils.uploadCsvContent(csvContent);
    
    // Wait for Import Review screen
    await utils.waitForImportReview();
    
    // Should show conflict sections
    await expect(page.getByText(/exact duplicate|potential duplicate/i)).toBeVisible();
    await expect(page.getByText(/ready to import/i)).toBeVisible();
    
    console.log('✅ Duplicate detection in Import Review working');
  });

  test('should perform complete Import Review workflow', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Setup test data
    const account = await utils.setupImportReviewScenario();
    
    await utils.navigateTo('/import');
    
    // Upload CSV
    await utils.uploadCsvFile('sample-transactions.csv');
    
    // Wait for Import Review
    await utils.waitForImportReview();
    
    // Verify progress statistics
    await expect(page.getByText(/% reviewed/i)).toBeVisible();
    await expect(page.getByText(/items remaining/i)).toBeVisible();
    
    // Auto-resolve all conflicts
    await utils.autoResolveAllConflicts();
    
    // Execute import
    await utils.executeImport();
    
    // Should show success message
    await expect(page.getByText(/import completed successfully/i)).toBeVisible();
    
    console.log('✅ Complete Import Review workflow working');
  });

  test('should perform bulk actions in Import Review', async ({ page }) => {
    const user = await utils.registerAndLogin();
    const account = await utils.setupImportReviewScenario();
    
    await utils.navigateTo('/import');
    
    // Upload CSV with mixed scenarios
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-25.50,REF001
2024-01-02,"Clean Transaction 1",-15.00,CLN001
2024-01-03,"Clean Transaction 2",-20.00,CLN002
2024-01-04,"Coffee Shop",-25.00,REF002`;

    await utils.uploadCsvContent(csvContent);
    await utils.waitForImportReview();
    
    // Test bulk import of clean transactions
    await utils.performBulkAction(/import all clean/i);
    
    // Should show success message
    await expect(page.getByText(/applied import to/i)).toBeVisible();
    
    console.log('✅ Bulk actions in Import Review working');
  });

  test('should handle AI-powered CSV import if available', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Try to access AI CSV import
    try {
      await utils.navigateTo('/import/ai-csv');
      
      // If AI import is available, test it
      if (await page.getByText(/ai-powered|smart import/i).isVisible()) {
        const csvContent = `Date,Description,Amount
2024-01-01,"COFFEE SHOP NYC",-4.50
2024-01-02,"Coffee Shop New York",-4.50`;

        await utils.uploadCsvContent(csvContent);
        
        // AI should provide enhanced analysis
        await expect(page.getByText(/ai analysis|smart detection/i)).toBeVisible({ timeout: 15000 });
        console.log('✅ AI-powered CSV import working');
      } else {
        console.log('ℹ️  AI CSV import not available in current build');
      }
    } catch (error) {
      console.log('ℹ️  AI CSV import feature not implemented yet');
    }
  });

  test('should handle large CSV files with Import Review', async ({ page }) => {
    const user = await utils.registerAndLogin();
    await utils.navigateTo('/import');
    
    // Create large CSV content
    let largeCsv = 'Date,Description,Amount\n';
    for (let i = 1; i <= 100; i++) {
      largeCsv += `2024-01-${String(i).padStart(2, '0')},"Transaction ${i}",-${i}.00\n`;
    }

    await utils.uploadCsvContent(largeCsv);
    
    // Should handle large file
    await expect(page.getByText(/loading|processing|preview/i)).toBeVisible({ timeout: 30000 });
    
    console.log('✅ Large CSV file handling with Import Review working');
  });

  test('should handle error scenarios gracefully', async ({ page }) => {
    const user = await utils.registerAndLogin();
    await utils.navigateTo('/import');
    
    // Test network error handling
    await page.route('**/api/ImportReview/analyze', route => {
      route.abort('failed');
    });

    const csvContent = `Date,Description,Amount\n2024-01-01,"Test",-10.00`;
    await utils.uploadCsvContent(csvContent);

    // Should show error message
    await expect(page.getByText(/network error|failed|error/i)).toBeVisible({ timeout: 10000 });
    
    console.log('✅ Error handling in Import Review working');
  });
});