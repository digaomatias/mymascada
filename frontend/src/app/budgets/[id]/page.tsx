'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { apiClient } from '@/lib/api-client';
import type { BudgetDetail } from '@/types/budget';
import { formatCurrency } from '@/types/budget';
import { toast } from 'sonner';
import {
  ArrowLeftIcon,
  CalendarDaysIcon,
  PencilSquareIcon,
  PlusIcon,
  TrashIcon,
} from '@heroicons/react/24/outline';
import { cn } from '@/lib/utils';
import { BudgetHealthInsight } from '@/components/budget/budget-health-insight';
import { BudgetDeleteDialog } from '@/components/budget/budget-delete-dialog';

const BUDGET_BASE = '/budgets';

function formatDateRange(startDate: string, endDate: string) {
  const start = new Date(startDate).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
  const end = new Date(endDate).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
  return `${start} \u2014 ${end}`;
}

export default function BudgetDetailPage() {
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('budgets');
  const budgetId = Number(params.id);
  const [budget, setBudget] = useState<BudgetDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (isNaN(budgetId)) {
      router.push(BUDGET_BASE);
      return;
    }

    const load = async () => {
      try {
        setLoading(true);
        const data = await apiClient.getBudget(budgetId);
        setBudget(data);
      } catch {
        toast.error(t('loadError'));
        router.push(BUDGET_BASE);
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [budgetId, router, t]);

  const handleDelete = async () => {
    if (!budget || deleting) return;
    try {
      setDeleting(true);
      await apiClient.deleteBudget(budget.id);
      toast.success(t('budgetDeleted'));
      router.push(BUDGET_BASE);
    } catch {
      toast.error(t('deleteError'));
    } finally {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <AppLayout>
        <div className="space-y-4">
          <div className="h-20 animate-pulse rounded-[24px] border border-violet-100/80 bg-white/80" />
          <div className="grid gap-4 md:grid-cols-4">
            {[1, 2, 3, 4].map((item) => (
              <div key={item} className="h-28 animate-pulse rounded-[22px] border border-violet-100/80 bg-white/80" />
            ))}
          </div>
          <div className="h-40 animate-pulse rounded-[24px] border border-violet-100/80 bg-white/80" />
        </div>
      </AppLayout>
    );
  }

  if (!budget) {
    return null;
  }

  const status =
    budget.usedPercentage >= 100
      ? { label: t('overBudget'), className: 'border-rose-200 bg-rose-50 text-rose-700' }
      : budget.usedPercentage >= 80
        ? { label: t('approaching'), className: 'border-amber-200 bg-amber-50 text-amber-700' }
        : { label: t('onTrack'), className: 'border-emerald-200 bg-emerald-50 text-emerald-700' };

  return (
    <AppLayout>
      <header className="flex flex-wrap items-start justify-between gap-4 mb-5">
          <div>
            <Link
              href={BUDGET_BASE}
              className="inline-flex items-center text-sm font-medium text-slate-500 transition-colors hover:text-violet-700"
            >
              <ArrowLeftIcon className="mr-1.5 h-4 w-4" />
              {t('backToBudgets')}
            </Link>

            <h1 className="mt-2 font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
              {budget.name}
            </h1>

            <div className="mt-2 flex flex-wrap items-center gap-2">
              <Badge variant="outline" className={cn('border text-xs font-semibold', status.className)}>
                {status.label}
              </Badge>
              {budget.isRecurring && (
                <Badge variant="secondary" className="text-xs font-semibold">
                  {t('recurring')}
                </Badge>
              )}
              <span className="inline-flex items-center gap-1 text-xs text-slate-500">
                <CalendarDaysIcon className="h-3.5 w-3.5" />
                {formatDateRange(budget.startDate, budget.endDate)}
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Link href={`${BUDGET_BASE}/${budget.id}/edit`}>
              <Button variant="outline">
                <PencilSquareIcon className="mr-1.5 h-4 w-4" />
                {t('editBudget')}
              </Button>
            </Link>
            <BudgetDeleteDialog
              budgetName={budget.name}
              onConfirm={handleDelete}
              trigger={
                <Button variant="outline" disabled={deleting} className="text-rose-700 hover:text-rose-800">
                  <TrashIcon className="mr-1.5 h-4 w-4" />
                  {t('deleteBudget')}
                </Button>
              }
            />
          </div>
      </header>

      <div className="space-y-5">
        <section className="grid gap-4 md:grid-cols-4">
          {[
            { label: t('totalBudgeted'), value: formatCurrency(budget.totalBudgeted), tone: 'text-slate-900' },
            { label: t('totalSpent'), value: formatCurrency(budget.totalSpent), tone: 'text-slate-900' },
            { label: t('totalRemaining'), value: formatCurrency(budget.totalRemaining), tone: budget.totalRemaining >= 0 ? 'text-emerald-600' : 'text-rose-600' },
            { label: t('progress'), value: `${budget.usedPercentage.toFixed(1)}%`, tone: 'text-slate-900' },
          ].map((item) => (
            <article
              key={item.label}
              className="rounded-2xl border border-violet-100/80 bg-white/90 p-4 shadow-[0_16px_34px_-28px_rgba(76,29,149,0.45)]"
            >
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-500">{item.label}</p>
              <p className={cn('mt-2 text-3xl font-semibold tracking-[-0.02em]', item.tone)}>{item.value}</p>
              {item.label === t('progress') ? (
                <p className="mt-1 text-xs text-slate-500">
                  {budget.daysRemaining > 0
                    ? budget.daysRemaining === 1
                      ? t('daysRemainingOne')
                      : t('daysRemaining', { count: budget.daysRemaining })
                    : t('periodComplete')}
                </p>
              ) : null}
            </article>
          ))}
        </section>

        <section className="rounded-[24px] border border-violet-100/80 bg-white/92 p-5 shadow-[0_18px_38px_-28px_rgba(76,29,149,0.48)]">
          <div className="flex items-center justify-between text-xs text-slate-500">
            <span>{t('overview')}</span>
            <span>{t('percentUsed', { percent: budget.usedPercentage.toFixed(1) })}</span>
          </div>
          <div className="mt-2 h-3 overflow-hidden rounded-full bg-slate-200">
            <div
              className={cn(
                'h-full rounded-full transition-all',
                budget.usedPercentage >= 100
                  ? 'bg-rose-500'
                  : budget.usedPercentage >= 80
                    ? 'bg-amber-500'
                    : 'bg-emerald-500',
              )}
              style={{ width: `${Math.min(budget.usedPercentage, 100)}%` }}
            />
          </div>
          <div className="mt-2 flex items-center justify-between text-xs text-slate-500">
            <span>{t('spentOfBudget', { spent: formatCurrency(budget.totalSpent), budget: formatCurrency(budget.totalBudgeted) })}</span>
            <span>{t('detail.elapsedOfPeriod', { percent: budget.periodElapsedPercentage.toFixed(0) })}</span>
          </div>
        </section>

        <BudgetHealthInsight
          totalBudgeted={budget.totalBudgeted}
          totalSpent={budget.totalSpent}
          periodElapsedPercentage={budget.periodElapsedPercentage}
        />

        <section className="rounded-[28px] border border-violet-100/80 bg-white/92 p-6 shadow-[0_20px_42px_-30px_rgba(76,29,149,0.45)]">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="text-2xl font-semibold tracking-[-0.02em] text-slate-900">
                {t('categories')}
              </h2>
              <p className="mt-1 text-sm text-slate-500">{t('detail.categoriesHint')}</p>
            </div>
            <Link href={`${BUDGET_BASE}/${budget.id}/edit`}>
              <Button variant="outline">
                <PlusIcon className="mr-1.5 h-4 w-4" />
                {t('addCategory')}
              </Button>
            </Link>
          </div>

          {budget.categories.length === 0 ? (
            <p className="rounded-xl border border-violet-100 bg-violet-50/40 p-5 text-sm text-slate-500">
              {t('noCategoriesInBudget')}
            </p>
          ) : (
            <div className="space-y-3">
              {budget.categories.map((category) => {
                const remainingPositive = category.remainingAmount >= 0;
                const statusClass =
                  category.status === 'Over'
                    ? 'border-rose-200 bg-rose-50 text-rose-700'
                    : category.status === 'Approaching'
                      ? 'border-amber-200 bg-amber-50 text-amber-700'
                      : 'border-emerald-200 bg-emerald-50 text-emerald-700';

                return (
                  <article
                    key={category.categoryId}
                    className="rounded-xl border border-violet-100/80 bg-white p-4"
                  >
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="text-base font-semibold text-slate-900">{category.categoryName}</p>
                        {category.parentCategoryName ? (
                          <p className="text-xs text-slate-500">{category.parentCategoryName}</p>
                        ) : null}
                      </div>
                      <Badge variant="outline" className={cn('border text-xs font-semibold', statusClass)}>
                        {category.status === 'Over'
                          ? t('overBudget')
                          : category.status === 'Approaching'
                            ? t('approaching')
                            : t('onTrack')}
                      </Badge>
                    </div>

                    <div className="mt-3 flex items-center justify-between text-sm">
                      <p className="text-slate-600">
                        {t('spentOfBudget', {
                          spent: formatCurrency(category.actualSpent),
                          budget: formatCurrency(category.effectiveBudget),
                        })}
                      </p>
                      <p className={cn('font-semibold', remainingPositive ? 'text-emerald-600' : 'text-rose-600')}>
                        {remainingPositive ? '+' : ''}
                        {formatCurrency(category.remainingAmount)}
                      </p>
                    </div>

                    <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-200">
                      <div
                        className={cn(
                          'h-full rounded-full',
                          category.status === 'Over'
                            ? 'bg-rose-500'
                            : category.status === 'Approaching'
                              ? 'bg-amber-500'
                              : 'bg-emerald-500',
                        )}
                        style={{ width: `${Math.min(category.usedPercentage, 100)}%` }}
                      />
                    </div>

                    <p className="mt-2 text-xs text-slate-500">
                      {t('detail.categoryTransactions', { count: category.transactionCount })}
                    </p>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>
    </AppLayout>
  );
}
