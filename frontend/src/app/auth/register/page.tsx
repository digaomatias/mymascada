'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/contexts/auth-context';
import { RegisterRequest } from '@/types/auth';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { CurrencyDollarIcon, EnvelopeIcon, CheckCircleIcon } from '@heroicons/react/24/outline';
import { GoogleSignInButton } from '@/components/auth/google-signin-button';
import { useFeatures } from '@/contexts/features-context';
import { useTranslations } from 'next-intl';

export default function RegisterPage() {
  const router = useRouter();
  const { register, isLoading } = useAuth();
  const { features } = useFeatures();
  const t = useTranslations('auth');
  const tVerify = useTranslations('auth.emailVerification');
  const tCommon = useTranslations('common');
  const [verificationSent, setVerificationSent] = useState(false);
  const [registeredEmail, setRegisteredEmail] = useState('');
  const [formData, setFormData] = useState<RegisterRequest>({
    email: '',
    userName: '', // Will be set to email
    password: '',
    confirmPassword: '',
    firstName: '',
    lastName: '',
    phoneNumber: '',
    currency: 'NZD', // Default to New Zealand Dollar
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
    inviteCode: '',
  });
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  const validatePassword = (password: string): string[] => {
    const errors: string[] = [];

    if (password.length < 8) {
      errors.push(t('errors.passwordMinLength'));
    }

    if (!/[A-Z]/.test(password)) {
      errors.push(t('errors.passwordUppercase'));
    }

    if (!/[a-z]/.test(password)) {
      errors.push(t('errors.passwordLowercase'));
    }

    if (!/\d/.test(password)) {
      errors.push(t('errors.passwordNumber'));
    }

    if (!/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) {
      errors.push(t('errors.passwordSpecial'));
    }

    // Check for common weak passwords
    const commonPasswords = [
      'password', 'Password', 'PASSWORD', '12345678', '123456789',
      'qwerty', 'abc123', 'letmein', 'welcome', 'monkey'
    ];

    if (commonPasswords.includes(password)) {
      errors.push(t('errors.passwordTooCommon'));
    }

    // Check if password contains personal information
    const personalInfo = [
      formData.firstName.toLowerCase(),
      formData.lastName.toLowerCase(),
      formData.email.split('@')[0].toLowerCase()
    ].filter(info => info.length > 0);

    for (const info of personalInfo) {
      if (password.toLowerCase().includes(info)) {
        errors.push(t('errors.passwordContainsPersonalInfo'));
        break;
      }
    }

    return errors;
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!formData.firstName.trim()) {
      newErrors.firstName = t('errors.firstNameRequired');
    }

    if (!formData.lastName.trim()) {
      newErrors.lastName = t('errors.lastNameRequired');
    }

    if (!formData.email.trim()) {
      newErrors.email = t('errors.emailRequired');
    } else if (!formData.email.match(/^[^\s@]+@[^\s@]+\.[^\s@]+$/)) {
      newErrors.email = t('errors.emailInvalid');
    }

    if (!formData.password) {
      newErrors.password = t('errors.passwordRequired');
    } else {
      const passwordErrors = validatePassword(formData.password);
      if (passwordErrors.length > 0) {
        newErrors.password = passwordErrors[0]; // Show first password error
      }
    }

    if (!formData.confirmPassword) {
      newErrors.confirmPassword = t('errors.passwordRequired');
    } else if (formData.password !== formData.confirmPassword) {
      newErrors.confirmPassword = t('errors.passwordsNotMatch');
    }

    if (!acceptedTerms) {
      newErrors.acceptTerms = t('acceptTermsRequired');
    }

    setValidationErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrors([]);

    if (!validateForm()) {
      return;
    }

    // Additional password strength validation for server submission
    const passwordErrors = validatePassword(formData.password);
    if (passwordErrors.length > 0) {
      setErrors(passwordErrors);
      return;
    }

    // Generate username from email (remove @ and domain)
    const usernameFromEmail = formData.email.split('@')[0];
    const registrationData = {
      ...formData,
      userName: usernameFromEmail,
    };

    const response = await register(registrationData);

    if (response.isSuccess) {
      if (response.requiresEmailVerification) {
        // Show verification email sent message
        setVerificationSent(true);
        setRegisteredEmail(formData.email);
      } else {
        // Direct login (e.g., for OAuth or if verification is disabled)
        router.push('/dashboard');
      }
    } else {
      setErrors(response.errors);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value,
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

  // Show verification email sent success screen
  if (verificationSent) {
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
              <div className="text-center">
                <div className="w-16 h-16 bg-gradient-to-br from-green-500 to-green-700 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                  <CheckCircleIcon className="w-8 h-8 text-white" />
                </div>
                <h2 className="text-2xl font-bold text-gray-900 mb-2">
                  {tVerify('registrationSuccess')}
                </h2>
                <p className="text-gray-600 mb-4">
                  {tVerify('checkEmailMessage')}
                </p>
                <div className="bg-gray-50 rounded-lg p-4 mb-6">
                  <div className="flex items-center justify-center gap-2 text-gray-700">
                    <EnvelopeIcon className="w-5 h-5" />
                    <span className="font-medium">{registeredEmail}</span>
                  </div>
                </div>
                <p className="text-sm text-gray-500 mb-6">
                  {tVerify('checkSpamFolder')}
                </p>
                <Link
                  href="/auth/login"
                  className="inline-flex items-center justify-center w-full px-4 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500"
                >
                  {tVerify('proceedToLogin')}
                </Link>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <div className="flex justify-center mb-6">
            <div className="w-20 h-20 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center">
              <CurrencyDollarIcon className="w-12 h-12 text-white" />
            </div>
          </div>
          <h2 className="text-h1 text-gray-900">
            {t('signUpTitle')}
          </h2>
          <p className="mt-2 text-gray-600">
            {t('signUpSubtitle')}
          </p>
        </div>

        <Card>
          <CardContent className="p-8">
            <form className="space-y-6" onSubmit={handleSubmit} noValidate>
              {errors.length > 0 && (
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

              <div className="grid grid-cols-2 gap-4">
                <Input
                  id="firstName"
                  name="firstName"
                  type="text"
                  value={formData.firstName}
                  onChange={handleInputChange}
                  label={t('firstName')}
                  placeholder={t('firstNamePlaceholder')}
                  error={!!validationErrors.firstName}
                  errorMessage={validationErrors.firstName}
                />
                <Input
                  id="lastName"
                  name="lastName"
                  type="text"
                  value={formData.lastName}
                  onChange={handleInputChange}
                  label={t('lastName')}
                  placeholder={t('lastNamePlaceholder')}
                  error={!!validationErrors.lastName}
                  errorMessage={validationErrors.lastName}
                />
              </div>

              <Input
                id="email"
                name="email"
                type="email"
                value={formData.email}
                onChange={handleInputChange}
                label={t('email')}
                placeholder={t('emailPlaceholder')}
                error={!!validationErrors.email}
                errorMessage={validationErrors.email}
              />

              <Input
                id="inviteCode"
                name="inviteCode"
                type="text"
                value={formData.inviteCode || ''}
                onChange={handleInputChange}
                label={t('inviteCode')}
                placeholder={t('inviteCodePlaceholder')}
              />

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

              <Input
                id="confirmPassword"
                name="confirmPassword"
                type="password"
                value={formData.confirmPassword}
                onChange={handleInputChange}
                label={t('confirmPassword')}
                placeholder={t('confirmPasswordPlaceholder')}
                error={!!validationErrors.confirmPassword}
                errorMessage={validationErrors.confirmPassword}
              />

              <div>
                <label className="flex items-start gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={acceptedTerms}
                    onChange={(e) => {
                      setAcceptedTerms(e.target.checked);
                      if (validationErrors.acceptTerms) {
                        setValidationErrors(prev => {
                          const newErrors = { ...prev };
                          delete newErrors.acceptTerms;
                          return newErrors;
                        });
                      }
                    }}
                    className="h-4 w-4 mt-0.5 text-primary border-gray-300 rounded-sm focus:ring-primary"
                  />
                  <span className="text-sm text-gray-600">
                    {t.rich('acceptTerms', {
                      terms: (chunks) => (
                        <Link href="/terms" className="font-medium text-primary hover:text-primary-600 underline" target="_blank">
                          {chunks}
                        </Link>
                      ),
                      privacy: (chunks) => (
                        <Link href="/privacy" className="font-medium text-primary hover:text-primary-600 underline" target="_blank">
                          {chunks}
                        </Link>
                      ),
                    })}
                  </span>
                </label>
                {validationErrors.acceptTerms && (
                  <p className="mt-1 text-sm text-danger-600">{validationErrors.acceptTerms}</p>
                )}
              </div>

              <Button
                type="submit"
                className="w-full"
                loading={isLoading}
                disabled={isLoading}
              >
                {isLoading ? tCommon('loading') : t('signUp')}
              </Button>

              {features.googleOAuth && (
                <>
                  <div className="relative">
                    <div className="absolute inset-0 flex items-center">
                      <div className="w-full border-t border-gray-300"></div>
                    </div>
                    <div className="relative flex justify-center text-sm">
                      <span className="px-2 bg-white text-gray-500">{t('orContinueWith')}</span>
                    </div>
                  </div>

                  <GoogleSignInButton
                    onError={(error) => setErrors([error])}
                  />
                </>
              )}

              <div className="text-center">
                <span className="text-sm text-gray-600">
                  {t('hasAccount')}{' '}
                  <Link href="/auth/login" className="font-medium text-primary hover:text-primary-600">
                    {t('signIn')}
                  </Link>
                </span>
              </div>
            </form>
          </CardContent>
        </Card>

        <div className="text-center text-xs text-gray-500">
          <Link href="/terms" className="hover:text-gray-700 underline">{t('termsLink')}</Link>
          {' · '}
          <Link href="/privacy" className="hover:text-gray-700 underline">{t('privacyLink')}</Link>
        </div>
      </div>
    </div>
  );
}
