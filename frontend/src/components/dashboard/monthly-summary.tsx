'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { formatCurrency, formatMonthYearFromName } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import { CategorySpendingChart } from '@/components/charts/category-spending-chart';
import { useTranslations } from 'next-intl';
import { useLocale } from '@/contexts/locale-context';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
  TagIcon
} from '@heroicons/react/24/outline';

interface MonthlySummary {
  year: number;
  month: number;
  monthName: string;
  totalIncome: number;
  totalExpenses: number;
  netAmount: number;
  transactionCount: number;
  topCategories: CategorySpending[];
}

interface CategorySpending {
  categoryId: number;
  categoryName: string;
  categoryColor?: string;
  amount: number;
  transactionCount: number;
  percentage: number;
}

export function MonthlySummary() {
  const router = useRouter();
  const t = useTranslations('dashboard');
  const tCommon = useTranslations('common');
  const { locale } = useLocale();
  const [currentDate, setCurrentDate] = useState(new Date());
  const [summary, setSummary] = useState<MonthlySummary | null>(null);
  const [loading, setLoading] = useState(true);

  const handleCategoryClick = (categoryId: number) => {
    const params = new URLSearchParams();
    params.set('categoryId', categoryId.toString());

    // Calculate month's date range from currentDate (avoid toISOString to prevent timezone shift)
    const year = currentDate.getFullYear();
    const month = currentDate.getMonth();
    const pad = (n: number) => n.toString().padStart(2, '0');
    const startDate = `${year}-${pad(month + 1)}-01`;
    const lastDay = new Date(year, month + 1, 0).getDate();
    const endDate = `${year}-${pad(month + 1)}-${pad(lastDay)}`;

    params.set('startDate', startDate);
    params.set('endDate', endDate);
    params.set('dateFilter', 'custom');

    router.push(`/transactions?${params.toString()}`);
  };

  const loadMonthlySummary = async (year: number, month: number) => {
    try {
      setLoading(true);
      const data = await apiClient.getMonthlySummary(year, month) as MonthlySummary;
      setSummary(data);
    } catch (error) {
      console.error('Failed to load monthly summary:', error);
      setSummary(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMonthlySummary(currentDate.getFullYear(), currentDate.getMonth() + 1);
  }, [currentDate]);

  const navigateMonth = (direction: 'prev' | 'next') => {
    const newDate = new Date(currentDate);
    if (direction === 'prev') {
      newDate.setMonth(newDate.getMonth() - 1);
    } else {
      newDate.setMonth(newDate.getMonth() + 1);
    }
    setCurrentDate(newDate);
  };

  const isCurrentMonth = () => {
    const now = new Date();
    return currentDate.getFullYear() === now.getFullYear() && 
           currentDate.getMonth() === now.getMonth();
  };

  if (loading) {
    return (
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <div className="animate-pulse">
            <div className="h-6 bg-gray-300 rounded w-1/3 mb-2"></div>
            <div className="h-4 bg-gray-300 rounded w-1/4"></div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="animate-pulse space-y-4">
            <div className="grid grid-cols-3 gap-4">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="h-16 bg-gray-300 rounded"></div>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-xl font-bold text-gray-900">
              {summary?.monthName
                ? formatMonthYearFromName(summary.monthName, summary.year, locale)
                : (() => {
                    const formatted = new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric' }).format(currentDate);
                    return formatted ? formatted[0].toUpperCase() + formatted.slice(1) : formatted;
                  })()}
            </CardTitle>
            {isCurrentMonth() && (
              <p className="text-sm text-gray-600 mt-1">{t('currentMonth')}</p>
            )}
          </div>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigateMonth('prev')}
              className="w-8 h-8 p-0"
            >
              <ChevronLeftIcon className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigateMonth('next')}
              className="w-8 h-8 p-0"
            >
              <ChevronRightIcon className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </CardHeader>
      
      <CardContent className="space-y-6">
        {/* Summary Stats */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-green-50 rounded-lg p-4">
            <div className="flex items-center gap-3">
              <ArrowTrendingUpIcon className="w-8 h-8 text-green-600" />
              <div>
                <p className="text-sm font-medium text-green-700">{tCommon('income')}</p>
                <p className="text-xl font-bold text-green-900">
                  {formatCurrency(summary?.totalIncome || 0)}
                </p>
              </div>
            </div>
          </div>

          <div className="bg-red-50 rounded-lg p-4">
            <div className="flex items-center gap-3">
              <ArrowTrendingDownIcon className="w-8 h-8 text-red-600" />
              <div>
                <p className="text-sm font-medium text-red-700">{tCommon('expense')}</p>
                <p className="text-xl font-bold text-red-900">
                  {formatCurrency(summary?.totalExpenses || 0)}
                </p>
              </div>
            </div>
          </div>

          <div className={`${(summary?.netAmount || 0) >= 0 ? 'bg-blue-50' : 'bg-yellow-50'} rounded-lg p-4`}>
            <div className="flex items-center gap-3">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center ${
                (summary?.netAmount || 0) >= 0 ? 'bg-blue-600' : 'bg-yellow-600'
              }`}>
                <span className="text-white font-bold text-sm">
                  {(summary?.netAmount || 0) >= 0 ? '+' : '-'}
                </span>
              </div>
              <div>
                <p className={`text-sm font-medium ${
                  (summary?.netAmount || 0) >= 0 ? 'text-blue-700' : 'text-yellow-700'
                }`}>
                  {t('netIncome')}
                </p>
                <p className={`text-xl font-bold ${
                  (summary?.netAmount || 0) >= 0 ? 'text-blue-900' : 'text-yellow-900'
                }`}>
                  {formatCurrency(summary?.netAmount || 0)}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Category Breakdown with Chart */}
        {summary && summary.topCategories.length > 0 && (
          <div className="space-y-6">
            {/* Chart View */}
            <CategorySpendingChart
              data={summary.topCategories}
              title={t('expenseDistribution')}
            />
            
            {/* Detailed List View */}
            <div>
              <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                <TagIcon className="w-5 h-5" />
                {t('categoryDetails')}
              </h3>
              <div className="space-y-3">
                {summary.topCategories.map((category) => (
                  <button
                    key={category.categoryId}
                    type="button"
                    onClick={() => handleCategoryClick(category.categoryId)}
                    className="flex items-center justify-between p-3 bg-gray-50 rounded-lg w-full text-left transition-colors hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 cursor-pointer"
                    aria-label={`View ${category.categoryName} transactions`}
                  >
                    <div className="flex items-center gap-3">
                      <div
                        className="w-4 h-4 rounded-full"
                        style={{ backgroundColor: category.categoryColor || '#6B7280' }}
                      ></div>
                      <div>
                        <p className="font-medium text-gray-900">{category.categoryName}</p>
                        <p className="text-sm text-gray-600">
                          {category.transactionCount === 1
                            ? t('nTransactions', { count: category.transactionCount })
                            : t('nTransactionsPlural', { count: category.transactionCount })}
                        </p>
                      </div>
                    </div>
                    <div className="text-right">
                      <p className="font-bold text-gray-900">{formatCurrency(category.amount)}</p>
                      <p className="text-sm text-gray-600">{category.percentage.toFixed(1)}%</p>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Empty State */}
        {summary && summary.transactionCount === 0 && (
          <div className="text-center py-8">
            <p className="text-gray-600">{t('noTransactionsMonth')}</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
