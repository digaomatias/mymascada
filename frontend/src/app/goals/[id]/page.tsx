'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
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
} from '@/components/ui/alert-dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { apiClient, GoalDetail, OnboardingStatusResponse } from '@/lib/api-client';
import { cn, formatCurrency, formatDate } from '@/lib/utils';
import { toast } from 'sonner';
import {
  ArrowLeftIcon,
  PencilIcon,
  TrashIcon,
  ArrowTrendingUpIcon,
  MapPinIcon as MapPinOutlineIcon,
  CalendarDaysIcon,
  LinkIcon,
} from '@heroicons/react/24/outline';
import { MapPinIcon as MapPinSolidIcon } from '@heroicons/react/24/solid';
import {
  getGoalTypeConfig,
  getGoalTrackingState,
  TRACKING_STATE_STYLES,
  JOURNEY_STAGES,
  type GoalContext,
} from '@/lib/goals/goal-type-config';

export default function GoalDetailPage() {
  const params = useParams();
  const router = useRouter();
  const t = useTranslations('goals');
  const tCommon = useTranslations('common');

  const goalId = Number(params.id);
  const [goal, setGoal] = useState<GoalDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [showProgressDialog, setShowProgressDialog] = useState(false);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [newAmount, setNewAmount] = useState('');
  const [isUpdatingProgress, setIsUpdatingProgress] = useState(false);
  const [goalCtx, setGoalCtx] = useState<GoalContext>({
    monthlyExpenses: 0,
    monthlyIncome: 0,
  });

  const loadGoal = async () => {
    try {
      setIsLoading(true);
      const [data, onboarding] = await Promise.all([
        apiClient.getGoal(goalId),
        apiClient.getOnboardingStatus().catch((): OnboardingStatusResponse => ({
          isComplete: false,
        })),
      ]);
      setGoal(data);
      setGoalCtx({
        monthlyExpenses: onboarding.monthlyExpenses ?? 0,
        monthlyIncome: onboarding.monthlyIncome ?? 0,
      });
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
  // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const handleTogglePin = async () => {
    if (!goal) return;
    const newPinned = !goal.isPinned;

    // Optimistic update
    setGoal((prev) => prev ? { ...prev, isPinned: newPinned } : prev);

    try {
      await apiClient.toggleGoalPin(goal.id, newPinned);
      toast.success(newPinned ? t('goalPinned') : t('goalUnpinned'));
    } catch {
      // Revert on error
      setGoal((prev) => prev ? { ...prev, isPinned: !newPinned } : prev);
      toast.error(t('pinError'));
    }
  };

  if (isLoading) {
    return (
      <AppLayout>
        <div className="space-y-6">
          <Skeleton className="h-8 w-48 rounded-[26px] border border-violet-100/80 bg-white/80" />
          <Skeleton className="h-12 w-80 rounded-[26px] border border-violet-100/80 bg-white/80" />
          <Skeleton className="h-40 rounded-[26px] border border-violet-100/80 bg-white/80" />
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <Skeleton className="h-24 rounded-2xl border border-violet-100/80 bg-white/80" />
            <Skeleton className="h-24 rounded-2xl border border-violet-100/80 bg-white/80" />
            <Skeleton className="h-24 rounded-2xl border border-violet-100/80 bg-white/80" />
            <Skeleton className="h-24 rounded-2xl border border-violet-100/80 bg-white/80" />
          </div>
          <Skeleton className="h-48 rounded-[24px] border border-violet-100/80 bg-white/80" />
        </div>
      </AppLayout>
    );
  }

  if (!goal) {
    return null;
  }

  const config = getGoalTypeConfig(goal.goalType);
  const tracking = getGoalTrackingState(goal);
  const stateStyle = TRACKING_STATE_STYLES[tracking];
  const hero = config.heroMetric(goal, goalCtx);
  const Icon = config.icon;
  const stage = JOURNEY_STAGES.find((s) => s.key === config.journeyStage);

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-3">
            <Link
              href="/goals"
              className="inline-flex items-center gap-1 text-sm text-slate-500 transition-colors hover:text-violet-700"
            >
              <ArrowLeftIcon className="h-4 w-4" />
              {t('backToGoals')}
            </Link>

            {/* Title row */}
            <div className="flex items-center gap-3">
              <div
                className={cn(
                  'flex h-11 w-11 shrink-0 items-center justify-center rounded-xl',
                  config.accentColor.bg,
                  config.accentColor.border,
                  'border',
                )}
              >
                <Icon className={cn('h-6 w-6', config.accentColor.text)} />
              </div>
              <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900">
                {goal.name}
              </h1>
            </div>

            {/* Badges row */}
            <div className="flex flex-wrap items-center gap-2">
              <span
                className={cn(
                  'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium',
                  stateStyle.bg,
                  stateStyle.text,
                  stateStyle.border,
                )}
              >
                {t(`trackingState.${tracking}`)}
              </span>
              <span className="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 text-xs font-medium text-slate-600">
                {t(`goalTypes.${goal.goalType}`)}
              </span>
              {stage && (
                <span className="inline-flex items-center rounded-full border border-violet-200 bg-violet-50 px-2.5 py-0.5 text-xs font-medium text-violet-700">
                  {t(`stages.${stage.key}`)}
                </span>
              )}
              <button
                onClick={handleTogglePin}
                className="rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-violet-50 hover:text-violet-600"
                title={goal.isPinned ? t('unpinGoal') : t('pinGoal')}
              >
                {goal.isPinned ? (
                  <MapPinSolidIcon className="h-4.5 w-4.5 text-violet-600" />
                ) : (
                  <MapPinOutlineIcon className="h-4.5 w-4.5" />
                )}
              </button>
            </div>

            {goal.description && (
              <p className="text-[15px] text-slate-500">{goal.description}</p>
            )}
          </div>

          {/* Actions */}
          <div className="flex flex-wrap items-center gap-2">
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
            <Button
              variant="outline"
              className="text-destructive hover:text-destructive"
              onClick={() => setShowDeleteDialog(true)}
            >
              <TrashIcon className="h-4 w-4 mr-2" />
              {tCommon('delete')}
            </Button>
          </div>
        </div>

        {/* Progress Hero Section */}
        <div className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <div className="space-y-4">
            <div className="flex items-end justify-between">
              <div>
                {hero && (
                  <div className="mb-1">
                    <span className={cn('text-sm font-medium', config.accentColor.text)}>
                      {hero.label}
                    </span>
                    {hero.subtext && (
                      <span className="ml-2 text-xs text-slate-400">{hero.subtext}</span>
                    )}
                  </div>
                )}
                <span className="text-sm text-slate-500">{t('detail.progress')}</span>
              </div>
              <span className="font-[var(--font-dash-sans)] text-4xl font-bold tracking-tight text-slate-900">
                {goal.progressPercentage.toFixed(1)}%
              </span>
            </div>
            <div className="h-3 overflow-hidden rounded-full bg-slate-100">
              <div
                className={cn('h-full rounded-full transition-all duration-500', config.accentColor.bar)}
                style={{ width: `${Math.min(goal.progressPercentage, 100)}%` }}
              />
            </div>
            <div className="flex justify-between text-sm text-slate-500">
              <span>
                {formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}
              </span>
              <span>
                {formatCurrency(goal.remainingAmount)} {t('detail.remaining').toLowerCase()}
              </span>
            </div>
          </div>
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <article className="rounded-2xl border border-slate-100 bg-white/90 p-4 shadow-sm backdrop-blur-xs">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
              {t('detail.currentAmount')}
            </p>
            <p className="mt-1 text-xl font-bold text-slate-900">
              {formatCurrency(goal.currentAmount)}
            </p>
          </article>
          <article className="rounded-2xl border border-slate-100 bg-white/90 p-4 shadow-sm backdrop-blur-xs">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
              {t('detail.targetAmount')}
            </p>
            <p className="mt-1 text-xl font-bold text-slate-900">
              {formatCurrency(goal.targetAmount)}
            </p>
          </article>
          <article className="rounded-2xl border border-slate-100 bg-white/90 p-4 shadow-sm backdrop-blur-xs">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
              {t('detail.remaining')}
            </p>
            <p className={cn('mt-1 text-xl font-bold', goal.remainingAmount <= 0 ? 'text-emerald-600' : 'text-slate-900')}>
              {formatCurrency(goal.remainingAmount)}
            </p>
          </article>
          <article className="rounded-2xl border border-slate-100 bg-white/90 p-4 shadow-sm backdrop-blur-xs">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
              {t('detail.deadline')}
            </p>
            {goal.deadline ? (
              <>
                <p className="mt-1 text-xl font-bold text-slate-900">
                  {formatDate(goal.deadline)}
                </p>
                <p className="text-xs text-slate-400 mt-0.5">
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
              <p className="mt-1 text-base text-slate-400">{t('detail.noDeadline')}</p>
            )}
          </article>
        </div>

        {/* Type-Specific Panel Slot */}
        {config.DetailPanel && (
          <config.DetailPanel goal={goal} ctx={goalCtx} />
        )}

        {/* Details Section */}
        <div className="rounded-[24px] border border-slate-100 bg-white/90 p-6 shadow-sm backdrop-blur-xs">
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                {t('detail.goalType')}
              </p>
              <div className="mt-1 flex items-center gap-2">
                <Icon className={cn('h-4 w-4', config.accentColor.text)} />
                <span className="text-sm font-medium text-slate-700">
                  {t(`goalTypes.${goal.goalType}`)}
                </span>
              </div>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                {t('detail.status')}
              </p>
              <p className="mt-1 text-sm font-medium text-slate-700">
                {t(`statuses.${goal.status}`)}
              </p>
            </div>
            {goal.linkedAccountName && (
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                  {t('detail.linkedAccount')}
                </p>
                <div className="mt-1 flex items-center gap-2">
                  <LinkIcon className="h-4 w-4 text-slate-400" />
                  <span className="text-sm font-medium text-slate-700">
                    {goal.linkedAccountName}
                  </span>
                </div>
              </div>
            )}
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                {t('detail.createdAt')}
              </p>
              <div className="mt-1 flex items-center gap-2">
                <CalendarDaysIcon className="h-4 w-4 text-slate-400" />
                <span className="text-sm font-medium text-slate-700">
                  {formatDate(goal.createdAt)}
                </span>
              </div>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                {t('detail.updatedAt')}
              </p>
              <div className="mt-1 flex items-center gap-2">
                <CalendarDaysIcon className="h-4 w-4 text-slate-400" />
                <span className="text-sm font-medium text-slate-700">
                  {formatDate(goal.updatedAt)}
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Delete Confirmation Dialog */}
        <ConfirmationDialog
          isOpen={showDeleteDialog}
          onClose={() => setShowDeleteDialog(false)}
          onConfirm={handleDelete}
          title={t('delete.title')}
          description={t('delete.description')}
          confirmText={t('delete.confirm')}
          cancelText={tCommon('cancel')}
          variant="danger"
        />

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
      </div>
    </AppLayout>
  );
}
