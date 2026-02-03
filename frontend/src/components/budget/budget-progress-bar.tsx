'use client';

import { cn } from '@/lib/utils';

interface BudgetProgressBarProps {
  usedPercentage: number;
  showLabel?: boolean;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

export function BudgetProgressBar({
  usedPercentage,
  showLabel = false,
  size = 'md',
  className,
}: BudgetProgressBarProps) {
  // Clamp percentage for display (but allow over 100% for visual effect)
  const displayPercentage = Math.min(usedPercentage, 100);

  // Determine color based on usage
  const getBarColor = () => {
    if (usedPercentage >= 100) return 'bg-red-500';
    if (usedPercentage >= 80) return 'bg-yellow-500';
    return 'bg-green-500';
  };

  const getTextColor = () => {
    if (usedPercentage >= 100) return 'text-red-600';
    if (usedPercentage >= 80) return 'text-yellow-600';
    return 'text-green-600';
  };

  const heightClass = {
    sm: 'h-1.5',
    md: 'h-2.5',
    lg: 'h-4',
  }[size];

  return (
    <div className={cn('w-full', className)}>
      <div className={cn('w-full bg-gray-200 rounded-full overflow-hidden', heightClass)}>
        <div
          className={cn(
            'h-full rounded-full transition-all duration-300',
            getBarColor()
          )}
          style={{ width: `${displayPercentage}%` }}
        />
      </div>
      {showLabel && (
        <p className={cn('text-sm mt-1', getTextColor())}>
          {usedPercentage.toFixed(0)}% used
        </p>
      )}
    </div>
  );
}

export function getStatusFromPercentage(usedPercentage: number): 'OnTrack' | 'Approaching' | 'Over' {
  if (usedPercentage >= 100) return 'Over';
  if (usedPercentage >= 80) return 'Approaching';
  return 'OnTrack';
}
