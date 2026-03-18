'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient } from '@/lib/api-client';
import { formatCurrency, cn } from '@/lib/utils';
import type { DashboardSummaryResponse } from '@/types/api-responses';

type Period = 'month' | 'quarter';

interface RunwayData {
  totalBalance: number;
  monthlyExpenses: number;
  monthlyIncome: number;
  runwayMonths: number;
  savingsRate: number;
  netSaved: number;
}

export function FinancialRunwayCard() {
  const [period, setPeriod] = useState<Period>('month');
  const t = useTranslations('dashboard.cards.runway');
  const [data, setData] = useState<RunwayData | null>(null);
  const [summaryData, setSummaryData] = useState<DashboardSummaryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setError(null);

        // Only fetch from the API once; reuse on period toggle
        let summary = summaryData;
        if (!summary) {
          summary = await apiClient.getDashboardSummary();
          setSummaryData(summary);
        }

        // The API returns monthly averages; for quarter view, multiply by 3
        const multiplier = period === 'quarter' ? 3 : 1;
        const income = summary.avgMonthlyIncome * multiplier;
        const expenses = summary.avgMonthlyExpenses * multiplier;
        const netSaved = income - expenses;
        const savingsRate = income > 0 ? Math.round((netSaved / income) * 100) : 0;

        setData({
          totalBalance: summary.totalBalance,
          monthlyExpenses: expenses,
          monthlyIncome: income,
          runwayMonths: summary.runwayMonths,
          savingsRate,
          netSaved,
        });
      } catch (err) {
        console.error('Failed to load runway data:', err);
        setError('Failed to load financial data');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [period, summaryData]);

  const tDashboard = useTranslations('dashboard');

  return (
    <DashboardCard cardId="financial-runway" loading={loading} error={error}>
      {data && (
        <div className="relative">
          <div className="relative">
            {/* Runway headline */}
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-sm font-semibold text-ink-500">{t('title')}</p>
                <div className="mt-2 flex items-baseline gap-3">
                  <p className="font-[var(--font-dash-mono)] text-5xl font-semibold tracking-[-0.02em] text-ink-900 sm:text-[3.2rem]">
                    {data.runwayMonths === 0 ? '0' : data.runwayMonths.toFixed(1)}
                  </p>
                  <p className="text-xl font-medium text-ink-400">{t('months')}</p>
                </div>
                <p className="mt-1.5 max-w-sm text-[15px] leading-relaxed text-ink-500">
                  {t('description', { months: data.runwayMonths === 0 ? '0' : data.runwayMonths.toFixed(1) })}
                </p>
              </div>
              {/* Inline period toggle */}
              <div className="inline-flex rounded-xl border border-primary-200/60 bg-white/90 p-1 shadow-sm backdrop-blur-sm">
                {(['month', 'quarter'] as const).map((p) => (
                  <button
                    key={p}
                    type="button"
                    onClick={() => setPeriod(p)}
                    className={cn(
                      'rounded-lg px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.12em] transition-all',
                      period === p
                        ? 'bg-primary-600 text-white shadow-sm'
                        : 'text-ink-500 hover:bg-primary-50 hover:text-primary-700',
                    )}
                  >
                    {tDashboard(p === 'month' ? 'periodMonth' : 'periodQuarter')}
                  </button>
                ))}
              </div>
            </div>

            {/* Progress bar */}
            <div className="mt-5">
              <div className="flex items-center justify-between text-xs font-medium text-ink-400">
                <span>{t('zeroMonths')}</span>
                <span className="text-primary-500">{t('targetRange')}</span>
              </div>
              <div className="mt-1.5 h-3 overflow-hidden rounded-full bg-primary-100/50">
                <div
                  className="relative h-full rounded-full bg-gradient-to-r from-primary-600 to-primary-300 transition-all duration-1000"
                  style={{ width: `${Math.min((data.runwayMonths / 6) * 100, 100)}%` }}
                >
                  <div className="absolute inset-0 animate-pulse rounded-full bg-white/20" />
                </div>
              </div>
              <div className="mt-2 flex items-center justify-between">
                <p className="text-xs text-ink-400">
                  <span className="font-[var(--font-dash-mono)] font-semibold text-ink-600">
                    {formatCurrency(data.totalBalance)}
                  </span>{' '}
                  {t('acrossAccounts')}
                </p>
              </div>
            </div>

            {/* KPI row */}
            <div className="mt-6 grid grid-cols-2 gap-4 rounded-2xl border border-primary-100/50 bg-primary-50/20 p-4 sm:grid-cols-4">
              {[
                { label: t('earned'), value: formatCurrency(data.monthlyIncome), accent: 'text-emerald-700' },
                { label: t('spent'), value: formatCurrency(data.monthlyExpenses), accent: 'text-ink-900' },
                { label: t('kept'), value: formatCurrency(data.netSaved), accent: 'text-sky-700' },
                { label: t('saved'), value: `${data.savingsRate}%`, accent: 'text-amber-700' },
              ].map((kpi) => (
                <div key={kpi.label}>
                  <p className="text-xs font-semibold uppercase tracking-wide text-ink-400">{kpi.label}</p>
                  <p className={`mt-1.5 font-[var(--font-dash-mono)] text-xl font-semibold ${kpi.accent}`}>
                    {kpi.value}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </DashboardCard>
  );
}
