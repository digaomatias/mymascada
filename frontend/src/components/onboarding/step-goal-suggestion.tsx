'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { CurrencyInput } from '@/components/ui/currency-input';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

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
      <div className="text-center space-y-2">
        <h2 className="text-xl font-bold text-gray-900">
          {t('goalSuggestion.title')}
        </h2>
        <p className="text-gray-600">
          {t('goalSuggestion.subtitle')}
        </p>
      </div>

      <Card>
        <CardContent className="pt-6 space-y-4">
          <div className="flex justify-between items-center">
            <span className="text-sm text-gray-500">{t('goalSuggestion.available')}</span>
            <span className="text-lg font-semibold text-green-600">
              {formatCurrency(monthlyAvailable)}
            </span>
          </div>

          {!isEditing ? (
            <div className="space-y-3">
              <div className="p-4 bg-primary-50 rounded-lg">
                <p className="font-medium text-gray-900">{goalName}</p>
                <p className="text-2xl font-bold text-primary-600 mt-1">
                  {formatCurrency(goalTargetAmount)}
                </p>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setIsEditing(true)}
                className="text-primary-600"
              >
                {t('goalSuggestion.editGoal')}
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
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
        </CardContent>
      </Card>

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
