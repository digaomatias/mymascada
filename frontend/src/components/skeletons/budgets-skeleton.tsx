import { Skeleton } from '@/components/ui/skeleton';

export function BudgetsSkeleton() {
  return (
    <div className="grid gap-4 md:grid-cols-2">
      {[1, 2, 3, 4].map((i) => (
        <div
          key={i}
          className="h-56 rounded-[24px] border border-violet-100/80 bg-white/80 p-5 animate-pulse"
        >
          <div className="flex items-start justify-between gap-3">
            <Skeleton className="h-6 w-2/5" />
            <Skeleton className="h-6 w-16 rounded-full" />
          </div>
          <div className="mt-6 space-y-2">
            <div className="flex justify-between">
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="h-4 w-16" />
            </div>
            <Skeleton className="h-2.5 w-full rounded-full" />
          </div>
          <div className="mt-5 flex items-center gap-3">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="h-3 w-20" />
          </div>
        </div>
      ))}
    </div>
  );
}
