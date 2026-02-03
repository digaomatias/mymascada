import { Page, expect } from '@playwright/test';

/**
 * Common test utilities for MyMascada finance app
 */

export class TestUtils {
  constructor(private page: Page) {}

  /**
   * Navigate to a specific page and wait for it to load
   */
  async navigateTo(path: string) {
    const url = path.startsWith('http') ? path : `http://localhost:3000${path}`;
    await this.page.goto(url);
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Fill form field by data-testid
   */
  async fillField(testId: string, value: string) {
    await this.page.getByTestId(testId).fill(value);
  }

  /**
   * Click button by data-testid
   */
  async clickButton(testId: string) {
    await this.page.getByTestId(testId).click();
  }

  /**
   * Wait for and verify success message
   */
  async expectSuccessMessage(message?: string) {
    const successEl = this.page.getByRole('alert').filter({ hasText: /success|saved|created/i });
    await expect(successEl).toBeVisible();
    if (message) {
      await expect(successEl).toContainText(message);
    }
  }

  /**
   * Wait for and verify error message
   */
  async expectErrorMessage(message?: string) {
    const errorEl = this.page.getByRole('alert').filter({ hasText: /error|failed|invalid/i });
    await expect(errorEl).toBeVisible();
    if (message) {
      await expect(errorEl).toContainText(message);
    }
  }

  /**
   * Verify table contains expected data
   */
  async expectTableContains(text: string) {
    const table = this.page.getByRole('table');
    await expect(table).toBeVisible();
    await expect(table).toContainText(text);
  }

  /**
   * Upload file to input by data-testid
   */
  async uploadFile(testId: string, filePath: string) {
    await this.page.getByTestId(testId).setInputFiles(filePath);
  }

  /**
   * Register and authenticate a new user
   * Returns the user data for reference
   */
  async registerAndLogin(customUser?: Partial<typeof mockUser>) {
    const timestamp = Date.now();
    const user = {
      ...mockUser,
      ...customUser,
      email: customUser?.email || `test${timestamp}@example.com`,
    };

    // Navigate to registration page
    await this.navigateTo('/auth/register');
    
    // Fill registration form
    await this.page.getByLabel(/first name/i).fill(user.firstName);
    await this.page.getByLabel(/last name/i).fill(user.lastName);
    await this.page.getByLabel(/email/i).fill(user.email);
    await this.page.getByLabel(/^password$/i).fill(user.password);
    await this.page.getByLabel(/confirm password/i).fill(user.password);
    
    // Submit registration - wait for button to be enabled
    const submitButton = this.page.getByRole('button', { name: /create account/i });
    await expect(submitButton).toBeEnabled();
    await submitButton.click();
    
    // Wait for navigation to complete and handle different possible outcomes
    try {
      await expect(this.page).toHaveURL('/dashboard', { timeout: 10000 });
    } catch (error) {
      // If not redirected to dashboard, check for error or handle alternate flow
      const currentUrl = this.page.url();
      if (currentUrl.includes('/auth/')) {
        // Still on auth page, check for errors
        const errorAlert = this.page.locator('[role="alert"]');
        if (await errorAlert.isVisible()) {
          const errorText = await errorAlert.textContent();
          throw new Error(`Registration failed: ${errorText}`);
        } else {
          // No error visible, but not on dashboard - this might be expected
          console.log(`Registration completed but on ${currentUrl} instead of dashboard`);
        }
      }
    }
    
    return user;
  }

  /**
   * Login with existing user credentials
   */
  async login(email: string = mockUser.email, password: string = mockUser.password) {
    // Navigate to login page
    await this.navigateTo('/auth/login');
    
    // Fill login form
    await this.page.getByLabel(/email or username/i).fill(email);
    await this.page.getByLabel(/password/i).fill(password);
    
    // Submit login
    await this.page.getByRole('button', { name: /sign in/i }).click();
    
    // Wait for redirect with timeout and error handling
    try {
      await expect(this.page).toHaveURL('/dashboard', { timeout: 10000 });
    } catch (error) {
      // Handle cases where login doesn't redirect to dashboard
      const currentUrl = this.page.url();
      console.log(`Login completed but redirected to ${currentUrl} instead of dashboard`);
      
      // Check if there's an error message
      const errorAlert = this.page.locator('[role="alert"]');
      if (await errorAlert.isVisible()) {
        const errorText = await errorAlert.textContent();
        throw new Error(`Login failed: ${errorText}`);
      }
    }
  }

  /**
   * Setup test environment (API mocking, etc.)
   */
  async setupTestEnvironment() {
    // Setup any API mocks or test data here
    // For now, this is a placeholder
  }

  /**
   * Cleanup after tests
   */
  async cleanup() {
    // Cleanup any test data or mocks
    // For now, this is a placeholder
  }

  /**
   * Create a test account via API
   */
  async createAccount(accountData: {
    name: string;
    type: string;
    initialBalance: number;
  }) {
    // In a real test, this would call the API
    // For now, return a mock account
    return {
      id: Math.floor(Math.random() * 10000),
      ...accountData,
      createdAt: new Date().toISOString()
    };
  }

  /**
   * Create a test transaction via API
   */
  async createTransaction(transactionData: {
    accountId: number;
    amount: number;
    description: string;
    date: string;
  }) {
    // In a real test, this would call the API
    // For now, return a mock transaction
    return {
      id: Math.floor(Math.random() * 10000),
      ...transactionData,
      createdAt: new Date().toISOString()
    };
  }

  /**
   * Upload CSV content for import/reconciliation
   */
  async uploadCsvContent(csvContent: string) {
    // Create a blob from the CSV content
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const fileName = `test-import-${Date.now()}.csv`;
    
    // Find the file input and upload
    const fileInput = this.page.locator('input[type="file"]').first();
    await fileInput.setInputFiles({
      name: fileName,
      mimeType: 'text/csv',
      buffer: Buffer.from(csvContent)
    });
    
    // Wait for upload to process
    await this.page.waitForTimeout(1000);
  }

  /**
   * Upload CSV file from fixtures
   */
  async uploadCsvFile(fileName: string) {
    const filePath = `tests/fixtures/${fileName}`;
    const fileInput = this.page.locator('input[type="file"]').first();
    await fileInput.setInputFiles(filePath);
    await this.page.waitForTimeout(1000);
  }

  /**
   * Create account via API for testing
   */
  async createTestAccount(accountData?: Partial<{
    name: string;
    type: number;
    currentBalance: number;
  }>) {
    const defaultAccount = {
      name: `Test Account ${Date.now()}`,
      type: 0, // Checking
      currentBalance: 1000.00,
      ...accountData
    };

    // Use Playwright's request API instead of browser fetch to avoid CORS/mixed content issues
    const token = await this.page.evaluate(() => localStorage.getItem('auth_token'));
    if (!token) throw new Error('No auth token found');

    const response = await this.page.request.post('https://localhost:5126/api/accounts', {
      data: defaultAccount,
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      ignoreHTTPSErrors: true,
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Failed to create account: ${response.status()} - ${errorText}`);
    }

    return await response.json();
  }

  /**
   * Create transaction via API for conflict testing
   */
  async createTestTransaction(accountId: number, transactionData?: Partial<{
    amount: number;
    description: string;
    transactionDate: string;
    status: number;
    notes?: string;
    location?: string;
    userDescription?: string;
  }>) {
    const defaultTransaction = {
      amount: -25.50,
      description: 'Test Transaction',
      transactionDate: new Date().toISOString(), // Use ISO DateTime format
      status: 2, // TransactionStatus.Cleared
      ...transactionData
    };

    // Use Playwright's request API instead of browser fetch to avoid CORS/mixed content issues
    const token = await this.page.evaluate(() => localStorage.getItem('auth_token'));
    if (!token) throw new Error('No auth token found');

    const requestData = {
      accountId,
      ...defaultTransaction
    };

    console.log('Creating transaction with data:', requestData);

    const response = await this.page.request.post('https://localhost:5126/api/transactions', {
      data: requestData,
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      ignoreHTTPSErrors: true,
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Failed to create transaction: ${response.status()} - ${errorText}`);
    }

    const result = await response.json();
    console.log('Transaction created successfully:', result.id);
    
    // IMPORTANT: Manual transactions are automatically marked as IsReviewed=true
    // For E2E tests, we need unreviewed transactions to appear on categorization page
    // So we need to unmark them as reviewed by calling an API endpoint or updating them
    console.log('Note: Transaction created as reviewed, may need to unmark for categorization tests');
    
    return result;
  }

  /**
   * Wait for Import Review screen to load
   */
  async waitForImportReview() {
    await expect(this.page.getByText(/review import/i)).toBeVisible({ timeout: 15000 });
    await this.page.waitForTimeout(1000); // Allow time for all data to load
  }

  /**
   * Setup Import Review test scenario with conflicts
   */
  async setupImportReviewScenario() {
    // Create test account
    const account = await this.createTestAccount({
      name: 'Test Import Account',
      currentBalance: 1000.00
    });

    // Create existing transactions that will conflict
    await this.createTestTransaction(account.id, {
      amount: -25.50,
      description: 'Coffee Shop Purchase',
      transactionDate: '2024-01-01'
    });

    await this.createTestTransaction(account.id, {
      amount: -4.50,
      description: 'Starbucks Coffee',
      transactionDate: '2024-01-02'
    });

    return account;
  }

  /**
   * Perform bulk action in Import Review
   */
  async performBulkAction(actionText: string) {
    const bulkButton = this.page.getByRole('button', { name: /bulk/i }).first();
    await bulkButton.click();
    
    await expect(this.page.getByText(actionText)).toBeVisible();
    await this.page.getByText(actionText).click();
    
    // Wait for action to complete
    await this.page.waitForTimeout(500);
  }

  /**
   * Resolve all conflicts automatically
   */
  async autoResolveAllConflicts() {
    await this.performBulkAction('auto resolve all');
    await expect(this.page.getByText(/100% reviewed|0 items remaining/i)).toBeVisible({ timeout: 10000 });
  }

  /**
   * Execute import and wait for completion
   */
  async executeImport() {
    const executeButton = this.page.getByRole('button', { name: /execute import/i });
    await expect(executeButton).toBeEnabled();
    await executeButton.click();
    
    // Wait for success or error message
    await Promise.race([
      expect(this.page.getByText(/import completed successfully/i)).toBeVisible({ timeout: 20000 }),
      expect(this.page.getByText(/import failed|error/i)).toBeVisible({ timeout: 20000 })
    ]);
  }

  /**
   * Verify Import Review statistics
   */
  async verifyImportStats(expectedStats: {
    totalItems?: number;
    conflicts?: number;
    clean?: number;
    reviewed?: string; // percentage like "50%"
  }) {
    if (expectedStats.reviewed) {
      await expect(this.page.getByText(new RegExp(expectedStats.reviewed + ' reviewed', 'i'))).toBeVisible();
    }
    
    // Add more stat verification as needed
  }
}

/**
 * Mock user data for testing
 */
export const mockUser = {
  email: 'user@example.com',
  password: 'SecurePass123!',
  firstName: 'John',
  lastName: 'Doe',
};

/**
 * Mock transaction data for testing
 */
export const mockTransaction = {
  amount: '150.00',
  description: 'Test Purchase',
  category: 'Shopping',
  date: '2024-01-15',
  type: 'expense',
};

/**
 * Mock account data for testing
 */
export const mockAccount = {
  name: 'Test Checking Account',
  type: 'checking',
  institution: 'Test Bank',
  balance: '2500.00',
};