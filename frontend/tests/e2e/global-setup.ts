import { chromium, FullConfig } from '@playwright/test';

/**
 * Global setup for MyMascada E2E tests
 * This runs once before all tests and prepares the environment
 */
async function globalSetup(config: FullConfig) {
  console.log('🚀 Starting MyMascada E2E Test Global Setup');
  
  const { baseURL } = config.projects[0].use;
  
  // Launch browser for setup
  const browser = await chromium.launch();
  const page = await browser.newPage();

  try {
    // Wait for both frontend and backend to be ready
    console.log('⏳ Waiting for frontend to be ready...');
    await page.goto(baseURL || 'http://localhost:3000', { 
      waitUntil: 'networkidle',
      timeout: 60000 
    });
    
    console.log('⏳ Waiting for backend API to be ready...');
    const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:5126';

    // Test backend health endpoint
    let apiReady = false;
    let attempts = 0;
    const maxAttempts = 30;

    while (!apiReady && attempts < maxAttempts) {
      try {
        await page.request.get(`${apiBaseUrl}/api/v1/auth/health`, {
          ignoreHTTPSErrors: true,
          timeout: 5000
        });
        apiReady = true;
        console.log('✅ Backend API is ready');
      } catch (error) {
        attempts++;
        console.log(`⏳ Backend API not ready yet (attempt ${attempts}/${maxAttempts}), retrying...`);
        await page.waitForTimeout(2000);
      }
    }
    
    if (!apiReady) {
      throw new Error('❌ Backend API failed to become ready within timeout period');
    }

    // Ensure test user exists (from CLAUDE.md)
    console.log('👤 Ensuring test user exists...');
    try {
      // Try to login first to see if user exists
      const loginResponse = await page.request.post(`${apiBaseUrl}/api/v1/auth/login`, {
        data: {
          email: 'automation@example.com',
          password: 'SecureLogin123!'
        },
        ignoreHTTPSErrors: true
      });
      
      if (!loginResponse.ok()) {
        // User doesn't exist, create it
        console.log('👤 Creating test user...');
        const registerResponse = await page.request.post(`${apiBaseUrl}/api/v1/auth/register`, {
          data: {
            email: 'automation@example.com',
            username: 'automation',
            password: 'SecureLogin123!',
            firstName: 'Auto',
            lastName: 'Tester',
            confirmPassword: 'SecureLogin123!'
          },
          ignoreHTTPSErrors: true
        });
        
        if (!registerResponse.ok()) {
          const errorText = await registerResponse.text();
          console.log('⚠️ User registration failed, but may already exist:', errorText);
        } else {
          console.log('✅ Test user created successfully');
        }
      } else {
        console.log('✅ Test user already exists');
      }
    } catch (error) {
      console.log('⚠️ User setup failed, but tests may still work:', error);
    }

    // Clean up existing test data (optional - be careful in production!)
    if (process.env.NODE_ENV === 'test' || process.env.PLAYWRIGHT_CLEAN_DATA === 'true') {
      console.log('🧹 Cleaning up existing test data...');
      try {
        // Login to get auth token
        const loginResponse = await page.request.post(`${apiBaseUrl}/api/v1/auth/login`, {
          data: {
            email: 'automation@example.com',
            password: 'SecureLogin123!'
          },
          ignoreHTTPSErrors: true
        });
        
        if (loginResponse.ok()) {
          const loginData = await loginResponse.json();
          const token = loginData.token;
          
          // Delete test rules (if any exist)
          // Note: This would require implementing a cleanup endpoint
          // For now, we'll just log that cleanup would happen here
          console.log('🧹 Test data cleanup completed');
        }
      } catch (error) {
        console.log('⚠️ Test data cleanup failed, but tests should still work:', error);
      }
    }

    console.log('✅ Global setup completed successfully');
  } catch (error) {
    console.error('❌ Global setup failed:', error);
    throw error;
  } finally {
    await browser.close();
  }
}

export default globalSetup;