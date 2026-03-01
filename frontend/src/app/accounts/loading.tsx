import { AppLayout } from '@/components/app-layout';
import { AccountsSkeleton } from '@/components/skeletons';
import { Skeleton } from '@/components/ui/skeleton';

export default function AccountsLoading() {
  return (
    <AppLayout>
      {/* Header shimmer */}
      <header className="flex flex-wrap items-end justify-between gap-4 mb-5 animate-pulse">
        <div>
          <Skeleton className="h-9 w-48" />
          <Skeleton className="h-4 w-64 mt-1.5" />
        </div>
        <Skeleton className="h-10 w-32 rounded-xl" variant="rounded-sm" />
      </header>

      <AccountsSkeleton />
    </AppLayout>
  );
}
