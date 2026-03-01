import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

/** A shimmer card matching the app's rounded-[26px] card style */
export function SkeletonCard({
  className,
  children,
}: {
  className?: string;
  children?: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        'rounded-[26px] border border-violet-100/80 bg-white/80 animate-pulse',
        className,
      )}
    >
      {children}
    </div>
  );
}

/** Shimmer rows for list/table pages */
export function SkeletonTableRows({ count = 5 }: { count?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: count }).map((_, i) => (
        <div
          key={i}
          className="flex items-center gap-3 rounded-[26px] border border-violet-100/80 bg-white/80 p-5 animate-pulse"
        >
          <Skeleton className="h-10 w-10 rounded-xl" variant="rounded-sm" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-3 w-1/5" />
          </div>
          <Skeleton className="h-5 w-20" />
        </div>
      ))}
    </div>
  );
}

/** Shimmer for a search input + filter buttons bar */
export function SkeletonFilterBar({ className }: { className?: string }) {
  return (
    <div className={cn('flex flex-wrap items-center gap-3 animate-pulse', className)}>
      <Skeleton className="h-10 flex-1 min-w-[200px] rounded-xl" variant="rounded-sm" />
      <Skeleton className="h-10 w-24 rounded-xl" variant="rounded-sm" />
      <Skeleton className="h-10 w-24 rounded-xl" variant="rounded-sm" />
    </div>
  );
}

/** Shimmer for a hero / summary section (e.g., net worth on accounts, insight banner) */
export function SkeletonHero({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-[26px] border border-violet-100/60 bg-white/80 p-6 animate-pulse',
        className,
      )}
    >
      <div className="space-y-3">
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-3 w-full max-w-xs" />
      </div>
    </div>
  );
}

/** Shimmer grid of stat cards (e.g., analytics summary) */
export function SkeletonStatCards({ count = 4 }: { count?: number }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {Array.from({ length: count }).map((_, i) => (
        <div
          key={i}
          className="h-32 rounded-2xl border border-violet-100/80 bg-white/85 animate-pulse"
        />
      ))}
    </div>
  );
}

/** Shimmer for a chart panel */
export function SkeletonPanel({
  className,
  height = 'h-[390px]',
}: {
  className?: string;
  height?: string;
}) {
  return (
    <div
      className={cn(
        'rounded-[26px] border border-violet-100/80 bg-white/85 animate-pulse',
        height,
        className,
      )}
    />
  );
}

/** Settings-style card skeleton */
export function SkeletonSettingsCard() {
  return (
    <div className="rounded-[26px] border border-violet-100/70 bg-white/80 p-6 animate-pulse">
      <div className="flex items-start gap-4">
        <Skeleton className="h-12 w-12 rounded-xl" variant="rounded-sm" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-5 w-40" />
          <Skeleton className="h-3 w-64" />
          <Skeleton className="h-10 w-full mt-2 rounded-xl" variant="rounded-sm" />
        </div>
      </div>
    </div>
  );
}
