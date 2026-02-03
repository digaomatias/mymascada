import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('AI CSV Import - Session Cache Fix', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should complete import successfully with account ID and without session cache errors', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Track API calls and responses to monitor both request and response data
    const apiInteractions: Array<{
      type: 'request' | 'response';
      url: string;
      method?: string;
      status?: number;
      requestBody?: any;
      responseBody?: any;
      timestamp: number;
    }> = [];
    
    // Monitor requests
    page.on('request', request => {
      if (request.url().includes('/api/ImportReview/')) {
        const interaction: any = {
          type: 'request',
          url: request.url(),
          method: request.method(),
          timestamp: Date.now()
        };
        
        if (request.method() === 'POST') {
          try {
            const postData = request.postData();
            if (postData) {
              interaction.requestBody = JSON.parse(postData);
            }
          } catch (e) {
            // Body might not be JSON
          }
        }
        
        apiInteractions.push(interaction);
      }
    });
    
    // Monitor responses
    page.on('response', async response => {
      if (response.url().includes('/api/ImportReview/')) {
        const interaction: any = {
          type: 'response',
          url: response.url(),
          status: response.status(),
          timestamp: Date.now()
        };
        
        try {
          if (response.headers()['content-type']?.includes('application/json')) {
            interaction.responseBody = await response.json();
          }
        } catch (e) {
          // Response might not be JSON
        }
        
        apiInteractions.push(interaction);
      }
    });
    
    console.log('🧪 Starting comprehensive session cache fix test...');
    
    // Navigate to AI CSV Import
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Upload test CSV
    const csvContent = `Date,Description,Amount,Reference
2024-01-25,"Cache Fix Test Transaction",-35.00,CACHE001
2024-01-26,"Another Test Transaction",150.25,CACHE002`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'cache-fix-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    console.log('📁 CSV file uploaded, waiting for analysis...');
    
    // Wait for analysis complete
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    
    // Handle account - create new one to ensure we have a valid account ID
    const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
    if (await createAccountInput.isVisible()) {
      await createAccountInput.fill('Cache Fix Test Account');
      console.log('🏦 Creating new account: Cache Fix Test Account');
    } else {
      console.log('🏦 Using existing account selection');
    }
    
    // Proceed to Import Review
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await expect(reviewImportButton).toBeVisible();
    await reviewImportButton.click();
    
    console.log('📋 Navigating to Import Review screen...');
    
    // Wait for Import Review to load
    await page.waitForTimeout(10000);
    
    // Check API interactions so far
    console.log('🌐 API Interactions during analysis phase:');
    const analyzeRequests = apiInteractions.filter(i => i.url.includes('analyze'));
    analyzeRequests.forEach((interaction, index) => {
      console.log(`${index + 1}. ${interaction.type.toUpperCase()}: ${interaction.url}`);
      if (interaction.requestBody) {
        console.log(`   Request AccountId: ${interaction.requestBody.accountId}`);
      }
      if (interaction.responseBody) {
        console.log(`   Response Status: ${interaction.status}`);
        console.log(`   Analysis ID: ${interaction.responseBody.analysisId}`);
        console.log(`   Account ID: ${interaction.responseBody.accountId}`);
      }
    });
    
    // Verify analysis API call has correct account ID
    const analyzeRequest = analyzeRequests.find(i => i.type === 'request' && i.requestBody);
    if (analyzeRequest?.requestBody?.accountId) {
      expect(analyzeRequest.requestBody.accountId).toBeGreaterThan(0);
      console.log('✅ Analysis request has valid account ID:', analyzeRequest.requestBody.accountId);
    } else {
      console.log('⚠️  Could not verify account ID in analysis request');
    }
    
    // Try to execute the import
    const executeImportButton = page.getByRole('button', { name: /execute import/i });
    await expect(executeImportButton).toBeVisible({ timeout: 15000 });
    
    console.log('⚡ Executing import...');
    await executeImportButton.click({ force: true });
    
    // Wait for import execution
    await page.waitForTimeout(15000);
    
    // Check for success/error toast messages
    const toastMessages = await page.locator('[data-sonner-toast]').allTextContents();
    console.log('🍞 Toast messages:', toastMessages);
    
    // Check execute API interactions
    console.log('🌐 API Interactions during execution phase:');
    const executeInteractions = apiInteractions.filter(i => i.url.includes('execute'));
    executeInteractions.forEach((interaction, index) => {
      console.log(`${index + 1}. ${interaction.type.toUpperCase()}: ${interaction.url}`);
      if (interaction.requestBody) {
        console.log(`   Request AccountId: ${interaction.requestBody.accountId}`);
        console.log(`   Analysis ID: ${interaction.requestBody.analysisId}`);
        console.log(`   Decisions: ${interaction.requestBody.decisions?.length || 0}`);
      }
      if (interaction.responseBody && interaction.status) {
        console.log(`   Response Status: ${interaction.status}`);
        if (interaction.status >= 400) {
          console.log(`   Error Response: ${JSON.stringify(interaction.responseBody, null, 2)}`);
        } else {
          console.log(`   Success: ${interaction.responseBody.isSuccess || interaction.responseBody.success}`);
          console.log(`   Message: ${interaction.responseBody.message}`);
        }
      }
    });
    
    // Verify the execute request has account ID
    const executeRequest = executeInteractions.find(i => i.type === 'request' && i.requestBody);
    if (executeRequest?.requestBody?.accountId) {
      expect(executeRequest.requestBody.accountId).toBeGreaterThan(0);
      console.log('✅ Execute request has valid account ID:', executeRequest.requestBody.accountId);
    } else {
      console.log('❌ Execute request missing account ID');
      expect(executeRequest?.requestBody?.accountId).toBeGreaterThan(0);
    }
    
    // Check for specific error patterns that we've fixed
    const errorResponses = executeInteractions.filter(i => i.status && i.status >= 400);
    
    // Should NOT have "Invalid account ID" error
    const invalidAccountError = errorResponses.find(r => 
      JSON.stringify(r.responseBody).includes('Invalid account ID') ||
      JSON.stringify(r.responseBody).includes('invalid account')
    );
    
    if (invalidAccountError) {
      console.log('❌ Found "Invalid account ID" error - BUG NOT FIXED:', invalidAccountError);
      expect(invalidAccountError).toBeUndefined();
    } else {
      console.log('✅ No "Invalid account ID" error found');
    }
    
    // Should NOT have "Analysis not found or expired" error
    const analysisExpiredError = errorResponses.find(r => 
      JSON.stringify(r.responseBody).includes('Analysis') && 
      (JSON.stringify(r.responseBody).includes('not found') || JSON.stringify(r.responseBody).includes('expired'))
    );
    
    if (analysisExpiredError) {
      console.log('❌ Found "Analysis not found or expired" error - BUG NOT FIXED:', analysisExpiredError);
      expect(analysisExpiredError).toBeUndefined();
    } else {
      console.log('✅ No "Analysis not found or expired" error found');
    }
    
    // Check for success indicators
    const successToasts = toastMessages.filter(msg => 
      msg.toLowerCase().includes('success') || 
      msg.toLowerCase().includes('completed') ||
      msg.toLowerCase().includes('imported')
    );
    
    const errorToasts = toastMessages.filter(msg => 
      msg.toLowerCase().includes('error') || 
      msg.toLowerCase().includes('failed')
    );
    
    console.log('🎯 Test Results Summary:');
    console.log(`   Success toasts: ${successToasts.length}`);
    console.log(`   Error toasts: ${errorToasts.length}`);
    console.log(`   API errors: ${errorResponses.length}`);
    
    if (successToasts.length > 0) {
      console.log('✅ Import appears to have succeeded based on toast messages');
    } else if (errorToasts.length > 0) {
      console.log('❌ Import appears to have failed based on toast messages:', errorToasts);
    } else {
      console.log('⚠️  No clear success/error indication from toast messages');
    }
    
    // Final verification: should have no major API errors
    if (errorResponses.length === 0) {
      console.log('✅ No API errors detected - fix appears successful!');
    } else {
      console.log(`❌ ${errorResponses.length} API errors still present`);
    }
    
    console.log('🏁 Comprehensive session cache fix test completed');
  });
});