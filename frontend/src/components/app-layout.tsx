'use client';

import Navigation from '@/components/navigation';
import { DashboardBackground } from '@/components/dashboard/dashboard-background';
import { NotificationBell } from '@/components/notifications/notification-bell';

interface AppLayoutProps {
  children: React.ReactNode;
  /** Extra classes applied to the <main> element */
  mainClassName?: string;
  /** If true, skip the atmospheric background (e.g. for pages with custom backgrounds) */
  noBackground?: boolean;
}

export function AppLayout({ children, mainClassName, noBackground }: AppLayoutProps) {
  return (
    <div className="flex flex-col lg:flex-row min-h-dvh bg-surface-alt">
      <Navigation />

      {/* Main content area — offset by sidebar width on lg+ */}
      <div className="flex-1 flex flex-col min-w-0 pb-20 lg:pb-0 lg:ml-[260px]">
        {!noBackground && <DashboardBackground />}

        {/* Desktop notification bell — floats top-right */}
        <div className="hidden lg:flex justify-end relative z-20 px-8 pt-6 -mb-6">
          <NotificationBell />
        </div>

        <main
          className={
            mainClassName ??
            'relative z-10 flex-1 w-full px-4 py-4 sm:px-6 sm:py-6 lg:px-8 lg:py-8'
          }
        >
          {children}
        </main>
      </div>
    </div>
  );
}
