import { Skeleton } from '@/components/ui/skeleton';
import { SkeletonFilterBar } from './skeleton-primitives';

export function TransactionsSkeleton() {
  return (
    <div className="space-y-4">
      <SkeletonFilterBar />

      {/* Transaction list card */}
      <div className="rounded-[26px] border border-violet-100/80 bg-white/90 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)]">
        <div className="p-0">
          <div className="divide-y divide-slate-100">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="flex items-center gap-3 p-4 animate-pulse">
                <Skeleton className="w-12 h-12 rounded-xl" variant="rounded-sm" />
                <div className="flex-1 space-y-2">
                  <Skeleton className="h-4 w-2/5" />
                  <div className="flex items-center gap-3">
                    <Skeleton className="h-3 w-20" />
                    <Skeleton className="h-3 w-16" />
                    <Skeleton className="h-3 w-24" />
                  </div>
                </div>
                <Skeleton className="h-5 w-20" />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
