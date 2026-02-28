import { AppLayout } from '@/components/app-layout';
import { RulesSkeleton } from '@/components/skeletons';
import { Skeleton } from '@/components/ui/skeleton';

export default function RulesLoading() {
  return (
    <AppLayout>
      {/* Header shimmer */}
      <div className="mb-5 animate-pulse">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <Skeleton className="h-9 w-56" />
            <Skeleton className="h-4 w-40 mt-1.5" />
          </div>
          <div className="flex gap-2">
            <Skeleton className="h-9 w-32 rounded-lg" variant="rounded-sm" />
            <Skeleton className="h-9 w-28 rounded-lg" variant="rounded-sm" />
          </div>
        </div>
      </div>

      <RulesSkeleton />
    </AppLayout>
  );
}
