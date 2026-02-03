'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { BaseModal } from './base-modal';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import { apiClient } from '@/lib/api-client';
import { formatCurrency, formatDate } from '@/lib/utils';
import { toast } from 'sonner';
import {
  DocumentDuplicateIcon,
  MagnifyingGlassIcon,
  AdjustmentsHorizontalIcon,
  ExclamationTriangleIcon,
  CheckCircleIcon,
  TrashIcon,
  EyeIcon,
  CalendarIcon,
  WalletIcon,
  TagIcon,
  XMarkIcon,
  ChevronDownIcon,
  ChevronUpIcon
} from '@heroicons/react/24/outline';
import type { DuplicateTransactionsResponse, DuplicateDetectionParams, DuplicateGroupDto } from '@/types/duplicates';
import { useTranslations } from 'next-intl';

interface DuplicatesModalProps {
  isOpen: boolean;
  onClose: () => void;
  onRefresh?: () => void;
}

const getConfidenceColor = (confidence: number) => {
  if (confidence >= 0.8) return 'text-green-600 bg-green-50 border-green-200';
  if (confidence >= 0.6) return 'text-yellow-600 bg-yellow-50 border-yellow-200';
  return 'text-orange-600 bg-orange-50 border-orange-200';
};

export function DuplicatesModal({ isOpen, onClose, onRefresh }: DuplicatesModalProps) {
  const t = useTranslations('transactions');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [loading, setLoading] = useState(false);
  const [duplicates, setDuplicates] = useState<DuplicateTransactionsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());
  const [selectedGroups, setSelectedGroups] = useState<Set<string>>(new Set());
  const [resolutionPending, setResolutionPending] = useState(false);
  
  // Detection parameters
  const [params, setParams] = useState<DuplicateDetectionParams>({
    amountTolerance: 0.01,
    dateToleranceDays: 1,
    includeReviewed: false,
    sameAccountOnly: false,
    minConfidence: 0.5
  });

  const fetchDuplicates = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      const response = await apiClient.getDuplicateTransactions(params) as DuplicateTransactionsResponse;
      setDuplicates(response);
    } catch (err) {
      console.error('Failed to fetch duplicates:', err);
      setError(t('duplicates.fetchFailed'));
      toast.error(tToasts('duplicatesFetchFailed'));
    } finally {
      setLoading(false);
    }
  }, [params]);

  useEffect(() => {
    if (isOpen) {
      fetchDuplicates();
    }
  }, [isOpen, fetchDuplicates]);

  const handleParamChange = (key: keyof DuplicateDetectionParams, value: unknown) => {
    setParams(prev => ({ ...prev, [key]: value }));
  };

  const runDetection = () => {
    fetchDuplicates();
  };

  const toggleGroupExpansion = (groupId: string) => {
    setExpandedGroups(prev => {
      const newSet = new Set(prev);
      if (newSet.has(groupId)) {
        newSet.delete(groupId);
      } else {
        newSet.add(groupId);
      }
      return newSet;
    });
  };

  const handleDeleteTransaction = async (transactionId: number) => {
    try {
      await apiClient.deleteTransaction(transactionId);
      toast.success(tToasts('transactionDeleted'));
      // Refresh the duplicates list
      fetchDuplicates();
      // Refresh the main transactions list
      onRefresh?.();
    } catch (err) {
      console.error('Failed to delete transaction:', err);
      toast.error(t('failedToDeleteTransaction'));
    }
  };

  const dismissDuplicateGroup = async (groupId: string) => {
    if (!duplicates) return;
    
    const group = duplicates.duplicateGroups.find(g => g.id === groupId);
    if (!group) return;
    
    try {
      // Mark this group as not duplicate via the backend API
      const resolution = {
        groupId,
        transactionIdsToKeep: group.transactions.map(t => t.id),
        transactionIdsToDelete: [],
        markAsNotDuplicate: true,
        notes: 'Marked as not duplicate via individual dismissal'
      };
      
      await apiClient.resolveDuplicates([resolution]);
      
      // Remove from UI after successful backend call
      setDuplicates({
        ...duplicates,
        duplicateGroups: duplicates.duplicateGroups.filter(g => g.id !== groupId),
        totalGroups: duplicates.totalGroups - 1
      });
      
      toast.success(t('duplicates.markedNotDuplicate'));
    } catch (err) {
      console.error('Failed to dismiss duplicate group:', err);
      toast.error(tToasts('duplicatesDismissFailed'));
    }
  };

  const toggleGroupSelection = (groupId: string) => {
    setSelectedGroups(prev => {
      const newSet = new Set(prev);
      if (newSet.has(groupId)) {
        newSet.delete(groupId);
      } else {
        newSet.add(groupId);
      }
      return newSet;
    });
  };

  const selectAllGroups = () => {
    if (!duplicates) return;
    setSelectedGroups(new Set(duplicates.duplicateGroups.map(g => g.id)));
  };

  const clearGroupSelection = () => {
    setSelectedGroups(new Set());
  };

  const resolveSelectedGroups = async (action: 'keep-newest' | 'keep-oldest' | 'mark-not-duplicate') => {
    if (!duplicates || selectedGroups.size === 0) return;

    setResolutionPending(true);
    
    try {
      const resolutions = Array.from(selectedGroups).map(groupId => {
        const group = duplicates.duplicateGroups.find(g => g.id === groupId);
        if (!group) return null;

        if (action === 'mark-not-duplicate') {
          return {
            groupId,
            transactionIdsToKeep: group.transactions.map(t => t.id),
            transactionIdsToDelete: [],
            markAsNotDuplicate: true,
            notes: 'Marked as not duplicate via bulk action'
          };
        }

        // Sort transactions by date for keep-newest/keep-oldest
        const sortedTransactions = [...group.transactions].sort((a, b) => {
          const dateA = new Date(a.transactionDate).getTime();
          const dateB = new Date(b.transactionDate).getTime();
          return action === 'keep-newest' ? dateB - dateA : dateA - dateB;
        });

        const transactionToKeep = sortedTransactions[0];
        const transactionsToDelete = sortedTransactions.slice(1);

        return {
          groupId,
          transactionIdsToKeep: [transactionToKeep.id],
          transactionIdsToDelete: transactionsToDelete.map(t => t.id),
          markAsNotDuplicate: false,
          notes: `Bulk resolution: kept ${action === 'keep-newest' ? 'newest' : 'oldest'} transaction`
        };
      }).filter((resolution): resolution is NonNullable<typeof resolution> => resolution !== null);

      const response = await apiClient.resolveDuplicates(resolutions) as {
        success: boolean;
        message: string;
        transactionsDeleted: number;
        transactionsKept: number;
        errors?: string[];
      };

      if (response.success) {
        toast.success(t('duplicates.resolvedSummary', { groups: selectedGroups.size, deleted: response.transactionsDeleted }));
        
        // Remove resolved groups from the display
        setDuplicates({
          ...duplicates,
          duplicateGroups: duplicates.duplicateGroups.filter(g => !selectedGroups.has(g.id)),
          totalGroups: duplicates.totalGroups - selectedGroups.size
        });
        
        // Clear selection
        setSelectedGroups(new Set());
        
        // Refresh the main transactions list
        onRefresh?.();
      } else {
        toast.error(t('duplicates.resolveErrors', { message: response.message }));
        
        if (response.errors && response.errors.length > 0) {
          response.errors.forEach(error => {
            toast.error(error, { duration: 6000 });
          });
        }
      }
    } catch (err) {
      console.error('Failed to resolve duplicates:', err);
      toast.error(tToasts('duplicatesResolveFailed'));
    } finally {
      setResolutionPending(false);
    }
  };

  const renderGroup = (group: DuplicateGroupDto) => {
    const isExpanded = expandedGroups.has(group.id);
    const isSelected = selectedGroups.has(group.id);
    const confidencePercent = Math.round(group.highestConfidence * 100);
    const confidenceLabel = group.highestConfidence >= 0.8
      ? t('duplicates.confidence.high')
      : group.highestConfidence >= 0.6
        ? t('duplicates.confidence.medium')
        : t('duplicates.confidence.low');
    
    return (
      <Card key={group.id} className={`border shadow-sm ${isSelected ? 'border-blue-500 bg-blue-50' : 'border-gray-200'}`}>
        <CardContent className="p-4">
          {/* Group Header */}
          <div className="flex items-start justify-between mb-3">
            <div className="flex items-center gap-3 flex-1 min-w-0">
              <Checkbox
                checked={isSelected}
                onCheckedChange={() => toggleGroupSelection(group.id)}
                className="flex-shrink-0"
              />
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-2">
                  <DocumentDuplicateIcon className="w-5 h-5 text-orange-500" />
                  <h3 className="font-medium text-gray-900 truncate">
                    {group.description}
                  </h3>
                  <Badge 
                    variant="outline" 
                    className={`${getConfidenceColor(group.highestConfidence)} border`}
                  >
                    {confidenceLabel} ({confidencePercent}%)
                  </Badge>
                </div>
                <div className="flex items-center gap-4 text-sm text-gray-600">
                  <span className="flex items-center gap-1">
                    <span className="font-medium">{formatCurrency(group.totalAmount)}</span>
                  </span>
                  <span className="flex items-center gap-1">
                    <CalendarIcon className="w-4 h-4" />
                    {group.dateRange}
                  </span>
                  <span>{t('duplicates.transactionCount', { count: group.transactions.length })}</span>
                </div>
              </div>
            </div>
            <div className="flex items-center gap-2 ml-4">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => toggleGroupExpansion(group.id)}
                className="flex items-center gap-1"
              >
                {isExpanded ? (
                  <>
                    <ChevronUpIcon className="w-4 h-4" />
                    <span className="hidden sm:inline">{tCommon('collapse')}</span>
                  </>
                ) : (
                  <>
                    <ChevronDownIcon className="w-4 h-4" />
                    <span className="hidden sm:inline">{tCommon('expand')}</span>
                  </>
                )}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => dismissDuplicateGroup(group.id)}
                className="flex items-center gap-1"
              >
                <XMarkIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('duplicates.notDuplicate')}</span>
              </Button>
            </div>
          </div>

          {/* Expanded Group Content */}
          {isExpanded && (
            <div className="space-y-3 pt-3 border-t border-gray-100">
              {group.transactions.map((transaction) => (
                <div key={transaction.id} className="bg-gray-50 rounded-lg p-3">
                  <div className="flex items-start justify-between">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-2">
                        <span className="text-sm font-medium text-gray-900">
                          #{transaction.id}
                        </span>
                        {transaction.userDescription && transaction.userDescription !== transaction.description && (
                          <span className="text-sm text-gray-600">
                            ({transaction.userDescription})
                          </span>
                        )}
                        <Badge variant={transaction.isReviewed ? 'default' : 'secondary'}>
                          {transaction.isReviewed ? t('reviewed') : t('unreviewed')}
                        </Badge>
                      </div>
                      
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-sm">
                        <div className="flex items-center gap-2">
                          <span className="font-medium text-lg">
                            {formatCurrency(Math.abs(transaction.amount))}
                          </span>
                          <Badge variant={transaction.amount > 0 ? 'default' : 'secondary'}>
                            {transaction.amount > 0 ? t('income') : t('expense')}
                          </Badge>
                        </div>
                        
                        <div className="flex items-center gap-1 text-gray-600">
                          <CalendarIcon className="w-4 h-4" />
                          {formatDate(transaction.transactionDate)}
                        </div>
                        
                        <div className="flex items-center gap-1 text-gray-600">
                          <WalletIcon className="w-4 h-4" />
                          {transaction.accountName}
                        </div>
                        
                        {transaction.categoryName && (
                          <div className="flex items-center gap-1 text-gray-600">
                            <TagIcon className="w-4 h-4" />
                            {transaction.categoryName}
                          </div>
                        )}
                      </div>
                    </div>
                    
                    <div className="flex items-center gap-2 ml-4">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => window.open(`/transactions/${transaction.id}`, '_blank')}
                        className="flex items-center gap-1"
                      >
                        <EyeIcon className="w-4 h-4" />
                        <span className="hidden sm:inline">{tCommon('view')}</span>
                      </Button>
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => handleDeleteTransaction(transaction.id)}
                        className="flex items-center gap-1 text-red-600 hover:text-red-700 hover:bg-red-50"
                      >
                        <TrashIcon className="w-4 h-4" />
                        <span className="hidden sm:inline">{tCommon('delete')}</span>
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    );
  };

  return (
    <BaseModal 
      isOpen={isOpen} 
      onClose={onClose} 
      title={t('findDuplicates')}
      size="xl"
    >
      <div className="space-y-6">
        {/* Detection Parameters */}
        <Card className="bg-blue-50 border-blue-200">
          <CardContent className="p-4">
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2">
                <AdjustmentsHorizontalIcon className="w-5 h-5 text-blue-600" />
                <h3 className="font-medium text-blue-900">{t('duplicates.detectionSettings')}</h3>
              </div>
              <div className="flex items-center gap-2">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowAdvanced(!showAdvanced)}
                  className="text-blue-600 hover:text-blue-700"
                >
                  {showAdvanced ? tCommon('simple') : tCommon('advanced')}
                </Button>
                <Button
                  onClick={runDetection}
                  disabled={loading}
                  className="bg-blue-600 hover:bg-blue-700"
                  size="sm"
                >
                  <MagnifyingGlassIcon className="w-4 h-4 mr-2" />
                  {loading ? t('duplicates.scanning') : t('duplicates.scanForDuplicates')}
                </Button>
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              <div>
                <Label htmlFor="minConfidence" className="text-sm font-medium text-gray-700">
                  {t('duplicates.minimumConfidence')}
                </Label>
                <Input
                  id="minConfidence"
                  type="number"
                  min="0"
                  max="1"
                  step="0.1"
                  value={params.minConfidence}
                  onChange={(e) => handleParamChange('minConfidence', parseFloat(e.target.value))}
                  className="mt-1"
                />
              </div>

              {showAdvanced && (
                <>
                  <div>
                    <Label htmlFor="amountTolerance" className="text-sm font-medium text-gray-700">
                      {t('duplicates.amountTolerance')}
                    </Label>
                    <Input
                      id="amountTolerance"
                      type="number"
                      min="0"
                      step="0.01"
                      value={params.amountTolerance}
                      onChange={(e) => handleParamChange('amountTolerance', parseFloat(e.target.value))}
                      className="mt-1"
                    />
                  </div>

                  <div>
                    <Label htmlFor="dateToleranceDays" className="text-sm font-medium text-gray-700">
                      {t('duplicates.dateTolerance')}
                    </Label>
                    <Input
                      id="dateToleranceDays"
                      type="number"
                      min="0"
                      max="30"
                      value={params.dateToleranceDays}
                      onChange={(e) => handleParamChange('dateToleranceDays', parseInt(e.target.value))}
                      className="mt-1"
                    />
                  </div>

                  <div className="flex items-center space-x-2">
                    <Checkbox
                      id="includeReviewed"
                      checked={params.includeReviewed}
                      onCheckedChange={(checked) => handleParamChange('includeReviewed', checked)}
                    />
                    <Label htmlFor="includeReviewed" className="text-sm font-medium text-gray-700">
                      {t('duplicates.includeReviewed')}
                    </Label>
                  </div>

                  <div className="flex items-center space-x-2">
                    <Checkbox
                      id="sameAccountOnly"
                      checked={params.sameAccountOnly}
                      onCheckedChange={(checked) => handleParamChange('sameAccountOnly', checked)}
                    />
                    <Label htmlFor="sameAccountOnly" className="text-sm font-medium text-gray-700">
                      {t('duplicates.sameAccountOnly')}
                    </Label>
                  </div>
                </>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Results */}
        <div className="space-y-4">
          {loading && (
            <div className="text-center py-8">
              <div className="inline-flex items-center gap-2 text-blue-600">
                <div className="w-5 h-5 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />
                <span>{t('duplicates.scanningDuplicates')}</span>
              </div>
            </div>
          )}

          {error && (
            <Card className="border-red-200 bg-red-50">
              <CardContent className="p-4">
                <div className="flex items-center gap-2 text-red-700">
                  <ExclamationTriangleIcon className="w-5 h-5" />
                  <span>{error}</span>
                </div>
              </CardContent>
            </Card>
          )}

          {duplicates && !loading && (
            <>
              {/* Summary */}
              <Card className="bg-green-50 border-green-200">
                <CardContent className="p-4">
                  <div className="flex items-center gap-2 text-green-700">
                    <CheckCircleIcon className="w-5 h-5" />
                    <span className="font-medium">
                      {duplicates.totalGroups === 0 ? (
                        t('duplicates.noneFound')
                      ) : (
                        t('duplicates.foundGroups', { groups: duplicates.totalGroups, transactions: duplicates.totalTransactions })
                      )}
                    </span>
                  </div>
                </CardContent>
              </Card>

              {/* Duplicate Groups */}
              {duplicates.duplicateGroups.length > 0 && (
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-lg font-medium text-gray-900">
                      {t('duplicates.potentialDuplicates')}
                    </h3>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => {
                        const allGroupIds = new Set(duplicates.duplicateGroups.map(g => g.id));
                        setExpandedGroups(
                          expandedGroups.size === allGroupIds.size ? new Set() : allGroupIds
                        );
                      }}
                    >
                      {expandedGroups.size === duplicates.duplicateGroups.length ? tCommon('collapseAll') : tCommon('expandAll')}
                    </Button>
                  </div>

                  {/* Bulk Actions */}
                  <Card className="bg-purple-50 border-purple-200">
                    <CardContent className="p-4">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-3">
                          <h4 className="font-medium text-purple-900">{t('duplicates.bulkActions')}</h4>
                          <span className="text-sm text-purple-700">
                            {t('duplicates.bulkSelected', { selected: selectedGroups.size, total: duplicates.duplicateGroups.length })}
                          </span>
                        </div>
                        <div className="flex items-center gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={selectAllGroups}
                            disabled={selectedGroups.size === duplicates.duplicateGroups.length}
                          >
                            {tCommon('selectAll')}
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={clearGroupSelection}
                            disabled={selectedGroups.size === 0}
                          >
                            {tCommon('clear')}
                          </Button>
                        </div>
                      </div>
                      
                      {selectedGroups.size > 0 && (
                        <div className="flex flex-wrap gap-2">
                          <Button
                            onClick={() => resolveSelectedGroups('keep-newest')}
                            disabled={resolutionPending}
                            className="bg-green-600 hover:bg-green-700 text-white"
                            size="sm"
                          >
                            {resolutionPending ? tCommon('processing') : t('duplicates.keepNewest')}
                          </Button>
                          <Button
                            onClick={() => resolveSelectedGroups('keep-oldest')}
                            disabled={resolutionPending}
                            className="bg-blue-600 hover:bg-blue-700 text-white"
                            size="sm"
                          >
                            {resolutionPending ? tCommon('processing') : t('duplicates.keepOldest')}
                          </Button>
                          <Button
                            onClick={() => resolveSelectedGroups('mark-not-duplicate')}
                            disabled={resolutionPending}
                            variant="secondary"
                            size="sm"
                          >
                            {resolutionPending ? tCommon('processing') : t('duplicates.markNotDuplicate')}
                          </Button>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                  
                  <div className="space-y-4">
                    {duplicates.duplicateGroups.map(renderGroup)}
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </BaseModal>
  );
}
