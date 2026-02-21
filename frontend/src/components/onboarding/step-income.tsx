'use client';

import { useTranslations } from 'next-intl';
import { ArrowDownTrayIcon } from '@heroicons/react/24/outline';
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
    <div className="space-y-6 py-4">
      {/* Gradient accent strip */}
      <div className="h-1 w-full rounded-full bg-gradient-to-r from-primary-600 via-fuchsia-500 to-primary-600" />

      {/* Icon badge */}
      <div className="w-12 h-12 rounded-xl bg-primary-50 ring-1 ring-primary-100 flex items-center justify-center">
        <ArrowDownTrayIcon className="w-6 h-6 text-primary-700" />
      </div>

      {/* Left-aligned title and subtitle */}
      <div className="space-y-1">
        <h2 className="text-xl font-bold text-slate-900">
          {t('income.title')}
        </h2>
        <p className="text-slate-600">
          {t('income.subtitle')}
        </p>
      </div>

      <div className="max-w-sm">
        <CurrencyInput
          value={value}
          onChange={onChange}
          currency={currency}
          label={t('income.label')}
          allowNegative={false}
          placeholder="0.00"
        />
        <p className="text-xs text-slate-400 mt-2">
          {t('income.helper')}
        </p>
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
