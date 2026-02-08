'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Card, CardContent } from '@/components/ui/card';
import { EnvelopeIcon, CheckCircleIcon, ExclamationCircleIcon } from '@heroicons/react/24/outline';
import { AppIcon } from '@/components/app-icon';
import { apiClient } from '@/lib/api-client';
import { useTranslations } from 'next-intl';

export default function ForgotPasswordPage() {
  const t = useTranslations('auth');
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.forgotPassword({ email });

      if (response.isSuccess) {
        setIsSubmitted(true);
      } else {
        // Even on validation errors, we show success to prevent enumeration
        setIsSubmitted(true);
      }
    } catch {
      // For network errors, show a generic message
      setError(t('errors.resetRequestFailed'));
    } finally {
      setIsLoading(false);
    }
  };

  // Success state after submission
  if (isSubmitted) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
        <div className="max-w-md w-full space-y-8">
          <div className="text-center">
            <div className="flex justify-center mb-6">
              <div className="w-20 h-20 bg-gradient-to-br from-green-500 to-green-700 rounded-2xl shadow-2xl flex items-center justify-center">
                <CheckCircleIcon className="w-12 h-12 text-white" />
              </div>
            </div>
            <h2 className="text-h1 text-gray-900">{t('checkYourEmail')}</h2>
          </div>

          <Card>
            <CardContent className="p-8">
              <div className="text-center space-y-4">
                <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
                  <EnvelopeIcon className="w-8 h-8 text-green-600" />
                </div>

                <p className="text-gray-700">
                  {t('forgotPasswordSentDescription')}
                </p>

                <p className="text-sm text-gray-500">
                  {t('forgotPasswordSpamNote')}
                </p>

                <div className="pt-4 space-y-3">
                  <Link
                    href="/auth/login"
                    className="inline-block w-full px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary-600 transition-colors text-center"
                  >
                    {t('backToSignIn')}
                  </Link>

                  <button
                    onClick={() => {
                      setIsSubmitted(false);
                      setEmail('');
                    }}
                    className="inline-block w-full px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition-colors"
                  >
                    {t('tryDifferentEmail')}
                  </button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  // Initial form state
  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <div className="flex justify-center mb-6">
            <div className="w-20 h-20">
              <AppIcon size={80} />
            </div>
          </div>
          <h2 className="text-h1 text-gray-900">{t('forgotPassword')}</h2>
          <p className="mt-2 text-gray-600">
            {t('forgotPasswordHelper')}
          </p>
        </div>

        <Card>
          <CardContent className="p-8">
            <form onSubmit={handleSubmit} className="space-y-6">
              {error && (
                <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-start gap-3">
                  <ExclamationCircleIcon className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
                  <p className="text-sm text-red-700">{error}</p>
                </div>
              )}

              <div>
                <label htmlFor="email" className="block text-sm font-medium text-gray-700 mb-1">
                  {t('email')}
                </label>
                <input
                  id="email"
                  name="email"
                  type="email"
                  autoComplete="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary focus:border-transparent"
                  placeholder={t('emailExample')}
                />
              </div>

              <button
                type="submit"
                disabled={isLoading || !email}
                className="w-full px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center"
              >
                {isLoading ? (
                  <>
                    <svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    {t('sending')}
                  </>
                ) : (
                  t('sendResetLink')
                )}
              </button>

              <div className="text-center">
                <Link
                  href="/auth/login"
                  className="text-sm text-primary hover:text-primary-600"
                >
                  {t('backToSignIn')}
                </Link>
              </div>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
