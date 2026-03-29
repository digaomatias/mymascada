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
    analysisId: 'analysis-2024-01-01',
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
          message: 'Similar transaction found with 86% confidence',
          confidenceScore: 0.86,
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
      const mockExecutionResponse = {
        success: true,
        message: 'Import completed successfully',
        importedTransactionsCount: 2,
        skippedTransactionsCount: 1,
        duplicateTransactionsCount: 0,
        mergedTransactionsCount: 1,
        processedItems: [],
        warnings: [],
        errors: [],
        targetAccountId: 123
      };

      (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockResolvedValue(mockExecutionResponse);

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

      // Should show all conflict groups (use getAllByText since text appears in summary + section)
      expect(screen.getAllByText('Exact Duplicates').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Potential Duplicates').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Ready to Import').length).toBeGreaterThan(0);

      // Use role-based queries to avoid button+span duplication
      // DOM order: exact dup section, potential dup section, clean section
      // Import buttons: [exactDup, potentialDup, clean1, clean2]
      const importButtons = screen.getAllByRole('button', { name: 'Import' });
      const skipButtons = screen.getAllByRole('button', { name: 'Skip' });

      // Import clean items
      fireEvent.click(importButtons[2]); // clean-item-1
      fireEvent.click(importButtons[3]); // clean-item-2

      // Skip exact duplicate
      fireEvent.click(skipButtons[0]); // duplicate-item-1

      // Merge potential duplicate (Merge buttons only exist on conflict items)
      const mergeButtons = screen.getAllByRole('button', { name: 'Merge' });
      fireEvent.click(mergeButtons[1]); // potential-duplicate-1

      // Wait for all decisions to be made
      await waitFor(() => {
        expect(screen.getByText('100% Reviewed')).toBeInTheDocument();
        expect(screen.getByText('0 items remaining')).toBeInTheDocument();
      });

      // Execute the import (2 imports)
      const executeButton = screen.getByRole('button', { name: /Execute Import/ });
      expect(executeButton).not.toBeDisabled();
      fireEvent.click(executeButton);

      // Verify API call was made with correct decisions
      await waitFor(() => {
        expect(apiClient.apiClient.executeImportReview).toHaveBeenCalledWith(
          expect.objectContaining({
            analysisId: 'analysis-2024-01-01',
            accountId: 123,
            decisions: expect.arrayContaining([
              expect.objectContaining({
                reviewItemId: 'clean-item-1',
                decision: ConflictResolution.Import
              }),
              expect.objectContaining({
                reviewItemId: 'clean-item-2',
                decision: ConflictResolution.Import
              }),
              expect.objectContaining({
                reviewItemId: 'duplicate-item-1',
                decision: ConflictResolution.Skip
              }),
              expect.objectContaining({
                reviewItemId: 'potential-duplicate-1',
                decision: ConflictResolution.MergeWithExisting
              })
            ])
          })
        );
      });

      // Verify completion callback
      expect(mockOnImportComplete).toHaveBeenCalledWith(
        expect.objectContaining({ success: true })
      );
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

      // Bulk auto-resolve is per-section. Only the clean section (2 items) has a Bulk button.
      // First, auto-resolve the clean section items.
      const bulkButtons = screen.getAllByRole('button', { name: 'Bulk' });
      fireEvent.click(bulkButtons[0]);

      await waitFor(() => {
        const autoResolveButton = screen.getByText(/Auto resolve all/);
        fireEvent.click(autoResolveButton);
      });

      // Wait for auto-resolve to take effect (2 clean items resolved → 50%)
      await waitFor(() => {
        expect(screen.getByText('50% Reviewed')).toBeInTheDocument();
      });

      // Skip the exact duplicate (first Skip button in DOM)
      fireEvent.click(screen.getAllByRole('button', { name: 'Skip' })[0]);

      // Wait for the skip to register before clicking merge
      await waitFor(() => {
        expect(screen.getByText('75% Reviewed')).toBeInTheDocument();
      });

      // Merge the potential duplicate (index 1: exact dup still has its Merge button at index 0)
      fireEvent.click(screen.getAllByRole('button', { name: 'Merge' })[1]);

      await waitFor(() => {
        expect(screen.getByText('100% Reviewed')).toBeInTheDocument();
        expect(screen.getByText('0 items remaining')).toBeInTheDocument();
      });

      // Execute button should be enabled
      const executeButton = screen.getByRole('button', { name: /Execute Import/ });
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

      // When toImport=0, button shows "Complete Review" and is disabled
      const executeButton = screen.getByRole('button', { name: 'Complete Review' });
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

      const executeButton = screen.getByRole('button', { name: /Execute Import/ });
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

      // Use getAllByText since "Exact Duplicates" appears in summary stats and section title
      const exactDuplicatesElements = screen.getAllByText('Exact Duplicates');
      const sectionHeader = exactDuplicatesElements[exactDuplicatesElements.length - 1];
      fireEvent.click(sectionHeader.closest('div')!);
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
      const executeButton = screen.getByRole('button', { name: /Execute Import/ });
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
        warnings: [],
        errors: [],
        targetAccountId: 123
      };

      (apiClient.apiClient.executeImportReview as ReturnType<typeof vi.fn>).mockResolvedValue(mockExecutionResponse);

      render(
        <ImportReviewScreen
          analysisResult={mockAnalysisResult}
          onImportComplete={mockOnImportComplete}
          onCancel={mockOnCancel}
        />
      );

      // Use role-based queries to target only button elements
      // DOM order: exact dup, potential dup, clean1, clean2
      const importButtons = screen.getAllByRole('button', { name: 'Import' });
      const skipButtons = screen.getAllByRole('button', { name: 'Skip' });
      const mergeButtons = screen.getAllByRole('button', { name: 'Merge' });

      // Import clean items (indices 2, 3 in DOM order)
      fireEvent.click(importButtons[2]);
      fireEvent.click(importButtons[3]);

      // Skip exact duplicate (index 0)
      fireEvent.click(skipButtons[0]);

      // Merge potential duplicate (index 1 - only conflict items have Merge)
      fireEvent.click(mergeButtons[1]);

      await waitFor(() => {
        const executeButton = screen.getByRole('button', { name: /Execute Import/ });
        fireEvent.click(executeButton);
      });

      await waitFor(() => {
        expect(mockOnImportComplete).toHaveBeenCalledWith(
          expect.objectContaining({ success: true })
        );
      });
    });
  });
});
