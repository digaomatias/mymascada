'use client';

import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useDashboard } from '@/contexts/dashboard-context';
import { apiClient } from '@/lib/api-client';

export interface DashboardData {
  totalBalance: number;
  monthlyIncome: number;
  monthlyExpenses: number;
  transactionCount: number;
  accountCount: number;
  recentTransactions: {
    id: number;
    amount: number;
    transactionDate: string;
    description: string;
    categoryName?: string;
    accountName?: string;
  }[];
}

const INITIAL_DATA: DashboardData = {
  totalBalance: 0,
  monthlyIncome: 0,
  monthlyExpenses: 0,
  transactionCount: 0,
  accountCount: 0,
  recentTransactions: [],
};

export function useDashboardData() {
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const { period } = useDashboard();
  const [data, setData] = useState<DashboardData>(INITIAL_DATA);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const now = new Date();
      const currentMonth = now.getMonth() + 1;
      const currentYear = now.getFullYear();

      // Parallel API calls
      const [accounts, transactions, transactionsPage] = await Promise.all([
        apiClient.getAccountsWithBalances() as Promise<
          { calculatedBalance: number }[]
        >,
        apiClient.getRecentTransactions(50) as Promise<
          {
            id: number;
            amount: number;
            transactionDate: string;
            description: string;
            categoryName?: string;
            accountName?: string;
          }[]
        >,
        apiClient.getTransactions({ pageSize: 1 }) as Promise<{
          totalCount: number;
        }>,
      ]);

      const totalBalance =
        accounts?.reduce(
          (sum, a) => sum + (a.calculatedBalance || 0),
          0,
        ) || 0;

      let monthlyIncome = 0;
      let monthlyExpenses = 0;

      if (period === 'quarter') {
        // Load 3 months of data
        const startMonth = currentMonth <= 3 ? 1 : currentMonth <= 6 ? 4 : currentMonth <= 9 ? 7 : 10;
        const startYear = currentYear;

        const summaryPromises = [];
        for (let i = 0; i < 3; i++) {
          let m = startMonth + i;
          let y = startYear;
          if (m > 12) { m -= 12; y += 1; }
          // Only include months up to current
          if (y < currentYear || (y === currentYear && m <= currentMonth)) {
            summaryPromises.push(
              apiClient
                .getMonthlySummary(y, m)
                .catch(() => ({ totalIncome: 0, totalExpenses: 0 })),
            );
          }
        }

        const summaries = (await Promise.all(summaryPromises)) as {
          totalIncome: number;
          totalExpenses: number;
        }[];

        monthlyIncome = summaries.reduce(
          (sum, s) => sum + (s?.totalIncome || 0),
          0,
        );
        monthlyExpenses = summaries.reduce(
          (sum, s) => sum + (s?.totalExpenses || 0),
          0,
        );
      } else {
        try {
          const summary = (await apiClient.getMonthlySummary(
            currentYear,
            currentMonth,
          )) as { totalIncome: number; totalExpenses: number };
          monthlyIncome = summary?.totalIncome || 0;
          monthlyExpenses = summary?.totalExpenses || 0;
        } catch {
          // Fallback to previous month
          try {
            const prevMonth = currentMonth === 1 ? 12 : currentMonth - 1;
            const prevYear =
              currentMonth === 1 ? currentYear - 1 : currentYear;
            const fallback = (await apiClient.getMonthlySummary(
              prevYear,
              prevMonth,
            )) as { totalIncome: number; totalExpenses: number };
            monthlyIncome = fallback?.totalIncome || 0;
            monthlyExpenses = fallback?.totalExpenses || 0;
          } catch {
            // Calculate from transactions as last resort
            const monthlyTxs =
              transactions?.filter((t) => {
                const d = new Date(t.transactionDate);
                return (
                  d.getMonth() + 1 === currentMonth &&
                  d.getFullYear() === currentYear
                );
              }) || [];

            monthlyIncome = monthlyTxs
              .filter((t) => t.amount > 0)
              .reduce((sum, t) => sum + t.amount, 0);
            monthlyExpenses = Math.abs(
              monthlyTxs
                .filter((t) => t.amount < 0)
                .reduce((sum, t) => sum + t.amount, 0),
            );
          }
        }
      }

      setData({
        totalBalance,
        monthlyIncome,
        monthlyExpenses,
        transactionCount: transactionsPage?.totalCount || 0,
        accountCount: accounts?.length || 0,
        recentTransactions: transactions?.slice(0, 5) || [],
      });
    } catch (err) {
      console.error('Failed to load dashboard data:', err);
      setError('Failed to load dashboard data');
    } finally {
      setLoading(false);
    }
  }, [period]);

  useEffect(() => {
    if (isAuthenticated && !authLoading && apiClient.getToken()) {
      load();
    }
  }, [isAuthenticated, authLoading, load]);

  return { data, loading, error, reload: load };
}
