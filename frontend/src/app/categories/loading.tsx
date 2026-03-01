import { AppLayout } from '@/components/app-layout';
import { CategoriesSkeleton } from '@/components/skeletons';
import { Skeleton } from '@/components/ui/skeleton';

export default function CategoriesLoading() {
  return (
    <AppLayout>
      {/* Header shimmer */}
      <div className="mb-5 animate-pulse">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <Skeleton className="h-9 w-48" />
            <Skeleton className="h-4 w-64 mt-1.5" />
          </div>
          <Skeleton className="h-9 w-32 rounded-lg" variant="rounded-sm" />
        </div>

        {/* Tabs shimmer */}
        <div className="mt-4 border-b border-slate-200">
          <div className="flex gap-4 sm:gap-6 -mb-px">
            <Skeleton className="h-10 w-28" />
            <Skeleton className="h-10 w-32" />
          </div>
        </div>

        {/* Search bar shimmer */}
        <Skeleton className="mt-4 h-10 w-full rounded-xl" variant="rounded-sm" />
      </div>

      <CategoriesSkeleton />
    </AppLayout>
  );
}
