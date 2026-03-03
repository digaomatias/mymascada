'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import Link from 'next/link';

const STORAGE_KEY = 'cookie_consent_accepted';
const CONSENT_VALUE = '1';

export function CookieConsent() {
  const t = useTranslations('cookieConsent');
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    try {
      if (!localStorage.getItem(STORAGE_KEY)) {
        setVisible(true);
      }
    } catch {
      // localStorage may be unavailable (e.g. SSR guard or private mode)
    }
  }, []);

  const handleAccept = () => {
    try {
      localStorage.setItem(STORAGE_KEY, CONSENT_VALUE);
    } catch {
      // ignore
    }
    setVisible(false);
  };

  if (!visible) return null;

  return (
    <div
      role="dialog"
      aria-live="polite"
      aria-label={t('ariaLabel')}
      className="fixed bottom-4 left-1/2 -translate-x-1/2 z-50 w-[calc(100%-2rem)] max-w-xl"
    >
      <div className="flex flex-col sm:flex-row items-start sm:items-center gap-3 rounded-2xl border border-slate-200 bg-white/95 backdrop-blur-sm shadow-lg px-5 py-4">
        <p className="flex-1 text-sm text-slate-600 leading-relaxed">
          {t('message')}{' '}
          <Link
            href="/privacy"
            className="font-medium text-violet-600 underline underline-offset-2 hover:text-violet-700 transition-colors"
          >
            {t('learnMore')}
          </Link>
        </p>
        <button
          onClick={handleAccept}
          className="shrink-0 rounded-xl bg-violet-600 px-5 py-2 text-sm font-semibold text-white shadow-sm hover:bg-violet-700 active:bg-violet-800 transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-violet-600"
        >
          {t('accept')}
        </button>
      </div>
    </div>
  );
}
