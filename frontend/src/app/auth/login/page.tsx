'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/contexts/auth-context';
import { LoginRequest } from '@/types/auth';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { EnvelopeIcon, ExclamationCircleIcon } from '@heroicons/react/24/outline';
import { AppIcon } from '@/components/app-icon';
import { GoogleSignInButton } from '@/components/auth/google-signin-button';
import { useFeatures } from '@/contexts/features-context';
import { useTranslations } from 'next-intl';
import { apiClient } from '@/lib/api-client';

export default function LoginPage() {
  const router = useRouter();
  const { login, isLoading } = useAuth();
  const { features } = useFeatures();
  const t = useTranslations('auth');
  const tVerify = useTranslations('auth.emailVerification');
  const [formData, setFormData] = useState<LoginRequest>({
    emailOrUserName: '',
    password: '',
    rememberMe: false,
  });
  const [errors, setErrors] = useState<string[]>([]);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [hasInteracted, setHasInteracted] = useState(false);
  const [requiresVerification, setRequiresVerification] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const [resendSuccess, setResendSuccess] = useState(false);

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!formData.emailOrUserName.trim()) {
      newErrors.emailOrUserName = t('errors.emailRequired');
    } else if (!formData.emailOrUserName.match(/^[^\s@]+@[^\s@]+\.[^\s@]+$/)) {
      newErrors.emailOrUserName = t('errors.emailInvalid');
    }

    if (!formData.password) {
      newErrors.password = t('errors.passwordRequired');
    }

    setValidationErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const validateField = (name: string, value: string) => {
    // Only validate if user has interacted with the form
    if (!hasInteracted) return;

    const newErrors = { ...validationErrors };

    if (name === 'emailOrUserName') {
      if (!value.trim()) {
        newErrors.emailOrUserName = t('errors.emailRequired');
      } else if (!value.match(/^[^\s@]+@[^\s@]+\.[^\s@]+$/)) {
        newErrors.emailOrUserName = t('errors.emailInvalid');
      } else {
        delete newErrors.emailOrUserName;
      }
    }

    setValidationErrors(newErrors);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors([]);
    setRequiresVerification(false);
    setResendSuccess(false);

    if (!validateForm()) {
      return;
    }

    const response = await login(formData);

    if (response.isSuccess) {
      router.push('/dashboard');
    } else if (response.requiresEmailVerification) {
      setRequiresVerification(true);
    } else {
      setErrors(response.errors);
    }
  };

  const handleResendVerification = async () => {
    setIsResending(true);
    setResendSuccess(false);
    try {
      await apiClient.resendVerificationEmail({ email: formData.emailOrUserName });
      setResendSuccess(true);
    } catch {
      setErrors([tVerify('resendError')]);
    } finally {
      setIsResending(false);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value, type, checked } = e.target;

    // Mark that user has interacted with the form
    if (!hasInteracted) {
      setHasInteracted(true);
    }

    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }));

    // Clear validation error for this field
    if (validationErrors[name]) {
      setValidationErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[name];
        return newErrors;
      });
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-50 to-primary-100 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <div className="flex justify-center mb-6">
            <div className="w-20 h-20">
              <AppIcon size={80} />
            </div>
          </div>
          <h2 className="text-h1 text-slate-900">
            {t('signInTitle')}
          </h2>
          <p className="mt-2 text-slate-600">
            {t('signInSubtitle')}
          </p>
        </div>

        <Card>
          <CardContent className="p-8">
            <form className="space-y-6" onSubmit={handleSubmit} noValidate>
              {requiresVerification && (
                <div className="bg-warning-50 border border-warning-200 rounded-card p-4">
                  <div className="flex items-start gap-3">
                    <ExclamationCircleIcon className="w-5 h-5 text-warning-600 mt-0.5 flex-shrink-0" />
                    <div className="flex-1">
                      <h3 className="text-sm font-medium text-warning-800 mb-1">
                        {tVerify('verificationRequired')}
                      </h3>
                      <p className="text-sm text-warning-700 mb-3">
                        {tVerify('pleaseVerifyEmail')}
                      </p>
                      <div className="flex items-center gap-2">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={handleResendVerification}
                          loading={isResending}
                          disabled={isResending}
                        >
                          <EnvelopeIcon className="w-4 h-4 mr-1" />
                          {tVerify('resendButton')}
                        </Button>
                        {resendSuccess && (
                          <span className="text-sm text-green-600">
                            {tVerify('resendSuccess')}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {errors.length > 0 && !requiresVerification && (
                <div className="bg-danger-50 border border-danger-200 rounded-card p-4">
                  <h3 className="text-sm font-medium text-danger-800 mb-2">
                    {t('errors.genericError')}
                  </h3>
                  <ul className="list-disc pl-5 space-y-1">
                    {errors.map((error, index) => (
                      <li key={index} className="text-sm text-danger-700">{error}</li>
                    ))}
                  </ul>
                </div>
              )}

              <div>
                <Input
                  id="emailOrUserName"
                  name="emailOrUserName"
                  type="text"
                  value={formData.emailOrUserName}
                  onChange={handleInputChange}
                  onBlur={(e) => validateField('emailOrUserName', e.target.value)}
                  label={t('email')}
                  placeholder={t('emailPlaceholder')}
                  error={!!validationErrors.emailOrUserName}
                  errorMessage={validationErrors.emailOrUserName}
                />
              </div>

              <div>
                <Input
                  id="password"
                  name="password"
                  type="password"
                  value={formData.password}
                  onChange={handleInputChange}
                  label={t('password')}
                  placeholder={t('passwordPlaceholder')}
                  error={!!validationErrors.password}
                  errorMessage={validationErrors.password}
                />
              </div>

              <div className="flex items-center justify-between">
                <div className="flex items-center">
                  <input
                    id="rememberMe"
                    name="rememberMe"
                    type="checkbox"
                    checked={formData.rememberMe}
                    onChange={handleInputChange}
                    className="h-4 w-4 text-primary border-slate-300 rounded-sm focus:ring-primary"
                  />
                  <label htmlFor="rememberMe" className="ml-2 block text-sm text-slate-700">
                    {t('rememberMe')}
                  </label>
                </div>

                <Link href="/auth/forgot-password" className="text-sm font-medium text-primary hover:text-primary-600">
                  {t('forgotPassword')}
                </Link>
              </div>

              <Button
                type="submit"
                className="w-full"
                loading={isLoading}
                disabled={isLoading}
              >
                {t('signIn')}
              </Button>
            </form>

            <div className="space-y-6 mt-6">
              {features.googleOAuth && (
                <>
                  <div className="relative">
                    <div className="absolute inset-0 flex items-center">
                      <div className="w-full border-t border-slate-200"></div>
                    </div>
                    <div className="relative flex justify-center text-sm">
                      <span className="px-2 bg-white text-slate-500">{t('orContinueWith')}</span>
                    </div>
                  </div>

                  <GoogleSignInButton
                    onError={(error) => setErrors([error])}
                  />
                </>
              )}

              <div className="text-center">
                <span className="text-sm text-slate-600">
                  {t('noAccount')}{' '}
                  <Link href="/auth/register" className="font-medium text-primary hover:text-primary-600">
                    {t('signUp')}
                  </Link>
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="text-center text-xs text-slate-500">
          <Link href="/terms" className="hover:text-slate-700 underline">{t('termsLink')}</Link>
          {' · '}
          <Link href="/privacy" className="hover:text-slate-700 underline">{t('privacyLink')}</Link>
        </div>
      </div>
    </div>
  );
}

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';
