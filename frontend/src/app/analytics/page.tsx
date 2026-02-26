'use client';

import React, { useState, useEffect } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { CategorySpendingChart } from '@/components/charts/category-spending-chart';
import { PeriodSelector, PeriodType } from '@/components/analytics/period-selector';
import { apiClient } from '@/lib/api-client';
import { formatCurrency, cn } from '@/lib/utils';
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
        <div className="rounded-xl border border-violet-100/60 bg-white p-3 shadow-lg shadow-violet-200/20">
          <p className="font-[var(--font-dash-sans)] font-semibold text-slate-900">{label}</p>
          {payload.map((entry: any, index: number) => (
            <p key={index} className="font-[var(--font-dash-mono)] text-sm" style={{ color: entry.color }}>
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
            <div className="h-8 bg-slate-200 rounded w-1/3 mb-8"></div>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={`stat-${i}`} className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20">
                  <div className="h-4 bg-slate-200 rounded w-2/3 mb-3"></div>
                  <div className="h-7 bg-slate-100 rounded w-1/2"></div>
                </div>
              ))}
            </div>
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={`chart-${i}`} className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20">
                  <div className="h-6 bg-slate-200 rounded w-1/2 mb-6"></div>
                  <div className="h-64 bg-slate-100 rounded-xl"></div>
                </div>
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
        <div className="mb-5">
          <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">{t('title')}</h1>
          <p className="text-[15px] text-slate-500 mt-1.5">{t('subtitle')}</p>
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
          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{t('summary.avgMonthlyIncome')}</p>
                  <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-emerald-600">{formatCurrency(avgMonthlyIncome)}</p>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-50">
                  <ArrowTrendingUpIcon className="w-5 h-5 text-emerald-600" />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{t('summary.avgMonthlyExpenses')}</p>
                  <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-red-600">{formatCurrency(avgMonthlyExpenses)}</p>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-red-50">
                  <ArrowTrendingDownIcon className="w-5 h-5 text-red-600" />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{t('summary.totalSaved')}</p>
                  <p className={cn(
                    'mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold',
                    (totalIncome - totalExpenses) >= 0 ? 'text-emerald-600' : 'text-red-600'
                  )}>
                    {formatCurrency(totalIncome - totalExpenses)}
                  </p>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-50">
                  <BanknotesIcon className="w-5 h-5 text-violet-600" />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{t('summary.savingsRate')}</p>
                  <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-violet-600">
                    {totalIncome > 0 ? ((totalIncome - totalExpenses) / totalIncome * 100).toFixed(1) : 0}%
                  </p>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-50">
                  <CurrencyDollarIcon className="w-5 h-5 text-violet-600" />
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Charts Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Income vs Expenses Trend */}
          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardHeader>
              <CardTitle className="font-[var(--font-dash-sans)] flex items-center gap-2 text-slate-900">
                <ChartBarIcon className="w-5 h-5 text-violet-500" />
                {t('charts.incomeVsExpenses')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <AreaChart data={monthlyTrends}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
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
          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardHeader>
              <CardTitle className="font-[var(--font-dash-sans)] flex items-center gap-2 text-slate-900">
                <CalendarIcon className="w-5 h-5 text-violet-500" />
                {t('charts.netIncomeTrend')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <LineChart data={monthlyTrends}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
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
          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardHeader>
              <CardTitle className="font-[var(--font-dash-sans)] text-slate-900">{t('charts.spendingByCategory')}</CardTitle>
            </CardHeader>
            <CardContent>
              {categoryData.length > 0 ? (
                <CategorySpendingChart data={categoryData} title="" />
              ) : (
                <div className="text-center py-8 text-slate-500">
                  {t('charts.noCategoryData')}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Yearly Comparison */}
          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardHeader>
              <CardTitle className="font-[var(--font-dash-sans)] text-slate-900">{t('charts.yearlyComparison')}</CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={yearlyData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
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
        <div className="mt-6">
          <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">{t('categoryTrends.title')}</h3>
                  <p className="text-sm text-slate-500">{t('categoryTrends.subtitle')}</p>
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