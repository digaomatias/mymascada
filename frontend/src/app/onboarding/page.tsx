'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';
import { OnboardingWizard } from '@/components/onboarding/onboarding-wizard';

export default function OnboardingPage() {
  const { user, isLoading, isAuthenticated } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.replace('/login');
    }
  }, [isLoading, isAuthenticated, router]);

  useEffect(() => {
    if (!isLoading && user?.isOnboardingComplete) {
      router.replace('/dashboard');
    }
  }, [isLoading, user, router]);

  if (isLoading || !isAuthenticated) {
    return null;
  }

  if (user?.isOnboardingComplete) {
    return null;
  }

  return <OnboardingWizard />;
}
