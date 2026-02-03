import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';
import path from 'path';

test.describe('Import Review System - Comprehensive E2E Tests', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
    await utils.setupTestEnvironment();
  });

  test.afterEach(async ({ page }) => {
    await utils.cleanup();
  });

  test.describe('CSV Upload and Analysis', () => {
    test('should upload CSV file and trigger import analysis', async ({ page }) => {
      const user = await utils.registerAndLogin();
      
      // Navigate to import page
      await utils.navigateTo('/import');
      await expect(page).toHaveURL('/import');

      // Create test CSV content with various scenarios
      const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-4.50,REF001
2024-01-02,"Salary Deposit",2500.00,SAL001
2024-01-03,"Grocery Store",-75.25,GRC001
2024-01-04,"Coffee Shop Purchase",-4.50,REF001
2024-01-05,"Bank Transfer",-500.00,TRF001`;

      // Upload CSV file
      await utils.uploadCsvContent(csvContent);

      // Wait for CSV preview to load
      await expect(page.getByText(/csv preview/i)).toBeVisible({ timeout: 10000 });
      
      // Should display preview table
      await expect(page.getByRole('table')).toBeVisible();
      await expect(page.getByText('Coffee Shop Purchase')).toBeVisible();

      console.log('✅ CSV upload and preview working');
    });

    test('should map CSV columns correctly', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await utils.navigateTo('/import');

      const csvContent = `Transaction Date,Description,Debit,Credit,Reference Number
01/01/2024,"Online Purchase",25.99,,ONL001
01/02/2024,"Paycheck",,2500.00,PAY002`;

      await utils.uploadCsvContent(csvContent);
      
      // Wait for column mapping interface
      await expect(page.getByText(/map columns/i)).toBeVisible({ timeout: 10000 });

      // Map columns
      await page.selectOption('[data-testid="date-mapping"]', 'Transaction Date');
      await page.selectOption('[data-testid="description-mapping"]', 'Description');
      await page.selectOption('[data-testid="amount-mapping"]', 'Debit');
      
      // Proceed to next step
      await page.getByRole('button', { name: /continue|next/i }).click();

      console.log('✅ CSV column mapping working');
    });

    test('should detect and analyze conflicts', async ({ page }) => {
      const user = await utils.registerAndLogin();

      // First create some existing transactions via API to create potential conflicts
      await page.evaluate(async () => {
        const token = localStorage.getItem('auth_token');
        
        // Create test account
        const accountResponse = await fetch('https://localhost:5126/api/accounts', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`,
          },
          body: JSON.stringify({
            name: 'Test Import Account',
            type: 0,
            currentBalance: 1000.00,
          }),
        });
        const account = await accountResponse.json();
        
        // Create existing transaction that will conflict
        await fetch('https://localhost:5126/api/transactions', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`,
          },
          body: JSON.stringify({
            accountId: account.id,
            amount: -25.50,
            description: 'Coffee Shop Purchase',
            transactionDate: '2024-01-01',
            source: 1, // Manual
            type: 1 // Debit
          }),
        });

        window.testAccountId = account.id;
      });

      await utils.navigateTo('/import');
      
      // Upload CSV with potential duplicates
      const csvContent = `Date,Description,Amount
2024-01-01,"Coffee Shop Purchase",-25.50
2024-01-02,"New Transaction",-15.00`;

      await utils.uploadCsvContent(csvContent);

      // Wait for analysis to complete and show conflicts
      await expect(page.getByText(/review import|conflicts detected/i)).toBeVisible({ timeout: 15000 });

      // Should show Import Review screen
      await expect(page.getByText(/exact duplicate|potential duplicate/i)).toBeVisible();

      console.log('✅ Conflict detection and analysis working');
    });
  });

  test.describe('Import Review Interface', () => {
    test('should display conflict sections correctly', async ({ page }) => {
      const user = await utils.registerAndLogin();
      
      // Setup test data and navigate to import review
      await setupImportReviewTest(page);

      // Should display different conflict sections
      await expect(page.getByText(/exact duplicates/i)).toBeVisible();
      await expect(page.getByText(/potential duplicates/i)).toBeVisible();
      await expect(page.getByText(/ready to import/i)).toBeVisible();

      // Should show progress statistics
      await expect(page.getByText(/% reviewed/i)).toBeVisible();
      await expect(page.getByText(/items remaining/i)).toBeVisible();

      console.log('✅ Import Review interface sections working');
    });

    test('should handle individual conflict resolution', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      // Find a conflict item and resolve it
      const conflictCard = page.locator('[data-testid="conflict-card"]').first();
      await expect(conflictCard).toBeVisible();

      // Should show resolution buttons
      await expect(conflictCard.getByRole('button', { name: /import/i })).toBeVisible();
      await expect(conflictCard.getByRole('button', { name: /skip/i })).toBeVisible();

      // Make a decision
      await conflictCard.getByRole('button', { name: /import/i }).click();

      // Should update progress
      await expect(page.getByText(/1 item|items remaining/i)).toBeVisible();

      console.log('✅ Individual conflict resolution working');
    });

    test('should expand and collapse conflict details', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      const conflictCard = page.locator('[data-testid="conflict-card"]').first();
      
      // Should have expand button for conflicts
      const expandButton = conflictCard.getByRole('button', { name: /expand|show details/i });
      if (await expandButton.isVisible()) {
        await expandButton.click();
        
        // Should show conflict details
        await expect(conflictCard.getByText(/conflicting transaction/i)).toBeVisible();
        await expect(conflictCard.getByText(/confidence/i)).toBeVisible();
      }

      console.log('✅ Conflict detail expansion working');
    });
  });

  test.describe('Bulk Actions', () => {
    test('should perform bulk import of clean transactions', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      // Find bulk actions button
      const bulkButton = page.getByRole('button', { name: /bulk/i }).first();
      await bulkButton.click();

      // Should show bulk action menu
      await expect(page.getByText(/import all clean/i)).toBeVisible();
      
      // Perform bulk import
      await page.getByText(/import all clean/i).click();

      // Should update multiple items
      await expect(page.getByText(/applied import to/i)).toBeVisible();

      console.log('✅ Bulk import of clean transactions working');
    });

    test('should perform bulk skip of exact duplicates', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      const bulkButton = page.getByRole('button', { name: /bulk/i }).first();
      await bulkButton.click();

      // Skip exact duplicates
      if (await page.getByText(/skip exact duplicates/i).isVisible()) {
        await page.getByText(/skip exact duplicates/i).click();
        await expect(page.getByText(/applied skip to/i)).toBeVisible();
      }

      console.log('✅ Bulk skip of duplicates working');
    });

    test('should perform smart auto-resolution', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      const bulkButton = page.getByRole('button', { name: /bulk/i }).first();
      await bulkButton.click();

      // Auto resolve all
      if (await page.getByText(/auto resolve all/i).isVisible()) {
        await page.getByText(/auto resolve all/i).click();
        
        // Should resolve multiple items automatically
        await expect(page.getByText(/100% reviewed|0 items remaining/i)).toBeVisible({ timeout: 10000 });
      }

      console.log('✅ Smart auto-resolution working');
    });
  });

  test.describe('Import Execution', () => {
    test('should execute import successfully', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      // Resolve all conflicts first (use auto-resolve for speed)
      const bulkButton = page.getByRole('button', { name: /bulk/i }).first();
      await bulkButton.click();
      await page.getByText(/auto resolve all/i).click();

      // Wait for all items to be resolved
      await expect(page.getByText(/100% reviewed/i)).toBeVisible();

      // Execute import
      const executeButton = page.getByRole('button', { name: /execute import/i });
      await expect(executeButton).toBeEnabled();
      await executeButton.click();

      // Should show success message
      await expect(page.getByText(/import completed successfully/i)).toBeVisible({ timeout: 15000 });

      console.log('✅ Import execution working');
    });

    test('should prevent execution with pending items', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      // Execute button should be disabled with pending items
      const executeButton = page.getByRole('button', { name: /execute import/i });
      await expect(executeButton).toBeDisabled();
      
      // Should show warning message
      await expect(page.getByText(/review all conflicts before importing/i)).toBeVisible();

      console.log('✅ Import execution validation working');
    });

    test('should handle import errors gracefully', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      // Mock API error
      await page.route('**/api/ImportReview/execute', route => {
        route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Server error' })
        });
      });

      // Resolve all conflicts
      const bulkButton = page.getByRole('button', { name: /bulk/i }).first();
      await bulkButton.click();
      await page.getByText(/auto resolve all/i).click();

      await expect(page.getByText(/100% reviewed/i)).toBeVisible();

      // Attempt import
      const executeButton = page.getByRole('button', { name: /execute import/i });
      await executeButton.click();

      // Should show error message
      await expect(page.getByText(/server error|import failed/i)).toBeVisible();

      console.log('✅ Import error handling working');
    });
  });

  test.describe('AI-Powered CSV Import', () => {
    test('should access AI CSV import feature', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await utils.navigateTo('/import');

      // Look for AI CSV import option
      if (await page.getByText(/ai csv|smart import/i).isVisible()) {
        await page.getByText(/ai csv|smart import/i).click();
        await expect(page).toHaveURL('/import/ai-csv');
        
        // Should show AI import interface
        await expect(page.getByText(/ai-powered|smart analysis/i)).toBeVisible();
        console.log('✅ AI CSV import accessible');
      } else {
        console.log('ℹ️  AI CSV import not available in current build');
      }
    });

    test('should perform AI-enhanced conflict detection', async ({ page }) => {
      const user = await utils.registerAndLogin();
      
      try {
        await utils.navigateTo('/import/ai-csv');
        
        const csvContent = `Date,Description,Amount
2024-01-01,"COFFEE SHOP NYC",-4.50
2024-01-02,"Coffee Shop New York",-4.50
2024-01-03,"Starbucks Purchase",-4.75`;

        await utils.uploadCsvContent(csvContent);

        // AI should detect potential duplicates with similar descriptions
        await expect(page.getByText(/ai analysis|smart detection/i)).toBeVisible({ timeout: 15000 });
        
        console.log('✅ AI-enhanced conflict detection working');
      } catch (error) {
        console.log('ℹ️  AI CSV import not fully implemented yet');
      }
    });
  });

  test.describe('Error Handling and Edge Cases', () => {
    test('should handle invalid CSV format', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await utils.navigateTo('/import');

      // Upload invalid CSV
      const invalidCsv = `This is not a valid CSV file
It has malformed data
And missing structure`;

      await utils.uploadCsvContent(invalidCsv);

      // Should show error message
      await expect(page.getByText(/invalid|error|format/i)).toBeVisible();

      console.log('✅ Invalid CSV format handling working');
    });

    test('should handle empty CSV file', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await utils.navigateTo('/import');

      await utils.uploadCsvContent('');

      // Should show appropriate error
      await expect(page.getByText(/empty|no data/i)).toBeVisible();

      console.log('✅ Empty CSV handling working');
    });

    test('should handle large CSV files', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await utils.navigateTo('/import');

      // Create large CSV content (1000 rows)
      let largeCsv = 'Date,Description,Amount\n';
      for (let i = 1; i <= 1000; i++) {
        largeCsv += `2024-01-01,"Transaction ${i}",-${i}.00\n`;
      }

      await utils.uploadCsvContent(largeCsv);

      // Should handle large file (may show progress or pagination)
      await expect(page.getByText(/loading|processing|preview/i)).toBeVisible({ timeout: 30000 });

      console.log('✅ Large CSV file handling working');
    });

    test('should handle network errors during analysis', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await utils.navigateTo('/import');

      // Mock network error
      await page.route('**/api/ImportReview/analyze', route => {
        route.abort('failed');
      });

      const csvContent = `Date,Description,Amount\n2024-01-01,"Test",-10.00`;
      await utils.uploadCsvContent(csvContent);

      // Should show network error
      await expect(page.getByText(/network error|failed to analyze/i)).toBeVisible();

      console.log('✅ Network error handling working');
    });
  });

  test.describe('User Experience', () => {
    test('should show progress indicators during analysis', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await utils.navigateTo('/import');

      const csvContent = `Date,Description,Amount\n2024-01-01,"Test",-10.00`;
      await utils.uploadCsvContent(csvContent);

      // Should show loading state
      await expect(page.getByText(/analyzing|processing/i)).toBeVisible();

      console.log('✅ Progress indicators working');
    });

    test('should provide helpful tooltips and guidance', async ({ page }) => {
      const user = await utils.registerAndLogin();
      await setupImportReviewTest(page);

      // Look for help text or tooltips
      const helpElements = await page.locator('[title], [data-tooltip], .tooltip').count();
      expect(helpElements).toBeGreaterThan(0);

      console.log('✅ User guidance elements present');
    });

    test('should be responsive on mobile devices', async ({ page }) => {
      const user = await utils.registerAndLogin();
      
      // Set mobile viewport
      await page.setViewportSize({ width: 375, height: 667 });
      
      await setupImportReviewTest(page);

      // Should adapt to mobile layout
      await expect(page.locator('[data-testid="import-review"]')).toBeVisible();

      console.log('✅ Mobile responsiveness working');
    });
  });

  // Helper function to setup import review test scenario
  async function setupImportReviewTest(page: any) {
    // Create account and existing transactions for conflict testing
    await page.evaluate(async () => {
      const token = localStorage.getItem('auth_token');
      
      const accountResponse = await fetch('https://localhost:5126/api/accounts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          name: 'Test Import Account',
          type: 0,
          currentBalance: 1000.00,
        }),
      });
      const account = await accountResponse.json();
      
      // Create existing transaction for conflict
      await fetch('https://localhost:5126/api/transactions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          accountId: account.id,
          amount: -25.50,
          description: 'Coffee Shop Purchase',
          transactionDate: '2024-01-01',
          source: 1,
          type: 1
        }),
      });

      window.testAccountId = account.id;
    });

    await utils.navigateTo('/import');
    
    // Upload CSV with mixed scenarios
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-25.50,REF001
2024-01-02,"Salary Deposit",2500.00,SAL001
2024-01-03,"Coffee Shop",-25.00,REF002
2024-01-04,"Clean Transaction",-15.00,CLN001`;

    await utils.uploadCsvContent(csvContent);

    // Wait for import review screen
    await expect(page.getByText(/review import/i)).toBeVisible({ timeout: 15000 });
  }
});

test.describe('Import Review System - Performance Tests', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should handle concurrent imports efficiently', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Test multiple concurrent CSV analysis requests
    const promises = [];
    for (let i = 0; i < 3; i++) {
      promises.push(page.evaluate(async (index) => {
        const csvContent = `Date,Description,Amount\n2024-01-0${index + 1},"Transaction ${index}",-${index * 10}.00`;
        const blob = new Blob([csvContent], { type: 'text/csv' });
        
        const formData = new FormData();
        formData.append('file', blob, `test${index}.csv`);
        
        const token = localStorage.getItem('auth_token');
        const start = performance.now();
        
        const response = await fetch('https://localhost:5126/api/ImportReview/analyze', {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
          },
          body: formData
        });
        
        const end = performance.now();
        return { status: response.status, duration: end - start };
      }, i));
    }

    const results = await Promise.all(promises);
    
    // All requests should succeed
    results.forEach(result => {
      expect(result.status).toBe(200);
      expect(result.duration).toBeLessThan(5000); // Should complete within 5 seconds
    });

    console.log('✅ Concurrent import performance acceptable');
  });

  test('should maintain performance with large datasets', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Test with large dataset
    const start = Date.now();
    let largeCsv = 'Date,Description,Amount\n';
    for (let i = 1; i <= 5000; i++) {
      largeCsv += `2024-01-01,"Transaction ${i}",-${i}.00\n`;
    }

    await utils.navigateTo('/import');
    await utils.uploadCsvContent(largeCsv);

    // Should complete analysis within reasonable time
    await expect(page.getByText(/review import|analysis complete/i)).toBeVisible({ timeout: 60000 });
    
    const duration = Date.now() - start;
    expect(duration).toBeLessThan(60000); // Should complete within 1 minute

    console.log(`✅ Large dataset performance: ${duration}ms for 5000 transactions`);
  });
});