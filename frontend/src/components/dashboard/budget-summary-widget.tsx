'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { BudgetProgressBar } from '@/components/budget/budget-progress-bar';
import { apiClient } from '@/lib/api-client';
import { BudgetSummary, formatCurrency } from '@/types/budget';
import {
  WalletIcon,
  ChevronRightIcon,
  PlusIcon,
  ExclamationTriangleIcon,
} from '@heroicons/react/24/outline';

export function BudgetSummaryWidget() {
  const t = useTranslations('budgets');
  const tDashboard = useTranslations('dashboard');
  const [budgets, setBudgets] = useState<BudgetSummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadBudgets = async () => {
      try {
        setLoading(true);
        // Get only current period, active budgets
        const data = await apiClient.getBudgets({
          includeInactive: false,
          onlyCurrentPeriod: true,
        });
        setBudgets(data);
      } catch (error) {
        console.error('Failed to load budgets:', error);
        setBudgets([]);
      } finally {
        setLoading(false);
      }
    };

    loadBudgets();
  }, []);

  // Calculate overall budget health
  const totalBudgeted = budgets.reduce((sum, b) => sum + b.totalBudgeted, 0);
  const totalSpent = budgets.reduce((sum, b) => sum + b.totalSpent, 0);
  const overallPercentage = totalBudgeted > 0 ? (totalSpent / totalBudgeted) * 100 : 0;
  const budgetsOverLimit = budgets.filter(b => b.usedPercentage >= 100).length;
  const budgetsApproaching = budgets.filter(b => b.usedPercentage >= 80 && b.usedPercentage < 100).length;

  if (loading) {
    return (
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <div className="flex items-center gap-2">
            <Skeleton className="h-6 w-6" />
            <Skeleton className="h-6 w-32" />
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-20 w-full" />
        </CardContent>
      </Card>
    );
  }

  // No budgets case
  if (budgets.length === 0) {
    return (
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <WalletIcon className="h-6 w-6 text-primary-600" />
              <CardTitle className="text-xl font-bold text-gray-900">{t('title')}</CardTitle>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="text-center py-6">
            <div className="w-12 h-12 bg-primary-100 rounded-full flex items-center justify-center mx-auto mb-3">
              <WalletIcon className="h-6 w-6 text-primary-600" />
            </div>
            <p className="text-gray-600 mb-4">{t('noBudgetsDescription')}</p>
            <Link href="/budgets/new">
              <Button size="sm">
                <PlusIcon className="h-4 w-4 mr-1" />
                {t('createFirstBudget')}
              </Button>
            </Link>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <WalletIcon className="h-6 w-6 text-primary-600" />
            <CardTitle className="text-xl font-bold text-gray-900">{t('title')}</CardTitle>
          </div>
          <Link href="/budgets">
            <Button variant="secondary" size="sm">
              {tDashboard('viewAll')}
              <ChevronRightIcon className="h-4 w-4 ml-1" />
            </Button>
          </Link>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Overall Progress */}
        <div className="space-y-2">
          <div className="flex justify-between items-center text-sm">
            <span className="text-gray-600">{t('overview')}</span>
            <span className="font-medium">
              {t('spentOfBudget', {
                spent: formatCurrency(totalSpent),
                budget: formatCurrency(totalBudgeted),
              })}
            </span>
          </div>
          <BudgetProgressBar usedPercentage={overallPercentage} size="md" />
          <div className="flex justify-between text-xs text-gray-500">
            <span>{t('percentUsed', { percent: overallPercentage.toFixed(0) })}</span>
            <span>
              {budgets.length === 1
                ? t('categoryCountOne')
                : `${budgets.length} budgets`}
            </span>
          </div>
        </div>

        {/* Alerts */}
        {(budgetsOverLimit > 0 || budgetsApproaching > 0) && (
          <div className="flex flex-wrap gap-2">
            {budgetsOverLimit > 0 && (
              <Badge variant="destructive" className="flex items-center gap-1">
                <ExclamationTriangleIcon className="h-3 w-3" />
                {budgetsOverLimit} {t('overBudget').toLowerCase()}
              </Badge>
            )}
            {budgetsApproaching > 0 && (
              <Badge variant="outline" className="border-yellow-500 text-yellow-600 bg-yellow-50 flex items-center gap-1">
                <ExclamationTriangleIcon className="h-3 w-3" />
                {budgetsApproaching} {t('approaching').toLowerCase()}
              </Badge>
            )}
          </div>
        )}

        {/* Top Budgets (show up to 3) */}
        <div className="space-y-2">
          {budgets.slice(0, 3).map((budget) => (
            <Link
              key={budget.id}
              href={`/budgets/${budget.id}`}
              className="block p-3 rounded-lg border hover:bg-gray-50 transition-colors"
            >
              <div className="flex items-center justify-between mb-2">
                <span className="font-medium text-sm truncate">{budget.name}</span>
                <Badge
                  variant={budget.usedPercentage >= 100 ? 'destructive' : budget.usedPercentage >= 80 ? 'outline' : 'secondary'}
                  className={budget.usedPercentage >= 80 && budget.usedPercentage < 100 ? 'border-yellow-500 text-yellow-600 bg-yellow-50' : ''}
                >
                  {budget.usedPercentage.toFixed(0)}%
                </Badge>
              </div>
              <BudgetProgressBar usedPercentage={budget.usedPercentage} size="sm" />
              <div className="flex justify-between text-xs text-gray-500 mt-1">
                <span>{formatCurrency(budget.totalSpent)} / {formatCurrency(budget.totalBudgeted)}</span>
                <span>
                  {budget.daysRemaining > 0
                    ? budget.daysRemaining === 1
                      ? t('daysRemainingOne')
                      : t('daysRemaining', { count: budget.daysRemaining })
                    : t('periodComplete')}
                </span>
              </div>
            </Link>
          ))}
        </div>

        {/* Show more link if there are more than 3 budgets */}
        {budgets.length > 3 && (
          <div className="text-center pt-2">
            <Link href="/budgets" className="text-sm text-primary-600 hover:text-primary-700">
              +{budgets.length - 3} more budgets
            </Link>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
