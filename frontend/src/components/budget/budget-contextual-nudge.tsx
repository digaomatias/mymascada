'use client';

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import type { BudgetSummary } from '@/types/budget';
import { formatCurrency } from '@/types/budget';
import { getBudgetRiskState } from '@/lib/budget/budget-triage';
import { LightBulbIcon } from '@heroicons/react/24/outline';
import { cn } from '@/lib/utils';

interface BudgetContextualNudgeProps {
  budgets: BudgetSummary[];
  basePath: string;
}

export function BudgetContextualNudge({ budgets, basePath }: BudgetContextualNudgeProps) {
  const t = useTranslations('budgets');

  const activeBudgets = budgets.filter((b) => b.isActive && b.isCurrentPeriod);

  const overBudget = activeBudgets.find((b) => getBudgetRiskState(b.usedPercentage, b.isActive) === 'over');
  const atRisk = activeBudgets.find((b) => getBudgetRiskState(b.usedPercentage, b.isActive) === 'risk');
  const endingSoon = activeBudgets.find((b) => b.daysRemaining <= 7 && b.daysRemaining > 0);

  let message: string;
  let ctaLabel: string | null = null;
  let ctaHref: string | null = null;
  let tone: 'amber' | 'emerald' = 'emerald';

  if (overBudget) {
    const overAmount = Math.abs(overBudget.totalRemaining);
    message = t('nudge.overLimit', { name: overBudget.name, amount: formatCurrency(overAmount) });
    ctaLabel = t('nudge.reviewSpending');
    ctaHref = `${basePath}/${overBudget.id}`;
    tone = 'amber';
  } else if (atRisk) {
    message = t('nudge.atRisk', { name: atRisk.name, percent: atRisk.usedPercentage.toFixed(0) });
    ctaLabel = t('nudge.viewDetails');
    ctaHref = `${basePath}/${atRisk.id}`;
    tone = 'amber';
  } else if (endingSoon) {
    message = t('nudge.endingSoon', { days: endingSoon.daysRemaining });
    ctaLabel = t('nudge.review');
    ctaHref = `${basePath}/${endingSoon.id}`;
    tone = 'emerald';
  } else {
    message = t('nudge.allOnTrack');
    tone = 'emerald';
  }

  if (activeBudgets.length === 0) return null;

  return (
    <section
      className={cn(
        'rounded-2xl border p-4',
        tone === 'amber'
          ? 'border-amber-200/70 bg-amber-50/50'
          : 'border-emerald-200/70 bg-emerald-50/50',
      )}
    >
      <div className="flex items-start gap-3">
        <div
          className={cn(
            'mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg',
            tone === 'amber' ? 'bg-amber-100 text-amber-700' : 'bg-emerald-100 text-emerald-700',
          )}
        >
          <LightBulbIcon className="h-[18px] w-[18px]" />
        </div>

        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-slate-800">{message}</p>
          {ctaLabel && ctaHref && (
            <Link
              href={ctaHref}
              className={cn(
                'mt-1.5 inline-flex items-center text-xs font-semibold transition-colors',
                tone === 'amber'
                  ? 'text-amber-700 hover:text-amber-800'
                  : 'text-emerald-700 hover:text-emerald-800',
              )}
            >
              {ctaLabel} →
            </Link>
          )}
        </div>
      </div>
    </section>
  );
}
