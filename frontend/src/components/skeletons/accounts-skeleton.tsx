import { Skeleton } from '@/components/ui/skeleton';
import { SkeletonHero } from './skeleton-primitives';

export function AccountsSkeleton() {
  return (
    <div className="space-y-5">
      {/* Hero net worth section shimmer */}
      <SkeletonHero />

      {/* Account cards shimmer */}
      <div className="space-y-3">
        {[1, 2, 3].map((i) => (
          <div
            key={i}
            className="rounded-[26px] border border-violet-100/80 bg-white/80 p-5 animate-pulse"
          >
            <div className="flex items-center gap-3">
              <Skeleton className="h-11 w-11 rounded-xl" variant="rounded-sm" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-5 w-1/3" />
                <Skeleton className="h-3 w-1/5" />
              </div>
              <div className="space-y-2 text-right">
                <Skeleton className="h-6 w-24" />
                <Skeleton className="h-3 w-12 ml-auto" />
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
