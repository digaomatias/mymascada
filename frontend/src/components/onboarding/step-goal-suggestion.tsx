'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ShieldCheckIcon, AdjustmentsHorizontalIcon, ArrowRightIcon } from '@heroicons/react/24/outline';
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
  const monthlyContribution = monthlyAvailable > 0 ? monthlyAvailable : Math.max(goalTargetAmount / 3, 0);
  const timelineProgress = [1, 2, 3].map((month) => {
    if (goalTargetAmount <= 0) {
      return 0;
    }
    return Math.min((monthlyContribution * month / goalTargetAmount) * 100, 100);
  });

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
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1.08fr)_minmax(0,0.92fr)] lg:gap-8">
      <section className="flex flex-col rounded-[24px] border border-violet-100 bg-white/80 p-5 sm:p-6">
        <div className="inline-flex w-fit items-center gap-2 rounded-full border border-violet-100 bg-violet-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-violet-600">
          <ShieldCheckIcon className="h-4 w-4" />
          {t('goalSuggestion.recommended')}
        </div>

        <div className="mt-5 space-y-2">
          <h2 className="font-[var(--font-onboarding-sans)] text-3xl font-semibold tracking-[-0.02em] text-slate-900">
            {t('goalSuggestion.title')}
          </h2>
          <p className="text-sm leading-relaxed text-slate-600 sm:text-base">
            {t('goalSuggestion.subtitle')}
          </p>
        </div>

        <div className="mt-5 rounded-2xl border border-violet-100 bg-violet-50/70 p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.17em] text-violet-500">
            {t('goalSuggestion.equationTitle')}
          </p>
          <div className="mt-3 flex flex-wrap items-center gap-2 text-sm font-semibold text-slate-700">
            <span className="rounded-lg bg-white px-3 py-1.5 shadow-sm">
              {t('goalSuggestion.equationIncome')}: <span className="font-[var(--font-onboarding-mono)]">{formatCurrency(monthlyIncome)}</span>
            </span>
            <span className="text-violet-500">-</span>
            <span className="rounded-lg bg-white px-3 py-1.5 shadow-sm">
              {t('goalSuggestion.equationExpenses')}: <span className="font-[var(--font-onboarding-mono)]">{formatCurrency(monthlyExpenses)}</span>
            </span>
            <span className="text-violet-500">=</span>
            <span className={`rounded-lg px-3 py-1.5 font-[var(--font-onboarding-mono)] shadow-sm ${
              monthlyAvailable >= 0 ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'
            }`}>
              {t('goalSuggestion.available')}: {formatCurrency(monthlyAvailable)}
            </span>
          </div>
        </div>

        <div className="mt-5 rounded-2xl border border-slate-200 bg-white p-4">
          {!isEditing ? (
            <div className="space-y-3">
              <div className="relative rounded-2xl border border-violet-200 bg-gradient-to-br from-violet-50 to-fuchsia-50 p-4">
                <span className="absolute right-3 top-3 rounded-full bg-violet-600 px-2 py-0.5 text-xs font-semibold text-white">
                  {t('goalSuggestion.recommended')}
                </span>
                <p className="pr-24 text-sm font-medium text-slate-600">{t('goalSuggestion.goalNameLabel')}</p>
                <p className="mt-1 text-lg font-semibold text-slate-900">{goalName}</p>
                <p className="mt-2 font-[var(--font-onboarding-mono)] text-3xl font-semibold text-violet-700">
                  {formatCurrency(goalTargetAmount)}
                </p>
              </div>
              <p className="text-sm leading-relaxed text-slate-600">
                {t('goalSuggestion.why')}
              </p>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setIsEditing(true)}
                className="rounded-lg border border-violet-200 bg-violet-50 text-violet-700 hover:bg-violet-100"
              >
                <AdjustmentsHorizontalIcon className="mr-1.5 h-4 w-4" />
                {t('goalSuggestion.editGoal')}
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              <Input
                label={t('goalSuggestion.goalNameLabel')}
                value={goalName}
                onChange={(event) => onGoalNameChange(event.target.value)}
                maxLength={100}
                className="rounded-xl border-violet-200 focus:border-violet-400 focus:ring-violet-200"
              />
              <CurrencyInput
                value={goalTargetAmount}
                onChange={onGoalTargetAmountChange}
                currency={currency}
                label={t('goalSuggestion.targetLabel')}
                allowNegative={false}
                className="rounded-xl border-violet-200 bg-white [font-family:var(--font-onboarding-mono)] focus:border-violet-400 focus:ring-violet-200"
              />
              <div className="flex justify-end">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setIsEditing(false)}
                  className="rounded-lg border border-slate-200 bg-white text-slate-600 hover:border-violet-200 hover:bg-violet-50"
                >
                  {t('goalSuggestion.doneEditing')}
                </Button>
              </div>
            </div>
          )}
        </div>

        <div className="mt-8 flex items-center justify-between">
          <Button
            variant="ghost"
            onClick={onBack}
            className="rounded-xl border border-slate-200 bg-white px-5 text-slate-700 hover:border-violet-200 hover:bg-violet-50"
          >
            {t('back')}
          </Button>
          <Button
            onClick={onNext}
            disabled={!goalName.trim() || goalTargetAmount <= 0}
            className="rounded-xl bg-violet-600 px-6 text-white shadow-[0_12px_25px_-15px_rgba(124,58,237,1)] hover:bg-violet-700"
          >
            {t('next')}
            <ArrowRightIcon className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </section>

      <aside className="relative overflow-hidden rounded-[24px] border border-violet-100 bg-gradient-to-br from-violet-50 via-white to-teal-50 p-5 sm:p-6">
        <div className="pointer-events-none absolute -bottom-12 -left-16 h-44 w-44 rounded-full bg-teal-200/45 blur-2xl" />
        <svg
          viewBox="0 0 280 190"
          className="h-[160px] w-full"
          role="img"
          aria-label="Emergency fund illustration"
        >
          <path d="M90 144c0-32 22-52 50-52s50 20 50 52v22H90z" fill="#ede9fe" stroke="#7c3aed" strokeWidth="3" />
          <path d="M111 114h58" stroke="#7c3aed" strokeWidth="3" strokeLinecap="round" />
          <path d="M120 96a25 25 0 0 1 40 0" fill="none" stroke="#7c3aed" strokeWidth="3" strokeLinecap="round" />
          <path d="M56 120l22-34 22 34" fill="none" stroke="#14b8a6" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M46 120h64v27H46z" fill="#ccfbf1" stroke="#14b8a6" strokeWidth="3" />
          <path d="M214 78c8 0 8 12 0 12s-8-12 0-12zM237 101c8 0 8 12 0 12s-8-12 0-12zM224 126c8 0 8 12 0 12s-8-12 0-12z" fill="#a78bfa" className="animate-pulse" />
        </svg>

        <div className="rounded-2xl border border-violet-200/85 bg-white/80 p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.17em] text-violet-500">
            {t('goalSuggestion.timelineTitle')}
          </p>
          <div className="mt-4 space-y-3">
            {timelineProgress.map((progress, index) => (
              <div key={index} className="space-y-1.5">
                <div className="flex items-center justify-between text-xs font-medium text-slate-500">
                  <span>{t('goalSuggestion.monthLabel', { month: index + 1 })}</span>
                  <span className="font-[var(--font-onboarding-mono)] text-slate-600">
                    {Math.round(progress)}%
                  </span>
                </div>
                <div className="h-2 rounded-full bg-violet-100">
                  <div
                    className="h-2 rounded-full bg-gradient-to-r from-violet-500 to-fuchsia-500 transition-all duration-700"
                    style={{ width: `${Math.max(progress, 6)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
          <p className="mt-4 text-xs leading-relaxed text-slate-500">
            {monthlyAvailable > 0 ? t('goalSuggestion.timelineHintPositive') : t('goalSuggestion.timelineHintNegative')}
          </p>
        </div>
      </aside>
    </div>
  );
}
