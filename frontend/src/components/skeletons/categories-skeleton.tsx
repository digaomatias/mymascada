import { Skeleton } from '@/components/ui/skeleton';

export function CategoriesSkeleton() {
  return (
    <div className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
      <div className="p-6 space-y-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 animate-pulse">
            <Skeleton className="w-3 h-3 rounded-full" variant="rounded-sm" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="h-3 w-1/5" />
            </div>
            <Skeleton className="h-5 w-20" />
          </div>
        ))}
      </div>
    </div>
  );
}
