'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ShieldCheckIcon, AdjustmentsHorizontalIcon } from '@heroicons/react/24/outline';
import { CurrencyInput } from '@/components/ui/currency-input';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

interface StepGoalSuggestionProps {
  monthlyIncome: number;
  monthlyExpenses: number;
  goalName: string;
  goalTargetAmount: number;
  onGoalNameChange: (name: string) => void;
  onGoalTargetAmountChange: (amount: number) => void;
  currency: string;
  onNext: () => void;
  onBack: () => void;
}

export function StepGoalSuggestion({
  monthlyIncome,
  monthlyExpenses,
  goalName,
  goalTargetAmount,
  onGoalNameChange,
  onGoalTargetAmountChange,
  currency,
  onNext,
  onBack,
}: StepGoalSuggestionProps) {
  const t = useTranslations('onboarding');
  const [isEditing, setIsEditing] = useState(false);

  const monthlyAvailable = monthlyIncome - monthlyExpenses;

  const formatCurrency = (amount: number) => {
    try {
      return new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency: currency,
      }).format(amount);
    } catch {
      return `${currency} ${amount.toFixed(2)}`;
    }
  };

  return (
    <div className="space-y-6 py-4">
      {/* Gradient accent strip */}
      <div className="h-1 w-full rounded-full bg-gradient-to-r from-primary-600 via-fuchsia-500 to-primary-600" />

      {/* Icon badge */}
      <div className="w-12 h-12 rounded-xl bg-primary-50 ring-1 ring-primary-100 flex items-center justify-center">
        <ShieldCheckIcon className="w-6 h-6 text-primary-700" />
      </div>

      <div className="space-y-1">
        <h2 className="text-xl font-bold text-slate-900">
          {t('goalSuggestion.title')}
        </h2>
        <p className="text-slate-600">
          {t('goalSuggestion.subtitle')}
        </p>
      </div>

      {/* Recommendation card */}
      <div className="rounded-xl bg-slate-50 ring-1 ring-slate-100 p-5 space-y-4">
        {/* Monthly available */}
        <div className="flex items-baseline justify-between">
          <span className="text-sm text-slate-500">{t('goalSuggestion.available')}</span>
          <span className="text-3xl font-bold tabular-nums text-emerald-600">
            {formatCurrency(monthlyAvailable)}
          </span>
        </div>

        <div className="h-px w-full bg-slate-200" />

        {!isEditing ? (
          <div className="space-y-3">
            <div className="relative p-4 bg-primary-50 rounded-lg ring-1 ring-primary-100">
              <span className="absolute top-3 right-3 rounded-full bg-primary-600 px-2 py-0.5 text-xs font-semibold text-white">
                {t('goalSuggestion.recommended')}
              </span>
              <p className="font-medium text-slate-900 pr-24">{goalName}</p>
              <p className="text-2xl font-bold text-primary-600 mt-1">
                {formatCurrency(goalTargetAmount)}
              </p>
            </div>

            {/* WHY explanation */}
            <p className="text-xs text-slate-500 leading-relaxed">
              {t('goalSuggestion.why')}
            </p>

            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsEditing(true)}
              className="text-primary-600 gap-1.5"
            >
              <AdjustmentsHorizontalIcon className="w-4 h-4" />
              {t('goalSuggestion.editGoal')}
            </Button>
          </div>
        ) : (
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">
                {t('goalSuggestion.goalNameLabel')}
              </label>
              <Input
                value={goalName}
                onChange={(e) => onGoalNameChange(e.target.value)}
                maxLength={100}
              />
            </div>
            <CurrencyInput
              value={goalTargetAmount}
              onChange={onGoalTargetAmountChange}
              currency={currency}
              label={t('goalSuggestion.targetLabel')}
              allowNegative={false}
            />
          </div>
        )}
      </div>

      <div className="flex justify-between pt-4">
        <Button variant="ghost" onClick={onBack}>
          {t('back')}
        </Button>
        <Button
          onClick={onNext}
          disabled={!goalName.trim() || goalTargetAmount <= 0}
        >
          {t('next')}
        </Button>
      </div>
    </div>
  );
}
