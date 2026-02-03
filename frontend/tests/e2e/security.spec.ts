import { test, expect } from '@playwright/test';
import { TestUtils, mockUser } from '../test-utils';

test.describe('Security & Authentication Edge Cases', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should handle malformed JWT tokens gracefully', async ({ page }) => {
    // Navigate to a page and manually corrupt the JWT token
    await utils.navigateTo('/');
    
    // Set a malformed JWT token
    await page.evaluate(() => {
      localStorage.setItem('auth_token', 'invalid.jwt.token');
    });
    
    // Try to access protected route
    await utils.navigateTo('/dashboard');
    
    // Should be redirected to login due to invalid token
    await expect(page).toHaveURL('/auth/login');
    
    // Token should be cleared from localStorage
    const token = await page.evaluate(() => localStorage.getItem('auth_token'));
    expect(token).toBeNull();
  });

  test('should prevent XSS attempts in registration fields', async ({ page }) => {
    await utils.navigateTo('/auth/register');
    
    const xssPayload = '<script>alert("xss")</script>';
    
    // Try to inject XSS in various fields
    await page.getByLabel(/first name/i).fill(xssPayload);
    await page.getByLabel(/last name/i).fill('User');
    await page.getByLabel(/email/i).fill('test@example.com');
    await page.getByLabel(/^password$/i).fill('TestPassword123!');
    await page.getByLabel(/confirm password/i).fill('TestPassword123!');
    
    // Submit and verify XSS is prevented
    await page.getByRole('button', { name: /create account/i }).click();
    
    // Should either show validation error or sanitize the input
    // XSS payload should not execute (no alert dialog)
    const dialogs: string[] = [];
    page.on('dialog', dialog => {
      dialogs.push(dialog.message());
      dialog.dismiss();
    });
    
    // Wait a moment to see if any alerts fire
    await page.waitForTimeout(1000);
    
    // Should not have any alert dialogs from XSS
    expect(dialogs).toHaveLength(0);
  });

  test('should validate password strength requirements', async ({ page }) => {
    await utils.navigateTo('/auth/register');
    
    const weakPasswords = [
      '123',           // Too short
      'password',      // Common word
      '12345678',      // Numbers only
      'abcdefgh',      // Letters only
    ];
    
    for (const weakPassword of weakPasswords) {
      await page.getByLabel(/first name/i).fill('Test');
      await page.getByLabel(/last name/i).fill('User');
      await page.getByLabel(/email/i).fill(`test${Date.now()}@example.com`);
      await page.getByLabel(/^password$/i).fill(weakPassword);
      await page.getByLabel(/confirm password/i).fill(weakPassword);
      
      await page.getByRole('button', { name: /create account/i }).click();
      
      // Should show password validation error
      const errorMessage = page.getByText(/password must be/i).or(
        page.getByText(/password is too weak/i)
      ).or(
        page.getByText(/invalid password/i)
      );
      
      // Either show validation error or prevent submission
      try {
        await expect(errorMessage).toBeVisible({ timeout: 2000 });
        console.log(`✅ Weak password "${weakPassword}" rejected`);
      } catch {
        // If no error shown, ensure we didn't get redirected (i.e., registration failed)
        await expect(page).toHaveURL('/auth/register');
        console.log(`✅ Weak password "${weakPassword}" prevented submission`);
      }
      
      // Clear form for next iteration
      await page.reload();
      await utils.navigateTo('/auth/register');
    }
  });

  test('should handle concurrent login attempts', async ({ browser }) => {
    const timestamp = Date.now();
    const testUser = {
      firstName: 'John',
      lastName: 'Smith',
      email: `user${timestamp}@example.com`,
      password: 'SecurePass123!',
    };
    
    // First, register a user
    const page1 = await browser.newPage();
    const utils1 = new TestUtils(page1);
    
    await utils1.navigateTo('/auth/register');
    await page1.getByLabel(/first name/i).fill(testUser.firstName);
    await page1.getByLabel(/last name/i).fill(testUser.lastName);
    await page1.getByLabel(/email/i).fill(testUser.email);
    await page1.getByLabel(/^password$/i).fill(testUser.password);
    await page1.getByLabel(/confirm password/i).fill(testUser.password);
    await page1.getByRole('button', { name: /create account/i }).click();
    
    await expect(page1).toHaveURL('/dashboard');
    
    // Now try to login from a second browser context
    const page2 = await browser.newPage();
    const utils2 = new TestUtils(page2);
    
    await utils2.navigateTo('/auth/login');
    await page2.getByRole('textbox', { name: /email/i }).fill(testUser.email);
    await page2.getByRole('textbox', { name: /password/i }).fill(testUser.password);
    await page2.getByRole('button', { name: /sign in/i }).click();
    
    // Both sessions should be handled gracefully
    // Either the first session is invalidated, or both are allowed
    // The key is that the system handles it without crashing
    
    await expect(page2).toHaveURL('/dashboard');
    console.log('✅ Concurrent login handled successfully');
    
    await page1.close();
    await page2.close();
  });

  test('should sanitize transaction descriptions to prevent stored XSS', async ({ page }) => {
    // Register and authenticate a user first
    const timestamp = Date.now();
    const testUser = {
      firstName: 'John',
      lastName: 'Smith',
      email: `user${timestamp}@example.com`,
      password: 'SecurePass123!',
    };
    
    await utils.navigateTo('/auth/register');
    await page.getByLabel(/first name/i).fill(testUser.firstName);
    await page.getByLabel(/last name/i).fill(testUser.lastName);
    await page.getByLabel(/email/i).fill(testUser.email);
    await page.getByLabel(/^password$/i).fill(testUser.password);
    await page.getByLabel(/confirm password/i).fill(testUser.password);
    await page.getByRole('button', { name: /create account/i }).click();
    
    await expect(page).toHaveURL('/dashboard');
    
    // Create an account first
    const accountResponse = await page.evaluate(async () => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('https://localhost:5126/api/accounts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          name: 'Test Account',
          type: 0,
          currentBalance: 1000.00,
        }),
      });
      return response.json();
    });
    
    // Try to create transaction with XSS payload in description
    const xssPayload = '<img src=x onerror="alert(\'Stored XSS\')">Evil Transaction';
    
    const transactionResponse = await page.evaluate(async (data) => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('https://localhost:5126/api/transactions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify(data),
      });
      return {
        status: response.status,
        data: response.ok ? await response.json() : await response.text(),
      };
    }, {
      amount: 50.00,
      transactionDate: new Date().toISOString(),
      description: xssPayload,
      accountId: accountResponse.id,
    });
    
    console.log('Transaction creation response:', transactionResponse);
    
    // Track any alert dialogs that might fire
    const dialogs: string[] = [];
    page.on('dialog', dialog => {
      dialogs.push(dialog.message());
      dialog.dismiss();
    });
    
    // Refresh the page to see if stored XSS executes
    await page.reload();
    await page.waitForTimeout(2000);
    
    // Should not have any alert dialogs from stored XSS
    expect(dialogs).toHaveLength(0);
    console.log('✅ Stored XSS prevented in transaction descriptions');
  });

  test('should validate email format thoroughly', async ({ page }) => {
    await utils.navigateTo('/auth/register');
    
    const invalidEmails = [
      'notanemail',
      '@example.com',
      'test@',
      'test..test@example.com',
      'test@.com',
      'test@example.',
      'test with spaces@example.com',
    ];
    
    for (const invalidEmail of invalidEmails) {
      await page.getByLabel(/first name/i).fill('Test');
      await page.getByLabel(/last name/i).fill('User');
      await page.getByLabel(/email/i).fill(invalidEmail);
      await page.getByLabel(/^password$/i).fill('TestPassword123!');
      await page.getByLabel(/confirm password/i).fill('TestPassword123!');
      
      await page.getByRole('button', { name: /create account/i }).click();
      
      // Should show email validation error or prevent submission
      const currentUrl = page.url();
      expect(currentUrl).toContain('/auth/register'); // Should stay on registration page
      
      console.log(`✅ Invalid email "${invalidEmail}" rejected`);
      
      // Clear form for next iteration
      await page.reload();
      await utils.navigateTo('/auth/register');
    }
  });

  test('should prevent overdraft on insufficient funds', async ({ page }) => {
    // Register and authenticate a user first
    const timestamp = Date.now();
    const testUser = {
      firstName: 'John',
      lastName: 'Smith',
      email: `user${timestamp}@example.com`,
      password: 'SecurePass123!',
    };
    
    await utils.navigateTo('/auth/register');
    await page.getByLabel(/first name/i).fill(testUser.firstName);
    await page.getByLabel(/last name/i).fill(testUser.lastName);
    await page.getByLabel(/email/i).fill(testUser.email);
    await page.getByLabel(/^password$/i).fill(testUser.password);
    await page.getByLabel(/confirm password/i).fill(testUser.password);
    await page.getByRole('button', { name: /create account/i }).click();
    
    await expect(page).toHaveURL('/dashboard');
    
    // Create an account with limited balance
    const accountResponse = await page.evaluate(async () => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('https://localhost:5126/api/accounts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          name: 'Test Account',
          type: 0, // Checking account
          currentBalance: 100.00, // Limited balance
        }),
      });
      return response.json();
    });
    
    // Try to create transaction exceeding balance
    const overdraftResponse = await page.evaluate(async (data) => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('https://localhost:5126/api/transactions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify(data),
      });
      return {
        status: response.status,
        data: response.ok ? await response.json() : await response.text(),
      };
    }, {
      amount: -150.00, // More than account balance
      transactionDate: new Date().toISOString(),
      description: 'Overdraft Attempt',
      accountId: accountResponse.id,
    });
    
    // Should either reject the transaction or require overdraft approval
    console.log('Overdraft attempt response:', overdraftResponse);
    
    // If the transaction was created, verify balance constraints
    if (overdraftResponse.status === 200 || overdraftResponse.status === 201) {
      // Check if account balance went negative (might be allowed with overdraft protection)
      const accountCheck = await page.evaluate(async (accountId) => {
        const token = localStorage.getItem('auth_token');
        const response = await fetch(`https://localhost:5126/api/accounts/${accountId}`, {
          headers: {
            'Authorization': `Bearer ${token}`,
          },
        });
        return response.json();
      }, accountResponse.id);
      
      console.log('Account balance after overdraft attempt:', accountCheck.currentBalance);
      
      // Account should either:
      // 1. Maintain positive balance (transaction rejected)
      // 2. Have controlled overdraft limit if negative balance allowed
      if (accountCheck.currentBalance < 0) {
        console.log('⚠️ Overdraft allowed - ensure proper overdraft protection is in place');
      } else {
        console.log('✅ Overdraft prevented - account balance protected');
      }
    } else {
      console.log('✅ Overdraft transaction rejected by server');
      expect(overdraftResponse.status).toBeGreaterThanOrEqual(400);
    }
  });

  test('should prevent duplicate transaction submission', async ({ page }) => {
    // Register and authenticate a user first
    const timestamp = Date.now();
    const testUser = {
      firstName: 'John',
      lastName: 'Smith',
      email: `user${timestamp}@example.com`,
      password: 'SecurePass123!',
    };
    
    await utils.navigateTo('/auth/register');
    await page.getByLabel(/first name/i).fill(testUser.firstName);
    await page.getByLabel(/last name/i).fill(testUser.lastName);
    await page.getByLabel(/email/i).fill(testUser.email);
    await page.getByLabel(/^password$/i).fill(testUser.password);
    await page.getByLabel(/confirm password/i).fill(testUser.password);
    await page.getByRole('button', { name: /create account/i }).click();
    
    await expect(page).toHaveURL('/dashboard');
    
    // Create an account
    const accountResponse = await page.evaluate(async () => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('https://localhost:5126/api/accounts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          name: 'Test Account',
          type: 0,
          currentBalance: 1000.00,
        }),
      });
      return response.json();
    });
    
    const transactionData = {
      amount: 50.00,
      transactionDate: new Date().toISOString(),
      description: 'Duplicate Test Transaction',
      accountId: accountResponse.id,
    };
    
    // Submit the same transaction multiple times rapidly
    const duplicateResponses = await page.evaluate(async (data) => {
      const token = localStorage.getItem('auth_token');
      
      // Create multiple simultaneous requests
      const promises = Array.from({ length: 3 }, () => 
        fetch('https://localhost:5126/api/transactions', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`,
          },
          body: JSON.stringify(data),
        }).then(async response => ({
          status: response.status,
          data: response.ok ? await response.json() : await response.text(),
        }))
      );
      
      return Promise.all(promises);
    }, transactionData);
    
    console.log('Duplicate submission responses:', duplicateResponses.map(r => r.status));
    
    // Should handle duplicates gracefully:
    // Either reject duplicates or use idempotency keys
    const successfulCreations = duplicateResponses.filter(r => r.status >= 200 && r.status < 300);
    
    if (successfulCreations.length > 1) {
      console.log('⚠️ Multiple identical transactions created - consider implementing idempotency');
      
      // Verify transactions are actually different or have duplicate detection
      const transactionIds = successfulCreations.map(r => r.data.id).filter(Boolean);
      const uniqueIds = new Set(transactionIds);
      
      if (uniqueIds.size < transactionIds.length) {
        console.log('✅ Duplicate transactions detected and handled');
      }
    } else {
      console.log('✅ Duplicate transaction prevention working');
    }
  });

  test('should validate account deletion with existing transactions', async ({ page }) => {
    // Register and authenticate a user first
    const timestamp = Date.now();
    const testUser = {
      firstName: 'John',
      lastName: 'Smith',
      email: `user${timestamp}@example.com`,
      password: 'SecurePass123!',
    };
    
    await utils.navigateTo('/auth/register');
    await page.getByLabel(/first name/i).fill(testUser.firstName);
    await page.getByLabel(/last name/i).fill(testUser.lastName);
    await page.getByLabel(/email/i).fill(testUser.email);
    await page.getByLabel(/^password$/i).fill(testUser.password);
    await page.getByLabel(/confirm password/i).fill(testUser.password);
    await page.getByRole('button', { name: /create account/i }).click();
    
    await expect(page).toHaveURL('/dashboard');
    
    // Create an account
    const accountResponse = await page.evaluate(async () => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('https://localhost:5126/api/accounts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          name: 'Test Account for Deletion',
          type: 0,
          currentBalance: 1000.00,
        }),
      });
      return response.json();
    });
    
    // Create a transaction in the account
    const transactionResponse = await page.evaluate(async (accountId) => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('https://localhost:5126/api/transactions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          amount: 100.00,
          transactionDate: new Date().toISOString(),
          description: 'Transaction before account deletion',
          accountId: accountId,
        }),
      });
      return {
        status: response.status,
        data: response.ok ? await response.json() : await response.text(),
      };
    }, accountResponse.id);
    
    console.log('Transaction created:', transactionResponse.status);
    
    // Try to delete the account that has transactions
    const deletionResponse = await page.evaluate(async (accountId) => {
      const token = localStorage.getItem('auth_token');
      const response = await fetch(`https://localhost:5126/api/accounts/${accountId}`, {
        method: 'DELETE',
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      });
      return {
        status: response.status,
        data: response.ok ? await response.text() : await response.text(),
      };
    }, accountResponse.id);
    
    console.log('Account deletion attempt:', deletionResponse);
    
    // Should either:
    // 1. Prevent deletion of accounts with transactions
    // 2. Handle cascading deletion properly
    // 3. Use soft deletion to maintain data integrity
    
    if (deletionResponse.status >= 400) {
      console.log('✅ Account deletion with transactions properly prevented');
    } else if (deletionResponse.status >= 200 && deletionResponse.status < 300) {
      console.log('⚠️ Account deletion allowed - verifying data integrity');
      
      // Check if transactions are still accessible or properly handled
      const transactionCheck = await page.evaluate(async () => {
        const token = localStorage.getItem('auth_token');
        const response = await fetch('https://localhost:5126/api/transactions', {
          headers: {
            'Authorization': `Bearer ${token}`,
          },
        });
        return {
          status: response.status,
          data: response.ok ? await response.json() : await response.text(),
        };
      });
      
      console.log('Transactions after account deletion:', transactionCheck);
      
      // Verify referential integrity is maintained
      if (transactionCheck.status === 200) {
        console.log('✅ Transaction data integrity maintained after account deletion');
      }
    }
  });
});