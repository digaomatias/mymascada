import { AppLayout } from '@/components/app-layout';
import { BudgetsSkeleton } from '@/components/skeletons';
import { Skeleton } from '@/components/ui/skeleton';

export default function BudgetsLoading() {
  return (
    <AppLayout>
      {/* Header shimmer */}
      <header className="flex flex-wrap items-end justify-between gap-4 mb-5 animate-pulse">
        <div>
          <Skeleton className="h-9 w-40" />
          <Skeleton className="h-4 w-56 mt-1.5" />
        </div>
        <div className="flex items-center gap-3">
          <Skeleton className="h-5 w-28" />
          <Skeleton className="h-5 w-32" />
          <Skeleton className="h-10 w-36 rounded-xl" variant="rounded-sm" />
        </div>
      </header>

      <div className="space-y-5">
        <BudgetsSkeleton />
      </div>
    </AppLayout>
  );
}
