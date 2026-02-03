import { test, expect } from '@playwright/test';

// Mock CSV data for testing different scenarios
const mockCSVData = {
  negativeDebits: `Date,Amount,Description
2025-01-01,-100.00,Grocery Store
2025-01-02,2000.00,Salary Deposit
2025-01-03,-50.00,Gas Station`,
  
  positiveDebits: `Date,Amount,Description
2025-01-01,100.00,Credit Card Payment
2025-01-02,-2000.00,Refund
2025-01-03,50.00,Restaurant`,
  
  withTypeColumn: `Date,Amount,Description,Type
2025-01-01,100.00,Grocery Store,Debit
2025-01-02,2000.00,Salary Deposit,Credit
2025-01-03,50.00,Gas Station,Debit`
};

// Helper function to upload CSV data
async function uploadCSV(page, csvData: string) {
  // Create a file from the CSV data
  const buffer = Buffer.from(csvData);
  
  // Look for file input and upload
  const fileInput = page.locator('input[type="file"]');
  await fileInput.setInputFiles({
    name: 'test.csv',
    mimeType: 'text/csv',
    buffer: buffer,
  });
}

// Helper function to wait for AI analysis to complete
async function waitForAnalysis(page) {
  // Wait for the mapping review screen to appear
  await expect(page.locator('text=Review & Configure Mapping')).toBeVisible({ timeout: 10000 });
}

// Helper function to set amount convention and check preview
async function testAmountConvention(page, convention: string, expectedResults: Array<{amount: string, type: 'income' | 'expense'}>) {
  // Select amount convention
  await page.selectOption('select[data-testid="amount-convention"]', convention);
  
  // Click preview button
  await page.click('button:has-text("Preview Data")');
  
  // Wait for preview to appear
  await expect(page.locator('text=Data Preview')).toBeVisible();
  
  // Check each expected result
  for (let i = 0; i < expectedResults.length; i++) {
    const row = page.locator(`table tbody tr:nth-child(${i + 1})`);
    await expect(row.locator('td:nth-child(2)')).toContainText(expectedResults[i].amount);
    
    const typeCell = row.locator('td:nth-child(4)');
    if (expectedResults[i].type === 'income') {
      await expect(typeCell).toContainText('income');
      await expect(typeCell.locator('span')).toHaveClass(/bg-green-100/);
    } else {
      await expect(typeCell).toContainText('expense');
      await expect(typeCell.locator('span')).toHaveClass(/bg-red-100/);
    }
  }
}

test.describe('CSV Preview - Reconciliation Screen', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to reconciliation screen
    await page.goto('/accounts/1/reconcile');
    
    // Start reconciliation (assuming it's already set up)
    await page.click('button:has-text("Start Reconciliation")');
    
    // Wait for import step
    await expect(page.locator('text=Import Bank Transactions')).toBeVisible();
    
    // Select AI CSV Import
    await page.click('button:has-text("AI CSV Import")');
  });

  test('should correctly preview negative-debits convention', async ({ page }) => {
    await uploadCSV(page, mockCSVData.negativeDebits);
    await waitForAnalysis(page);
    
    await testAmountConvention(page, 'negative-debits', [
      { amount: '$100.00', type: 'expense' },  // -100.00 -> expense
      { amount: '$2000.00', type: 'income' },  // 2000.00 -> income  
      { amount: '$50.00', type: 'expense' }    // -50.00 -> expense
    ]);
  });

  test('should correctly preview positive-debits convention', async ({ page }) => {
    await uploadCSV(page, mockCSVData.positiveDebits);
    await waitForAnalysis(page);
    
    await testAmountConvention(page, 'positive-debits', [
      { amount: '$100.00', type: 'expense' },  // 100.00 -> expense (credit card)
      { amount: '$2000.00', type: 'income' },  // -2000.00 -> income (refund)
      { amount: '$50.00', type: 'expense' }    // 50.00 -> expense
    ]);
  });

  test('should correctly preview unsigned_with_type convention', async ({ page }) => {
    await uploadCSV(page, mockCSVData.withTypeColumn);
    await waitForAnalysis(page);
    
    // Select amount convention
    await page.selectOption('select[data-testid="amount-convention"]', 'unsigned_with_type');
    
    // Map type values
    await page.click('button:has-text("Debit"):has-text("Expense")');
    await page.click('button:has-text("Credit"):has-text("Income")');
    
    await testAmountConvention(page, 'unsigned_with_type', [
      { amount: '$100.00', type: 'expense' },  // Debit -> expense
      { amount: '$2000.00', type: 'income' },  // Credit -> income
      { amount: '$50.00', type: 'expense' }    // Debit -> expense
    ]);
  });

  test('should show validation error for missing required fields', async ({ page }) => {
    await uploadCSV(page, mockCSVData.negativeDebits);
    await waitForAnalysis(page);
    
    // Clear required field
    await page.selectOption('select[data-testid="amount-column"]', '');
    
    // Try to preview
    await page.click('button:has-text("Preview Data")');
    
    // Should show error
    await expect(page.locator('text=Please select all required columns')).toBeVisible();
  });

  test('should successfully extract transactions after preview', async ({ page }) => {
    await uploadCSV(page, mockCSVData.negativeDebits);
    await waitForAnalysis(page);
    
    // Set convention and preview
    await testAmountConvention(page, 'negative-debits', [
      { amount: '$100.00', type: 'expense' },
      { amount: '$2000.00', type: 'income' },
      { amount: '$50.00', type: 'expense' }
    ]);
    
    // Extract transactions
    await page.click('button:has-text("Extract for Reconciliation")');
    
    // Should show success message and transactions
    await expect(page.locator('text=Successfully extracted 3 transactions')).toBeVisible();
    await expect(page.locator('text=Bank Transactions (3)')).toBeVisible();
  });
});

test.describe('CSV Preview - AI Import Screen', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to AI CSV import screen
    await page.goto('/import/ai-csv');
  });

  test('should correctly preview negative-debits convention on import screen', async ({ page }) => {
    await uploadCSV(page, mockCSVData.negativeDebits);
    await waitForAnalysis(page);
    
    await testAmountConvention(page, 'negative-debits', [
      { amount: '$100.00', type: 'expense' },
      { amount: '$2000.00', type: 'income' },
      { amount: '$50.00', type: 'expense' }
    ]);
  });

  test('should correctly preview positive-debits convention on import screen', async ({ page }) => {
    await uploadCSV(page, mockCSVData.positiveDebits);
    await waitForAnalysis(page);
    
    await testAmountConvention(page, 'positive-debits', [
      { amount: '$100.00', type: 'expense' },
      { amount: '$2000.00', type: 'income' },
      { amount: '$50.00', type: 'expense' }
    ]);
  });

  test('should correctly preview unsigned_with_type convention on import screen', async ({ page }) => {
    await uploadCSV(page, mockCSVData.withTypeColumn);
    await waitForAnalysis(page);
    
    // Select amount convention
    await page.selectOption('select[data-testid="amount-convention"]', 'unsigned_with_type');
    
    // Map type values
    await page.click('button:has-text("Debit"):has-text("Expense")');
    await page.click('button:has-text("Credit"):has-text("Income")');
    
    await testAmountConvention(page, 'unsigned_with_type', [
      { amount: '$100.00', type: 'expense' },
      { amount: '$2000.00', type: 'income' },
      { amount: '$50.00', type: 'expense' }
    ]);
  });

  test('should handle edge cases correctly', async ({ page }) => {
    const edgeCaseCSV = `Date,Amount,Description
2025-01-01,0.00,Zero Amount
2025-01-02,-0.01,Small Negative
2025-01-03,0.01,Small Positive
2025-01-04,1000000.00,Large Amount`;
    
    await uploadCSV(page, edgeCaseCSV);
    await waitForAnalysis(page);
    
    await testAmountConvention(page, 'negative-debits', [
      { amount: '$0.00', type: 'income' },     // 0.00 -> income (>= 0)
      { amount: '$0.01', type: 'expense' },    // -0.01 -> expense
      { amount: '$0.01', type: 'income' },     // 0.01 -> income
      { amount: '$1000000.00', type: 'income' } // 1000000.00 -> income
    ]);
  });

  test('should import transactions after successful preview', async ({ page }) => {
    await uploadCSV(page, mockCSVData.negativeDebits);
    await waitForAnalysis(page);
    
    // Set convention and preview
    await testAmountConvention(page, 'negative-debits', [
      { amount: '$100.00', type: 'expense' },
      { amount: '$2000.00', type: 'income' },
      { amount: '$50.00', type: 'expense' }
    ]);
    
    // Import transactions
    await page.click('button:has-text("Import Transactions")');
    
    // Should show success and redirect
    await expect(page.locator('text=Successfully imported 3 transactions')).toBeVisible();
  });
});

test.describe('CSV Preview - Cross-Screen Consistency', () => {
  test('should show identical results on both screens', async ({ page }) => {
    const csvData = mockCSVData.negativeDebits;
    
    // Test reconciliation screen
    await page.goto('/accounts/1/reconcile');
    await page.click('button:has-text("Start Reconciliation")');
    await page.click('button:has-text("AI CSV Import")');
    
    await uploadCSV(page, csvData);
    await waitForAnalysis(page);
    
    // Get preview results from reconciliation
    await page.selectOption('select[data-testid="amount-convention"]', 'negative-debits');
    await page.click('button:has-text("Preview Data")');
    
    const reconResults = await page.locator('table tbody tr').evaluateAll(rows => 
      rows.map(row => ({
        amount: row.cells[1].textContent?.trim(),
        type: row.cells[3].textContent?.trim()
      }))
    );
    
    // Test import screen
    await page.goto('/import/ai-csv');
    await uploadCSV(page, csvData);
    await waitForAnalysis(page);
    
    await page.selectOption('select[data-testid="amount-convention"]', 'negative-debits');
    await page.click('button:has-text("Preview Data")');
    
    const importResults = await page.locator('table tbody tr').evaluateAll(rows => 
      rows.map(row => ({
        amount: row.cells[1].textContent?.trim(),
        type: row.cells[3].textContent?.trim()
      }))
    );
    
    // Results should be identical
    expect(reconResults).toEqual(importResults);
  });
});

test.describe('CSV Preview - Real World Data', () => {
  test('should handle actual bank CSV format', async ({ page }) => {
    // Based on the user's screenshot data
    const realWorldCSV = `Type,Details,Particulars,Code,Reference,Amount,Date
Transfer,01-0676-0217445-01,Debit,Transfer,204054,-1400.00,10/07/2025
Direct Credit,Ec Group,Rnmp9Q,,,1487.53,10/07/2025
Automatic Payment,Connolly Gear Trust,Ronaldglgep,,Matiasleote,-800.00,09/07/2025
Direct Debit,Heartland Bank Ltd,Heartland,2518501Lw,,-289.10,04/07/2025
Direct Credit,Tiny Tuis Ea,Tiny Tuis,Reimbursemnt,,40.40,03/07/2025`;
    
    await page.goto('/accounts/1/reconcile');
    await page.click('button:has-text("Start Reconciliation")');
    await page.click('button:has-text("AI CSV Import")');
    
    await uploadCSV(page, realWorldCSV);
    await waitForAnalysis(page);
    
    await testAmountConvention(page, 'negative-debits', [
      { amount: '$1400.00', type: 'expense' },  // -1400.00 -> expense
      { amount: '$1487.53', type: 'income' },   // 1487.53 -> income
      { amount: '$800.00', type: 'expense' },   // -800.00 -> expense
      { amount: '$289.10', type: 'expense' },   // -289.10 -> expense
      { amount: '$40.40', type: 'income' }      // 40.40 -> income
    ]);
  });
});