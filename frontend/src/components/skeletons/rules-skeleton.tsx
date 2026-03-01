import { Skeleton } from '@/components/ui/skeleton';

export function RulesSkeleton() {
  return (
    <div className="space-y-5">
      {/* Stats cards skeleton */}
      <div className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs animate-pulse">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-5">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="space-y-2">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-8 w-16" />
            </div>
          ))}
        </div>
      </div>

      {/* Filter bar skeleton */}
      <div className="rounded-[20px] border border-violet-100/60 bg-white/90 p-4 shadow-sm shadow-violet-200/20 backdrop-blur-xs animate-pulse">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <Skeleton className="h-10 rounded-xl" variant="rounded-sm" />
          <Skeleton className="h-10 rounded-xl" variant="rounded-sm" />
          <Skeleton className="h-10 rounded-xl" variant="rounded-sm" />
        </div>
      </div>

      {/* Rules list skeleton */}
      <div className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
        <div className="p-0">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="border-b border-slate-100 last:border-b-0 p-5 animate-pulse">
              <div className="flex items-start gap-3">
                <Skeleton className="w-4 h-4 mt-1 rounded" variant="rounded-sm" />
                <div className="flex-1 space-y-2">
                  <div className="flex items-center gap-2">
                    <Skeleton className="h-5 w-40" />
                    <Skeleton className="h-4 w-16 rounded-full" />
                  </div>
                  <div className="flex items-center gap-4">
                    <Skeleton className="h-3 w-32" />
                    <Skeleton className="h-3 w-24" />
                    <Skeleton className="h-3 w-16" />
                  </div>
                  <div className="flex items-center gap-4">
                    <Skeleton className="h-3 w-20" />
                    <Skeleton className="h-3 w-28" />
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
