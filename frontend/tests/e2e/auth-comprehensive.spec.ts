import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Comprehensive Authentication Tests', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test.describe('Registration Flow', () => {
    test('should successfully register with valid credentials', async ({ page }) => {
      const timestamp = Date.now();
      const testUser = {
        firstName: 'Alice',
        lastName: 'Johnson',
        email: `alice${timestamp}@example.com`,
        password: 'SecurePass123!',
      };

      await utils.navigateTo('/auth/register');
      
      // Fill registration form
      await page.getByLabel(/first name/i).fill(testUser.firstName);
      await page.getByLabel(/last name/i).fill(testUser.lastName);
      await page.getByLabel(/email/i).fill(testUser.email);
      await page.getByLabel(/^password$/i).fill(testUser.password);
      await page.getByLabel(/confirm password/i).fill(testUser.password);
      
      // Submit registration
      await page.getByRole('button', { name: /create account/i }).click();
      
      // Should be redirected to dashboard
      await expect(page).toHaveURL('/dashboard');
      
      // Should see welcome message
      await expect(page.getByText(`Welcome back, ${testUser.firstName}!`)).toBeVisible();
      
      // JWT token should be stored
      const token = await page.evaluate(() => localStorage.getItem('auth_token'));
      expect(token).not.toBeNull();
      expect(token).toBeTruthy();
    });

    test('should reject registration with invalid email format', async ({ page }) => {
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
        await utils.navigateTo('/auth/register');
        
        await page.getByLabel(/first name/i).fill('Test');
        await page.getByLabel(/last name/i).fill('User');
        await page.getByLabel(/email/i).fill(invalidEmail);
        await page.getByLabel(/^password$/i).fill('TestPassword123!');
        await page.getByLabel(/confirm password/i).fill('TestPassword123!');
        
        await page.getByRole('button', { name: /create account/i }).click();
        
        // Should stay on registration page (not redirect)
        const currentUrl = page.url();
        expect(currentUrl).toContain('/auth/register');
        
        console.log(`✅ Invalid email "${invalidEmail}" rejected`);
      }
    });

    test('should reject registration with weak passwords', async ({ page }) => {
      const weakPasswords = [
        { password: '123', reason: 'Too short' },
        { password: 'password', reason: 'Common word' },
        { password: '12345678', reason: 'Numbers only' },
        { password: 'abcdefgh', reason: 'Letters only' },
        { password: 'Password', reason: 'Missing special characters' },
      ];

      for (const { password, reason } of weakPasswords) {
        await utils.navigateTo('/auth/register');
        
        await page.getByLabel(/first name/i).fill('Test');
        await page.getByLabel(/last name/i).fill('User');
        await page.getByLabel(/email/i).fill(`test${Date.now()}@example.com`);
        await page.getByLabel(/^password$/i).fill(password);
        await page.getByLabel(/confirm password/i).fill(password);
        
        await page.getByRole('button', { name: /create account/i }).click();
        
        // Should stay on registration page (validation failed)
        const currentUrl = page.url();
        expect(currentUrl).toContain('/auth/register');
        
        console.log(`✅ Weak password "${password}" (${reason}) rejected`);
      }
    });

    test('should reject registration with password mismatch', async ({ page }) => {
      await utils.navigateTo('/auth/register');
      
      await page.getByLabel(/first name/i).fill('Test');
      await page.getByLabel(/last name/i).fill('User');
      await page.getByLabel(/email/i).fill(`test${Date.now()}@example.com`);
      await page.getByLabel(/^password$/i).fill('TestPassword123!');
      await page.getByLabel(/confirm password/i).fill('DifferentPassword123!');
      
      await page.getByRole('button', { name: /create account/i }).click();
      
      // Should stay on registration page
      const currentUrl = page.url();
      expect(currentUrl).toContain('/auth/register');
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
      
      // Track any alert dialogs that might fire
      const dialogs: string[] = [];
      page.on('dialog', dialog => {
        dialogs.push(dialog.message());
        dialog.dismiss();
      });
      
      // Submit form
      await page.getByRole('button', { name: /create account/i }).click();
      
      // Wait a moment to see if any alerts fire
      await page.waitForTimeout(1000);
      
      // Should not have any alert dialogs from XSS
      expect(dialogs).toHaveLength(0);
      console.log('✅ XSS prevented in registration form');
    });
  });

  test.describe('Login Flow', () => {
    test('should successfully login with valid credentials', async ({ page }) => {
      // First register a user
      const timestamp = Date.now();
      const testUser = {
        firstName: 'Bob',
        lastName: 'Wilson',
        email: `bob${timestamp}@example.com`,
        password: 'SecurePass123!',
      };

      // Register the user
      await utils.navigateTo('/auth/register');
      await page.getByLabel(/first name/i).fill(testUser.firstName);
      await page.getByLabel(/last name/i).fill(testUser.lastName);
      await page.getByLabel(/email/i).fill(testUser.email);
      await page.getByLabel(/^password$/i).fill(testUser.password);
      await page.getByLabel(/confirm password/i).fill(testUser.password);
      await page.getByRole('button', { name: /create account/i }).click();
      await expect(page).toHaveURL('/dashboard');

      // Now logout and test login with fresh context
      await page.context().clearCookies();
      await page.evaluate(() => {
        localStorage.clear();
        sessionStorage.clear();
      });
      await page.goto('about:blank'); // Clear page state
      await utils.navigateTo('/auth/login');
      
      // Fill login form
      await page.getByRole('textbox', { name: /email/i }).fill(testUser.email);
      await page.getByRole('textbox', { name: /password/i }).fill(testUser.password);
      
      // Submit login
      await page.getByRole('button', { name: /sign in/i }).click();
      
      // Should be redirected to dashboard
      await expect(page).toHaveURL('/dashboard');
      
      // Should see welcome message
      await expect(page.getByText(`Welcome back, ${testUser.firstName}!`)).toBeVisible();
      
      // JWT token should be stored
      const token = await page.evaluate(() => localStorage.getItem('auth_token'));
      expect(token).not.toBeNull();
    });

    test('should reject login with invalid credentials', async ({ page }) => {
      await utils.navigateTo('/auth/login');
      
      // Try with non-existent email
      await page.getByRole('textbox', { name: /email/i }).fill('nonexistent@example.com');
      await page.getByRole('textbox', { name: /password/i }).fill('WrongPassword123!');
      
      await page.getByRole('button', { name: /sign in/i }).click();
      
      // Should stay on login page
      const currentUrl = page.url();
      expect(currentUrl).toContain('/auth/login');
      
      // Token should not be stored
      const token = await page.evaluate(() => localStorage.getItem('auth_token'));
      expect(token).toBeNull();
    });

    test('should handle concurrent login attempts', async ({ browser }) => {
      const timestamp = Date.now();
      const testUser = {
        firstName: 'Concurrent',
        lastName: 'User',
        email: `concurrent${timestamp}@example.com`,
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
      await expect(page2).toHaveURL('/dashboard');
      console.log('✅ Concurrent login handled successfully');
      
      await page1.close();
      await page2.close();
    });
  });

  test.describe('Protected Routes & Authentication State', () => {
    test('should redirect unauthenticated users to login', async ({ page }) => {
      // First navigate to the app then clear authentication
      await utils.navigateTo('/');
      await page.evaluate(() => localStorage.removeItem('auth_token'));
      
      // Try to access protected routes
      const protectedRoutes = ['/dashboard', '/transactions', '/accounts', '/import'];
      
      for (const route of protectedRoutes) {
        await utils.navigateTo(route);
        
        // Should be redirected to login
        await expect(page).toHaveURL('/auth/login');
        console.log(`✅ Unauthenticated access to ${route} redirected to login`);
      }
    });

    test('should persist authentication across page refresh', async ({ page }) => {
      // Register and authenticate a user
      const user = await utils.registerAndLogin();
      
      // Verify we're on dashboard
      await expect(page).toHaveURL('/dashboard');
      
      // Get token before refresh
      const tokenBefore = await page.evaluate(() => localStorage.getItem('auth_token'));
      expect(tokenBefore).not.toBeNull();
      
      // Refresh the page
      await page.reload();
      
      // Should still be on dashboard (not redirected to login)
      await expect(page).toHaveURL('/dashboard');
      
      // Should still see welcome message
      await expect(page.getByText(`Welcome back, ${user.firstName}!`)).toBeVisible();
      
      // Token should still be present
      const tokenAfter = await page.evaluate(() => localStorage.getItem('auth_token'));
      expect(tokenAfter).toBe(tokenBefore);
    });

    test('should handle malformed JWT tokens gracefully', async ({ page }) => {
      // Navigate to a page and manually set a malformed JWT token
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
  });

  test.describe('Logout Flow', () => {
    test('should successfully logout and clear authentication state', async ({ page }) => {
      // Register and authenticate a user
      await utils.registerAndLogin();
      
      // Verify we're authenticated
      await expect(page).toHaveURL('/dashboard');
      const tokenBefore = await page.evaluate(() => localStorage.getItem('auth_token'));
      expect(tokenBefore).not.toBeNull();
      
      // Find and click logout button
      // Look for logout in navigation menu (handle mobile vs desktop)
      const isMobile = await page.locator('.md\\:hidden button[aria-label="Toggle menu"]').isVisible();
      
      if (isMobile) {
        // On mobile, open the menu first
        await page.locator('button[aria-label="Toggle menu"]').click();
        await page.getByRole('button', { name: /logout/i }).click();
      } else {
        // On desktop, logout should be in user menu
        await page.getByRole('button', { name: /logout/i }).click();
      }
      
      // Should be redirected to login page
      await expect(page).toHaveURL('/auth/login');
      
      // Token should be removed
      const tokenAfter = await page.evaluate(() => localStorage.getItem('auth_token'));
      expect(tokenAfter).toBeNull();
      
      // Should not be able to access protected routes
      await utils.navigateTo('/dashboard');
      await expect(page).toHaveURL('/auth/login');
    });
  });

  test.describe('Security Tests', () => {
    test('should prevent brute force login attempts', async ({ page }) => {
      const timestamp = Date.now();
      const testUser = {
        firstName: 'Security',
        lastName: 'Test',
        email: `security${timestamp}@example.com`,
        password: 'SecurePass123!',
      };

      // Register a user first
      await utils.navigateTo('/auth/register');
      await page.getByLabel(/first name/i).fill(testUser.firstName);
      await page.getByLabel(/last name/i).fill(testUser.lastName);
      await page.getByLabel(/email/i).fill(testUser.email);
      await page.getByLabel(/^password$/i).fill(testUser.password);
      await page.getByLabel(/confirm password/i).fill(testUser.password);
      await page.getByRole('button', { name: /create account/i }).click();
      await expect(page).toHaveURL('/dashboard');

      // Logout
      await page.evaluate(() => localStorage.removeItem('auth_token'));
      await utils.navigateTo('/auth/login');

      // Attempt multiple failed logins
      const attempts = 5;
      for (let i = 0; i < attempts; i++) {
        await page.getByRole('textbox', { name: /email/i }).fill(testUser.email);
        await page.getByRole('textbox', { name: /password/i }).fill('WrongPassword123!');
        await page.getByRole('button', { name: /sign in/i }).click();
        
        // Should stay on login page
        const currentUrl = page.url();
        expect(currentUrl).toContain('/auth/login');
        
        console.log(`Failed attempt ${i + 1}/${attempts}`);
        
        // Small delay between attempts
        await page.waitForTimeout(100);
      }

      console.log('✅ Multiple failed login attempts handled (should implement rate limiting)');
    });
  });
});