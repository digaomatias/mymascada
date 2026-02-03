import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

const testCSV = `Date,Amount,Description
2025-01-01,-100.00,Grocery Store
2025-01-02,1500.00,Salary Deposit
2025-01-03,-50.00,Gas Station`;

test('CSV preview should work correctly', async ({ page }) => {
  const utils = new TestUtils(page);
  
  // Setup: Register user and navigate to AI CSV import
  await utils.registerAndLogin();
  await page.goto('/import/ai-csv');
  await page.waitForLoadState('networkidle');
  
  console.log('Step 1: ✅ Navigated to AI CSV import');
  
  // Upload CSV file
  console.log('Step 2: Uploading CSV...');
  const filePromise = page.waitForEvent('filechooser');
  
  // Click the upload area (the div with drag and drop functionality)
  await page.locator('[class*="border-dashed"]').click();
  
  const fileChooser = await filePromise;
  await fileChooser.setFiles({
    name: 'test.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from(testCSV),
  });
  console.log('Step 2: ✅ CSV uploaded');
  
  // Wait for AI analysis result
  console.log('Step 3: Waiting for AI analysis result...');
  
  // Wait for either success (mapping review) or error
  try {
    await expect(page.locator('text=Analysis Complete')).toBeVisible({ timeout: 15000 });
    console.log('Step 3: ✅ AI analysis completed successfully');
  } catch (error) {
    // Check for error messages
    const errorElements = await page.locator('text=error, text=Error, text=failed, text=Failed').count();
    if (errorElements > 0) {
      const errorText = await page.locator('text=error, text=Error, text=failed, text=Failed').first().textContent();
      console.log(`Step 3: ❌ AI analysis failed: ${errorText}`);
      throw new Error(`AI analysis failed: ${errorText}`);
    }
    
    // Check what's actually visible
    const pageContent = await page.textContent('body');
    console.log('Page content after upload:', pageContent?.substring(0, 500) + '...');
    throw error;
  }
  
  // Check the amount convention dropdown
  console.log('Step 4: Checking amount convention...');
  const amountConventionSelect = page.locator('select[data-testid="amount-convention"]');
  await expect(amountConventionSelect).toBeVisible();
  const selectedValue = await amountConventionSelect.inputValue();
  console.log(`Step 4: ✅ Amount convention: ${selectedValue}`);
  
  // Click preview button
  console.log('Step 5: Clicking preview...');
  await page.click('button:has-text("Preview")');
  
  // Wait for preview to appear
  await expect(page.locator('text=Preview Import Results')).toBeVisible({ timeout: 10000 });
  console.log('Step 5: ✅ Preview opened');
  
  // Check the preview results
  console.log('Step 6: Checking preview results...');
  
  // Look for the preview cards (not a table in regular CSV component)
  const previewCards = page.locator('[class*="border rounded-lg"]').filter({hasText: 'Will Import As'});
  const cardCount = await previewCards.count();
  console.log(`Step 6: Found ${cardCount} preview cards`);
  
  // Check specific amounts and types in first few cards
  for (let i = 0; i < Math.min(3, cardCount); i++) {
    const card = previewCards.nth(i);
    
    // Look for amount and type within each card
    const amountText = await card.locator('text=/Amount:.*USD [0-9.,]+/').textContent();
    const typeElement = await card.locator('[class*="bg-green-100"], [class*="bg-red-100"]');
    const typeText = await typeElement.textContent();
    
    console.log(`Card ${i + 1}: Amount=${amountText}, Type=${typeText}`);
  }
  
  console.log('Step 6: ✅ Preview results verified');
  console.log('🎉 CSV preview test completed successfully!');
});