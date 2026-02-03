import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { ImportReviewScreen } from '@/components/import-review/import-review-screen';
import { ImportAnalysisResult, ConflictType, ConflictResolution, TransactionSource } from '@/types/import-review';
import * as apiClient from '@/lib/api-client';

// Mock the API client
jest.mock('@/lib/api-client', () => ({
  apiClient: {
    executeImportReview: jest.fn()
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
          message: 'Similar transaction found with 82% confidence',
          confidenceScore: 0.82,
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

  const mockOnImportComplete = jest.fn();
  const mockOnCancel = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
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
        processedItems: [
          {
            reviewItemId: 'clean-item-1',
            action: ConflictResolution.Import,
            success: true,
            createdTransactionId: 101
          },
          {
            reviewItemId: 'clean-item-2',
            action: ConflictResolution.Import,
            success: true,
            createdTransactionId: 102
          },
          {
            reviewItemId: 'duplicate-item-1',
            action: ConflictResolution.Skip,
            success: true
          },
          {
            reviewItemId: 'potential-duplicate-1',
            action: ConflictResolution.MergeWithExisting,
            success: true,
            updatedTransactionId: 789
          }
        ],
        warnings: [],
        errors: [],
        targetAccountId: 123
      };

      (apiClient.apiClient.executeImportReview as jest.Mock).mockResolvedValue(mockExecutionResponse);

      render(
        <ImportReviewScreen
          analysisResult={mockAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
          accountName="Test Checking Account"
          showBulkActions={true}
        />
      );

      // Verify initial state
      expect(screen.getByText('Review Import')).toBeInTheDocument();
      expect(screen.getByText('Importing to Test Checking Account')).toBeInTheDocument();
      expect(screen.getByText('0% Reviewed')).toBeInTheDocument();
      expect(screen.getByText('4 items remaining')).toBeInTheDocument();

      // Should show all conflict groups
      expect(screen.getByText('Exact Duplicates')).toBeInTheDocument();
      expect(screen.getByText('Potential Duplicates')).toBeInTheDocument();
      expect(screen.getByText('Ready to Import')).toBeInTheDocument();

      // Use bulk action for clean imports
      const bulkButton = screen.getAllByText('Bulk')[0];
      fireEvent.click(bulkButton);

      await waitFor(() => {
        const importCleanButton = screen.getByText(/Import all clean/);
        fireEvent.click(importCleanButton);
      });

      // Manually resolve the duplicate
      const skipButtons = screen.getAllByText('Skip');
      fireEvent.click(skipButtons[0]); // Skip the exact duplicate

      // Manually resolve the potential duplicate
      const mergeButtons = screen.getAllByText('Merge');
      fireEvent.click(mergeButtons[0]); // Merge the potential duplicate

      // Wait for all decisions to be made
      await waitFor(() => {
        expect(screen.getByText('100% Reviewed')).toBeInTheDocument();
        expect(screen.getByText('0 items remaining')).toBeInTheDocument();
      });

      // Execute the import
      const executeButton = screen.getByText('Execute Import (3)');
      expect(executeButton).not.toBeDisabled();
      fireEvent.click(executeButton);

      // Verify API call was made with correct decisions
      await waitFor(() => {
        expect(apiClient.apiClient.executeImportReview).toHaveBeenCalledWith({
          analysisId: '2024-01-01T10:00:00Z',
          accountId: 123,
          decisions: expect.arrayContaining([
            expect.objectContaining({
              reviewItemId: 'clean-item-1',
              action: ConflictResolution.Import
            }),
            expect.objectContaining({
              reviewItemId: 'clean-item-2',
              action: ConflictResolution.Import
            }),
            expect.objectContaining({
              reviewItemId: 'duplicate-item-1',
              action: ConflictResolution.Skip
            }),
            expect.objectContaining({
              reviewItemId: 'potential-duplicate-1',
              action: ConflictResolution.MergeWithExisting
            })
          ])
        });
      });

      // Verify completion callback
      expect(mockOnImportComplete).toHaveBeenCalledWith(mockExecutionResponse);
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

      // Use bulk auto-resolve
      const bulkButton = screen.getAllByText('Bulk')[0];
      fireEvent.click(bulkButton);

      await waitFor(() => {
        const autoResolveButton = screen.getByText(/Auto resolve all/);
        fireEvent.click(autoResolveButton);
      });

      // Should automatically resolve all items based on smart logic
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
      const executeButton = screen.getByText('Execute Import (0)');
      expect(executeButton).toBeDisabled();
      expect(screen.getByText('Review all conflicts before importing')).toBeInTheDocument();
    });

    test('handles API errors during execution gracefully', async () => {
      const mockError = new Error('Server error occurred');
      (apiClient.apiClient.executeImportReview as jest.Mock).mockRejectedValue(mockError);

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
      const exactDuplicatesHeader = screen.getByText('Exact Duplicates');
      fireEvent.click(exactDuplicatesHeader.closest('div')!);

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

      (apiClient.apiClient.executeImportReview as jest.Mock).mockResolvedValue(mockExecutionResponse);

      render(
        <ImportReviewScreen
          analysisResult={mockAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
        />
      );

      // Make different decisions for each item type
      const importButtons = screen.getAllByText('Import');
      const skipButtons = screen.getAllByText('Skip');
      const mergeButtons = screen.getAllByText('Merge');

      // Import clean items
      fireEvent.click(importButtons[0]);
      fireEvent.click(importButtons[1]);

      // Skip exact duplicate
      fireEvent.click(skipButtons[0]);

      // Merge potential duplicate
      fireEvent.click(mergeButtons[0]);

      await waitFor(() => {
        const executeButton = screen.getByText(/Execute Import/);
        fireEvent.click(executeButton);
      });

      expect(mockOnImportComplete).toHaveBeenCalledWith(mockExecutionResponse);
    });
  });
});