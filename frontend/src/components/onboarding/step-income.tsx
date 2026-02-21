'use client';

import { useTranslations } from 'next-intl';
import { ArrowDownTrayIcon, ArrowTrendingUpIcon } from '@heroicons/react/24/outline';
import { CurrencyInput } from '@/components/ui/currency-input';
import { Button } from '@/components/ui/button';

interface StepIncomeProps {
  value: number;
  onChange: (value: number) => void;
  currency: string;
  onNext: () => void;
  onBack: () => void;
}

export function StepIncome({ value, onChange, currency, onNext, onBack }: StepIncomeProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,0.92fr)_minmax(0,1.08fr)] lg:gap-8">
      <aside className="relative overflow-hidden rounded-[24px] border border-emerald-100 bg-gradient-to-br from-emerald-50 via-violet-50 to-white p-5 sm:p-6">
        <div className="pointer-events-none absolute -bottom-12 -right-10 h-36 w-36 rounded-full bg-emerald-200/50 blur-2xl" />
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-emerald-700/90">
          {t('income.visualLabel')}
        </p>
        <svg
          viewBox="0 0 320 220"
          className="mt-4 h-[180px] w-full"
          role="img"
          aria-label="Income illustration"
        >
          <rect x="52" y="118" width="176" height="84" rx="20" fill="#ffffff" stroke="#10b981" strokeWidth="3" />
          <rect x="64" y="136" width="92" height="34" rx="8" fill="#d1fae5" />
          <path d="M73 147h74M73 158h54" stroke="#047857" strokeWidth="3" strokeLinecap="round" />
          <path d="M201 118v-20a20 20 0 0 0-20-20H99a20 20 0 0 0-20 20v20" fill="none" stroke="#10b981" strokeWidth="3" />
          {[
            { cx: 250, cy: 160, delay: '0s' },
            { cx: 270, cy: 128, delay: '0.5s' },
            { cx: 246, cy: 94, delay: '1s' },
          ].map((coin) => (
            <g
              key={`${coin.cx}-${coin.cy}`}
              className="animate-[onboarding-coin-rise_2.2s_ease-in-out_infinite]"
              style={{ animationDelay: coin.delay }}
            >
              <circle cx={coin.cx} cy={coin.cy} r="14" fill="#ecfdf5" stroke="#10b981" strokeWidth="3" />
              <path d={`M${coin.cx - 5} ${coin.cy}h10`} stroke="#059669" strokeWidth="3" strokeLinecap="round" />
            </g>
          ))}
          <path d="M30 190h260" stroke="#a7f3d0" strokeWidth="4" strokeLinecap="round" />
        </svg>
        <div className="rounded-2xl border border-emerald-200/80 bg-white/80 p-4">
          <p className="text-sm font-semibold text-emerald-900">{t('income.detailTitle')}</p>
          <p className="mt-1 text-sm leading-relaxed text-emerald-800/80">{t('income.detailText')}</p>
        </div>
      </aside>

      <section className="flex flex-col rounded-[24px] border border-violet-100 bg-white/80 p-5 sm:p-6">
        <div className="inline-flex w-fit items-center gap-2 rounded-full border border-violet-100 bg-violet-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-violet-600">
          <ArrowTrendingUpIcon className="h-4 w-4" />
          {t('income.label')}
        </div>

        <div className="mt-5 space-y-2">
          <h2 className="font-[var(--font-onboarding-sans)] text-3xl font-semibold tracking-[-0.02em] text-slate-900">
            {t('income.title')}
          </h2>
          <p className="text-sm leading-relaxed text-slate-600 sm:text-base">
            {t('income.subtitle')}
          </p>
        </div>

        <div className="mt-6">
          <CurrencyInput
            value={value}
            onChange={onChange}
            currency={currency}
            label={t('income.label')}
            allowNegative={false}
            placeholder="0.00"
            className="h-16 rounded-2xl border-violet-200 bg-white px-5 text-3xl font-semibold tracking-tight text-slate-900 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)] [font-family:var(--font-onboarding-mono)] [font-variant-numeric:tabular-nums] focus:border-violet-400 focus:ring-4 focus:ring-violet-200/60"
          />
          <p className="mt-2 text-sm text-slate-500">
            {t('income.helper')}
          </p>
        </div>

        <div className="mt-8 flex items-center justify-between">
          <Button
            variant="ghost"
            onClick={onBack}
            className="rounded-xl border border-slate-200 bg-white px-5 text-slate-700 hover:border-violet-200 hover:bg-violet-50"
          >
            {t('back')}
          </Button>
          <Button
            onClick={onNext}
            disabled={value <= 0}
            className="rounded-xl bg-violet-600 px-6 text-white shadow-[0_12px_25px_-15px_rgba(124,58,237,1)] hover:bg-violet-700"
          >
            {t('next')}
            <ArrowDownTrayIcon className="ml-2 h-4 w-4 rotate-180" />
          </Button>
        </div>
      </section>
    </div>
  );
}
