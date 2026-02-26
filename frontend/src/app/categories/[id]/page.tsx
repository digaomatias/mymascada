'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import {
  ArrowLeftIcon,
  PencilIcon,
  TrashIcon,
  TagIcon,
} from '@heroicons/react/24/outline';
import { renderCategoryIcon } from '@/lib/category-icons';
import { formatCurrency, formatDate, cn } from '@/lib/utils';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import { TransactionList } from '@/components/transaction-list';

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

type DateFilter = 'thisMonth' | 'last7' | 'last30' | 'last3Months' | 'all';

const typeColorMap: Record<number, { badge: string; stat: string }> = {
  1: { badge: 'bg-emerald-100 text-emerald-700', stat: 'text-emerald-600' },
  2: { badge: 'bg-red-100 text-red-700', stat: 'text-red-600' },
  3: { badge: 'bg-blue-100 text-blue-700', stat: 'text-blue-600' },
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
  const [categoryStats, setCategoryStats] = useState<CategoryStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingStats, setLoadingStats] = useState(false);
  const [dateFilter, setDateFilter] = useState<DateFilter>('thisMonth');

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

  const getDateRangeFromFilter = useCallback((filter: DateFilter) => {
    const now = new Date();
    const todayUTC = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));

    switch (filter) {
      case 'last7': {
        const last7Start = new Date(todayUTC.getTime() - 6 * 24 * 60 * 60 * 1000);
        return { start: last7Start.toISOString().split('T')[0], end: todayUTC.toISOString().split('T')[0] };
      }
      case 'thisMonth': {
        const startOfMonthUTC = new Date(Date.UTC(todayUTC.getUTCFullYear(), todayUTC.getUTCMonth(), 1));
        return { start: startOfMonthUTC.toISOString().split('T')[0], end: todayUTC.toISOString().split('T')[0] };
      }
      case 'last30': {
        const last30Start = new Date(todayUTC.getTime() - 29 * 24 * 60 * 60 * 1000);
        return { start: last30Start.toISOString().split('T')[0], end: todayUTC.toISOString().split('T')[0] };
      }
      case 'last3Months': {
        const threeMonthsAgoUTC = new Date(Date.UTC(todayUTC.getUTCFullYear(), todayUTC.getUTCMonth() - 3, todayUTC.getUTCDate()));
        return { start: threeMonthsAgoUTC.toISOString().split('T')[0], end: todayUTC.toISOString().split('T')[0] };
      }
      default:
        return { start: undefined, end: undefined };
    }
  }, []);

  const loadCategoryStats = useCallback(async () => {
    try {
      setLoadingStats(true);
      const dateRange = getDateRangeFromFilter(dateFilter);

      interface TransactionRequestParams {
        categoryId: number;
        pageSize: number;
        startDate?: string;
        endDate?: string;
      }

      const reqParams: TransactionRequestParams = {
        categoryId: parseInt(categoryId),
        pageSize: 1000,
      };

      if (dateRange.start) reqParams.startDate = dateRange.start;
      if (dateRange.end) reqParams.endDate = dateRange.end;

      const transactionsData = await apiClient.getTransactions(reqParams) as { transactions: Transaction[] };
      const transactions = transactionsData.transactions || [];

      const totalAmount = transactions.reduce((sum, t) => sum + t.amount, 0);
      const stats: CategoryStats = {
        transactionCount: transactions.length,
        totalAmount: Math.abs(totalAmount),
        averageAmount: transactions.length > 0 ? Math.abs(totalAmount) / transactions.length : 0,
        lastTransactionDate: transactions.length > 0 ? transactions[0]?.transactionDate : undefined,
      };

      setCategoryStats(stats);
    } catch (error) {
      console.error('Failed to load category stats:', error);
      setCategoryStats({ transactionCount: 0, totalAmount: 0, averageAmount: 0 });
    } finally {
      setLoadingStats(false);
    }
  }, [categoryId, dateFilter, getDateRangeFromFilter]);

  useEffect(() => {
    if (isAuthenticated && categoryId) {
      loadCategoryDetails();
      loadCategoryStats();
    }
  }, [isAuthenticated, categoryId, loadCategoryDetails, loadCategoryStats]);

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
          <div className="mt-6 text-slate-700 font-medium">{t('details.loadingCategory')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !category) {
    return null;
  }

  const colors = typeColorMap[category.type] || typeColorMap[2];
  const dateFilters: DateFilter[] = ['thisMonth', 'last7', 'last30', 'last3Months', 'all'];

  return (
    <AppLayout>
      {/* Navigation Bar */}
      <header className="flex flex-wrap items-center justify-between gap-4 mb-5">
        <Link href="/categories">
          <Button variant="secondary" size="sm" className="flex items-center gap-2">
            <ArrowLeftIcon className="w-4 h-4" />
            <span className="hidden sm:inline">{t('details.backToCategories')}</span>
            <span className="sm:hidden">{tCommon('back')}</span>
          </Button>
        </Link>

        {!category.isSystemCategory && (
          <div className="flex items-center gap-2">
            <Button
              variant="secondary"
              size="sm"
              className="flex items-center gap-2 border-red-300 text-red-600 hover:bg-red-50"
              onClick={handleDeleteCategory}
            >
              <TrashIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{tCommon('delete')}</span>
            </Button>
            <Link href={`/categories/${category.id}/edit`}>
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <PencilIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{tCommon('edit')}</span>
              </Button>
            </Link>
          </div>
        )}
      </header>

      <div className="space-y-5">
        {/* Hero Section */}
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <div className="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-6">
            {/* Left: Category identity */}
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-3">
                <div
                  className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl"
                  style={{ backgroundColor: category.color || '#6B7280' }}
                >
                  {renderCategoryIcon(category.icon, 'h-6 w-6 text-white')}
                </div>
                <div className="min-w-0">
                  <h1 className="font-[var(--font-dash-sans)] text-2xl sm:text-3xl font-semibold tracking-[-0.03em] text-slate-900 truncate">
                    {category.name}
                  </h1>
                  <div className="mt-0.5 flex flex-wrap items-center gap-2">
                    <span className={cn('inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold', colors.badge)}>
                      {getCategoryTypeLabel(category.type)}
                    </span>
                    {category.isSystemCategory && (
                      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-100 text-slate-600">
                        {t('details.systemCategory')}
                      </span>
                    )}
                  </div>
                </div>
              </div>

              {category.description && (
                <p className="mt-3 text-sm text-slate-500">{category.description}</p>
              )}
            </div>

            {/* Right: Quick stats */}
            <div className="flex items-start gap-5 lg:gap-6">
              {/* Total Spent/Earned */}
              <div className="text-left lg:text-right">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {category.type === 1 ? t('totalEarned') : t('details.totalAmount')}
                </p>
                <p className={cn('mt-1 font-[var(--font-dash-mono)] text-xl font-semibold', colors.stat)}>
                  {formatCurrency(category.totalAmount ? Math.abs(category.totalAmount) : 0)}
                </p>
              </div>

              <div className="h-12 w-px bg-slate-200 self-center" />

              {/* Transaction Count */}
              <div className="text-left lg:text-right">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('details.totalTransactions')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-xl font-semibold text-slate-900">
                  {category.transactionCount}
                </p>
              </div>

              <div className="h-12 w-px bg-slate-200 self-center" />

              {/* Average per Transaction */}
              <div className="text-left lg:text-right">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('details.averagePerTransaction')}
                </p>
                <p className={cn('mt-1 font-[var(--font-dash-mono)] text-xl font-semibold', colors.stat)}>
                  {formatCurrency(
                    category.transactionCount > 0
                      ? Math.abs(category.totalAmount) / category.transactionCount
                      : 0
                  )}
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* Stats Section with Date Filters */}
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-5 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          {/* Date Filter Pills */}
          <div className="flex flex-wrap gap-1.5 mb-5">
            {dateFilters.map((filterType) => (
              <button
                key={filterType}
                type="button"
                onClick={() => setDateFilter(filterType)}
                className={cn(
                  'px-3 py-1.5 text-xs rounded-lg transition-colors cursor-pointer font-medium',
                  dateFilter === filterType
                    ? 'bg-gradient-to-r from-primary-500 to-primary-600 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
                )}
              >
                {getDateFilterLabel(filterType)}
              </button>
            ))}
          </div>

          {/* Stat Cards */}
          {loadingStats ? (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="animate-pulse rounded-xl bg-slate-50 p-4">
                  <div className="h-3 bg-slate-200 rounded w-1/2 mb-3" />
                  <div className="h-7 bg-slate-200 rounded w-2/3" />
                </div>
              ))}
            </div>
          ) : categoryStats ? (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="rounded-xl bg-slate-50/80 p-4">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('details.totalAmount')}
                </p>
                <p className={cn('mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold', colors.stat)}>
                  {formatCurrency(categoryStats.totalAmount)}
                </p>
              </div>

              <div className="rounded-xl bg-slate-50/80 p-4">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('details.totalTransactions')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
                  {categoryStats.transactionCount}
                </p>
              </div>

              <div className="rounded-xl bg-slate-50/80 p-4">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('details.averageAmount')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
                  {formatCurrency(categoryStats.averageAmount || 0)}
                </p>
              </div>
            </div>
          ) : (
            <div className="rounded-xl bg-slate-50 p-4 text-center text-sm text-slate-500">
              {t('details.failedToLoadStats')}
            </div>
          )}
        </section>

        {/* Transactions Section */}
        <section className="rounded-[26px] border border-violet-100/80 bg-white/90 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)] p-5">
          <TransactionList
            categoryId={parseInt(categoryId)}
            showCategoryFilter={false}
            compact={false}
            title={t('details.recentTransactions')}
          />
        </section>

        {/* Category Info */}
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-5 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <h2 className="font-[var(--font-dash-sans)] text-base font-semibold text-slate-900 mb-4">
            {tCommon('details')}
          </h2>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('details.fullPath')}
              </span>
              <span className="font-[var(--font-dash-mono)] text-sm text-slate-700">
                {category.fullPath}
              </span>
            </div>

            <div className="h-px bg-slate-100" />

            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('details.created')}
              </span>
              <span className="font-[var(--font-dash-mono)] text-sm text-slate-700">
                {formatDate(category.createdAt)}
              </span>
            </div>

            <div className="h-px bg-slate-100" />

            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('details.lastUpdated')}
              </span>
              <span className="font-[var(--font-dash-mono)] text-sm text-slate-700">
                {formatDate(category.updatedAt)}
              </span>
            </div>

          </div>
        </section>
      </div>
    </AppLayout>
  );
}
