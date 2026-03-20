'use client';

import { useTranslations } from 'next-intl';

interface StepProgressIndicatorProps {
  currentStep: number;
  totalSteps: number;
}

export function StepProgressIndicator({ currentStep, totalSteps }: StepProgressIndicatorProps) {
  const t = useTranslations('onboarding');
  const completionPercent = Math.round((currentStep / totalSteps) * 100);

  return (
    <div className="w-full rounded-2xl border border-ink-200 bg-white/70 px-4 py-3 backdrop-blur-sm">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-sm font-semibold tracking-[0.12em] text-primary-700/90">
          {t('progress.step', { current: currentStep, total: totalSteps })}
        </span>
        <span className="font-[var(--font-onboarding-mono)] text-xs font-medium text-primary-500">
          {completionPercent}%
        </span>
      </div>
      <div className="flex gap-2">
        {Array.from({ length: totalSteps }, (_, i) => (
          <div key={i} className="relative h-2.5 flex-1 overflow-hidden rounded-full bg-primary-100">
            {i < currentStep && (
              <div className="absolute inset-0 bg-gradient-to-r from-primary-500 via-primary-500 to-primary-600" />
            )}
            {i === currentStep - 1 && (
              <div className="absolute inset-0 -translate-x-full bg-gradient-to-r from-transparent via-white/70 to-transparent animate-[onboarding-shimmer_1.8s_linear_infinite]" />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
