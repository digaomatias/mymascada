'use client';

import { useTranslations } from 'next-intl';
import { CreditCardIcon } from '@heroicons/react/24/outline';
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
    <div className="space-y-6 py-4">
      {/* Gradient accent strip */}
      <div className="h-1 w-full rounded-full bg-gradient-to-r from-slate-400 via-slate-600 to-slate-400" />

      <div className="grid gap-6 lg:grid-cols-[1fr_0.8fr] items-start">
        {/* Left side: main content */}
        <div className="space-y-6">
          {/* Icon badge */}
          <div className="w-12 h-12 rounded-xl bg-slate-50 ring-1 ring-slate-200 flex items-center justify-center">
            <CreditCardIcon className="w-6 h-6 text-slate-700" />
          </div>

          {/* Left-aligned title and subtitle */}
          <div className="space-y-1">
            <h2 className="text-xl font-bold text-slate-900">
              {t('expenses.title')}
            </h2>
            <p className="text-slate-600">
              {t('expenses.subtitle')}
            </p>
          </div>

          <div className="max-w-sm">
            <CurrencyInput
              value={value}
              onChange={onChange}
              currency={currency}
              label={t('expenses.label')}
              allowNegative={false}
              placeholder="0.00"
            />
            <p className="text-xs text-slate-400 mt-2">
              {t('expenses.helper')}
            </p>
          </div>
        </div>

        {/* Right side: decorative breakdown (desktop only) */}
        <div className="hidden lg:block">
          <div className="rounded-xl bg-slate-50 ring-1 ring-slate-100 p-4 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              {t('expenses.breakdown')}
            </p>
            <div className="space-y-2.5">
              {BREAKDOWN_ITEMS.map((item) => (
                <div key={item.key} className="space-y-1">
                  <span className="text-xs text-slate-500">
                    {t(`expenses.${item.key}`)}
                  </span>
                  <div className="h-2 w-full rounded-full bg-slate-200">
                    <div className={`h-2 rounded-full ${item.color} ${item.width}`} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      <div className="flex justify-between pt-4">
        <Button variant="ghost" onClick={onBack}>
          {t('back')}
        </Button>
        <Button onClick={onNext} disabled={value <= 0}>
          {t('next')}
        </Button>
      </div>
    </div>
  );
}
