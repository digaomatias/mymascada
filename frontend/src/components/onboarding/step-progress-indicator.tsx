'use client';

import { useTranslations } from 'next-intl';

interface StepProgressIndicatorProps {
  currentStep: number;
  totalSteps: number;
}

export function StepProgressIndicator({ currentStep, totalSteps }: StepProgressIndicatorProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="w-full">
      <div className="flex items-center justify-between mb-2">
        <span className="text-sm text-gray-500">
          {t('progress.step', { current: currentStep, total: totalSteps })}
        </span>
      </div>
      <div className="flex gap-1.5">
        {Array.from({ length: totalSteps }, (_, i) => (
          <div
            key={i}
            className={`h-1.5 flex-1 rounded-full transition-colors duration-300 ${
              i < currentStep ? 'bg-primary-600' : 'bg-gray-200'
            }`}
          />
        ))}
      </div>
    </div>
  );
}
