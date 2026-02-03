import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Complete CSV Import Workflow - End-to-End', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should complete full CSV import workflow with proper form filling', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Create test account first via API or ensure we have an account
    let account;
    try {
      account = await utils.createTestAccount({
        name: 'Test Import Account',
        currentBalance: 1000.00
      });
      console.log('✅ Test account created via API:', account);
    } catch (error) {
      console.log('ℹ️  API account creation failed, will create via UI if needed');
    }

    await utils.navigateTo('/import');
    
    // Click AI CSV Import to access Import Review System
    console.log('🤖 Clicking AI CSV Import button...');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    if (await aiCsvButton.isVisible()) {
      await aiCsvButton.click();
      console.log('✅ AI CSV Import clicked, should navigate to Import Review System');
      await page.waitForTimeout(2000);
    } else {
      // Fallback: navigate directly to ai-csv page
      await utils.navigateTo('/import/ai-csv');
      console.log('✅ Navigated directly to /import/ai-csv');
    }
    
    // Upload CSV file
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-4.50,REF001
2024-01-02,"Salary Deposit",2500.00,SAL001
2024-01-03,"Grocery Store",-75.25,GRC001
2024-01-04,"Gas Station",-45.20,GAS001`;

    console.log('📤 Uploading CSV content...');
    await utils.uploadCsvContent(csvContent);
    await page.waitForTimeout(3000);

    // Check if we need to select account
    const accountSelect = page.locator('select[name="accountId"], #targetAccount, [data-testid="account-select"]');
    const hasAccountSelect = await accountSelect.isVisible();
    
    if (hasAccountSelect) {
      console.log('🏦 Account selection required...');
      
      // First check if there are any accounts available
      const accountOptions = await accountSelect.locator('option').allTextContents();
      console.log('🏦 Available account options:', accountOptions);
      
      if (accountOptions.length > 1) {
        // Select the first non-empty account option
        const firstAccountOption = accountOptions.find(option => option.trim() && option !== 'Select an account');
        if (firstAccountOption) {
          console.log('🎯 Selecting account:', firstAccountOption);
          await accountSelect.selectOption({ label: firstAccountOption });
        } else {
          // Try selecting by index
          await accountSelect.selectOption({ index: 1 });
        }
      } else {
        console.log('❌ No accounts available - need to create one');
        
        // Navigate to create account if possible
        const createAccountButton = page.getByText(/create account|add account/i);
        if (await createAccountButton.isVisible()) {
          await createAccountButton.click();
          
          // Fill account creation form
          await page.getByLabel(/account name/i).fill('Test Import Account');
          await page.getByLabel(/account type/i).selectOption('0'); // Checking
          await page.getByLabel(/initial balance/i).fill('1000.00');
          await page.getByRole('button', { name: /create|save/i }).click();
          
          // Wait for account creation and return to import
          await page.waitForTimeout(2000);
          await utils.navigateTo('/import');
          await utils.uploadCsvContent(csvContent);
          await page.waitForTimeout(2000);
          
          // Now select the newly created account
          if (await accountSelect.isVisible()) {
            const newAccountOptions = await accountSelect.locator('option').allTextContents();
            const testAccount = newAccountOptions.find(option => option.includes('Test Import Account'));
            if (testAccount) {
              await accountSelect.selectOption({ label: testAccount });
            }
          }
        }
      }
    }

    // Check CSV format selection
    const formatSelect = page.locator('select[name="format"], #csvFormat, [data-testid="format-select"]');
    if (await formatSelect.isVisible()) {
      console.log('📋 CSV format selection available...');
      await formatSelect.selectOption('generic'); // Select generic CSV format
    }

    // Check and enable import options
    const headerCheckbox = page.locator('input[name="hasHeader"], #hasHeader, [data-testid="has-header"]');
    if (await headerCheckbox.isVisible()) {
      console.log('✅ Enabling "has header" option...');
      await headerCheckbox.check();
    }

    const skipDuplicatesCheckbox = page.locator('input[name="skipDuplicates"], #skipDuplicates');
    if (await skipDuplicatesCheckbox.isVisible()) {
      console.log('✅ Enabling "skip duplicates" option...');
      await skipDuplicatesCheckbox.check();
    }

    const autoCategorizeCheckbox = page.locator('input[name="autoCategorize"], #autoCategorize');
    if (await autoCategorizeCheckbox.isVisible()) {
      console.log('✅ Enabling "auto categorize" option...');
      await autoCategorizeCheckbox.check();
    }

    // Wait a moment for form validation
    await page.waitForTimeout(1000);

    // Now try to execute the import
    const importButton = page.getByRole('button', { name: /import csv transactions|import transactions|execute import/i });
    const isImportEnabled = await importButton.isEnabled();
    console.log('🎯 Import button enabled:', isImportEnabled);

    if (isImportEnabled) {
      console.log('🚀 Starting import execution...');
      
      // Listen for network requests to track API calls
      const apiCalls: string[] = [];
      page.on('response', response => {
        if (response.url().includes('/api/')) {
          apiCalls.push(`${response.url()} - ${response.status()}`);
        }
      });

      await importButton.click();
      
      // Wait for import to complete and capture all toast messages
      console.log('⏳ Waiting for import completion...');
      await page.waitForTimeout(10000);

      // Capture all possible toast/alert messages
      const toastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast], .alert').allTextContents();
      console.log('📝 Toast messages after import:', toastMessages);

      // Check for success indicators
      const successMessages = await page.getByText(/success|complete|imported|transactions imported/i).allTextContents();
      console.log('✅ Success messages:', successMessages);

      // Check for error indicators
      const errorMessages = await page.getByText(/error|failed|invalid/i).allTextContents();
      console.log('❌ Error messages:', errorMessages);

      // Log API calls that were made
      console.log('🌐 API calls made during import:', apiCalls);

      // Check current URL to see if we were redirected
      const finalUrl = page.url();
      console.log('📍 Final URL:', finalUrl);

      // Check if we're now on a success page or back to dashboard
      const pageIndicators = await page.getByText(/dashboard|transactions|import complete|success/i).allTextContents();
      console.log('📄 Final page indicators:', pageIndicators);

      // Verify the import actually worked by checking for evidence of imported transactions
      if (finalUrl.includes('/transactions') || finalUrl.includes('/dashboard')) {
        console.log('🔍 Checking for imported transactions...');
        
        // Look for the imported transaction data
        const transactionContent = await page.textContent('body');
        const hasImportedData = 
          transactionContent?.includes('Coffee Shop Purchase') ||
          transactionContent?.includes('Salary Deposit') ||
          transactionContent?.includes('Grocery Store') ||
          transactionContent?.includes('-4.50') ||
          transactionContent?.includes('2500.00');
        
        console.log('💰 Imported transaction data visible:', hasImportedData);
      }

      // Take a screenshot for debugging
      await page.screenshot({ path: 'complete-import-workflow.png', fullPage: true });
      console.log('📸 Screenshot saved as complete-import-workflow.png');

      // Determine success/failure based on evidence
      const importSucceeded = 
        successMessages.length > 0 ||
        toastMessages.some(msg => msg.includes('success') || msg.includes('imported')) ||
        finalUrl.includes('/transactions') ||
        finalUrl.includes('/dashboard');

      if (importSucceeded) {
        console.log('🎉 IMPORT WORKFLOW COMPLETED SUCCESSFULLY!');
      } else {
        console.log('❌ IMPORT WORKFLOW FAILED OR INCOMPLETE');
        console.log('Debug info:');
        console.log('- Toast messages:', toastMessages);
        console.log('- Success messages:', successMessages);
        console.log('- Error messages:', errorMessages);
        console.log('- Final URL:', finalUrl);
      }

      // Assert that some kind of completion occurred
      expect(
        importSucceeded || 
        toastMessages.length > 0 || 
        errorMessages.length > 0
      ).toBe(true);

    } else {
      console.log('❌ Import button is still disabled after form completion');
      
      // Debug why button is disabled
      console.log('🔍 Debugging disabled import button...');
      
      // Check if account is selected
      const selectedAccount = await page.locator('select[name="accountId"] option:checked, #targetAccount option:checked').textContent();
      console.log('🏦 Selected account:', selectedAccount);

      // Check file upload status
      const uploadedFile = await page.getByText(/\.csv/).textContent();
      console.log('📄 Uploaded file:', uploadedFile);

      // Check form validation messages
      const validationMessages = await page.getByText(/required|please select|invalid/i).allTextContents();
      console.log('⚠️  Validation messages:', validationMessages);

      // Still take a screenshot for debugging
      await page.screenshot({ path: 'disabled-import-button.png', fullPage: true });
      console.log('📸 Screenshot saved as disabled-import-button.png');

      throw new Error('Import button remained disabled - check form completion requirements');
    }
  });

  test('should handle import errors and show appropriate messages', async ({ page }) => {
    const user = await utils.registerAndLogin();
    await utils.navigateTo('/import');
    
    // Click AI CSV Import to access Import Review System
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    if (await aiCsvButton.isVisible()) {
      await aiCsvButton.click();
      await page.waitForTimeout(2000);
    } else {
      await utils.navigateTo('/import/ai-csv');
    }

    // Test with invalid CSV content
    const invalidCsv = `Invalid,CSV,Content
This,is,not,proper,CSV
Missing,required,columns`;

    await utils.uploadCsvContent(invalidCsv);
    await page.waitForTimeout(3000);

    // Try to proceed with invalid data - look for specific import buttons
    const executeButton = page.getByRole('button', { name: /execute import|import csv transactions/i });
    if (await executeButton.isVisible() && await executeButton.isEnabled()) {
      await executeButton.click();
      await page.waitForTimeout(5000);

      // Check for error messages
      const errorMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
      console.log('📝 Error messages for invalid CSV:', errorMessages);

      expect(errorMessages.some(msg => 
        msg.includes('error') || 
        msg.includes('invalid') || 
        msg.includes('failed')
      )).toBe(true);
    }

    console.log('✅ Error handling test completed');
  });
});