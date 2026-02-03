'use client';

import { useState, useMemo } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { 
  CheckCircleIcon, 
  ExclamationTriangleIcon,
  InformationCircleIcon,
  ArrowLeftIcon,
  DocumentCheckIcon,
  ChevronDownIcon,
  ChevronUpIcon
} from '@heroicons/react/24/outline';
import { ConflictResolutionCard } from './conflict-resolution-card';
import { BulkActionsPanel } from './bulk-actions-panel';
import { ImportSummaryStats } from './import-summary-stats';
import { 
  ImportReviewScreenProps, 
  ImportReviewItem, 
  ConflictResolution, 
  ConflictType,
  ImportExecutionRequest,
  ImportExecutionResult,
  ImportDecision
} from '@/types/import-review';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';

export function ImportReviewScreen({
  analysisResult,
  onImportComplete,
  onCancel,
  accountName,
  showBulkActions = true
}: ImportReviewScreenProps) {
  const tCommon = useTranslations('common');
  const tImport = useTranslations('import');
  const tToasts = useTranslations('toasts');
  const [reviewItems, setReviewItems] = useState<ImportReviewItem[]>(analysisResult.reviewItems);
  const [isImporting, setIsImporting] = useState(false);
  const [importCompleted, setImportCompleted] = useState(false);
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({
    conflicts: true,
    clean: false
  });

  // Group items by conflict type for organized display
  const groupedItems = useMemo(() => {
    const groups = {
      exactDuplicates: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
      ),
      potentialDuplicates: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.PotentialDuplicate) &&
        !item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
      ),
      transferConflicts: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.TransferConflict) &&
        !item.conflicts.some(c => [ConflictType.ExactDuplicate, ConflictType.PotentialDuplicate].includes(c.type))
      ),
      manualConflicts: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.ManualEntryConflict) &&
        !item.conflicts.some(c => [ConflictType.ExactDuplicate, ConflictType.PotentialDuplicate, ConflictType.TransferConflict].includes(c.type))
      ),
      cleanImports: reviewItems.filter(item => item.conflicts.length === 0)
    };


    return groups;
  }, [reviewItems]);

  // Calculate progress and status
  const progressStats = useMemo(() => {
    const total = reviewItems.length;
    const reviewed = reviewItems.filter(item => 
      item.reviewDecision !== ConflictResolution.Pending
    ).length;
    const toImport = reviewItems.filter(item => 
      item.reviewDecision === ConflictResolution.Import
    ).length;
    const toSkip = reviewItems.filter(item => 
      item.reviewDecision === ConflictResolution.Skip
    ).length;

    const stats = {
      total,
      reviewed,
      pending: total - reviewed,
      toImport,
      toSkip,
      progressPercent: total > 0 ? Math.round((reviewed / total) * 100) : 0
    };

    // Debug logging to help identify the issue
    console.log('🔢 Progress Stats:', stats);
    console.log('📋 Review Items Summary:', reviewItems.map(item => ({
      id: item.id.substring(0, 8),
      decision: item.reviewDecision,
      conflicts: item.conflicts.length
    })));

    return stats;
  }, [reviewItems]);

  const handleDecisionChange = (itemId: string, decision: ConflictResolution, notes?: string) => {
    setReviewItems(prev => prev.map(item => 
      item.id === itemId 
        ? { ...item, reviewDecision: decision, userNotes: notes, isProcessed: false }
        : item
    ));
  };

  const handleBulkAction = (itemIds: string[], action: ConflictResolution) => {
    setReviewItems(prev => prev.map(item => 
      itemIds.includes(item.id)
        ? { ...item, reviewDecision: action, isProcessed: false }
        : item
    ));
    
    const decisionLabel = (() => {
      switch (action) {
        case ConflictResolution.Import:
          return tImport('review.decisions.import');
        case ConflictResolution.Skip:
          return tImport('review.decisions.skip');
        case ConflictResolution.MergeWithExisting:
          return tImport('review.decisions.merge');
        case ConflictResolution.ReplaceExisting:
          return tImport('review.decisions.replace');
        case ConflictResolution.Pending:
        default:
          return tImport('review.decisions.pending');
      }
    })();
    toast.success(tToasts('importReviewBulkApplied', { action: decisionLabel, count: itemIds.length }));
  };

  const toggleSection = (section: string) => {
    setExpandedSections(prev => ({
      ...prev,
      [section]: !prev[section]
    }));
  };

  const handleExecuteImport = async () => {
    // Prevent multiple simultaneous executions and re-imports
    if (isImporting) {
      console.warn('Import already in progress, ignoring duplicate request');
      return;
    }
    
    if (importCompleted) {
      console.warn('Import already completed, ignoring duplicate request');
      toast.info(tToasts('importReviewAlreadyCompleted'));
      return;
    }
    
    setIsImporting(true);
    
    try {
      // Comprehensive validation before execution
      if (!analysisResult) {
        toast.error(tToasts('importReviewMissingAnalysis'));
        return;
      }

      if (!analysisResult.accountId) {
        toast.error(tToasts('importReviewInvalidAccount'));
        return;
      }

      const pendingItems = reviewItems.filter(item => item.reviewDecision === ConflictResolution.Pending);
      if (pendingItems.length > 0) {
        toast.error(tToasts('importReviewPendingItems', { count: pendingItems.length }));
        return;
      }

      const decisions: ImportDecision[] = reviewItems
        .filter(item => item.reviewDecision !== ConflictResolution.Pending)
        .map(item => {
          // Validate each decision
          if (!item.id) {
            throw new Error('Invalid review item: missing ID');
          }

          if (!Object.values(ConflictResolution).includes(item.reviewDecision)) {
            throw new Error(`Invalid decision: ${item.reviewDecision}`);
          }

          return {
            reviewItemId: item.id,
            decision: item.reviewDecision, // ConflictResolution enum value (now numeric)
            userNotes: item.userNotes || '',
            candidate: item.importCandidate // Include candidate data to avoid cache dependency
          };
        });

      if (decisions.length === 0) {
        toast.error(tToasts('importReviewNoDecisions'));
        return;
      }

      const request: ImportExecutionRequest = {
        analysisId: analysisResult.analysisId || analysisResult.analysisTimestamp || (analysisResult as any).analyzedAt,
        accountId: analysisResult.accountId,
        decisions
      };

      // Debug: Log decision candidates to see their transaction types
      console.log('🔍 Decision Candidates Debug:');
      decisions.forEach((decision, index) => {
        if (decision.candidate) {
          console.log(`Decision ${index}: amount=${decision.candidate.amount}, type=${decision.candidate.type}, decision=${decision.decision}`);
        }
      });

      // Debug: Log analysis result structure
      console.log('🔍 Analysis Result Debug:', {
        hasAnalysisId: !!analysisResult.analysisId,
        hasAnalysisTimestamp: !!analysisResult.analysisTimestamp,
        analysisId: analysisResult.analysisId,
        analysisTimestamp: analysisResult.analysisTimestamp,
        allKeys: Object.keys(analysisResult)
      });

      // Validate request structure
      if (!request.analysisId) {
        console.error('❌ Missing analysis ID in request:', request);
        toast.error(tToasts('importReviewMissingAnalysisId'));
        return;
      }

      console.log('Executing import with request:', request);

      // Use the correct API endpoint
      const result = await apiClient.executeImportReview(request);

      // Debug: Log the actual response structure
      console.log('🔍 Raw API Response:', result);
      console.log('🔍 Response keys:', Object.keys(result));

      // Validate response
      if (!result) {
        throw new Error('Empty response from server');
      }

      if (typeof result.success !== 'boolean') {
        console.warn('Legacy response format detected, attempting to process...');
      }

      // Process result based on structure - handle both new and legacy formats
      const resultAny = result as any;
      const success = resultAny.success ?? (resultAny.importedTransactionsCount !== undefined);
      const importedCount = resultAny.statistics?.importedCount || resultAny.importedTransactionsCount || resultAny.Statistics?.ImportedCount || 0;
      const skippedCount = resultAny.statistics?.skippedCount || resultAny.skippedTransactionsCount || resultAny.Statistics?.SkippedCount || 0;
      const mergedCount = resultAny.statistics?.mergedCount || resultAny.mergedTransactionsCount || resultAny.Statistics?.MergedCount || 0;
      const errorCount = resultAny.statistics?.errorCount || resultAny.errors?.length || resultAny.Statistics?.ErrorCount || 0;

      // Debug: Log the parsed values
      console.log('📊 Parsed Response Values:', {
        success,
        importedCount,
        skippedCount,
        mergedCount,
        errorCount,
        rawErrors: resultAny.errors,
        rawStatistics: resultAny.statistics
      });

      // Determine success based on multiple criteria
      const hasImportedTransactions = importedCount > 0 || mergedCount > 0;
      const hasPositiveResult = hasImportedTransactions || skippedCount > 0;
      const actualSuccess = success || hasPositiveResult;

      if (actualSuccess && errorCount === 0) {
        console.log('✅ Import completed successfully without errors');
        const totalProcessed = importedCount + mergedCount;
        const message = totalProcessed > 0 
          ? tToasts('importReviewCompleted', {
            imported: importedCount,
            merged: mergedCount,
            skipped: skippedCount
          })
          : tToasts('importReviewCompletedSkipped', { skipped: skippedCount });
        toast.success(message);
        setImportCompleted(true);
        
        // Create normalized result for completion screen
        const normalizedResult: ImportExecutionResult = {
          success: true,
          message: resultAny.message || tToasts('importReviewCompletedDefault'),
          importedTransactionsCount: importedCount,
          skippedTransactionsCount: skippedCount,
          duplicateTransactionsCount: 0, // Not used in our current flow
          mergedTransactionsCount: mergedCount,
          processedItems: resultAny.processedItems || [],
          warnings: resultAny.warnings || [],
          errors: resultAny.errors || [],
          targetAccountId: analysisResult.accountId,
          createdAccountId: resultAny.createdAccountId
        };
        onImportComplete(normalizedResult);
      } else if (actualSuccess && errorCount > 0) {
        console.log(`⚠️  Import completed with warnings: ${errorCount} errors, ${importedCount} imported, ${mergedCount} merged`);
        const totalProcessed = importedCount + mergedCount;
        toast.warning(tToasts('importReviewCompletedWithErrors', {
          errors: errorCount,
          total: totalProcessed
        }));
        setImportCompleted(true);
        
        // Create normalized result for completion screen
        const normalizedResult: ImportExecutionResult = {
          success: true,
          message: resultAny.message || tToasts('importReviewCompletedWithWarnings'),
          importedTransactionsCount: importedCount,
          skippedTransactionsCount: skippedCount,
          duplicateTransactionsCount: 0, // Not used in our current flow
          mergedTransactionsCount: mergedCount,
          processedItems: resultAny.processedItems || [],
          warnings: resultAny.warnings || [],
          errors: resultAny.errors || [],
          targetAccountId: analysisResult.accountId,
          createdAccountId: resultAny.createdAccountId
        };
        onImportComplete(normalizedResult);
      } else {
        console.log('❌ Import failed:', resultAny.message);
        throw new Error(resultAny.message || tToasts('importReviewFailedNoTransactions'));
      }

    } catch (error) {
      console.error('Import execution failed:', error);
      
      // Detailed error handling
      if (error instanceof Error) {
        if (error.message.includes('Network Error') || error.message.includes('fetch')) {
          toast.error(tToasts('networkErrorRetry'));
        } else if (error.message.includes('401') || error.message.includes('403')) {
          toast.error(tToasts('authErrorRefresh'));
        } else if (error.message.includes('400')) {
          toast.error(tToasts('importReviewInvalidData'));
        } else if (error.message.includes('500')) {
          toast.error(tToasts('serverErrorTryLater'));
        } else {
          toast.error(tToasts('importReviewFailedWithMessage', { message: error.message }));
        }
      } else {
        toast.error(tToasts('importReviewUnexpectedError'));
      }

      // Log detailed error information for debugging
      console.error('Detailed import error info:', {
        error,
        analysisResult,
        reviewItems: reviewItems.map(item => ({
          id: item.id,
          decision: item.reviewDecision,
          conflicts: item.conflicts.length
        })),
        requestWouldBe: {
          analysisId: analysisResult?.analysisId,
          accountId: analysisResult?.accountId,
          decisionsCount: reviewItems.filter(item => item.reviewDecision !== ConflictResolution.Pending).length
        }
      });
    } finally {
      setIsImporting(false);
    }
  };

  const renderConflictGroup = (
    title: string, 
    items: ImportReviewItem[], 
    icon: React.ReactNode,
    colorClass: string,
    description: string
  ) => {
    if (items.length === 0) return null;

    const sectionKey = title.toLowerCase().replace(/\s+/g, '_');
    const isExpanded = expandedSections[sectionKey] ?? true;


    return (
      <Card key={title} className="bg-white border-0 shadow-sm">
        <CardHeader 
          className="cursor-pointer"
          onClick={() => toggleSection(sectionKey)}
        >
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${colorClass}`}>
                {icon}
              </div>
              <div>
                <span className="text-lg font-semibold">{title}</span>
                <span className="ml-2 text-sm text-gray-500">({items.length})</span>
              </div>
            </div>
            <div className="flex items-center gap-2">
              {showBulkActions && items.length > 1 && (
                <BulkActionsPanel
                  items={items}
                  onBulkAction={handleBulkAction}
                />
              )}
              {isExpanded ? (
                <ChevronUpIcon className="w-5 h-5 text-gray-400" />
              ) : (
                <ChevronDownIcon className="w-5 h-5 text-gray-400" />
              )}
            </div>
          </CardTitle>
          <p className="text-sm text-gray-600">{description}</p>
        </CardHeader>
        
        {isExpanded && (
          <CardContent className="space-y-4">
            {items.map(item => (
              <ConflictResolutionCard
                key={item.id}
                reviewItem={item}
                onDecisionChange={handleDecisionChange}
                showDetails={true}
              />
            ))}
          </CardContent>
        )}
      </Card>
    );
  };

  const canProceed = progressStats.pending === 0 && !importCompleted;

  return (
    <div className="max-w-6xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button 
            variant="ghost" 
            size="sm" 
            onClick={onCancel}
            className="flex items-center gap-2"
          >
            <ArrowLeftIcon className="w-4 h-4" />
            {tCommon('back')}
          </Button>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">{tImport('review.title')}</h1>
            <p className="text-gray-600">
              {accountName ? tImport('review.subtitleImporting', { accountName }) : tImport('review.subtitleReview')}
            </p>
          </div>
        </div>
      </div>

      {/* Workflow Guidance */}
      {progressStats.total > 0 && progressStats.pending > 0 && (
        <Card className="bg-gradient-to-r from-blue-50 to-purple-50 border-blue-200">
          <CardContent className="p-4">
            <div className="flex items-start gap-3">
              <InformationCircleIcon className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
              <div className="text-sm">
                <p className="font-medium text-gray-900 mb-1">{tImport('review.guidance.title')}</p>
                <p className="text-gray-600 mb-2">
                  {tImport('review.guidance.body')}
                </p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs text-gray-500">
                  <div>• {tImport('review.guidance.individual')}</div>
                  <div>• {tImport('review.guidance.bulk')}</div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Progress and Summary */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <ImportSummaryStats 
            summary={analysisResult?.summary}
            progress={progressStats}
          />
        </div>
        <div className="lg:col-span-1">
          <Card className="bg-gradient-to-br from-blue-50 to-indigo-50 border-blue-200">
            <CardContent className="p-6 text-center">
              <div className="w-16 h-16 bg-blue-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <DocumentCheckIcon className="w-8 h-8 text-blue-600" />
              </div>
              <h3 className="font-semibold text-gray-900 mb-2">
                {tImport('review.progress.reviewedPercent', { percent: progressStats.progressPercent })}
              </h3>
              <div className="text-sm text-gray-600 mb-4 space-y-1">
                <p>{tImport('review.progress.itemsRemaining', { count: progressStats.pending })}</p>
                {progressStats.toImport > 0 && (
                  <p className="text-green-600 font-medium">
                    {tImport('review.progress.readyToImport', { count: progressStats.toImport })}
                  </p>
                )}
                {progressStats.toSkip > 0 && (
                  <p className="text-gray-500">{tImport('review.progress.willBeSkipped', { count: progressStats.toSkip })}</p>
                )}
              </div>
              <Button
                onClick={handleExecuteImport}
                disabled={!canProceed || isImporting}
                className={`w-full ${importCompleted ? 'bg-green-600 hover:bg-green-700' : progressStats.toImport > 0 ? 'bg-blue-600 hover:bg-blue-700' : 'bg-gray-600 hover:bg-gray-700'}`}
              >
                {isImporting ? (
                  <>
                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                    {progressStats.toImport > 0 ? tImport('review.actions.importing') : tImport('review.actions.completingReview')}
                  </>
                ) : importCompleted ? (
                  <>
                    <CheckCircleIcon className="w-4 h-4 mr-2" />
                    {progressStats.toImport > 0 ? tImport('review.actions.importCompleted') : tImport('review.actions.reviewCompleted')}
                  </>
                ) : progressStats.toImport > 0 ? (
                  <>
                    <CheckCircleIcon className="w-4 h-4 mr-2" />
                    {tImport('review.actions.executeImport', { count: progressStats.toImport })}
                  </>
                ) : (
                  <>
                    <CheckCircleIcon className="w-4 h-4 mr-2" />
                    {tImport('review.actions.completeReview')}
                  </>
                )}
              </Button>
              {!canProceed && !importCompleted && (
                <p className="text-xs text-orange-600 mt-2">
                  {progressStats.toImport > 0 
                    ? tImport('review.actions.reviewConflictsBeforeImport')
                    : tImport('review.actions.reviewConflictsBeforeComplete')
                  }
                </p>
              )}
              {importCompleted && (
                <p className="text-xs text-green-600 mt-2">
                  {progressStats.toImport > 0 
                    ? tImport('review.actions.importCompletedSuccess')
                    : tImport('review.actions.reviewCompletedSuccess')
                  }
                </p>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Conflict Groups */}
      <div className="space-y-4">
        {/* Exact Duplicates */}
        {renderConflictGroup(
          tImport('review.groups.exact.title'),
          groupedItems.exactDuplicates,
          <ExclamationTriangleIcon className="w-5 h-5 text-red-600" />,
          'bg-red-100',
          tImport('review.groups.exact.description')
        )}

        {/* Potential Duplicates */}
        {renderConflictGroup(
          tImport('review.groups.potential.title'),
          groupedItems.potentialDuplicates,
          <ExclamationTriangleIcon className="w-5 h-5 text-orange-600" />,
          'bg-orange-100',
          tImport('review.groups.potential.description')
        )}

        {/* Transfer Conflicts */}
        {renderConflictGroup(
          tImport('review.groups.transfer.title'),
          groupedItems.transferConflicts,
          <ExclamationTriangleIcon className="w-5 h-5 text-purple-600" />,
          'bg-purple-100',
          tImport('review.groups.transfer.description')
        )}

        {/* Manual Entry Conflicts */}
        {renderConflictGroup(
          tImport('review.groups.manual.title'),
          groupedItems.manualConflicts,
          <ExclamationTriangleIcon className="w-5 h-5 text-blue-600" />,
          'bg-blue-100',
          tImport('review.groups.manual.description')
        )}

        {/* Clean Imports */}
        {renderConflictGroup(
          tImport('review.groups.clean.title'),
          groupedItems.cleanImports,
          <CheckCircleIcon className="w-5 h-5 text-green-600" />,
          'bg-green-100',
          tImport('review.groups.clean.description')
        )}
      </div>

      {/* Warnings and Errors */}
      {(analysisResult.warnings.length > 0 || analysisResult.errors.length > 0) && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-yellow-800">
              <InformationCircleIcon className="w-5 h-5" />
              {tImport('review.analysisNotes')}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {analysisResult.warnings.map((warning, index) => (
              <p key={index} className="text-sm text-yellow-700">
                ⚠️ {warning}
              </p>
            ))}
            {analysisResult.errors.map((error, index) => (
              <p key={index} className="text-sm text-red-700">
                ❌ {error}
              </p>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
