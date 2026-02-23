'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { BudgetProgressBar } from '@/components/budget/budget-progress-bar';
import { CategoryBudgetRow } from '@/components/budget/category-budget-row';
import { apiClient } from '@/lib/api-client';
import { BudgetDetail } from '@/types/budget';
import { formatCurrency } from '@/types/budget';
import { toast } from 'sonner';
import {
  ArrowLeft,
  CalendarDays,
  Edit,
  Repeat,
  Trash2,
  Wallet,
} from 'lucide-react';

export default function BudgetDetailPage() {
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('budgets');
  const tCommon = useTranslations('common');

  const budgetId = Number(params.id);
  const [budget, setBudget] = useState<BudgetDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const loadBudget = async () => {
    try {
      setIsLoading(true);
      const data = await apiClient.getBudget(budgetId);
      setBudget(data);
    } catch {
      toast.error(t('loadError'));
      router.push('/budgets');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (budgetId) {
      loadBudget();
    }
  }, [budgetId]);

  const handleDelete = async () => {
    try {
      await apiClient.deleteBudget(budgetId);
      toast.success(t('budgetDeleted'));
      router.push('/budgets');
    } catch {
      toast.error(t('deleteError'));
    }
  };

  const handleRemoveCategory = async (categoryId: number) => {
    const category = budget?.categories.find(c => c.categoryId === categoryId);
    if (!category || !confirm(t('removeCategoryConfirm', { category: category.categoryName }))) {
      return;
    }

    try {
      await apiClient.removeBudgetCategory(budgetId, categoryId);
      toast.success(t('categoryRemoved'));
      loadBudget();
    } catch {
      toast.error(t('deleteError'));
    }
  };

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString(undefined, {
      weekday: 'short',
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const getStatusBadge = () => {
    if (!budget) return null;
    if (budget.usedPercentage >= 100) {
      return (
        <Badge variant="destructive">
          {t('overBudget')}
        </Badge>
      );
    }
    if (budget.usedPercentage >= 80) {
      return (
        <Badge variant="outline" className="border-yellow-500 text-yellow-600 bg-yellow-50">
          {t('approaching')}
        </Badge>
      );
    }
    return (
      <Badge variant="outline" className="border-green-500 text-green-600 bg-green-50">
        {t('onTrack')}
      </Badge>
    );
  };

  if (isLoading) {
    return (
      <AppLayout>
          <Skeleton className="h-8 w-64" />
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Skeleton className="h-32" />
            <Skeleton className="h-32" />
            <Skeleton className="h-32" />
          </div>
          <Skeleton className="h-64" />
      </AppLayout>
    );
  }

  if (!budget) {
    return null;
  }

  return (
    <AppLayout>
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="space-y-1">
          <Link href="/budgets">
            <Button
              variant="ghost"
              size="sm"
              className="mb-2 -ml-2"
            >
              <ArrowLeft className="h-4 w-4 mr-1" />
              {tCommon('back')}
            </Button>
          </Link>
          <div className="flex items-center gap-3">
            <Wallet className="h-6 w-6" />
            <h1 className="text-2xl font-bold">{budget.name}</h1>
            {getStatusBadge()}
            {!budget.isActive && (
              <Badge variant="outline" className="text-muted-foreground">
                {t('inactive')}
              </Badge>
            )}
            {budget.isRecurring && (
              <Badge variant="secondary" className="gap-1">
                <Repeat className="h-3 w-3" />
                {t('recurring')}
              </Badge>
            )}
          </div>
          {budget.description && (
            <p className="text-muted-foreground">{budget.description}</p>
          )}
          <div className="flex items-center gap-1 text-sm text-muted-foreground mt-2">
            <CalendarDays className="h-4 w-4" />
            <span>
              {formatDate(budget.startDate)} — {formatDate(budget.endDate)}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Link href={`/budgets/${budget.id}/edit`}>
            <Button variant="outline">
              <Edit className="h-4 w-4 mr-2" />
              {tCommon('edit')}
            </Button>
          </Link>
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="outline" className="text-destructive hover:text-destructive">
                <Trash2 className="h-4 w-4 mr-2" />
                {tCommon('delete')}
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>{t('deleteBudget')}</AlertDialogTitle>
                <AlertDialogDescription>
                  {t('deleteConfirm', { name: budget.name })}
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{tCommon('cancel')}</AlertDialogCancel>
                <AlertDialogAction
                  onClick={handleDelete}
                  className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                >
                  {tCommon('delete')}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="pt-6">
            <p className="text-sm text-muted-foreground">{t('totalBudgeted')}</p>
            <p className="text-2xl font-bold">{formatCurrency(budget.totalBudgeted)}</p>
          </CardContent>
        </Card>
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="pt-6">
            <p className="text-sm text-muted-foreground">{t('totalSpent')}</p>
            <p className="text-2xl font-bold">{formatCurrency(budget.totalSpent)}</p>
          </CardContent>
        </Card>
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="pt-6">
            <p className="text-sm text-muted-foreground">{t('totalRemaining')}</p>
            <p className={`text-2xl font-bold ${budget.totalRemaining >= 0 ? 'text-green-600' : 'text-red-600'}`}>
              {formatCurrency(budget.totalRemaining)}
            </p>
          </CardContent>
        </Card>
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="pt-6">
            <p className="text-sm text-muted-foreground">{t('progress')}</p>
            <p className="text-2xl font-bold">{budget.usedPercentage.toFixed(1)}%</p>
            <p className="text-xs text-muted-foreground mt-1">
              {budget.daysRemaining > 0
                ? budget.daysRemaining === 1
                  ? t('daysRemainingOne')
                  : t('daysRemaining', { count: budget.daysRemaining })
                : t('periodComplete')}
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Overall Progress */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardContent className="pt-6">
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-muted-foreground">{t('overview')}</span>
              <span className="font-medium">{t('percentUsed', { percent: budget.usedPercentage.toFixed(1) })}</span>
            </div>
            <BudgetProgressBar usedPercentage={budget.usedPercentage} size="lg" />
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>{t('spentOfBudget', { spent: formatCurrency(budget.totalSpent), budget: formatCurrency(budget.totalBudgeted) })}</span>
              <span>
                {budget.periodElapsedPercentage.toFixed(0)}% of period elapsed
              </span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Category Breakdown */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>{t('categories')}</CardTitle>
            <Link href={`/budgets/${budget.id}/edit`}>
              <Button variant="outline" size="sm">
                {t('addCategory')}
              </Button>
            </Link>
          </div>
        </CardHeader>
        <CardContent>
          {budget.categories.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">
              {t('noCategoriesInBudget')}
            </div>
          ) : (
            <div className="space-y-3">
              {budget.categories.map((category) => (
                <CategoryBudgetRow
                  key={category.categoryId}
                  category={category}
                  showActions={true}
                  onRemove={handleRemoveCategory}
                />
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </AppLayout>
  );
}
