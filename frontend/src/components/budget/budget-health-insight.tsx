'use client';

import { useTranslations } from 'next-intl';
import { computeBudgetPaceDelta } from '@/lib/budget/budget-triage';
import { formatCurrency } from '@/types/budget';
import { cn } from '@/lib/utils';
import { ArrowTrendingDownIcon, ArrowTrendingUpIcon } from '@heroicons/react/24/outline';

interface BudgetHealthInsightProps {
  totalBudgeted: number;
  totalSpent: number;
  periodElapsedPercentage: number;
}

export function BudgetHealthInsight({
  totalBudgeted,
  totalSpent,
  periodElapsedPercentage,
}: BudgetHealthInsightProps) {
  const t = useTranslations('budgets');
  const pace = computeBudgetPaceDelta({
    totalBudgeted,
    totalSpent,
    periodElapsedPercentage,
  });

  if (totalBudgeted === 0 && totalSpent === 0) {
    return (
      <div className="rounded-2xl border border-slate-200/70 bg-slate-50/50 p-4">
        <div className="flex items-start gap-2.5">
          <div className="mt-0.5 flex h-7 w-7 items-center justify-center rounded-lg bg-slate-100 text-slate-500">
            <ArrowTrendingDownIcon className="h-4 w-4" />
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-500">
              {t('detail.paceLabel')}
            </p>
            <p className="mt-1 text-sm font-medium text-slate-800">
              {t('detail.noSpendingYet')}
            </p>
          </div>
        </div>
      </div>
    );
  }

  const positive = !pace.isOverspendingPace;
  const absoluteVariance = Math.abs(pace.variance);

  return (
    <div
      className={cn(
        'rounded-2xl border p-4',
        positive
          ? 'border-emerald-200/70 bg-emerald-50/50'
          : 'border-amber-200/70 bg-amber-50/50',
      )}
    >
      <div className="flex items-start gap-2.5">
        <div
          className={cn(
            'mt-0.5 flex h-7 w-7 items-center justify-center rounded-lg',
            positive ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700',
          )}
        >
          {positive ? (
            <ArrowTrendingDownIcon className="h-4 w-4" />
          ) : (
            <ArrowTrendingUpIcon className="h-4 w-4" />
          )}
        </div>

        <div className="min-w-0 flex-1">
          <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-500">
            {t('detail.paceLabel')}
          </p>
          <p className="mt-1 text-sm font-medium text-slate-800">
            {positive
              ? t('detail.paceAhead', {
                  amount: formatCurrency(absoluteVariance),
                  percent: Math.abs(pace.variancePct).toFixed(0),
                })
              : t('detail.paceBehind', {
                  amount: formatCurrency(absoluteVariance),
                  percent: Math.abs(pace.variancePct).toFixed(0),
                })}
          </p>
          <p className="mt-1 text-xs text-slate-500">
            {t('detail.paceDetail', {
              expected: formatCurrency(pace.expectedSpent),
              actual: formatCurrency(pace.actualSpent),
            })}
          </p>
        </div>
      </div>
    </div>
  );
}
