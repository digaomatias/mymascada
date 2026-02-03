import { chromium, FullConfig } from '@playwright/test';

async function globalSetup(config: FullConfig) {
  // Global setup for authentication state, test data, etc.
  const browser = await chromium.launch();
  const page = await browser.newPage();
  
  // Pre-authenticate a test user if needed
  // This creates a reusable authentication state for tests
  try {
    await page.goto('http://localhost:3000');
    // Wait for the app to load
    await page.waitForLoadState('networkidle');
    
    console.log('✅ Application is accessible for testing');
  } catch (error) {
    console.error('❌ Failed to access application:', error);
    throw error;
  } finally {
    await browser.close();
  }
}

export default globalSetup;