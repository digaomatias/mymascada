'use client';

import { useState, useEffect, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { Card, CardContent } from '@/components/ui/card';
import { CheckCircleIcon, ExclamationCircleIcon, EyeIcon, EyeSlashIcon, XCircleIcon } from '@heroicons/react/24/outline';
import { AppIcon } from '@/components/app-icon';
import { apiClient } from '@/lib/api-client';
import { useTranslations } from 'next-intl';

function ResetPasswordForm() {
  const searchParams = useSearchParams();
  const t = useTranslations('auth');

  const [formData, setFormData] = useState({
    email: '',
    token: '',
    newPassword: '',
    confirmPassword: '',
  });
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [isSuccess, setIsSuccess] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  // Extract token and email from URL on mount
  useEffect(() => {
    const token = searchParams.get('token') || '';
    const email = searchParams.get('email') || '';

    setFormData((prev) => ({
      ...prev,
      token,
      email,
    }));
  }, [searchParams]);

  // Password validation
  const validatePassword = (password: string): Record<string, boolean> => {
    return {
      minLength: password.length >= 8,
      hasUppercase: /[A-Z]/.test(password),
      hasLowercase: /[a-z]/.test(password),
      hasNumber: /\d/.test(password),
      hasSpecial: /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password),
    };
  };

  const passwordChecks = validatePassword(formData.newPassword);
  const allPasswordChecksPassing = Object.values(passwordChecks).every(Boolean);
  const passwordsMatch = formData.newPassword === formData.confirmPassword && formData.confirmPassword.length > 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setErrors([]);
    setValidationErrors({});

    // Client-side validation
    const newValidationErrors: Record<string, string> = {};

    if (!formData.email) {
      newValidationErrors.email = t('errors.emailRequired');
    }

    if (!formData.token) {
      newValidationErrors.token = t('errors.resetTokenMissing');
    }

    if (!allPasswordChecksPassing) {
      newValidationErrors.newPassword = t('errors.passwordRequirements');
    }

    if (!passwordsMatch) {
      newValidationErrors.confirmPassword = t('errors.passwordsNotMatch');
    }

    if (Object.keys(newValidationErrors).length > 0) {
      setValidationErrors(newValidationErrors);
      setIsLoading(false);
      return;
    }

    try {
      const response = await apiClient.resetPassword({
        email: formData.email,
        token: formData.token,
        newPassword: formData.newPassword,
        confirmPassword: formData.confirmPassword,
      });

      if (response.isSuccess) {
        setIsSuccess(true);
      } else {
        setErrors(response.errors || [t('errors.resetFailed')]);
      }
    } catch (err: unknown) {
      const error = err as Error & { authResponse?: { errors?: string[] } };
      if (error.authResponse?.errors) {
        setErrors(error.authResponse.errors);
      } else {
        setErrors([t('errors.unexpected')]);
      }
    } finally {
      setIsLoading(false);
    }
  };

  // Success state
  if (isSuccess) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
        <div className="max-w-md w-full space-y-8">
          <div className="text-center">
            <div className="flex justify-center mb-6">
              <div className="w-20 h-20 bg-gradient-to-br from-green-500 to-green-700 rounded-2xl shadow-2xl flex items-center justify-center">
                <CheckCircleIcon className="w-12 h-12 text-white" />
              </div>
            </div>
            <h2 className="text-h1 text-gray-900">{t('passwordResetSuccessTitle')}</h2>
          </div>

          <Card>
            <CardContent className="p-8">
              <div className="text-center space-y-4">
                <p className="text-gray-700">
                  {t('passwordResetSuccessDescription')}
                </p>

                <p className="text-sm text-gray-500">
                  {t('passwordResetSecurityNote')}
                </p>

                <div className="pt-4">
                  <Link
                    href="/auth/login"
                    className="inline-block w-full px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary-600 transition-colors text-center"
                  >
                    {t('signInNow')}
                  </Link>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  // Missing token/email state
  if (!formData.token || !formData.email) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
        <div className="max-w-md w-full space-y-8">
          <div className="text-center">
            <div className="flex justify-center mb-6">
              <div className="w-20 h-20 bg-gradient-to-br from-red-500 to-red-700 rounded-2xl shadow-2xl flex items-center justify-center">
                <XCircleIcon className="w-12 h-12 text-white" />
              </div>
            </div>
            <h2 className="text-h1 text-gray-900">{t('invalidResetLinkTitle')}</h2>
          </div>

          <Card>
            <CardContent className="p-8">
              <div className="text-center space-y-4">
                <p className="text-gray-700">
                  {t('invalidResetLinkDescription')}
                </p>

                <div className="pt-4 space-y-3">
                  <Link
                    href="/auth/forgot-password"
                    className="inline-block w-full px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary-600 transition-colors text-center"
                  >
                    {t('requestNewResetLink')}
                  </Link>

                  <Link
                    href="/auth/login"
                    className="inline-block w-full px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition-colors text-center"
                  >
                    {t('backToSignIn')}
                  </Link>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  // Form state
  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <div className="flex justify-center mb-6">
            <div className="w-20 h-20">
              <AppIcon size={80} />
            </div>
          </div>
          <h2 className="text-h1 text-gray-900">{t('resetPasswordTitle')}</h2>
          <p className="mt-2 text-gray-600">
            {t('resetPasswordSubtitle')}
          </p>
        </div>

        <Card>
          <CardContent className="p-8">
            <form onSubmit={handleSubmit} className="space-y-6">
              {errors.length > 0 && (
                <div className="bg-red-50 border border-red-200 rounded-lg p-4">
                  <div className="flex items-start gap-3">
                    <ExclamationCircleIcon className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
                    <div>
                      {errors.map((error, index) => (
                        <p key={index} className="text-sm text-red-700">{error}</p>
                      ))}
                    </div>
                  </div>
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
                  value={formData.email}
                  disabled
                  className="w-full px-4 py-2 border border-gray-300 rounded-lg bg-gray-50 text-gray-600"
                />
                {validationErrors.email && (
                  <p className="mt-1 text-sm text-red-600">{validationErrors.email}</p>
                )}
              </div>

              <div>
                <label htmlFor="newPassword" className="block text-sm font-medium text-gray-700 mb-1">
                  {t('newPassword')}
                </label>
                <div className="relative">
                  <input
                    id="newPassword"
                    name="newPassword"
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="new-password"
                    required
                    value={formData.newPassword}
                    onChange={(e) => setFormData({ ...formData, newPassword: e.target.value })}
                    className={`w-full px-4 py-2 pr-10 border rounded-lg focus:ring-2 focus:ring-primary focus:border-transparent ${validationErrors.newPassword ? 'border-red-300' : 'border-gray-300'}`}
                    placeholder={t('newPasswordPlaceholder')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-gray-600"
                  >
                    {showPassword ? (
                      <EyeSlashIcon className="w-5 h-5" />
                    ) : (
                      <EyeIcon className="w-5 h-5" />
                    )}
                  </button>
                </div>
                {validationErrors.newPassword && (
                  <p className="mt-1 text-sm text-red-600">{validationErrors.newPassword}</p>
                )}

                {/* Password requirements checklist */}
                <div className="mt-3 space-y-1">
                  <p className="text-xs font-medium text-gray-600">{t('passwordRequirementsTitle')}</p>
                  <ul className="text-xs space-y-1">
                    <li className={passwordChecks.minLength ? 'text-green-600' : 'text-gray-500'}>
                      {passwordChecks.minLength ? '✓' : '○'} {t('passwordRequirements.minLength')}
                    </li>
                    <li className={passwordChecks.hasUppercase ? 'text-green-600' : 'text-gray-500'}>
                      {passwordChecks.hasUppercase ? '✓' : '○'} {t('passwordRequirements.uppercase')}
                    </li>
                    <li className={passwordChecks.hasLowercase ? 'text-green-600' : 'text-gray-500'}>
                      {passwordChecks.hasLowercase ? '✓' : '○'} {t('passwordRequirements.lowercase')}
                    </li>
                    <li className={passwordChecks.hasNumber ? 'text-green-600' : 'text-gray-500'}>
                      {passwordChecks.hasNumber ? '✓' : '○'} {t('passwordRequirements.number')}
                    </li>
                    <li className={passwordChecks.hasSpecial ? 'text-green-600' : 'text-gray-500'}>
                      {passwordChecks.hasSpecial ? '✓' : '○'} {t('passwordRequirements.special')}
                    </li>
                  </ul>
                </div>
              </div>

              <div>
                <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-700 mb-1">
                  {t('confirmPassword')}
                </label>
                <div className="relative">
                  <input
                    id="confirmPassword"
                    name="confirmPassword"
                    type={showConfirmPassword ? 'text' : 'password'}
                    autoComplete="new-password"
                    required
                    value={formData.confirmPassword}
                    onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                    className={`w-full px-4 py-2 pr-10 border rounded-lg focus:ring-2 focus:ring-primary focus:border-transparent ${validationErrors.confirmPassword ? 'border-red-300' : 'border-gray-300'}`}
                    placeholder={t('confirmPasswordPlaceholder')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                    className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-gray-600"
                  >
                    {showConfirmPassword ? (
                      <EyeSlashIcon className="w-5 h-5" />
                    ) : (
                      <EyeIcon className="w-5 h-5" />
                    )}
                  </button>
                </div>
                {validationErrors.confirmPassword && (
                  <p className="mt-1 text-sm text-red-600">{validationErrors.confirmPassword}</p>
                )}
                {formData.confirmPassword && (
                  <p className={`mt-1 text-xs ${passwordsMatch ? 'text-green-600' : 'text-red-600'}`}>
                    {passwordsMatch ? t('passwordsMatch') : t('passwordsDoNotMatch')}
                  </p>
                )}
              </div>

              <button
                type="submit"
                disabled={isLoading || !allPasswordChecksPassing || !passwordsMatch}
                className="w-full px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center"
              >
                {isLoading ? (
                  <>
                    <svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    {t('resettingPassword')}
                  </>
                ) : (
                  t('resetPassword')
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

export default function ResetPasswordPage() {
  const tCommon = useTranslations('common');
  return (
    <Suspense fallback={
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
          <p className="mt-4 text-gray-600">{tCommon('loading')}</p>
        </div>
      </div>
    }>
      <ResetPasswordForm />
    </Suspense>
  );
}
