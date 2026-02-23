'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useState, Suspense } from 'react';
import { toast } from 'sonner';
import { AppLayout } from '@/components/app-layout';
import { apiClient } from '@/lib/api-client';
import { DashboardProvider } from '@/contexts/dashboard-context';
import { DashboardHeader } from '@/components/dashboard/dashboard-header';
import { DashboardTemplateRenderer } from '@/components/dashboard/dashboard-template-renderer';
import { useTranslations } from 'next-intl';
import { CreditCardIcon } from '@heroicons/react/24/outline';

function DashboardContent() {
  const { isAuthenticated, isLoading, user, loginWithToken } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [ready, setReady] = useState(false);

  // Handle Google OAuth code from URL
  useEffect(() => {
    const code = searchParams.get('code');
    if (code && !isAuthenticated) {
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
  }, [searchParams, isAuthenticated, loginWithToken, router, tToasts]);

  // Redirect unauthenticated users
  useEffect(() => {
    if (!isLoading && !isAuthenticated && !searchParams.get('code')) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router, searchParams]);

  // Onboarding redirect
  const isOnboardingComplete =
    (user as Record<string, unknown> | null)?.isOnboardingComplete ?? true;

  useEffect(() => {
    if (isAuthenticated && !isLoading && !isOnboardingComplete) {
      router.push('/onboarding');
    }
  }, [isAuthenticated, isLoading, isOnboardingComplete, router]);

  // Mark ready once authenticated
  useEffect(() => {
    if (isAuthenticated && !isLoading) {
      setReady(true);
    }
  }, [isAuthenticated, isLoading]);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <CreditCardIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-600 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !ready) {
    return null;
  }

  return (
    <DashboardProvider>
      <AppLayout mainClassName="relative z-10 flex-1 mx-auto w-full max-w-[1440px] px-4 py-6 sm:px-5 lg:px-6 lg:py-8">
        <DashboardHeader />
        <DashboardTemplateRenderer />
      </AppLayout>
    </DashboardProvider>
  );
}

export default function DashboardPage() {
  const tCommon = useTranslations('common');
  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
              <CreditCardIcon className="w-8 h-8 text-white" />
            </div>
            <div className="mt-6 text-slate-600 font-medium">{tCommon('loading')}</div>
          </div>
        </div>
      }
    >
      <DashboardContent />
    </Suspense>
  );
}

export const dynamic = 'force-dynamic';
