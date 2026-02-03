import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Import Review - Quick Upload Test', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should access Import Review System and find file upload', async ({ page }) => {
    const user = await utils.registerAndLogin();
    
    // Navigate to import page
    await utils.navigateTo('/import');
    console.log('📍 Current URL after /import:', page.url());
    
    // Click AI CSV Import button
    const aiCsvButton = page.getByRole('button', { name: /ai csv import/i });
    if (await aiCsvButton.isVisible()) {
      console.log('🤖 AI CSV Import button found, clicking...');
      await aiCsvButton.click();
      await page.waitForTimeout(3000);
    } else {
      console.log('🤖 AI CSV Import button not found, navigating directly...');
      await utils.navigateTo('/import/ai-csv');
      await page.waitForTimeout(3000);
    }
    
    const finalUrl = page.url();
    console.log('📍 Final URL:', finalUrl);
    
    // Take screenshot for debugging
    await page.screenshot({ path: 'import-review-page.png', fullPage: true });
    console.log('📸 Screenshot saved as import-review-page.png');
    
    // Check if we're on the right page
    const hasImportReviewTitle = await page.getByText(/ai-powered csv import/i).isVisible();
    console.log('🎯 AI-Powered CSV Import title visible:', hasImportReviewTitle);
    
    // Check for file upload area
    const fileAreas = await page.locator('input[type="file"], [role="button"]:has-text("upload"), [role="button"]:has-text("choose"), .upload').allTextContents();
    console.log('📁 File upload areas found:', fileAreas);
    
    // Check all clickable elements in the upload area
    const clickableElements = await page.locator('button, [role="button"], .cursor-pointer, [onclick], input[type="file"]').allTextContents();
    console.log('🖱️  Clickable elements found:', clickableElements);
    
    // Look for drag and drop areas
    const dragDropAreas = await page.locator('[data-testid*="upload"], [data-testid*="drop"], .drop-zone, .upload-zone').count();
    console.log('📤 Drag-drop areas found:', dragDropAreas);
    
    // Try to find any file-related elements
    const fileRelated = await page.getByText(/upload|choose file|select file|drag.*drop|browse/i).allTextContents();
    console.log('📋 File-related text found:', fileRelated);
    
    // Check if there's a file input anywhere on the page
    const fileInputs = await page.locator('input[type="file"]').count();
    console.log('📄 File inputs found:', fileInputs);
    
    // Click the upload area (entire drag-and-drop area) to trigger file input
    console.log('🎯 Looking for upload area...');
    const uploadArea = page.locator('.border-dashed, [role="button"]:has-text("Upload your CSV file")').first();
    
    if (await uploadArea.isVisible()) {
      console.log('✅ Upload area found! Clicking...');
      await uploadArea.click();
      
      // Wait a moment for the dynamic file input to be created
      await page.waitForTimeout(2000);
      
      // Check if file input appeared
      const newFileInputs = await page.locator('input[type="file"]').count();
      console.log('📄 File inputs after clicking upload area:', newFileInputs);
      
      // Since the component creates dynamic file input, let's try a different approach
      // We'll use the file chooser event that Playwright provides
      console.log('🎯 Setting up file chooser handler...');
      
      const csvContent = `Date,Description,Amount,Reference
2024-01-01,"Coffee Shop Purchase",-4.50,REF001
2024-01-02,"Salary Deposit",2500.00,SAL001
2024-01-03,"Grocery Store",-75.25,GRC001`;
      
      try {
        // Handle the file chooser dialog that will be triggered
        const fileChooserPromise = page.waitForEvent('filechooser');
        
        // Click the upload area again to trigger the file chooser
        await uploadArea.click();
        
        // Wait for the file chooser to appear
        const fileChooser = await fileChooserPromise;
        console.log('✅ File chooser dialog captured!');
        
        // Create a temporary file and upload it
        await fileChooser.setFiles({
          name: 'test-import.csv',
          mimeType: 'text/csv',
          buffer: Buffer.from(csvContent)
        });
        
        console.log('✅ File uploaded via file chooser!');
        
        // Wait for processing and check for next steps
        await page.waitForTimeout(15000);
        
        // Check for Import Review indicators
        const stepTwoVisible = await page.getByText(/review.*map|step 2/i).isVisible();
        console.log('📊 Step 2 (Review & Map) visible:', stepTwoVisible);
        
        const stepThreeVisible = await page.getByText(/review conflicts|step 3/i).isVisible();
        console.log('📊 Step 3 (Review Conflicts) visible:', stepThreeVisible);
        
        // Look for Import Review System elements
        const conflictElements = await page.getByText(/exact duplicate|potential duplicate|ready to import/i).allTextContents();
        console.log('🔍 Conflict detection elements:', conflictElements);
        
        // Look for bulk actions
        const bulkActionsVisible = await page.getByText(/bulk/i).isVisible();
        console.log('🔘 Bulk actions visible:', bulkActionsVisible);
        
        // Look for execute import button
        const executeImportVisible = await page.getByText(/execute import/i).isVisible();
        console.log('▶️  Execute import button visible:', executeImportVisible);
        
        // Capture final state
        await page.screenshot({ path: 'after-upload.png', fullPage: true });
        console.log('📸 Post-upload screenshot saved as after-upload.png');
        
        // Check for any toast messages
        const toastMessages = await page.locator('[role="alert"], .toast, [data-sonner-toast]').allTextContents();
        console.log('📝 Toast messages:', toastMessages);
        
        // Check current URL to see what step we're on
        const currentUrl = page.url();
        console.log('📍 Current URL after upload:', currentUrl);
        
      } catch (error) {
        console.log('❌ Upload failed:', error.message);
      }
    } else {
      console.log('❌ Choose File button not found');
    }
    
    console.log('🏁 Quick test completed');
  });
});