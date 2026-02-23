'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient } from '@/lib/api-client';
import { cn, formatCurrency } from '@/lib/utils';
import {
  LightBulbIcon,
  ArrowRightIcon,
  PlusIcon,
  WalletIcon,
} from '@heroicons/react/24/outline';

interface BudgetRow {
  id: number;
  name: string;
  totalSpent: number;
  totalBudgeted: number;
  usedPercentage: number;
}

const BAR_COLORS = ['#10b981', '#ef4444', '#f59e0b', '#3b82f6', '#8b5cf6', '#ec4899'];

export function BudgetHealthCard() {
  const t = useTranslations('dashboard.cards.budget');
  const [budgets, setBudgets] = useState<BudgetRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await apiClient.getBudgets({
          includeInactive: false,
          onlyCurrentPeriod: true,
        });
        setBudgets(
          (data || []).map((b) => ({
            id: b.id,
            name: b.name,
            totalSpent: b.totalSpent,
            totalBudgeted: b.totalBudgeted,
            usedPercentage: b.usedPercentage,
          })),
        );
      } catch (err) {
        console.error('Failed to load budgets:', err);
        setError('Failed to load budget data');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const overCount = budgets.filter((b) => b.usedPercentage >= 100).length;
  const topOverBudget = budgets.find((b) => b.usedPercentage >= 100);
  const overAmount = topOverBudget
    ? topOverBudget.totalSpent - topOverBudget.totalBudgeted
    : 0;

  return (
    <DashboardCard cardId="budget-health" loading={loading} error={error}>
      {budgets.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-violet-400 to-fuchsia-400 shadow-lg mb-4">
            <WalletIcon className="h-7 w-7 text-white" />
          </div>
          <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900 mb-2">
            {t('noBudgets')}
          </h3>
          <p className="text-sm text-slate-500 mb-4">{t('noBudgetsDesc')}</p>
          <Link
            href="/budgets/new"
            className="inline-flex items-center gap-1 rounded-lg bg-violet-600 px-4 py-2 text-sm font-semibold text-white hover:bg-violet-700"
          >
            <PlusIcon className="h-4 w-4" />
            {t('createBudget')}
          </Link>
        </div>
      ) : (
        <>
          <div className="flex items-center justify-between">
            <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold tracking-[-0.02em] text-slate-900">
              {t('title')}
            </h3>
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-violet-500">
              {overCount > 0
                ? t('overLimit', { count: overCount, total: budgets.length })
                : t('allOnTrack')}
            </p>
          </div>

          <div className="mt-5 space-y-3">
            {budgets.slice(0, 4).map((row, i) => {
              const usage = Math.round(row.usedPercentage);
              const over = usage > 100;
              const color = over ? '#ef4444' : BAR_COLORS[i % BAR_COLORS.length];
              return (
                <Link key={row.id} href={`/budgets/${row.id}`} className="block">
                  <div className="mb-1.5 flex items-center justify-between text-xs">
                    <span className="font-medium text-slate-600">{row.name}</span>
                    <span
                      className={cn(
                        'font-[var(--font-dash-mono)] font-semibold',
                        over ? 'text-rose-600' : 'text-slate-500',
                      )}
                    >
                      {formatCurrency(row.totalSpent)} / {formatCurrency(row.totalBudgeted)}
                    </span>
                  </div>
                  <div className="h-2 rounded-full bg-slate-100">
                    <div
                      className="h-2 rounded-full transition-all duration-500"
                      style={{
                        width: `${Math.min(usage, 100)}%`,
                        backgroundColor: color,
                      }}
                    />
                  </div>
                </Link>
              );
            })}
          </div>

          {/* What-if simulation hint */}
          {topOverBudget && (
            <div className="mt-5 rounded-2xl border border-violet-200/50 bg-gradient-to-br from-violet-50/80 to-fuchsia-50/40 p-4">
              <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.1em] text-violet-600">
                <LightBulbIcon className="h-3.5 w-3.5" />
                {t('whatIf')}
              </div>
              <p className="mt-2 text-sm leading-relaxed text-slate-700">
                {t('whatIfDesc', {
                  name: topOverBudget.name,
                  amount: formatCurrency(overAmount),
                })}
              </p>
              <div className="mt-3">
                <Link
                  href="/budgets"
                  className="inline-flex items-center gap-1 rounded-lg bg-violet-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-violet-700"
                >
                  {t('adjustBudget')}
                  <ArrowRightIcon className="h-3 w-3" />
                </Link>
              </div>
            </div>
          )}
        </>
      )}
    </DashboardCard>
  );
}
