'use client';

import { useEffect } from 'react';
import { useTranslations } from 'next-intl';

interface ErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function GlobalError({ error, reset }: ErrorProps) {
  const t = useTranslations('common');

  useEffect(() => {
    console.error('[ErrorBoundary]', error);
  }, [error]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="max-w-md w-full text-center">
        <div className="mb-6 flex justify-center">
          <div className="w-16 h-16 rounded-full bg-danger-100 flex items-center justify-center">
            <svg
              className="w-8 h-8 text-danger-600"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={1.5}
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"
              />
            </svg>
          </div>
        </div>

        <h1 className="text-2xl font-semibold text-gray-900 mb-2">
          {t('error')}
        </h1>
        <p className="text-gray-500 mb-8">
          {error.message || 'An unexpected error occurred. Please try again.'}
        </p>

        <button
          onClick={reset}
          className="btn btn-primary"
        >
          {t('retry')}
        </button>
      </div>
    </div>
  );
}
