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
          'rounded-[28px] border border-violet-100/70 bg-white/85 p-6 shadow-[0_20px_50px_-28px_rgba(76,29,149,0.3)] backdrop-blur-xl sm:p-8',
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
          'rounded-[28px] border border-rose-200/70 bg-white/85 p-6 shadow-[0_20px_50px_-28px_rgba(76,29,149,0.3)] backdrop-blur-xl sm:p-8',
          colSpanClass,
          className,
        )}
      >
        <p className="text-sm text-rose-600">{error}</p>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-[28px] border border-violet-100/70 shadow-[0_20px_50px_-28px_rgba(76,29,149,0.3)] backdrop-blur-xl',
        gradient
          ? 'bg-gradient-to-br from-[#f5f0ff] via-white to-[#fdf2ff] border-violet-200/50'
          : 'bg-white/85',
        'p-6 sm:p-8',
        colSpanClass,
        className,
      )}
    >
      {children}
    </div>
  );
}
