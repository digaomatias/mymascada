import { SkeletonSettingsCard } from './skeleton-primitives';

export function SettingsSkeleton() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
      {Array.from({ length: 6 }).map((_, i) => (
        <SkeletonSettingsCard key={i} />
      ))}
    </div>
  );
}
