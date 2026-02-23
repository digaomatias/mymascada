'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { 
  ArrowLeftIcon,
  PencilIcon,
  TrashIcon,
  TagIcon,
  CalendarIcon,
  CurrencyDollarIcon
} from '@heroicons/react/24/outline';
import { renderCategoryIcon } from '@/lib/category-icons';
import { formatCurrency, formatDate } from '@/lib/utils';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface Category {
  id: number;
  name: string;
  description?: string;
  color?: string;
  icon?: string;
  type: number;
  isSystemCategory: boolean;
  isActive: boolean;
  sortOrder: number;
  parentCategoryId?: number;
  parentCategoryName?: string;
  fullPath: string;
  transactionCount: number;
  totalAmount: number;
  createdAt: string;
  updatedAt: string;
}

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  accountName?: string;
  status: number;
  isReviewed: boolean;
}

interface CategoryStats {
  transactionCount: number;
  totalAmount: number;
  averageAmount?: number;
  lastTransactionDate?: string;
}

const categoryTypeColors = {
  1: 'bg-green-100 text-green-800',
  2: 'bg-red-100 text-red-800',
  3: 'bg-blue-100 text-blue-800'
};

export default function CategoryDetailsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const params = useParams();
  const categoryId = params.id as string;
  const t = useTranslations('categories');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');

  const getCategoryTypeLabel = (type: number) => {
    switch (type) {
      case 1: return t('types.income');
      case 2: return t('types.expense');
      case 3: return t('types.transfer');
      default: return '';
    }
  };

  const getDateFilterLabel = (filterType: string) => {
    switch (filterType) {
      case 'all': return t('dateFilters.allTime');
      case 'last7': return t('dateFilters.last7Days');
      case 'thisMonth': return t('dateFilters.thisMonth');
      case 'last30': return t('dateFilters.last30Days');
      case 'last3Months': return t('dateFilters.last3Months');
      default: return '';
    }
  };
  
  const [category, setCategory] = useState<Category | null>(null);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [categoryStats, setCategoryStats] = useState<CategoryStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingTransactions, setLoadingTransactions] = useState(false);
  const [loadingStats, setLoadingStats] = useState(false);
  const [dateFilter, setDateFilter] = useState<'all' | 'last7' | 'thisMonth' | 'last30' | 'last3Months' | 'custom'>('thisMonth');

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const loadCategoryDetails = useCallback(async () => {
    try {
      setLoading(true);
      const categoryData = await apiClient.getCategory(parseInt(categoryId)) as Category;
      setCategory(categoryData);
    } catch (error) {
      console.error('Failed to load category:', error);
      toast.error(tToasts('categoryLoadFailed'));
      router.push('/categories');
    } finally {
      setLoading(false);
    }
  }, [categoryId, router]);

  const loadCategoryTransactions = useCallback(async () => {
    try {
      setLoadingTransactions(true);
      const transactionsData = await apiClient.getTransactions({
        categoryId: parseInt(categoryId),
        pageSize: 20
      }) as { transactions: Transaction[] };
      setTransactions(transactionsData.transactions || []);
    } catch (error) {
      console.error('Failed to load category transactions:', error);
      setTransactions([]);
    } finally {
      setLoadingTransactions(false);
    }
  }, [categoryId]);

  // Helper function to get date range from filter type
  const getDateRangeFromFilter = useCallback((filter: typeof dateFilter) => {
    const now = new Date();
    const todayUTC = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
    
    switch (filter) {
      case 'last7':
        const last7Start = new Date(todayUTC.getTime() - 6 * 24 * 60 * 60 * 1000);
        return {
          start: last7Start.toISOString().split('T')[0],
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
      
      default: // 'all'
        return {
          start: undefined,
          end: undefined
        };
    }
  }, []);

  const loadCategoryStats = useCallback(async () => {
    try {
      setLoadingStats(true);
      const dateRange = getDateRangeFromFilter(dateFilter);
      
      // For now, we'll fetch transactions and calculate stats on frontend
      // TODO: Update backend API to accept date range parameters
      interface TransactionRequestParams {
        categoryId: number;
        pageSize: number;
        startDate?: string;
        endDate?: string;
      }

      const params: TransactionRequestParams = {
        categoryId: parseInt(categoryId),
        pageSize: 1000 // Get all transactions for accurate stats
      };
      
      if (dateRange.start) params.startDate = dateRange.start;
      if (dateRange.end) params.endDate = dateRange.end;
      
      const transactionsData = await apiClient.getTransactions(params) as { transactions: Transaction[] };
      const transactions = transactionsData.transactions || [];
      
      // Calculate stats from transactions
      // For expense categories, show absolute amounts (positive numbers) for better UX
      const totalAmount = transactions.reduce((sum, t) => sum + t.amount, 0);
      const stats: CategoryStats = {
        transactionCount: transactions.length,
        totalAmount: Math.abs(totalAmount), // Show absolute value for expense totals
        averageAmount: transactions.length > 0 ? Math.abs(totalAmount) / transactions.length : 0,
        lastTransactionDate: transactions.length > 0 ? transactions[0]?.transactionDate : undefined
      };
      
      setCategoryStats(stats);
    } catch (error) {
      console.error('Failed to load category stats:', error);
      setCategoryStats({
        transactionCount: 0,
        totalAmount: 0,
        averageAmount: 0
      });
    } finally {
      setLoadingStats(false);
    }
  }, [categoryId, dateFilter, getDateRangeFromFilter]);

  useEffect(() => {
    if (isAuthenticated && categoryId) {
      loadCategoryDetails();
      loadCategoryTransactions();
      loadCategoryStats();
    }
  }, [isAuthenticated, categoryId, loadCategoryDetails, loadCategoryTransactions, loadCategoryStats]);

  useEffect(() => {
    if (isAuthenticated && categoryId) {
      loadCategoryStats();
    }
  }, [dateFilter, isAuthenticated, categoryId, loadCategoryStats]);

  const handleDeleteCategory = async () => {
    if (!category) return;

    if (!confirm(t('details.deleteConfirmMessage', { name: category.name }))) {
      return;
    }

    try {
      await apiClient.deleteCategory(category.id);
      toast.success(tToasts('categoryDeleted'));
      router.push('/categories');
    } catch (error) {
      console.error('Failed to delete category:', error);
      toast.error(tToasts('categoryDeleteFailed'));
    }
  };

  if (isLoading || loading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <TagIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('details.loadingCategory')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !category) {
    return null;
  }

  return (
    <AppLayout>
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          {/* Navigation Bar */}
          <div className="flex items-center justify-between mb-6">
            <Link href="/categories">
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('details.backToCategories')}</span>
                <span className="sm:hidden">{tCommon('back')}</span>
              </Button>
            </Link>

            {/* Action buttons */}
            {!category.isSystemCategory && (
              <div className="flex gap-2">
                <Link href={`/categories/${category.id}/edit`}>
                  <Button variant="secondary" size="sm" className="flex items-center gap-2">
                    <PencilIcon className="w-4 h-4" />
                    <span className="hidden sm:inline">{tCommon('edit')}</span>
                  </Button>
                </Link>
                <Button
                  variant="secondary"
                  size="sm"
                  className="flex items-center gap-2 text-red-600 hover:text-red-700"
                  onClick={handleDeleteCategory}
                >
                  <TrashIcon className="w-4 h-4" />
                  <span className="hidden sm:inline">{tCommon('delete')}</span>
                </Button>
              </div>
            )}
          </div>
          
          {/* Category Header */}
          <div className="text-center mb-8">
            <div 
              className="w-20 h-20 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6"
              style={{ backgroundColor: category.color || '#6B7280' }}
            >
              {renderCategoryIcon(category.icon, "w-10 h-10 text-white")}
            </div>
            
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {category.name}
            </h1>
            
            <div className="flex items-center justify-center gap-4 text-sm">
              <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium ${categoryTypeColors[category.type as keyof typeof categoryTypeColors]}`}>
                {getCategoryTypeLabel(category.type)}
              </span>
              {category.isSystemCategory && (
                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-gray-100 text-gray-800">
                  {t('details.systemCategory')}
                </span>
              )}
            </div>
            
            {category.description && (
              <p className="text-gray-600 mt-4 max-w-2xl mx-auto">
                {category.description}
              </p>
            )}
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Category Stats */}
          <div className="lg:col-span-1 space-y-6">
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-lg font-bold text-gray-900">{t('details.statsTitle')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {/* Date Range Filter */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    {t('details.dateRange')}
                  </label>
                  <div className="flex flex-wrap gap-1">
                    {(['thisMonth', 'last7', 'last30', 'last3Months', 'all'] as const).map((filterType) => {
                      return (
                        <button
                          key={filterType}
                          type="button"
                          onClick={() => setDateFilter(filterType)}
                          className={`px-2 py-1 text-xs rounded-md transition-colors cursor-pointer ${
                            dateFilter === filterType
                              ? 'bg-gradient-to-r from-primary-500 to-primary-600 text-white font-medium'
                              : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                          }`}
                        >
                          {getDateFilterLabel(filterType)}
                        </button>
                      );
                    })}
                  </div>
                </div>

                {/* Stats Display */}
                {loadingStats ? (
                  <div className="space-y-3">
                    {Array.from({ length: 3 }).map((_, i) => (
                      <div key={i} className="animate-pulse p-3 bg-gray-100 rounded-lg">
                        <div className="h-4 bg-gray-300 rounded w-1/2 mb-2"></div>
                        <div className="h-6 bg-gray-300 rounded w-1/3"></div>
                      </div>
                    ))}
                  </div>
                ) : categoryStats ? (
                  <>
                    <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                      <span className="text-sm font-medium text-gray-700">{t('details.totalTransactions')}</span>
                      <span className="text-lg font-bold text-gray-900">{categoryStats.transactionCount}</span>
                    </div>

                    <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                      <span className="text-sm font-medium text-gray-700">{t('details.totalAmount')}</span>
                      <span className={`text-lg font-bold ${
                        category?.type === 1 ? 'text-green-600' :  // Income categories - green
                        category?.type === 2 ? 'text-red-600' :    // Expense categories - red
                        'text-blue-600'                            // Transfer categories - blue
                      }`}>
                        {formatCurrency(categoryStats.totalAmount)}
                      </span>
                    </div>

                    {categoryStats.transactionCount > 0 && (
                      <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                        <span className="text-sm font-medium text-gray-700">{t('details.averageAmount')}</span>
                        <span className="text-sm text-gray-600">
                          {formatCurrency(categoryStats.averageAmount || 0)}
                        </span>
                      </div>
                    )}
                  </>
                ) : (
                  <div className="p-3 bg-gray-50 rounded-lg text-center text-gray-500">
                    {t('details.failedToLoadStats')}
                  </div>
                )}
                
                {/* Category Info */}
                <div className="border-t pt-4 space-y-3">
                  <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                    <span className="text-sm font-medium text-gray-700">{t('details.fullPath')}</span>
                    <span className="text-sm text-gray-600">{category.fullPath}</span>
                  </div>

                  <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                    <span className="text-sm font-medium text-gray-700">{t('details.created')}</span>
                    <span className="text-sm text-gray-600">{formatDate(category.createdAt)}</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Recent Transactions */}
          <div className="lg:col-span-2">
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-lg font-bold text-gray-900 flex items-center justify-between">
                  {t('details.recentTransactions')}
                  <Link href={`/transactions?categoryId=${category.id}`}>
                    <Button variant="secondary" size="sm">
                      {t('details.viewAll')}
                    </Button>
                  </Link>
                </CardTitle>
              </CardHeader>
              <CardContent>
                {loadingTransactions ? (
                  <div className="space-y-3">
                    {Array.from({ length: 5 }).map((_, i) => (
                      <div key={i} className="animate-pulse">
                        <div className="flex items-center gap-4 p-3 bg-gray-100 rounded-lg">
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
                ) : transactions.length === 0 ? (
                  <div className="text-center py-8">
                    <CurrencyDollarIcon className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                    <h3 className="text-lg font-medium text-gray-900 mb-2">{t('details.noTransactionsYet')}</h3>
                    <p className="text-gray-600">
                      {t('details.transactionsWillAppear')}
                    </p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {transactions.slice(0, 10).map((transaction) => (
                      <Link 
                        key={transaction.id} 
                        href={`/transactions/${transaction.id}`}
                        className="block p-3 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex-1 min-w-0">
                            <p className="text-sm font-medium text-gray-900 truncate">
                              {transaction.userDescription || transaction.description}
                            </p>
                            <div className="flex items-center gap-2 mt-1 text-xs text-gray-500">
                              <CalendarIcon className="w-3 h-3" />
                              {formatDate(transaction.transactionDate)}
                              {transaction.accountName && (
                                <>
                                  <span>•</span>
                                  <span>{transaction.accountName}</span>
                                </>
                              )}
                            </div>
                          </div>
                          <div className="ml-4">
                            <span className={`text-sm font-bold ${transaction.amount >= 0 ? 'text-green-600' : 'text-gray-900'}`}>
                              {transaction.amount >= 0 ? '+' : ''}{formatCurrency(Math.abs(transaction.amount))}
                            </span>
                          </div>
                        </div>
                      </Link>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
    </AppLayout>
  );
}
