'use client';

import { useCallback, useEffect, useState, useMemo } from 'react';
import React from 'react';
import { useTranslations } from 'next-intl';
import { useTransactionFilters } from '@/hooks/use-transaction-filters';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { formatCurrency, formatDate } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import { createTransactionDetailUrl } from '@/lib/navigation-utils';
import { DateTimePicker } from '@/components/ui/date-time-picker';
import { useRouter } from 'next/navigation';
import { 
  MagnifyingGlassIcon,
  FunnelIcon,
  EllipsisVerticalIcon,
  PencilIcon,
  TrashIcon,
  CalendarIcon,
  TagIcon,
  BuildingOffice2Icon,
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
  EyeIcon,
  CheckIcon,
  XMarkIcon,
  ArrowsRightLeftIcon,
  WalletIcon
} from '@heroicons/react/24/outline';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { TransactionReviewButton } from '@/components/buttons/transaction-review-button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { CategoryPicker } from '@/components/forms/category-picker';
import { CategoryFilter } from '@/components/forms/category-filter';
import { useDeviceDetect } from '@/hooks/use-device-detect';
import { toast } from 'sonner';
import { ContextualTransactionLink } from '@/components/ui/contextual-transaction-link';
import { InlineTransferCreator } from '@/components/forms/inline-transfer-creator';

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  categoryName?: string;
  categoryColor?: string;
  accountId?: number;
  accountName?: string;
  status: number;
  isReviewed: boolean;
  transferId?: string;
  relatedTransactionId?: number;
  isTransferSource?: boolean;
  type: number;
}

interface TransactionListProps {
  accountId?: number;
  onTransactionUpdate?: () => void;
  onFilteredBalanceChange?: (balance: number | null) => void;
  showAccountFilter?: boolean;
  compact?: boolean;
  title?: string;
}

export function TransactionList({ 
  accountId, 
  onTransactionUpdate,
  onFilteredBalanceChange, 
  showAccountFilter = true, 
  compact = false,
  title
}: TransactionListProps) {
  const { isMobile } = useDeviceDetect();
  const router = useRouter();
  const t = useTranslations('transactions');
  const tFilters = useTranslations('transactions.filters');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const resolvedTitle = title ?? t('title');
  const {
    searchTerm,
    setSearchTerm,
    currentPage,
    setCurrentPage,
    transferFilter,
    setTransferFilter,
    reviewFilter,
    setReviewFilter,
    typeFilter,
    setTypeFilter,
    reconciliationFilter,
    setReconciliationFilter,
    selectedCategoryId,
    setSelectedCategoryId,
    selectedAccountId,
    setSelectedAccountId,
    dateFilter,
    setDateFilter,
    startDate,
    setStartDate,
    endDate,
    setEndDate,
    isSelectionMode,
    setIsSelectionMode,
    selectedTransactionIds,
    setSelectedTransactionIds,
    bulkCategorizing,
    setBulkCategorizing,
    showBulkDeleteDialog,
    setShowBulkDeleteDialog,
    allCategories,
    setAllCategories,
  } = useTransactionFilters();

  const effectiveAccountId = accountId?.toString() || selectedAccountId;

  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [showFilters, setShowFilters] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState<{ show: boolean; transactionId?: number }>({ show: false });
  const [accounts, setAccounts] = useState<Array<{ id: number; name: string }>>([]);
  const [openMenuId, setOpenMenuId] = useState<number | null>(null);
  const [expandedTransfers, setExpandedTransfers] = useState<Set<string>>(new Set());
  const [createTransferForTransaction, setCreateTransferForTransaction] = useState<Transaction | null>(null);

  // Helper function to get date range from filter type (UTC normalized)
  const getDateRangeFromFilter = useCallback((filter: typeof dateFilter) => {
    // Work with UTC dates to avoid timezone issues with PostgreSQL
    const now = new Date();
    const todayUTC = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
    
    switch (filter) {
      case 'last7':
        const last7Start = new Date(todayUTC.getTime() - 6 * 24 * 60 * 60 * 1000);
        return {
          start: last7Start.toISOString().split('T')[0],
          end: todayUTC.toISOString().split('T')[0]
        };
      
      case 'thisWeek':
        const startOfWeekUTC = new Date(todayUTC);
        startOfWeekUTC.setUTCDate(todayUTC.getUTCDate() - todayUTC.getUTCDay()); // Sunday
        return {
          start: startOfWeekUTC.toISOString().split('T')[0],
          end: todayUTC.toISOString().split('T')[0]
        };
      
      case 'thisMonth':
        const startOfMonthUTC = new Date(Date.UTC(todayUTC.getUTCFullYear(), todayUTC.getUTCMonth(), 1));
        return {
          start: startOfMonthUTC.toISOString().split('T')[0],
          end: todayUTC.toISOString().split('T')[0]
        };
      
      case 'last30':
        const last30Start = new Date(todayUTC.getTime() - 29 * 24 * 60 * 60 * 1000);
        return {
          start: last30Start.toISOString().split('T')[0],
          end: todayUTC.toISOString().split('T')[0]
        };
      
      case 'last3Months':
        const threeMonthsAgoUTC = new Date(Date.UTC(todayUTC.getUTCFullYear(), todayUTC.getUTCMonth() - 3, todayUTC.getUTCDate()));
        return {
          start: threeMonthsAgoUTC.toISOString().split('T')[0],
          end: todayUTC.toISOString().split('T')[0]
        };
      
      case 'custom':
        return {
          start: startDate || undefined,
          end: endDate || undefined
        };
      
      default: // 'all'
        return {
          start: undefined,
          end: undefined
        };
    }
  }, [startDate, endDate]);

  // Helper function to determine if transaction is a transfer
  const isTransfer = useCallback((transaction: Transaction): boolean => {
    return !!(transaction.transferId);
  }, []);

  const toggleTransferExpansion = (transferId: string) => {
    setExpandedTransfers(prev => {
      const newSet = new Set(prev);
      if (newSet.has(transferId)) {
        newSet.delete(transferId);
      } else {
        newSet.add(transferId);
      }
      return newSet;
    });
  };

  // Group transfers together for display
  const groupedTransactions = useMemo(() => {
    const grouped: Array<Transaction | {
      id: string;
      isTransferGroup: true;
      transferId: string;
      transactions: Transaction[];
      amount: number;
      transactionDate: string;
      description: string;
    }> = [];

    const processedTransfers = new Set<string>();

    for (const transaction of transactions) {
      if (transaction.transferId && !processedTransfers.has(transaction.transferId)) {
        // Find related transaction
        const relatedTransaction = transactions.find(t => 
          t.transferId === transaction.transferId && t.id !== transaction.id
        );

        if (relatedTransaction) {
          // Create a transfer group
          const sourceTransaction = transaction.isTransferSource ? transaction : relatedTransaction;
          const destTransaction = transaction.isTransferSource ? relatedTransaction : transaction;
          
          grouped.push({
            id: `transfer-${transaction.transferId}`,
            isTransferGroup: true,
            transferId: transaction.transferId,
            transactions: [sourceTransaction, destTransaction],
            amount: Math.abs(sourceTransaction.amount),
            transactionDate: sourceTransaction.transactionDate,
            description: `Transfer: ${sourceTransaction.accountName} → ${destTransaction.accountName}`
          });
          
          processedTransfers.add(transaction.transferId);
        } else {
          // Single transaction (transfer partner not in current page)
          grouped.push(transaction);
        }
      } else if (!transaction.transferId) {
        // Regular transaction
        grouped.push(transaction);
      }
      // Skip if already processed as part of a transfer group
    }

    return grouped;
  }, [transactions]);

  const fetchTransactions = useCallback(async (page = 1, search = '', filter = transferFilter, categoryId = selectedCategoryId, accountId_param = effectiveAccountId, reviewStatus = reviewFilter, currentDateFilter = dateFilter, currentTypeFilter = typeFilter, currentReconciliationFilter = reconciliationFilter) => {
    try {
      setLoading(true);
      const params: {
        page: number;
        pageSize: number;
        searchTerm?: string;
        onlyTransfers?: boolean;
        includeTransfers?: boolean;
        categoryId?: number;
        accountId?: number;
        isReviewed?: boolean;
        isReconciled?: boolean;
        startDate?: string;
        endDate?: string;
        transactionType?: string;
      } = {
        page,
        pageSize: compact ? 10 : 20,
        searchTerm: search || undefined,
      };

      // Add transfer filtering
      if (filter === 'only') {
        params.onlyTransfers = true;
      } else if (filter === 'exclude') {
        params.includeTransfers = false;
      }

      // Add category and account filters
      if (categoryId) {
        params.categoryId = parseInt(categoryId);
      }
      if (accountId_param) {
        params.accountId = parseInt(accountId_param);
      }

      // Add review status filter (server-side)
      if (reviewStatus === 'reviewed') {
        params.isReviewed = true;
      } else if (reviewStatus === 'not-reviewed') {
        params.isReviewed = false;
      }

      // Add date range filter
      const dateRange = getDateRangeFromFilter(currentDateFilter);
      if (dateRange.start) {
        params.startDate = dateRange.start;
      }
      if (dateRange.end) {
        params.endDate = dateRange.end;
      }

      // Add transaction type filter
      if (currentTypeFilter === 'income') {
        params.transactionType = 'income';
      } else if (currentTypeFilter === 'expense') {
        params.transactionType = 'expense';
      }

      // Add reconciliation status filter
      if (currentReconciliationFilter === 'reconciled') {
        params.isReconciled = true;
      } else if (currentReconciliationFilter === 'not-reconciled') {
        params.isReconciled = false;
      }

      const response = await apiClient.getTransactions(params) as {
        transactions: Transaction[];
        totalPages: number;
        totalCount: number;
        page: number;
        summary?: {
          totalBalance: number;
          totalIncome: number;
          totalExpenses: number;
          incomeTransactionCount: number;
          expenseTransactionCount: number;
          transferTransactionCount: number;
          unreviewedTransactionCount: number;
        };
      };
      
      setTransactions(response?.transactions || []);
      setTotalPages(response?.totalPages || 1);
      setTotalCount(response?.totalCount || 0);
      setCurrentPage(response?.page || 1);
      
      // Update filtered balance if callback is provided
      if (onFilteredBalanceChange && accountId && response?.summary) {
        onFilteredBalanceChange(response.summary.totalBalance);
      }
    } catch (error) {
      console.error('Failed to fetch transactions:', error);
      setTransactions([]);
    } finally {
      setLoading(false);
    }
  }, [transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, getDateRangeFromFilter, compact, accountId, onFilteredBalanceChange, setCurrentPage]);

  useEffect(() => {
    fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentPage, searchTerm, transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter]);

  const handleSearch = (value: string) => {
    setSearchTerm(value);
    setCurrentPage(1);
  };

  const loadCategories = useCallback(async () => {
    try {
      const categoriesData = await apiClient.getCategories() as Array<{ id: number; name: string; fullPath?: string; type: number; parentId: number | null }>;
      setAllCategories(categoriesData || []);
    } catch (error) {
      console.error('Failed to load categories:', error);
      setAllCategories([]);
    }
  }, []);

  const loadAccounts = useCallback(async () => {
    try {
      const accountsData = await apiClient.getAccounts() as Array<{ id: number; name: string }>;
      setAccounts(accountsData || []);
    } catch (error) {
      console.error('Failed to load accounts:', error);
      setAccounts([]);
    }
  }, []);

  // Load initial data
  useEffect(() => {
    loadCategories();
    if (showAccountFilter) {
      loadAccounts();
    }
  }, [showAccountFilter, loadCategories]);

  // Bulk selection functions
  const toggleSelectionMode = () => {
    setIsSelectionMode(!isSelectionMode);
    setSelectedTransactionIds(new Set());
  };

  const toggleTransactionSelection = (transactionId: number) => {
    const newSelected = new Set(selectedTransactionIds);
    if (newSelected.has(transactionId)) {
      newSelected.delete(transactionId);
    } else {
      newSelected.add(transactionId);
    }
    setSelectedTransactionIds(newSelected);
  };

  const selectAllTransactions = () => {
    const allIds = new Set(transactions.map(t => t.id));
    setSelectedTransactionIds(allIds);
  };

  const clearSelection = () => {
    setSelectedTransactionIds(new Set());
  };

  const handleBulkCategoryAssignment = async (categoryId: string | number) => {
    if (selectedTransactionIds.size === 0) return;

    setBulkCategorizing(true);
    const selectedIds = Array.from(selectedTransactionIds);
    let successCount = 0;
    let errorCount = 0;

    try {
      // Update each transaction individually
      for (const transactionId of selectedIds) {
        try {
          const transaction = transactions.find(t => t.id === transactionId);
          if (transaction) {
            // Map status to enum values
            const statusMap: Record<string, number> = {
              'pending': 1,
              'cleared': 2, 
              'reconciled': 3,
              'cancelled': 4
            };
            
            // Convert status to number
            let statusValue = 2; // Default to Cleared
            if (typeof transaction.status === 'string') {
              statusValue = statusMap[(transaction.status as string).toLowerCase()] || 2;
            } else if (typeof transaction.status === 'number') {
              statusValue = transaction.status;
            }

            await apiClient.updateTransaction(transactionId, {
              ...transaction,
              categoryId: Number(categoryId),
              transactionDate: transaction.transactionDate,
              status: statusValue,
            });
            successCount++;
          }
        } catch (err) {
          console.error(`Failed to update transaction ${transactionId}:`, err);
          errorCount++;
        }
      }

      // Show results
      if (successCount > 0) {
        toast.success(t('categoryAssigned', { count: successCount }));
      }
      if (errorCount > 0) {
        toast.error(t('failedToUpdate', { count: errorCount }));
      }

      // Refresh transactions and clear selection
      await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter);
      setSelectedTransactionIds(new Set());
      setIsSelectionMode(false);

      if (onTransactionUpdate) {
        onTransactionUpdate();
      }
    } catch (err) {
      console.error('Bulk category assignment failed:', err);
      toast.error(tToasts('categoryAssignFailed'));
    } finally {
      setBulkCategorizing(false);
    }
  };

  const handleLongPress = (transactionId: number) => {
    if (!isSelectionMode) {
      setIsSelectionMode(true);
      setSelectedTransactionIds(new Set([transactionId]));
    }
  };

  const handleApplyFilters = () => {
    setCurrentPage(1);
    setShowFilters(false);
  };

  const handleBulkDelete = () => {
    setShowBulkDeleteDialog(true);
  };

  const confirmBulkDelete = async () => {
    setBulkCategorizing(true);
    const selectedIds = Array.from(selectedTransactionIds);
    let successCount = 0;
    let errorCount = 0;

    try {
      // Delete each transaction individually
      for (const transactionId of selectedIds) {
        try {
          await apiClient.deleteTransaction(transactionId);
          successCount++;
        } catch (err) {
          console.error(`Failed to delete transaction ${transactionId}:`, err);
          errorCount++;
        }
      }

      // Show results
      if (successCount > 0) {
        toast.success(t('deleted', { count: successCount }));
      }
      if (errorCount > 0) {
        toast.error(t('failedToDelete', { count: errorCount }));
      }

      // Refresh transactions and clear selection
      await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter);
      setSelectedTransactionIds(new Set());
      setIsSelectionMode(false);

      if (onTransactionUpdate) {
        onTransactionUpdate();
      }
    } catch (err) {
      console.error('Bulk delete failed:', err);
      toast.error(tToasts('transactionsDeleteFailed'));
    } finally {
      setBulkCategorizing(false);
      setShowBulkDeleteDialog(false);
    }
  };

  const handleDelete = async () => {
    if (deleteConfirm.transactionId) {
      try {
        await apiClient.deleteTransaction(deleteConfirm.transactionId);
        await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter);
        toast.success(tToasts('transactionDeleted'));
        
        if (onTransactionUpdate) {
          onTransactionUpdate();
        }
      } catch (error) {
        console.error('Failed to delete transaction:', error);
        toast.error(t('failedToDeleteTransaction'));
      } finally {
        setDeleteConfirm({ show: false });
      }
    }
  };

  return (
    <div className="space-y-4">
      {/* Header Controls */}
      {!compact && (
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">{resolvedTitle}</h2>
            <p className="text-sm text-gray-600">
              {totalCount > 0 ? t('count', { count: totalCount }) : t('noTransactions')}
            </p>
          </div>
          
          <div className="flex gap-2">
            {!isSelectionMode ? (
              <>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setShowFilters(!showFilters)}
                  className="flex items-center gap-2"
                >
                  <FunnelIcon className="w-4 h-4" />
                  <span className="hidden sm:inline">{tCommon('filters')}</span>
                </Button>
                
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={toggleSelectionMode}
                  className="flex items-center gap-2"
                >
                  <CheckIcon className="w-4 h-4" />
                  <span className="hidden sm:inline">{t('select')}</span>
                </Button>
              </>
            ) : (
              <>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={toggleSelectionMode}
                  className="flex items-center gap-2"
                >
                  <XMarkIcon className="w-4 h-4" />
                  {tCommon('cancel')}
                </Button>
                
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={selectAllTransactions}
                  className="flex items-center gap-2"
                  disabled={selectedTransactionIds.size === transactions.length}
                >
                  <CheckIcon className="w-4 h-4" />
                  <span className="hidden sm:inline">{tCommon('all')}</span>
                </Button>
                
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={clearSelection}
                  className="flex items-center gap-2"
                  disabled={selectedTransactionIds.size === 0}
                >
                  {tCommon('clear')}
                </Button>
              </>
            )}
          </div>
        </div>
      )}

      {/* Search Bar */}
      <div className="relative">
        <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
        <Input
          type="text"
          placeholder={t('searchPlaceholder')}
          value={searchTerm}
          onChange={(e) => handleSearch(e.target.value)}
          className="pl-10 w-full"
        />
      </div>

      {/* Filters */}
      {showFilters && (
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg overflow-visible">
          <CardContent className="p-4 overflow-visible">
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 overflow-visible">
              <div className="overflow-visible">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tFilters('dateRange')}
                </label>
                
                <div className="flex flex-wrap gap-1 mb-3">
                  {(['all', 'last7', 'thisMonth', 'last30', 'last3Months', 'custom'] as const).map((filterType) => {
                    const labels = {
                      all: isMobile ? t('shortLabels.all') : tFilters('allTime'),
                      last7: isMobile ? t('shortLabels.7d') : tFilters('last7Days'),
                      thisMonth: isMobile ? t('shortLabels.thisMonth') : tFilters('thisMonth'),
                      last30: isMobile ? t('shortLabels.30d') : tFilters('last30Days'),
                      last3Months: isMobile ? t('shortLabels.3mo') : tFilters('last3Months'),
                      custom: tFilters('custom')
                    };
                    
                    return (
                      <button
                        key={filterType}
                        type="button"
                        onClick={() => setDateFilter(filterType)}
                        className={`${isMobile ? 'px-2 py-1 text-xs' : 'px-3 py-1.5 text-xs'} rounded-md transition-colors cursor-pointer ${
                          dateFilter === filterType
                            ? 'bg-gradient-to-r from-primary-500 to-primary-600 text-white font-medium'
                            : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                        }`}
                      >
                        {labels[filterType]}
                      </button>
                    );
                  })}
                </div>
                
                {dateFilter === 'custom' && (
                  <div className="flex gap-2 overflow-visible">
                    <DateTimePicker
                      value={startDate}
                      onChange={(value) => setStartDate(value)}
                      placeholder={t('startDate')}
                      showTime={false}
                    />
                    <DateTimePicker
                      value={endDate}
                      onChange={(value) => setEndDate(value)}
                      placeholder={t('endDate')}
                      showTime={false}
                    />
                  </div>
                )}
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tCommon('category')}
                </label>
                <CategoryFilter
                  value={selectedCategoryId}
                  onChange={(categoryId) => setSelectedCategoryId(categoryId)}
                  categories={allCategories}
                  placeholder={tFilters('allCategories')}
                />
              </div>
              
              {showAccountFilter && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    {tCommon('account')}
                  </label>
                  <select
                    className="select text-sm"
                    value={selectedAccountId}
                    onChange={(e) => setSelectedAccountId(e.target.value)}
                  >
                    <option value="">{tFilters('allAccounts')}</option>
                    {accounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.name}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tFilters('transfers')}
                </label>
                <select
                  className="select text-sm"
                  value={transferFilter}
                  onChange={(e) => setTransferFilter(e.target.value as 'all' | 'only' | 'exclude')}
                >
                  <option value="all">{tFilters('allTransactions')}</option>
                  <option value="only">{tFilters('onlyTransfers')}</option>
                  <option value="exclude">{tFilters('excludeTransfers')}</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tFilters('reviewStatus')}
                </label>
                <select
                  className="select text-sm"
                  value={reviewFilter}
                  onChange={(e) => setReviewFilter(e.target.value as 'all' | 'reviewed' | 'not-reviewed')}
                >
                  <option value="all">{tFilters('allTransactions')}</option>
                  <option value="reviewed">{tFilters('reviewedOnly')}</option>
                  <option value="not-reviewed">{tFilters('notReviewedOnly')}</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tCommon('type')}
                </label>
                <select
                  className="select text-sm"
                  value={typeFilter}
                  onChange={(e) => setTypeFilter(e.target.value as 'all' | 'income' | 'expense')}
                >
                  <option value="all">{tFilters('allTypes')}</option>
                  <option value="income">{tFilters('incomeOnly')}</option>
                  <option value="expense">{tFilters('expenseOnly')}</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tFilters('reconciliationStatus')}
                </label>
                <select
                  className="select text-sm"
                  value={reconciliationFilter}
                  onChange={(e) => setReconciliationFilter(e.target.value as 'all' | 'reconciled' | 'not-reconciled')}
                >
                  <option value="all">{tFilters('allReconciliation')}</option>
                  <option value="reconciled">{tFilters('reconciledOnly')}</option>
                  <option value="not-reconciled">{tFilters('notReconciledOnly')}</option>
                </select>
              </div>
            </div>
            
            <div className="flex justify-end mt-4 gap-2">
              <Button variant="secondary" size="sm" onClick={() => {
                setTransferFilter('all');
                setReviewFilter('all');
                setTypeFilter('all');
                setReconciliationFilter('all');
                setSelectedCategoryId('');
                if (showAccountFilter) setSelectedAccountId('');
                setDateFilter('all');
                setStartDate('');
                setEndDate('');
                setCurrentPage(1);
              }}>
                {tCommon('clear')}
              </Button>
              <Button size="sm" onClick={handleApplyFilters}>{t('applyFilters')}</Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Bulk Action Toolbar */}
      {isSelectionMode && selectedTransactionIds.size > 0 && (
        <Card className="bg-white/95 backdrop-blur-sm border-0 shadow-lg">
          <CardContent className={`${isMobile ? 'p-3' : 'p-4'}`}>
            <div className={`flex items-center ${isMobile ? 'gap-2' : 'justify-between'}`}>
              <div className="flex items-center gap-2">
                <span className={`${isMobile ? 'px-2 py-1 bg-primary-100 text-primary-700 rounded-full text-xs font-medium' : 'text-sm font-medium text-gray-700'}`}>
                  {isMobile
                    ? t('selectedCountShort', { count: selectedTransactionIds.size })
                    : t('selectedCount', { count: selectedTransactionIds.size })}
                </span>
              </div>
              
              <div className="flex-1 flex items-center justify-end gap-2">
                {bulkCategorizing ? (
                  <div className="flex items-center gap-2">
                    <div className="w-4 h-4 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" />
                    <span className={`${isMobile ? 'text-xs' : 'text-sm'} text-gray-600`}>
                      Processing...
                    </span>
                  </div>
                ) : (
                  <div className={`flex items-center gap-2 ${isMobile ? 'flex-1' : ''}`}>
                    <div className={`flex items-center gap-2 ${isMobile ? 'flex-1' : ''}`}>
                      <span className="text-sm text-gray-600 hidden sm:inline">{tCommon('category')}:</span>
                      <div className={`${isMobile ? 'flex-1' : 'w-48'}`}>
                        <CategoryPicker
                          value=""
                          onChange={handleBulkCategoryAssignment}
                          categories={allCategories}
                          placeholder={isMobile ? `${tCommon('category')}...` : t('chooseCategory')}
                          disabled={bulkCategorizing}
                          disableQuickPicks={true}
                        />
                      </div>
                    </div>
                    
                    <Button 
                      variant="secondary" 
                      size="sm" 
                      onClick={handleBulkDelete}
                      className="flex items-center gap-1 text-red-600 hover:text-red-700"
                    >
                      <TrashIcon className="w-4 h-4" />
                      {!isMobile && <span>{tCommon('delete')}</span>}
                    </Button>
                  </div>
                )}
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Transaction List */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        {loading ? (
          <CardContent className="p-6">
            <div className="space-y-4">
              {Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="animate-pulse">
                  <div className="flex items-center gap-4 p-4 bg-gray-100 rounded-lg">
                    <div className="w-12 h-12 bg-gray-300 rounded-xl"></div>
                    <div className="flex-1">
                      <div className="h-4 bg-gray-300 rounded w-1/2 mb-2"></div>
                      <div className="h-3 bg-gray-300 rounded w-1/4"></div>
                    </div>
                    <div className="h-6 bg-gray-300 rounded w-20"></div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        ) : transactions.length === 0 ? (
          <CardContent className="p-8 text-center">
            <div className="w-20 h-20 bg-gradient-to-br from-primary-400 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <WalletIcon className="w-10 h-10 text-white" />
            </div>
            <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('noTransactionsFound')}</h3>
            <p className="text-gray-600 mb-6">
              {searchTerm ? t('adjustSearchTerms') : accountId ? t('noTransactionsFiltered') : t('getStarted')}
            </p>
          </CardContent>
        ) : (
          <CardContent className="p-0">
            <div className="divide-y divide-gray-100">
              {groupedTransactions.map((item) => {
                // Handle transfer groups
                if ('isTransferGroup' in item && item.isTransferGroup) {
                  const isExpanded = expandedTransfers.has(item.transferId);
                  return (
                    <div key={item.id}>
                      <div 
                        className={`${isMobile ? 'p-3 border-l-4 border-blue-500' : 'p-4'} hover:bg-gray-50 transition-colors cursor-pointer`}
                        onClick={() => toggleTransferExpansion(item.transferId)}
                      >
                        <div className={`flex items-center ${isMobile ? 'gap-2' : 'gap-3'}`}>
                          {!isMobile && (
                            <div className="w-12 h-12 rounded-xl flex items-center justify-center shadow-sm bg-gradient-to-br from-blue-100 to-blue-200">
                              <ArrowsRightLeftIcon className="w-6 h-6 text-blue-600" />
                            </div>
                          )}
                          
                          <div className="flex-1 min-w-0">
                            <div className="flex items-start justify-between">
                              <div className="min-w-0 flex-1">
                                <p className="text-sm font-semibold text-gray-900 truncate">
                                  {item.description}
                                </p>
                                
                                <div className="flex items-center gap-3 mt-1 text-xs text-gray-500">
                                  <span className="flex items-center gap-1">
                                    <CalendarIcon className="w-3 h-3" />
                                    {formatDate(item.transactionDate)}
                                  </span>
                                  <span className="text-blue-600 font-medium">{tCommon('transfer')}</span>
                                </div>
                              </div>
                              
                              <div className="flex items-center gap-3 ml-3">
                                <div className="text-right">
                                  <p className="text-sm font-bold text-blue-600">
                                    {formatCurrency(item.amount)}
                                  </p>
                                  <p className="text-xs text-gray-500">
                                    {t('internalTransfer')}
                                  </p>
                                </div>
                                <svg
                                  className={`w-5 h-5 text-gray-400 transition-transform ${isExpanded ? 'rotate-180' : ''}`}
                                  fill="none"
                                  viewBox="0 0 24 24"
                                  stroke="currentColor"
                                >
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                                </svg>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                      
                      {isExpanded && (
                        <div className="bg-gray-50 border-t border-gray-100">
                          <div className="p-4 space-y-3">
                            {item.transactions.map((trans) => (
                              <div key={trans.id} className={`flex items-center justify-between p-3 bg-white rounded-lg border ${trans.isTransferSource ? 'border-red-200' : 'border-green-200'}`}>
                                <div className="flex items-center gap-3">
                                  <div className={`w-10 h-10 rounded-full flex items-center justify-center ${trans.isTransferSource ? 'bg-red-100' : 'bg-green-100'}`}>
                                    {trans.isTransferSource ? (
                                      <ArrowTrendingDownIcon className="w-5 h-5 text-red-600" />
                                    ) : (
                                      <ArrowTrendingUpIcon className="w-5 h-5 text-green-600" />
                                    )}
                                  </div>
                                  <div>
                                    <p className="text-sm font-medium text-gray-900">
                                      {trans.accountName}
                                    </p>
                                    <p className="text-xs text-gray-500">
                                      Transaction #{trans.id}
                                    </p>
                                  </div>
                                </div>
                                <div className="flex items-center gap-3">
                                  <p className={`text-sm font-bold ${trans.isTransferSource ? 'text-red-600' : 'text-green-600'}`}>
                                    {formatCurrency(trans.amount)}
                                  </p>
                                  <ContextualTransactionLink
                                    transactionId={trans.id}
                                    className="text-gray-400 hover:text-gray-600"
                                    onClick={(e) => e.stopPropagation()}
                                  >
                                    <EyeIcon className="w-4 h-4" />
                                  </ContextualTransactionLink>
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  );
                }

                // Handle regular transactions
                const transaction = item as Transaction;
                const isIncome = transaction.amount > 0; // Positive = Income, Negative = Expense
                const isSelected = selectedTransactionIds.has(transaction.id);

                return (
                  <React.Fragment key={transaction.id}>
                  <div 
                    className={`relative group ${
                      isSelected 
                        ? 'bg-primary-50 border-l-4 border-primary-500' 
                        : isMobile 
                          ? `border-l-4 ${
                              isTransfer(transaction) 
                                ? 'border-blue-500' 
                                : isIncome 
                                  ? 'border-success-500' 
                                  : 'border-red-500'
                            }` 
                          : ''
                    } ${openMenuId === transaction.id ? 'z-30' : ''}`}
                  >
                    <div 
                      className={`block ${isMobile ? 'p-3 pr-3' : 'p-4 pr-12'} transition-colors ${
                        isSelectionMode 
                          ? 'cursor-pointer hover:bg-gray-50' 
                          : 'cursor-pointer hover:bg-gray-50'
                      }`}
                      onClick={(e) => {
                        if (isSelectionMode) {
                          const target = e.target as HTMLElement;
                          const checkboxArea = target.closest('[data-checkbox="true"]');
                          if (!checkboxArea) {
                            e.preventDefault();
                            toggleTransactionSelection(transaction.id);
                          }
                        } else {
                          router.push(createTransactionDetailUrl(transaction.id, window.location.href));
                        }
                      }}
                      onTouchStart={(e) => {
                        if (isMobile && !isSelectionMode) {
                          const touchStartTime = Date.now();
                          const startTarget = e.currentTarget;
                          const startTouch = e.touches[0];
                          
                          const timeout = setTimeout(() => {
                            handleLongPress(transaction.id);
                          }, 500);
                          
                          const cleanup = () => {
                            clearTimeout(timeout);
                            document.removeEventListener('touchend', handleTouchEnd);
                            document.removeEventListener('touchmove', handleTouchMove);
                          };
                          
                          const handleTouchMove = (moveEvent: TouchEvent) => {
                            const moveTouch = moveEvent.touches[0];
                            const deltaX = Math.abs(moveTouch.clientX - startTouch.clientX);
                            const deltaY = Math.abs(moveTouch.clientY - startTouch.clientY);
                            
                            if (deltaX > 10 || deltaY > 10) {
                              cleanup();
                            }
                          };
                          
                          const handleTouchEnd = (endEvent: TouchEvent) => {
                            const touchDuration = Date.now() - touchStartTime;
                            clearTimeout(timeout);
                            
                            const endTarget = document.elementFromPoint(
                              endEvent.changedTouches[0].clientX,
                              endEvent.changedTouches[0].clientY
                            );
                            
                            const isValidTarget = endTarget && (
                              startTarget.contains(endTarget) || 
                              endTarget === startTarget
                            );
                            
                            if (touchDuration < 500 && isValidTarget && !isSelectionMode) {
                              const checkboxArea = endTarget?.closest('[data-checkbox="true"]');
                              if (!checkboxArea) {
                                e.preventDefault();
                                router.push(createTransactionDetailUrl(transaction.id, window.location.href));
                              }
                            }
                            
                            cleanup();
                          };
                          
                          document.addEventListener('touchend', handleTouchEnd);
                          document.addEventListener('touchmove', handleTouchMove);
                        }
                      }}
                    >
                      <div className={`flex items-center ${isMobile ? 'gap-2' : 'gap-3'}`}>
                        {isSelectionMode && (
                          <div 
                            className="flex-shrink-0 cursor-pointer"
                            data-checkbox="true"
                            onClick={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              toggleTransactionSelection(transaction.id);
                            }}
                            onTouchStart={(e) => {
                              e.stopPropagation();
                            }}
                            onTouchEnd={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              toggleTransactionSelection(transaction.id);
                            }}
                          >
                            <div className={`w-5 h-5 rounded border-2 flex items-center justify-center ${
                              isSelected 
                                ? 'bg-primary-500 border-primary-500' 
                                : 'border-gray-300 hover:border-primary-400'
                            }`}>
                              {isSelected && (
                                <CheckIcon className="w-3 h-3 text-white" />
                              )}
                            </div>
                          </div>
                        )}
                        
                        {!isMobile && (
                          <div className={`w-12 h-12 rounded-xl flex items-center justify-center shadow-sm ${
                            isTransfer(transaction)
                              ? 'bg-gradient-to-br from-blue-100 to-blue-200'
                              : isIncome 
                                ? 'bg-gradient-to-br from-success-100 to-success-200' 
                                : 'bg-gradient-to-br from-red-100 to-red-200'
                          }`}>
                            {isTransfer(transaction) ? (
                              <ArrowsRightLeftIcon className="w-6 h-6 text-blue-600" />
                            ) : isIncome ? (
                              <ArrowTrendingUpIcon className="w-6 h-6 text-success-600" />
                            ) : (
                              <ArrowTrendingDownIcon className="w-6 h-6 text-red-600" />
                            )}
                          </div>
                        )}
                        
                        <div className="flex-1 min-w-0">
                          <div className="flex items-start justify-between">
                            <div className="min-w-0 flex-1">
                              <div className="flex items-center gap-2">
                                <p className="text-sm font-semibold text-gray-900 truncate">
                                  {transaction.userDescription || transaction.description}
                                </p>
                                {isTransfer(transaction) && (
                                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800 flex-shrink-0">
                                    <ArrowsRightLeftIcon className="w-3 h-3" />
                                    {tCommon('transfer')}
                                  </span>
                                )}
                              </div>
                              
                              <div className="flex items-center gap-2 sm:gap-3 mt-1 text-xs text-gray-500 flex-wrap">
                                <span className="flex items-center gap-1 whitespace-nowrap">
                                  <CalendarIcon className="w-3 h-3 flex-shrink-0" />
                                  {formatDate(transaction.transactionDate)}
                                </span>

                                {transaction.categoryName && (
                                  <span className="flex items-center gap-1 max-w-[120px] sm:max-w-none">
                                    <TagIcon className="w-3 h-3 flex-shrink-0" />
                                    <span className="truncate">{transaction.categoryName}</span>
                                  </span>
                                )}

                                {transaction.status === 3 && (
                                  <span
                                    className="inline-flex items-center justify-center w-4 h-4 rounded-full bg-purple-100 text-purple-700 text-[10px] font-bold flex-shrink-0"
                                    title={t('status.reconciled')}
                                  >
                                    {(t('status.reconciled')[0] || 'R').toUpperCase()}
                                  </span>
                                )}

                                {transaction.accountName && showAccountFilter && (
                                  <span className="flex items-center gap-1 max-w-[100px] sm:max-w-none">
                                    <BuildingOffice2Icon className="w-3 h-3 flex-shrink-0" />
                                    <span className="truncate">{transaction.accountName}</span>
                                  </span>
                                )}
                              </div>
                            </div>
                            
                            <div className="flex items-center gap-3 ml-3">
                              <div className="text-right">
                                <p className={`text-sm font-bold ${
                                  isIncome ? 'text-success-600' : 'text-red-600'
                                }`}>
                                  {formatCurrency(transaction.amount)}
                                </p>
                                
                                {!isMobile && (
                                  <TransactionReviewButton 
                                    transactionId={transaction.id}
                                    isReviewed={transaction.isReviewed}
                                    onReviewComplete={() => {
                                      fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter);
                                      if (onTransactionUpdate) onTransactionUpdate();
                                    }}
                                  />
                                )}
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                    
                    {!isSelectionMode && !isMobile && (
                      <div className="absolute top-4 right-4 z-10">
                        <DropdownMenu onOpenChange={(isOpen) => setOpenMenuId(isOpen ? transaction.id : null)}>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="sm" className="w-8 h-8 p-0 bg-white shadow-sm opacity-0 group-hover:opacity-100 transition-opacity">
                              <EllipsisVerticalIcon className="w-4 h-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-48 bg-white border border-gray-200 shadow-lg z-50">
                            <DropdownMenuItem asChild>
                              <ContextualTransactionLink 
                                transactionId={transaction.id}
                                mode="edit"
                                className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded-sm"
                              >
                                <PencilIcon className="w-4 h-4" />
                                {t('editTransaction')}
                              </ContextualTransactionLink>
                            </DropdownMenuItem>
                            <DropdownMenuItem asChild>
                              <ContextualTransactionLink
                                transactionId={transaction.id}
                                className="flex items-center gap-2 cursor-pointer px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded-sm"
                              >
                                <EyeIcon className="w-4 h-4" />
                                {t('viewDetails')}
                              </ContextualTransactionLink>
                            </DropdownMenuItem>
                            {/* Only show Create Transfer for non-transfer transactions */}
                            {!transaction.transferId && (
                              <DropdownMenuItem
                                onClick={() => {
                                  setCreateTransferForTransaction(transaction);
                                  setOpenMenuId(null);
                                }}
                                className="flex items-center gap-2 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded-sm cursor-pointer"
                              >
                                <ArrowsRightLeftIcon className="w-4 h-4" />
                                {t('createTransfer')}
                              </DropdownMenuItem>
                            )}
                            <DropdownMenuSeparator className="my-1 bg-gray-200" />
                            <DropdownMenuItem 
                              onClick={() => setDeleteConfirm({ show: true, transactionId: transaction.id })}
                              className="flex items-center gap-2 px-3 py-2 text-sm text-red-600 hover:bg-red-50 rounded-sm cursor-pointer"
                            >
                              <TrashIcon className="w-4 h-4" />
                              {t('deleteTransaction')}
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    )}
                  </div>

                  {/* Inline Transfer Creator - shows below selected transaction */}
                  {createTransferForTransaction?.id === transaction.id && (
                    <div className="mt-2">
                      <InlineTransferCreator
                        sourceTransaction={{
                          id: transaction.id,
                          amount: transaction.amount,
                          transactionDate: transaction.transactionDate,
                          description: transaction.userDescription || transaction.description,
                          accountName: transaction.accountName || t('unknownAccount'),
                          accountId: transaction.accountId || accountId || 0
                        }}
                        onCancel={() => setCreateTransferForTransaction(null)}
                        onSuccess={() => {
                          setCreateTransferForTransaction(null);
                          // Refresh the transaction list
                          fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, effectiveAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter);
                          onTransactionUpdate?.();
                          toast.success(tToasts('transferCreated'));
                        }}
                      />
                    </div>
                  )}
                </React.Fragment>
                );
              })}
            </div>
            
            {/* Pagination */}
            {totalPages > 1 && (
              <div className="border-t border-gray-100 p-4">
                <div className="flex items-center justify-between gap-2">
                  <p className="text-sm text-gray-700 whitespace-nowrap">
                    {t('page', { current: currentPage, total: totalPages })}
                  </p>

                  <div className="flex gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      disabled={currentPage === 1}
                      onClick={() => setCurrentPage(currentPage - 1)}
                    >
                      <span className="hidden sm:inline">{tCommon('previous')}</span>
                      <span className="sm:hidden">{tCommon('previousShort')}</span>
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      disabled={currentPage === totalPages}
                      onClick={() => setCurrentPage(currentPage + 1)}
                    >
                      {tCommon('next')}
                    </Button>
                  </div>
                </div>
              </div>
            )}
          </CardContent>
        )}
      </Card>

      {/* Confirmation Dialogs */}
      <ConfirmationDialog
        isOpen={showBulkDeleteDialog}
        onClose={() => setShowBulkDeleteDialog(false)}
        onConfirm={confirmBulkDelete}
        title={t('deleteSelected')}
        description={t('deleteSelectedConfirm', { count: selectedTransactionIds.size })}
        confirmText={tCommon('delete')}
        cancelText={tCommon('cancel')}
        variant="danger"
      />
      
      <ConfirmationDialog
        isOpen={deleteConfirm.show}
        onClose={() => setDeleteConfirm({ show: false })}
        onConfirm={handleDelete}
        title={t('deleteTransaction')}
        description={t('deleteConfirmFull')}
        confirmText={tCommon('delete')}
        cancelText={tCommon('cancel')}
        variant="danger"
      />
    </div>
  );
}
