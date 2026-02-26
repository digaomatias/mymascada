'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { CategorySpendingChart } from '@/components/charts/category-spending-chart';
import { PeriodSelector, PeriodType } from '@/components/analytics/period-selector';
import { apiClient } from '@/lib/api-client';
import { cn, formatCurrency } from '@/lib/utils';
import { useAuth } from '@/contexts/auth-context';
import {
  ArrowRightIcon,
  ArrowTrendingDownIcon,
  ArrowTrendingUpIcon,
  BanknotesIcon,
  CalendarIcon,
  ChartBarIcon,
  CurrencyDollarIcon,
  SparklesIcon,
} from '@heroicons/react/24/outline';
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

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

const PANEL_CLASS =
  'rounded-[26px] border border-violet-100/70 bg-white/92 p-5 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs';

const STAT_CARD_CLASS =
  'rounded-2xl border border-violet-100/80 bg-white/92 p-4 shadow-[0_16px_34px_-26px_rgba(76,29,149,0.4)]';

interface StatCardProps {
  label: string;
  value: string;
  hint?: string;
  tone: string;
  iconBg: string;
  iconColor: string;
  icon: React.ComponentType<React.SVGProps<SVGSVGElement>>;
}

function StatCard({ label, value, hint, tone, iconBg, iconColor, icon: Icon }: StatCardProps) {
  return (
    <article className={STAT_CARD_CLASS}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{label}</p>
          <p className={cn('mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold', tone)}>{value}</p>
          {hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>}
        </div>
        <div className={cn('flex h-10 w-10 items-center justify-center rounded-xl', iconBg)}>
          <Icon className={cn('h-5 w-5', iconColor)} />
        </div>
      </div>
    </article>
  );
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

      const trends: MonthlyTrend[] = [];

      let monthsToLoad: number;
      let startMonth: number;
      let startYear: number;

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
          month -= 12;
          year += 1;
        }

        try {
          const summary = (await apiClient.getMonthlySummary(year, month)) as any;
          trends.push({
            month: new Date(year, month - 1).toLocaleDateString('en-US', {
              month: 'short',
              year: '2-digit',
            }),
            income: summary.totalIncome || 0,
            expenses: Math.abs(summary.totalExpenses || 0),
            net: summary.netAmount || 0,
          });
        } catch {
          // Ignore missing months.
        }
      }

      setMonthlyTrends(trends);

      const categoryMonth = timeRange === 'quarter' ? quarterStartMonth : selectedMonth;
      const currentSummary = (await apiClient.getMonthlySummary(selectedYear, categoryMonth)) as any;
      setCategoryData(currentSummary?.topCategories ?? []);

      const yearlyComparison: YearlyComparison[] = [];
      for (let y = selectedYear - 2; y <= selectedYear; y++) {
        let yearIncome = 0;
        let yearExpenses = 0;

        for (let m = 1; m <= 12; m++) {
          try {
            const summary = (await apiClient.getMonthlySummary(y, m)) as any;
            yearIncome += summary.totalIncome || 0;
            yearExpenses += Math.abs(summary.totalExpenses || 0);
          } catch {
            // Ignore missing months.
          }
        }

        yearlyComparison.push({
          year: y,
          totalIncome: yearIncome,
          totalExpenses: yearExpenses,
          netIncome: yearIncome - yearExpenses,
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
        <div className="rounded-xl border border-violet-100/70 bg-white/98 p-3 shadow-[0_18px_36px_-26px_rgba(76,29,149,0.55)] backdrop-blur-xs">
          <p className="font-[var(--font-dash-sans)] text-sm font-semibold text-slate-900">{label}</p>
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

  const totalIncome = monthlyTrends.reduce((sum, m) => sum + m.income, 0);
  const totalExpenses = monthlyTrends.reduce((sum, m) => sum + m.expenses, 0);
  const avgMonthlyIncome = monthlyTrends.length > 0 ? totalIncome / monthlyTrends.length : 0;
  const avgMonthlyExpenses = monthlyTrends.length > 0 ? totalExpenses / monthlyTrends.length : 0;
  const netAmount = totalIncome - totalExpenses;
  const savingsRate = totalIncome > 0 ? (netAmount / totalIncome) * 100 : 0;
  const totalCategorySpend = categoryData.reduce((sum, item) => sum + Math.abs(item.amount), 0);

  const bestMonth = useMemo(
    () => (monthlyTrends.length > 0 ? monthlyTrends.reduce((best, cur) => (cur.net > best.net ? cur : best)) : null),
    [monthlyTrends],
  );

  const lowestMonth = useMemo(
    () =>
      monthlyTrends.length > 0
        ? monthlyTrends.reduce((worst, cur) => (cur.net < worst.net ? cur : worst))
        : null,
    [monthlyTrends],
  );

  const bestYear = useMemo(
    () => (yearlyData.length > 0 ? yearlyData.reduce((best, cur) => (cur.netIncome > best.netIncome ? cur : best)) : null),
    [yearlyData],
  );

  if (isLoading || loading) {
    return (
      <AppLayout>
        <div className="animate-pulse space-y-5">
          <div className="h-10 w-64 rounded-xl bg-slate-200" />
          <div className="h-20 rounded-[24px] border border-violet-100/80 bg-white/85" />
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div
                key={`metric-${i}`}
                className="h-32 rounded-2xl border border-violet-100/80 bg-white/85"
              />
            ))}
          </div>
          <div className="grid gap-6 xl:grid-cols-3">
            <div className="h-[390px] rounded-[26px] border border-violet-100/80 bg-white/85 xl:col-span-2" />
            <div className="h-[390px] rounded-[26px] border border-violet-100/80 bg-white/85" />
            <div className="h-[390px] rounded-[26px] border border-violet-100/80 bg-white/85 xl:col-span-2" />
            <div className="h-[390px] rounded-[26px] border border-violet-100/80 bg-white/85" />
          </div>
        </div>
      </AppLayout>
    );
  }

  const insightTone = netAmount >= 0 ? 'emerald' : 'amber';

  return (
    <AppLayout>
      <header className="mb-5 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
            {t('title')}
          </h1>
          <p className="mt-1.5 text-[15px] text-slate-500">{t('subtitle')}</p>
        </div>

        <Link href="/analytics/trends">
          <Button variant="outline" className="gap-2">
            {t('categoryTrends.viewCategoryTrends')}
            <ArrowRightIcon className="h-4 w-4" />
          </Button>
        </Link>
      </header>

      <PeriodSelector
        period={timeRange}
        selectedYear={selectedYear}
        selectedMonth={selectedMonth}
        onPeriodChange={setTimeRange}
        onYearChange={setSelectedYear}
        onMonthChange={setSelectedMonth}
      />

      <section
        className={cn(
          'mb-6 rounded-2xl border p-4',
          insightTone === 'emerald' ? 'border-emerald-200/75 bg-emerald-50/55' : 'border-amber-200/75 bg-amber-50/55',
        )}
      >
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div className="flex items-start gap-3">
            <div
              className={cn(
                'mt-0.5 flex h-9 w-9 items-center justify-center rounded-lg',
                insightTone === 'emerald' ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700',
              )}
            >
              <SparklesIcon className="h-5 w-5" />
            </div>
            <div>
              <p className="text-sm font-semibold text-slate-800">
                {netAmount >= 0 ? t('onTrack') : t('behindSchedule')}
              </p>
              <p className="text-sm text-slate-600">
                {t('summary.totalSaved')}: <span className="font-[var(--font-dash-mono)]">{formatCurrency(netAmount)}</span>{' '}
                • {t('summary.savingsRate')}: <span className="font-[var(--font-dash-mono)]">{savingsRate.toFixed(1)}%</span>
              </p>
            </div>
          </div>

          <p className="text-xs font-medium uppercase tracking-[0.08em] text-slate-500">
            {t('dateRange')} • {monthlyTrends.length}
          </p>
        </div>
      </section>

      <section className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label={t('summary.avgMonthlyIncome')}
          value={formatCurrency(avgMonthlyIncome)}
          hint={t('charts.income')}
          tone="text-emerald-600"
          iconBg="bg-emerald-50"
          iconColor="text-emerald-600"
          icon={ArrowTrendingUpIcon}
        />

        <StatCard
          label={t('summary.avgMonthlyExpenses')}
          value={formatCurrency(avgMonthlyExpenses)}
          hint={t('charts.expenses')}
          tone="text-rose-600"
          iconBg="bg-rose-50"
          iconColor="text-rose-600"
          icon={ArrowTrendingDownIcon}
        />

        <StatCard
          label={t('summary.totalSaved')}
          value={formatCurrency(netAmount)}
          hint={netAmount >= 0 ? t('onTrack') : t('behindSchedule')}
          tone={netAmount >= 0 ? 'text-emerald-600' : 'text-rose-600'}
          iconBg="bg-violet-50"
          iconColor="text-violet-600"
          icon={BanknotesIcon}
        />

        <StatCard
          label={t('stats.totalSpending')}
          value={formatCurrency(totalCategorySpend)}
          hint={`${categoryData.length} ${t('byCategory').toLowerCase()}`}
          tone="text-slate-900"
          iconBg="bg-slate-100"
          iconColor="text-slate-700"
          icon={CurrencyDollarIcon}
        />
      </section>

      <div className="grid gap-6 xl:grid-cols-3">
        <section className={cn(PANEL_CLASS, 'xl:col-span-2')}>
          <div className="flex items-center justify-between gap-3">
            <h2 className="flex items-center gap-2 font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
              <ChartBarIcon className="h-5 w-5 text-violet-600" />
              {t('charts.incomeVsExpenses')}
            </h2>
            <span className="text-xs uppercase tracking-[0.08em] text-slate-500">{t('trends')}</span>
          </div>

          <div className="mt-4 h-[330px] rounded-2xl bg-slate-50/55 p-2">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={monthlyTrends} margin={{ top: 8, right: 8, left: -12, bottom: 4 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                <XAxis dataKey="month" tick={{ fontSize: 12, fill: '#64748b' }} tickLine={false} axisLine={false} />
                <YAxis
                  tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`}
                  tick={{ fontSize: 12, fill: '#64748b' }}
                  tickLine={false}
                  axisLine={false}
                />
                <Tooltip content={<CustomTooltip />} />
                <Legend />
                <Area
                  type="monotone"
                  dataKey="income"
                  stroke="#10b981"
                  fill="#10b981"
                  fillOpacity={0.22}
                  strokeWidth={2.5}
                  name={t('charts.income')}
                />
                <Area
                  type="monotone"
                  dataKey="expenses"
                  stroke="#f43f5e"
                  fill="#f43f5e"
                  fillOpacity={0.18}
                  strokeWidth={2.5}
                  name={t('charts.expenses')}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </section>

        <section className={PANEL_CLASS}>
          <div className="flex items-center justify-between gap-3">
            <h2 className="flex items-center gap-2 font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
              <CalendarIcon className="h-5 w-5 text-violet-600" />
              {t('charts.netIncomeTrend')}
            </h2>
          </div>

          <div className="mt-4 h-[240px] rounded-2xl bg-slate-50/55 p-2">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={monthlyTrends} margin={{ top: 10, right: 8, left: -12, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                <XAxis dataKey="month" tick={{ fontSize: 11, fill: '#64748b' }} tickLine={false} axisLine={false} />
                <YAxis
                  tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`}
                  tick={{ fontSize: 11, fill: '#64748b' }}
                  tickLine={false}
                  axisLine={false}
                />
                <Tooltip content={<CustomTooltip />} />
                <Line
                  type="monotone"
                  dataKey="net"
                  stroke="#6366f1"
                  strokeWidth={2.7}
                  dot={{ fill: '#6366f1', r: 3.5 }}
                  activeDot={{ r: 5 }}
                  name={t('charts.netIncome')}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>

          <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-1">
            <div className="rounded-xl border border-violet-100/80 bg-violet-50/35 p-3">
              <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{t('stats.highestMonth')}</p>
              <p className="mt-1 font-[var(--font-dash-mono)] text-sm font-semibold text-slate-900">
                {bestMonth ? `${bestMonth.month} • ${formatCurrency(bestMonth.net)}` : '—'}
              </p>
            </div>
            <div className="rounded-xl border border-violet-100/80 bg-violet-50/35 p-3">
              <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{t('stats.lowestMonth')}</p>
              <p className="mt-1 font-[var(--font-dash-mono)] text-sm font-semibold text-slate-900">
                {lowestMonth ? `${lowestMonth.month} • ${formatCurrency(lowestMonth.net)}` : '—'}
              </p>
            </div>
          </div>
        </section>

        <section className={cn(PANEL_CLASS, 'xl:col-span-2')}>
          {categoryData.length > 0 ? (
            <CategorySpendingChart data={categoryData} title={t('charts.spendingByCategory')} />
          ) : (
            <div className="flex h-[320px] items-center justify-center rounded-2xl border border-dashed border-violet-200/80 bg-violet-50/30 text-slate-500">
              <p className="text-sm font-medium">{t('charts.noCategoryData')}</p>
            </div>
          )}
        </section>

        <section className={PANEL_CLASS}>
          <div className="flex items-center justify-between gap-3">
            <h2 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
              {t('charts.yearlyComparison')}
            </h2>
          </div>

          <div className="mt-4 h-[240px] rounded-2xl bg-slate-50/55 p-2">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={yearlyData} margin={{ top: 10, right: 8, left: -14, bottom: 4 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                <XAxis dataKey="year" tick={{ fontSize: 12, fill: '#64748b' }} tickLine={false} axisLine={false} />
                <YAxis
                  tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`}
                  tick={{ fontSize: 11, fill: '#64748b' }}
                  tickLine={false}
                  axisLine={false}
                />
                <Tooltip content={<CustomTooltip />} />
                <Legend />
                <Bar dataKey="totalIncome" fill="#10b981" name={t('charts.income')} radius={[7, 7, 0, 0]} />
                <Bar dataKey="totalExpenses" fill="#f43f5e" name={t('charts.expenses')} radius={[7, 7, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>

          <div className="mt-4 rounded-xl border border-violet-100/80 bg-violet-50/35 p-3">
            <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">{t('summary.totalSaved')}</p>
            <p className="mt-1 font-[var(--font-dash-mono)] text-sm font-semibold text-slate-900">
              {bestYear ? `${bestYear.year} • ${formatCurrency(bestYear.netIncome)}` : '—'}
            </p>
          </div>
        </section>
      </div>
    </AppLayout>
  );
}
