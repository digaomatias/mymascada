import { describe, expect, test } from 'vitest';
import {
  buildBudgetTriageSummary,
  computeBudgetPaceDelta,
  getBudgetRiskState,
  sortBudgetsForTriage,
} from '@/lib/budget/budget-triage';
import type { BudgetSummary } from '@/types/budget';

function createBudget(overrides: Partial<BudgetSummary>): BudgetSummary {
  return {
    id: 1,
    name: 'Base Budget',
    periodType: 'Monthly',
    startDate: '2026-02-01',
    endDate: '2026-02-28',
    isRecurring: true,
    isActive: true,
    categoryCount: 2,
    totalBudgeted: 1000,
    totalSpent: 500,
    totalRemaining: 500,
    usedPercentage: 50,
    daysRemaining: 10,
    isCurrentPeriod: true,
    ...overrides,
  };
}

describe('budget triage utility', () => {
  test('classifies risk state correctly', () => {
    expect(getBudgetRiskState(110, true)).toBe('over');
    expect(getBudgetRiskState(85, true)).toBe('risk');
    expect(getBudgetRiskState(60, true)).toBe('onTrack');
    expect(getBudgetRiskState(20, false)).toBe('inactive');
  });

  test('sorts budgets by risk priority then days remaining', () => {
    const budgets = sortBudgetsForTriage([
      createBudget({ id: 1, usedPercentage: 30, daysRemaining: 5 }),
      createBudget({ id: 2, usedPercentage: 105, daysRemaining: 8 }),
      createBudget({ id: 3, usedPercentage: 82, daysRemaining: 2 }),
      createBudget({ id: 4, usedPercentage: 84, daysRemaining: 1 }),
    ]);

    expect(budgets.map((item) => item.id)).toEqual([2, 4, 3, 1]);
  });

  test('computes triage summary and pace delta', () => {
    const summary = buildBudgetTriageSummary([
      createBudget({ id: 1, usedPercentage: 105 }),
      createBudget({ id: 2, usedPercentage: 82 }),
      createBudget({ id: 3, usedPercentage: 35, daysRemaining: 3 }),
    ]);

    expect(summary.overCount).toBe(1);
    expect(summary.atRiskCount).toBe(1);
    expect(summary.onTrackCount).toBe(1);
    expect(summary.nearestDeadline?.id).toBe(3);

    const pace = computeBudgetPaceDelta({
      totalBudgeted: 1000,
      totalSpent: 650,
      periodElapsedPercentage: 50,
    });

    expect(Math.round(pace.expectedSpent)).toBe(500);
    expect(Math.round(pace.variance)).toBe(150);
    expect(pace.isOverspendingPace).toBe(true);
  });
});
