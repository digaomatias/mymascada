'use client';

import { useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { RuleBuilderWizard } from '@/components/rules/rule-builder-wizard';
import { AdjustmentsHorizontalIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

export default function NewRulePage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('rules');

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
      return;
    }
  }, [isAuthenticated, isLoading, router]);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <AdjustmentsHorizontalIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-700 font-medium">{t('new.title')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  return (
    <AppLayout>
      <div className="max-w-4xl mx-auto">
        <div className="mb-5">
          <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
            {t('new.title')}
          </h1>
          <p className="text-[15px] text-slate-500 mt-1.5">
            {t('new.subtitle')}
          </p>
        </div>

        <RuleBuilderWizard />
      </div>
    </AppLayout>
  );
}
