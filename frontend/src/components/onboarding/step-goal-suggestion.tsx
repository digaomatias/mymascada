'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ShieldCheckIcon, AdjustmentsHorizontalIcon, ArrowRightIcon } from '@heroicons/react/24/outline';
import { CurrencyInput } from '@/components/ui/currency-input';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';

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
  isSubmitting?: boolean;
  accounts?: Array<{ id: number; name: string; currentBalance: number }>;
  linkedAccountId?: number;
  onLinkedAccountChange?: (id: number | undefined) => void;
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
  isSubmitting,
  accounts,
  linkedAccountId,
  onLinkedAccountChange,
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
      return new Intl.NumberFormat('en-NZ', {
        style: 'currency',
        currency: currency,
      }).format(amount);
    } catch {
      return `${currency} ${amount.toFixed(2)}`;
    }
  };

  // Validation: target vs monthly expenses
  const isBelowMinimum = monthlyExpenses > 0 && goalTargetAmount > 0 && goalTargetAmount < monthlyExpenses;
  const isBelowRecommended = monthlyExpenses > 0 && goalTargetAmount >= monthlyExpenses && goalTargetAmount < monthlyExpenses * 3;

  const linkedAccount = accounts?.find(a => a.id === linkedAccountId);

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
                {isBelowMinimum && (
                  <p className="mt-2 text-xs font-medium text-red-600">
                    {t('goalSuggestion.belowMinimum', { amount: formatCurrency(monthlyExpenses) })}
                  </p>
                )}
                {isBelowRecommended && (
                  <p className="mt-2 text-xs font-medium text-amber-600">
                    {t('goalSuggestion.belowRecommended')}
                  </p>
                )}
                <p className={`mt-2 text-xs font-medium ${
                  monthlyAvailable >= 0 ? 'text-emerald-600' : 'text-amber-600'
                }`}>
                  {t('goalSuggestion.available')}: <span className="font-[var(--font-onboarding-mono)]">{formatCurrency(monthlyAvailable)}</span>{t('goalSuggestion.perMonth')}
                </p>
                {linkedAccount && (
                  <p className="mt-2 text-xs font-medium text-emerald-600">
                    {t('goalSuggestion.linkedTo', { accountName: linkedAccount.name })} — {formatCurrency(linkedAccount.currentBalance)} {t('goalSuggestion.existingBalance')}
                  </p>
                )}
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
                error={isBelowMinimum}
                errorMessage={isBelowMinimum ? t('goalSuggestion.belowMinimum', { amount: formatCurrency(monthlyExpenses) }) : undefined}
                className="rounded-xl border-violet-200 bg-white [font-family:var(--font-onboarding-mono)] focus:border-violet-400 focus:ring-violet-200"
              />
              {isBelowRecommended && (
                <p className="text-xs font-medium text-amber-600">
                  {t('goalSuggestion.belowRecommended')}
                </p>
              )}
              {accounts && accounts.length > 0 && (
                <div className="space-y-1.5">
                  <label className="text-sm font-medium text-slate-700">
                    {t('goalSuggestion.linkedAccountLabel')}
                  </label>
                  <Select
                    value={linkedAccountId?.toString() ?? ''}
                    onChange={(e) => {
                      const val = e.target.value;
                      onLinkedAccountChange?.(val ? Number(val) : undefined);
                    }}
                    className="rounded-xl border-violet-200 focus:border-violet-400 focus:ring-violet-200"
                  >
                    <option value="">{t('goalSuggestion.noLinkedAccount')}</option>
                    {accounts.map((account) => (
                      <option key={account.id} value={account.id.toString()}>
                        {account.name}
                      </option>
                    ))}
                  </Select>
                  <p className="text-xs text-slate-500">{t('goalSuggestion.linkedAccountHelp')}</p>
                  {linkedAccount && (
                    <div className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-3 py-1 text-xs font-medium text-emerald-700">
                      {t('goalSuggestion.startingBalance')}: {formatCurrency(linkedAccount.currentBalance)}
                    </div>
                  )}
                </div>
              )}
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
            disabled={!goalName.trim() || goalTargetAmount <= 0 || isBelowMinimum || isSubmitting}
            className="rounded-xl bg-violet-600 px-6 text-white shadow-[0_12px_25px_-15px_rgba(124,58,237,1)] hover:bg-violet-700"
          >
            {isSubmitting ? t('submitting') : t('finish')}
            {!isSubmitting && <ArrowRightIcon className="ml-2 h-4 w-4" />}
          </Button>
        </div>
      </section>

      <aside className="relative overflow-hidden rounded-[24px] border border-violet-100 bg-gradient-to-br from-violet-50 via-white to-teal-50 p-5 sm:p-6">
        <div className="pointer-events-none absolute -bottom-12 -left-16 h-44 w-44 rounded-full bg-teal-200/45 blur-2xl" />
        <svg
          viewBox="0 0 280 190"
          className="h-[160px] w-full"
          role="img"
          aria-label="Financial goal illustration"
        >
          {/* Ground line */}
          <path d="M20 170h240" stroke="#c4b5fd" strokeWidth="2" strokeLinecap="round" />

          {/* Rising steps / staircase — centered in viewBox */}
          <g transform="translate(69,170)">
            {/* Step 1 */}
            <rect x="0" y="-28" width="40" height="28" rx="4" fill="#ede9fe" stroke="#8b5cf6" strokeWidth="2" />
            <text x="20" y="-10" textAnchor="middle" fontSize="11" fontWeight="700" fill="#7c3aed" fontFamily="system-ui">1</text>

            {/* Step 2 */}
            <rect x="48" y="-56" width="40" height="56" rx="4" fill="#ddd6fe" stroke="#8b5cf6" strokeWidth="2" />
            <text x="68" y="-32" textAnchor="middle" fontSize="11" fontWeight="700" fill="#7c3aed" fontFamily="system-ui">2</text>

            {/* Step 3 */}
            <rect x="96" y="-84" width="40" height="84" rx="4" fill="#c4b5fd" stroke="#7c3aed" strokeWidth="2" />
            <text x="116" y="-56" textAnchor="middle" fontSize="11" fontWeight="700" fill="#6d28d9" fontFamily="system-ui">3</text>
          </g>

          {/* Flag on top of step 3 */}
          <g transform="translate(185,62)">
            {/* Flagpole */}
            <path d="M0 24 v-48" stroke="#7c3aed" strokeWidth="2.5" strokeLinecap="round" />
            {/* Flag */}
            <path d="M0-24 l26 10 l-26 10 z" fill="#8b5cf6" stroke="#7c3aed" strokeWidth="1.5" strokeLinejoin="round" />
            {/* Star on flag */}
            <circle cx="12" cy="-14" r="3" fill="#ede9fe" />
          </g>
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
