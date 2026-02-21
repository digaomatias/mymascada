'use client';

import { useTranslations } from 'next-intl';
import { SparklesIcon } from '@heroicons/react/24/solid';
import { ArrowRightIcon } from '@heroicons/react/24/outline';
import { Button } from '@/components/ui/button';

interface StepWelcomeProps {
  onNext: () => void;
}

export function StepWelcome({ onNext }: StepWelcomeProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="flex flex-col items-center text-center space-y-6 py-8">
      <div className="w-24 h-24 bg-gradient-to-br from-primary-400 to-primary-600 rounded-3xl shadow-lg shadow-primary-500/25 flex items-center justify-center">
        <SparklesIcon className="w-12 h-12 text-white" />
      </div>

      <div className="space-y-3">
        <p className="text-xs font-semibold uppercase tracking-widest text-primary-600">
          {t('welcome.title').split('!')[0]}
        </p>
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">
          {t('welcome.title')}
        </h1>
        <p className="text-base text-slate-600 leading-relaxed max-w-md">
          {t('welcome.subtitle')}
        </p>
      </div>

      <Button onClick={onNext} size="lg" className="gap-2">
        {t('welcome.cta')}
        <ArrowRightIcon className="w-4 h-4" />
      </Button>
    </div>
  );
}
