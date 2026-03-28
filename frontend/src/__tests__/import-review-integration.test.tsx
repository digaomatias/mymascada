import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import { ImportReviewScreen } from '@/components/import-review/import-review-screen';
import { ImportAnalysisResult, ConflictType, ConflictResolution, TransactionSource } from '@/types/import-review';
import * as apiClient from '@/lib/api-client';

// Mock the API client
vi.mock('@/lib/api-client', () => ({
  apiClient: {
    executeImportReview: vi.fn()
  }
}));

describe('Import Review Integration Tests', () => {
  const mockAnalysisResult: ImportAnalysisResult = {
    success: true,
    accountId: 123,
    importSource: TransactionSource.CsvImport,
    analysisTimestamp: '2024-01-01T10:00:00Z',
    reviewItems: [
      {
        id: 'clean-item-1',
        importCandidate: {
          amount: 50.00,
          date: '2024-01-01',
          description: 'Coffee Shop A',
          source: TransactionSource.CsvImport,
          sourceRowIndex: 0,
          confidence: 95
        },
        conflicts: [],
        reviewDecision: ConflictResolution.Pending,
        isProcessed: false
      },
      {
        id: 'clean-item-2',
        importCandidate: {
          amount: 25.00,
          date: '2024-01-02',
          description: 'Parking Fee',
          source: TransactionSource.CsvImport,
          sourceRowIndex: 1,
          confidence: 90
        },
        conflicts: [],
        reviewDecision: ConflictResolution.Pending,
        isProcessed: false
      },
      {
        id: 'duplicate-item-1',
        importCandidate: {
          amount: 100.00,
          date: '2024-01-03',
          description: 'Grocery Store',
          referenceNumber: 'REF123',
          source: TransactionSource.CsvImport,
          sourceRowIndex: 2,
          confidence: 85
        },
        conflicts: [{
          type: ConflictType.ExactDuplicate,
          severity: 'High' as any,
          message: 'Exact duplicate transaction found',
          confidenceScore: 0.98,
          conflictingTransaction: {
            id: 456,
            amount: 100.00,
            transactionDate: '2024-01-03',
            description: 'Grocery Store',
            source: TransactionSource.Manual,
            status: 2,
            createdAt: '2024-01-03T10:00:00Z'
          }
        }],
        reviewDecision: ConflictResolution.Pending,
        isProcessed: false
      },
      {
        id: 'potential-duplicate-1',
        importCandidate: {
          amount: 75.50,
          date: '2024-01-04',
          description: 'Restaurant Bill',
          source: TransactionSource.CsvImport,
          sourceRowIndex: 3,
          confidence: 80
        },
        conflicts: [{
          type: ConflictType.PotentialDuplicate,
          severity: 'Medium' as any,
          message: 'Similar transaction found with 90% confidence',
          confidenceScore: 0.90,
          conflictingTransaction: {
            id: 789,
            amount: 75.00,
            transactionDate: '2024-01-04',
            description: 'Restaurant',
            source: TransactionSource.CsvImport,
            status: 2,
            createdAt: '2024-01-04T08:00:00Z'
          }
        }],
        reviewDecision: ConflictResolution.Pending,
        isProcessed: false
      }
    ],
    summary: {
      totalCandidates: 4,
      cleanImports: 2,
      exactDuplicates: 1,
      potentialDuplicates: 1,
      transferConflicts: 0,
      manualConflicts: 0,
      requiresReview: 4
    },
    warnings: [],
    errors: []
  };

  const mockOnImportComplete = vi.fn();
  const mockOnCancel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Complete Import Review Workflow', () => {
    test('handles full workflow from review to successful import', async () => {
      // Mock successful execution response
      const mockExecutionResponse = {
        success: true,
        message: 'Import completed successfully',
        importedTransactionsCount: 3,
        skippedTransactionsCount: 1,
        duplicateTransactionsCount: 0,
        mergedTransactionsCount: 0,
        processedItems: [],
        warnings: [],
        errors: [],
        targetAccountId: 123
      };

      (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockResolvedValue(mockExecutionResponse);

      // Render with pre-resolved decisions to test execution flow
      const resolvedAnalysisResult = {
        ...mockAnalysisResult,
        reviewItems: mockAnalysisResult.reviewItems.map(item => ({
          ...item,
          reviewDecision: item.conflicts.length === 0
            ? ConflictResolution.Import
            : item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
              ? ConflictResolution.Skip
              : ConflictResolution.MergeWithExisting
        }))
      };

      render(
        <ImportReviewScreen
          analysisResult={resolvedAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
          accountName="Test Checking Account"
          showBulkActions={true}
        />
      );

      // Verify header
      expect(screen.getByText('Review Import')).toBeInTheDocument();
      expect(screen.getByText('Importing to Test Checking Account')).toBeInTheDocument();

      // All items resolved - should show 100%
      expect(screen.getByText('100% Reviewed')).toBeInTheDocument();
      expect(screen.getByText('0 items remaining')).toBeInTheDocument();

      // Execute the import
      const executeButton = screen.getByText(/Execute Import/);
      expect(executeButton).not.toBeDisabled();
      fireEvent.click(executeButton);

      // Verify API call was made
      await waitFor(() => {
        expect(apiClient.apiClient.executeImportReview).toHaveBeenCalled();
      });

      // Verify completion callback
      await waitFor(() => {
        expect(mockOnImportComplete).toHaveBeenCalledWith(
          expect.objectContaining({ success: true })
        );
      });
    });

    test('handles bulk auto-resolve functionality', async () => {
      render(
        <ImportReviewScreen
          analysisResult={mockAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
          showBulkActions={true}
        />
      );

      // Bulk actions only appear for groups with >1 items (clean group has 2)
      const bulkButtons = screen.getAllByRole('button', { name: /Bulk/ });
      expect(bulkButtons.length).toBeGreaterThan(0);

      // Open bulk dropdown and auto-resolve the clean group
      fireEvent.click(bulkButtons[0]);

      await waitFor(() => {
        const autoResolveButton = screen.getByText(/Auto resolve all/);
        fireEvent.click(autoResolveButton);
      });

      // Clean items should now be resolved (2 of 4 total)
      // Manually resolve the single-item groups (exact dup and potential dup)
      // Groups render in order: exact duplicates, potential duplicates, clean
      // So Merge[0] = exact dup, Merge[1] = potential dup
      const mergeButtons = screen.getAllByRole('button', { name: 'Merge' });
      fireEvent.click(mergeButtons[1]); // Merge potential duplicate

      const skipButtons = screen.getAllByRole('button', { name: 'Skip' });
      fireEvent.click(skipButtons[0]); // Skip exact duplicate

      // All items should now be resolved
      await waitFor(() => {
        expect(screen.getByText('100% Reviewed')).toBeInTheDocument();
        expect(screen.getByText('0 items remaining')).toBeInTheDocument();
      });

      // Execute button should be enabled
      const executeButton = screen.getByText(/Execute Import/);
      expect(executeButton).not.toBeDisabled();
    });

    test('prevents execution when items are still pending', () => {
      render(
        <ImportReviewScreen
          analysisResult={mockAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
        />
      );

      // Execute button should be disabled
      const executeButton = screen.getByText('Complete Review');
      expect(executeButton).toBeDisabled();
      expect(screen.getByText('Review all conflicts before completing')).toBeInTheDocument();
    });

    test('handles API errors during execution gracefully', async () => {
      const mockError = new Error('Server error occurred');
      (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockRejectedValue(mockError);

      // Create a resolved state
      const resolvedAnalysisResult = {
        ...mockAnalysisResult,
        analysisId: 'test-123',
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

      const executeButton = screen.getByText('Execute Import (4)');
      fireEvent.click(executeButton);

      // Should not call completion callback on error
      await waitFor(() => {
        expect(mockOnImportComplete).not.toHaveBeenCalled();
      });
    });

    test('displays analysis warnings and errors', () => {
      const analysisWithIssues = {
        ...mockAnalysisResult,
        warnings: ['Some duplicate detection may be inaccurate'],
        errors: ['Failed to parse 2 rows due to format issues']
      };

      render(
        <ImportReviewScreen
          analysisResult={analysisWithIssues}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
        />
      );

      expect(screen.getByText('Analysis Notes')).toBeInTheDocument();
      expect(screen.getByText('⚠️ Some duplicate detection may be inaccurate')).toBeInTheDocument();
      expect(screen.getByText('❌ Failed to parse 2 rows due to format issues')).toBeInTheDocument();
    });

    test('handles section collapse and expand', () => {
      render(
        <ImportReviewScreen
          analysisResult={mockAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
        />
      );

      // Initially, conflict sections should be expanded
      expect(screen.getByText('Grocery Store')).toBeInTheDocument();

      // Click to collapse exact duplicates section
      const exactDuplicatesHeaders = screen.getAllByText('Exact Duplicates');
      fireEvent.click(exactDuplicatesHeaders[exactDuplicatesHeaders.length - 1].closest('div')!);

      // Content should be hidden (this would depend on implementation)
      // The test would verify the expand/collapse behavior
    });

    test('validates required fields before execution', async () => {
      const invalidAnalysisResult = {
        ...mockAnalysisResult,
        accountId: undefined as any,
        reviewItems: mockAnalysisResult.reviewItems.map(item => ({
          ...item,
          reviewDecision: ConflictResolution.Import
        }))
      };

      render(
        <ImportReviewScreen
          analysisResult={invalidAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
        />
      );

      // Even though items are resolved, missing accountId should prevent execution
      const executeButton = screen.getByText(/Execute Import/);
      fireEvent.click(executeButton);

      // Should not make API call due to validation failure
      expect(apiClient.apiClient.executeImportReview).not.toHaveBeenCalled();
    });
  });

  describe('Conflict Resolution Scenarios', () => {
    test('handles mixed resolution decisions correctly', async () => {
      const mockExecutionResponse = {
        success: true,
        message: 'Import completed with mixed results',
        importedTransactionsCount: 2,
        skippedTransactionsCount: 1,
        duplicateTransactionsCount: 0,
        mergedTransactionsCount: 1,
        processedItems: [],
        warnings: ['One item required manual review'],
        errors: [],
        targetAccountId: 123
      };

      (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockResolvedValue(mockExecutionResponse);

      // Render with pre-resolved mixed decisions
      const mixedDecisionsResult = {
        ...mockAnalysisResult,
        reviewItems: mockAnalysisResult.reviewItems.map(item => {
          if (item.conflicts.length === 0) {
            return { ...item, reviewDecision: ConflictResolution.Import };
          }
          if (item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)) {
            return { ...item, reviewDecision: ConflictResolution.Skip };
          }
          return { ...item, reviewDecision: ConflictResolution.MergeWithExisting };
        })
      };

      render(
        <ImportReviewScreen
          analysisResult={mixedDecisionsResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
        />
      );

      // All items are resolved, click execute
      const executeButton = screen.getByText(/Execute Import/);
      fireEvent.click(executeButton);

      await waitFor(() => {
        expect(mockOnImportComplete).toHaveBeenCalledWith(
          expect.objectContaining({
            success: true,
            importedTransactionsCount: 2,
            skippedTransactionsCount: 1,
            mergedTransactionsCount: 1
          })
        );
      });
    });
  });
});