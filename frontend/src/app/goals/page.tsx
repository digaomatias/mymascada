'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import Navigation from '@/components/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { apiClient, GoalSummary } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { toast } from 'sonner';
import { FlagIcon, PlusIcon } from '@heroicons/react/24/outline';

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

export default function GoalsPage() {
  const t = useTranslations('goals');
  const router = useRouter();

  const [goals, setGoals] = useState<GoalSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showCompleted, setShowCompleted] = useState(false);

  const loadGoals = async () => {
    try {
      setIsLoading(true);
      const data = await apiClient.getGoals({
        includeCompleted: showCompleted,
      });
      setGoals(data);
    } catch {
      toast.error(t('loadError'));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadGoals();
  }, [showCompleted]);

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />

      <main className="container mx-auto px-4 py-6 space-y-6">
        {/* Header with Filters */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2 text-gray-900">
              <FlagIcon className="h-6 w-6" />
              {t('title')}
            </h1>
            <p className="text-gray-600">{t('subtitle')}</p>
          </div>

          <div className="flex items-center gap-4 sm:gap-6">
            {/* Filters */}
            <div className="flex items-center gap-4 text-sm">
              <div className="flex items-center space-x-2">
                <Checkbox
                  id="showCompleted"
                  checked={showCompleted}
                  onCheckedChange={(checked) => setShowCompleted(checked === true)}
                />
                <Label htmlFor="showCompleted" className="text-sm text-gray-700 cursor-pointer">
                  {t('filters.showCompleted')}
                </Label>
              </div>
            </div>

            {/* Create button */}
            <Link href="/goals/new">
              <Button>
                <PlusIcon className="h-4 w-4 mr-2" />
                {t('createGoal')}
              </Button>
            </Link>
          </div>
        </div>

        {/* Goal List */}
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
        ) : goals.length === 0 ? (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="py-12">
              <div className="flex flex-col items-center justify-center text-center space-y-4">
                <div className="rounded-full bg-primary-100 p-4">
                  <FlagIcon className="h-8 w-8 text-primary-600" />
                </div>
                <div>
                  <h3 className="font-semibold text-lg text-gray-900">{t('noGoals')}</h3>
                  <p className="text-gray-600 mt-1">
                    {t('noGoalsDescription')}
                  </p>
                </div>
                <Link href="/goals/new">
                  <Button>
                    <PlusIcon className="h-4 w-4 mr-2" />
                    {t('createFirstGoal')}
                  </Button>
                </Link>
              </div>
            </CardContent>
          </Card>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {goals.map((goal) => (
              <Card
                key={goal.id}
                className="bg-white/90 backdrop-blur-xs border-0 shadow-lg hover:shadow-xl transition-shadow cursor-pointer"
                onClick={() => router.push(`/goals/${goal.id}`)}
              >
                <CardContent className="p-6 space-y-4">
                  <div className="flex items-start justify-between">
                    <div className="min-w-0 flex-1">
                      <h3 className="font-semibold text-lg text-gray-900 truncate">{goal.name}</h3>
                      <Badge variant="secondary" className="mt-1 text-xs">
                        {t(`goalTypes.${goal.goalType}`)}
                      </Badge>
                    </div>
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
                  </div>

                  {/* Progress bar */}
                  <div className="space-y-2">
                    <div className="flex justify-between text-sm">
                      <span className="text-gray-600">{t('detail.progress')}</span>
                      <span className="font-medium">{goal.progressPercentage.toFixed(0)}%</span>
                    </div>
                    <div className={`h-2.5 rounded-full ${getProgressBgColor(goal.progressPercentage)}`}>
                      <div
                        className={`h-full rounded-full transition-all duration-500 ${getProgressColor(goal.progressPercentage)}`}
                        style={{ width: `${Math.min(goal.progressPercentage, 100)}%` }}
                      />
                    </div>
                    <div className="flex justify-between text-xs text-gray-500">
                      <span>{formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}</span>
                      {goal.deadline && goal.daysRemaining !== undefined && (
                        <span>
                          {goal.daysRemaining > 0
                            ? goal.daysRemaining === 1
                              ? t('detail.daysRemainingOne')
                              : t('detail.daysRemaining', { count: goal.daysRemaining })
                            : t('detail.overdue')}
                        </span>
                      )}
                    </div>
                  </div>

                  {goal.linkedAccountName && (
                    <p className="text-xs text-gray-500 truncate">
                      {t('detail.linkedAccount')}: {goal.linkedAccountName}
                    </p>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
