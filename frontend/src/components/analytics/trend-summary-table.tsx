'use client';

import React, { useMemo } from 'react';
import { formatCurrency, cn } from '@/lib/utils';
import { CategoryTrendData } from '@/lib/api-client';
import { ArrowTrendingUpIcon, ArrowTrendingDownIcon, MinusIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

interface TrendSummaryTableProps {
  categories: CategoryTrendData[];
  selectedCategoryIds: number[];
}

// Enriched fields that may be present from the API
interface EnrichedCategoryTrendData extends CategoryTrendData {
  trend?: string;
  trendPercentage?: number;
  highestMonth?: { periodLabel: string; amount: number };
  lowestMonth?: { periodLabel: string; amount: number };
}

interface CategoryWithTrend extends CategoryTrendData {
  trend: 'up' | 'down' | 'stable';
  trendPercentage: number;
  highestMonth: { label: string; amount: number };
  lowestMonth: { label: string; amount: number };
}

export function TrendSummaryTable({ categories, selectedCategoryIds }: TrendSummaryTableProps) {
  const t = useTranslations('analytics.categoryTrends');
  // Filter and use API trend data when available, fall back to client-side calculation
  const categoriesWithTrend = useMemo<CategoryWithTrend[]>(() => {
    return categories
      .filter((cat) => selectedCategoryIds.includes(cat.categoryId))
      .map((cat) => {
        const enriched = cat as EnrichedCategoryTrendData;
        const periods = cat.periods;

        // Use API-provided trend data if available
        let trend: 'up' | 'down' | 'stable' = 'stable';
        let trendPercentage = 0;

        if (enriched.trend !== undefined && enriched.trendPercentage !== undefined) {
          // Use enriched API fields
          const apiTrend = enriched.trend.toLowerCase();
          if (apiTrend === 'up' || apiTrend === 'increasing') {
            trend = 'up';
          } else if (apiTrend === 'down' || apiTrend === 'decreasing') {
            trend = 'down';
          } else {
            trend = 'stable';
          }
          trendPercentage = enriched.trendPercentage;
        } else if (periods.length >= 3) {
          // Fallback: Calculate 3-month rolling average trend
          const last3 = periods.slice(-3);
          const prev3 = periods.slice(-6, -3);

          if (prev3.length >= 3) {
            const last3Avg = last3.reduce((sum, p) => sum + p.amount, 0) / 3;
            const prev3Avg = prev3.reduce((sum, p) => sum + p.amount, 0) / 3;

            if (prev3Avg > 0) {
              trendPercentage = ((last3Avg - prev3Avg) / prev3Avg) * 100;
              if (trendPercentage > 5) {
                trend = 'up';
              } else if (trendPercentage < -5) {
                trend = 'down';
              }
            }
          }
        }

        // Use API-provided highest/lowest months if available, else single-pass fallback
        let highestMonth = enriched.highestMonth
          ? { label: enriched.highestMonth.periodLabel, amount: enriched.highestMonth.amount }
          : { label: '', amount: 0 };
        let lowestMonth = enriched.lowestMonth
          ? { label: enriched.lowestMonth.periodLabel, amount: enriched.lowestMonth.amount }
          : { label: '', amount: Infinity };

        if (!enriched.highestMonth || !enriched.lowestMonth) {
          for (const p of periods) {
            if (!enriched.highestMonth && p.amount > highestMonth.amount) {
              highestMonth = { label: p.periodLabel, amount: p.amount };
            }
            if (!enriched.lowestMonth && p.amount < lowestMonth.amount && p.amount > 0) {
              lowestMonth = { label: p.periodLabel, amount: p.amount };
            }
          }
        }

        // Handle case where all months are 0
        if (lowestMonth.amount === Infinity) {
          lowestMonth = { label: '-', amount: 0 };
        }

        return {
          ...cat,
          trend,
          trendPercentage,
          highestMonth,
          lowestMonth,
        };
      })
      .sort((a, b) => b.totalSpent - a.totalSpent);
  }, [categories, selectedCategoryIds]);

  const TrendIcon = ({ trend }: { trend: 'up' | 'down' | 'stable' }) => {
    switch (trend) {
      case 'up':
        return <ArrowTrendingUpIcon className="h-4 w-4 text-rose-500" />;
      case 'down':
        return <ArrowTrendingDownIcon className="h-4 w-4 text-emerald-500" />;
      default:
        return <MinusIcon className="h-4 w-4 text-slate-400" />;
    }
  };

  if (categoriesWithTrend.length === 0) {
    return (
      <div className="text-center py-8 text-slate-500">
        {t('selectToView')}
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-2xl border border-ink-200 bg-white/95">
      <table className="min-w-full divide-y divide-slate-200">
        <thead className="bg-primary-50/45">
          <tr>
            <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">
              {t('category')}
            </th>
            <th className="px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">
              {t('avgMonthly')}
            </th>
            <th className="px-4 py-3 text-center text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">
              {t('trend')}
            </th>
            <th className="px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">
              {t('highestMonth')}
            </th>
            <th className="px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">
              {t('lowestMonth')}
            </th>
            <th className="px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">
              {t('total')}
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 bg-white">
          {categoriesWithTrend.map((cat) => (
            <tr key={cat.categoryId} className="transition-colors hover:bg-primary-50/45">
              <td className="px-4 py-3 whitespace-nowrap">
                <div className="flex items-center gap-2">
                  <div
                    className="w-3 h-3 rounded-full flex-shrink-0"
                    style={{ backgroundColor: cat.categoryColor || '#8B5CF6' }}
                  />
                  <span className="text-sm font-medium text-slate-900">
                    {cat.categoryName}
                  </span>
                </div>
              </td>
              <td className="px-4 py-3 whitespace-nowrap text-right font-[var(--font-dash-mono)] text-sm text-slate-900">
                {formatCurrency(cat.averageMonthlySpent)}
              </td>
              <td className="px-4 py-3 whitespace-nowrap text-center">
                <div className="flex items-center justify-center gap-1">
                  <TrendIcon trend={cat.trend} />
                  <span
                    className={cn(
                      'text-sm font-medium',
                      cat.trend === 'up' && 'text-rose-600',
                      cat.trend === 'down' && 'text-emerald-600',
                      cat.trend === 'stable' && 'text-slate-500'
                    )}
                  >
                    {cat.trendPercentage > 0 ? '+' : ''}
                    {cat.trendPercentage.toFixed(1)}%
                  </span>
                </div>
              </td>
              <td className="px-4 py-3 whitespace-nowrap text-right">
                <div className="text-sm">
                  <span className="font-[var(--font-dash-mono)] font-medium text-slate-900">
                    {formatCurrency(cat.highestMonth.amount)}
                  </span>
                  <span className="text-slate-500 ml-1">
                    ({cat.highestMonth.label})
                  </span>
                </div>
              </td>
              <td className="px-4 py-3 whitespace-nowrap text-right">
                <div className="text-sm">
                  <span className="font-[var(--font-dash-mono)] font-medium text-slate-900">
                    {formatCurrency(cat.lowestMonth.amount)}
                  </span>
                  <span className="text-slate-500 ml-1">
                    ({cat.lowestMonth.label})
                  </span>
                </div>
              </td>
              <td className="px-4 py-3 whitespace-nowrap text-right font-[var(--font-dash-mono)] text-sm font-bold text-slate-900">
                {formatCurrency(cat.totalSpent)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
