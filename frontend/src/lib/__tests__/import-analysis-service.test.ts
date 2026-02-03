import { ImportAnalysisService } from '../import-analysis-service';
import { TransactionSource, ConflictType, ConflictResolution } from '@/types/import-review';

describe('ImportAnalysisService', () => {
  describe('detectConflicts', () => {
    const mockExistingTransactions = [
      {
        id: 1,
        amount: 100.00,
        transactionDate: '2024-01-01',
        description: 'Coffee Shop',
        referenceId: 'REF123',
        externalReferenceId: 'EXT456',
        source: TransactionSource.Manual,
        status: 2,
        createdAt: '2024-01-01T10:00:00Z'
      },
      {
        id: 2,
        amount: 250.50,
        transactionDate: '2024-01-02',
        description: 'Grocery Store Purchase',
        referenceId: 'REF789',
        source: TransactionSource.CsvImport,
        status: 2,
        createdAt: '2024-01-02T09:00:00Z'
      }
    ];

    const mockImportCandidates = [
      {
        amount: 100.00,
        date: '2024-01-01',
        description: 'Coffee Shop',
        referenceNumber: 'REF123',
        externalId: 'EXT456',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 95
      },
      {
        amount: 250.75, // Slightly different amount
        date: '2024-01-02',
        description: 'Grocery Store',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 1,
        confidence: 85
      },
      {
        amount: 75.00,
        date: '2024-01-03',
        description: 'New Transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 2,
        confidence: 90
      }
    ];

    test('detects exact duplicates correctly', () => {
      const service = new ImportAnalysisService();
      const conflicts = service.detectConflicts(mockImportCandidates, mockExistingTransactions);

      const exactDuplicateItem = conflicts.find(item => 
        item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
      );

      expect(exactDuplicateItem).toBeDefined();
      expect(exactDuplicateItem?.importCandidate.amount).toBe(100.00);
      expect(exactDuplicateItem?.conflicts[0].confidenceScore).toBeGreaterThan(0.9);
    });

    test('detects potential duplicates based on similarity', () => {
      const service = new ImportAnalysisService();
      const conflicts = service.detectConflicts(mockImportCandidates, mockExistingTransactions);

      const potentialDuplicateItem = conflicts.find(item =>
        item.conflicts.some(c => c.type === ConflictType.PotentialDuplicate)
      );

      expect(potentialDuplicateItem).toBeDefined();
      expect(potentialDuplicateItem?.importCandidate.amount).toBe(250.75);
    });

    test('identifies clean imports with no conflicts', () => {
      const service = new ImportAnalysisService();
      const conflicts = service.detectConflicts(mockImportCandidates, mockExistingTransactions);

      const cleanImport = conflicts.find(item => 
        item.conflicts.length === 0
      );

      expect(cleanImport).toBeDefined();
      expect(cleanImport?.importCandidate.amount).toBe(75.00);
    });

    test('assigns correct conflict severity levels', () => {
      const service = new ImportAnalysisService();
      const conflicts = service.detectConflicts(mockImportCandidates, mockExistingTransactions);

      const exactDuplicate = conflicts.find(item =>
        item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
      );

      expect(exactDuplicate?.conflicts[0].severity).toBe('High');
    });

    test('generates appropriate conflict messages', () => {
      const service = new ImportAnalysisService();
      const conflicts = service.detectConflicts(mockImportCandidates, mockExistingTransactions);

      const conflictItem = conflicts.find(item => item.conflicts.length > 0);
      
      expect(conflictItem?.conflicts[0].message).toBeDefined();
      expect(conflictItem?.conflicts[0].message.length).toBeGreaterThan(0);
    });

    test('handles empty existing transactions list', () => {
      const service = new ImportAnalysisService();
      const conflicts = service.detectConflicts(mockImportCandidates, []);

      expect(conflicts).toHaveLength(3);
      expect(conflicts.every(item => item.conflicts.length === 0)).toBe(true);
    });

    test('handles empty import candidates list', () => {
      const service = new ImportAnalysisService();
      const conflicts = service.detectConflicts([], mockExistingTransactions);

      expect(conflicts).toHaveLength(0);
    });
  });

  describe('calculateConfidenceScore', () => {
    const service = new ImportAnalysisService();

    test('returns high confidence for exact matches', () => {
      const candidate = {
        amount: 100.00,
        date: '2024-01-01',
        description: 'Test Transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 95
      };

      const existing = {
        id: 1,
        amount: 100.00,
        transactionDate: '2024-01-01',
        description: 'Test Transaction',
        source: TransactionSource.Manual,
        status: 2,
        createdAt: '2024-01-01T10:00:00Z'
      };

      const confidence = service.calculateConfidenceScore(candidate, existing);
      expect(confidence).toBeGreaterThan(0.9);
    });

    test('returns lower confidence for partial matches', () => {
      const candidate = {
        amount: 100.50, // Slightly different
        date: '2024-01-01',
        description: 'Test Transaction Modified',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 85
      };

      const existing = {
        id: 1,
        amount: 100.00,
        transactionDate: '2024-01-01',
        description: 'Test Transaction',
        source: TransactionSource.Manual,
        status: 2,
        createdAt: '2024-01-01T10:00:00Z'
      };

      const confidence = service.calculateConfidenceScore(candidate, existing);
      expect(confidence).toBeLessThan(0.9);
      expect(confidence).toBeGreaterThan(0.5);
    });

    test('returns very low confidence for poor matches', () => {
      const candidate = {
        amount: 500.00, // Very different
        date: '2024-01-15', // Different date
        description: 'Completely Different Transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 60
      };

      const existing = {
        id: 1,
        amount: 100.00,
        transactionDate: '2024-01-01',
        description: 'Test Transaction',
        source: TransactionSource.Manual,
        status: 2,
        createdAt: '2024-01-01T10:00:00Z'
      };

      const confidence = service.calculateConfidenceScore(candidate, existing);
      expect(confidence).toBeLessThan(0.3);
    });
  });

  describe('generateAnalysisSummary', () => {
    const service = new ImportAnalysisService();

    test('generates correct summary statistics', () => {
      const reviewItems = [
        {
          id: '1',
          importCandidate: {
            amount: 100,
            date: '2024-01-01',
            description: 'Clean',
            source: TransactionSource.CsvImport,
            sourceRowIndex: 0,
            confidence: 95
          },
          conflicts: [],
          reviewDecision: ConflictResolution.Pending,
          isProcessed: false
        },
        {
          id: '2',
          importCandidate: {
            amount: 200,
            date: '2024-01-02',
            description: 'Duplicate',
            source: TransactionSource.CsvImport,
            sourceRowIndex: 1,
            confidence: 85
          },
          conflicts: [{
            type: ConflictType.ExactDuplicate,
            severity: 'High' as any,
            message: 'Exact duplicate',
            confidenceScore: 0.95
          }],
          reviewDecision: ConflictResolution.Pending,
          isProcessed: false
        }
      ];

      const summary = service.generateAnalysisSummary(reviewItems);

      expect(summary.totalCandidates).toBe(2);
      expect(summary.cleanImports).toBe(1);
      expect(summary.exactDuplicates).toBe(1);
      expect(summary.requiresReview).toBe(2);
    });

    test('handles empty review items', () => {
      const service = new ImportAnalysisService();
      const summary = service.generateAnalysisSummary([]);

      expect(summary.totalCandidates).toBe(0);
      expect(summary.cleanImports).toBe(0);
      expect(summary.exactDuplicates).toBe(0);
      expect(summary.requiresReview).toBe(0);
    });
  });

  describe('optimizeBulkActions', () => {
    const service = new ImportAnalysisService();

    test('suggests correct bulk actions for common scenarios', () => {
      const reviewItems = [
        // Clean items
        {
          id: '1',
          importCandidate: {
            amount: 100,
            date: '2024-01-01',
            description: 'Clean 1',
            source: TransactionSource.CsvImport,
            sourceRowIndex: 0,
            confidence: 95
          },
          conflicts: [],
          reviewDecision: ConflictResolution.Pending,
          isProcessed: false
        },
        // Exact duplicate
        {
          id: '2',
          importCandidate: {
            amount: 200,
            date: '2024-01-02',
            description: 'Duplicate',
            source: TransactionSource.CsvImport,
            sourceRowIndex: 1,
            confidence: 85
          },
          conflicts: [{
            type: ConflictType.ExactDuplicate,
            severity: 'High' as any,
            message: 'Exact duplicate',
            confidenceScore: 0.98
          }],
          reviewDecision: ConflictResolution.Pending,
          isProcessed: false
        }
      ];

      const suggestions = service.optimizeBulkActions(reviewItems);

      expect(suggestions.importAllClean).toBe(true);
      expect(suggestions.skipAllExactDuplicates).toBe(true);
    });

    test('handles items with mixed conflict types', () => {
      const reviewItems = [
        {
          id: '1',
          importCandidate: {
            amount: 100,
            date: '2024-01-01',
            description: 'Potential duplicate',
            source: TransactionSource.CsvImport,
            sourceRowIndex: 0,
            confidence: 75
          },
          conflicts: [{
            type: ConflictType.PotentialDuplicate,
            severity: 'Medium' as any,
            message: 'Potential duplicate found',
            confidenceScore: 0.75
          }],
          reviewDecision: ConflictResolution.Pending,
          isProcessed: false
        }
      ];

      const suggestions = service.optimizeBulkActions(reviewItems);

      expect(suggestions.importAllClean).toBe(false);
      expect(suggestions.skipAllExactDuplicates).toBe(false);
      expect(suggestions.hasLowConfidenceItems).toBe(true);
    });
  });

  describe('validateImportCandidate', () => {
    const service = new ImportAnalysisService();

    test('validates correct import candidate', () => {
      const candidate = {
        amount: 100.00,
        date: '2024-01-01',
        description: 'Valid transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 95
      };

      const validation = service.validateImportCandidate(candidate);
      expect(validation.isValid).toBe(true);
      expect(validation.errors).toHaveLength(0);
    });

    test('identifies missing required fields', () => {
      const candidate = {
        amount: 100.00,
        date: '', // Missing date
        description: 'Valid transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 95
      };

      const validation = service.validateImportCandidate(candidate);
      expect(validation.isValid).toBe(false);
      expect(validation.errors).toContain('Date is required');
    });

    test('validates amount constraints', () => {
      const candidate = {
        amount: NaN, // Invalid amount
        date: '2024-01-01',
        description: 'Valid transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 95
      };

      const validation = service.validateImportCandidate(candidate);
      expect(validation.isValid).toBe(false);
      expect(validation.errors.some(error => error.includes('amount'))).toBe(true);
    });

    test('validates date format', () => {
      const candidate = {
        amount: 100.00,
        date: 'invalid-date',
        description: 'Valid transaction',
        source: TransactionSource.CsvImport,
        sourceRowIndex: 0,
        confidence: 95
      };

      const validation = service.validateImportCandidate(candidate);
      expect(validation.isValid).toBe(false);
      expect(validation.errors.some(error => error.includes('date'))).toBe(true);
    });
  });
});