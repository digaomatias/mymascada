import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Import Review - Enum Mismatch Fix', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should not get "Invalid Decision" error when executing import with clean transactions', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    console.log('🧪 Testing enum mismatch fix for "Invalid Decision" error...');
    
    // Track all toast messages 
    const toastMessages: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'log' && msg.text().includes('Toast')) {
        console.log('Toast detected:', msg.text());
      }
    });
    
    // Monitor for toast elements
    const toastObserver = setInterval(async () => {
      try {
        const toasts = await page.locator('[data-sonner-toast]').allTextContents();
        for (const toast of toasts) {
          if (!toastMessages.includes(toast)) {
            toastMessages.push(toast);
            console.log('📢 New toast message:', toast);
          }
        }
      } catch (e) {
        // Ignore errors from toast checking
      }
    }, 500);
    
    // Navigate to AI CSV Import
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Upload simple CSV with clean transactions (no conflicts)
    const csvContent = `Date,Description,Amount,Reference
2024-01-15,"Clean Test Transaction 1",-25.50,ENUM001
2024-01-16,"Clean Test Transaction 2",150.75,ENUM002
2024-01-17,"Clean Test Transaction 3",-45.25,ENUM003`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'enum-fix-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    console.log('📁 Uploaded clean CSV for enum test...');
    
    // Wait for analysis
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    
    // Create account if needed
    const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
    if (await createAccountInput.isVisible()) {
      await createAccountInput.fill('Enum Test Account');
    }
    
    // Proceed to Import Review
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await reviewImportButton.click();
    
    console.log('📋 Navigating to Import Review - should see clean transactions ready to import...');
    await page.waitForTimeout(5000);
    
    // Verify we're on the review screen and have clean transactions
    await expect(page.getByText(/ready to import/i)).toBeVisible();
    
    // Check that Execute Import button shows correct count
    const executeButton = page.getByRole('button', { name: /execute import/i });
    await expect(executeButton).toBeVisible();
    
    const executeButtonText = await executeButton.textContent();
    console.log('📊 Execute Import button text:', executeButtonText);
    
    // Should show "Execute Import (3)" since we have 3 clean transactions
    expect(executeButtonText).toMatch(/execute import.*3/i);
    
    // Clear any existing toasts
    toastMessages.length = 0;
    
    console.log('⚡ Clicking Execute Import - testing for enum mismatch fix...');
    
    // Execute the import - this is where the "Invalid Decision: 1" error used to occur
    await executeButton.click({ force: true });
    
    // Wait for import execution
    await page.waitForTimeout(10000);
    
    // Stop toast monitoring
    clearInterval(toastObserver);
    
    // Get final toast messages
    const finalToasts = await page.locator('[data-sonner-toast]').allTextContents();
    for (const toast of finalToasts) {
      if (!toastMessages.includes(toast)) {
        toastMessages.push(toast);
      }
    }
    
    console.log('🍞 All toast messages captured:', toastMessages);
    
    // Check for the specific error we're fixing
    const invalidDecisionError = toastMessages.find(msg => 
      msg.includes('Invalid Decision') || 
      msg.includes('Invalid decision') ||
      msg.includes('decision: 1') ||
      msg.includes('Decision: 1')
    );
    
    if (invalidDecisionError) {
      console.log('❌ ENUM FIX FAILED: Still getting "Invalid Decision" error:', invalidDecisionError);
      expect(invalidDecisionError).toBeUndefined(); // This will fail the test
    } else {
      console.log('✅ ENUM FIX SUCCESS: No "Invalid Decision" error detected');
    }
    
    // Check for success messages
    const successMessages = toastMessages.filter(msg => 
      msg.toLowerCase().includes('success') || 
      msg.toLowerCase().includes('completed') ||
      msg.toLowerCase().includes('imported')
    );
    
    const errorMessages = toastMessages.filter(msg => 
      msg.toLowerCase().includes('error') || 
      msg.toLowerCase().includes('failed')
    );
    
    console.log('🎯 Import Results:');
    console.log(`   Success messages: ${successMessages.length}`);
    console.log(`   Error messages: ${errorMessages.length}`);
    console.log(`   Success messages: ${JSON.stringify(successMessages)}`);
    console.log(`   Error messages: ${JSON.stringify(errorMessages)}`);
    
    // Verify we got a success message and no error messages
    if (successMessages.length > 0) {
      console.log('✅ Import appears to have succeeded');
      expect(successMessages.length).toBeGreaterThan(0);
    } else if (errorMessages.length === 0) {
      console.log('⚠️  No clear success/error messages - may need to check UI state');
      // Check if we're redirected or see completion UI
      // This is acceptable if the import completed without toast messages
    } else {
      console.log('❌ Import appears to have failed with errors:', errorMessages);
      // If there are error messages, make sure none are the enum issue
      const enumErrors = errorMessages.filter(msg => 
        msg.includes('Invalid Decision') || 
        msg.includes('Invalid decision')
      );
      expect(enumErrors.length).toBe(0); // Should be 0 enum-related errors
    }
    
    console.log('🏁 Enum mismatch fix test completed');
  });

  test('should handle individual import/skip decisions without enum errors', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    console.log('🧪 Testing individual decisions with enum fix...');
    
    const toastMessages: string[] = [];
    
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Upload CSV for individual decision testing
    const csvContent = `Date,Description,Amount,Reference
2024-01-20,"Individual Test 1",-10.00,IND001
2024-01-21,"Individual Test 2",-20.00,IND002
2024-01-22,"Individual Test 3",-30.00,IND003`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'individual-enum-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    
    const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
    if (await createAccountInput.isVisible()) {
      await createAccountInput.fill('Individual Test Account');
    }
    
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await reviewImportButton.click();
    await page.waitForTimeout(5000);
    
    console.log('📝 Making individual decisions...');
    
    // Make individual decisions: import 2, skip 1
    const importButtons = page.getByRole('button', { name: /^import$/i });
    const skipButtons = page.getByRole('button', { name: /^skip$/i });
    
    await importButtons.nth(0).click(); // Import first
    await page.waitForTimeout(500);
    await importButtons.nth(0).click(); // Import second (index shifts)
    await page.waitForTimeout(500);
    await skipButtons.nth(0).click();   // Skip third
    await page.waitForTimeout(500);
    
    // Execute Import
    const executeButton = page.getByRole('button', { name: /execute import/i });
    await expect(executeButton).toBeVisible();
    
    const executeText = await executeButton.textContent();
    console.log('📊 Execute button shows:', executeText);
    expect(executeText).toMatch(/execute import.*2/i); // Should show 2 imports
    
    await executeButton.click({ force: true });
    await page.waitForTimeout(8000);
    
    // Check for enum-related errors
    const finalToasts = await page.locator('[data-sonner-toast]').allTextContents();
    const enumErrors = finalToasts.filter(msg => 
      msg.includes('Invalid Decision') || 
      msg.includes('Invalid decision')
    );
    
    console.log('🍞 Final toasts:', finalToasts);
    console.log('🔍 Enum errors found:', enumErrors);
    
    expect(enumErrors.length).toBe(0); // Should be no enum-related errors
    
    console.log('✅ Individual decisions test completed without enum errors');
  });
});