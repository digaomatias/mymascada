'use client';

import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';

interface StepWelcomeProps {
  onNext: () => void;
}

export function StepWelcome({ onNext }: StepWelcomeProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="flex flex-col items-center text-center space-y-6 py-8">
      <div className="text-6xl">🦌</div>
      <div className="space-y-2">
        <h1 className="text-2xl font-bold text-gray-900">
          {t('welcome.title')}
        </h1>
        <p className="text-gray-600 max-w-md">
          {t('welcome.subtitle')}
        </p>
      </div>
      <Button onClick={onNext} size="lg">
        {t('welcome.cta')}
      </Button>
    </div>
  );
}
