'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import AccountForm, { Account } from '@/components/forms/account-form';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import {
  ArrowLeftIcon,
  BuildingOffice2Icon,
  CheckIcon
} from '@heroicons/react/24/outline';

export default function NewAccountPage() {
  const { isAuthenticated, isLoading, user } = useAuth();
  const router = useRouter();
  const t = useTranslations('accounts');
  const tCommon = useTranslations('common');
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const handleSubmit = async (data: Omit<Account, 'id'>) => {
    setLoading(true);
    try {
      await apiClient.createAccount(data);
      
      setSuccess(true);
      
      // Redirect after a brief success message
      setTimeout(() => {
        router.push('/accounts');
      }, 1500);
    } catch (error) {
      console.error('Failed to create account:', error);
      throw error; // Let the form handle the error display
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    router.push('/accounts');
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingOffice2Icon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  if (success) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <Card className="mx-4 max-w-md w-full bg-white/90 backdrop-blur-xs border-0 shadow-2xl">
          <CardContent className="p-8 text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-success-500 to-success-600 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <CheckIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('accountCreated')}</h2>
            <p className="text-gray-600 mb-6">{t('accountCreatedDesc')}</p>
            <div className="text-sm text-gray-500">{t('redirectingToAccounts')}</div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <AppLayout>
      {/* Header */}
      <div className="mb-6 lg:mb-8">
        {/* Navigation Bar */}
        <div className="flex items-center justify-between mb-6">
          <Link href="/accounts">
            <Button variant="secondary" size="sm" className="flex items-center gap-2">
              <ArrowLeftIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{t('backToAccounts')}</span>
              <span className="sm:hidden">{t('back')}</span>
            </Button>
          </Link>
        </div>

        {/* Page Title */}
        <div className="text-center mb-8">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
            {t('createNewAccount')}
          </h1>
          <p className="text-gray-600 text-sm sm:text-base">
            {t('createNewAccountDesc')}
          </p>
        </div>
      </div>

      {/* Account Form */}
      <div className="max-w-2xl mx-auto">
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <BuildingOffice2Icon className="w-6 h-6 text-primary-600" />
              {t('accountDetails')}
            </CardTitle>
          </CardHeader>

          <CardContent>
            <AccountForm
              variant="full"
              initialData={{ currency: user?.currency || 'NZD' }}
              onSubmit={handleSubmit}
              onCancel={handleCancel}
              loading={loading}
              submitText={t('createAccount')}
              showCancel={true}
            />
          </CardContent>
        </Card>
      </div>
    </AppLayout>
  );
}