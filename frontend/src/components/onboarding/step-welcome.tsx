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
            viewBox="0 0 760 200"
            className="h-[180px] w-full animate-[onboarding-float_7s_ease-in-out_infinite]"
            role="img"
            aria-label="Financial setup illustration"
          >
            <defs>
              <linearGradient id="welcome-link-path" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" stopColor="#c4b5fd" />
                <stop offset="100%" stopColor="#7c3aed" />
              </linearGradient>
            </defs>
            {/* Journey path — drawn behind the icons */}
            <path
              d="M80 140 C170 50 260 40 330 100 C390 146 450 146 580 88 C625 58 665 46 710 46"
              fill="none"
              stroke="url(#welcome-link-path)"
              strokeWidth="3"
              strokeLinecap="round"
            />

            {/* 1) Dollar coin — at start of path */}
            <g transform="translate(80,140)">
              <circle r="30" fill="#ffffff" stroke="#ede9fe" strokeWidth="2.5" />
              <circle r="14" fill="#ede9fe" stroke="#7c3aed" strokeWidth="2" />
              <text x="0" y="5" textAnchor="middle" fontSize="16" fontWeight="700" fill="#7c3aed" fontFamily="system-ui">$</text>
            </g>

            {/* 2) Receipt — peak 1 */}
            <g transform="translate(250,62)">
              <circle r="30" fill="#ffffff" stroke="#ede9fe" strokeWidth="2.5" />
              <path d="M-11-14h22v28l-3.5-2.5-3.5 2.5-3.5-2.5-3.5 2.5-3.5-2.5-3.5 2.5V-14z" fill="#ede9fe" stroke="#7c3aed" strokeWidth="2" strokeLinejoin="round" />
              <path d="M-6-5h12M-6 1h12" stroke="#7c3aed" strokeWidth="2" strokeLinecap="round" />
            </g>

            {/* 3) Shield — valley */}
            <g transform="translate(420,146)">
              <circle r="30" fill="#ffffff" stroke="#ede9fe" strokeWidth="2.5" />
              <path d="M0-14l14 5v10c0 8-5.5 15-14 19-8.5-4-14-11-14-19V-9z" fill="#ede9fe" stroke="#7c3aed" strokeWidth="2" strokeLinejoin="round" />
              <path d="M-5 2l4 4 9-9" stroke="#7c3aed" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" fill="none" />
            </g>

            {/* 4) Bar chart / growth — rising */}
            <g transform="translate(586,86)">
              <circle r="30" fill="#ffffff" stroke="#ede9fe" strokeWidth="2.5" />
              <rect x="-13" y="2" width="7" height="12" rx="1.5" fill="#ede9fe" stroke="#7c3aed" strokeWidth="2" />
              <rect x="-3" y="-4" width="7" height="18" rx="1.5" fill="#ede9fe" stroke="#7c3aed" strokeWidth="2" />
              <rect x="7" y="-10" width="7" height="24" rx="1.5" fill="#ede9fe" stroke="#7c3aed" strokeWidth="2" />
              <path d="M-12-3 L-6-10 L0-7 L6-13 L12-18" stroke="#7c3aed" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" fill="none" />
              <circle cx="12" cy="-18" r="2.5" fill="#7c3aed" />
            </g>

            {/* 5) Goal flag — end */}
            <g transform="translate(710,46)">
              <circle r="30" fill="#ffffff" stroke="#ede9fe" strokeWidth="2.5" />
              <path d="M-5-12v26" stroke="#7c3aed" strokeWidth="2.5" strokeLinecap="round" />
              <path d="M-5-12h18l-4.5 6 4.5 6H-5z" fill="#ede9fe" stroke="#7c3aed" strokeWidth="2" strokeLinejoin="round" />
              <path d="M-10 14h14" stroke="#7c3aed" strokeWidth="2" strokeLinecap="round" />
            </g>
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
