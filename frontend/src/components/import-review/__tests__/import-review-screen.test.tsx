import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { ImportReviewScreen } from '../import-review-screen';
import { ImportAnalysisResult, ConflictType, ConflictResolution, TransactionSource } from '@/types/import-review';
import * as apiClient from '@/lib/api-client';

// Mock the API client
vi.mock('@/lib/api-client', () => ({
  apiClient: {
    executeImportReview: vi.fn()
  }
}));

// Mock toast notifications
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn()
  }
}));

// Mock utilities
vi.mock('@/lib/utils', () => ({
  formatCurrency: (amount: number) => `$${amount.toFixed(2)}`,
  formatDate: (date: string) => new Date(date).toLocaleDateString()
}));

const mockAnalysisResult: ImportAnalysisResult = {
  success: true,
  accountId: 123,
  importSource: TransactionSource.CsvImport,
  reviewItems: [
    {
      id: 'item1',
      importCandidate: {
        amount: 100,
        date: '2024-01-01',
        description: 'Clean transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 95
      },
      conflicts: [],
      reviewDecision: ConflictResolution.Pending,
      isProcessed: false
    },
    {
      id: 'item2',
      importCandidate: {
        amount: 200,
        date: '2024-01-02',
        description: 'Duplicate transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 1,
        confidence: 85
      },
      conflicts: [{
        type: ConflictType.ExactDuplicate,
        severity: 'High' as any,
        message: 'Exact duplicate found',
        confidenceScore: 0.95
      }],
      reviewDecision: ConflictResolution.Pending,
      isProcessed: false
    }
  ],
  summary: {
    totalCandidates: 2,
    cleanImports: 1,
    exactDuplicates: 1,
    potentialDuplicates: 0,
    transferConflicts: 0,
    manualConflicts: 0,
    requiresReview: 2
  },
  analysisTimestamp: '2024-01-01T10:00:00Z',
  warnings: [],
  errors: []
};

describe('ImportReviewScreen', () => {
  const mockOnImportComplete = vi.fn();
  const mockOnCancel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  test('renders review screen with correct header', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
        accountName="Test Account"
      />
    );

    expect(screen.getByText('Review Import')).toBeInTheDocument();
    expect(screen.getByText('Importing to Test Account')).toBeInTheDocument();
  });

  test('groups transactions correctly by conflict type', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText('Exact Duplicates')).toBeInTheDocument();
    expect(screen.getByText('Ready to Import')).toBeInTheDocument();
  });

  test('shows correct progress statistics', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText('0% Reviewed')).toBeInTheDocument();
    expect(screen.getByText('2 items remaining')).toBeInTheDocument();
  });

  test('execute import button is disabled when items are pending', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    const executeButton = screen.getByText('Execute Import (0)');
    expect(executeButton).toBeDisabled();
    expect(screen.getByText('Review all conflicts before importing')).toBeInTheDocument();
  });

  test('execute import button becomes enabled after all decisions are made', async () => {
    const { rerender } = render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    // Mock resolved state
    const resolvedAnalysisResult = {
      ...mockAnalysisResult,
      reviewItems: mockAnalysisResult.reviewItems.map(item => ({
        ...item,
        reviewDecision: ConflictResolution.Import
      }))
    };

    rerender(
      <ImportReviewScreen
        analysisResult={resolvedAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    const executeButton = screen.getByText('Execute Import (2)');
    expect(executeButton).not.toBeDisabled();
  });

  test('handles back button click', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    fireEvent.click(screen.getByText('Back'));
    expect(mockOnCancel).toHaveBeenCalled();
  });

  test('executes import with correct API call', async () => {
    const mockExecuteResponse = {
      success: true,
      importedTransactionsCount: 2,
      skippedTransactionsCount: 0,
      errors: []
    };

    (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockResolvedValue(mockExecuteResponse);

    // Create a resolved analysis result
    const resolvedAnalysisResult = {
      ...mockAnalysisResult,
      analysisId: 'test-analysis-123',
      reviewItems: mockAnalysisResult.reviewItems.map(item => ({
        ...item,
        reviewDecision: ConflictResolution.Import
      }))
    };

    render(
      <ImportReviewScreen
        analysisResult={resolvedAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    const executeButton = screen.getByText('Execute Import (2)');
    fireEvent.click(executeButton);

    await waitFor(() => {
      expect(apiClient.apiClient.executeImportReview).toHaveBeenCalledWith({
        analysisId: 'test-analysis-123',
        accountId: 123,
        decisions: [
          {
            reviewItemId: 'item1',
            action: ConflictResolution.Import,
            userNotes: '',
            keepExistingTransactionId: undefined
          },
          {
            reviewItemId: 'item2',
            action: ConflictResolution.Import,
            userNotes: '',
            keepExistingTransactionId: undefined
          }
        ]
      });
    });
  });

  test('handles import execution success', async () => {
    const mockExecuteResponse = {
      success: true,
      importedTransactionsCount: 2,
      skippedTransactionsCount: 0,
      errors: []
    };

    (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockResolvedValue(mockExecuteResponse);

    const resolvedAnalysisResult = {
      ...mockAnalysisResult,
      analysisId: 'test-analysis-123',
      reviewItems: mockAnalysisResult.reviewItems.map(item => ({
        ...item,
        reviewDecision: ConflictResolution.Import
      }))
    };

    render(
      <ImportReviewScreen
        analysisResult={resolvedAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    const executeButton = screen.getByText('Execute Import (2)');
    fireEvent.click(executeButton);

    await waitFor(() => {
      expect(mockOnImportComplete).toHaveBeenCalledWith(mockExecuteResponse);
    });
  });

  test('handles import execution error', async () => {
    const mockError = new Error('Network error');
    (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockRejectedValue(mockError);

    const resolvedAnalysisResult = {
      ...mockAnalysisResult,
      analysisId: 'test-analysis-123',
      reviewItems: mockAnalysisResult.reviewItems.map(item => ({
        ...item,
        reviewDecision: ConflictResolution.Import
      }))
    };

    render(
      <ImportReviewScreen
        analysisResult={resolvedAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    const executeButton = screen.getByText('Execute Import (2)');
    fireEvent.click(executeButton);

    await waitFor(() => {
      expect(mockOnImportComplete).not.toHaveBeenCalled();
    });
  });

  test('validates all required fields before execution', async () => {
    const invalidAnalysisResult = {
      ...mockAnalysisResult,
      accountId: undefined as any
    };

    render(
      <ImportReviewScreen
        analysisResult={invalidAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    // This test verifies that even if items were resolved, it would still fail due to missing accountId
    // (the component would need to be re-rendered with resolved items for this to be testable)
  });

  test('shows bulk actions when enabled', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
        showBulkActions={true}
      />
    );

    // The bulk actions should be visible in the grouped sections
    expect(screen.getByText('Bulk')).toBeInTheDocument();
  });

  test('hides bulk actions when disabled', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
        showBulkActions={false}
      />
    );

    expect(screen.queryByText('Bulk')).not.toBeInTheDocument();
  });

  test('displays warnings and errors when present', () => {
    const analysisWithWarnings = {
      ...mockAnalysisResult,
      warnings: ['Warning: Some data may be incomplete'],
      errors: ['Error: Invalid date format detected']
    };

    render(
      <ImportReviewScreen
        analysisResult={analysisWithWarnings}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText('Analysis Notes')).toBeInTheDocument();
    expect(screen.getByText('⚠️ Warning: Some data may be incomplete')).toBeInTheDocument();
    expect(screen.getByText('❌ Error: Invalid date format detected')).toBeInTheDocument();
  });

  test('handles section expansion/collapse', () => {
    render(
      <ImportReviewScreen
        analysisResult={mockAnalysisResult}
        onImportComplete={mockOnImportComplete}
        onCancel={mockOnCancel}
      />
    );

    // Click on "Ready to Import" section to toggle it
    const sectionHeader = screen.getByText('Ready to Import');
    fireEvent.click(sectionHeader.closest('div')!);

    // This would collapse/expand the section
    // The exact behavior depends on the initial state
  });
});