'use client';

import React, { useState, useEffect } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { CategorySpendingChart } from '@/components/charts/category-spending-chart';
import { PeriodSelector, PeriodType } from '@/components/analytics/period-selector';
import { apiClient } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  CalendarIcon,
  ChartBarIcon,
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
  BanknotesIcon,
  CurrencyDollarIcon,
  ArrowRightIcon
} from '@heroicons/react/24/outline';
import {
  LineChart,
  Line,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
  BarChart,
  Bar
} from 'recharts';
import { useTranslations } from 'next-intl';

interface MonthlyTrend {
  month: string;
  income: number;
  expenses: number;
  net: number;
}

interface YearlyComparison {
  year: number;
  totalIncome: number;
  totalExpenses: number;
  netIncome: number;
}

export default function AnalyticsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('analytics');
  const [selectedYear, setSelectedYear] = useState(new Date().getFullYear());
  const [selectedMonth, setSelectedMonth] = useState(new Date().getMonth() + 1);
  const [monthlyTrends, setMonthlyTrends] = useState<MonthlyTrend[]>([]);
  const [yearlyData, setYearlyData] = useState<YearlyComparison[]>([]);
  const [categoryData, setCategoryData] = useState<{
    categoryId: number;
    categoryName: string;
    categoryColor?: string;
    amount: number;
    transactionCount: number;
    percentage: number;
  }[]>([]);
  const [loading, setLoading] = useState(true);
  const [timeRange, setTimeRange] = useState<PeriodType>('year');

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated) {
      loadAnalyticsData();
    }
  }, [isAuthenticated, selectedYear, selectedMonth, timeRange]);

  const loadAnalyticsData = async () => {
    try {
      setLoading(true);

      // Load monthly trends for the selected period
      const trends: MonthlyTrend[] = [];

      // Calculate start month and months to load based on period type
      let monthsToLoad: number;
      let startMonth: number;
      let startYear: number;

      // Get the quarter from selected month (1-4)
      const selectedQuarter = Math.ceil(selectedMonth / 3);
      const quarterStartMonth = (selectedQuarter - 1) * 3 + 1;

      switch (timeRange) {
        case 'month':
          monthsToLoad = 1;
          startMonth = selectedMonth;
          startYear = selectedYear;
          break;
        case 'quarter':
          monthsToLoad = 3;
          startMonth = quarterStartMonth;
          startYear = selectedYear;
          break;
        case 'year':
          monthsToLoad = 12;
          startMonth = 1;
          startYear = selectedYear;
          break;
        case 'all':
        default:
          monthsToLoad = 24;
          startMonth = 1;
          startYear = selectedYear - 1;
          break;
      }

      for (let i = 0; i < monthsToLoad; i++) {
        let month = startMonth + i;
        let year = startYear;

        while (month > 12) {
          month = month - 12;
          year = year + 1;
        }

        try {
          const summary = await apiClient.getMonthlySummary(year, month) as any;
          trends.push({
            month: new Date(year, month - 1).toLocaleDateString('en-US', { month: 'short', year: '2-digit' }),
            income: summary.totalIncome || 0,
            expenses: Math.abs(summary.totalExpenses || 0),
            net: summary.netAmount || 0
          });
        } catch {
          // Skip months without data
        }
      }

      setMonthlyTrends(trends);

      // Load category spending for selected month (or first month of selected period)
      const categoryMonth = timeRange === 'quarter' ? quarterStartMonth : selectedMonth;
      const currentSummary = await apiClient.getMonthlySummary(
        selectedYear,
        categoryMonth
      ) as any;

      if (currentSummary?.topCategories) {
        setCategoryData(currentSummary.topCategories);
      }
      
      // Load yearly comparison (last 3 years)
      const yearlyComparison: YearlyComparison[] = [];
      for (let y = selectedYear - 2; y <= selectedYear; y++) {
        let yearIncome = 0;
        let yearExpenses = 0;
        
        for (let m = 1; m <= 12; m++) {
          try {
            const summary = await apiClient.getMonthlySummary(y, m) as any;
            yearIncome += summary.totalIncome || 0;
            yearExpenses += Math.abs(summary.totalExpenses || 0);
          } catch {
            // Skip months without data
          }
        }
        
        yearlyComparison.push({
          year: y,
          totalIncome: yearIncome,
          totalExpenses: yearExpenses,
          netIncome: yearIncome - yearExpenses
        });
      }
      
      setYearlyData(yearlyComparison);
      
    } catch (error) {
      console.error('Failed to load analytics data:', error);
    } finally {
      setLoading(false);
    }
  };

  const CustomTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload[0]) {
      return (
        <div className="bg-white p-3 rounded-lg shadow-lg border border-gray-200">
          <p className="font-semibold text-gray-900">{label}</p>
          {payload.map((entry: any, index: number) => (
            <p key={index} className="text-sm" style={{ color: entry.color }}>
              {entry.name}: {formatCurrency(entry.value)}
            </p>
          ))}
        </div>
      );
    }
    return null;
  };

  if (isLoading || loading) {
    return (
      <AppLayout>
          <div className="animate-pulse">
            <div className="h-8 bg-gray-300 rounded w-1/3 mb-8"></div>
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              {Array.from({ length: 4 }).map((_, i) => (
                <Card key={i} className="bg-white/90">
                  <CardHeader>
                    <div className="h-6 bg-gray-300 rounded w-1/2"></div>
                  </CardHeader>
                  <CardContent>
                    <div className="h-64 bg-gray-200 rounded"></div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </div>
      </AppLayout>
    );
  }

  const totalIncome = monthlyTrends.reduce((sum, m) => sum + m.income, 0);
  const totalExpenses = monthlyTrends.reduce((sum, m) => sum + m.expenses, 0);
  const avgMonthlyIncome = monthlyTrends.length > 0 ? totalIncome / monthlyTrends.length : 0;
  const avgMonthlyExpenses = monthlyTrends.length > 0 ? totalExpenses / monthlyTrends.length : 0;

  return (
    <AppLayout>
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">{t('title')}</h1>
          <p className="text-gray-600">{t('subtitle')}</p>
        </div>

        {/* Period Selector */}
        <PeriodSelector
          period={timeRange}
          selectedYear={selectedYear}
          selectedMonth={selectedMonth}
          onPeriodChange={setTimeRange}
          onYearChange={setSelectedYear}
          onMonthChange={setSelectedMonth}
        />

        {/* Summary Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('summary.avgMonthlyIncome')}</p>
                  <p className="text-2xl font-bold text-green-600">{formatCurrency(avgMonthlyIncome)}</p>
                </div>
                <ArrowTrendingUpIcon className="w-8 h-8 text-green-500" />
              </div>
            </CardContent>
          </Card>

          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('summary.avgMonthlyExpenses')}</p>
                  <p className="text-2xl font-bold text-red-600">{formatCurrency(avgMonthlyExpenses)}</p>
                </div>
                <ArrowTrendingDownIcon className="w-8 h-8 text-red-500" />
              </div>
            </CardContent>
          </Card>

          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('summary.totalSaved')}</p>
                  <p className="text-2xl font-bold text-blue-600">
                    {formatCurrency(totalIncome - totalExpenses)}
                  </p>
                </div>
                <BanknotesIcon className="w-8 h-8 text-blue-500" />
              </div>
            </CardContent>
          </Card>

          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('summary.savingsRate')}</p>
                  <p className="text-2xl font-bold text-purple-600">
                    {totalIncome > 0 ? ((totalIncome - totalExpenses) / totalIncome * 100).toFixed(1) : 0}%
                  </p>
                </div>
                <CurrencyDollarIcon className="w-8 h-8 text-purple-500" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Charts Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Income vs Expenses Trend */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <ChartBarIcon className="w-5 h-5" />
                {t('charts.incomeVsExpenses')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <AreaChart data={monthlyTrends}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                  <XAxis dataKey="month" tick={{ fontSize: 12 }} />
                  <YAxis tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`} tick={{ fontSize: 12 }} />
                  <Tooltip content={<CustomTooltip />} />
                  <Legend />
                  <Area
                    type="monotone"
                    dataKey="income"
                    stackId="1"
                    stroke="#10B981"
                    fill="#10B981"
                    fillOpacity={0.6}
                    name={t('charts.income')}
                  />
                  <Area
                    type="monotone"
                    dataKey="expenses"
                    stackId="2"
                    stroke="#EF4444"
                    fill="#EF4444"
                    fillOpacity={0.6}
                    name={t('charts.expenses')}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>

          {/* Net Income Trend */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CalendarIcon className="w-5 h-5" />
                {t('charts.netIncomeTrend')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <LineChart data={monthlyTrends}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                  <XAxis dataKey="month" tick={{ fontSize: 12 }} />
                  <YAxis tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`} tick={{ fontSize: 12 }} />
                  <Tooltip content={<CustomTooltip />} />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="net"
                    stroke="#3B82F6"
                    strokeWidth={3}
                    dot={{ fill: '#3B82F6' }}
                    name={t('charts.netIncome')}
                  />
                </LineChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>

          {/* Category Spending Chart */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle>{t('charts.spendingByCategory')}</CardTitle>
            </CardHeader>
            <CardContent>
              {categoryData.length > 0 ? (
                <CategorySpendingChart data={categoryData} title="" />
              ) : (
                <div className="text-center py-8 text-gray-500">
                  {t('charts.noCategoryData')}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Yearly Comparison */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle>{t('charts.yearlyComparison')}</CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={yearlyData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                  <XAxis dataKey="year" tick={{ fontSize: 12 }} />
                  <YAxis tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`} tick={{ fontSize: 12 }} />
                  <Tooltip content={<CustomTooltip />} />
                  <Legend />
                  <Bar dataKey="totalIncome" fill="#10B981" name={t('charts.income')} radius={[8, 8, 0, 0]} />
                  <Bar dataKey="totalExpenses" fill="#EF4444" name={t('charts.expenses')} radius={[8, 8, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </div>

        {/* Category Trends Link */}
        <div className="mt-8">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900">{t('categoryTrends.title')}</h3>
                  <p className="text-sm text-gray-600">{t('categoryTrends.subtitle')}</p>
                </div>
                <Link href="/analytics/trends">
                  <Button variant="primary" className="flex items-center gap-2">
                    {t('categoryTrends.viewCategoryTrends')}
                    <ArrowRightIcon className="w-4 h-4" />
                  </Button>
                </Link>
              </div>
            </CardContent>
          </Card>
        </div>
    </AppLayout>
  );
}