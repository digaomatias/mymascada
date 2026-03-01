import { Skeleton } from '@/components/ui/skeleton';

export function GoalsSkeleton() {
  return (
    <div className="space-y-3">
      {[1, 2, 3].map((i) => (
        <div
          key={i}
          className="rounded-[26px] border border-violet-100/80 bg-white/80 p-5 animate-pulse"
        >
          <div className="flex items-start gap-3">
            <Skeleton className="h-10 w-10 rounded-xl" variant="rounded-sm" />
            <div className="flex-1 space-y-2">
              <div className="flex items-center gap-2">
                <Skeleton className="h-5 w-1/3" />
                <Skeleton className="h-5 w-16 rounded-full" />
              </div>
            </div>
          </div>
          <div className="mt-4 flex justify-between">
            <Skeleton className="h-4 w-1/4" />
            <Skeleton className="h-4 w-1/5" />
          </div>
          <Skeleton className="mt-3 h-2.5 w-full rounded-full" />
          <div className="mt-3 flex justify-between">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="h-3 w-20" />
          </div>
        </div>
      ))}
    </div>
  );
}
