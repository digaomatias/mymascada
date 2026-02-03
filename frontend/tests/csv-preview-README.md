# CSV Preview Tests

## Overview
Comprehensive Playwright tests for CSV preview functionality on both reconciliation and AI import screens.

## Test Scenarios

### Amount Conventions Tested:
1. **negative-debits**: Negative values are expenses, positive are income
2. **positive-debits**: Positive values are expenses, negative are income (Credit Cards)  
3. **unsigned_with_type**: Use type column to determine income/expense

### Test Data:
- **Standard data**: Mix of positive/negative amounts
- **Credit card data**: Reversed sign convention
- **Type column data**: Separate column indicating transaction type
- **Edge cases**: Zero amounts, very small amounts, large amounts
- **Real world data**: Based on actual bank CSV format from user

### Screens Tested:
- **Reconciliation screen**: `/accounts/[id]/reconcile` → AI CSV Import
- **AI Import screen**: `/import/ai-csv`

## Running the Tests

### Run all CSV preview tests:
```bash
npm run test:e2e -- csv-preview.spec.ts
```

### Run with UI (visual test runner):
```bash
npm run test:e2e:ui -- csv-preview.spec.ts
```

### Run in headed mode (see browser):
```bash
npm run test:e2e:headed -- csv-preview.spec.ts
```

### Debug mode (step through tests):
```bash
npm run test:e2e:debug -- csv-preview.spec.ts
```

### View test report:
```bash
npm run test:e2e:report
```

## Test Structure

### Helper Functions:
- `uploadCSV()`: Uploads CSV data to file input
- `waitForAnalysis()`: Waits for AI analysis to complete
- `testAmountConvention()`: Tests specific amount convention with expected results

### Test Groups:
1. **Reconciliation Screen Tests**: Tests preview on reconciliation import step
2. **AI Import Screen Tests**: Tests preview on dedicated import screen
3. **Cross-Screen Consistency**: Ensures both screens show identical results
4. **Real World Data**: Tests with actual bank CSV format

## Expected Behavior

### For `negative-debits` convention:
- Positive amounts (e.g., `1487.53`) → **income** (green badge)
- Negative amounts (e.g., `-1400.00`) → **expense** (red badge)

### For `positive-debits` convention:
- Positive amounts (e.g., `100.00`) → **expense** (red badge)  
- Negative amounts (e.g., `-2000.00`) → **income** (green badge)

### For `unsigned_with_type` convention:
- Amount sign ignored, type determined by column mapping
- User maps type values (e.g., "Debit" → Expense, "Credit" → Income)

## Debugging Failed Tests

1. **Check browser console**: Tests will show what values are being parsed
2. **Review screenshots**: Saved in `test-results/` on failure
3. **Check test data**: Ensure CSV data matches expected format
4. **Verify selectors**: Components should have `data-testid` attributes

## Adding New Test Cases

To add new scenarios:

1. Add CSV data to `mockCSVData` object
2. Create new test case with expected results
3. Use `testAmountConvention()` helper for consistent testing
4. Add both reconciliation and import screen variants