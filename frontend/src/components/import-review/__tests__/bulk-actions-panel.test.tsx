import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, test, expect, beforeEach, vi } from 'vitest';
import { BulkActionsPanel } from '../bulk-actions-panel';
import { ImportReviewItem, ConflictType, ConflictResolution, TransactionSource } from '@/types/import-review';

// Mock data for testing
const mockItems: ImportReviewItem[] = [
  {
    id: '1',
    importCandidate: {
      amount: 100,
      date: '2024-01-01',
      description: 'Test transaction 1',
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
      description: 'Test transaction 2',
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
  },
  {
    id: '3',
    importCandidate: {
      amount: 150,
      date: '2024-01-03',
      description: 'Test transaction 3',
      source: TransactionSource.CsvImport,
      sourceRowIndex: 2,
      confidence: 70
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

describe('BulkActionsPanel', () => {
  const mockOnBulkAction = vi.fn();

  beforeEach(() => {
    mockOnBulkAction.mockClear();
  });

  test('renders bulk actions button', () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    expect(screen.getByText('Bulk')).toBeInTheDocument();
  });

  test('shows dropdown when bulk button is clicked', async () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    fireEvent.click(screen.getByText('Bulk'));
    
    await waitFor(() => {
      expect(screen.getByText(/Bulk Actions \(3 items\)/)).toBeInTheDocument();
    });
  });

  test('shows correct bulk action options based on items', async () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    fireEvent.click(screen.getByText('Bulk'));
    
    await waitFor(() => {
      // Should show "Import all clean" for items without conflicts
      expect(screen.getByText(/Import all clean/)).toBeInTheDocument();
      
      // Should show "Skip exact duplicates" for items with exact duplicate conflicts
      expect(screen.getByText(/Skip exact duplicates/)).toBeInTheDocument();
      
      // Should show "Auto resolve all" for pending items
      expect(screen.getByText(/Auto resolve all/)).toBeInTheDocument();
    });
  });

  test('executes import all clean action correctly', async () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    fireEvent.click(screen.getByText('Bulk'));
    
    await waitFor(() => {
      const importCleanButton = screen.getByText(/Import all clean/);
      fireEvent.click(importCleanButton);
    });
    
    expect(mockOnBulkAction).toHaveBeenCalledWith(['1'], ConflictResolution.Import);
  });

  test('executes skip exact duplicates action correctly', async () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    fireEvent.click(screen.getByText('Bulk'));
    
    await waitFor(() => {
      const skipDuplicatesButton = screen.getByText(/Skip exact duplicates/);
      fireEvent.click(skipDuplicatesButton);
    });
    
    expect(mockOnBulkAction).toHaveBeenCalledWith(['2'], ConflictResolution.Skip);
  });

  test('auto resolve works correctly with smart logic', async () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    fireEvent.click(screen.getByText('Bulk'));
    
    await waitFor(() => {
      const autoResolveButton = screen.getByText(/Auto resolve all/);
      fireEvent.click(autoResolveButton);
    });
    
    // Should make multiple calls for auto-resolution
    expect(mockOnBulkAction).toHaveBeenCalledTimes(3);
    
    // Clean item should be imported
    expect(mockOnBulkAction).toHaveBeenCalledWith(['1'], ConflictResolution.Import);
    
    // Exact duplicate should be skipped
    expect(mockOnBulkAction).toHaveBeenCalledWith(['2'], ConflictResolution.Skip);
    
    // High confidence potential duplicate should be merged
    expect(mockOnBulkAction).toHaveBeenCalledWith(['3'], ConflictResolution.MergeWithExisting);
  });

  test('does not render when no actions are available', () => {
    const emptyItems: ImportReviewItem[] = [];
    
    const { container } = render(
      <BulkActionsPanel items={emptyItems} onBulkAction={mockOnBulkAction} />
    );
    
    expect(container.firstChild).toBeNull();
  });

  test('closes dropdown when backdrop is clicked', async () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    fireEvent.click(screen.getByText('Bulk'));
    
    await waitFor(() => {
      expect(screen.getByText(/Bulk Actions/)).toBeInTheDocument();
    });
    
    // Click backdrop
    const backdrop = document.querySelector('.fixed.inset-0');
    if (backdrop) {
      fireEvent.click(backdrop);
    }
    
    await waitFor(() => {
      expect(screen.queryByText(/Bulk Actions/)).not.toBeInTheDocument();
    });
  });

  test('shows correct item counts for each action', async () => {
    render(<BulkActionsPanel items={mockItems} onBulkAction={mockOnBulkAction} />);
    
    fireEvent.click(screen.getByText('Bulk'));
    
    await waitFor(() => {
      expect(screen.getByText(/Import all clean \(1\)/)).toBeInTheDocument();
      expect(screen.getByText(/Skip exact duplicates \(1\)/)).toBeInTheDocument();
      expect(screen.getByText(/Auto resolve all \(3\)/)).toBeInTheDocument();
    });
  });
});