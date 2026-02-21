'use client';

import { useTranslations } from 'next-intl';
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
      <div className="text-center space-y-2">
        <h2 className="text-xl font-bold text-gray-900">
          {t('income.title')}
        </h2>
        <p className="text-gray-600">
          {t('income.subtitle')}
        </p>
      </div>

      <div className="max-w-sm mx-auto">
        <CurrencyInput
          value={value}
          onChange={onChange}
          currency={currency}
          label={t('income.label')}
          allowNegative={false}
          placeholder="0.00"
        />
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
