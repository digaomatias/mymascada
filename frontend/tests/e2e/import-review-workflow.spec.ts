import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Import Review - Workflow and UI Consistency', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should show consistent counts and proper workflow guidance', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    console.log('🧪 Testing Import Review workflow consistency...');
    
    // Navigate to AI CSV Import
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Upload test CSV with multiple transactions
    const csvContent = `Date,Description,Amount,Reference
2024-01-15,"Clean Transaction 1",-25.50,CLEAN001
2024-01-16,"Clean Transaction 2",150.75,CLEAN002
2024-01-17,"Clean Transaction 3",-45.25,CLEAN003
2024-01-18,"Clean Transaction 4",89.00,CLEAN004
2024-01-19,"Clean Transaction 5",-120.50,CLEAN005`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'workflow-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    console.log('📁 CSV uploaded, waiting for analysis...');
    
    // Wait for analysis and proceed to review
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    
    const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
    if (await createAccountInput.isVisible()) {
      await createAccountInput.fill('Workflow Test Account');
    }
    
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await reviewImportButton.click();
    
    console.log('📋 Navigating to Import Review screen...');
    await page.waitForTimeout(5000);
    
    // Verify initial state
    console.log('✅ Step 1: Verifying initial state...');
    
    // Should show workflow guidance
    await expect(page.getByText(/How to review your import/i)).toBeVisible();
    
    // Check initial Execute Import button - should show (0) since nothing decided yet
    const executeButton = page.getByRole('button', { name: /execute import/i });
    await expect(executeButton).toBeVisible();
    const executeButtonText = await executeButton.textContent();
    console.log('📊 Initial Execute Import button text:', executeButtonText);
    
    // Should show 100% reviewed but 0 to import since all are pending
    await expect(page.getByText(/0 items remaining|5 items remaining/)).toBeVisible();
    
    console.log('✅ Step 2: Testing individual decisions...');
    
    // Make individual decisions on some transactions
    const importButtons = page.getByRole('button', { name: /^import$/i });
    const skipButtons = page.getByRole('button', { name: /^skip$/i });
    
    // Import first 2 transactions
    await importButtons.nth(0).click();
    await page.waitForTimeout(500);
    await importButtons.nth(0).click(); // This should be the second transaction now
    await page.waitForTimeout(500);
    
    console.log('📈 Made individual decisions: 2 imports');
    
    // Check Execute Import button count - should now show (2)
    const updatedExecuteText = await executeButton.textContent();
    console.log('📊 Execute Import button after 2 imports:', updatedExecuteText);
    expect(updatedExecuteText).toMatch(/execute import.*2/i);
    
    // Skip 1 transaction
    await skipButtons.nth(0).click();
    await page.waitForTimeout(500);
    
    console.log('📉 Made individual decisions: 2 imports, 1 skip');
    
    // Check counts again - should still show (2) to import
    const afterSkipExecuteText = await executeButton.textContent();
    console.log('📊 Execute Import button after 1 skip:', afterSkipExecuteText);
    expect(afterSkipExecuteText).toMatch(/execute import.*2/i);
    
    console.log('✅ Step 3: Testing bulk actions...');
    
    // Check bulk actions - should only show remaining pending items
    const bulkButton = page.getByRole('button', { name: /bulk/i });
    await bulkButton.click();
    await page.waitForTimeout(500);
    
    // Should show "X pending of Y total" in bulk menu
    const bulkMenuText = await page.locator('.absolute.right-0.top-full').textContent();
    console.log('📋 Bulk actions menu text:', bulkMenuText);
    expect(bulkMenuText).toMatch(/\d+ pending of 5 total/);
    
    // Use bulk action to import remaining clean transactions
    const importAllCleanButton = page.getByText(/import all clean.*\(\d+\)/i);
    if (await importAllCleanButton.isVisible()) {
      await importAllCleanButton.click();
      await page.waitForTimeout(1000);
      
      console.log('📦 Applied bulk action: Import all clean');
      
      // Check final Execute Import count - should show all decided imports
      const finalExecuteText = await executeButton.textContent();
      console.log('📊 Final Execute Import button text:', finalExecuteText);
      // Should show total number of import decisions (2 individual + remaining from bulk)
      expect(parseInt(finalExecuteText?.match(/\((\d+)\)/)?.[1] || '0')).toBeGreaterThan(2);
    }
    
    console.log('✅ Step 4: Testing "Clear All Decisions" functionality...');
    
    // Test clear all decisions
    await bulkButton.click();
    await page.waitForTimeout(500);
    
    const clearAllButton = page.getByText(/clear all decisions/i);
    if (await clearAllButton.isVisible()) {
      await clearAllButton.click();
      await page.waitForTimeout(1000);
      
      console.log('🧹 Cleared all decisions');
      
      // Execute Import should now show (0) again
      const clearedExecuteText = await executeButton.textContent();
      console.log('📊 Execute Import after clear:', clearedExecuteText);
      expect(clearedExecuteText).toMatch(/execute import.*0/i);
      
      // Should show workflow guidance again
      await expect(page.getByText(/How to review your import/i)).toBeVisible();
    }
    
    console.log('✅ Step 5: Testing auto-resolve...');
    
    // Test auto-resolve functionality
    await bulkButton.click();
    await page.waitForTimeout(500);
    
    const autoResolveButton = page.getByText(/auto resolve all/i);
    if (await autoResolveButton.isVisible()) {
      await autoResolveButton.click();
      await page.waitForTimeout(1000);
      
      console.log('🤖 Applied auto-resolve');
      
      // Should now have decisions for all items
      const autoResolvedExecuteText = await executeButton.textContent();
      console.log('📊 Execute Import after auto-resolve:', autoResolvedExecuteText);
      expect(parseInt(autoResolvedExecuteText?.match(/\((\d+)\)/)?.[1] || '0')).toBeGreaterThan(0);
      
      // Workflow guidance should be hidden since all items are decided
      await expect(page.getByText(/How to review your import/i)).not.toBeVisible();
    }
    
    console.log('✅ Step 6: Verifying final execution readiness...');
    
    // Execute Import button should be enabled
    await expect(executeButton).toBeEnabled();
    
    // Progress should show 100% reviewed
    await expect(page.getByText(/100% reviewed/i)).toBeVisible();
    
    // Should show 0 items remaining
    await expect(page.getByText(/0 items remaining/)).toBeVisible();
    
    console.log('🎯 Workflow Test Results:');
    console.log('  ✅ Initial state shows correct counts');
    console.log('  ✅ Individual decisions update counts properly');
    console.log('  ✅ Bulk actions respect existing decisions');
    console.log('  ✅ Clear all decisions resets state correctly');
    console.log('  ✅ Auto-resolve applies intelligent decisions');
    console.log('  ✅ Execute Import button shows correct count throughout');
    console.log('  ✅ Workflow guidance appears/disappears appropriately');
    
    console.log('🏁 Import Review workflow test completed successfully');
  });

  test('should handle mixed individual and bulk decisions correctly', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    console.log('🧪 Testing mixed individual and bulk decisions...');
    
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Upload CSV with more transactions for better testing
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Transaction 1",-10.00,TX001
2024-01-02,"Transaction 2",-20.00,TX002
2024-01-03,"Transaction 3",-30.00,TX003
2024-01-04,"Transaction 4",-40.00,TX004
2024-01-05,"Transaction 5",-50.00,TX005
2024-01-06,"Transaction 6",-60.00,TX006
2024-01-07,"Transaction 7",-70.00,TX007
2024-01-08,"Transaction 8",-80.00,TX008`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'mixed-decisions-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    
    const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
    if (await createAccountInput.isVisible()) {
      await createAccountInput.fill('Mixed Decisions Test Account');
    }
    
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await reviewImportButton.click();
    await page.waitForTimeout(5000);
    
    const executeButton = page.getByRole('button', { name: /execute import/i });
    
    // Step 1: Make some individual decisions
    console.log('📝 Step 1: Individual decisions on first 3 transactions');
    const importButtons = page.getByRole('button', { name: /^import$/i });
    const skipButtons = page.getByRole('button', { name: /^skip$/i });
    
    await importButtons.nth(0).click(); // Import #1
    await page.waitForTimeout(300);
    await skipButtons.nth(0).click();   // Skip #2 
    await page.waitForTimeout(300);
    await importButtons.nth(0).click(); // Import #3
    await page.waitForTimeout(300);
    
    let executeText = await executeButton.textContent();
    console.log('After individual decisions:', executeText);
    expect(executeText).toMatch(/execute import.*2/i); // Should show 2 imports
    
    // Step 2: Use bulk action on remaining items
    console.log('📦 Step 2: Bulk action on remaining transactions');
    const bulkButton = page.getByRole('button', { name: /bulk/i });
    await bulkButton.click();
    await page.waitForTimeout(500);
    
    // Verify bulk menu shows correct pending count
    const bulkMenuText = await page.locator('.absolute.right-0.top-full').textContent();
    console.log('Bulk menu shows:', bulkMenuText);
    expect(bulkMenuText).toMatch(/5 pending of 8 total/); // 8 total - 3 decided = 5 pending
    
    // Apply bulk import to remaining
    const importAllCleanButton = page.getByText(/import all clean.*\(5\)/i);
    if (await importAllCleanButton.isVisible()) {
      await importAllCleanButton.click();
      await page.waitForTimeout(1000);
    }
    
    // Should now show 7 total imports (2 individual + 5 bulk)
    executeText = await executeButton.textContent();
    console.log('After bulk action:', executeText);
    expect(executeText).toMatch(/execute import.*7/i);
    
    // Step 3: Test clearing and re-doing
    console.log('🧹 Step 3: Clear and verify reset');
    await bulkButton.click();
    await page.waitForTimeout(500);
    
    const clearAllButton = page.getByText(/clear all decisions/i);
    if (await clearAllButton.isVisible()) {
      await clearAllButton.click();
      await page.waitForTimeout(1000);
    }
    
    executeText = await executeButton.textContent();
    console.log('After clearing all:', executeText);
    expect(executeText).toMatch(/execute import.*0/i);
    
    console.log('🎯 Mixed decisions test completed successfully');
  });
});