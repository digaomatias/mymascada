'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent } from '@/components/ui/card';
import { apiClient, GoalSummary } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { LightBulbIcon, SparklesIcon } from '@heroicons/react/24/outline';

interface CoachingInsightCardProps {
  monthlyIncome: number;
  monthlyExpenses: number;
}

function getInsight(
  goals: GoalSummary[],
  monthlyIncome: number,
  monthlyExpenses: number,
): { key: string; params?: Record<string, string>; icon: 'lightbulb' | 'sparkles' } {
  // Overspending takes priority
  if (monthlyExpenses > monthlyIncome && monthlyIncome > 0) {
    return { key: 'overSpending', icon: 'lightbulb' };
  }

  // No goals
  if (goals.length === 0) {
    return { key: 'noGoals', icon: 'lightbulb' };
  }

  // Find primary goal (highest progress among active non-complete)
  const activeGoals = goals.filter((g) => g.status !== 'Completed');
  if (activeGoals.length === 0) {
    return { key: 'default', icon: 'sparkles' };
  }

  const primary = activeGoals.reduce((best, g) =>
    g.progressPercentage > best.progressPercentage ? g : best,
  );

  // Fresh user with goal at 0% - show emergency fund motivation
  if (primary.progressPercentage === 0 && primary.currentAmount === 0) {
    const motivationMessages = 5;
    const motivationIndex = primary.id % motivationMessages;
    return {
      key: `emergencyMotivation${motivationIndex + 1}`,
      icon: 'lightbulb',
    };
  }

  // Check if on track based on deadline
  if (primary.deadline && primary.daysRemaining !== undefined && primary.daysRemaining > 0) {
    const createdDate = new Date();
    createdDate.setDate(createdDate.getDate() - 30); // approximate
    const deadlineDate = new Date(primary.deadline);
    const totalDays = Math.max(
      (deadlineDate.getTime() - createdDate.getTime()) / (1000 * 60 * 60 * 24),
      1,
    );
    const elapsedDays = totalDays - primary.daysRemaining;
    const expectedProgress = Math.min((elapsedDays / totalDays) * 100, 100);

    if (primary.progressPercentage >= expectedProgress) {
      return {
        key: 'onTrack',
        params: { goalName: primary.name },
        icon: 'sparkles',
      };
    }

    // Behind schedule - suggest extra amount
    const remaining = primary.targetAmount - primary.currentAmount;
    const monthsLeft = Math.max(primary.daysRemaining / 30, 1);
    const monthlyNeeded = remaining / monthsLeft;
    const monthlySavings = monthlyIncome - monthlyExpenses;
    const suggestedExtra = Math.max(monthlyNeeded - monthlySavings, 0);

    if (suggestedExtra > 0) {
      return {
        key: 'behindSchedule',
        params: {
          amount: formatCurrency(Math.round(suggestedExtra)),
          goalName: primary.name,
        },
        icon: 'lightbulb',
      };
    }
  }

  // Goal with some progress but no deadline or on-track info
  if (primary.progressPercentage > 0) {
    return {
      key: 'progressEncouragement',
      params: {
        amount: formatCurrency(primary.currentAmount),
        goalName: primary.name,
      },
      icon: 'sparkles',
    };
  }

  return { key: 'default', icon: 'sparkles' };
}

export function CoachingInsightCard({
  monthlyIncome,
  monthlyExpenses,
}: CoachingInsightCardProps) {
  const t = useTranslations('dashboard.coaching');
  const [goals, setGoals] = useState<GoalSummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadGoals = async () => {
      try {
        const data = await apiClient.getGoals({ includeCompleted: false });
        setGoals(data);
      } catch (error) {
        console.error('Failed to load goals for coaching:', error);
        setGoals([]);
      } finally {
        setLoading(false);
      }
    };

    loadGoals();
  }, []);

  if (loading) {
    return null;
  }

  const insight = getInsight(goals, monthlyIncome, monthlyExpenses);
  const IconComponent = insight.icon === 'lightbulb' ? LightBulbIcon : SparklesIcon;

  return (
    <Card className="bg-white/90 backdrop-blur-xs border-0 border-l-4 border-l-primary-500 shadow-lg">
      <CardContent className="p-4 lg:p-6">
        <div className="flex items-start gap-3">
          <div className="flex-shrink-0 w-10 h-10 rounded-full bg-primary-100 flex items-center justify-center">
            <IconComponent className="w-5 h-5 text-primary-600" />
          </div>
          <div>
            <p className="text-xs font-semibold text-primary-600 uppercase tracking-wide mb-1">
              {t('title')}
            </p>
            <p className="text-sm text-gray-700 leading-relaxed">
              {t(insight.key, insight.params)}
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
