'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/contexts/auth-context';
import { LoginRequest } from '@/types/auth';
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
  const [isAccountLocked, setIsAccountLocked] = useState(false);
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
    setIsAccountLocked(false);
    setResendSuccess(false);

    if (!validateForm()) {
      return;
    }

    const response = await login(formData);

    if (response.isSuccess) {
      router.push('/dashboard');
    } else if (response.requiresEmailVerification) {
      setRequiresVerification(true);
    } else if (response.isAccountLocked) {
      setIsAccountLocked(true);
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

    if (!hasInteracted) {
      setHasInteracted(true);
    }

    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }));

    if (validationErrors[name]) {
      setValidationErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[name];
        return newErrors;
      });
    }
  };

  return (
    <div className="min-h-dvh flex flex-col lg:flex-row bg-surface-alt">
      {/* ── Brand panel — visible on desktop ── */}
      <div className="hidden lg:flex lg:w-[45%] xl:w-[48%] flex-col justify-between bg-surface-brand p-10 xl:p-14">
        {/* Top: logo + name */}
        <div className="flex items-center gap-3">
          <AppIcon size={32} />
          <span className="text-[15px] font-semibold tracking-[-0.02em] text-ink-800">
            MyMascada
          </span>
        </div>

        {/* Center: brand headline */}
        <div className="max-w-md">
          <h1 className="font-display text-[2.5rem] xl:text-[2.85rem] leading-[1.08] tracking-[-0.035em] text-ink-950">
            {t('brandHeading')}
          </h1>
          <p className="mt-5 text-[15px] leading-relaxed text-ink-500 max-w-sm">
            {t('brandDescription')}
          </p>
        </div>

        {/* Bottom: legal links */}
        <div className="flex items-center gap-4 text-xs text-ink-400">
          <Link href="/terms" className="hover:text-ink-600 transition-colors">{t('termsLink')}</Link>
          <span aria-hidden>·</span>
          <Link href="/privacy" className="hover:text-ink-600 transition-colors">{t('privacyLink')}</Link>
        </div>
      </div>

      {/* ── Form panel ── */}
      <div className="flex-1 flex flex-col items-center justify-center px-6 py-10 sm:px-10 lg:px-16">
        <div className="w-full max-w-[400px]">
          {/* Mobile: logo + heading (stagger 0) */}
          <div
            className="lg:hidden mb-10 flex flex-col items-center"
            style={{ animation: 'fadeInUp 600ms cubic-bezier(0.16, 1, 0.3, 1) both' }}
          >
            <AppIcon size={56} className="mb-4" />
            <h1 className="font-display text-[1.75rem] tracking-[-0.03em] text-center text-ink-950">
              {t('signInTitle')}
            </h1>
            <p className="mt-1.5 text-sm text-ink-500">
              {t('signInSubtitle')}
            </p>
          </div>

          {/* Desktop: heading only (stagger 0) */}
          <div
            className="hidden lg:block mb-8"
            style={{ animation: 'fadeInUp 600ms cubic-bezier(0.16, 1, 0.3, 1) both' }}
          >
            <h2 className="text-xl font-semibold tracking-[-0.02em] text-ink-900">
              {t('signInTitle')}
            </h2>
            <p className="mt-1.5 text-sm text-ink-500">
              {t('signInSubtitle')}
            </p>
          </div>

          {/* ── Error / verification states ── */}
          {requiresVerification && (
            <div className="mb-5 rounded-xl border border-warning-200 bg-warning-50 p-4">
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
                      <span className="text-sm text-success-600">
                        {tVerify('resendSuccess')}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {isAccountLocked && (
            <div className="mb-5 rounded-xl border border-danger-200 bg-danger-50 p-4">
              <div className="flex items-start gap-3">
                <ExclamationCircleIcon className="w-5 h-5 text-danger-600 mt-0.5 flex-shrink-0" />
                <div className="flex-1">
                  <h3 className="text-sm font-medium text-danger-800 mb-1">
                    {t('errors.accountLocked')}
                  </h3>
                  <p className="text-sm text-danger-700">
                    {t('errors.accountLockedDetail')}
                  </p>
                </div>
              </div>
            </div>
          )}

          {errors.length > 0 && !requiresVerification && !isAccountLocked && (
            <div className="mb-5 rounded-xl border border-danger-200 bg-danger-50 p-4">
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

          {/* ── Form (stagger 1) ── */}
          <form
            className="space-y-5"
            onSubmit={handleSubmit}
            noValidate
            style={{ animation: 'fadeInUp 600ms 80ms cubic-bezier(0.16, 1, 0.3, 1) both' }}
          >
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
                  className="h-4 w-4 accent-primary border-ink-300 rounded-sm focus:outline-hidden focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2"
                />
                <label htmlFor="rememberMe" className="ml-2 block text-sm text-ink-600">
                  {t('rememberMe')}
                </label>
              </div>

              <Link href="/auth/forgot-password" className="text-sm font-medium text-primary hover:text-primary-700 transition-colors">
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

          {/* ── OAuth + sign up (stagger 2) ── */}
          <div
            className="space-y-5 mt-6"
            style={{ animation: 'fadeInUp 600ms 160ms cubic-bezier(0.16, 1, 0.3, 1) both' }}
          >
            {features.googleOAuth && (
              <>
                <div className="relative">
                  <div className="absolute inset-0 flex items-center">
                    <div className="w-full border-t border-ink-200"></div>
                  </div>
                  <div className="relative flex justify-center text-xs">
                    <span className="px-3 bg-surface-alt text-ink-400">{t('orContinueWith')}</span>
                  </div>
                </div>

                <GoogleSignInButton
                  onError={(error) => setErrors([error])}
                />
              </>
            )}

            <p className="text-center text-sm text-ink-500">
              {t('noAccount')}{' '}
              <Link href="/auth/register" className="font-medium text-primary hover:text-primary-700 transition-colors">
                {t('signUp')}
              </Link>
            </p>
          </div>

          {/* Mobile: legal links */}
          <div className="lg:hidden mt-8 text-center text-xs text-ink-400">
            <Link href="/terms" className="hover:text-ink-600 transition-colors">{t('termsLink')}</Link>
            {' · '}
            <Link href="/privacy" className="hover:text-ink-600 transition-colors">{t('privacyLink')}</Link>
          </div>
        </div>
      </div>
    </div>
  );
}

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';
