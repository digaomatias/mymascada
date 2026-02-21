'use client';

import { useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useAuth } from '@/contexts/auth-context';
import { apiClient } from '@/lib/api-client';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StepProgressIndicator } from './step-progress-indicator';
import { StepWelcome } from './step-welcome';
import { StepIncome } from './step-income';
import { StepExpenses } from './step-expenses';
import { StepGoalSuggestion } from './step-goal-suggestion';
import { StepDataEntry } from './step-data-entry';

const TOTAL_STEPS = 6;

export function OnboardingWizard() {
  const t = useTranslations('onboarding');
  const router = useRouter();
  const { user, refreshUser } = useAuth();

  const [currentStep, setCurrentStep] = useState(1);
  const [monthlyIncome, setMonthlyIncome] = useState(0);
  const [monthlyExpenses, setMonthlyExpenses] = useState(0);
  const [goalName, setGoalName] = useState('');
  const [goalTargetAmount, setGoalTargetAmount] = useState(0);
  const [dataEntryMethod, setDataEntryMethod] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const currency = user?.currency || 'NZD';

  // When moving from expenses to goal suggestion, pre-populate defaults
  const handleExpensesNext = useCallback(() => {
    if (!goalName) {
      setGoalName(t('goalSuggestion.suggestedGoal'));
    }
    if (goalTargetAmount === 0) {
      setGoalTargetAmount(monthlyExpenses * 3);
    }
    setCurrentStep(4);
  }, [goalName, goalTargetAmount, monthlyExpenses, t]);

  const handleComplete = useCallback(async () => {
    setIsSubmitting(true);
    setError(null);

    try {
      await apiClient.completeOnboarding({
        monthlyIncome,
        monthlyExpenses,
        goalName: goalName.trim(),
        goalTargetAmount,
        goalType: 'EmergencyFund',
        dataEntryMethod,
      });

      setCurrentStep(6);

      // Refresh user data so isOnboardingComplete updates
      await refreshUser();

      // Redirect after a brief pause
      setTimeout(() => {
        router.replace('/dashboard');
      }, 2000);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'An error occurred';
      setError(message);
      setIsSubmitting(false);
    }
  }, [monthlyIncome, monthlyExpenses, goalName, goalTargetAmount, dataEntryMethod, refreshUser, router]);

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="w-full max-w-lg">
        {currentStep > 1 && currentStep < 6 && (
          <div className="mb-6">
            <StepProgressIndicator currentStep={currentStep} totalSteps={TOTAL_STEPS} />
          </div>
        )}

        <Card>
          <CardContent className="p-6">
            {currentStep === 1 && (
              <StepWelcome onNext={() => setCurrentStep(2)} />
            )}

            {currentStep === 2 && (
              <StepIncome
                value={monthlyIncome}
                onChange={setMonthlyIncome}
                currency={currency}
                onNext={() => setCurrentStep(3)}
                onBack={() => setCurrentStep(1)}
              />
            )}

            {currentStep === 3 && (
              <StepExpenses
                value={monthlyExpenses}
                onChange={setMonthlyExpenses}
                currency={currency}
                onNext={handleExpensesNext}
                onBack={() => setCurrentStep(2)}
              />
            )}

            {currentStep === 4 && (
              <StepGoalSuggestion
                monthlyIncome={monthlyIncome}
                monthlyExpenses={monthlyExpenses}
                goalName={goalName}
                goalTargetAmount={goalTargetAmount}
                onGoalNameChange={setGoalName}
                onGoalTargetAmountChange={setGoalTargetAmount}
                currency={currency}
                onNext={() => setCurrentStep(5)}
                onBack={() => setCurrentStep(3)}
              />
            )}

            {currentStep === 5 && (
              <StepDataEntry
                value={dataEntryMethod}
                onChange={setDataEntryMethod}
                onNext={handleComplete}
                onBack={() => setCurrentStep(4)}
              />
            )}

            {currentStep === 6 && (
              <div className="flex flex-col items-center text-center space-y-4 py-8">
                <div className="text-5xl">🎉</div>
                <h2 className="text-xl font-bold text-gray-900">
                  {t('complete.title')}
                </h2>
                <p className="text-gray-600">
                  {t('complete.subtitle')}
                </p>
                <p className="text-sm text-gray-400">
                  {t('complete.redirecting')}
                </p>
              </div>
            )}

            {error && (
              <div className="mt-4 p-3 bg-red-50 border border-red-200 rounded-md">
                <p className="text-sm text-red-600">{error}</p>
              </div>
            )}

            {isSubmitting && currentStep === 5 && (
              <div className="mt-4 flex justify-center">
                <Button loading disabled>
                  {t('finish')}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
