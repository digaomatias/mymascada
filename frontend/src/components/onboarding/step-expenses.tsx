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
          <rect x="48" y="124" width="194" height="72" rx="18" fill="#ffffff" stroke="#f59e0b" strokeWidth="3" />
          <rect x="66" y="82" width="126" height="72" rx="10" fill="#fffbeb" stroke="#f59e0b" strokeWidth="3" />
          <rect x="90" y="64" width="126" height="72" rx="10" fill="#fff7ed" stroke="#fb923c" strokeWidth="3" />
          <path d="M79 102h58M79 114h72M79 126h40" stroke="#b45309" strokeWidth="3" strokeLinecap="round" />
          <path d="M104 84h58M104 96h72M104 108h36" stroke="#c2410c" strokeWidth="3" strokeLinecap="round" />
          <path
            d="M236 70a38 38 0 1 1-1 0"
            fill="none"
            stroke="#f59e0b"
            strokeWidth="9"
            strokeLinecap="round"
          />
          <path d="M236 70a38 38 0 0 1 24 10" fill="none" stroke="#7c3aed" strokeWidth="9" strokeLinecap="round" />
          {[
            { x1: 216, y1: 170, x2: 258, y2: 154 },
            { x1: 206, y1: 145, x2: 252, y2: 129 },
            { x1: 198, y1: 118, x2: 242, y2: 102 },
          ].map((arrow, index) => (
            <g
              key={`${arrow.x1}-${arrow.y1}`}
              className="animate-[onboarding-drift_2.6s_ease-in-out_infinite]"
              style={{ animationDelay: `${index * 0.3}s` }}
            >
              <path d={`M${arrow.x1} ${arrow.y1}L${arrow.x2} ${arrow.y2}`} stroke="#f59e0b" strokeWidth="2.8" strokeLinecap="round" strokeDasharray="4 5" />
              <path d={`M${arrow.x2 - 6} ${arrow.y2 - 2}L${arrow.x2 + 2} ${arrow.y2}L${arrow.x2 - 2} ${arrow.y2 + 6}`} fill="none" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
            </g>
          ))}
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
