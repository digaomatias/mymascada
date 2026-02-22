'use client';

import { useState, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { DM_Mono, Manrope } from 'next/font/google';
import { cn } from '@/lib/utils';
import { useLocale } from '@/contexts/locale-context';
import { StepProgressIndicator } from '@/components/onboarding/step-progress-indicator';
import { StepWelcome } from '@/components/onboarding/step-welcome';
import { StepIncome } from '@/components/onboarding/step-income';
import { StepExpenses } from '@/components/onboarding/step-expenses';
import { StepGoalSuggestion } from '@/components/onboarding/step-goal-suggestion';
import { StepComplete } from '@/components/onboarding/step-complete';

const TOTAL_STEPS = 5;

const manrope = Manrope({
  subsets: ['latin'],
  variable: '--font-onboarding-sans',
});

const dmMono = DM_Mono({
  subsets: ['latin'],
  weight: ['400', '500'],
  variable: '--font-onboarding-mono',
});

/**
 * Dev-only preview page for the onboarding wizard.
 * No auth, no API calls — just renders the UI with mock data.
 * Navigate to /onboarding/preview to use it.
 */
export default function OnboardingPreviewPage() {
  const t = useTranslations('onboarding');
  const { locale, setLocale } = useLocale();

  const [currentStep, setCurrentStep] = useState(1);
  const [monthlyIncome, setMonthlyIncome] = useState(0);
  const [monthlyExpenses, setMonthlyExpenses] = useState(0);
  const [goalName, setGoalName] = useState('');
  const [goalTargetAmount, setGoalTargetAmount] = useState(0);
  const [linkedAccountId, setLinkedAccountId] = useState<number | undefined>(undefined);

  const mockAccounts = [
    { id: 1, name: 'Savings Account', currentBalance: 5000 },
    { id: 2, name: 'Emergency Fund', currentBalance: 12500 },
    { id: 3, name: 'Checking Account', currentBalance: 1200 },
  ];

  const currency = 'BRL';

  const handleExpensesNext = useCallback(() => {
    if (!goalName) {
      setGoalName(t('goalSuggestion.suggestedGoal'));
    }
    if (goalTargetAmount === 0) {
      setGoalTargetAmount(monthlyExpenses * 3);
    }
    setCurrentStep(4);
  }, [goalName, goalTargetAmount, monthlyExpenses, t]);

  const handleComplete = useCallback(() => {
    // No API call — just advance to completion step
    setCurrentStep(5);
  }, []);

  const showProgress = currentStep > 1 && currentStep < 6;

  return (
    <div
      className={cn(
        'relative min-h-dvh overflow-hidden bg-[#f6f3ff] px-4 py-6 sm:px-6 lg:px-10',
        manrope.variable,
        dmMono.variable
      )}
    >
      {/* Dev toolbar */}
      <div className="fixed bottom-4 left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-full border border-violet-200 bg-white/95 px-4 py-2 shadow-lg backdrop-blur">
        <span className="text-xs font-medium text-violet-600">Step:</span>
        {[1, 2, 3, 4, 5].map((step) => (
          <button
            key={step}
            onClick={() => {
              if (step >= 3 && monthlyIncome === 0) setMonthlyIncome(5000);
              if (step >= 4 && monthlyExpenses === 0) setMonthlyExpenses(3500);
              if (step >= 4 && !goalName) {
                setGoalName(t('goalSuggestion.suggestedGoal'));
                setGoalTargetAmount(3500 * 3);
              }
              setCurrentStep(step);
            }}
            className={cn(
              'flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold transition-all',
              currentStep === step
                ? 'bg-violet-600 text-white'
                : 'bg-violet-100 text-violet-600 hover:bg-violet-200'
            )}
          >
            {step}
          </button>
        ))}
        <div className="mx-1 h-5 w-px bg-violet-200" />
        <div className="flex items-center gap-1">
          {(['en', 'pt-BR'] as const).map((lang) => (
            <button
              key={lang}
              onClick={() => setLocale(lang)}
              className={cn(
                'rounded-full px-2.5 py-1 text-xs font-bold transition-all',
                locale === lang
                  ? 'bg-violet-600 text-white'
                  : 'bg-violet-100 text-violet-600 hover:bg-violet-200'
              )}
            >
              {lang === 'en' ? 'EN' : 'PT'}
            </button>
          ))}
        </div>
      </div>

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
                  accounts={mockAccounts}
                  linkedAccountId={linkedAccountId}
                  onLinkedAccountChange={setLinkedAccountId}
                />
              )}

              {currentStep === 5 && (
                <StepComplete
                  countdown={3}
                  onGoNow={() => setCurrentStep(1)}
                />
              )}
            </div>
          </div>
        </div>

        {!showProgress && currentStep !== 5 && (
          <p className="mx-auto text-center text-xs font-medium tracking-[0.12em] text-violet-500/70">
            {t('welcome.duration')}
          </p>
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
