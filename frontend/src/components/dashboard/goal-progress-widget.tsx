'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { apiClient, GoalSummary } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { GoalProgressRing } from '@/components/dashboard/goal-progress-ring';
import { MilestoneCelebration } from '@/components/dashboard/milestone-celebration';
import {
  FlagIcon,
  ChevronRightIcon,
  PlusIcon,
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

function getRingColor(percentage: number): string {
  if (percentage >= 100) return '#10b981';
  if (percentage >= 90) return '#3b82f6';
  if (percentage >= 60) return '#eab308';
  return '#22c55e';
}

interface GoalProgressWidgetProps {
  heroMode?: boolean;
}

export function GoalProgressWidget({ heroMode = true }: GoalProgressWidgetProps) {
  const t = useTranslations('goals');
  const tDashboard = useTranslations('dashboard');
  const tHero = useTranslations('dashboard.hero');
  const [goals, setGoals] = useState<GoalSummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadGoals = async () => {
      try {
        setLoading(true);
        const data = await apiClient.getGoals({ includeCompleted: false });
        setGoals(data);
      } catch (error) {
        console.error('Failed to load goals:', error);
        setGoals([]);
      } finally {
        setLoading(false);
      }
    };

    loadGoals();
  }, []);

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
          {heroMode ? (
            <div className="flex items-center gap-6">
              <Skeleton className="h-[120px] w-[120px] rounded-full flex-shrink-0" />
              <div className="flex-1 space-y-3">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-4 w-32" />
                <Skeleton className="h-4 w-24" />
              </div>
            </div>
          ) : (
            <>
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-20 w-full" />
            </>
          )}
        </CardContent>
      </Card>
    );
  }

  // No goals state
  if (goals.length === 0) {
    if (!heroMode) {
      // Classic: compact inline CTA
      return (
        <div className="flex items-center justify-between bg-white/90 backdrop-blur-xs rounded-lg shadow-sm border border-gray-100 px-4 py-3">
          <div className="flex items-center gap-3">
            <FlagIcon className="h-5 w-5 text-primary-600 flex-shrink-0" />
            <p className="text-sm text-gray-600">{t('noGoalsDescription')}</p>
          </div>
          <Link href="/goals/new">
            <Button size="sm" variant="secondary" className="flex-shrink-0">
              <PlusIcon className="h-4 w-4 mr-1" />
              {t('createFirstGoal')}
            </Button>
          </Link>
        </div>
      );
    }

    // Hero: enhanced CTA
    return (
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardContent className="p-6 lg:p-8">
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-primary-400 to-primary-600 rounded-2xl shadow-lg flex items-center justify-center mx-auto mb-4">
              <FlagIcon className="w-8 h-8 text-white" />
            </div>
            <h3 className="text-xl font-bold text-gray-900 mb-2">
              {tHero('createGoal')}
            </h3>
            <p className="text-sm text-gray-600 mb-6 max-w-md mx-auto">
              {tHero('createGoalDesc')}
            </p>
            <Link href="/goals/new">
              <Button className="bg-gradient-to-r from-primary-500 to-primary-700 hover:from-primary-600 hover:to-primary-800 text-white">
                <PlusIcon className="h-4 w-4 mr-2" />
                {tHero('createGoal')}
              </Button>
            </Link>
          </div>
        </CardContent>
      </Card>
    );
  }

  // Sort by progress desc
  const sorted = [...goals].sort(
    (a, b) => b.progressPercentage - a.progressPercentage,
  );
  const topGoals = sorted.slice(0, 3);
  const primary = sorted[0];
  const others = sorted.slice(1, 3);

  // Classic mode: compact card with progress bars
  if (!heroMode) {
    return (
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <FlagIcon className="h-6 w-6 text-primary-600" />
              <CardTitle className="text-xl font-bold text-gray-900">{t('title')}</CardTitle>
            </div>
            <Link href="/goals">
              <Button variant="secondary" size="sm">
                {tDashboard('viewAll')}
                <ChevronRightIcon className="h-4 w-4 ml-1" />
              </Button>
            </Link>
          </div>
        </CardHeader>
        <CardContent className="space-y-2">
          {topGoals.map((goal) => (
            <Link
              key={goal.id}
              href={`/goals/${goal.id}`}
              className="block p-3 rounded-lg border hover:bg-gray-50 transition-colors"
            >
              <div className="flex items-center justify-between mb-2">
                <span className="font-medium text-sm truncate">{goal.name}</span>
                <span className="text-sm font-medium text-gray-600">
                  {goal.progressPercentage.toFixed(0)}%
                </span>
              </div>
              <div className={`h-2 rounded-full ${getProgressBgColor(goal.progressPercentage)}`}>
                <div
                  className={`h-full rounded-full transition-all duration-500 ${getProgressColor(goal.progressPercentage)}`}
                  style={{ width: `${Math.min(goal.progressPercentage, 100)}%` }}
                />
              </div>
              <div className="flex justify-between text-xs text-gray-500 mt-1">
                <span>{formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}</span>
                {goal.deadline && goal.daysRemaining !== undefined && goal.daysRemaining > 0 && (
                  <span>
                    {goal.daysRemaining === 1
                      ? t('detail.daysRemainingOne')
                      : t('detail.daysRemaining', { count: goal.daysRemaining })}
                  </span>
                )}
              </div>
            </Link>
          ))}

          {goals.length > 3 && (
            <div className="text-center pt-2">
              <Link href="/goals" className="text-sm text-primary-600 hover:text-primary-700">
                +{goals.length - 3} more goals
              </Link>
            </div>
          )}
        </CardContent>
      </Card>
    );
  }

  // Hero mode: large ring for primary goal + compact list for others
  return (
    <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FlagIcon className="h-6 w-6 text-primary-600" />
            <CardTitle className="text-xl font-bold text-gray-900">
              {tHero('primaryGoal')}
            </CardTitle>
          </div>
          <Link href="/goals">
            <Button variant="secondary" size="sm">
              {tHero('viewAllGoals')}
              <ChevronRightIcon className="h-4 w-4 ml-1" />
            </Button>
          </Link>
        </div>
      </CardHeader>
      <CardContent>
        {/* Primary goal with ring */}
        <Link
          href={`/goals/${primary.id}`}
          className="flex items-center gap-6 p-4 rounded-xl border hover:bg-gray-50 transition-colors"
        >
          <div className="flex-shrink-0">
            <GoalProgressRing
              percentage={primary.progressPercentage}
              size={120}
              strokeWidth={8}
              color={getRingColor(primary.progressPercentage)}
            />
          </div>
          <div className="flex-1 min-w-0">
            <h3 className="text-lg font-semibold text-gray-900 truncate">
              {primary.name}
            </h3>
            <p className="text-sm text-gray-600 mt-1">
              {formatCurrency(primary.currentAmount)} / {formatCurrency(primary.targetAmount)}
            </p>
            {primary.deadline && primary.daysRemaining !== undefined && primary.daysRemaining > 0 && (
              <p className="text-sm text-gray-500 mt-1">
                {primary.daysRemaining === 1
                  ? t('detail.daysRemainingOne')
                  : t('detail.daysRemaining', { count: primary.daysRemaining })}
              </p>
            )}
          </div>
        </Link>

        <MilestoneCelebration
          goalId={primary.id}
          percentage={primary.progressPercentage}
          goalName={primary.name}
        />

        {/* Other goals - compact list */}
        {others.length > 0 && (
          <div className="mt-4 pt-4 border-t">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">
              {tHero('otherGoals')}
            </p>
            <div className="space-y-2">
              {others.map((goal) => (
                <Link
                  key={goal.id}
                  href={`/goals/${goal.id}`}
                  className="block p-3 rounded-lg border hover:bg-gray-50 transition-colors"
                >
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-medium text-sm truncate">
                      {goal.name}
                    </span>
                    <span className="text-sm font-medium text-gray-600">
                      {goal.progressPercentage.toFixed(0)}%
                    </span>
                  </div>
                  <div
                    className={`h-2 rounded-full ${getProgressBgColor(goal.progressPercentage)}`}
                  >
                    <div
                      className={`h-full rounded-full transition-all duration-500 ${getProgressColor(goal.progressPercentage)}`}
                      style={{
                        width: `${Math.min(goal.progressPercentage, 100)}%`,
                      }}
                    />
                  </div>
                  <div className="flex justify-between text-xs text-gray-500 mt-1">
                    <span>
                      {formatCurrency(goal.currentAmount)} /{' '}
                      {formatCurrency(goal.targetAmount)}
                    </span>
                    {goal.deadline &&
                      goal.daysRemaining !== undefined &&
                      goal.daysRemaining > 0 && (
                        <span>
                          {goal.daysRemaining === 1
                            ? t('detail.daysRemainingOne')
                            : t('detail.daysRemaining', {
                                count: goal.daysRemaining,
                              })}
                        </span>
                      )}
                  </div>
                </Link>
              ))}
            </div>
          </div>
        )}

        {/* Show more link if more than 3 goals */}
        {goals.length > 3 && (
          <div className="text-center pt-3">
            <Link
              href="/goals"
              className="text-sm text-primary-600 hover:text-primary-700"
            >
              +{goals.length - 3} {tDashboard('viewAll').toLowerCase()}
            </Link>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
