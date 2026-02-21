'use client';

import { useTranslations } from 'next-intl';
import { ArrowRightIcon } from '@heroicons/react/24/outline';
import { Button } from '@/components/ui/button';

interface StepWelcomeProps {
  onNext: () => void;
}

export function StepWelcome({ onNext }: StepWelcomeProps) {
  const t = useTranslations('onboarding');
  const trustSignals = [
    t('welcome.trustPrivate'),
    t('welcome.trustEncrypted'),
    t('welcome.trustEditable'),
  ];

  return (
    <div className="flex flex-col items-center gap-8 py-2 sm:py-5">
      <div className="relative w-full overflow-hidden rounded-[28px] border border-violet-100 bg-gradient-to-br from-violet-50 via-white to-fuchsia-50 px-5 py-6 sm:px-8 sm:py-7">
        <div className="pointer-events-none absolute -right-16 -top-20 h-48 w-48 rounded-full bg-violet-200/55 blur-2xl" />
        <div className="pointer-events-none absolute -bottom-20 -left-16 h-44 w-44 rounded-full bg-fuchsia-200/40 blur-2xl" />
        <div className="relative">
          <p className="mb-4 text-xs font-semibold uppercase tracking-[0.2em] text-violet-500">
            {t('welcome.badge')}
          </p>
          <svg
            viewBox="0 0 760 250"
            className="h-[190px] w-full animate-[onboarding-float_7s_ease-in-out_infinite]"
            role="img"
            aria-label="Financial setup illustration"
          >
            <defs>
              <linearGradient id="welcome-link" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stopColor="#a78bfa" />
                <stop offset="100%" stopColor="#7c3aed" />
              </linearGradient>
            </defs>
            <path
              d="M82 180 C170 80 255 72 338 132 C412 186 495 186 582 120 C627 86 666 74 712 74"
              fill="none"
              stroke="url(#welcome-link)"
              strokeWidth="4"
              strokeLinecap="round"
            />
            {[
              { cx: 80, cy: 180 },
              { cx: 246, cy: 96 },
              { cx: 414, cy: 184 },
              { cx: 582, cy: 120 },
              { cx: 712, cy: 74 },
            ].map((node, index) => (
              <g key={`${node.cx}-${node.cy}`}>
                <circle
                  cx={node.cx}
                  cy={node.cy}
                  r="16"
                  fill="#ffffff"
                  stroke="#7c3aed"
                  strokeWidth="3"
                  className={index % 2 === 0 ? 'animate-pulse' : undefined}
                />
                <circle cx={node.cx} cy={node.cy} r="5" fill="#7c3aed" />
              </g>
            ))}
            <rect x="52" y="194" width="56" height="34" rx="10" fill="#ede9fe" stroke="#7c3aed" strokeWidth="3" />
            <path d="M64 206h32M64 216h22" stroke="#7c3aed" strokeWidth="3" strokeLinecap="round" />
            <rect x="222" y="114" width="52" height="34" rx="9" fill="#ede9fe" stroke="#7c3aed" strokeWidth="3" />
            <path d="M232 122h32M232 130h20" stroke="#7c3aed" strokeWidth="3" strokeLinecap="round" />
            <circle cx="412" cy="207" r="24" fill="#ede9fe" stroke="#7c3aed" strokeWidth="3" />
            <path d="M404 206l6 6 12-13" stroke="#7c3aed" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
            <path d="M564 144l18-28 18 28" fill="none" stroke="#7c3aed" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
            <rect x="555" y="144" width="54" height="28" rx="8" fill="#ede9fe" stroke="#7c3aed" strokeWidth="3" />
            <path d="M699 96l13-15 13 15v19h-26z" fill="#ede9fe" stroke="#7c3aed" strokeWidth="3" strokeLinejoin="round" />
            <path d="M707 114h10" stroke="#7c3aed" strokeWidth="3" strokeLinecap="round" />
          </svg>
        </div>
      </div>

      <div className="max-w-2xl space-y-3 text-center">
        <h1 className="font-[var(--font-onboarding-sans)] text-4xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-5xl">
          {t('welcome.title')}
        </h1>
        <p className="mx-auto max-w-xl text-base leading-relaxed text-slate-600 sm:text-lg">
          {t('welcome.subtitle')}
        </p>
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-violet-500/80">
          {t('welcome.duration')}
        </p>
      </div>

      <Button
        onClick={onNext}
        size="lg"
        className="h-12 rounded-xl bg-gradient-to-r from-violet-600 via-violet-500 to-fuchsia-500 px-8 text-base font-semibold text-white shadow-[0_14px_30px_-16px_rgba(124,58,237,0.9)] transition-all duration-300 hover:-translate-y-0.5 hover:shadow-[0_18px_36px_-15px_rgba(124,58,237,0.95)] focus-visible:ring-violet-300"
      >
        {t('welcome.cta')}
        <ArrowRightIcon className="h-4 w-4" />
      </Button>

      <div className="flex flex-wrap items-center justify-center gap-2.5">
        {trustSignals.map((signal) => (
          <span
            key={signal}
            className="rounded-full border border-violet-200 bg-white/80 px-3 py-1.5 text-xs font-medium text-violet-700"
          >
            {signal}
          </span>
        ))}
      </div>
    </div>
  );
}
