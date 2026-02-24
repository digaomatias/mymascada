import type { BudgetDetail, BudgetSummary } from '@/types/budget';

export type BudgetRiskState = 'over' | 'risk' | 'onTrack' | 'inactive';

export interface BudgetPaceDelta {
  expectedSpent: number;
  actualSpent: number;
  variance: number;
  variancePct: number;
  isOverspendingPace: boolean;
}

export interface BudgetTriageSummary {
  overCount: number;
  atRiskCount: number;
  onTrackCount: number;
  inactiveCount: number;
  nearestDeadline: BudgetSummary | null;
}

export function getBudgetRiskState(
  usedPercentage: number,
  isActive: boolean,
): BudgetRiskState {
  if (!isActive) return 'inactive';
  if (usedPercentage >= 100) return 'over';
  if (usedPercentage >= 80) return 'risk';
  return 'onTrack';
}

export function getBudgetPriorityScore(
  usedPercentage: number,
  isActive: boolean,
): number {
  const state = getBudgetRiskState(usedPercentage, isActive);
  switch (state) {
    case 'over':
      return 400;
    case 'risk':
      return 300;
    case 'onTrack':
      return 200;
    case 'inactive':
      return 100;
    default:
      return 0;
  }
}

export function sortBudgetsForTriage<T extends BudgetSummary>(budgets: T[]): T[] {
  return [...budgets].sort((a, b) => {
    const aPriority = getBudgetPriorityScore(a.usedPercentage, a.isActive);
    const bPriority = getBudgetPriorityScore(b.usedPercentage, b.isActive);

    if (aPriority !== bPriority) {
      return bPriority - aPriority;
    }

    return a.daysRemaining - b.daysRemaining;
  });
}

export function buildBudgetTriageSummary(
  budgets: BudgetSummary[],
): BudgetTriageSummary {
  const summary: BudgetTriageSummary = {
    overCount: 0,
    atRiskCount: 0,
    onTrackCount: 0,
    inactiveCount: 0,
    nearestDeadline: null,
  };

  budgets.forEach((budget) => {
    const state = getBudgetRiskState(budget.usedPercentage, budget.isActive);
    if (state === 'over') summary.overCount += 1;
    if (state === 'risk') summary.atRiskCount += 1;
    if (state === 'onTrack') summary.onTrackCount += 1;
    if (state === 'inactive') summary.inactiveCount += 1;
  });

  const currentActive = budgets
    .filter((budget) => budget.isActive && budget.isCurrentPeriod)
    .sort((a, b) => a.daysRemaining - b.daysRemaining);

  summary.nearestDeadline = currentActive[0] ?? null;
  return summary;
}

export function computeBudgetPaceDelta(
  budget: Pick<BudgetDetail, 'totalBudgeted' | 'totalSpent' | 'periodElapsedPercentage'>,
): BudgetPaceDelta {
  const elapsedRatio = Math.max(0, Math.min(budget.periodElapsedPercentage, 100)) / 100;
  const expectedSpent = budget.totalBudgeted * elapsedRatio;
  const actualSpent = budget.totalSpent;
  const variance = actualSpent - expectedSpent;
  const variancePct = expectedSpent > 0 ? (variance / expectedSpent) * 100 : 0;

  return {
    expectedSpent,
    actualSpent,
    variance,
    variancePct,
    isOverspendingPace: variance > 0,
  };
}
