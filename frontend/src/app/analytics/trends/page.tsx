'use client';

import React, { useState, useEffect } from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { CategoryTrendChart } from '@/components/analytics/category-trend-chart';
import { CategorySelector } from '@/components/analytics/category-selector';
import { TrendSummaryTable } from '@/components/analytics/trend-summary-table';
import { apiClient, CategoryTrendsResponse } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowLeftIcon,
  ChartBarIcon,
  CalendarIcon,
  BanknotesIcon,
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

export default function CategoryTrendsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('analytics');
  const [loading, setLoading] = useState(true);
  const [trendData, setTrendData] = useState<CategoryTrendsResponse | null>(null);
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<number[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated) {
      loadCategoryTrends();
    }
  }, [isAuthenticated]);

  const loadCategoryTrends = async () => {
    try {
      setLoading(true);
      setError(null);

      const data = await apiClient.getCategoryTrends();
      setTrendData(data);

      // Auto-select top 5 categories by spending
      if (data.categories.length > 0) {
        const sortedCategories = [...data.categories].sort(
          (a, b) => b.totalSpent - a.totalSpent
        );
        const top5Ids = sortedCategories.slice(0, 5).map((cat) => cat.categoryId);
        setSelectedCategoryIds(top5Ids);
      }
    } catch (err) {
      console.error('Failed to load category trends:', err);
      setError(t('categoryTrends.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  // Calculate summary statistics
  const getSummaryStats = () => {
    if (!trendData) return null;

    const { categories, periodSummaries } = trendData;

    // Total spending across all categories and periods
    const totalSpending = categories.reduce((sum, cat) => sum + cat.totalSpent, 0);

    // Average monthly spending
    const monthCount = periodSummaries.length || 1;
    const avgMonthlySpending = totalSpending / monthCount;

    // Highest spending month
    const highestMonth = periodSummaries.reduce(
      (max, p) => (p.totalSpent > max.totalSpent ? p : max),
      periodSummaries[0] || { periodLabel: '-', totalSpent: 0 }
    );

    // Lowest spending month (excluding zeros)
    const nonZeroPeriods = periodSummaries.filter((p) => p.totalSpent > 0);
    const lowestMonth = nonZeroPeriods.length > 0
      ? nonZeroPeriods.reduce(
          (min, p) => (p.totalSpent < min.totalSpent ? p : min),
          nonZeroPeriods[0]
        )
      : { periodLabel: '-', totalSpent: 0 };

    return {
      totalSpending,
      avgMonthlySpending,
      highestMonth,
      lowestMonth,
      categoryCount: categories.length,
      monthCount,
    };
  };

  const stats = getSummaryStats();

  if (isLoading || loading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
        <Navigation />
        <main className="container-responsive py-8">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-300 rounded w-1/3 mb-8"></div>
            <div className="h-[500px] bg-gray-200 rounded mb-6"></div>
            <div className="h-48 bg-gray-200 rounded"></div>
          </div>
        </main>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
        <Navigation />
        <main className="container-responsive py-8">
          <div className="text-center py-12">
            <p className="text-red-600 mb-4">{error}</p>
            <Button variant="primary" onClick={loadCategoryTrends}>
              {t('categoryTrends.tryAgain')}
            </Button>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />

      <main className="container-responsive py-8">
        {/* Header with back link */}
        <div className="mb-6">
          <Link href="/analytics" className="inline-flex items-center gap-2 text-primary-600 hover:text-primary-700 mb-4">
            <ArrowLeftIcon className="w-4 h-4" />
            <span>{t('categoryTrends.backToAnalytics')}</span>
          </Link>
          <h1 className="text-3xl font-bold text-gray-900 mb-2">{t('categoryTrends.title')}</h1>
          <p className="text-gray-600">
            {t('categoryTrends.subtitle')}
          </p>
        </div>

        {/* Summary Stats */}
        {stats && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-purple-100 rounded-lg">
                    <BanknotesIcon className="w-5 h-5 text-purple-600" />
                  </div>
                  <div>
                    <p className="text-sm text-gray-600">{t('stats.totalSpending')}</p>
                    <p className="text-xl font-bold text-gray-900">
                      {formatCurrency(stats.totalSpending)}
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-blue-100 rounded-lg">
                    <CalendarIcon className="w-5 h-5 text-blue-600" />
                  </div>
                  <div>
                    <p className="text-sm text-gray-600">{t('stats.avgMonthly')}</p>
                    <p className="text-xl font-bold text-gray-900">
                      {formatCurrency(stats.avgMonthlySpending)}
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-red-100 rounded-lg">
                    <ChartBarIcon className="w-5 h-5 text-red-600" />
                  </div>
                  <div>
                    <p className="text-sm text-gray-600">{t('stats.highestMonth')}</p>
                    <p className="text-lg font-bold text-gray-900">
                      {formatCurrency(stats.highestMonth.totalSpent)}
                    </p>
                    <p className="text-xs text-gray-500">{stats.highestMonth.periodLabel}</p>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-green-100 rounded-lg">
                    <ChartBarIcon className="w-5 h-5 text-green-600" />
                  </div>
                  <div>
                    <p className="text-sm text-gray-600">{t('stats.lowestMonth')}</p>
                    <p className="text-lg font-bold text-gray-900">
                      {formatCurrency(stats.lowestMonth.totalSpent)}
                    </p>
                    <p className="text-xs text-gray-500">{stats.lowestMonth.periodLabel}</p>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        )}

        {/* Main content grid */}
        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* Category Selector (sidebar on large screens) */}
          <div className="lg:col-span-1 order-2 lg:order-1">
            <CategorySelector
              categories={trendData?.categories || []}
              selectedCategoryIds={selectedCategoryIds}
              onSelectionChange={setSelectedCategoryIds}
              maxSelections={10}
            />
          </div>

          {/* Main Chart */}
          <div className="lg:col-span-3 order-1 lg:order-2">
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <ChartBarIcon className="w-5 h-5" />
                  {t('categoryTrends.spendingOverTime')}
                </CardTitle>
              </CardHeader>
              <CardContent>
                <CategoryTrendChart
                  categories={trendData?.categories || []}
                  selectedCategoryIds={selectedCategoryIds}
                />
              </CardContent>
            </Card>
          </div>
        </div>

        {/* Category Comparison Table */}
        <Card className="mt-6 bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardHeader>
            <CardTitle>{t('categoryTrends.categoryComparison')}</CardTitle>
          </CardHeader>
          <CardContent>
            <TrendSummaryTable
              categories={trendData?.categories || []}
              selectedCategoryIds={selectedCategoryIds}
            />
          </CardContent>
        </Card>
      </main>
    </div>
  );
}
