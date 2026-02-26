'use client';

import React from 'react';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts';
import { formatCurrency } from '@/lib/utils';
import { CategoryTrendData, PeriodAmount } from '@/lib/api-client';
import { ChartBarIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

// Default color palette for categories without a color
const DEFAULT_COLORS = [
  '#8B5CF6', // purple
  '#EC4899', // pink
  '#10B981', // green
  '#F59E0B', // yellow
  '#3B82F6', // blue
  '#EF4444', // red
  '#6366F1', // indigo
  '#14B8A6', // teal
  '#F97316', // orange
  '#84CC16', // lime
];

interface CategoryTrendChartProps {
  categories: CategoryTrendData[];
  selectedCategoryIds: number[];
}

interface ChartDataPoint {
  periodLabel: string;
  [key: string]: number | string;
}

export function CategoryTrendChart({ categories, selectedCategoryIds }: CategoryTrendChartProps) {
  const t = useTranslations('analytics.categoryTrends');
  // Filter to only selected categories
  const displayCategories = categories.filter((cat) =>
    selectedCategoryIds.includes(cat.categoryId)
  );

  // Build chart data with one entry per period
  const chartData: ChartDataPoint[] = [];

  if (displayCategories.length > 0) {
    // Use the periods from the first category as the base (they all have the same periods)
    const periods = displayCategories[0]?.periods || [];

    periods.forEach((period: PeriodAmount, index: number) => {
      const dataPoint: ChartDataPoint = {
        periodLabel: period.periodLabel,
      };

      displayCategories.forEach((cat) => {
        dataPoint[`cat_${cat.categoryId}`] = cat.periods[index]?.amount || 0;
      });

      chartData.push(dataPoint);
    });
  }

  // Get color for a category
  const getCategoryColor = (category: CategoryTrendData, index: number): string => {
    return category.categoryColor || DEFAULT_COLORS[index % DEFAULT_COLORS.length];
  };

  // Custom tooltip
  const CustomTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload.length > 0) {
      // Calculate total for this period
      const total = payload.reduce((sum: number, entry: any) => sum + (entry.value || 0), 0);

      return (
        <div className="max-w-xs rounded-xl border border-violet-100/70 bg-white/98 p-4 shadow-[0_18px_36px_-26px_rgba(76,29,149,0.55)] backdrop-blur-xs">
          <p className="mb-2 font-[var(--font-dash-sans)] text-sm font-semibold text-slate-900">{label}</p>
          {payload.map((entry: any, index: number) => {
            const percentage = total > 0 ? ((entry.value / total) * 100).toFixed(1) : 0;
            return (
              <div
                key={index}
                className="flex items-center justify-between py-1 text-sm"
              >
                <div className="flex items-center gap-2">
                  <div
                    className="w-3 h-3 rounded-full"
                    style={{ backgroundColor: entry.color }}
                  />
                  <span className="max-w-[120px] truncate text-slate-700">{entry.name}</span>
                </div>
                <div className="text-right">
                  <span className="font-[var(--font-dash-mono)] font-medium text-slate-900">
                    {formatCurrency(entry.value)}
                  </span>
                  <span className="ml-1 text-slate-500">({percentage}%)</span>
                </div>
              </div>
            );
          })}
          <div className="mt-2 flex items-center justify-between border-t border-slate-200 pt-2 text-sm">
            <span className="font-medium text-slate-700">{t('total')}</span>
            <span className="font-[var(--font-dash-mono)] font-bold text-slate-900">
              {formatCurrency(total)}
            </span>
          </div>
        </div>
      );
    }
    return null;
  };

  if (displayCategories.length === 0) {
    return (
      <div className="flex h-[420px] items-center justify-center rounded-2xl border border-dashed border-violet-200/70 bg-violet-50/30 text-slate-500">
        <div className="text-center">
          <div className="mx-auto mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-violet-100 text-violet-600">
            <ChartBarIcon className="h-5 w-5" />
          </div>
          <p className="text-sm font-medium">{t('selectToViewTrends')}</p>
        </div>
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={420}>
      <LineChart data={chartData} margin={{ top: 16, right: 12, left: 4, bottom: 8 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
        <XAxis
          dataKey="periodLabel"
          axisLine={{ stroke: '#cbd5e1' }}
          tick={{ fontSize: 12, fill: '#64748b' }}
          tickLine={{ stroke: '#cbd5e1' }}
        />
        <YAxis
          tickFormatter={(value) => {
            if (value >= 1000) {
              return `$${(value / 1000).toFixed(0)}k`;
            }
            return `$${value}`;
          }}
          axisLine={{ stroke: '#cbd5e1' }}
          tick={{ fontSize: 12, fill: '#64748b' }}
          tickLine={{ stroke: '#cbd5e1' }}
        />
        <Tooltip content={<CustomTooltip />} />
        <Legend wrapperStyle={{ paddingTop: '16px' }} />
        {displayCategories.map((category, index) => (
          <Line
            key={category.categoryId}
            type="monotone"
            dataKey={`cat_${category.categoryId}`}
            name={category.categoryName}
            stroke={getCategoryColor(category, index)}
            strokeWidth={2.6}
            dot={{ fill: getCategoryColor(category, index), strokeWidth: 0, r: 3.5 }}
            activeDot={{ r: 5 }}
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  );
}
