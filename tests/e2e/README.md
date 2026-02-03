# MyMascada E2E Test Suite

This comprehensive End-to-End test suite validates the Rules system functionality using Playwright.

## Overview

The test suite covers:
- ✅ Complete rule creation wizard (6 steps)
- ✅ All rule types (Contains, StartsWith, EndsWith, Equals, Regex)
- ✅ Real transaction matching (no mock data)
- ✅ Case sensitivity handling
- ✅ CategoryPicker integration
- ✅ Data validation and edge cases
- ✅ Rule management features
- ✅ Performance testing
- ✅ API integration

## Test Structure

### Test Categories

1. **Setup and Data Preparation**
   - Creates test account and transactions
   - Ensures consistent test environment

2. **Rule Types Testing**
   - Tests each rule type with real data
   - Validates pattern matching accuracy
   - Verifies transaction filtering

3. **Case Sensitivity Testing**
   - Tests case-sensitive vs case-insensitive matching
   - Validates pattern comparison logic

4. **Edge Cases and Validation**
   - Tests no-match scenarios
   - Validates required field enforcement
   - Handles invalid regex patterns

5. **Rule Management**
   - Tests rule statistics display
   - Validates show/hide inactive rules
   - Tests rule listing functionality

6. **Integration Tests**
   - CategoryPicker integration
   - Data persistence through wizard steps
   - Cross-component interactions

7. **Performance Tests**
   - Rule testing performance with many transactions
   - UI responsiveness validation

8. **API Integration Tests**
   - Backend error handling
   - Network failure scenarios

## Test Data

### Test Transactions Created
```typescript
const testTransactions = [
  { amount: 25.99, description: 'New World Supermarket - Weekly groceries' },
  { amount: 18.45, description: 'New World Metro - Quick lunch' },
  { amount: 12.50, description: 'New World Express - Snacks' },
  { amount: 35.20, description: 'Starbucks Coffee - Morning latte' },
  { amount: 55.00, description: 'Walmart Superstore - Household items' },
  { amount: 15.75, description: 'Coffee Supreme - Afternoon coffee' },
  { amount: 89.99, description: 'Regular Expression Test 123' },
  { amount: 42.00, description: 'Starts with this pattern' },
  { amount: 28.30, description: 'Something ends with pattern' },
  { amount: 99.99, description: 'EXACT MATCH TEST' }
];
```

### Test Rules Created
Each rule type is tested with specific patterns:
- **Contains**: "New World" → matches 3 transactions
- **StartsWith**: "Starts" → matches 1 transaction  
- **EndsWith**: "pattern" → matches 2 transactions
- **Equals**: "EXACT MATCH TEST" → matches 1 transaction
- **Regex**: "\\d{3}" → matches 1 transaction

## Running Tests

### Prerequisites
1. Backend running on `https://localhost:5126`
2. Frontend running on `http://localhost:3000`
3. Test user credentials: `automation@example.com` / `SecureLogin123!`

### Commands

```bash
# Install Playwright browsers
npm run test:e2e:install

# Run all E2E tests
npm run test:e2e

# Run tests with UI mode (interactive)
npm run test:e2e:ui

# Run tests in headed mode (visible browser)
npm run test:e2e:headed

# Debug specific test
npm run test:e2e:debug

# View test report
npm run test:e2e:report
```

### Environment Variables

```bash
# Optional: Custom base URL
PLAYWRIGHT_BASE_URL=http://localhost:3000

# Optional: Custom API URL  
NEXT_PUBLIC_API_URL=https://localhost:5126

# Optional: Enable test data cleanup
PLAYWRIGHT_CLEAN_DATA=true

# Optional: Test environment flag
NODE_ENV=test
```

## Test Configuration

### Browsers Tested
- ✅ Chromium (Desktop)
- ✅ Firefox (Desktop)
- ✅ WebKit/Safari (Desktop)
- ✅ Mobile Chrome (Pixel 5)
- ✅ Mobile Safari (iPhone 12)
- ✅ Microsoft Edge
- ✅ Google Chrome

### Test Settings
- **Timeout**: 30 seconds per test
- **Retries**: 2 on CI, 0 locally
- **Parallel**: Full parallel execution
- **Screenshots**: On failure only
- **Videos**: Retained on failure
- **Traces**: On first retry

## Global Setup & Teardown

### Setup (`global-setup.ts`)
1. Waits for frontend and backend to be ready
2. Tests API health endpoint
3. Ensures test user exists
4. Optional: Cleans existing test data

### Teardown (`global-teardown.ts`)
1. Removes test rules created during tests
2. Removes test transactions
3. Removes test accounts
4. Only runs in test environment

## Helper Class

The `RulesTestHelper` class provides reusable methods:

```typescript
class RulesTestHelper {
  async login()
  async createTestAccount(name: string)
  async createTestTransaction(transaction: TestTransaction)
  async navigateToRules()
  async startRuleCreation()
  async fillBasicInfo(rule: TestRule)
  async fillPattern(rule: TestRule)
  async selectCategory(categoryName: string)
  async skipAdvancedSettings()
  async testRule()
  async verifyTestResults(expectedMatches: number, expectedTransactions: string[])
  async createRule()
  async verifyRuleInList(ruleName: string)
}
```

## Key Validations

### Rule Testing Accuracy
- ✅ No mock data ("Sample transaction matching rule pattern")
- ✅ Real transaction data from database
- ✅ Accurate pattern matching
- ✅ Correct transaction filtering
- ✅ Proper match count display

### Data Integrity
- ✅ Form data persists when navigating wizard steps
- ✅ Selected categories display correctly
- ✅ Rule type changes reflect properly
- ✅ Case sensitivity settings maintained

### Error Handling
- ✅ Required field validation
- ✅ Invalid regex pattern handling
- ✅ API error graceful degradation
- ✅ Network failure scenarios

### Performance
- ✅ Rule testing completes within 5 seconds
- ✅ UI remains responsive during operations
- ✅ Large transaction datasets handled efficiently

## CI/CD Integration

The tests are designed to run in CI environments:

```yaml
# Example GitHub Actions integration
- name: Run E2E Tests
  run: |
    npm run test:e2e
  env:
    PLAYWRIGHT_BASE_URL: http://localhost:3000
    NEXT_PUBLIC_API_URL: https://localhost:5126
    NODE_ENV: test
    PLAYWRIGHT_CLEAN_DATA: true
```

## Debugging

### Common Issues

1. **Tests timing out**
   - Check backend/frontend are running
   - Verify API health endpoint responds
   - Check network connectivity

2. **Authentication failures**
   - Verify test user credentials
   - Check user creation in global setup
   - Validate login API response

3. **Data not found**
   - Ensure test transactions are created
   - Check database connectivity
   - Verify account creation succeeded

4. **Element not found**
   - Check UI component rendering
   - Verify selectors are correct
   - Use `page.pause()` for debugging

### Debug Mode

Run individual tests in debug mode:

```bash
npx playwright test rules-system.spec.ts --debug
```

This opens the Playwright Inspector for step-by-step debugging.

## Maintenance

### Adding New Tests
1. Follow existing test patterns
2. Use the `RulesTestHelper` class
3. Add test data cleanup to teardown
4. Update this README

### Updating Selectors
If UI changes, update selectors in:
- `RulesTestHelper` class methods
- Individual test assertions
- CategoryPicker interactions

### Test Data Management
- Add new test patterns to cleanup lists
- Ensure test isolation
- Use unique identifiers for test data

## Coverage

This test suite provides comprehensive coverage of:
- 🎯 **Rules Wizard**: All 6 steps validated
- 🎯 **Rule Types**: All 5 types tested with real data
- 🎯 **Pattern Matching**: Contains, StartsWith, EndsWith, Equals, Regex
- 🎯 **UI Components**: CategoryPicker, form validation, navigation
- 🎯 **API Integration**: Real backend calls, error handling
- 🎯 **Data Flow**: Database → API → Frontend → User interaction
- 🎯 **Edge Cases**: No matches, invalid input, error scenarios
- 🎯 **Performance**: Large datasets, response times
- 🎯 **Cross-browser**: 6 different browser configurations

The test suite directly addresses the original issue where rule testing showed mock data instead of real transaction matches, ensuring this regression cannot happen again.