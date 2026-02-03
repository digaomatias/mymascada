import { chromium, FullConfig } from '@playwright/test';

/**
 * Global teardown for MyMascada E2E tests
 * This runs once after all tests complete
 */
async function globalTeardown(config: FullConfig) {
  console.log('🧹 Starting MyMascada E2E Test Global Teardown');
  
  // Only clean up in test environment to avoid accidentally affecting production data
  if (process.env.NODE_ENV === 'test' || process.env.PLAYWRIGHT_CLEAN_DATA === 'true') {
    const browser = await chromium.launch();
    const page = await browser.newPage();

    try {
      const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:5126';
      
      console.log('👤 Cleaning up test data...');
      
      // Login to get auth token
      const loginResponse = await page.request.post(`${apiBaseUrl}/api/auth/login`, {
        data: {
          email: 'automation@example.com',
          password: 'SecureLogin123!'
        },
        ignoreHTTPSErrors: true
      });
      
      if (loginResponse.ok()) {
        const loginData = await loginResponse.json();
        const token = loginData.token;
        
        // Clean up test rules
        console.log('🧹 Cleaning up test rules...');
        try {
          const rulesResponse = await page.request.get(`${apiBaseUrl}/api/rules`, {
            headers: {
              'Authorization': `Bearer ${token}`
            },
            ignoreHTTPSErrors: true
          });
          
          if (rulesResponse.ok()) {
            const rules = await rulesResponse.json();
            
            // Delete rules that were created by tests (identified by name patterns)
            const testRulePatterns = [
              'Contains - New World',
              'StartsWith - Starts', 
              'EndsWith - pattern',
              'Equals - Exact Match',
              'Regex - Numbers',
              'Case Sensitive Coffee',
              'Case Insensitive coffee',
              'No Match Rule',
              'Test Rule',
              'Invalid Regex Rule',
              'CategoryPicker Integration Test',
              'Data Integrity Test Rule',
              'Performance Test Rule',
              'API Error Test'
            ];
            
            for (const rule of rules) {
              if (testRulePatterns.some(pattern => rule.name?.includes(pattern))) {
                console.log(`🗑️ Deleting test rule: ${rule.name}`);
                await page.request.delete(`${apiBaseUrl}/api/rules/${rule.id}`, {
                  headers: {
                    'Authorization': `Bearer ${token}`
                  },
                  ignoreHTTPSErrors: true
                });
              }
            }
          }
        } catch (error) {
          console.log('⚠️ Rule cleanup failed:', error);
        }
        
        // Clean up test transactions
        console.log('🧹 Cleaning up test transactions...');
        try {
          const transactionsResponse = await page.request.get(`${apiBaseUrl}/api/transactions`, {
            headers: {
              'Authorization': `Bearer ${token}`
            },
            ignoreHTTPSErrors: true
          });
          
          if (transactionsResponse.ok()) {
            const transactionsData = await transactionsResponse.json();
            const transactions = transactionsData.items || transactionsData;
            
            // Delete transactions created by tests (identified by description patterns)
            const testTransactionPatterns = [
              'New World Supermarket - Weekly groceries',
              'New World Metro - Quick lunch',
              'New World Express - Snacks',
              'Starbucks Coffee - Morning latte',
              'Walmart Superstore - Household items',
              'Coffee Supreme - Afternoon coffee',
              'Regular Expression Test 123',
              'Starts with this pattern',
              'Something ends with pattern',
              'EXACT MATCH TEST'
            ];
            
            for (const transaction of transactions) {
              if (testTransactionPatterns.includes(transaction.description)) {
                console.log(`🗑️ Deleting test transaction: ${transaction.description}`);
                await page.request.delete(`${apiBaseUrl}/api/transactions/${transaction.id}`, {
                  headers: {
                    'Authorization': `Bearer ${token}`
                  },
                  ignoreHTTPSErrors: true
                });
              }
            }
          }
        } catch (error) {
          console.log('⚠️ Transaction cleanup failed:', error);
        }
        
        // Clean up test accounts
        console.log('🧹 Cleaning up test accounts...');
        try {
          const accountsResponse = await page.request.get(`${apiBaseUrl}/api/accounts`, {
            headers: {
              'Authorization': `Bearer ${token}`
            },
            ignoreHTTPSErrors: true
          });
          
          if (accountsResponse.ok()) {
            const accounts = await accountsResponse.json();
            
            // Delete accounts created by tests
            const testAccountPatterns = [
              'E2E Test Account',
              'Test Account E2E'
            ];
            
            for (const account of accounts) {
              if (testAccountPatterns.some(pattern => account.name?.includes(pattern))) {
                console.log(`🗑️ Deleting test account: ${account.name}`);
                await page.request.delete(`${apiBaseUrl}/api/accounts/${account.id}`, {
                  headers: {
                    'Authorization': `Bearer ${token}`
                  },
                  ignoreHTTPSErrors: true
                });
              }
            }
          }
        } catch (error) {
          console.log('⚠️ Account cleanup failed:', error);
        }
        
        console.log('✅ Test data cleanup completed');
      } else {
        console.log('⚠️ Could not login for cleanup, skipping...');
      }
    } catch (error) {
      console.error('❌ Global teardown failed:', error);
      // Don't throw here - teardown failures shouldn't fail the entire test suite
    } finally {
      await browser.close();
    }
  } else {
    console.log('ℹ️ Skipping cleanup (not in test environment)');
  }
  
  console.log('✅ Global teardown completed');
}

export default globalTeardown;