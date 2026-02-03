import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { apiClient } from '../api-client';
import { TransactionSource, ConflictResolution } from '@/types/import-review';

// Mock fetch globally
global.fetch = vi.fn();

describe('API Client - Import Review', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  describe('analyzeImportForReview', () => {
    const mockAnalysisRequest = {
      source: TransactionSource.CsvImport,
      accountId: 123,
      csvData: {
        content: 'base64content',
        mappings: { amount: 'Amount', date: 'Date', description: 'Description' },
        hasHeader: true
      }
    };

    test('makes correct API call for CSV analysis', async () => {
      const mockResponse = {
        success: true,
        accountId: 123,
        reviewItems: [],
        summary: {
          totalCandidates: 0,
          cleanImports: 0,
          exactDuplicates: 0,
          potentialDuplicates: 0,
          transferConflicts: 0,
          manualConflicts: 0,
          requiresReview: 0
        },
        analysisTimestamp: '2024-01-01T10:00:00Z',
        warnings: [],
        errors: []
      };

      (fetch as any).mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockResponse),
        text: vi.fn().mockResolvedValue(JSON.stringify(mockResponse))
      });

      const result = await apiClient.analyzeImportForReview(mockAnalysisRequest);

      expect(fetch).toHaveBeenCalledWith('/api/ImportReview/analyze', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(mockAnalysisRequest)
      });

      expect(result).toEqual(mockResponse);
    });

    test('handles API error responses', async () => {
      (fetch as any).mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        text: vi.fn().mockResolvedValue('Invalid request data'),
        json: vi.fn().mockRejectedValue(new Error('Not JSON'))
      });

      await expect(apiClient.analyzeImportForReview(mockAnalysisRequest))
        .rejects
        .toThrow('HTTP error! status: 400');
    });

    test('handles network errors', async () => {
      (fetch as any).mockRejectedValue(new Error('Network error'));

      await expect(apiClient.analyzeImportForReview(mockAnalysisRequest))
        .rejects
        .toThrow('Network error');
    });

    test('handles JSON parsing errors', async () => {
      (fetch as any).mockResolvedValue({
        ok: true,
        json: vi.fn().mockRejectedValue(new Error('Invalid JSON')),
        text: vi.fn().mockResolvedValue('invalid json')
      });

      await expect(apiClient.analyzeImportForReview(mockAnalysisRequest))
        .rejects
        .toThrow('Invalid JSON');
    });
  });

  describe('executeImportReview', () => {
    const mockExecutionRequest = {
      analysisId: 'test-analysis-123',
      accountId: 123,
      decisions: [
        {
          reviewItemId: 'item1',
          action: ConflictResolution.Import,
          userNotes: 'Approved for import'
        },
        {
          reviewItemId: 'item2',
          action: ConflictResolution.Skip,
          userNotes: 'Duplicate transaction'
        }
      ]
    };

    test('makes correct API call for import execution', async () => {
      const mockResponse = {
        success: true,
        message: 'Import completed successfully',
        importedTransactionsCount: 1,
        skippedTransactionsCount: 1,
        duplicateTransactionsCount: 0,
        mergedTransactionsCount: 0,
        processedItems: [],
        warnings: [],
        errors: [],
        targetAccountId: 123
      };

      (fetch as any).mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockResponse),
        text: vi.fn().mockResolvedValue(JSON.stringify(mockResponse))
      });

      const result = await apiClient.executeImportReview(mockExecutionRequest);

      expect(fetch).toHaveBeenCalledWith('/api/ImportReview/execute', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(mockExecutionRequest)
      });

      expect(result).toEqual(mockResponse);
    });

    test('handles execution errors', async () => {
      (fetch as any).mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        text: vi.fn().mockResolvedValue('Server error occurred'),
        json: vi.fn().mockRejectedValue(new Error('Not JSON'))
      });

      await expect(apiClient.executeImportReview(mockExecutionRequest))
        .rejects
        .toThrow('HTTP error! status: 500');
    });

    test('handles empty decision list', async () => {
      const emptyRequest = {
        ...mockExecutionRequest,
        decisions: []
      };

      const mockResponse = {
        success: true,
        message: 'No decisions to process',
        importedTransactionsCount: 0,
        skippedTransactionsCount: 0,
        duplicateTransactionsCount: 0,
        mergedTransactionsCount: 0,
        processedItems: [],
        warnings: ['No decisions provided'],
        errors: [],
        targetAccountId: 123
      };

      (fetch as any).mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockResponse),
        text: vi.fn().mockResolvedValue(JSON.stringify(mockResponse))
      });

      const result = await apiClient.executeImportReview(emptyRequest);
      expect(result.warnings).toContain('No decisions provided');
    });

    test('handles partial success responses', async () => {
      const mockResponse = {
        success: true,
        message: 'Import completed with some errors',
        importedTransactionsCount: 1,
        skippedTransactionsCount: 0,
        duplicateTransactionsCount: 0,
        mergedTransactionsCount: 0,
        processedItems: [
          {
            reviewItemId: 'item1',
            action: ConflictResolution.Import,
            success: true,
            createdTransactionId: 456
          },
          {
            reviewItemId: 'item2',
            action: ConflictResolution.Import,
            success: false,
            error: 'Validation failed'
          }
        ],
        warnings: [],
        errors: ['Some items failed to process'],
        targetAccountId: 123
      };

      (fetch as any).mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockResponse),
        text: vi.fn().mockResolvedValue(JSON.stringify(mockResponse))
      });

      const result = await apiClient.executeImportReview(mockExecutionRequest);
      expect(result.errors).toContain('Some items failed to process');
      expect(result.processedItems).toHaveLength(2);
    });
  });

  describe('API endpoint URLs', () => {
    test('uses correct case-sensitive endpoints', async () => {
      const mockRequest = {
        source: TransactionSource.CsvImport,
        accountId: 123
      };

      (fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({})
      });

      await apiClient.analyzeImportForReview(mockRequest);

      // Verify the endpoint uses PascalCase to match backend routing
      expect(fetch).toHaveBeenCalledWith(
        '/api/ImportReview/analyze',
        expect.any(Object)
      );
    });

    test('execution endpoint uses correct case', async () => {
      const mockRequest = {
        analysisId: 'test',
        accountId: 123,
        decisions: []
      };

      (fetch as any).mockResolvedValue({
        ok: true,
        json: async () => ({})
      });

      await apiClient.executeImportReview(mockRequest);

      expect(fetch).toHaveBeenCalledWith(
        '/api/ImportReview/execute',
        expect.any(Object)
      );
    });
  });

  describe('Error handling and edge cases', () => {
    test('handles malformed API responses gracefully', async () => {
      (fetch as any).mockResolvedValue({
        ok: true,
        json: async () => null
      });

      const mockRequest = {
        source: TransactionSource.CsvImport,
        accountId: 123
      };

      const result = await apiClient.analyzeImportForReview(mockRequest);
      expect(result).toBeNull();
    });

    test('preserves original error messages in failures', async () => {
      const errorMessage = 'Specific validation error';
      
      (fetch as any).mockResolvedValue({
        ok: false,
        status: 422,
        statusText: 'Unprocessable Entity',
        text: vi.fn().mockResolvedValue(errorMessage),
        json: vi.fn().mockRejectedValue(new Error('Not JSON'))
      });

      const mockRequest = {
        analysisId: 'test',
        accountId: 123,
        decisions: []
      };

      try {
        await apiClient.executeImportReview(mockRequest);
        fail('Expected error to be thrown');
      } catch (error) {
        expect(error).toBeInstanceOf(Error);
        expect((error as Error).message).toContain('422');
      }
    });

    test('handles timeout scenarios', async () => {
      const timeoutError = new Error('Request timeout');
      timeoutError.name = 'AbortError';
      
      (fetch as any).mockRejectedValue(timeoutError);

      const mockRequest = {
        source: TransactionSource.CsvImport,
        accountId: 123
      };

      await expect(apiClient.analyzeImportForReview(mockRequest))
        .rejects
        .toThrow('Request timeout');
    });
  });
});