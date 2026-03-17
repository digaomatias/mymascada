'use client';

import { useAuth } from '@/contexts/auth-context';
import { useTranslations } from 'next-intl';
import { Skeleton } from '@/components/ui/skeleton';

export function DashboardHeader() {
  const { user, isLoading } = useAuth();
  const t = useTranslations('dashboard');

  const firstName = user?.firstName?.trim();
  const userName = user?.userName?.trim();
  const name = firstName || userName;

  return (
    <header className="flex flex-wrap items-end justify-between gap-4 mb-5">
      <div>
        <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
          {isLoading && !name ? (
            <Skeleton className="h-9 w-64 inline-block" />
          ) : name ? (
            t('welcomeBack', { name })
          ) : (
            t('welcomeBackGeneric')
          )}
        </h1>
        <p className="mt-1.5 text-[15px] text-slate-500">
          {t('overview')}
        </p>
      </div>
    </header>
  );
}
