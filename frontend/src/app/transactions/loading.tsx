import { AppLayout } from '@/components/app-layout';
import { TransactionsSkeleton } from '@/components/skeletons';
import { Skeleton } from '@/components/ui/skeleton';

export default function TransactionsLoading() {
  return (
    <AppLayout>
      {/* Header shimmer */}
      <div className="mb-5 animate-pulse">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <Skeleton className="h-9 w-48" />
            <Skeleton className="h-4 w-32 mt-1.5" />
          </div>
          <div className="flex gap-2">
            <Skeleton className="h-9 w-24 rounded-lg" variant="rounded-sm" />
            <Skeleton className="h-9 w-24 rounded-lg" variant="rounded-sm" />
          </div>
        </div>
      </div>

      <TransactionsSkeleton />
    </AppLayout>
  );
}
