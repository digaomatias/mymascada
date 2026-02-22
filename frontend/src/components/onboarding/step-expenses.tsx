'use client';

import { useTranslations } from 'next-intl';
import { CreditCardIcon, ArrowRightIcon } from '@heroicons/react/24/outline';
import { CurrencyInput } from '@/components/ui/currency-input';
import { Button } from '@/components/ui/button';

interface StepExpensesProps {
  value: number;
  onChange: (value: number) => void;
  currency: string;
  onNext: () => void;
  onBack: () => void;
}

const BREAKDOWN_ITEMS = [
  { key: 'housing', width: 'w-4/5', color: 'bg-slate-700' },
  { key: 'food', width: 'w-3/5', color: 'bg-slate-500' },
  { key: 'transport', width: 'w-2/5', color: 'bg-slate-400' },
  { key: 'entertainment', width: 'w-1/4', color: 'bg-slate-300' },
] as const;

export function StepExpenses({ value, onChange, currency, onNext, onBack }: StepExpensesProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] lg:gap-8">
      <aside className="relative overflow-hidden rounded-[24px] border border-amber-100 bg-gradient-to-br from-amber-50 via-rose-50 to-white p-5 sm:p-6">
        <div className="pointer-events-none absolute -left-14 -top-14 h-36 w-36 rounded-full bg-rose-200/40 blur-2xl" />
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-amber-700/90">
          {t('expenses.visualLabel')}
        </p>
        <svg
          viewBox="0 0 320 220"
          className="mt-4 h-[178px] w-full"
          role="img"
          aria-label="Expense illustration"
        >
          {/* Receipt */}
          <g transform="translate(120,18)">
            {/* Receipt body with torn bottom edge */}
            <path
              d="M-50 0 h100 a6 6 0 0 1 6 6 v148 l-7-5 -7 5 -7-5 -7 5 -7-5 -7 5 -7-5 -7 5 -7-5 -7 5 -7-5 -7 5 -7-5 -7 5 v-148 a6 6 0 0 1 6 -6z"
              fill="#ffffff"
              stroke="#f59e0b"
              strokeWidth="2.5"
              strokeLinejoin="round"
            />

            {/* Receipt header — store name */}
            <rect x="-30" y="14" width="60" height="6" rx="3" fill="#fde68a" />

            {/* Divider line */}
            <path d="M-38 30 h76" stroke="#fde68a" strokeWidth="1.5" strokeDasharray="3 3" />

            {/* Line items */}
            <path d="M-34 44 h42" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />
            <path d="M26 44 h12" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />

            <path d="M-34 58 h50" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />
            <path d="M28 58 h10" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />

            <path d="M-34 72 h36" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />
            <path d="M24 72 h14" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />

            <path d="M-34 86 h46" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />
            <path d="M30 86 h8" stroke="#d97706" strokeWidth="2.5" strokeLinecap="round" />

            {/* Total divider */}
            <path d="M-38 100 h76" stroke="#f59e0b" strokeWidth="2" />

            {/* Total line — bold */}
            <path d="M-34 116 h28" stroke="#92400e" strokeWidth="3.5" strokeLinecap="round" />
            <path d="M18 116 h20" stroke="#92400e" strokeWidth="3.5" strokeLinecap="round" />
          </g>

          {/* Expense category icons — house, bag, car */}
          {/* House (rent) */}
          <g transform="translate(246,56)">
            <g className="animate-[onboarding-coin-fall_2.4s_ease-in-out_infinite]" style={{ animationDelay: '0s' }}>
              <circle r="18" fill="#fffbeb" stroke="#f59e0b" strokeWidth="2.5" />
              <path d="M0-9 l10 8 v8 h-6 v-5 h-8 v5 h-6 v-8 z" fill="#fde68a" stroke="#d97706" strokeWidth="1.8" strokeLinejoin="round" />
            </g>
          </g>

          {/* Shopping bag (groceries) */}
          <g transform="translate(268,108)">
            <g className="animate-[onboarding-coin-fall_2.4s_ease-in-out_infinite]" style={{ animationDelay: '0.5s' }}>
              <circle r="15" fill="#fffbeb" stroke="#f59e0b" strokeWidth="2.5" />
              <rect x="-7" y="-3" width="14" height="12" rx="2" fill="#fde68a" stroke="#d97706" strokeWidth="1.8" />
              <path d="M-4-3 v-3 a4 4 0 0 1 8 0 v3" fill="none" stroke="#d97706" strokeWidth="1.8" strokeLinecap="round" />
            </g>
          </g>

          {/* Car (transport) */}
          <g transform="translate(250,158)">
            <g className="animate-[onboarding-coin-fall_2.4s_ease-in-out_infinite]" style={{ animationDelay: '1s' }}>
              <circle r="13" fill="#fffbeb" stroke="#f59e0b" strokeWidth="2.5" />
              <path d="M-7 1 h14 v4 h-14 z M-5 1 l2-5 h6 l2 5" fill="#fde68a" stroke="#d97706" strokeWidth="1.5" strokeLinejoin="round" />
              <circle cx="-4" cy="5" r="1.5" fill="#d97706" />
              <circle cx="4" cy="5" r="1.5" fill="#d97706" />
            </g>
          </g>

          {/* Downward arrow — money going out */}
          <g transform="translate(60,105)">
            <g className="animate-[onboarding-coin-fall_2s_ease-in-out_infinite]" style={{ animationDelay: '0.3s' }}>
              <path d="M0-15 v30 M-10 5 l10 14 l10-14" stroke="#f59e0b" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" fill="none" />
            </g>
          </g>
        </svg>
        <div className="rounded-2xl border border-amber-200/80 bg-white/80 p-4">
          <p className="text-sm font-semibold text-amber-900">{t('expenses.breakdown')}</p>
          <p className="mt-1 text-sm leading-relaxed text-amber-900/75">{t('expenses.detailText')}</p>
        </div>
      </aside>

      <section className="flex flex-col rounded-[24px] border border-violet-100 bg-white/80 p-5 sm:p-6">
        <div className="inline-flex w-fit items-center gap-2 rounded-full border border-amber-100 bg-amber-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-amber-700">
          <CreditCardIcon className="h-4 w-4" />
          {t('expenses.label')}
        </div>

        <div className="mt-5 space-y-2">
          <h2 className="font-[var(--font-onboarding-sans)] text-3xl font-semibold tracking-[-0.02em] text-slate-900">
            {t('expenses.title')}
          </h2>
          <p className="text-sm leading-relaxed text-slate-600 sm:text-base">
            {t('expenses.subtitle')}
          </p>
        </div>

        <div className="mt-6">
          <CurrencyInput
            value={value}
            onChange={onChange}
            currency={currency}
            label={t('expenses.label')}
            allowNegative={false}
            placeholder="0.00"
            className="h-16 rounded-2xl border-amber-200 bg-white px-5 text-3xl font-semibold tracking-tight text-slate-900 shadow-[inset_0_1px_0_rgba(255,255,255,0.9)] [font-family:var(--font-onboarding-mono)] [font-variant-numeric:tabular-nums] focus:border-amber-400 focus:ring-4 focus:ring-amber-200/65"
          />
          <p className="mt-2 text-sm text-slate-500">
            {t('expenses.helper')}
          </p>
        </div>

        <div className="mt-5 rounded-2xl border border-slate-200 bg-slate-50/80 p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
            {t('expenses.snapshot')}
          </p>
          <div className="mt-3 flex flex-wrap gap-2.5">
            {BREAKDOWN_ITEMS.slice(0, 3).map((item) => (
              <span
                key={item.key}
                className="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600"
              >
                {t(`expenses.${item.key}`)}
              </span>
            ))}
          </div>
        </div>

        <div className="mt-8 flex items-center justify-between">
          <Button
            variant="ghost"
            onClick={onBack}
            className="rounded-xl border border-slate-200 bg-white px-5 text-slate-700 hover:border-amber-200 hover:bg-amber-50"
          >
            {t('back')}
          </Button>
          <Button
            onClick={onNext}
            disabled={value <= 0}
            className="rounded-xl bg-violet-600 px-6 text-white shadow-[0_12px_25px_-15px_rgba(124,58,237,1)] hover:bg-violet-700"
          >
            {t('next')}
            <ArrowRightIcon className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </section>
    </div>
  );
}
