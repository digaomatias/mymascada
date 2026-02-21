'use client';

import { useTranslations } from 'next-intl';
import { ArrowRightIcon } from '@heroicons/react/24/outline';
import { Button } from '@/components/ui/button';

interface StepCompleteProps {
  countdown: number;
  onGoNow: () => void;
}

export function StepComplete({ countdown, onGoNow }: StepCompleteProps) {
  const t = useTranslations('onboarding');
  const progressPercent = Math.max(0, Math.min((countdown / 3) * 100, 100));

  return (
    <div className="flex flex-col items-center gap-6 py-4 text-center sm:py-6">
      <span className="rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-emerald-700">
        {t('complete.badge')}
      </span>

      <div className="relative">
        <svg viewBox="0 0 140 140" className="h-28 w-28 sm:h-32 sm:w-32" role="img" aria-label="Setup complete">
          <circle cx="70" cy="70" r="56" fill="#ecfdf5" stroke="#a7f3d0" strokeWidth="8" />
          <circle
            cx="70"
            cy="70"
            r="56"
            fill="none"
            stroke="#10b981"
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray="352"
            strokeDashoffset="352"
            className="animate-[onboarding-ring-draw_900ms_ease-out_forwards]"
          />
          <path
            d="M48 72l16 16 30-34"
            fill="none"
            stroke="#059669"
            strokeWidth="8"
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeDasharray="100"
            strokeDashoffset="100"
            className="animate-[onboarding-check-draw_520ms_ease-out_420ms_forwards]"
          />
        </svg>
        <span className="absolute -right-1 top-1.5 h-3 w-3 rounded-full bg-violet-500 shadow-[0_0_0_8px_rgba(139,92,246,0.18)]" />
      </div>

      <div className="space-y-2">
        <h2 className="font-[var(--font-onboarding-sans)] text-4xl font-semibold tracking-[-0.03em] text-slate-900">
          {t('complete.title')}
        </h2>
        <p className="max-w-lg text-sm leading-relaxed text-slate-600 sm:text-base">
          {t('complete.subtitle')}
        </p>
      </div>

      <div className="w-full max-w-[320px] rounded-2xl border border-violet-100 bg-violet-50/80 p-3">
        <p className="text-sm font-medium text-violet-700">
          {t('complete.countdown', { seconds: countdown })}
        </p>
        <div className="mt-2 h-2 rounded-full bg-violet-100">
          <div
            className="h-2 rounded-full bg-gradient-to-r from-violet-500 to-fuchsia-500 transition-all duration-700"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
      </div>

      <Button
        onClick={onGoNow}
        className="rounded-xl bg-violet-600 px-6 text-white shadow-[0_12px_25px_-15px_rgba(124,58,237,1)] hover:bg-violet-700"
      >
        {t('complete.goNow')}
        <ArrowRightIcon className="ml-2 h-4 w-4" />
      </Button>
    </div>
  );
}
