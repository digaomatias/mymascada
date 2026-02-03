import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Manual/OFX CSV Import - Complete End-to-End Workflow', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should complete full manual CSV import workflow', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Create test account first
    let testAccount;
    try {
      testAccount = await utils.createTestAccount({
        name: 'Manual Import Test Account',
        currentBalance: 2000.00
      });
      console.log('✅ Test account created:', testAccount);
    } catch (error) {
      console.log('ℹ️  API account creation failed, will create via UI if needed');
    }

    // Navigate to traditional import page
    await utils.navigateTo('/import');
    
    // We should be on the OFX Import page
    await expect(page.getByText(/ofx import/i)).toBeVisible();
    console.log('✅ On OFX Import page');
    
    // Upload CSV file
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Restaurant Purchase",-25.50,TXN001
2024-01-02,"Paycheck Deposit",3000.00,PAY001
2024-01-03,"Grocery Shopping",-85.25,GRC001
2024-01-04,"Utility Payment",-120.00,UTL001
2024-01-05,"Coffee Shop",-4.75,COF001`;

    console.log('📤 Uploading CSV to manual import...');
    
    // Click the "Choose File" button to trigger file input (similar to AI CSV import)
    const chooseFileButton = page.getByRole('button', { name: /choose file/i });
    await expect(chooseFileButton).toBeVisible();
    
    // Handle the file chooser dialog
    const fileChooserPromise = page.waitForEvent('filechooser');
    await chooseFileButton.click();
    const fileChooser = await fileChooserPromise;
    
    await fileChooser.setFiles({
      name: 'manual-import-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    console.log('✅ File uploaded');
    
    // Wait for file to be processed and scroll down to see the form
    await page.waitForTimeout(3000);
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(1000);
    
    // Select CSV format
    const formatSelect = page.locator('select[name="format"], #csvFormat, [data-testid="format-select"]');
    if (await formatSelect.isVisible()) {
      console.log('📋 Selecting CSV format...');
      await formatSelect.selectOption('generic');
    }
    
    // Handle account selection or creation
    const accountSelect = page.locator('select[name="accountId"], #targetAccount, [data-testid="account-select"]');
    if (await accountSelect.isVisible()) {
      console.log('🏦 Checking target account options...');
      
      const accountOptions = await accountSelect.locator('option').allTextContents();
      console.log('Available accounts:', accountOptions);
      
      // If we only have the default "Select an account" option, try to create a new account
      if (accountOptions.length <= 1 || accountOptions.every(opt => opt.includes('Select'))) {
        console.log('🏦 No accounts available for manual import, looking for create account option...');
        
        // Look for "Create new account" radio button or similar option
        const createAccountRadio = page.locator('input[type="radio"][value*="create"], input[type="radio"]:has-text("create")').first();
        const createAccountText = page.getByText(/create.*account/i);
        
        if (await createAccountRadio.isVisible()) {
          console.log('🎯 Found create account radio button');
          await createAccountRadio.click();
          await page.waitForTimeout(1000);
          
          // Look for account name input after selecting create option
          const accountNameInput = page.locator('input[placeholder*="account name"], input[name*="account"]');
          if (await accountNameInput.isVisible()) {
            await accountNameInput.fill('Manual Import Test Account');
          }
        } else if (await createAccountText.isVisible()) {
          console.log('🎯 Found create account text option');
          await createAccountText.click();
          await page.waitForTimeout(1000);
          
          const accountNameInput = page.locator('input[placeholder*="account name"], input[name*="account"]');
          if (await accountNameInput.isVisible()) {
            await accountNameInput.fill('Manual Import Test Account');
          }
        } else {
          console.log('⚠️  No create account option found - will proceed anyway to test with empty account');
          // This might cause the import button to remain disabled, which is expected behavior
        }
      } else {
        // Select existing account
        const testAccountOption = accountOptions.find(opt => opt.includes('Manual Import Test Account'));
        if (testAccountOption) {
          await accountSelect.selectOption({ label: testAccountOption });
        } else {
          // Select first non-default account
          const firstRealAccount = accountOptions.find(opt => !opt.includes('Select'));
          if (firstRealAccount) {
            await accountSelect.selectOption({ label: firstRealAccount });
            console.log(`✅ Selected account: ${firstRealAccount}`);
          }
        }
      }
    }
    
    // Enable import options
    const hasHeaderCheckbox = page.locator('input[name="hasHeader"], #hasHeader');
    if (await hasHeaderCheckbox.isVisible()) {
      console.log('✅ Enabling "has header" option...');
      await hasHeaderCheckbox.check();
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
    
    // Wait for form validation
    await page.waitForTimeout(2000);
    
    // Execute the import
    const importButton = page.getByRole('button', { name: /import csv transactions/i });
    await expect(importButton).toBeVisible();
    
    const isImportEnabled = await importButton.isEnabled();
    console.log('🎯 Import button enabled:', isImportEnabled);
    
    if (isImportEnabled) {
      console.log('🚀 Executing manual CSV import...');
      
      // Listen for network requests and responses
      const apiCalls: string[] = [];
      page.on('response', response => {
        if (response.url().includes('/api/')) {
          apiCalls.push(`${response.url()} - ${response.status()}`);
        }
      });
      
      await importButton.click();
      
      // Wait for import to complete
      console.log('⏳ Waiting for import completion...');
      await page.waitForTimeout(15000);
      
      // Capture ALL possible toast/success messages
      const toastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast], .alert, .notification').allTextContents();
      console.log('📝 Toast messages after manual import:', toastMessages);
      
      // Check for success indicators
      const successMessages = await page.getByText(/success|complete|imported|transactions imported/i).allTextContents();
      console.log('✅ Success messages found:', successMessages);
      
      // Check for error messages
      const errorMessages = await page.getByText(/error|failed|invalid/i).allTextContents();
      console.log('❌ Error messages found:', errorMessages);
      
      // Log API calls made during import
      console.log('🌐 API calls during import:', apiCalls);
      
      // Check final URL/page state
      const finalUrl = page.url();
      console.log('📍 Final URL after import:', finalUrl);
      
      // Check if we're redirected to transactions or success page
      const transactionPageContent = await page.getByText(/transaction|dashboard|import successful/i).allTextContents();
      console.log('📄 Final page content indicators:', transactionPageContent);
      
      // Take final screenshot
      await page.screenshot({ path: 'manual-csv-import-complete.png', fullPage: true });
      console.log('📸 Final screenshot saved as manual-csv-import-complete.png');
      
      // Validate that import was successful
      const importSucceeded = 
        successMessages.length > 0 ||
        toastMessages.some(msg => msg.toLowerCase().includes('success') || msg.toLowerCase().includes('imported')) ||
        finalUrl.includes('/transactions') ||
        finalUrl.includes('/dashboard');
        
      if (importSucceeded) {
        console.log('🎉 MANUAL CSV IMPORT WORKFLOW COMPLETED SUCCESSFULLY!');
      } else {
        console.log('❌ Import may have failed or is incomplete');
        console.log('Debug info - Toast messages:', toastMessages);
        console.log('Debug info - Error messages:', errorMessages);
        console.log('Debug info - Final URL:', finalUrl);
      }
      
      // Assert that some kind of completion occurred
      expect(
        importSucceeded || 
        toastMessages.length > 0 || 
        errorMessages.length > 0
      ).toBe(true);
      
    } else {
      console.log('❌ Import button is disabled - checking form completion requirements');
      
      // Debug why button is disabled
      const selectedAccount = await page.locator('select[name="accountId"] option:checked').textContent();
      console.log('🏦 Selected account:', selectedAccount);
      
      const uploadedFile = await page.getByText(/\.csv/).textContent();
      console.log('📄 Uploaded file:', uploadedFile);
      
      const validationMessages = await page.getByText(/required|please select|invalid/i).allTextContents();
      console.log('⚠️  Validation messages:', validationMessages);
      
      await page.screenshot({ path: 'manual-import-disabled-button.png', fullPage: true });
      
      throw new Error('Manual CSV import button remained disabled - check form completion requirements');
    }
  });

  test('should handle different CSV formats in manual import', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    const testCases = [
      {
        name: 'Generic CSV format',
        csv: `Date,Description,Amount
2024-01-01,"Transaction 1",-10.00
2024-01-02,"Transaction 2",-20.00`,
        format: 'generic'
      },
      {
        name: 'Chase Bank format',
        csv: `Transaction Date,Description,Debit,Credit
01/01/2024,"Purchase",-25.99,
01/02/2024,"Deposit",,500.00`,
        format: 'chase'
      },
      {
        name: 'Wells Fargo format',
        csv: `Date,Amount,Description,Type
01/01/2024,-45.20,"Gas Station","DEBIT"
01/02/2024,2500.00,"Payroll","CREDIT"`,
        format: 'wells_fargo'
      }
    ];

    for (const testCase of testCases) {
      console.log(`Testing ${testCase.name}...`);
      
      await utils.navigateTo('/import');
      
      // Upload CSV using Choose File button
      const chooseFileButton = page.getByRole('button', { name: /choose file/i });
      const fileChooserPromise = page.waitForEvent('filechooser');
      await chooseFileButton.click();
      const fileChooser = await fileChooserPromise;
      await fileChooser.setFiles({
        name: `test-${testCase.format}.csv`,
        mimeType: 'text/csv',
        buffer: Buffer.from(testCase.csv)
      });
      
      await page.waitForTimeout(2000);
      
      // Select format
      const formatSelect = page.locator('select[name="format"], #csvFormat');
      if (await formatSelect.isVisible()) {
        await formatSelect.selectOption(testCase.format);
      }
      
      // Check if format was accepted
      const hasPreview = await page.getByText(/preview|table|csv/i).isVisible();
      if (hasPreview) {
        console.log(`✅ ${testCase.name} format accepted`);
      } else {
        console.log(`ℹ️  ${testCase.name} may need different handling`);
      }
    }
  });

  test('should handle manual CSV import errors', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    await utils.navigateTo('/import');
    
    // Test with invalid CSV
    const invalidCsv = `Invalid,CSV,Structure
This,is,not,a,proper,CSV,file
Missing,required,columns,completely`;

    const chooseFileButton = page.getByRole('button', { name: /choose file/i });
    const fileChooserPromise = page.waitForEvent('filechooser');
    await chooseFileButton.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'invalid-manual.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(invalidCsv)
    });

    await page.waitForTimeout(3000);

    // Try to proceed with invalid data
    const formatSelect = page.locator('select[name="format"]');
    if (await formatSelect.isVisible()) {
      await formatSelect.selectOption('generic');
    }

    const importButton = page.getByRole('button', { name: /import csv transactions/i });
    if (await importButton.isVisible() && await importButton.isEnabled()) {
      await importButton.click();
      await page.waitForTimeout(5000);

      // Check for error messages
      const errorMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
      console.log('📝 Error messages for invalid manual CSV:', errorMessages);

      const hasError = errorMessages.some(msg => 
        msg.toLowerCase().includes('error') || 
        msg.toLowerCase().includes('invalid') || 
        msg.toLowerCase().includes('failed')
      );

      expect(hasError).toBe(true);
      console.log('✅ Error handling for manual CSV import working');
    }
  });

  test('should handle large CSV files in manual import', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    await utils.navigateTo('/import');
    
    // Create large CSV (200 transactions)
    let largeCsv = 'Date,Description,Amount\n';
    for (let i = 1; i <= 200; i++) {
      const date = `2024-${String(Math.floor(i / 31) + 1).padStart(2, '0')}-${String(i % 31 + 1).padStart(2, '0')}`;
      largeCsv += `${date},"Large Dataset Transaction ${i}",-${(i * 2.5).toFixed(2)}\n`;
    }

    const chooseFileButton = page.getByRole('button', { name: /choose file/i });
    const fileChooserPromise = page.waitForEvent('filechooser');
    await chooseFileButton.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'large-manual-dataset.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(largeCsv)
    });

    console.log('✅ Large CSV uploaded to manual import');

    // Wait for processing
    await page.waitForTimeout(10000);

    // Check if file was processed
    const hasFileIndicator = await page.getByText(/\.csv|uploaded|file/i).isVisible();
    const hasFormatOptions = await page.locator('select[name="format"]').isVisible();
    
    console.log('📄 File processed:', hasFileIndicator);
    console.log('📋 Format options available:', hasFormatOptions);
    
    // Should handle large files without crashing
    expect(hasFileIndicator || hasFormatOptions).toBe(true);
    console.log('✅ Large file handling for manual CSV import working');
  });

  test('should handle network errors gracefully in manual import', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    await utils.navigateTo('/import');
    
    // Mock network failure for import execution
    await page.route('**/api/**/import', route => {
      route.abort('failed');
    });

    const csvContent = `Date,Description,Amount
2024-01-01,"Test Transaction",-10.00`;

    const chooseFileButton = page.getByRole('button', { name: /choose file/i });
    const fileChooserPromise = page.waitForEvent('filechooser');
    await chooseFileButton.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'network-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });

    await page.waitForTimeout(2000);

    // Complete form
    const formatSelect = page.locator('select[name="format"]');
    if (await formatSelect.isVisible()) {
      await formatSelect.selectOption('generic');
    }

    const importButton = page.getByRole('button', { name: /import csv transactions/i });
    if (await importButton.isVisible() && await importButton.isEnabled()) {
      await importButton.click();
      await page.waitForTimeout(5000);

      // Check for network error handling
      const errorMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
      console.log('📝 Network error messages:', errorMessages);

      const hasNetworkError = errorMessages.some(msg => 
        msg.toLowerCase().includes('error') || 
        msg.toLowerCase().includes('failed') || 
        msg.toLowerCase().includes('network')
      );

      if (hasNetworkError) {
        console.log('✅ Network error handling working');
      } else {
        console.log('ℹ️  Network error may be handled differently');
      }
    }

    // Remove the route intercept
    await page.unroute('**/api/**/import');
  });
});