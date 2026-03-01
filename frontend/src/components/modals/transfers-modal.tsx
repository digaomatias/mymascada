'use client';

import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { BaseModal } from './base-modal';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { apiClient } from '@/lib/api-client';
import { formatCurrency, formatDate } from '@/lib/utils';
import { toast } from 'sonner';
import {
  ArrowsRightLeftIcon,
  MagnifyingGlassIcon,
  AdjustmentsHorizontalIcon,
  ExclamationTriangleIcon,
  CheckCircleIcon,
  EyeIcon,
  CalendarIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  ArrowRightIcon
} from '@heroicons/react/24/outline';
import type {
  PotentialTransfersResponse,
  TransferDetectionParams,
  TransferGroup
} from '@/types/transfers';
import { useTranslations } from 'next-intl';

interface TransfersModalProps {
  isOpen: boolean;
  onClose: () => void;
  onRefresh?: () => void;
}


export function TransfersModal({ isOpen, onClose, onRefresh }: TransfersModalProps) {
  const t = useTranslations('transactions');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [loading, setLoading] = useState(false);
  const [transfers, setTransfers] = useState<PotentialTransfersResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());
  const [selectedGroups, setSelectedGroups] = useState<Set<string>>(new Set());
  const [processingConfirmation, setProcessingConfirmation] = useState(false);
  
  // Detection parameters - simplified with sensible defaults (memoized to prevent infinite loop)
  const params = useMemo<TransferDetectionParams>(() => ({
    amountTolerance: 1.0, // $1 tolerance - hidden from user
    dateToleranceDays: 3, // 3 day tolerance - hidden from user
    includeReviewed: false,
    minConfidence: 0.8, // High confidence - hidden from user
    includeExistingTransfers: false
  }), []);

  const fetchPotentialTransfers = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      const response = await apiClient.getPotentialTransfers(params) as PotentialTransfersResponse;
      setTransfers(response);
    } catch (err) {
      console.error('Failed to fetch potential transfers:', err);
      setError(t('transfers.fetchFailed'));
      toast.error(tToasts('transfersFetchFailed'));
    } finally {
      setLoading(false);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params]);

  useEffect(() => {
    if (isOpen) {
      fetchPotentialTransfers();
    }
  }, [isOpen, fetchPotentialTransfers]);


  const runDetection = () => {
    fetchPotentialTransfers();
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
    if (!transfers) return;
    setSelectedGroups(new Set(transfers.transferGroups.map(g => g.id)));
  };

  const clearGroupSelection = () => {
    setSelectedGroups(new Set());
  };

  const confirmSelectedTransfers = async () => {
    if (!transfers || selectedGroups.size === 0) return;

    setProcessingConfirmation(true);
    
    try {
      let successCount = 0;
      let errorCount = 0;

      // Get the selected transfer groups
      const selectedTransferGroups = transfers.transferGroups.filter(g => selectedGroups.has(g.id));

      // Process each transfer confirmation
      for (const group of selectedTransferGroups) {
        try {
          await apiClient.linkTransactionsAsTransfer({
            sourceTransactionId: group.sourceTransaction.id,
            destinationTransactionId: group.destinationTransaction.id,
            description: t('transferDescription', { from: group.sourceTransaction.accountName ?? '', to: group.destinationTransaction.accountName ?? '' })
          });
          successCount++;
        } catch (error) {
          console.error(`Failed to confirm transfer for group ${group.id}:`, error);
          errorCount++;
        }
      }

      // Show results
      if (successCount > 0 && errorCount === 0) {
        toast.success(t('transfers.confirmSuccess', { count: successCount }));
      } else if (successCount > 0 && errorCount > 0) {
        toast.warning(t('transfers.confirmPartial', { success: successCount, failed: errorCount }));
      } else {
        toast.error(t('transfers.confirmFailed', { count: errorCount }));
      }
      
      // Remove successfully confirmed groups from the display
      if (successCount > 0) {
        const successfulGroupIds = selectedTransferGroups.slice(0, successCount).map(g => g.id);
        setTransfers({
          ...transfers,
          transferGroups: transfers.transferGroups.filter(g => !successfulGroupIds.includes(g.id)),
          totalGroups: transfers.totalGroups - successCount
        });
      }
      
      // Clear selection
      setSelectedGroups(new Set());
      
      // Refresh the main transactions list
      onRefresh?.();
    } catch (err) {
      console.error('Failed to confirm transfers:', err);
      toast.error(tToasts('transfersConfirmFailed'));
    } finally {
      setProcessingConfirmation(false);
    }
  };

  const renderTransferGroup = (group: TransferGroup) => {
    const isExpanded = expandedGroups.has(group.id);
    const isSelected = selectedGroups.has(group.id);
    
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
                  <ArrowsRightLeftIcon className="w-5 h-5 text-purple-500" />
                  <h3 className="font-medium text-gray-900 truncate">
                    {t('transfers.transferMatch')}
                  </h3>
                </div>
                <div className="flex items-center gap-4 text-sm text-gray-600">
                  <span className="flex items-center gap-1">
                    <span className="font-medium">{formatCurrency(group.amount)}</span>
                  </span>
                  <span className="flex items-center gap-1">
                    <CalendarIcon className="w-4 h-4" />
                    {group.dateRange}
                  </span>
                  <span>{group.sourceTransaction.accountName} → {group.destinationTransaction.accountName}</span>
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
            </div>
          </div>

          {/* Match Reasons */}
          {group.matchReasons.length > 0 && (
            <div className="mb-3">
              <div className="flex flex-wrap gap-1">
                {group.matchReasons.map((reason, index) => (
                  <Badge key={index} variant="secondary" className="text-xs">
                    {reason}
                  </Badge>
                ))}
              </div>
            </div>
          )}

          {/* Expanded Group Content */}
          {isExpanded && (
            <div className="space-y-3 pt-3 border-t border-gray-100">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Source Transaction */}
                <div className="bg-red-50 rounded-lg p-3 border border-red-200">
                  <div className="flex items-center gap-2 mb-2">
                    <ArrowRightIcon className="w-4 h-4 text-red-600" />
                    <span className="text-sm font-medium text-red-800">{t('transferFrom', { name: group.sourceTransaction.accountName ?? '' })}</span>
                  </div>
                  <div className="space-y-1 text-sm">
                    <div className="font-medium text-gray-900">
                      #{group.sourceTransaction.id} - {formatCurrency(Math.abs(group.sourceTransaction.amount))}
                    </div>
                    <div className="text-gray-600">{group.sourceTransaction.description}</div>
                    <div className="text-gray-500">{formatDate(group.sourceTransaction.transactionDate)}</div>
                  </div>
                </div>

                {/* Destination Transaction */}
                <div className="bg-green-50 rounded-lg p-3 border border-green-200">
                  <div className="flex items-center gap-2 mb-2">
                    <ArrowRightIcon className="w-4 h-4 text-green-600" />
                    <span className="text-sm font-medium text-green-800">{t('transferTo', { name: group.destinationTransaction.accountName ?? '' })}</span>
                  </div>
                  <div className="space-y-1 text-sm">
                    <div className="font-medium text-gray-900">
                      #{group.destinationTransaction.id} - {formatCurrency(group.destinationTransaction.amount)}
                    </div>
                    <div className="text-gray-600">{group.destinationTransaction.description}</div>
                    <div className="text-gray-500">{formatDate(group.destinationTransaction.transactionDate)}</div>
                  </div>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex justify-end gap-2 pt-2">
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => window.open(`/transactions/${group.sourceTransaction.id}`, '_blank')}
                >
                  <EyeIcon className="w-4 h-4 mr-1" />
                  {t('transfers.viewSource')}
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => window.open(`/transactions/${group.destinationTransaction.id}`, '_blank')}
                >
                  <EyeIcon className="w-4 h-4 mr-1" />
                  {t('transfers.viewDestination')}
                </Button>
              </div>
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
      title={t('manageTransfers')}
      size="xl"
    >
      <div className="space-y-6">
        {/* Simple Detection */}
        <Card className="bg-purple-50 border-purple-200">
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <AdjustmentsHorizontalIcon className="w-5 h-5 text-purple-600" />
                <h3 className="font-medium text-purple-900">{t('transfers.detectionTitle')}</h3>
                <span className="text-sm text-purple-700">{t('transfers.detectionSubtitle')}</span>
              </div>
              <Button
                onClick={runDetection}
                disabled={loading}
                className="bg-purple-600 hover:bg-purple-700"
                size="sm"
              >
                <MagnifyingGlassIcon className="w-4 h-4 mr-2" />
                {loading ? t('transfers.scanning') : t('transfers.findTransfers')}
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Results */}
        <div className="space-y-4">
          {loading && (
            <div className="text-center py-8">
              <div className="inline-flex items-center gap-2 text-purple-600">
                <div className="w-5 h-5 border-2 border-purple-600 border-t-transparent rounded-full animate-spin" />
                <span>{t('transfers.scanningPotential')}</span>
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

          {transfers && !loading && (
            <>
              {/* Summary */}
              <Card className="bg-green-50 border-green-200">
                <CardContent className="p-4">
                  <div className="flex items-center gap-2 text-green-700">
                    <CheckCircleIcon className="w-5 h-5" />
                    <span className="font-medium">
                      {transfers.totalGroups === 0 ? (
                        t('transfers.noneFound')
                      ) : (
                        t('transfers.foundMatches', { count: transfers.totalGroups })
                      )}
                    </span>
                  </div>
                  <p className="text-sm text-green-600 mt-2">
                    {t('transfers.manualHint')}
                  </p>
                </CardContent>
              </Card>

              {/* Transfer Groups */}
              {transfers.transferGroups.length > 0 && (
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-lg font-medium text-gray-900">
                      {t('transfers.potentialMatches')}
                    </h3>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => {
                        const allGroupIds = new Set(transfers.transferGroups.map(g => g.id));
                        setExpandedGroups(
                          expandedGroups.size === allGroupIds.size ? new Set() : allGroupIds
                        );
                      }}
                    >
                      {expandedGroups.size === transfers.transferGroups.length ? tCommon('collapseAll') : tCommon('expandAll')}
                    </Button>
                  </div>

                  {/* Bulk Actions for Transfer Groups */}
                  <Card className="bg-blue-50 border-blue-200">
                    <CardContent className="p-4">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-3">
                          <h4 className="font-medium text-blue-900">{t('transfers.bulkActionsTitle')}</h4>
                          <span className="text-sm text-blue-700">
                            {t('transfers.bulkSelected', { selected: selectedGroups.size, total: transfers.transferGroups.length })}
                          </span>
                        </div>
                        <div className="flex items-center gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={selectAllGroups}
                            disabled={selectedGroups.size === transfers.transferGroups.length}
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
                            onClick={confirmSelectedTransfers}
                            disabled={processingConfirmation}
                            className="bg-green-600 hover:bg-green-700 text-white"
                            size="sm"
                          >
                            {processingConfirmation
                              ? t('transfers.processing')
                              : t('transfers.confirmSelected', { count: selectedGroups.size })}
                          </Button>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                  
                  <div className="space-y-4">
                    {transfers.transferGroups.map(renderTransferGroup)}
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
