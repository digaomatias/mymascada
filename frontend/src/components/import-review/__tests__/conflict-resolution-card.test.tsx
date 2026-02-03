import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { ConflictResolutionCard } from '../conflict-resolution-card';
import { ImportReviewItem, ConflictType, ConflictResolution, TransactionSource } from '@/types/import-review';

// Mock the formatCurrency and formatDate functions
vi.mock('@/lib/utils', () => ({
  formatCurrency: (amount: number) => `$${amount.toFixed(2)}`,
  formatDate: (date: string) => new Date(date).toLocaleDateString()
}));

const mockReviewItem: ImportReviewItem = {
  id: 'test-item-1',
  importCandidate: {
    amount: 250.50,
    date: '2024-01-15',
    description: 'Coffee Shop Purchase',
    referenceNumber: 'REF123',
    source: TransactionSource.CsvImport,
    sourceRowIndex: 0,
    confidence: 85
  },
  conflicts: [{
    type: ConflictType.PotentialDuplicate,
    severity: 'Medium' as any,
    message: 'Similar transaction found with 85% confidence',
    confidenceScore: 0.85,
    conflictingTransaction: {
      id: 456,
      amount: 250.50,
      transactionDate: '2024-01-14',
      description: 'Coffee Shop',
      source: TransactionSource.Manual,
      status: 2,
      createdAt: '2024-01-14T10:00:00Z'
    }
  }],
  reviewDecision: ConflictResolution.Pending,
  isProcessed: false
};

const cleanReviewItem: ImportReviewItem = {
  id: 'test-item-2',
  importCandidate: {
    amount: 100.00,
    date: '2024-01-16',
    description: 'Grocery Store',
    source: TransactionSource.CsvImport,
    sourceRowIndex: 1,
    confidence: 95
  },
  conflicts: [],
  reviewDecision: ConflictResolution.Pending,
  isProcessed: false
};

describe('ConflictResolutionCard', () => {
  const mockOnDecisionChange = vi.fn();

  beforeEach(() => {
    mockOnDecisionChange.mockClear();
  });

  test('renders transaction details correctly', () => {
    render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    expect(screen.getByText('Coffee Shop Purchase')).toBeInTheDocument();
    expect(screen.getByText('$250.50')).toBeInTheDocument();
    expect(screen.getByText('Ref: REF123')).toBeInTheDocument();
  });

  test('shows conflict badge for items with conflicts', () => {
    render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    expect(screen.getByText('Potential Duplicate')).toBeInTheDocument();
  });

  test('does not show conflict badge for clean items', () => {
    render(
      <ConflictResolutionCard
        reviewItem={cleanReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    expect(screen.queryByText('Potential Duplicate')).not.toBeInTheDocument();
  });

  test('shows decision buttons when not readonly', () => {
    render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    expect(screen.getByText('Import')).toBeInTheDocument();
    expect(screen.getByText('Skip')).toBeInTheDocument();
    expect(screen.getByText('Merge')).toBeInTheDocument();
    expect(screen.getByText('Replace')).toBeInTheDocument();
  });

  test('handles decision change correctly', async () => {
    render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    fireEvent.click(screen.getByText('Import'));

    await waitFor(() => {
      expect(mockOnDecisionChange).toHaveBeenCalledWith(
        'test-item-1',
        ConflictResolution.Import,
        ''
      );
    });
  });

  test('includes user notes in decision change', async () => {
    render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    // Add some notes
    const notesTextarea = screen.getByPlaceholderText('Add notes about this decision...');
    fireEvent.change(notesTextarea, { target: { value: 'Test notes' } });

    fireEvent.click(screen.getByText('Skip'));

    await waitFor(() => {
      expect(mockOnDecisionChange).toHaveBeenCalledWith(
        'test-item-1',
        ConflictResolution.Skip,
        'Test notes'
      );
    });
  });

  test('expands conflict details when expand button is clicked', async () => {
    render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    // Click expand button
    const expandButton = screen.getByRole('button');
    fireEvent.click(expandButton);

    await waitFor(() => {
      expect(screen.getByText('Conflicting Transaction')).toBeInTheDocument();
      expect(screen.getByText('Similar transaction found with 85% confidence')).toBeInTheDocument();
      expect(screen.getByText('Coffee Shop')).toBeInTheDocument();
    });
  });

  test('shows confidence indicator for conflicts', () => {
    render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    // Expand to see confidence details
    const expandButton = screen.getByRole('button');
    fireEvent.click(expandButton);

    expect(screen.getByText('Confidence: 85%')).toBeInTheDocument();
  });

  test('applies correct styling for conflict vs clean items', () => {
    const { rerender } = render(
      <ConflictResolutionCard
        reviewItem={mockReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    // Check conflict styling
    const conflictCard = screen.getByRole('generic').closest('.border-l-orange-400');
    expect(conflictCard).toBeInTheDocument();

    // Switch to clean item
    rerender(
      <ConflictResolutionCard
        reviewItem={cleanReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    // Check clean styling
    const cleanCard = screen.getByRole('generic').closest('.border-l-green-400');
    expect(cleanCard).toBeInTheDocument();
  });

  test('shows only basic buttons for clean items', () => {
    render(
      <ConflictResolutionCard
        reviewItem={cleanReviewItem}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    expect(screen.getByText('Import')).toBeInTheDocument();
    expect(screen.getByText('Skip')).toBeInTheDocument();
    
    // Should not show merge/replace options for items without conflicts
    expect(screen.queryByText('Merge')).not.toBeInTheDocument();
    expect(screen.queryByText('Replace')).not.toBeInTheDocument();
  });

  test('readonly mode shows decision status without buttons', () => {
    const resolvedItem = {
      ...mockReviewItem,
      reviewDecision: ConflictResolution.Import,
      userNotes: 'Reviewed and approved'
    };

    render(
      <ConflictResolutionCard
        reviewItem={resolvedItem}
        onDecisionChange={mockOnDecisionChange}
        isReadOnly={true}
      />
    );

    expect(screen.queryByRole('button', { name: 'Import' })).not.toBeInTheDocument();
    expect(screen.getByText('Import')).toBeInTheDocument(); // Shows as status
    expect(screen.getByText('"Reviewed and approved"')).toBeInTheDocument();
  });

  test('handles missing reference number gracefully', () => {
    const itemWithoutRef = {
      ...mockReviewItem,
      importCandidate: {
        ...mockReviewItem.importCandidate,
        referenceNumber: undefined
      }
    };

    render(
      <ConflictResolutionCard
        reviewItem={itemWithoutRef}
        onDecisionChange={mockOnDecisionChange}
      />
    );

    expect(screen.queryByText(/Ref:/)).not.toBeInTheDocument();
  });
});