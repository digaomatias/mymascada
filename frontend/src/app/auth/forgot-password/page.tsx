'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
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
      <div className="min-h-screen bg-gradient-to-br from-primary-50 to-primary-100 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
        <div className="max-w-md w-full space-y-8">
          <div className="text-center">
            <div className="flex justify-center mb-6">
              <div className="w-20 h-20 bg-gradient-to-br from-green-500 to-green-700 rounded-2xl shadow-2xl flex items-center justify-center">
                <CheckCircleIcon className="w-12 h-12 text-white" />
              </div>
            </div>
            <h2 className="text-h1 text-slate-900">{t('checkYourEmail')}</h2>
          </div>

          <Card>
            <CardContent className="p-8">
              <div className="text-center space-y-4">
                <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
                  <EnvelopeIcon className="w-8 h-8 text-green-600" />
                </div>

                <p className="text-slate-700">
                  {t('forgotPasswordSentDescription')}
                </p>

                <p className="text-sm text-slate-500">
                  {t('forgotPasswordSpamNote')}
                </p>

                <div className="pt-4 space-y-3">
                  <Link
                    href="/auth/login"
                    className="btn btn-primary w-full justify-center"
                  >
                    {t('backToSignIn')}
                  </Link>

                  <Button
                    variant="outline"
                    className="w-full"
                    onClick={() => {
                      setIsSubmitted(false);
                      setEmail('');
                    }}
                  >
                    {t('tryDifferentEmail')}
                  </Button>
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
    <div className="min-h-screen bg-gradient-to-br from-primary-50 to-primary-100 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <div className="flex justify-center mb-6">
            <div className="w-20 h-20">
              <AppIcon size={80} />
            </div>
          </div>
          <h2 className="text-h1 text-slate-900">{t('forgotPassword')}</h2>
          <p className="mt-2 text-slate-600">
            {t('forgotPasswordHelper')}
          </p>
        </div>

        <Card>
          <CardContent className="p-8">
            <form onSubmit={handleSubmit} className="space-y-6">
              {error && (
                <div className="bg-danger-50 border border-danger-200 rounded-card p-4 flex items-start gap-3">
                  <ExclamationCircleIcon className="w-5 h-5 text-danger-500 flex-shrink-0 mt-0.5" />
                  <p className="text-sm text-danger-700">{error}</p>
                </div>
              )}

              <Input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                label={t('email')}
                placeholder={t('emailExample')}
              />

              <Button
                type="submit"
                className="w-full"
                loading={isLoading}
                disabled={isLoading || !email}
              >
                {t('sendResetLink')}
              </Button>

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
