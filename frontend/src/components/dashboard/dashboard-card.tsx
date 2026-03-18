'use client';

import { cn } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';

interface DashboardCardProps {
  cardId: string;
  children: React.ReactNode;
  className?: string;
  colSpan?: 'full' | 'half' | 'auto';
  loading?: boolean;
  error?: string | null;
  gradient?: boolean;
}

const baseCardClasses =
  'rounded-2xl border border-ink-200 bg-surface shadow-card';

export function DashboardCard({
  children,
  className,
  colSpan,
  loading = false,
  error = null,
  gradient = false,
}: DashboardCardProps) {
  const colSpanClass =
    colSpan === 'full'
      ? 'col-span-full'
      : colSpan === 'half'
        ? 'col-span-1'
        : '';

  if (loading) {
    return (
      <div
        className={cn(
          baseCardClasses,
          'p-6 sm:p-8',
          colSpanClass,
          className,
        )}
      >
        <div className="space-y-4">
          <Skeleton className="h-5 w-32" />
          <Skeleton className="h-8 w-48" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-3/4" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div
        className={cn(
          'rounded-2xl border border-danger-200 bg-surface p-6 shadow-card sm:p-8',
          colSpanClass,
          className,
        )}
      >
        <p className="text-sm text-danger-600">{error}</p>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'relative overflow-hidden',
        baseCardClasses,
        gradient
          ? 'bg-gradient-to-br from-surface-brand via-surface to-[oklch(98%_0.012_55)]'
          : '',
        'p-6 sm:p-8',
        colSpanClass,
        className,
      )}
    >
      {children}
    </div>
  );
}
