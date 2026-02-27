'use client';

import { useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { RuleSuggestionsView } from '@/components/rules/rule-suggestions-view';
import { SparklesIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

export default function RuleSuggestionsPage() {
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
      <AppLayout>
        <div className="min-h-[50vh] flex items-center justify-center">
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
              <SparklesIcon className="w-8 h-8 text-white" />
            </div>
            <div className="mt-6 text-slate-700 font-medium">{t('suggestions.title')}</div>
          </div>
        </div>
      </AppLayout>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  return (
    <AppLayout>
      <div className="mb-5">
        <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
          {t('suggestions.title')}
        </h1>
        <p className="text-[15px] text-slate-500 mt-1.5">
          {t('suggestions.subtitle')}
        </p>
      </div>

      <RuleSuggestionsView />
    </AppLayout>
  );
}
