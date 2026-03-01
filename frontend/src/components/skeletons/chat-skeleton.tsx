import { Skeleton } from '@/components/ui/skeleton';

export function ChatSkeleton() {
  return (
    <>
      {/* Header bar shimmer */}
      <div className="flex items-center justify-between px-4 py-2 border-b border-slate-200 bg-white/90 backdrop-blur-xs animate-pulse">
        <div className="flex items-center gap-2">
          <Skeleton className="w-5 h-5 rounded" variant="rounded-sm" />
          <Skeleton className="h-4 w-24" />
        </div>
        <Skeleton className="h-8 w-28 rounded-lg" variant="rounded-sm" />
      </div>

      {/* Messages area shimmer */}
      <div className="flex-1 overflow-hidden px-4 py-4 space-y-4 animate-pulse">
        {/* Assistant message */}
        <div className="flex justify-start">
          <div className="max-w-[75%] rounded-2xl border border-slate-200 bg-white px-4 py-3 space-y-2">
            <Skeleton className="h-3 w-48" />
            <Skeleton className="h-3 w-64" />
            <Skeleton className="h-3 w-36" />
          </div>
        </div>

        {/* User message */}
        <div className="flex justify-end">
          <div className="max-w-[75%] rounded-2xl bg-violet-600/20 px-4 py-3 space-y-2">
            <Skeleton className="h-3 w-40" />
          </div>
        </div>

        {/* Assistant message */}
        <div className="flex justify-start">
          <div className="max-w-[75%] rounded-2xl border border-slate-200 bg-white px-4 py-3 space-y-2">
            <Skeleton className="h-3 w-56" />
            <Skeleton className="h-3 w-44" />
          </div>
        </div>
      </div>

      {/* Input area shimmer */}
      <div className="border-t border-slate-200 bg-white px-4 py-3 animate-pulse">
        <div className="flex items-end gap-2">
          <Skeleton className="flex-1 h-10 rounded-xl" variant="rounded-sm" />
          <Skeleton className="shrink-0 w-10 h-10 rounded-xl" variant="rounded-sm" />
        </div>
      </div>
    </>
  );
}
