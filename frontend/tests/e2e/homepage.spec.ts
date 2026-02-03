import { test, expect } from '@playwright/test';
import { TestUtils } from '../test-utils';

test.describe('Homepage', () => {
  let utils: TestUtils;

  test.beforeEach(async ({ page }) => {
    utils = new TestUtils(page);
  });

  test('should display homepage correctly', async ({ page }) => {
    await utils.navigateTo('/');
    
    // Should see the welcome message
    await expect(page.getByText('Welcome to MyMascada')).toBeVisible();
    
    // Should see navigation bar brand
    await expect(page.getByRole('navigation').getByText('MyMascada')).toBeVisible();
    
    // Should see feature cards
    await expect(page.getByText('Transaction Tracking')).toBeVisible();
    await expect(page.getByText('AI-Powered Insights')).toBeVisible();
    await expect(page.getByText('Secure & Private')).toBeVisible();
    
    // Should see action buttons (use first() to handle multiple occurrences)
    await expect(page.getByRole('link', { name: 'Sign In' }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: 'Get Started' }).first()).toBeVisible();
  });

  test('should have correct navigation links', async ({ page }) => {
    await utils.navigateTo('/');
    
    // Check link targets
    const signInLink = page.getByRole('link', { name: 'Sign In' }).first();
    const getStartedLink = page.getByRole('link', { name: 'Get Started' }).first();
    
    await expect(signInLink).toHaveAttribute('href', '/auth/login');
    await expect(getStartedLink).toHaveAttribute('href', '/auth/register');
  });

  test('should display correct branding', async ({ page }) => {
    await utils.navigateTo('/');
    
    // Should see logo and brand name
    await expect(page.getByText('MyMascada').first()).toBeVisible();
    
    // Should see hero section
    await expect(page.getByText('Your AI-powered personal finance management application')).toBeVisible();
  });

  test('should have proper meta tags', async ({ page }) => {
    await utils.navigateTo('/');
    
    // Check page title
    await expect(page).toHaveTitle(/MyMascada/);
  });

  test('should be responsive on mobile', async ({ page }) => {
    // This test will run on mobile viewports defined in config
    await utils.navigateTo('/');
    
    // Content should still be visible on mobile
    await expect(page.getByText('Welcome to MyMascada')).toBeVisible();
    await expect(page.getByText('Transaction Tracking')).toBeVisible();
  });
});