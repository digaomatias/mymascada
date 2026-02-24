'use client';

import { useEffect, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { apiClient, GoalSummary, OnboardingStatusResponse } from '@/lib/api-client';
import { formatCurrency, cn } from '@/lib/utils';
import { toast } from 'sonner';
import {
  ArrowRightIcon,
  CalendarDaysIcon,
  LinkIcon,
  LightBulbIcon,
  MapPinIcon as MapPinOutlineIcon,
  PlusIcon,
  SparklesIcon,
} from '@heroicons/react/24/outline';
import { MapPinIcon as MapPinSolidIcon } from '@heroicons/react/24/solid';
import {
  type GoalContext,
  type JourneyStage,
  JOURNEY_STAGES,
  TRACKING_STATE_STYLES,
  getGoalTypeConfig,
  getGoalTrackingState,
  sortGoalsByJourney,
  pickGoalNudge,
} from '@/lib/goals/goal-type-config';

export default function GoalsPage() {
  const t = useTranslations('goals');
  const router = useRouter();
  const [goals, setGoals] = useState<GoalSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showCompleted, setShowCompleted] = useState(false);
  const [goalCtx, setGoalCtx] = useState<GoalContext>({
    monthlyExpenses: 0,
    monthlyIncome: 0,
  });

  useEffect(() => {
    const load = async () => {
      try {
        setIsLoading(true);
        const [goalsData, onboarding] = await Promise.all([
          apiClient.getGoals({ includeCompleted: showCompleted }),
          apiClient.getOnboardingStatus().catch((): OnboardingStatusResponse => ({
            isComplete: false,
          })),
        ]);
        setGoals(goalsData);
        setGoalCtx({
          monthlyExpenses: onboarding.monthlyExpenses ?? 0,
          monthlyIncome: onboarding.monthlyIncome ?? 0,
        });
      } catch {
        toast.error(t('loadError'));
      } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [showCompleted, t]);

  const sorted = useMemo(() => sortGoalsByJourney(goals), [goals]);
  const nudge = useMemo(() => pickGoalNudge(goals, goalCtx), [goals, goalCtx]);

  const focusGoalId = useMemo(() => {
    const first = sorted.find((g) => {
      const st = getGoalTrackingState(g);
      return st !== 'completed' && st !== 'paused';
    });
    return first?.id ?? null;
  }, [sorted]);

  // Current focus stage (stage of the first active goal)
  const currentStage = useMemo<JourneyStage | null>(() => {
    const first = sorted.find((g) => {
      const st = getGoalTrackingState(g);
      return st !== 'completed' && st !== 'paused';
    });
    if (!first) return null;
    return getGoalTypeConfig(first.goalType).journeyStage;
  }, [sorted]);

  // Which stages have at least one goal (include completed so dots stay filled)
  const activeStages = useMemo(() => {
    const stages = new Set<JourneyStage>();
    for (const goal of sorted) {
      const st = getGoalTrackingState(goal);
      if (st !== 'paused') {
        stages.add(getGoalTypeConfig(goal.goalType).journeyStage);
      }
    }
    return stages;
  }, [sorted]);

  // Pinned goals: pinned + non-completed/paused
  const pinnedGoals = useMemo(
    () =>
      sorted.filter((g) => {
        if (!g.isPinned) return false;
        const st = getGoalTrackingState(g);
        return st !== 'completed' && st !== 'paused';
      }),
    [sorted],
  );

  // Group by journey stage (exclude completed/paused AND pinned to avoid duplication)
  const stageGroups = useMemo(() => {
    const groups = new Map<JourneyStage, GoalSummary[]>();
    for (const stage of JOURNEY_STAGES) {
      groups.set(stage.key, []);
    }
    for (const goal of sorted) {
      const config = getGoalTypeConfig(goal.goalType);
      const trackingState = getGoalTrackingState(goal);
      if (trackingState === 'completed' || trackingState === 'paused') continue;
      if (goal.isPinned) continue;
      const list = groups.get(config.journeyStage);
      if (list) list.push(goal);
    }
    return groups;
  }, [sorted]);

  const completedGoals = useMemo(
    () =>
      sorted.filter((g) => {
        const st = getGoalTrackingState(g);
        return st === 'completed' || st === 'paused';
      }),
    [sorted],
  );

  const handleTogglePin = async (goal: GoalSummary, e: React.MouseEvent) => {
    e.stopPropagation();
    const newPinned = !goal.isPinned;

    // Optimistic update
    setGoals((prev) =>
      prev.map((g) => (g.id === goal.id ? { ...g, isPinned: newPinned } : g)),
    );

    try {
      await apiClient.toggleGoalPin(goal.id, newPinned);
      toast.success(newPinned ? t('goalPinned') : t('goalUnpinned'));
    } catch {
      // Revert on error
      setGoals((prev) =>
        prev.map((g) => (g.id === goal.id ? { ...g, isPinned: !newPinned } : g)),
      );
      toast.error(t('pinError'));
    }
  };

  return (
    <AppLayout>
      {/* Header */}
      <header className="flex flex-wrap items-end justify-between gap-4 mb-5">
        <div>
          <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
            {t('title')}
          </h1>
          <p className="mt-1.5 text-[15px] text-slate-500">
            {t('journeySubtitle')}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2">
            <Checkbox
              id="showCompleted"
              checked={showCompleted}
              onCheckedChange={(checked) => setShowCompleted(checked === true)}
            />
            <Label htmlFor="showCompleted" className="text-sm text-slate-600">
              {t('filters.showCompleted')}
            </Label>
          </div>
          <Link href="/goals/new">
            <Button>
              <PlusIcon className="mr-1.5 h-4 w-4" />
              {t('createGoal')}
            </Button>
          </Link>
        </div>
      </header>

      <div className="space-y-5">
        {/* Nudge */}
        {!isLoading && (
          <NudgeBanner
            tone={nudge.tone}
            message={nudge.message}
            ctaLabel={nudge.ctaLabel}
            ctaHref={nudge.ctaHref}
          />
        )}

        {/* Journey map (soft dots) */}
        {!isLoading && sorted.length > 0 && (
          <JourneyMap currentStage={currentStage} activeStages={activeStages} stageLabels={t} />
        )}

        {/* Loading */}
        {isLoading ? (
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div
                key={i}
                className="h-40 animate-pulse rounded-[26px] border border-violet-100/80 bg-white/80"
              />
            ))}
          </div>
        ) : sorted.length === 0 ? (
          /* Empty state */
          <section className="rounded-[28px] border border-violet-100/80 bg-white/92 p-10 text-center shadow-[0_20px_50px_-30px_rgba(76,29,149,0.45)]">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-violet-100 text-violet-600">
              <SparklesIcon className="h-7 w-7" />
            </div>
            <h3 className="mt-4 text-xl font-semibold text-slate-900">{t('emptyState.title')}</h3>
            <p className="mt-2 text-sm text-slate-500">
              {t('emptyState.description')}
            </p>
            <Link href="/goals/new" className="mt-5 inline-flex">
              <Button>
                <PlusIcon className="mr-1.5 h-4 w-4" />
                {t('emptyState.cta')}
              </Button>
            </Link>
          </section>
        ) : (
          /* Section-divider layout grouped by stage */
          <div className="space-y-6">
            {/* Pinned section */}
            {pinnedGoals.length > 0 && (
              <section>
                <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-violet-500">
                  {t('pinned')}
                </h2>
                <div className="space-y-3">
                  {pinnedGoals.map((goal) => (
                    <GoalCard
                      key={goal.id}
                      goal={goal}
                      ctx={goalCtx}
                      isFocus={goal.id === focusGoalId}
                      onClick={() => router.push(`/goals/${goal.id}`)}
                      onTogglePin={handleTogglePin}
                      t={t}
                    />
                  ))}
                </div>
              </section>
            )}

            {/* Stage sections */}
            {JOURNEY_STAGES.map((stage) => {
              const stageGoals = stageGroups.get(stage.key) ?? [];
              if (stageGoals.length === 0) return null;
              return (
                <section key={stage.key}>
                  <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
                    {t(`stages.${stage.key}`)}
                  </h2>
                  <div className="space-y-3">
                    {stageGoals.map((goal) => (
                      <GoalCard
                        key={goal.id}
                        goal={goal}
                        ctx={goalCtx}
                        isFocus={goal.id === focusGoalId}
                        onClick={() => router.push(`/goals/${goal.id}`)}
                        onTogglePin={handleTogglePin}
                        t={t}
                      />
                    ))}
                  </div>
                </section>
              );
            })}

            {/* Completed / paused at the bottom */}
            {showCompleted && completedGoals.length > 0 && (
              <section>
                <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('completed')}
                </h2>
                <div className="space-y-3">
                  {completedGoals.map((goal) => (
                    <GoalCard
                      key={goal.id}
                      goal={goal}
                      ctx={goalCtx}
                      isFocus={false}
                      onClick={() => router.push(`/goals/${goal.id}`)}
                      onTogglePin={handleTogglePin}
                      t={t}
                    />
                  ))}
                </div>
              </section>
            )}
          </div>
        )}
      </div>
    </AppLayout>
  );
}

// --- Journey map (soft dots, no numbers) ---

function JourneyMap({
  currentStage,
  activeStages,
  stageLabels,
}: {
  currentStage: JourneyStage | null;
  activeStages: Set<JourneyStage>;
  stageLabels: ReturnType<typeof useTranslations<'goals'>>;
}) {
  return (
    <div className="flex items-center justify-center gap-0 overflow-x-auto rounded-2xl border border-violet-100/60 bg-white/80 px-4 py-3.5 shadow-sm">
      {JOURNEY_STAGES.map((stage, i) => {
        const isCurrent = stage.key === currentStage;
        const hasGoals = activeStages.has(stage.key);
        const isLast = i === JOURNEY_STAGES.length - 1;

        return (
          <div key={stage.key} className="flex items-center">
            <div className="flex flex-col items-center gap-1.5">
              {/* Dot */}
              <div className="relative flex items-center justify-center">
                {/* Pulse ring for current focus stage */}
                {isCurrent && (
                  <span className="absolute h-5 w-5 animate-ping rounded-full bg-violet-400/30" />
                )}
                <span
                  className={cn(
                    'relative block rounded-full transition-colors',
                    isCurrent
                      ? 'h-3.5 w-3.5 bg-violet-600 ring-[3px] ring-violet-200'
                      : hasGoals
                        ? 'h-2.5 w-2.5 bg-violet-500'
                        : 'h-2.5 w-2.5 border-[1.5px] border-slate-300 bg-white',
                  )}
                />
              </div>
              {/* Label */}
              <span
                className={cn(
                  'text-[10px] font-semibold uppercase tracking-[0.08em]',
                  isCurrent
                    ? 'text-violet-700'
                    : hasGoals
                      ? 'text-violet-500'
                      : 'text-slate-400',
                )}
              >
                {stageLabels(`stages.${stage.key}`)}
              </span>
            </div>
            {/* Connector line */}
            {!isLast && (
              <div
                className={cn(
                  'mx-2 h-px w-8 sm:w-12',
                  hasGoals || isCurrent ? 'bg-violet-200' : 'bg-slate-200',
                )}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}

// --- Nudge banner ---

function NudgeBanner({
  tone,
  message,
  ctaLabel,
  ctaHref,
}: {
  tone: 'amber' | 'rose' | 'emerald';
  message: string;
  ctaLabel?: string;
  ctaHref?: string;
}) {
  const toneStyles = {
    amber: {
      wrapper: 'border-amber-200/70 bg-amber-50/50',
      icon: 'bg-amber-100 text-amber-700',
      cta: 'text-amber-700 hover:text-amber-800',
    },
    rose: {
      wrapper: 'border-rose-200/70 bg-rose-50/50',
      icon: 'bg-rose-100 text-rose-700',
      cta: 'text-rose-700 hover:text-rose-800',
    },
    emerald: {
      wrapper: 'border-emerald-200/70 bg-emerald-50/50',
      icon: 'bg-emerald-100 text-emerald-700',
      cta: 'text-emerald-700 hover:text-emerald-800',
    },
  };
  const s = toneStyles[tone];

  return (
    <section className={cn('rounded-2xl border p-4', s.wrapper)}>
      <div className="flex items-start gap-3">
        <div
          className={cn(
            'mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg',
            s.icon,
          )}
        >
          <LightBulbIcon className="h-[18px] w-[18px]" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-slate-800">{message}</p>
          {ctaLabel && ctaHref && (
            <Link
              href={ctaHref}
              className={cn(
                'mt-1.5 inline-flex items-center text-xs font-semibold transition-colors',
                s.cta,
              )}
            >
              {ctaLabel} &rarr;
            </Link>
          )}
        </div>
      </div>
    </section>
  );
}

// --- Goal card ---

function GoalCard({
  goal,
  ctx,
  isFocus,
  onClick,
  onTogglePin,
  t,
}: {
  goal: GoalSummary;
  ctx: GoalContext;
  isFocus: boolean;
  onClick: () => void;
  onTogglePin: (goal: GoalSummary, e: React.MouseEvent) => void;
  t: ReturnType<typeof useTranslations<'goals'>>;
}) {
  const config = getGoalTypeConfig(goal.goalType);
  const trackingState = getGoalTrackingState(goal);
  const stateStyle = TRACKING_STATE_STYLES[trackingState];
  const hero = config.heroMetric(goal, ctx);
  const Icon = config.icon;

  return (
    <article
      onClick={onClick}
      className="cursor-pointer rounded-[26px] border border-violet-100/80 bg-white/90 p-5 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)] transition-shadow hover:shadow-[0_24px_52px_-28px_rgba(76,29,149,0.55)]"
    >
      {/* Top row: icon + name + badges + pin */}
      <div className="flex items-start gap-3">
        <div
          className={cn(
            'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl',
            config.accentColor.bg,
            config.accentColor.text,
          )}
        >
          <Icon className="h-5 w-5" />
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h3 className="line-clamp-1 text-lg font-semibold tracking-[-0.02em] text-slate-900">
              {goal.name}
            </h3>
            <span
              className={cn(
                'shrink-0 rounded-full border px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-[0.08em]',
                stateStyle.bg,
                stateStyle.text,
                stateStyle.border,
              )}
            >
              {stateStyle.label}
            </span>
            {isFocus && (
              <span className="shrink-0 rounded-full bg-violet-600 px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-white">
                {t('focus')}
              </span>
            )}
          </div>
        </div>

        {/* Pin toggle */}
        <button
          onClick={(e) => onTogglePin(goal, e)}
          className="shrink-0 rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-violet-50 hover:text-violet-600"
          title={goal.isPinned ? t('unpinGoal') : t('pinGoal')}
        >
          {goal.isPinned ? (
            <MapPinSolidIcon className="h-4.5 w-4.5 text-violet-600" />
          ) : (
            <MapPinOutlineIcon className="h-4.5 w-4.5" />
          )}
        </button>
      </div>

      {/* Hero metric + amounts */}
      <div className="mt-3 flex flex-wrap items-baseline justify-between gap-2">
        {hero ? (
          <div>
            <p className="text-base font-semibold text-slate-900">{hero.label}</p>
            {hero.subtext && (
              <p className="text-xs text-slate-500">{hero.subtext}</p>
            )}
          </div>
        ) : (
          <p className="text-base font-semibold text-slate-900">
            {t('funded', { percent: `${goal.progressPercentage.toFixed(0)}%` })}
          </p>
        )}
        <p className="text-sm text-slate-600">
          {formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}
        </p>
      </div>

      {/* Progress bar */}
      <div className="mt-3 h-2.5 overflow-hidden rounded-full bg-slate-200">
        <div
          className={cn('h-full rounded-full transition-all', config.accentColor.bar)}
          style={{ width: `${Math.min(goal.progressPercentage, 100)}%` }}
        />
      </div>

      {/* Footer: metadata + link */}
      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
        <div className="flex flex-wrap items-center gap-3">
          {goal.deadline && goal.daysRemaining !== undefined && (
            <span className="inline-flex items-center gap-1">
              <CalendarDaysIcon className="h-3.5 w-3.5" />
              {goal.daysRemaining > 0
                ? t('daysRemaining', { count: goal.daysRemaining })
                : t('deadlinePassed')}
            </span>
          )}
          {goal.linkedAccountName && (
            <span className="inline-flex items-center gap-1">
              <LinkIcon className="h-3.5 w-3.5" />
              {goal.linkedAccountName}
            </span>
          )}
        </div>
        <span className="inline-flex items-center gap-1 text-xs font-semibold text-violet-600">
          {t('viewDetails')}
          <ArrowRightIcon className="h-3.5 w-3.5" />
        </span>
      </div>
    </article>
  );
}
