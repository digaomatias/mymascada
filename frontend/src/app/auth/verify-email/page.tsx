'use client';

import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { CurrencyDollarIcon, CheckCircleIcon, ExclamationCircleIcon, EnvelopeIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';
import { apiClient } from '@/lib/api-client';

export default function VerifyEmailPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = useTranslations('auth.emailVerification');

  const [status, setStatus] = useState<'loading' | 'success' | 'error' | 'resend'>('loading');
  const [message, setMessage] = useState('');
  const [resendEmail, setResendEmail] = useState('');
  const [isResending, setIsResending] = useState(false);
  const [resendSuccess, setResendSuccess] = useState(false);

  const token = searchParams.get('token');
  const email = searchParams.get('email');

  useEffect(() => {
    const verifyEmail = async () => {
      if (!token || !email) {
        setStatus('resend');
        setMessage(t('missingParams'));
        return;
      }

      try {
        const response = await apiClient.confirmEmail({ email, token });
        if (response.success) {
          setStatus('success');
          setMessage(response.message || t('successMessage'));
        } else {
          setStatus('error');
          setMessage(response.message || t('errorMessage'));
        }
      } catch {
        setStatus('error');
        setMessage(t('errorMessage'));
      }
    };

    verifyEmail();
  }, [token, email, t]);

  const handleResendVerification = async () => {
    if (!resendEmail.trim()) return;

    setIsResending(true);
    try {
      const response = await apiClient.resendVerificationEmail({ email: resendEmail });
      setResendSuccess(true);
      setMessage(response.message || t('resendSuccess'));
    } catch {
      setMessage(t('resendError'));
    } finally {
      setIsResending(false);
    }
  };

  const renderContent = () => {
    switch (status) {
      case 'loading':
        return (
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto mb-6">
              <EnvelopeIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('verifying')}</h2>
            <p className="text-gray-600">{t('pleaseWait')}</p>
          </div>
        );

      case 'success':
        return (
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-green-500 to-green-700 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <CheckCircleIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('successTitle')}</h2>
            <p className="text-gray-600 mb-6">{message}</p>
            <Button onClick={() => router.push('/auth/login')} className="w-full">
              {t('proceedToLogin')}
            </Button>
          </div>
        );

      case 'error':
        return (
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-red-500 to-red-700 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <ExclamationCircleIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('errorTitle')}</h2>
            <p className="text-gray-600 mb-6">{message}</p>

            <div className="space-y-4">
              <p className="text-sm text-gray-500">{t('needNewLink')}</p>
              <Input
                type="email"
                placeholder={t('emailPlaceholder')}
                value={resendEmail}
                onChange={(e) => setResendEmail(e.target.value)}
              />
              <Button
                onClick={handleResendVerification}
                loading={isResending}
                disabled={isResending || !resendEmail.trim()}
                className="w-full"
              >
                {t('resendButton')}
              </Button>
              {resendSuccess && (
                <p className="text-sm text-green-600">{t('resendSuccess')}</p>
              )}
            </div>

            <div className="mt-6">
              <Link href="/auth/login" className="text-sm text-primary-600 hover:text-primary-800">
                {t('backToLogin')}
              </Link>
            </div>
          </div>
        );

      case 'resend':
        return (
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <EnvelopeIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('resendTitle')}</h2>
            <p className="text-gray-600 mb-6">{message}</p>

            <div className="space-y-4">
              <Input
                type="email"
                placeholder={t('emailPlaceholder')}
                value={resendEmail}
                onChange={(e) => setResendEmail(e.target.value)}
              />
              <Button
                onClick={handleResendVerification}
                loading={isResending}
                disabled={isResending || !resendEmail.trim()}
                className="w-full"
              >
                {t('resendButton')}
              </Button>
              {resendSuccess && (
                <p className="text-sm text-green-600">{t('resendSuccess')}</p>
              )}
            </div>

            <div className="mt-6">
              <Link href="/auth/login" className="text-sm text-primary-600 hover:text-primary-800">
                {t('backToLogin')}
              </Link>
            </div>
          </div>
        );
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <div className="flex justify-center mb-6">
            <div className="w-20 h-20 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center">
              <CurrencyDollarIcon className="w-12 h-12 text-white" />
            </div>
          </div>
        </div>

        <Card>
          <CardContent className="p-8">
            {renderContent()}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
