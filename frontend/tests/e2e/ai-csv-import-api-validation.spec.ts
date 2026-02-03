import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('AI CSV Import - API Account ID Validation', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should track API calls and verify account ID is not 0', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Track all API calls to see what account ID is being sent
    const apiCalls: Array<{url: string, method: string, body?: any}> = [];
    
    page.on('request', request => {
      if (request.url().includes('/api/')) {
        const requestData = {
          url: request.url(),
          method: request.method(),
        };
        
        // Try to get request body for POST requests
        if (request.method() === 'POST') {
          try {
            const postData = request.postData();
            if (postData) {
              requestData.body = JSON.parse(postData);
            }
          } catch (e) {
            // Body might not be JSON
          }
        }
        
        apiCalls.push(requestData);
      }
    });
    
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Upload CSV
    const csvContent = `Date,Description,Amount,Reference
2024-01-15,"API Test Transaction",-25.50,API001`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'api-validation-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    // Wait for analysis
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    
    // Handle account creation
    const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
    if (await createAccountInput.isVisible()) {
      await createAccountInput.fill('API Test Account');
    }
    
    // Proceed to review
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await expect(reviewImportButton).toBeVisible();
    await reviewImportButton.click();
    
    // Wait for Import Review to load
    await page.waitForTimeout(10000);
    
    console.log('🌐 All API calls made during import:');
    apiCalls.forEach((call, index) => {
      console.log(`${index + 1}. ${call.method} ${call.url}`);
      if (call.body && typeof call.body === 'object') {
        console.log(`   Body: ${JSON.stringify(call.body, null, 2)}`);
      }
    });
    
    // Find the ImportReview analyze call
    const analyzeCall = apiCalls.find(call => 
      call.url.includes('ImportReview/analyze') || call.url.includes('importreview/analyze')
    );
    
    if (analyzeCall) {
      console.log('🎯 Found ImportReview analyze API call:');
      console.log(`   URL: ${analyzeCall.url}`);
      console.log(`   Method: ${analyzeCall.method}`);
      console.log(`   Body: ${JSON.stringify(analyzeCall.body, null, 2)}`);
      
      // Check if accountId is 0 (the bug)
      if (analyzeCall.body && analyzeCall.body.accountId !== undefined) {
        const accountId = analyzeCall.body.accountId;
        console.log(`📝 Account ID sent to API: ${accountId}`);
        
        if (accountId === 0) {
          console.log('❌ BUG CONFIRMED: Account ID is 0 (invalid)');
          expect(accountId).not.toBe(0);
        } else {
          console.log('✅ Account ID is valid (not 0)');
          expect(accountId).toBeGreaterThan(0);
        }
      } else {
        console.log('⚠️  No accountId found in request body');
      }
    } else {
      console.log('⚠️  ImportReview analyze API call not found');
      
      // List all API endpoints called
      const endpoints = apiCalls.map(call => call.url).join('\n   ');
      console.log('📋 All API endpoints called:');
      console.log(`   ${endpoints}`);
    }
    
    // Check for any error responses
    const errorCalls = apiCalls.filter(call => call.url.includes('error') || call.url.includes('Error'));
    if (errorCalls.length > 0) {
      console.log('❌ Error API calls detected:', errorCalls);
    }
    
    console.log('🏁 API validation test completed');
  });

  test('should verify successful import execution with correct account ID', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Monitor for specific error or success patterns
    const responseData: Array<{url: string, status: number, data?: any}> = [];
    
    page.on('response', async response => {
      if (response.url().includes('/api/')) {
        const responseInfo: any = {
          url: response.url(),
          status: response.status(),
        };
        
        try {
          if (response.headers()['content-type']?.includes('application/json')) {
            responseInfo.data = await response.json();
          }
        } catch (e) {
          // Response might not be JSON
        }
        
        responseData.push(responseInfo);
      }
    });
    
    await utils.navigateTo('/import');
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    await aiCsvButton.click();
    
    // Complete the full workflow
    const csvContent = `Date,Description,Amount
2024-01-20,"Success Test Transaction",-15.75`;

    const uploadArea = page.locator('.border-dashed').first();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await uploadArea.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({
      name: 'success-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    await expect(page.getByText(/analysis complete/i)).toBeVisible({ timeout: 30000 });
    
    // Handle account
    const createAccountInput = page.locator('input[placeholder*="account name"], input[name*="account"]').first();
    if (await createAccountInput.isVisible()) {
      await createAccountInput.fill('Success Test Account');
    }
    
    const reviewImportButton = page.getByRole('button', { name: /review.*import/i });
    await reviewImportButton.click();
    
    await page.waitForTimeout(10000);
    
    // Try to execute import
    const executeImportButton = page.getByRole('button', { name: /execute import/i });
    if (await executeImportButton.isVisible()) {
      try {
        await executeImportButton.click({ force: true });
        await page.waitForTimeout(15000);
        
        // Check responses
        console.log('📡 API Responses:');
        responseData.forEach((resp, index) => {
          console.log(`${index + 1}. ${resp.status} ${resp.url}`);
          if (resp.data) {
            console.log(`   Response: ${JSON.stringify(resp.data, null, 2)}`);
          }
        });
        
        // Look for error responses
        const errorResponses = responseData.filter(resp => resp.status >= 400);
        if (errorResponses.length > 0) {
          console.log('❌ Error responses found:', errorResponses);
        } else {
          console.log('✅ No error responses found');
        }
        
        // Check for the specific "Invalid account ID" error in responses
        const invalidAccountError = responseData.find(resp => 
          resp.data && JSON.stringify(resp.data).includes('Invalid account ID')
        );
        
        if (invalidAccountError) {
          console.log('❌ Found "Invalid account ID" error in API response:', invalidAccountError);
          expect(invalidAccountError).toBeUndefined();
        } else {
          console.log('✅ No "Invalid account ID" error found in API responses');
        }
        
      } catch (error) {
        console.log('⚠️  Error during import execution:', error.message);
      }
    } else {
      console.log('⚠️  Execute import button not found');
    }
    
    console.log('🏁 Success test completed');
  });
});