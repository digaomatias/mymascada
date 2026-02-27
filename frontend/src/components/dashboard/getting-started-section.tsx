'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { apiClient } from '@/lib/api-client';
import {
  BuildingLibraryIcon,
  ArrowUpTrayIcon,
  TagIcon,
  ArrowRightIcon,
} from '@heroicons/react/24/outline';

export function GettingStartedSection() {
  const t = useTranslations('dashboard.gettingStarted');
  const [hasAccounts, setHasAccounts] = useState<boolean | null>(null);

  useEffect(() => {
    apiClient
      .getAccounts()
      .then((data: unknown) => {
        const accounts = data as { id: number }[];
        setHasAccounts(Array.isArray(accounts) && accounts.length > 0);
      })
      .catch(() => {
        // Silently hide the section on error — don't block the dashboard
        setHasAccounts(true);
      });
  }, []);

  // Don't render until we know the account state, or if user already has accounts
  if (hasAccounts !== false) {
    return null;
  }

  const steps = [
    {
      number: 1,
      icon: BuildingLibraryIcon,
      title: t('step1Title'),
      desc: t('step1Desc'),
      cta: t('step1Cta'),
      href: '/accounts/new',
      gradient: 'from-violet-500 to-violet-600',
      ring: 'ring-violet-100',
    },
    {
      number: 2,
      icon: ArrowUpTrayIcon,
      title: t('step2Title'),
      desc: t('step2Desc'),
      cta: t('step2Cta'),
      href: '/import',
      gradient: 'from-blue-500 to-blue-600',
      ring: 'ring-blue-100',
    },
    {
      number: 3,
      icon: TagIcon,
      title: t('step3Title'),
      desc: t('step3Desc'),
      cta: t('step3Cta'),
      href: '/categories',
      gradient: 'from-fuchsia-500 to-fuchsia-600',
      ring: 'ring-fuchsia-100',
    },
  ];

  return (
    <section className="rounded-3xl border border-violet-100/60 bg-gradient-to-br from-violet-50/80 to-fuchsia-50/60 p-6 shadow-sm sm:p-8">
      {/* Heading */}
      <div className="mb-6">
        <h2 className="text-xl font-bold text-slate-800 sm:text-2xl">{t('title')}</h2>
        <p className="mt-1 text-sm text-slate-500">{t('subtitle')}</p>
      </div>

      {/* Steps */}
      <div className="grid gap-4 sm:grid-cols-3">
        {steps.map((step) => {
          const Icon = step.icon;
          return (
            <Link
              key={step.number}
              href={step.href}
              className={`group flex flex-col gap-3 rounded-2xl border border-white/80 bg-white/70 p-5 shadow-sm ring-2 ${step.ring} transition-all duration-200 hover:bg-white hover:shadow-md hover:-translate-y-0.5`}
            >
              {/* Icon + number */}
              <div className="flex items-center gap-3">
                <div
                  className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br ${step.gradient} shadow`}
                >
                  <Icon className="h-5 w-5 text-white" />
                </div>
                <span className="text-xs font-semibold uppercase tracking-widest text-slate-400">
                  Step {step.number}
                </span>
              </div>

              {/* Text */}
              <div className="flex-1">
                <p className="font-semibold text-slate-800">{step.title}</p>
                <p className="mt-1 text-xs text-slate-500 leading-relaxed">{step.desc}</p>
              </div>

              {/* CTA */}
              <div className="flex items-center gap-1 text-sm font-medium text-violet-600 group-hover:text-violet-700 transition-colors">
                {step.cta}
                <ArrowRightIcon className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
              </div>
            </Link>
          );
        })}
      </div>
    </section>
  );
}
