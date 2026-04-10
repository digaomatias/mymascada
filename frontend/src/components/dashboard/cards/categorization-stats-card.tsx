'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import {
  SparklesIcon,
  BoltIcon,
  TagIcon,
  LightBulbIcon,
  ArrowRightIcon,
} from '@heroicons/react/24/outline';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient } from '@/lib/api-client';
import type { CategorizationStatsResponse } from '@/lib/api-client';

/**
 * Dashboard card that surfaces the categorization pipeline's performance:
 *   - How many transactions were auto-categorized this month (and by which handler)
 *   - How many transactions still need review
 *   - How many rule suggestions are waiting for the user
 *
 * Links straight into the relevant remediation flow so the user can act.
 */
export function CategorizationStatsCard() {
  const t = useTranslations('dashboard.cards.categorization');
  const [stats, setStats] = useState<CategorizationStatsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await apiClient.getCategorizationStats();
        if (!cancelled) setStats(data);
      } catch (err) {
        console.error('Failed to load categorization stats:', err);
        if (!cancelled) setError(t('loadError'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [t]);

  return (
    <DashboardCard cardId="categorization-stats" loading={loading} error={error}>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary-100">
            <SparklesIcon className="h-4 w-4 text-primary-600" />
          </div>
          <div>
            <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold tracking-[-0.02em] text-ink-900">
              {t('title')}
            </h3>
            <p className="text-xs text-ink-500">{t('subtitle')}</p>
          </div>
        </div>
      </div>

      {stats && (
        <div className="mt-5 space-y-3">
          {/* Auto-categorized headline */}
          <div className="rounded-xl border border-primary-100 bg-primary-50/60 p-3.5">
            <div className="flex items-start gap-2">
              <BoltIcon className="mt-0.5 h-4 w-4 shrink-0 text-primary-500" />
              <p
                className="text-sm font-semibold text-ink-800"
                data-testid="categorization-stats-auto"
              >
                {t('autoThisMonth', { count: stats.autoCategorizedThisMonth })}
              </p>
            </div>
            {stats.autoCategorizedThisMonth > 0 && (
              <p className="mt-1 ml-6 text-xs text-ink-500">
                {stats.rulesPercentage}% {t('byRules')}
                {' · '}
                {stats.mlPercentage}% {t('byMl')}
                {stats.llmPercentage > 0 ? ` · ${stats.llmPercentage}% ${t('byLlm')}` : ''}
              </p>
            )}
          </div>

          {/* Needs review */}
          {stats.needsReview > 0 ? (
            <Link
              href="/transactions/quick-categorize"
              className="group flex w-full items-start gap-3 rounded-xl border border-amber-200 bg-amber-50/40 p-3.5 text-left transition-all hover:-translate-y-0.5 hover:shadow-sm"
              data-testid="categorization-stats-needs-review"
            >
              <TagIcon className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-ink-800">
                  {t('needsReview', { count: stats.needsReview })}
                </p>
                <p className="mt-0.5 text-xs text-amber-700">
                  {t('quickCategorize')}
                </p>
              </div>
              <ArrowRightIcon className="mt-1 h-4 w-4 shrink-0 text-amber-600 transition-transform group-hover:translate-x-0.5" />
            </Link>
          ) : null}

          {/* Pending suggestions */}
          {stats.pendingSuggestions > 0 ? (
            <Link
              href="/rules/suggestions"
              className="group flex w-full items-start gap-3 rounded-xl border border-emerald-200 bg-emerald-50/40 p-3.5 text-left transition-all hover:-translate-y-0.5 hover:shadow-sm"
              data-testid="categorization-stats-pending-suggestions"
            >
              <LightBulbIcon className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-ink-800">
                  {t('pendingSuggestions', { count: stats.pendingSuggestions })}
                </p>
                <p className="mt-0.5 text-xs text-emerald-700">{t('viewSuggestions')}</p>
              </div>
              <ArrowRightIcon className="mt-1 h-4 w-4 shrink-0 text-emerald-600 transition-transform group-hover:translate-x-0.5" />
            </Link>
          ) : null}

          {stats.needsReview === 0 && stats.pendingSuggestions === 0 && (
            <p className="py-2 text-center text-xs text-ink-400">{t('allCaughtUp')}</p>
          )}
        </div>
      )}
    </DashboardCard>
  );
}
