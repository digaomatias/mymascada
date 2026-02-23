'use client';

import { useAuth } from '@/contexts/auth-context';
import { useTranslations } from 'next-intl';

export function DashboardHeader() {
  const { user } = useAuth();
  const t = useTranslations('dashboard');

  return (
    <header className="flex flex-wrap items-end justify-between gap-4 mb-5">
      <div>
        <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
          {t('welcomeBack', { name: user?.firstName || user?.userName || '' })}
        </h1>
        <p className="mt-1.5 text-[15px] text-slate-500">
          {t('overview')}
        </p>
      </div>
    </header>
  );
}
