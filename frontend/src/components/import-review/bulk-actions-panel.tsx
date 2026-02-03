'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { 
  CheckCircleIcon,
  XMarkIcon,
  EllipsisHorizontalIcon
} from '@heroicons/react/24/outline';
import { 
  ImportReviewItem, 
  ConflictResolution, 
  ConflictType 
} from '@/types/import-review';

interface BulkActionsPanelProps {
  items: ImportReviewItem[];
  onBulkAction: (itemIds: string[], action: ConflictResolution) => void;
}

export function BulkActionsPanel({ items, onBulkAction }: BulkActionsPanelProps) {
  const tImport = useTranslations('import');
  const [isOpen, setIsOpen] = useState(false);

  // Calculate what bulk actions are available based on the items
  const availableActions = {
    skipAll: items.length > 0,
    importAllClean: items.some(item => item.conflicts.length === 0),
    skipAllExactDuplicates: items.some(item => 
      item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
    ),
    skipAllPotentialDuplicates: items.some(item => 
      item.conflicts.some(c => c.type === ConflictType.PotentialDuplicate)
    ),
    mergeAllHighConfidence: items.some(item => 
      item.conflicts.some(c => c.confidenceScore > 0.9)
    ),
    importAllLowRisk: items.some(item => 
      item.conflicts.length === 0 || item.conflicts.every(c => c.confidenceScore < 0.5)
    ),
    autoResolveAll: items.some(item => item.reviewDecision === ConflictResolution.Pending)
  };

  const handleBulkAction = (action: ConflictResolution, filterFn?: (item: ImportReviewItem) => boolean) => {
    // Only apply bulk actions to items that are still pending
    const pendingItems = items.filter(item => item.reviewDecision === ConflictResolution.Pending);
    const targetItems = filterFn ? pendingItems.filter(filterFn) : pendingItems;
    const itemIds = targetItems.map(item => item.id);
    
    if (itemIds.length > 0) {
      onBulkAction(itemIds, action);
    }
    
    setIsOpen(false);
  };

  const handleAutoResolve = () => {
    // Smart auto-resolution based on conflict types and confidence scores
    items.forEach(item => {
      if (item.reviewDecision !== ConflictResolution.Pending) return;

      let suggestedAction = ConflictResolution.Import; // Default for clean items

      if (item.conflicts.length > 0) {
        const hasExactDuplicate = item.conflicts.some(c => c.type === ConflictType.ExactDuplicate);
        const hasHighConfidencePotential = item.conflicts.some(c => 
          c.type === ConflictType.PotentialDuplicate && c.confidenceScore > 0.85
        );
        const hasLowConfidenceConflicts = item.conflicts.every(c => c.confidenceScore < 0.5);

        if (hasExactDuplicate) {
          suggestedAction = ConflictResolution.Skip;
        } else if (hasHighConfidencePotential) {
          suggestedAction = ConflictResolution.MergeWithExisting;
        } else if (hasLowConfidenceConflicts) {
          suggestedAction = ConflictResolution.Import;
        } else {
          // Leave as pending for manual review
          return;
        }
      }

      onBulkAction([item.id], suggestedAction);
    });
    
    setIsOpen(false);
  };

  const getActionCount = (filterFn?: (item: ImportReviewItem) => boolean) => {
    // Only count items that haven't been decided yet (are still pending)
    const pendingItems = items.filter(item => item.reviewDecision === ConflictResolution.Pending);
    return filterFn ? pendingItems.filter(filterFn).length : pendingItems.length;
  };

  if (!Object.values(availableActions).some(Boolean)) {
    return null;
  }

  return (
    <div className="relative">
      <Button
        variant="secondary"
        size="sm"
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-1"
      >
        <EllipsisHorizontalIcon className="w-4 h-4" />
        <span className="text-xs">{tImport('review.bulk.label')}</span>
      </Button>

      {isOpen && (
        <>
          {/* Backdrop */}
          <div 
            className="fixed inset-0 z-40"
            onClick={() => setIsOpen(false)}
          />
          
          {/* Dropdown Menu */}
          <div className="absolute right-0 top-full mt-1 z-50 w-64 bg-white border border-gray-200 rounded-lg shadow-lg py-1">
            <div className="px-3 py-2 text-xs font-medium text-gray-500 border-b border-gray-100">
              {tImport('review.bulk.summary', { pending: getActionCount(), total: items.length })}
            </div>

            {/* Import All Clean */}
            {availableActions.importAllClean && (
              <button
                onClick={() => handleBulkAction(
                  ConflictResolution.Import,
                  (item) => item.conflicts.length === 0
                )}
                className="w-full px-3 py-2 text-left text-sm hover:bg-green-50 flex items-center gap-2 text-green-700"
              >
                <CheckCircleIcon className="w-4 h-4" />
                <span>{tImport('review.bulk.importAllClean', { count: getActionCount(item => item.conflicts.length === 0) })}</span>
              </button>
            )}

            {/* Skip All Exact Duplicates */}
            {availableActions.skipAllExactDuplicates && (
              <button
                onClick={() => handleBulkAction(
                  ConflictResolution.Skip,
                  (item) => item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
                )}
                className="w-full px-3 py-2 text-left text-sm hover:bg-red-50 flex items-center gap-2 text-red-700"
              >
                <XMarkIcon className="w-4 h-4" />
                <span>{tImport('review.bulk.skipExactDuplicates', {
                  count: getActionCount(item => item.conflicts.some(c => c.type === ConflictType.ExactDuplicate))
                })}</span>
              </button>
            )}

            {/* Import All Low Risk */}
            {availableActions.importAllLowRisk && (
              <button
                onClick={() => handleBulkAction(
                  ConflictResolution.Import,
                  (item) => item.conflicts.length === 0 || item.conflicts.every(c => c.confidenceScore < 0.5)
                )}
                className="w-full px-3 py-2 text-left text-sm hover:bg-blue-50 flex items-center gap-2 text-blue-700"
              >
                <CheckCircleIcon className="w-4 h-4" />
                <span>{tImport('review.bulk.importLowRisk', {
                  count: getActionCount(item => item.conflicts.length === 0 || item.conflicts.every(c => c.confidenceScore < 0.5))
                })}</span>
              </button>
            )}

            {/* Auto Resolve All */}
            {availableActions.autoResolveAll && (
              <button
                onClick={handleAutoResolve}
                className="w-full px-3 py-2 text-left text-sm hover:bg-purple-50 flex items-center gap-2 text-purple-700"
              >
                <EllipsisHorizontalIcon className="w-4 h-4" />
                <span>{tImport('review.bulk.autoResolveAll', {
                  count: getActionCount(item => item.reviewDecision === ConflictResolution.Pending)
                })}</span>
              </button>
            )}

            <div className="border-t border-gray-100 mt-1 pt-1">
              {/* Clear All Decisions - reset to pending */}
              {items.some(item => item.reviewDecision !== ConflictResolution.Pending) && (
                <button
                  onClick={() => {
                    const decidedItems = items.filter(item => item.reviewDecision !== ConflictResolution.Pending);
                    onBulkAction(decidedItems.map(item => item.id), ConflictResolution.Pending);
                    setIsOpen(false);
                  }}
                  className="w-full px-3 py-2 text-left text-sm hover:bg-yellow-50 flex items-center gap-2 text-yellow-700"
                >
                  <EllipsisHorizontalIcon className="w-4 h-4" />
                  <span>{tImport('review.bulk.clearDecisions', {
                    count: items.filter(item => item.reviewDecision !== ConflictResolution.Pending).length
                  })}</span>
                </button>
              )}
              
              {/* Skip All */}
              {availableActions.skipAll && getActionCount() > 0 && (
                <button
                  onClick={() => handleBulkAction(ConflictResolution.Skip)}
                  className="w-full px-3 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2 text-gray-700"
                >
                  <XMarkIcon className="w-4 h-4" />
                  <span>{tImport('review.bulk.skipAllPending', { count: getActionCount() })}</span>
                </button>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
