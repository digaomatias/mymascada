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
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <SkeletonCard className="h-48 p-6" />
              <SkeletonCard className="h-48 p-6" />
              <SkeletonCard className="h-48 p-6" />
            </div>
            <SkeletonPanel height="h-64" />
          </div>
        )}
      </AppLayout>
    </DashboardProvider>
  );
}

function DashboardSuspenseFallback() {
  return (
    <AppLayout mainClassName="relative z-10 flex-1 w-full px-4 py-6 sm:px-5 lg:px-6 lg:py-8">
      <header className="flex flex-wrap items-end justify-between gap-4 mb-5 animate-pulse">
        <div>
          <Skeleton className="h-9 w-64" />
          <Skeleton className="h-4 w-40 mt-1.5" />
        </div>
      </header>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <SkeletonCard className="h-48 p-6" />
        <SkeletonCard className="h-48 p-6" />
        <SkeletonCard className="h-48 p-6" />
      </div>
      <SkeletonPanel className="mt-4" height="h-64" />
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
