import { SkeletonStatCards, SkeletonPanel } from './skeleton-primitives';

export function AnalyticsSkeleton() {
  return (
    <>
      {/* Insight banner skeleton */}
      <div className="mb-6 h-20 rounded-[24px] border border-violet-100/80 bg-white/85 animate-pulse" />

      {/* Stat cards skeleton */}
      <div className="mb-6">
        <SkeletonStatCards count={4} />
      </div>

      {/* Chart panels skeleton — matches xl:grid-cols-3 layout */}
      <div className="grid gap-6 xl:grid-cols-3">
        <SkeletonPanel className="xl:col-span-2" />
        <SkeletonPanel />
        <SkeletonPanel className="xl:col-span-2" />
        <SkeletonPanel />
      </div>
    </>
  );
}
