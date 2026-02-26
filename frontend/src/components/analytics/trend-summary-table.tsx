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

interface CategoryWithTrend extends CategoryTrendData {
  trend: 'up' | 'down' | 'stable';
  trendPercentage: number;
  highestMonth: { label: string; amount: number };
  lowestMonth: { label: string; amount: number };
}

export function TrendSummaryTable({ categories, selectedCategoryIds }: TrendSummaryTableProps) {
  const t = useTranslations('analytics.categoryTrends');
  // Filter and calculate trends for selected categories
  const categoriesWithTrend = useMemo<CategoryWithTrend[]>(() => {
    return categories
      .filter((cat) => selectedCategoryIds.includes(cat.categoryId))
      .map((cat) => {
        const periods = cat.periods;

        // Calculate 3-month rolling average trend
        let trend: 'up' | 'down' | 'stable' = 'stable';
        let trendPercentage = 0;

        if (periods.length >= 3) {
          // Compare last 3 months average to previous 3 months average
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

        // Find highest and lowest months
        let highestMonth = { label: '', amount: 0 };
        let lowestMonth = { label: '', amount: Infinity };

        periods.forEach((p) => {
          if (p.amount > highestMonth.amount) {
            highestMonth = { label: p.periodLabel, amount: p.amount };
          }
          if (p.amount < lowestMonth.amount && p.amount > 0) {
            lowestMonth = { label: p.periodLabel, amount: p.amount };
          }
        });

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
        return <ArrowTrendingUpIcon className="w-4 h-4 text-red-500" />;
      case 'down':
        return <ArrowTrendingDownIcon className="w-4 h-4 text-emerald-500" />;
      default:
        return <MinusIcon className="w-4 h-4 text-slate-400" />;
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
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-slate-200">
        <thead className="bg-slate-50/80">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-400">
              {t('category')}
            </th>
            <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
              {t('avgMonthly')}
            </th>
            <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-slate-400">
              {t('trend')}
            </th>
            <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
              {t('highestMonth')}
            </th>
            <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
              {t('lowestMonth')}
            </th>
            <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
              {t('total')}
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {categoriesWithTrend.map((cat) => (
            <tr key={cat.categoryId} className="hover:bg-slate-50/60 transition-colors">
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
                      cat.trend === 'up' && 'text-red-600',
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
