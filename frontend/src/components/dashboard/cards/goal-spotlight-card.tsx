'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient, GoalSummary } from '@/lib/api-client';
import { formatCurrency, cn } from '@/lib/utils';
import {
  ShieldCheckIcon,
  CheckCircleIcon,
  ArrowRightIcon,
  PlusIcon,
  FlagIcon,
} from '@heroicons/react/24/outline';

function GoalRing({ value, label, size = 148 }: { value: number; label: string; size?: number }) {
  const r = (size - 24) / 2;
  const circ = 2 * Math.PI * r;
  const offset = circ - (Math.min(value, 100) / 100) * circ;
  return (
    <div className="relative" style={{ width: size, height: size }}>
      <svg viewBox={`0 0 ${size} ${size}`} className="h-full w-full" style={{ transform: 'rotate(-90deg)' }}>
        <defs>
          <linearGradient id="goal-ring-grad" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#7c3aed" />
            <stop offset="50%" stopColor="#a78bfa" />
            <stop offset="100%" stopColor="#d946ef" />
          </linearGradient>
        </defs>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="#f3f0ff" strokeWidth="12" />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          stroke="url(#goal-ring-grad)"
          strokeWidth="12"
          strokeLinecap="round"
          strokeDasharray={circ}
          strokeDashoffset={offset}
          className="transition-all duration-1000 ease-out"
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
          {Math.round(value)}%
        </span>
        <span className="text-[10px] font-semibold uppercase tracking-[0.12em] text-violet-500">
          {label}
        </span>
      </div>
    </div>
  );
}

export function GoalSpotlightCard() {
  const t = useTranslations('dashboard.cards.goal');
  const [goals, setGoals] = useState<GoalSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await apiClient.getGoals({ includeCompleted: false });
        setGoals(data);
      } catch (err) {
        console.error('Failed to load goals:', err);
        setError('Failed to load goals');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const primary = goals.length > 0
    ? [...goals].sort((a, b) => b.progressPercentage - a.progressPercentage)[0]
    : null;

  return (
    <DashboardCard cardId="goal-spotlight" loading={loading} error={error}>
      {!primary ? (
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-violet-400 to-fuchsia-400 shadow-lg mb-4">
            <FlagIcon className="h-7 w-7 text-white" />
          </div>
          <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900 mb-2">
            {t('noGoals')}
          </h3>
          <p className="text-sm text-slate-500 mb-4">{t('noGoalsDesc')}</p>
          <Link
            href="/goals/new"
            className="inline-flex items-center gap-1 rounded-lg bg-violet-600 px-4 py-2 text-sm font-semibold text-white hover:bg-violet-700"
          >
            <PlusIcon className="h-4 w-4" />
            {t('createGoal')}
          </Link>
        </div>
      ) : (
        <div className="relative flex flex-1 flex-col">
          <div className="pointer-events-none absolute -bottom-10 -right-10 h-32 w-32 rounded-full bg-fuchsia-100/40 blur-2xl" aria-hidden />

          <div className="relative flex flex-1 flex-col">
            <div className="inline-flex w-fit items-center gap-1.5 rounded-full border border-violet-200/60 bg-violet-50 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-violet-600">
              <ShieldCheckIcon className="h-3 w-3" />
              {t('safetyNet')}
            </div>
            <h3 className="mt-2.5 font-[var(--font-dash-sans)] text-xl font-semibold tracking-[-0.02em] text-slate-900">
              {primary.name}
            </h3>
            <p className="mt-1 text-sm text-slate-500">
              <span className="font-[var(--font-dash-mono)] font-semibold text-slate-700">
                {formatCurrency(primary.currentAmount)}
              </span>
              {' / '}
              <span className="font-[var(--font-dash-mono)] font-semibold text-slate-700">
                {formatCurrency(primary.targetAmount)}
              </span>
            </p>

            <div className="my-auto flex justify-center py-4">
              <GoalRing value={primary.progressPercentage} label={t('funded')} />
            </div>

            {/* Progress context */}
            <div className="flex items-center justify-between rounded-xl border border-violet-100/60 bg-violet-50/30 px-3 py-2.5">
              <p className="text-xs text-slate-600">
                <strong className="text-slate-800">
                  {primary.progressPercentage.toFixed(0)}%
                </strong>{' '}
                {t('funded')}
              </p>
              <Link
                href={`/goals/${primary.id}`}
                className="inline-flex items-center gap-1 text-xs font-semibold text-violet-600 hover:text-violet-800"
              >
                {t('details')}
              </Link>
            </div>

            <div className="mt-3 flex items-center justify-between">
              {primary.deadline && primary.daysRemaining !== undefined && primary.daysRemaining > 0 && (
                <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">
                  <CheckCircleIcon className="h-3.5 w-3.5" />
                  {t('onTrack')}
                </span>
              )}
              <Link
                href={`/goals/${primary.id}`}
                className={cn(
                  'inline-flex items-center gap-1 rounded-lg bg-violet-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-violet-700',
                  !primary.deadline || !primary.daysRemaining ? 'ml-auto' : '',
                )}
              >
                {t('viewGoal')} <ArrowRightIcon className="h-3 w-3" />
              </Link>
            </div>
          </div>
        </div>
      )}
    </DashboardCard>
  );
}
