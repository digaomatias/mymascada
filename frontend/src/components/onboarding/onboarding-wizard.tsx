'use client';

import { useState, useCallback, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { DM_Mono, Manrope } from 'next/font/google';
import { useAuth } from '@/contexts/auth-context';
import { apiClient } from '@/lib/api-client';
import { cn } from '@/lib/utils';
import { StepProgressIndicator } from './step-progress-indicator';
import { StepWelcome } from './step-welcome';
import { StepIncome } from './step-income';
import { StepExpenses } from './step-expenses';
import { StepGoalSuggestion } from './step-goal-suggestion';
import { StepComplete } from './step-complete';

const TOTAL_STEPS = 5;
const REDIRECT_SECONDS = 3;

const manrope = Manrope({
  subsets: ['latin'],
  variable: '--font-onboarding-sans',
});

const dmMono = DM_Mono({
  subsets: ['latin'],
  weight: ['400', '500'],
  variable: '--font-onboarding-mono',
});

export function OnboardingWizard() {
  const t = useTranslations('onboarding');
  const router = useRouter();
  const { user, refreshUser } = useAuth();

  const [currentStep, setCurrentStep] = useState(1);
  const [monthlyIncome, setMonthlyIncome] = useState(0);
  const [monthlyExpenses, setMonthlyExpenses] = useState(0);
  const [goalName, setGoalName] = useState('');
  const [goalTargetAmount, setGoalTargetAmount] = useState(0);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSkipping, setIsSkipping] = useState(false);
  const [redirectCountdown, setRedirectCountdown] = useState(REDIRECT_SECONDS);
  const [error, setError] = useState<string | null>(null);
  const [accounts, setAccounts] = useState<Array<{ id: number; name: string; currentBalance: number }>>([]);
  const [linkedAccountId, setLinkedAccountId] = useState<number | undefined>(undefined);

  const currency = user?.currency || 'NZD';

  useEffect(() => {
    apiClient.getAccountsWithBalances()
      .then((data: unknown) => {
        if (Array.isArray(data)) {
          setAccounts(data.map((a: Record<string, unknown>) => ({ id: a.id as number, name: a.name as string, currentBalance: (a.currentBalance as number) ?? 0 })));
        }
      })
      .catch(() => {
        // Accounts are optional for onboarding — silently ignore
      });
  }, []);

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
        dataEntryMethod: 'manual',
        ...(linkedAccountId !== undefined && { linkedAccountId }),
      });

      setCurrentStep(5);
      setRedirectCountdown(REDIRECT_SECONDS);
      setIsSubmitting(false);
    } catch (err) {
      console.error('Onboarding error:', err);
      const message = err instanceof Error ? err.message : 'An error occurred';
      setError(message);
      setIsSubmitting(false);
    }
  }, [monthlyIncome, monthlyExpenses, goalName, goalTargetAmount, linkedAccountId]);

  const navigateToDashboard = useCallback(async () => {
    try {
      await refreshUser();
    } catch {
      // The user state can be refreshed on the dashboard route if this fails.
    }
    router.replace('/dashboard');
  }, [refreshUser, router]);

  const handleSkip = useCallback(async () => {
    setIsSkipping(true);
    setError(null);
    try {
      await apiClient.completeOnboarding({
        monthlyIncome: 0,
        monthlyExpenses: 0,
        goalName: t('goalSuggestion.suggestedGoal'),
        goalTargetAmount: 0,
        goalType: 'EmergencyFund',
        dataEntryMethod: 'manual',
      });
      await navigateToDashboard();
    } catch (err) {
      console.error('Onboarding error:', err);
      const message = err instanceof Error ? err.message : 'An error occurred';
      setError(message);
      setIsSkipping(false);
    }
  }, [navigateToDashboard, t]);

  useEffect(() => {
    if (currentStep !== 5) {
      return;
    }

    const countdownInterval = setInterval(() => {
      setRedirectCountdown((previous) => (previous > 1 ? previous - 1 : 1));
    }, 1000);

    const redirectTimer = setTimeout(() => {
      void navigateToDashboard();
    }, REDIRECT_SECONDS * 1000);

    return () => {
      clearInterval(countdownInterval);
      clearTimeout(redirectTimer);
    };
  }, [currentStep, navigateToDashboard]);

  const showProgress = currentStep > 1 && currentStep < 6;

  return (
    <div
      className={cn(
        'relative min-h-dvh overflow-hidden bg-[#f6f3ff] px-4 py-6 sm:px-6 lg:px-10',
        manrope.variable,
        dmMono.variable
      )}
    >
      <div aria-hidden="true" className="pointer-events-none absolute inset-0">
        <div className="absolute -left-44 -top-44 h-[580px] w-[580px] rounded-full bg-violet-200/55 blur-[110px]" />
        <div className="absolute -bottom-56 -right-40 h-[560px] w-[560px] rounded-full bg-fuchsia-200/50 blur-[120px]" />
        <div
          className="absolute inset-0 opacity-[0.08]"
          style={{
            backgroundImage:
              'linear-gradient(to right, rgba(124, 58, 237, 0.2) 1px, transparent 1px), linear-gradient(to bottom, rgba(124, 58, 237, 0.2) 1px, transparent 1px)',
            backgroundSize: '30px 30px',
          }}
        />
      </div>

      <div className="relative z-10 mx-auto flex w-full max-w-[980px] flex-col justify-center gap-5 py-2 sm:py-8">
        {showProgress && (
          <div className="mx-auto w-full max-w-[860px] lg:max-w-full">
            <StepProgressIndicator currentStep={currentStep} totalSteps={TOTAL_STEPS} />
          </div>
        )}

        <div
          className={cn(
            'w-full overflow-hidden rounded-[30px] border border-violet-100/85 bg-white/85 shadow-[0_20px_70px_-32px_rgba(76,29,149,0.42)] backdrop-blur-xl',
            (currentStep === 1 || currentStep === 5) && 'mx-auto max-w-[860px]'
          )}
        >
          <div className="px-4 py-5 sm:px-7 sm:py-8 lg:px-10 lg:py-9">
            <div
              key={currentStep}
              className="animate-[onboarding-step-in_360ms_cubic-bezier(0.22,1,0.36,1)]"
            >
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
                  onNext={handleComplete}
                  onBack={() => setCurrentStep(3)}
                  isSubmitting={isSubmitting}
                  accounts={accounts}
                  linkedAccountId={linkedAccountId}
                  onLinkedAccountChange={setLinkedAccountId}
                />
              )}

              {currentStep === 5 && (
                <StepComplete
                  countdown={redirectCountdown}
                  onGoNow={() => {
                    void navigateToDashboard();
                  }}
                />
              )}
            </div>
          </div>
        </div>

        {error && (
          <div className="mx-auto w-full max-w-[860px] rounded-2xl border border-red-200 bg-red-50/90 px-4 py-3">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {currentStep !== 5 && (
          <div className="flex flex-col items-center gap-2">
            {!showProgress && (
              <p className="text-center text-xs font-medium tracking-[0.12em] text-violet-500/70">
                {t('welcome.duration')}
              </p>
            )}
            <button
              type="button"
              onClick={() => { void handleSkip(); }}
              disabled={isSkipping || isSubmitting}
              className="text-xs text-violet-400/70 hover:text-violet-500 transition-colors disabled:opacity-50 disabled:cursor-not-allowed underline underline-offset-2"
            >
              {isSkipping ? t('skipping') : t('skip')}
            </button>
          </div>
        )}

        {currentStep === 5 && (
          <p className="mx-auto text-center text-xs font-medium tracking-[0.12em] text-violet-500/70">
            {t('complete.redirecting')}
          </p>
        )}
      </div>
    </div>
  );
}
