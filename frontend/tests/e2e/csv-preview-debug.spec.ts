import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('CSV Preview Debug', () => {
  test('should register user and create account successfully', async ({ page }) => {
    const utils = new TestUtils(page);
    
    // Step 1: Register and login
    console.log('Step 1: Registering user...');
    await utils.registerAndLogin();
    console.log('Step 1: ✅ User registered and logged in');
    
    // Step 2: Navigate to accounts
    console.log('Step 2: Navigating to accounts...');
    await page.goto('/accounts');
    await page.waitForLoadState('networkidle');
    console.log('Step 2: ✅ Navigated to accounts page');
    
    // Step 3: Click Add Account button
    console.log('Step 3: Clicking Add Account...');
    await page.click('button:has-text("Add Account")');
    await page.waitForTimeout(1000); // Wait for modal/page to load
    console.log('Step 3: ✅ Add Account clicked');
    
    // Step 4: Fill account form
    console.log('Step 4: Filling account form...');
    
    // Wait for form fields to be visible
    await expect(page.locator('#name')).toBeVisible({ timeout: 10000 });
    
    await page.fill('#name', 'Test Checking');
    await page.selectOption('#type', '1'); // Checking account
    await page.fill('#currentBalance', '1000');
    console.log('Step 4: ✅ Form filled');
    
    // Step 5: Submit form
    console.log('Step 5: Submitting form...');
    await page.click('button:has-text("Create Account")');
    console.log('Step 5: ✅ Form submitted');
    
    // Step 6: Wait for success
    console.log('Step 6: Waiting for success message...');
    await expect(page.locator('text=Account created')).toBeVisible({ timeout: 10000 });
    console.log('Step 6: ✅ Account created successfully');
    
    // Step 7: Verify account appears in list
    console.log('Step 7: Checking account list...');
    await expect(page.getByRole('heading', { name: 'Test Checking' })).toBeVisible();
    console.log('Step 7: ✅ Account appears in list');
    
    console.log('🎉 All steps completed successfully!');
  });
  
  test('should navigate to AI CSV import page', async ({ page }) => {
    const utils = new TestUtils(page);
    
    // Register and login
    await utils.registerAndLogin();
    
    // Navigate to AI CSV import
    console.log('Navigating to AI CSV import...');
    await page.goto('/import/ai-csv');
    await page.waitForLoadState('networkidle');
    
    // Check if the upload area is visible
    await expect(page.locator('text=Upload your bank statement CSV')).toBeVisible({ timeout: 10000 });
    console.log('✅ AI CSV import page loaded successfully');
    
    // Test file upload trigger
    console.log('Testing file upload trigger...');
    const filePromise = page.waitForEvent('filechooser', { timeout: 5000 });
    await page.click('text=Upload your bank statement CSV');
    
    const fileChooser = await filePromise;
    console.log('✅ File chooser opened successfully');
    
    // Close without uploading
    await page.keyboard.press('Escape');
  });
  
  test('should handle reconciliation flow setup', async ({ page }) => {
    const utils = new TestUtils(page);
    
    // Register and login
    await utils.registerAndLogin();
    
    // Create account first
    await page.goto('/accounts');
    await page.click('button:has-text("Add Account")');
    await page.fill('#name', 'Test Checking');
    await page.selectOption('#type', '1');
    await page.fill('#currentBalance', '1000');
    await page.click('button:has-text("Create Account")');
    await expect(page.locator('text=Account created')).toBeVisible();
    
    // Click on the account to go to details
    console.log('Clicking on account...');
    await page.click('table tbody tr:first-child');
    await page.waitForLoadState('networkidle');
    
    // Look for Reconcile button
    console.log('Looking for Reconcile button...');
    await expect(page.locator('button:has-text("Reconcile")')).toBeVisible({ timeout: 10000 });
    
    // Click Reconcile
    console.log('Clicking Reconcile...');
    await page.click('button:has-text("Reconcile")');
    await page.waitForLoadState('networkidle');
    
    // Fill reconciliation form
    console.log('Filling reconciliation form...');
    await page.fill('input[type="number"]', '1000');
    await page.click('button:has-text("Start Reconciliation")');
    
    // Wait for import step
    console.log('Waiting for import step...');
    await expect(page.locator('text=Import Bank Transactions')).toBeVisible({ timeout: 10000 });
    
    // Check for AI CSV Import button
    await expect(page.locator('button:has-text("AI CSV Import")')).toBeVisible();
    console.log('✅ Reconciliation flow setup completed successfully');
  });
});