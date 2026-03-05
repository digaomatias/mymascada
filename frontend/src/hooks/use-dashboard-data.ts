'use client';

import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { apiClient } from '@/lib/api-client';
import type { DashboardSummaryResponse } from '@/types/api-responses';

export interface DashboardData {
  totalBalance: number;
  monthlyIncome: number;
  monthlyExpenses: number;
  transactionCount: number;
  recentTransactions: {
    id: number;
    amount: number;
    transactionDate: string;
    description: string;
    categoryName?: string;
    accountName?: string;
  }[];
  // Enriched fields from the dashboard-summary API
  runwayMonths: number;
  savingsRate: number;
  netSaved: number;
  avgMonthlyIncome: number;
  avgMonthlyExpenses: number;
  totalAssets: number;
  totalLiabilities: number;
  netWorth: number;
  isUsingFallbackMonth: boolean;
  displayMonth: number;
  displayYear: number;
}

const INITIAL_DATA: DashboardData = {
  totalBalance: 0,
  monthlyIncome: 0,
  monthlyExpenses: 0,
  transactionCount: 0,
  recentTransactions: [],
  runwayMonths: 0,
  savingsRate: 0,
  netSaved: 0,
  avgMonthlyIncome: 0,
  avgMonthlyExpenses: 0,
  totalAssets: 0,
  totalLiabilities: 0,
  netWorth: 0,
  isUsingFallbackMonth: false,
  displayMonth: new Date().getMonth() + 1,
  displayYear: new Date().getFullYear(),
};

export function useDashboardData() {
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const [data, setData] = useState<DashboardData>(INITIAL_DATA);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const summary: DashboardSummaryResponse = await apiClient.getDashboardSummary();

      setData({
        totalBalance: summary.totalBalance,
        monthlyIncome: summary.monthlyIncome,
        monthlyExpenses: summary.monthlyExpenses,
        transactionCount: summary.transactionCount,
        recentTransactions: summary.recentTransactions.map(t => ({
          id: t.id,
          amount: t.amount,
          transactionDate: t.transactionDate,
          description: t.description,
          categoryName: t.categoryName ?? undefined,
          accountName: t.accountName,
        })),
        runwayMonths: summary.runwayMonths,
        savingsRate: summary.savingsRate,
        netSaved: summary.netSaved,
        avgMonthlyIncome: summary.avgMonthlyIncome,
        avgMonthlyExpenses: summary.avgMonthlyExpenses,
        totalAssets: summary.totalAssets,
        totalLiabilities: summary.totalLiabilities,
        netWorth: summary.netWorth,
        isUsingFallbackMonth: summary.isUsingFallbackMonth,
        displayMonth: summary.displayMonth,
        displayYear: summary.displayYear,
      });
    } catch (err) {
      console.error('Failed to load dashboard data:', err);
      setError('Failed to load dashboard data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated && !authLoading && apiClient.getToken()) {
      load();
    }
  }, [isAuthenticated, authLoading, load]);

  return { data, loading, error, reload: load };
}
