'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
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
          <div className="mt-6 text-slate-700 font-medium">{tCommon('loading')}</div>
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
        <div className="mx-4 max-w-md w-full rounded-[26px] border border-violet-100/70 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs bg-white/92 p-8 text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-success-500 to-success-600 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
            <CheckIcon className="w-8 h-8 text-white" />
          </div>
          <h2 className="text-2xl font-semibold text-slate-900 mb-2">{t('accountCreated')}</h2>
          <p className="text-slate-600 mb-6">{t('accountCreatedDesc')}</p>
          <div className="text-sm text-slate-500">{t('redirectingToAccounts')}</div>
        </div>
      </div>
    );
  }

  return (
    <AppLayout>
      {/* Header */}
      <header className="flex flex-wrap items-center justify-between gap-4 mb-5">
        <Link href="/accounts">
          <Button variant="secondary" size="sm" className="flex items-center gap-2">
            <ArrowLeftIcon className="w-4 h-4" />
            <span className="hidden sm:inline">{t('backToAccounts')}</span>
            <span className="sm:hidden">{t('back')}</span>
          </Button>
        </Link>
      </header>

      {/* Page Title */}
      <div className="mb-6">
        <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
          {t('createNewAccount')}
        </h1>
        <p className="mt-1.5 text-[15px] text-slate-500">
          {t('createNewAccountDesc')}
        </p>
      </div>

      {/* Account Form */}
      <div className="max-w-2xl mx-auto">
        <div className="rounded-[26px] border border-violet-100/70 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs bg-white/92 p-6">
          <AccountForm
            variant="full"
            initialData={{ currency: user?.currency || 'NZD' }}
            onSubmit={handleSubmit}
            onCancel={handleCancel}
            loading={loading}
            submitText={t('createAccount')}
            showCancel={true}
          />
        </div>
      </div>
    </AppLayout>
  );
}
