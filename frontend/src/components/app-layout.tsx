'use client';

import Navigation from '@/components/navigation';
import { DashboardBackground } from '@/components/dashboard/dashboard-background';

interface AppLayoutProps {
  children: React.ReactNode;
  /** Extra classes applied to the <main> element */
  mainClassName?: string;
  /** If true, skip the atmospheric background (e.g. for pages with custom backgrounds) */
  noBackground?: boolean;
}

export function AppLayout({ children, mainClassName, noBackground }: AppLayoutProps) {
  return (
    <div className="flex min-h-dvh bg-[#faf8ff]">
      <Navigation />

      {/* Main content area — offset by sidebar width on lg+ */}
      <div className="flex-1 flex flex-col min-w-0 lg:ml-[260px]">
        {!noBackground && <DashboardBackground />}

        <main
          className={
            mainClassName ??
            'relative z-10 flex-1 mx-auto w-full max-w-7xl px-4 py-4 sm:px-6 sm:py-6 lg:px-8 lg:py-8'
          }
        >
          {children}
        </main>
      </div>
    </div>
  );
}
