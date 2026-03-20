'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, Suspense } from 'react';
import { toast } from 'sonner';
import { AppLayout } from '@/components/app-layout';
import { apiClient } from '@/lib/api-client';
import { DashboardProvider } from '@/contexts/dashboard-context';
import { DashboardHeader } from '@/components/dashboard/dashboard-header';
import { DashboardTemplateRenderer } from '@/components/dashboard/dashboard-template-renderer';
import { useTranslations } from 'next-intl';
import { useAuthGuard } from '@/hooks/use-auth-guard';
import { SkeletonCard, SkeletonPanel } from '@/components/skeletons';
import { Skeleton } from '@/components/ui/skeleton';

function DashboardContent() {
  const { user, loginWithToken } = useAuth();
  const { shouldRender, isAuthResolved } = useAuthGuard();
  const router = useRouter();
  const searchParams = useSearchParams();
  const tToasts = useTranslations('toasts');

  // Handle Google OAuth code from URL
  useEffect(() => {
    const code = searchParams.get('code');
    if (code && !isAuthResolved) {
      apiClient
        .exchangeCode(code)
        .then((result) => loginWithToken(result.token))
        .then((success) => {
          if (success) {
            toast.success(tToasts('signedIn'));
            router.replace('/dashboard');
          } else {
            toast.error(tToasts('error.generic'));
            router.push('/auth/login');
          }
        })
        .catch(() => {
          toast.error(tToasts('error.generic'));
          router.push('/auth/login');
        });
    }
  }, [searchParams, isAuthResolved, loginWithToken, router, tToasts]);

  // Onboarding redirect
  const isOnboardingComplete =
    (user as Record<string, unknown> | null)?.isOnboardingComplete ?? true;

  useEffect(() => {
    if (isAuthResolved && !isOnboardingComplete) {
      router.push('/onboarding');
    }
  }, [isAuthResolved, isOnboardingComplete, router]);

  if (!shouldRender) return null;

  return (
    <DashboardProvider>
      <AppLayout mainClassName="relative z-10 flex-1 w-full px-4 py-6 sm:px-5 lg:px-6 lg:py-8">
        <DashboardHeader />
        {isAuthResolved ? (
          <DashboardTemplateRenderer />
        ) : (
          <DashboardBodySkeleton className="space-y-4" />
        )}
      </AppLayout>
    </DashboardProvider>
  );
}

function DashboardBodySkeleton({ className }: { className?: string }) {
  return (
    <div className={className}>
      <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div
            key={i}
            style={{
              animation: 'fadeInUp 500ms cubic-bezier(0.16, 1, 0.3, 1) both',
              animationDelay: `${i * 60}ms`,
            }}
          >
            <SkeletonCard className="h-48 p-6">
              <div className="space-y-4">
                <Skeleton className="h-4 w-24 rounded" />
                <Skeleton className="h-8 w-32 rounded" />
                <Skeleton className="h-3 w-full rounded" />
                <Skeleton className="h-3 w-3/4 rounded" />
              </div>
            </SkeletonCard>
          </div>
        ))}
      </div>
      <div
        style={{
          animation: 'fadeInUp 500ms 200ms cubic-bezier(0.16, 1, 0.3, 1) both',
        }}
      >
        <SkeletonPanel className="mt-5" height="h-64">
          <div className="p-6 space-y-4">
            <Skeleton className="h-5 w-40 rounded" />
            <Skeleton className="h-40 w-full rounded-xl" />
          </div>
        </SkeletonPanel>
      </div>
    </div>
  );
}

function DashboardSuspenseFallback() {
  return (
    <AppLayout mainClassName="relative z-10 flex-1 w-full px-4 py-6 sm:px-5 lg:px-6 lg:py-8">
      <header className="flex flex-wrap items-end justify-between gap-4 mb-6">
        <div>
          <Skeleton className="h-9 w-64 rounded" />
          <Skeleton className="h-4 w-40 mt-2 rounded" />
        </div>
      </header>
      <DashboardBodySkeleton />
    </AppLayout>
  );
}

export default function DashboardPage() {
  return (
    <Suspense fallback={<DashboardSuspenseFallback />}>
      <DashboardContent />
    </Suspense>
  );
}

export const dynamic = 'force-dynamic';
