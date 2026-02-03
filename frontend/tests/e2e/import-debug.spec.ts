import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Import Review System - Debug Tests', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should debug complete import workflow and capture all messages', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Setup test account first
    let account;
    try {
      account = await utils.createTestAccount({
        name: 'Test Import Account Debug',
        currentBalance: 1000.00
      });
      console.log('✅ Test account created:', account);
    } catch (error) {
      console.log('ℹ️  Creating account via API failed, continuing...');
    }

    await utils.navigateTo('/import');
    
    // Upload CSV and log every step
    const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-4.50,REF001
2024-01-02,"Salary Deposit",2500.00,SAL001
2024-01-03,"Grocery Store",-75.25,GRC001`;

    console.log('📤 Uploading CSV content...');
    await utils.uploadCsvContent(csvContent);
    
    // Wait and log what happens
    console.log('⏳ Waiting for processing...');
    await page.waitForTimeout(5000);
    
    // Capture current page state
    const currentUrl = page.url();
    console.log('📍 Current URL:', currentUrl);
    
    // Check for all possible toast messages
    console.log('🔍 Checking for toast messages...');
    const toastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
    if (toastMessages.length > 0) {
      console.log('📝 Toast messages found:', toastMessages);
    } else {
      console.log('📝 No toast messages found');
    }
    
    // Check for error messages anywhere on page
    const errorTexts = await page.getByText(/error|failed|invalid/i).allTextContents();
    if (errorTexts.length > 0) {
      console.log('❌ Error messages found:', errorTexts);
    }
    
    // Check for success/progress messages
    const successTexts = await page.getByText(/success|complete|imported/i).allTextContents();
    if (successTexts.length > 0) {
      console.log('✅ Success messages found:', successTexts);
    }
    
    // Log all visible text content to understand current state
    const pageContent = await page.textContent('body');
    const contentLines = pageContent?.split('\n').slice(0, 20) || [];
    console.log('📄 Page content preview:');
    contentLines.forEach((line, index) => {
      if (line.trim()) {
        console.log(`  ${index + 1}: ${line.trim()}`);
      }
    });
    
    // Check if we're on Import Review screen
    const hasReviewScreen = await page.getByText(/review import|import review/i).isVisible();
    console.log('🔍 Import Review screen present:', hasReviewScreen);
    
    if (hasReviewScreen) {
      console.log('📊 Import Review screen detected - testing workflow...');
      
      // Check progress
      const progressTexts = await page.getByText(/% reviewed|items remaining/i).allTextContents();
      console.log('📈 Progress indicators:', progressTexts);
      
      // Check for sections
      const sections = await page.getByText(/exact duplicate|potential duplicate|ready to import/i).allTextContents();
      console.log('📂 Conflict sections found:', sections);
      
      // Look for bulk actions
      const bulkButton = page.getByRole('button', { name: /bulk/i }).first();
      const hasBulkActions = await bulkButton.isVisible();
      console.log('🔘 Bulk actions available:', hasBulkActions);
      
      if (hasBulkActions) {
        console.log('🎯 Testing bulk actions...');
        await bulkButton.click();
        await page.waitForTimeout(1000);
        
        const bulkOptions = await page.getByText(/import all|skip all|auto resolve/i).allTextContents();
        console.log('📋 Bulk options available:', bulkOptions);
        
        // Try auto resolve
        const autoResolve = page.getByText(/auto resolve/i);
        if (await autoResolve.isVisible()) {
          console.log('🤖 Attempting auto-resolve...');
          await autoResolve.click();
          await page.waitForTimeout(2000);
          
          // Check for toast messages after auto-resolve
          const newToastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
          console.log('📝 Toast messages after auto-resolve:', newToastMessages);
          
          // Check progress after auto-resolve
          const newProgressTexts = await page.getByText(/% reviewed|items remaining/i).allTextContents();
          console.log('📈 Progress after auto-resolve:', newProgressTexts);
        }
      }
      
      // Look for execute button
      const executeButton = page.getByRole('button', { name: /execute import/i });
      const hasExecuteButton = await executeButton.isVisible();
      console.log('▶️  Execute button available:', hasExecuteButton);
      
      if (hasExecuteButton) {
        const isExecuteEnabled = await executeButton.isEnabled();
        console.log('✅ Execute button enabled:', isExecuteEnabled);
        
        if (isExecuteEnabled) {
          console.log('🚀 Attempting import execution...');
          
          // Listen for all network requests
          page.on('response', response => {
            if (response.url().includes('/api/')) {
              console.log(`🌐 API Response: ${response.url()} - ${response.status()}`);
            }
          });
          
          await executeButton.click();
          
          // Wait longer for execution to complete
          console.log('⏳ Waiting for import execution...');
          await page.waitForTimeout(10000);
          
          // Check for all possible results
          const allToastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
          console.log('📝 All toast messages after execution:', allToastMessages);
          
          const allErrorMessages = await page.getByText(/error|failed|invalid/i).allTextContents();
          console.log('❌ All error messages after execution:', allErrorMessages);
          
          const allSuccessMessages = await page.getByText(/success|complete|imported/i).allTextContents();
          console.log('✅ All success messages after execution:', allSuccessMessages);
          
          // Check current URL after execution
          const finalUrl = page.url();
          console.log('📍 Final URL after execution:', finalUrl);
          
          // Take a screenshot for debugging
          await page.screenshot({ path: 'debug-import-execution.png', fullPage: true });
          console.log('📸 Screenshot saved as debug-import-execution.png');
        } else {
          console.log('❌ Execute button is disabled - checking why...');
          
          const pendingItems = await page.getByText(/pending|review|remaining/i).allTextContents();
          console.log('⏸️  Pending items messages:', pendingItems);
        }
      }
    } else {
      console.log('📋 Different workflow detected - checking what\'s available...');
      
      // Check for traditional import buttons
      const importButtons = await page.getByRole('button', { name: /import/i }).allTextContents();
      console.log('🔘 Import buttons found:', importButtons);
      
      // Check for any form or action buttons
      const allButtons = await page.getByRole('button').allTextContents();
      console.log('🔘 All buttons on page:', allButtons);
      
      // Try to find and click any available import action
      const activeImportButton = page.getByRole('button', { name: /import.*transactions|execute|process/i });
      if (await activeImportButton.isVisible()) {
        const isEnabled = await activeImportButton.isEnabled();
        console.log('🎯 Found import action button, enabled:', isEnabled);
        
        if (isEnabled) {
          console.log('🚀 Clicking import action button...');
          await activeImportButton.click();
          await page.waitForTimeout(5000);
          
          const finalToastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
          console.log('📝 Toast messages after traditional import:', finalToastMessages);
        }
      }
    }
    
    console.log('🏁 Debug test completed');
  });
});