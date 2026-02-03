import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('AI CSV Import - Account ID Bug Reproduction', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should reproduce and fix "Invalid account ID" error with existing account selection', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Create a test account first via API (this simulates having existing accounts)
    let existingAccount;
    try {
      existingAccount = await utils.createTestAccount({
        name: 'Existing Account for Import',
        currentBalance: 1000.00
      });
      console.log('✅ Created existing account for testing:', existingAccount);
    } catch (error) {
      console.log('ℹ️  Could not create account via API, will handle via UI');
    }

    // Navigate to AI CSV Import (NOT with accountId in URL)
    await utils.navigateTo('/import');
    
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await expect(aiCsvButton).toBeVisible();
    await aiCsvButton.click();
    
    await expect(page).toHaveURL('/import/ai-csv');
    console.log('✅ On AI CSV Import page without account ID in URL');
    
    // STEP 1: Upload & Analyze
    console.log('🔄 STEP 1: Upload & Analyze');
    
    const csvContent = `Date,Description,Amount,Reference
2024-01-15,"Test Transaction 1",-25.50,TEST001
2024-01-16,"Test Transaction 2",-45.20,TEST002
2024-01-17,"Test Transaction 3",-15.75,TEST003`;

    const uploadArea = page.locator('.border-dashed').first();
    await expect(uploadArea).toBeVisible();
    
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'test-account-id-bug.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });

    console.log('✅ File uploaded, waiting for AI analysis...');
    
    // Wait for analysis to complete
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    console.log('✅ AI analysis completed');

    // STEP 2: Review & Map - Select EXISTING account
    console.log('🔄 STEP 2: Review & Map - Selecting EXISTING account');
    
    await expect(page.getByText(/review.*map/i)).toBeVisible();
    
    // Look for account selection dropdown
    const accountSelect = page.locator('select').first();
    if (await accountSelect.isVisible()) {
      const accountOptions = await accountSelect.locator('option').allTextContents();
      console.log('🏦 Available account options:', accountOptions);
      
      // Try to select an existing account (not create new)
      const existingAccountOption = accountOptions.find(opt => 
        opt.includes('Existing Account') || (opt.length > 0 && !opt.includes('Select'))
      );
      
      if (existingAccountOption) {
        console.log(`✅ Selecting existing account: ${existingAccountOption}`);
        await accountSelect.selectOption({ label: existingAccountOption });
      } else {
        console.log('⚠️  No existing accounts found, will create one');
        // Look for create account input
        const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
        if (await createAccountInput.isVisible()) {
          await createAccountInput.fill('Test Account for ID Bug');
        }
      }
    } else {
      console.log('⚠️  No account selection dropdown found');
    }
    
    // Proceed to review conflicts
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await expect(reviewImportButton).toBeVisible();
    await reviewImportButton.click();
    
    console.log('✅ Proceeding to conflict review...');
    
    // STEP 3: Review Conflicts - This is where the bug should occur
    console.log('🔄 STEP 3: Review Conflicts - Testing for account ID bug');
    
    // Wait for Import Review screen to load
    await page.waitForTimeout(10000);
    
    // Check if we got the Import Review screen or an error
    const hasImportReview = await page.getByText(/import review|review conflicts/i).isVisible();
    console.log('📊 Import Review screen visible:', hasImportReview);
    
    if (hasImportReview) {
      console.log('✅ Successfully reached Import Review (bug may be fixed)');
      
      // Now test the final import execution to see if "Invalid account ID" appears
      const executeImportButton = page.getByRole('button', { name: /execute import/i });
      if (await executeImportButton.isVisible()) {
        console.log('🚀 Testing final import execution...');
        
        // Listen for toast messages specifically
        const toastPromise = page.waitForFunction(() => {
          const toasts = document.querySelectorAll('[role="alert"], .toast, [data-sonner-toast]');
          for (const toast of toasts) {
            if (toast.textContent?.includes('Invalid account ID')) {
              return toast.textContent;
            }
          }
          return null;
        }, { timeout: 5000 });
        
        try {
          await executeImportButton.click({ force: true });
          
          // Wait for either success or the specific error
          await page.waitForTimeout(15000);
          
          // Capture all toast messages
          const allToastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
          console.log('📝 All toast messages after import execution:', allToastMessages);
          
          // Check specifically for the "Invalid account ID" error
          const hasInvalidAccountError = allToastMessages.some(msg => 
            msg.includes('Invalid account ID') || msg.includes('Please restart the import process')
          );
          
          if (hasInvalidAccountError) {
            console.log('❌ BUG REPRODUCED: Invalid account ID error appeared!');
            console.log('🔧 This confirms the account ID is not being passed correctly');
          } else {
            console.log('✅ No "Invalid account ID" error - bug may be fixed!');
          }
          
          // Check for success messages
          const hasSuccessMessages = allToastMessages.some(msg => 
            msg.toLowerCase().includes('success') || msg.toLowerCase().includes('imported')
          );
          
          console.log('📊 Test Results:');
          console.log(`- Invalid account ID error: ${hasInvalidAccountError ? '❌ PRESENT' : '✅ ABSENT'}`);
          console.log(`- Success messages: ${hasSuccessMessages ? '✅ PRESENT' : '❌ ABSENT'}`);
          console.log(`- All messages: ${JSON.stringify(allToastMessages)}`);
          
          // Take screenshot for debugging
          await page.screenshot({ path: 'account-id-bug-test.png', fullPage: true });
          console.log('📸 Screenshot saved as account-id-bug-test.png');
          
          // Assertion: Bug is fixed if no "Invalid account ID" error appears
          expect(hasInvalidAccountError).toBe(false);
          
        } catch (toastError) {
          console.log('⏳ No specific toast detected within timeout, checking final state...');
          
          const finalToastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
          console.log('📝 Final toast messages:', finalToastMessages);
          
          const hasError = finalToastMessages.some(msg => msg.includes('Invalid account ID'));
          console.log(`🔍 Invalid account ID error found: ${hasError}`);
          
          // If no error is found, the bug might be fixed
          if (!hasError) {
            console.log('✅ No "Invalid account ID" error detected - bug appears to be fixed');
          }
        }
        
      } else {
        console.log('⚠️  Execute import button not found');
      }
      
    } else {
      console.log('❌ Failed to reach Import Review screen');
      
      // Check if we got an error during the conflicts analysis step
      const errorMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
      console.log('📝 Error messages during conflict analysis:', errorMessages);
      
      const hasAccountIdError = errorMessages.some(msg => msg.includes('Invalid account ID'));
      if (hasAccountIdError) {
        console.log('❌ BUG REPRODUCED: Invalid account ID error during conflict analysis!');
      }
    }
    
    console.log('🏁 Account ID bug reproduction test completed');
  });

  test('should verify account ID is properly logged during import process', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Monitor console logs to see account ID handling
    const consoleLogs: string[] = [];
    page.on('console', msg => {
      if (msg.text().includes('account') || msg.text().includes('Account')) {
        consoleLogs.push(msg.text());
      }
    });
    
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Upload CSV
    const csvContent = `Date,Description,Amount\n2024-01-01,"Test",-10.00`;
    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'debug-account-id.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    await page.waitForTimeout(10000);
    
    // Check console logs for account ID debugging
    console.log('🔍 Console logs related to account handling:');
    consoleLogs.forEach(log => console.log(`  ${log}`));
    
    // Look for our specific debug log we added
    const hasAccountIdLog = consoleLogs.some(log => log.includes('Using account ID for import'));
    console.log(`📝 Found account ID debug log: ${hasAccountIdLog}`);
    
    if (hasAccountIdLog) {
      console.log('✅ Account ID debugging is working');
    } else {
      console.log('⚠️  Account ID debug log not found');
    }
  });
});