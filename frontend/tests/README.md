# E2E Testing with Playwright

This directory contains end-to-end tests for the MyMascada finance application using Playwright.

## Setup

Playwright has been configured to test the application across multiple browsers (Chromium, Firefox, WebKit) and devices (desktop and mobile).

### Prerequisites

1. Install dependencies:
   ```bash
   npm install
   ```

2. Install Playwright browsers:
   ```bash
   npm run test:e2e:install
   ```

## Running Tests

### Basic Commands

```bash
# Run all e2e tests (headless)
npm run test:e2e

# Run tests with UI (visual test runner)
npm run test:e2e:ui

# Run tests in headed mode (see browser)
npm run test:e2e:headed

# Debug tests step by step
npm run test:e2e:debug

# View test reports
npm run test:e2e:report
```

### Specific Test Execution

```bash
# Run specific test file
npx playwright test auth.spec.ts

# Run tests matching pattern
npx playwright test --grep "login"

# Run only on specific browser
npx playwright test --project=chromium

# Run only on mobile
npx playwright test --project="Mobile Chrome"
```

## Test Structure

### Directory Layout

```
tests/
├── e2e/                     # End-to-end test files
│   ├── auth.spec.ts         # Authentication flow tests
│   ├── transactions.spec.ts # Transaction management tests
│   └── csv-import.spec.ts   # CSV import functionality tests
├── fixtures/                # Test data files
│   └── sample-transactions.csv
├── test-utils.ts           # Common test utilities
├── global-setup.ts         # Global test setup
└── global-teardown.ts      # Global test cleanup
```

### Test Categories

1. **Authentication Tests** (`auth.spec.ts`)
   - Homepage display for unauthenticated users
   - Navigation to login/register pages
   - Form validation
   - Login/logout flows (skipped until backend ready)

2. **Transaction Management** (`transactions.spec.ts`)
   - Dashboard overview display
   - Transaction CRUD operations (skipped until backend ready)
   - Filtering and searching
   - Data validation

3. **CSV Import** (`csv-import.spec.ts`)
   - File upload functionality
   - CSV parsing and validation
   - Column mapping
   - Duplicate detection
   - Categorization

## Test Utilities

The `TestUtils` class provides common functionality:

```typescript
import { TestUtils } from '../test-utils';

const utils = new TestUtils(page);

// Navigation
await utils.navigateTo('/dashboard');

// Form interactions
await utils.fillField('email', 'test@example.com');
await utils.clickButton('submit');

// Assertions
await utils.expectSuccessMessage('Transaction saved');
await utils.expectTableContains('Coffee Shop');

// File uploads
await utils.uploadFile('file-input', './fixtures/transactions.csv');
```

## Mock Data

Test utilities include mock data for consistent testing:

```typescript
import { mockUser, mockTransaction, mockAccount } from '../test-utils';

// Use in tests
await page.fill('[name="email"]', mockUser.email);
```

## Configuration

### Playwright Config (`playwright.config.ts`)

- **Base URL**: `http://localhost:3000`
- **Browsers**: Chromium, Firefox, WebKit
- **Mobile**: Pixel 5, iPhone 12
- **Reporters**: HTML, JSON, JUnit
- **Retries**: 2 on CI, 0 locally
- **Parallel**: Enabled (except CI)

### Global Setup

- Verifies application accessibility before tests
- Can be extended for authentication setup
- Database seeding for test data

## Best Practices

### Writing Tests

1. **Use data-testid attributes** for reliable element selection:
   ```tsx
   <button data-testid="submit-button">Submit</button>
   ```

2. **Wait for network idle** after navigation:
   ```typescript
   await page.goto('/dashboard');
   await page.waitForLoadState('networkidle');
   ```

3. **Use meaningful test descriptions**:
   ```typescript
   test('should create new transaction with valid data', async ({ page }) => {
     // Test implementation
   });
   ```

4. **Test both happy path and error cases**:
   ```typescript
   test('should validate required fields on empty form submission', async ({ page }) => {
     // Test validation
   });
   ```

### Test Organization

1. **Group related tests** using `test.describe()`
2. **Use beforeEach** for common setup
3. **Skip tests** that depend on unimplemented backend features
4. **Tag tests** for different execution scenarios

### Debugging

1. **Use headed mode** for visual debugging:
   ```bash
   npm run test:e2e:headed
   ```

2. **Debug specific test**:
   ```bash
   npx playwright test auth.spec.ts --debug
   ```

3. **Pause execution** in test:
   ```typescript
   await page.pause(); // Opens Playwright Inspector
   ```

4. **Take screenshots** for debugging:
   ```typescript
   await page.screenshot({ path: 'debug.png' });
   ```

## CI/CD Integration

Tests are configured for CI environments:

- **Retries**: 2 attempts on failure
- **Workers**: Single worker to avoid conflicts
- **Reports**: JUnit XML for CI integration
- **Videos**: Recorded on failure

### GitHub Actions Example

```yaml
- name: Run Playwright tests
  run: npm run test:e2e
  
- name: Upload test results
  uses: actions/upload-artifact@v3
  if: always()
  with:
    name: playwright-report
    path: test-results/
```

## Known Issues & Limitations

1. **Backend Integration**: Many tests are skipped pending backend implementation
2. **Authentication**: Auth flow tests need backend API endpoints
3. **File Uploads**: CSV import tests require backend processing
4. **Real Data**: Tests use mock data until database integration

## Future Enhancements

1. **Visual Regression Testing**: Add screenshot comparisons
2. **API Testing**: Integrate API tests with e2e flows
3. **Performance Testing**: Add Lighthouse audits
4. **Accessibility Testing**: Integrate axe-core for a11y testing
5. **Component Testing**: Add Playwright component tests

## Troubleshooting

### Common Issues

1. **Port conflicts**: Ensure port 3000 is available
2. **Browser installation**: Run `npm run test:e2e:install`
3. **Flaky tests**: Add proper waits and increase timeouts
4. **File paths**: Use absolute paths for fixtures

### Debug Commands

```bash
# Check Playwright installation
npx playwright --version

# List available browsers
npx playwright install --dry-run

# Generate test code
npx playwright codegen localhost:3000

# Run single test with trace
npx playwright test auth.spec.ts --trace on
```

For more information, see the [Playwright documentation](https://playwright.dev/docs/intro).