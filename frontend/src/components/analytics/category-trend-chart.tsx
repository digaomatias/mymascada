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
        <div className="bg-white p-4 rounded-lg shadow-lg border border-gray-200 max-w-xs">
          <p className="font-semibold text-gray-900 mb-2">{label}</p>
          {payload.map((entry: any, index: number) => {
            const percentage = total > 0 ? ((entry.value / total) * 100).toFixed(1) : 0;
            return (
              <div
                key={index}
                className="flex justify-between items-center text-sm py-1"
              >
                <div className="flex items-center gap-2">
                  <div
                    className="w-3 h-3 rounded-full"
                    style={{ backgroundColor: entry.color }}
                  />
                  <span className="text-gray-700 truncate max-w-[120px]">{entry.name}</span>
                </div>
                <div className="text-right">
                  <span className="font-medium text-gray-900">{formatCurrency(entry.value)}</span>
                  <span className="text-gray-500 ml-1">({percentage}%)</span>
                </div>
              </div>
            );
          })}
          <div className="mt-2 pt-2 border-t border-gray-200 flex justify-between items-center text-sm">
            <span className="font-medium text-gray-700">{t('total')}</span>
            <span className="font-bold text-gray-900">{formatCurrency(total)}</span>
          </div>
        </div>
      );
    }
    return null;
  };

  if (displayCategories.length === 0) {
    return (
      <div className="h-[500px] flex items-center justify-center text-gray-500">
        <p>{t('selectToViewTrends')}</p>
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={500}>
      <LineChart data={chartData} margin={{ top: 20, right: 30, left: 20, bottom: 20 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
        <XAxis
          dataKey="periodLabel"
          tick={{ fontSize: 12 }}
          tickLine={{ stroke: '#e5e7eb' }}
        />
        <YAxis
          tickFormatter={(value) => {
            if (value >= 1000) {
              return `$${(value / 1000).toFixed(0)}k`;
            }
            return `$${value}`;
          }}
          tick={{ fontSize: 12 }}
          tickLine={{ stroke: '#e5e7eb' }}
        />
        <Tooltip content={<CustomTooltip />} />
        <Legend wrapperStyle={{ paddingTop: '20px' }} />
        {displayCategories.map((category, index) => (
          <Line
            key={category.categoryId}
            type="monotone"
            dataKey={`cat_${category.categoryId}`}
            name={category.categoryName}
            stroke={getCategoryColor(category, index)}
            strokeWidth={2}
            dot={{ fill: getCategoryColor(category, index), strokeWidth: 2, r: 4 }}
            activeDot={{ r: 6, strokeWidth: 2 }}
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  );
}
