'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { 
  CheckCircleIcon,
  XMarkIcon,
  ArrowPathIcon,
  ExclamationTriangleIcon,
  ChevronDownIcon,
  ChevronUpIcon
} from '@heroicons/react/24/outline';
import { formatCurrency, formatDate } from '@/lib/utils';
import { ConfidenceIndicator } from '@/components/ui/confidence-indicator';
import { 
  ConflictResolutionCardProps, 
  ConflictResolution, 
  ConflictType,
  ConflictSeverity
} from '@/types/import-review';

export function ConflictResolutionCard({
  reviewItem,
  onDecisionChange,
  isReadOnly = false,
  showDetails = true
}: ConflictResolutionCardProps) {
  const tImport = useTranslations('import');
  const [isExpanded, setIsExpanded] = useState(false);
  const [userNotes, setUserNotes] = useState(reviewItem.userNotes || '');

  const { importCandidate, conflicts, reviewDecision } = reviewItem;
  const hasConflicts = conflicts.length > 0;
  const primaryConflict = conflicts[0]; // Highest priority conflict

  const handleDecisionChange = (decision: ConflictResolution) => {
    onDecisionChange(reviewItem.id, decision, userNotes);
  };

  const getConflictTypeColor = (type: ConflictType) => {
    switch (type) {
      case ConflictType.ExactDuplicate:
        return 'bg-red-100 text-red-800 border-red-200';
      case ConflictType.PotentialDuplicate:
        return 'bg-orange-100 text-orange-800 border-orange-200';
      case ConflictType.TransferConflict:
        return 'bg-purple-100 text-purple-800 border-purple-200';
      case ConflictType.ManualEntryConflict:
        return 'bg-blue-100 text-blue-800 border-blue-200';
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200';
    }
  };

  const getConflictTypeLabel = (type: ConflictType) => {
    switch (type) {
      case ConflictType.ExactDuplicate:
        return tImport('review.conflictTypes.exactDuplicate');
      case ConflictType.PotentialDuplicate:
        return tImport('review.conflictTypes.potentialDuplicate');
      case ConflictType.TransferConflict:
        return tImport('review.conflictTypes.transferConflict');
      case ConflictType.ManualEntryConflict:
        return tImport('review.conflictTypes.manualEntry');
      case ConflictType.AmountMismatch:
        return tImport('review.conflictTypes.amountMismatch');
      case ConflictType.DateMismatch:
        return tImport('review.conflictTypes.dateMismatch');
      case ConflictType.CategoryConflict:
        return tImport('review.conflictTypes.categoryConflict');
      default:
        return tImport('review.conflictTypes.unknown');
    }
  };


  const getDecisionIcon = (decision: ConflictResolution) => {
    switch (decision) {
      case ConflictResolution.Import:
        return <CheckCircleIcon className="w-4 h-4" />;
      case ConflictResolution.Skip:
        return <XMarkIcon className="w-4 h-4" />;
      case ConflictResolution.MergeWithExisting:
        return <ArrowPathIcon className="w-4 h-4" />;
      case ConflictResolution.ReplaceExisting:
        return <ArrowPathIcon className="w-4 h-4" />;
      default:
        return <ExclamationTriangleIcon className="w-4 h-4" />;
    }
  };

  const getDecisionLabel = (decision: ConflictResolution) => {
    switch (decision) {
      case ConflictResolution.Import:
        return tImport('review.decisions.import');
      case ConflictResolution.Skip:
        return tImport('review.decisions.skip');
      case ConflictResolution.MergeWithExisting:
        return tImport('review.decisions.merge');
      case ConflictResolution.ReplaceExisting:
        return tImport('review.decisions.replace');
      default:
        return tImport('review.decisions.pending');
    }
  };

  const getDecisionColor = (decision: ConflictResolution) => {
    switch (decision) {
      case ConflictResolution.Import:
        return 'bg-green-500 hover:bg-green-600';
      case ConflictResolution.Skip:
        return 'bg-red-500 hover:bg-red-600';
      case ConflictResolution.MergeWithExisting:
        return 'bg-blue-500 hover:bg-blue-600';
      case ConflictResolution.ReplaceExisting:
        return 'bg-purple-500 hover:bg-purple-600';
      default:
        return 'bg-yellow-500 hover:bg-yellow-600';
    }
  };


  return (
    <Card className={`transition-all duration-200 ${
      hasConflicts 
        ? 'border-l-4 border-l-orange-400 bg-orange-50/30' 
        : 'border-l-4 border-l-green-400 bg-green-50/30'
    }`}>
      <CardContent className="p-4">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          <div className="flex-1">
            <div className="flex items-center gap-3 mb-2">
              <div className="font-medium text-gray-900">
                {importCandidate.description}
              </div>
              {hasConflicts && (
                <Badge className={getConflictTypeColor(primaryConflict.type)}>
                  {getConflictTypeLabel(primaryConflict.type)}
                </Badge>
              )}
              {hasConflicts && (
                <ConfidenceIndicator confidence={primaryConflict.confidenceScore} />
              )}
            </div>
            
            <div className="flex items-center gap-4 text-sm text-gray-600">
              <span className={`font-medium ${
                importCandidate.amount > 0 ? 'text-success-600' : 'text-red-600'
              }`}>
                {formatCurrency(importCandidate.amount)}
              </span>
              <span>{formatDate(importCandidate.date)}</span>
              {importCandidate.referenceNumber && (
                <span className="text-xs bg-gray-100 px-2 py-1 rounded">
                  {tImport('review.referenceLabel', { reference: importCandidate.referenceNumber })}
                </span>
              )}
            </div>
          </div>

          {showDetails && hasConflicts && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsExpanded(!isExpanded)}
              className="ml-4"
            >
              {isExpanded ? (
                <ChevronUpIcon className="w-4 h-4" />
              ) : (
                <ChevronDownIcon className="w-4 h-4" />
              )}
            </Button>
          )}
        </div>

        {/* Conflict Details */}
        {hasConflicts && isExpanded && (
          <div className="mb-4 p-3 bg-white rounded-lg border">
            <h4 className="font-medium text-gray-900 mb-3">{tImport('review.conflictingTransaction')}</h4>
            
            <div className="space-y-3">
              {conflicts.map((conflict, index) => (
                <div key={index} className="flex justify-between items-start">
                  <div className="flex-1">
                    <div className="font-medium text-gray-700 mb-1">
                      {conflict.message}
                    </div>
                    {conflict.conflictingTransaction && (
                      <>
                        <div className="font-medium text-gray-700">
                          {conflict.conflictingTransaction.description}
                        </div>
                        <div className="flex items-center gap-4 text-sm text-gray-500 mt-1">
                          <span className={`font-medium ${
                            conflict.conflictingTransaction.amount >= 0 ? 'text-green-600' : 'text-red-600'
                          }`}>
                            {formatCurrency(conflict.conflictingTransaction.amount)}
                          </span>
                          <span>{formatDate(conflict.conflictingTransaction.transactionDate)}</span>
                          <span className="text-xs">
                            {tImport('review.transactionIdLabel', { id: conflict.conflictingTransaction.id })}
                          </span>
                        </div>
                      </>
                    )}
                    
                    <div className="flex flex-wrap gap-1 mt-2">
                      <Badge variant="outline" className={`text-xs ${
                        conflict.severity === ConflictSeverity.High || conflict.severity === ConflictSeverity.Critical 
                          ? 'border-red-300 text-red-700'
                          : conflict.severity === ConflictSeverity.Medium
                          ? 'border-orange-300 text-orange-700'
                          : 'border-gray-300 text-gray-600'
                      }`}>
                        {tImport('review.severityLabel', { severity: ConflictSeverity[conflict.severity] })}
                      </Badge>
                    </div>
                  </div>
                  
                  <div className="ml-4 text-right">
                    <div className="text-sm font-medium text-gray-700">
                      {tImport('review.confidenceLabel', { percent: Math.round(conflict.confidenceScore * 100) })}
                    </div>
                    <ConfidenceIndicator confidence={conflict.confidenceScore} size="sm" />
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Decision Buttons */}
        {!isReadOnly && (
          <div className="space-y-3">
            <div className="flex flex-wrap gap-2">
              <Button
                size="sm"
                variant={reviewDecision === ConflictResolution.Import ? "primary" : "secondary"}
                onClick={() => handleDecisionChange(ConflictResolution.Import)}
                className={reviewDecision === ConflictResolution.Import ? getDecisionColor(ConflictResolution.Import) : ''}
              >
                {getDecisionIcon(ConflictResolution.Import)}
                <span className="ml-1">{tImport('review.decisions.import')}</span>
              </Button>

              <Button
                size="sm"
                variant={reviewDecision === ConflictResolution.Skip ? "primary" : "secondary"}
                onClick={() => handleDecisionChange(ConflictResolution.Skip)}
                className={reviewDecision === ConflictResolution.Skip ? getDecisionColor(ConflictResolution.Skip) : ''}
              >
                {getDecisionIcon(ConflictResolution.Skip)}
                <span className="ml-1">{tImport('review.decisions.skip')}</span>
              </Button>

              {hasConflicts && (
                <>
                  <Button
                    size="sm"
                    variant={reviewDecision === ConflictResolution.MergeWithExisting ? "primary" : "secondary"}
                    onClick={() => handleDecisionChange(ConflictResolution.MergeWithExisting)}
                    className={reviewDecision === ConflictResolution.MergeWithExisting ? getDecisionColor(ConflictResolution.MergeWithExisting) : ''}
                  >
                    {getDecisionIcon(ConflictResolution.MergeWithExisting)}
                    <span className="ml-1">{tImport('review.decisions.merge')}</span>
                  </Button>

                  <Button
                    size="sm"
                    variant={reviewDecision === ConflictResolution.ReplaceExisting ? "primary" : "secondary"}
                    onClick={() => handleDecisionChange(ConflictResolution.ReplaceExisting)}
                    className={reviewDecision === ConflictResolution.ReplaceExisting ? getDecisionColor(ConflictResolution.ReplaceExisting) : ''}
                  >
                    {getDecisionIcon(ConflictResolution.ReplaceExisting)}
                    <span className="ml-1">{tImport('review.decisions.replace')}</span>
                  </Button>
                </>
              )}
            </div>

            {/* Notes Input */}
            <div>
              <textarea
                value={userNotes}
                onChange={(e) => setUserNotes(e.target.value)}
                placeholder={tImport('review.notesPlaceholder')}
                className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 resize-none"
                rows={2}
              />
            </div>
          </div>
        )}

        {/* Decision Status (Read-only mode) */}
        {isReadOnly && reviewDecision !== ConflictResolution.Pending && (
          <div className="flex items-center gap-2 mt-3">
            <div className={`flex items-center gap-1 px-2 py-1 rounded text-sm font-medium text-white ${getDecisionColor(reviewDecision)}`}>
              {getDecisionIcon(reviewDecision)}
              <span>{getDecisionLabel(reviewDecision)}</span>
            </div>
            {userNotes && (
              <span className="text-sm text-gray-600">&quot;{userNotes}&quot;</span>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
