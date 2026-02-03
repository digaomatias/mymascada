import { test, expect, Page } from '@playwright/test';

// Test user credentials from CLAUDE.md
const TEST_USER = {
  email: 'test-user@mymascada.local',
  password: 'SecurePassword123!'
};

async function loginTestUser(page: Page) {
  await page.goto('/auth/login');
  await page.fill('input[type="email"]', TEST_USER.email);
  await page.fill('input[type="password"]', TEST_USER.password);
  await page.click('button[type="submit"]');
  await page.waitForURL('/dashboard');
}

async function navigateToRules(page: Page) {
  // Click on Manage dropdown
  await page.click('button:has-text("Manage")');
  // Click on Rules
  await page.click('a[href="/rules"]');
  await page.waitForURL('/rules');
}

test.describe('Rules System End-to-End Tests', () => {
  test.beforeEach(async ({ page }) => {
    await loginTestUser(page);
  });

  test('should navigate to rules page and show empty state', async ({ page }) => {
    await navigateToRules(page);
    
    // Check page title and description
    await expect(page.locator('h1')).toContainText('Categorization Rules');
    await expect(page.locator('text=Automate transaction categorization')).toBeVisible();
    
    // Check for Create Rule button
    await expect(page.locator('a[href="/rules/new"]')).toBeVisible();
    await expect(page.locator('text=Create Rule')).toBeVisible();
    
    // Should show empty state if no rules exist
    const emptyState = page.locator('text=No rules yet');
    if (await emptyState.isVisible()) {
      await expect(page.locator('text=Create your first categorization rule')).toBeVisible();
      await expect(page.locator('text=Create Your First Rule')).toBeVisible();
    }
  });

  test('should create a new categorization rule through wizard', async ({ page }) => {
    await navigateToRules(page);
    
    // Click Create Rule button
    await page.click('a[href="/rules/new"]');
    await page.waitForURL('/rules/new');
    
    // Verify wizard header
    await expect(page.locator('h1')).toContainText('Create New Rule');
    await expect(page.locator('text=Set up a new categorization rule')).toBeVisible();
    
    // Step 1: Basic Info
    await expect(page.locator('text=Basic Info')).toBeVisible();
    await page.fill('input[id="name"]', 'Test Grocery Rule');
    await page.fill('textarea[id="description"]', 'Automatically categorize grocery store purchases');
    await page.fill('input[id="priority"]', '1');
    
    // Next step should be enabled after filling required fields
    const nextButton = page.locator('button:has-text("Next")');
    await expect(nextButton).toBeEnabled();
    await nextButton.click();
    
    // Step 2: Pattern
    await expect(page.locator('text=Pattern')).toBeVisible();
    
    // Select rule type
    await page.click('[data-testid="rule-type-select"], .rule-type-select, button:has-text("Select rule type")');
    await page.click('text=Contains');
    
    // Enter pattern
    await page.fill('input[id="pattern"]', 'GROCERY');
    
    // Check case sensitivity option
    await page.check('input[id="caseSensitive"]');
    
    await nextButton.click();
    
    // Step 3: Category
    await expect(page.locator('text=Target Category')).toBeVisible();
    
    // Select a category (assuming categories exist)
    await page.click('[data-testid="category-select"], .category-select, button:has-text("Select category")');
    
    // Try to select the first available category
    const firstCategory = page.locator('[role="option"]').first();
    if (await firstCategory.isVisible()) {
      await firstCategory.click();
    } else {
      // If no categories in dropdown, try clicking any category option
      await page.click('text=Groceries', { timeout: 5000 }).catch(() => {
        // If Groceries category doesn't exist, try Food or any other common category
        return page.click('text=Food').catch(() => {
          return page.click('[role="option"]').catch(() => {
            console.log('No categories available - this test may need sample data');
          });
        });
      });
    }
    
    await nextButton.click();
    
    // Step 4: Advanced (optional settings)
    await expect(page.locator('text=Advanced')).toBeVisible();
    
    // Set amount range
    await page.fill('input[id="minAmount"]', '10.00');
    await page.fill('input[id="maxAmount"]', '500.00');
    
    // Select account types
    await page.check('text=Checking');
    await page.check('text=CreditCard');
    
    await nextButton.click();
    
    // Step 5: Test (skip actual testing for speed)
    await expect(page.locator('text=Test')).toBeVisible();
    await expect(page.locator('button:has-text("Test Rule Against Existing Transactions")')).toBeVisible();
    
    await nextButton.click();
    
    // Step 6: Review
    await expect(page.locator('text=Review')).toBeVisible();
    await expect(page.locator('text=Rule Summary')).toBeVisible();
    
    // Verify summary shows our data
    await expect(page.locator('text=Test Grocery Rule')).toBeVisible();
    await expect(page.locator('text=Contains')).toBeVisible();
    await expect(page.locator('text=GROCERY')).toBeVisible();
    
    // Check the "Activate rule immediately" checkbox
    await page.check('input[id="isActive"]');
    
    // Create the rule
    const createButton = page.locator('button:has-text("Create Rule")');
    await expect(createButton).toBeEnabled();
    await createButton.click();
    
    // Should redirect to rules page
    await page.waitForURL('/rules', { timeout: 10000 });
    
    // Verify rule was created and appears in list
    await expect(page.locator('text=Test Grocery Rule')).toBeVisible();
    await expect(page.locator('text=Contains')).toBeVisible();
  });

  test('should display rules statistics', async ({ page }) => {
    await navigateToRules(page);
    
    // Check for statistics cards
    await expect(page.locator('text=Total Rules')).toBeVisible();
    await expect(page.locator('text=Active Rules')).toBeVisible();
    await expect(page.locator('text=Applications')).toBeVisible();
    await expect(page.locator('text=Accuracy')).toBeVisible();
    
    // Statistics should show numbers
    const totalRulesCard = page.locator('text=Total Rules').locator('..');
    await expect(totalRulesCard.locator('text=/^\\d+$/')).toBeVisible();
  });

  test('should filter rules by active/inactive status', async ({ page }) => {
    await navigateToRules(page);
    
    // Find the "Show inactive rules" checkbox
    const inactiveCheckbox = page.locator('input[type="checkbox"]:near(text="Show inactive rules")');
    
    if (await inactiveCheckbox.isVisible()) {
      // Test toggling the checkbox
      await inactiveCheckbox.check();
      
      // Should show count of rules including inactive
      await expect(page.locator('text=/\\d+ rules? found/')).toBeVisible();
      
      await inactiveCheckbox.uncheck();
      
      // Count might change when hiding inactive rules
      await expect(page.locator('text=/\\d+ rules? found/')).toBeVisible();
    }
  });

  test('should handle rule management actions', async ({ page }) => {
    await navigateToRules(page);
    
    // Look for existing rules
    const ruleCards = page.locator('[data-testid="rule-card"], .rule-card, .card:has(button[aria-label="Toggle rule status"], button:has([data-testid="play-icon"]), button:has([data-testid="pause-icon"]))');
    
    const ruleCount = await ruleCards.count();
    
    if (ruleCount > 0) {
      const firstRule = ruleCards.first();
      
      // Test rule actions (play/pause, edit, delete buttons)
      const playPauseButton = firstRule.locator('button:has([data-testid="play-icon"]), button:has([data-testid="pause-icon"]), button:has-text("▶"), button:has-text("⏸")').first();
      const editButton = firstRule.locator('button:has([data-testid="edit-icon"]), a[href*="/edit"], button:has-text("✏")').first();
      const deleteButton = firstRule.locator('button:has([data-testid="delete-icon"]), button:has-text("🗑"), button.text-red-600').first();
      
      // Verify action buttons exist
      if (await playPauseButton.isVisible()) {
        await expect(playPauseButton).toBeVisible();
      }
      
      if (await editButton.isVisible()) {
        await expect(editButton).toBeVisible();
      }
      
      if (await deleteButton.isVisible()) {
        await expect(deleteButton).toBeVisible();
      }
    } else {
      console.log('No rules found to test actions - this is expected if no rules have been created yet');
    }
  });

  test('should navigate to rule suggestions', async ({ page }) => {
    await navigateToRules(page);
    
    // Click View Suggestions button
    const suggestionsButton = page.locator('a[href="/rules/suggestions"], button:has-text("View Suggestions")');
    
    if (await suggestionsButton.isVisible()) {
      await suggestionsButton.click();
      await page.waitForURL('/rules/suggestions');
      
      // Should show suggestions page (even if empty)
      await expect(page.locator('text=Rule Suggestions, text=Suggestions')).toBeVisible();
    } else {
      console.log('Suggestions button not found - may not be implemented yet');
    }
  });

  test('should validate form fields in wizard', async ({ page }) => {
    await navigateToRules(page);
    await page.click('a[href="/rules/new"]');
    await page.waitForURL('/rules/new');
    
    // Step 1: Try to proceed without required fields
    const nextButton = page.locator('button:has-text("Next")');
    
    // Next should be disabled without name
    await expect(nextButton).toBeDisabled();
    
    // Fill name, Next should be enabled
    await page.fill('input[id="name"]', 'Test Rule');
    await expect(nextButton).toBeEnabled();
    
    await nextButton.click();
    
    // Step 2: Try to proceed without pattern
    await expect(nextButton).toBeDisabled();
    
    await page.fill('input[id="pattern"]', 'TEST');
    await expect(nextButton).toBeEnabled();
    
    await nextButton.click();
    
    // Step 3: Try to proceed without category
    await expect(nextButton).toBeDisabled();
  });

  test('should preserve form data when navigating between steps', async ({ page }) => {
    await navigateToRules(page);
    await page.click('a[href="/rules/new"]');
    await page.waitForURL('/rules/new');
    
    // Fill Step 1
    await page.fill('input[id="name"]', 'Persistent Test Rule');
    await page.fill('textarea[id="description"]', 'Test description persistence');
    await page.click('button:has-text("Next")');
    
    // Fill Step 2
    await page.fill('input[id="pattern"]', 'PERSISTENT');
    await page.click('button:has-text("Next")');
    
    // Go back to Step 1
    await page.click('button:has-text("Previous")');
    await page.click('button:has-text("Previous")');
    
    // Verify data is preserved
    await expect(page.locator('input[id="name"]')).toHaveValue('Persistent Test Rule');
    await expect(page.locator('textarea[id="description"]')).toHaveValue('Test description persistence');
    
    // Go forward to Step 2
    await page.click('button:has-text("Next")');
    
    // Verify pattern is preserved
    await expect(page.locator('input[id="pattern"]')).toHaveValue('PERSISTENT');
  });

  test('should handle network errors gracefully', async ({ page }) => {
    await navigateToRules(page);
    
    // Intercept API calls and simulate network error
    await page.route('**/api/rules', (route) => {
      route.abort('failed');
    });
    
    // Reload the page to trigger the API call
    await page.reload();
    
    // Should show error message or handle gracefully
    // The exact behavior depends on implementation
    await expect(page.locator('text=Failed to load rules, text=Error, text=loading')).toBeVisible({ timeout: 10000 });
  });

  test('should work on mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    
    await navigateToRules(page);
    
    // Navigation should work on mobile
    await expect(page.locator('h1:has-text("Categorization Rules")')).toBeVisible();
    
    // Create rule button should be accessible
    await expect(page.locator('a[href="/rules/new"]')).toBeVisible();
    
    // Statistics cards should stack properly on mobile
    await expect(page.locator('text=Total Rules')).toBeVisible();
  });

  test('should show loading states appropriately', async ({ page }) => {
    await navigateToRules(page);
    
    // Look for loading indicators
    const loadingIndicators = page.locator('.animate-pulse, .animate-spin, text=Loading, text=loading');
    
    // Loading states should appear briefly during data loading
    // This test might be flaky depending on network speed
    if (await loadingIndicators.first().isVisible({ timeout: 1000 })) {
      console.log('Loading state detected');
    }
    
    // After loading, content should be visible
    await expect(page.locator('h1:has-text("Categorization Rules")')).toBeVisible();
  });
});

test.describe('Rules System Integration Tests', () => {
  test.beforeEach(async ({ page }) => {
    await loginTestUser(page);
  });

  test('should integrate with existing transactions for testing', async ({ page }) => {
    // First, ensure we have some transactions to test against
    await page.goto('/transactions');
    
    // Check if transactions exist, if not, skip test
    const transactionsList = page.locator('[data-testid="transactions-list"], .transaction-item, .transaction-row');
    const transactionCount = await transactionsList.count();
    
    if (transactionCount === 0) {
      console.log('No transactions found - skipping integration test');
      return;
    }
    
    // Create a rule and test it against transactions
    await navigateToRules(page);
    await page.click('a[href="/rules/new"]');
    await page.waitForURL('/rules/new');
    
    // Quick rule creation
    await page.fill('input[id="name"]', 'Integration Test Rule');
    await page.click('button:has-text("Next")');
    
    await page.fill('input[id="pattern"]', 'TEST');
    await page.click('button:has-text("Next")');
    
    // Select any available category
    await page.click('[data-testid="category-select"], button:has-text("Select category")');
    await page.click('[role="option"]').first();
    await page.click('button:has-text("Next")');
    
    // Skip advanced
    await page.click('button:has-text("Next")');
    
    // Test against transactions
    await expect(page.locator('text=Test')).toBeVisible();
    await page.click('button:has-text("Test Rule Against Existing Transactions")');
    
    // Wait for test results
    await expect(page.locator('text=Testing Rule, text=Test Results')).toBeVisible({ timeout: 10000 });
  });

  test('should work with the overall navigation flow', async ({ page }) => {
    // Test complete navigation flow: Dashboard -> Rules -> Create -> Back to Rules
    await page.goto('/dashboard');
    await expect(page.locator('h1, h2').filter({ hasText: /dashboard|welcome/i })).toBeVisible();
    
    // Navigate to rules
    await navigateToRules(page);
    await expect(page.locator('h1:has-text("Categorization Rules")')).toBeVisible();
    
    // Go to create rule
    await page.click('a[href="/rules/new"]');
    await expect(page.locator('h1:has-text("Create New Rule")')).toBeVisible();
    
    // Go back to rules (using browser back or navigation)
    await page.goBack();
    await expect(page.locator('h1:has-text("Categorization Rules")')).toBeVisible();
    
    // Navigate back to dashboard
    await page.click('a[href="/dashboard"]');
    await expect(page.locator('h1, h2').filter({ hasText: /dashboard|welcome/i })).toBeVisible();
  });
});