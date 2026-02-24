import { describe, expect, test } from 'vitest';
import {
  buildDashboardNudges,
  pickNextBestAction,
} from '@/lib/dashboard/education-nudges';

describe('dashboard education nudges', () => {
  test('returns negative cashflow as highest-priority critical nudge', () => {
    const nudges = buildDashboardNudges({
      monthlyIncome: 3000,
      monthlyExpenses: 3600,
      runwayMonths: 2,
      savingsRate: -20,
      hasEmergencyFundGoal: true,
      emergencyFundProgress: 30,
      overBudgetCount: 1,
      largestOverBudgetAmount: 200,
    });

    expect(nudges[0].id).toBe('negative-cashflow');
    expect(nudges[0].severity).toBe('critical');
  });

  test('keeps over-budget ahead of low savings rate', () => {
    const nudges = buildDashboardNudges({
      monthlyIncome: 5000,
      monthlyExpenses: 4200,
      runwayMonths: 4,
      savingsRate: 16,
      hasEmergencyFundGoal: true,
      emergencyFundProgress: 60,
      overBudgetCount: 2,
      largestOverBudgetAmount: 180,
    });

    const overBudgetIndex = nudges.findIndex((nudge) => nudge.id === 'over-budget');
    const lowSavingsIndex = nudges.findIndex((nudge) => nudge.id === 'low-savings-rate');

    expect(overBudgetIndex).toBeGreaterThanOrEqual(0);
    expect(lowSavingsIndex).toBeGreaterThanOrEqual(0);
    expect(overBudgetIndex).toBeLessThan(lowSavingsIndex);
  });

  test('returns emergency-fund creation nudge when no emergency goal exists', () => {
    const nudge = pickNextBestAction(
      buildDashboardNudges({
        monthlyIncome: 4800,
        monthlyExpenses: 3900,
        runwayMonths: 2,
        savingsRate: 19,
        hasEmergencyFundGoal: false,
        emergencyFundProgress: null,
        overBudgetCount: 0,
        largestOverBudgetAmount: 0,
      }),
    );

    expect(nudge).not.toBeNull();
    expect(nudge?.id).toBe('missing-emergency-fund');
    expect(nudge?.concept).toBe('emergency-fund');
    expect(nudge?.ctaHref).toBe('/goals/new');
  });

  test('returns healthy momentum when no risk rule is triggered', () => {
    const nudges = buildDashboardNudges({
      monthlyIncome: 6500,
      monthlyExpenses: 4200,
      runwayMonths: 7,
      savingsRate: 35,
      hasEmergencyFundGoal: true,
      emergencyFundProgress: 100,
      overBudgetCount: 0,
      largestOverBudgetAmount: 0,
    });

    expect(nudges).toHaveLength(1);
    expect(nudges[0].id).toBe('healthy-momentum');
    expect(nudges[0].severity).toBe('positive');
  });
});
