'use client';

import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { BudgetProgressBar } from './budget-progress-bar';
import { BudgetSummary } from '@/types/budget';
import { formatCurrency } from '@/types/budget';
import { cn } from '@/lib/utils';
import { CalendarDays, ChevronRight, Repeat, Tag } from 'lucide-react';

interface BudgetProgressCardProps {
  budget: BudgetSummary;
  showActions?: boolean;
  className?: string;
}

export function BudgetProgressCard({
  budget,
  showActions = true,
  className,
}: BudgetProgressCardProps) {
  const t = useTranslations('budgets');

  const getStatusBadge = () => {
    if (budget.usedPercentage >= 100) {
      return (
        <Badge variant="destructive" className="text-xs">
          {t('overBudget')}
        </Badge>
      );
    }
    if (budget.usedPercentage >= 80) {
      return (
        <Badge variant="outline" className="text-xs border-yellow-500 text-yellow-600 bg-yellow-50">
          {t('approaching')}
        </Badge>
      );
    }
    return (
      <Badge variant="outline" className="text-xs border-green-500 text-green-600 bg-green-50">
        {t('onTrack')}
      </Badge>
    );
  };

  const getPeriodBadge = () => {
    if (budget.isCurrentPeriod) {
      return (
        <Badge variant="secondary" className="text-xs">
          {t('current')}
        </Badge>
      );
    }
    const today = new Date();
    const startDate = new Date(budget.startDate);
    if (startDate > today) {
      return (
        <Badge variant="outline" className="text-xs">
          {t('upcoming')}
        </Badge>
      );
    }
    return (
      <Badge variant="outline" className="text-xs text-muted-foreground">
        {t('past')}
      </Badge>
    );
  };

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
    });
  };

  return (
    <Card className={cn('bg-white/90 backdrop-blur-xs border-0 shadow-lg hover:shadow-xl transition-shadow', className)}>
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <CardTitle className="text-lg font-semibold">
            {budget.name}
          </CardTitle>
          <div className="flex items-center gap-1.5 flex-shrink-0">
            {getPeriodBadge()}
            {getStatusBadge()}
            {!budget.isActive && (
              <Badge variant="outline" className="text-xs text-muted-foreground">
                {t('inactive')}
              </Badge>
            )}
          </div>
        </div>
        {budget.description && (
          <p className="text-sm text-muted-foreground line-clamp-1 mt-1">
            {budget.description}
          </p>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Progress Section */}
        <div className="space-y-2">
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">
              {t('spentOfBudget', {
                spent: formatCurrency(budget.totalSpent),
                budget: formatCurrency(budget.totalBudgeted),
              })}
            </span>
            <span className={cn(
              'font-medium',
              budget.totalRemaining >= 0 ? 'text-green-600' : 'text-red-600'
            )}>
              {budget.totalRemaining >= 0 ? '+' : ''}{formatCurrency(budget.totalRemaining)}
            </span>
          </div>
          <BudgetProgressBar usedPercentage={budget.usedPercentage} size="md" />
        </div>

        {/* Info Row */}
        <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
          <div className="flex items-center gap-1">
            <CalendarDays className="h-3.5 w-3.5" />
            <span className="whitespace-nowrap">
              {formatDate(budget.startDate)} - {formatDate(budget.endDate)}
            </span>
          </div>
          <div className="flex items-center gap-1">
            <Tag className="h-3.5 w-3.5" />
            <span>
              {budget.categoryCount === 1
                ? t('categoryCountOne')
                : t('categoryCount', { count: budget.categoryCount })}
            </span>
          </div>
          {budget.isRecurring && (
            <div className="flex items-center gap-1 text-blue-600" title={t('recurring')}>
              <Repeat className="h-3.5 w-3.5" />
              <span>{t('recurring')}</span>
            </div>
          )}
          {budget.isCurrentPeriod && budget.daysRemaining > 0 && (
            <span className="text-muted-foreground">
              {budget.daysRemaining === 1
                ? t('daysRemainingOne')
                : t('daysRemaining', { count: budget.daysRemaining })}
            </span>
          )}
        </div>

        {/* Actions */}
        {showActions && (
          <div className="pt-2">
            <Link href={`/budgets/${budget.id}`}>
              <Button variant="outline" className="w-full">
                {t('budgetDetails')}
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </Link>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
