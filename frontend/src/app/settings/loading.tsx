import { AppLayout } from '@/components/app-layout';
import { SettingsSkeleton } from '@/components/skeletons/settings-skeleton';
import { Skeleton } from '@/components/ui/skeleton';

export default function SettingsLoading() {
  return (
    <AppLayout>
      {/* Header shimmer */}
      <div className="mb-6 lg:mb-8 animate-pulse">
        <Skeleton className="h-9 w-40" />
        <Skeleton className="h-4 w-56 mt-1.5" />
      </div>

      <SettingsSkeleton />
    </AppLayout>
  );
}
