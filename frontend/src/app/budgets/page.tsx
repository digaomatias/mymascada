'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { apiClient } from '@/lib/api-client';
import type { BudgetSummary } from '@/types/budget';
import { formatCurrency } from '@/types/budget';
import { toast } from 'sonner';
import {
  ArrowRightIcon,
  CalendarDaysIcon,
  PlusIcon,
  SparklesIcon,
  TagIcon,
} from '@heroicons/react/24/outline';
import {
  getBudgetRiskState,
  sortBudgetsForTriage,
} from '@/lib/budget/budget-triage';
import { BudgetContextualNudge } from '@/components/budget/budget-contextual-nudge';
import { cn } from '@/lib/utils';

const BUDGET_BASE = '/budgets';

function statusClasses(state: ReturnType<typeof getBudgetRiskState>) {
  if (state === 'over') {
    return 'border-rose-200 bg-rose-50 text-rose-700';
  }
  if (state === 'risk') {
    return 'border-amber-200 bg-amber-50 text-amber-700';
  }
  if (state === 'inactive') {
    return 'border-slate-200 bg-slate-100 text-slate-600';
  }
  return 'border-emerald-200 bg-emerald-50 text-emerald-700';
}

function statusLabel(
  t: ReturnType<typeof useTranslations>,
  state: ReturnType<typeof getBudgetRiskState>,
) {
  if (state === 'over') return t('overBudget');
  if (state === 'risk') return t('approaching');
  if (state === 'inactive') return t('inactive');
  return t('onTrack');
}

function formatDateRange(startDate: string, endDate: string) {
  const start = new Date(startDate).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
  });
  const end = new Date(endDate).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
  });
  return `${start} - ${end}`;
}

export default function BudgetsPage() {
  const t = useTranslations('budgets');
  const [budgets, setBudgets] = useState<BudgetSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showInactive, setShowInactive] = useState(false);
  const [currentPeriodOnly, setCurrentPeriodOnly] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        setIsLoading(true);
        const data = await apiClient.getBudgets({
          includeInactive: showInactive,
          onlyCurrentPeriod: currentPeriodOnly,
        });
        setBudgets(data);
      } catch {
        toast.error(t('loadError'));
      } finally {
        setIsLoading(false);
      }
    };

    load();
  }, [showInactive, currentPeriodOnly, t]);

  const sortedBudgets = useMemo(() => sortBudgetsForTriage(budgets), [budgets]);

  return (
    <AppLayout>
      <header className="flex flex-wrap items-end justify-between gap-4 mb-5">
          <div>
            <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
              {t('title')}
            </h1>
            <p className="mt-1.5 text-[15px] text-slate-500">{t('subtitle')}</p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <div className="flex items-center gap-4 text-sm">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="showInactive"
                  checked={showInactive}
                  onCheckedChange={(checked) => setShowInactive(checked === true)}
                />
                <Label htmlFor="showInactive" className="text-sm text-slate-600">
                  {t('filters.showInactive')}
                </Label>
              </div>

              <div className="flex items-center gap-2">
                <Checkbox
                  id="currentPeriodOnly"
                  checked={currentPeriodOnly}
                  onCheckedChange={(checked) => setCurrentPeriodOnly(checked === true)}
                />
                <Label htmlFor="currentPeriodOnly" className="text-sm text-slate-600">
                  {t('filters.currentPeriodOnly')}
                </Label>
              </div>
            </div>

            <Link href={`${BUDGET_BASE}/new`}>
              <Button>
                <PlusIcon className="mr-1.5 h-4 w-4" />
                {t('createBudget')}
              </Button>
            </Link>
          </div>
      </header>

      <div className="space-y-5">
        <BudgetContextualNudge budgets={sortedBudgets} basePath={BUDGET_BASE} />

        {isLoading ? (
          <div className="grid gap-4 md:grid-cols-2">
            {[1, 2, 3, 4].map((item) => (
              <div
                key={item}
                className="h-56 animate-pulse rounded-[24px] border border-violet-100/80 bg-white/80"
              />
            ))}
          </div>
        ) : sortedBudgets.length === 0 ? (
          <section className="rounded-[28px] border border-violet-100/80 bg-white/92 p-10 text-center shadow-[0_20px_50px_-30px_rgba(76,29,149,0.45)]">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-violet-100 text-violet-600">
              <SparklesIcon className="h-7 w-7" />
            </div>
            <h3 className="mt-4 text-xl font-semibold text-slate-900">{t('noBudgets')}</h3>
            <p className="mt-2 text-sm text-slate-500">{t('noBudgetsDescription')}</p>
            <Link href={`${BUDGET_BASE}/new`} className="mt-5 inline-flex">
              <Button>
                <PlusIcon className="mr-1.5 h-4 w-4" />
                {t('createFirstBudget')}
              </Button>
            </Link>
          </section>
        ) : (
          <section className="grid gap-4 md:grid-cols-2">
            {sortedBudgets.map((budget) => {
              const state = getBudgetRiskState(budget.usedPercentage, budget.isActive);
              const amountTone = budget.totalRemaining >= 0 ? 'text-emerald-600' : 'text-rose-600';

              return (
                <article
                  key={budget.id}
                  className="rounded-[26px] border border-violet-100/80 bg-white/90 p-5 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)]"
                >
                  <div className="flex items-start justify-between gap-3">
                    <h3 className="line-clamp-2 text-[1.45rem] font-semibold tracking-[-0.03em] text-slate-900">
                      {budget.name}
                    </h3>
                    <span
                      className={cn(
                        'shrink-0 rounded-full border px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.08em]',
                        statusClasses(state),
                      )}
                    >
                      {statusLabel(t, state)}
                    </span>
                  </div>

                  <div className="mt-4">
                    <div className="flex items-center justify-between text-sm">
                      <p className="text-slate-600">
                        {t('spentOfBudget', {
                          spent: formatCurrency(budget.totalSpent),
                          budget: formatCurrency(budget.totalBudgeted),
                        })}
                      </p>
                      <p className={cn('font-semibold', amountTone)}>
                        {budget.totalRemaining >= 0 ? '+' : ''}
                        {formatCurrency(budget.totalRemaining)}
                      </p>
                    </div>
                    <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-200">
                      <div
                        className={cn(
                          'h-full rounded-full transition-all',
                          state === 'over'
                            ? 'bg-rose-500'
                            : state === 'risk'
                              ? 'bg-amber-500'
                              : 'bg-emerald-500',
                        )}
                        style={{ width: `${Math.min(budget.usedPercentage, 100)}%` }}
                      />
                    </div>
                  </div>

                  <div className="mt-4 flex flex-wrap items-center gap-3 text-xs text-slate-500">
                    <span className="inline-flex items-center gap-1">
                      <CalendarDaysIcon className="h-3.5 w-3.5" />
                      {formatDateRange(budget.startDate, budget.endDate)}
                    </span>
                    <span className="inline-flex items-center gap-1">
                      <TagIcon className="h-3.5 w-3.5" />
                      {budget.categoryCount === 1
                        ? t('categoryCountOne')
                        : t('categoryCount', { count: budget.categoryCount })}
                    </span>
                    {budget.isCurrentPeriod && (
                      <span>
                        {budget.daysRemaining === 1
                          ? t('daysRemainingOne')
                          : t('daysRemaining', { count: budget.daysRemaining })}
                      </span>
                    )}
                  </div>

                  <div className="mt-4 flex items-center justify-end">
                    <Link
                      href={`${BUDGET_BASE}/${budget.id}`}
                      className="inline-flex items-center gap-1 text-xs font-semibold text-violet-600 transition-colors hover:text-violet-700"
                    >
                      {t('viewDetails')}
                      <ArrowRightIcon className="h-3.5 w-3.5" />
                    </Link>
                  </div>
                </article>
              );
            })}
          </section>
        )}
      </div>
    </AppLayout>
  );
}
