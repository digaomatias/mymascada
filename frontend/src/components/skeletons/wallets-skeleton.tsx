import { Skeleton } from '@/components/ui/skeleton';

export function WalletsSkeleton() {
  return (
    <div className="space-y-5">
      {/* Hero skeleton */}
      <div className="rounded-[26px] border border-violet-100/80 bg-white/80 p-6 animate-pulse">
        <Skeleton className="h-4 w-24" />
        <Skeleton className="mt-2 h-8 w-40" />
      </div>
      {/* Card grid skeleton */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {[1, 2, 3].map((i) => (
          <div
            key={i}
            className="rounded-[26px] border border-violet-100/80 bg-white/80 p-5 animate-pulse"
          >
            <div className="flex items-center gap-3">
              <Skeleton className="h-10 w-10 rounded-full" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-5 w-2/3" />
                <Skeleton className="h-3 w-1/3" />
              </div>
            </div>
            <div className="mt-4 flex justify-between">
              <Skeleton className="h-6 w-24" />
              <Skeleton className="h-4 w-20" />
            </div>
            <Skeleton className="mt-3 h-1.5 w-full rounded-full" />
          </div>
        ))}
      </div>
    </div>
  );
}
