'use client';

import { useAuth } from '@/contexts/auth-context';
import { AiSuggestionsProvider } from '@/contexts/ai-suggestions-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState, useCallback, useMemo } from 'react';
import React from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { formatCurrency, formatDate } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { 
  ArrowLeftIcon,
  MagnifyingGlassIcon,
  FunnelIcon,
  CalendarIcon,
  TagIcon,
  BuildingOffice2Icon,
  WalletIcon,
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
  CheckIcon
} from '@heroicons/react/24/outline';
import { CategoryPicker } from '@/components/forms/category-picker';
import { CategorizationRibbon } from '@/components/forms/categorization-ribbon';
import { Pagination, usePagination } from '@/components/ui/pagination';
import { useDeviceDetect } from '@/hooks/use-device-detect';
import { useTransactionCandidates } from '@/hooks/use-transaction-candidates';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  categoryName?: string;
  categoryColor?: string;
  accountName?: string;
  status: number;
  isReviewed: boolean;
  transferId?: string;
  relatedTransactionId?: number;
  isTransferSource?: boolean;
  type: number;
  categoryId?: number;
}

export default function CategorizePage() {
  const { isAuthenticated, isLoading } = useAuth();
  const { isMobile } = useDeviceDetect();
  const router = useRouter();
  const t = useTranslations('transactions');
  const tFilters = useTranslations('transactions.filters');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const { 
    paginationInfo, 
    handlePageChange, 
    handlePageSizeChange, 
    updatePagination,
    currentPage,
    pageSize 
  } = usePagination(25); // Use 25 as default page size
  const [showFilters, setShowFilters] = useState(false);
  const [accounts, setAccounts] = useState<Array<{ id: number; name: string }>>([]);
  const [selectedAccountId, setSelectedAccountId] = useState<string>('');
  const [showAll, setShowAll] = useState(false);
  const [allCategories, setAllCategories] = useState<Array<{ id: number; name: string; fullPath?: string; type: number; parentId: number | null }>>([]);
  const [categorizedCount, setCategorizedCount] = useState(0);
  const [batchCategorizationKey, setBatchCategorizationKey] = useState(0);

  // Memoize query parameters to prevent infinite re-renders
  const candidatesQueryParams = useMemo(() => ({
    page: currentPage,
    pageSize: pageSize,
    searchTerm: searchTerm || undefined,
    accountId: selectedAccountId ? parseInt(selectedAccountId) : undefined,
    needsCategorization: !showAll ? true : undefined,
    includeTransfers: false,
    onlyWithCandidates: false // Get all transactions, even those without candidates
  }), [currentPage, pageSize, searchTerm, selectedAccountId, showAll]);

  // Memoize filter criteria for rule auto-categorization
  const ruleFilterCriteria = useMemo(() => ({
    accountIds: selectedAccountId ? [parseInt(selectedAccountId)] : undefined,
    searchText: searchTerm || undefined,
    onlyUnreviewed: !showAll,
    excludeTransfers: true
  }), [selectedAccountId, searchTerm, showAll]);

  // Batch fetch AI suggestions for all visible transactions
  const transactionCandidates = useTransactionCandidates({
    queryParams: candidatesQueryParams,
    enabled: isAuthenticated && transactions.length > 0
  });

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const fetchUncategorizedTransactions = useCallback(async (page = 1, search = '', accountId = selectedAccountId) => {
    try {
      setLoading(true);
      const params: {
        page: number;
        pageSize: number;
        searchTerm?: string;
        accountId?: number;
        needsCategorization?: boolean;
        includeTransfers?: boolean;
      } = {
        page,
        pageSize: pageSize,
        searchTerm: search || undefined,
        includeTransfers: false, // Exclude transfers from categorization
      };

      // Handle uncategorized/all filtering
      if (!showAll) {
        // Default: show only uncategorized transactions
        params.needsCategorization = true;
      }
      // When showAll is true, send no filter — show all transactions

      // Add account filter
      if (accountId) {
        params.accountId = parseInt(accountId);
      }

      // Get uncategorized transactions from API (server-side filtering)
      const response = await apiClient.getTransactions(params) as {
        transactions: Transaction[];
        totalPages: number;
        totalCount: number;
        page: number;
      };
      
      // No frontend filtering needed - API handles uncategorized filtering
      setTransactions(response?.transactions || []);
      updatePagination({
        totalCount: response?.totalCount || 0,
        totalPages: response?.totalPages || 1,
        page: response?.page || 1
      });
    } catch (error) {
      console.error('Failed to fetch uncategorized transactions:', error);
      setTransactions([]);
    } finally {
      setLoading(false);
    }
  }, [selectedAccountId, pageSize, showAll, updatePagination]);

  useEffect(() => {
    if (isAuthenticated) {
      fetchUncategorizedTransactions(currentPage, searchTerm, selectedAccountId);
    }
  }, [isAuthenticated, currentPage, pageSize, searchTerm, selectedAccountId, showAll, fetchUncategorizedTransactions]);

  const handleSearch = (value: string) => {
    setSearchTerm(value);
    handlePageChange(1); // Reset to first page
    fetchUncategorizedTransactions(1, value, selectedAccountId);
  };

  const loadCategories = async () => {
    try {
      const categoriesData = await apiClient.getCategories() as Array<{ id: number; name: string; fullPath?: string; type: number; parentId: number | null }>;
      setAllCategories(categoriesData || []);
    } catch (error) {
      console.error('Failed to load categories:', error);
      setAllCategories([]);
    }
  };

  const loadAccounts = async () => {
    try {
      const accountsData = await apiClient.getAccounts() as Array<{ id: number; name: string }>;
      setAccounts(accountsData || []);
    } catch (error) {
      console.error('Failed to load accounts:', error);
      setAccounts([]);
    }
  };

  // Load initial data when authenticated
  useEffect(() => {
    if (isAuthenticated) {
      loadCategories();
      loadAccounts();
    }
  }, [isAuthenticated]);

  const handleApplyFilters = () => {
    handlePageChange(1);
    fetchUncategorizedTransactions(1, searchTerm, selectedAccountId);
    setShowFilters(false);
  };

  const handleBatchCategorizationComplete = () => {
    // Force CategoryPicker components to refresh and show AI suggestions
    setBatchCategorizationKey(prev => prev + 1);
    // Invalidate and refresh the cached candidates data to show fresh suggestions
    transactionCandidates.invalidateCandidates();
    toast.success(tToasts('aiSuggestionsAvailable'));
  };

  const handleCategorizeTransaction = async (transactionId: number, categoryId: string | number) => {
    try {
      const transaction = transactions.find(t => t.id === transactionId);
      if (!transaction) return;

      // Map status to enum values
      const statusMap: Record<string, number> = {
        'pending': 1,
        'cleared': 2, 
        'reconciled': 3,
        'cancelled': 4
      };
      
      // Convert status to number if it's a string
      let statusValue = 2; // Default to Cleared
      if (typeof transaction.status === 'string') {
        statusValue = statusMap[(transaction.status as string).toLowerCase()] || 2;
      } else if (typeof transaction.status === 'number') {
        statusValue = transaction.status;
      }

      // Update the transaction category
      await apiClient.updateTransaction(transactionId, {
        ...transaction,
        categoryId: Number(categoryId),
        transactionDate: transaction.transactionDate,
        status: statusValue,
      });

      // Mark as reviewed when categorized
      await apiClient.reviewTransaction(transactionId);

      // Remove from the list since it's now categorized
      setTransactions(prev => prev.filter(t => t.id !== transactionId));
      setCategorizedCount(prev => prev + 1);
      
      // Update pagination info
      updatePagination({
        totalCount: paginationInfo.totalCount - 1,
        totalPages: Math.ceil((paginationInfo.totalCount - 1) / paginationInfo.pageSize),
        page: paginationInfo.currentPage
      });
      
      toast.success(tToasts('transactionCategorized'));
    } catch (error) {
      console.error('Failed to categorize transaction:', error);
      toast.error(tToasts('transactionCategorizeFailed'));
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
      
      <AiSuggestionsProvider>
        <main className="container-responsive py-4 sm:py-6 lg:py-8">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          {/* Navigation Bar */}
          <div className="flex items-center justify-between mb-6">
            <Button 
              variant="secondary" 
              size="sm" 
              className="flex items-center gap-2"
              onClick={() => router.back()}
            >
              <ArrowLeftIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{tCommon('back')}</span>
            </Button>
          </div>
          
          {/* Page Title */}
          <div className="text-center mb-8">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('categorizeTitle')}
            </h1>
            <div className="flex flex-col items-center gap-2">
              <div className="flex items-center justify-center gap-4 text-sm text-gray-600">
                <span>
                  {t('categorizeCountLabel', {
                    count: paginationInfo.totalCount,
                    status: showAll ? t('categorizeFilter.allTransactions') : t('categorizeFilter.uncategorized')
                  })}
                </span>
                {categorizedCount > 0 && (
                  <span className="px-2 py-1 bg-success-100 text-success-700 rounded-md font-medium">
                    {t('categorizedLabel', { count: categorizedCount })}
                  </span>
                )}
              </div>
              <p className="text-xs text-gray-500">
                {t('categorizeTransferExclusion')}
              </p>
            </div>
          </div>
        </div>

        {/* Search and Filters */}
        <div className="mb-6">
          <div className="flex gap-2 mb-4">
            <div className="flex-1 relative">
              <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
              <Input
                type="text"
                placeholder={t('searchPlaceholder')}
                value={searchTerm}
                onChange={(e) => handleSearch(e.target.value)}
                className="pl-10 w-full"
              />
            </div>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setShowFilters(!showFilters)}
              className="flex items-center gap-2"
            >
              <FunnelIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{tCommon('filters')}</span>
            </Button>
          </div>

          {/* Filters */}
          {showFilters && (
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {tCommon('account')}
                    </label>
                    <Select
                      className="w-full text-sm"
                      value={selectedAccountId}
                      onChange={(e) => setSelectedAccountId(e.target.value)}
                    >
                      <option value="">{tFilters('allAccounts')}</option>
                      {accounts.map((account) => (
                        <option key={account.id} value={account.id}>
                          {account.name}
                        </option>
                      ))}
                    </Select>
                  </div>
                  
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {t('categorizeFilter.label')}
                    </label>
                    <Select
                      className="w-full text-sm"
                      value={showAll ? 'all' : 'uncategorized'}
                      onChange={(e) => setShowAll(e.target.value === 'all')}
                    >
                      <option value="uncategorized">{t('categorizeFilter.uncategorized')}</option>
                      <option value="all">{t('categorizeFilter.allTransactions')}</option>
                    </Select>
                  </div>
                </div>
                
                <div className="flex justify-end mt-4 gap-2">
                  <Button variant="secondary" size="sm" onClick={() => {
                    setSelectedAccountId('');
                    setShowAll(false);
                    handlePageChange(1);
                    fetchUncategorizedTransactions(1, searchTerm, '');
                  }}>
                    {tCommon('clear')}
                  </Button>
                  <Button size="sm" onClick={handleApplyFilters}>{t('applyFilters')}</Button>
                </div>
              </CardContent>
            </Card>
          )}
        </div>

        {/* Smart Categorization Ribbon */}
        {transactions.length > 0 && !showAll && (
          <CategorizationRibbon
            transactions={transactions}
            onTransactionCategorized={handleCategorizeTransaction}
            onRefresh={() => fetchUncategorizedTransactions(currentPage, searchTerm, selectedAccountId)}
            onBatchCategorizationComplete={handleBatchCategorizationComplete}
            filterCriteria={ruleFilterCriteria}
          />
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
              {/* Show success state only if we've actually categorized transactions or no filters are applied */}
              {(categorizedCount > 0 || (!selectedAccountId && !searchTerm)) ? (
                <>
                  <div className="w-20 h-20 bg-gradient-to-br from-success-400 to-success-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                    <CheckIcon className="w-10 h-10 text-white" />
                  </div>
                  <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('categorizeAllDoneTitle')}</h3>
                  <p className="text-gray-600 mb-6">
                    {categorizedCount > 0 
                      ? t('categorizeAllDoneWithCount', { count: categorizedCount })
                      : t('categorizeAllDoneEmpty')
                    }
                  </p>
                  <div className="flex justify-center">
                    <Link href="/transactions">
                      <Button className="flex items-center gap-2">
                        <ArrowLeftIcon className="w-4 h-4" />
                        {t('backToTransactions')}
                      </Button>
                    </Link>
                  </div>
                </>
              ) : (
                <>
                  <div className="w-20 h-20 bg-gradient-to-br from-gray-400 to-gray-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                    <FunnelIcon className="w-10 h-10 text-white" />
                  </div>
                  <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('categorizeNoMatchesTitle')}</h3>
                  <p className="text-gray-600 mb-6">
                    {t('categorizeNoMatchesDescription', {
                      status: showAll ? t('categorizeFilter.allTransactions') : t('categorizeFilter.uncategorized')
                    })}
                  </p>
                  <div className="flex justify-center gap-2">
                    <Button 
                      variant="secondary" 
                      onClick={() => {
                        setSelectedAccountId('');
                        setSearchTerm('');
                        setShowAll(false);
                        handlePageChange(1);
                        fetchUncategorizedTransactions(1, '', '');
                      }}
                    >
                      {t('clearFilters')}
                    </Button>
                    <Link href="/transactions">
                      <Button className="flex items-center gap-2">
                        <ArrowLeftIcon className="w-4 h-4" />
                        {t('backToTransactions')}
                      </Button>
                    </Link>
                  </div>
                </>
              )}
            </CardContent>
          ) : (
            <CardContent className="p-0">
              {/* Transaction Cards */}
              <div className="divide-y divide-gray-100">
                {transactions.map((transaction) => {
                  const isIncome = transaction.amount > 0;
                  
                  return (
                    <div key={transaction.id} className={`${isMobile ? 'p-3 border-l-4' : 'p-4'} ${isIncome ? 'border-success-500' : 'border-gray-300'} hover:bg-gray-50 transition-colors`}>
                      <div className={`flex items-center ${isMobile ? 'gap-2' : 'gap-3'}`}>
                        {/* Transaction Icon - Desktop Only */}
                        {!isMobile && (
                          <div className={`w-12 h-12 rounded-xl flex items-center justify-center shadow-sm ${
                            isIncome 
                              ? 'bg-gradient-to-br from-success-100 to-success-200' 
                              : 'bg-gradient-to-br from-gray-100 to-gray-200'
                          }`}>
                            {isIncome ? (
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
                              <p className="text-sm font-semibold text-gray-900 truncate">
                                {transaction.userDescription || transaction.description}
                              </p>
                              
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

                                {transaction.categoryName && (
                                  <span className="flex items-center gap-1">
                                    <TagIcon className="w-3 h-3" />
                                    {transaction.categoryName}
                                  </span>
                                )}
                              </div>
                            </div>
                            
                            {/* Amount */}
                            <div className="text-right ml-3">
                              <p className={`text-sm font-bold ${
                                isIncome ? 'text-success-600' : 'text-gray-900'
                              }`}>
                                {isIncome ? '+' : ''}{formatCurrency(Math.abs(transaction.amount))}
                              </p>
                            </div>
                          </div>
                          
                          {/* Enhanced Category Picker with AI */}
                          <div className="mt-3">
                            <CategoryPicker
                              key={`${transaction.id}-${batchCategorizationKey}`}
                              value={transaction.categoryId?.toString() || ""}
                              onChange={(categoryId) => handleCategorizeTransaction(transaction.id, categoryId)}
                              categories={allCategories}
                              placeholder={t('chooseCategory')}
                              transaction={{
                                id: transaction.id,
                                description: transaction.userDescription || transaction.description,
                                amount: transaction.amount
                              }}
                              // Pass batch-fetched AI suggestions and flag to use them exclusively
                              aiSuggestions={transactionCandidates.getSuggestionsForTransaction(transaction.id)}
                              isLoadingAiSuggestions={transactionCandidates.isLoading}
                              showAiSuggestions={true}
                              useExternalAiSuggestions={true}
                              autoApplyHighConfidence={false}
                              confidenceThreshold={0.85}
                              onAiSuggestionApplied={(suggestion) => {
                                toast.success(
                                  tToasts('aiSuggestionApplied', { 
                                    category: suggestion.categoryName, 
                                    confidence: Math.round(suggestion.confidence * 100) 
                                  }),
                                  { duration: 3000 }
                                );
                                // Invalidate candidates for this transaction
                                transactionCandidates.invalidateCandidates();
                              }}
                              onAiSuggestionRejected={(suggestion) => {
                                toast.info(tToasts('aiSuggestionDismissed', { category: suggestion.categoryName }));
                                // Invalidate candidates for this transaction
                                transactionCandidates.invalidateCandidates();
                              }}
                            />
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
              
              {/* Standardized Pagination */}
              <Pagination
                pagination={paginationInfo}
                onPageChange={(page) => {
                  handlePageChange(page);
                  fetchUncategorizedTransactions(page, searchTerm, selectedAccountId);
                }}
                onPageSizeChange={(newPageSize) => {
                  handlePageSizeChange(newPageSize);
                  fetchUncategorizedTransactions(1, searchTerm, selectedAccountId);
                }}
                loading={loading}
                className="border-t-0"
              />
            </CardContent>
          )}
        </Card>
        </main>
      </AiSuggestionsProvider>
    </div>
  );
}
