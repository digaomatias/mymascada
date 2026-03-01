import { AppLayout } from '@/components/app-layout';
import { AnalyticsSkeleton } from '@/components/skeletons/analytics-skeleton';
import { Skeleton } from '@/components/ui/skeleton';

export default function AnalyticsLoading() {
  return (
    <AppLayout>
      {/* Header shimmer */}
      <header className="mb-5 flex flex-wrap items-end justify-between gap-4 animate-pulse">
        <div>
          <Skeleton className="h-9 w-48" />
          <Skeleton className="h-4 w-64 mt-1.5" />
        </div>
        <Skeleton className="h-10 w-44 rounded-lg" variant="rounded-sm" />
      </header>

      {/* Period selector shimmer */}
      <div className="mb-6 flex flex-wrap items-center gap-3 animate-pulse">
        <Skeleton className="h-10 w-32 rounded-xl" variant="rounded-sm" />
        <Skeleton className="h-10 w-28 rounded-xl" variant="rounded-sm" />
        <Skeleton className="h-10 w-28 rounded-xl" variant="rounded-sm" />
      </div>

      <AnalyticsSkeleton />
    </AppLayout>
  );
}
