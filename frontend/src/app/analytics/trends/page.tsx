'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { CategoryTrendChart } from '@/components/analytics/category-trend-chart';
import { CategorySelector } from '@/components/analytics/category-selector';
import { TrendSummaryTable } from '@/components/analytics/trend-summary-table';
import { apiClient, CategoryTrendsResponse } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { useAuth } from '@/contexts/auth-context';
import {
  ArrowLeftIcon,
  ArrowPathIcon,
  BanknotesIcon,
  CalendarIcon,
  ChartBarIcon,
  SparklesIcon,
} from '@heroicons/react/24/outline';

const PANEL_CLASS =
  'rounded-[26px] border border-violet-100/70 bg-white/92 p-5 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs';

const METRIC_CARD_CLASS =
  'rounded-2xl border border-violet-100/80 bg-white/92 p-4 shadow-[0_16px_34px_-26px_rgba(76,29,149,0.4)]';

const SKELETON_BANNER_CLASS = 'h-20 rounded-[24px] border border-violet-100/80 bg-white/85';
const SKELETON_METRIC_CARD_CLASS = 'h-28 rounded-2xl border border-violet-100/80 bg-white/85';
const SKELETON_PANEL_CLASS = 'rounded-[26px] border border-violet-100/80 bg-white/85';

interface TrendStats {
  totalSpending: number;
  avgMonthlySpending: number;
  highestMonth: { periodLabel: string; totalSpent: number };
  lowestMonth: { periodLabel: string; totalSpent: number };
  categoryCount: number;
  monthCount: number;
}

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
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  const loadCategoryTrends = async () => {
    try {
      setLoading(true);
      setError(null);

      const data = await apiClient.getCategoryTrends();
      setTrendData(data);

      if (data.categories.length > 0) {
        const sortedCategories = [...data.categories].sort((a, b) => b.totalSpent - a.totalSpent);
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

  const stats = useMemo<TrendStats | null>(() => {
    if (!trendData) return null;

    const { categories, periodSummaries } = trendData;
    const totalSpending = categories.reduce((sum, cat) => sum + cat.totalSpent, 0);
    const monthCount = periodSummaries.length || 1;
    const avgMonthlySpending = totalSpending / monthCount;

    const highestMonth = periodSummaries.reduce(
      (max, p) => (p.totalSpent > max.totalSpent ? p : max),
      periodSummaries[0] || { periodLabel: '-', totalSpent: 0 },
    );

    const nonZeroPeriods = periodSummaries.filter((p) => p.totalSpent > 0);
    const lowestMonth =
      nonZeroPeriods.length > 0
        ? nonZeroPeriods.reduce((min, p) => (p.totalSpent < min.totalSpent ? p : min), nonZeroPeriods[0])
        : { periodLabel: '-', totalSpent: 0 };

    return {
      totalSpending,
      avgMonthlySpending,
      highestMonth,
      lowestMonth,
      categoryCount: categories.length,
      monthCount,
    };
  }, [trendData]);

  const selectedCategoryNames = useMemo(() => {
    if (!trendData) return [];
    return trendData.categories
      .filter((category) => selectedCategoryIds.includes(category.categoryId))
      .map((category) => category.categoryName)
      .slice(0, 3);
  }, [trendData, selectedCategoryIds]);

  if (isLoading || loading) {
    return (
      <AppLayout>
        <div className="animate-pulse space-y-5">
          <div className="h-9 w-64 rounded-xl bg-slate-200" />
          <div className={SKELETON_BANNER_CLASS} />
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className={SKELETON_METRIC_CARD_CLASS} />
            ))}
          </div>
          <div className="grid gap-6 xl:grid-cols-[310px_minmax(0,1fr)]">
            <div className={SKELETON_PANEL_CLASS + ' h-[620px]'} />
            <div className="space-y-6">
              <div className={SKELETON_PANEL_CLASS + ' h-[520px]'} />
              <div className={SKELETON_PANEL_CLASS + ' h-[360px]'} />
            </div>
          </div>
        </div>
      </AppLayout>
    );
  }

  if (error) {
    return (
      <AppLayout>
        <section className="mx-auto max-w-xl rounded-[26px] border border-rose-200/80 bg-rose-50/70 p-8 text-center">
          <p className="text-sm font-medium text-rose-700">{error}</p>
          <Button variant="outline" onClick={loadCategoryTrends} className="mt-4 gap-2">
            <ArrowPathIcon className="h-4 w-4" />
            {t('categoryTrends.tryAgain')}
          </Button>
        </section>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
      <header className="mb-5">
        <Link
          href="/analytics"
          className="mb-4 inline-flex items-center gap-2 text-sm font-medium text-slate-500 transition-colors hover:text-violet-700"
        >
          <ArrowLeftIcon className="h-4 w-4" />
          <span>{t('categoryTrends.backToAnalytics')}</span>
        </Link>

        <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
          {t('categoryTrends.title')}
        </h1>
        <p className="mt-1.5 text-[15px] text-slate-500">{t('categoryTrends.subtitle')}</p>
      </header>

      <section className="mb-6 rounded-2xl border border-violet-100/80 bg-white/92 p-4 shadow-[0_16px_32px_-28px_rgba(76,29,149,0.45)]">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div className="flex items-start gap-3">
            <div className="mt-0.5 flex h-9 w-9 items-center justify-center rounded-lg bg-violet-100 text-violet-700">
              <SparklesIcon className="h-5 w-5" />
            </div>
            <div>
              <p className="text-sm font-semibold text-slate-800">{t('categoryTrends.seeDescription')}</p>
              <p className="text-sm text-slate-600">
                {selectedCategoryNames.length > 0 ? selectedCategoryNames.join(', ') : t('categoryTrends.selectCategories')}
              </p>
            </div>
          </div>

          <p className="text-xs font-medium uppercase tracking-[0.08em] text-slate-500">
            {t('categoryTrends.nOfMaxSelected', {
              count: selectedCategoryIds.length,
              max: Math.min(10, trendData?.categories.length ?? 10),
            })}
          </p>
        </div>
      </section>

      {stats && (
        <section className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <article className={METRIC_CARD_CLASS}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{t('stats.totalSpending')}</p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
                  {formatCurrency(stats.totalSpending)}
                </p>
                <p className="mt-1 text-xs text-slate-500">{stats.categoryCount} {t('byCategory').toLowerCase()}</p>
              </div>
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-50 text-violet-600">
                <BanknotesIcon className="h-5 w-5" />
              </div>
            </div>
          </article>

          <article className={METRIC_CARD_CLASS}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{t('stats.avgMonthly')}</p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
                  {formatCurrency(stats.avgMonthlySpending)}
                </p>
                <p className="mt-1 text-xs text-slate-500">{stats.monthCount} {t('byMonth').toLowerCase()}</p>
              </div>
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                <CalendarIcon className="h-5 w-5" />
              </div>
            </div>
          </article>

          <article className={METRIC_CARD_CLASS}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{t('stats.highestMonth')}</p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-xl font-semibold text-rose-600">
                  {formatCurrency(stats.highestMonth.totalSpent)}
                </p>
                <p className="mt-1 text-xs text-slate-500">{stats.highestMonth.periodLabel}</p>
              </div>
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-rose-50 text-rose-600">
                <ChartBarIcon className="h-5 w-5" />
              </div>
            </div>
          </article>

          <article className={METRIC_CARD_CLASS}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{t('stats.lowestMonth')}</p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-xl font-semibold text-emerald-600">
                  {formatCurrency(stats.lowestMonth.totalSpent)}
                </p>
                <p className="mt-1 text-xs text-slate-500">{stats.lowestMonth.periodLabel}</p>
              </div>
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-50 text-emerald-600">
                <ChartBarIcon className="h-5 w-5" />
              </div>
            </div>
          </article>
        </section>
      )}

      <div className="grid gap-6 xl:grid-cols-[310px_minmax(0,1fr)]">
        <aside className="self-start xl:sticky xl:top-6">
          <CategorySelector
            categories={trendData?.categories || []}
            selectedCategoryIds={selectedCategoryIds}
            onSelectionChange={setSelectedCategoryIds}
            maxSelections={10}
          />
        </aside>

        <div className="space-y-6">
          <section className={PANEL_CLASS}>
            <div className="mb-4 flex items-center justify-between gap-3">
              <h2 className="flex items-center gap-2 font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
                <ChartBarIcon className="h-5 w-5 text-violet-600" />
                {t('categoryTrends.spendingOverTime')}
              </h2>
              <span className="text-xs uppercase tracking-[0.08em] text-slate-500">{t('trends')}</span>
            </div>

            <CategoryTrendChart
              categories={trendData?.categories || []}
              selectedCategoryIds={selectedCategoryIds}
            />
          </section>

          <section className={PANEL_CLASS}>
            <h2 className="mb-4 font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
              {t('categoryTrends.categoryComparison')}
            </h2>
            <TrendSummaryTable
              categories={trendData?.categories || []}
              selectedCategoryIds={selectedCategoryIds}
            />
          </section>
        </div>
      </div>
    </AppLayout>
  );
}
