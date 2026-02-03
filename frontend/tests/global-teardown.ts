import { FullConfig } from '@playwright/test';

async function globalTeardown(config: FullConfig) {
  // Clean up test data, close connections, etc.
  console.log('🧹 Cleaning up test environment');
  
  // Add any cleanup logic here:
  // - Clear test database
  // - Remove uploaded test files
  // - Reset application state
}

export default globalTeardown;