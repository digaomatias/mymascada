'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { useDashboard } from '@/contexts/dashboard-context';
import { apiClient, GoalSummary } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import {
  ExclamationTriangleIcon,
  CheckCircleIcon,
  SparklesIcon,
  ArrowRightIcon,
} from '@heroicons/react/24/outline';
import { SparklesIcon as SparklesSolid } from '@heroicons/react/24/solid';

interface Insight {
  type: 'warning' | 'positive';
  text: string;
  actionLabel?: string;
  actionHref?: string;
}

function getInsights(
  goals: GoalSummary[],
  monthlyIncome: number,
  monthlyExpenses: number,
  budgets: { name: string; usedPercentage: number; totalSpent: number; totalBudgeted: number }[],
): Insight[] {
  const insights: Insight[] = [];

  // Over-budget insight
  const overBudget = budgets.filter((b) => b.usedPercentage >= 100);
  if (overBudget.length > 0) {
    const top = overBudget[0];
    const overAmount = top.totalSpent - top.totalBudgeted;
    insights.push({
      type: 'warning',
      text: `${top.name} is $${overAmount.toFixed(0)} over budget. Cutting back could help reach your goals faster.`,
      actionLabel: `Review ${top.name.toLowerCase()}`,
      actionHref: '/budgets',
    });
  }

  // Savings rate insight
  if (monthlyIncome > 0) {
    const savingsRate = Math.round(
      ((monthlyIncome - monthlyExpenses) / monthlyIncome) * 100,
    );
    if (savingsRate > 0) {
      insights.push({
        type: 'positive',
        text: `Savings rate at ${savingsRate}% this month. Keep it up!`,
        actionLabel: 'Set a savings goal',
        actionHref: '/goals/new',
      });
    } else if (savingsRate < 0) {
      insights.push({
        type: 'warning',
        text: 'Spending is exceeding income this month. Review your expenses.',
        actionLabel: 'Review expenses',
        actionHref: '/transactions',
      });
    }
  }

  // Goal progress
  if (goals.length > 0) {
    const primary = [...goals].sort(
      (a, b) => b.progressPercentage - a.progressPercentage,
    )[0];
    if (primary.progressPercentage > 0 && primary.progressPercentage < 100) {
      insights.push({
        type: 'positive',
        text: `${primary.name} is ${primary.progressPercentage.toFixed(0)}% funded (${formatCurrency(primary.currentAmount)} of ${formatCurrency(primary.targetAmount)}).`,
        actionLabel: 'View goal',
        actionHref: `/goals/${primary.id}`,
      });
    }
  }

  return insights.slice(0, 2);
}

export function AICoachCard() {
  const { period } = useDashboard();
  const t = useTranslations('dashboard.cards.coach');
  const [insights, setInsights] = useState<Insight[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setError(null);

        const now = new Date();
        const currentMonth = now.getMonth() + 1;
        const currentYear = now.getFullYear();

        const [goals, summary, budgets] = await Promise.all([
          apiClient.getGoals({ includeCompleted: false }).catch(() => [] as GoalSummary[]),
          apiClient
            .getMonthlySummary(currentYear, currentMonth)
            .catch(() => ({ totalIncome: 0, totalExpenses: 0 })) as Promise<{
            totalIncome: number;
            totalExpenses: number;
          }>,
          apiClient
            .getBudgets({ includeInactive: false, onlyCurrentPeriod: true })
            .catch(() => []) as Promise<
            { name: string; usedPercentage: number; totalSpent: number; totalBudgeted: number }[]
          >,
        ]);

        const income = summary.totalIncome || 0;
        const expenses = summary.totalExpenses || 0;

        setInsights(getInsights(goals, income, expenses, budgets));
      } catch (err) {
        console.error('Failed to load coach data:', err);
        setError('Failed to load insights');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [period]);

  return (
    <DashboardCard cardId="ai-coach" loading={loading} error={error} gradient>
      <div className="pointer-events-none absolute -right-16 -top-16 h-48 w-48 rounded-full bg-violet-200/20 blur-3xl" aria-hidden />

      <div className="relative">
        <div className="flex items-center gap-2.5">
          <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-500 shadow-sm">
            <SparklesSolid className="h-4.5 w-4.5 text-white" />
          </div>
          <div>
            <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold tracking-[-0.02em] text-slate-900">
              {t('title')}
            </h3>
            <p className="text-xs text-slate-400">{t('subtitle')}</p>
          </div>
        </div>

        <div className="mt-5 space-y-3">
          {insights.length === 0 ? (
            <div className="rounded-2xl border border-emerald-200/50 bg-emerald-50/40 p-4">
              <div className="flex items-start gap-2.5">
                <CheckCircleIcon className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                <p className="text-[15px] leading-relaxed text-slate-700">
                  {t('noInsights')}
                </p>
              </div>
            </div>
          ) : (
            insights.map((insight, i) => (
              <div
                key={i}
                className={
                  insight.type === 'warning'
                    ? 'rounded-2xl border border-violet-200/50 bg-white/80 p-4'
                    : 'rounded-2xl border border-emerald-200/50 bg-emerald-50/40 p-4'
                }
              >
                <div className="flex items-start gap-2.5">
                  {insight.type === 'warning' ? (
                    <ExclamationTriangleIcon className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" />
                  ) : (
                    <CheckCircleIcon className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                  )}
                  <div className="min-w-0 flex-1">
                    <p className="text-[15px] leading-relaxed text-slate-700">
                      {insight.text}
                    </p>
                    {insight.actionLabel && insight.actionHref && (
                      <div className="mt-2.5">
                        <Link
                          href={insight.actionHref}
                          className={`inline-flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-white transition-all ${
                            insight.type === 'warning'
                              ? 'bg-violet-600 hover:bg-violet-700'
                              : 'bg-emerald-600 hover:bg-emerald-700'
                          }`}
                        >
                          {insight.actionLabel}
                          <ArrowRightIcon className="h-3 w-3" />
                        </Link>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            ))
          )}
        </div>

        <div className="mt-4">
          <Link
            href="/chat"
            className="inline-flex items-center gap-2 rounded-xl border border-violet-200/60 bg-white/80 px-4 py-2.5 text-sm font-semibold text-violet-600 transition-all hover:-translate-y-0.5 hover:bg-violet-50 hover:shadow-sm"
          >
            <SparklesIcon className="h-4 w-4" />
            {t('askAbout')}
          </Link>
        </div>
      </div>
    </DashboardCard>
  );
}
