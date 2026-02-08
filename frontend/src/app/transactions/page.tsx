'use client';

import { useAuth } from '@/contexts/auth-context';
import { useFeatures } from '@/contexts/features-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState, useCallback, Suspense } from 'react';
import React from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { formatCurrency, formatDate } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import { createTransactionDetailUrl } from '@/lib/navigation-utils';
import { DateTimePicker } from '@/components/ui/date-time-picker';
import Link from 'next/link';
import {
  MagnifyingGlassIcon,
  PlusIcon,
  FunnelIcon,
  EllipsisVerticalIcon,
  PencilIcon,
  TrashIcon,
  CalendarIcon,
  TagIcon,
  BuildingOffice2Icon,
  WalletIcon,
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
  DocumentArrowUpIcon,
  EyeIcon,
  CheckIcon,
  XMarkIcon,
  ArrowsRightLeftIcon,
  SparklesIcon,
  ArrowPathIcon
} from '@heroicons/react/24/outline';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { AddTransactionButton } from '@/components/buttons/add-transaction-button';
import { TransactionReviewButton } from '@/components/buttons/transaction-review-button';
import { AkahuSyncButton } from '@/components/buttons/akahu-sync-button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { CategoryPicker } from '@/components/forms/category-picker';
import { CategoryFilter } from '@/components/forms/category-filter';
import { useDeviceDetect } from '@/hooks/use-device-detect';
import { toast } from 'sonner';
import { DuplicatesModal } from '@/components/modals/duplicates-modal';
import { TransfersModal } from '@/components/modals/transfers-modal';
import { ContextualTransactionLink } from '@/components/ui/contextual-transaction-link';
import { useTransactionFilters, SortField, SortDirection } from '@/hooks/use-transaction-filters';
import { InlineTransferCreator } from '@/components/forms/inline-transfer-creator';
import { useTranslations } from 'next-intl';
import { FloatingActionButton } from '@/components/ui/floating-action-button';
import { MobileActionsOverflow } from '@/components/ui/mobile-actions-overflow';

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  categoryName?: string;
  categoryColor?: string;
  accountName?: string;
  accountId?: number;
  status: number;
  isReviewed: boolean;
  transferId?: string;
  relatedTransactionId?: number;
  isTransferSource?: boolean;
  type: number;
}

// interface TransactionListResponse {
//   transactions: Transaction[];
//   totalCount: number;
//   page: number;
//   pageSize: number;
//   totalPages: number;
// }

function TransactionsPageContent() {
  const { isAuthenticated, isLoading } = useAuth();
  const { features } = useFeatures();
  const { isMobile } = useDeviceDetect();
  const router = useRouter();
  const t = useTranslations('transactions');
  const tCommon = useTranslations('common');
  const tFilters = useTranslations('transactions.filters');
  const tToasts = useTranslations('toasts');
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
    reconciliationFilter,
    setReconciliationFilter,
    sortBy,
    setSortBy,
    sortDirection,
    setSortDirection,
  } = useTransactionFilters();

  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [showFilters, setShowFilters] = useState(false);
  const [hasUnreviewed, setHasUnreviewed] = useState(false);
  const [showReviewAllDialog, setShowReviewAllDialog] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState<{ show: boolean; transactionId?: number }>({ show: false });
  const [showDuplicatesModal, setShowDuplicatesModal] = useState(false);
  const [showTransfersModal, setShowTransfersModal] = useState(false);
  const [accounts, setAccounts] = useState<Array<{ id: number; name: string }>>([]);
  const [openMenuId, setOpenMenuId] = useState<number | null>(null);
  const [expandedTransfers, setExpandedTransfers] = useState<Set<string>>(new Set());
  const [createTransferForTransaction, setCreateTransferForTransaction] = useState<Transaction | null>(null);
  const [hasAkahuConnection, setHasAkahuConnection] = useState<boolean>(false);
  const [isSyncing, setIsSyncing] = useState(false);

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

  // Helper function to get display text for current date filter
  const getActiveDateRangeText = useCallback(() => {
    if (dateFilter === 'all') return null;
    
    const range = getDateRangeFromFilter(dateFilter);
    if (!range.start || !range.end) return null;
    
    const startDate = new Date(range.start);
    const endDate = new Date(range.end);
    
    return `${startDate.toLocaleDateString()} - ${endDate.toLocaleDateString()}`;
  }, [dateFilter, getDateRangeFromFilter]);

  // Helper function to determine if transaction is a transfer
  const isTransfer = useCallback((transaction: Transaction): boolean => {
    return !!transaction.transferId && transaction.transferId !== null;
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

  const handleReverseTransfer = async (transferId: string) => {
    try {
      // Find the transfer group in the current data
      const transferGroup = groupedTransactions.find(item => 
        'isTransferGroup' in item && item.transferId === transferId
      );
      
      if (!transferGroup || !('transactions' in transferGroup)) {
        toast.error(t('transferNotFound'));
        return;
      }

      // Call API to reverse the transfer
      await apiClient.reverseTransfer(transferId);

      toast.success(t('transferReversed'));

      // Refresh the transactions list
      await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
    } catch (error) {
      console.error('Failed to reverse transfer:', error);
      toast.error(t('failedToReverseTransfer'));
    }
  };

  // Group transfers together for display
  const groupedTransactions = React.useMemo(() => {
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

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const fetchTransactions = useCallback(async (page = 1, search = '', filter = transferFilter, categoryId = selectedCategoryId, accountId = selectedAccountId, reviewStatus = reviewFilter, currentDateFilter = dateFilter, currentTypeFilter = typeFilter, currentReconciliationFilter = reconciliationFilter, currentSortBy = sortBy, currentSortDirection = sortDirection) => {
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
        sortBy?: string;
        sortDirection?: string;
      } = {
        page,
        pageSize: 20,
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
      if (accountId) {
        params.accountId = parseInt(accountId);
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

      // Add sort params
      if (currentSortBy) {
        params.sortBy = currentSortBy;
      }
      if (currentSortDirection) {
        params.sortDirection = currentSortDirection;
      }

      const response = await apiClient.getTransactions(params) as {
        transactions: Transaction[];
        totalPages: number;
        totalCount: number;
        page: number;
      };
      
      setTransactions(response?.transactions || []);
      setTotalPages(response?.totalPages || 1);
      setTotalCount(response?.totalCount || 0);
      setCurrentPage(response?.page || 1);
      
      // Check if any transactions need review
      const unreviewed = response?.transactions?.some((t: Transaction) => !t.isReviewed) || false;
      setHasUnreviewed(unreviewed);
    } catch (error) {
      console.error('Failed to fetch transactions:', error);
      setTransactions([]);
    } finally {
      setLoading(false);
    }
  }, [getDateRangeFromFilter, setCurrentPage, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection]);

  useEffect(() => {
    if (isAuthenticated) {
      fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, startDate, endDate, reconciliationFilter, sortBy, sortDirection]);

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
  }, [setAllCategories]);

  const loadAccounts = useCallback(async () => {
    try {
      const accountsData = await apiClient.getAccounts() as Array<{ id: number; name: string }>;
      setAccounts(accountsData || []);
    } catch (error) {
      console.error('Failed to load accounts:', error);
      setAccounts([]);
    }
  }, [setAccounts]);

  const loadAllCategories = useCallback(async () => {
    try {
      const categoriesData = await apiClient.getCategories() as Array<{ id: number; name: string; fullPath?: string }>;
      // We need to ensure the categories have the fields required by CategoryPicker
      const formattedCategories = categoriesData.map(c => ({ ...c, type: 0, parentId: null }));
      setAllCategories(formattedCategories || []);
    } catch (error) {
      console.error('Failed to load categories for bulk assignment:', error);
      setAllCategories([]);
    }
  }, [setAllCategories]);

  // Load initial data when authenticated
  useEffect(() => {
    if (isAuthenticated) {
      loadCategories();
      loadAccounts();
      // Load all categories for bulk assignment
      loadAllCategories();
    }
  }, [isAuthenticated, loadCategories, loadAccounts, loadAllCategories]);

  // Check Akahu connection status for mobile overflow menu
  useEffect(() => {
    const checkAkahuConnection = async () => {
      try {
        const response = await apiClient.hasAkahuCredentials();
        setHasAkahuConnection(response.hasCredentials);
      } catch (error) {
        console.error('Failed to check Akahu connection:', error);
        setHasAkahuConnection(false);
      }
    };

    if (isAuthenticated) {
      checkAkahuConnection();
    }
  }, [isAuthenticated]);

  // Handle sync for mobile overflow menu
  const handleMobileSync = async () => {
    setIsSyncing(true);
    try {
      const results = await apiClient.syncAllConnections();
      const successful = results.filter((r: { isSuccess: boolean }) => r.isSuccess);
      const totalImported = results.reduce((sum: number, r: { transactionsImported: number }) => sum + r.transactionsImported, 0);

      if (results.length === 0) {
        toast.success(tToasts('bankSyncSuccess'));
      } else if (successful.length === results.length) {
        if (totalImported > 0) {
          toast.success(tToasts('syncComplete', { count: totalImported }));
        } else {
          toast.success(tToasts('bankSyncSuccess'));
        }
      } else if (successful.length > 0) {
        toast.warning(tToasts('syncPartial', { successful: successful.length, total: results.length }));
      } else {
        toast.error(tToasts('bankSyncFailed'));
      }

      // Refresh transactions after sync
      await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
    } catch (error) {
      console.error('Failed to sync bank data:', error);
      toast.error(tToasts('bankSyncFailed'));
    } finally {
      setIsSyncing(false);
    }
  };

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
      await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
      setSelectedTransactionIds(new Set());
      setIsSelectionMode(false);
    } catch (err) {
      console.error('Bulk category assignment failed:', err);
      toast.error(tToasts('error.generic'));
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
    // fetchTransactions will be triggered by the useEffect
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
      await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
      setSelectedTransactionIds(new Set());
      setIsSelectionMode(false);
    } catch (err) {
      console.error('Bulk delete failed:', err);
      toast.error(tToasts('error.generic'));
    } finally {
      setBulkCategorizing(false);
      setShowBulkDeleteDialog(false);
    }
  };

  // Note: Moving transactions between accounts is not currently supported by the API
  // This feature would need backend support to modify the account association

  const handleDelete = async () => {
    if (deleteConfirm.transactionId) {
      try {
        await apiClient.deleteTransaction(deleteConfirm.transactionId);
        await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
        toast.success(t('deletedSuccess'));
      } catch (error) {
        console.error('Failed to delete transaction:', error);
        toast.error(t('failedToDeleteTransaction'));
      } finally {
        setDeleteConfirm({ show: false });
      }
    }
  };

  const handleReviewAll = async () => {
    try {
      const result = await apiClient.reviewAllTransactions();
      if (result.success) {
        toast.success(t('transactionsReviewed', { count: result.reviewedCount }));
        await fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
      } else {
        toast.error(t('failedToReviewAll'));
      }
    } catch (error) {
      console.error('Failed to review all transactions:', error);
      toast.error(t('failedToReviewAll'));
    } finally {
      setShowReviewAllDialog(false);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <WalletIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      
      <main className="container-responsive py-4 sm:py-6 lg:py-8">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900">
                {t('title')}
              </h1>
              <div className="flex items-center gap-2 mt-1">
                <p className="text-gray-600">
                  {totalCount > 0 ? t('count', { count: totalCount }) : t('noTransactions')}
                </p>
                {getActiveDateRangeText() && (
                  <span className="px-2 py-1 bg-primary-100 text-primary-700 rounded-md text-xs font-medium">
                    {getActiveDateRangeText()}
                  </span>
                )}
              </div>
            </div>
            
            <div className="flex gap-2">
              {!isSelectionMode ? (
                <>
                  {/* Desktop: Full layout with all buttons visible */}
                  <div className="hidden md:flex gap-2">
                    <AkahuSyncButton
                      onSyncComplete={() => fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection)}
                    />

                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => setShowFilters(!showFilters)}
                      className="flex items-center gap-2"
                    >
                      <FunnelIcon className="w-4 h-4" />
                      {tCommon('filters')}
                    </Button>

                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={toggleSelectionMode}
                      className="flex items-center gap-2"
                    >
                      <CheckIcon className="w-4 h-4" />
                      {t('select')}
                    </Button>

                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button
                          variant="secondary"
                          size="sm"
                          className="flex items-center gap-2"
                        >
                          <EllipsisVerticalIcon className="w-4 h-4" />
                          {t('more')}
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="w-48 bg-white shadow-lg border border-gray-200 rounded-lg">
                        {features.aiCategorization && (
                          <DropdownMenuItem asChild>
                            <Link href="/import/ai-csv" className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 cursor-pointer">
                              <SparklesIcon className="w-4 h-4" />
                              <span>{t('aiCsvImport')}</span>
                            </Link>
                          </DropdownMenuItem>
                        )}

                        <DropdownMenuItem asChild>
                          <Link href="/import" className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 cursor-pointer">
                            <DocumentArrowUpIcon className="w-4 h-4" />
                            <span>{t('ofxImport')}</span>
                          </Link>
                        </DropdownMenuItem>

                        <DropdownMenuItem asChild>
                          <Link href="/transactions/categorize" className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 cursor-pointer">
                            <TagIcon className="w-4 h-4" />
                            <span>{t('categorize')}</span>
                          </Link>
                        </DropdownMenuItem>

                        <DropdownMenuItem
                          onClick={() => setShowDuplicatesModal(true)}
                          className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 cursor-pointer"
                        >
                          <DocumentArrowUpIcon className="w-4 h-4 rotate-180" />
                          <span>{t('findDuplicates')}</span>
                        </DropdownMenuItem>

                        <DropdownMenuItem
                          onClick={() => setShowTransfersModal(true)}
                          className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 cursor-pointer"
                        >
                          <ArrowsRightLeftIcon className="w-4 h-4" />
                          <span>{t('manageTransfers')}</span>
                        </DropdownMenuItem>

                        {hasUnreviewed && (
                          <>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              onClick={() => setShowReviewAllDialog(true)}
                              className="flex items-center gap-2 px-3 py-2 hover:bg-orange-50 cursor-pointer text-orange-600"
                            >
                              <EyeIcon className="w-4 h-4" />
                              <span>{t('reviewAll')}</span>
                            </DropdownMenuItem>
                          </>
                        )}
                      </DropdownMenuContent>
                    </DropdownMenu>

                    <AddTransactionButton
                      onSuccess={() => fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection)}
                      className="btn-sm"
                    />
                  </div>

                  {/* Mobile: Compact layout with filter button and overflow menu */}
                  <div className="flex md:hidden gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => setShowFilters(!showFilters)}
                      aria-label={tCommon('filters')}
                    >
                      <FunnelIcon className="w-4 h-4" />
                    </Button>

                    <MobileActionsOverflow
                      actions={[
                        {
                          id: 'sync',
                          label: isSyncing ? t('akahuSync.syncing') : t('akahuSync.refresh'),
                          icon: <ArrowPathIcon className={`w-4 h-4 ${isSyncing ? 'animate-spin' : ''}`} />,
                          onClick: handleMobileSync,
                          show: hasAkahuConnection,
                          disabled: isSyncing,
                        },
                        {
                          id: 'select',
                          label: t('select'),
                          icon: <CheckIcon className="w-4 h-4" />,
                          onClick: toggleSelectionMode,
                        },
                        {
                          id: 'aiImport',
                          label: t('aiCsvImport'),
                          icon: <SparklesIcon className="w-4 h-4" />,
                          href: '/import/ai-csv',
                          show: features.aiCategorization,
                        },
                        {
                          id: 'ofxImport',
                          label: t('ofxImport'),
                          icon: <DocumentArrowUpIcon className="w-4 h-4" />,
                          href: '/import',
                        },
                        {
                          id: 'categorize',
                          label: t('categorize'),
                          icon: <TagIcon className="w-4 h-4" />,
                          href: '/transactions/categorize',
                        },
                        {
                          id: 'duplicates',
                          label: t('findDuplicates'),
                          icon: <DocumentArrowUpIcon className="w-4 h-4 rotate-180" />,
                          onClick: () => setShowDuplicatesModal(true),
                        },
                        {
                          id: 'transfers',
                          label: t('manageTransfers'),
                          icon: <ArrowsRightLeftIcon className="w-4 h-4" />,
                          onClick: () => setShowTransfersModal(true),
                        },
                        {
                          id: 'reviewAll',
                          label: t('reviewAll'),
                          icon: <EyeIcon className="w-4 h-4" />,
                          onClick: () => setShowReviewAllDialog(true),
                          show: hasUnreviewed,
                          variant: 'danger',
                        },
                      ]}
                    />
                  </div>
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

          {/* Search Bar */}
          <div className="mt-4 relative">
            <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
            <Input
              type="text"
              placeholder={t('searchPlaceholder')}
              value={searchTerm}
              onChange={(e) => handleSearch(e.target.value)}
              className="pl-10 w-full"
            />
          </div>

          {/* Filters (collapsed by default on mobile) */}
          {showFilters && (
            <Card className="mt-4 bg-white/90 backdrop-blur-xs border-0 shadow-lg overflow-visible">
              <CardContent className="p-4 overflow-visible">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 overflow-visible">
                  <div className="overflow-visible">
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {tFilters('dateRange')}
                    </label>

                    {/* Quick date filters */}
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

                    {/* Custom date inputs - only show when custom is selected */}
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
                      {tFilters('category')}
                    </label>
                    <CategoryFilter
                      value={selectedCategoryId}
                      onChange={(categoryId) => setSelectedCategoryId(categoryId)}
                      categories={allCategories}
                      placeholder={tFilters('allCategories')}
                    />
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {tFilters('account')}
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

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {tFilters('type')}
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
                      {tFilters('sortBy')}
                    </label>
                    <select
                      className="select text-sm"
                      value={`${sortBy}-${sortDirection}`}
                      onChange={(e) => {
                        const [field, direction] = e.target.value.split('-') as [SortField, SortDirection];
                        setSortBy(field);
                        setSortDirection(direction);
                      }}
                    >
                      <option value="transactionDate-desc">{tFilters('dateNewest')}</option>
                      <option value="transactionDate-asc">{tFilters('dateOldest')}</option>
                      <option value="amount-desc">{tFilters('amountHighest')}</option>
                      <option value="amount-asc">{tFilters('amountLowest')}</option>
                      <option value="description-asc">{tFilters('descriptionAZ')}</option>
                      <option value="description-desc">{tFilters('descriptionZA')}</option>
                      <option value="category-asc">{tFilters('categoryAZ')}</option>
                      <option value="category-desc">{tFilters('categoryZA')}</option>
                    </select>
                  </div>
                </div>

                <div className="flex justify-end mt-4 gap-2">
                  <Button variant="secondary" size="sm" onClick={() => {
                    setTransferFilter('all');
                    setReviewFilter('all');
                    setReconciliationFilter('all');
                    setTypeFilter('all');
                    setSelectedCategoryId('');
                    setSelectedAccountId('');
                    setDateFilter('all');
                    setStartDate('');
                    setEndDate('');
                    setSortBy('transactionDate');
                    setSortDirection('desc');
                    setCurrentPage(1);
                  }}>
                    {tCommon('clear')}
                  </Button>
                  <Button size="sm" onClick={handleApplyFilters}>{t('applyFilters')}</Button>
                </div>
              </CardContent>
            </Card>
          )}
        </div>

        {/* Bulk Action Toolbar */}
        {isSelectionMode && selectedTransactionIds.size > 0 && (
          <Card className="mb-4 bg-white/95 backdrop-blur-sm border-0 shadow-lg sticky top-4 z-[5]">
            <CardContent className={`${isMobile ? 'p-3' : 'p-4'}`}>
              <div className={`flex items-center ${isMobile ? 'gap-2' : 'justify-between'}`}>
                <div className="flex items-center gap-2">
                  <span className={`${isMobile ? 'px-2 py-1 bg-primary-100 text-primary-700 rounded-full text-xs font-medium' : 'text-sm font-medium text-gray-700'}`}>
                    {tCommon('selected', { count: selectedTransactionIds.size })}
                  </span>
                </div>

                <div className="flex-1 flex items-center justify-end gap-2">
                  {bulkCategorizing ? (
                    <div className="flex items-center gap-2">
                      <div className="w-4 h-4 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" />
                      <span className={`${isMobile ? 'text-xs' : 'text-sm'} text-gray-600`}>
                        {t('processing')}
                      </span>
                    </div>
                  ) : (
                    <div className={`flex items-center gap-2 ${isMobile ? 'flex-1' : ''}`}>
                      {/* Category Assignment */}
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

                      {/* Bulk Delete Button */}
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
              <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('noTransactionsFiltered')}</h3>
              <p className="text-gray-600 mb-6">
                {searchTerm ? t('adjustSearchTerms') : t('getStarted')}
              </p>
              <div className="flex justify-center">
                <Link href="/transactions/new">
                  <Button className="flex items-center gap-2">
                    <PlusIcon className="w-4 h-4" />
                    {t('addFirst')}
                  </Button>
                </Link>
              </div>
            </CardContent>
          ) : (
            <CardContent className="p-0">
              {/* Mobile-First Transaction Cards */}
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
                            {/* Transfer Icon - Desktop Only */}
                            {!isMobile && (
                              <div className="w-12 h-12 rounded-xl flex items-center justify-center shadow-sm bg-gradient-to-br from-blue-100 to-blue-200">
                                <svg className="w-6 h-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                                </svg>
                              </div>
                            )}
                            
                            {/* Transfer Details */}
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
                                
                                {/* Amount and Expand Arrow */}
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
                        
                        {/* Expanded Details */}
                        {isExpanded && (
                          <div className="bg-gray-50 border-t border-gray-100">
                            <div className="p-4 space-y-3">
                              {/* Individual Transactions */}
                              {item.transactions.map((trans) => (
                                <div key={trans.id} className={`flex items-center justify-between p-3 bg-white rounded-lg border ${trans.isTransferSource ? 'border-red-200' : 'border-green-200'}`}>
                                  <div className="flex items-center gap-3">
                                    <div className={`w-10 h-10 rounded-full flex items-center justify-center ${trans.isTransferSource ? 'bg-red-100' : 'bg-green-100'}`}>
                                      {trans.isTransferSource ? (
                                        <svg className="w-5 h-5 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 8l4-4m0 0l-4-4m4 4H3" />
                                        </svg>
                                      ) : (
                                        <svg className="w-5 h-5 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16l-4-4m0 0l4-4m-4 4h18" />
                                        </svg>
                                      )}
                                    </div>
                                    <div>
                                      <p className="text-sm font-medium text-gray-900">
                                        {trans.accountName}
                                      </p>
                                      <p className="text-xs text-gray-500">
                                        {t('transactionNumber', { id: trans.id })}
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
                              
                              {/* Actions */}
                              <div className="flex justify-end gap-2 pt-2">
                                <Button
                                  size="sm"
                                  variant="secondary"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    handleReverseTransfer(item.transferId);
                                  }}
                                  className="flex items-center gap-1"
                                >
                                  <ArrowsRightLeftIcon className="w-4 h-4" />
                                  {t('reverseDirection')}
                                </Button>
                                <ContextualTransactionLink
                                  transactionId={item.transactions[0].id}
                                  onClick={(e) => e.stopPropagation()}
                                >
                                  <Button size="sm" variant="primary">
                                    {t('viewSourceTransaction')}
                                  </Button>
                                </ContextualTransactionLink>
                              </div>
                            </div>
                          </div>
                        )}
                      </div>
                    );
                  }

                  // Handle regular transactions
                  const transaction = item as Transaction;
                  const isIncome = transaction.amount > 0;
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
                                    : 'border-gray-300'
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
                            // In selection mode, only handle clicks that aren't on the checkbox
                            const target = e.target as HTMLElement;
                            const checkboxArea = target.closest('[data-checkbox="true"]');
                            if (!checkboxArea) {
                              e.preventDefault();
                              toggleTransactionSelection(transaction.id);
                            }
                          } else {
                            // Navigate to transaction details on both mobile and desktop
                            router.push(createTransactionDetailUrl(transaction.id, window.location.href));
                          }
                        }}
                        onTouchStart={(e) => {
                          if (isMobile && !isSelectionMode) {
                            // Store touch start info for long press detection
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
                              // Cancel if finger moved too far (more than 10px)
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
                              
                              // Check if touch ended on the same element or a child
                              const endTarget = document.elementFromPoint(
                                endEvent.changedTouches[0].clientX,
                                endEvent.changedTouches[0].clientY
                              );
                              
                              const isValidTarget = endTarget && (
                                startTarget.contains(endTarget) || 
                                endTarget === startTarget
                              );
                              
                              // If it's a short tap (less than 500ms) and ended on valid target, navigate
                              if (touchDuration < 500 && isValidTarget && !isSelectionMode) {
                                // Check if tap was on checkbox area
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
                          {/* Checkbox (Selection Mode) */}
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
                          
                          {/* Transaction Icon - Desktop Only */}
                          {!isMobile && (
                            <div className={`w-12 h-12 rounded-xl flex items-center justify-center shadow-sm ${
                              isTransfer(transaction)
                                ? 'bg-gradient-to-br from-blue-100 to-blue-200'
                                : isIncome 
                                  ? 'bg-gradient-to-br from-success-100 to-success-200' 
                                  : 'bg-gradient-to-br from-gray-100 to-gray-200'
                            }`}>
                              {isTransfer(transaction) ? (
                                <ArrowsRightLeftIcon className="w-6 h-6 text-blue-600" />
                              ) : isIncome ? (
                                <ArrowTrendingUpIcon className="w-6 h-6 text-success-600" />
                              ) : (
                                <ArrowTrendingDownIcon className="w-6 h-6 text-gray-600" />
                              )}
                            </div>
                          )}
                          
                          {/* Transaction Details */}
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
                                
                                {isMobile ? (
                                  /* Mobile: Single line - category moved to right side */
                                  <div className="flex items-center gap-3 mt-1 text-xs text-gray-500">
                                    <span className="flex items-center gap-1">
                                      <CalendarIcon className="w-3 h-3" />
                                      {formatDate(transaction.transactionDate)}
                                    </span>
                                    
                                    {transaction.accountName && (
                                      <span className="flex items-center gap-1">
                                        <BuildingOffice2Icon className="w-3 h-3" />
                                        {transaction.accountName}
                                      </span>
                                    )}
                                  </div>
                                ) : (
                                  /* Desktop: Single line */
                                  <div className="flex items-center gap-3 mt-1 text-xs text-gray-500">
                                    <span className="flex items-center gap-1">
                                      <CalendarIcon className="w-3 h-3" />
                                      {formatDate(transaction.transactionDate)}
                                    </span>
                                    
                                    {transaction.categoryName && (
                                      <span className="flex items-center gap-1">
                                        <TagIcon className="w-3 h-3" />
                                        {transaction.categoryName}
                                      </span>
                                    )}
                                    
                                    {transaction.accountName && (
                                      <span className="flex items-center gap-1">
                                        <BuildingOffice2Icon className="w-3 h-3" />
                                        {transaction.accountName}
                                      </span>
                                    )}
                                  </div>
                                )}
                              </div>
                              
                              {/* Amount and Actions */}
                              <div className="flex items-center gap-3 ml-3">
                                <div className="text-right">
                                  <p className={`text-sm font-bold ${
                                    isIncome ? 'text-success-600' : 'text-red-600'
                                  }`}>
                                    {formatCurrency(transaction.amount)}
                                  </p>
                                  
                                  {isMobile ? (
                                    /* Mobile: Show category if available, no review button for cleaner layout */
                                    transaction.categoryName && (
                                      <div className="flex items-center justify-end gap-1 mt-1">
                                        <TagIcon className="w-3 h-3 text-gray-400" />
                                        <span className="text-xs text-gray-600 font-medium">{transaction.categoryName}</span>
                                      </div>
                                    )
                                  ) : (
                                    /* Desktop: Show review button */
                                    <TransactionReviewButton 
                                      transactionId={transaction.id}
                                      isReviewed={transaction.isReviewed}
                                      onReviewComplete={() => fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection)}
                                    />
                                  )}
                                </div>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                      
                      {/* Action Menu - Only show when not in selection mode and not on mobile */}
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
                      <div className="mt-2 mx-4 mb-4">
                        <InlineTransferCreator
                          sourceTransaction={{
                            id: transaction.id,
                            amount: transaction.amount,
                            transactionDate: transaction.transactionDate,
                            description: transaction.userDescription || transaction.description,
                            accountName: transaction.accountName || 'Unknown Account',
                            accountId: transaction.accountId || 0
                          }}
                          onCancel={() => setCreateTransferForTransaction(null)}
                          onSuccess={() => {
                            setCreateTransferForTransaction(null);
                            fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection);
                            toast.success(t('transferCreatedSuccess'));
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
                  <div className="flex items-center justify-between">
                    <p className="text-sm text-gray-700">
                      {t('page', { current: currentPage, total: totalPages })}
                    </p>

                    <div className="flex gap-2">
                      <Button
                        variant="secondary"
                        size="sm"
                        disabled={currentPage === 1}
                        onClick={() => {
                          setCurrentPage(currentPage - 1);
                          setShowFilters(false);
                        }}
                      >
                        {tCommon('previous')}
                      </Button>
                      <Button
                        variant="secondary"
                        size="sm"
                        disabled={currentPage === totalPages}
                        onClick={() => {
                          setCurrentPage(currentPage + 1);
                          setShowFilters(false);
                        }}
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
      </main>

      {/* Floating Action Button - Mobile only, hidden in selection mode */}
      {!isSelectionMode && (
        <FloatingActionButton
          onClick={() => router.push('/transactions/new')}
          icon={<PlusIcon className="w-6 h-6" />}
          label={t('addTransaction')}
        />
      )}

      {/* Confirmation Dialogs */}

      {/* Bulk Delete Confirmation Dialog */}
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
        isOpen={showReviewAllDialog}
        onClose={() => setShowReviewAllDialog(false)}
        onConfirm={handleReviewAll}
        title={t('markAllReviewed')}
        description={t('markAllReviewedConfirm')}
        confirmText={t('reviewAll')}
        cancelText={tCommon('cancel')}
        variant="default"
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

      <DuplicatesModal
        isOpen={showDuplicatesModal}
        onClose={() => setShowDuplicatesModal(false)}
        onRefresh={() => fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection)}
      />

      <TransfersModal
        isOpen={showTransfersModal}
        onClose={() => setShowTransfersModal(false)}
        onRefresh={() => fetchTransactions(currentPage, searchTerm, transferFilter, selectedCategoryId, selectedAccountId, reviewFilter, dateFilter, typeFilter, reconciliationFilter, sortBy, sortDirection)}
      />
    </div>
  );
}

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';

export default function TransactionsPage() {
  const tCommon = useTranslations('common');
  return (
    <Suspense fallback={<div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center"><div className="text-gray-700 font-medium">{tCommon('loading')}</div></div>}>
      <TransactionsPageContent />
    </Suspense>
  );
}
