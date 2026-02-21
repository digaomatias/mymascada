'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import Navigation from '@/components/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { apiClient, GoalDetail } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { toast } from 'sonner';
import {
  ArrowLeftIcon,
  FlagIcon,
  PencilIcon,
  TrashIcon,
  ArrowTrendingUpIcon,
} from '@heroicons/react/24/outline';

function getProgressColor(percentage: number): string {
  if (percentage >= 100) return 'bg-emerald-500';
  if (percentage >= 90) return 'bg-blue-500';
  if (percentage >= 60) return 'bg-yellow-500';
  return 'bg-green-500';
}

function getProgressBgColor(percentage: number): string {
  if (percentage >= 100) return 'bg-emerald-100';
  if (percentage >= 90) return 'bg-blue-100';
  if (percentage >= 60) return 'bg-yellow-100';
  return 'bg-green-100';
}

export default function GoalDetailPage() {
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('goals');
  const tCommon = useTranslations('common');

  const goalId = Number(params.id);
  const [goal, setGoal] = useState<GoalDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [showProgressDialog, setShowProgressDialog] = useState(false);
  const [newAmount, setNewAmount] = useState('');
  const [isUpdatingProgress, setIsUpdatingProgress] = useState(false);

  const loadGoal = async () => {
    try {
      setIsLoading(true);
      const data = await apiClient.getGoal(goalId);
      setGoal(data);
    } catch {
      toast.error(t('loadError'));
      router.push('/goals');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (goalId) {
      loadGoal();
    }
  }, [goalId]);

  const handleDelete = async () => {
    try {
      await apiClient.deleteGoal(goalId);
      toast.success(t('goalDeleted'));
      router.push('/goals');
    } catch {
      toast.error(t('deleteError'));
    }
  };

  const handleUpdateProgress = async () => {
    const amount = parseFloat(newAmount);
    if (isNaN(amount) || amount < 0) return;

    try {
      setIsUpdatingProgress(true);
      const updated = await apiClient.updateGoalProgress(goalId, amount);
      setGoal(updated);
      setShowProgressDialog(false);
      setNewAmount('');
      toast.success(t('progressUpdated'));
    } catch {
      toast.error(t('updateError'));
    } finally {
      setIsUpdatingProgress(false);
    }
  };

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
        <Navigation />
        <main className="container mx-auto px-4 py-6 space-y-6">
          <Skeleton className="h-8 w-64" />
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Skeleton className="h-32" />
            <Skeleton className="h-32" />
            <Skeleton className="h-32" />
          </div>
          <Skeleton className="h-64" />
        </main>
      </div>
    );
  }

  if (!goal) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      <main className="container mx-auto px-4 py-6 space-y-6">
        {/* Header */}
        <div className="flex items-start justify-between">
          <div className="space-y-1">
            <Link href="/goals">
              <Button variant="ghost" size="sm" className="mb-2 -ml-2">
                <ArrowLeftIcon className="h-4 w-4 mr-1" />
                {t('backToGoals')}
              </Button>
            </Link>
            <div className="flex items-center gap-3">
              <FlagIcon className="h-6 w-6" />
              <h1 className="text-2xl font-bold">{goal.name}</h1>
              <Badge
                variant={goal.status === 'Completed' ? 'default' : goal.status === 'Active' ? 'outline' : 'secondary'}
                className={
                  goal.status === 'Completed'
                    ? 'bg-emerald-100 text-emerald-700 border-emerald-200'
                    : goal.status === 'Active'
                    ? 'border-primary-300 text-primary-700'
                    : ''
                }
              >
                {t(`statuses.${goal.status}`)}
              </Badge>
              <Badge variant="secondary">
                {t(`goalTypes.${goal.goalType}`)}
              </Badge>
            </div>
            {goal.description && (
              <p className="text-muted-foreground">{goal.description}</p>
            )}
          </div>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              onClick={() => {
                setNewAmount(goal.currentAmount.toString());
                setShowProgressDialog(true);
              }}
            >
              <ArrowTrendingUpIcon className="h-4 w-4 mr-2" />
              {t('detail.updateProgress')}
            </Button>
            <Link href={`/goals/${goal.id}/edit`}>
              <Button variant="outline">
                <PencilIcon className="h-4 w-4 mr-2" />
                {tCommon('edit')}
              </Button>
            </Link>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="outline" className="text-destructive hover:text-destructive">
                  <TrashIcon className="h-4 w-4 mr-2" />
                  {tCommon('delete')}
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>{t('delete.title')}</AlertDialogTitle>
                  <AlertDialogDescription>
                    {t('delete.description')}
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>{tCommon('cancel')}</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={handleDelete}
                    className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                  >
                    {t('delete.confirm')}
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </div>
        </div>

        {/* Progress Hero */}
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="pt-6 pb-6">
            <div className="space-y-4">
              <div className="flex justify-between items-center">
                <span className="text-sm text-muted-foreground">{t('detail.progress')}</span>
                <span className="text-3xl font-bold">{goal.progressPercentage.toFixed(1)}%</span>
              </div>
              <div className={`h-4 rounded-full ${getProgressBgColor(goal.progressPercentage)}`}>
                <div
                  className={`h-full rounded-full transition-all duration-500 ${getProgressColor(goal.progressPercentage)}`}
                  style={{ width: `${Math.min(goal.progressPercentage, 100)}%` }}
                />
              </div>
              <div className="flex justify-between text-sm text-muted-foreground">
                <span>{formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}</span>
                <span>{formatCurrency(goal.remainingAmount)} {t('detail.remaining').toLowerCase()}</span>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="pt-6">
              <p className="text-sm text-muted-foreground">{t('detail.currentAmount')}</p>
              <p className="text-2xl font-bold">{formatCurrency(goal.currentAmount)}</p>
            </CardContent>
          </Card>
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="pt-6">
              <p className="text-sm text-muted-foreground">{t('detail.targetAmount')}</p>
              <p className="text-2xl font-bold">{formatCurrency(goal.targetAmount)}</p>
            </CardContent>
          </Card>
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="pt-6">
              <p className="text-sm text-muted-foreground">{t('detail.remaining')}</p>
              <p className={`text-2xl font-bold ${goal.remainingAmount <= 0 ? 'text-emerald-600' : ''}`}>
                {formatCurrency(goal.remainingAmount)}
              </p>
            </CardContent>
          </Card>
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="pt-6">
              <p className="text-sm text-muted-foreground">{t('detail.deadline')}</p>
              {goal.deadline ? (
                <>
                  <p className="text-2xl font-bold">{formatDate(goal.deadline)}</p>
                  <p className="text-xs text-muted-foreground mt-1">
                    {goal.daysRemaining !== undefined && goal.daysRemaining > 0
                      ? goal.daysRemaining === 1
                        ? t('detail.daysRemainingOne')
                        : t('detail.daysRemaining', { count: goal.daysRemaining })
                      : goal.daysRemaining !== undefined && goal.daysRemaining <= 0
                      ? t('detail.overdue')
                      : ''}
                  </p>
                </>
              ) : (
                <p className="text-lg text-muted-foreground">{t('detail.noDeadline')}</p>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Details Card */}
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="pt-6">
            <div className="grid grid-cols-2 gap-6 text-sm">
              <div>
                <p className="text-muted-foreground">{t('detail.goalType')}</p>
                <p className="font-medium">{t(`goalTypes.${goal.goalType}`)}</p>
              </div>
              <div>
                <p className="text-muted-foreground">{t('detail.status')}</p>
                <p className="font-medium">{t(`statuses.${goal.status}`)}</p>
              </div>
              {goal.linkedAccountName && (
                <div>
                  <p className="text-muted-foreground">{t('detail.linkedAccount')}</p>
                  <p className="font-medium">{goal.linkedAccountName}</p>
                </div>
              )}
              <div>
                <p className="text-muted-foreground">{t('detail.createdAt')}</p>
                <p className="font-medium">{formatDate(goal.createdAt)}</p>
              </div>
              <div>
                <p className="text-muted-foreground">{t('detail.updatedAt')}</p>
                <p className="font-medium">{formatDate(goal.updatedAt)}</p>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Update Progress Dialog */}
        <AlertDialog open={showProgressDialog} onOpenChange={setShowProgressDialog}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('detail.updateProgressTitle')}</AlertDialogTitle>
              <AlertDialogDescription>
                {t('detail.updateProgressDescription')}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <div className="py-4">
              <Label htmlFor="newAmount">{t('detail.newAmount')}</Label>
              <Input
                id="newAmount"
                type="number"
                min="0"
                step="0.01"
                value={newAmount}
                onChange={(e) => setNewAmount(e.target.value)}
                placeholder="0.00"
                className="mt-2"
              />
            </div>
            <AlertDialogFooter>
              <AlertDialogCancel>{tCommon('cancel')}</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleUpdateProgress}
                disabled={isUpdatingProgress || isNaN(parseFloat(newAmount)) || parseFloat(newAmount) < 0}
              >
                {isUpdatingProgress ? tCommon('saving') : tCommon('update')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </main>
    </div>
  );
}
