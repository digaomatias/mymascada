import { AppLayout } from '@/components/app-layout';
import { SkeletonCard, SkeletonPanel } from '@/components/skeletons';
import { Skeleton } from '@/components/ui/skeleton';

export default function DashboardLoading() {
  return (
    <AppLayout mainClassName="relative z-10 flex-1 w-full px-4 py-6 sm:px-5 lg:px-6 lg:py-8">
      {/* Header shimmer */}
      <header className="flex flex-wrap items-end justify-between gap-4 mb-5 animate-pulse">
        <div>
          <Skeleton className="h-9 w-64" />
          <Skeleton className="h-4 w-40 mt-1.5" />
        </div>
      </header>

      {/* Card grid shimmer */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <SkeletonCard className="h-48 p-6" />
        <SkeletonCard className="h-48 p-6" />
        <SkeletonCard className="h-48 p-6" />
      </div>

      <SkeletonPanel className="mt-4" height="h-64" />
    </AppLayout>
  );
}
