'use client';

import React, { useState } from 'react';
import {
  PieChart,
  Pie,
  Cell,
  ResponsiveContainer,
  Tooltip,
  Legend,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid
} from 'recharts';
import { Button } from '@/components/ui/button';
import { formatCurrency } from '@/lib/utils';
import { ChartBarIcon, ChartPieIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

interface CategoryData {
  categoryId: number;
  categoryName: string;
  categoryColor?: string;
  amount: number;
  transactionCount: number;
  percentage: number;
}

interface CategorySpendingChartProps {
  data: CategoryData[];
  title?: string;
}

// Default color palette for categories without colors
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

export function CategorySpendingChart({ data, title }: CategorySpendingChartProps) {
  const t = useTranslations('dashboard');
  const [chartType, setChartType] = useState<'pie' | 'bar'>('pie');
  const displayTitle = title || t('spendingByCategory');
  
  // Prepare data for charts
  const chartData = data.map((item, index) => ({
    ...item,
    displayName: item.categoryName,
    value: Math.abs(item.amount), // Use absolute values for display
    color: item.categoryColor || DEFAULT_COLORS[index % DEFAULT_COLORS.length]
  }));

  // Custom tooltip
  const CustomTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ payload: CategoryData & { displayName: string; value: number; color: string } }> }) => {
    if (active && payload && payload[0]) {
      const data = payload[0].payload;
      return (
        <div className="bg-white p-3 rounded-lg shadow-lg border border-gray-200">
          <p className="font-semibold text-gray-900">{data.displayName}</p>
          <p className="text-sm text-gray-600">
            Amount: {formatCurrency(data.value)}
          </p>
          <p className="text-sm text-gray-600">
            Percentage: {data.percentage.toFixed(1)}%
          </p>
          <p className="text-sm text-gray-600">
            Transactions: {data.transactionCount}
          </p>
        </div>
      );
    }
    return null;
  };

  // Custom label for pie chart
  const renderCustomLabel = (entry: any) => {
    const percentage = (entry.percent * 100);
    if (percentage < 5) return null; // Don't show label for small slices
    return `${percentage.toFixed(0)}%`;
  };

  if (!data || data.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-gray-500">{t('noSpendingData')}</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold text-gray-900">{displayTitle}</h3>
        <div className="flex gap-2">
          <Button
            variant={chartType === 'pie' ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => setChartType('pie')}
            className="flex items-center gap-1"
          >
            <ChartPieIcon className="w-4 h-4" />
            {t('pie')}
          </Button>
          <Button
            variant={chartType === 'bar' ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => setChartType('bar')}
            className="flex items-center gap-1"
          >
            <ChartBarIcon className="w-4 h-4" />
            {t('bar')}
          </Button>
        </div>
      </div>

      <div className="bg-white rounded-lg p-4">
        {chartType === 'pie' ? (
          <ResponsiveContainer width="100%" height={300}>
            <PieChart>
              <Pie
                data={chartData}
                cx="50%"
                cy="50%"
                labelLine={false}
                label={renderCustomLabel}
                outerRadius={100}
                fill="#8884d8"
                dataKey="value"
                nameKey="displayName"
              >
                {chartData.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={entry.color} />
                ))}
              </Pie>
              <Tooltip content={<CustomTooltip />} />
              <Legend
                verticalAlign="bottom"
                height={36}
              />
            </PieChart>
          </ResponsiveContainer>
        ) : (
          <ResponsiveContainer width="100%" height={300}>
            <BarChart
              data={chartData}
              margin={{ top: 20, right: 30, left: 20, bottom: 60 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
              <XAxis 
                dataKey="displayName" 
                angle={-45}
                textAnchor="end"
                height={100}
                tick={{ fontSize: 12 }}
              />
              <YAxis 
                tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`}
                tick={{ fontSize: 12 }}
              />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="value" radius={[8, 8, 0, 0]}>
                {chartData.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={entry.color} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        )}
      </div>

      {/* Summary Stats */}
      <div className="grid grid-cols-2 gap-4 text-sm">
        <div className="bg-gray-50 rounded-lg p-3">
          <p className="text-gray-600">{t('totalSpending')}</p>
          <p className="text-lg font-bold text-gray-900">
            {formatCurrency(chartData.reduce((sum, item) => sum + item.value, 0))}
          </p>
        </div>
        <div className="bg-gray-50 rounded-lg p-3">
          <p className="text-gray-600">{t('categories')}</p>
          <p className="text-lg font-bold text-gray-900">{data.length}</p>
        </div>
      </div>
    </div>
  );
}