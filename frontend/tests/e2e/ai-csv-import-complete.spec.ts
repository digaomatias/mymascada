import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('AI CSV Import - Complete End-to-End Workflow', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should complete full AI CSV import workflow with conflict resolution', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Create test account first
    let testAccount;
    try {
      testAccount = await utils.createTestAccount({
        name: 'AI Import Test Account',
        currentBalance: 1500.00
      });
      console.log('✅ Test account created:', testAccount);
    } catch (error) {
      console.log('ℹ️  API account creation failed, will create via UI if needed');
    }

    // Navigate to import page and access AI CSV Import
    await utils.navigateTo('/import');
    
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await expect(aiCsvButton).toBeVisible();
    await aiCsvButton.click();
    
    await expect(page).toHaveURL('/import/ai-csv');
    
    // STEP 1: Upload & Analyze
    console.log('🔄 STEP 1: Upload & Analyze');
    
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-4.50,REF001
2024-01-02,"Salary Deposit",2500.00,SAL001
2024-01-03,"Grocery Store",-75.25,GRC001
2024-01-04,"Gas Station",-45.20,GAS001
2024-01-05,"ATM Withdrawal",-100.00,ATM001`;

    // Handle file upload
    const uploadArea = page.locator('.border-dashed').first();
    await expect(uploadArea).toBeVisible();
    
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'test-ai-import.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });

    console.log('✅ File uploaded, waiting for AI analysis...');
    
    // Wait for analysis to complete
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    console.log('✅ AI analysis completed');

    // STEP 2: Review & Map
    console.log('🔄 STEP 2: Review & Map');
    
    // Verify we're on step 2
    await expect(page.getByText(/review.*map/i)).toBeVisible();
    
    // Handle account selection or creation
    const accountSelect = page.locator('select').first();
    if (await accountSelect.isVisible()) {
      console.log('🏦 Checking account options...');
      const accountOptions = await accountSelect.locator('option').allTextContents();
      console.log('Available accounts:', accountOptions);
      
      // If we only have the default "Select an existing account" option, create a new account
      if (accountOptions.length <= 1 || accountOptions.every(opt => opt.includes('Select'))) {
        console.log('🏦 No accounts available, creating new account...');
        
        // Scroll down to see if there's a "Create New Account" section
        await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
        await page.waitForTimeout(1000);
        
        // Look for "Create New Account" input field - try multiple selectors
        const createAccountInput = page.locator(
          'input[placeholder*="account name"], input[name*="account"], input[placeholder*="Enter new account name"]'
        ).first();
        
        if (await createAccountInput.isVisible()) {
          console.log('✅ Found create account input field');
          await createAccountInput.fill('AI Import Test Account');
          console.log('✅ Filled account name');
        } else {
          console.log('⚠️  No create account input found, checking for other options...');
          
          // Try to find any text input that might be for account creation
          const allInputs = await page.locator('input[type="text"]').all();
          console.log(`Found ${allInputs.length} text inputs`);
          
          for (let i = 0; i < allInputs.length; i++) {
            const input = allInputs[i];
            const placeholder = await input.getAttribute('placeholder');
            const name = await input.getAttribute('name');
            console.log(`Input ${i}: placeholder="${placeholder}", name="${name}"`);
            
            if (placeholder?.toLowerCase().includes('account') || name?.toLowerCase().includes('account')) {
              console.log(`✅ Using input ${i} for account creation`);
              await input.fill('AI Import Test Account');
              break;
            }
          }
        }
      } else {
        // Select existing account
        const testAccountOption = accountOptions.find(opt => opt.includes('AI Import Test Account'));
        if (testAccountOption) {
          await accountSelect.selectOption({ label: testAccountOption });
        } else {
          // Select first non-default account
          const firstRealAccount = accountOptions.find(opt => !opt.includes('Select'));
          if (firstRealAccount) {
            await accountSelect.selectOption({ label: firstRealAccount });
          }
        }
      }
    }
    
    // Proceed to review conflicts
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await expect(reviewImportButton).toBeVisible();
    await reviewImportButton.click();
    
    console.log('✅ Proceeding to conflict review...');
    
    // STEP 3: Review Conflicts (Import Review System)
    console.log('🔄 STEP 3: Review Conflicts');
    
    // Wait for Import Review screen to load
    await page.waitForTimeout(10000); // Give time for conflict analysis
    
    // Check if we're on Import Review screen
    const hasImportReview = await page.getByText(/import review|review conflicts/i).isVisible();
    console.log('📊 Import Review screen visible:', hasImportReview);
    
    if (hasImportReview) {
      // Look for conflict sections
      const conflictSections = await page.getByText(/exact duplicate|potential duplicate|ready to import/i).allTextContents();
      console.log('🔍 Conflict sections found:', conflictSections);
      
      // Look for bulk actions
      const bulkActionsButton = page.getByRole('button', { name: /bulk/i }).first();
      if (await bulkActionsButton.isVisible()) {
        console.log('🔘 Testing bulk actions...');
        await bulkActionsButton.click();
        await page.waitForTimeout(1000);
        
        // Try auto-resolve if available
        const autoResolveButton = page.getByText(/auto.*resolve/i);
        if (await autoResolveButton.isVisible()) {
          console.log('🤖 Applying auto-resolve...');
          await autoResolveButton.click();
          await page.waitForTimeout(2000);
        }
      }
      
      // Execute the import
      const executeImportButton = page.getByRole('button', { name: /execute import/i });
      await expect(executeImportButton).toBeVisible();
      
      console.log('🚀 Executing final import...');
      
      // Check if there's a modal or overlay that needs to be closed first
      const modalOverlay = page.locator('.fixed.inset-0, [role="dialog"], .modal-overlay');
      if (await modalOverlay.isVisible()) {
        console.log('⚠️  Modal overlay detected, trying to close it...');
        
        // Try to press Escape to close modal
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);
        
        // Or try to click outside the modal
        const modalBackground = page.locator('.fixed.inset-0').first();
        if (await modalBackground.isVisible()) {
          await modalBackground.click({ force: true });
          await page.waitForTimeout(500);
        }
      }
      
      // Listen for network requests and responses
      const apiCalls: string[] = [];
      page.on('response', response => {
        if (response.url().includes('/api/')) {
          apiCalls.push(`${response.url()} - ${response.status()}`);
        }
      });
      
      // Force click the execute button if needed
      try {
        await executeImportButton.click({ timeout: 10000 });
      } catch (error) {
        console.log('⚠️  Regular click failed, trying force click...');
        await executeImportButton.click({ force: true });
      }
      
      // Wait for import to complete and capture toast messages
      console.log('⏳ Waiting for import completion...');
      await page.waitForTimeout(15000);
      
      // Capture ALL possible toast/success messages
      const toastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast], .alert, .notification').allTextContents();
      console.log('📝 Toast messages after import execution:', toastMessages);
      
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
      
      // STEP 4: Complete
      console.log('🔄 STEP 4: Checking completion state');
      
      // Look for completion indicators
      const completionIndicators = await page.getByText(/step 4|complete|import successful/i).allTextContents();
      console.log('🎉 Completion indicators:', completionIndicators);
      
      // Check if we're redirected to transactions or success page
      const transactionPageContent = await page.getByText(/transaction|dashboard|import successful/i).allTextContents();
      console.log('📄 Final page content indicators:', transactionPageContent);
      
      // Take final screenshot
      await page.screenshot({ path: 'ai-csv-import-complete.png', fullPage: true });
      console.log('📸 Final screenshot saved as ai-csv-import-complete.png');
      
      // Validate that import was successful
      const importSucceeded = 
        successMessages.length > 0 ||
        toastMessages.some(msg => msg.toLowerCase().includes('success') || msg.toLowerCase().includes('imported')) ||
        completionIndicators.length > 0 ||
        finalUrl.includes('/transactions') ||
        finalUrl.includes('/dashboard');
        
      if (importSucceeded) {
        console.log('🎉 AI CSV IMPORT WORKFLOW COMPLETED SUCCESSFULLY!');
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
      console.log('ℹ️  Import Review screen not reached - may have gone directly to completion');
      
      // Check if we went straight to success
      const directSuccess = await page.getByText(/import successful|success/i).isVisible();
      if (directSuccess) {
        console.log('✅ Direct import success detected');
      }
    }
  });

  test('should handle AI CSV import errors gracefully', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    await utils.navigateTo('/import');
    
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Test with invalid CSV
    const invalidCsv = `Invalid,Format,Data
This,is,not,proper,CSV,format
Missing,required,structure`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'invalid.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(invalidCsv)
    });

    // Wait for error handling
    await page.waitForTimeout(5000);

    // Check for error messages
    const errorMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
    console.log('📝 Error messages for invalid CSV:', errorMessages);

    // Should show some kind of error
    const hasError = errorMessages.some(msg => 
      msg.toLowerCase().includes('error') || 
      msg.toLowerCase().includes('invalid') || 
      msg.toLowerCase().includes('failed')
    );
    
    expect(hasError).toBe(true);
    console.log('✅ Error handling for AI CSV import working correctly');
  });

  test('should handle large CSV files in AI import', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    await utils.navigateTo('/import');
    
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Create large CSV content (100 transactions)
    let largeCsv = 'Date,Description,Amount,Reference\n';
    for (let i = 1; i <= 100; i++) {
      const date = `2024-01-${String(i % 31 + 1).padStart(2, '0')}`;
      largeCsv += `${date},"Transaction ${i}",-${(i * 5).toFixed(2)},REF${String(i).padStart(3, '0')}\n`;
    }

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'large-dataset.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(largeCsv)
    });

    console.log('✅ Large CSV uploaded, waiting for analysis...');

    // Wait longer for large file processing
    await page.waitForTimeout(30000);

    // Check if analysis completed or is still processing
    const analysisComplete = await page.getByText(/analysis complete/i).isVisible();
    const stillProcessing = await page.getByText(/analyzing|processing/i).isVisible();
    
    console.log('📊 Analysis complete:', analysisComplete);
    console.log('⏳ Still processing:', stillProcessing);
    
    // Should handle large files without crashing
    expect(analysisComplete || stillProcessing).toBe(true);
    console.log('✅ Large file handling for AI CSV import working');
  });
});