'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { BudgetProgressCard } from '@/components/budget/budget-progress-card';
import { apiClient } from '@/lib/api-client';
import { BudgetSummary } from '@/types/budget';
import { toast } from 'sonner';
import { PiggyBank, Plus, Wallet } from 'lucide-react';

export default function BudgetsPage() {
  const t = useTranslations('budgets');

  const [budgets, setBudgets] = useState<BudgetSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showInactive, setShowInactive] = useState(false);
  const [currentPeriodOnly, setCurrentPeriodOnly] = useState(false);

  const loadBudgets = async () => {
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

  useEffect(() => {
    loadBudgets();
  }, [showInactive, currentPeriodOnly]);

  return (
    <AppLayout>
        {/* Header with Filters */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2 text-gray-900">
              <Wallet className="h-6 w-6" />
              {t('title')}
            </h1>
            <p className="text-gray-600">{t('subtitle')}</p>
          </div>

          <div className="flex items-center gap-4 sm:gap-6">
            {/* Filters */}
            <div className="flex items-center gap-4 text-sm">
              <div className="flex items-center space-x-2">
                <Checkbox
                  id="showInactive"
                  checked={showInactive}
                  onCheckedChange={(checked) => setShowInactive(checked === true)}
                />
                <Label htmlFor="showInactive" className="text-sm text-gray-700 cursor-pointer">
                  {t('filters.showInactive')}
                </Label>
              </div>
              <div className="flex items-center space-x-2">
                <Checkbox
                  id="currentPeriodOnly"
                  checked={currentPeriodOnly}
                  onCheckedChange={(checked) => setCurrentPeriodOnly(checked === true)}
                />
                <Label htmlFor="currentPeriodOnly" className="text-sm text-gray-700 cursor-pointer">
                  {t('filters.currentPeriodOnly')}
                </Label>
              </div>
            </div>

            {/* Create button */}
            <Link href="/budgets/new">
              <Button>
                <Plus className="h-4 w-4 mr-2" />
                {t('createBudget')}
              </Button>
            </Link>
          </div>
        </div>

        {/* Budget List */}
        {isLoading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {[1, 2, 3].map((i) => (
              <Card key={i} className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                <CardContent className="p-6 space-y-4">
                  <Skeleton className="h-6 w-3/4" />
                  <Skeleton className="h-4 w-1/2" />
                  <Skeleton className="h-2.5 w-full" />
                  <Skeleton className="h-4 w-full" />
                </CardContent>
              </Card>
            ))}
          </div>
        ) : budgets.length === 0 ? (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="py-12">
              <div className="flex flex-col items-center justify-center text-center space-y-4">
                <div className="rounded-full bg-primary-100 p-4">
                  <PiggyBank className="h-8 w-8 text-primary-600" />
                </div>
                <div>
                  <h3 className="font-semibold text-lg text-gray-900">{t('noBudgets')}</h3>
                  <p className="text-gray-600 mt-1">
                    {t('noBudgetsDescription')}
                  </p>
                </div>
                <Link href="/budgets/new">
                  <Button>
                    <Plus className="h-4 w-4 mr-2" />
                    {t('createFirstBudget')}
                  </Button>
                </Link>
              </div>
            </CardContent>
          </Card>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {budgets.map((budget) => (
              <BudgetProgressCard
                key={budget.id}
                budget={budget}
                showActions={true}
              />
            ))}
          </div>
        )}
    </AppLayout>
  );
}
