'use client';

import { useTranslations } from 'next-intl';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { BudgetProgressBar } from './budget-progress-bar';
import { BudgetCategoryProgress } from '@/types/budget';
import { formatCurrency } from '@/types/budget';
import { cn } from '@/lib/utils';
import { Trash2, RefreshCw, FolderTree } from 'lucide-react';

interface CategoryBudgetRowProps {
  category: BudgetCategoryProgress;
  onRemove?: (categoryId: number) => void;
  onEdit?: (categoryId: number) => void;
  showActions?: boolean;
  className?: string;
}

export function CategoryBudgetRow({
  category,
  onRemove,
  onEdit,
  showActions = false,
  className,
}: CategoryBudgetRowProps) {
  const t = useTranslations('budgets');

  const getStatusBadge = () => {
    switch (category.status) {
      case 'Over':
        return (
          <Badge variant="destructive" className="text-xs">
            {t('overBudget')}
          </Badge>
        );
      case 'Approaching':
        return (
          <Badge variant="outline" className="text-xs border-yellow-500 text-yellow-600 bg-yellow-50">
            {t('approaching')}
          </Badge>
        );
      default:
        return (
          <Badge variant="outline" className="text-xs border-green-500 text-green-600 bg-green-50">
            {t('onTrack')}
          </Badge>
        );
    }
  };

  return (
    <div
      className={cn(
        'p-4 rounded-lg border bg-card hover:bg-accent/5 transition-colors',
        className
      )}
    >
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-2">
          {category.categoryIcon && (
            <span className="text-lg">{category.categoryIcon}</span>
          )}
          <div>
            <div className="flex items-center gap-2">
              <span className="font-medium">{category.categoryName}</span>
              {category.parentCategoryName && (
                <span className="text-xs text-muted-foreground">
                  ({category.parentCategoryName})
                </span>
              )}
            </div>
            <div className="flex items-center gap-2 mt-0.5">
              {category.allowRollover && (
                <div
                  className="flex items-center gap-1 text-xs text-blue-600"
                  title={t('wizard.allowRolloverHelp')}
                >
                  <RefreshCw className="h-3 w-3" />
                  <span>{t('wizard.allowRollover')}</span>
                </div>
              )}
              {category.includeSubcategories && (
                <div
                  className="flex items-center gap-1 text-xs text-purple-600"
                  title={t('wizard.includeSubcategoriesHelp')}
                >
                  <FolderTree className="h-3 w-3" />
                  <span>{t('wizard.includeSubcategories')}</span>
                </div>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge()}
          {showActions && (
            <>
              {onEdit && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => onEdit(category.categoryId)}
                >
                  {t('editBudget')}
                </Button>
              )}
              {onRemove && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => onRemove(category.categoryId)}
                  className="text-destructive hover:text-destructive"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              )}
            </>
          )}
        </div>
      </div>

      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted-foreground">
            {t('spentOfBudget', {
              spent: formatCurrency(category.actualSpent),
              budget: formatCurrency(category.effectiveBudget),
            })}
          </span>
          <span
            className={cn(
              'font-medium',
              category.remainingAmount >= 0 ? 'text-green-600' : 'text-red-600'
            )}
          >
            {category.remainingAmount >= 0 ? '+' : ''}
            {formatCurrency(category.remainingAmount)}
          </span>
        </div>
        <BudgetProgressBar usedPercentage={category.usedPercentage} size="sm" />

        {/* Additional Info */}
        <div className="flex items-center justify-between text-xs text-muted-foreground pt-1">
          <span>{category.transactionCount} transactions</span>
          {category.rolloverAmount > 0 && (
            <span className="text-blue-600">
              +{formatCurrency(category.rolloverAmount)} rollover
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
