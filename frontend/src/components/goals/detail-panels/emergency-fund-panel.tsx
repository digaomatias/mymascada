'use client';

import { useEffect, useState } from 'react';
import {
  apiClient,
  type GoalDetail,
  type EmergencyFundAnalysisDto,
} from '@/lib/api-client';
import type { GoalContext } from '@/lib/goals/goal-type-config';
import { formatCurrency } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';

interface EmergencyFundPanelProps {
  goal: GoalDetail;
  ctx: GoalContext;
}

const MONTH_NAMES = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
];

function LoadingSkeleton() {
  return (
    <div className="rounded-[24px] border border-teal-100/60 bg-white/90 p-6 shadow-sm backdrop-blur-xs">
      <div className="space-y-6">
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-3 w-full rounded-full" />
        <div className="space-y-3">
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-8 w-full" />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Skeleton className="h-20 rounded-2xl" />
          <Skeleton className="h-20 rounded-2xl" />
        </div>
      </div>
    </div>
  );
}

export function EmergencyFundPanel({ goal }: EmergencyFundPanelProps) {
  const [analysis, setAnalysis] = useState<EmergencyFundAnalysisDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function fetchAnalysis() {
      try {
        setIsLoading(true);
        setError(null);
        const data = await apiClient.getEmergencyFundAnalysis(goal.id);
        if (!cancelled) {
          setAnalysis(data);
        }
      } catch (err) {
        if (!cancelled) {
          setError('Unable to load emergency fund analysis.');
          console.error('Failed to fetch emergency fund analysis:', err);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    fetchAnalysis();
    return () => { cancelled = true; };
  }, [goal.id]);

  if (isLoading) {
    return <LoadingSkeleton />;
  }

  if (error || !analysis) {
    return (
      <div className="rounded-[24px] border border-teal-100/60 bg-white/90 p-6 shadow-sm backdrop-blur-xs">
        <p className="text-sm text-slate-500">{error ?? 'No analysis data available.'}</p>
      </div>
    );
  }

  // Insufficient transaction data fallback
  if (analysis.transactionMonthsAvailable < 1) {
    return (
      <div className="rounded-[24px] border border-teal-100/60 bg-white/90 p-6 shadow-sm backdrop-blur-xs">
        <div className="space-y-3">
          <h3 className="text-base font-semibold text-slate-900">Emergency Fund Analysis</h3>
          <p className="text-sm text-slate-500">
            Add more transactions for a personalized analysis. We need at least one month of
            expense data to calculate your emergency coverage.
          </p>
          {analysis.onboardingMonthlyExpenses > 0 && (
            <div className="mt-4 rounded-2xl border border-slate-100 bg-slate-50/60 p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                Current Baseline (from onboarding)
              </p>
              <p className="mt-1 font-[var(--font-dash-sans)] text-xl font-bold text-slate-900">
                {formatCurrency(analysis.onboardingMonthlyExpenses)}
                <span className="text-sm font-normal text-slate-400">/month</span>
              </p>
            </div>
          )}
        </div>
      </div>
    );
  }

  const maxExpense = Math.max(...analysis.monthlyBreakdown.map((m) => m.totalExpenses), 1);
  const coveragePct = Math.min((analysis.monthsCovered / 6) * 100, 100);
  const target3MPct = analysis.recommendedTarget3M > 0
    ? Math.min((analysis.currentAmount / analysis.recommendedTarget3M) * 100, 100)
    : 0;
  const target6MPct = analysis.recommendedTarget6M > 0
    ? Math.min((analysis.currentAmount / analysis.recommendedTarget6M) * 100, 100)
    : 0;

  return (
    <div className="rounded-[24px] border border-teal-100/60 bg-white/90 p-6 shadow-sm backdrop-blur-xs">
      <div className="space-y-6">
        {/* Coverage Section */}
        <div className="space-y-3">
          <h3 className="text-base font-semibold text-slate-900">Emergency Coverage</h3>
          <div className="flex items-baseline gap-2">
            <span className="font-[var(--font-dash-sans)] text-3xl font-bold tracking-tight text-teal-700">
              {analysis.monthsCovered.toFixed(1)}
            </span>
            <span className="text-sm text-slate-500">
              of 6 months
            </span>
          </div>
          <div className="h-3 overflow-hidden rounded-full bg-slate-100">
            <div
              className="h-full rounded-full bg-teal-500 transition-all duration-500"
              style={{ width: `${coveragePct}%` }}
            />
          </div>
          <p className="text-sm text-slate-500">
            Based on your average monthly expenses of{' '}
            <span className="font-medium text-slate-700">
              {formatCurrency(analysis.averageMonthlyExpenses)}
            </span>
          </p>
        </div>

        {/* Monthly Expense Trend */}
        {analysis.monthlyBreakdown.length > 0 && (
          <div className="space-y-3">
            <h3 className="text-base font-semibold text-slate-900">Monthly Expenses</h3>
            <div className="space-y-2">
              {analysis.monthlyBreakdown.map((month) => {
                const barPct = (month.totalExpenses / maxExpense) * 100;
                const label = `${MONTH_NAMES[month.month - 1]} ${month.year}`;
                return (
                  <div key={`${month.year}-${month.month}`} className="flex items-center gap-3">
                    <span className="w-20 shrink-0 text-xs text-slate-500">{label}</span>
                    <div className="relative h-6 flex-1 overflow-hidden rounded-md bg-slate-50">
                      <div
                        className="absolute inset-y-0 left-0 rounded-md bg-teal-100 transition-all duration-300"
                        style={{ width: `${barPct}%` }}
                      />
                      <span className="absolute inset-y-0 left-2 flex items-center font-[var(--font-dash-sans)] text-xs font-medium text-slate-700">
                        {formatCurrency(month.totalExpenses)}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Recurring Expenses */}
        {analysis.activeRecurringCount > 0 && (
          <div className="rounded-2xl border border-slate-100 bg-slate-50/60 p-4">
            <p className="text-sm text-slate-600">
              <span className="font-medium text-slate-900">{analysis.activeRecurringCount}</span>{' '}
              active recurring expenses totaling{' '}
              <span className="font-[var(--font-dash-sans)] font-medium text-slate-900">
                {formatCurrency(analysis.monthlyRecurringTotal)}
              </span>
              /month
            </p>
          </div>
        )}

        {/* Recommended Targets */}
        <div className="space-y-3">
          <h3 className="text-base font-semibold text-slate-900">Recommended Targets</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {/* 3-Month Target */}
            <div className="rounded-2xl border border-teal-100 bg-teal-50/40 p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-teal-600">
                3-Month Target
              </p>
              <p className="mt-1 font-[var(--font-dash-sans)] text-xl font-bold text-slate-900">
                {formatCurrency(analysis.recommendedTarget3M)}
              </p>
              <div className="mt-2 h-2 overflow-hidden rounded-full bg-teal-100">
                <div
                  className="h-full rounded-full bg-teal-500 transition-all duration-500"
                  style={{ width: `${target3MPct}%` }}
                />
              </div>
              <p className="mt-1 text-xs text-slate-500">
                {target3MPct >= 100
                  ? 'Target reached'
                  : `${formatCurrency(Math.max(analysis.recommendedTarget3M - analysis.currentAmount, 0))} to go`}
              </p>
            </div>

            {/* 6-Month Target */}
            <div className="rounded-2xl border border-slate-100 bg-slate-50/40 p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-500">
                6-Month Target
              </p>
              <p className="mt-1 font-[var(--font-dash-sans)] text-xl font-bold text-slate-900">
                {formatCurrency(analysis.recommendedTarget6M)}
              </p>
              <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-200">
                <div
                  className="h-full rounded-full bg-teal-500 transition-all duration-500"
                  style={{ width: `${target6MPct}%` }}
                />
              </div>
              <p className="mt-1 text-xs text-slate-500">
                {target6MPct >= 100
                  ? 'Target reached'
                  : `${formatCurrency(Math.max(analysis.recommendedTarget6M - analysis.currentAmount, 0))} to go`}
              </p>
            </div>
          </div>
        </div>

        {/* LLM Essential Analysis Section */}
        {analysis.essentialAnalysis && (
          <div className="space-y-4">
            <h3 className="text-base font-semibold text-slate-900">
              Essential vs. Discretionary
            </h3>

            {/* Summary Amounts */}
            <div className="grid grid-cols-2 gap-3">
              <div className="rounded-2xl border border-teal-100 bg-teal-50/40 p-4">
                <p className="text-xs font-semibold uppercase tracking-[0.12em] text-teal-600">
                  Essentials
                </p>
                <p className="mt-1 font-[var(--font-dash-sans)] text-xl font-bold text-slate-900">
                  {formatCurrency(analysis.essentialAnalysis.estimatedMonthlyEssentials)}
                </p>
                <p className="text-xs text-slate-400">/month</p>
              </div>
              <div className="rounded-2xl border border-slate-100 bg-slate-50/40 p-4">
                <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-500">
                  Discretionary
                </p>
                <p className="mt-1 font-[var(--font-dash-sans)] text-xl font-bold text-slate-900">
                  {formatCurrency(analysis.essentialAnalysis.estimatedMonthlyDiscretionary)}
                </p>
                <p className="text-xs text-slate-400">/month</p>
              </div>
            </div>

            {/* Category List */}
            {analysis.essentialAnalysis.categories.length > 0 && (
              <div className="space-y-1.5">
                {analysis.essentialAnalysis.categories.map((cat) => (
                  <div
                    key={cat.categoryName}
                    className="flex items-center justify-between rounded-xl px-3 py-2 text-sm hover:bg-slate-50"
                  >
                    <div className="flex items-center gap-2">
                      <span className="text-slate-700">{cat.categoryName}</span>
                      <span
                        className={
                          cat.isEssential
                            ? 'inline-flex rounded-full bg-teal-50 px-2 py-0.5 text-[11px] font-medium text-teal-700'
                            : 'inline-flex rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500'
                        }
                      >
                        {cat.isEssential ? 'Essential' : 'Discretionary'}
                      </span>
                    </div>
                    <span className="font-[var(--font-dash-sans)] text-sm font-medium text-slate-700">
                      {formatCurrency(cat.monthlyAverage)}
                    </span>
                  </div>
                ))}
              </div>
            )}

            {/* AI Reasoning */}
            {analysis.essentialAnalysis.reasoning && (
              <div className="rounded-2xl border border-slate-100 bg-slate-50/40 p-4">
                <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                  AI Analysis
                </p>
                <p className="mt-2 text-sm leading-relaxed text-slate-600">
                  {analysis.essentialAnalysis.reasoning}
                </p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
